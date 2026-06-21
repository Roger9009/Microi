using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Microi.License
{
    /// <summary>
    /// 跨平台硬件指纹采集工具
    /// 
    /// HID 生成规则：
    ///   1. 优先读取环境变量 MICROI_MACHINE_ID（Docker/容器部署时可固定HID）
    ///   2. Linux：读取 /etc/machine-id 或 /var/lib/dbus/machine-id
    ///   3. Windows：读取注册表 HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid
    ///   4. 拼接首个有效网卡的 MAC 地址
    ///   5. SHA256(machineId + ":" + mac) → 64位大写十六进制字符串
    /// </summary>
    public static class HardwareHelper
    {
        private static string _cachedHid = null;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取当前服务器的硬件指纹ID（线程安全，结果缓存）
        /// </summary>
        public static string GetHardwareId()
        {
            if (_cachedHid != null) return _cachedHid;
            lock (_lock)
            {
                if (_cachedHid != null) return _cachedHid;
                _cachedHid = ComputeHardwareId();
                return _cachedHid;
            }
        }

        /// <summary>
        /// 获取诊断信息（调试用，包含各原始值）
        /// </summary>
        public static object GetDiagnosticInfo()
        {
            var envOverride = Environment.GetEnvironmentVariable("MICROI_MACHINE_ID");
            var machineId = GetMachineId();
            var mac = GetPrimaryMac();
            return new
            {
                HID = GetHardwareId(),
                EnvOverride = string.IsNullOrWhiteSpace(envOverride) ? "(未设置)" : envOverride,
                MachineId = machineId,
                PrimaryMac = mac,
                OS = RuntimeInformation.OSDescription,
                IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                DockerHint = Environment.GetEnvironmentVariable("MICROI_MACHINE_ID") != null
                    ? "已使用 MICROI_MACHINE_ID 环境变量固定HID（容器部署推荐）"
                    : "未使用环境变量固定HID，容器部署时建议设置 MICROI_MACHINE_ID"
            };
        }

        // ─────────────────────────────────────────────
        // 内部实现
        // ─────────────────────────────────────────────

        private static string ComputeHardwareId()
        {
            // 1. 容器/Docker 环境：优先使用固定的机器ID
            var envId = Environment.GetEnvironmentVariable("MICROI_MACHINE_ID");
            if (!string.IsNullOrWhiteSpace(envId))
            {
                return Sha256Hex(envId.Trim());
            }

            // 2. 获取机器唯一ID + 网卡MAC
            var machineId = GetMachineId();
            var mac = GetPrimaryMac();
            var raw = $"{machineId}:{mac}";
            return Sha256Hex(raw);
        }

        private static string GetMachineId()
        {
            // Linux 优先
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            var id = File.ReadAllText(path).Trim();
                            if (!string.IsNullOrWhiteSpace(id))
                                return id;
                        }
                    }
                    catch { }
                }
            }

            // Windows 注册表
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var guid = ReadWindowsMachineGuid();
                    if (!string.IsNullOrWhiteSpace(guid))
                        return guid;
                }
                catch { }
            }

            // macOS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    var result = RunCommand("ioreg", "-rd1 -c IOPlatformExpertDevice");
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(
                            result, @"IOPlatformUUID.*?=.*?""(.+?)""");
                        if (match.Success) return match.Groups[1].Value;
                    }
                }
                catch { }
            }

            // 最终兜底：使用主机名 + 环境组合（稳定性较低）
            return Environment.MachineName + "_" + (Environment.ProcessorCount);
        }

        private static string GetPrimaryMac()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                             && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                             && n.OperationalStatus == OperationalStatus.Up)
                    .OrderBy(n => n.Name)
                    .ToList();

                if (nics.Count > 0)
                {
                    var mac = nics[0].GetPhysicalAddress()?.ToString();
                    if (!string.IsNullOrWhiteSpace(mac) && mac != "000000000000")
                        return mac;
                }

                // 兜底：取任意可用网卡
                var anyNic = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .FirstOrDefault();
                return anyNic?.GetPhysicalAddress()?.ToString() ?? "NOMAC";
            }
            catch
            {
                return "NOMAC";
            }
        }

        private static string ReadWindowsMachineGuid()
        {
#if NETSTANDARD2_1
            // netstandard2.1 中通过反射访问注册表（仅在 Windows 上运行时有效）
            try
            {
                var winRegType = Type.GetType("Microsoft.Win32.Registry, System.Private.Registry");
                if (winRegType != null)
                {
                    var localMachine = winRegType.GetProperty("LocalMachine")?.GetValue(null);
                    if (localMachine != null)
                    {
                        var openSubKey = localMachine.GetType().GetMethod("OpenSubKey", new[] { typeof(string) });
                        var key = openSubKey?.Invoke(localMachine, new object[] { @"SOFTWARE\Microsoft\Cryptography" });
                        if (key != null)
                        {
                            var getValue = key.GetType().GetMethod("GetValue", new[] { typeof(string) });
                            var guid = getValue?.Invoke(key, new object[] { "MachineGuid" })?.ToString();
                            return guid ?? "";
                        }
                    }
                }
            }
            catch { }
            return "";
#else
            return "";
#endif
        }

        private static string RunCommand(string command, string args)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(command, args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                var output = proc?.StandardOutput.ReadToEnd();
                proc?.WaitForExit(3000);
                return output ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
        }
    }
}

using System;
using System.Collections.Generic;

namespace Microi.net.Business
{
    /// <summary>
    /// 插件运行期描述符，记录插件实例、生命周期阶段和运行日志。
    /// 轻量 POCO，日志缓冲上限 200 条（环形覆盖），防止内存泄漏。
    /// </summary>
    public sealed class BusinessPluginDescriptor
    {
        public BusinessPluginDescriptor(IBusinessPlugin plugin, string dllPath = null)
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            DllPath = dllPath ?? "(内置)";
            Stage = PluginLifecycleStage.Discovered;
            AppendLog($"[发现] 插件 [{Key}] 已扫描。");
        }

        /// <summary>插件实例（单例）</summary>
        public IBusinessPlugin Plugin { get; }

        /// <summary>插件 DLL 路径（内置插件显示 "(内置)"）</summary>
        public string DllPath { get; }

        /// <summary>插件 Key</summary>
        public string Key => Plugin.Key;

        /// <summary>当前生命周期阶段</summary>
        public PluginLifecycleStage Stage { get; set; }

        /// <summary>启动失败时的异常信息</summary>
        public string Error { get; set; }

        /// <summary>最近一次阶段变更时间</summary>
        public DateTime StageChangedTime { get; set; } = DateTime.Now;

        /// <summary>是否已成功启动并正在运行</summary>
        public bool IsRunning => Stage == PluginLifecycleStage.Started;

        /// <summary>是否已停止（允许重新启动）</summary>
        public bool IsStopped => Stage == PluginLifecycleStage.Stopped;

        /// <summary>是否为故障状态</summary>
        public bool IsFaulted => Stage == PluginLifecycleStage.Faulted;

        #region 日志缓冲

        private readonly List<string> _logs = new List<string>();
        private readonly object _logLock = new object();
        private const int MaxLogEntries = 200;

        /// <summary>获取插件日志快照（线程安全）</summary>
        public IReadOnlyList<string> GetLogs()
        {
            lock (_logLock) return _logs.ToArray();
        }

        /// <summary>追加一条插件日志（环形缓冲，超过上限移除最早的）</summary>
        public void AppendLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            lock (_logLock)
            {
                if (_logs.Count >= MaxLogEntries)
                    _logs.RemoveAt(0);
                _logs.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
            }
        }

        /// <summary>清空日志缓冲</summary>
        public void ClearLogs()
        {
            lock (_logLock) _logs.Clear();
        }

        #endregion
    }
}

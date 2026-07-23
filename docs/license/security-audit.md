# 本地附加授权安全审计报告

> **审计日期**：2026-07-17 | **审计范围**：`Microi.LocalLicense/` + `LocalLicenseController.cs`
>
> 本报告不审计框架 `/api/License/*`，也不允许本地授权访问或修改框架主库 `diy_license`。

---

## 漏洞分级说明

| 级别 | 颜色 | 定义 |
|------|:----:|------|
| **🔴 严重** | 红 | 可导致系统被完全绕过或授权信息泄露 |
| **🟠 高危** | 橙 | 可导致授权验证被绕过或功能门控失效 |
| **🟡 中危** | 黄 | 限制条件下可被利用，或攻击面较小 |
| **🟢 低危** | 蓝 | 信息泄露或理论攻击向量 |

---

## 漏洞详情

### 🔴 V-001：DeriveDbKey() 硬编码回退密钥（已修复）

**发现**：`DeriveDbKey()` 曾在本地授权加密密钥未设置时回退到硬编码字符串。任何知道此字符串的人均可解密独立授权库 `diy_local_license` 表中的加密数据。

**影响**：数据库中的 `LicenseContent`（AES 加密的 License 载荷）和宽限期证明记录可被解密。

**修复**（2026-07-04）：去掉了硬编码回退。未设置环境变量时抛出 `InvalidOperationException` 并显示配置指引。

**验证方法**：
```bash
# 设置环境变量后启动
export MICROI_LOCAL_LICENSE_ENCRYPT_KEY="your-32-char-random-string"
```

---

### 🟠 V-002：心跳文件 `.local_lic_hb` 明文存储（已修复）

**发现**：`.local_lic_hb` 文件曾以明文存储心跳状态和吊销信息。拥有本地文件系统访问权限的攻击者可：
- 修改时间戳重置离线天数计数器
- 移除 `Revoked` 标记绕过吊销检测

**影响**：结合 12 小时间隔，最多可延长 12 小时无吊销检测窗口。

**修复**（2026-07-04）：采用 `AesEncrypt(DeriveFileKey(hid))` 对心跳文件进行 AES 加密存储。解密密钥绑定 HID，攻击者同时需要有文件系统和 HID 信息才能解密。

---

### 🟡 V-003：宽限期文件 `.local_lic_grace` 缺少完整性保护（已修复）

**发现**：`.local_lic_grace` 文件曾仅存储 `timestamp|reason` 纯文本，可被任意修改以重置宽限期。

**影响**：配合删除宽限期 DB 记录，可无限获得 7 天宽限期。

**修复**（2026-07-04）：写入时追加 `HMAC-SHA256` 签名，读取时验证签名。无效签名的文件被忽略。

---

### 🟡 V-004：匿名 LocalLicense API 缺少限流（已修复）

**发现**：`GetHardwareId`、`Verify`、`GetConfig`、`GetStatus`、`WriteLicenseFile`、`Check`、`QueryApplication` 等匿名端点无任何频率限制。

**影响**：攻击者可高频调用 `Verify` 进行枚举攻击，或高频写文件产生 IO 压力。

**修复**（2026-07-04）：添加基于 IP 的内存限流器，每 IP 每分钟最多 10 次匿名请求。

---

### 🟡 V-005：心跳响应使用 `dynamic` 反序列化（已修复）

**发现**：`HeartbeatAsync()` 中 `JsonConvert.DeserializeObject<dynamic>(body)` 接受任意 JSON 结构。

**影响**：若心跳服务器被攻陷或响应被篡改，可能因异常数据结构导致未捕获的异常。

**修复**（2026-07-04）：改用 `Dictionary<string, object>` + `TryGetValue`，包含错误恢复逻辑。

---

### 🟢 V-006：离线注册文件缺少非重放保护（未修复）

**发现**：`GenerateRegistrationPackage()` 生成的注册文件哈希仅包含 `HID|company|name|phone|type|time`，不包含服务器端随机数（nonce）。

**影响**：同一 HID 生成的两个注册文件，若在 60 秒内上传，可能被判定为重放（已有 IP 限流部分缓解）。

**状态**：接受风险 — 已有 `ImportRegistrationFile` 的 IP+HID 双维度 60 秒限流。

---

### 🟢 V-007：功能门控仅检查有效性不检查吊销状态（未修复）

**发现**：`IsFeatureAllowed()` 调用 `_cachedVerify ?? Verify()` 检查 License 有效性，但不检查 `_revokedByServer`。

**影响**：若心跳检测到吊销但未重启，功能门控仍返回 `true`。

**状态**：接受风险 — 吊销后下次重启会拒绝启动。实时吊销需通过心跳间隔（最大 12 小时）生效。

---

## 安全措施总结

| 防护层 | 措施 | 状态 |
|--------|------|:----:|
| 传输加密 | RSA-2048 签名 + AES-256 加密 | ✅ |
| 密钥安全 | 私钥仅环境变量，不落代码 | ✅ |
| 防篡改 | 签名验证 + HMAC 完整性 | ✅ |
| 频控 | 匿名 API 限流 + 注册文件 IP+HID 限流 | ✅ |
| 抗重放 | 注册文件限流 + 心跳签名 | 部分 |
| 日志审计 | 所有签发/审核/吊销操作记录 `diy_local_license_log` | ✅ |

## 配置文件检查清单

```bash
# 必须设置的环境变量
export MICROI_LOCAL_LICENSE_ENCRYPT_KEY="your-32-char-random-string"  # 数据库加密密钥
export MICROI_LOCAL_LICENSE_PUBLIC_KEY="MIIBIjANBgkqhkiG9w0B..."      # RSA 公钥

# 本地附加授权中心额外设置
export MICROI_LOCAL_LICENSE_PRIVATE_KEY="MIIEvQIBADANBgkqhkiG9w0B..." # RSA 私钥

# Docker 部署推荐
export MICROI_MACHINE_ID="your-fixed-machine-id"                 # 固定 HID
```

## 兼容迁移审计边界

旧 `License*`、`MICROI_LICENSE_*`、`license.json`、`license-*.pem`、`.lic_*` 和 `diy_license*` 仅允许作为安全兼容迁移输入。旧表只能从 `LocalLicenseDbConn` 指向的独立授权库读取并复制到 `diy_local_license*`；不得更新或删除旧表，更不得连接、读取或修改框架主库 `diy_license`。

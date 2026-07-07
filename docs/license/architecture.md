# License 架构概览

> 文档版本：2026-07-04 | 适用范围：全部角色

---

## 分层设计

```
Microi.License/                          ← 【核心库】独立项目（netstandard2.1）
├── LicenseService.cs      完整逻辑：验证/签发/宽限期/心跳/AES加密/离线申请
├── LicenseModel.cs        数据模型：LicensePayload, LicenseVerifyResult
└── HardwareHelper.cs      跨平台 HID 采集

Microi.net.Api/Handler/License/
├── LicenseServiceBridge.cs  【桥接层】extern alias 委托给 Microi.License
└── LicenseBackgroundService.cs   后台心跳服务

Microi.net.Api/
├── Program.cs              启动 License 验证
└── Controllers/LicenseController.cs  REST API
```

## 版本模式

| 模式 | 条件 | License 校验 | 功能限制 |
|------|------|:---:|---------|
| **开源版** | `DefaultPublicKeyBase64` 未替换 | ❌ 跳过 | 仅在线 AI 受限 |
| **个人版** | 公钥已配置 + 有效 `license.json` (Personal) | ✅ | 高级报表、自定义域名 |
| **企业版** | 公钥已配置 + 有效 `license.json` (Enterprise) | ✅ | AI 插件、多租户、全功能 |

## 三层防御机制

```
第一层：RSA 离线验证（本地 SHA256withRSA 签名校验）
  • 公钥内嵌于 DLL，无网络依赖
  • HID 硬件绑定，换机器自动失效
  • 签名不可伪造（需 RSA 私钥）

第二层：在线心跳验证（每 12 小时后台服务）
  • 向官方服务器发送心跳检测吊销状态
  • 服务端吊销后最多 12 小时内生效
  • 离线环境允许继续运行（30 天内零告警）

第三层：宽限期机制（文件 + DB 双重防篡改）
  • License 缺失时提供 7 天宽限
  • 首次部署自动授予 7 天初始宽限期
  • 删除 .lic_grace 文件无效（DB 保留最早时间）
  • 还原旧数据库无效（ValidProof HMAC 含 HID 绑定）
```

## 桥接模式 + extern alias

`Microi.net` NuGet 包内含 `LicenseService` 类型，与 `Microi.License` 项目同名类型冲突。通过 `extern alias` 解决：

```xml
<!-- Microi.net.Api.csproj -->
<ProjectReference Include="../Microi.License/Microi.License.csproj">
  <Aliases>MicroiLicense</Aliases>
</ProjectReference>
```

```csharp
// LicenseServiceBridge.cs
extern alias MicroiLicense;
using LicSvc = MicroiLicense::Microi.License.LicenseService;
```

API 项目代码无需修改，`LicenseService` 通过桥接类透明解析。

## 关键流程

```
启动流程：
  Program.cs
    ├─ IsOpenSourceMode() → true → 直接启动
    └─ false → LicenseService.Verify()
         ├─ Valid → 启动 ✅
         └─ Invalid → CheckGracePeriod()
              ├─ 有宽限期 → 启动（宽限模式）⚠️
              └─ 无宽限期 → Environment.Exit(1) ❌

申请流程：
  客户 → 前端提交申请 → License服务器 → 管理员审核 → 签发 → 客户部署

心跳流程：
  LicenseBackgroundService（每 12h）
    └─ HeartbeatAsync() → 检测吊销 → 检查离线天数

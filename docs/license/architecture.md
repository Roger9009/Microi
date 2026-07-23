# 本地附加授权架构概览

> 文档版本：2026-07-17 | 适用范围：项目自定义 `Microi.LocalLicense`
>
> 本架构与 Microi 框架授权隔离。框架 `/api/License/*` 和框架主库 `diy_license` 保持原样，不属于本地附加授权。

---

## 分层设计

```
Microi.LocalLicense/                          ← 【核心库】独立项目（netstandard2.1）
├── LocalLicenseService.cs      验证/签发/宽限期/心跳/AES加密/离线申请
├── LocalLicenseModel.cs        LocalLicensePayload, LocalLicenseVerifyResult
├── LocalLicenseHardwareHelper.cs
└── LocalLicenseDatabaseInitializer.cs       独立授权库建表和兼容迁移

Microi.net.Api/Handler/LocalLicense/
├── LocalLicenseServiceFacade.cs  【桥接层】extern alias 委托给 Microi.LocalLicense
└── LocalLicenseBackgroundService.cs   后台心跳服务

Microi.net.Api/
├── Program.cs              启动本地附加授权验证
└── Controllers/LocalLicenseController.cs  REST API（/api/LocalLicense/*）
```

## 版本模式

| 模式 | 条件 | License 校验 | 功能限制 |
|------|------|:---:|---------|
| **开源版** | `DefaultPublicKeyBase64` 未替换 | ❌ 跳过 | 仅在线 AI 受限 |
| **个人版** | 公钥已配置 + 有效 `local-license.json` (Personal) | ✅ | 高级报表、自定义域名 |
| **企业版** | 公钥已配置 + 有效 `local-license.json` (Enterprise) | ✅ | AI 插件、多租户、全功能 |

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
  • 删除 .local_lic_grace 文件无效（DB 保留最早时间）
  • 还原旧数据库无效（ValidProof HMAC 含 HID 绑定）
```

## 桥接模式 + extern alias

`Microi.net` NuGet 包内含框架 `LicenseService` 类型；本地附加授权通过 `extern alias` 显式引用自己的服务，避免命名冲突：

```xml
<!-- Microi.net.Api.csproj -->
<ProjectReference Include="../Microi.LocalLicense/Microi.LocalLicense.csproj">
  <Aliases>MicroiLocalLicense</Aliases>
</ProjectReference>
```

```csharp
// LocalLicenseServiceFacade.cs
extern alias MicroiLocalLicense;
using LicSvc = MicroiLocalLicense::Microi.LocalLicense.LocalLicenseService;
```

API 通过 `LocalLicenseServiceFacade` 访问本地授权，不接管框架 `LicenseService`。

## 关键流程

```
启动流程：
  Program.cs
    ├─ IsOpenSourceMode() → true → 直接启动
    └─ false → LocalLicenseServiceFacade.Verify()
         ├─ Valid → 启动 ✅
         └─ Invalid → CheckGracePeriod()
              ├─ 有宽限期 → 启动（宽限模式）⚠️
              └─ 无宽限期 → Environment.Exit(1) ❌

申请流程：
  客户 → 前端提交申请 → 本地授权中心 → 管理员审核 → 签发 → 客户部署

心跳流程：
  LocalLicenseBackgroundService（每 12h）
    └─ HeartbeatAsync() → 检测吊销 → 检查离线天数
```

## 数据隔离与迁移

本地授权中心只连接 `LocalLicenseDbConn` 指定的独立库，并使用 `diy_local_license`、`diy_local_license_log`。旧 `diy_license`、`diy_license_log` 仅可在该独立库中作为兼容迁移源，迁移行为是只复制缺失记录，不修改旧表。框架主库的 `diy_license` 永远不是迁移源。

旧 `License*`、`MICROI_LICENSE_*`、`license.json`、`license-*.pem`、`.lic_*` 仅是兼容读取回退；现行名称为 `LocalLicense*`、`MICROI_LOCAL_LICENSE_*`、`local-license.json`、`local-license-*.pem`、`.local_lic_*`。

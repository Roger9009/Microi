# 本地附加授权开发指南

> **适用读者**：开发者 | **涉及文件**：后端 `Microi.LocalLicense/` + 前端 `views/system/local-license.vue`
>
> 本文只涉及自定义本地附加授权，不得改造或替代框架 `LicenseController`、`/api/License/*` 和框架主库 `diy_license`。

---

## 项目结构

```
Microi.Server/
├── Microi.LocalLicense/                    ← 本地附加授权库（netstandard2.1）
│   ├── LocalLicenseService.cs              ← 全部授权逻辑
│   ├── LocalLicenseModel.cs                ← 数据模型
│   ├── LocalLicenseHardwareHelper.cs       ← HID 采集
│   └── LocalLicenseDatabaseInitializer.cs  ← 独立授权库建表/迁移
├── Microi.net.Api/
│   ├── Controllers/LocalLicenseController.cs
│   └── Handler/LocalLicense/
│       ├── LocalLicenseServiceFacade.cs
│       └── LocalLicenseBackgroundService.cs

Microi.Client/
└── src/
    ├── utils/business-base.js              ← LocalLicenseApi
    └── views/system/
        ├── local-license.vue               ← /local-license
        └── LocalLicenseAdminConsole.vue    ← /local-license-admin
```

## 扩展本地授权功能

### 添加新的产品特性

在 `LocalLicenseService.cs` 的 `Features` 类中添加常量：

```csharp
public static class Features
{
    public const string AiPlugin = "ai_plugin";
    public const string MultiTenant = "multi_tenant";
    public const string AdvancedReport = "advanced_report";
    public const string CustomDomain = "custom_domain";
    public const string LicenseAdmin = "license_admin";
    // 新增：
    public const string NewFeature = "new_feature";
}
```

然后在需要检查权限的地方：

```csharp
if (!LocalLicenseService.IsFeatureAllowed(LocalLicenseService.Features.NewFeature))
    return new DosResult(0, null, "当前 License 不包含此功能");
```

### 添加新的 API 端点

1. 在 `LocalLicenseController.cs` 中添加 Action 方法
2. 在 `business-base.js` 的 `LocalLicenseApi` 中添加对应方法
3. 在 `docs/license/api-reference.md` 中记录

### 前端扩展

`local-license.vue` 已内置完整的多 Tab 页面：
- Tab 1：提交授权申请（在线 + 离线）
- Tab 2：检查并部署 License
- Tab 3：手动导入授权文件
- Tab 4（管理员）：License 列表管理
- Tab 5（管理员）：操作日志

新增 Tab 只需在 `el-tabs` 中添加 `el-tab-pane`。

## 本地开发/调试

### 快速进入宽限期模式

在 `Program.cs` 中设置宽限期（无需真实 License）：

```csharp
LocalLicenseServiceFacade.SetGracePeriodMode(true);
```

### 生成测试密钥对

```
GET /api/LocalLicense/GenerateKeyPair   ← 需要超级管理员权限
```

将输出的 `PublicKeyBase64` 临时替换到常量，用于本地测试。

### 跳过 License 验证（开发环境）

```csharp
// Program.cs 中将 License 验证改为宽限期模式
if (builder.Environment.IsDevelopment())
{
    LocalLicenseServiceFacade.SetGracePeriodMode(true);
}
```

## 集成业务底座

本地附加授权信息可通过 `LocalLicenseServiceFacade` 获取：

```csharp
// 检查是否已授权
var verifyResult = LocalLicenseServiceFacade.Verify();
if (!verifyResult.Valid)
    return new DosResult(0, null, "请先完成 License 授权");

// 获取公司名称
var company = verifyResult.Company;

// 检查功能权限
if (!LocalLicenseServiceFacade.IsFeatureAllowed("multi_tenant"))
    return new DosResult(0, null, "多租户功能需要企业版 License");
```

## 独立数据与兼容迁移

- 现行表为独立授权库中的 `diy_local_license`、`diy_local_license_log`。
- `LocalLicenseDatabaseInitializer` 只在 `LocalLicenseDbConn` 指向的独立库中建表。
- 若该独立库中存在旧自定义授权表 `diy_license`、`diy_license_log`，初始化器只复制缺失数据到新表，不删除或更新旧表。
- 绝不把框架主库 `diy_license` 当作迁移源；框架升级类也不得修改它。
- 旧 `License*`、`MICROI_LICENSE_*`、`license.json`、`license-*.pem`、`.lic_*` 仅为兼容迁移回退，不是现行开发接口。

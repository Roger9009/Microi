# License 开发指南

> **适用读者**：开发者 | **涉及文件**：后端 `Microi.License/` + 前端 `views/system/license.vue`

---

## 项目结构

```
Microi.Server/
├── Microi.License/               ← 核心授权库（netstandard2.1，独立编译）
│   ├── LicenseService.cs         ← 全部授权逻辑
│   ├── LicenseModel.cs           ← 数据模型
│   └── HardwareHelper.cs         ← HID 采集
├── Microi.net.Api/
│   ├── Controllers/LicenseController.cs    ← REST API
│   └── Handler/License/
│       ├── LicenseServiceBridge.cs         ← 桥接层
│       └── LicenseBackgroundService.cs     ← 心跳

Microi.Client/
└── src/
    ├── utils/business-base.js    ← LicenseApi（前端 API 客户端）
    └── views/system/license.vue   ← 授权管理页面（1294 行）
```

## 扩展 License 功能

### 添加新的产品特性

在 `LicenseService.cs` 的 `Features` 类中添加常量：

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
if (!LicenseService.IsFeatureAllowed(LicenseService.Features.NewFeature))
    return new DosResult(0, null, "当前 License 不包含此功能");
```

### 添加新的 API 端点

1. 在 `LicenseController.cs` 中添加 Action 方法
2. 在 `business-base.js` 的 `LicenseApi` 中添加对应方法
3. 在 `docs/license/api-reference.md` 中记录

### 前端扩展

`license.vue`（1294 行）已内置完整的多 Tab 页面：
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
LicenseService.SetGracePeriodMode(true);
```

### 生成测试密钥对

```
GET /api/License/GenerateKeyPair   ← 需要超级管理员权限
```

将输出的 `PublicKeyBase64` 临时替换到常量，用于本地测试。

### 跳过 License 验证（开发环境）

```csharp
// Program.cs 中将 License 验证改为宽限期模式
if (builder.Environment.IsDevelopment())
{
    LicenseService.SetGracePeriodMode(true);
}
```

## 集成业务底座

License 信息可在业务模块中通过注入或静态访问获取：

```csharp
// 检查是否已授权
var verifyResult = LicenseService.Verify();
if (!verifyResult.Valid)
    return new DosResult(0, null, "请先完成 License 授权");

// 获取公司名称
var company = verifyResult.Company;

// 检查功能权限
if (!LicenseService.IsFeatureAllowed("multi_tenant"))
    return new DosResult(0, null, "多租户功能需要企业版 License");
```

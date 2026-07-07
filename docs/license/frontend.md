# License 前端集成指南

> **适用读者**：前端开发者

---

## API 客户端

`Microi.Client/src/utils/business-base.js` 提供 `LicenseApi` 对象：

```javascript
import { LicenseApi } from '@/utils/business-base';

// 获取 HID
const { Data: { HID } } = await LicenseApi.getHardwareId();

// 验证本地 License
const verifyResult = await LicenseApi.verify();
if (verifyResult.Data?.IsLicensed) {
  console.log('已授权:', verifyResult.Data.Company);
} else {
  console.log('未授权或宽限期模式');
}

// 写入 License 文件（部署）
const res = await LicenseApi.writeLicenseFile(licenseJsonString);
if (res.Code === 1) ElMessage.success('部署成功，请重启服务');
```

## 页面路由

License 管理页面路由已在 `asyncRoutes` 中注册：

```javascript
// router/index.js
{
  path: '/license',
  component: Layout,
  hidden: true,
  children: [{
    path: '/license',
    name: 'system_license',
    component: () => import('@/views/system/license.vue'),
    meta: { title: '授权管理' }
  }]
}
```

## 页面结构

`views/system/license.vue`（1294 行）包含 5 个 Tab：

| Tab | 功能 | 用户权限 |
|-----|------|---------|
| **提交授权申请** | 在线/离线申请 License | 匿名 |
| **检查并部署License** | 查询签发状态、自动/手动部署 | 匿名 |
| **手动导入授权文件** | 上传 .lic 文件或粘贴 JSON | 匿名 |
| **License 管理** | 列表查看、审核、签发、作废 | 超级管理员 |
| **操作日志** | 按 HID 筛选操作记录 | 超级管理员 |

## 在线申请流程（前端调用链）

```
用户填写表单
  → 获取验证码（fetch LICENSE_API_BASE + /api/License/GetCaptcha）
  → 查询已有申请（fetch LICENSE_API_BASE + /api/License/QueryApplication）
  → 提交申请（fetch LICENSE_API_BASE + /api/License/Apply）
  → 切换 Tab 到「检查并部署」
  → 检查状态（fetch LICENSE_API_BASE + /api/License/Check）
  → 部署（调用本地 /api/License/WriteLicenseFile）
```

## 特殊说明

- 申请流程调用的是 **License 服务器**（`LICENSE_API_BASE = "https://api.itdos.com"`），不是本地服务器
- 部署写文件操作调用的是**本地服务器**`/api/License/WriteLicenseFile`
- `GetHardwareId`、`Verify`、`Diagnostics` 是本地端点，**匿名可访问**
- `List`、`Logs`、`Issue`、`Revoke` 等管理端点需要**超级管理员**权限

## 本地开发测试

```javascript
// 快速测试 License 状态
const res = await LicenseApi.verify();
console.log('License:', res.Data);

// 获取运行状态摘要（轻量，含心跳/宽限期）
const status = await LicenseApi.getStatus();
console.log('状态摘要:', status.Data);

// 获取心跳状态
const hb = await LicenseApi.getHeartbeatStatus();
console.log('心跳:', hb.Data);

// 获取诊断信息
const diag = await LicenseApi.diagnostics();
console.log('诊断:', JSON.stringify(diag.Data, null, 2));
```

## 授权总控台

### 访问地址

```
路由：/#/license-admin
页面：Microi.Client/src/views/system/LicenseAdminConsole.vue
```

### 登录方式

授权总控台连接到 **License 服务器**（默认 `https://api.itdos.com`），使用平台的**超级管理员账号**登录鉴权。

| 项目 | 说明 |
|------|------|
| 认证方式 | 在平台主页面正常登录后，总控台自动携带登录态 |
| 所需权限 | `sys_user.Level` >= `DiyCommon.MaxRoleLevel`（超级管理员） |
| 适用场景 | 管理所有已授权客户的 License（签发/审核/驳回/作废/恢复） |

> 总控台所有 API 调用（Approve/Reject/Revoke/Issue/List/Logs）均在 License 服务器端验证管理员权限。普通用户无法操作。

### 首次使用流程

```
1. 登录平台（使用超级管理员账号）
2. 访问 /#/license-admin
3. 系统自动加载所有授权客户列表
4. 可执行：审核申请、直接签发、驳回、作废等操作
```

## 监控仪表盘集成

`BusinessMonitorDashboard.vue`（`views/business/`）已集成 License 状态卡片：

| 字段 | API 来源 |
|------|---------|
| 授权状态标签 | `LicenseApi.getStatus()` → `Data.IsLicensed / IsOpenSource / IsGracePeriod` |
| 授权公司 / 产品版本 | `Data.Company` / `Data.ProductType` |
| 剩余天数 / 到期时间 | `Data.DaysRemaining` / `Data.ExpirationDate` |
| 心跳状态 | `Data.IsRevokedByServer` / `Data.OfflineDays` |
| 宽限期剩余天数 | `Data.GraceDaysLeft` |

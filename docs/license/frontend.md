# 本地附加授权前端集成指南

> **适用读者**：前端开发者
>
> 本文页面和请求均属于自定义本地附加授权。框架 `/api/License/*` 及其页面保持原样，不属于本文范围。

---

## API 客户端

`Microi.Client/src/utils/business-base.js` 提供 `LocalLicenseApi` 对象：

```javascript
import { LocalLicenseApi } from '@/utils/business-base';

// 获取 HID
const { Data: { HID } } = await LocalLicenseApi.getHardwareId();

// 验证本地附加授权
const verifyResult = await LocalLicenseApi.verify();
if (verifyResult.Data?.IsLicensed) {
  console.log('已授权:', verifyResult.Data.Company);
} else {
  console.log('未授权或宽限期模式');
}

// 写入 local-license.json（部署）
const res = await LocalLicenseApi.writeLicenseFile(localLicenseJsonString);
if (res.Code === 1) ElMessage.success('部署成功，请重启服务');
```

## 页面路由

本地附加授权页面已在 `asyncRoutes` 中注册：

```javascript
// router/index.js
{
  path: '/local-license',
  component: Layout,
  hidden: true,
  children: [{
    path: '/local-license',
    name: 'system_local_license',
    component: () => import('@/views/system/local-license.vue'),
    meta: { title: '授权管理' }
  }]
}
```

## 页面结构

`views/system/local-license.vue` 是客户侧授权页面，包含 3 个 Tab：

| Tab | 功能 | 用户权限 |
|-----|------|---------|
| **提交授权申请** | 在线/离线申请本地附加授权 | 匿名 |
| **检查并部署本地授权** | 查询签发状态、自动/手动部署 | 匿名 |
| **手动导入授权文件** | 上传 `local-license.json` 或粘贴 JSON | 匿名 |

## 在线申请流程（前端调用链）

```
用户填写表单
  → 获取验证码（fetch LICENSE_API_BASE + /api/LocalLicense/GetCaptcha）
  → 查询已有申请（fetch LICENSE_API_BASE + /api/LocalLicense/QueryApplication）
  → 提交申请（fetch LICENSE_API_BASE + /api/LocalLicense/Apply）
  → 切换 Tab 到「检查并部署」
  → 检查状态（fetch LICENSE_API_BASE + /api/LocalLicense/Check）
  → 部署（调用本地 /api/LocalLicense/WriteLicenseFile）
```

在线申请只有这一条入口，直接写入授权中心独立数据库并立即出现在总控台待审核列表。
`.milic` 仅用于无法联网的离线场景，不再提供“生成后直接提交注册文件”的重复在线入口。

## 特殊说明

- 申请流程调用的是**本地附加授权中心**（`LICENSE_API_BASE = "https://api.itdos.com"`），不是客户本地服务器
- 部署写文件操作调用的是客户本地服务器 `/api/LocalLicense/WriteLicenseFile`
- `GetHardwareId`、`Verify`、`Diagnostics` 是本地端点，**匿名可访问**
- `List`、`Logs`、`Issue`、`Revoke` 等管理端点需要**超级管理员**权限

## 本地开发测试

```javascript
// 快速测试本地附加授权状态
const res = await LocalLicenseApi.verify();
console.log('License:', res.Data);

// 获取运行状态摘要（轻量，含心跳/宽限期）
const status = await LocalLicenseApi.getStatus();
console.log('状态摘要:', status.Data);

// 获取心跳状态
const hb = await LocalLicenseApi.getHeartbeatStatus();
console.log('心跳:', hb.Data);

// 获取诊断信息
const diag = await LocalLicenseApi.diagnostics();
console.log('诊断:', JSON.stringify(diag.Data, null, 2));
```

## 授权总控台

### 访问地址

```
路由：/#/local-license-admin
页面：Microi.Client/src/views/system/LocalLicenseAdminConsole.vue
```

### 登录方式

授权总控台连接到**本地附加授权中心**（默认 `https://api.itdos.com`），使用平台的**超级管理员账号**登录鉴权。

| 项目 | 说明 |
|------|------|
| 认证方式 | 在平台主页面正常登录后，总控台自动携带登录态 |
| 所需权限 | `sys_user.Level` >= `DiyCommon.MaxRoleLevel`（超级管理员） |
| 适用场景 | 管理所有客户的本地附加授权（签发/审核/驳回/作废/恢复） |

> 总控台所有 `/api/LocalLicense/*` 管理调用（Approve/Reject/Revoke/Issue/List/Logs）均在本地附加授权中心验证管理员权限。普通用户无法操作。

### 首次使用流程

```
1. 登录平台（使用超级管理员账号）
2. 访问 /#/local-license-admin
3. 系统自动加载所有授权客户列表
4. 可导入客户提交的 `.milic` 注册文件
5. 可执行：审核申请、直接签发、驳回、作废等操作
```

总控台请求会携带当前平台登录 Token；`ImportRegistrationFile` 仅允许超级管理员调用。

## 监控仪表盘集成

`BusinessMonitorDashboard.vue`（`views/business/`）已集成本地附加授权状态卡片：

| 字段 | API 来源 |
|------|---------|
| 授权状态标签 | `LocalLicenseApi.getStatus()` → `Data.IsLicensed / IsOpenSource / IsGracePeriod` |
| 授权公司 / 产品版本 | `Data.Company` / `Data.ProductType` |
| 剩余天数 / 到期时间 | `Data.DaysRemaining` / `Data.ExpirationDate` |
| 心跳状态 | `Data.IsRevokedByServer` / `Data.OfflineDays` |
| 宽限期剩余天数 | `Data.GraceDaysLeft` |

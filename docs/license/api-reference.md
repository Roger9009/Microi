# 本地附加授权 API 参考

> **适用读者**：开发者 | **基础路径**：`/api/LocalLicense/`
>
> 本文只描述 `LocalLicenseController`。框架 `/api/License/*` 保持原样，不属于本地附加授权，也不得按本文改名。

---

## 客户服务器端点（匿名或登录）

这些端点在**所有服务器**上可用，不需要私钥。

| 方法 | 端点 | 鉴权 | 说明 |
|------|------|------|------|
| GET | `/api/LocalLicense/GetHardwareId` | 匿名 | 获取当前服务器 HID |
| GET | `/api/LocalLicense/Verify` | 匿名 | 验证本地附加授权状态 |
| GET | `/api/LocalLicense/GetConfig` | 匿名 | 获取可配置项（ContactEmail、HeartbeatIntervalHours 等） |
| GET/POST | `/api/LocalLicense/GetStatus` | 匿名 | 获取运行状态摘要（含心跳/宽限期/吊销信息） |
| GET/POST | `/api/LocalLicense/GetHeartbeatStatus` | 匿名 | 获取心跳状态（不触发 Verify） |
| POST | `/api/LocalLicense/Heartbeat` | 匿名 | 授权中心接收客户心跳，返回 Ok/Revoked/Expired 等状态 |
| POST | `/api/LocalLicense/WriteLicenseFile` | 匿名 | 写入 `local-license.json` |
| POST | `/api/LocalLicense/GenerateRegistrationFile` | 匿名 | 生成离线注册申请包 |
| GET/POST | `/api/LocalLicense/Diagnostics` | 登录 | 获取完整诊断信息 |
| POST | `/api/LocalLicense/Check` | 匿名 | 查询某个 HID 的本地授权状态 |
| POST | `/api/LocalLicense/QueryApplication` | 匿名 | 查询申请状态 |

### GET /api/LocalLicense/GetHardwareId

```json
// Response
{ "Code": 1, "Data": { "HID": "A1B2C3D4E5F6..." } }
```

### GET /api/LocalLicense/Verify

```json
// Response
{
  "Code": 1,
  "Data": {
    "Valid": true,
    "IsLicensed": true,
    "HID": "A1B2C3D4E5F6...",
    "Company": "示例公司",
    "ProductType": "Enterprise",
    "ExpirationDate": "2027-06-01T00:00:00Z",
    "IssuedDate": "2026-06-01T00:00:00",
    "DaysRemaining": 365,
    "IsGracePeriod": false,
    "Message": "License有效，剩余 365 天"
  }
}
```

### POST /api/LocalLicense/WriteLicenseFile

```json
// Request
{ "LicenseContent": "{ \"HID\": \"...\", ... }" }

// Response
{ "Code": 1, "Msg": "License 部署成功！" }
```

写入前自动执行：AES 解密（若加密）→ JSON 解析 → RSA 签名验证 → HID 匹配检查 → 到期检查。

### POST /api/LocalLicense/GenerateRegistrationFile

```json
// Request
{
  "Company": "示例公司",
  "Name": "联系人",
  "Phone": "138xxxxxxxx",
  "ProductType": "Enterprise",
  "Remark": "可选备注"
}
```

## 本地授权中心端点（超级管理员）

这些端点在**本地授权中心（有私钥）**上可用，需要超级管理员权限。

| 方法 | 端点 | 说明 |
|------|------|------|
| POST | `/api/LocalLicense/Apply` | 提交授权申请（含验证码） |
| POST | `/api/LocalLicense/Issue` | 直接签发本地授权 |
| POST | `/api/LocalLicense/Approve` | 审批通过并签发 |
| POST | `/api/LocalLicense/Reject` | 驳回申请 |
| POST | `/api/LocalLicense/Revoke` | 吊销/恢复本地授权 |
| GET | `/api/LocalLicense/GenerateKeyPair` | 生成 RSA 密钥对 |
| GET | `/api/LocalLicense/List` | 本地授权列表（支持 ?status= 筛选） |
| GET | `/api/LocalLicense/Logs` | 操作日志（支持 ?hid= 筛选） |

### POST /api/LocalLicense/Issue

```json
// Request
{
  "HID": "A1B2C3D4E5F6...",
  "Company": "示例公司",
  "Name": "联系人",
  "Phone": "138xxxxxxxx",
  "ProductType": "Enterprise",
  "ExpirationDate": "2027-06-01T00:00:00Z"
}
```

### POST /api/LocalLicense/Revoke

```json
// Request
{
  "HID": "A1B2C3D4E5F6...",
  "Revoke": true
}
```

## 操作日志动作类型

| Action | 触发场景 |
|--------|---------|
| `Apply` | 提交授权申请 |
| `Issue` | 直接签发 License |
| `Approve` | 审核通过并签发 |
| `Reject` | 驳回申请 |
| `Revoke` | 作废 License |
| `Restore` | 恢复已作废的 License |
| `Deploy` | 部署 License 到本地服务器 |
| `ImportReg` | 导入离线注册文件 |

## 前端 API 客户端

在 `Microi.Client/src/utils/business-base.js` 中提供 `LocalLicenseApi`：

```javascript
import { LocalLicenseApi } from '@/utils/business-base';

// 本地端点
const hid = await LocalLicenseApi.getHardwareId();
const status = await LocalLicenseApi.verify();
const diag = await LocalLicenseApi.diagnostics();

// 本地附加授权中心端点
const result = await LocalLicenseApi.apply(hid, company, name, phone, ...);
const result = await LocalLicenseApi.issue(hid, company, ...);
const result = await LocalLicenseApi.approve(hid);
const result = await LocalLicenseApi.revoke(hid);
```

## `local-license.json` 文件格式

```json
{
  "HID": "A1B2C3D4E5F6...",
  "Company": "示例公司",
  "Name": "张三",
  "Phone": "138xxxxxxxx",
  "IP": "192.168.1.100",
  "ProductType": "Enterprise",
  "IssuedAt": "2026-06-01T00:00:00Z",
  "ExpirationDate": "2027-06-01T00:00:00Z",
  "UpdateExpirationDate": "2027-06-01T00:00:00Z",
  "Signature": "Base64EncodedRSASignature..."
}
```

### 产品类型功能矩阵

| ProductType | AI 插件 | 多租户 | 高级报表 | 自定义域名 |
|------------|:-------:|:------:|:--------:|:----------:|
| `Personal` | ❌ | ❌ | ✅ | ✅ |
| `Enterprise` | ✅ | ✅ | ✅ | ✅ |

## 名称和数据边界

现行配置、文件和表分别使用 `LocalLicense*` / `MICROI_LOCAL_LICENSE_*`、`local-license.json` / `local-license-*.pem` / `.local_lic_*`、`diy_local_license` / `diy_local_license_log`。旧 `License*`、`MICROI_LICENSE_*`、`license.json`、`license-*.pem`、`.lic_*`、`diy_license*` 只用于安全兼容迁移；旧表迁移仅限独立 `LocalLicenseDbConn`，绝不修改框架主库 `diy_license`。

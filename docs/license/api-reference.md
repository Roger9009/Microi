# License API 参考

> **适用读者**：开发者 | **基础路径**：`/api/License/`

---

## 客户服务器端点（匿名或登录）

这些端点在**所有服务器**上可用，不需要私钥。

| 方法 | 端点 | 鉴权 | 说明 |
|------|------|------|------|
| GET | `/api/License/GetHardwareId` | 匿名 | 获取当前服务器 HID |
| GET | `/api/License/Verify` | 匿名 | 验证本地 License 状态 |
| GET | `/api/License/GetConfig` | 匿名 | 获取可配置项（ContactEmail、HeartbeatIntervalHours 等） |
| GET/POST | `/api/License/GetStatus` | 匿名 | 获取 License 运行状态摘要（含心跳/宽限期/吊销信息） |
| GET/POST | `/api/License/GetHeartbeatStatus` | 匿名 | 获取 License 心跳状态（不触发 Verify） |
| POST | `/api/License/WriteLicenseFile` | 匿名 | 写入 License 文件到磁盘 |
| POST | `/api/License/GenerateRegistrationFile` | 匿名 | 生成离线注册申请包 |
| GET/POST | `/api/License/Diagnostics` | 登录 | 获取完整诊断信息 |
| POST | `/api/License/Check` | 匿名 | 查询某个 HID 的 License 状态 |
| POST | `/api/License/QueryApplication` | 匿名 | 查询申请状态 |

### GET /api/License/GetHardwareId

```json
// Response
{ "Code": 1, "Data": { "HID": "A1B2C3D4E5F6..." } }
```

### GET /api/License/Verify

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

### POST /api/License/WriteLicenseFile

```json
// Request
{ "LicenseContent": "{ \"HID\": \"...\", ... }" }

// Response
{ "Code": 1, "Msg": "License 部署成功！" }
```

写入前自动执行：AES 解密（若加密）→ JSON 解析 → RSA 签名验证 → HID 匹配检查 → 到期检查。

### POST /api/License/GenerateRegistrationFile

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

## License 服务器端点（超级管理员）

这些端点在 **License 服务器（有私钥）** 上可用，需要超级管理员权限。

| 方法 | 端点 | 说明 |
|------|------|------|
| POST | `/api/License/Apply` | 提交授权申请（含验证码） |
| POST | `/api/License/Issue` | 直接签发 License |
| POST | `/api/License/Approve` | 审批通过并签发 |
| POST | `/api/License/Reject` | 驳回申请 |
| POST | `/api/License/Revoke` | 吊销/恢复 License |
| GET | `/api/License/GenerateKeyPair` | 生成 RSA 密钥对 |
| GET | `/api/License/List` | License 列表（支持 ?status= 筛选） |
| GET | `/api/License/Logs` | 操作日志（支持 ?hid= 筛选） |

### POST /api/License/Issue

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

### POST /api/License/Revoke

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

在 `Microi.Client/src/utils/business-base.js` 中提供 `LicenseApi`：

```javascript
import { LicenseApi } from '@/utils/business-base';

// 本地端点
const hid = await LicenseApi.getHardwareId();
const status = await LicenseApi.verify();
const diag = await LicenseApi.diagnostics();

// License 服务器端点
const result = await LicenseApi.apply(hid, company, name, phone, ...);
const result = await LicenseApi.issue(hid, company, ...);
const result = await LicenseApi.approve(hid);
const result = await LicenseApi.revoke(hid);
```

## License 文件格式

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

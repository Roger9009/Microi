# License 初始化配置

> **适用场景**：首次部署 Microi 平台时需要生成 RSA 密钥对并配置公钥/私钥。
> 
> **⚠️ 警告**：密钥对只生成一次！更换公钥会导致所有历史 License 文件无法验证。

---

## 步骤 1：生成 RSA 密钥对

调用管理接口（**仅在 License 服务器上执行一次**）：

```
GET /api/License/GenerateKeyPair
```

需要 **超级管理员** 身份。输出示例：

```json
{
  "PublicKeyBase64": "MIIBIjANBgkqhkiG9w0B...",
  "PrivateKeyBase64": "MIIEvQIBADANBgkqhkiG9w0B...",
  "PublicKeyPem": "-----BEGIN PUBLIC KEY-----\n...\n-----END PUBLIC KEY-----",
  "PrivateKeyPem": "[REDACTED]"
}
```

## 步骤 2：配置公钥（所有服务器必须）

### 方式 A：替换代码常量（推荐）

在 `Microi.Server/Microi.License/LicenseService.cs` 第 32 行替换：

```csharp
// 替换前
private const string DefaultPublicKeyBase64 = "REPLACE_WITH_YOUR_RSA2048_PUBLIC_KEY_BASE64";

// 替换后
private const string DefaultPublicKeyBase64 = "MIIBIjANBgkqhkiG9w0B...";
```

替换后**重新编译部署**所有服务器。

### 方式 B：环境变量（优先级更高）

```bash
# Linux
export MICROI_LICENSE_PUBLIC_KEY="MIIBIjANBgkqhkiG9w0B..."

# Windows PowerShell
$env:MICROI_LICENSE_PUBLIC_KEY = "MIIBIjANBgkqhkiG9w0B..."

# Docker Compose
environment:
  - MICROI_LICENSE_PUBLIC_KEY=MIIBIjANBgkqhkiG9w0B...
```

## 步骤 3：配置私钥（仅 License 服务器）

**不要将私钥写入代码。** 通过环境变量配置：

```bash
# Linux
export MICROI_LICENSE_PRIVATE_KEY="MIIEvQIBADANBgkqhkiG9w0B..."

# Windows PowerShell
$env:MICROI_LICENSE_PRIVATE_KEY = "MIIEvQIBADANBgkqhkiG9w0B..."

# Docker Compose
environment:
  - MICROI_LICENSE_PRIVATE_KEY=MIIEvQIBADANBgkqhkiG9w0B...
```

## 步骤 4：签发第一个 License

部署完成后，通过管理页面或 API 签发 License：

```json
POST /api/License/Issue
{
  "HID": "A1B2C3D4E5F6...",
  "Company": "示例公司",
  "Name": "联系人",
  "Phone": "138xxxxxxxx",
  "ProductType": "Enterprise",
  "ExpirationDate": "2027-06-01T00:00:00Z"
}
```

## 步骤 5：配置心跳 URL（可选）

在 `appsettings.json` 中配置：

```json
{
  "AppSettings": {
    "LicenseHeartbeatUrl": "https://your-license-server.com/api/License/Heartbeat",
    "LicenseContactEmail": "admin@yourcompany.com"
  }
}
```

默认值为 `https://api.itdos.com/api/License/Heartbeat` 和 `license@microi.net`。

## 硬件指纹（HID）固定（Docker/K8s）

Docker 容器每次重启 HID 可能变化。设置环境变量固定 HID：

```yaml
environment:
  - MICROI_MACHINE_ID=your-fixed-machine-id-here
```

优先级：`MICROI_MACHINE_ID` > `/etc/machine-id` > 注册表 `MachineGuid` > MAC 地址。

## 首次部署引导宽限期

`CheckGracePeriod()` 检测到以下情况时，**自动授予 7 天初始宽限期**：
- 数据库已就绪
- 无 `ValidProof` 记录（从未有过 License）
- 无 `.lic_grace` 文件（从未进入过宽限期）

日志输出：
```
Microi：【🆕License引导】首次部署，自动授予 7 天初始宽限期。
请尽快生成密钥对并自签发 License！
```

此机制确保开发者首次部署时能正常启动系统，有足够时间完成密钥配置。

---

## 业务底座独立管理员（BizAdmin）

业务底座内置了独立的超级管理员入口，用于底座自身的 Schema 管理和文档配置。

| 项目 | 值 |
|------|-----|
| 路由 | `POST /api/BusinessAuth/Login` |
| 用户名 | **`bizadmin`**（固定，不可修改） |
| 默认密码 | **`Admin@123`** |
| 存储方式 | Redis Hash `Microi:{osClient}:BizAdmin` → `PwdHash = SHA256(password)` |
| Token 有效期 | 24 小时 |

### ⚠️ 安全注意事项

1. **首次登录后必须修改默认密码**：调用 `POST /api/BusinessAuth/SetPassword`
2. 密码建议使用 12 位以上包含大小写字母、数字和特殊字符的强密码
3. `BusinessAuthController` 已内置登录失败锁定：**连续 5 次失败锁定 15 分钟**
4. 生产环境建议在前端 Nginx/反向代理层对 `/api/BusinessAuth/` 路径加 IP 白名单

### 修改密码

```json
POST /api/BusinessAuth/SetPassword
{
  "OsClient": "your-tenant",
  "Token": "您的当前Token",
  "OldPassword": "Admin@123",
  "NewPassword": "NewStrongPass@2026"
}
```

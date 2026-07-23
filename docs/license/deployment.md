# 本地附加授权部署指南

> **适用读者**：运维/管理员

---

## 服务器角色

`Microi.LocalLicense` 采用双角色架构，与框架授权独立：

| 角色 | 私钥 | 功能 | 典型环境 |
|------|:----:|------|---------|
| **本地授权中心** | ✅ 有 | 签发、审核、吊销本地附加授权 | 官方 api.itdos.com |
| **客户服务器** | ❌ 无 | 验证、部署、启动运行 | 客户内网/云服务器 |

---

## 部署本地授权中心

### 前提条件
- 已完成[密钥配置](setup.md)
- 已设置 `MICROI_LOCAL_LICENSE_PRIVATE_KEY` 环境变量
- 已创建独立授权数据库（不得与 Microi 框架业务库共用）

### 配置建议

```json
// appsettings.json
{
  "AppSettings": {
    "LocalLicenseHeartbeatUrl": "https://your-license-server.com/api/LocalLicense/Heartbeat",
    "LocalLicenseContactEmail": "admin@yourcompany.com",
    "LocalLicenseDbType": "SqlServer",
    "LocalLicenseDbConn": "Server=db;Database=microi_local_license;User Id=local_license_user;Password=***;TrustServerCertificate=true;"
  }
}
```

也可使用环境变量 `MICROI_LOCAL_LICENSE_DB_TYPE`、`MICROI_LOCAL_LICENSE_DB_CONN`。授权中心启动后会通过底座 DDL
在独立库中幂等创建 `diy_local_license`、`diy_local_license_log`。未配置独立连接时，申请、审核、签发、查询均拒绝工作，
不会回退到 `OsClientDbConn` 指向的 Microi 框架库。

### 安全注意事项

1. **私钥安全**：`MICROI_LOCAL_LICENSE_PRIVATE_KEY` 仅设置在本地授权中心，客户服务器不要设置
2. **HTTPS 强制**：本地授权中心必须使用 HTTPS
3. **限流**：`ImportRegistrationFile` 接口有 IP+HID 双维度 60 秒限流
4. **权限控制**：Issue/Approve/Revoke/GenerateKeyPair 仅超级管理员可调用

---

## 部署客户服务器

### 授权申请流程

#### 在线流程（可访问本地授权中心）

```
1. 前端「授权管理」→「提交授权申请」
2. 填写公司信息 + 验证码 → 点击提交
3. 本地授权中心管理员审核
4. 前端「检查并部署License」→「自动部署到服务器」
5. 重启生效
```

#### 离线流程（纯内网）

```
1. 前端「提交授权申请」→「离线申请：生成注册文件」
2. 下载 .milic 注册文件
3. 发送至 license@microi.net
4. 授权管理员在「License 授权总控台」导入 .milic 并审核签发
5. 收到 `local-license.json` 后 → 「手动导入授权文件」
6. 重启生效
```

### License 续期

```
1. 到期前联系官方续期
2. 前端「检查并部署License」重新部署
3. 调用 GET `/api/LocalLicense/Verify` 确认
```

---

## Docker 部署

### 固定 HID

```yaml
version: '3.8'
services:
  microi:
    image: microi/server:latest
    environment:
      - MICROI_MACHINE_ID=your-fixed-machine-id-here
      - MICROI_LOCAL_LICENSE_PUBLIC_KEY=your-public-key-base64
      # 本地授权中心额外设置：
      - MICROI_LOCAL_LICENSE_PRIVATE_KEY=your-private-key-base64
      - MICROI_LOCAL_LICENSE_ENCRYPT_KEY=your-32-char-random-string
    volumes:
      - ./data:/app/data
    ports:
      - "7266:7266"
```

### 本地授权文件持久化

`local-license.json`、`local-license-public.pem`、`local-license-private.pem` 和 `.local_lic_*` 均位于应用的 `AppContext.BaseDirectory`。容器部署时应按镜像目录结构持久化这些具体文件；不要把空宿主目录直接覆盖整个应用目录。私钥优先通过 `MICROI_LOCAL_LICENSE_PRIVATE_KEY` 注入，不建议持久化 `local-license-private.pem`。

---

## 故障排除

| 问题 | 原因 | 解决 |
|------|------|------|
| 启动时 `License验证未通过` | `local-license.json` 缺失/无效 | 检查文件是否存在，或自动进入宽限期 |
| `License与当前服务器不匹配` | HID 不匹配 | 检查是否更换了硬件/Docker 容器 |
| `License签名验证失败` | 公钥不匹配 | 确认 `DefaultPublicKeyBase64` 已替换 |
| `License已于 X 到期` | License 过期 | 联系续期 |
| `License已被官方服务器吊销` | 心跳检测到吊销 | 联系管理员恢复 |
| 心跳 `已离线 N 天` | 无法连接本地授权中心 | 检查网络，离线 30 天内无影响 |

## 兼容迁移安全边界

现行名称必须使用 `LocalLicense*`、`MICROI_LOCAL_LICENSE_*`、`local-license.json`、`local-license-*.pem`、`.local_lic_*` 和 `diy_local_license*`。旧 `License*`、`MICROI_LICENSE_*`、`license.json`、`license-*.pem`、`.lic_*`、`diy_license*` 仅用于安全兼容迁移：

- 旧文件和配置仅作为读取回退，新部署不得继续写旧名称。
- 旧表只允许从 `LocalLicenseDbConn` 指向的独立授权库复制，且不更新、不删除旧表。
- 框架 `/api/License/*` 保持原样，绝不属于本地附加授权。
- 绝不访问或修改 Microi 框架主库 `diy_license`。

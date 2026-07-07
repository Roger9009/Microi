# License 部署指南

> **适用读者**：运维/管理员

---

## 服务器角色

Microi License 系统采用双角色架构：

| 角色 | 私钥 | 功能 | 典型环境 |
|------|:----:|------|---------|
| **License 服务器** | ✅ 有 | 签发、审核、吊销 License | 官方 api.itdos.com |
| **客户服务器** | ❌ 无 | 验证、部署、启动运行 | 客户内网/云服务器 |

---

## 部署 License 服务器

### 前提条件
- 已完成[密钥配置](setup.md)
- 已设置 `MICROI_LICENSE_PRIVATE_KEY` 环境变量
- 数据库 `diy_license` 表已就绪

### 配置建议

```json
// appsettings.json
{
  "AppSettings": {
    "LicenseHeartbeatUrl": "https://your-license-server.com/api/License/Heartbeat",
    "LicenseContactEmail": "admin@yourcompany.com"
  }
}
```

### 安全注意事项

1. **私钥安全**：`MICROI_LICENSE_PRIVATE_KEY` 仅设置在 License 服务器，客户服务器不要设置
2. **HTTPS 强制**：License 服务器必须使用 HTTPS
3. **限流**：`ImportRegistrationFile` 接口有 IP+HID 双维度 60 秒限流
4. **权限控制**：Issue/Approve/Revoke/GenerateKeyPair 仅超级管理员可调用

---

## 部署客户服务器

### 授权申请流程

#### 在线流程（可访问 License 服务器）

```
1. 前端「授权管理」→「提交授权申请」
2. 填写公司信息 + 验证码 → 点击提交
3. License 服务器管理员审核
4. 前端「检查并部署License」→「自动部署到服务器」
5. 重启生效
```

#### 离线流程（纯内网）

```
1. 前端「提交授权申请」→「离线申请：生成注册文件」
2. 下载 .milic 注册文件
3. 发送至 license@microi.net 或通过「直接提交到 License 服务器」
4. 收到 license.json 后 → 「手动导入授权文件」
5. 重启生效
```

### License 续期

```
1. 到期前联系官方续期
2. 前端「检查并部署License」重新部署
3. 调用 GET /api/License/Verify 确认
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
      |      - MICROI_MACHINE_ID=your-fixed-machine-id-here
            - MICROI_LICENSE_PUBLIC_KEY=your-public-key-base64
            # License 服务器额外设置：
            - MICROI_LICENSE_PRIVATE_KEY=your-private-key-base64
            # 🔴 必须设置：License 文件加密密钥（V-001 修复）
            - MICROI_LICENSE_ENCRYPT_KEY=your-32-char-random-string
          volumes:
      - ./data:/app/data
    ports:
      - "7266:7266"
```

### License 文件持久化

```yaml
volumes:
  - ./license:/app/license
  # license.json 将写入 /app/license/license.json
```

---

## 故障排除

| 问题 | 原因 | 解决 |
|------|------|------|
| 启动时 `License验证未通过` | license.json 缺失/无效 | 检查文件是否存在，或自动进入宽限期 |
| `License与当前服务器不匹配` | HID 不匹配 | 检查是否更换了硬件/Docker 容器 |
| `License签名验证失败` | 公钥不匹配 | 确认 `DefaultPublicKeyBase64` 已替换 |
| `License已于 X 到期` | License 过期 | 联系续期 |
| `License已被官方服务器吊销` | 心跳检测到吊销 | 联系管理员恢复 |
| 心跳 `已离线 N 天` | 无法连接 License 服务器 | 检查网络，离线 30 天内无影响 |

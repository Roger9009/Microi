# Microi 吾码 — License 授权系统文档

> **文档版本**：v5.7.6 | **最后更新**：2026-07-04
> 
> License 授权系统是 Microi 平台的商业功能管理组件，负责许可证的生成、验证、心跳检测和宽限期管理。

---

## 📚 文档目录

| 文档 | 适用读者 | 内容 |
|------|---------|------|
| [架构概览](architecture.md) | 全部 | 分层设计、版本模式、防御机制 |
| [初始化配置](setup.md) | 运维/管理员 | 密钥生成、公私钥配置、Docker 部署 |
| [API 参考](api-reference.md) | 开发者 | 全部 REST API 端点及参数 |
| [部署指南](deployment.md) | 运维 | License 服务器 vs 客户服务器部署 |
| [开发指南](development.md) | 开发者 | 扩展 License 功能、集成指引 |
| [前端集成](frontend.md) | 前端开发者 | 前端页面结构、API 调用方式 |
| [安全审计](security-audit.md) | 运维/开发者 | 漏洞报告、安全加固措施 |

---

## 快速导航

### 🆕 首次部署

```
1. 阅读 架构概览 → 了解分层和模式
2. 阅读 初始化配置 → 生成密钥对 + 配置公钥
3. 阅读 部署指南 → 按角色部署
4. 阅读 API 参考 → 调用签发接口
```

### 🔧 日常运维

```
查看 License 状态  →  GET /api/License/Verify
吊销操作          →  POST /api/License/Revoke
查看操作日志       →  GET /api/License/Logs
在线申请部署       →  前端页面「授权管理」
```

### 💻 开发新功能

```
前端调用 License API  →  使用 LicenseApi（business-base.js）
后端新增端点         →  在 LicenseController 添加
新增 License 特性     →  在 LicenseService 注册 Features 常量
```

---

## 相关文件

| 文件 | 位置 |
|------|------|
| 核心授权逻辑 | `Microi.Server/Microi.License/LicenseService.cs` |
| 数据模型 | `Microi.Server/Microi.License/LicenseModel.cs` |
| HID 采集 | `Microi.Server/Microi.License/HardwareHelper.cs` |
| 桥接层 | `Microi.Server/Microi.net.Api/Handler/License/LicenseServiceBridge.cs` |
| 心跳服务 | `Microi.Server/Microi.net.Api/Handler/License/LicenseBackgroundService.cs` |
| REST API | `Microi.Server/Microi.net.Api/Controllers/LicenseController.cs` |
| 前端页面 | `Microi.Client/src/views/system/license.vue` |
| API 客户端 | `Microi.Client/src/utils/business-base.js` (`LicenseApi`) |

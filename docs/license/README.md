# Microi 吾码 — 本地附加授权文档

> **文档版本**：v5.7.7 | **最后更新**：2026-07-17
> 
> 本目录只描述项目自定义的本地附加授权（`Microi.LocalLicense`），负责许可证生成、验证、心跳检测和宽限期管理。它与 Microi 框架授权完全隔离；框架 `/api/License/*` 及框架主库 `diy_license` 不属于本文档范围，也不得被本地授权迁移或升级修改。

---

## 📚 文档目录

| 文档 | 适用读者 | 内容 |
|------|---------|------|
| [架构概览](architecture.md) | 全部 | 分层设计、版本模式、防御机制 |
| [初始化配置](setup.md) | 运维/管理员 | 密钥生成、公私钥配置、Docker 部署 |
| [API 参考](api-reference.md) | 开发者 | 全部 REST API 端点及参数 |
| [部署指南](deployment.md) | 运维 | 本地授权中心 vs 客户服务器部署 |
| [开发指南](development.md) | 开发者 | 扩展本地附加授权、集成指引 |
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
查看本地授权状态    →  GET /api/LocalLicense/Verify
吊销操作          →  POST /api/LocalLicense/Revoke
查看操作日志       →  GET /api/LocalLicense/Logs
在线申请部署       →  前端页面「授权管理」
```

### 💻 开发新功能

```
前端调用本地授权 API  →  使用 LocalLicenseApi（business-base.js）
后端新增端点          →  在 LocalLicenseController 添加
新增授权特性          →  在 LocalLicenseService 注册 Features 常量
```

---

## 相关文件

| 文件 | 位置 |
|------|------|
| 核心授权逻辑 | `Microi.Server/Microi.LocalLicense/LocalLicenseService.cs` |
| 数据模型 | `Microi.Server/Microi.LocalLicense/LocalLicenseModel.cs` |
| HID 采集 | `Microi.Server/Microi.LocalLicense/LocalLicenseHardwareHelper.cs` |
| 桥接层 | `Microi.Server/Microi.net.Api/Handler/LocalLicense/LocalLicenseServiceFacade.cs` |
| 心跳服务 | `Microi.Server/Microi.net.Api/Handler/LocalLicense/LocalLicenseBackgroundService.cs` |
| REST API | `Microi.Server/Microi.net.Api/Controllers/LocalLicenseController.cs` |
| 客户页面 | `Microi.Client/src/views/system/local-license.vue`（`/local-license`） |
| 管理页面 | `Microi.Client/src/views/system/LocalLicenseAdminConsole.vue`（`/local-license-admin`） |
| API 客户端 | `Microi.Client/src/utils/business-base.js`（`LocalLicenseApi`） |

## 兼容迁移边界

旧自定义授权名称 `License*`、`MICROI_LICENSE_*`、`license.json`、`license-*.pem`、`.lic_*`、`diy_license`、`diy_license_log` 仅用于安全兼容迁移。文件和配置回退只读取旧值；旧表迁移也只会在 `LocalLicenseDbConn` 指向的独立授权库内复制数据到新表。任何情况下都不得读取、写入、迁移或删除 Microi 框架主库的 `diy_license`。

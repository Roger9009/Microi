# Microi吾码 技术路线评估

## 1. 平台总览

**Microi吾码** 是一款面向开发者的开源 AI 低代码平台，核心理念是“低代码 + AI 编程”深度融合。平台通过 V8 引擎（JavaScript 解释器）让业务人员在 Web 端用 JS 编写后端接口与表单事件，同时支持 VS Code 插件实现本地 AI 编程闭环。

当前版本：**v5.7.6**（2026 年）  
技术栈：**.NET 10 + Vue 3 + Redis + Dos.ORM + Jint(V8)**  
开源协议：MIT

---

## 2. 技术栈与架构模式

### 2.1 后端架构

| 层级 | 技术选型 | 说明 |
|------|---------|------|
| Web API | ASP.NET Core 10 + MVC | 入口项目 `Microi.net.Api`，控制器 + 动态路由 |
| 目标框架 | .NET 10 / netstandard2.1 | API 项目用 .NET 10，核心库用 netstandard2.1 兼容多版本 |
| ORM | 自研 **Dos.ORM** | 多数据库适配（MySQL/SQL Server/Oracle），读写分离 |
| 缓存 | Redis + MemoryCache | L1/L2 级缓存，多节点共享 Redis |
| 实时通讯 | SignalR + StackExchangeRedis | 分布式消息推送，20 分钟超时 |
| 脚本引擎 | **Jint 4.5.0** | 在 .NET 中运行 JavaScript，即 V8 引擎 |
| 认证 | JWT + 自定义 DiyToken | 双轨 Token 机制 |
| 消息队列 | RabbitMQ | 模块化集成 |
| 物联网 | MQTTnet | MQTT 服务器/客户端 |
| 搜索引擎 | Elasticsearch | 分词搜索 |
| 文件存储 | MinIO / 阿里云 OSS / S3 | 分布式存储 |
| 定时任务 | 自研 Job 引擎 | 调用接口引擎或 DLL |

### 2.2 前端架构

| 层级 | 技术选型 | 说明 |
|------|---------|------|
| 框架 | **Vue 3.5.27** | Composition / Options API 混用 |
| 构建工具 | **Vite 7.3.1** | 快速开发与构建 |
| 状态管理 | **Pinia 3** + 持久化插件 | 跨页面状态共享 |
| UI 组件 | **Element-Plus 2.13.7** | 主界面 |
| 图标 | FontAwesome + Element 图标 | 双图标体系 |
| 可视化 | ECharts 6 + AntV X6 + VisActor | 报表、流程图、大屏 |
| 编辑器 | Monaco + CodeMirror + WangEditor | 代码/富文本 |
| 微前端 | `@micro-zoe/micro-app` | Vue3 微前端方案 |
| 移动端 | UniApp | 小程序/H5/App |
| 3D 渲染 | Three.js | 数字孪生 |
| 测试 | Playwright | E2E 测试 |

### 2.3 架构模式

- **模块化插件架构**：每个能力对应一个独立项目（`Microi.Cache`、`Microi.MQ`、`Microi.Office`…），通过 `services.AddMicroiXXX()` 在 `Program.cs` 中按需注册。
- **自研 ORM 与缓存中间件**：不依赖 EF Core，使用自研 Dos.ORM 实现跨数据库。
- **V8 脚本驱动**：核心业务逻辑（接口引擎、表单事件）可通过 JavaScript 在线编写，无需重新编译发布。
- **SaaS 多租户**：支持数据库隔离、TenantId 隔离、组织机构隔离三种模式。
- **服务定位器模式**：`MicroiEngine` 提供静态 `IServiceProvider` 访问，方便在静态类或脚本中调用服务。

---

## 3. 项目结构拆解

```
Microi.net/
├── Microi.Server/                  # 后端源码
│   ├── Microi.net.Api/             # ASP.NET Core 入口，控制器、Program.cs、动态路由
│   ├── Microi.Core/                # 核心库：接口定义、模型、SaaS、Token、V8 基础
│   ├── Microi.V8Engine/            # V8 引擎扩展（Jint + 业务 API 绑定）
│   ├── Microi.Cache/               # Redis + 内存缓存
│   ├── Microi.ORM/                 # 多数据库 ORM（底层）
│   ├── Microi.MongoDB/             # MongoDB 日志
│   ├── Microi.MQ/                  # RabbitMQ
│   ├── Microi.MQTT/                # 物联网 MQTT
│   ├── Microi.SearchEngine/        # Elasticsearch
│   ├── Microi.Office/              # Excel/Word/邮件
│   ├── Microi.Job/                 # 定时任务
│   ├── Microi.Spider/              # 网页采集
│   ├── Microi.WeChat/              # 微信生态
│   ├── Microi.Captcha/             # 验证码
│   ├── Microi.HDFS/                # 分布式存储
│   ├── Microi.Upgrade/             # 数据库热升级脚本
│   ├── Microi.License/             # 新授权模块（参考实现）
│   ├── Dos.ORM/                    # 自研 ORM 基础
│   └── Dos.Common/                 # 通用工具、加密、结果对象
├── Microi.Client/                  # PC 前端（Vue3 + Vite）
├── microi.uniapp/                  # 移动端
├── microi.app/                     # HBuilderX 打包工程
├── microi.mcp/                     # MCP Server（AI Agent 工具）
├── microi.skills/                  # AI Skills 知识库
└── microi.doc/                     # VitePress 官方文档
```

---

## 4. 当前设计优势

1. **高度模块化**：每个引擎独立项目，可裁剪、可替换、可单独发布 NuGet。
2. **低代码与 AI 深度融合**：V8 脚本 + AI 生成代码，降低业务开发门槛。
3. **多数据库/跨平台**：netstandard2.1 核心库 + Dos.ORM 适配 MySQL/SQL Server/Oracle。
4. **热升级能力**：`Microi.Upgrade` 让数据库版本随应用启动自动升级，便于多环境部署。
5. **丰富的企业级能力**：工作流、报表、打印、大屏、IM、物联网、采集、微信等集成度高。
6. **版本隔离策略**：`Directory.Build.props` 自动识别本地项目 vs NuGet，方便开源版与商业版切换。

---

## 5. 优化与升级空间

### 5.1 架构层面

| 方向 | 现状 | 建议 | 优先级 |
|------|------|------|--------|
| 依赖注入 | 混合静态服务定位器（`MicroiEngine`）与 DI | 逐步减少静态类依赖，推广构造函数注入；降低单测难度 | 高 |
| 控制器膨胀 | 控制器职责较大，部分静态类 | 将 `LicenseService` 等静态类重构为 Scoped 服务，便于生命周期管理 | 中 |
| 配置管理 | 环境变量 + `appsettings` + `.microi-local` 混合 | 统一使用 `IOptions<T>` + 配置验证，减少运行时硬编码 | 中 |
| 异常处理 | 多处 try-catch 返回 `DosResult(0,...)` | 引入全局异常中间件 + 日志链路追踪，减少重复代码 | 中 |
| 健康检查 | 未见 `/health` 端点 | 增加 ASP.NET Core HealthChecks，集成 Redis/DB/ORM 检查 | 低 |

### 5.2 后端代码质量

| 方向 | 现状 | 建议 |
|------|------|------|
| 字符串硬编码 | 表名、字段名、SQL 中大量硬编码 | 使用常量类或 ORM 强类型实体，减少 typo 风险 |
| 异步模型 | 部分控制器同步调用 DB，部分未配 `async` | 统一 `async/await`，避免线程池饥饿 |
| 空值处理 | 大量 `?.` 和手动 null 检查 | 启用 `Nullable` 已开启，可进一步使用 `required`、 record 等特性 |
| 测试覆盖 | 缺少可见的单元测试项目 | 为 V8 引擎、ORM、License 等核心模块添加 xUnit 测试 |
| 代码重复 | 部分工具方法在多个模块重复 | 下沉到 `Dos.Common` 或 `Microi.Core` |

### 5.3 安全与合规

| 方向 | 现状 | 建议 |
|------|------|------|
| 密钥管理 | 私钥可放环境变量或 PEM 文件 | 生产环境建议接入 Azure Key Vault / AWS Secrets Manager / 国产 KMS |
| License 文件 | `license.json` 写入磁盘 | 支持加密存储或容器 Secret 挂载，增加防篡改校验 |
| 用户输入 | V8 引擎执行用户脚本 | 需要沙箱限制、超时控制、资源配额，防止恶意脚本 |
| 依赖漏洞 | csproj 中 `NU1902/NU1903` 被忽略 | 建立定期依赖扫描流程（`dotnet list package --vulnerable`） |

### 5.4 性能与可观测性

| 方向 | 现状 | 建议 |
|------|------|------|
| 日志 | `ConsoleLogInterceptor` 捕获输出 | 引入结构化日志（Serilog + ELK / Loki），统一日志级别 |
| 链路追踪 | 未见 OpenTelemetry | 接入 OpenTelemetry + Jaeger/Zipkin，追踪 V8 执行与 DB 调用 |
| 性能监控 | 无内置 Metrics | 添加 `System.Diagnostics.Metrics` 或 Prometheus exporter |
| 缓存策略 | Redis 已使用 | 对热点配置、字典表增加缓存过期策略与本地缓存一致性问题处理 |
| 数据库连接 | 最大连接池、生命周期在常量中 | 支持动态调整，结合连接池监控 |

### 5.5 前端与工程化

| 方向 | 现状 | 建议 |
|------|------|------|
| 依赖数量 | 依赖超 130 个 | 定期审计，移除未使用依赖，减少构建体积与攻击面 |
| 组件设计 | 部分页面大而全（如 `license.vue` 898 行） | 拆分为子组件，提高可维护性 |
| 类型安全 | 使用 JS/Vue，少量 TS | 核心业务模块逐步迁移到 TypeScript |
| 构建优化 | 已用 Vite，但未用 SSR/Split | 启用代码分割、懒加载，减少首屏加载 |
| 测试 | 有 Playwright E2E | 补充单元测试（Vitest）和组件测试 |

### 5.6 云原生与 DevOps

| 方向 | 现状 | 建议 |
|------|------|------|
| 容器化 | 已包含 Dockerfile | 构建多阶段镜像，优化镜像体积 |
| 配置外部化 | 部分配置在本地文件 | 使用 ConfigMap/Secret 注入，适应 K8s |
| 灰度发布 | 未见 | 结合接口引擎特性，可实现按租户/按功能灰度 |
| 自动化 | 一键发布脚本存在 | 补充 GitHub Actions / GitLab CI 流水线 |

---

## 6. 关键风险点

1. **自研 ORM 长期维护成本**：Dos.ORM 是平台核心，但社区维护力量有限，长期需考虑与 Dapper / EF Core 的兼容层。
2. **Jint 脚本安全**：执行用户提交的 JS 代码必须严格限制 API、资源和时间，否则存在安全风险。
3. **版本碎片化**：多模块版本号（5.7.6）需要同步更新，容易遗漏。
4. **商业版与开源版代码隔离**：`Directory.Build.props` 已做条件引用，但需注意商业功能泄漏到开源代码。
5. **静态类的可测试性**：大量静态类（如 `LicenseService`）不利于单元测试和 Mock。
6. **前端依赖老旧**：jQuery 4、Underscore 等现代 Vue 项目已较少使用，可考虑移除。

---

## 7. 推荐升级路线图

### 短期（1-2 个月）

- [ ] 统一 `LicenseService` 等静态类为 DI 服务，补充单元测试
- [ ] 引入全局异常处理中间件与结构化日志
- [ ] 补充 `/health` 端点与依赖漏洞扫描
- [ ] 前端 `license.vue` 拆分子组件，增加 TypeScript 类型
- [ ] 完善 `.gitignore` 与安全密钥管理规范

### 中期（3-6 个月）

- [ ] 引入 OpenTelemetry 链路追踪与 Prometheus 指标
- [ ] 建立核心模块 xUnit 测试套件（V8 引擎、FormEngine、License）
- [ ] 优化前端构建体积，移除未使用依赖，启用懒加载
- [ ] 实现 Jint 沙箱安全策略（超时、内存、API 白名单）
- [ ] 建立 CI/CD 流水线与容器镜像自动构建

### 长期（6-12 个月）

- [ ] 评估 Dos.ORM 与 Dapper/EF Core 的兼容或替换方案
- [ ] 引入 gRPC 服务间通信，支持真正的微服务拆分
- [ ] 完善多租户下的数据隔离审计与安全合规
- [ ] 前端核心模块 TypeScript 化，提升可维护性
- [ ] 提供官方 Helm Chart / K8s Operator 部署方案

---

## 8. 总结

Microi吾码是一个**成熟、功能丰富、模块化程度高**的低代码平台，技术路线清晰，符合中大型企业的复杂业务需求。其主要竞争力在于：

- 自研 ORM + V8 脚本引擎带来的高度灵活性
- 低代码与 AI 编程的深度融合
- 多引擎、多数据库、多端统一的企业级能力

下一阶段建议优先关注：**代码可测试性、安全沙箱、可观测性、前端工程化**，这些改进将显著提升平台的稳定性与规模化交付能力。

---

*文档生成时间：2026-06-21*  
*版本：v5.7.6*

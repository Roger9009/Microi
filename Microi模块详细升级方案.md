# Microi吾码 各模块详细升级方案

> 本文基于 v5.7.6 代码框架，对每个模块给出：现状、职责边界、技术债务、优化方向与可执行任务清单。

---

## 1. Microi.net.Api（Web API 入口层）

### 当前状态
- ASP.NET Core 10 Web 项目，负责 HTTP 入口、控制器、Swagger、动态路由、静态文件、Session、JWT。
- `Program.cs` 采用模块化注册方式：通过 `services.AddMicroiXXX()` 按需加载各引擎。
- 包含自定义 `DynamicRoute` 用于接口引擎/数据源引擎动态匹配 `{controller}/{action}/{param...}`。

### 技术债务
- 启动文件接近 450 行，注册逻辑、中间件配置、延迟初始化全部堆在 `Program.cs`。
- 静态服务定位器 `MicroiEngine` 与 DI 混用。
- 缺乏全局异常中间件与请求日志中间件。
- 部分控制器逻辑依赖静态类，单测困难。

### 优化方案
1. **启动文件拆分**
   - 将 `Program.cs` 拆分为 `ServicesExtensions/` 下的 `AddMicroiServices()`、`AddAuthServices()`、`AddSignalRServices()`、`AddSwaggerServices()` 等扩展方法。
   - 引入 `IStartupFilter` 或 `WebApplicationBuilder` 扩展，让各模块自行注册自己的服务与中间件。
2. **全局异常与日志中间件**
   - 添加 `GlobalExceptionMiddleware` 统一返回 `DosResult` 结构。
   - 添加 `RequestLoggingMiddleware` 记录请求路径、耗时、异常。
3. **控制器标准化**
   - 定义 `ApiControllerBase` 基类，统一 `Json()` 返回格式、当前用户获取、权限检查。
   - 将 `LicenseService` 等静态类改为 `Scoped` 服务，通过构造函数注入。
4. **健康检查**
   - 添加 `services.AddHealthChecks()`，检查 DB、Redis、ORM 初始化状态。
5. **版本化 API 路由**
   - 动态路由保留 `/api/{controller}/{action}`，同时支持显式 `[Route("api/v1/[controller]/[action]")]`。

### 任务清单
- [ ] 拆分 `Program.cs` 为多个扩展类
- [ ] 实现 `GlobalExceptionMiddleware` 与 `RequestLoggingMiddleware`
- [ ] 创建 `ApiControllerBase` 基类
- [ ] 将 `LicenseService` 改为 `ILicenseService` 注入
- [ ] 添加 `/health` 与 `/health/ready` 端点
- [ ] 补充 API 控制器单元测试（Moq + xUnit）

---

## 2. Microi.Core（核心基础设施库）

### 当前状态
- `netstandard2.1` 类库，包含接口定义、数据模型、SaaS 引擎、Token、ORM 封装、V8 基础、工作流、表单引擎等。
- `MicroiEngine` 提供静态服务定位器，方便在静态方法或脚本中解析服务。
- `Dos.Common` 的 `DosResult<T>` 作为统一返回结构。

### 技术债务
- 职责过重：Core 中既包含接口抽象，也包含业务逻辑（如 `SysUserLogic`）。
- 静态类多，依赖关系不清晰。
- 缺少强类型配置对象（`IOptions<T>`）。

### 优化方案
1. **按职责拆分 Core**
   - 将业务逻辑（`SysUserLogic`、`SysMenuLogic`）迁移到独立的 `Microi.Business` 或保留在对应模块中。
   - 保留 Core 为“纯基础设施”：接口、模型、常量、工具、抽象基类。
2. **强类型配置**
   - 为 `OsClient`、`Redis`、`Jwt`、`License` 等配置定义 Options 类，配合 `IOptionsSnapshot<T>` 使用。
3. **统一结果模型**
   - 在 `DosResult` 基础上扩展分页、错误码枚举、链路 ID。
4. **接口抽象下沉**
   - 将 `IMicroiCache`、`IMicroiORM`、`IMicroiAI` 等接口定义在 Core，实现分布在各模块。

### 任务清单
- [ ] 梳理 Core 中业务逻辑类，制定迁移计划
- [ ] 为高频配置添加 `IOptions<T>` 绑定
- [ ] 定义 `MicroiException` 异常体系
- [ ] 将 `DosResult` 扩展为支持 `TraceId` 与 `ErrorCode`
- [ ] 补充 Core 接口的 XML 注释，生成 Swagger 文档

---

## 3. Microi.V8Engine（V8 脚本引擎）

### 当前状态
- 基于 `Jint` 4.5.0 的 JavaScript 解释器。
- 通过全局对象 `V8` 暴露后端能力：数据库、缓存、HTTP、Office、MQ、MongoDB、ApiEngine 等。
- 支持接口引擎（在线写 JS 接口）和表单 V8 事件（InFormV8 / SubmitFormV8）。

### 技术债务
- 用户脚本在安全沙箱方面较弱，缺少执行时间、内存限制、API 白名单控制。
- 调试与错误定位困难，缺少行号映射与调用栈。
- 脚本性能无监控。

### 优化方案
1. **安全沙箱**
   - 使用 `Jint.Options` 限制 `MaxStatements`、`Timeout`、`MemoryLimit`。
   - 定义 `V8` API 白名单，禁止访问文件系统、环境变量等敏感操作（除非显式授权）。
   - 对脚本进行 AST 预审或黑名单关键词检查。
2. **脚本缓存与预编译**
   - 将常用接口脚本预编译为 `Jint.Engine` 可重用的 `Script` 对象，减少重复解析开销。
   - 对脚本做内容哈希缓存，未变更时直接复用。
3. **调试与日志**
   - 在脚本执行时捕获 JS 调用栈，输出到日志。
   - 支持 SourceMap 或至少记录原始脚本行号。
4. **性能监控**
   - 记录每个 V8 接口的执行耗时、CPU、内存峰值，接入 Prometheus 指标。
5. **类型约束**
   - 为 `V8` 全局对象提供 TypeScript 声明文件（`microi.v8.d.ts`），提升本地 AI 编程体验。

### 任务清单
- [ ] 配置 Jint 安全限制（Timeout/MemoryLimit/MaxStatements）
- [ ] 实现 V8 API 白名单与敏感操作拦截
- [ ] 脚本预编译与缓存机制
- [ ] 完善 JS 异常调用栈输出
- [ ] 添加 V8 执行耗时/内存指标
- [ ] 维护 `microi.v8.d.ts` 类型定义

---

## 4. Dos.ORM / Microi.ORM（数据访问层）

### 当前状态
- 自研 ORM，支持 MySQL、SQL Server、Oracle。
- 提供 `DbSession`、`FromSql`、`Where`、`AddInParameter`、`ToArray`、`First`、`ExecuteNonQuery` 等 API。
- 多租户通过 `OsClient` 获取不同 `DbSession` 实现。

### 技术债务
- 手写 SQL 和表名字符串较多，缺少强类型实体约束。
- 动态查询拼接可能引入 SQL 注入风险（当前使用参数化，但复杂拼接需审计）。
- 缺乏审计日志（谁改了哪张表、哪条记录）。
- 读写分离、分库分表依赖外部配置，未提供统一抽象。

### 优化方案
1. **强类型实体生成**
   - 基于数据库 Schema 自动生成实体类（T4 / Source Generator），减少手写表名/字段名。
   - 为 `diy_license` 等新增表创建实体类，替代裸 SQL。
2. **SQL 审计与拦截**
   - 在 `DbSession` 中增加拦截器钩子，记录执行 SQL、参数、耗时、调用方。
   - 对慢 SQL 自动报警。
3. **读写分离统一接口**
   - 提供 `DbRead` / `DbWrite` 自动路由，配置文件指定只读从库连接串。
4. **分库分表策略**
   - 对日志、历史数据等大表提供按时间/租户的分表策略抽象。
5. **迁移与升级**
   - 升级脚本目前按版本号硬编码在 `Microi.Upgrade`；可引入 FluentMigrator 或 EF Migrations 兼容层，用于复杂迁移回滚。

### 任务清单
- [ ] 审计 Dos.ORM 中所有 SQL 拼接点，确保参数化
- [ ] 为新增表创建实体类并替换裸 SQL
- [ ] 实现 SQL 执行拦截器（审计、慢查询）
- [ ] 统一读写分离 API
- [ ] 评估 FluentMigrator 替代或补充升级脚本
- [ ] 添加 ORM 单元测试（内存中 SQLite / TestContainers）

---

## 5. Microi.Cache（缓存层）

### 当前状态
- Redis 分布式缓存 + 内存缓存两层。
- 使用 `IMicroiCache` / `IMicroiCacheTenant` 接口。
- 多租户缓存通过 `OsClient` 区分 Key 前缀。

### 技术债务
- 缓存穿透、击穿、雪崩策略不够明确。
- 缓存与数据库一致性依赖手动处理。
- 缺少缓存命中率监控。

### 优化方案
1. **缓存策略标准化**
   - 定义 `CacheAside`、`ReadThrough`、`WriteThrough` 三种策略工具类。
   - 对热点数据（如配置、字典、权限）使用 Cache-Aside + 过期时间。
2. **防穿透/击穿**
   - 引入空值缓存、互斥锁（Redis SETNX）防止缓存击穿。
   - 对高并发接口使用本地缓存 + Redis 二级缓存。
3. **缓存失效广播**
   - 使用 Redis Pub/Sub 通知所有节点清除本地缓存，保证一致性。
4. **监控**
   - 记录缓存命中率、失效次数、命中时长，接入 Prometheus。

### 任务清单
- [ ] 定义缓存策略枚举与工具类
- [ ] 实现缓存击穿保护（空值缓存 + 互斥锁）
- [ ] 实现 Redis 缓存失效广播
- [ ] 添加缓存命中率指标
- [ ] 梳理高频缓存 Key，统一前缀规范

---

## 6. Microi.MQ（消息队列）

### 当前状态
- RabbitMQ 集成，提供 `V8.MQ.SendMsg` 与消费接口。
- 接口引擎中通过 `V8.Param.Message` 接收消息。

### 技术债务
- 仅支持 RabbitMQ，缺少 Kafka / NATS / Pulsar 扩展点。
- 消息消费确认、死信队列、重试策略未显式抽象。
- 缺少消息轨迹追踪。

### 优化方案
1. **抽象消息队列接口**
   - 定义 `IMicroiMessageQueue` 接口，支持多种后端实现。
   - 默认 RabbitMQ，可选 Kafka/NATS 实现。
2. **消费可靠性**
   - 支持自动确认与手动确认切换。
   - 实现死信队列（DLQ）与指数退避重试。
3. **消息轨迹**
   - 为每条消息生成 `MessageId`，记录发送、消费、失败全链路。
4. **V8 集成增强**
   - 支持在接口引擎中声明消息队列消费者，自动注册到 MQ 模块。

### 任务清单
- [ ] 抽象 `IMicroiMessageQueue` 接口
- [ ] 实现 RabbitMQ 消费确认与重试策略
- [ ] 添加死信队列支持
- [ ] 消息发送/消费轨迹记录
- [ ] 支持接口引擎声明消费者

---

## 7. Microi.MongoDB（NoSQL 日志）

### 当前状态
- 用于日志系统，支持亿级数据毫秒级分页。
- 提供 `V8.MongoDb` 操作 API。

### 技术债务
- 未明确分片、索引、TTL 策略。
- 缺少 MongoDB 连接池监控。
- 与关系型数据库的混合事务不一致。

### 优化方案
1. **索引与 TTL 管理**
   - 为日志集合按时间字段建立 TTL 索引，自动清理过期数据。
   - 对查询字段建立复合索引，避免全表扫描。
2. **连接池监控**
   - 暴露 MongoDB 连接数、等待队列长度等指标。
3. **分片支持**
   - 对超大数据集合支持按时间分片或 MongoDB 原生分片配置。
4. **归档策略**
   - 冷热数据分离：热数据保留在 MongoDB，冷数据定期归档到对象存储。

### 任务清单
- [ ] 梳理 MongoDB 集合索引，补充缺失索引
- [ ] 为日志集合添加 TTL 策略
- [ ] 添加 MongoDB 连接池指标
- [ ] 实现日志冷热数据归档

---

## 8. Microi.SearchEngine（搜索引擎）

### 当前状态
- Elasticsearch 集成，提供分词搜索能力。

### 技术债务
- 索引创建、映射、同步依赖手动脚本。
- 缺少增量同步机制（如 Canal/Debezium）。
- 搜索权限控制与数据权限未完全打通。

### 优化方案
1. **索引管理自动化**
   - 定义索引模板与生命周期策略（ILM）。
   - 提供索引重建、别名切换工具。
2. **增量同步**
   - 通过 ORM 拦截器或数据库变更监听实现近实时同步。
   - 支持全量重建与增量同步两种模式。
3. **权限过滤**
   - 在搜索请求中注入当前用户的组织/角色过滤条件。
4. **多搜索引擎支持**
   - 抽象 `ISearchEngine` 接口，支持 Elasticsearch、OpenSearch、Meilisearch。

### 任务清单
- [ ] 抽象 `ISearchEngine` 接口
- [ ] 实现索引模板与 ILM 策略
- [ ] 基于 ORM 拦截器的增量同步
- [ ] 搜索权限过滤集成
- [ ] 支持 OpenSearch 适配

---

## 9. Microi.Office（文档处理）

### 当前状态
- 提供 Excel 导入导出、Word 模板、邮件发送、OnlyOffice 集成。
- V8 中通过 `V8.Office` 调用。

### 技术债务
- 大文件导出可能占用大量内存，缺少流式处理。
- 模板引擎与前端模板未统一。
- 缺少文档转换队列（如 PDF 生成）。

### 优化方案
1. **流式 Excel 导出**
   - 使用 EPPlus / NPOI 的流式写入，支持分批导出，避免大对象进入 LOH。
2. **模板统一**
   - 使用统一的模板语言（如 Handlebars / Liquid）支持 Word、Excel、邮件、打印模板。
3. **异步文档队列**
   - 将大文件导出、PDF 转换放入 `Microi.Job` 或 `Microi.MQ` 异步执行，完成后通知用户。
4. **OnlyOffice 安全**
   - 增加文档编辑权限校验与 JWT 校验，防止越权编辑。

### 任务清单
- [ ] 评估 EPPlus 流式导出替换方案
- [ ] 统一模板引擎（Handlebars/Liquid）
- [ ] 大文件导出异步队列化
- [ ] 强化 OnlyOffice JWT 校验

---

## 10. Microi.Job（任务调度）

### 当前状态
- 定时任务引擎，支持调用接口引擎或自定义 DLL。
- 通过 `services.AddMicroiJob(dbConn)` 注册。

### 技术债务
- 缺少可视化调度控制台。
- 未支持集群锁（多节点重复执行）。
- 任务失败重试、报警机制较弱。

### 优化方案
1. **集群调度锁**
   - 使用 Redis 分布式锁保证同一任务在集群中只执行一次。
2. **可视化控制台**
   - 前端页面展示任务列表、执行历史、下次执行时间、手动触发。
3. **失败重试与报警**
   - 配置重试次数、退避策略、失败通知（邮件/企业微信）。
4. **任务编排**
   - 支持 DAG 任务依赖，前置任务成功后触发后续任务。

### 任务清单
- [ ] 实现 Redis 分布式任务锁
- [ ] 前端任务调度控制台页面
- [ ] 任务失败重试与报警机制
- [ ] 支持 DAG 任务编排

---

## 11. Microi.MQTT（物联网）

### 当前状态
- 基于 MQTTnet 的 MQTT 服务器/客户端。
- 支持事件：StartServer / Connected / Disconnected / MessageReceived / StopServer。
- V8 中通过 `V8.MQTT` 访问。

### 技术债务
- 设备认证与授权未完全打通。
- 消息持久化与 QoS 策略需要明确。
- 缺少设备影子与规则引擎。

### 优化方案
1. **设备认证**
   - 支持用户名/密码 + JWT / TLS 客户端证书认证。
   - 设备与平台用户体系打通。
2. **消息持久化**
   - 将 MQTT 消息写入 MongoDB 或时序数据库（InfluxDB/TDengine）。
3. **规则引擎**
   - 支持基于 Topic 的规则：转发到 MQ、调用接口引擎、触发报警。
4. **高可用**
   - 支持 MQTT Broker 集群（如 EMQ X / HiveMQ）替代内嵌 Broker。

### 任务清单
- [ ] 设备认证与平台用户体系打通
- [ ] MQTT 消息持久化到 MongoDB
- [ ] 实现 Topic 规则引擎
- [ ] 支持外置 MQTT Broker 集群

---

## 12. Microi.HDFS（分布式存储）

### 当前状态
- 支持阿里云 OSS、MinIO、亚马逊 S3。
- 提供私有文件 URL 获取能力。

### 技术债务
- 缺少统一文件元数据管理表。
- 大文件分片上传、断点续传未统一。
- 未支持本地存储与云存储自动分层。

### 优化方案
1. **统一文件元数据**
   - 创建 `sys_file` 表记录文件名、大小、存储类型、Bucket、Path、MD5、引用计数。
2. **分片上传**
   - 前端 + 后端统一支持分片上传，后端合并后写入对象存储。
3. **存储策略**
   - 配置文件按大小/类型自动选择本地存储或云存储。
4. **垃圾回收**
   - 定期扫描无引用文件，清理对象存储中的孤立文件。

### 任务清单
- [ ] 创建统一文件元数据表
- [ ] 实现分片上传与合并
- [ ] 支持存储策略配置
- [ ] 实现文件引用计数与垃圾回收

---

## 13. Microi.WeChat（微信生态）

### 当前状态
- 微信公众号/小程序集成，基于 Senparc.Weixin。
- 提供多公众号/多小程序配置。

### 技术债务
- 依赖 Senparc 旧版本，与 .NET 10 兼容性需持续关注。
- 缺少微信支付集成。
- 消息推送与事件处理未完全抽象。

### 优化方案
1. **依赖升级**
   - 升级 Senparc.Weixin 到最新稳定版，验证 .NET 10 兼容性。
2. **微信支付**
   - 接入微信支付 V3，支持 JSAPI、Native、小程序支付。
3. **事件路由**
   - 抽象微信公众号消息事件路由，支持在接口引擎中注册事件处理。
4. **模板消息**
   - 统一模板消息管理表，支持按用户/角色批量发送。

### 任务清单
- [ ] 升级 Senparc.Weixin 依赖
- [ ] 接入微信支付 V3
- [ ] 抽象微信事件路由到接口引擎
- [ ] 模板消息管理表与批量发送

---

## 14. Microi.Captcha（验证码）

### 当前状态
- 验证码生成模块，支持算术/字符验证码。

### 技术债务
- 缺少行为验证码（滑动、点选）。
- 验证码存储依赖 Session，分布式部署下可能不一致。

### 优化方案
1. **验证码存储迁移到 Redis**
   - 使用 `CaptchaId + Redis` 存储，支持分布式多节点。
2. **行为验证码**
   - 集成滑动验证码、点选验证码，提升安全性。
3. **限流与防刷**
   - 对验证码接口增加 IP/用户限流。

### 任务清单
- [ ] 验证码答案存储迁移到 Redis
- [ ] 集成滑动/点选验证码
- [ ] 添加验证码接口限流

---

## 15. Microi.Upgrade（数据库升级）

### 当前状态
- 平台启动时自动执行数据库升级脚本。
- 每个版本一个 `N-UpgradeXXX.cs` 文件，包含 `Version` 与 `Sql`。
- 已添加 `14-UpgradeLicense.cs` 创建 `diy_license` 表。

### 技术债务
- 升级脚本不支持回滚。
- 复杂迁移（数据迁移、拆分字段）难以在纯 SQL 中完成。
- 缺少升级日志表与失败重试机制。

### 优化方案
1. **升级日志表**
   - 创建 `sys_upgrade_history` 记录每次升级版本、执行时间、状态、错误信息。
2. **支持 C# 迁移步骤**
   - 除了 `Sql` 字符串，允许迁移类实现 `UpgradeStep` 接口，执行复杂数据迁移。
3. **回滚机制**
   - 对破坏性操作生成回滚 SQL，失败时自动回滚。
4. **幂等性**
   - 所有 SQL 脚本使用 `IF NOT EXISTS`，保证重复执行不报错。

### 任务清单
- [ ] 创建 `sys_upgrade_history` 表
- [ ] 抽象 `IUpgradeStep` 接口支持 C# 迁移
- [ ] 为升级脚本添加回滚 SQL
- [ ] 确保所有脚本具备幂等性

---

## 16. Microi.License（授权模块 - 新增）

### 当前状态
- 已创建独立项目 `Microi.License` 与 `Microi.net.Api/Handler/License` 实现。
- 使用 RSA-2048 签名验证 License 文件。
- 支持硬件指纹（HID）绑定、申请、签发、审核、作废、手动导入。
- 启动时验证 License，失败进入宽限期。

### 技术债务
- 公钥以常量硬编码，需手动替换后重新编译。
- `LicenseService` 为静态类，不利于单测与生命周期管理。
- 宽限期策略（时长、功能限制）未细化。
- 缺少 License 服务器与管理后台页面。

### 优化方案
1. **密钥管理升级**
   - 支持公钥从环境变量/配置文件/远程 KMS 加载，避免硬编码。
   - 提供 `LicenseService` 初始化接口，支持运行时替换公钥。
2. **服务化改造**
   - 将 `LicenseService` 改为 `ILicenseService` Scoped 服务，注入数据库会话。
3. **宽限期策略**
   - 定义宽限期天数、受限功能列表（如禁止 AI 模块、限制并发数）。
   - 在权限中间件中读取 `LicenseService.IsGracePeriod` 做功能限制。
4. **License 服务器后台**
   - 在管理后台增加 License 申请列表、审核、签发、作废页面。
5. **License 文件加密**
   - 对写入的 `license.json` 做 AES 加密，防止明文泄露与篡改。

### 任务清单
- [ ] 支持公钥从环境变量/文件/KMS 动态加载
- [ ] 将 `LicenseService` 改为 DI 服务
- [ ] 定义宽限期策略与功能限制
- [ ] 开发 License 管理后台页面
- [ ] 对 License 文件加密存储
- [ ] 补充 License 单元测试（RSA 签名/验证、硬件指纹）

---

## 17. Microi.Client（PC 前端）

### 当前状态
- Vue 3 + Vite + Pinia + Element-Plus 构建。
- 包含表单引擎、模块引擎、界面引擎、报表引擎、大屏、流程图等。
- `license.vue` 已提供授权申请、检查部署、手动导入功能。

### 技术债务
- 依赖数量超过 130 个，构建体积大。
- 部分页面过大（如 `license.vue` 898 行），可维护性一般。
- 使用 JS 为主，类型安全不足。
- 双图标体系（FontAwesome + Element）增加复杂度。
- 部分老旧库（jQuery、Underscore）现代 Vue 项目中已不常用。

### 优化方案
1. **依赖治理**
   - 使用 `depcheck` / `unimported` 扫描未使用依赖，移除 jQuery、Underscore 等。
   - 对大型库（Monaco、ECharts、Three.js）按需加载或拆分包。
2. **组件拆分**
   - 将 `license.vue` 拆分为 `LicenseStatus.vue`、`LicenseApplyForm.vue`、`LicenseDeploy.vue`、`LicenseImport.vue`。
   - 其他大页面（如表单设计器、模块引擎）逐步拆分。
3. **TypeScript 迁移**
   - 新模块使用 TypeScript + `<script setup>`。
   - 为核心 API 请求层、状态管理提供类型定义。
4. **构建优化**
   - 启用 Vite 代码分割、`rollup-plugin-visualizer` 分析构建产物。
   - 使用 CDN 加载大型第三方库（如 Monaco、ECharts）。
5. **图标统一**
   - 统一使用 Element-Plus 图标或 `@element-plus/icons-vue`，移除 FontAwesome 依赖。
6. **测试覆盖**
   - 已有 Playwright E2E，补充 Vitest 单元测试与组件测试。

### 任务清单
- [ ] 扫描并移除未使用依赖
- [ ] 拆分 `license.vue` 为子组件
- [ ] 新模块使用 TypeScript
- [ ] 按需加载 Monaco、ECharts
- [ ] 统一图标体系
- [ ] 补充 Vitest 测试

---

## 18. microi.uniapp（移动端）

### 当前状态
- UniApp 跨端项目，支持小程序、H5、App。
- 与 PC 前端共享部分 API 与业务逻辑。

### 技术债务
- 与 PC 前端代码复用率低，部分能力重复实现。
- 缺少移动端专属组件库与性能优化。
- 蓝牙打印、扫码等原生能力依赖插件，需持续维护。

### 优化方案
1. **跨端组件复用**
   - 将通用业务组件（如表单渲染、审批流程）封装为 uni-app 组件，尽可能与 PC 端共享配置 Schema。
2. **性能优化**
   - 使用分包加载、图片懒加载、减少 setData 调用。
3. **原生能力抽象**
   - 统一蓝牙、扫码、定位、打印等原生能力调用层，适配不同平台差异。
4. **离线能力**
   - 对关键配置与字典数据做本地缓存，支持弱网环境。

### 任务清单
- [ ] 梳理 PC 与移动端可复用组件
- [ ] 实现通用表单渲染组件（uni-app）
- [ ] 移动端性能优化（分包、懒加载）
- [ ] 抽象原生能力调用层
- [ ] 增加离线缓存能力

---

## 19. microi.mcp（MCP Server）

### 当前状态
- 提供 MCP Server，让 AI Agent（Cursor / Claude）可以调用平台能力。

### 技术债务
- 工具数量与覆盖范围有限。
- 缺少权限控制与审计。
- 与 `microi.skills` 知识库的联动可进一步增强。

### 优化方案
1. **工具扩展**
   - 为每个核心模块（表单、流程、用户、权限、License）暴露 MCP 工具。
2. **权限与审计**
   - MCP 调用需携带用户 Token，执行前校验权限，并记录调用日志。
3. **知识库联动**
   - 让 MCP Server 可以读取 `microi.skills` 中的 SKILL.md，动态生成工具说明。
4. **标准化协议**
   - 跟进 MCP 协议最新版本，支持 SSE / STDIO 传输。

### 任务清单
- [ ] 扩展 MCP 工具覆盖核心模块
- [ ] 添加 MCP 调用权限校验与审计
- [ ] 读取 microi.skills 动态生成工具说明
- [ ] 支持 SSE 传输模式

---

## 20. microi.skills（AI Skills 知识库）

### 当前状态
- 为 AI 编程提供业务上下文知识库。
- 每个 Skill 包含 `SKILL.md` 与 `references/` 参考资料。

### 技术债务
- 知识库更新依赖手动维护。
- 缺少与向量数据库的自动同步。
- 版本管理与 Skill 依赖关系未明确。

### 优化方案
1. **自动同步到向量数据库**
   - 在 Skill 变更时，自动切分并向量化到 Qdrant / Milvus。
2. **Skill 版本管理**
   - 为每个 Skill 增加版本号与变更记录，AI 可引用最新版本。
3. **Skill 依赖**
   - 允许 Skill 声明依赖其他 Skill，构建知识图谱。
4. **本地 AI 编程增强**
   - VS Code 插件读取 Skill 后，自动生成 Cursor Rules / Copilot Instructions。

### 任务清单
- [ ] 实现 Skill 变更自动向量化
- [ ] 增加 Skill 版本与变更记录
- [ ] 支持 Skill 依赖声明
- [ ] VS Code 插件自动生成 AI 编程规则

---

## 21. 安全与合规横向方案

| 领域 | 现状 | 优化方向 |
|------|------|----------|
| 认证 | JWT + DiyToken | 支持 OAuth2 / OIDC / SSO 企业集成 |
| 授权 | 表/字段/菜单/按钮/接口级权限 | 引入 RBAC + ABAC 混合模型，支持数据权限表达式 |
| 审计 | 部分日志 | 统一审计日志表，记录所有数据变更与管理员操作 |
| 加密 | DES/RSA/AES | 统一使用 AES-256-GCM，密钥走 KMS |
| 输入校验 | 部分手动校验 | 引入 FluentValidation 或数据注解统一校验 |
| 漏洞扫描 | 手动忽略 NU1902/NU1903 | 建立 CI 依赖漏洞扫描与修复流程 |

---

## 22. 推荐实施顺序

```
第一阶段（1-2 个月）：基础治理
├── Microi.net.Api：拆分 Program.cs、全局异常、健康检查
├── Microi.Core：强类型配置、统一异常、结果模型增强
├── LicenseService：改为 DI 服务、公钥外部化、管理后台
├── Microi.Client：license.vue 拆分、移除未使用依赖
└── 安全：依赖漏洞扫描、KMS 密钥管理规范

第二阶段（3-6 个月）：性能与可观测性
├── V8 引擎：沙箱安全、脚本缓存、性能监控
├── ORM：SQL 审计、实体类生成、读写分离
├── Cache：防击穿、失效广播、命中率监控
├── 日志：Serilog + OpenTelemetry + Prometheus
└── CI/CD：GitHub Actions + Docker 镜像自动构建

第三阶段（6-12 个月）：架构升级
├── ORM：评估 FluentMigrator / Dapper 兼容层
├── MQ：抽象接口、Kafka 适配、死信队列
├── Search：增量同步、OpenSearch 适配
├── 微服务：gRPC 服务间通信、按领域拆分服务
└── 前端：TypeScript 化、组件库统一、SSR 评估
```

---

*文档生成时间：2026-06-21*  
*版本：v5.7.6*

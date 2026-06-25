# Microi 业务底座（ERP / MES 可扩展框架）

为在 Microi 吾码低代码平台之上编写业务代码而设计的分层框架，内置**两套生命周期**：

- **模块插件生命周期**：每个业务系统（ERP/MES/...）是一个可热插拔模块，统一经历 `发现 → ConfigureServices → 注册 → 启动前 → 启动后 → 停止`。
- **单据状态机生命周期**：每张业务单据（订单/工单）通过声明式状态机完成 `草稿 → 提交 → 审核 → 完成 → 作废` 等流转，带守卫与事件钩子。

## 目录结构

| 目录 | 程序集 | 职责 |
|------|--------|------|
| `Model/` | `Microi.Business.Model` | 共享模型：`BusinessEntity`、`BusinessStatefulEntity<TState>`、`BusinessParam`、生命周期枚举 |
| `PubilcModule/` | `Microi.Business.Core` | **内核**：模块生命周期 + 状态机 + 基础服务（CRUD/状态流转）+ DI 扩展 |
| `ComonBusiness/` | `Microi.Business.Common` | 公共业务：单据编号 `IBillNoService`、控制器基类 `BusinessControllerBase` |
| `ErpModule/` | `Microi.Erp` | ERP 示例：销售订单（`SalesOrder`） |
| `MesModule/` | `Microi.Mes` | MES 示例：生产工单（`WorkOrder`） |

依赖方向：`Erp/Mes → Common → Core → Model → Microi.Core`

## 核心概念

### 1. 模块插件生命周期（`IBusinessModule`）

```csharp
public class ErpModule : BusinessModuleBase
{
    public override string Key => "erp";
    public override string Name => "ERP 进销存";
    public override int Order => 100;              // 越小越先加载
    public override string[] DependsOn => new[] { "common" };

    public override void ConfigureServices(IServiceCollection services) { /* 注册服务 */ }
    public override Task OnStartingAsync(BusinessModuleContext ctx) { /* 启动前校验 */ return Task.CompletedTask; }
    public override Task OnStartedAsync(BusinessModuleContext ctx)  { /* 启动后可用 */ return Task.CompletedTask; }
    public override Task OnStoppingAsync(BusinessModuleContext ctx) { /* 释放资源 */ return Task.CompletedTask; }
}
```

启动时 `BusinessModuleManager` 自动扫描所有程序集，按 `Order` + `DependsOn` 拓扑排序后逐阶段驱动。单个模块失败不影响其它模块（标记为 `Faulted`）。

### 2. 单据状态机生命周期（`BusinessStateMachine`）

在服务里声明式定义流转：

```csharp
protected override void ConfigureStateMachine(BusinessStateMachine<JObject, SalesOrderStatus> sm)
{
    sm.Permit(Draft, Submitted, "Submit", guard: ctx => { /* 校验 */ })
      .Permit(Submitted, Audited, "Audit")
      .OnEnter(Audited, ctx => { ctx.Entity["AuditorId"] = ...; return Task.CompletedTask; });
}
```

调用 `ExecuteTriggerAsync(param)`（`param.Trigger = "Audit"`）即可：加载单据 → 守卫校验 → `OnExit` → 触发钩子 → 写状态 → `OnEnter` → 持久化。

### 3. 实体 CRUD 生命周期（`BusinessServiceBase`）

`AddAsync/UptAsync/DelAsync/GetListAsync` 内置 `OnBeforeXxx/OnAfterXxx` 钩子（类似表单 V8 事件），子类按需重写。底层复用平台 `MicroiEngine.FormEngine`。

## 接入到主站点（`Microi.net.Api`）

### 步骤 1：在 `Microi.net.Api.csproj` 增加项目引用

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\ErpModule\Microi.Erp.csproj" />
  <ProjectReference Include="..\..\src\MesModule\Microi.Mes.csproj" />
</ItemGroup>
```

> 引用 ERP/MES 即可，`Common/Core/Model` 会随依赖自动带入。

### 步骤 2：在 `Program.cs` 装配与启动

```csharp
// builder.Services 阶段（与 services.AddMicroiJob(...) 同区域）
services.AddMicroiBusiness();

// app = builder.Build() 之后（与 app.UseMicroiJob() 同区域）
app.UseMicroiBusiness();
```

`AddMicroiBusiness` 会自动发现模块、调用其 `ConfigureServices`，并把模块程序集里的 Controller 注册为 ApplicationPart，使 `api/SalesOrder/*`、`api/WorkOrder/*` 等接口可被路由发现。

### 步骤 3：将项目加入解决方案（可选，便于 IDE 管理）

```powershell
dotnet sln Microi.Server/Microi.net.sln add `
  src/Model/Microi.Business.Model.csproj `
  src/PubilcModule/Microi.Business.Core.csproj `
  src/ComonBusiness/Microi.Business.Common.csproj `
  src/ErpModule/Microi.Erp.csproj `
  src/MesModule/Microi.Mes.csproj
```

## 数据表约定与自动建表（Code-First）

**无需手动建表**：框架启动时会根据实体自动建表/补列。

### 工作机制

1. 实体类标注 `[BusinessTable("表名")]`，继承 `BusinessEntity` / `BusinessStatefulEntity<TState>`。
2. 启动时（`UseMicroiBusiness` → 模块"启动前"阶段），`BusinessSchemaInitializer` 扫描每个 `AutoMigrate=true` 模块程序集中的 `[BusinessTable]` 实体：
   - 表不存在 → 调用平台 `IMicroiORM.AddDiyTable` 建表（自带系统字段 `Id/CreateTime/UpdateTime/UserId/UserName/IsDeleted`）；
   - 表已存在但缺列 → 调用 `AddColumn` 增量补列。
3. 列类型由 `SqlTypeMapper` 按 CLR 类型 + 数据库方言推断（支持 MySQL/SqlServer/Oracle/PostgreSQL/达梦/人大金仓），也可用 `[BusinessColumn(Type="decimal(18,2)", NotNull=true, Label="金额")]` 显式指定。
4. 幂等：可重复启动，不会重复建表/建列。

```csharp
[BusinessTable("erp_sales_order", Comment = "ERP-销售订单")]
public class SalesOrder : BusinessStatefulEntity<SalesOrderStatus>
{
    public string CustomerId { get; set; }                       // 自动推断 varchar(255)
    [BusinessColumn(Type = "decimal(18,2)", Label = "金额")]
    public decimal? TotalAmount { get; set; }
    [BusinessColumn(Ignore = true)]
    public string TempOnly { get; set; }                          // 不建列
}
```

### 配置项

```csharp
services.AddMicroiBusiness(opt =>
{
    opt.AutoMigrate = true;                 // 全局开关，默认 true
    // opt.MigrateOsClients.Add("tenantA"); // 指定租户；为空=主租户
});
```

- 单模块可重写 `public override bool AutoMigrate => false;` 关闭自动建表。
- 多租户独立库：新建租户后可手动调用 `new BusinessSchemaInitializer().EnsureSchema(entityTypes, osClient)`。

### 手动建表（可选）

如需手动控制，仍提供脚本 `src/sql/business_tables.mysql.sql`。
建表后可在低代码平台执行"加载非 diy 表"或 `FormEngine.AddTable` 注册元数据，以便在表单引擎中可视化管理。

## 主表 / 明细表 / 扩展表 与动态加字段

每个业务文档支持「主表 + 明细表(1:N) + 扩展表(1:1)」结构，且可在前端可视化查看结构并动态加字段。

### 声明关系

```csharp
[BusinessTable("erp_sales_order", Comment = "ERP-销售订单")]
[BusinessExtensionTable(typeof(SalesOrderExt))]                          // 1:1 扩展表（同 Id）
[BusinessDetailTable(typeof(SalesOrderItem), "OrderId", PropertyName = "Items")] // 1:N 明细表
public class SalesOrder : BusinessStatefulEntity<SalesOrderStatus> { ... }

[BusinessTable("erp_sales_order_ext", Comment = "ERP-销售订单扩展")]
public class SalesOrderExt : BusinessEntity { ... }                       // 自定义字段落在这里

[BusinessTable("erp_sales_order_item", Comment = "ERP-销售订单明细")]
public class SalesOrderItem : BusinessEntity { public string OrderId { get; set; } ... }
```

明细表、扩展表本身也是 `[BusinessTable]`，启动时一并自动建表。

### 读取时自动合并

服务重写 `EntityType` 后，`GetModelWithRelationsAsync` 会：
- 把扩展表(同 Id)的列合并进主对象；
- 把明细集合挂到主对象的 `PropertyName`（如 `Items`）。

```csharp
protected override Type EntityType => typeof(SalesOrder);
// 控制器：api/SalesOrder/GetModelWithRelations  →  返回含 Items[] 与扩展字段的主单
```

### 结构查看 + 动态加字段（前端页面）

- 页面：`http://<站点>/business-schema.html`（自包含，Vue3 + Element Plus CDN）。
- 顶部填入 `Authorization Token`（与 `OsClient`），即可：
  - 左侧列出所有业务文档（主表）；
  - 右侧查看主/明细/扩展表的实时列结构；
  - 「添加字段」可选择**合并到主表 / 某明细表 / 扩展表**，提交即通过平台多方言 DDL 真实加列（幂等）。

### 结构 API（`api/BusinessSchema/*`）

| Action | 说明 |
|--------|------|
| `GetDocuments` | 列出所有业务文档（主表） |
| `GetDocumentSchema` | 入参 `MasterTable`，返回主+明细+扩展的完整结构与列 |
| `GetTableColumns` | 入参 `TableName`，返回单表列结构 |
| `AddField` | 入参 `MasterTable/TargetTable/FieldName/DataType/Length/RawType/NotNull/Label`，向目标表加列 |

`DataType` 预设：`string/text/int/long/decimal/double/bool/datetime/raw`（`raw` 用 `RawType` 指定原始 SQL 类型）。

## 新增一个业务模块（如 WMS）

1. 新建 `src/WmsModule/Microi.Wms.csproj`，引用 `Microi.Business.Common`。
2. 实体继承 `BusinessStatefulEntity<TState>`，定义状态枚举。
3. 服务继承 `BusinessStatefulServiceBase<TParam, TState>`，实现 `TableKey` 与 `ConfigureStateMachine`。
4. 控制器继承 `BusinessControllerBase`。
5. 新建 `WmsModule : BusinessModuleBase`。
6. 在 API 项目引用该模块——**无需改动内核代码**，启动时自动装配。

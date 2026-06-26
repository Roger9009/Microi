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

重写 `EntityType` 后，可调用 `SaveWithRelationsAsync(JObject masterData, string osClient)`：
- 主单按 `Id` 是否存在自动 insert/update；
- 一对一扩展表按同 `Id` upsert；
- 一对多明细表按传入集合做 insert/update/delete 同步。

对应控制器示例：`api/SalesOrder/Save`，入参为完整 JSON（含 `Items` 明细与扩展字段）。

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

### 动态关系绑定（无需改 C# 代码）

除代码特性外，还可通过前端 **直接新建扩展表/明细表并绑定到主单**，关系持久化到 `business_doc_relation` 表。

两种来源**自动合并**：`GetDocumentSchema`、保存关系、级联删除均同时处理静态（代码特性）和动态（DB 记录）关系，互不干扰。

```js
// 前端调用示例：绑定扩展表
POST api/BusinessSchema/BindRelation
{ "MasterTable": "erp_sales_order", "RelationTable": "erp_sales_order_invoice",
  "RelationType": "Extension", "Label": "发票信息" }

// 绑定明细表
POST api/BusinessSchema/BindRelation
{ "MasterTable": "erp_sales_order", "RelationTable": "erp_sales_fee_item",
  "RelationType": "Detail", "ForeignKey": "OrderId", "PropertyName": "FeeItems", "Label": "费用明细" }

// 解绑（使用 GetDocumentSchema 返回的 RelationId）
POST api/BusinessSchema/UnbindRelation
{ "RelationId": "xxx", "MasterTable": "erp_sales_order" }
```

### 结构查看 + 动态加字段（前端页面）

管理入口三个页面均为**纯静态 HTML**（Vue3 + Element Plus CDN），零构建直接部署到 `wwwroot` 使用：

| 页面 | 地址 | 功能 |
|------|------|------|
| 🔐 登录页 | `/business-login.html` | bizadmin 独立登录（默认密码 `Admin@123`）、在线改密 |
| 🗂️ 结构管理 | `/business-schema.html` | 查看结构、加字段、**新建/绑定/解绑关联表**、字段配置（含导出/导入） |
| 📄 单据保存 | `/business-document.html` | 动态加载表单，新增/编辑含关联表的完整单据 |

访问 `business-schema.html` 时：
- 若已通过 `business-login.html` 登录（bizadmin），Token 会从 localStorage 自动读取，无需再手填；
- 动态绑定的关联表会显示「**动态绑定**」Tag，并提供「**解绑**」按钮（静态关系不可解绑）；
- 「**+ 新建/绑定关联表**」按钮可直接在前端创建扩展表或明细表并绑定到当前文档。

### 结构 API（`api/BusinessSchema/*`）

| Action | 说明 |
|--------|------|
| `GetDocuments` | 列出所有业务文档（主表） |
| `GetDocumentSchema` | 入参 `MasterTable`，返回主+明细+扩展（含动态关系）的完整结构与列；`BusinessTableInfo.IsDynamic=true` 表示动态绑定 |
| `GetTableColumns` | 入参 `TableName`，返回单表列结构 |
| `AddField` | 入参 `MasterTable/TargetTable/FieldName/DataType/Length/RawType/NotNull/Label`，向目标表加列 |
| `GetFieldConfigs` | 入参 `TableName`，返回物理列 + 字段配置合并后的已解析字段 |
| `SaveFieldConfigs` | 入参 `TableName` + `Fields[]`，批量保存字段配置（按 `TableName+FieldName` upsert） |
| `DeleteFieldConfig` | 入参 `TableName` + `FieldName`，删除某字段配置 |
| `ExportFieldConfigs` | 入参 `TableName`，导出字段配置 JSON（可跨环境迁移） |
| `ImportFieldConfigs` | 入参 `TableName` + `Configs[]`，按 `TableName+FieldName` upsert 导入字段配置 |
| **`BindRelation`** | 入参 `MasterTable/RelationTable/RelationType(Extension\|Detail)/ForeignKey/PropertyName/Label`，动态绑定关联表到主文档 |
| **`UnbindRelation`** | 入参 `RelationId`（`business_doc_relation.Id`）+ `MasterTable`，解除动态关系绑定 |

### 单据保存示例（前端页面）

- 页面：`http://<站点>/business-document.html`（自包含，Vue3 + Element Plus CDN）。
- 选择业务模块（SalesOrder / WorkOrder）、输入单据 Id（留空为新增），即可：
  - 通过 `GetModelWithRelations` 加载主单 + 扩展字段 + 明细 Items；
  - 编辑主单字段、扩展字段、增删明细行；
  - 调用 `api/SalesOrder/Save` 或 `api/WorkOrder/Save` 完成主-细-扩展表一并落库。
- 保存后端默认开启事务：主单与关系表要么全部成功，要么全部回滚。

### 字段配置与更新时忽略字段

`BusinessServiceBase.UptAsync` 默认启用 `EnforceFieldConfigOnUpt=true`，更新前会自动读取 `business_field_config` 中 `IsUpdate=false` 的字段，并加入 `param._NotSaveField`，使这些字段不会被更新。`BusinessParam._NotSaveField` 已对外开放，业务服务也可手动追加。

`DataType` 预设：`string/text/int/long/decimal/double/bool/datetime/raw`（`raw` 用 `RawType` 指定原始 SQL 类型）。

### 字段配置导出 / 导入

```js
// 导出（在迁移前备份）
GET api/BusinessSchema/ExportFieldConfigs?TableName=erp_sales_order&OsClient=demo
// → { Code:1, Data: [ { TableName, FieldName, Label, Component, ... } ] }

// 导入（在目标环境执行）
POST api/BusinessSchema/ImportFieldConfigs
{ "TableName": "erp_sales_order", "Configs": [ ... ], "OsClient": "demo" }
```

### 登录鉴权 API（`api/BusinessAuth/*`）

| Action | 说明 |
|--------|------|
| `Login` | `{ OsClient, Username("bizadmin"), Password }` → `{ Token }` |
| `Verify` | `{ OsClient, Token }` → `{ Code:1 }` 有效 / `{ Code:0 }` 过期 |
| `SetPassword` | `{ OsClient, Token, OldPassword, NewPassword }` → 修改密码 |
| `Logout` | `{ OsClient, Token }` → Token 立即失效 |

> 账号体系：密码 SHA256 哈希存 Redis Hash `Microi:{osClient}:BizAdmin`；Token 以 Unix 时间戳为过期标记，24h 自动失效；支持多租户（OsClient 隔离）。

### 动态关系表（`business_doc_relation`）

由框架启动时自动建表，字段说明：

| 字段 | 类型 | 说明 |
|------|------|------|
| `MasterTable` | varchar | 主表名（如 `erp_sales_order`） |
| `RelationTable` | varchar | 关联表名 |
| `RelationType` | varchar | `Extension`（1:1）/ `Detail`（1:N） |
| `ForeignKey` | varchar | 明细表外键列名（Detail 时有值） |
| `PropertyName` | varchar | JSON 集合属性名（Detail 时有值） |
| `Label` | varchar | 显示名称 |

---

## 新增一个业务模块（如 WMS）

1. 新建 `src/WmsModule/Microi.Wms.csproj`，引用 `Microi.Business.Common`。
2. 实体继承 `BusinessStatefulEntity<TState>`，定义状态枚举。
3. 服务继承 `BusinessStatefulServiceBase<TParam, TState>`，实现 `TableKey` 与 `ConfigureStateMachine`。
4. 控制器继承 `BusinessControllerBase`。
5. 新建 `WmsModule : BusinessModuleBase`。
6. 在 API 项目引用该模块——**无需改动内核代码**，启动时自动装配。

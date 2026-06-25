# Devin 交接笔记：Microi 业务底座开发

> 记录业务底座（ERP/MES）模块开发过程中踩过的坑、特殊配置、以及待办事项，方便后续维护与继续开发。

---

## 1. 项目背景

在 Microi 吾码低代码平台之上，构建一个可热插拔的「业务底座」模块：

- **目录**：`d:\code\Microi\src\`
- **核心程序集**：
  - `Microi.Business.Model`：共享模型（`BusinessParam`、`BusinessEntity`、关系特性）
  - `Microi.Business.Core`：内核（`BusinessServiceBase`、状态机、表结构服务、字段配置）
  - `Microi.Business.Common`：公共控制器基类
  - `Microi.Erp` / `Microi.Mes`：ERP、MES 示例模块
- **前端页面**：`Microi.Server\Microi.net.Api\wwwroot\business-schema.html`、`business-document.html`

---

## 2. 已实现的重大功能

1. **字段配置系统**
   - `business_field_config` 表存储字段级配置（描述、语言 ID、类型、组件、是否更新、隐藏、默认显示、必填、排序等）。
   - `BusinessFieldConfigService` / `BusinessFieldConfigCache` 负责解析与缓存。
   - `BusinessParam._NotSaveField` 对外开放，更新时 `BusinessServiceBase` 自动把 `IsUpdate=false` 的字段加入 `_NotSaveField`。
   - 前端 `business-schema.html` 可对每个表做「字段配置」内联编辑。

2. **主-细-扩展表保存**
   - `BusinessDocumentWriter` 提供 `SaveAsync` 与 `SaveRelationsAsync`。
   - `BusinessServiceBase.SaveWithRelationsAsync(JObject, osClient)` 流程：
     1. `JObject → TParam`；
     2. 走 `AddAsync`/`UptAsync`（运行生命周期钩子，如生成单据号）；
     3. 自动创建 `DbTrans` 事务；
     4. 同步扩展表（同 Id upsert）与明细表（insert/update/delete）。
   - 控制器示例：`api/SalesOrder/Save`、`api/WorkOrder/Save`。

3. **业务开发前端**
   - `business-schema.html`：表结构查看、动态加字段、字段配置。
   - `business-document.html`：单据保存示例（SalesOrder/WorkOrder）。

---

## 3. 踩坑记录

### 3.1 `_NotSaveField` 编译问题

**现象**：`BusinessServiceBase.UptAsync` 中访问 `param._NotSaveField` 时报 `TParam` 不包含该定义。

**原因**：`_NotSaveField` 在平台 `DiyTableRowParam` 中存在，但业务参数 `BusinessParam` 继承的是 `BaseParam`，未包含该属性。

**解决**：在 `BusinessParam` 中显式添加：

```csharp
public System.Collections.Generic.List<string> _NotSaveField { get; set; }
```

**文件**：`src\Model\Param\BusinessParam.cs`

---

### 3.2 `FormEngine` 返回 `Data` 类型不统一

**现象**：新增/更新后通过 `masterResult.Data?.Id` 取不到 Id。

**原因**：`DosResult.Data` 是 `object`，实际可能是 `JObject`、`dynamic`、`DapperRow` 或匿名对象。

**解决**：统一转换：

```csharp
var masterObj = masterResult.Data as JObject ?? JObject.FromObject(masterResult.Data);
var id = masterObj["Id"]?.ToString();
```

**文件**：`BusinessDocumentWriter.cs`、`BusinessServiceBase.SaveWithRelationsAsync`

---

### 3.3 `JObject → TParam` 会丢失动态字段

**现象**：`SaveWithRelationsAsync` 接收完整 JSON（含 `Items` 明细、扩展字段），但 `AddAsync`/`UptAsync` 只保存 `TParam` 中的属性。

**原因**：`JObject.ToObject<TParam>()` 只能映射 `TParam` 已声明的属性，动态字段需从原始 `JObject` 中单独处理。

**解决**：
- 主单保存用 `TParam`（运行生命周期钩子）；
- 扩展表、明细表保存仍使用原始 `JObject`，通过 `BusinessDocumentWriter.SaveRelationsAsync` 读取 `Items` 等集合。

**代价**：如果主表有未声明在 `TParam` 中的字段，保存时会丢失。业务模块需保持 `TParam` 与主表列同步。

---

### 3.4 事务生命周期与 `DbTrans.Close()`

**现象**：担心 `Rollback`/`Commit` 后连接未释放。

**原因**：`Dos.ORM.DbTrans.Commit()` 与 `Rollback()` 内部已经调用 `Close()`，但若直接 `return` 而不显式回滚，可能导致事务悬挂。

**解决**：
- 成功路径：`trans.Commit()`（内部 Close）；
- 失败路径：`trans.Rollback()`；
- `finally` 中再调用 `trans?.Close()` 兜底。

**文件**：`BusinessServiceBase.SaveWithRelationsAsync`

---

### 3.5 前端页面组件命名与 CDN 版本

**现象**：`business-schema.html` / `business-document.html` 使用 Element Plus CDN，需确保组件名与版本一致。

**注意**：
- Vue 3 全局构建：`Vue.createApp`；
- Element Plus 全局对象：`ElementPlus`；
- `el-switch` 默认绑定布尔值，需保证初始值为布尔，避免字符串 `"true"`/`"false"` 导致异常。

---

## 4. 特殊配置

### 4.1 业务表自动建表

- 实体标注 `[BusinessTable("表名", Comment = "...")]`；
- 启动时 `UseMicroiBusiness()` 会自动扫描 `AutoMigrate=true` 的模块，调用 `IMicroiORM.AddDiyTable` / `AddColumn`；
- 多租户独立库：可手动调用 `BusinessSchemaInitializer.EnsureSchema`。

### 4.2 字段配置缓存失效

- `BusinessFieldConfigCache` 是内存缓存，Key：`{osClient}|{tableName}`（小写）。
- 保存/删除配置后调用 `Invalidate(osClient, tableName)` 使其失效。
- 目前缓存不会自动过期；若多实例部署，需通过 Redis 或事件机制同步失效。

### 4.3 控制器鉴权与上下文

- `BusinessControllerBase` 使用 `[Authorize]` + `DiyToken.GetCurrentToken()`；
- 对于 `JObject` 入参的新接口，新增 `GetCurrentContext()` 获取 `(OsClient, JObject CurrentUser)`；
- 传统 `BusinessParam` 接口仍使用 `await FillContext(param)`。

---

## 5. 遗留待开发需求

### 5.1 高优先级

- [ ] **单元测试**：为 `BusinessDocumentWriter.SaveRelationsAsync`、`SaveWithRelationsAsync` 编写单元测试或集成测试，验证：
  - 新增 SalesOrder 时自动生成 BillNo；
  - 更新时 Items 的新增/更新/删除同步；
  - 事务回滚场景。
- [ ] **生产环境事务验证**：`DbTrans` 跨 MySQL/SqlServer/Oracle/PostgreSQL/达梦/人大金仓的一致性需在实际数据库上跑通。
- [ ] **前端字段配置保存后的刷新**：`business-schema.html` 字段配置保存后，当前页面已刷新，但左侧文档列表未联动刷新。

### 5.2 中优先级

- [ ] **通用单据编辑器**：`business-document.html` 当前只 hard-code 了 SalesOrder/WorkOrder 字段。应改为根据 `GetDocumentSchema` 动态渲染表单，支持任意业务单据。
- [ ] **字段配置默认值持久化**：当前字段配置未保存时，由 `BusinessFieldConfigService.BuildDef` 推断默认值，但首次保存后这些默认值会被写死。需确认这是预期行为。
- [ ] **WorkOrder 扩展示例**：目前 WorkOrder 只有主表，可添加 `WorkOrderExt` 扩展表与 `WorkOrderItem` 明细表，完整演示主-细-扩展。

### 5.3 低优先级

- [ ] **主表与扩展表字段命名冲突**：`BusinessDocumentReader` 在扩展表字段与主表字段同名时不会覆盖主表；保存时也可能把主表字段误写入扩展表。建议约定扩展表字段加前缀或进一步检测冲突。
- [ ] **前端字段配置的 UI 增强**：当前为表格内联编辑，字段多时横向滚动；后续可改为弹窗表单或分组卡片。
- [ ] **删除主单时级联删除关系表**：目前 `DelAsync` 只删除主单，未清理扩展表/明细表，可能产生孤儿数据。

---

## 6. 常用命令

```powershell
# 编译 API 项目（推荐每次修改后执行）
dotnet build Microi.Server/Microi.net.Api/Microi.net.Api.csproj -c Debug --nologo

# 将业务模块加入解决方案（如新增 WMS）
dotnet sln Microi.Server/Microi.net.sln add src/WmsModule/Microi.Wms.csproj
```

---

## 7. 关键文件速查

| 文件 | 作用 |
|------|------|
| `src\Model\Param\BusinessParam.cs` | 业务参数基类，含 `_NotSaveField` |
| `src\PubilcModule\Service\BusinessServiceBase.cs` | CRUD 生命周期 + `SaveWithRelationsAsync` |
| `src\PubilcModule\Schema\BusinessDocumentWriter.cs` | 主-细-扩展表保存写入器 |
| `src\PubilcModule\Schema\BusinessDocumentReader.cs` | 主-细-扩展表读取合并器 |
| `src\PubilcModule\Schema\BusinessFieldConfigService.cs` | 字段配置解析与保存 |
| `src\PubilcModule\Schema\BusinessFieldConfigCache.cs` | 字段配置内存缓存 |
| `src\ComonBusiness\Web\BusinessSchemaController.cs` | 表结构 + 字段配置 API |
| `src\ComonBusiness\Web\BusinessControllerBase.cs` | 控制器基类 + `GetCurrentContext` |
| `src\ErpModule\SalesOrder\SalesOrderController.cs` | `Save` 接口示例 |
| `Microi.Server\Microi.net.Api\wwwroot\business-schema.html` | 业务开发模块前端 |
| `Microi.Server\Microi.net.Api\wwwroot\business-document.html` | 单据保存示例前端 |

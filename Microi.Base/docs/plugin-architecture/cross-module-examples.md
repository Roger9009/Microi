# 跨模块调用规范 — 正反示例

> 插件架构下跨模块通信的唯一合法途径是 **DI 注入接口**。

---

## ✅ 正确做法：DI 构造函数注入接口

### MES 插件调用 ERP 插件

```csharp
// ====== 在 MES 插件中调用 ERP 的销售订单服务 ======

// 1. 契约层已定义接口（Microi.Platform.Contracts/IErp/ISalesOrderService.cs）
public interface ISalesOrderService : IBatchQueryable<SalesOrderDto>
{
    Task<SalesOrderDto> GetByIdAsync(string id);
    Task<BatchResult> BatchCreateAsync(BatchCreateOrderRequest request);
}

// 2. MES 插件通过 DI 构造函数注入接口（不是具体类）
public sealed class WorkOrderServiceImpl : IWorkOrderService
{
    // ✅ 正确：注入接口
    private readonly ISalesOrderService _salesOrderService;

    public WorkOrderServiceImpl(ISalesOrderService salesOrderService)
    {
        _salesOrderService = salesOrderService; // 可为 null（ERP 未加载时）
    }

    // ✅ 正确：通过接口调用，只依赖契约层
    public async Task<WorkOrderReport> GenerateReportAsync()
    {
        if (_salesOrderService == null)
            throw new InvalidOperationException("ERP 插件未加载");

        // 使用流式分页遍历大量数据（不一次性加载到内存）
        decimal total = 0;
        await foreach (var order in _salesOrderService.QueryStreamAsync(500))
        {
            total += order.TotalAmount;
        }
        return new WorkOrderReport { TotalSalesAmount = total };
    }
}
```

### ERP 插件调用 MES 插件

```csharp
// ====== 在 ERP 插件中调用 MES 的工单服务 ======

public sealed class SalesOrderServiceImpl : ISalesOrderService
{
    // ✅ 正确：注入接口
    private readonly IWorkOrderService _workOrderService;

    public SalesOrderServiceImpl(IWorkOrderService workOrderService)
    {
        _workOrderService = workOrderService;
    }

    // ✅ 正确：批量分页调用，非循环单条
    public async Task<bool> HasRelatedWorkOrders(string orderId)
    {
        if (_workOrderService == null) return false;

        var page = 1;
        const int pageSize = 100;
        while (true)
        {
            var result = await _workOrderService.QueryPagedAsync(page, pageSize);
            if (result.Items.Any(wo => wo.WorkOrderNo.Contains(orderId)))
                return true;
            if (!result.HasMore) break;
            page++;
        }
        return false;
    }
}
```

---

## ❌ 错误做法（禁止）

### 反例 1：直接 new 其他插件实现

```csharp
// ❌ 禁止：直接 new 其他插件的具体实现类
public sealed class WorkOrderServiceImpl : IWorkOrderService
{
    public async Task DoSomething()
    {
        // ❌ 违反了跨模块通信规则
        // 1. 直接引用了 Microi.Plugin.Erp 的命名空间（编译错误——插件项目没有引用该项）
        // 2. 绕过了 DI 容器
        // 3. 无法获知 ERP 插件是否已加载
        var erpService = new Microi.Plugin.Erp.Services.SalesOrderServiceImpl(null);
        await erpService.GetByIdAsync("abc");
    }
}
```

### 反例 2：静态引用其他插件程序集

```csharp
// ❌ 禁止：在 .csproj 中添加对其他插件项目的 ProjectReference
// <ProjectReference Include="..\Microi.Plugin.Erp\Microi.Plugin.Erp.csproj" />

// ❌ 禁止：使用 Assembly.LoadFrom 动态加载其他插件 DLL
var erpAssembly = Assembly.LoadFrom("../Microi.Plugin.Erp.dll");
```

### 反例 3：循环单条查询大量数据

```csharp
// ❌ 禁止：循环中逐条查询数据库
public async Task<List<SalesOrderDto>> GetAllOrdersAsync()
{
    var result = new List<SalesOrderDto>();
    var ids = await GetAllOrderIdsAsync(); // 假设 100 万条
    foreach (var id in ids)                 // 100 万次数据库往返
    {
        var order = await GetByIdAsync(id);
        result.Add(order);
    }
    return result; // 100 万条在内存中
}

// ✅ 应使用流式分页
public async IAsyncEnumerable<SalesOrderDto> GetAllOrdersStreamAsync()
{
    await foreach (var order in QueryStreamAsync(1000))
    {
        yield return order;
    }
}
```

### 反例 4：插件内静态缓存大量业务数据

```csharp
// ❌ 禁止：静态字典/缓存存储大量业务对象
public sealed class WorkOrderServiceImpl : IWorkOrderService
{
    private static readonly Dictionary<string, WorkOrderDto> _cache = new();
    // 100 万条工单 → 内存爆炸 → GC 压力 → 24 小时车间不稳定
}
```

---

## 📐 依赖拓扑

```
┌─────────────────────────────────────────────────────┐
│  Microi.Plugin.Erp     Microi.Plugin.Mes            │
│  (引用 Base+Contracts)  (引用 Base+Contracts)        │
│        │                      │                     │
│        └──────┬───────────────┘                    │
│               ↓ (DI 注入接口)                       │
│  ┌────────────────────────────┐                    │
│  │ Microi.Platform.Contracts   │  ← 仅 DTO + 接口  │
│  └────────────┬───────────────┘                    │
│               ↓                                     │
│  ┌────────────────────────────┐                    │
│  │ Microi.Platform.Base        │  ← NuGet 收敛      │
│  └────────────────────────────┘                    │
│               ↑                                     │
│  ┌────────────────────────────┐                    │
│  │ Microi.Platform.Host        │  ← 仅引 Base+Cont  │
│  └────────────────────────────┘                    │
└─────────────────────────────────────────────────────┘
```

# 大批量生产数据性能优化清单

> 适配 24 小时车间稳定运行，面向 10 万 ~ 1000 万行数据量。

---

## 1. 批量写入

| # | 规则 | ✅/❌ |
|---|------|:----:|
| 1.1 | 批量插入使用 `INSERT INTO (...) VALUES (...),(...),(...)` 或 `SqlBulkCopy` | ✅ |
| 1.2 | 单批次 500~2000 行，避免单条 INSERT 循环 | ✅ |
| 1.3 | 事务粒度控制在单批次，避免跨批次长事务锁表 | ✅ |
| 1.4 | 禁用 `for` / `foreach` 内 `await InsertSingleAsync()` | ❌ |

```csharp
// ✅ 正确：批量 INSERT
public Task<BatchResult> BatchCreateAsync(BatchCreateOrderRequest request)
{
    // 使用 SqlBulkCopy 或批量 VALUES
    // INSERT INTO SalesOrder (...) VALUES (@p1,@p2,...),(@p3,@p4,...),...
}

// ❌ 错误：循环单条
public async Task CreateMany(List<SalesOrderDto> orders)
{
    foreach (var o in orders)
        await InsertSingleAsync(o);  // N 次数据库往返
}
```

---

## 2. 流式读取（IAsyncEnumerable）

| # | 规则 | ✅/❌ |
|---|------|:----:|
| 2.1 | 全量遍历使用 `IAsyncEnumerable<T>` 流式读取 | ✅ |
| 2.2 | 数据库端使用 `DbDataReader` 游标，不缓冲全部行 | ✅ |
| 2.3 | 批量大小 (batchSize) 在 500~5000 之间 | ✅ |
| 2.4 | 禁用 `ToList()` / `ToArray()` 一次性全量加载到内存 | ❌ |

```csharp
// ✅ 正确：流式读取（数据库游标）
public async IAsyncEnumerable<SalesOrderDto> QueryStreamAsync(int batchSize = 1000)
{
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        yield return MapFromReader(reader);
    }
}

// ❌ 错误：全量加载到内存
public async Task<List<SalesOrderDto>> GetAllOrdersAsync()
{
    var allRows = await db.QueryAsync<SalesOrderDto>("SELECT * FROM SalesOrder");
    return allRows.ToList(); // 100 万行全在内存
}
```

---

## 3. 分页查询

| # | 规则 | ✅/❌ |
|---|------|:----:|
| 3.1 | 所有列表查询返回 `PagedResult<T>`，不返回裸 `List<T>` | ✅ |
| 3.2 | 数据库端使用 `OFFSET ... FETCH NEXT` 或 `Keyset Pagination` | ✅ |
| 3.3 | 大偏移量场景使用游标分页（`WHERE Id > @lastId`）代替 `OFFSET` | ✅ |
| 3.4 | 前端禁止一次性请求全部页（必须按需翻页） | ✅ |

---

## 4. 内存与 GC

| # | 规则 | ✅/❌ |
|---|------|:----:|
| 4.1 | 插件禁止持有静态 `Dictionary<T>` / `ConcurrentDictionary<T>` 缓存大量业务数据 | ✅ |
| 4.2 | 缓存仅用于配置/元数据（如字段映射表），数据量 < 1000 条 | ✅ |
| 4.3 | 多线程写操作使用 `lock` 或 `Channel<T>`，禁止无锁共享可变状态 | ✅ |
| 4.4 | 24 小时运行场景必须定期检查内存趋势，确保无泄漏 | ✅ |

---

## 5. 启动性能

| # | 规则 | ✅/❌ |
|---|------|:----:|
| 5.1 | 插件 DLL 仅启动时加载一次，无运行期 Assembly.Load | ✅ |
| 5.2 | 禁用可回收 AssemblyLoadContext（避免 GC 卡顿） | ✅ |
| 5.3 | `ConfigureServices` 仅做 DI 注册，不做数据库查询 | ✅ |
| 5.4 | 插件实例化使用惰性创建（`Lazy<T>` 或首次调用时创建） | ✅ |

---

## 6. 24 小时车间稳定性

| # | 规则 | ✅/❌ |
|---|------|:----:|
| 6.1 | 所有 `try-catch` 不吞异常，至少记录日志 | ✅ |
| 6.2 | 批量操作失败不影响剩余批次（单批失败继续下一批） | ✅ |
| 6.3 | 连接池复用，禁止每次请求新建连接 | ✅ |
| 6.4 | 长事务超时设置（`CommandTimeout`） | ✅ |
| 6.5 | 插件停止逻辑通过宿主生命周期管理，插件内部无 `Dispose` 资源 | ✅ |

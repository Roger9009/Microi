# 业务底座 src 框架架构全景图

> 重构日期：2026-07-11 | 编译状态：0 错误

---

## 一、分层架构

```
┌─────────────────────────────────────────────────────────────┐
│                       src/ 目录结构                          │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Layer 1 — Model（实体基座）                                  │
│  ├── Model/                         Microi.Business.Model    │
│                                                              │
│  Layer 2 — Core（公共核心）                                   │
│  ├── PubilcModule/                  Microi.Business.Core     │
│  │   ├── Lifecycle/                模块生命周期              │
│  │   ├── Schema/                   Schema 服务               │
│  │   ├── Plugin/                   插件系统                  │
│  │   └── ServiceBase.cs                                     │
│                                                              │
│  Layer 3 — Common（通用业务）                                 │
│  ├── ComonBusiness/                 Microi.Business.Common   │
│  │   ├── Web/                      控制器 + API              │
│  │   ├── BillNo/                   单据编号                  │
│  │   └── Schema/                   Schema API               │
│                                                              │
│  Layer 4 — 生产模块（编译引用）                                │
│  ├── ErpModule/                     Microi.Erp              │
│  └── MesModule/                     Microi.Mes              │
│                                                              │
│  Layer 5 — 独立 DLL 插件（一业务一 DLL）                      │
│  ├── Microi.Plugin.Demo.SalesOrder/ → SalesOrder.dll        │
│  ├── Microi.Plugin.Demo.WorkOrder/   → WorkOrder.dll        │
│  ├── Microi.Plugin.Erp/             → Erp.dll               │
│  ├── Microi.Plugin.Mes/             → Mes.dll               │
│  ├── Microi.Platform.Base/          NuGet 收敛               │
│  ├── Microi.Platform.Contracts/     DTO + 接口               │
│  └── Microi.Platform.Host/          宿主加载                 │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 二、千级程序场景：独立 DLL 替换流程

```
总控台 → 插件管理 → 找到目标插件 → [停止] → [卸载]
   ↓
替换 DLL 文件（plugins/ 目录下）
   ↓
重启应用（或手动 [启动]）
   ↓
新 DLL 自动注册服务 → 恢复运行
```

每个客户可定制自己的 DLL：

```
plugins/
├── SalesOrder.dll          ← 标准版 v1.0
├── SalesOrder.customer-a.dll ← 客户A定制版 v1.1（额外字段）
├── WorkOrder.dll           ← 标准版 v1.0
└── WorkOrder.customer-b.dll ← 客户B定制版 v2.0（特殊流程）
```

---

## 三、API 端点汇总

| 路由前缀 | 功能 |
|---------|------|
| `api/BusinessDoc/` | 通用 CRUD |
| `api/BusinessSchema/` | Schema 管理 |
| `api/BusinessMonitor/` | 模块监控 |
| `api/BusinessAuth/` | 业务认证 |
| `api/BusinessBase/` | 总控台 + 健康检查 |
| `api/BusinessBase/Plugin/` | 插件管理（启停/日志/替换） |
| `api/SalesOrder/` | 销售订单（Demo 插件） |
| `api/WorkOrder/` | 工单（Demo 插件） |

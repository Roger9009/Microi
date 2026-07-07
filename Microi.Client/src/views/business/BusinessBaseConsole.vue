<template>
    <div class="business-base-console">
        <div class="header">
            <h3>业务底座总控台</h3>
            <el-button type="primary" @click="refreshAll" :loading="loading">
                <el-icon><Refresh /></el-icon> 刷新
            </el-button>
        </div>

        <el-tabs v-model="activeTab" type="border-card">
            <!-- ════════════════════════════════════════════ -->
            <!-- Tab 1: 仪表盘概览 -->
            <!-- ════════════════════════════════════════════ -->
            <el-tab-pane label="仪表盘概览" name="overview">
                <!-- 状态统计卡片 -->
                <el-row :gutter="16" style="margin-bottom:16px;">
                    <el-col :span="6">
                        <el-card shadow="hover" @click="$router.push('/business/monitor')" style="cursor:pointer;">
                            <div style="text-align:center;padding:12px 0;">
                                <div style="font-size:28px;font-weight:700;color:#409eff;">{{ dashboard.ModuleStats?.Total || 0 }}</div>
                                <div style="font-size:13px;color:#909399;">业务模块</div>
                            </div>
                        </el-card>
                    </el-col>
                    <el-col :span="6">
                        <el-card shadow="hover" @click="$router.push('/business/monitor')" style="cursor:pointer;">
                            <div style="text-align:center;padding:12px 0;">
                                <div style="font-size:28px;font-weight:700;color:#67c23a;">{{ dashboard.ModuleStats?.Started || 0 }}</div>
                                <div style="font-size:13px;color:#909399;">模块已启动</div>
                            </div>
                        </el-card>
                    </el-col>
                    <el-col :span="6">
                        <el-card shadow="hover">
                            <div style="text-align:center;padding:12px 0;">
                                <div style="font-size:28px;font-weight:700;color:#e6a23c;">{{ dashboard.PluginStats?.Total || 0 }}</div>
                                <div style="font-size:13px;color:#909399;">插件</div>
                            </div>
                        </el-card>
                    </el-col>
                    <el-col :span="6">
                        <el-card shadow="hover">
                            <div style="text-align:center;padding:12px 0;">
                                <div style="font-size:28px;font-weight:700;color:#f56c6c;">{{ (dashboard.FaultedModules?.length || 0) + (dashboard.FaultedPlugins?.length || 0) }}</div>
                                <div style="font-size:13px;color:#909399;">异常</div>
                            </div>
                        </el-card>
                    </el-col>
                </el-row>

                <el-row :gutter="16" style="margin-bottom:16px;">
                    <el-col :span="12">
                        <el-card shadow="never">
                            <template #header><span>异常模块</span></template>
                            <el-empty v-if="!dashboard.FaultedModules || dashboard.FaultedModules.length === 0"
                                description="无异常模块" />
                            <el-table v-else :data="dashboard.FaultedModules" border stripe size="small">
                                <el-table-column prop="Key" label="Key" width="100" />
                                <el-table-column prop="Name" label="名称" width="160" />
                                <el-table-column prop="Error" label="错误信息" show-overflow-tooltip />
                            </el-table>
                        </el-card>
                    </el-col>
                    <el-col :span="12">
                        <el-card shadow="never">
                            <template #header><span>异常插件</span></template>
                            <el-empty v-if="!dashboard.FaultedPlugins || dashboard.FaultedPlugins.length === 0"
                                description="无异常插件" />
                            <el-table v-else :data="dashboard.FaultedPlugins" border stripe size="small">
                                <el-table-column prop="Key" label="Key" width="100" />
                                <el-table-column prop="Name" label="名称" width="160" />
                                <el-table-column prop="Error" label="错误信息" show-overflow-tooltip />
                            </el-table>
                        </el-card>
                    </el-col>
                </el-row>

                <!-- 导航入口 -->
                <el-card shadow="never">
                    <template #header><span>快捷入口</span></template>
                    <el-row :gutter="12">
                        <el-col :span="6" v-for="(entry, i) in quickEntries" :key="i">
                            <el-card shadow="hover" @click="$router.push(entry.path)" style="cursor:pointer;margin-bottom:8px;">
                                <div style="text-align:center;padding:8px 0;">
                                    <el-icon :size="24" :color="entry.color"><component :is="entry.icon" /></el-icon>
                                    <div style="margin-top:6px;font-weight:600;">{{ entry.label }}</div>
                                </div>
                            </el-card>
                        </el-col>
                    </el-row>
                </el-card>
            </el-tab-pane>

            <!-- ════════════════════════════════════════════ -->
            <!-- Tab 2: 模块管理 -->
            <!-- ════════════════════════════════════════════ -->
            <el-tab-pane label="模块管理" name="modules">
                <div class="section-toolbar">
                    <el-button type="primary" @click="loadModules" :loading="loading">
                        <el-icon><Refresh /></el-icon> 刷新
                    </el-button>
                </div>
                <el-table :data="modules" border stripe v-loading="loading" size="small" max-height="500">
                    <el-table-column prop="Name" label="名称" width="160" />
                    <el-table-column prop="Key" label="Key" width="100" />
                    <el-table-column prop="Version" label="版本" width="80" />
                    <el-table-column prop="Order" label="顺序" width="60" />
                    <el-table-column prop="StageName" label="状态" width="100">
                        <template #default="{ row }">
                            <el-tag :type="moduleStageTag(row.StageName)" size="small">{{ row.StageName }}</el-tag>
                        </template>
                    </el-table-column>
                    <el-table-column prop="Error" label="错误" min-width="200" show-overflow-tooltip />
                    <el-table-column prop="DependsOn" label="依赖" width="140">
                        <template #default="{ row }">
                            <el-tag v-for="dep in (row.DependsOn || [])" :key="dep" size="small" style="margin-right:4px;">{{ dep }}</el-tag>
                            <span v-if="!row.DependsOn?.length" style="color:#909399;">-</span>
                        </template>
                    </el-table-column>
                    <el-table-column prop="AutoMigrate" label="自动建表" width="80">
                        <template #default="{ row }">✔</template>
                    </el-table-column>
                </el-table>
            </el-tab-pane>

            <!-- ════════════════════════════════════════════ -->
            <!-- Tab 3: 插件管理 -->
            <!-- ════════════════════════════════════════════ -->
            <el-tab-pane label="插件管理" name="plugins">
                <div class="section-toolbar">
                    <el-button type="primary" @click="loadModules" :loading="loading">
                        <el-icon><Refresh /></el-icon> 刷新
                    </el-button>
                </div>
                <el-table v-if="plugins.length > 0" :data="plugins" border stripe size="small" max-height="500">
                    <el-table-column prop="Name" label="名称" width="160" />
                    <el-table-column prop="Key" label="Key" width="100" />
                    <el-table-column prop="Version" label="版本" width="80" />
                    <el-table-column prop="Order" label="顺序" width="60" />
                    <el-table-column label="状态" width="100">
                        <template #default="{ row }">
                            <el-tag :type="row.Stage === 5 ? 'success' : row.Stage === 99 ? 'danger' : 'info'" size="small">
                                {{ row.Stage === 5 ? '已启动' : row.Stage === 99 ? '异常' : '阶段:' + row.Stage }}
                            </el-tag>
                        </template>
                    </el-table-column>
                    <el-table-column prop="Error" label="错误" min-width="200" show-overflow-tooltip />
                    <el-table-column label="SDK 模板" width="140">
                        <template #default="{ row }">
                            <el-tag v-if="row.Key === 'audit-log' || row.Key === 'data-sync'" type="success" size="small">内置</el-tag>
                        </template>
                    </el-table-column>
                </el-table>
                <el-empty v-else description="暂无插件。在 Program.cs 中添加 services.AddMicroiPlugin() 可启用插件系统。" />
            </el-tab-pane>

            <!-- ════════════════════════════════════════════ -->
            <!-- Tab 4: 配置管理 -->
            <!-- ════════════════════════════════════════════ -->
            <el-tab-pane label="配置管理" name="config">
                <el-card shadow="never">
                    <template #header><span>业务底座配置</span></template>
                    <el-descriptions :column="2" border :label-style="{ width: '160px', fontWeight: 600 }">
                        <el-descriptions-item label="模块自动建表">
                            <el-tag type="success">启用</el-tag>
                        </el-descriptions-item>
                        <el-descriptions-item label="插件自动扫描">
                            <el-tag type="success">启用</el-tag>
                        </el-descriptions-item>
                        <el-descriptions-item label="业务模块数">{{ config.ModuleCount }}</el-descriptions-item>
                        <el-descriptions-item label="插件数">{{ config.PluginCount }}</el-descriptions-item>
                    </el-descriptions>

                    <el-divider content-position="left">可用 API 端点</el-divider>
                    <el-table :data="configEndpoints" border stripe size="small">
                        <el-table-column prop="group" label="API 组" width="180" />
                        <el-table-column prop="endpoints" label="端点" />
                    </el-table>

                    <el-divider content-position="left">开发配置 (appsettings.json)</el-divider>
                    <el-alert title="业务底座调试配置" type="info" :closable="false" show-icon>
                        <template #default>
                            <pre style="margin:8px 0 0;font-size:12px;background:#f5f7fa;padding:8px;border-radius:4px;">{
  "BusinessBypass": {
    "BizAdminDefaultPwd": "Admin@123",
    "AutoMigrate": true,
    "MonitorIntervalSec": 60,
    "DebugMode": false
  }
}</pre>
                            <p style="margin:8px 0 0;font-size:12px;color:#909399;">
                                配置节位于 Microi.Server/Microi.net.Api/appsettings.json
                            </p>
                        </template>
                    </el-alert>
                </el-card>
            </el-tab-pane>

            <!-- ════════════════════════════════════════════ -->
            <!-- Tab 5: SDK 与模板 -->
            <!-- ════════════════════════════════════════════ -->
            <el-tab-pane label="SDK 与模板" name="sdk">
                <el-card shadow="never" style="margin-bottom:12px;">
                    <template #header><span>插件开发 SDK</span></template>
                    <el-alert title="插件系统" type="success" :closable="false" show-icon style="margin-bottom:12px;">
                        <template #default>
                            <p>业务底座支持通过 <strong>IBusinessPlugin</strong> 接口开发自定义插件，实现完整的生命周期管理。</p>
                        </template>
                    </el-alert>

                    <el-descriptions :column="1" border :label-style="{ width: '140px', fontWeight: 600 }">
                        <el-descriptions-item label="接口">IBusinessPlugin (6 个生命周期钩子)</el-descriptions-item>
                        <el-descriptions-item label="基类">BusinessPluginBase (默认空实现)</el-descriptions-item>
                        <el-descriptions-item label="注册方式">services.AddMicroiPlugin()</el-descriptions-item>
                        <el-descriptions-item label="启动方式">app.UseMicroiPlugin()</el-descriptions-item>
                        <el-descriptions-item label="示例插件">AuditLogPlugin / DataSyncPlugin</el-descriptions-item>
                    </el-descriptions>

                    <el-divider content-position="left">示例代码</el-divider>
                    <pre style="font-size:12px;background:#f5f7fa;padding:12px;border-radius:4px;overflow-x:auto;">public class MyPlugin : BusinessPluginBase
{
    public override string Key => "my-plugin";
    public override string Name => "我的插件";
    public override int Order => 100;

    public override Task OnStartAsync(PluginContext ctx)
    {
        Console.WriteLine("插件已启动");
        return Task.CompletedTask;
    }
}

// Program.cs
services.AddMicroiPlugin(opt => opt.AddPlugin&lt;MyPlugin&gt;());
app.UseMicroiPlugin();</pre>
                </el-card>

                <el-card shadow="never">
                    <template #header><span>业务模块开发</span></template>
                    <el-alert title="模块开发" type="info" :closable="false" show-icon>
                        <template #default>
                            <p>通过 <strong>IBusinessModule</strong> 和 <strong>BusinessModuleBase</strong> 开发业务模块（如 ERP/MES）。</p>
                        </template>
                    </el-alert>
                    <pre style="font-size:12px;background:#f5f7fa;padding:12px;border-radius:4px;margin-top:12px;">public class MyModule : BusinessModuleBase
{
    public override string Key => "my-module";
    public override string Name => "我的业务模块";
    public override int Order => 80;

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped&lt;MyService&gt;();
    }
}</pre>
                </el-card>
            </el-tab-pane>
        </el-tabs>
    </div>
</template>

<script>
import { Refresh, Monitor, Setting, Document, Grid } from "@element-plus/icons-vue";
import { BusinessBaseApi, BusinessMonitorApi } from "@/utils/business-base";

export default {
    name: "business_base_console",
    components: { Refresh, Monitor, Setting, Document, Grid },
    data() {
        return {
            loading: false,
            activeTab: "overview",
            dashboard: { ModuleStats: {}, PluginStats: {}, FaultedModules: [], FaultedPlugins: [] },
            modules: [],
            plugins: [],
            config: { ModuleCount: 0, PluginCount: 0, AvailableEndpoints: [] },
            quickEntries: [
                { path: "/business/doc/list", label: "文档管理", icon: "Document", color: "#409eff" },
                { path: "/business/schema", label: "表结构管理", icon: "Grid", color: "#67c23a" },
                { path: "/business/monitor", label: "模块监控", icon: "Monitor", color: "#e6a23c" },
                { path: "/license-admin", label: "License 总控台", icon: "Setting", color: "#f56c6c" }
            ]
        };
    },
    computed: {
        configEndpoints() {
            if (!this.config.AvailableEndpoints) return [];
            return this.config.AvailableEndpoints.map(function (ep) {
                var parts = ep.split("/");
                var group = parts.length >= 2 ? parts[0] + "/" + parts[1] : ep;
                var endpoints = parts.length >= 2 ? parts.slice(2).join("/") : "";
                return { group: group, endpoints: endpoints };
            });
        }
    },
    mounted() {
        this.refreshAll();
    },
    methods: {
        async refreshAll() {
            this.loading = true;
            try {
                var [dashRes, configRes] = await Promise.all([
                    BusinessBaseApi.getDashboard(),
                    BusinessBaseApi.getConfig()
                ]);
                if (dashRes && dashRes.Code === 1) {
                    this.dashboard = dashRes.Data || {};
                }
                if (configRes && configRes.Code === 1) {
                    this.config = configRes.Data || {};
                }
                await this.loadModules();
            } catch (e) {
                console.error("刷新总控台失败:", e);
            } finally {
                this.loading = false;
            }
        },
        async loadModules() {
            try {
                var res = await BusinessMonitorApi.getModules();
                if (res && res.Code === 1) {
                    this.modules = res.Data || [];
                }
            } catch (e) {
                console.error("加载模块列表失败:", e);
            }
        },
        moduleStageTag(stage) {
            var map = { Started: "success", Starting: "warning", Registered: "primary", Faulted: "danger", Stopped: "info" };
            return map[stage] || "info";
        }
    }
};
</script>

<style scoped>
.business-base-console { padding: 16px; }
.header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.header h3 { margin: 0; }
.section-toolbar { margin-bottom: 12px; }
</style>

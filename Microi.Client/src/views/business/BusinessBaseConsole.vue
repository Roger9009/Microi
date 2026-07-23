<template>
    <div class="business-base-console">
        <div class="header">
            <h3>业务底座总控台</h3>
            <el-button type="primary" @click="refreshCurrentTab" :loading="loading">
                <el-icon><Refresh /></el-icon> 刷新
            </el-button>
        </div>

        <el-tabs v-model="activeTab" type="border-card" @tab-change="onTabChange">
            <!-- ═══════════════════════════════════════════ -->
            <!-- Tab 1: 仪表盘概览 -->
            <!-- ═══════════════════════════════════════════ -->
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

            <!-- ═══════════════════════════════════════════ -->
            <!-- Tab 2: 模块管理 -->
            <!-- ═══════════════════════════════════════════ -->
            <el-tab-pane label="模块管理" name="modules">
                <div class="section-toolbar">
                    <el-button type="primary" @click="loadModules" :loading="modulesLoading">
                        <el-icon><Refresh /></el-icon> 刷新
                    </el-button>
                </div>
                <el-table :data="modules" border stripe v-loading="modulesLoading" size="small" max-height="500">
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

            <!-- ═══════════════════════════════════════════ -->
            <!-- Tab 3: 插件管理 -->
            <!-- ═══════════════════════════════════════════ -->
            <el-tab-pane label="插件管理" name="plugins">
                <div class="section-toolbar">
                    <el-button type="primary" @click="loadPlugins" :loading="pluginsLoading">
                        <el-icon><Refresh /></el-icon> 刷新
                    </el-button>
                    <el-tag type="info" size="small" style="margin-left:8px;">{{ plugins.length }} 个插件</el-tag>
                </div>

                <!-- 启停确认弹窗 -->
                <el-dialog v-model="confirmDialog.visible" :title="confirmDialog.title" width="400px">
                    <p style="margin-bottom:12px;">{{ confirmDialog.message }}</p>
                    <el-alert v-if="confirmDialog.action === 'unload'" type="warning" :closable="false" show-icon>
                        卸载后无法重新启动，需替换 DLL 后重启应用。请确认。
                    </el-alert>
                    <template #footer>
                        <el-button @click="confirmDialog.visible = false">取消</el-button>
                        <el-button type="primary" :type="confirmDialog.confirmType" @click="doConfirmAction" :loading="confirmDialog.loading">
                            {{ confirmDialog.confirmText }}
                        </el-button>
                    </template>
                </el-dialog>

                <!-- 日志查看弹窗 -->
                <el-dialog v-model="logDialog.visible" :title="'插件日志 — ' + logDialog.key" width="700px" top="5vh">
                    <div style="max-height:450px;overflow-y:auto;background:#1e1e1e;border-radius:4px;padding:12px;">
                        <pre v-if="logDialog.logs && logDialog.logs.length > 0"
                            style="color:#d4d4d4;font-size:12px;line-height:1.6;white-space:pre-wrap;word-break:break-all;margin:0;">{{ logDialog.logs.join('\n') }}</pre>
                        <div v-else style="color:#909399;text-align:center;padding:24px;">暂无日志</div>
                    </div>
                    <template #footer>
                        <el-button @click="clearPluginLogs(logDialog.key)" :loading="logDialog.loading" size="small">
                            清空日志
                        </el-button>
                        <el-button @click="logDialog.visible = false">关闭</el-button>
                    </template>
                </el-dialog>

                <el-table :data="plugins" border stripe v-loading="pluginsLoading" size="small" max-height="450">
                    <el-table-column prop="Name" label="名称" width="150" />
                    <el-table-column prop="Key" label="Key" width="110" />
                    <el-table-column prop="Version" label="版本" width="70" />
                    <el-table-column label="状态" width="110">
                        <template #default="{ row }">
                            <el-tag :type="pluginStatusTag(row)" size="small">{{ pluginStatusText(row) }}</el-tag>
                        </template>
                    </el-table-column>
                    <el-table-column prop="DllPath" label="DLL 路径" min-width="180" show-overflow-tooltip />
                    <el-table-column prop="StageChangedTime" label="状态变更" width="150" />
                    <el-table-column label="操作" width="280" fixed="right">
                        <template #default="{ row }">
                            <el-button-group>
                                <el-button v-if="row.IsRunning" type="warning" size="small"
                                    @click="confirmStopPlugin(row)">停止</el-button>
                                <el-button v-if="row.IsStopped" type="success" size="small"
                                    @click="confirmStartPlugin(row)">启动</el-button>
                                <el-button v-if="row.IsStopped" type="danger" size="small"
                                    @click="confirmUnloadPlugin(row)">卸载</el-button>
                                <el-button type="primary" size="small"
                                    @click="showPluginLogs(row.Key)">日志</el-button>
                            </el-button-group>
                        </template>
                    </el-table-column>
                </el-table>
                <el-empty v-if="plugins.length === 0 && !pluginsLoading" description="暂无插件。在 Program.cs 中添加 services.AddMicroiPlugin() 可启用插件系统。" />
            </el-tab-pane>

            <!-- ═══════════════════════════════════════════ -->
            <!-- Tab 4: 配置管理 -->
            <!-- ═══════════════════════════════════════════ -->
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
    "BizAdminDefaultPwd": "********",
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

            <!-- ═══════════════════════════════════════════ -->
            <!-- Tab 5: SDK 与模板 -->
            <!-- ═══════════════════════════════════════════ -->
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
import { ElMessage } from "element-plus";
import { BusinessBaseApi, BusinessMonitorApi, PluginApi } from "@/utils/business-base";

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
            configEndpoints: [],
            modulesLoaded: false,
            pluginsLoaded: false,
            modulesLoading: false,
            pluginsLoading: false,
            quickEntries: [
                { path: "/business/doc/list", label: "文档管理", icon: "Document", color: "#409eff" },
                { path: "/business/schema", label: "表结构管理", icon: "Grid", color: "#67c23a" },
                { path: "/business/monitor", label: "模块监控", icon: "Monitor", color: "#e6a23c" },
                { path: "/local-license-admin", label: "本地 License 总控台", icon: "Setting", color: "#f56c6c" }
            ],
            // 插件启停确认弹窗
            confirmDialog: { visible: false, title: "", message: "", action: "", key: "", confirmType: "primary", confirmText: "确认", loading: false },
            // 日志查看弹窗
            logDialog: { visible: false, key: "", logs: [], loading: false }
        };
    },
    watch: {
        // 缓存 configEndpoints，仅在 AvailableEndpoints 变化时重新计算
        "config.AvailableEndpoints": {
            handler(val) {
                if (!val || !val.length) { this.configEndpoints = []; return; }
                var result = [];
                for (var i = 0; i < val.length; i++) {
                    var ep = val[i];
                    var parts = ep.split("/");
                    var group = parts.length >= 2 ? parts[0] + "/" + parts[1] : ep;
                    var endpoints = parts.length >= 2 ? parts.slice(2).join("/") : "";
                    result.push({ group: group, endpoints: endpoints });
                }
                this.configEndpoints = result;
            },
            immediate: true
        }
    },
    mounted() {
        this.loadDashboard();
    },
    methods: {
        onTabChange(tabName) {
            if (tabName === "modules" && !this.modulesLoaded) {
                this.loadModules();
            }
            if (tabName === "plugins" && !this.pluginsLoaded) {
                this.loadPlugins();
            }
        },
        async refreshCurrentTab() {
            if (this.activeTab === "overview") {
                await this.loadDashboard();
            } else if (this.activeTab === "modules") {
                await this.loadModules();
            } else if (this.activeTab === "plugins") {
                await this.loadPlugins();
            } else if (this.activeTab === "config") {
                await this.loadConfig();
            }
        },
        async loadDashboard() {
            this.loading = true;
            try {
                var dashRes = await BusinessBaseApi.getDashboard();
                if (dashRes && dashRes.Code === 1) {
                    this.dashboard = dashRes.Data || {};
                }
                await this.loadConfig();
            } catch (e) {
                console.error("加载仪表盘失败:", e);
            } finally {
                this.loading = false;
            }
        },
        async loadConfig() {
            try {
                var configRes = await BusinessBaseApi.getConfig();
                if (configRes && configRes.Code === 1) {
                    this.config = configRes.Data || {};
                }
            } catch (e) {
                console.error("加载配置失败:", e);
            }
        },
        async loadModules() {
            this.modulesLoading = true;
            this.modulesLoaded = true;
            try {
                var res = await BusinessMonitorApi.getModules();
                if (res && res.Code === 1) {
                    this.modules = res.Data || [];
                }
            } catch (e) {
                console.error("加载模块列表失败:", e);
            } finally {
                this.modulesLoading = false;
            }
        },
        async loadPlugins() {
            this.pluginsLoading = true;
            this.pluginsLoaded = true;
            try {
                var res = await PluginApi.list();
                if (res && res.Code === 1) {
                    this.plugins = (res.Data && res.Data.Plugins) ? res.Data.Plugins : [];
                }
            } catch (e) {
                console.error("加载插件列表失败:", e);
            } finally {
                this.pluginsLoading = false;
            }
        },
        // ── 插件启停按钮 ──
        confirmStopPlugin(row) {
            this.confirmDialog = {
                visible: true, title: "停止插件", action: "stop", key: row.Key,
                message: `确认停止插件「${row.Name}」(${row.Key}) ？停止后该插件服务将不可用。`,
                confirmType: "warning", confirmText: "确认停止", loading: false
            };
        },
        confirmStartPlugin(row) {
            this.confirmDialog = {
                visible: true, title: "启动插件", action: "start", key: row.Key,
                message: `确认重新启动插件「${row.Name}」(${row.Key}) ？将从 Stopped 状态恢复运行。`,
                confirmType: "success", confirmText: "确认启动", loading: false
            };
        },
        confirmUnloadPlugin(row) {
            this.confirmDialog = {
                visible: true, title: "卸载插件", action: "unload", key: row.Key,
                message: `确认卸载插件「${row.Name}」(${row.Key}) ？卸载后可替换 DLL。`,
                confirmType: "danger", confirmText: "确认卸载", loading: false
            };
        },
        async doConfirmAction() {
            this.confirmDialog.loading = true;
            try {
                var action = this.confirmDialog.action;
                var key = this.confirmDialog.key;
                var res;
                if (action === "stop") res = await PluginApi.stop(key);
                else if (action === "start") res = await PluginApi.start(key);
                else if (action === "unload") res = await PluginApi.unload(key);

                if (res && res.Code === 1) {
                    ElMessage.success(res.Msg || "操作成功");
                } else {
                    ElMessage.error(res ? res.Msg : "操作失败");
                }
                this.confirmDialog.visible = false;
                await this.loadPlugins();
            } catch (e) {
                console.error("插件操作失败:", e);
                ElMessage.error("插件操作失败: " + (e.message || e));
            } finally {
                this.confirmDialog.loading = false;
            }
        },
        // ── 日志查看 ──
        async showPluginLogs(key) {
            this.logDialog = { visible: true, key: key, logs: [], loading: true };
            try {
                var res = await PluginApi.logs(key);
                if (res && res.Code === 1) {
                    this.logDialog.logs = res.Data && res.Data.Logs ? res.Data.Logs : [];
                }
            } catch (e) {
                console.error("加载插件日志失败:", e);
            } finally {
                this.logDialog.loading = false;
            }
        },
        async clearPluginLogs(key) {
            this.logDialog.loading = true;
            try {
                var res = await PluginApi.clearLogs(key);
                if (res && res.Code === 1) {
                    this.logDialog.logs = [];
                    ElMessage.success("日志已清空");
                }
            } catch (e) {
                console.error("清空日志失败:", e);
            } finally {
                this.logDialog.loading = false;
            }
        },
        // ── 插件状态辅助 ──
        pluginStatusTag(row) {
            if (row.IsRunning) return "success";
            if (row.IsFaulted) return "danger";
            if (row.IsStopped) return "info";
            return "warning";
        },
        pluginStatusText(row) {
            if (row.IsRunning) return "运行中";
            if (row.IsFaulted) return "故障";
            if (row.IsStopped) return "已停止";
            return row.Stage || "未知";
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

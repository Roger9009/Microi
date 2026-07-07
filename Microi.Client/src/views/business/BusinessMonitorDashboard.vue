<template>
    <div class="business-monitor">
        <div class="header">
            <h3>业务模块监控仪表盘</h3>
            <el-button type="primary" @click="refreshAll" :loading="loading">
                <el-icon><Refresh /></el-icon> 刷新
            </el-button>
        </div>

        <!-- 概览统计卡片 -->
        <el-row :gutter="16" style="margin-bottom: 16px;">
            <el-col :span="6">
                <el-card shadow="hover">
                    <div style="text-align: center; padding: 8px 0;">
                        <div style="font-size: 36px; font-weight: 700; color: #409eff;">{{ stats.total }}</div>
                        <div style="font-size: 13px; color: #909399; margin-top: 4px;">模块总数</div>
                    </div>
                </el-card>
            </el-col>
            <el-col :span="6">
                <el-card shadow="hover">
                    <div style="text-align: center; padding: 8px 0;">
                        <div style="font-size: 36px; font-weight: 700; color: #67c23a;">{{ stats.started }}</div>
                        <div style="font-size: 13px; color: #909399; margin-top: 4px;">已启动</div>
                    </div>
                </el-card>
            </el-col>
            <el-col :span="6">
                <el-card shadow="hover">
                    <div style="text-align: center; padding: 8px 0;">
                        <div style="font-size: 36px; font-weight: 700; color: #e6a23c;">{{ stats.starting }}</div>
                        <div style="font-size: 13px; color: #909399; margin-top: 4px;">启动中</div>
                    </div>
                </el-card>
            </el-col>
            <el-col :span="6">
                <el-card shadow="hover">
                    <div style="text-align: center; padding: 8px 0;">
                        <div style="font-size: 36px; font-weight: 700; color: #f56c6c;">{{ stats.faulted }}</div>
                        <div style="font-size: 13px; color: #909399; margin-top: 4px;">异常模块</div>
                    </div>
                </el-card>
            </el-col>
        </el-row>

        <!-- 健康检查 -->
        <el-card shadow="never" style="margin-bottom: 12px;">
            <template #header>
                <span>健康检查</span>
                <el-tag v-if="healthResult" :type="healthResult.Code === 1 ? 'success' : 'danger'"
                    style="margin-left: 12px;">{{ healthResult.Msg }}</el-tag>
            </template>
            <pre v-if="healthResult" style="margin: 0; font-size: 13px; background: #f5f7fa; padding: 12px; border-radius: 4px;">
{{ JSON.stringify(healthResult.Data, null, 2) }}</pre>
        </el-card>

        <!-- License 授权状态 -->
        <el-card shadow="never" style="margin-bottom: 12px;">
            <template #header>
                <span>License 授权</span>
                <el-tag v-if="licenseStatus" :type="licenseStatus.IsLicensed ? 'success' : licenseStatus.IsOpenSource ? 'info' : 'warning'"
                    style="margin-left: 12px;">
                    {{ licenseStatus.IsLicensed ? '已授权' : licenseStatus.IsOpenSource ? '开源版' : licenseStatus.IsGracePeriod ? '宽限期' : '未授权' }}
                </el-tag>
            </template>
            <el-row :gutter="16" v-if="licenseStatus">
                <el-col :span="6">
                    <div style="font-size:12px;color:#909399;">授权公司</div>
                    <div style="font-weight:600;">{{ licenseStatus.Company || '-' }}</div>
                </el-col>
                <el-col :span="4">
                    <div style="font-size:12px;color:#909399;">产品版本</div>
                    <div><el-tag size="small" :type="licenseStatus.ProductType === 'Enterprise' ? 'danger' : 'warning'">{{ licenseStatus.ProductType || '-' }}</el-tag></div>
                </el-col>
                <el-col :span="4">
                    <div style="font-size:12px;color:#909399;">剩余天数</div>
                    <div :style="{ color: (licenseStatus.DaysRemaining !== undefined && licenseStatus.DaysRemaining < 30) ? '#f56c6c' : '#67c23a', fontWeight:600 }">
                        {{ licenseStatus.DaysRemaining !== undefined ? licenseStatus.DaysRemaining + ' 天' : '-' }}
                    </div>
                </el-col>
                <el-col :span="5">
                    <div style="font-size:12px;color:#909399;">到期时间</div>
                    <div>{{ licenseStatus.ExpirationDate || '-' }}</div>
                </el-col>
                <el-col :span="5">
                    <div style="font-size:12px;color:#909399;">心跳状态</div>
                    <div>
                        <el-tag v-if="licenseStatus.IsRevokedByServer" type="danger" size="small">已吊销</el-tag>
                        <el-tag v-else-if="licenseStatus.OfflineDays > 0" type="warning" size="small">离线 {{ licenseStatus.OfflineDays }} 天</el-tag>
                        <el-tag v-else type="success" size="small">正常</el-tag>
                        <span v-if="licenseStatus.GraceDaysLeft > 0" style="margin-left:8px;color:#e6a23c;font-size:12px;">宽限期剩余 {{ licenseStatus.GraceDaysLeft }} 天</span>
                    </div>
                </el-col>
            </el-row>
        </el-card>

        <!-- 模块列表 -->
        <el-card shadow="never">
            <template #header><span>模块详情</span></template>
            <el-table :data="modules" border stripe v-loading="loading" max-height="500">
                <el-table-column prop="Name" label="模块名称" width="160" />
                <el-table-column prop="Key" label="Key" width="100" />
                <el-table-column prop="Version" label="版本" width="80" />
                <el-table-column prop="Order" label="顺序" width="70" />
                <el-table-column prop="StageName" label="状态" width="100">
                    <template #default="{ row }">
                        <el-tag :type="stageTagType(row.StageName)" size="small">{{ stageLabel(row.StageName) }}</el-tag>
                    </template>
                </el-table-column>
                <el-table-column prop="Error" label="错误信息" min-width="300" show-overflow-tooltip>
                    <template #default="{ row }">
                        <span v-if="row.Error" style="color: #f56c6c;">{{ row.Error }}</span>
                        <span v-else style="color: #909399;">-</span>
                    </template>
                </el-table-column>
                <el-table-column prop="DependsOn" label="依赖" width="160">
                    <template #default="{ row }">
                        <template v-if="row.DependsOn && row.DependsOn.length">
                            <el-tag v-for="dep in row.DependsOn" :key="dep" size="small" style="margin-right: 4px;">{{ dep }}</el-tag>
                        </template>
                        <span v-else style="color: #909399;">-</span>
                    </template>
                </el-table-column>
                <el-table-column prop="AutoMigrate" label="自动建表" width="90">
                    <template #default="{ row }">
                        <span v-if="row.AutoMigrate" style="color: #67c23a;">✔</span>
                        <span v-else style="color: #909399;">✘</span>
                    </template>
                </el-table-column>
                <el-table-column prop="StageChangedTime" label="阶段变更时间" width="170" />
            </el-table>
        </el-card>

        <!-- 异常模块 -->
        <el-card v-if="faultedModules.length > 0" shadow="never"
            style="margin-top: 12px; border-top: 3px solid #f56c6c;">
            <template #header>
                <span style="color: #f56c6c;">异常模块 ({{ faultedModules.length }})</span>
            </template>
            <el-table :data="faultedModules" border stripe size="small">
                <el-table-column prop="Name" label="模块" width="160" />
                <el-table-column prop="Key" label="Key" width="100" />
                <el-table-column prop="Error" label="错误" min-width="400" show-overflow-tooltip />
                <el-table-column prop="StageChangedTime" label="异常时间" width="170" />
            </el-table>
        </el-card>
    </div>
</template>

<script>
import { Refresh } from "@element-plus/icons-vue";
import { ElMessage } from "element-plus";
import { BusinessMonitorApi, LicenseApi } from "@/utils/business-base";

export default {
    name: "business_monitor",
    components: { Refresh },
    data() {
        return {
            loading: false,
            modules: [],
            faultedModules: [],
            healthResult: null,
            licenseStatus: null,
            stats: { total: 0, started: 0, starting: 0, faulted: 0 }
        };
    },
    mounted() {
        this.refreshAll();
    },
    methods: {
        stageTagType(stageName) {
            var map = { Started: "success", Starting: "warning", Registered: "primary",
                Stopped: "info", Faulted: "danger", Discovered: "", Stopping: "warning" };
            return map[stageName] || "info";
        },
        stageLabel(stageName) {
            var map = { Started: "✅ 已启动", Starting: "⏳ 启动中", Registered: "📋 已注册",
                Stopped: "⏹ 已停止", Faulted: "❌ 异常", Discovered: "🔍 已发现", Stopping: "⏳ 停止中" };
            return map[stageName] || stageName;
        },
        async refreshAll() {
            this.loading = true;
            try {
                var modRes = await BusinessMonitorApi.getModules();
                var healthRes = await BusinessMonitorApi.health();
                var licRes = await LicenseApi.getStatus();
                if (modRes && modRes.Code === 1) {
                    this.modules = modRes.Data || [];
                    this.faultedModules = this.modules.filter(function (m) {
                        return m.StageName === "Faulted";
                    });
                    this.stats.total = this.modules.length;
                    this.stats.started = this.modules.filter(function (m) {
                        return m.StageName === "Started";
                    }).length;
                    this.stats.starting = this.modules.filter(function (m) {
                        return m.StageName === "Starting" || m.StageName === "Registered";
                    }).length;
                    this.stats.faulted = this.faultedModules.length;
                }
                this.healthResult = healthRes;
                if (licRes && licRes.Code === 1) {
                    this.licenseStatus = licRes.Data;
                }
            } catch (e) {
                console.error("刷新监控失败:", e);
            } finally {
                this.loading = false;
            }
        }
    }
};
</script>

<style scoped>
.business-monitor {
    padding: 16px;
}
.header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;
}
.header h3 {
    margin: 0;
}
</style>

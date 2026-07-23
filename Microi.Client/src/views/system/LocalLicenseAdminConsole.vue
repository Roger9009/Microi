<template>
    <div class="license-admin-console">
        <div class="header">
            <h3>License 授权总控台</h3>
            <el-button type="primary" @click="refreshAll" :loading="loading">
                <el-icon><Refresh /></el-icon> 刷新
            </el-button>
        </div>

        <!-- 统计卡片 -->
        <el-row :gutter="16" style="margin-bottom: 16px;">
            <el-col :span="6">
                <el-card shadow="hover">
                    <div style="text-align:center;padding:8px 0;">
                        <div style="font-size:32px;font-weight:700;color:#409eff;">{{ stats.total }}</div>
                        <div style="font-size:13px;color:#909399;">授权总数</div>
                    </div>
                </el-card>
            </el-col>
            <el-col :span="6">
                <el-card shadow="hover">
                    <div style="text-align:center;padding:8px 0;">
                        <div style="font-size:32px;font-weight:700;color:#67c23a;">{{ stats.issued }}</div>
                        <div style="font-size:13px;color:#909399;">已签发</div>
                    </div>
                </el-card>
            </el-col>
            <el-col :span="6">
                <el-card shadow="hover">
                    <div style="text-align:center;padding:8px 0;">
                        <div style="font-size:32px;font-weight:700;color:#e6a23c;">{{ stats.pending }}</div>
                        <div style="font-size:13px;color:#909399;">待审核</div>
                    </div>
                </el-card>
            </el-col>
            <el-col :span="6">
                <el-card shadow="hover">
                    <div style="text-align:center;padding:8px 0;">
                        <div style="font-size:32px;font-weight:700;color:#f56c6c;">{{ stats.expiringSoon }}</div>
                        <div style="font-size:13px;color:#909399;">即将到期 (≤30天)</div>
                    </div>
                </el-card>
            </el-col>
        </el-row>

        <!-- 工具栏 -->
        <el-card shadow="never" style="margin-bottom:12px;">
            <el-form :model="query" inline label-width="70px">
                <el-form-item label="公司名称">
                    <el-input v-model="query.CompanyKeyword" placeholder="搜索公司" clearable style="width:180px;" @keyup.enter="loadList" />
                </el-form-item>
                <el-form-item label="状态">
                    <el-select v-model="query.Status" placeholder="全部" clearable style="width:130px;" @change="loadList">
                        <el-option label="全部" value="" />
                        <el-option label="已签发" value="Issued" />
                        <el-option label="待审核" value="Pending" />
                        <el-option label="已驳回" value="Rejected" />
                        <el-option label="已作废" value="Revoked" />
                    </el-select>
                </el-form-item>
                <el-form-item label="产品">
                    <el-select v-model="query.ProductType" placeholder="全部" clearable style="width:120px;" @change="loadList">
                        <el-option label="全部" value="" />
                        <el-option label="企业版" value="Enterprise" />
                        <el-option label="个人版" value="Personal" />
                    </el-select>
                </el-form-item>
                <el-form-item>
                    <el-button type="primary" @click="loadList"><el-icon><Search /></el-icon> 查询</el-button>
                    <el-button type="success" @click="showIssueDialog = true"><el-icon><Plus /></el-icon> 直接签发</el-button>
                    <el-upload accept=".milic" :auto-upload="false" :show-file-list="false"
                        :on-change="importRegistrationFile" style="display:inline-block;margin-left:12px;">
                        <el-button type="warning" :loading="importingRegistration">导入注册文件</el-button>
                    </el-upload>
                </el-form-item>
            </el-form>
        </el-card>

        <!-- 客户授权列表 -->
        <el-card shadow="never">
            <el-table :data="licenseList" border stripe v-loading="loading" style="width:100%;" max-height="calc(100vh - 380px)"
                @sort-change="onSortChange">
                <el-table-column prop="Company" label="授权公司" min-width="160" show-overflow-tooltip />
                <el-table-column prop="Name" label="联系人" width="100" />
                <el-table-column prop="Phone" label="电话" width="130" />
                <el-table-column prop="HID" label="硬件指纹 HID" min-width="200" show-overflow-tooltip />
                <el-table-column prop="ProductType" label="版本" width="80">
                    <template #default="{ row }">
                        <el-tag :type="row.ProductType === 'Enterprise' ? 'danger' : 'warning'" size="small">
                            {{ row.ProductType === 'Enterprise' ? '企业' : '个人' }}
                        </el-tag>
                    </template>
                </el-table-column>
                <el-table-column prop="Status" label="状态" width="90">
                    <template #default="{ row }">
                        <el-tag :type="statusTagType(row.Status)" size="small">{{ statusLabel(row.Status) }}</el-tag>
                    </template>
                </el-table-column>
                <el-table-column prop="ExpirationDate" label="到期时间" width="110" sortable="custom">
                    <template #default="{ row }">
                        <span :style="{ color: isExpiringSoon(row.ExpirationDate) ? '#f56c6c' : 'inherit', fontWeight: isExpiringSoon(row.ExpirationDate) ? 600 : 'normal' }">
                            {{ row.ExpirationDate ? formatDate(row.ExpirationDate) : '-' }}
                        </span>
                    </template>
                </el-table-column>
                <el-table-column prop="IssuedAt" label="签发时间" width="110" sortable="custom">
                    <template #default="{ row }">{{ row.IssuedAt ? formatDate(row.IssuedAt) : '-' }}</template>
                </el-table-column>
                <el-table-column prop="CreateTime" label="创建时间" width="110" sortable="custom">
                    <template #default="{ row }">{{ row.CreateTime ? formatDate(row.CreateTime) : '-' }}</template>
                </el-table-column>
                <el-table-column label="操作" width="220" fixed="right">
                    <template #default="{ row }">
                        <template v-if="row.Status === 'Pending'">
                            <el-button size="small" type="success" @click="onApprove(row)" :loading="acting === row.HID">通过</el-button>
                            <el-button size="small" type="danger" @click="onReject(row)" :loading="acting === row.HID">驳回</el-button>
                        </template>
                        <template v-else-if="row.Status === 'Issued'">
                            <el-button size="small" type="warning" @click="onToggleRevoke(row, true)" :loading="acting === row.HID">作废</el-button>
                        </template>
                        <template v-else-if="row.Status === 'Revoked'">
                            <el-button size="small" type="primary" @click="onToggleRevoke(row, false)" :loading="acting === row.HID">恢复</el-button>
                        </template>
                        <template v-else-if="row.Status === 'Rejected'">
                            <el-button size="small" type="primary" @click="onApprove(row)" :loading="acting === row.HID">重新签发</el-button>
                        </template>
                        <el-button size="small" @click="viewLogs(row.HID)">日志</el-button>
                    </template>
                </el-table-column>
            </el-table>

            <!-- 分页 -->
            <div v-if="total > 0" style="display:flex;justify-content:flex-end;padding:12px 0;">
                <el-pagination :current-page="page" :page-size="pageSize" :total="total"
                    layout="total, prev, pager, next, jumper" @current-change="onPageChange" />
            </div>
        </el-card>

        <!-- 签发弹窗 -->
        <el-dialog v-model="showIssueDialog" title="直接签发 License" width="550px" :close-on-click-modal="false">
            <el-form :model="issueForm" label-width="110px">
                <el-form-item label="硬件指纹 HID" required>
                    <el-input v-model="issueForm.HID" placeholder="客户服务器的硬件指纹" />
                </el-form-item>
                <el-row :gutter="16">
                    <el-col :span="12">
                        <el-form-item label="公司名称" required>
                            <el-input v-model="issueForm.Company" placeholder="授权公司" />
                        </el-form-item>
                    </el-col>
                    <el-col :span="12">
                        <el-form-item label="产品版本">
                            <el-select v-model="issueForm.ProductType" style="width:100%;">
                                <el-option label="企业版 Enterprise" value="Enterprise" />
                                <el-option label="个人版 Personal" value="Personal" />
                            </el-select>
                        </el-form-item>
                    </el-col>
                </el-row>
                <el-row :gutter="16">
                    <el-col :span="12">
                        <el-form-item label="联系人">
                            <el-input v-model="issueForm.Name" placeholder="姓名" />
                        </el-form-item>
                    </el-col>
                    <el-col :span="12">
                        <el-form-item label="联系电话">
                            <el-input v-model="issueForm.Phone" placeholder="电话" />
                        </el-form-item>
                    </el-col>
                </el-row>
                <el-form-item label="到期时间">
                    <el-date-picker v-model="issueForm.ExpirationDate" type="date" placeholder="默认一年后"
                        style="width:100%;" value-format="YYYY-MM-DD" />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="showIssueDialog = false">取消</el-button>
                <el-button type="primary" :loading="issuing" @click="doIssue">确认签发</el-button>
            </template>
        </el-dialog>

        <!-- 驳回弹窗 -->
        <el-dialog v-model="showRejectDialog" title="驳回申请" width="400px">
            <el-form label-width="80px">
                <el-form-item label="HID">
                    <el-tag>{{ rejectTarget?.HID }}</el-tag>
                </el-form-item>
                <el-form-item label="公司">
                    <span>{{ rejectTarget?.Company }}</span>
                </el-form-item>
                <el-form-item label="驳回原因" required>
                    <el-input v-model="rejectReason" type="textarea" :rows="3" placeholder="请填写驳回原因" />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="showRejectDialog = false">取消</el-button>
                <el-button type="danger" :loading="rejecting" @click="doReject">确认驳回</el-button>
            </template>
        </el-dialog>

        <!-- 日志弹窗 -->
        <el-dialog v-model="showLogsDialog" title="操作日志" width="800px" top="5vh">
            <div style="margin-bottom:12px;">
                <el-tag>HID: {{ logHidFilter }}</el-tag>
            </div>
            <el-table :data="logList" border stripe v-loading="logsLoading" size="small" max-height="400">
                <el-table-column prop="Action" label="操作" width="90">
                    <template #default="{ row }">
                        <el-tag :type="logActionTag(row.Action)" size="small">{{ row.Action }}</el-tag>
                    </template>
                </el-table-column>
                <el-table-column prop="Operator" label="操作人" width="100" />
                <el-table-column prop="OperatorIP" label="IP" width="130" />
                <el-table-column prop="Detail" label="详情" min-width="200" show-overflow-tooltip />
                <el-table-column prop="CreateTime" label="时间" width="160" />
            </el-table>
        </el-dialog>
    </div>
</template>

<script>
import { Refresh, Search, Plus } from "@element-plus/icons-vue";
import { ElMessage, ElMessageBox } from "element-plus";

const LS_BASE = "https://api.itdos.com";

export default {
    name: "local_license_admin_console",
    components: { Refresh, Search, Plus },
    data() {
        return {
            loading: false,
            issuing: false,
            rejecting: false,
            acting: "",
            licenseList: [],
            total: 0,
            page: 1,
            pageSize: 20,
            query: {
                CompanyKeyword: "",
                Status: "",
                ProductType: ""
            },
            stats: { total: 0, issued: 0, pending: 0, expiringSoon: 0 },
            // 签发
            showIssueDialog: false,
            issueForm: { HID: "", Company: "", Name: "", Phone: "", ProductType: "Enterprise", ExpirationDate: "" },
            // 驳回
            showRejectDialog: false,
            rejectTarget: null,
            rejectReason: "",
            // 日志
            showLogsDialog: false,
            logHidFilter: "",
            logList: [],
            logsLoading: false,
            importingRegistration: false
        };
    },
    mounted() {
        this.refreshAll();
    },
    methods: {
        centralFetch(path, options) {
            const request = options || {};
            const headers = Object.assign({}, request.headers || {});
            const token = this.DiyCommon && this.DiyCommon.getToken ? this.DiyCommon.getToken() : "";
            if (token) headers.authorization = "Bearer " + token;
            request.headers = headers;
            return fetch(path.startsWith("http") ? path : LS_BASE + path, request)
                .then(function (response) { return response.json(); });
        },
        importRegistrationFile(file) {
            const self = this;
            if (!file || !file.raw) return;
            self.importingRegistration = true;
            const reader = new FileReader();
            reader.onload = function (event) {
                self.centralFetch("/api/LocalLicense/ImportRegistrationFile", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ FileContent: event.target.result })
                }).then(async function (result) {
                    if (result && (result.Code === 1 || result.Code === 2)) {
                        ElMessage.success(result.Msg || "注册文件已导入");
                        await self.refreshAll();
                    } else {
                        ElMessage.error((result && result.Msg) || "注册文件导入失败");
                    }
                }).catch(function (error) {
                    ElMessage.error("注册文件导入异常: " + (error.message || ""));
                }).finally(function () {
                    self.importingRegistration = false;
                });
            };
            reader.onerror = function () {
                self.importingRegistration = false;
                ElMessage.error("读取注册文件失败");
            };
            reader.readAsText(file.raw);
        },
        async refreshAll() {
            this.loading = true;
            try {
                await this.loadList();
                await this.loadStats();
            } catch (e) {
                ElMessage.error("加载失败: " + (e.message || ""));
            } finally {
                this.loading = false;
            }
        },
        onPageChange(newPage) {
            this.page = newPage;
            this.loadList();
        },
        async loadList() {
            try {
                var url = LS_BASE + "/api/LocalLicense/List?page=" + this.page + "&pageSize=" + this.pageSize;
                if (this.query.Status) url += "&status=" + this.query.Status;
                var res = await this.centralFetch(url, { method: "GET" });
                if (res && res.Code === 1 && res.Data) {
                    var all = res.Data.List || [];
                    // 客户端公司名过滤
                    if (this.query.CompanyKeyword) {
                        var kw = this.query.CompanyKeyword.toLowerCase();
                        all = all.filter(function (r) {
                            return (r.Company || "").toLowerCase().includes(kw);
                        });
                    }
                    if (this.query.ProductType) {
                        all = all.filter(function (r) { return r.ProductType === this.query.ProductType; }.bind(this));
                    }
                    this.licenseList = all;
                    this.total = res.Data.Total || all.length;
                } else {
                    ElMessage.warning((res && res.Msg) || "加载列表失败");
                }
            } catch (e) {
                ElMessage.error("加载列表异常: " + (e.message || ""));
            }
        },
        async loadStats() {
            try {
                var res = await this.centralFetch("/api/LocalLicense/List?pageSize=10000", { method: "GET" });
                if (res && res.Code === 1 && res.Data) {
                    var list = res.Data.List || [];
                    this.stats.total = list.length;
                    this.stats.issued = list.filter(function (r) { return r.Status === "Issued"; }).length;
                    this.stats.pending = list.filter(function (r) { return r.Status === "Pending"; }).length;
                    var now = new Date();
                    this.stats.expiringSoon = list.filter(function (r) {
                        if (r.Status !== "Issued" || !r.ExpirationDate) return false;
                        var exp = new Date(r.ExpirationDate);
                        var days = (exp - now) / (1000 * 60 * 60 * 24);
                        return days >= 0 && days <= 30;
                    }).length;
                }
            } catch (e) {
                console.error("加载统计失败:", e);
            }
        },
        onSortChange(_ref) {
            if (_ref && _ref.prop) {
                // 客户端排序逻辑可在此扩展
            }
        },
        // ── 状态帮助 ──
        statusTagType(s) {
            var m = { Pending: "warning", Issued: "success", Revoked: "danger", Rejected: "info" };
            return m[s] || "info";
        },
        statusLabel(s) {
            var m = { Pending: "待审核", Issued: "已签发", Revoked: "已作废", Rejected: "已驳回" };
            return m[s] || s;
        },
        formatDate(d) {
            if (!d) return "-";
            return d.replace("T", " ").substring(0, 10);
        },
        isExpiringSoon(d) {
            if (!d) return false;
            var days = (new Date(d) - new Date()) / (1000 * 60 * 60 * 24);
            return days >= 0 && days <= 30;
        },
        logActionTag(a) {
            var m = { Apply: "", Issue: "success", Approve: "primary", Reject: "danger", Revoke: "danger", Restore: "success" };
            return m[a] || "info";
        },
        // ── 操作 ──
        async onApprove(row) {
            this.acting = row.HID;
            try {
                var res = await this.centralFetch("/api/LocalLicense/Approve", {
                    method: "POST", headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ HID: row.HID })
                });
                if (res && res.Code === 1) {
                    ElMessage.success("签发成功");
                    await this.refreshAll();
                } else {
                    ElMessage.error((res && res.Msg) || "签发失败");
                }
            } catch (e) {
                ElMessage.error("操作异常: " + (e.message || ""));
            } finally {
                this.acting = "";
            }
        },
        onReject(row) {
            this.rejectTarget = row;
            this.rejectReason = "";
            this.showRejectDialog = true;
        },
        async doReject() {
            if (!this.rejectReason.trim()) {
                ElMessage.warning("请填写驳回原因");
                return;
            }
            this.rejecting = true;
            try {
                var res = await this.centralFetch("/api/LocalLicense/Reject", {
                    method: "POST", headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ HID: this.rejectTarget.HID, RejectReason: this.rejectReason })
                });
                if (res && res.Code === 1) {
                    ElMessage.success("已驳回");
                    this.showRejectDialog = false;
                    await this.refreshAll();
                } else {
                    ElMessage.error((res && res.Msg) || "驳回失败");
                }
            } catch (e) {
                ElMessage.error("操作异常: " + (e.message || ""));
            } finally {
                this.rejecting = false;
            }
        },
        async onToggleRevoke(row, revoke) {
            try {
                await ElMessageBox.confirm(
                    revoke ? "确认作废该客户的 License？作废后该服务器将拒绝启动。" : "确认恢复该客户的 License？",
                    revoke ? "作废确认" : "恢复确认",
                    { confirmButtonText: "确认", cancelButtonText: "取消", type: "warning" }
                );
                this.acting = row.HID;
                var res = await this.centralFetch("/api/LocalLicense/Revoke", {
                    method: "POST", headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ HID: row.HID, Revoke: revoke })
                });
                if (res && res.Code === 1) {
                    ElMessage.success(revoke ? "已作废" : "已恢复");
                    await this.refreshAll();
                } else {
                    ElMessage.error((res && res.Msg) || "操作失败");
                }
            } catch (e) {
                if (e !== "cancel") {
                    ElMessage.error("操作异常: " + (e.message || ""));
                }
            } finally {
                this.acting = "";
            }
        },
        async doIssue() {
            if (!this.issueForm.HID || !this.issueForm.Company) {
                ElMessage.warning("请填写 HID 和公司名称");
                return;
            }
            this.issuing = true;
            try {
                var payload = {
                    HID: this.issueForm.HID.trim(),
                    Company: this.issueForm.Company.trim(),
                    Name: this.issueForm.Name.trim(),
                    Phone: this.issueForm.Phone.trim(),
                    ProductType: this.issueForm.ProductType
                };
                if (this.issueForm.ExpirationDate) {
                    payload.ExpirationDate = this.issueForm.ExpirationDate + "T00:00:00Z";
                }
                var res = await this.centralFetch("/api/LocalLicense/Issue", {
                    method: "POST", headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });
                if (res && res.Code === 1) {
                    ElMessage.success("签发成功");
                    this.showIssueDialog = false;
                    this.issueForm = { HID: "", Company: "", Name: "", Phone: "", ProductType: "Enterprise", ExpirationDate: "" };
                    await this.refreshAll();
                } else {
                    ElMessage.error((res && res.Msg) || "签发失败");
                }
            } catch (e) {
                ElMessage.error("签发异常: " + (e.message || ""));
            } finally {
                this.issuing = false;
            }
        },
        async viewLogs(hid) {
            this.logHidFilter = hid;
            this.logsLoading = true;
            this.showLogsDialog = true;
            try {
                var res = await this.centralFetch("/api/LocalLicense/Logs?hid=" + encodeURIComponent(hid), { method: "GET" });
                if (res && res.Code === 1 && res.Data) {
                    this.logList = res.Data.List || [];
                } else {
                    this.logList = [];
                }
            } catch (e) {
                ElMessage.error("加载日志失败");
            } finally {
                this.logsLoading = false;
            }
        }
    }
};
</script>

<style scoped>
.license-admin-console { padding: 16px; }
.header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
.header h3 { margin: 0; }
</style>

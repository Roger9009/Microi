<template>
    <div class="business-doc-list">
        <!-- 顶部工具栏 -->
        <div class="header">
            <el-select v-model="selectedTable" placeholder="选择业务文档类型" style="width: 300px;" filterable
                @change="onTableChange">
                <el-option v-for="doc in documentTypes" :key="doc.MasterTable" :label="doc.Label + ' (' + doc.MasterTable + ')'"
                    :value="doc.MasterTable" />
            </el-select>
            <el-button type="primary" @click="refreshList" :disabled="!selectedTable" style="margin-left: 8px;">
                <el-icon><Search /></el-icon> 查询
            </el-button>
            <el-button type="success" @click="onAdd" :disabled="!selectedTable" style="margin-left: 8px;">
                <el-icon><Plus /></el-icon> 新增
            </el-button>
            <el-button @click="loadDocuments" style="margin-left: 8px;">
                <el-icon><Refresh /></el-icon> 刷新
            </el-button>
            <el-button v-if="selectedTable && multipleSelection.length > 0" type="danger"
                style="margin-left: 8px;" @click="onBatchDelete">
                <el-icon><Delete /></el-icon> 批量删除 ({{ multipleSelection.length }})
            </el-button>
        </div>

        <!-- 查询条件 -->
        <el-card v-if="selectedTable" shadow="never" style="margin-top: 12px;">
            <el-form :model="queryForm" inline label-width="60px">
                <el-form-item label="关键词">
                    <el-input v-model="queryForm.Keyword" placeholder="搜索..." clearable style="width: 200px;"
                        @keyup.enter="refreshList" />
                </el-form-item>
                <el-form-item label="状态">
                    <el-select v-model="queryForm.StatusFilter" placeholder="全部状态" clearable style="width: 130px;">
                        <el-option label="草稿" :value="0" />
                        <el-option label="已提交" :value="1" />
                        <el-option label="处理中" :value="2" />
                        <el-option label="已完成" :value="3" />
                        <el-option label="已作废" :value="4" />
                    </el-select>
                </el-form-item>
                <el-form-item label="每页">
                    <el-select v-model="queryForm.PageSize" style="width: 100px;">
                        <el-option :value="10" label="10" />
                        <el-option :value="20" label="20" />
                        <el-option :value="50" label="50" />
                        <el-option :value="100" label="100" />
                    </el-select>
                </el-form-item>
                <el-form-item>
                    <el-button type="primary" @click="refreshList">查询</el-button>
                    <el-button @click="resetQuery">重置</el-button>
                </el-form-item>
            </el-form>
        </el-card>

        <!-- 数据表格 -->
        <el-table v-if="selectedTable" ref="tableRef" :data="list" v-loading="loading" border stripe
            style="margin-top: 12px; width: 100%;" @sort-change="onSortChange" max-height="calc(100vh - 320px)"
            @selection-change="onSelectionChange">
            <el-table-column type="selection" width="50" />
            <el-table-column label="Id" width="200" show-overflow-tooltip>
                <template #default="{ row }">
                    <el-tooltip :content="row.Id" placement="top">
                        <span class="id-cell" @click="copyId(row.Id)">{{ row.Id?.substring(0, 8) }}...</span>
                    </el-tooltip>
                </template>
            </el-table-column>
            <el-table-column v-for="col in displayColumns" :key="col.Name" :prop="col.Name"
                :label="col.Comment || col.Name" :width="guessColumnWidth(col)" show-overflow-tooltip
                :sortable="isSortableType(col.DataType)">
                <template #default="{ row }">
                    <template v-if="col.DataType?.includes('datetime') || col.DataType?.includes('timestamp')">
                        {{ row[col.Name] ? formatDate(row[col.Name]) : '-' }}
                    </template>
                    <template v-else-if="col.Name === 'Status' && statusMap[row[col.Name]]">
                        <el-tag :type="statusTagType(row[col.Name])">{{ statusMap[row[col.Name]] }}</el-tag>
                    </template>
                    <template v-else>
                        {{ row[col.Name] ?? '-' }}
                    </template>
                </template>
            </el-table-column>
            <el-table-column label="操作" width="340" fixed="right">
                <template #default="{ row }">
                    <el-button size="small" type="primary" link @click="onView(row)">查看</el-button>
                    <el-button size="small" type="warning" link @click="onEdit(row)">编辑</el-button>
                    <el-button size="small" type="success" link @click="onShowExecute(row)"
                        :disabled="!row.Status || row.Status >= 3">
                        <el-icon><CaretRight /></el-icon> 流转
                    </el-button>
                    <el-popconfirm title="确认删除此记录？" @confirm="onDelete(row)">
                        <template #reference>
                            <el-button size="small" type="danger" link>删除</el-button>
                        </template>
                    </el-popconfirm>
                </template>
            </el-table-column>
        </el-table>

        <!-- 分页 -->
        <div v-if="total > 0" style="display: flex; justify-content: flex-end; padding: 12px 0;">
            <el-pagination v-model:current-page="queryForm.PageIndex" :page-size="queryForm.PageSize" :total="total"
                layout="total, prev, pager, next, jumper" @current-change="refreshList" />
        </div>

        <!-- 状态流转对话框 -->
        <el-dialog v-model="executeVisible" title="状态流转" width="420px">
            <el-form label-width="100px">
                <el-form-item label="当前状态">
                    <el-tag :type="statusTagType(executeTarget?.Status)">{{ statusLabel(executeTarget?.Status) }}</el-tag>
                </el-form-item>
                <el-form-item label="流转动作" required>
                    <el-select v-model="executeTrigger" placeholder="选择操作" style="width: 100%;">
                        <el-option label="提交 (Submit)" value="Submit" />
                        <el-option label="审核通过 (Audit)" value="Audit" />
                        <el-option label="完成 (Finish)" value="Finish" />
                        <el-option label="作废 (Cancel)" value="Cancel" />
                    </el-select>
                </el-form-item>
                <el-form-item label="操作附言">
                    <el-input v-model="executeRemark" type="textarea" :rows="2" placeholder="可选" />
                </el-form-item>
            </el-form>
            <template #footer>
                <el-button @click="executeVisible = false">取消</el-button>
                <el-button type="primary" @click="onExecute" :loading="executing">确认执行</el-button>
            </template>
        </el-dialog>

        <!-- 空状态 -->
        <el-empty v-if="!selectedTable" description="请从上方下拉框选择要管理的业务文档类型" />
    </div>
</template>

<script>
import { Search, Plus, Refresh, Delete, CaretRight } from "@element-plus/icons-vue";
import { ElMessage, ElMessageBox } from "element-plus";
import { BusinessDocApi, BusinessSchemaApi } from "@/utils/business-base";

export default {
    name: "business_doc_list",
    components: { Search, Plus, Refresh, Delete, CaretRight },
    data() {
        return {
            loading: false,
            documentTypes: [],
            selectedTable: "",
            list: [],
            total: 0,
            schema: null,
            multipleSelection: [],
            executeVisible: false,
            executeTarget: null,
            executeTrigger: "",
            executeRemark: "",
            executing: false,
            queryForm: {
                Keyword: "",
                StatusFilter: null,
                PageIndex: 1,
                PageSize: 20,
                SortField: "",
                SortType: ""
            },
            statusMap: {}
        };
    },
    computed: {
        displayColumns() {
            if (!this.schema || !this.schema.Master || !this.schema.Master.Columns) return [];
            var cols = this.schema.Master.Columns.filter(function (c) {
                var name = c.Name ? c.Name.toLowerCase() : "";
                return !c.IsSystem || name === "id" || name === "status" || name === "billno";
            });
            return cols.slice(0, 8);
        }
    },
    mounted() {
        this.loadDocuments();
    },
    methods: {
        async loadDocuments() {
            try {
                var res = await BusinessSchemaApi.getDocuments();
                if (res && res.Code === 1) {
                    this.documentTypes = res.Data || [];
                }
            } catch (e) {
                console.error("加载文档类型失败:", e);
            }
        },
        async onTableChange(table) {
            this.selectedTable = table;
            this.queryForm.PageIndex = 1;
            await this.loadSchema(table);
            await this.refreshList();
        },
        async loadSchema(table) {
            try {
                var res = await BusinessSchemaApi.getDocumentSchema(table);
                if (res && res.Code === 1) {
                    this.schema = res.Data;
                }
            } catch (e) {
                console.error("加载 Schema 失败:", e);
            }
        },
        async refreshList() {
            if (!this.selectedTable) return;
            this.loading = true;
            try {
                var params = {
                    _PageIndex: this.queryForm.PageIndex,
                    _PageSize: this.queryForm.PageSize
                };
                if (this.queryForm.Keyword) {
                    params._Where = [[["Id", "like", "%" + this.queryForm.Keyword + "%"]]];
                }
                if (this.queryForm.SortField) {
                    params._SortField = this.queryForm.SortField;
                    params._SortType = this.queryForm.SortType || "asc";
                }
                if (this.queryForm.StatusFilter !== null && this.queryForm.StatusFilter !== undefined
                    && this.queryForm.StatusFilter !== "") {
                    var sf = this.queryForm.StatusFilter;
                    if (!params._Where) params._Where = [];
                    params._Where.push(["Status", "=", sf]);
                }
                var res = await BusinessDocApi.getList(this.selectedTable, params);
                if (res && res.Code === 1) {
                    this.list = res.Data || [];
                    this.total = res.Total || this.list.length;
                } else {
                    this.list = [];
                    this.total = 0;
                }
            } catch (e) {
                ElMessage.error("查询失败: " + (e.message || "未知错误"));
            } finally {
                this.loading = false;
            }
        },
        resetQuery() {
            this.queryForm.Keyword = "";
            this.queryForm.StatusFilter = null;
            this.queryForm.PageIndex = 1;
            this.refreshList();
        },
        onSortChange(_ref) {
            if (_ref) {
                this.queryForm.SortField = _ref.prop || "";
                this.queryForm.SortType = _ref.order === "ascending" ? "asc" : _ref.order === "descending" ? "desc" : "";
                this.refreshList();
            }
        },
        onView(row) {
            this.$router.push("/business/doc/view/" + this.selectedTable + "/" + row.Id);
        },
        onEdit(row) {
            this.$router.push("/business/doc/edit/" + this.selectedTable + "/" + row.Id);
        },
        onAdd() {
            this.$router.push("/business/doc/add/" + this.selectedTable);
        },
        onSelectionChange(val) {
            this.multipleSelection = val;
        },
        onShowExecute(row) {
            this.executeTarget = row;
            this.executeTrigger = "";
            this.executeRemark = "";
            this.executeVisible = true;
        },
        async onExecute() {
            if (!this.executeTrigger) {
                ElMessage.warning("请选择流转动作");
                return;
            }
            this.executing = true;
            try {
                var res = await BusinessDocApi.execute(this.selectedTable, this.executeTarget.Id, this.executeTrigger, this.executeRemark);
                if (res && res.Code === 1) {
                    ElMessage.success(res.Msg || "流转成功");
                    this.executeVisible = false;
                    await this.refreshList();
                } else {
                    ElMessage.error((res && res.Msg) || "流转失败");
                }
            } catch (e) {
                ElMessage.error("流转异常: " + (e.message || ""));
            } finally {
                this.executing = false;
            }
        },
        async onBatchDelete() {
            if (!this.multipleSelection || this.multipleSelection.length === 0) return;
            try {
                await ElMessageBox.confirm("确认删除选中的 " + this.multipleSelection.length + " 条记录？此操作不可恢复。", "批量删除", { confirmButtonText: "确认删除", cancelButtonText: "取消", type: "warning" });
                var ids = this.multipleSelection.map(function (r) { return r.Id; });
                var res = await BusinessDocApi.delBatch(this.selectedTable, ids);
                if (res && res.Code === 1) {
                    ElMessage.success(res.Msg || "批量删除成功");
                } else {
                    ElMessage.warning((res && res.Msg) || "部分删除失败");
                }
                await this.refreshList();
            } catch (e) {
                if (e !== "cancel") {
                    ElMessage.error("批量删除异常: " + (e.message || ""));
                }
            }
        },
        async onDelete(row) {
            try {
                var res = await BusinessDocApi.del(this.selectedTable, row.Id);
                if (res && res.Code === 1) {
                    ElMessage.success("删除成功");
                    await this.refreshList();
                } else {
                    ElMessage.error((res && res.Msg) || "删除失败");
                }
            } catch (e) {
                ElMessage.error("删除异常: " + (e.message || "未知错误"));
            }
        },
        copyId(id) {
            if (navigator.clipboard) navigator.clipboard.writeText(id || "");
            ElMessage.success("已复制 Id");
        },
        guessColumnWidth(col) {
            if (!col || !col.Name) return "";
            var name = col.Name.toLowerCase();
            if (name === "id") return 200;
            if (name === "status" || name === "sort") return 90;
            if (name === "billno") return 180;
            if (col.DataType && (col.DataType.includes("datetime") || col.DataType.includes("timestamp"))) return 170;
            if (col.DataType && (col.DataType.includes("decimal") || col.DataType.includes("double"))) return 130;
            return "";
        },
        isSortableType(dataType) {
            if (!dataType) return false;
            return dataType.includes("int") || dataType.includes("decimal") || dataType.includes("double") ||
                   dataType.includes("datetime") || dataType.includes("varchar");
        },
        formatDate(val) {
            if (!val) return "-";
            return val.replace("T", " ").substring(0, 19);
        },
        statusTagType(val) {
            var num = Number(val);
            if (isNaN(num)) return "info";
            if (num <= 0) return "info";
            if (num === 1) return "primary";
            if (num === 2) return "warning";
            if (num === 3) return "success";
            if (num >= 4) return "danger";
            return "info";
        },
        statusLabel(val) {
            var labels = { 0: "草稿", 1: "已提交", 2: "处理中", 3: "已完成", 4: "已作废", 5: "已关闭" };
            return labels[Number(val)] || ("状态:" + val);
        }
    }
};
</script>

<style scoped>
.business-doc-list { padding: 16px; }
.header { display: flex; align-items: center; }
.id-cell { cursor: pointer; color: #409eff; text-decoration: underline; text-decoration-style: dotted; }
</style>

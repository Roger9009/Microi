<template>
    <div class="business-schema-manager">
        <div class="header">
            <h3>业务表结构管理</h3>
            <div>
                <el-select v-model="selectedTable" placeholder="选择文档" filterable style="width: 300px;" @change="loadSchema">
                    <el-option v-for="doc in documents" :key="doc.MasterTable"
                        :label="doc.Label + ' (' + doc.MasterTable + ')'" :value="doc.MasterTable" />
                </el-select>
                <el-button type="primary" @click="refreshDocs" style="margin-left: 8px;">
                    <el-icon><Refresh /></el-icon> 刷新
                </el-button>
            </div>
        </div>

        <template v-if="schema">
            <el-tabs v-model="activeTab" type="border-card" style="margin-top: 12px;">
                <!-- Tab 1: 表结构概览 -->
                <el-tab-pane label="表结构概览" name="overview">
                    <el-descriptions :column="3" border :label-style="{ width: '140px', fontWeight: 600 }">
                        <el-descriptions-item label="主表名">{{ schema.MasterTable }}</el-descriptions-item>
                        <el-descriptions-item label="标签">{{ schema.Label }}</el-descriptions-item>
                        <el-descriptions-item label="列数">{{ (schema.Master && schema.Master.Columns) ? schema.Master.Columns.length : 0 }}</el-descriptions-item>
                        <el-descriptions-item label="扩展表">{{ (schema.Extensions && schema.Extensions.length) || 0 }} 个</el-descriptions-item>
                        <el-descriptions-item label="明细表">{{ (schema.Details && schema.Details.length) || 0 }} 个</el-descriptions-item>
                        <el-descriptions-item label="是否存在">
                            <el-tag :type="schema.Master && schema.Master.Exists ? 'success' : 'danger'">
                                {{ schema.Master && schema.Master.Exists ? '已存在' : '未创建' }}
                            </el-tag>
                        </el-descriptions-item>
                    </el-descriptions>
                    <h4 style="margin-top: 16px;">主表列信息</h4>
                    <el-table :data="(schema.Master && schema.Master.Columns) || []" border stripe size="small" max-height="400">
                        <el-table-column prop="Name" label="列名" width="180" />
                        <el-table-column prop="ColumnType" label="类型" width="160" />
                        <el-table-column prop="Comment" label="注释" min-width="200" show-overflow-tooltip />
                        <el-table-column label="可空" width="70">
                            <template #default="{ row }">
                                <el-tag :type="row.Nullable ? 'info' : 'danger'" size="small">{{ row.Nullable ? '是' : '否' }}</el-tag>
                            </template>
                        </el-table-column>
                        <el-table-column label="系统字段" width="90">
                            <template #default="{ row }">
                                <el-tag v-if="row.IsSystem" type="warning" size="small">系统</el-tag>
                                <span v-else>-</span>
                            </template>
                        </el-table-column>
                        <el-table-column label="主键" width="70">
                            <template #default="{ row }">
                                <span v-if="row.IsPrimaryKey" style="color: #e6a23c;">★</span>
                            </template>
                        </el-table-column>
                    </el-table>
                </el-tab-pane>

                <!-- Tab 2: 扩展表 -->
                <el-tab-pane label="扩展表" name="extensions">
                    <el-empty v-if="!schema.Extensions || schema.Extensions.length === 0" description="暂无扩展表" />
                    <el-table v-else :data="schema.Extensions" border stripe>
                        <el-table-column prop="TableName" label="表名" width="200" />
                        <el-table-column prop="Label" label="标签" width="160" />
                        <el-table-column label="存在" width="80">
                            <template #default="{ row }">
                                <el-tag :type="row.Exists ? 'success' : 'info'" size="small">{{ row.Exists ? '是' : '否' }}</el-tag>
                            </template>
                        </el-table-column>
                        <el-table-column label="动态绑定" width="90">
                            <template #default="{ row }">
                                <el-tag v-if="row.IsDynamic" type="warning" size="small">动态</el-tag>
                                <span v-else>静态</span>
                            </template>
                        </el-table-column>
                        <el-table-column label="列数" width="70">
                            <template #default="{ row }">{{ (row.Columns && row.Columns.length) || 0 }}</template>
                        </el-table-column>
                    </el-table>
                </el-tab-pane>

                <!-- Tab 3: 明细表 -->
                <el-tab-pane label="明细表" name="details">
                    <el-empty v-if="!schema.Details || schema.Details.length === 0" description="暂无明细表" />
                    <el-table v-else :data="schema.Details" border stripe>
                        <el-table-column prop="TableName" label="表名" width="200" />
                        <el-table-column prop="Label" label="标签" width="160" />
                        <el-table-column prop="ForeignKey" label="外键列" width="130" />
                        <el-table-column prop="PropertyName" label="JSON属性名" width="140" />
                        <el-table-column label="存在" width="80">
                            <template #default="{ row }">
                                <el-tag :type="row.Exists ? 'success' : 'info'" size="small">{{ row.Exists ? '是' : '否' }}</el-tag>
                            </template>
                        </el-table-column>
                        <el-table-column label="动态绑定" width="90">
                            <template #default="{ row }">
                                <el-tag v-if="row.IsDynamic" type="warning" size="small">动态</el-tag>
                                <span v-else>静态</span>
                            </template>
                        </el-table-column>
                        <el-table-column label="列数" width="70">
                            <template #default="{ row }">{{ (row.Columns && row.Columns.length) || 0 }}</template>
                        </el-table-column>
                    </el-table>
                </el-tab-pane>

                <!-- Tab 4: 动态加字段 -->
                <el-tab-pane label="动态加字段" name="add-field">
                    <el-alert title="可在任意已注册的文档主表/扩展表/明细表中动态添加字段。扩展表不存在时自动创建。"
                        type="info" :closable="false" show-icon style="margin-bottom: 16px;" />
                    <el-form :model="fieldForm" label-width="120px">
                        <el-row :gutter="24">
                            <el-col :span="12">
                                <el-form-item label="目标表" required>
                                    <el-select v-model="fieldForm.TargetTable" filterable style="width: 100%;"
                                        placeholder="选择目标表">
                                        <el-option :value="schema.MasterTable" :label="'主表: ' + schema.MasterTable" />
                                        <el-option v-for="ext in (schema.Extensions || [])" :key="ext.TableName"
                                            :value="ext.TableName" :label="'扩展表: ' + (ext.Label || ext.TableName)" />
                                        <el-option v-for="d in (schema.Details || [])" :key="d.TableName"
                                            :value="d.TableName" :label="'明细表: ' + (d.Label || d.TableName)" />
                                    </el-select>
                                </el-form-item>
                            </el-col>
                            <el-col :span="12">
                                <el-form-item label="字段名" required>
                                    <el-input v-model="fieldForm.FieldName" placeholder="仅允许字母、数字、下划线" />
                                </el-form-item>
                            </el-col>
                        </el-row>
                        <el-row :gutter="24">
                            <el-col :span="8">
                                <el-form-item label="数据类型" required>
                                    <el-select v-model="fieldForm.DataType" style="width: 100%;">
                                        <el-option label="字符串 (String)" value="string" />
                                        <el-option label="文本 (Text)" value="text" />
                                        <el-option label="整数 (Int)" value="int" />
                                        <el-option label="小数 (Decimal)" value="decimal" />
                                        <el-option label="日期时间 (DateTime)" value="datetime" />
                                        <el-option label="布尔 (Bool)" value="bool" />
                                    </el-select>
                                </el-form-item>
                            </el-col>
                            <el-col :span="8">
                                <el-form-item label="长度">
                                    <el-input-number v-model="fieldForm.Length" :min="0" :max="9999" style="width: 100%;" />
                                </el-form-item>
                            </el-col>
                            <el-col :span="8">
                                <el-form-item label="不能为空">
                                    <el-switch v-model="fieldForm.NotNull" />
                                </el-form-item>
                            </el-col>
                        </el-row>
                        <el-form-item>
                            <el-button type="primary" @click="onAddField" :loading="addingField">
                                <el-icon><Plus /></el-icon> 添加字段
                            </el-button>
                        </el-form-item>
                    </el-form>
                </el-tab-pane>
            </el-tabs>
        </template>

        <el-empty v-else-if="!loading" description="请选择要管理的业务文档" style="margin-top: 40px;" />
        <div v-else style="display: flex; align-items: center; justify-content: center; padding: 60px; color: #909399;">
            <el-icon class="is-loading" :size="32"><Loading /></el-icon> 加载中...
        </div>
    </div>
</template>

<script>
import { Refresh, Plus, Loading } from "@element-plus/icons-vue";
import { ElMessage } from "element-plus";
import { BusinessSchemaApi } from "@/utils/business-base";

export default {
    name: "business_schema_manager",
    components: { Refresh, Plus, Loading },
    data() {
        return {
            documents: [],
            selectedTable: "",
            schema: null,
            loading: false,
            addingField: false,
            activeTab: "overview",
            fieldForm: {
                TargetTable: "",
                FieldName: "",
                DataType: "string",
                Length: 255,
                NotNull: false
            }
        };
    },
    mounted() {
        this.refreshDocs();
    },
    methods: {
        async refreshDocs() {
            this.loading = true;
            try {
                var res = await BusinessSchemaApi.getDocuments();
                if (res && res.Code === 1) {
                    this.documents = res.Data || [];
                }
            } catch (e) {
                ElMessage.error("加载文档列表失败");
            } finally {
                this.loading = false;
            }
        },
        async loadSchema(table) {
            if (!table) return;
            this.selectedTable = table;
            this.loading = true;
            this.schema = null;
            try {
                var res = await BusinessSchemaApi.getDocumentSchema(table);
                if (res && res.Code === 1) {
                    this.schema = res.Data;
                    this.fieldForm.TargetTable = table;
                } else {
                    ElMessage.warning((res && res.Msg) || "加载表结构失败");
                }
            } catch (e) {
                ElMessage.error("加载表结构异常");
            } finally {
                this.loading = false;
            }
        },
        async onAddField() {
            if (!this.fieldForm.TargetTable || !this.fieldForm.FieldName) {
                ElMessage.warning("请填写目标表和字段名");
                return;
            }
            this.addingField = true;
            try {
                var params = {
                    MasterTable: this.selectedTable,
                    TargetTable: this.fieldForm.TargetTable,
                    FieldName: this.fieldForm.FieldName,
                    DataType: this.fieldForm.DataType,
                    Label: this.fieldForm.FieldName,
                    NotNull: this.fieldForm.NotNull
                };
                if (this.fieldForm.Length > 0) params.Length = this.fieldForm.Length;
                var res = await BusinessSchemaApi.addField(params);
                if (res && res.Code === 1) {
                    ElMessage.success("字段添加成功，刷新结构可查看");
                    this.fieldForm.FieldName = "";
                } else {
                    ElMessage.error((res && res.Msg) || "添加失败");
                }
            } catch (e) {
                ElMessage.error("添加异常: " + (e.message || ""));
            } finally {
                this.addingField = false;
            }
        }
    }
};
</script>

<style scoped>
.business-schema-manager {
    padding: 16px;
}
.header {
    display: flex;
    justify-content: space-between;
    align-items: center;
}
.header h3 {
    margin: 0;
}
</style>

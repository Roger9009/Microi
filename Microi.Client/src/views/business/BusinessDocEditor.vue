<template>
    <div class="business-doc-editor">
        <div class="header">
            <el-page-header :icon="ArrowLeft" @back="goBack">
                <template #content>
                    <span style="font-size: 18px; font-weight: 600;">{{ isAdd ? '新增' : '编辑' }} {{ schemaLabel }}</span>
                </template>
            </el-page-header>
            <div class="header-actions">
                <el-button type="primary" @click="onSave" :loading="saving">
                    <el-icon><Check /></el-icon> 保存
                </el-button>
                <el-button v-if="!isAdd" @click="onRefresh" :loading="loading">
                    <el-icon><Refresh /></el-icon> 刷新
                </el-button>
            </div>
        </div>

        <!-- 主表单 -->
        <el-card shadow="never" style="margin-top: 12px;" v-loading="loading">
            <el-form :model="formData" label-width="120px">
                <el-row :gutter="24">
                    <el-col :span="8">
                        <el-form-item label="单据编号">
                            <el-input v-model="formData.BillNo" disabled placeholder="保存后自动生成" />
                        </el-form-item>
                    </el-col>
                    <el-col :span="8">
                        <el-form-item label="状态">
                            <el-tag v-if="formData.Status !== undefined && formData.Status !== null"
                                :type="statusTagType(formData.Status)">
                                {{ statusLabel(formData.Status) }}
                            </el-tag>
                            <span v-else>-</span>
                        </el-form-item>
                    </el-col>
                    <el-col :span="8">
                        <el-form-item v-if="!isAdd" label="记录 Id">
                            <el-input v-model="formData.Id" disabled size="small" />
                        </el-form-item>
                    </el-col>
                </el-row>

                <el-divider content-position="left">业务字段</el-divider>
                <el-row :gutter="24">
                    <template v-for="col in editableColumns" :key="col.Name">
                        <el-col :span="colSpan(col)">
                            <el-form-item :label="col.Comment || col.Name">
                                <!-- 文本域 -->
                                <el-input v-if="isTextType(col)" v-model="formData[col.Name]"
                                    type="textarea" :rows="3" :placeholder="'请输入' + (col.Comment || col.Name)" />
                                <!-- 数字 -->
                                <el-input-number v-else-if="isIntType(col)" v-model="formData[col.Name]"
                                    :min="getMin(col)" :max="getMax(col)" style="width: 100%;" controls-position="right" />
                                <el-input-number v-else-if="isFloatType(col)" v-model="formData[col.Name]"
                                    :min="0" :precision="2" style="width: 100%;" controls-position="right" />
                                <!-- 日期时间 -->
                                <el-date-picker v-else-if="isDateTimeType(col)" v-model="formData[col.Name]"
                                    type="datetime" placeholder="选择日期时间" format="YYYY-MM-DD HH:mm:ss"
                                    value-format="YYYY-MM-DD HH:mm:ss" style="width: 100%;" />
                                <el-date-picker v-else-if="isDateType(col)" v-model="formData[col.Name]"
                                    type="date" placeholder="选择日期" format="YYYY-MM-DD" value-format="YYYY-MM-DD"
                                    style="width: 100%;" />
                                <!-- 开关 -->
                                <el-switch v-else-if="isBoolType(col)" v-model="formData[col.Name]" />
                                <!-- 默认文本 -->
                                <el-input v-else v-model="formData[col.Name]"
                                    :placeholder="'请输入' + (col.Comment || col.Name)" clearable />
                            </el-form-item>
                        </el-col>
                    </template>
                </el-row>
            </el-form>
        </el-card>

        <!-- 明细表 -->
        <el-card v-if="details.length > 0" shadow="never" style="margin-top: 12px;">
            <template #header>
                <span>明细数据</span>
                <el-button size="small" type="primary" style="margin-left: 12px;" @click="addDetailRow">
                    <el-icon><Plus /></el-icon> 添加明细行
                </el-button>
            </template>
            <template v-for="detail in details" :key="detail.TableName">
                <h4 v-if="details.length > 1" style="margin: 8px 0; color: #409eff;">{{ detail.Label || detail.TableName }}</h4>
                <el-table :data="detailItems[detail.PropertyName || detail.TableName] || []" border stripe size="small">
                    <el-table-column type="index" label="#" width="50" />
                    <el-table-column v-for="col in detailColumns(detail)" :key="col.Name" :prop="col.Name"
                        :label="col.Comment || col.Name" show-overflow-tooltip>
                        <template #default="{ row: $row }">
                            <el-input v-model="$row[col.Name]" size="small" :placeholder="col.Comment || col.Name" />
                        </template>
                    </el-table-column>
                    <el-table-column label="操作" width="80" fixed="right">
                        <template #default="{ row: $row, $index }">
                            <el-button size="small" type="danger" link @click="removeDetailRow(detail, $index)">删除</el-button>
                        </template>
                    </el-table-column>
                </el-table>
            </template>
        </el-card>

        <!-- 扩展表提示 -->
        <el-card v-if="extensions.length > 0" shadow="never" style="margin-top: 12px;">
            <template #header><span>扩展数据（自动合并）</span></template>
            <el-tag v-for="ext in extensions" :key="ext.TableName" style="margin-right: 8px;">
                {{ ext.Label || ext.TableName }}
            </el-tag>
            <p style="color: #909399; font-size: 12px; margin-top: 8px;">扩展表字段已自动合并到上方业务字段中。</p>
        </el-card>
    </div>
</template>

<script>
import { ArrowLeft, Check, Refresh, Plus } from "@element-plus/icons-vue";
import { ElMessage } from "element-plus";
import { BusinessDocApi, BusinessSchemaApi } from "@/utils/business-base";

export default {
    name: "business_doc_editor",
    components: { ArrowLeft, Check, Refresh, Plus },
    data() {
        return {
            loading: false,
            saving: false,
            tableName: "",
            docId: "",
            isAdd: true,
            schema: null,
            formData: {},
            detailItems: {}
        };
    },
    computed: {
        schemaLabel() {
            var d = this.schema;
            var label = (d && d.Label) || this.tableName;
            return label;
        },
        editableColumns() {
            if (!this.schema || !this.schema.Master || !this.schema.Master.Columns) return [];
            return this.schema.Master.Columns.filter(function (c) {
                var name = c.Name ? c.Name.toLowerCase() : "";
                return !c.IsSystem && name !== "id" && name !== "osclient";
            });
        },
        details() {
            return (this.schema && this.schema.Details) || [];
        },
        extensions() {
            return (this.schema && this.schema.Extensions) || [];
        }
    },
    mounted() {
        var action = this.$route.params.action || "add";
        this.tableName = this.$route.params.table || "";
        this.docId = this.$route.params.id || "";
        this.isAdd = (action === "add" || !this.docId);
        if (this.tableName) {
            this.loadSchema();
        } else {
            ElMessage.warning("缺少文档类型参数");
        }
    },
    methods: {
        detailColumns(detail) {
            if (!detail || !detail.Columns) return [];
            var fk = detail.ForeignKey ? detail.ForeignKey.toLowerCase() : "";
            return detail.Columns.filter(function (c) {
                var name = c.Name ? c.Name.toLowerCase() : "";
                return !c.IsSystem && name !== "id" && name !== "osclient" && name !== fk;
            });
        },
        // ── 类型判断 ──
        isTextType(col) {
            if (!col || !col.DataType) return false;
            var t = col.DataType.toLowerCase();
            return t.includes("text") || t === "longtext";
        },
        isIntType(col) {
            if (!col || !col.DataType) return false;
            var t = col.DataType.toLowerCase();
            return t === "int" || t === "integer" || t === "tinyint" || t === "smallint" || t === "bigint";
        },
        isFloatType(col) {
            if (!col || !col.DataType) return false;
            var t = col.DataType.toLowerCase();
            return t.includes("decimal") || t.includes("double") || t.includes("float") || t.includes("numeric");
        },
        isDateTimeType(col) {
            if (!col || !col.DataType) return false;
            var t = col.DataType.toLowerCase();
            return t.includes("datetime") || t.includes("timestamp");
        },
        isDateType(col) {
            if (!col || !col.DataType) return false;
            return col.DataType.toLowerCase() === "date";
        },
        isBoolType(col) {
            if (!col || !col.DataType) return false;
            var t = col.DataType.toLowerCase();
            return t === "bit" || t === "bool" || t === "boolean";
        },
        colSpan(col) {
            if (!col || !col.Name) return 12;
            if (this.isTextType(col)) return 24;
            var n = col.Name.toLowerCase();
            if (n === "remark" || n === "description") return 24;
            return 12;
        },
        getMin(col) {
            if (!col || !col.DataType) return -999999999;
            var t = col.DataType.toLowerCase();
            if (t === "tinyint") return 0;
            if (t === "smallint") return -32768;
            return -999999999;
        },
        getMax(col) {
            if (!col || !col.DataType) return 999999999;
            var t = col.DataType.toLowerCase();
            if (t === "tinyint") return 255;
            if (t === "smallint") return 32767;
            return 999999999;
        },
        statusTagType(val) {
            var num = Number(val);
            if (isNaN(num) || num <= 0) return "info";
            if (num === 1) return "primary";
            if (num === 2) return "warning";
            if (num === 3) return "success";
            return "danger";
        },
        statusLabel(val) {
            var labels = { 0: "草稿", 1: "已提交", 2: "处理中", 3: "已完成", 4: "已作废", 5: "已关闭" };
            return labels[val] || ("状态:" + val);
        },
        // ── 核心方法 ──
        async loadSchema() {
            try {
                var res = await BusinessSchemaApi.getDocumentSchema(this.tableName);
                if (res && res.Code === 1) {
                    this.schema = res.Data;
                    if (!this.isAdd) await this.loadData();
                } else {
                    ElMessage.error("加载表结构失败");
                }
            } catch (e) {
                ElMessage.error("加载表结构异常: " + (e.message || ""));
            }
        },
        async loadData() {
            if (this.isAdd || !this.docId) return;
            this.loading = true;
            try {
                var res = await BusinessDocApi.getModelWithRelations(this.tableName, this.docId);
                if (res && res.Code === 1 && res.Data) {
                    var obj = res.Data;
                    this.formData = {};
                    Object.keys(obj).forEach(function (key) {
                        if (key !== "OsClient") {
                            this.formData[key] = obj[key];
                        }
                    }.bind(this));
                    if (this.schema && this.schema.Details) {
                        this.detailItems = {};
                        this.schema.Details.forEach(function (detail) {
                            var propName = detail.PropertyName || detail.TableName;
                            var items = obj[propName];
                            this.detailItems[propName] = Array.isArray(items) ? items.map(function (item) {
                                return Object.assign({}, item);
                            }) : [];
                        }.bind(this));
                    }
                } else {
                    ElMessage.warning((res && res.Msg) || "未找到数据");
                }
            } catch (e) {
                ElMessage.error("加载数据异常: " + (e.message || ""));
            } finally {
                this.loading = false;
            }
        },
        async onSave() {
            this.saving = true;
            try {
                var data = Object.assign({}, this.formData);
                if (this.schema && this.schema.Details) {
                    this.schema.Details.forEach(function (detail) {
                        var propName = detail.PropertyName || detail.TableName;
                        data[propName] = this.detailItems[propName] || [];
                    }.bind(this));
                }
                var res = await BusinessDocApi.save(this.tableName, data);
                if (res && res.Code === 1) {
                    ElMessage.success("保存成功");
                    var newId = (res.Data && (res.Data.Id || res.Data.id)) || data.Id;
                    if (newId) {
                        this.docId = newId;
                        this.isAdd = false;
                        this.formData.Id = newId;
                    }
                } else {
                    ElMessage.error((res && res.Msg) || "保存失败");
                }
            } catch (e) {
                ElMessage.error("保存异常: " + (e.message || ""));
            } finally {
                this.saving = false;
            }
        },
        onRefresh() {
            this.loadData();
        },
        addDetailRow() {
            if (!this.schema || !this.schema.Details) return;
            var vm = this;
            this.schema.Details.forEach(function (detail) {
                var propName = detail.PropertyName || detail.TableName;
                if (!vm.detailItems[propName]) {
                    vm.detailItems[propName] = [];
                }
                vm.detailItems[propName].push({});
            });
        },
        removeDetailRow(detail, index) {
            var propName = detail.PropertyName || detail.TableName;
            if (this.detailItems[propName]) {
                this.detailItems[propName].splice(index, 1);
            }
        },
        goBack() {
            this.$router.push("/business/doc/list");
        }
    }
};
</script>

<style scoped>
.business-doc-editor {
    padding: 16px;
}
.header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
}
.header-actions {
    display: flex;
    gap: 8px;
}
</style>

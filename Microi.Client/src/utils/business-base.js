/**
 * 业务底座 API 工具库 (Business Base API Helper)
 * 
 * 封装与 `BusinessDocumentController`、`BusinessModuleMonitorController`、
 * `BusinessSchemaController`、`BusinessAuthController` 等通用业务后端 API 的交互。
 *
 * 用法：
 *   import { BusinessDocApi, BusinessMonitorApi, BusinessSchemaApi } from '@/utils/business-base';
 *   const list = await BusinessDocApi.getList('erp_sales_order', { _PageSize: 20 });
 */

import { DiyCommon } from '@/utils/diy.common';

// ── 内部封装：统一走 DiyCommon.PostAsync（自动携带 Token / OsClient） ──

async function post(url, params = {}) {
  const result = await DiyCommon.PostAsync(url, params, null, null, 'json');
  return result;
}

// ═══════════════════════════════════════════════════════════════
//  1. 业务文档通用 CRUD — 对应 BusinessDocumentController
// ═══════════════════════════════════════════════════════════════

export const BusinessDocApi = {
  /**
   * 获取业务文档列表（分页查询）
   * @param {string} masterTable - 表名/表单 Key
   * @param {object} params - 查询参数 { _Where, _PageIndex, _PageSize, _SortField, _SortType, ... }
   */
  getList(masterTable, params = {}) {
    return post('/api/BusinessDoc/GetList', { MasterTable: masterTable, ...params });
  },

  /**
   * 获取单条业务文档
   * @param {string} masterTable - 表名
   * @param {string} id - 记录 Id
   */
  getModel(masterTable, id) {
    return post('/api/BusinessDoc/GetModel', { MasterTable: masterTable, Id: id });
  },

  /**
   * 获取单条并自动合并扩展表列 + 加载明细集合
   * @param {string} masterTable - 表名
   * @param {string} id - 记录 Id
   */
  getModelWithRelations(masterTable, id) {
    return post('/api/BusinessDoc/GetModelWithRelations', { MasterTable: masterTable, Id: id });
  },

  /**
   * 新增业务文档
   * @param {string} masterTable - 表名
   * @param {object} data - 业务数据
   */
  add(masterTable, data) {
    return post('/api/BusinessDoc/Add', { MasterTable: masterTable, ...data });
  },

  /**
   * 修改业务文档
   * @param {string} masterTable - 表名
   * @param {object} data - 业务数据（必须包含 Id）
   */
  upt(masterTable, data) {
    return post('/api/BusinessDoc/Upt', { MasterTable: masterTable, ...data });
  },

  /**
   * 删除业务文档（含级联清理扩展表与明细表）
   * @param {string} masterTable - 表名
   * @param {string} id - 记录 Id
   */
  del(masterTable, id) {
    return post('/api/BusinessDoc/Del', { MasterTable: masterTable, Id: id });
  },

  /**
   * 保存完整业务文档（主单 + 扩展表 + 明细集合 Items）
   * @param {string} masterTable - 表名
   * @param {object} data - 完整 JSON 数据
   */
  save(masterTable, data) {
    return post('/api/BusinessDoc/Save', { MasterTable: masterTable, ...data });
  },

  /**
   * 执行状态流转（驱动单据状态机）
   * @param {string} masterTable - 表名
   * @param {string} id - 记录 Id
   * @param {string} trigger - 触发动作（Submit/Audit/Finish/Cancel）
   * @param {string} [operateRemark] - 操作附言
   */
  execute(masterTable, id, trigger, operateRemark) {
    return post('/api/BusinessDoc/Execute', { MasterTable: masterTable, Id: id, Trigger: trigger, OperateRemark: operateRemark || '' });
  },

  /**
   * 批量删除业务文档
   * @param {string} masterTable - 表名
   * @param {string[]} ids - 要删除的 Id 数组
   */
  delBatch(masterTable, ids) {
    return post('/api/BusinessDoc/DelBatch', { MasterTable: masterTable, Ids: ids });
  }
};

// ═══════════════════════════════════════════════════════════════
//  2. 业务模块监控 — 对应 BusinessModuleMonitorController
// ═══════════════════════════════════════════════════════════════

export const BusinessMonitorApi = {
  /** 所有模块状态概览 */
  getModules() {
    return post('/api/BusinessMonitor/Modules');
  },

  /** 指定模块详情 */
  getModule(key) {
    return post('/api/BusinessMonitor/Module', { key });
  },

  /** 已启动模块列表 */
  getStarted() {
    return post('/api/BusinessMonitor/Started');
  },

  /** 有错误模块列表 */
  getFaulted() {
    return post('/api/BusinessMonitor/Faulted');
  },

  /** 健康检查 */
  health() {
    return post('/api/BusinessMonitor/Health');
  }
};

// ═══════════════════════════════════════════════════════════════
//  3. 业务 Schema 管理 — 对应 BusinessSchemaController
// ═══════════════════════════════════════════════════════════════

export const BusinessSchemaApi = {
  /** 列出所有业务文档主表 */
  getDocuments(osClient) {
    return post('/api/BusinessSchema/GetDocuments', { OsClient: osClient });
  },

  /** 获取文档完整结构（主表 + 明细 + 扩展 + 列） */
  getDocumentSchema(masterTable, osClient) {
    return post('/api/BusinessSchema/GetDocumentSchema', { MasterTable: masterTable, OsClient: osClient });
  },

  /** 获取单表列结构 */
  getTableColumns(tableName, osClient) {
    return post('/api/BusinessSchema/GetTableColumns', { TableName: tableName, OsClient: osClient });
  },

  /** 动态加字段 */
  addField({ MasterTable, TargetTable, FieldName, DataType, Label, NotNull, Length, OsClient }) {
    return post('/api/BusinessSchema/AddField', { MasterTable, TargetTable, FieldName, DataType, Label, NotNull, Length, OsClient });
  },

  /** 获取字段配置 */
  getFieldConfigs(tableName, osClient) {
    return post('/api/BusinessSchema/GetFieldConfigs', { TableName: tableName, OsClient: osClient });
  },

  /** 批量保存字段配置 */
  saveFieldConfigs(configs, osClient) {
    return post('/api/BusinessSchema/SaveFieldConfigs', { Configs: configs, OsClient: osClient });
  },

  /** 绑定扩展/明细表关系 */
  bindRelation({ MasterTable, RelationTable, RelationType, ForeignKey, PropertyName, Label, OsClient }) {
    return post('/api/BusinessSchema/BindRelation', { MasterTable, RelationTable, RelationType, ForeignKey, PropertyName, Label, OsClient });
  },

  /** 解除关系绑定 */
  unbindRelation(relationId, masterTable, osClient) {
    return post('/api/BusinessSchema/UnbindRelation', { RelationId: relationId, MasterTable: masterTable, OsClient: osClient });
  }
};

// ═══════════════════════════════════════════════════════════════
//  4. 业务底座独立鉴权 — 对应 BusinessAuthController
// ═══════════════════════════════════════════════════════════════

export const BusinessAuthApi = {
  /** 业务底座管理员登录 */
  login(osClient, username, password) {
    return DiyCommon.Post('/api/BusinessAuth/Login', { OsClient: osClient, Username: username, Password: password }, null, null, 'json');
  },

  /** 验证 Token 是否有效 */
  verify(osClient, token) {
    return DiyCommon.Post('/api/BusinessAuth/Verify', { OsClient: osClient, Token: token }, null, null, 'json');
  },

  /** 修改管理员密码 */
  setPassword(osClient, token, oldPassword, newPassword) {
    return DiyCommon.Post('/api/BusinessAuth/SetPassword', { OsClient, Token: token, OldPassword: oldPassword, NewPassword: newPassword }, null, null, 'json');
  },

  /** 登出 */
  logout(osClient, token) {
    return DiyCommon.Post('/api/BusinessAuth/Logout', { OsClient: osClient, Token: token }, null, null, 'json');
  }
};

// ═══════════════════════════════════════════════════════════════
//  5. 本地 License 授权管理 — 对应 LocalLicenseController
// ═══════════════════════════════════════════════════════════════

export const LocalLicenseApi = {
  /** 获取当前服务器 HID */
  getHardwareId() {
    return post('/api/LocalLicense/GetHardwareId');
  },

  /** 验证本地 License 状态 */
  verify() {
    return post('/api/LocalLicense/Verify');
  },

  /** 获取 License 运行状态摘要（轻量，含心跳/宽限期/吊销信息） */
  getStatus() {
    return post('/api/LocalLicense/GetStatus');
  },

  /** 获取 License 心跳状态（不触发 Verify） */
  getHeartbeatStatus() {
    return post('/api/LocalLicense/GetHeartbeatStatus');
  },

  /** 获取 License 配置（ContactEmail 等） */
  getConfig() {
    return post('/api/LocalLicense/GetConfig');
  },

  /** 获取诊断信息（需登录） */
  diagnostics() {
    return post('/api/LocalLicense/Diagnostics');
  },

  /** 写入 License 文件到磁盘（前验证签名+HID+到期） */
  writeLicenseFile(licenseContent) {
    return post('/api/LocalLicense/WriteLicenseFile', { LicenseContent: licenseContent });
  },

  /** 生成离线注册申请文件 */
  generateRegistrationFile({ Company, Name, Phone, ProductType, Remark }) {
    return post('/api/LocalLicense/GenerateRegistrationFile', { Company, Name, Phone, ProductType, Remark });
  }
};

// ═══════════════════════════════════════════════════════════════
//  6. 插件管理 API — 对应 PluginAdminController
// ═══════════════════════════════════════════════════════════════

export const PluginApi = {
  /** 获取全部插件列表与状态 */
  list() {
    return post('/api/BusinessBase/Plugin/List');
  },

  /** 停止指定插件 */
  stop(key) {
    return post('/api/BusinessBase/Plugin/Stop', { Key: key });
  },

  /** 重新启动指定插件 */
  start(key) {
    return post('/api/BusinessBase/Plugin/Start', { Key: key });
  },

  /** 卸载指定插件 */
  unload(key) {
    return post('/api/BusinessBase/Plugin/Unload', { Key: key });
  },

  /** 获取指定插件的日志 */
  logs(key) {
    return post('/api/BusinessBase/Plugin/Logs', { Key: key });
  },

  /** 清空指定插件的日志 */
  clearLogs(key) {
    return post('/api/BusinessBase/Plugin/ClearLogs', { Key: key });
  }
};

// ═══════════════════════════════════════════════════════════════
//  7. 业务底座总控台 — 对应 BusinessBaseController
// ═══════════════════════════════════════════════════════════════

export const BusinessBaseApi = {
  /** 总控台仪表盘数据（模块/插件状态汇总） */
  getDashboard() {
    return post('/api/BusinessBase/GetDashboard');
  },

  /** 获取业务底座配置信息 */
  getConfig() {
    return post('/api/BusinessBase/GetConfig');
  },

  /** 健康检查 */
  health() {
    return post('/api/BusinessBase/Health');
  }
};

// ═══════════════════════════════════════════════════════════════
//  7. 默认导出：聚合所有 API
// ═══════════════════════════════════════════════════════════════

export default {
  BusinessDocApi,
  BusinessMonitorApi,
  BusinessSchemaApi,
  BusinessAuthApi,
  LocalLicenseApi,
  BusinessBaseApi,
  PluginApi
};

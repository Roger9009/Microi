using System;
using System.Threading.Tasks;
using Dos.Common;
using Dos.ORM;
using Newtonsoft.Json.Linq;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务服务基类（实体 CRUD 生命周期）。
    /// 封装平台 FormEngine 的增删改查，并为每个动作提供 Before/After 扩展钩子，
    /// 类似表单引擎的 V8 事件，子类可只重写关心的钩子来扩展业务规则。
    ///
    /// 约定：TParam 继承 BusinessParam（即平台 BaseParam），可直接使用 _Where/分页/_CurrentUser/OsClient。
    /// </summary>
    /// <typeparam name="TParam">业务参数类型</typeparam>
    public abstract class BusinessServiceBase<TParam>
        where TParam : BusinessParam
    {
        /// <summary>
        /// 对应的表单引擎 Key（即数据表标识），如 "erp_sales_order"。
        /// </summary>
        protected abstract string TableKey { get; }

        /// <summary>平台表单引擎实例。</summary>
        protected IFormEngine FormEngine => MicroiEngine.FormEngine;

        /// <summary>
        /// 主表实体类型（带 [BusinessTable] 及明细/扩展关系特性）。
        /// 重写后即可使用 GetModelWithRelationsAsync 进行扩展表合并与明细加载。
        /// </summary>
        protected virtual Type EntityType => null;

        /// <summary>
        /// 更新时是否根据字段配置(IsUpdate=false)自动忽略非更新字段。默认 true。
        /// </summary>
        protected virtual bool EnforceFieldConfigOnUpt => true;

        #region 查询

        /// <summary>获取列表（分页/条件由 _Where、_PageIndex、_PageSize 控制）。</summary>
        public virtual async Task<DosResultList<dynamic>> GetListAsync(TParam param, DbTrans trans = null)
        {
            await OnBeforeQueryAsync(param);
            var result = await FormEngine.GetTableDataAsync(TableKey, param, trans);
            await OnAfterQueryAsync(param, result);
            return result;
        }

        /// <summary>获取单条。</summary>
        public virtual async Task<DosResult<dynamic>> GetModelAsync(TParam param, DbTrans trans = null)
        {
            return await FormEngine.GetFormDataAsync(TableKey, param, trans);
        }

        /// <summary>
        /// 获取单条并合并扩展表列、加载明细集合（需重写 EntityType）。
        /// </summary>
        public virtual async Task<DosResult<dynamic>> GetModelWithRelationsAsync(TParam param, DbTrans trans = null)
        {
            var result = await FormEngine.GetFormDataAsync(TableKey, param, trans);
            if (result == null || result.Code != 1 || result.Data == null || EntityType == null)
                return result;

            var master = result.Data as JObject ?? JObject.FromObject(result.Data);
            await BusinessDocumentReader.EnrichAsync(master, EntityType, param.OsClient, trans);
            result.Data = master;
            return result;
        }

        #endregion

        #region 新增

        /// <summary>新增一条数据。</summary>
        public virtual async Task<DosResult> AddAsync(TParam param, DbTrans trans = null)
        {
            var check = await OnBeforeAddAsync(param);
            if (check != null && check.Code != 1) return check;

            var result = await FormEngine.AddFormDataAsync(TableKey, param, trans);

            if (result != null && result.Code == 1)
                await OnAfterAddAsync(param, result);
            return result;
        }

        #endregion

        #region 修改

        /// <summary>修改一条数据。</summary>
        public virtual async Task<DosResult> UptAsync(TParam param, DbTrans trans = null)
        {
            var check = await OnBeforeUptAsync(param);
            if (check != null && check.Code != 1) return check;

            await ApplyNonUpdatableFieldsAsync(param);

            var result = await FormEngine.UptFormDataAsync(TableKey, param, trans);

            if (result != null && result.Code == 1)
                await OnAfterUptAsync(param, result);
            return result;
        }

        #endregion

        #region 删除

        /// <summary>删除一条数据，并级联清理扩展表与明细表（需重写 EntityType）。</summary>
        public virtual async Task<DosResult> DelAsync(TParam param, DbTrans trans = null)
        {
            var check = await OnBeforeDelAsync(param);
            if (check != null && check.Code != 1) return check;

            var id = param?.Id;
            var ownTrans = trans == null && EntityType != null && !string.IsNullOrWhiteSpace(id);
            if (ownTrans)
            {
                var client = OsClientExtend.GetClient(param.OsClient);
                if (client?.Db != null) trans = client.Db.BeginTransaction();
                else ownTrans = false;
            }

            try
            {
                var result = await FormEngine.DelFormDataAsync(TableKey, param, trans);
                if (result == null || result.Code != 1)
                {
                    if (ownTrans) trans.Rollback();
                    return result ?? new DosResult(0, null, "删除主单失败。");
                }

                if (EntityType != null && !string.IsNullOrWhiteSpace(id))
                {
                    var relResult = await BusinessDocumentWriter.DeleteRelationsAsync(id, EntityType, param.OsClient, trans);
                    if (relResult != null && relResult.Code != 1)
                    {
                        if (ownTrans) trans.Rollback();
                        return relResult;
                    }
                }

                if (ownTrans) trans.Commit();
                await OnAfterDelAsync(param, result);
                return result;
            }
            catch (Exception ex)
            {
                if (ownTrans) trans.Rollback();
                return new DosResult(0, null, $"删除业务文档异常：{ex.Message}");
            }
            finally
            {
                if (ownTrans) trans?.Close();
            }
        }

        #endregion

        #region 主细扩展表保存

        /// <summary>
        /// 保存业务文档（主单 + 扩展表 + 明细表）。
        /// 入参为完整 JSON，可包含扩展字段及明细集合（如 Items[]）。
        /// 主单保存会走 AddAsync/UptAsync 及其生命周期钩子（如生成单据号、状态初始化）；
        /// 保存成功后，再同步扩展表与明细表。
        /// 需重写 <see cref="EntityType"/> 以识别关系特性。
        /// </summary>
        public virtual async Task<DosResult> SaveWithRelationsAsync(JObject masterData, string osClient, DbTrans trans = null)
        {
            if (EntityType == null)
                return new DosResult(0, null, $"[{GetType().Name}] 未重写 EntityType，无法保存关联表。");
            if (masterData == null)
                return new DosResult(0, null, "主单数据不能为空。");

            var client = OsClientExtend.GetClient(osClient);
            if (client?.Db == null)
                return new DosResult(0, null, $"租户[{osClient}]数据库会话不可用。");

            var check = await OnBeforeSaveWithRelationsAsync(masterData, osClient);
            if (check != null && check.Code != 1) return check;

            var ownTrans = trans == null;
            if (ownTrans)
                trans = client.Db.BeginTransaction();

            try
            {
                masterData["OsClient"] = osClient;
                var param = masterData.ToObject<TParam>();
                if (param == null) param = Activator.CreateInstance<TParam>();
                param.OsClient = osClient;

                var id = masterData["Id"]?.ToString();
                var isNew = string.IsNullOrWhiteSpace(id);

                DosResult masterResult;
                if (isNew)
                {
                    masterResult = await AddAsync(param, trans);
                }
                else
                {
                    masterResult = await UptAsync(param, trans);
                }

                if (masterResult == null || masterResult.Code != 1)
                {
                    if (ownTrans) trans.Rollback();
                    return masterResult ?? new DosResult(0, null, "主单保存失败。");
                }

                var masterObj = masterResult.Data as JObject ?? JObject.FromObject(masterResult.Data);
                id = masterObj["Id"]?.ToString() ?? id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    if (ownTrans) trans.Rollback();
                    return new DosResult(0, null, "主单保存后未返回 Id。");
                }

                masterData["Id"] = id;
                var relationsResult = await BusinessDocumentWriter.SaveRelationsAsync(masterData, EntityType, TableKey, osClient, trans);
                if (relationsResult != null && relationsResult.Code != 1)
                {
                    if (ownTrans) trans.Rollback();
                    return relationsResult;
                }

                if (ownTrans) trans.Commit();
                await OnAfterSaveWithRelationsAsync(masterData, osClient, masterResult);
                return masterResult;
            }
            catch (Exception ex)
            {
                if (ownTrans) trans.Rollback();
                return new DosResult(0, null, $"保存业务文档异常：{ex.Message}");
            }
            finally
            {
                if (ownTrans) trans?.Close();
            }
        }

        #endregion

        /// <summary>
        /// 依据字段配置，将「不参与更新」的字段并入 param._NotSaveField，使其在更新时被忽略。
        /// </summary>
        protected virtual async Task ApplyNonUpdatableFieldsAsync(TParam param)
        {
            if (!EnforceFieldConfigOnUpt || param == null) return;

            var nonUpdatable = await BusinessFieldConfigCache.GetNonUpdatableFields(TableKey, param.OsClient);
            if (nonUpdatable == null || nonUpdatable.Count == 0) return;

            var list = param._NotSaveField ?? new System.Collections.Generic.List<string>();
            foreach (var f in nonUpdatable)
                if (!list.Contains(f)) list.Add(f);
            param._NotSaveField = list;
        }

        #region 生命周期钩子（子类按需重写）

        /// <summary>查询前。可在此追加默认过滤（如租户、软删除）。</summary>
        protected virtual Task OnBeforeQueryAsync(TParam param) => Task.CompletedTask;

        /// <summary>查询后。可在此做数据脱敏、补充关联信息。</summary>
        protected virtual Task OnAfterQueryAsync(TParam param, DosResultList<dynamic> result) => Task.CompletedTask;

        /// <summary>新增前。返回非 null 且 Code!=1 的结果可中止新增（用于校验）。</summary>
        protected virtual Task<DosResult> OnBeforeAddAsync(TParam param) => Task.FromResult<DosResult>(null);

        /// <summary>新增后。</summary>
        protected virtual Task OnAfterAddAsync(TParam param, DosResult result) => Task.CompletedTask;

        /// <summary>修改前。返回非 null 且 Code!=1 的结果可中止修改。</summary>
        protected virtual Task<DosResult> OnBeforeUptAsync(TParam param) => Task.FromResult<DosResult>(null);

        /// <summary>修改后。</summary>
        protected virtual Task OnAfterUptAsync(TParam param, DosResult result) => Task.CompletedTask;

        /// <summary>删除前。返回非 null 且 Code!=1 的结果可中止删除。</summary>
        protected virtual Task<DosResult> OnBeforeDelAsync(TParam param) => Task.FromResult<DosResult>(null);

        /// <summary>删除后。</summary>
        protected virtual Task OnAfterDelAsync(TParam param, DosResult result) => Task.CompletedTask;

        /// <summary>保存主细扩展表前。返回非 null 且 Code!=1 的结果可中止保存。</summary>
        protected virtual Task<DosResult> OnBeforeSaveWithRelationsAsync(JObject masterData, string osClient) => Task.FromResult<DosResult>(null);

        /// <summary>保存主细扩展表后。</summary>
        protected virtual Task OnAfterSaveWithRelationsAsync(JObject masterData, string osClient, DosResult result) => Task.CompletedTask;

        #endregion
    }
}

using System;
using System.Threading.Tasks;
using Dos.Common;
using Dos.ORM;
using Newtonsoft.Json.Linq;

namespace Microi.net.Business
{
    /// <summary>
    /// 带单据状态机的业务服务基类。
    /// 在 CRUD 之上整合状态机生命周期：以平台原生的 JObject 作为状态机实体，
    /// 子类只需声明状态枚举并构建状态机，即可获得"提交/审核/完成/作废"等流转能力。
    /// </summary>
    /// <typeparam name="TParam">业务参数类型（需带 Trigger 字段）</typeparam>
    /// <typeparam name="TState">状态枚举类型</typeparam>
    public abstract class BusinessStatefulServiceBase<TParam, TState> : BusinessServiceBase<TParam>
        where TParam : BusinessParam
        where TState : struct, Enum
    {
        private readonly Lazy<BusinessStateMachine<JObject, TState>> _machine;

        protected BusinessStatefulServiceBase()
        {
            _machine = new Lazy<BusinessStateMachine<JObject, TState>>(() =>
            {
                var sm = new BusinessStateMachine<JObject, TState>(GetStatus, SetStatus);
                ConfigureStateMachine(sm);
                return sm;
            });
        }

        /// <summary>状态字段名（数据库列名），默认 "Status"。</summary>
        protected virtual string StatusField => "Status";

        /// <summary>状态机实例（首次访问时构建）。</summary>
        protected BusinessStateMachine<JObject, TState> StateMachine => _machine.Value;

        /// <summary>
        /// 子类在此声明合法流转与钩子，例如：
        /// sm.Permit(Draft, Submitted, "Submit").OnEnter(Submitted, ctx => ...);
        /// </summary>
        protected abstract void ConfigureStateMachine(BusinessStateMachine<JObject, TState> sm);

        /// <summary>
        /// 执行一次状态流转：根据 param.Id 加载单据 → 触发 param.Trigger → 成功后回写状态。
        /// </summary>
        public virtual async Task<DosResult> ExecuteTriggerAsync(TParam param, DbTrans trans = null)
        {
            if (string.IsNullOrWhiteSpace(param?.Id))
                return new DosResult(0, null, "缺少单据 Id，无法执行状态流转。");
            if (string.IsNullOrWhiteSpace(param.Trigger))
                return new DosResult(0, null, "缺少 Trigger 动作名，无法执行状态流转。");

            var modelResult = await FormEngine.GetFormDataAsync(TableKey, param, trans);
            if (modelResult == null || modelResult.Code != 1 || modelResult.Data == null)
                return new DosResult(0, null, "单据不存在或已被删除。");

            var entity = modelResult.Data as JObject ?? JObject.FromObject(modelResult.Data);

            var ctx = new StateContext<JObject, TState>
            {
                Entity = entity,
                Trigger = param.Trigger,
                CurrentUser = param._CurrentUser,
                OsClient = param.OsClient,
                OperateRemark = param.OperateRemark,
                Trans = trans
            };

            var fireResult = await StateMachine.FireAsync(ctx);
            if (!fireResult.Success)
                return new DosResult(0, null, fireResult.Msg);

            // 回写：仅持久化 Id + 状态字段 + 钩子可能修改的其它字段（整个 entity 回写）
            entity["Id"] = param.Id;
            var uptResult = await FormEngine.UptFormDataAsync(TableKey, entity, trans);
            if (uptResult == null || uptResult.Code != 1)
                return new DosResult(0, null, "状态已校验通过，但持久化失败：" + (uptResult?.Msg ?? "未知错误"));

            return new DosResult(1, new
            {
                Id = param.Id,
                From = fireResult.From.ToString(),
                To = fireResult.To.ToString(),
                Trigger = fireResult.Trigger
            }, fireResult.Msg);
        }

        /// <summary>从 JObject 读取当前状态。</summary>
        protected virtual TState GetStatus(JObject entity)
        {
            var token = entity[StatusField];
            if (token == null || token.Type == JTokenType.Null)
                return default;
            try
            {
                var intVal = token.Value<int>();
                return (TState)Enum.ToObject(typeof(TState), intVal);
            }
            catch
            {
                var str = token.ToString();
                return Enum.TryParse<TState>(str, true, out var parsed) ? parsed : default;
            }
        }

        /// <summary>把新状态写回 JObject（以 int 存储）。</summary>
        protected virtual void SetStatus(JObject entity, TState state)
        {
            entity[StatusField] = Convert.ToInt32(state);
        }
    }
}

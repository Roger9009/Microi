using System;
using Dos.ORM;
using Newtonsoft.Json.Linq;

namespace Microi.net.Business
{
    /// <summary>
    /// 状态流转上下文，贯穿一次 FireAsync 调用，供 guard / OnExit / OnEnter / OnTransition 钩子使用。
    /// </summary>
    /// <typeparam name="TEntity">业务实体类型</typeparam>
    /// <typeparam name="TState">状态枚举类型</typeparam>
    public sealed class StateContext<TEntity, TState>
        where TState : struct, Enum
    {
        /// <summary>业务实体（状态流转的对象）</summary>
        public TEntity Entity { get; set; }

        /// <summary>流转前的状态</summary>
        public TState From { get; set; }

        /// <summary>目标状态</summary>
        public TState To { get; set; }

        /// <summary>触发动作名</summary>
        public string Trigger { get; set; }

        /// <summary>当前操作用户</summary>
        public JObject CurrentUser { get; set; }

        /// <summary>租户标识</summary>
        public string OsClient { get; set; }

        /// <summary>操作附言（审核意见、作废原因等）</summary>
        public string OperateRemark { get; set; }

        /// <summary>可选共享数据库事务（与平台 FormEngine 共用事务）</summary>
        public DbTrans Trans { get; set; }

        /// <summary>钩子可写入此处用于阻止流转，并返回原因。</summary>
        public string RejectReason { get; private set; }

        /// <summary>是否已被钩子拒绝流转。</summary>
        public bool IsRejected => !string.IsNullOrEmpty(RejectReason);

        /// <summary>在钩子内拒绝本次流转。</summary>
        public void Reject(string reason)
        {
            RejectReason = reason;
        }
    }
}

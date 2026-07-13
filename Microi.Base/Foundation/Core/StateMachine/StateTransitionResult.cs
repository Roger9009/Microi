using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 状态流转结果。
    /// </summary>
    /// <typeparam name="TState">状态枚举类型</typeparam>
    public sealed class StateTransitionResult<TState>
        where TState : struct, Enum
    {
        /// <summary>是否流转成功</summary>
        public bool Success { get; set; }

        /// <summary>流转前状态</summary>
        public TState From { get; set; }

        /// <summary>流转后状态（成功时为目标状态，失败时等于 From）</summary>
        public TState To { get; set; }

        /// <summary>触发动作</summary>
        public string Trigger { get; set; }

        /// <summary>提示/失败原因</summary>
        public string Msg { get; set; }

        public static StateTransitionResult<TState> Ok(TState from, TState to, string trigger)
            => new StateTransitionResult<TState> { Success = true, From = from, To = to, Trigger = trigger, Msg = "流转成功" };

        public static StateTransitionResult<TState> Fail(TState from, string trigger, string msg)
            => new StateTransitionResult<TState> { Success = false, From = from, To = from, Trigger = trigger, Msg = msg };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务单据状态机（单据级生命周期）。
    /// 通过声明式 Permit 定义合法流转，配合 guard 守卫与 OnEnter/OnExit/OnTransition 钩子，
    /// 让"草稿→提交→审核→完成→作废"这类流程可配置、可校验、可扩展。
    ///
    /// 线程安全：构建（Permit/OnXxx）应在初始化期完成，之后只读触发 FireAsync。
    /// </summary>
    /// <typeparam name="TEntity">业务实体类型</typeparam>
    /// <typeparam name="TState">状态枚举类型</typeparam>
    public sealed class BusinessStateMachine<TEntity, TState>
        where TState : struct, Enum
    {
        private sealed class Transition
        {
            public TState From;
            public TState To;
            public string Trigger;
            public Func<StateContext<TEntity, TState>, Task<bool>> Guard;
        }

        private readonly List<Transition> _transitions = new List<Transition>();
        private readonly Dictionary<TState, List<Func<StateContext<TEntity, TState>, Task>>> _onEnter
            = new Dictionary<TState, List<Func<StateContext<TEntity, TState>, Task>>>();
        private readonly Dictionary<TState, List<Func<StateContext<TEntity, TState>, Task>>> _onExit
            = new Dictionary<TState, List<Func<StateContext<TEntity, TState>, Task>>>();
        private readonly Dictionary<string, List<Func<StateContext<TEntity, TState>, Task>>> _onTrigger
            = new Dictionary<string, List<Func<StateContext<TEntity, TState>, Task>>>(StringComparer.OrdinalIgnoreCase);

        private readonly Func<TEntity, TState> _stateGetter;
        private readonly Action<TEntity, TState> _stateSetter;

        /// <summary>
        /// 创建状态机。
        /// </summary>
        /// <param name="stateGetter">从实体读取当前状态</param>
        /// <param name="stateSetter">把新状态写回实体</param>
        public BusinessStateMachine(Func<TEntity, TState> stateGetter, Action<TEntity, TState> stateSetter)
        {
            _stateGetter = stateGetter ?? throw new ArgumentNullException(nameof(stateGetter));
            _stateSetter = stateSetter ?? throw new ArgumentNullException(nameof(stateSetter));
        }

        /// <summary>
        /// 声明一条合法流转：当处于 from 状态、触发 trigger 时，可流转到 to 状态。
        /// </summary>
        /// <param name="guard">可选守卫，返回 false 则不允许流转</param>
        public BusinessStateMachine<TEntity, TState> Permit(
            TState from, TState to, string trigger,
            Func<StateContext<TEntity, TState>, Task<bool>> guard = null)
        {
            _transitions.Add(new Transition { From = from, To = to, Trigger = trigger, Guard = guard });
            return this;
        }

        /// <summary>进入某状态后执行的钩子（如：生成单据编号、发通知）。</summary>
        public BusinessStateMachine<TEntity, TState> OnEnter(TState state, Func<StateContext<TEntity, TState>, Task> handler)
        {
            AddHandler(_onEnter, state, handler);
            return this;
        }

        /// <summary>离开某状态前执行的钩子。</summary>
        public BusinessStateMachine<TEntity, TState> OnExit(TState state, Func<StateContext<TEntity, TState>, Task> handler)
        {
            AddHandler(_onExit, state, handler);
            return this;
        }

        /// <summary>触发某动作时执行的钩子（无论目标状态）。</summary>
        public BusinessStateMachine<TEntity, TState> OnTransition(string trigger, Func<StateContext<TEntity, TState>, Task> handler)
        {
            if (!_onTrigger.TryGetValue(trigger, out var list))
            {
                list = new List<Func<StateContext<TEntity, TState>, Task>>();
                _onTrigger[trigger] = list;
            }
            if (handler != null) list.Add(handler);
            return this;
        }

        /// <summary>判断在 current 状态下是否允许触发 trigger。</summary>
        public bool CanFire(TState current, string trigger)
        {
            return _transitions.Any(t =>
                EqualityComparer<TState>.Default.Equals(t.From, current) &&
                string.Equals(t.Trigger, trigger, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>获取 current 状态下所有可用的触发动作。</summary>
        public IReadOnlyList<string> PermittedTriggers(TState current)
        {
            return _transitions
                .Where(t => EqualityComparer<TState>.Default.Equals(t.From, current))
                .Select(t => t.Trigger)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 执行一次状态流转。
        /// 顺序：匹配流转 → guard 守卫 → OnExit(from) → OnTransition(trigger) → 写入新状态 → OnEnter(to)。
        /// 任一钩子调用 ctx.Reject 或 guard 返回 false 时终止并返回失败。
        /// </summary>
        public async Task<StateTransitionResult<TState>> FireAsync(StateContext<TEntity, TState> ctx)
        {
            if (ctx == null || ctx.Entity == null)
                return StateTransitionResult<TState>.Fail(default, ctx?.Trigger, "实体为空，无法流转。");

            var current = _stateGetter(ctx.Entity);
            ctx.From = current;

            var transition = _transitions.FirstOrDefault(t =>
                EqualityComparer<TState>.Default.Equals(t.From, current) &&
                string.Equals(t.Trigger, ctx.Trigger, StringComparison.OrdinalIgnoreCase));

            if (transition == null)
            {
                return StateTransitionResult<TState>.Fail(current, ctx.Trigger,
                    $"当前状态[{current}]不允许执行动作[{ctx.Trigger}]。");
            }

            ctx.To = transition.To;

            // 守卫
            if (transition.Guard != null)
            {
                bool ok = await transition.Guard(ctx);
                if (!ok || ctx.IsRejected)
                {
                    return StateTransitionResult<TState>.Fail(current, ctx.Trigger,
                        ctx.IsRejected ? ctx.RejectReason : $"动作[{ctx.Trigger}]守卫校验未通过。");
                }
            }

            // 离开旧状态
            await RunHandlers(_onExit, current, ctx);
            if (ctx.IsRejected) return StateTransitionResult<TState>.Fail(current, ctx.Trigger, ctx.RejectReason);

            // 触发动作钩子
            if (_onTrigger.TryGetValue(ctx.Trigger, out var triggerHandlers))
            {
                foreach (var h in triggerHandlers)
                {
                    await h(ctx);
                    if (ctx.IsRejected) return StateTransitionResult<TState>.Fail(current, ctx.Trigger, ctx.RejectReason);
                }
            }

            // 写入新状态
            _stateSetter(ctx.Entity, transition.To);

            // 进入新状态
            await RunHandlers(_onEnter, transition.To, ctx);
            if (ctx.IsRejected)
            {
                // 进入钩子拒绝则回滚内存状态
                _stateSetter(ctx.Entity, current);
                return StateTransitionResult<TState>.Fail(current, ctx.Trigger, ctx.RejectReason);
            }

            return StateTransitionResult<TState>.Ok(current, transition.To, ctx.Trigger);
        }

        private static void AddHandler(
            Dictionary<TState, List<Func<StateContext<TEntity, TState>, Task>>> dict,
            TState state, Func<StateContext<TEntity, TState>, Task> handler)
        {
            if (handler == null) return;
            if (!dict.TryGetValue(state, out var list))
            {
                list = new List<Func<StateContext<TEntity, TState>, Task>>();
                dict[state] = list;
            }
            list.Add(handler);
        }

        private static async Task RunHandlers(
            Dictionary<TState, List<Func<StateContext<TEntity, TState>, Task>>> dict,
            TState state, StateContext<TEntity, TState> ctx)
        {
            if (dict.TryGetValue(state, out var list))
            {
                foreach (var h in list)
                {
                    await h(ctx);
                    if (ctx.IsRejected) return;
                }
            }
        }
    }
}

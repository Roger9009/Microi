using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务实体基类。
    /// 约定与低代码平台 diy 表的系统字段保持一致，便于直接落库到 FormEngine 表。
    /// 所有 ERP/MES 业务实体都应继承此类。
    /// </summary>
    public abstract class BusinessEntity
    {
        /// <summary>
        /// 主键（平台默认使用 Ulid/Guid 字符串）
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 租户标识（SaaS 多租户隔离）
        /// </summary>
        public string OsClient { get; set; }

        /// <summary>
        /// 操作人 Id（创建/最后修改人，对应平台 diy 表 UserId 字段）
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 操作人名称（对应平台 diy 表 UserName 字段）
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreateTime { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 软删除标记：0=正常，1=已删除
        /// </summary>
        public int? IsDeleted { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int? Sort { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }
    }

    /// <summary>
    /// 带状态机生命周期的业务实体基类。
    /// TState 通常是一个枚举，表示单据的状态（如：草稿、已提交、已审核、已完成、已作废）。
    /// </summary>
    /// <typeparam name="TState">状态枚举类型</typeparam>
    public abstract class BusinessStatefulEntity<TState> : BusinessEntity
        where TState : struct, Enum
    {
        /// <summary>
        /// 单据编号（业务可读编码，如 SO20260624001）
        /// </summary>
        public string BillNo { get; set; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public TState Status { get; set; }
    }
}

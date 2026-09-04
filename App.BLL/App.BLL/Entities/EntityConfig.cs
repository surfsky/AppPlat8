using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
//using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Entities
{
    /// <summary>
    /// 类库配置信息
    /// EntityConfig.Instance.OnGetDb = () => ....;
    /// </summary>
    public class EntityConfig
    {
        /// <summary>单例对象（线程安全）</summary>
        public static EntityConfig Instance = new Lazy<EntityConfig>().Value;

        /// <summary>数据库上下文（需配置 OnGetDb事件）</summary>
        public static DbContext Db => Instance.OnGetDb();

        /// <summary>获取数据库事件</summary>
        public event Func<DbContext> OnGetDb;

        /// <summary>获取当前请求的数据访问作用域事件</summary>
        public event Func<DataAccessScope> OnGetDataAccessScope;

        /// <summary>获取当前请求的数据录入审计上下文事件</summary>
        public event Func<DataAuditScope> OnGetDataAuditScope;

        /// <summary>
        /// 实体变更审计事件（宿主在 Startup 注册订阅，例如写 Logs 表、更新二级索引、广播消息）。
        /// 参数：(实体操作, 实体对象, 描述文本)
        ///   - Edit：描述 = 字段级 Diff 文本
        ///   - New / Delete：描述 = 主键 + 名称 概述
        /// 低层 Entity 只负责广播事件；具体副作用一律由宿主决定，保证类库单一性。
        /// </summary>
        public event Action<EntityOp, object, string> OnEntityAudit;

        /// <summary>当前请求的数据访问作用域</summary>
        public static DataAccessScope DataAccessScope => Instance.OnGetDataAccessScope?.Invoke();

        /// <summary>当前请求的数据录入审计上下文</summary>
        public static DataAuditScope DataAuditScope => Instance.OnGetDataAuditScope?.Invoke();

        /// <summary>触发实体变更审计事件（仅供 EntityBase 内部调用）</summary>
        internal static void RaiseEntityAudit(EntityOp op, object entity, string message)
            => Instance.OnEntityAudit?.Invoke(op, entity, message);
    }
}

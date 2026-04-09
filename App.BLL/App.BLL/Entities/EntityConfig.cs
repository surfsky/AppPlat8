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

        /// <summary>当前请求的数据访问作用域</summary>
        public static DataAccessScope DataAccessScope => Instance.OnGetDataAccessScope?.Invoke();

        /// <summary>当前请求的数据录入审计上下文</summary>
        public static DataAuditScope DataAuditScope => Instance.OnGetDataAuditScope?.Invoke();

    }
}

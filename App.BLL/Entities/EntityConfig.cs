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

    }
}

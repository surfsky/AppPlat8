using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Web;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
//using App.Components;
using App.Utils;
//using System.Data.Entity;
//using System.Data.Entity.Infrastructure;
using Microsoft.EntityFrameworkCore;
//using EntityFramework.Extensions;

namespace App.Entities
{

    /// <summary>
    /// 实体操作方式
    /// </summary>
    public enum EntityOp
    {
        [UI("新增")]  New,
        [UI("编辑")]  Edit,
        [UI("删除")]  Delete,
    }

    /// <summary>
    /// 使用 SnowflakeId 算法创建Id
    /// </summary>
    public class SnowflakeIdAttribute : Attribute { }

    /// <summary>
    /// 数据实体基类，定义了
    /// （1） 公共属性（如id的生成、历史附表、资源附表等）。
    /// （2）操作历史附表（History）：可用于记录流程流转历史。
    /// （3）资源附表（Res）的增删逻辑：可用于存储与实体相关的资源（如图片、文件等）。
    /// （4）数据导出逻辑：可用于将实体数据导出到客户端用的接口字段。
    /// 数据操作请用泛型类 EntityBase&lt;T&gt;
    /// </summary>
    public class EntityBase : IId, ILogChange, IExport
    {
        /// <summary>Id字段。如要自定义数据库字段名，请重载并加上[Column("XXXId")]</summary>
        /// <remarks>此处用virtual标注，不会在本表中生成数据库字段，而在子类表中生成字段</remarks>
        [Key]
        [UI("Id", Mode=PageMode.View | PageMode.Edit, ReadOnly=true)]
        //[DatabaseGenerated(DatabaseGeneratedOption.None)]
        //[SnowflakeId]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public virtual long Id { get; set; }
        
        [UI("创建时间", Mode=PageMode.Edit, ReadOnly=true)]
        public virtual DateTime? CreateDt { get; set; }  // 在保存时会自动设置
        
        [UI("更改时间", Mode=PageMode.Edit, ReadOnly = true)]  
        public virtual DateTime? UpdateDt { get; set; }  // 在更新时会自动设置

        [UI("创建人")]  public virtual long? CreatorId { get; set; }
        [UI("责任人")]  public virtual long? OwnerId { get; set; }


        public void SetOwner(long ownerId)
        {
            OwnerId = ownerId;
        }

        //---------------------------------------------
        // 虚拟方法
        //---------------------------------------------
        /// <summary>保存前处理（如设置某些计算字段）</summary>
        public virtual void BeforeSave(EntityOp op)  {}

        /// <summary>数据 CURD 更改后处理（如统计、刷新缓存）</summary>
        public virtual void AfterChange(EntityOp op) {}

        /// <summary>删除相关数据（如相关表，级联数据）</summary>
        public virtual void OnDeleteReference(long id) { }

        /// <summary>删除</summary>
        public virtual void Delete(bool log = false)
        {
            Log(log, "删除", this.Id, this);
            // 逻辑删除
            if (this is IDeleteLogic)
            {
                (this as IDeleteLogic).InUsed = false;
            }
            // 物理删除
            else
            {
                // 级联删除附属资源
                DeleteAtt();
                DeleteHistories();
                OnDeleteReference(this.Id);
                var entry = Db.Entry(this);
                entry.State = EntityState.Deleted;
            }
            Db.SaveChanges();

            // 删除后处理
            AfterChange(EntityOp.Delete);
        }


        //---------------------------------------------
        // 数据库
        //---------------------------------------------
        /// <summary>数据上下文</summary>
        public static DbContext Db => EntityConfig.Db;

        /// <summary>设置状态为已修改</summary>
        public void SetModified()
        {
            Db.Entry(this).State = EntityState.Modified;
        }


        //---------------------------------------------
        // 日志
        // 可考虑做成事件暴露给调用者
        //---------------------------------------------
        protected void Log(bool save, string action, object id, object data)
        {
            Log(save, action, id, data, this.GetType());
        }
        protected static void Log(bool save, string action, object id, object data, Type type)
        {
            if (!save) return;
            string json = Jsonlizer.ToJson(data, 20, true, true);  // 序列化为json，并跳过复杂的属性
            string txt = string.Format("{0}: Id={1}, Type={2}, Data={3}", action, id, type, json);
            UtilConfig.Log("Database", txt);
        }




        //---------------------------------------------
        // 导出
        //---------------------------------------------
        /// <summary>获取导出对象（可用于接口数据输出）</summary>
        /// <param name="type">导出详细信息还是概述信息</param>
        /// <remarks>
        /// - 统一用 IExport 接口，以功能换性能。
        /// - 不采用 Expression 的原因：有些复杂导出属性要用方法生成，无法被EF解析。
        /// - 对于字段实在太多的类，如有有性能问题，可先 Select 后再 Export，注意字段要一致。
        /// - 可标注属性 [UI("xx", Export=ExportType.Detail]，并用默认 Export 方法导出。
        /// </remarks>
        /// <example>
        /// var item = User.Get(..).Export();
        /// var items = User.Search(....).ToList().Cast(t => t.Export());
        /// </example>
        /// <todo>
        /// 现有的Export区分三种类型的代码非常繁琐（参考User.Export），故可考虑自动拼装属性，逻辑如：
        /// - 子类根据 ExportType 分别导出各自属性（不重叠）
        /// - 基类的方法自动组装这些字段（Detail包含Normal, Normal包含Simple）
        /// - 可用字典，或用动态类实现该逻辑
        /// </todo>
        public virtual object Export(ExportMode type = ExportMode.Normal)
        {
            return this;
        }
        /// <summary>导出json</summary>
        public string ExportJson(ExportMode type = ExportMode.Normal)
        {
            return Export(type).ToJson();
        }

        //---------------------------------------------
        // 全局唯一键
        //---------------------------------------------
        [NotMapped, JsonIgnore]
        [UI("全局唯一Id")]
        public string UniId
        {
            get
            {
                var type = this.GetType();
                if (type.FullName.Contains("System.Data.Entity.DynamicProxies"))
                    type = type.BaseType;
                return BuildUniId(type.Name, this.Id);
            }
        }
        protected static string BuildUniId(string prefix, long id)
        {
            return string.Format("{0}-{1}", prefix, id);
        }


        //---------------------------------------------
        // 操作历史
        //---------------------------------------------
        [NotMapped]
        [UI("操作历史", Column = ColumnType.None, Editor = EditorType.None, Export = ExportMode.Detail)]
        public List<History> Histories
        {
            get
            {
                var key = this.UniId;
                var items = History.Set.Where(t => t.Key == key).OrderBy(t => t.CreateDt).ToList();
                return (items.Count == 0) ? null : items;
            }
        }

        [NotMapped]
        [UI("最后操作历史", Column = ColumnType.None, Editor = EditorType.None, Export = ExportMode.Detail)]
        public History LastHistory
        {
            get
            {
                var key = this.UniId;
                return History.Search(key: key).Sort(t => t.CreateDt, false).FirstOrDefault();
            }
        }

        /// <summary>增加操作历史</summary>
        public History AddHistory(
            long? userId, string userName, string userMobile, 
            string statusName, int? status = null, 
            string remark = "", List<string> fileUrls = null
            )
        {
            return History.AddHistory(
                this.UniId, 
                userId, 
                statusName,
                statusId: status,
                userName: userName,
                userMobile:  userMobile,
                remark: remark,
                fileUrls: fileUrls
                );
        }

        /// <summary>删除附属历史</summary>
        public void DeleteHistories()
        {
            History.DeleteBatch(this.UniId);
        }

        //---------------------------------------------
        // 附件资源
        //---------------------------------------------
        [NotMapped, JsonIgnore] public List<string> AttUrls => Atts?.Select(t => t.Url).ToList();
        [NotMapped, JsonIgnore] public List<string> ImageUrls => Atts?.Where(t => t.Type == AttType.Image).Select(t => t.Url).ToList();

        // 附件
        [NotMapped]
        [UI("所有附件", Column = ColumnType.None, Editor = EditorType.None, Export = ExportMode.Detail)]
        public List<Att> Atts
        {
            get
            {
                var key = this.UniId;
                var items = Att.Set.Where(t => t.Key == key).OrderBy(t => t.SortId).ToList();
                return (items.Count == 0) ? null : items;  // 如果数目为空，强制输出null，以简化json结构
            }
        }

        [NotMapped, JsonIgnore]
        [UI("图片附件", Column = ColumnType.None, Editor = EditorType.None, Export = ExportMode.Detail)]
        public List<Att> Images
        {
            get
            {
                var key = this.UniId;
                var items = Att.Set.Where(t => t.Key == key).Where(t => t.Type == AttType.Image).OrderBy(t => t.SortId).ToList();
                return (items.Count == 0) ? null : items;
            }
        }

        // files
        [NotMapped, JsonIgnore]
        [UI("文件附件", Column = ColumnType.None, Editor = EditorType.None, Export = ExportMode.Detail)]
        public List<Att> Files
        {
            get
            {
                var key = this.UniId;
                var items = Att.Set.Where(t => t.Key == key).Where(t => t.Type == AttType.File).OrderBy(t => t.SortId).ToList();
                return (items.Count == 0) ? null : items;
            }
        }

        /// <summary>删除附件</summary>
        public void DeleteAtt()
        {
            Att.DeleteBatch(this.UniId);
        }

        /// <summary>删除附件</summary>
        public static void DeleteAtt(Type type, long id)
        {
            //string uniId = BuildUniId(typeof(T).Name, id);
            string uniId = BuildUniId(type.Name, id);
            Att.DeleteBatch(uniId);
        }

        /// <summary>添加附件</summary>
        public void AddAtt(List<string> fileUrls)
        {
            string key = this.UniId;
            foreach (var url in fileUrls)
                Att.Add(AttType.File, key, url);
        }


        //---------------------------------------------
        // UI 配置(供UI自动化生成使用）
        // 理论上用静态方法更合适，但现在的interface不支持静态方法，先这么用吧。
        //---------------------------------------------
        /// <summary>网格设置信息</summary>
        public virtual UISetting GridUI()
        {
            return new UISetting(this.GetType());
        }
        /// <summary>表单设置i信息</summary>
        public virtual UISetting FormUI()
        {
            return new UISetting(this.GetType());
        }
        public virtual UISetting SearchUI()
        {
            var m = GetSearchMethod(this.GetType());
            if (m != null)
                return new UISetting(m);
            return null;
        }
        /// <summary>找到实体检索方法（具有[SearcherAttribute]，若没有则尝试找名称为"Search"的方法）</summary>
        static MethodInfo GetSearchMethod(Type type)
        {
            foreach (var m in type.GetMethods())
            {
                if (m.GetAttribute<SearcherAttribute>() != null)
                    return m;
            }
            return type.GetMethods("Search", false).FirstOrDefault();
        }
    }
}
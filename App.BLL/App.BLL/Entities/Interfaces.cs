using App.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace App.Entities
{
    //-------------------------------------------------------
    // 实体属性接口
    //-------------------------------------------------------
    /// <summary>记录数据操作日期</summary>
    public interface ILogChange
    {
        DateTime? CreateDt { get; set; }
        DateTime? UpdateDt { get; set; }
    }


    /// <summary>逻辑删除接口</summary>
    public interface IDeleteLogic
    {
        bool? IsDel { get; set; }
    }

    /// <summary>排序索引接口</summary>
    public interface ISort
    {
        /// <summary>排序索引</summary>
        int SortId { get; set; }
    }


    /// <summary>是否检测并发冲突</summary>
    public interface ICollsionDetect
    {
        /// <summary>并发冲突Id。保存时如果与库中不一致则抛出异常</summary>
        [ConcurrencyCheck]
        int? CollisionId { get; set; }
    }


    /// <summary>树接口</summary>
    public interface ITree<T> : IId, ISort, IClone<T>
    {
        /// <summary>名称</summary>
        string Name { get; set; }

        /// <summary>父Id</summary>
        long? ParentId { get; set; }

        /// <summary>菜单在树形结构中的层级（从0开始）</summary>
        int TreeLevel { get; set; }

        /// <summary>子节点列表</summary>
        List<T> Children { get; set; }
    }


    /// <summary>
    /// 缓存数据接口。这是一个标注接口，无任何成员。
    /// 标注了本接口的类，将全部从缓存中获取数据，以加快响应速度。（考虑删除，直接用 All属性）
    /// </summary>
    public interface ICacheAll
    {
    }


    //-------------------------------------------------------
    // 实体方法接口
    //-------------------------------------------------------
    /// <summary>导出数据接口（供输出json给客户端用）</summary>
    public interface IExport
    {
        object Export(ExportMode type);
    }

    /// <summary>克隆接口</summary>
    public interface IClone<T>
    {
        T Clone();
    }

    /// <summary>初始化数据接口</summary>
    public interface IInit
    {
        /// <summary>批量初始化数据</summary>
        static abstract void Init();
    }

    /// <summary>修正数据接口</summary>
    /// <remarks>用于修正数据错误或填充某些字段，如补充缺失的默认值。</remarks>
    public interface IFix<T>
    {
        /// <summary>修正实体自身数据</summary>
        T Fix();
    }

    /// <summary>批量修正数据接口</summary>
    public interface IFixAll
    {
        /// <summary>批量修正数据</summary>
        static abstract int FixAll();
    }

}
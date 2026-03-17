using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;


namespace App.DAL
{
    /// <summary>
    /// 序列号类别
    /// </summary>
    public enum SequenceType
    {
        [UI("订单")]   Order,
    }


    /// <summary>
    /// 循环方式
    /// </summary>
    public enum LoopType
    {
        [UI("年")] Year,
        [UI("月")] Month,
        [UI("日")] Day,
    }

    /// <summary>序列号</summary>
    [UI("系统", "序列号生成器配置")]
    public class Sequence : EntityBase<Sequence>
    {
        [UI("类别")]          public SequenceType? Type { get; set; }
        [UI("循环方式")]      public LoopType? Loop { get; set; }
        [UI("格式")]          public string Format { get; set; }
        [UI("最后时间")]      public DateTime? LastDt { get; set; }
        [UI("最后值")]        public long? LastValue { get; set; }
        [UI("最后序列值")]    public string LastSeq { get; set; }

        //
        [UI("类别")]          public string TypeName { get { return Type.GetTitle(); } }
        [UI("循环方式")]      public string LoopName { get { return Loop.GetTitle(); } }

        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        /// <summary>创建订单序列号发生器</summary>
        public static Sequence GetSequence(SequenceType type)
        {
            var seq = Set.FirstOrDefault(t => t.Type == type);
            if (seq == null)
            {
                seq = new Sequence();
                seq.Type = type;
                seq.Loop = LoopType.Day;
                seq.Format = "{0:yyyyMMdd}{1:000000}";
                seq.LastDt = new DateTime(2019, 1, 1);
                seq.LastValue = 0;
                seq.Save();
            }
            return seq;
        }

        /// <summary>查询</summary>
        public static IQueryable<Sequence> Search(SequenceType? type)
        {
            IQueryable<Sequence> q = Set;
            if (type != null) q = q.Where(t => t.Type == type);
            return q;
        }

        // 加锁避免业务数据冲突
        static object _lock = new object();

        /// <summary>生成新序列号</summary>
        public static string Generate(SequenceType type)
        {
            lock (_lock)
            {
                var seq = GetSequence(type);
                return seq.Generate();
            }
        }

        /// <summary>生成新序列号</summary>
        public string Generate()
        {
            var now = DateTime.Now;
            long newValue = CalcNewValue(this.Loop, this.LastDt, this.LastValue, now);

            // 输出序列号
            var txt = string.Format(this.Format, now, newValue);
            this.LastDt = now;
            this.LastValue = newValue;
            this.LastSeq = txt;
            this.Save();
            return txt;
        }

        /// <summary>计算新序列值</summary>
        private static long CalcNewValue(LoopType? loop, DateTime? lastDt, long? lastValue, DateTime now)
        {
            if (lastDt == null || lastValue == null || loop == null)
                return 1;

            var dt = lastDt.Value;
            if (loop == LoopType.Year)
            {
                if (now.Year > dt.Year)
                    return 1;
            }
            else if (loop == LoopType.Month)
            {
                if (now.Year > dt.Year || now.Month > dt.Month)
                    return 1;
            }
            else if (loop == LoopType.Day)
            {
                if (now.Year > dt.Year || now.Month > dt.Month || now.Day > dt.Day)
                    return  1;
            }
            return lastValue.Value + 1;
        }

    }
}
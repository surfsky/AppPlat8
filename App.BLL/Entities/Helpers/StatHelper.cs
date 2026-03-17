using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using App.Utils;

namespace App.Entities
{
    /// <summary>
    /// 统计数据项（统计及报表用到）。格式如 {Name:"商品1", Step:"201901", Value:13}
    /// </summary>
    public class StatItem
    {
        public string Name { get; set; }
        public string Step { get; set; }
        public double Value { get; set; }

        /// <summary>统计数据项</summary>
        /// <param name="name">名称.如“商品1”</param>
        /// <param name="step">步骤名称。如“201902”</param>
        /// <param name="value">值。如“23”</param>
        public StatItem(string name, string step, double value)
        {
            this.Name = name;
            this.Step = step;
            this.Value = value;
        }
    }

    /// <summary>
    /// 统计帮助类，可统计日、月、年等数据。
    /// </summary>
    public class StatHelper
    {
        //---------------------------------------------
        // 统计
        //---------------------------------------------
        /// <summary>日统计</summary>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="whereExpression"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<StatItem> StatDayNew<T>(DateTime startDt, DateTime? endDt, Expression<Func<T, bool>> whereExpression = null)
            where T : EntityBase<T>, new()
        {
            var q = EntityConfig.Db.Set<T>().Where(t => t.CreateDt >= startDt);
            if (endDt != null) q = q.Where(t => t.CreateDt <= endDt);
            if (whereExpression != null) q = q.Where(whereExpression);

            // 每日的增量数据（未测试）
            var items = q
                .GroupBy(t => new
                {
                    //Day = DbFunctions.TruncateTime(t.CreateDt).Value
                    Day = t.CreateDt.Value.Date
                    //Day = EF.Functions..
                })
                .Select(t => new
                {
                    Day = t.Key.Day,
                    Cnt = t.Count()
                })
                .OrderBy(t => new { t.Day })
                .ToList()
                .Select(t => new StatItem("", t.Day.ToString("MMdd"), t.Cnt))
                .ToList()
                ;
            ;
            return items;
        }

        /// <summary>日存量统计</summary>
        public static List<StatItem> StatDayAmount<T>(DateTime startDt, DateTime? endDt, Expression<Func<T, bool>> whereExpression = null)
            where T : EntityBase<T>, new()
        {
            var n = EntityConfig.Db.Set<T>().Where(t => t.CreateDt < startDt).Where(whereExpression).Count();    // 初始数据
            var items = StatDayNew(startDt, endDt, whereExpression); // 每日新增数据
            return ToAmountData(items, n);
        }

        /// <summary>将日数据转化为累计数据</summary>
        public static List<StatItem> ToAmountData(List<StatItem> items, int baseAmount)
        {
            // 存量 = 之前量 + 今日增量
            return items
                .Each2((item, preItem) => item.Value = item.Value + (preItem?.Value ?? 0))
                .Each(t => t.Value += baseAmount)
                ;
        }
    }
}

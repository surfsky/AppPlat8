using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{

    [UI("OA", "预算支付状态")]
    public enum BudgetPayStatus
    {
        [UI("规划")]          Plan,
        [UI("未支付")]        Unpaid,
        [UI("部分支付")]      PartialPaid,
        [UI("已支付")]        Paid
    }

    [UI("OA", "预算分类")]
    public class BudgetType : EntityBase<BudgetType>
    {
        [UI("名称")]    public string Name { get; set; }

        public static IQueryable<BudgetType> Search(string name)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())       q = q.Where(o => o.Name.Contains(name.Trim()));
            return q;
         }
    }

    /// <summary>预算</summary>
    [UI("OA", "预算")]
    public class Budget : EntityBase<Budget>
    {
        [UI("预算年份")]        public int Year { get; set; }
        [UI("组织")]           public long? OrgId { get; set; }
        [UI("预算分类")]        public long? TypeId { get; set; }
        [UI("名称")]           public string Name { get; set; }
        [UI("厂商")]           public string Company { get; set; }
        [UI("关联项目")]        public string Project { get; set; }
        [UI("金额")]           public decimal? Amount { get; set; }
        [UI("备注")]           public string Remark { get; set; }
        [UI("支付日期")]        public DateTime? PayDt { get; set; }
        [UI("支付状态")]        public BudgetPayStatus? PayStatus { get; set; }

        public virtual Org Org { get; set; }
        public virtual BudgetType Type { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Year,
                OrgId,
                OrgName = Org?.Name,
                TypeId,
                TypeName = Type?.Name,
                Name,
                Company,
                Project,
                PayDt,
                PayStatus,
                Amount,
                Remark, 
                CreateDt,
                UpdateDt
            };
        }

        public static IQueryable<Budget> Search(string name,int? year, long? orgId, long? typeId, BudgetPayStatus? payStatus)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())     q = q.Where(o => o.Name.Contains(name.Trim()));
            if (year.IsNotEmpty())      q = q.Where(o => o.Year == year.Value);
            if (orgId.IsNotEmpty())    q = q.Where(o => o.OrgId == orgId.Value);
            if (typeId.IsNotEmpty())   q = q.Where(o => o.TypeId == typeId.Value);
            if (payStatus.IsNotEmpty()) q = q.Where(o => o.PayStatus == payStatus.Value);
            return q;
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>项目管理</summary>
    [UI("OA", "项目")]
    public class Project : EntityBase<Project>
    {
        [UI("名称")]        public string Name { get; set; }
        [UI("简称别称")]     public string Alias { get; set; }
        [UI("组织")]        public long? OrgId { get; set; }
        [UI("责任人")]      public string PersonInCharge { get; set; }
        [UI("开发厂商")]     public string DevCompany { get; set; }
        [UI("维护厂商")]     public string MaintCompany { get; set; }
        [UI("合同时间")]     public DateTime? ContractDt { get; set; }
        [UI("进度百分比")]   public float? Progress { get; set; }

        public virtual Org Org { get; set; }


        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                Alias,
                OrgId,
                OrgName = Org?.Name,
                PersonInCharge,
                DevCompany,
                MaintCompany,
                ContractDt,
                Progress
            };
        }

        public static IQueryable<Project> Search(string name, long? orgId)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())          q = q.Where(o => o.Name.Contains(name.Trim()));
            if (orgId.IsNotEmpty())        q = q.Where(o => o.OrgId == orgId.Value);
            return q;
        }
    }

    /// <summary>项目进度历史</summary>
    [UI("OA", "项目进度历史")]
    public class ProjectLog : EntityBase<ProjectLog>
    {
        [UI("项目Id")]      public long? ProjectId { get; set; }
        [UI("时间")]        public DateTime LogDt { get; set; }
        [UI("人员")]        public string Person { get; set; }
        [UI("状态")]        public string Status { get; set; }
        [UI("说明")]        public string Description { get; set; }
        [UI("进度百分比")]   public float? Progress { get; set; }

        public virtual Project Project { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                ProjectId,
                LogDt,
                Person,
                Status,
                Description,
                Progress
            };
        }

        public static IQueryable<ProjectLog> Search(long? projectId)
        {
            var q = IncludeSet.AsQueryable();
            if (projectId.IsNotEmpty()) q = q.Where(o => o.ProjectId == projectId.Value);
            return q;
        }
    }
}

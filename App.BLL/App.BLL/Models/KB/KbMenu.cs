using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

namespace App.DAL
{
    /// <summary>文档目录</summary>
    [UI("KB", "目录")]
    public class KbMenu : TreeEntity<KbMenu>
    {
        [UI("归属组织")] public long? OrgId { get; set; }

        public virtual Org Org { get; set; }

        [NotMapped, UI("归属组织")] public string OrgName => Org?.Name;
        [NotMapped, UI("归属组织全称")] public string OrgFullName => Org?.FullName ?? Org?.Name;

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                SortId,
                FullName,
                TreeLevel,
                OrgId,
                OrgName,
                OrgFullName,
                Children = Children?.Select(t => t.Export(type)).ToList()
            };
        }

        public static IQueryable<KbMenu> Search(string name)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty()) q = q.Where(o => o.Name.Contains(name.Trim()));
            return q;
        }
    }
}

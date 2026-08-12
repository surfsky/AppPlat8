using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>文档目录</summary>
    [UI("KB", "目录")]
    public class KbMenu : TreeEntity<KbMenu>
    {
        public static IQueryable<KbMenu> Search(string name)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty()) q = q.Where(o => o.Name.Contains(name.Trim()));
            return q;
        }
    }
}

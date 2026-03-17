using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;
using Newtonsoft.Json;

namespace App.DAL
{
    /// <summary>文档目录</summary>
    [UI("OA", "文档目录")]
    public class ArticleDir : TreeEntity<ArticleDir>
    {
        public static IQueryable<ArticleDir> Search(string name)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty()) q = q.Where(o => o.Name.Contains(name.Trim()));
            return q;
        }
    }
}

using System.Linq;
using App.DAL;
using App.HttpApi;
using Microsoft.EntityFrameworkCore;

namespace App.API
{
    /// <summary>参考网站接口</summary>
    public class Sites
    {
        /// <summary>获取网站分组</summary>
        [HttpApi("获取参考网站分组", AuthLogin = true)]
        public static APIResult GetSiteGroups()
        {
            var list = Site.Set.AsNoTracking()
                .OrderBy(t => t.Type)
                .ThenBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList();

            var groups = list
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Type) ? "其它" : t.Type.Trim())
                .Select(g => new
                {
                    Type = g.Key,
                    Items = g.Select(t => t.Export()).ToList()
                })
                .ToList();

            return groups.ToResult();
        }
    }
}

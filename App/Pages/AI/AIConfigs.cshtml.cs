using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.AI
{
    [CheckPower(Power.ConfigAI)]
    public class AIConfigsModel : AdminModel
    {
        public AIConfig Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(Paging pi, string name)
        {
            var list = AIConfig.Search(name, null).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (!CheckPower(Power.ConfigAI))
                return BuildResult(403, "无权操作");
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");

            foreach (var id in ids)
                AIConfig.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}

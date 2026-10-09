using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Maintains
{
    [Auth(Power.Admin)]
    public class IPFilterFormModel : AuthModel
    {
        public IPFilter Item { get; set; }

        public void OnGet(){}

        public IActionResult OnGetData(long id)
        {
            var item = IPFilter.GetDetail(id) ?? new IPFilter();
            return BuildResult(0, "success", item.Export(ExportMode.Detail));
        }

        public IActionResult OnPostSave([FromBody] IPFilter req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (string.IsNullOrWhiteSpace(req.IP))
                return BuildResult(400, "IP不能为空");
            if (!CheckPower(Power.Admin))
                return BuildResult(403, "无权操作");

            IPFilter item;
            if (req.Id > 0)
            {
                item = IPFilter.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "IP过滤不存在");
            }
            else
            {
                item = new IPFilter();
            }

            item.IP = req.IP?.Trim();
            item.StartDt = req.StartDt;
            item.EndDt = req.EndDt;
            item.Remark = req.Remark;
            item.Save();
            return BuildResult(0, "保存成功", new { id = item.Id });
        }
    }
}

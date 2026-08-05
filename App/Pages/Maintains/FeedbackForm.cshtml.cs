using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Maintains
{
    [Auth(Power.FeedBackView)]
    public class FeedbackFormModel : AdminModel
    {
        public Feedback Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(long id)
        {
            var item = Feedback.GetDetail(id) ?? new Feedback();
            if (id == 0)
            {
                var user = GetUser();
                item.UserID ??= user?.Id;
                item.User ??= user?.RealName ?? user?.Name;
                item.Contacts ??= user?.Mobile;
                item.Type ??= FeedType.Bug;
                item.Status ??= FeedStatus.Create;
                item.App ??= FeedApp.Web;
            }

            return BuildResult(0, "success", item.Export(ExportMode.Detail));
        }

        public IActionResult OnPostSave([FromBody] Feedback req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (string.IsNullOrWhiteSpace(req.Title))
                return BuildResult(400, "概述不能为空");

            var needPower = req.Id > 0 ? Power.FeedBackEdit : Power.FeedBackNew;
            if (!CheckPower(needPower))
                return BuildResult(403, "无权操作");

            Feedback item;
            if (req.Id > 0)
            {
                item = Feedback.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "反馈不存在");
            }
            else
            {
                item = new Feedback();
                var user = GetUser();
                item.UserID = user?.Id;
                item.User = user?.RealName ?? user?.Name;
                item.Contacts = user?.Mobile;
                item.Status = FeedStatus.Create;
                item.IsDel = false;
            }

            item.Type = req.Type ?? item.Type ?? FeedType.Bug;
            item.Status = req.Status ?? item.Status ?? FeedStatus.Create;
            item.App = req.App ?? item.App ?? FeedApp.Web;
            item.AppVersion = req.AppVersion?.Trim();
            item.AppModule = req.AppModule?.Trim();

            item.UserID = req.UserID ?? item.UserID;
            item.User = req.User?.Trim() ?? item.User;
            item.Contacts = req.Contacts?.Trim();

            item.Title = req.Title?.Trim();
            item.Content = req.Content?.Trim();
            item.Reply = req.Reply?.Trim();
            item.Image1 = req.Image1;
            item.Image2 = req.Image2;
            item.Image3 = req.Image3;
            item.ReplyImage = req.ReplyImage;

            item.Save();
            return BuildResult(0, "保存成功", new { id = item.Id });
        }
    }
}

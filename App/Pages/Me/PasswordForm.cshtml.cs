using App.DAL;
using App.Utils;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using App.Web;

namespace App.Pages.Me
{
    public class PasswordFormModel : AdminModel
    {
        [BindProperty]
        public PasswordEntity Item { get; set; }

        public void OnGet()
        {
            Item = new PasswordEntity();
        }

        public IActionResult OnPost()
        {
            long? id = GetUserId();
            if (id == null)
                return BuildResult(401, "请先登录");

            if (Item == null)
                return BuildResult(400, "参数错误");

            Item.OldPassword = Item.OldPassword?.Trim();
            Item.NewPassword = Item.NewPassword?.Trim();
            Item.ConfirmPassword = Item.ConfirmPassword?.Trim();

            if (string.IsNullOrWhiteSpace(Item.OldPassword))
                return BuildResult(400, "当前密码不能为空！");

            if (string.IsNullOrWhiteSpace(Item.NewPassword))
                return BuildResult(400, "新密码不能为空！");

            if (string.IsNullOrWhiteSpace(Item.ConfirmPassword))
                return BuildResult(400, "确认密码不能为空！");

            if (Item.NewPassword != Item.ConfirmPassword)
                return BuildResult(400, "确认密码和新密码不一致！");

            var user = App.DAL.User.Get(id.Value);
            if (user == null)
                return BuildResult(404, "用户不存在");

            if (!PasswordUtil.ComparePasswords(user.Password, Item.OldPassword))
                return BuildResult(400, "当前密码不正确！");

            user.Password = PasswordUtil.CreateDbPassword(Item.NewPassword);
            user.Save();

            return BuildResult(0, "修改密码成功！");
        }

        public class PasswordEntity
        {
            [Display(Name = "旧密码")]
            [Required]
            public string OldPassword { get; set; }

            [Display(Name = "新密码")]
            [Required]
            public string NewPassword { get; set; }

            [Display(Name = "确认密码")]
            [Required]
            public string ConfirmPassword { get; set; }
        }
    }
}

using App.DAL;
using App.Utils;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace App.Pages.Admins
{
    [CheckPower(Power.UserEdit)]
    public class UserPasswordFormModel : AdminModel
    {
        [BindProperty]
        public long Id { get; set; }

        [BindProperty]
        public string Name { get; set; }

        [BindProperty]
        public string Password { get; set; }

        public void OnGet(long id)
        {
            var user = App.DAL.User.Get(id);
            if (user != null)
            {
                Id = user.Id;
                Name = user.Name;
            }
        }

        public IActionResult OnPost()
        {
            var user = App.DAL.User.Get(Id);
            if (user == null)
            {
                return BuildResult(404, "用户不存在");
            }

            if (user.Name == "admin" && GetUserName() != "admin")
            {
                return BuildResult(403, "你无权修改超级管理员的密码！");
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                return BuildResult(400, "密码不能为空");
            }

            user.Password = PasswordUtil.CreateDbPassword(Password.Trim());
            user.Save();

            return BuildResult(0, "密码修改成功");
        }
    }
}

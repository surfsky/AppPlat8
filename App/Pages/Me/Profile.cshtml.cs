using System.Linq;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using App.Components;
using System;

namespace App.Pages.Me
{
    /// <summary>
    /// 用户个人信息编辑模型
    /// </summary>
    public class Profile
    {
        public long Id { get; set; }

        public string AccountName { get; set; }
        public string OrgName { get; set; }
        public string RoleNames { get; set; }
        public string LastLoginTime { get; set; }
        public string TakeOfficeTime { get; set; }
        public string CreateTime { get; set; }

        public string Photo { get; set; }
        public string Mobile { get; set; }
        public string RealName { get; set; }
        public string Email { get; set; }
        public string Gender { get; set; }
        public string OfficePhone { get; set; }
        public DateTime? Birthday { get; set; }
        public string Address { get; set; }
        public string Remark { get; set; }
    }


    public class ProfileModel : AdminModel
    {
        [BindProperty]
        public Profile Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData()
        {
            var uid = GetUserId();
            if (uid == null)
                return BuildResult(401, "请先登录");

            var user = App.DAL.User.GetDetail(uid.Value);
            if (user == null)
                return BuildResult(404, "用户不存在");

            Item = ToProfileEditor(user);
            return BuildResult(0, "success", Item);
        }

        public IActionResult OnPostSave([FromBody] Profile req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var uid = GetUserId();
            if (uid == null)
                return BuildResult(401, "请先登录");

            var user = App.DAL.User.GetDetail(uid.Value);
            if (user == null)
                return BuildResult(404, "用户不存在");

            user.Photo = Uploader.SaveFile(nameof(User), req.Photo);
            user.Mobile = req.Mobile?.Trim();
            user.RealName = req.RealName?.Trim();
            user.Email = req.Email?.Trim();
            user.Gender = req.Gender?.Trim();
            user.OfficePhone = req.OfficePhone?.Trim();
            user.Birthday = req.Birthday;
            user.Address = req.Address?.Trim();
            user.Remark = req.Remark?.Trim();
            user.Save();

            return BuildResult(0, "保存成功", ToProfileEditor(user));
        }

        private static Profile ToProfileEditor(User user)
        {
            return new Profile
            {
                Id = user.Id,
                AccountName = user.Name,
                OrgName = user.Org?.Name,
                RoleNames = user.Roles == null ? "" : string.Join("、", user.Roles.Select(r => r.Name)),
                LastLoginTime = user.LastLoginDt?.ToString("yyyy-MM-dd HH:mm:ss"),
                TakeOfficeTime = user.TakeOfficeDt?.ToString("yyyy-MM-dd"),
                CreateTime = user.CreateDt?.ToString("yyyy-MM-dd HH:mm:ss"),

                Photo = user.Photo,
                Mobile = user.Mobile,
                RealName = user.RealName,
                Email = user.Email,
                Gender = user.Gender,
                OfficePhone = user.OfficePhone,
                Birthday = user.Birthday,
                Address = user.Address,
                Remark = user.Remark
            };
        }


    }
}

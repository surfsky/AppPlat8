using App.DAL;
using App.Utils;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;
using User = App.DAL.User;
using App.Entities;

namespace App.Pages.Admins
{
    [Auth(Power.UserView)]
    public class UserFormModel : AdminModel
    {
        public List<SelectListItem> RoleList { get; set; }  // 角色列表
        public App.DAL.User Item { get; set; }  // 用户实体，这样传递所有数据是很危险的，算了先这样吧
        public bool IsNameEditable {get;set;} = true;

        public void OnGet(long id)
        {
            RoleList = Role.Set.Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToList();
            IsNameEditable = id <= 0;
            Item = id > 0 ? App.DAL.User.GetDetail(t => t.Id == id) : new App.DAL.User();
        }

        public IActionResult OnGetData(long id)
        {
            Item = (id > 0) ? App.DAL.User.GetDetail(t=>t.Id == id) : new App.DAL.User();
            return BuildResult(0, "success", Item.Export( ExportMode.Detail));
        }


        public IActionResult OnPostSave([FromBody] User req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            if (string.IsNullOrWhiteSpace(req.Name))
                return BuildResult(400, "账号不能为空");

            User user;
            if (req.Id == 0)
            {
                // New user
                user = new App.DAL.User();
                if (App.DAL.User.Set.Any(u => u.Name == req.Name))
                    return BuildResult(400, "账号已存在");
                user.Password = PasswordHelper.CreateDbPassword(SiteConfig.Instance.DefaultPassword);
            }
            else
            {
                user = App.DAL.User.GetDetail(u => u.Id == req.Id);
                if (user == null)
                    return BuildResult(404, "用户不存在");
                req.Name = user.Name;
            }
            user.Name = req.Name;
            user.RealName = req.RealName;
            user.OrgId = req.OrgId;
            var authOrgIds = (req.AuthOrgIds ?? new List<long>()).Where(t => t > 0).Distinct().ToList();
            user.AuthOrgId = authOrgIds.Count > 0 ? authOrgIds[0] : req.OrgId;
            user.Title = req.Title;
            user.Mobile = req.Mobile;
            user.Email = req.Email;
            user.Gender = req.Gender;
            user.IsDel = req.IsDel;
            user.Remark = req.Remark;
            user.Photo = Uploader.SaveFile(nameof(User), req.Photo);
            user.SetRoles(req.RoleIds);
            user.Save();
            UserOrg.Delete(t => t.UserId == user.Id);
            foreach (var authOrgId in authOrgIds)
            {
                new UserOrg
                {
                    UserId = user.Id,
                    OrgId = authOrgId
                }.Save();
            }
            return BuildResult(0, "保存成功");
        }
    }
}

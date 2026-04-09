using App.DAL;
using App.Utils;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.EntityFrameworkCore;
using User = App.DAL.User;
using App.EleUI;
using App.Entities;

namespace App.Pages.Admins
{
    [CheckPower(Power.UserView)]
    public class UserFormModel : AdminModel
    {
        public List<SelectListItem> RoleList { get; set; }  // 角色列表
        public App.DAL.User Item { get; set; }  // 用户实体，这样传递所有数据是很危险的，算了先这样吧
        public bool IsNameEditable {get;set;} = true;

        public void OnGet(long id)
        {
            RoleList = Role.Set.Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToList();
        }

        public IActionResult OnGetData(long id)
        {
            if (id > 0)
            {
                Item = App.DAL.User.GetDetail(t=>t.Id == id);
                IsNameEditable = false;
            }
            else
            {
                Item = new App.DAL.User();
                IsNameEditable = true;
            }
            return BuildResult(0, "success", Item.Export());
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
            }
            user.Name = req.Name;
            user.RealName = req.RealName;
            user.OrgId = req.OrgId;
            user.Title = req.Title;
            user.Mobile = req.Mobile;
            user.Email = req.Email;
            user.Gender = req.Gender;
            user.InUsed = req.InUsed;
            user.Remark = req.Remark;
            user.Photo = Uploader.SaveFile(nameof(User), req.Photo);
            user.RoleIds = req.RoleIds;
            user.Roles = req.RoleIds.Cast<long, Role>(t => Role.Get(t)).ToList();  // 更新用户角色, req.RoleIds 是前端提交的角色ID列表
            user.Save();
            return BuildResult(0, "保存成功");
        }
    }
}

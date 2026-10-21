using App.DAL;
using App.Utils;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;
using App.Entities;
using UserEntity = App.DAL.User;

namespace App.Pages.Admins
{
    [Auth(Power.UserView)]
    public class UserFormModel : AuthModel
    {
        public List<SelectListItem> RoleList { get; set; }  // 角色列表
        public UserEntity Item { get; set; }  // 用户实体，这样传递所有数据是很危险的，算了先这样吧
        public bool IsNameEditable {get;set;} = true;

        public void OnGet(long id)
        {
            RoleList = GetRoleList();
            IsNameEditable = id <= 0;
        }

        List<SelectListItem> GetRoleList()
        {
            if (Auth.IsAdmin(Auth.GetUser()))
                return Role.Set.Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToList();
            else
                return Auth.GetUser().Roles.Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToList();
        }

        public IActionResult OnGetData(long id)
        {
            if (id > 0)
            {
                Item = UserEntity.GetDetail(t=>t.Id == id);
                if (!OrgFilter.IsAuth(Auth.GetUser(), Item?.AuthOrgId))
                    return BuildResult(403, "无权访问该用户");
            }
            else
                Item = new UserEntity();
            return BuildResult(0, "success", Item?.Export(ExportMode.Detail));
        }


        public IActionResult OnPostSave([FromBody] UserEntity req)
        {
            // AuthOrgId 限制（防越权）
            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);
            if (!isAdmin)
            {
                // OrgId 和 AuthOrgId 不能超过当前管理员可见范围
                if (!OrgFilter.IsAuth(cu, req.OrgId))        return BuildResult(403, $"无权将用户部门设置为该组织（OrgId={req.OrgId}）");
                if (!OrgFilter.IsAuth(cu, req.AuthOrgId))    return BuildResult(403, $"无权将用户授权组织设置为该范围（AuthOrgId={req.AuthOrgId}）");

                // 若为编辑已有用户：目标用户原本必须在管理员的 scope 中，否则禁止修改（防止跨部门窜改）
                if (req.Id > 0)
                {
                    var old = UserEntity.Get(req.Id);
                    if (!OrgFilter.IsAuth(cu, old?.AuthOrgId))
                        return BuildResult(403, "无权修改该用户");
                }
            }

            // user 实体
            UserEntity user;
            if (req.Id == 0)
            {
                // New user
                user = new UserEntity();
                if (UserEntity.Set.Any(u => u.Name == req.Name))
                    return BuildResult(400, "账号已存在");
                user.Password = PasswordHelper.CreateDbPassword(SiteConfig.Instance.DefaultPassword);
            }
            else
            {
                user = UserEntity.GetDetail(u => u.Id == req.Id);
                if (user == null)
                    return BuildResult(404, "用户不存在");
                req.Name = user.Name;
            }

            // 更新用户信息
            user.Name = req.Name;
            user.RealName = req.RealName;
            user.OrgId = req.OrgId;
            user.Title = req.Title;
            user.Mobile = req.Mobile;
            user.Email = req.Email;
            user.Gender = req.Gender;
            user.IsDel = req.IsDel;
            user.Remark = req.Remark;
            user.Photo = Uploader.SaveFile(nameof(UserEntity), req.Photo);
            user.AuthOrgId = req.AuthOrgId;
            user.SetRoles(req.RoleIds);
            user.Save();
            return BuildResult(0, "保存成功");
        }
    }
}


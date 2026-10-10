using App.DAL;
using App.Utils;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;
using App.Entities;

namespace App.Pages.Admins
{
    using UserEntity = App.DAL.User;

    [Auth(Power.UserView)]
    public class UserFormModel : AuthModel
    {
        public List<SelectListItem> RoleList { get; set; }  // 角色列表
        public UserEntity Item { get; set; }  // 用户实体，这样传递所有数据是很危险的，算了先这样吧
        public bool IsNameEditable {get;set;} = true;

        public void OnGet(long id)
        {
            RoleList = Role.Set.Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToList();
            IsNameEditable = id <= 0;
            if (id > 0)
            {
                Item = UserEntity.GetDetail(t => t.Id == id);
                if (!CanAccessTarget(Item))
                    Item = null;   // 越权：直接不回显，让前端报错
            }
            else
            {
                Item = new UserEntity();
            }
        }

        public IActionResult OnGetData(long id)
        {
            if (id > 0)
            {
                Item = UserEntity.GetDetail(t=>t.Id == id);
                if (!CanAccessTarget(Item))
                    return BuildResult(403, "无权访问该用户");
            }
            else
                Item = new UserEntity();
            return BuildResult(0, "success", Item?.Export(ExportMode.Detail));
        }


        public IActionResult OnPostSave([FromBody] UserEntity req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            if (string.IsNullOrWhiteSpace(req.Name))
                return BuildResult(400, "账号不能为空");

            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);

            // ------------ 越权校验：非全局管理员时，员工的 OrgId / AuthOrgId 必须落在管理员的 AuthOrgId 子树内 ------------
            if (!isAdmin)
            {
                // req.OrgId 不能超出管理员可见范围
                if (!OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, req.OrgId))
                    return BuildResult(403, $"无权将用户部门设置为该组织（OrgId={req.OrgId}）");

                // req.AuthOrgId：单值优先；否则从兼容集合 AuthOrgIds 取首个正数值；最后兜底 OrgId
                long? reqAuthOrgId = req.AuthOrgId;
                if (!reqAuthOrgId.HasValue || reqAuthOrgId.Value <= 0)
                {
                    reqAuthOrgId = (req.AuthOrgIds ?? new List<long>())
                        .Where(t => t > 0)
                        .Cast<long?>()
                        .FirstOrDefault();
                }
                reqAuthOrgId = reqAuthOrgId ?? req.OrgId;
                if (!OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, reqAuthOrgId))
                    return BuildResult(403, $"无权将用户授权组织设置为该范围（AuthOrgId={reqAuthOrgId}）");

                // 若为编辑已有用户：目标用户原本必须在管理员的 scope 中，否则禁止修改（防止跨部门窜改）
                if (req.Id > 0)
                {
                    var old = UserEntity.Get(req.Id);
                    if (!CanAccessTarget(old))
                        return BuildResult(403, "无权修改该用户");
                }
            }

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
            user.SetRoles(req.RoleIds);

            // 授权组织：单值优先 → 兼容集合取首个 → 空则 BeforeSave 会兜底为 OrgId
            if (req.AuthOrgId.HasValue && req.AuthOrgId.Value > 0)
            {
                user.AuthOrgId = req.AuthOrgId.Value;
            }
            else
            {
                var authOrgIds = (req.AuthOrgIds ?? new List<long>()).Where(t => t > 0).Distinct().ToList();
                user.SetAuthOrgs(authOrgIds);
            }
            user.Save();
            return BuildResult(0, "保存成功");
        }

        //-------------------------------------------
        // 工具方法
        //-------------------------------------------
        /// <summary>
        /// 判断当前登录用户是否可访问 targetUser（基于授权组织子树约束）。
        /// - 全局管理员：放行；
        /// - 目标为空或没配任何组织：拒绝（避免看到"无主"数据）；
        /// - 目标的 EffectiveAuthOrgId 必须在当前用户授权子树中（包含自身）。
        /// </summary>
        private bool CanAccessTarget(UserEntity target)
        {
            if (target == null) return false;
            var cu = Auth.GetUser();
            if (Auth.IsAdmin(cu)) return true;
            var effective = target.EffectiveAuthOrgId;
            return OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, effective);
        }
    }
}


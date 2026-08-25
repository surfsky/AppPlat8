using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Utils;
using App.Entities;
using App.Components;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace App.DAL
{
    /*
    /// <summary>角色人员</summary>
    [UI("系统", "角色人员")]
    public class UserRole : EntityBase<UserRole>
    {
        public long RoleId { get; set; }
        public long UserId { get; set; }

        public virtual Role Role { get; set; }
        public virtual User User { get; set; }
    }
    */

    //public class User : IKeyId
    public class User : EntityBase<User>, IDeleteLogic
    {
        [UI("是否失效")]   public bool? IsDel { get; set; } = false;
        [UI("用户名")]     public string Name { get; set; }
        [UI("邮箱")]       public string Email { get; set; }
        [UI("密码")]       public string Password { get; set; }
        [UI("性别")]        public string Gender { get; set; }
        [UI("昵称")]        public string NickName { get; set; }
        [UI("真实姓名")]     public string RealName { get; set; }
        [UI("照片")]        public string Photo { get; set; }
        [UI("工作电话")]    public string OfficePhone { get; set; }
        [UI("手机号")]        public string Mobile { get; set; }
        [UI("地址")]        public string Address { get; set; }
        [UI("备注")]        public string Remark { get; set; }
        [UI("身份证")]        public string IdCard { get; set; }
        [UI("生日")]           public DateTime? Birthday { get; set; }
        [UI("任职时间")]        public DateTime? TakeOfficeDt { get; set; }
        [UI("上次登录时间")]     public DateTime? LastLoginDt { get; set; }
        [UI("职务")]            public string  Title { get; set; }
        [UI("所属组织")]        public long? OrgId { get; set; }
        [UI("授权组织")]        public long? AuthOrgId { get; set; }

        // Relations
        [UI("所属组织")]        public virtual Org Org { get; set; }
        [UI("授权组织")]        public virtual Org AuthOrg { get; set; }
        [UI("用户角色")]        public virtual List<Role> Roles { get; set; } = new List<Role>();
        [UI("授权组织")]        public virtual List<UserOrg> UserOrgs { get; set; } = new List<UserOrg>();


        //------------------------------------------------------
        // 计算属性
        //------------------------------------------------------
        public string OrgName => this.Org?.Name;
        public string OrgFullName => this.Org?.FullName;
        public string AuthOrgName => this.AuthOrg?.Name;
        public string AuthOrgFullName => this.AuthOrg?.FullName ?? this.AuthOrg?.Name;
        public string MobileMasked => this.Mobile?.Mask(3, 4);
        public string OfficePhoneMasked => this.OfficePhone?.Mask(3, 4);
        public string AuthOrgNames => GetAuthorizedOrgs()
            .Select(t => t.FullName ?? t.Name)
            .Where(t => t.IsNotEmpty())
            .Distinct()
            .ToJoinString("，");

        //------------------------------------------------------
        // 角色相关（用UserRoles表存储）
        //------------------------------------------------------
        public string RoleNames => (this.Roles ?? new List<Role>())
            .Where(t => t != null && t.Name.IsNotEmpty())
            .Select(t => t.Name.Trim())
            .Distinct()
            .ToJoinString(",");
        [NotMapped] private List<long> _roleIds;
        [UI("角色IDs"), NotMapped]
        public virtual List<long> RoleIds
        {
            get
            {
                if (_roleIds != null)
                    return _roleIds;
                return (this.Roles ?? new List<Role>())
                    .Where(t => t != null)
                    .Select(t => t.Id)
                    .Distinct()
                    .ToList();
            }
            set
            {
                _roleIds = (value ?? new List<long>())
                    .Where(t => t > 0)
                    .Distinct()
                    .ToList();
            }
        }

        [NotMapped] private List<long> _authOrgIds;
        [UI("授权组织IDs"), NotMapped]
        public virtual List<long> AuthOrgIds
        {
            get
            {
                if (_authOrgIds != null)
                    return _authOrgIds;
                return GetAuthorizedOrgs()
                    .Where(t => t != null)
                    .Select(t => t.Id)
                    .Distinct()
                    .ToList();
            }
            set
            {
                _authOrgIds = (value ?? new List<long>())
                    .Where(t => t > 0)
                    .Distinct()
                    .ToList();
            }
        }


        /// <summary>获取用户的所有角色IDs。</summary>
        public List<long> GetRoleIds()
        {
            if (_roleIds != null)
                return this.RoleIds;

            if ((this.Roles ?? new List<Role>()).Count > 0)
                return this.RoleIds;

            if (this.Id <= 0)
                return new List<long>();

            return User.Set
                .Where(t => t.Id == this.Id)
                .SelectMany(t => t.Roles)
                .Select(t => t.Id)
                .Distinct()
                .ToList();
        }

        /// <summary>获取用户直接授权的组织列表（含主组织、默认授权组织）。</summary>
        public List<Org> GetAuthorizedOrgs()
        {
            var orgs = new List<Org>();

            void addOrg(Org org)
            {
                if (org == null) return;
                if (orgs.Any(t => t.Id == org.Id)) return;
                orgs.Add(org);
            }

            addOrg(this.Org);
            addOrg(this.AuthOrg);
            foreach (var item in this.UserOrgs ?? new List<UserOrg>())
                addOrg(item?.Org);

            if (orgs.Count > 0)
                return orgs;

            var ids = new List<long>();
            if (this.OrgId.HasValue && this.OrgId.Value > 0) ids.Add(this.OrgId.Value);
            if (this.AuthOrgId.HasValue && this.AuthOrgId.Value > 0) ids.Add(this.AuthOrgId.Value);
            if (this.Id > 0)
                ids.AddRange(UserOrg.Set.Where(t => t.UserId == this.Id && t.OrgId != null).Select(t => t.OrgId.Value).ToList());

            ids = ids.Where(t => t > 0).Distinct().ToList();
            return ids.Count == 0 ? new List<Org>() : Org.Set.Where(t => ids.Contains(t.Id)).ToList();
        }

        /// <summary>按角色ID列表更新导航属性。</summary>
        public void SetRoles(IEnumerable<long> roleIds)
        {
            this.RoleIds = roleIds?.ToList();
            var ids = this.RoleIds;
            this.Roles = (ids.Count == 0)
                ? new List<Role>()
                : Role.Set.Where(t => ids.Contains(t.Id)).ToList();
        }

        /// <summary>设置授权组织。</summary>
        public void SetAuthOrgs(IEnumerable<long> orgIds)
        {
            this.AuthOrgIds = orgIds?.ToList();
            var ids = this.AuthOrgIds;
            this.UserOrgs = (ids.Count == 0)
                ? new List<UserOrg>()
                : Org.Set
                    .Where(t => ids.Contains(t.Id))
                    .Select(t => new UserOrg
                    {
                        UserId = this.Id > 0 ? this.Id : null,
                        OrgId = t.Id,
                        Org = t
                    })
                    .ToList();
        }

        //------------------------------------------------------
        // 权限（用RolePower表存储）
        //------------------------------------------------------
        /// <summary>获取用户权限（admin拥有所有权限、普通用户根据角色来获取权限）</summary>
        public List<Power> GetPowers()
        {
            var powers = new List<Power>();
            if (this.Name == "admin")
                powers = typeof(Power).GetEnums<Power>();
            else
            {
                var roleIds = this.Roles.Select(t => t.Id).ToList();
                RolePower.Search(t => roleIds.Contains(t.RoleId)).ToList().ForEach(t => powers.Add(t.PowerId));
            }
            return powers;
        }

        /// <summary>用户是否拥有指定权限</summary>
        public bool HasPower(Power power)
        {
            if (this.Name == "admin") return true;
            var powers = this.GetPowers();
            return powers.Contains(power);
        }



        //------------------------------------------------------
        // 
        //------------------------------------------------------
        /// <summary>导出数据（可根据不同场景导出不同字段）</summary>
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                this.Id,
                this.Name,
                this.RealName,
                this.OrgId,
                this.OrgName,
                this.OrgFullName,
                this.AuthOrgId,
                this.AuthOrgName,
                this.AuthOrgFullName,
                AuthOrgIds = this.AuthOrgIds,
                this.AuthOrgNames,
                this.Email,
                this.Gender,
                this.Birthday,
                this.TakeOfficeDt,
                this.LastLoginDt,
                this.Title,
                OfficePhone = (type == ExportMode.Detail) ? this.OfficePhone : this.OfficePhone?.Mask(),
                Mobile = (type == ExportMode.Detail) ? this.Mobile : this.Mobile?.Mask(),
                this.Address,
                this.Remark,
                this.IdCard,
                this.Photo,
                this.IsDel,
                RoleIds = this.GetRoleIds(),
                this.RoleNames,
                Roles = this.Roles.Export(type),
                AuthOrgs = this.GetAuthorizedOrgs().Export(type),
            };
        }

        /// <summary>获取用户详情（包含关联数据）</summary>
        public static User GetDetail(Func<User, bool> predicate)
        {
            var user = DataSet
                .Include(u => u.Org)
                .Include(u => u.AuthOrg)
                .Include(u => u.Roles)
                .Include(u => u.UserOrgs)
                    .ThenInclude(t => t.Org)
                .FirstOrDefault(predicate);
            if (user == null)
                return null;
            user.RoleIds = user.Roles.Select(r => r.Id).ToList();
            user.AuthOrgIds = user.GetAuthorizedOrgs().Select(t => t.Id).Distinct().ToList();
            return user;
        }

        /// <summary>搜索用户列表</summary>
        public static IQueryable<User> Search(string name, string realName, long? deptId=null, long? roleId=null, bool? isDel=null)
        {
            var q = DataSet
                .Include(u => u.Org)
                .Include(u => u.AuthOrg)
                .Include(u => u.Roles)
                .Include(u => u.UserOrgs)
                    .ThenInclude(t => t.Org)
                .AsQueryable();
            if (name.IsNotEmpty())     q = q.Where(t => t.Name.Contains(name));
            if (realName.IsNotEmpty()) q = q.Where(t => t.RealName.Contains(realName));
            if (deptId != null)        q = q.Where(t => t.OrgId == deptId);
            if (roleId != null)        q = q.Where(t => t.Roles.Any(r => r.Id == roleId));
            if (isDel == true)         q = q.Where(t => t.IsDel == true);
            if (isDel == false)        q = q.Where(t => t.IsDel == false || t.IsDel == null);

            return q;
        }

    }


}

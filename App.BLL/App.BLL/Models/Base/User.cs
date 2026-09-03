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

        // Relations
        [UI("所属组织")]        public virtual Org Org { get; set; }
        [UI("用户角色")]        public virtual List<Role> Roles { get; set; } = new List<Role>();
        [UI("授权组织")]        public virtual List<UserOrg> UserOrgs { get; set; } = new List<UserOrg>();


        //------------------------------------------------------
        // 计算属性
        //------------------------------------------------------
        public string OrgName => this.Org?.Name;
        public string OrgFullName => this.Org?.FullName;
        public string AuthOrgName => GetAuthorizedOrgs()
            .Select(t => t.Name)
            .FirstOrDefault(t => t.IsNotEmpty());
        public string AuthOrgFullName => GetAuthorizedOrgs()
            .Select(t => t.FullName ?? t.Name)
            .FirstOrDefault(t => t.IsNotEmpty());
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
                // 仅返回 UserOrgs 中显式保存的授权组织 (不自动合并 OrgId，
                // 否则每次保存都会把所属部门也写进 UserOrgs 造成重复 & UI 回显脏数据)
                var ids = (this.UserOrgs ?? new List<UserOrg>())
                    .Where(t => t != null && t.OrgId.HasValue && t.OrgId.Value > 0)
                    .Select(t => t.OrgId.Value)
                    .Distinct()
                    .ToList();
                if (ids.Count == 0 && this.Id > 0)
                {
                    ids = UserOrg.Set
                        .Where(t => t.UserId == this.Id && t.OrgId != null && t.OrgId > 0)
                        .Select(t => t.OrgId.Value)
                        .Distinct()
                        .ToList();
                }
                return ids;
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

        /// <summary>获取用户直接授权的组织列表（含主组织、UserOrgs）。</summary>
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
            foreach (var item in this.UserOrgs ?? new List<UserOrg>())
                addOrg(item?.Org);

            if (orgs.Count > 0)
                return orgs;

            var ids = new List<long>();
            if (this.OrgId.HasValue && this.OrgId.Value > 0) ids.Add(this.OrgId.Value);
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
                if (roleIds.Count == 0 && this.Id > 0)
                {
                    // 兼容：从 Join Table UserRole(EF 命名 RolesId+UsersId) 查
                    roleIds = QueryUserRoleIds(this.Id);
                }
                if (roleIds.Count > 0)
                {
                    RolePower.Search(t => roleIds.Contains(t.RoleId))
                        .ToList().ForEach(t => powers.Add(t.PowerId));
                }
                powers = powers.Distinct().ToList();
            }
            return powers;
        }

        /// <summary>
        /// 权限版本戳：当前用户权限的最新更新时间（Tick 秒）。
        /// Auth 层用此版本戳判断 Session 缓存的权限列表是否过期，避免：
        /// 管理员改了角色权限/用户角色后，用户 Session 期内仍然拿老权限导致 403。
        ///
        /// 版本包含：
        ///   - 用户主表 UpdateDt
        ///   - 用户关联的 UserRole (多对多) 最新 UpdateDt（角色变了）
        ///   - 用户角色的 Role.UpdateDt
        ///   - 用户所有角色对应 RolePower 最新 UpdateDt
        /// 4 者中最大的 DateTime.Ticks / 1e7 (秒) 作为版本号
        /// </summary>
        public long GetPermissionVersion()
        {
            if (this.Name == "admin")
            {
                // admin 无需失效，给固定值即可
                return 0L;
            }
            long maxTicks = 0;
            void feed(DateTime? dt)
            {
                if (dt == null) return;
                var t = dt.Value.Ticks / 10_000_000L;
                if (t > maxTicks) maxTicks = t;
            }

            feed(this.UpdateDt);

            // 取角色ID (从导航属性或 Join Table)
            var roleIds = this.Roles?.Select(t => t.Id).ToList() ?? new List<long>();
            if (roleIds.Count == 0 && this.Id > 0)
                roleIds = QueryUserRoleIds(this.Id);
            if (roleIds.Count == 0)
                return maxTicks;
            // 2. Role.UpdateDt + 3. RolePower.UpdateDt
            var roleUps = Role.Set
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.UpdateDt)
                .ToList();
            foreach (var d in roleUps) feed(d);

            var rpUps = RolePower.Search(p => roleIds.Contains(p.RoleId))
                .Select(p => p.UpdateDt)
                .ToList();
            foreach (var d in rpUps) feed(d);
            return maxTicks;
        }

        /// <summary>用户是否拥有指定权限</summary>
        public bool HasPower(Power power)
        {
            if (this.Name == "admin") return true;
            var powers = this.GetPowers();
            return powers.Contains(power);
        }

        //------------------------------------------------------
        // 内部辅助
        //------------------------------------------------------
        /// <summary>
        /// 从 EF 生成的 UserRole 跳过导航表（列名 RolesId+UsersId）中查某用户的所有角色ID。
        /// 用于导航属性 Roles 未 Include 时兜底，避免 GetPowers() 返回空集合。
        /// 直接复用 EntityBase.Db 上下文，不 new 新的 DbContext。
        /// </summary>
        private static List<long> QueryUserRoleIds(long userId)
        {
            if (userId <= 0) return new List<long>();
            try
            {
                var conn = Db.Database.GetDbConnection();
                var wasClosed = conn.State == System.Data.ConnectionState.Closed;
                if (wasClosed) conn.Open();
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT RolesId FROM UserRole WHERE UsersId = @uid";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@uid";
                        p.Value = userId;
                        cmd.Parameters.Add(p);
                        var list = new List<long>();
                        using (var rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                if (!rdr.IsDBNull(0)) list.Add(rdr.GetInt64(0));
                            }
                        }
                        return list;
                    }
                }
                finally
                {
                    if (wasClosed) conn.Close();
                }
            }
            catch
            {
                return new List<long>();
            }
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
                .Include(u => u.Roles)
                .Include(u => u.UserOrgs)
                    .ThenInclude(t => t.Org)
                .FirstOrDefault(predicate);
            if (user == null)
                return null;
            user.RoleIds = user.Roles.Select(r => r.Id).ToList();
            // UI 回显只取显式保存的 UserOrgs 授权 (不包含 OrgId 所属部门,
            // 否则每次保存都会把部门也写进 UserOrgs 造成越积越多)
            user.AuthOrgIds = (user.UserOrgs ?? new List<UserOrg>())
                .Where(t => t != null && t.OrgId.HasValue && t.OrgId.Value > 0)
                .Select(t => t.OrgId.Value)
                .Distinct()
                .ToList();
            return user;
        }

        /// <summary>搜索用户列表</summary>
        public static IQueryable<User> Search(string name, string realName, long? deptId=null, long? roleId=null, bool? isDel=null)
        {
            var q = DataSet
                .Include(u => u.Org)
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

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
        [UI("失效时间")]    public DateTime? DeleteDt { get; set; }
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
        /// <summary>授权组织（单值）；保存前若为空将自动兜底为 OrgId</summary>
        [UI("授权组织")]        public long? AuthOrgId { get; set; }

        // Relations
        [UI("所属组织")]        public virtual Org Org { get; set; }
        [UI("用户角色")]        public virtual List<Role> Roles { get; set; } = new List<Role>();
        [UI("授权组织")]        public virtual Org AuthOrg { get; set; }


        //------------------------------------------------------
        // 计算属性
        //------------------------------------------------------
        public string DisplayName => $"{this.RealName}({this.Mobile})";
        public string OrgName => this.Org?.Name;
        public string OrgFullName => this.Org?.FullName;

        /// <summary>授权组织或所属组织的兜底值（AuthOrgId ?? OrgId）</summary>
        [NotMapped]
        public long? EffectiveAuthOrgId => this.AuthOrgId ?? this.OrgId;

        public string AuthOrgName
        {
            get
            {
                if (this.AuthOrg?.Name.IsNotEmpty() == true) return this.AuthOrg.Name;
                if (this.Org?.Name.IsNotEmpty() == true) return this.Org.Name;
                return null;
            }
        }
        public string AuthOrgFullName
        {
            get
            {
                if (this.AuthOrg?.FullName.IsNotEmpty() == true) return this.AuthOrg.FullName;
                if (this.AuthOrg?.Name.IsNotEmpty() == true) return this.AuthOrg.Name;
                if (this.Org?.FullName.IsNotEmpty() == true) return this.Org.FullName;
                if (this.Org?.Name.IsNotEmpty() == true) return this.Org.Name;
                return null;
            }
        }
        public string MobileMasked => this.Mobile?.Mask(3, 4);
        public string OfficePhoneMasked => this.OfficePhone?.Mask(3, 4);

        /// <summary>授权组织显示文本（单值语义，兼容原有拼接字段便于前端展示）</summary>
        public string AuthOrgNames => GetAuthorizedOrgs()
            .Select(t => t.FullName ?? t.Name)
            .Where(t => t.IsNotEmpty())
            .Distinct()
            .ToJoinString("，");

        //------------------------------------------------------
        // 角色相关（用UserRoles表存储）
        //------------------------------------------------------
        public string RoleNames => this.Roles==null ? "" : this.Roles.Select(t => t.Name).Distinct().ToJoinString(",");

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

        /// <summary>
        /// 授权组织兼容包装属性。
        /// 后端模型收敛为单值 User.AuthOrgId；此处保留 List<long> 形态，
        /// 以便 UI 侧 EleTreePicker (Multiple 模式) 无需改动即可工作，
        /// setter 仅取集合中首个正数值写入 AuthOrgId，getter 返回单元素包装。
        /// </summary>
        [NotMapped] private List<long> _authOrgIdsBacking;
        [UI("授权组织IDs"), NotMapped]
        public virtual List<long> AuthOrgIds
        {
            get
            {
                if (_authOrgIdsBacking != null)
                    return _authOrgIdsBacking;
                if (this.AuthOrgId.HasValue && this.AuthOrgId.Value > 0)
                    return new List<long> { this.AuthOrgId.Value };
                return new List<long>();
            }
            set
            {
                var first = (value ?? new List<long>())
                    .Where(t => t > 0)
                    .Distinct()
                    .Cast<long?>()
                    .FirstOrDefault();
                // 保留显式集合（避免每次 setter 后 getter 再从 AuthOrgId 回取造成"丢失其他值"的直觉不一致，
                // 但持久化只会写首个正数值到 AuthOrgId 列）
                _authOrgIdsBacking = (value ?? new List<long>())
                    .Where(t => t > 0)
                    .Distinct()
                    .ToList();
                this.AuthOrgId = first;
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

        /// <summary>获取用户授权组织（单值语义；返回 0~1 个 Org，保持签名兼容）。</summary>
        public List<Org> GetAuthorizedOrgs()
        {
            var list = new List<Org>();
            var orgId = this.EffectiveAuthOrgId;
            if (!orgId.HasValue || orgId.Value <= 0) return list;

            // 优先走导航属性（若 GetDetail 已 Include）
            if (this.AuthOrg != null && this.AuthOrg.Id == orgId.Value)
            {
                list.Add(this.AuthOrg);
                return list;
            }
            if (this.Org != null && this.Org.Id == orgId.Value)
            {
                list.Add(this.Org);
                return list;
            }

            var org = Org.Set.FirstOrDefault(o => o.Id == orgId.Value);
            if (org != null) list.Add(org);
            return list;
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

        /// <summary>设置授权组织（集合中仅首个正数值写入 AuthOrgId；持久化前仍可能被兜底为 OrgId）。</summary>
        public void SetAuthOrgs(IEnumerable<long> orgIds)
        {
            this.AuthOrgIds = orgIds?.ToList();
            var ids = this.AuthOrgIds;
            if (ids.Count == 0)
            {
                this.AuthOrg = null;
                return;
            }
            // 装载导航属性（便于后续立即访问 AuthOrg.Name 等，避免 N+1）
            var first = ids[0];
            this.AuthOrg = Org.Set.FirstOrDefault(o => o.Id == first);
        }

        //------------------------------------------------------
        // 保存钩子：AuthOrgId 空值兜底
        //------------------------------------------------------
        /// <summary>保存前若 AuthOrgId 为空，则兜底为所属组织 OrgId。</summary>
        public override void BeforeSave(EntityOp op)
        {
            base.BeforeSave(op);
            if (!this.AuthOrgId.HasValue || this.AuthOrgId.Value <= 0)
            {
                this.AuthOrgId = (this.OrgId.HasValue && this.OrgId.Value > 0) ? this.OrgId : (long?)null;
            }
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
            var authOrgs = this.GetAuthorizedOrgs()
                .Select(t => new
                {
                    t.Id,
                    t.ParentId,
                    t.Name,
                    t.FullName,
                    Level = (int?)t.Level,
                    t.Remark,
                    t.SortId,
                    t.TreeLevel,
                    Gps = type == ExportMode.Detail ? t.Gps : null,
                    GeoData = type == ExportMode.Detail ? t.GeoData : null,
                })
                .ToList();
            var roles = this.Roles.Export(type);

            return new
            {
                this.Id,
                this.Name,
                this.RealName,
                this.DisplayName,
                this.OrgId,
                this.OrgName,
                this.OrgFullName,
                this.AuthOrgId,
                this.AuthOrgName,
                this.AuthOrgFullName,
                this.AuthOrgIds,
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
                Roles = roles,
                AuthOrgs = authOrgs,
            };
        }

        /// <summary>获取用户详情（包含关联数据）</summary>
        public static User GetDetail(Func<User, bool> predicate)
        {
            var user = DataSet
                .Include(u => u.Org)
                .Include(u => u.Roles)
                .Include(u => u.AuthOrg)
                .FirstOrDefault(predicate)
                ;
            if (user == null)
                return null;
            user.RoleIds = user.Roles.Select(r => r.Id).ToList();
            // AuthOrgIds 回显由 NotMapped 包装属性直接从 AuthOrgId 构造；
            // 此处显式重置临时集合，以避免多次调用间串扰。
            user._authOrgIdsBacking = null;
            return user;
        }

        /// <summary>搜索用户列表</summary>
        public static IQueryable<User> Search(string keyword="", string name="", string realName="", long? orgId = null, long? roleId = null, bool? isDel = null, bool includeSubOrg = true)
        {
            var q = DataSet
                .Include(u => u.Org)
                .Include(u => u.AuthOrg)
                .Include(u => u.Roles)
                .AsNoTracking()
                .AsQueryable();
            if (keyword.IsNotEmpty()) q = q.Where(t => t.Name.Contains(keyword) || t.RealName.Contains(keyword));
            if (name.IsNotEmpty())     q = q.Where(t => t.Name.Contains(name));
            if (realName.IsNotEmpty()) q = q.Where(t => t.RealName.Contains(realName));
            if (orgId != null)
            {
                if (includeSubOrg)
                {
                    var subIds = Org.GetChildIds(orgId.Value) ?? new List<long>();
                    if (subIds.Count == 0)
                        subIds = new List<long> { orgId.Value };
                    q = q.Where(t => t.OrgId.HasValue && subIds.Contains(t.OrgId.Value));
                }
                else
                {
                    q = q.Where(t => t.OrgId.HasValue && t.OrgId.Value == orgId.Value);
                }
            }
            if (roleId != null)        q = q.Where(t => t.Roles.Any(r => r.Id == roleId));
            if (isDel == true)         q = q.Where(t => t.IsDel == true);                      // 已删除数据
            if (isDel == false)        q = q.Where(t => t.IsDel == false || t.IsDel == null);  // 未删除数据

            return q;
        }

    }


}

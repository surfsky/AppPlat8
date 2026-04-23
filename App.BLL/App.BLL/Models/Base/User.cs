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
        [UI("是否在用")]   public bool? InUsed { get; set; } = true;
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
        

        // 用户角色（多对多关系）
        [UI("用户角色")]        public virtual List<Role> Roles { get; set; } = new List<Role>();
        [UI("角色IDs"), NotMapped]  public virtual List<long> RoleIds {get; set;}   // 冗余设计用于前端绑定

        // extend
        public string MobileMasked => this.Mobile?.Mask(3, 4);
        public string OfficePhoneMasked => this.OfficePhone?.Mask(3, 4);


        //------------------------------------------------------
        // 权限
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
                OrgName = this.Org?.Name,
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
                this.InUsed,
                this.RoleIds,
                Roles = this.Roles.Export(type),
            };
        }

        /// <summary>获取用户详情（包含关联数据）</summary>
        public static User GetDetail(Func<User, bool> predicate)
        {
            var user = DataSet.Include(u => u.Org).Include(u => u.AuthOrg).Include(u => u.Roles).FirstOrDefault(predicate);
            if (user == null)
                return null;
            user.RoleIds = user.Roles.Select(r => r.Id).ToList();
            return user;
        }

        /// <summary>搜索用户列表</summary>
        public static IQueryable<User> Search(string name, string realName, long? deptId=null, long? roleId=null)
        {
            var q = DataSet.Include(u => u.Org).Include(u => u.AuthOrg).Include(u => u.Roles).AsQueryable();
            if (name.IsNotEmpty())     q = q.Where(t => t.Name.Contains(name));
            if (realName.IsNotEmpty()) q = q.Where(t => t.RealName.Contains(realName));
            if (deptId != null)        q = q.Where(t => t.OrgId == deptId);
            if (roleId != null)        q = q.Where(t => t.Roles.Any(r => r.Id == roleId));

            return q;
        }

    }


}
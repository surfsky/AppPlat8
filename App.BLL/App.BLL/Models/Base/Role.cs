using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    public class Role : EntityBase<Role>
    {
        [UI("名称")]    public string Name { get; set; }
        [UI("备注")]    public string Remark { get; set; }


        public virtual List<User> Users { get; set; } = new List<User>();
        public virtual List<RolePower> RolePowers { get; set; } = new List<RolePower>();

        public override string ToString()
        {
            return this.Name;
        }

        public static IQueryable<Role> Search(string name)
        {
            IQueryable<Role> q = Role.IncludeSet;
            if (name.IsNotEmpty())  q = q.Where(x => x.Name.Contains(name));
            return q;
        }
    }
}
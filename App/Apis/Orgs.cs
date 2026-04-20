using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using App.Components;
using App.HttpApi;
using App.DAL;
using App.Utils;
using App.Web;
using System.Linq;
using App.Entities;
using App.EleUI;

namespace App.API
{
    [Scope("Base")]
    [Description("组织")]
    public class Orgs
    {
        [HttpApi("获取所有组织", AuthLogin=true)]
        public static APIResult GetOrgs()
        {
            return App.DAL.Org.Set.OrderBy(o => o.SortId).ToList().ToResult();
        }

        [HttpApi("获取组织树形结构", AuthLogin=true)]
        public static APIResult GetOrgTree()
        {
            return App.DAL.Org.GetTree().ToResult();
        }

        [HttpApi("获取授权组织树", AuthLogin=true)]
        public static APIResult GetAuthOrgTree()
        {
            var user = Auth.GetUser();
            if (user.Name == "admin")
                return App.DAL.Org.GetTree().ToResult();
            var authOrgId = user.AuthOrgId ?? user.OrgId;
            var items = EntityHelper.GetDescendants(App.DAL.Org.All, authOrgId);
            return items.ToTree().ToResult();
        }
    }
}

using App.DAL;
using App.Pages.Admin;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace App
{
    /// <summary>
    /// 带授权校验的后台页面模型基类
    /// </summary>
    [Authorize]
    public class AdminModel : BaseModel
    {
        public string JsonData { get; set; } = "{}";

        public PageMode Mode
        {
            get
            {
                var mode = Asp.GetQuery<PageMode>("md");
                if (mode != null)
                    return mode.Value;
                else
                {
                    var id = Asp.GetQueryInt("id");
                    if (id != null)
                        return PageMode.Edit;
                }
                return PageMode.View;
            }
        }

    }
}

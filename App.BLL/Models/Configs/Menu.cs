using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Utils;
using App.Entities;

namespace App.DAL
{
    /// <summary>
    /// 菜单
    /// </summary>
    public class Menu : TreeEntity<Menu>
    {
        [UI("图标")]     public string ImageUrl { get; set; }
        [UI("链接")]     public string NavigateUrl { get; set; }
        [UI("备注")]     public string Remark { get; set; }
        [UI("目标页面")]  public string Target { get; set; }
        [UI("是否展开")]  public bool? Expanded { get; set; } = false;
        [UI("是否可见")]  public bool? Visible { get; set; } = true;
        [UI("是否固定")]  public bool? Fixed { get; set; } = false;
        [UI("浏览权限")]  public Power? Power { get; set; }

        [NotMapped]   public string PowerName => Power?.ToString();


        //------------------------------------------------
        // 方法
        //------------------------------------------------
        public override Menu Clone()
        {
            return base.Clone().Let(t => {
                t.ImageUrl = this.ImageUrl;
                t.NavigateUrl = this.NavigateUrl;
                t.Remark = this.Remark;
                t.Target = this.Target;
                t.Expanded = this.Expanded;
                t.Visible = this.Visible;
                t.Fixed = this.Fixed;
                t.Power = this.Power;
            });
        }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                SortId,
                TreeLevel,

                ImageUrl,
                NavigateUrl,
                Remark,
                Target,
                Expanded,
                Visible,
                Fixed,
                Power,
                PowerName,

                Children,
            };
        }
    }
}
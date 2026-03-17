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
    public class Org : TreeEntity<Org>
    {
        [UI("备注")] public string Remark { get; set; }

        public override object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                Remark,
                SortId,
                TreeLevel,
                Children // Add this line to include children in JSON output
            };
        }
        
        public override Org Clone()
        {
            return base.Clone().SetValue(t => t.Remark, this.Remark);
        }
    }
}
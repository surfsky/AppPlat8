using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace App.Pages.Base
{
    [Auth(Power.CompanyView)]
    [IgnoreAntiforgeryToken]
    public class CompanyFormModel : AdminModel
    {
        [BindProperty]
        public Company Item { get; set; }

        public void OnGet(){}

        /// <summary>获取厂商详情</summary>
        public IActionResult OnGetData(long id)
        {
            var item = Company.Get(id);
            if (item == null)
                return BuildResult(404, "无效参数");
            else
                return BuildResult(0, "success", item.Export());
        }

        /// <summary>保存厂商信息</summary>
        public IActionResult OnPostSave([FromBody] Company req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Name))
                    return BuildResult(400, "名称不能为空");

                var needPower = (req.Id == 0) ? Power.CompanyNew : Power.CompanyEdit;
                if (!CheckPower(needPower))
                    return BuildResult(403, "无权操作");
                
                //
                Company item = null;
                if (req.Id == 0)
                    item = new Company();
                else
                {
                    item = Company.Get(req.Id);
                    if (item == null)
                        return BuildResult(404, "记录不存在");
                }

                item.Name = req.Name;
                item.AbbrName = req.AbbrName;
                item.UnifiedSocialCreditCode = req.UnifiedSocialCreditCode;
                item.LegalPerson = req.LegalPerson;
                item.LegalPersonPhone = req.LegalPersonPhone;
                item.Contact = req.Contact;
                item.ContactPhone = req.ContactPhone;
                item.Address = req.Address;
                item.Save(null, log: true);
                return BuildResult(0, "保存成功", new { id = item.Id });
            }
            catch (Exception ex)
            {
                Logger.Error("Company Save Fail: {0}", ex.ToString());
                return BuildResult(500, ex.Message);
            }
        }
    }
}

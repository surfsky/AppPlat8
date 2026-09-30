using globalCulture = App.EleUI.Culture;
using App.EleUI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace App.Pages.EleUISamples
{
    public class CultureModel : BaseModel
    {
        [BindProperty]
        public string CultureName { get; set; } = "Chinese";

        [BindProperty]
        public SampleItem Item { get; set; } = new();

        public class SampleItem
        {
            public long? Id { get; set; }

            [Display(Name = "名称")]
            public string Name { get; set; }

            [Display(Name = "日期")]
            public DateTime? Date { get; set; }

            [Display(Name = "日期时间")]
            public DateTime? DateTimeValue { get; set; }

            [Display(Name = "开始日期")]
            public DateTime? StartDate { get; set; }

            public DateTime? EndDate { get; set; }

            [Display(Name = "级别")]
            public int Level { get; set; }

            [Display(Name = "是否启用")]
            public bool Enabled { get; set; }

            [Display(Name = "所属用户")]
            public long? UserId { get; set; }

            public string UserName { get; set; }

            [Display(Name = "备注")]
            public string Remark { get; set; }
        }

        public List<User> SampleRows { get; set; } = new();

        public void OnGet()
        {
            var cookie = Request.Cookies[".AspNetCore.Culture"];
            globalCulture culture = globalCulture.Chinese;
            if (!string.IsNullOrWhiteSpace(cookie))
            {
                if (cookie.Contains("=en") || cookie.Contains("|uic=en", StringComparison.OrdinalIgnoreCase))
                    culture = globalCulture.English;
            }
            CultureName = culture.ToString();
            Texts.SetCurrent(culture);

            SampleRows = Data.QueryUsers(1, 5, "", "", "").Items ?? new List<User>();
        }

        public IActionResult OnPostSwitchCulture([FromBody] SwitchCultureRequest req)
        {
            var cultureName = (req?.Culture ?? "Chinese").Trim();
            var culture = Enum.TryParse<globalCulture>(cultureName, true, out var c) ? c : globalCulture.Chinese;
            Texts.SetCurrent(culture);

            var cookieVal = culture == globalCulture.English
                ? "c=en-US|uic=en-US"
                : "c=zh-CN|uic=zh-CN";
            Response.Cookies.Append(".AspNetCore.Culture", cookieVal, new CookieOptions
            {
                HttpOnly = false,
                Expires = DateTimeOffset.Now.AddYears(1),
                Path = "/"
            });

            return BuildResult(0, "ok", new { culture = culture.ToString() });
        }

        public IActionResult OnGetSampleData(App.Components.Paging pi, string name)
        {
            var result = Data.QueryUsers(pi.PageIndex, pi.PageSize, name, "", "");
            return BuildResult(0, "success", new { items = result.Items, total = result.Total });
        }

        public IActionResult OnPostShowMessageBox([FromBody] EmptyRequest _)
        {
            return EleHandler.ShowMessageBox(
                text: Texts.Current.SaveSuccess,
                title: Texts.Current.Prompt,
                type: NotifyType.Success,
                isAlert: true);
        }

        public IActionResult OnPostShowInputBox([FromBody] EmptyRequest _)
        {
            return EleHandler.ShowInputBox(
                text: Texts.Current.Prompt,
                title: Texts.Current.PleaseInput,
                inputPlaceholder: Texts.Current.PleaseInputContent,
                serverHandler: "SaveInput");
        }

        public IActionResult OnPostSaveInput([FromBody] SaveInputRequest req)
        {
            return EleHandler.ShowNotify(Texts.Current.SaveSuccess + ": " + (req?.InputValue ?? ""), NotifyType.Success, "ok");
        }

        public class SwitchCultureRequest
        {
            public string Culture { get; set; }
        }

        public class EmptyRequest { }

        public class SaveInputRequest
        {
            public string InputValue { get; set; }
            public string Action { get; set; }
        }
    }
}

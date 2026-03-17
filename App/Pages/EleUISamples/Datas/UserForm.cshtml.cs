using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Pages.EleUISamples
{
    public class UserFormModel : BaseModel
    {
        [BindProperty]
        public User Item { get; set; }

        public List<Role> RoleList { get; set; }
        public List<SelectListItem> CityList { get; set; }
        public List<SelectListItem> TypeList { get; set; }

        public void OnGet(long id)
        {
            RoleList = Data.GetRoles();
            CityList = BuildCityList();
            TypeList = BuildTypeList();
        }

       private static List<SelectListItem> BuildCityList()
        {
            return new List<SelectListItem>
            {
                new SelectListItem("北京", "Beijing"),
                new SelectListItem("上海", "Shanghai"),
                new SelectListItem("广州", "Guangzhou")
            };
        }

        private static List<SelectListItem> BuildTypeList()
        {
            return new List<SelectListItem>
            {
                new SelectListItem("类型A", "A"),
                new SelectListItem("类型B", "B"),
                new SelectListItem("类型C", "C")
            };
        }
         

        public IActionResult OnGetData(long id)
        {
            var user = Data.GetUser(id) ?? new User
            {
                Birthday = DateTime.Today,
                IsEnabled = true,
                Gender = "男",
                Types = new List<string>()
            };
            return BuildResult(0, "success", user);
        }

        public IActionResult OnPostSave([FromBody] User req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
                return BuildResult(400, "账号不能为空");

            req.Photo = Uploader.SaveFile(nameof(User), req.Photo);
            req.Photos = Uploader.SaveFiles(nameof(User), req.Photos);
            var saved = Data.SaveUser(req);
            return BuildResult(0, "保存成功", saved);
        }

    }
}

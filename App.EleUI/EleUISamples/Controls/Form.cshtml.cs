using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using App.Utils;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;

namespace App.Pages.EleUISamples
{

    public class FormModel : BaseModel
    {
        [BindProperty]
        public User Item { get; set; }
        public List<SelectListItem> TypeList { get; set; }
        public List<SelectListItem> CityList { get; set; }
        public List<Dept> DeptTree { get; set; }
        public List<Role> RoleList { get; set; }

        public void OnGet()
        {
            RoleList = new List<Role>
            {
                new Role { Id = 1, Name = "管理员" },
                new Role { Id = 2, Name = "编辑" },
                new Role { Id = 3, Name = "访客" }
            };
            TypeList = new List<SelectListItem>
            {
                new SelectListItem("类型A", "A"),
                new SelectListItem("类型B", "B"),
                new SelectListItem("类型C", "C")
            };
            
             CityList = new List<SelectListItem>
            {
                new SelectListItem("北京", "Beijing"),
                new SelectListItem("上海", "Shanghai"),
                new SelectListItem("广州", "Guangzhou")
            };

            DeptTree = new List<Dept>
            {
                new Dept
                {
                    Id = 1, Name = "部门1", Children = new List<Dept>
                    {
                        new Dept { Id = 11, Name = "部门1-1" },
                        new Dept { Id = 12, Name = "部门1-2" }
                    }
                },
                new Dept { Id = 2, Name = "部门2" }
            };
        }

        public IActionResult OnGetData()
        {
            Item = new User
            {
                Id = 1,
                Name = "测试用户",
                Age = 18,
                Score = 99.5m,
                Birthday = DateTime.Now,
                IsEnabled = true,
                Gender = "男",
                City = "Beijing",
                Types = new List<string> { "A", "C" },
                Icon = "fas fa-user", // Initial icon value for testing
                Description = "这是一个测试表单",
                RoleIds = new List<long> { 1, 3 } // Assign some role IDs for testing
            };
            return new JsonResult(new { code = 0, msg = "", data = Item });
        }


        public IActionResult OnPostSave([FromBody] User req)
        {
            req.Photo = Uploader.SaveFile("User", req.Photo); // Ensure the URL is properly formatted
            req.Photos = Uploader.SaveFiles("User", req.Photos);
            return BuildResult(0, "保存成功", req);
        }

        public IActionResult OnPostAdd([FromBody] User req)
        {
            Console.WriteLine("Add request: " + (req?.Name ?? "(null)"));
            // in demo user entity has no Id field; just echo back
            if (req != null && req.Id == 0)
            {
                req.Id = new Random().Next(1, 1000); // Assign a random Id for demo purposes
            }
            return new JsonResult(new App.HttpApi.APIResult(0, "新增成功", req));
        }
    }
}

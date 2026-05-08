using System;
using System.Collections.Generic;

namespace App.Pages.EleUISamples
{
    public class DeptUsersModel : BaseModel
    {
        public List<Dept> DeptTree { get; set; } = new();
        public string DefaultUsersUrl { get; set; } = string.Empty;
        public string AllUsersUrl => BuildUsersUrl(0);

        public void OnGet()
        {
            DeptTree = BuildDeptTree();
            DefaultUsersUrl = BuildUsersUrl(0);
        }

        private static string BuildUsersUrl(long deptId)
        {
            if (deptId <= 0)
                return "/EleUISamples/Datas/DeptUsersUsers";

            return $"/EleUISamples/Datas/DeptUsersUsers?deptId={Uri.EscapeDataString(deptId.ToString())}";
        }

        private static List<Dept> BuildDeptTree()
        {
            return new List<Dept>
            {
                new Dept
                {
                    Id = 0,
                    Name = "全部部门",
                    Children = new List<Dept>
                    {
                        new Dept
                        {
                            Id = 100,
                            Name = "综合管理部",
                            Children = new List<Dept>
                            {
                                new Dept { Id = 110, Name = "行政办公室" },
                                new Dept { Id = 120, Name = "人力资源部" }
                            }
                        },
                        new Dept
                        {
                            Id = 200,
                            Name = "研发中心",
                            Children = new List<Dept>
                            {
                                new Dept { Id = 210, Name = "平台研发部" },
                                new Dept { Id = 220, Name = "应用研发部" }
                            }
                        },
                        new Dept
                        {
                            Id = 300,
                            Name = "运营中心",
                            Children = new List<Dept>
                            {
                                new Dept { Id = 310, Name = "客户成功部" },
                                new Dept { Id = 320, Name = "市场运营部" }
                            }
                        }
                    }
                }
            };
        }
    }
}

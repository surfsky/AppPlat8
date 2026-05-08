using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using App.EleUI;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUISamples
{
    public class TreeModel : BaseModel
    {
        public List<Dept> BasicTree { get; set; } = new();
        public List<long> DefaultCheckedKeys { get; set; } = new();
        public List<long> DefaultExpandedKeys { get; set; } = new();

        public void OnGet()
        {
            BasicTree = BuildDeptTree();
            DefaultExpandedKeys = new List<long> { 100, 200 };
            DefaultCheckedKeys = new List<long> { 111, 221, 320 };
        }

        public IActionResult OnPostNodeClick([FromBody] JsonElement body)
        {
            var e = TreeNodeClickedEvent.Parse(body);
            var nodeName = e?.Value?.Name;
            var nodeId = e?.Value?.Id;
            //return BuildResult(0, $"节点点击成功：{nodeName} (ID={nodeId})");
            return EleManager.ShowNotify($"节点点击成功：您点击了节点 {nodeName} (ID={nodeId})", NotifyType.Info,  "success");
        }

        public IActionResult OnPostCheckedChanged([FromBody] JsonElement body)
        {
            var e = TreeNodeChangedEvent.Parse(body);
            var nodeName = e?.Value?.Data?.Name;
            var isChecked = e?.Value?.Checked == true;
            var isIndeterminate = e?.Value?.Indeterminate == true;
            var stateText = isChecked ? "已勾选" : (isIndeterminate ? "半选" : "已取消勾选");
            return EleManager.ShowNotify($"节点勾选状态变化：{nodeName} {stateText}", NotifyType.Info, "info");
        }

        private static List<Dept> BuildDeptTree()
        {
            return new List<Dept>
            {
                new Dept
                {
                    Id = 100,
                    Name = "综合管理部",
                    Children = new List<Dept>
                    {
                        new Dept
                        {
                            Id = 110,
                            Name = "行政办公室",
                            Children = new List<Dept>
                            {
                                new Dept { Id = 111, Name = "档案组" },
                                new Dept { Id = 112, Name = "后勤组" }
                            }
                        },
                        new Dept
                        {
                            Id = 120,
                            Name = "人力资源部",
                            Children = new List<Dept>
                            {
                                new Dept { Id = 121, Name = "招聘组" },
                                new Dept { Id = 122, Name = "培训组" }
                            }
                        }
                    }
                },
                new Dept
                {
                    Id = 200,
                    Name = "研发中心",
                    Children = new List<Dept>
                    {
                        new Dept
                        {
                            Id = 210,
                            Name = "平台研发部",
                            Children = new List<Dept>
                            {
                                new Dept { Id = 211, Name = "后端组" },
                                new Dept { Id = 212, Name = "基础设施组" }
                            }
                        },
                        new Dept
                        {
                            Id = 220,
                            Name = "应用研发部",
                            Children = new List<Dept>
                            {
                                new Dept { Id = 221, Name = "Web组" },
                                new Dept { Id = 222, Name = "移动组" }
                            }
                        }
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
            };
        }
    }
}

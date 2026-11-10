using System.Collections.Generic;
using System.Linq;
using App.Components;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUIDemo
{
    public class TreePickerModel : BaseModel
    {
        public PickerDemoItem Item { get; set; } = new();
        public List<PickerNode> TreeNodes { get; private set; } = new();
        public string MultiDefault { get; private set; } = "93,97";
        public string SingleDefault { get; private set; } = "97";

        public void OnGet()
        {
            TreeNodes = BuildTree();
        }

        public IActionResult OnGetData(Paging pi, List<long> tagSpecIds, long? singleTagId)
        {
            var row = new PickerDemoItem
            {
                Name = "筛选值已传到 EleTable 的 Data handler",
                SelectedTagIds = string.Join(", ", tagSpecIds ?? new List<long>()),
                SelectedSingleTagId = singleTagId?.ToString() ?? "(空)"
            };
            pi.SetTotal(1);
            return BuildResult(0, "success", new[] { row }, pi);
        }

        public IActionResult OnGetTree()
        {
            return BuildResult(0, "success", BuildTree());
        }

        public IActionResult OnGetFormData()
        {
            return BuildResult(0, "success", new
            {
                formTagSpecIds = new long[] { 93, 97 },
                formSingleTagId = 97
            });
        }

        public IActionResult OnPostSave([FromBody] Dictionary<string, object> values)
        {
            return BuildResult(0, "表单当前值", values);
        }

        private static List<PickerNode> BuildTree() => new()
        {
            new PickerNode
            {
                Id = 1,
                Name = "应急专用",
                Children = new List<PickerNode>
                {
                    new PickerNode
                    {
                        Id = 5,
                        Name = "厂房区域",
                        Children = new List<PickerNode>
                        {
                            new PickerNode { Id = 93, Name = "独立厂房" },
                            new PickerNode { Id = 95, Name = "厂中厂" },
                            new PickerNode { Id = 94, Name = "园中园" }
                        }
                    },
                    new PickerNode
                    {
                        Id = 6,
                        Name = "厂房范围",
                        Children = new List<PickerNode>
                        {
                            new PickerNode { Id = 97, Name = "集聚区内" },
                            new PickerNode { Id = 98, Name = "集聚区外" }
                        }
                    }
                }
            }
        };
    }

    public class PickerNode
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public List<PickerNode> Children { get; set; } = new();
    }

    public class PickerDemoItem
    {
        public string Name { get; set; }
        public List<long> TagSpecIds { get; set; } = new();
        public long? SingleTagId { get; set; }
        public string SelectedTagIds { get; set; }
        public string SelectedSingleTagId { get; set; }
        public List<long> FormTagSpecIds { get; set; } = new();
        public long? FormSingleTagId { get; set; }
    }
}

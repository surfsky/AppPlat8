using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUISamples
{
    /// <summary>弹出选择器（ElePicker）示例页面模型</summary>
    public class PickerModel : BaseModel
    {
        [BindProperty]
        public PickerItem Item { get; set; }

        /// <summary>加载示例表单数据（EleForm 客户端模式通过 ?handler=Data 拉取）</summary>
        public IActionResult OnGetData()
        {
            Item = new PickerItem
            {
                UserId = "2",
                UserName = "zhangsan（张三）",
                RoleId = "3",
                RoleName = "User",
                DeptId = "210",
                DeptName = "平台研发部",
                MultiIds = "1,3",
                MultiNames = "admin（管理员）,lisi（李四）"
            };
            return new JsonResult(new { code = 0, msg = "", data = Item });
        }

        /// <summary>保存示例数据</summary>
        public IActionResult OnPostSave([FromBody] PickerItem req)
        {
            return BuildResult(0, "保存成功", req);
        }
    }

    /// <summary>选择器示例数据</summary>
    public class PickerItem
    {
        [Display(Name = "用户")] public string UserId { get; set; }
        public string UserName { get; set; }
        [Display(Name = "角色")] public string RoleId { get; set; }
        public string RoleName { get; set; }
        [Display(Name = "部门")] public string DeptId { get; set; }
        public string DeptName { get; set; }
        [Display(Name = "多选")] public string MultiIds { get; set; }
        public string MultiNames { get; set; }
    }
}

using System;
using System.Collections.Generic;
using App.EleUI;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUISamples
{
    public class ServerDrawerModel : BaseModel
    {
        public ServerDrawerRow Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(App.Components.Paging pi)
        {
            var rows = new List<ServerDrawerRow>
            {
                new ServerDrawerRow
                {
                    Name = "关闭后动作演示",
                    ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    TraceId = Random.Shared.Next(1000, 9999).ToString()
                }
            };

            return BuildResult(0, "success", rows, pi);
        }

        public IActionResult OnPostOpenRefreshData()
        {
            return OpenDemoDrawer("RefreshData", DrawerCloseAction.RefreshData);
        }

        public IActionResult OnPostOpenRefreshPage()
        {
            return OpenDemoDrawer("RefreshPage", DrawerCloseAction.RefreshPage);
        }

        public IActionResult OnPostOpenNone()
        {
            return OpenDemoDrawer("None", DrawerCloseAction.None);
        }

        private IActionResult OpenDemoDrawer(string actionName, DrawerCloseAction action)
        {
            return EleManager.OpenClientDrawer(
                title: $"CloseAction = {actionName}",
                content: "点击右上角关闭按钮或遮罩关闭抽屉，观察列表刷新行为。",
                direction: "rtl",
                size: "520px",
                showFooter: true,
                footerButtons: new List<DrawerFooterButtonArgs>
                {
                    new DrawerFooterButtonArgs(Text: "关闭", Type: "primary", Action: "close")
                },
                closeAction: action);
        }
    }

    public class ServerDrawerRow
    {
        public string Name { get; set; }
        public string ServerTime { get; set; }
        public string TraceId { get; set; }
    }
}

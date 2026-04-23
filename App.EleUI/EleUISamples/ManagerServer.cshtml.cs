using App.EleUI;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace App.Pages.EleUISamples
{
    public class MessageBoxCallbackRequest
    {
        public string Action { get; set; }
        public string ClientHandler { get; set; }
        public string Text { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
    }

    public class DrawerClosedCallbackRequest
    {
        public string Action { get; set; }
    }

    public class InputBoxCallbackRequest
    {
        public string Action { get; set; }
        public string Value { get; set; }
        public string ClientHandler { get; set; }
        public string Text { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
    }

    /// <summary>
    /// Handler 触发服务端命令，再由前端 EleManager 执行。
    /// </summary>
    public class ManagerServerModel : BaseModel
    {
        public void OnGet()
        {
        }


        //---------------------------------------------------
        // Toast/Notify
        //---------------------------------------------------
        public IActionResult OnPostToast()
        {
            return EleManager.ShowClientToast("服务端下发 Toast 成功", NotifyType.Warning);
        }

        public IActionResult OnPostNotify()
        {
            return EleManager.ShowClientNotify("服务端下发 Notify 成功", NotifyType.Info, "Server Notify");
        }


        //---------------------------------------------------
        // Messagebox
        //---------------------------------------------------
        public IActionResult OnPostOpenMessageBoxAlert()
        {
            return EleManager.OpenClientMessageBox(
                text: "这是一个最简单的弹出框，仅显示一个确定按钮。",
                title: "Alert",
                type: NotifyType.Info,
                comfirmButtonText: "OK",
                isAlert: true,
                clientHandler: "inputHandler",
                serverHandler: ""
            );
        }

        public IActionResult OnPostOpenMessageBoxServer()
        {
            return EleManager.OpenClientMessageBox(
                text: "确认执行 MessageBox 服务端回调演示？",
                title: "服务端 MessageBox",
                type: NotifyType.Warning,
                comfirmButtonText: "继续",
                cancelButtonText: "取消",
                clientHandler: "inputHandler",
                serverHandler: "MessageBoxCallback"
            );
        }

        public IActionResult OnPostMessageBoxCallback([FromBody] MessageBoxCallbackRequest req)
        {
            var action = (req?.Action ?? string.Empty).Trim().ToLowerInvariant();
            if (action == "confirm")
            {
                return EleManager.ShowClientNotify("服务端回调：用户点击了确定", NotifyType.Success, "MessageBox Callback");
            }

            return EleManager.ShowClientNotify("服务端回调：用户点击了取消", NotifyType.Info, "MessageBox Callback");
        }

        //---------------------------------------------------
        // InputBox
        //---------------------------------------------------
        public IActionResult OnPostOpenInputBox()
        {
            return EleManager.OpenClientInputBox(
                text: "请输入用户名",
                title: "InputBox",
                inputPlaceholder: "例如：admin",
                inputValue: "",
                comfirmButtonText: "提交",
                cancelButtonText: "取消",
                type: NotifyType.Info,
                clientHandler: "inputHandler",
                serverHandler: ""
            );
        }

        public IActionResult OnPostOpenInputBoxServer()
        {
            return EleManager.OpenClientInputBox(
                text: "请输入要保存的备注",
                title: "InputBox Server Callback",
                inputPlaceholder: "请输入备注",
                inputValue: "来自服务端默认值",
                comfirmButtonText: "保存",
                cancelButtonText: "取消",
                type: NotifyType.Warning,
                clientHandler: "inputHandler",
                serverHandler: "InputBoxCallback"
            );
        }

        public IActionResult OnPostOpenInputBoxValidate()
        {
            return EleManager.OpenClientInputBox(
                text: "请输入邮箱地址",
                title: "InputBox with Validation",
                inputPlaceholder: "例如：user@example.com",
                inputValue: "",
                comfirmButtonText: "验证",
                cancelButtonText: "取消",
                type: NotifyType.Info,
                clientHandler: "inputHandler",
                serverHandler: "InputValidateCallback",
                inputPattern: "^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$",
                inputErrorMessage: "请输入有效的邮箱地址"
            );
        }

        public IActionResult OnPostInputValidateCallback([FromBody] InputBoxCallbackRequest req)
        {
            var action = (req?.Action ?? string.Empty).Trim().ToLowerInvariant();
            if (action == "confirm")
            {
                var email = req?.Value ?? string.Empty;
                return EleManager.ShowClientNotify($"邮箱验证通过：{email}", NotifyType.Success, "Validation Success");
            }

            return EleManager.ShowClientNotify("用户取消了验证", NotifyType.Info, "Validation Canceled");
        }

        public IActionResult OnPostInputBoxCallback([FromBody] InputBoxCallbackRequest req)
        {
            var action = (req?.Action ?? string.Empty).Trim().ToLowerInvariant();
            if (action == "confirm")
            {
                var value = req?.Value ?? string.Empty;
                return EleManager.ShowClientNotify($"服务端回调：用户输入了：{value}", NotifyType.Success, "InputBox Callback");
            }

            return EleManager.ShowClientNotify("服务端回调：用户取消了 InputBox", NotifyType.Info, "InputBox Callback");
        }


        //---------------------------------------------------
        // Drawer
        //---------------------------------------------------
        public IActionResult OnPostOpenDrawer()
        {
            return EleManager.OpenClientDrawer(
                title: "服务端 Drawer",
                content: "这是由服务端命令打开的 Drawer 内容。",
                size: "40%",
                direction: "rtl",
                showFooter: true,
                footerButtons: new List<DrawerFooterButtonArgs>
                {
                    new DrawerFooterButtonArgs(Text: "本地关闭", Type: "primary", Action: "close")
                },
                serverCloseHandler: "DrawerClosed");
        }

        public IActionResult OnPostCloseDrawer()
        {
            return EleManager.CloseClientDrawer();
        }

        public IActionResult OnPostDrawerClosed([FromBody] DrawerClosedCallbackRequest req)
        {
            var action = (req?.Action ?? string.Empty).Trim().ToLowerInvariant();
            return EleManager.ShowClientNotify($"服务端回调：Drawer 已关闭（action={action}）", NotifyType.Info, "Drawer Closed");
        }
    }
}

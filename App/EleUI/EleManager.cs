using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Dynamic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using App.DAL;
using App.Components;
using App.HttpApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using App.EleUI;


namespace App.EleUI
{
    /// <summary>
    /// 客户端命令类型
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ClientCommandType
    {
        Notify,
        //Message,
        Toast,
        MessageBox,
        InputBox,
        ShowLoading,
        CloseLoading,
        OpenDrawer,
        CloseDrawer
    }

    /// <summary>
    /// 通知类型
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum NotifyType
    {
        Success,
        Warning,
        Info,
        Error
    }

    /// <summary>
    /// 客户端命令
    /// </summary>
    public record ClientCommand(ClientCommandType Command, object Args, string RequestId, DateTime IssuedAtUtc);

    /// <summary>
    /// 通知参数
    /// </summary>
    public record NotifyArgs(NotifyType Type, string Message, string Title = null);

    /// <summary>
    /// 加载参数
    /// </summary>
    public record LoadingArgs(string Text = "加载中...");

    /// <summary>
    /// 抽屉参数
    /// </summary>
    public record DrawerFooterButtonArgs(
        string Text,
        string Type = null,
        bool Plain = false,
        string Action = "close",
        string Handler = null);

    /// <summary>
    /// 抽屉参数
    /// </summary>
    public record DrawerArgs(
        string Title = null,
        string Content = null,
        string Url = null,
        string Size = null,
        string Direction = null,
        bool? WithHeader = null,
        bool? ShowClose = null,
        bool? Modal = null,
        bool? CloseOnClickModal = null,
        bool? DestroyOnClose = null,
        bool? ShowFooter = null,
        string FooterAlign = null,
        List<DrawerFooterButtonArgs> FooterButtons = null,
        string ClientCloseHandler = null,
        string ServerCloseHandler = null,
        bool? Html = null);

    /// <summary>
    /// MessageBox 参数
    /// </summary>
    public record MessageBoxArgs(
        string Text,
        string Title = "提示",
        NotifyType Type = NotifyType.Info,
        string ComfirmButtonText = "确定",
        string CancelButtonText = "取消",
        bool IsAlert = false,
        string ClientHandler = null,
        string ServerHandler = null);

    /// <summary>
    /// InputBox 参数
    /// </summary>
    public record InputBoxArgs(
        string Text,
        string Title = "请输入",
        string InputPlaceholder = "请输入内容",
        string InputValue = "",
        string ComfirmButtonText = "确定",
        string CancelButtonText = "取消",
        NotifyType Type = NotifyType.Info,
        string ClientHandler = null,
        string ServerHandler = null,
        string InputPattern = null,
        string InputErrorMessage = null);


    /// <summary>
    /// 
    /// </summary>
    public class EleManager
    {
        //-------------------------------------------------
        // 构建API结果
        //-------------------------------------------------
        /// <summary>构建API结果</summary>
        public static JsonResult BuildResult(int code, string msg, object data = null, Paging pager = null)
        {
            // 首字母小写驼峰命名，和JavaScript的命名习惯一致
            // 忽略循环引用，避免序列化时出错
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
            return new JsonResult(new APIResult(code, msg, data, pager), settings);
        }

        private static IActionResult BuildClientCommandResult(ClientCommandType commandType, object args, string msg = "success")
        {
            var cmd = new ClientCommand(
                Command: commandType,
                Args: args,
                RequestId: Guid.NewGuid().ToString("N"),
                IssuedAtUtc: DateTime.UtcNow
            );
            return BuildResult(0, msg, cmd);
        }

        /// <summary>显示客户端 Toast（就是element中的 message 走轻提示）</summary>
        public static IActionResult ShowClientToast(string message, NotifyType type = NotifyType.Info, string title = "Title")
        {
            return BuildClientCommandResult(
                ClientCommandType.Toast,
                new NotifyArgs(Type: type, Message: message, Title: title)
            );
        }

        /// <summary>显示客户端提示</summary>
        public static IActionResult ShowClientNotify(string message, NotifyType type = NotifyType.Info, string title="Title")
        {
            return BuildClientCommandResult(
                ClientCommandType.Notify,
                new NotifyArgs(Type: type, Message: message, Title: title)
            );
        }

        /// <summary>显示客户端 Loading</summary>
        public static IActionResult ShowClientLoading(string text = "加载中...")
        {
            return BuildClientCommandResult(
                ClientCommandType.ShowLoading,
                new LoadingArgs(Text: text)
            );
        }

        /// <summary>关闭客户端 Loading</summary>
        public static IActionResult CloseClientLoading()
        {
            return BuildClientCommandResult(ClientCommandType.CloseLoading, new { });
        }

        /// <summary>打开客户端 Drawer</summary>
        public static IActionResult OpenClientDrawer(
            string title = null,
            string content = null,
            string url = null,
            string size = null,
            string direction = null,
            bool? withHeader = null,
            bool? showClose = null,
            bool? modal = null,
            bool? closeOnClickModal = null,
            bool? destroyOnClose = null,
            bool? showFooter = null,
            string footerAlign = null,
            List<DrawerFooterButtonArgs> footerButtons = null,
            string clientCloseHandler = null,
            string serverCloseHandler = null,
            bool? html = null)
        {
            return BuildClientCommandResult(
                ClientCommandType.OpenDrawer,
                new DrawerArgs(
                    Title: title,
                    Content: content,
                    Url: url,
                    Size: size,
                    Direction: direction,
                    WithHeader: withHeader,
                    ShowClose: showClose,
                    Modal: modal,
                    CloseOnClickModal: closeOnClickModal,
                    DestroyOnClose: destroyOnClose,
                    ShowFooter: showFooter,
                    FooterAlign: footerAlign,
                    FooterButtons: footerButtons,
                    ClientCloseHandler: clientCloseHandler,
                    ServerCloseHandler: serverCloseHandler,
                    Html: html)
            );
        }

        /// <summary>关闭客户端 Drawer</summary>
        public static IActionResult CloseClientDrawer()
        {
            return BuildClientCommandResult(ClientCommandType.CloseDrawer, new { });
        }

        /// <summary>
        /// 打开客户端 MessageBox。
        /// 若设置 ServerHandler，前端点击确认/取消后会 POST 到 ?handler={ServerHandler}。
        /// </summary>
        /// <param name="isAlert">是否为 Alert 类型，若为 true，则仅显示一个确定按钮。</param>
        /// <param name="clientHandler">客户端回调处理函数名称。</param>
        /// <param name="serverHandler">服务端回调处理函数名称。</param>
        public static IActionResult OpenClientMessageBox(
            string text,
            string title = "提示",
            NotifyType type = NotifyType.Info,
            string comfirmButtonText = "确定",
            string cancelButtonText = "取消",
            bool isAlert = false,
            string clientHandler = null,
            string serverHandler = null)
        {
            return BuildClientCommandResult(
                ClientCommandType.MessageBox,
                new MessageBoxArgs(
                    Text: text,
                    Title: title,
                    Type: type,
                    ComfirmButtonText: comfirmButtonText,
                    CancelButtonText: cancelButtonText,
                    IsAlert: isAlert,
                    ClientHandler: clientHandler,
                    ServerHandler: serverHandler));
        }

        /// <summary>
        /// 打开客户端 InputBox（基于 ElMessageBox.prompt）。
        /// 若设置 ServerHandler，前端点击确认/取消后会 POST 到 ?handler={ServerHandler}。
        /// </summary>
        /// <param name="clientHandler">客户端回调处理函数名称。</param>
        /// <param name="serverHandler">服务端回调处理函数名称。</param>
        /// <param name="inputPattern">输入验证正则表达式（可选）。</param>
        /// <param name="inputErrorMessage">验证失败提示信息（可选）。</param>
        public static IActionResult OpenClientInputBox(
            string text,
            string title = "请输入",
            string inputPlaceholder = "请输入内容",
            string inputValue = "",
            string comfirmButtonText = "确定",
            string cancelButtonText = "取消",
            NotifyType type = NotifyType.Info,
            string clientHandler = null,
            string serverHandler = null,
            string inputPattern = null,
            string inputErrorMessage = null)
        {
            return BuildClientCommandResult(
                ClientCommandType.InputBox,
                new InputBoxArgs(
                    Text: text,
                    Title: title,
                    InputPlaceholder: inputPlaceholder,
                    InputValue: inputValue,
                    ComfirmButtonText: comfirmButtonText,
                    CancelButtonText: cancelButtonText,
                    Type: type,
                    ClientHandler: clientHandler,
                    ServerHandler: serverHandler,
                    InputPattern: inputPattern,
                    InputErrorMessage: inputErrorMessage));
        }
    }
}
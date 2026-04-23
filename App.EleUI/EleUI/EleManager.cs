using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Dynamic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using App.Components;
using App.HttpApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using System.Linq.Expressions;
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
        CloseDrawer,
        SetControl
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
    /// 抽屉关闭后的客户端动作
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum DrawerCloseAction
    {
        RefreshPage,
        RefreshData,
        None
    }

    /// <summary>
    /// 客户端命令
    /// </summary>
    public record ClientCommand(ClientCommandType Command, object Args, string RequestId, DateTime Utc);

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
        bool? Resizable = null,
        bool? Modal = null,
        bool? CloseOnClickModal = null,
        bool? DestroyOnClose = null,
        bool? ShowFooter = null,
        string FooterAlign = null,
        List<DrawerFooterButtonArgs> FooterButtons = null,
        string CloseHandler = null,
        string ServerCloseHandler = null,
        DrawerCloseAction CloseAction = DrawerCloseAction.RefreshData,
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
    /// 控件目标类型
    /// </summary>
    public enum ControlTargetType
    {
        Field,
        ControlId
    }

    /// <summary>
    /// 强类型控件目标
    /// </summary>
    public readonly record struct ControlTarget(ControlTargetType Type, string Name)
    {
        public static ControlTarget Field(string fieldExpress) => new(ControlTargetType.Field, fieldExpress);
        public static ControlTarget ControlId(string controlId) => new(ControlTargetType.ControlId, controlId);

        public override string ToString()
        {
            var name = (Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var prefix = Type == ControlTargetType.ControlId ? "controlId" : "field";
            return $"{prefix}:{name}";
        }
    }

    /// <summary>
    /// 控件变更事件请求（通用）
    /// </summary>
    public class ControlChangeRequest
    {
        public string EventName { get; set; }
        public string ControlId { get; set; }
        public string FieldExpress { get; set; }
        public object Value { get; set; }
        public Newtonsoft.Json.Linq.JObject Form { get; set; }
    }

    /// <summary>
    /// 控件命令链式构建器
    /// </summary>
    public sealed class ControlCommandBuilder
    {
        private readonly List<ControlPatchArgs> _items = new();

        public ControlCommandBuilder SetControl(
            ControlTarget target,
            bool? Enabled = null,
            bool? Visible = null,
            object Data = null,
            object Value = null)
        {
            _items.Add(new ControlPatchArgs(target.ToString(), Enabled, Visible, Data, Value));
            return this;
        }

        public ControlCommandBuilder SetControl<T>(
            Expression<Func<T, object>> fieldExpress,
            bool? Enabled = null,
            bool? Visible = null,
            object Data = null,
            object Value = null)
        {
            var fieldName = EleManager.ResolveFieldExpress(fieldExpress);
            return SetControl(ControlTarget.Field(fieldName), Enabled, Visible, Data, Value);
        }

        public ControlCommandBuilder SetControlEnable<T>(Expression<Func<T, object>> fieldExpress, bool enabled)
        {
            return SetControl(fieldExpress, Enabled: enabled);
        }

        public ControlCommandBuilder SetControlVisible<T>(Expression<Func<T, object>> fieldExpress, bool visible)
        {
            return SetControl(fieldExpress, Visible: visible);
        }

        public ControlCommandBuilder SetControlData<T>(Expression<Func<T, object>> fieldExpress, object data, object value = null)
        {
            return SetControl(fieldExpress, Data: data, Value: value);
        }

        public IActionResult ToActionResult()
        {
            return EleManager.SetControl(_items);
        }
    }

    /// <summary>
    /// 控件状态变更项
    /// </summary>
    public record ControlPatchArgs(
        string Target,
        bool? Enabled = null,
        bool? Visible = null,
        object Data = null,
        object Value = null);

    /// <summary>
    /// 控件状态变更参数
    /// </summary>
    public record SetControlArgs(List<ControlPatchArgs> Items);


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
                Utc: DateTime.UtcNow
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
            string direction = "rtl",
            bool? withHeader = null,
            bool? showClose = true,
            bool? modal = true,
            bool? closeOnClickModal = false,
            bool? destroyOnClose = true,
            bool? resizable = true,
            bool? showFooter = null,
            string footerAlign = null,
            List<DrawerFooterButtonArgs> footerButtons = null,
            string closeHandler = null,
            string serverCloseHandler = null,
            DrawerCloseAction closeAction = DrawerCloseAction.RefreshData,
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
                    Resizable: resizable,
                    Modal: modal,
                    CloseOnClickModal: closeOnClickModal,
                    DestroyOnClose: destroyOnClose,
                    ShowFooter: showFooter,
                    FooterAlign: footerAlign,
                    FooterButtons: footerButtons,
                    CloseHandler: closeHandler,
                    ServerCloseHandler: serverCloseHandler,
                    CloseAction: closeAction,
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

        /// <summary>链式起点：强类型字段表达式</summary>
        public static ControlCommandBuilder SetControl<T>(
            Expression<Func<T, object>> fieldExpress,
            bool? Enabled = null,
            bool? Visible = null,
            object Data = null,
            object Value = null)
        {
            return new ControlCommandBuilder().SetControl(fieldExpress, Enabled, Visible, Data, Value);
        }

        /// <summary>链式起点：强类型目标</summary>
        public static ControlCommandBuilder SetControl(
            ControlTarget target,
            bool? Enabled = null,
            bool? Visible = null,
            object Data = null,
            object Value = null)
        {
            return new ControlCommandBuilder().SetControl(target, Enabled, Visible, Data, Value);
        }

        /// <summary>批量设置控件状态（底层入口）</summary>
        public static IActionResult SetControl(IEnumerable<ControlPatchArgs> items)
        {
            var list = (items ?? Enumerable.Empty<ControlPatchArgs>())
                .Select(i => new ControlPatchArgs(
                    Target: NormalizeControlTarget(i.Target),
                    Enabled: i.Enabled,
                    Visible: i.Visible,
                    Data: i.Data,
                    Value: i.Value))
                .ToList();

            return BuildClientCommandResult(
                ClientCommandType.SetControl,
                new SetControlArgs(list));
        }

        private static string NormalizeControlTarget(string target)
        {
            var normalized = (target ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return normalized;

            if (normalized.StartsWith("field:", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("controlId:", StringComparison.OrdinalIgnoreCase))
                return normalized;

            return $"field:{normalized}";
        }

        private static string NormalizeControlTarget(ControlTarget target)
        {
            return NormalizeControlTarget(target.ToString());
        }

        public static ControlTarget FieldTarget(string fieldExpress)
        {
            return ControlTarget.Field(fieldExpress);
        }

        public static ControlTarget ControlIdTarget(string controlId)
        {
            return ControlTarget.ControlId(controlId);
        }

        internal static string ResolveFieldExpress<T>(Expression<Func<T, object>> expr)
        {
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));

            MemberExpression memberExpr = expr.Body as MemberExpression;
            if (memberExpr == null && expr.Body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                memberExpr = unary.Operand as MemberExpression;

            if (memberExpr == null)
                throw new ArgumentException("字段表达式必须是属性访问，如 t => t.CountyId", nameof(expr));

            var name = memberExpr.Member?.Name;
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("无法解析字段名称", nameof(expr));

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Dynamic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using App.Components;
using System.Linq.Expressions;
using App.EleUI;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace App.EleUI
{
    //======================================================================
    // EleManager client command
    //======================================================================
    /// <summary>
    /// 客户端命令类型
    /// </summary>
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
        SetControl,
        RefreshData,
        RefreshPage,
        Sleep,  // 暂停指定毫秒后，再执行后续命令
        Redirect // 将当前窗口导航到指定地址
    }

    /// <summary>客户端命令</summary>
    public record ClientCommand(ClientCommandType Command, object Args)
    {
        public string RequestId => Guid.NewGuid().ToString("N");
        public DateTime Utc => DateTime.UtcNow;
    }


    //======================================================================
    // client command args
    //======================================================================
    /// <summary>
    /// 刷新作用域（适用于 RefreshData / RefreshPage）
    /// </summary>
    public enum RefreshScope
    {
        /// <summary>只刷新当前 iframe / 当前 EleTable 实例（默认）</summary>
        Self,
        /// <summary>刷新 parent window（打开本 Drawer 的父页面）</summary>
        Parent,
        /// <summary>刷新顶级 window（top）</summary>
        Top
    }

    /// <summary>
    /// 通知类型
    /// </summary>
    public enum NotifyType
    {
        Success,
        Warning,
        Info,
        Error
    }


    /// <summary>通知参数</summary>
    public record NotifyArgs(NotifyType Type, string Message, string Title = null);

    /// <summary>加载参数</summary>
    public record LoadingArgs(string Text = "加载中...");


    /// <summary>刷新数据命令参数</summary>
    public record RefreshDataArgs(RefreshScope Scope = RefreshScope.Parent, string InstanceId = null);

    /// <summary>刷新页面命令参数</summary>
    public record RefreshPageArgs(RefreshScope Scope = RefreshScope.Parent, bool ForceReload = true);

    /// <summary>睡眠参数（毫秒），用于让前面的提示先显示出来，再执行后续命令</summary>
    public record SleepArgs(int Milliseconds = 1000);

    /// <summary>页面跳转参数</summary>
    public record RedirectArgs(string Url);


    //======================================================================
    // client command chain
    //======================================================================
    /// <summary>客户端命令链。实现 IActionResult 以兼容直接 return；显式调用 ToResult() 可得到 JsonResult。</summary>
    public sealed class ClientCommandChain : IActionResult
    {
        private readonly List<ClientCommand> _commands = new();
        private bool _reloadData;

        internal ClientCommandChain(ClientCommand command)
        {
            if (command != null) _commands.Add(command);
        }

        internal ClientCommandChain(IEnumerable<ClientCommand> commands)
        {
            if (commands != null) _commands.AddRange(commands.Where(command => command != null));
        }

        public ClientCommandChain Then(ClientCommand command)
        {
            if (command != null) _commands.Add(command);
            return this;
        }

        public ClientCommandChain Sleep(int milliseconds = 1000)
            => Then(new ClientCommand(ClientCommandType.Sleep, new SleepArgs(milliseconds)));

        public ClientCommandChain Redirect(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("跳转地址不能为空。", nameof(url));
            return Then(new ClientCommand(ClientCommandType.Redirect, new RedirectArgs(url)));
        }

        public ClientCommandChain CloseDrawer()
            => Then(new ClientCommand(ClientCommandType.CloseDrawer, new { }));

        public ClientCommandChain RefreshData(RefreshScope scope = RefreshScope.Parent, string instanceId = null)
            => Then(new ClientCommand(ClientCommandType.RefreshData, new RefreshDataArgs(scope, instanceId)));

        public ClientCommandChain RefreshPage(RefreshScope scope = RefreshScope.Parent, bool forceReload = true)
            => Then(new ClientCommand(ClientCommandType.RefreshPage, new RefreshPageArgs(scope, forceReload)));

        /// <summary>客户端命令执行后重新加载当前 EleTable 数据，并尽量保留选中行。</summary>
        public ClientCommandChain ReloadData()
        {
            _reloadData = true;
            return this;
        }

        public ClientCommandChain Toast(string message, NotifyType type = NotifyType.Info, string title = null)
            => Then(new ClientCommand(ClientCommandType.Toast, new NotifyArgs(type, message, title)));

        public ClientCommandChain Notify(string message, NotifyType type = NotifyType.Info, string title = null)
            => Then(new ClientCommand(ClientCommandType.Notify, new NotifyArgs(type, message, title)));

        public JsonResult ToResult(string msg = "ok") => EleHandler.BuildCommandChainResult(msg, _commands, _reloadData);

        public Task ExecuteResultAsync(ActionContext context) => ToResult().ExecuteResultAsync(context);
    }



    //======================================================================
    // Drawer command
    //======================================================================
    /// <summary>
    /// 抽屉关闭后的客户端动作
    /// </summary>
    public enum DrawerCloseAction
    {
        RefreshPage,
        RefreshData,
        None
    }

    /// <summary>抽屉参数</summary>
    public record DrawerFooterButtonArgs(
        string Text,
        string Type = null,
        bool Plain = false,
        string Action = "close",
        string Handler = null);

    /// <summary>抽屉标签参数</summary>
    public record DrawerTabArgs(
        string Label,
        string Url,
        string Key = null);

    /// <summary>抽屉参数</summary>
    public record DrawerArgs(
        string Title = null,
        string Content = null,
        string Url = null,
        List<string> Urls = null,
        List<DrawerTabArgs> Tabs = null,
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
        bool? Html = null
        );

    //======================================================================
    // Popups command
    //======================================================================
    /// <summary>MessageBox 参数</summary>
    public record MessageBoxArgs(
        string Text,
        string Title = null,
        NotifyType Type = NotifyType.Info,
        string ComfirmButtonText = null,
        string CancelButtonText = null,
        bool IsAlert = false,
        string ClientHandler = null,
        string ServerHandler = null
        );

    /// <summary>InputBox 参数</summary>
    public record InputBoxArgs(
        string Text,
        string Title = null,
        string InputPlaceholder = null,
        string InputValue = "",
        string ComfirmButtonText = null,
        string CancelButtonText = null,
        NotifyType Type = NotifyType.Info,
        string ClientHandler = null,
        string ServerHandler = null,
        string InputPattern = null,
        string InputErrorMessage = null
        );

    //======================================================================
    // Control command
    //======================================================================
    /// <summary>控件目标类型</summary>
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
        public JsonElement Form { get; set; }
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
            var fieldName = EleHandler.ResolveFieldExpress(fieldExpress);
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

        public IActionResult ToActionResult(string msg = "success")
        {
            return EleHandler.SetControl(_items, msg);
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


    //======================================================================
    // EleManager 客户端命令管理器
    //======================================================================
    /// <summary>
    /// ElemUI Server Handler <----> EleManager client ui
    /// </summary>
    public class EleHandler
    {
        //-------------------------------------------------
        // Json result
        //-------------------------------------------------
        private static readonly JsonSerializerOptions _jsonOptions;

        static EleHandler()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        /// <summary>构建API结果</summary>
        public static JsonResult BuildResult(int code, string msg, object data = null, Paging pager = null)
        {
            return new JsonResult(new EleResult(code, msg, data, pager), _jsonOptions);
        }

        //--------------------------------------------------------------
        // build client command result
        //--------------------------------------------------------------
        /// <summary>构建一条客户端命令结果</summary>
        public static JsonResult BuildCommandResult(ClientCommandType commandType, object args, string msg = "ok")
        {
            return BuildResult(0, msg, new ClientCommand(commandType, args));
        }

        /// <summary>构建多条客户端命令（命令按数组顺序串行执行，可实现 toast → 关抽屉 → 刷数据 等串联动作）</summary>
        public static JsonResult BuildCommandsResult(params ClientCommand[] commands)
            => BuildCommandsResult("ok", commands);

        /// <summary>构建命令链响应。</summary>
        internal static JsonResult BuildCommandsResult(string msg, params ClientCommand[] commands)
        {
            var list = (commands ?? Array.Empty<ClientCommand>()).ToList();
            var payload = new
            {
                commands = list,
                commandCount = list.Count
            };
            return BuildResult(0, msg, payload);
        }

        internal static JsonResult BuildCommandChainResult(string msg, IEnumerable<ClientCommand> commands, bool reloadData)
        {
            var list = (commands ?? Enumerable.Empty<ClientCommand>()).ToList();
            return BuildResult(0, msg, new
            {
                commands = list,
                commandCount = list.Count,
                reloadData
            });
        }


        //--------------------------------------------------------------
        // 传递客户端命令
        //--------------------------------------------------------------
        /// <summary>显示客户端 Toast（就是element中的 message 走轻提示）</summary>
        public static ClientCommandChain ShowToast(string message, NotifyType type = NotifyType.Info, string title = "Title")
        {
            return new ClientCommandChain(new ClientCommand(
                ClientCommandType.Toast,
                new NotifyArgs(Type: type, Message: message, Title: title)));
        }

        /// <summary>Toast 命令链起点。</summary>
        public static ClientCommandChain Toast(string message, NotifyType type = NotifyType.Info, string title = "Title")
            => ShowToast(message, type, title);

        /// <summary>显示客户端提示</summary>
        public static ClientCommandChain ShowNotify(string message, NotifyType type = NotifyType.Info, string title="Title")
        {
            return new ClientCommandChain(new ClientCommand(
                ClientCommandType.Notify,
                new NotifyArgs(Type: type, Message: message, Title: title)));
        }

        //---------------------------------------------------------------
        // Loading
        //---------------------------------------------------------------
        /// <summary>显示客户端 Loading</summary>
        public static ClientCommandChain ShowLoading(string text = "加载中...")
        {
            return new ClientCommandChain(new ClientCommand(
                ClientCommandType.ShowLoading,
                new LoadingArgs(Text: text)));
        }

        /// <summary>关闭客户端 Loading</summary>
        public static ClientCommandChain CloseLoading()
        {
            return new ClientCommandChain(new ClientCommand(ClientCommandType.CloseLoading, new { }));
        }

        //---------------------------------------------------------------
        // Drawer
        //---------------------------------------------------------------
        /// <summary>打开客户端 Drawer</summary>
        public static ClientCommandChain ShowDrawer(
            string title = null,
            string content = null,
            string url = null,
            List<string> urls = null,
            List<DrawerTabArgs> tabs = null,
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
            var normalizedTabs = tabs?
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Url))
                .Select(t => new
                {
                    label = t.Label,
                    url = t.Url,
                    key = t.Key
                })
                .ToList();

            var normalizedUrls = (urls ?? normalizedTabs?.Select(t => t.url).ToList())
                ?.Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            return new ClientCommandChain(new ClientCommand(
                ClientCommandType.OpenDrawer,
                new
                {
                    title,
                    content,
                    url,
                    urls = normalizedUrls,
                    tabs = normalizedTabs,
                    size,
                    direction,
                    withHeader,
                    showClose,
                    resizable,
                    modal,
                    closeOnClickModal,
                    destroyOnClose,
                    showFooter,
                    footerAlign,
                    footerButtons,
                    closeHandler,
                    serverCloseHandler,
                    closeAction,
                    html
                }));
        }

        /// <summary>关闭客户端 Drawer</summary>
        public static ClientCommandChain CloseDrawer()
        {
            return new ClientCommandChain(new ClientCommand(ClientCommandType.CloseDrawer, new { }));
        }

        //---------------------------------------------------------------
        // 客户端数据刷新
        //---------------------------------------------------------------
        /// <summary>
        /// 刷新 EleTable 数据（不刷新页面）。
        /// 默认从 Drawer 页发给父 iframe 的 parent window，命中 Atts 列表的 window.__attsTableInstance__。
        /// </summary>
        public static ClientCommandChain RefreshData(RefreshScope scope = RefreshScope.Parent, string instanceId = null)
        {
            return new ClientCommandChain(new ClientCommand(
                ClientCommandType.RefreshData,
                new RefreshDataArgs(Scope: scope, InstanceId: instanceId)));
        }

        /// <summary>
        /// 刷新整个页面（location.reload）。
        /// </summary>
        public static ClientCommandChain RefreshPage(RefreshScope scope = RefreshScope.Parent, bool forceReload = true)
        {
            return new ClientCommandChain(new ClientCommand(
                ClientCommandType.RefreshPage,
                new RefreshPageArgs(Scope: scope, ForceReload: forceReload)));
        }

        /// <summary>
        /// 打开客户端 MessageBox。
        /// 若设置 ServerHandler，前端点击确认/取消后会 POST 到 ?handler={ServerHandler}。
        /// </summary>
        /// <param name="isAlert">是否为 Alert 类型，若为 true，则仅显示一个确定按钮。</param>
        /// <param name="clientHandler">客户端回调处理函数名称。</param>
        /// <param name="serverHandler">服务端回调处理函数名称。</param>
        public static ClientCommandChain ShowMessageBox(
            string text,
            string title = null,
            NotifyType type = NotifyType.Info,
            string comfirmButtonText = null,
            string cancelButtonText = null,
            bool isAlert = false,
            string clientHandler = null,
            string serverHandler = null)
        {
            return new ClientCommandChain(new ClientCommand(
                ClientCommandType.MessageBox,
                new MessageBoxArgs(
                    Text: text,
                    Title: title ?? Texts.Current.Prompt,
                    Type: type,
                    ComfirmButtonText: comfirmButtonText ?? Texts.Current.Confirm,
                    CancelButtonText: cancelButtonText ?? Texts.Current.Cancel,
                    IsAlert: isAlert,
                    ClientHandler: clientHandler,
                    ServerHandler: serverHandler)));
        }

        /// <summary>
        /// 打开客户端 InputBox（基于 ElMessageBox.prompt）。
        /// 若设置 ServerHandler，前端点击确认/取消后会 POST 到 ?handler={ServerHandler}。
        /// </summary>
        /// <param name="clientHandler">客户端回调处理函数名称。</param>
        /// <param name="serverHandler">服务端回调处理函数名称。</param>
        /// <param name="inputPattern">输入验证正则表达式（可选）。</param>
        /// <param name="inputErrorMessage">验证失败提示信息（可选）。</param>
        public static ClientCommandChain ShowInputBox(
            string text,
            string title = null,
            string inputPlaceholder = null,
            string inputValue = "",
            string comfirmButtonText = null,
            string cancelButtonText = null,
            NotifyType type = NotifyType.Info,
            string clientHandler = null,
            string serverHandler = null,
            string inputPattern = null,
            string inputErrorMessage = null)
        {
            return new ClientCommandChain(new ClientCommand(
                ClientCommandType.InputBox,
                new InputBoxArgs(
                    Text: text,
                    Title: title ?? Texts.Current.PleaseInput,
                    InputPlaceholder: inputPlaceholder ?? Texts.Current.PleaseInput,
                    InputValue: inputValue,
                    ComfirmButtonText: comfirmButtonText ?? Texts.Current.Confirm,
                    CancelButtonText: cancelButtonText ?? Texts.Current.Cancel,
                    Type: type,
                    ClientHandler: clientHandler,
                    ServerHandler: serverHandler,
                    InputPattern: inputPattern,
                    InputErrorMessage: inputErrorMessage)));
        }

        //---------------------------------------------------------------
        // 客户端控件设置
        //---------------------------------------------------------------
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
        public static JsonResult SetControl(IEnumerable<ControlPatchArgs> items, string msg = "success")
        {
            var list = (items ?? Enumerable.Empty<ControlPatchArgs>())
                .Select(i => new ControlPatchArgs(
                    Target: NormalizeControlTarget(i.Target),
                    Enabled: i.Enabled,
                    Visible: i.Visible,
                    Data: i.Data,
                    Value: i.Value))
                .ToList();

            return BuildCommandResult(
                ClientCommandType.SetControl,
                new SetControlArgs(list),
                msg);
        }

        // 规范化控件目标字符串，支持自动添加 "field:" 前缀
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

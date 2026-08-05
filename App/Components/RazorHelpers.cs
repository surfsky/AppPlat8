using App.DAL;
using Microsoft.AspNetCore.Http;

namespace App.Components
{
    /// <summary>
    /// Razor 表达式全局帮助方法。
    /// <para>
    /// 在 <c>_ViewImports.cshtml</c> 中通过 <c>@using static</c> 导入后，
    /// 所有 .cshtml 页面可以直接使用 <c>@QString("key")</c> 读取当前 URL 查询参数，
    /// 无需 <c>@Model.</c> 前缀。
    /// </para>
    /// <code>
    /// // _ViewImports.cshtml
    /// @using static App.Components.RazorHelpers
    ///
    /// // 任意 .cshtml
    /// &lt;EleInput Value="@QString("name")" /&gt;
    /// &lt;EleSelect Value="@QString("objectType")" /&gt;
    /// </code>
    /// </summary>
    public static class RazorHelpers
    {
        /// <summary>
        /// 读取当前 URL 的查询参数（querystring）。
        /// 无对应参数时返回 <paramref name="defaultValue"/>。
        /// </summary>
        public static string QString(string key, string defaultValue = null)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            var req = App.Web.Asp.Request;
            if (req == null) return defaultValue;
            return req.Query.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)
                ? v.ToString()
                : defaultValue;
        }

        /// <summary>QString 的短别名</summary>
        public static string Q(string key, string defaultValue = null) => QString(key, defaultValue);

        /// <summary>检查当前用户是否有指定权限</summary>
        public static bool AuthPower(Power power) => Auth.CheckPower(power);
    }
}

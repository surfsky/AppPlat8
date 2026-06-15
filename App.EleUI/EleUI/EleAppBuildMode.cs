namespace App.EleUI
{
    /// <summary>
    /// Vue 应用构建模式。
    /// </summary>
    public enum EleAppBuildMode
    {
        None,   // 不渲染 Vue 应用，仅渲染静态 HTML
        Server, // 服务器端渲染，注入初始数据
        Client, // 客户端渲染，不注入初始数据, 由客户端请求OnGetData()方法异步加载
    }
}

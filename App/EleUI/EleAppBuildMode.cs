namespace App.EleUI
{
    /// <summary>
    /// Vue 应用构建模式。Server 模式会在服务器端渲染时将初始数据注入到页面中，Client 模式则不注入初始数据，由客户端请求OnGetData()方法异步加载。
    /// </summary>
    public enum EleAppBuildMode
    {
        None,
        Server,
        Client,
    }
}

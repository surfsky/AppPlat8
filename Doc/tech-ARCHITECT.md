# 技术解决方案

本文档记录了项目的技术解决方案。


## API服务为什么要用 HttpApi
    Core API太弱了
    改用HttpAPI，附加功能如：
        文档: 替代 Swagger，自带 API 文档和测试网页
        缓存：可设置每个接口的数据缓存方案
        授权: AuthLogin, AuthToken, AuthRole, AuthUser
        WAF：AuthTraffic
        异常: HttpApi.onException
        统一结构：APIResult内置code、msg, data、pager等规范结构数据。

## 前后端技术架构选择

- 方案一：razor tag helper 对 vue + element plus + tailswindCss 进行封装
    特点：兼顾性能和开发效率，接口可用HttpApi、RazorPageModel
    优点：用Razor tagHelper 封装组件后，每个页面要写的代码量极少
    缺点：ai不是很擅长，返工率高，测试不便。若更换UI库，需要重新封装组件。
    其他：控件库可以卖钱

- 方案二：Blazor
    特点：服务器端渲染方式类似webform，完全不分前后端，代码量少。
    缺点：下载大、初始化慢、服务器占用内存高、并发差、与其他前端框架集成有点困难
    优点：开发快速，全 C# 开发
    其他：不知道微信浏览器是否支持
    混用：
        项目的首页、关于页、SEO 核心页用.cshtml（Razor Pages）做服务端渲染，保证搜索引擎抓取和首屏加载速度；
        项目的交互式模块（如后台数据表格、表单提交、实时通知）用.razor（Blazor）组件实现，减少 JavaScript 开发量。

- 方案三：写数据接口，前端用任意前端框架
    特点：完全前后端分离
    推荐前端方案：
        Vite + Vue + Element Plus + Tailwind CSS + TypeScript
        Vite + React + Element Plus + Tailwind CSS + TypeScript
        Vite + TDesign + TypeScript (手机端网页)
        UniApp + typeScript + TDesign（跨平台）
        Vite + Vant + TypeScript（手机端网页）
    优点：AI 较为擅长这种逻辑清晰的分工方式，接口稳定后，前端随便让AI改，也不会影响后端逻辑。
    缺点：AI 会写一堆的前端代码，每个页面都不一样（也许就是AI时代的编程方式吧），另外还有很多 js 代码工作量。如果人工编程会是噩梦。

- RazorPage or Blazor？

    Razor Pages（.cshtml）和 Blazor（.razor）同属ASP.NET Core 生态，可以在同一个ASP.NET Core 项目中混用
    典型场景：
        项目的首页、关于页、SEO 核心页用.cshtml（Razor Pages）做服务端渲染，保证搜索引擎抓取和首屏加载速度；
        项目的交互式模块（如后台数据表格、表单提交、实时通知）用 Blazor 组件实现，减少 JavaScript 开发量。



更多技术细节，请查看/Doc/Specs/ 下的文档
# AppPlat8 技术栈与系统架构

## 技术栈

### 后端（.NET 8.0 / ASP.NET Core，C#）

| 类别 | 选型 |
|---|---|
| 运行时 | .NET 8.0（`global.json` / 各 csproj `net8.0`） |
| Web 框架 | ASP.NET Core MVC + Razor Pages + Blazor Server + BootstrapBlazor + SignalR |
| ORM | Entity Framework Core 8.0（App/App.csproj:107-114） |
| 数据库 | SQLite（默认）、MySQL（Pomelo）、SQL Server；PostgreSQL 规划中 |
| API 框架 | 自研 HttpApi（App.API/HttpApi，替代 WebAPI 的数据服务框架，内置权限/异常/日志/限流/文档/测试） |
| 定时任务 | Quartz 3.13（App.Consoler） |
| 日志 | Serilog + 自研 Logger（App/Logs） |
| Excel | NPOI 2.7.2 |
| 图像 | SkiaSharp、System.Drawing（图片缓存/缩放/验证码） |
| AI/ML | Microsoft.ML.Vision + SciSharp TensorFlow Redist |
| 缓存 | MemoryCache + StackExchangeRedis（App.Utils/App.Utils.csproj:74） |
| 其它 | aliyun SMS SDK（短信）、Jint（JS引擎）、Z.EntityFramework.Plus（批量）、PinYinConverterCore、CodeDom |

### 前端（App.EleUI / App/wwwroot）

| 类别 | 选型 |
|---|---|
| 框架 | Vue 3 + Element Plus + Tailwind CSS 4（App/Pages/Shared/_Layout.cshtml:7-16，CDN 引入） |
| 自研控件库 | App.EleUI：Razor TagHelper 服务端渲染 + esbuild 打包的 JS 运行时（Form/Table/List/Tree/Drawer 等） |
| 地图 GIS | Mapbox GL JS v2.9.1（三维地形、GeoJSON、台风/气象图层、全景/3D 模型） |
| 其它 | editor.md（CodeMirror）、jQuery、Bootstrap、open-iconic |

### 测试与部署

- 测试：xUnit（App.UtilsTests、App.BLL.Tests、HttpApi.Test）、Playwright/Puppeteer 网页自动化
- 部署：Docker（Linux）、文件部署、`dotnet app.dll --urls=...`

## 系统分层架构

```
┌──────────────────────────────────────────────────────────────────┐
│                    表示层  App (ASP.NET Core Host)                 │
│   Razor Pages / MVC 页面  │  Blazor/BootstrapBlazor  │  Static     │
│   Pages/(OA,Checks,GIS,Admins,AI...)  Components/   │  wwwroot    │
├──────────────────────────────────────────────────────────────────┤
│            前端 UI 运行时（浏览器端）                              │
│   Vue3 + Element Plus + Tailwind  │  App.EleUI(eleui.js)  │  GIS   │
├──────────────────────────────────────────────────────────────────┤
│                接口 / 服务层                                       │
│   HttpApi 框架(App.API/HttpApi)          SignalR ChatHub           │
│   权限·异常·日志·限流·文档·测试       (RESTful /HttpApi/*)         │
├──────────────────────────────────────────────────────────────────┤
│             业务逻辑层  App.BLL  (业务实体 + 业务规则)              │
│   Models/ (OA,Checks,GIS,Tasks,Workflows,Maintains,CRM,Configs)   │
│   Entities/ (EntityBase/TreeEntity + 数据权限·审计 Scope)          │
├──────────────────────────────────────────────────────────────────┤
│             数据访问层  EF Core 8.0  (AppPlatContext)              │
│   DbContext · Migrations · Z.EntityFramework.Plus(批量/缓存)       │
├──────────────────────────────────────────────────────────────────┤
│             基础工具层  App.Utils (通用工具/缓存/Redis)            │
│               App.Web (Asp请求上下文/AuthHelper/Cookie)            │
├──────────────────────────────────────────────────────────────────┤
│         基础设施：SQLite / MySQL / SQL Server · Redis · Files       │
└──────────────────────────────────────────────────────────────────┘

外部进程：App.Consoler(Quartz定时任务)   →   业务层/数据库
测试项目：App.UtilsTests · App.BLL.Tests · HttpApi.Test
```

### 依赖方向

依赖自上而下：`App → App.Web → App.API/HttpApi → App.BLL → App.Utils`（App/App.csproj:381-387 的 ProjectReference）。

- App 引用 App.Web、App.API/HttpApi、App.BLL、App.Utils、App.EleUI
- App.BLL 仅依赖 App.Utils
- App.EleUI 为独立 Razor 类库（RCL），被 App 引用
- App.Consoler（Quartz 后台任务）为独立进程，通过业务层访问数据库

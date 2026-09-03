# AppPlat8

该系统是一个基于 .NET 8.0 的低代码 Web 应用开发平台，提供了以下功能：

- 用户管理：用户、角色、权限、组等。
- 运维：日志、菜单、在线等。
- OA：文章、资产、预算、项目、交办任务等。
- 驾驶舱：地图、图层、企业、区域、面板等。
- 隐患管理：对象、隐患、排查、任务、报表等。

内置以下开源方案

- UI 组件：基于 Element Plus 、VUE3、tailwindcss 构建的 UI 组件库。代码精简。
- Entities：基于EntityFramework 的数据实体 OR Mapping 方案。代码精简。
- HttpApi服务：提供 API 服务，用于前端调用，内置权限、异常、日志、文档、测试页面等功能；
- 工作流引擎： 基于 App.LiteFlow 的工作流引擎的低代码工作流管理系统。

作者

- 作者: https://github.com/surfsky
- 项目网址：https://github.com/surfsky/AppPlat8
- License: MIT

![web](./Doc/images/web.png)
![mobile](./Doc/images/mobile.png)
![gis](./Doc/images/gis.png)
![gisglobal](./Doc/images/gisGlobal.png)
![cityweather](./Doc/images/cityWeather.png)

## 快速开始

1. 确保已安装 .NET 8 SDK。
2. 在 `appsettings.json` 中配置数据库连接。
3. 运行应用程序:
   ```bash
    # 编译并运行主项目
    cd AppPlat
    dotnet build
    dotnet run --project App
    dotnet run --project App/App.csproj --urls "http://172.20.165.221:6060"
    dotnet app.dll --urls=http://localhost:6060;http://abc.org

    # 编译EleUI示例项目
    dotnet build App.EleUI/EleUISamples/EleUISamples.csproj
    dotnet run --project App.EleUI/EleUISamples/EleUISamples.csproj
    调试： vscode 左侧的调试图标页面打开，选择 Debug EleUISamples 进行调试。或者顶部的命令行中输入：Debug EleUISamples；

    # 运行Consoler项目
    # 或 dotnet app.Consoler.dll --conn=Data Source=./App/Db/sqlite.db
    dotnet run --project App.Consoler


    # 测试项目
    dotnet test App.Utils/App.UtilsTests/App.UtilsTests.csproj
   ```
4. 打开浏览器，访问 `http://localhost:6060` 或 `http://abc.org`。

其它

1. 数据库迁移: 运行

```bash
   dotnet ef migrations add UserAuthOrgs --project App/App.csproj --startup-project App/App.csproj
```

2. 若端口被占用，查找占用 6060 的进程，然后kill

```bash
    lsof -nP -iTCP:6060 -sTCP:LISTEN && lsof -ti tcp:6060 | xargs -n 1 kill -9
```

## 部署

文件部署方式

- 将/App目录下的所有文件复制到部署目录下。
- 配置数据库连接字符串：在部署目录下的 `appsettings.json` 文件中配置数据库连接字符串。
- dotnet app.dll --urls=http://localhost:6060;http://abc.org

Docker 部署方式

- 应用程序目录：
- 数据库文件映射：
- 临时文件映射：

## 数据备份

数据库备份：

- sqlite 数据库备份：直接拷贝一份到别地方就行
- postgresql 数据库备份：用 pg_dump 命令备份，用 pgagent 配置定时任务备份
- mysql 数据库备份：用 mysqldump 命令备份，用 mysqlbackup 配置定时任务备份

用户上传的文件备份：

- /App/Files/

## AI开发环境

vscode copilot：
    C# 补全和重构：内置支持（用microsoft xxx 插件）
    C# 代码调试：内置支持；
    20260601更改付费逻辑后，10美元一天就用完了，提高一档要39美元
trae：
    C# 补全和重构：编程提示需安装ReSharper 插件，重构经常不能完全生效；
    C# 代码调试：调试需要安装 C# with NetCoreDbg，
    lite $3，pro $10, pro+$30, Ultra $200；
Cursor：
    pro $20每月（500次请求）
OpenAI Codex:

CodeGraphy 代码图谱以减少token消耗
  npm install -g @colbymchenry/codegraph

- 测试
  - 若有需要，请自动化测试，不限于以下方法
  - 对于类库方法，创建对应的测试用例并进行单元测试；
  - 对于网页：
    - curl页面，保存临时文件运行调试；
    - 创建或修改自动测试脚本（在目录 /AppPlat/WebTest/ 下）进行测试并截图验证
    - 自动化网页测试工具不限于：Puppeteer、Playwright、Microsoft playright-mcp、Chrome DevTools MCP、Selenium
    - 对于有授权的网页(AllowAttribute, AuthAttribute)，若绕不开登录页面，可先关闭页面授权进行测试，测试成功后再开启页面授权。

## 菜单及访问权限

以实际运行时的菜单为准

```
｜目录｜名称    ｜网页                     ｜ 访问权限            ｜
｜---｜--------｜------------------------｜--------------------｜
排查
    对象      Checks/CheckObjects           CheckObjectView
    排查      Checks/CheckLogs              CheckLogView
    检查表    Checks/CheckSheets            CheckSheetView
    隐患      Checks/CheckHarzards          CheckHarzardView
    任务      Checks/CheckTasks             CheckTaskView
    报表      Checks/CheckReports           CheckReportView
OA
    资产      OA/Assets                     AssetView
    预算      OA/Budgets                    BudgetView
    公告      OA/Annouces                   AnnouceView
    公司      OA/Company                    CompanyView
知识库
    文档      Articles/Articles             ArticleView
    目录      Articles/ArticleDirs          ArticleView
交办
    项目      OA/Projects                   ProjectView
    交办      OA/Tasks                      TaskView
    事件      OA/Events                     EventView
驾驶舱
    驾驶舱    GIS/Index                     GisIndexView
    菜单      GIS/Menu                      GisMenuView
    点位      GIS/Geometry                  GisGeometryView
    面板      GIS/Panels                    GisPanelView
账户
    组织      Admins/Orgs                   OrgView
    用户      Admins/Users                  UserView
    权限      Admins/Roles                  RoleView
运维
    菜单      Maintains/Menus               MenuView
    在线      Maintains/Onlines             OnlineView
    配置      Maintains/Config              ConfigView
    日志      Maintains/Logs                LogView
开发
    图标     Dev/Icons                      Dev
    API     Dev/API                        Dev
    控件库   EleUI/Index                    Dev
修改密码     Admins/ChangePassword          Site
安全退出     Logout                         Site
登陆        Login                          Site
```

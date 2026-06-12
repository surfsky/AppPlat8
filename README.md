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

![web](./Doc/web.png)
![mobile](./Doc/mobile.png)


## 快速开始

1. 确保已安装 .NET 8 SDK。
2. 在 `appsettings.json` 中配置数据库连接。
3. 运行应用程序:
   ```bash
    # 编译并运行主项目
    cd AppPlat
    dotnet build
    dotnet run --project App
    dotnet run --project App/App.csproj --urls "http://127.0.0.1:6060"
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

1. 数据库迁移: 运行 `dotnet ef migrations add OrgLevelSolo --project App/App.csproj --startup-project App/App.csproj`.
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




## 进度

- [x] 基础框架
    - [x] 登陆退出
    - [x] 响应式管理后台页面布局
    - [x] 权限：角色、用户、菜单
    - [x] 菜单：动态加载、权限控制
    - [x] 修改密码
    - [x] 账户
    - [x] 关于
    - [x] API 框架：HttpApi
- [x] 账户
    - [x] 组织
    - [x] 用户
    - [x] 角色权限
- [x] 运维
    - [x] 菜单: 展示、修改
    - [x] 站点配置
    - [x] 日志
- [x] OA
    - [x] 资产：清单、详情、资产类别
    - [x] 预算：清单、详情、预算类别、预算跟踪（尚未实现）
    - [x] 公告：契丹、详情、展示页面（尚未实现）
    - [x] 公司：清单、详情
- [x] 交办
    - [x] 交办：清单、详情、进度跟踪（尚未实现）
    - [x] 事件：清单、详情
    - [x] 项目：清单、详情、进度跟踪（尚未实现）
- [x] 知识库
    - [x] 文档: 清单、详情、附件（尚未实现）、评论（尚未实现）
    - [x] 目录: 清单、详情
    - [x] 各种文档的在线展示
      - [x] Word
      - [x] Excel
      - [x] PDF
      - [x] Markdown
      - [x] 脑图
      - [x] 视频
      - [x] 全景图
      - [x] 三维模型
- [x] 平台框架
    - [x] 图标
    - [x] API 框架
    - [x] EleUI控件
      - [x] Form
      - [x] Table
      - [x] Control
      - [x] Tree
      - [x] IconSelect
      - [x] UserSelect
      - [x] TreeSelect
      - [x] EleManager
      - [x] Panel
      - [x] CollapsPanel
      - [x] SplitPanal
      - [x] App
      - [x] 复杂组合页面
- [ ] 欢迎页（Dashboard）
    - [x] 基础页面
    - [ ] 消息: 公告
    - [ ] 任务: 个人、部门、单位
- [ ] 排查
    - [x] 对象: 清单、详情、检索（尚未完整实现）
        - [x] 清单详情
        - [x] 联系人子表
        - [x] 附件子表
        - [x] 数据导入
    - [ ] 检查表：
        - [x] 标签管理
        - [x] 清单、详情
        - [x] 检查项
        - [x] 标签数据导入
        - [ ] 检查表数据导入
    - [ ] 排查
        - [x] 排查：清单、详情
        - [x] 隐患：清单、详情
        - [x] 任务：清单、详情
        - [x] 检查表：检查表、标签、检查项
        - [x] 风险点：清单、详情
        - [ ] 日常排查流程：
            - [ ] 选取一个企业、点击排查、选择检查表、显示检查项、填写检查信息、生成隐患记录、提交
            - [ ] 查看隐患记录、复查登记
        - [ ] 任务排查流程：
            - [ ] 创建任务，筛选企业、检查表、承接单位
            - [ ] 承接单位分配任务到个人
            - [ ] 个人点击任务、点击排查、填写检查信息、生成隐患记录、提交
            - [ ] 查看隐患记录、复查登记
    - [ ] 报表：
        - [ ] 定时统计引擎
        - [ ] 统计表结构设计
        - [ ] 对象报表
        - [ ] 排查报表
        - [ ] 隐患报表
        - [ ] 任务报表
        - [ ] 综合报表
- [ ] 驾驶舱
    - [x] 设计
    - [x] 基础功能
      - [x] 地图：MapBox地图，支持三维地图
      - [x] 图层面板：图层树展示
      - [x] 工具栏：查找、全屏、复位
      - [x] 地图视图切换：矢量、遥感、三维等
      - [x] 模式切换：数据、统计
    - [x] 图层菜单
      - [x] 图层菜单管理：清单、详情、修改、删除；
      - [x] 图层菜单面板：显示菜单树，右侧有该点位的数据量统计标签。
      - [x] 点击节点后，可在地图上显示该菜单下的所有点位
      - [x] 点击统计标签后，可以展示该菜单下的点位清单
    - [x] 点位
      - [x] 点位管理：包括点、线、面等，支持geojson数据格式，及扩展datajson数据信息
      - [x] 点位在地图上展示：在地图上展示点位（包括点、线、面）
      - [x] 点位详情面板，扩展datajson字段可展示为表格格式
      - [x] 点位列表面板，展示该图层菜单下的所有点位清单，点击可地图居中定位
      - [ ] 支持多种展示方式：
        - [x] 点、区域：在地图上直接展示
        - [x] 全景图：在地图上显示点，用附件方式容纳和展示
        - [ ] 三维建筑模型：
            - [x] 在地图上显示点，用附件方式容纳和展示；
            - [ ] 在地图上直接展示三维模型；
        - [ ] 监控：在地图上显示点，点击后可播放监控视频（使用PageUrl打开监控页面）
        - [ ] 区域图片：在地图上直接展示，会随着Zoom级别变化而自动拉伸显示（或切块展示）
    - [x] 统计面板
      - [ ] 区域：
        - [ ] 屏幕分成左、右、中上、中、中下几个区域，每个区域内进行流式布局，可定义尺寸Size、流式布局方向FlowDirection、间距Gap、边距Margin、可滚动方向ScrollDirection等；
        - [ ] 接口返回这些区域的json配置信息；
        - [ ] 驾驶舱页面对这些区域进行响应式布局；
      - [x] 面板
        - [x] 基础信息：区域、尺寸、提示等
        - [x] 展示方式：简单文本、html片段、表格、图表等
      - [ ] 统计图表
        - [x] 支持折线图、柱状图、饼图等
        - [ ] 数据源配置：从数据库、文件、接口等获取数据
        - [ ] 业务报表：根据需求完成业务统计图表6个
    - [x] 场景
        - [x] 场景管理：清单(Icon, Name, Desc, MapCenter, MapZoom)、详情；
        - [x] 场景图层管理：管理场景下的所有图层
        - [x] 场景面板管理：管理场景下的面板
        - [x] 驾驶舱显示场景图标，点击后可切换场景，选择某个场景后会自动切换地图中心、缩放等级、显示的图层、面板等
- [ ] 工作流
    - [x] 工作流引擎：采用成熟开源的低代码工作流引擎；
    - [ ] 工作流定义管理：流程列表、流程详情
    - [ ] 流程设计器：用Canvas展示流程图，支持拖拽节点、连接线、删除节点等操作
    - [ ] 工作流实例管理：实例列表、实例详情、审批操作
    - [ ] 结合业务嵌入工作流：业务属于哪个工作流、当前节点、后继节点、数据表单及当前审批选项、自动流转及提醒等；
- [ ] 后台任务(App.Scheduler)
    - [x] 定时任务引擎：采用成熟开源的 Quartz 等定时任务框架；
    - [ ] 定时任务管理：根据需求设置定时任务，如单据定时自动流转、统计数据、发送邮件等
    - [ ] 任务执行：根据任务配置，定时执行任务，可查看进度、日志等
- [ ] AI 能力
    - [x] AI 简单对答
    - [ ] AI 上传图片分析
    - [ ] AI 访问知识库，进行文档检索问答
    - [ ] AI 使用 MCP 访问业务数据，进行问答（需要解决授权问题）
- [ ] 部署
    - [x] 本地部署
    - [x] 内网穿透测试
    - [ ] 服务器配置
    - [ ] Docker 部署
- [ ] 安全加固
    - [x] 禁止弱密码
    - [x] 密码加密存储
    - [x] 防机器人登录、二次安全防护（如手机短信）
    - [x] 敏感信息脱敏展示
    - [x] 审计日志：记录所有用户操作、系统事件等
    - [ ] 网络安全防护：防火墙、VPN、DDoS防护等
    - [ ] 数据库自动异地备份
- [ ] 性能优化
    - [ ] 更换 PostgreSql 数据库，支持高并发；
    - [ ] 数据库量大了后采用分区表、物化视图；
    - [ ] 数据接口层尽可能使用缓存，减少数据库查询次数。后台管理可以不用缓存。
    - [ ] 多Web服务器部署+负载均衡，避免单点故障。
    - [ ] 统计宽表及定时统计逻辑，避免直接查询数据库。
    - [ ] 统计类查询，采用异步任务方式，成功后再提供下载；
- [ ] 文档
    - [ ] 建设方案
        - [ ] 系统架构设计文档：包括前端、后端、数据库、缓存等组件的设计和架构
        - [ ] 功能文档：包括系统的所有功能模块、操作流程等
        - [ ] 系统接口文档：包括系统提供的所有接口、参数说明、返回值说明等
        - [ ] 系统数据库设计文档：包括数据库表结构、索引、约束等
        - [ ] 时间计划：包括系统建设的时间计划、阶段、任务分配等
    - [ ] 用户手册
        - [ ] 用户登录注册：包括用户账号、密码、登录流程等
        - [ ] 用户操作：包括用户在系统中的日常操作，如查询、新增、修改、删除等
    - [ ] 管理员手册
        - [ ] 系统管理员文档：
            - [ ] 系统部署：包括本地部署、服务器配置、Docker部署、数据库等
            - [ ] 系统配置：组织、权限、角色、用户、菜单、日志等；
        - [ ] 组织管理员文档：包括本组织的账号、角色、数据访问权限、日志审计等；
    - [ ] 二次开发者手册
        - [ ] 系统代码结构文档：包括系统的代码结构、模块划分、主要类和方法说明等
        - [ ] 系统数据库设计文档：包括数据库表结构、索引、约束等
        - [ ] 系统接口文档：包括系统提供的所有接口、参数说明、返回值说明等
        - [ ] 扩展开发文档：包括如何在系统基础上进行扩展开发，如新增功能模块、修改系统配置等；
        - [ ] 系统组件使用文档：包括系统中使用的组件、插件、库等的使用方法、配置参数等


- [ ] 扩展
  - [ ] 接入资规划局遥感地图数据
  - [ ] 接入其它图层，如台风、气象数据等
  
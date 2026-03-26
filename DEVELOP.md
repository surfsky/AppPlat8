# 开发指南

此文档描述了基于 AppPlat8 进行低代码扩展开发的步骤。


## 先了解项目结构


核心项目

1. **App** ：使用 Razor Pages 和 HttpApi 的主 Web 应用程序项目。
    - Pages: 按功能区域组织的 Razor 页面（Admins、OA、GIS、Maintains）。
    - Components: UI 组件和 Tag Helpers（EleForm、EleTable 等）。
    - Apis: API 控制器和端点。
    - wwwroot: 静态资源（CSS、JS、图片）。
2. **App.BLL** ：业务逻辑层，数据实体、业务逻辑和数据访问方法。
    - DAL: 定义各模块（OA、GIS、业务、管理）的实体和数据访问逻辑。
    - Entities: 基础实体类、数据库辅助类（EF Core、Linq）和接口。
    - Components: 业务组件，如短信发送、密码工具等。


辅助类库项目

1. **App.API**：HTTP API 服务框架。
    - HttpApi: 核心 API 处理逻辑，包括解析、URL 处理和响应格式化组件。
    - HttpApi.Test: API 层的单元测试。
2. **App.Utils** 通用工具类和辅助类集合。
    - Base: 集合、转换、日期/时间、字符串等通用辅助类。
    - IO: 文件和资源处理。
    - Net: 网络工具（HTTP、Socket）。
    - Security: 加密和密码辅助类。
    - Serialization: JSON 和 XML 处理。
    - Drawing: 图像处理和验证码生成。
3. **App.Web**： ASP.NET 服务类库。
    - Services: 服务端服务。
    - Helpers: Cookie、认证和脚本辅助类。
    - Base Classes: 页面和控制器的基类。


其他目录

1. **Design目录**： UI设计稿（基于Pencil 插件实现），可用AI MCP自动转为网页代码；
2. **Codes 目录**： 一些试验性代码、临时代码
3. **Doc 目录**： 一些说明文档


## 开发步骤

基础步骤

- 实现实体:
  1. 在 `App.BLL/DAL/DAL` 文件夹中添加实体类，继承 `EntityBase<T>`，树结构实体类可继承自 `TreeEntityBase<T>`。
  2. 在 `App.BLL/DAL/AppPlatContext.cs` 文件中添加实体集。
  3. 运行 `dotnet ef migrations add AddGPS --project App/App.csproj --startup-project App/App.csproj` 创建数据库迁移。
- 添加权限: 在 `App.BLL/Base/Power` 文件中添加权限常量。
- 添加页面: 在 `App/Pages` 文件夹中添加, 并应用权限控制。可参照 /pages/admins/users 和 userform 页面。
- 配置菜单: 运行项目后，在 `Pages/Maintains/Menus` 页面，把页面添加到菜单中，并设置访问权限。


备注

- 实体模型扩展
    - 树结构实体类：可继承自 `TreeEntityBase<T>`，可用于表示树状结构，如菜单、分类等。可参考 Menu 实体类，可用扩展方法ToTree转换为树状结构。
    - 多对多关系：可参考 User.Roles 及 Role.User 关联属性，及 pages/admins/UserForm 页面。
    - 如果只是“存一下 IDs 方便回显”，可定义实体属性 List<long> XXXIds 的方式保存Ids数据， 见AppPlatContext.MapListIds()方法。如果有“按角色筛用户”的常规业务查询，还是用 n:n 关联表最稳。
    - 可实现相关常用方法，如Export、Search、Fix、Init等方法；
    - 数据脱敏：实现实体类的 Export() 方法，在导出数据时对敏感字段进行处理，如：this.Mobile = this.Mobile.Mask();
- 实现 API: 在 `App/Apis` 文件夹中添加 API 服务。
- 定制 Controller 服务：在 `App/Controllers` 文件夹中添加 Controller 服务。
- 页面中控件的只读控制， 可在页面模型中提供 XXXReadonly之类的属性， 参考 /admins/UserForm 页面。
- 权限管控
    - 基于 User-Role-Power 模型的权限管控。
    - 其中 Power 是权限常量，在 `App.BLL/Base/Power` 文件中添加权限常量。
    - 所有操作授权都只基于用户拥有的 Power 来确定，可调用 User.HasPower(Power.XXXView) 来判断用户是否有访问权限。
    - 页面模型中可设置访问权限：CheckPower(Power.XXXView)，参考 /admins/Users 页面。
    - 控件、按钮等可设置可见性、可操作权限：VPower, UPower, APower 等，参考 /admins/Users 页面。
    - 数据访问权限：用户表有一个授权组织ID（User.AuthOrgId）, 其他数据表若有 OrgId 的参数，根据用户AuthOrgId来进行过滤。对应的组织树可用
        <EleTreeSelect For="Item.DutyOrgId" Label="责任组织" Api="/httpapi/orgs/GetAuthOrgTree" ></EleTreeSelect>
        该API在 /app/apis/orgs/GetAuthOrgTree 中实现。


## 菜单

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
    驾驶舱    GIS/GisIndex                  GisIndexView
    区域      GIS/Regions                   RegionView
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




## 进度规划

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
- [ ] 知识库
    - [ ] 文档: 清单、详情、附件（尚未实现）、评论（尚未实现）
    - [x] 目录: 清单、详情
- [ ] 平台框架
    - [x] 图标
    - [x] API
    - [ ] EleUI控件
      - [x] Form
      - [x] Table
      - [x] Control
      - [x] IconSelect
      - [x] UserSelect
      - [x] TreeSelect
      - [ ] EleManager
      - [ ] Panel
      - [ ] CollapsPanel
      - [ ] SplitPanal
      - [ ] 复杂页面
- [ ] 欢迎页：
    - [x] 基础页面
    - [ ] 任务: 个人、部门、单位
    - [ ] 消息: 公告
- [ ] 排查
    - [x] 对象: 清单、详情、检索（尚未完整实现）
        - [x] 清单详情
        - [ ] 联系人子表
        - [ ] 附件子表
        - [ ] 数据导入
    - [ ] 检查表：
        - [x] 标签管理
        - [x] 清单、详情
        - [x] 检查项
        - [ ] 标签数据导入
        - [ ] 检查表数据导入
    - [ ] 排查
        - [x] 排查：清单、详情
        - [x] 隐患：清单、详情
        - [x] 任务：清单、详情
        - [ ] 检查表：检查表、标签、检查项
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
    - [ ] 驾驶舱: 
      - [x] 地图：地图图层、缩放平移、三维等
      - [ ] 面板
      - [ ] 图层树
      - [ ] 图层
      - [ ] 点位
      - [ ] 点位详情
    - [x] 区域：清单、详情、在地图上定位（尚未实现）
    - [ ] 点位：清单、详情、在地图上定位（尚未实现）
    - [ ] 面板：清单、详情、数据内容、展示方式
- [ ] 工作流
    - [x] 工作流引擎(App.LiteFlow)：或基于 Activiti 的低代码工作流管理系统
    - [ ] 工作流定义：流程设计器、流程列表、流程详情
    - [ ] 工作流实例：实例列表、实例详情、审批操作
    - [ ] 工作流组件：流程图、审批表单等
    - [ ] 后台任务(App.Scheduler)
- [ ] 部署
    - [x] 本地部署
    - [x] 内网穿透测试
    - [ ] 更换 PostgreSql
    - [ ] 服务器配置
    - [ ] Docker 部署
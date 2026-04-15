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
  3. 运行 `dotnet ef migrations add CheckSheetItem --project App/App.csproj --startup-project App/App.csproj` 创建数据库迁移。
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
- Tailwind 色彩在 `/wwwroot/tailwindConfig.js` 中配置。







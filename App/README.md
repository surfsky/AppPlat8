# App 项目

该项目为 AppPlat 项目的主项目，用于展示管理后台网站。

## 目录

- Pages: 网站页面；
- Components: 服务端组件；
- Apis: API 服务；
- Controller: 控制器，简单路由服务；
- wwwroot: 静态资源（CSS、JS、图片）；
- Properties: 项目属性，如应用程序配置文件、启动配置文件；
- WebTest: 自动化网页测试脚本（ nodejs + playwright ）；

- sqlite.db: 数据库文件，默认存储在项目根目录下。需定期备份
- Files: 项目文档存储目录，需定期备份
- Caches：缓存目录，用于存储临时文件，可定期删除；
- Logs：日志目录，用于存储应用程序运行时的日志文件，可定期删除；


## 菜单

以最新版本的菜单为准。

- 排查
    - 对象      ~/Checks/CheckObjects
    - 排查      ~/Checks/CheckLogs
    - 检查表    ~/Checks/CheckSheets
    - 隐患      ~/Checks/CheckHarzards
    - 任务      ~/Checks/CheckTasks
    - 报表      ~/Checks/CheckReports
- OA
    - 资产      ~/OA/Assets
    - 文档      ~/OA/Articles
    - 预算      ~/OA/Budgets
    - 项目      ~/OA/Projects
    - 公告      ~/OA/Annouces
    - 交办      ~/OA/Tasks
    - 事件      ~/OA/Events
    - 公司      ~/OA/Company
- 驾驶舱
    - 驾驶舱    ~/GIS/GisIndex
    - 区域      ~/GIS/Regions
- 账户
    - 组织      ~/Admins/Orgs
    - 用户      ~/Admins/Users
    - 权限      ~/Admins/Roles
- 运维
    - 菜单      ~/Maintains/Menus
    - 在线      ~/Maintains/Onlines
    - 配置      ~/Maintains/Config
    - 日志      ~/Maintains/Logs
- 开发
    - 图标     ~/Dev/Icons
    - API      ~Dev/API
-修改密码     ~/Admins/ChangePassword
-安全退出     ~/Logout

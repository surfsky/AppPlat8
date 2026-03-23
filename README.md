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
    # 编译
    cd AppPlat
    dotnet build

    # 运行项目
    dotnet run --project App

    # 或直接运行bin目录下的 dll 文件
    dotnet app.dll --urls=http://localhost:6060;http://abc.org

    # 关闭
    Ctrl+C
   ```
4.打开浏览器，访问 `http://localhost:6060` 或 `http://abc.org`。
5.若端口被占用，查找占用 6060 的进程，然后kill
  ```bash
    lsof -iTCP:6060 -sTCP:LISTEN
    kill -9 <pid>
  ```
6. 测试项目
  ```bash
  dotnet test App.UtilsTests/App.UtilsTests.csproj
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





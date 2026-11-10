# EleUI Samples

本目录是 EleUI 在 Razor Pages 下的示例集合，覆盖布局、控件、弹窗、数据页面、管理器能力等常见场景。

## 快速运行

``` bash
dotnet build App.EleUI/EleUIDemo/EleUIDemo.csproj
dotnet run --project App.EleUI/EleUIDemo/EleUIDemo.csproj

# 访问 http://localhost:6002
# 端口号在 Program.cs 中配置
```

## 访问入口

- 总入口页: /EleUIDemo
- 索引文件: App/Pages/EleUIDemo/Index.cshtml

## 目录结构

- Apps: App 壳与 iframe 页切换示例
- Containers: 容器类组件示例（Card、SplitPanel、Container）
- Controls: 表单与交互控件示例
- Datas: 表格、列表、表单及联动数据页示例
- Layouts: Row / Column / Grid 布局示例
- Popups: Drawer / Dialog / ServerDrawer 等弹层示例
- 根页面: Manager、ManagerServer、Theme、Index

## 页面清单

### 根页面

- /EleUIDemo/Manager: 客户端管理器调用示例
- /EleUIDemo/ManagerServer: 服务端 Handler + 管理器命令示例
- /EleUIDemo/Theme: 主题切换示例

### Layouts

- /EleUIDemo/Layouts/Row
- /EleUIDemo/Layouts/Column
- /EleUIDemo/Layouts/Grid

### Containers

- /EleUIDemo/Containers/Card
- /EleUIDemo/Containers/Container
- /EleUIDemo/Containers/SplitPanel

### Controls

- /EleUIDemo/Controls/Button
- /EleUIDemo/Controls/Form
- /EleUIDemo/Controls/TreePicker: EleForm / EleTable 默认值名称回显与选中路径展开
- /EleUIDemo/Controls/Icons
- /EleUIDemo/Controls/ImageViewer
- /EleUIDemo/Controls/CitySelect: OnChange + SetControl 联动示例

### Popups

- /EleUIDemo/Popups/Drawer
- /EleUIDemo/Popups/OpenDrawer
- /EleUIDemo/Popups/ServerDrawer
- /EleUIDemo/Popups/Dialog

### Apps

- /EleUIDemo/Apps/Index
- /EleUIDemo/Apps/Home
- /EleUIDemo/Apps/List
- /EleUIDemo/Apps/About
- /EleUIDemo/Apps/Form

### Datas

- /EleUIDemo/Datas/Users: EleTable + 查询 + 增删改
- /EleUIDemo/Datas/Timer: EleTimer 定时刷新 EleTable 数据
- /EleUIDemo/Datas/UserList: EleList 列表示例
- /EleUIDemo/Datas/UserForm: 表单示例
- /EleUIDemo/Datas/RoleUsers: SplitPanel 左右联动（左角色，右用户）
- /EleUIDemo/Datas/RoleUsersUsers: RoleUsers 右侧用户表格子页

## EleForm BuildMode 说明

```html
<EleForm BuildMode="None">      不生成 Vue app mount
<EleForm BuildMode="Client">    生成 Vue app mount，客户端渲染，可用 OnGetData 初始化数据
<EleForm BuildMode="Server" DataFor="..."> 生成 Vue app mount，服务端渲染
```

## 维护建议

- 新增示例页面后，同步更新本文件与 Index.cshtml。
- 页面路径建议与目录、文件名保持一致，便于检索。
- 联动示例优先放在 Controls 或 Datas 下，避免重复分散。

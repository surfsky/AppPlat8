# EleUI Samples

本目录是 EleUI 在 Razor Pages 下的示例集合，覆盖布局、控件、弹窗、数据页面、管理器能力等常见场景。

## 访问入口

- 总入口页: /EleUISamples
- 索引文件: App/Pages/EleUISamples/Index.cshtml

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

- /EleUISamples/Manager: 客户端管理器调用示例
- /EleUISamples/ManagerServer: 服务端 Handler + 管理器命令示例
- /EleUISamples/Theme: 主题切换示例

### Layouts

- /EleUISamples/Layouts/Row
- /EleUISamples/Layouts/Column
- /EleUISamples/Layouts/Grid

### Containers

- /EleUISamples/Containers/Card
- /EleUISamples/Containers/Container
- /EleUISamples/Containers/SplitPanel

### Controls

- /EleUISamples/Controls/Button
- /EleUISamples/Controls/Form
- /EleUISamples/Controls/Icons
- /EleUISamples/Controls/ImageViewer
- /EleUISamples/Controls/CitySelect: OnChange + SetControl 联动示例

### Popups

- /EleUISamples/Popups/Drawer
- /EleUISamples/Popups/OpenDrawer
- /EleUISamples/Popups/ServerDrawer
- /EleUISamples/Popups/Dialog

### Apps

- /EleUISamples/Apps/Index
- /EleUISamples/Apps/Home
- /EleUISamples/Apps/List
- /EleUISamples/Apps/About
- /EleUISamples/Apps/Form

### Datas

- /EleUISamples/Datas/Users: EleTable + 查询 + 增删改
- /EleUISamples/Datas/UserList: EleList 列表示例
- /EleUISamples/Datas/UserForm: 表单示例
- /EleUISamples/Datas/RoleUsers: SplitPanel 左右联动（左角色，右用户）
- /EleUISamples/Datas/RoleUsersUsers: RoleUsers 右侧用户表格子页

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

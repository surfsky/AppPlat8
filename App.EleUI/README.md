# App.EleUI 组件说明

`App.EleUI` 是一套基于 Razor  + Vue3 + Element Plus 的页面UI框架，目标是：

- 在 Razor 页面里用声明式标签，强类型绑定，快速搭建表单、表格、布局与弹窗等。
- 服务器端事件处理，支持异步更新页面状态。

## 参考

- Element Plus: https://element-plus.org/zh-CN
- Vue 3: https://cn.vuejs.org/


## 控件使用示例

``` html
<script src="~/eleui/eleui.js"></script>
<EleButton Handler="OnClick">默认按钮</EleButton>
```        

``` cs
public void OnClick([FromForm] req)
{
    // 处理点击事件
}
```


## 运行

```bash
# 编译整解决方案
dotnet build AppPlat.sln

# 编译 App.EleUI 控件库
#`App.EleUI/EleUI/App.EleUI.csproj` 在 `BuildEleUiAssets` 目标里会自动执行 `npm install` 与 `npm run build`。
# 前端打包产物输出到：`App.EleUI/EleUI/wwwroot/eleui/eleui.js`。
dotnet build App.EleUI/EleUI/App.EleUI.csproj

# 运行 EleUI 示例站：
dotnet run --project App.EleUI/EleUISamples/EleUISamples.csproj


# 若端口被占用，查找占用 6070 的进程，然后kill
lsof -iTCP:6070 -sTCP:LISTEN
kill -9 <pid>
```


## 发布 NuGet

```bash
# 打包
dotnet pack App.EleUI/EleUI/App.EleUI.csproj -c Release -o ./nupkgs`

# 推送到 NuGet.org
dotnet nuget push ./nupkgs/App.EleUI.*.nupkg --source https://api.nuget.org/v3/index.json --api-key <YOUR_API_KEY> --skip-duplicate
```

## 目录结构

- `App.EleUI/EleUI`：控件库项目根目录
  - `Columns/`: 表格列扩展（操作列、图标列、序号列、图片列等）
  - `Containers/`: 容器类组件（`EleContainer`、`EleCard`、`EleSplitPanel`）
  - `Controls/`: 表单与基础控件（`EleInput`、`EleSelect`、`EleLabel` 等）
  - `Layouts/`: 纯布局组件（`Row`/`Column`/`Grid`）
  - `Popups/`: 弹层组件（`EleDialog`、`EleDrawer`、`ElePopover`）
  - `EleUIJs/`: 前端运行时与构建器（`EleForm`、`EleTable`、`EleManager`）
- `App.EleUI/EleUISamples`：示例网站项目。


## 关键基类职责

- `EleControl`: 所有可视控件基础能力
  - 公共属性：`Width`、`Height`、`Radius`、`Border`、`BorderColor`、`Rounded`、`Shadow`、`Enabled`
  - 通用能力：权限检查、`v-model`/`:disabled` 注入、基础 style 组装
- `EleFormControl`: 表单字段基础能力
  - `For`、`Label`、`FillRow`、`Required`、自动从模型推导标签和字段绑定
- `EleItem`: 面向布局容器的 Tailwind 工具类基础
  - `W/H/MinW/MaxW/MinH/MaxH`、`P/Px/Py`、`M/Mx/My`、`Bg`、`Overflow`

## 控件清单

- 核心
  - `EleApp`
  - `EleForm`
  - `EleTable`
  - `EleManager`
- Controls
  - `EleButton`
  - `EleInput`
  - `EleNumber`
  - `EleDatePicker`
  - `EleSelect`
  - `EleTreeSelect`
  - `EleRadio`
  - `EleSwitch`
  - `EleHidden`
  - `EleLabel`
  - `EleIcon`
  - `EleImageUpload`
  - `ElePicker`
  - `EleIconPicker`
- Containers
  - `EleContainer`
  - `EleCard`
  - `EleSplitPanel`
- Layouts
  - `Row`
  - `Column`
  - `Grid`
- Popups
  - `EleDialog`
  - `EleDrawer`
  - `ElePopover`
  - `ElePopconfirm`
  - `EleTooltip`
- Columns
  - `EleColumn`
  - `EleColumns`
  - `EleOpColumn`
  - `EleNumColumn`
  - `EleIconColumn`
  - `EleImageColumn`

## 前端运行时文件（EleUIJs）

- `EleUI.js`: 总入口（导出并初始化运行时）
- `EleAppBuilder.js`: Vue 应用通用挂载器
- `EleFormAppBuilder.js`: 表单页挂载器
- `EleTableAppBuilder.js`: 表格页挂载器
- `EleForm.js`: 表单行为（加载、保存、选择器弹窗、上传等）
- `EleTable.js`: 表格行为（查询、分页、打开编辑抽屉等）
- `EleManager.js`: 全局交互（消息、确认框、抽屉、统一关闭协议）
- `DrawerHelper.js`: 抽屉生命周期与跨 iframe 关闭协议处理
- `Utils.js`: 通用工具
- `EleFixes.css`: 一些运行时样式修正


## 类继承关系图

```mermaid
classDiagram
TagHelper <|-- EleControl
EleControl <|-- EleFormControl
EleControl <|-- EleItem

EleControl <|-- EleButton
EleControl <|-- EleTable
EleControl <|-- EleIcon
EleControl <|-- EleHidden
EleControl <|-- EleCard
EleControl <|-- EleSplitPanel
EleControl <|-- EleSplitPanelItem
EleControl <|-- EleDialog
EleControl <|-- EleDrawer
EleControl <|-- ElePopover
EleControl <|-- ElePopconfirm
EleControl <|-- EleTooltip

EleFormControl <|-- EleInput
EleFormControl <|-- EleNumber
EleFormControl <|-- EleDatePicker
EleFormControl <|-- EleSelect
EleFormControl <|-- EleTreeSelect
EleFormControl <|-- EleRadio
EleFormControl <|-- EleSwitch
EleFormControl <|-- EleLabel
EleFormControl <|-- ElePicker
EleFormControl <|-- EleImageUpload
EleFormControl <|-- EleIconPicker

EleItem <|-- EleContainer
EleItem <|-- EleLayoutRow
EleItem <|-- EleLayoutColumn
EleItem <|-- EleLayoutGrid

 <|-- EleForm
 <|-- EleApp
 <|-- Toolbar
 <|-- EleColumns
 <|-- EleColumn
 <|-- EleOpColumn
 <|-- EleNumColumn
 <|-- EleIconColumn
 <|-- EleImageColumn
 <|-- EleToolbar
 <|-- EleCardHeader
 <|-- EleDialogHeader
 <|-- EleDialogContent
 <|-- EleDialogFooter
 <|-- EleDrawerContent
 <|-- EleDrawerFooter
```

## 命名与约定

- 带弹窗选择语义的控件统一使用 `XXXPicker` 后缀。如：`ElePicker`、`EleIconPicker`
- 抽屉/iframe 关闭统一走 `EleManager.closePage(...)` 协议。


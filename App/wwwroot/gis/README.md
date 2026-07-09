# GIS 前端脚本说明

本文档用于说明 `App/wwwroot/gis` 目录下各个 `JS` 文件的职责，便于后续维护、排查依赖关系与做模块复用。

## 目录概览

- `gis-index-*.js`：`/GIS/Index` 首页相关脚本，按启动、数据、UI、场景、面板等职责拆分。
- `gis-*.js`：GIS 通用能力脚本，如地址搜索、点位 marker、图表辅助、面板元素等。
- `maphelper.js`：图形编辑器与地图绘制的底层能力。
- `overlay/core/*.js`：叠加图层基础框架。
- `overlay/layers/*.js`：各类专题叠加图层实现。

## 根目录脚本

### 通用脚本

- `gis-address-search.js`
  - 统一封装地址与经纬度搜索逻辑。
  - 负责输入标准化、经纬度解析、调用 `/HttpApi/Gis/GetAddrs` 与 `/HttpApi/Gis/GetAddr`。
  - 被 `/GIS/Index`、`/GIS/Locator` 等页面复用。

- `gis-chart-helper.js`
  - 负责统计面板图表配置解析与 ECharts 渲染。
  - 支持按数据源加载图表数据并生成图表 `option`。

- `gis-panel-element.js`
  - 定义可复用的 `gis-panel` 自定义元素。
  - 统一封装 GIS 面板的标题栏、关闭按钮、提示信息等基础结构。

- `gis-point-marker.js`
  - 统一创建点位 marker。
  - 负责图标、标签、缩放、标签色彩、换行、点击态、选中态等点位展示细节。
  - 目前被 `gis/index` 与 `GIS/GeometryMap` 复用。

- `gis-public-cameras.js`
  - 提供公开摄像头/视频测试数据。
  - 主要用于构造视频类点位或调试预览。

- `maphelper.js`
  - GIS 图形编辑器底层核心。
  - 负责图形编辑、点线面样式、矩形/圆形绘制、撤销重做、属性同步、参考点显示等。
  - 被 `GIS/GeometryEditor` 等页面使用。

### GIS 首页脚本

- `gis-index-boot.js`
  - `/GIS/Index` 启动入口。
  - 串联地图初始化、页面事件绑定、场景恢复、首次数据加载等流程。

- `gis-index-map.js`
  - 封装地图级公共能力。
  - 包括中文标签替换、中心坐标显示、几何辅助加载、重置视图等。

- `gis-index-ui.js`
  - 管理 GIS 首页界面状态。
  - 控制顶部时间、工具栏、图层面板、场景菜单、视图菜单等显示与联动。

- `gis-index-view.js`
  - 负责地图视图能力。
  - 包括底图切换、投影切换、3D 地形、旋转视图等。

- `gis-index-scene.js`
  - 负责场景切换逻辑。
  - 场景切换时同步底图、视角、地形、面板和菜单状态。

- `gis-index-sites.js`
  - 负责“参考网站”抽屉。
  - 实现站点数据加载、分组展示、样式控制等。

- `gis-index-address.js`
  - GIS 首页搜索框交互层。
  - 负责地址联想列表显示、点击候选项定位、搜索红点 marker 等。
  - 底层搜索逻辑复用 `gis-address-search.js`。

- `gis-index-action.js`
  - 负责首页上的交互动作。
  - 如打开点位详情、附件预览、视频播放、统计模式切换等。

- `gis-index-overlays.js`
  - 负责叠加图层系统初始化。
  - 将雷达、卫星云图、风场、台风等 overlay 挂到 GIS 首页。

- `gis-index-panels.js`
  - 负责统计面板渲染。
  - 包括左右列布局、数据加载、图表面板生成。

- `gis-index-detail.js`
  - 负责点位详情面板。
  - 生成详情表格、扩展属性行、附件展示和相关操作入口。

- `gis-index-point-list.js`
  - 负责点位清单面板。
  - 提供分页、查询、筛选、刷新、定位、详情联动等能力。

### GIS 首页数据层脚本

- `gis-index-data.js`
  - GIS 首页数据层总入口。
  - 对外统一暴露菜单、图形、点位等数据相关 API。

- `gis-index-data-menu.js`
  - 负责左侧图层菜单树。
  - 包括菜单构建、勾选联动、缩放级别可见性控制等。

- `gis-index-data-geometry.js`
  - 负责图形与点位数据渲染。
  - 包括几何数据加载、点位 marker 重建、shape 图层排序、显隐同步与地图刷新。

- `gis-index-data-utils.js`
  - 数据层通用工具函数。
  - 提供 `Gps`/`GeoJson` 解析、中心点计算、图标路径解析、图片地址规范化等。

- `gis-index-object.js`
  - 负责检查对象或对象型点位数据加载。
  - 实现基础对象 marker 构建、显隐控制与地图联动。

## overlay 核心框架

### `overlay/core`

- `overlay/core/LayerManager.js`
  - overlay 图层管理器。
  - 统一管理图层注册、启停、刷新、状态信息与 UI 绑定。

- `overlay/core/MapLayer.js`
  - overlay 图层基类。
  - 定义图层通用接口与时间、显隐、信息文案等公共逻辑。

- `overlay/core/utils.js`
  - overlay 通用工具。
  - 包含超时请求、GeoJSON source 更新、数组分块、状态信息输出等辅助方法。

## overlay 专题图层

### `overlay/layers`

- `overlay/layers/AdminBoundaryLayer.js`
  - 行政边界图层。
  - 根据地图中心与缩放级别动态加载国、省、市、区县边界。

- `overlay/layers/CityHumidityLayer.js`
  - 城市湿度图层。
  - 以文字标签方式展示城市湿度数据。

- `overlay/layers/CityTempLayer.js`
  - 城市温度图层。
  - 以彩色标签方式展示城市温度数据。

- `overlay/layers/CityWeatherLayer.js`
  - 城市天气图层。
  - 用自定义 marker 展示天气图标、温度，并支持点击查看多日天气。

- `overlay/layers/LatLonGridLayer.js`
  - 经纬网图层。
  - 根据当前缩放级别动态绘制经纬网格线及边缘坐标注记。

- `overlay/layers/PressureLayer.js`
  - 气压图层。
  - 负责气压场采样、等压线生成与标签显示。

- `overlay/layers/RadarLayer.js`
  - 降水雷达图层。
  - 从 RainViewer 获取雷达瓦片并叠加显示。

- `overlay/layers/SatelliteFallbackLayer.js`
  - 卫星云图兜底图层。
  - 使用 NASA 等来源的瓦片作为卫星图备用数据源。

- `overlay/layers/SatelliteLiveLayer.js`
  - 近实时卫星云图图层。
  - 从 RainViewer 获取红外云图瓦片并叠加显示。

- `overlay/layers/SatelliteWorldMosaicLayer.js`
  - 全球卫星拼图图层。
  - 通过 WMS 服务加载全球红外拼图。

- `overlay/layers/TidePanelLayer.js`
  - 潮位专题图层。
  - 负责潮位站点、潮汐曲线、海况信息及站点切换。

- `overlay/layers/TyphoonLayer.js`
  - 台风专题图层。
  - 负责台风列表、路径、风圈、预报线、图例和弹窗等完整专题表现。

- `overlay/layers/WindLayer.js`
  - 风场图层。
  - 通过 Canvas 粒子动画展示风速风向。

## 维护建议

- 新增 `/GIS/Index` 脚本时，优先按职责拆到现有模块，而不是继续堆进 `gis-index-boot.js`。
- 点位渲染逻辑尽量复用 `gis-point-marker.js`，避免不同页面重复维护 marker 样式。
- 地址/经纬度搜索逻辑尽量统一走 `gis-address-search.js`，不要在页面脚本里各自重复解析。
- 若新增 overlay 专题图层，优先继承 `overlay/core/MapLayer.js` 并交由 `LayerManager.js` 统一管理。

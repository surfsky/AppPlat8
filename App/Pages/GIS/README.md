# GIS 地图管理和展示

## 字段变更说明

### 2026.5 字段调整

- GisGeometry 表主文件字段由 `Att` 改为 `File`，所有前端、接口、JS 需同步兼容。
- 前端所有图片/文件展示、上传、下载等逻辑，需优先读取 `file` 字段，兼容历史 `att` 字段。
- 主要受影响文件：
	- `/App/wwwroot/gis/gis-index-data.js`（地图图片展示、文件点位）
	- `/App/Pages/GIS/GeometryMap.cshtml(.cs)`、`Index.cshtml(.cs)` 等接口输出

### 兼容处理建议

- JS 端统一用 `item.file || item.File || item.att || item.Att` 取值，优先 file。
- 兼容历史数据，后端接口输出建议 file/att 均保留一段时间。

---

## 文件

| 文件      |  功能           |
|-----------|----------------|
| ApiData   | Api 接口数据清单 |
| ApiForm   | Api 接口表单     |

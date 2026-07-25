# ChartEditor 配置说明

本文档用于约定 `GisPanel.ChartJson` 的结构。

## 1. 顶层结构

```json
{
  "version": 1,
  "type": "line",
  "title": "示例图表",
  "source": {
    "mode": "json",
    "json": {},
    "api": {
      "url": "",
      "method": "GET",
      "dataPath": "data"
    }
  },
  "options": {}
}
```

字段说明：

- `version`: 版本号，当前固定 `1`
- `type`: 图表类型，支持 `line`、`bar`、`pie`
- `title`: 图表标题
- `source.mode`: 数据源模式，`json` 或 `api`
- `source.json`: 内嵌数据（`mode=json` 时使用）
- `source.api`: 接口数据配置（`mode=api` 时使用）
- `options`: ECharts 原生配置补丁（浅合并）

## 2. JSON 数据源格式

### 2.1 折线图 / 柱形图

```json
{
  "version": 1,
  "type": "line",
  "title": "事件趋势",
  "source": {
    "mode": "json",
    "json": {
      "categories": ["周一", "周二", "周三", "周四", "周五"],
      "series": [
        { "name": "事件数", "data": [12, 18, 15, 20, 14] },
        { "name": "已处置", "data": [6, 10, 9, 12, 8] }
      ]
    }
  },
  "options": {}
}
```

说明：

- `categories`: X轴类目数组
- `series`: 系列数组
- 每个系列可写为对象 `{name,data}`，也支持直接写 `data` 数组（名称自动生成）

### 2.2 饼图

```json
{
  "version": 1,
  "type": "pie",
  "title": "风险等级分布",
  "source": {
    "mode": "json",
    "json": {
      "data": [
        { "name": "高", "value": 8 },
        { "name": "中", "value": 15 },
        { "name": "低", "value": 26 }
      ]
    }
  },
  "options": {}
}
```

## 3. API 数据源格式

```json
{
  "version": 1,
  "type": "bar",
  "title": "近7日任务",
  "source": {
    "mode": "api",
    "api": {
      "url": "/GIS/Index?handler=DailyStats",
      "method": "GET",
      "dataPath": "data"
    }
  },
  "options": {}
}
```

要求：

- `dataPath` 指向最终图表数据对象
- line/bar 期望目标对象包含 `categories` 和 `series`
- pie 期望目标对象包含 `data`

## 4. 主题参数

`ChartEditor` 支持 `theme` URL 参数：

- `theme=dark`: 深色预览
- `theme=light`: 浅色预览
- 默认 `dark`

示例：

- `/GIS/ChartEditor?theme=dark`
- `/GIS/ChartEditor?theme=light`

## 5. 与页面联动

- GIS 驾驶舱页面：默认使用 `dark` 渲染图表
- `Me/Dashboard` 页面：默认使用 `light` 渲染图表
- `GisPanel.Content` 与 `GisPanel.ChartJson` 可同时配置，图表显示在上方、HTML 内容显示在下方

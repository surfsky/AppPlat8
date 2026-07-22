# 技术记录

## Z-Index 统一规划（2026-07-22）

目标：统一处理弹层层级，避免抽屉、确认框、消息提示、加载层、图片预览互相遮挡。

### 1. 统一入口
- 在 App.EleUI/EleUI/EleUIJs/EleManager.js 中新增统一能力：
- _scanMaxZIndex(): 扫描当前页面常见弹层（overlay、drawer、dialog、message、notification、loading、image-viewer）的有效 z-index 最大值。
- resolvePopupZIndex(base, step): 在基础层级上取 max(base, currentMax + step)。
- resolvePopupOptions(options, baseZIndex, extras): 合并弹层参数，自动补齐 zIndex，并默认 appendTo 顶层 body。

### 2. 分层基线
- message/toast: base 6000
- notification: base 6001
- messagebox/confirm/prompt: base 6002
- loading: base 6003
- image viewer: base 6100

说明：
- 以上基线只保证“最低安全值”；真实渲染会动态提升到当前最大层级之上。
- 支持调用方传入自定义 zIndex；若传入则不覆盖。

### 3. 已接入组件
- App.EleUI/EleUI/EleUIJs/manager/messageMethods.js
- App.EleUI/EleUI/EleUIJs/manager/serverMethods.js
- App.EleUI/EleUI/EleUIJs/manager/loadingMethods.js
- App.EleUI/EleUI/EleUIJs/manager/imageViewerMethods.js

### 4. GIS 页面专项
- gis/index 场景菜单层级提升：.view-menu / #scene-menu 提升到 stats-overlay 之上，避免统计面板遮挡。
- GeometryForm 关闭确认场景此前已做页面兜底，后续可在 EleUI 产物重新打包后逐步收敛。

### 5. 后续建议
- 如果新增弹层组件（如自定义 popover、third-party modal），统一走 resolvePopupOptions。
- 避免在业务页面硬编码极大 z-index（如 999999），优先使用统一规划能力。

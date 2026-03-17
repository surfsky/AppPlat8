Tailwind CSS 常用用法速查（分类紧凑）

官网地址：

- 尺寸
  - 4像素倍数：w-4 w-6 w-40 h-4 h-10。其中 w-4 表示宽度为 4*4px=16px。其中 0.25rem（4px）是 UI 设计中最常用的 “基础栅格单位”
  - 任意值：w-[250px]、h-[44px]
  - 计算：w-full占满父元素, w-screen占满全屏, w-auto自适应内容, w-1/2父元素的1/2
  - 最小/最大：min-w-[120px]、max-w-screen-lg、min-h-[44px]
  - 响应式布局：
    在普通样式类前加上断点前缀即可（手机竖屏无前缀，手机横屏sm: 平板md: 桌面lg: xl: 2xl:）。
    样式会向下覆盖：大屏样式会覆盖小屏样式，未指定的断点继承更小屏的样式。
    ```
    // 手机竖屏单列，手机横屏双列，平板四列
    <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
      <!-- 子元素：4个卡片 -->
      <div class="bg-slate-100 p-4 rounded">卡片1</div>
      <div class="bg-slate-100 p-4 rounded">卡片2</div>
      <div class="bg-slate-100 p-4 rounded">卡片3</div>
      <div class="bg-slate-100 p-4 rounded">卡片4</div>
    </div>
    // 仅大屏显示的按钮
    <button class="hidden sm:block px-4 py-2 bg-white text-slate-900 rounded">仅大屏显示的按钮</button>
    // 文字和间距逐步增大
    <h1 class="text-2xl sm:text-3xl md:text-4xl lg:text-5xl mb-4 sm:mb-6 md:mb-8">
      响应式标题
    </h1>
    // flex flex-col md:flex-row（移动端纵向排列，平板横向排列）
    <menu class="flex flex-col md:flex-row">....</menu>
    ```

- 布局
  - flex：flex、inline-flex、flex-row、flex-col、flex-wrap、items-center、justify-between、gap-2
    - Flex 细节：flex-1、grow、shrink、basis-1/2 basis-[150px]
    - 示例：<div class="flex items-center justify-between gap-2"></div>
    - 间隔：space-x-2 space-y-1（忽略最后一个子项的底部间距）
  - grid：grid、grid-cols-2、grid-cols-3、
    - 间隔：gap-4、gap-y-4 gap-x-0
    - 间隔：space-x-2 space-y-1（忽略最后一个子项的底部间距）
  - 外边距：m-2 mt-4 mx-4
  - 内边距：p-2 py-2 px-4 pt- pb- pl- pr-

- 排版
  - 字体大小：text-xs text-sm text-base text-lg text-xl
  - 粗细：font-light font-normal font-medium font-bold
  - 行高：leading-4 leading-6 leading-[44px]
  - 省略/换行：truncate、whitespace-nowrap、break-words
  
- 位置
  translate-x-full 表示 transform: translateX(100%); 沿 X 轴向右平移自身 100% 宽度
  负号：表示方向相反。如translate-x-full 表示 transform: translateX(-100%);


- 颜色与背景
  - 颜色：text-gray-700 text-blue-700，颜色后面的数字范围为50到900（50是最浅的颜色，900是最深的颜色），这样设计基于人眼视觉感知调校，保证不同色系的同等级颜色视觉深浅一致，适配统一的设计体系。
  - 背景：bg-white bg-gray-50 bg-blue-50
  - 渐变：bg-gradient-to-b from-purple-500 via-purple-700 to-purple-900 
  - 变暗：hover:brightness-90
  - 透明度：primary/70, opacity-70

- 阴影
  阴影深度和模糊逐步增大：shadow-none, shadow-sm, shadow(等于shadow-md), shadow-lg, shadow-xl, shadow-2xl
  ```
  <!-- 无阴影 -->
  <div class="shadow-none p-4 bg-white rounded">无阴影卡片</div>
  <!-- 轻量阴影（输入框） -->
  <input class="shadow-sm w-64 p-2 border rounded" placeholder="shadow-sm 输入框">
  <!-- 标准阴影（卡片） -->
  <div class="shadow-md p-6 bg-white rounded-lg">shadow-md 标准卡片</div>
  <!-- 强烈阴影（弹窗） -->
  <div class="shadow-xl p-8 bg-white rounded-lg">shadow-xl 弹窗</div>
  <!-- hover 时阴影变大 -->
  <button class="shadow-md hover:shadow-lg p-3 bg-blue-500 text-white rounded">Hover 放大阴影</button>
  <!-- 自定义：水平偏移2px，垂直偏移4px，模糊8px，颜色rgba(0,0,0,0.1) -->
  <div class="shadow-[2px_4px_8px_rgba(0,0,0,0.1)] p-4 bg-white rounded">自定义阴影</div>
  在 tailwind.config.js 中定义自定义阴影，全局复用，咨询豆包
  ```

- 边框与圆角
  - 边框：border border-b border-r、border-gray-200
  - 圆角：rounded rounded-sm rounded-md rounded-lg
  - 分割线：divide-x divide-y divide-gray-200
  - Ring: focus:ring-2 focus:ring-primary/70 focus:border-primary
    ring 基于 box-shadow 实现的 “虚拟边框”，不占用布局空间，是输入框聚焦高亮的最佳实践

- 溢出与滚动
  - overflow-hidden overflow-auto overflow-x-auto overflow-y-auto

- 定位与层级
  - relative absolute fixed sticky
  - inset：inset-0 top-0 right-0
  - z-index：z-10 z-50

- 显示
  - block inline-block inline hidden

- 交互与过渡
  - 状态：hover:、focus:、active:、disabled:
  - 过渡：transition、duration-150、ease-in-out、transition-colors
  ```
  <button class="bg-blue-500 hover:bg-blue-600 text-white px-4 py-2 rounded transition-colors">
    原生色系 hover 变深
  </button>
  
  <div class="bg-slate-100 hover:bg-slate-200 text-slate-800 hover:text-slate-900  p-4 rounded transition-colors cursor-pointer">
    背景+文本同时 hover 变深
  </div>

  <!-- Checkbox 重绘 -->
  <input type="checkbox" class="peer hidden">
  <div class="peer-checked:bg-blue-500 w-6 h-6 border"></div>

  <!-- Input 包裹器 ->
  <div class="border p-2 peer-focus:border-blue-500">
    <input class="peer" placeholder="点我试试">
  </div>
  ```
  - 监听 peer（被监听者）、peer-checked（勾选了）、peer-focus（聚焦了）
  

- 光标 
  - cursor-pointer 

- 示例片段
  - 工具栏统一高度与行高：class="h-[44px] leading-[44px]"
  - 分组标题行：class="flex border-b bg-gray-50 text-gray-700 font-medium"
  - 操作区紧凑图标行：class="flex items-center gap-1 whitespace-nowrap"

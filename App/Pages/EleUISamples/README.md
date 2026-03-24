# EleUI 组件示例页面


## 关于Icon


EleButton 的 Icon 规则在 EleButtonTagHelper.cs
    含空格/连字符（如 fas fa-xxx）按 CSS 类渲染。如 fas fa-floppy-disk
    否则按 Element Plus 图标组件名渲染，如 Search
    例如：<EleButton Type="Primary" Command="Save" Icon="fas fa-floppy-disk">保存</EleButton>
其中
    Font Awesome 图标名（推荐你现在这种）：https://fontawesome.com/icons
    Element Plus 图标组件名（无空格写法）：https://element-plus.org/en-US/component/icon.html


EleForm
    <EleForm BuildMode="None">  不生成Vue app mount
    <EleForm BuildMode="Client">  生成Vue app mount，客户端渲染，用 onGetData 初始化数据
    <EleForm BuildMode="Server" DataFor="...">  生成Vue app mount，服务端渲染

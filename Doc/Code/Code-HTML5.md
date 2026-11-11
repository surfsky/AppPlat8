## Js

globalThis 是 JavaScript 里“统一的全局对象引用”。它的意义是：写一份代码，不用关心当前运行环境，全都能拿到“全局对象”。
在本项目中的 EleManager 也注册了在 globalThis 上：
    在 Vue 模板里优先用 $eleManager
    在普通 JS 里用 globalThis.EleManager

# Vue 3 + Razor + Element Plus 速查手册

适用场景：本项目中的 Razor Pages 页面里嵌入 Vue 3（CDN 方式），配合 Element Plus 与 axios。

## 1. 一眼速查

- 创建应用：`const app = Vue.createApp({ setup() { ... } })`
- 挂载插件：`app.use(ElementPlus, { locale: ElementPlusLocaleZhCn })`
- 挂载页面：`app.mount('#app')`
- 生命周期：`onMounted`、`onUnmounted`
- 响应式数据：`ref`、`reactive`、`computed`、`watch`
- 双向数据绑定：`v-model`
- 事件：用 `v-on:click="..."`（注意避免 `@click` 与 Razor 冲突）
- 网络请求：`axios.get(...)`、`axios.post(...)`
- 用户提示：`ElementPlus.ElMessage.success/error(...)`

## 2. 项目标准模板（Razor 页面）

- 最简单的 vue 应用框架

```html
<script src="https://unpkg.com/vue@3/dist/vue.global.js"></script>
<script src="https://unpkg.com/element-plus@2.3.1/dist/index.full.js"></script>
<div id="app">{{ msg }}</div>
<script>
const app = Vue.createApp({            // 创建应用
  setup() {                            // 应用设置
    const msg = ref('Hello Vue!');     // 响应式变量
    return { msg };                    // 返回响应式变量，供元素绑定使用
  }
});
app.use(ElementPlus);                  // 使用 ElementPlus 插件
app.mount('#app');                     // 挂载应用到 #app 元素
</script>
```

- 结合了ElementPlus 控件的页面示例

```html
<div id="app">
  <el-input v-model="keyword" placeholder="关键字" clearable></el-input>
  <el-button type="primary" v-on:click="reload">刷新</el-button>
  <el-table :data="rows" v-loading="loading" style="margin-top: 12px;">
    <el-table-column prop="id" label="ID" width="80"></el-table-column>
    <el-table-column prop="name" label="名称"></el-table-column>
  </el-table>
</div>

<script>
const { createApp, ref, onMounted } = Vue;
const app = createApp({
  setup() {
    const loading = ref(false);
    const keyword = ref('');
    const rows = ref([]);
    async function reload() {
      loading.value = true;
      try {
        const res = await axios.get('?handler=List', { params: { keyword: keyword.value } });
        rows.value = res.data?.data || [];
      } catch (err) {
        ElementPlus.ElMessage.error('加载失败');
      } finally {
        loading.value = false;
      }
    }
    onMounted(reload);
    return { loading, keyword, rows, reload };
  }
});

// 使用 ElementPlus 插件，设置为中文。引入 ElementPlus 图标组件库并注册全局组件
app.use(ElementPlus, { locale: ElementPlusLocaleZhCn });
for (const [key, component] of Object.entries(ElementPlusIconsVue)) {
  app.component(key, component);
}
app.mount('#app');
</script>
```

## 3. 响应式数据

```js
const { ref, reactive, computed, watch, watchEffect } = Vue;
const count = ref(1);                                // 创建响应式数据（基础类型）
const form = reactive({ name: '', age: 0 });         // 创建响应式数据（对象类型）
const doubleCount = computed(() => count.value * 2); // 创建响应式数据（自动计算）
// 监听 count 变化
watch(count, (newVal, oldVal) => {
  console.log('count:', oldVal, '->', newVal);
});
// 监听 form.name 变化
watch(() => form.name, (name) => {
  console.log('name changed:', name);
});
// 监听所有数据的变化
watchEffect(() => {
  console.log('doubleCount =', doubleCount.value);
});
```

- `ref`：创建响应式基础类型，读写使用 `.value`
- `reactive`：创建响应式对象数组，可直接用属性访问成员，如 `data.name = '张三'`
- `computed`：创建响应式计算数据，带缓存
- `watch`：创建响应式监听器，精确监听指定数据的变化过程
- `watchEffect`：创建响应式监听器，监听所有数据的变化结果

## 4. 模板语法

- 条件渲染：`v-if / v-else-if / v-else / v-show`，其中v-if 与 v-show 的区别是：v-if 是惰性的，只有当条件为真时才会渲染元素；v-show 是不惰性的，无论条件是否为真，都会渲染元素，只是通过 CSS 显示/隐藏。
- 列表渲染：`v-for="item in list" :key="item.id"`
- 属性绑定：`:disabled="loading"`、`:class="cls"`
- 事件绑定：`v-on:click="save"`、`v-on:change="onChange"`，可简写为 `@click="save"`、`@change="onChange"`，但会与 Razor 冲突，所以不建议使用
- 表单绑定：`v-model="form.title"`，双向绑定，即修改表单数据会自动更新到响应式数据，修改响应式数据也会自动更新到表单数据。

示例：

```html
<el-input v-model="form.title" :disabled="readOnly"></el-input>
<el-button type="primary" :loading="saving" v-on:click="save">保存</el-button>
<el-tag v-if="form.isTop">置顶</el-tag>
<div v-show="!rows.length">暂无数据</div>
```

### 4.1 插槽（slot）

- 默认插槽：`<slot />`
- 具名插槽：`<slot name="header" />`
- 使用具名插槽：`<template #header>...</template>`
- 这个东西在 Html5 custom element 中已经实现了。

子组件 MyCard.vue ：

```vue
<template>
  <div class="card">
    <div class="card-header">
      <slot name="header">默认标题</slot>
    </div>
    <div class="card-body">
      <slot>默认内容</slot>
    </div>
    <div class="card-footer">
      <slot name="footer"></slot>
    </div>
  </div>
</template>
```

使用：

```html
<MyCard>
  <template #header>
    <h3>公告详情</h3>
  </template>
  <p>这里是默认插槽内容（正文区域）。</p>
  <template #footer>
    <el-button type="primary">保存</el-button>
  </template>
</MyCard>
```

作用域插槽（子传父）示例：

```vue
<!-- 子组件 -->
<slot name="row" :row="item" :index="i"></slot>
```

```html
<!-- 父组件 -->
<MyList>
  <template #row="{ row, index }">
    <span>{{ index + 1 }} - {{ row.name }}</span>
  </template>
</MyList>
```

- `#header` 是 `v-slot:header` 的简写。
- 默认插槽可省略名字，父组件里直接写普通内容即可。
- 作用域插槽适合表格行渲染、列表项渲染等场景。

## 5. 生命周期

```js
const { onMounted, onUnmounted } = Vue;
onMounted(() => {
  // 页面挂载后初始化数据
  reload();
});
onUnmounted(() => {
  // 释放事件、计时器
  window.removeEventListener('resize', onResize);
});
```

## 6. 组件通信（SFC 场景）

 MyDialog.vue 文件：

```vue
<script setup>
// 定义组件属性（类似参数）
const props = defineProps({
  modelValue: Boolean,
  title: String
});

// 定义事件（类似消息通道）
const emit = defineEmits(['update:modelValue', 'submit']);
function close()      { emit('update:modelValue', false);}
function submit(data) { emit('submit', data);}
</script>
```

使用：
```
<MyDialog v-model="visible" v-on:submit="handleSubmit" />
```

### 6.1 component 常用知识点速查

- 定义组件：可用 `script setup`（SFC）或对象字面量（CDN）。
- 注册组件：`app.component('MyCard', MyCard)`（全局）或在父组件 `components` 中局部注册。
- 传值：`props`（父 -> 子）。
- 回传：`emit`（子 -> 父）。
- 内容分发：`slot`（默认/具名/作用域）。
- 动态组件：`<component :is="currentComp" />`。
- 缓存组件：`<keep-alive><component :is="currentComp" /></keep-alive>`。
- 异步组件：`defineAsyncComponent(() => import('./MyComp.vue'))`。

### 6.2 组件定义与注册

CDN 写法（本项目 Razor 页面常见）：

```html
<script>
const UserCard = {
  props: { user: Object },
  template: `<div class="p-2 border rounded">{{ user.name }}</div>`
};

const app = Vue.createApp({
  setup() {
    const user = Vue.ref({ name: '张三' });
    return { user };
  }
});

app.component('UserCard', UserCard); // 全局注册
app.mount('#app');
</script>
```

SFC 局部注册（非 `script setup`）：

```vue
<script>
import UserCard from './UserCard.vue';
export default {
  components: { UserCard }
};
</script>
```

### 6.3 父子组件数据流

单向数据流建议：

- 父组件把数据通过 `props` 传给子组件。
- 子组件不要直接修改 `props`，而是通过 `emit` 通知父组件更新。

CounterEditor.vue：

```vue
<script setup>
const props = defineProps({ count: Number });
const emit = defineEmits(['update:count']);

function plusOne() {
  emit('update:count', (props.count || 0) + 1);
}
</script>

<template>
  <el-button v-on:click="plusOne">+1</el-button>
</template>
```

使用：

```html
<CounterEditor :count="count" v-on:update:count="(v) => count = v" />
```

### 6.4 动态组件与缓存

```html
<el-radio-group v-model="tab">
  <el-radio-button label="list">列表</el-radio-button>
  <el-radio-button label="form">表单</el-radio-button>
</el-radio-group>

<keep-alive>
  <component :is="tab === 'list' ? 'ListPanel' : 'FormPanel'"></component>
</keep-alive>
```

- 使用 `keep-alive` 可保留组件内部状态（如表单输入、滚动位置）。
- 常用于 tab 页、主从面板切换。

### 6.5 组件命名与实践建议

- 组件名建议使用多词：`AnnouncementForm`、`UserSelector`。
- 一个组件只做一件事，避免页面逻辑和业务逻辑全部堆在一个文件。
- 公共组件优先抽离可配置项：`props` + `slots` + `emits`。
- 在 Razor 页面中使用组件事件时优先 `v-on:*` 写法，避免 `@` 冲突。


## 7. axios 请求规范（Razor Handler）

示例：

```js
// get
const res = await axios.get('?handler=List', {
  params: { pageIndex: 1, pageSize: 20, keyword: keyword.value }
});

// post
const payload = { ids: selectedIds.value };
const res = await axios.post('?handler=Delete', payload, {
  headers: { 'Content-Type': 'application/json' }
});

// 并行请求
const [a, b] = await Promise.all([
  axios.get('?handler=Meta'),
  axios.get('?handler=Permissions')
]);
```


## 8. Element Plus 高频组件

- 表格：`el-table`、`el-table-column`
- 表单：`el-form`、`el-form-item`、`el-input`、`el-select`
- 对话框：`el-dialog v-model="visible"`
- 抽屉：`el-drawer v-model="visible" size="50%"`
- 消息：`ElMessage.success/error/warning/confirm`
- 图标：`<el-icon><Edit /></el-icon>`

操作列示例：

```html
<el-table-column label="操作" width="120">
  <template #default="scope">
    <el-icon class="cursor-pointer" v-on:click="edit(scope.row)"><Edit /></el-icon>
    <el-icon class="cursor-pointer text-red-500" v-on:click="remove(scope.row)"><Delete /></el-icon>
  </template>
</el-table-column>
```

## 9. Razor + Vue 易错点

- 使用模板字符串或 URL 时，注意 Razor 的 `@` 符号冲突。
- 不要直接写 `@click`：在 `.cshtml` 中会被 Razor 解析。若必须使用简写，可写 `@@click="..."`，推荐写法：`v-on:click="..."`。
- 在 `setup()` 内定义的方法、变量必须 `return` 出去，模板才能访问。
- `v-for` 必须给稳定 `:key`，避免表格/表单状态错乱。

## 10. 页面开发常用模式

列表页通用结构：

- 状态：`loading`、`rows`、`search`、`pageIndex`、`pageSize`、`total`
- 方法：`reload`、`onSearchClear`、`handlePageSizeChange`
- 操作：`openForm`、`openView`、`deleteSingleItem`、`deleteSelectedItems`
- 生命周期：`onMounted(reload)`

弹窗/抽屉通用结构：

- 父页：控制 `visible`
- 子页：通过 `props` 接收 `id/readOnly`
- 保存后：子页 `emit('saved')`，父页关闭并刷新列表

## 11. 排错清单

- 页面空白：先看浏览器控制台是否 `Vue/ElementPlus/axios` 未加载。
- 按钮无效：检查是否用了 `@click`（Razor 冲突）而非 `v-on:click`。
- 数据不更新：检查 `ref` 是否漏了 `.value`。
- 请求 400：检查后端处理器签名、参数名、请求头与防伪校验。
- 对话框不弹：检查 `v-model` 绑定变量是否是响应式并已返回。
- 图标不显示：检查是否循环注册 `ElementPlusIconsVue`。

## 12. 常用片段

成功提示：

```js
ElementPlus.ElMessage.success('保存成功');
```

删除确认：

```js
await ElementPlus.ElMessageBox.confirm('确认删除该记录吗？', '提示', {
  type: 'warning'
});
```

---

参考：

- Vue 3 文档：https://cn.vuejs.org/
- Element Plus：https://element-plus.org/zh-CN/


# Element Plus 常用控件速查

## 页面框架

- 全局
  - 安装后在页面里使用：app.use(ElementPlus)


## 布局和容器

- el-container / el-header / el-aside / el-main
  - 示例：
    <el-container>
      <el-aside width="250px"></el-aside>
      <el-container>
        <el-header class="h-[44px] leading-[44px]"></el-header>
        <el-main></el-main>
      </el-container>
    </el-container>
  
- el-row / el-col

- 滚动条 el-scrollbar
  - 包裹长列表以获得更好的滚动性能
  - 示例：<el-scrollbar><div v-for="x in list">...</div></el-scrollbar>


## 公共

- 按钮 el-button
  - 主要属性：type(primary/success/warning/danger/info)、size(small/default/large)、plain、round、circle、loading、disabled
  - 事件：v-on:click
  - 图标：<el-icon><Check /></el-icon>
  - 示例：
    <el-button type="primary" :loading="saving" v-on:click="save">
      <el-icon class="mr-1"><Check /></el-icon>保存
    </el-button>
  - 外框风格（描边）：在按钮上加 plain
    <el-button type="primary" plain>保存</el-button>

- 图标 el-icon
  - 用法：<el-icon><Edit /></el-icon>
  - 可与文字、按钮并排；可加 class 控制大小/颜色/间距
  - 图标：for (const [k, c] of Object.entries(ElementPlusIconsVue)) app.component(k, c)

- 加载
  - v-loading="loading" 绑定到容器或表格



## 弹出组件 Popup 

- 对话框 el-dialog
  - 显示：v-model="visible"；标题：title；尺寸：width；位置：top
  - 交互：:close-on-click-modal="false"、destroy-on-close
  - 头部插槽：<template #header>...</template>
  - 示例：
    <el-dialog v-model="visible" title="标题" width="60%" top="5vh" :close-on-click-modal="false" destroy-on-close>
      内容
    </el-dialog>

- 抽屉 el-drawer
  - 显示：v-model、方向：direction(ltr/rtl/ttb/btt)、尺寸：size(如 '50%')
  - 头部：:with-header="true"；点击遮罩不关闭：:close-on-click-modal="false"


## Form

- 输入 el-input / 选择 el-select
  - input：<el-input v-model="text" placeholder="输入..." />
  - select：<el-select v-model="val"><el-option v-for="o in opts" :key="o.value" :label="o.label" :value="o.value" /></el-select>

- 树下拉框
 <el-tree-select
    v-model="value"
    :data="data"
    :render-after-expand="false"
    show-checkbox
    style="width: 240px"
  />
  const data = [
    {
      value: '1',
      label: 'Level one 1',
      children: [
        {
          value: '1-1',
          label: 'Level two 1-1',
          children: [
            {
              value: '1-1-1',
              label: 'Level three 1-1-1',
            },
          ],
        },
      ],
    },
    ...
  ];
- 复选框 el-checkbox / el-checkbox-group
  - 单选：<el-checkbox v-model="checked">文字</el-checkbox>
  - 组：<el-checkbox-group v-model="checkedIds"><el-checkbox :label="1">A</el-checkbox></el-checkbox-group>
  - 半选：:indeterminate="bool"
  - 事件：v-on:change


## Table

- 空态 el-empty
  - 用法：<el-empty description="暂无数据"></el-empty>

- 表格 el-table / el-table-column
  - 数据：<el-table :data="rows" v-loading="loading">
  - 列：<el-table-column prop="name" label="名称" min-width="120" />
  - 自定义单元格：<template #default="scope">scope.row.xxx</template>
  - 固定列：fixed="left|right"；宽度：width|min-width
  - 示例：
    <el-table :data="rows">
      <el-table-column label="操作" fixed="left" min-width="100">
        <template #default="scope">
          <el-icon class="mr-1" v-on:click="edit(scope.row)"><Edit /></el-icon>
          <el-icon class="text-red-500" v-on:click="del(scope.row)"><Delete /></el-icon>
        </template>
      </el-table-column>
      <el-table-column prop="name" label="名称" />
    </el-table>


## 方法

- 消息
  - ElMessage.success('成功')、ElMessage.error('失败')、ElMessage.warning('警告')

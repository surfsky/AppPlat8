<template>
  <div class="slider-container" ref="sliderContainer">
    <div class="slider-bg" :style="{ width: sliderX + 'px' }"></div>
    <span v-if="!isVerified">拖动滑块验证</span>
    <span v-else style="color: white; position: relative; z-index: 1;">验证通过</span>
    <div class="slider-btn" 
         :style="{ left: sliderX + 'px' }"
         @mousedown="startDrag"
         @touchstart="startDrag">
      <el-icon v-if="isVerified" color="#67C23A"><Check /></el-icon>
      <el-icon v-else><Right /></el-icon>
    </div>
  </div>
</template>

<script>
export default {
  props: {
    sliderX: Number,
    isVerified: Boolean,
    startDrag: Function,
    sliderContainer: Object
  }
};
</script>

<style scoped>
.slider-container {
  position: relative;
  background: #f0f2f5;
  height: 40px;
  border-radius: 4px;
  text-align: center;
  line-height: 40px;
  color: #909399;
  font-size: 14px;
  margin-bottom: 20px;
  user-select: none;
}
.slider-bg {
  position: absolute;
  left: 0;
  top: 0;
  height: 100%;
  background: #67C23A;
  width: 0;
  border-radius: 4px 0 0 4px;
}
.slider-btn {
  position: absolute;
  left: 0;
  top: 0;
  width: 40px;
  height: 40px;
  background: white;
  border: 1px solid #dcdfe6;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  cursor: pointer;
  display: flex;
  justify-content: center;
  align-items: center;
  border-radius: 4px;
  transition: background 0.3s;
}
.slider-btn:hover {
  background: #ecf5ff;
}
</style>

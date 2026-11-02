/*********************************************************
 * 地图星空背景帮助类
 *********************************************************/
export class MapStarHelper {
    constructor(state) {
        this.state = state;
        this.canvas = null;
        this.resizeHandler = () => this.drawStars();
    }

    /** 初始化星空画布。 */
    init() {
        if (this.canvas) return;
        const canvas = document.createElement('canvas');
        canvas.id = 'gis3-stars';
        canvas.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:0;';
        this.state.mapContainer.appendChild(canvas);
        this.canvas = canvas;

        this.drawStars();
        window.addEventListener('resize', this.resizeHandler);
        this.updateVisibility();
    }

    /** 根据容器尺寸绘制随机星星。 */
    drawStars() {
        if (!this.canvas) return;
        const container = this.state.mapContainer;
        const dpr = window.devicePixelRatio || 1;
        const w = container.clientWidth * dpr;
        const h = container.clientHeight * dpr;
        this.canvas.width = w; this.canvas.height = h;
        this.canvas.style.width = container.clientWidth + 'px';
        this.canvas.style.height = container.clientHeight + 'px';
        const ctx = this.canvas.getContext('2d');
        ctx.fillStyle = '#000'; ctx.fillRect(0, 0, w, h);
        const count = Math.floor(w * h / 1500);
        for (let i = 0; i < count; i++) {
            ctx.fillStyle = `rgba(200,220,255,${Math.random() * 0.7 + 0.3})`;
            ctx.beginPath();
            ctx.arc(Math.random() * w, Math.random() * h, Math.random() * 1.5 + 0.3, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    /** 打开或关闭星空。 */
    setVisible(visible) {
        if (this.canvas) this.canvas.style.display = visible ? 'block' : 'none';
    }

    /** 仅在暗色主题和球体投影下显示星空。 */
    updateVisibility() {
        this.setVisible(this.state.currentTheme === 'dark' && this.state.currentProjection === 'globe');
    }

    /** 清理画布和窗口事件。 */
    destroy() {
        window.removeEventListener('resize', this.resizeHandler);
        this.canvas?.remove();
        this.canvas = null;
    }
}
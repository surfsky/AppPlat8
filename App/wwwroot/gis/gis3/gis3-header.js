// Gis3Header - 顶部标题栏（时钟）
class Gis3Header {
    constructor(titleEl) {
        this.el = document.getElementById(titleEl);
        this.timer = null;
    }
    start() {
        this._tick();
        this.timer = setInterval(() => this._tick(), 1000);
    }
    _tick() {
        if (!this.el) return;
        const n = new Date();
        const pad = v => String(v).padStart(2, '0');
        const wd = ['日','一','二','三','四','五','六'][n.getDay()];
        this.el.textContent = `${n.getFullYear()}年${pad(n.getMonth()+1)}月${pad(n.getDate())}日 星期${wd} ${pad(n.getHours())}:${pad(n.getMinutes())}:${pad(n.getSeconds())}`;
    }
}
window.Gis3Header = Gis3Header;
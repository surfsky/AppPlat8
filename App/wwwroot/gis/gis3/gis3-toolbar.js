// Gis3Toolbar - 顶部搜索工具栏
class Gis3Toolbar {
    constructor(mapHelper) {
        this.mapHelper = mapHelper;
        this.wrap = document.querySelector('.map-toolbar');
        this.input = document.getElementById('address-input');
        this.btn = document.getElementById('btn-search');
        this.suggest = document.getElementById('address-suggest-list');
    }
    init() {
        if (!this.input) return;
        this.input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') this._onSearch();
        });
        if (this.btn) this.btn.addEventListener('click', () => this._onSearch());
    }
    // 解析经纬度或地址搜索
    _onSearch() {
        const q = (this.input.value || '').trim();
        if (!q) return;
        // 尝试解析经纬度 "lng,lat"
        const m = q.match(/^(-?\d+(\.\d+)?)\s*[, ]\s*(-?\d+(\.\d+)?)$/);
        if (m) {
            const lng = parseFloat(m[1]), lat = parseFloat(m[3]);
            if (lng >= -180 && lng <= 180 && lat >= -90 && lat <= 90) {
                this.mapHelper && this.mapHelper.flyTo(lng, lat, 12);
                return;
            }
        }
        // 其他地址搜索：简单高德地理编码
        this._amapGeo(q);
    }
    _amapGeo(q) {
        // 懒加载 GisAddressSearch（如果存在）
        if (window.GisAddressSearch && this.mapHelper) {
            window.GisAddressSearch.search({
                keywords: q,
                onSelect: (poi) => {
                    if (poi && poi.lnglat) {
                        this.mapHelper.flyTo(poi.lnglat.lng, poi.lnglat.lat, 14);
                    }
                },
                onResults: (list) => {
                    if (!this.suggest) return;
                    this.suggest.innerHTML = '';
                    list.slice(0, 8).forEach(r => {
                        const div = document.createElement('div');
                        div.className = 'address-suggest-item';
                        div.innerHTML = `<div class="suggest-title">${r.name}</div><div class="suggest-addr">${r.address || ''}</div>`;
                        div.onclick = () => {
                            this.mapHelper.flyTo(r.lnglat.lng, r.lnglat.lat, 14);
                            this.suggest.innerHTML = '';
                        };
                        this.suggest.appendChild(div);
                    });
                }
            });
        }
    }
    // 折叠/展开
    toggle() {
        if (!this.wrap) return;
        this.wrap.classList.toggle('toolbar-collapsed');
    }
}
window.Gis3Toolbar = Gis3Toolbar;
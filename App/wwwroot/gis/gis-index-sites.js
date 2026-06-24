/**
 * 参考网站抽屉
 */
(function () {
    class GisSiteDrawer {
        /**构造 */
        constructor() {
            this.btnId = 'btn-site-toggle';
            this.hostId = 'gis-site-drawer-host';
            this.styleId = 'gis-site-drawer-style';
            this.api = '/httpapi/sites/GetSiteGroups';
            this.inited = false;
            this.loading = false;
            this.loaded = false;
            this.opened = false;
            this.groups = [];
            this.btn = null;
            this.host = null;
            this.panel = null;
            this.body = null;
        }

        /**初始化 */
        init() {
            if (this.inited) return;
            this.btn = document.getElementById(this.btnId);
            if (!this.btn) return;

            this.ensureStyle();
            this.ensureHost();
            this.bindEvents();
            this.inited = true;
        }

        /**注入样式 */
        ensureStyle() {
            if (document.getElementById(this.styleId)) return;

            const style = document.createElement('style');
            style.id = this.styleId;
            style.textContent = `
#${this.hostId} {
    position: fixed;
    inset: 0;
    z-index: 80;
    pointer-events: none;
}
#${this.hostId} .site-mask {
    position: absolute;
    inset: 0;
    background: rgba(2, 6, 23, 0.36);
    opacity: 0;
    transition: opacity .22s ease;
}
#${this.hostId} .site-drawer {
    position: absolute;
    top: 0;
    right: 0;
    width: min(420px, calc(100vw - 26px));
    height: 100%;
    display: flex;
    flex-direction: column;
    background: linear-gradient(180deg, rgba(2, 6, 23, 0.98), rgba(3, 16, 80, 0.98));
    border-left: 1px solid rgba(56, 189, 248, 0.38);
    box-shadow: -16px 0 40px rgba(2, 6, 23, 0.46);
    transform: translateX(100%);
    transition: transform .22s ease;
}
#${this.hostId}.is-open {
    pointer-events: auto;
}
#${this.hostId}.is-open .site-mask {
    opacity: 1;
}
#${this.hostId}.is-open .site-drawer {
    transform: translateX(0);
}
#${this.hostId} .site-head {
    padding: 18px 18px 14px;
    border-bottom: 1px solid rgba(56, 189, 248, 0.18);
}
#${this.hostId} .site-title {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    color: #f8fafc;
}
#${this.hostId} .site-title-text {
    font-size: 18px;
    font-weight: 700;
    letter-spacing: .02em;
}
#${this.hostId} .site-close {
    width: 32px;
    height: 32px;
    border: 1px solid rgba(56, 189, 248, 0.26);
    border-radius: 999px;
    background: rgba(15, 23, 42, 0.55);
    color: #cbd5e1;
    cursor: pointer;
}
#${this.hostId} .site-close:hover {
    color: #fff;
    border-color: rgba(56, 189, 248, 0.58);
    background: rgba(14, 66, 146, 0.6);
}
#${this.hostId} .site-tip {
    margin-top: 8px;
    color: rgba(191, 219, 254, 0.82);
    font-size: 12px;
}
#${this.hostId} .site-body {
    flex: 1;
    min-height: 0;
    overflow: auto;
    padding: 16px 14px 20px;
}
#${this.hostId} .site-empty,
#${this.hostId} .site-loading {
    padding: 22px 14px;
    color: rgba(191, 219, 254, 0.78);
    text-align: center;
    font-size: 13px;
}
#${this.hostId} .site-group + .site-group {
    margin-top: 16px;
}
#${this.hostId} .site-group-title {
    margin-bottom: 10px;
    padding-left: 2px;
    color: #7dd3fc;
    font-size: 13px;
    font-weight: 700;
    letter-spacing: .08em;
}
#${this.hostId} .site-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
}
#${this.hostId} .site-item {
    display: flex;
    align-items: flex-start;
    gap: 12px;
    width: 100%;
    padding: 12px;
    border: 1px solid rgba(56, 189, 248, 0.16);
    border-radius: 12px;
    background: rgba(15, 23, 42, 0.46);
    color: #e2e8f0;
    text-align: left;
    cursor: pointer;
    transition: transform .16s ease, border-color .16s ease, background .16s ease;
}
#${this.hostId} .site-item:hover {
    transform: translateX(-2px);
    border-color: rgba(56, 189, 248, 0.56);
    background: rgba(14, 66, 146, 0.42);
}
#${this.hostId} .site-icon {
    width: 42px;
    height: 42px;
    flex: 0 0 42px;
    border-radius: 12px;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(8, 145, 178, 0.16);
    color: #7dd3fc;
    font-size: 18px;
    overflow: hidden;
}
#${this.hostId} .site-icon img {
    width: 100%;
    height: 100%;
    object-fit: cover;
}
#${this.hostId} .site-main {
    min-width: 0;
    flex: 1;
}
#${this.hostId} .site-name {
    color: #f8fafc;
    font-size: 15px;
    font-weight: 700;
    line-height: 1.35;
}
#${this.hostId} .site-desc {
    margin-top: 4px;
    color: white;
    opacity: 0.5;
    font-size: 12px;
    line-height: 1.45;
}
#${this.hostId} .site-url {
    margin-top: 6px;
    color: white;
    opacity: 0.5;
    font-size: 12px;
    line-height: 1.3;
    word-break: break-all;
    /*太长缩略显示*/
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}
@media (max-width: 640px) {
    #${this.hostId} .site-drawer {
        width: calc(100vw - 12px);
    }
}
`;
            document.head.appendChild(style);
        }

        /**构造宿主 */
        ensureHost() {
            if (this.host) return;

            const host = document.createElement('div');
            host.id = this.hostId;
            host.innerHTML = `
<div class="site-mask"></div>
<aside class="site-drawer" aria-label="参考网站抽屉">
    <div class="site-head">
        <div class="site-title">
            <span class="site-title-text">参考网站</span>
            <button type="button" class="site-close" aria-label="关闭网站抽屉">
                <i class="fa-solid fa-xmark"></i>
            </button>
        </div>
    </div>
    <div class="site-body">
        <div class="site-loading">正在加载网站...</div>
    </div>
</aside>`;

            document.body.appendChild(host);
            this.host = host;
            this.panel = host.querySelector('.site-drawer');
            this.body = host.querySelector('.site-body');
        }

        /**绑定事件 */
        bindEvents() {
            this.btn.addEventListener('click', (e) => {
                e.stopPropagation();
                this.toggle();
            });

            this.host.querySelector('.site-mask').addEventListener('click', () => this.close());
            this.host.querySelector('.site-close').addEventListener('click', () => this.close());
            this.panel.addEventListener('click', (e) => e.stopPropagation());

            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape') this.close();
            });
        }

        /**切换 */
        toggle() {
            if (this.opened) {
                this.close();
                return;
            }
            this.open();
        }

        /**打开 */
        async open() {
            this.host.classList.add('is-open');
            this.btn.classList.add('active');
            this.opened = true;
            await this.load();
        }

        /**关闭 */
        close() {
            if (!this.host) return;
            this.host.classList.remove('is-open');
            if (this.btn) this.btn.classList.remove('active');
            this.opened = false;
        }

        /**加载 */
        async load() {
            if (this.loaded || this.loading) return;

            this.loading = true;
            this.renderLoading();

            try {
                const res = await axios.get(this.api);
                const body = res?.data || {};
                if (body.code !== 0) {
                    this.renderEmpty(body.message || '网站加载失败');
                    return;
                }

                this.groups = this.normalizeGroups(body.data);
                this.loaded = true;
                this.render();
            } catch (err) {
                console.error('加载参考网站失败', err);
                this.renderEmpty('网站加载失败');
            } finally {
                this.loading = false;
            }
        }

        /**标准化 */
        normalizeGroups(list) {
            if (!Array.isArray(list)) return [];

            return list.map(group => ({
                type: group.type || group.Type || '其它',
                items: Array.isArray(group.items || group.Items) ? (group.items || group.Items) : []
            })).filter(group => group.items.length > 0);
        }

        /**渲染加载中 */
        renderLoading() {
            if (!this.body) return;
            this.body.innerHTML = '<div class="site-loading">正在加载网站...</div>';
        }

        /**渲染空态 */
        renderEmpty(msg) {
            if (!this.body) return;
            this.body.innerHTML = `<div class="site-empty">${this.escapeHtml(msg || '暂无网站')}</div>`;
        }

        /**渲染 */
        render() {
            if (!this.body) return;
            if (!this.groups.length) {
                this.renderEmpty('暂无网站');
                return;
            }

            this.body.innerHTML = this.groups.map(group => this.renderGroup(group)).join('');
            this.bindItemEvents();
        }

        /**渲染分组 */
        renderGroup(group) {
            return `
<section class="site-group">
    <div class="site-group-title">${this.escapeHtml(group.type)}</div>
    <div class="site-list">
        ${group.items.map(item => this.renderItem(item)).join('')}
    </div>
</section>`;
        }

        /**渲染站点 */
        renderItem(item) {
            const id = item.id ?? item.Id ?? 0;
            const name = item.name || item.Name || '';
            const desc = item.desc || item.Desc || '';
            const url = item.url || item.Url || '';
            const icon = item.icon || item.Icon || '';

            return `
<button type="button" class="site-item" data-site-id="${id}" data-site-url="${this.escapeAttr(url)}">
    <span class="site-icon">${this.renderIcon(icon, name)}</span>
    <span class="site-main">
        <span class="site-name">${this.escapeHtml(name)}</span>
        <br>
        ${desc ? `<span class="site-desc">${this.escapeHtml(desc)}</span>` : ''}
        ${url ? `<span class="site-url">${this.escapeHtml(url)}</span>` : ''}
    </span>
</button>`;
        }

        /**渲染图标 */
        renderIcon(icon, name) {
            const text = this.escapeHtml((name || '?').trim().slice(0, 1).toUpperCase());
            if (!icon) return text;

            if (this.isIconClass(icon)) {
                return `<i class="${this.escapeAttr(icon)}"></i>`;
            }

            if (this.isImageUrl(icon)) {
                return `<img src="${this.escapeAttr(icon)}" alt="${this.escapeAttr(name || 'icon')}">`;
            }

            return this.escapeHtml(icon.trim().slice(0, 1).toUpperCase());
        }

        /**绑定列表事件 */
        bindItemEvents() {
            if (!this.body) return;

            this.body.querySelectorAll('.site-item').forEach(btn => {
                btn.addEventListener('click', () => {
                    const url = btn.dataset.siteUrl || '';
                    if (!url) return;
                    window.open(url, '_blank', 'noopener,noreferrer');
                });
            });
        }

        /**是否图标类 */
        isIconClass(icon) {
            return /(^|\s)(fa[srbld]?|fa-[\w-]+)/i.test(icon || '');
        }

        /**是否图片地址 */
        isImageUrl(icon) {
            return /(\.png|\.jpg|\.jpeg|\.gif|\.svg|\.webp)(\?|$)/i.test(icon || '') || /^(https?:\/\/|\/)/i.test(icon || '');
        }

        /**转义 html */
        escapeHtml(text) {
            return String(text || '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        /**转义属性 */
        escapeAttr(text) {
            return this.escapeHtml(text).replace(/`/g, '&#96;');
        }
    }

    const drawer = new GisSiteDrawer();
    window.addEventListener('gis:index-ready', () => drawer.init());
})();

/**
 * GIS 顶部入口：事件抽屉 + 信息抽屉
 */
(function () {
    class GisInfoDrawer {
        /**构造 */
        constructor() {
            this.btnId = 'btn-info-toggle';
            this.eventBtnId = 'btn-event-toggle';
            this.hostId = 'gis-info-drawer-host';
            this.styleId = 'gis-info-drawer-style';
            this.inited = false;
            this.opened = false;
            this.activeTab = 'sites';
            this.tabs = [
                { key: 'sites', label: '常用网址', url: '/Open/Sites' },
                { key: 'contacts', label: '联系人', url: '/OA/Contacts' }
            ];
            this.btn = null;
            this.eventBtn = null;
            this.host = null;
            this.panel = null;
            this.iframe = null;
            this.tabEls = [];
        }

        /**初始化 */
        init() {
            if (this.inited) return;

            this.btn = document.getElementById(this.btnId);
            this.eventBtn = document.getElementById(this.eventBtnId);
            if (!this.btn && !this.eventBtn) return;

            this.ensureStyle();
            this.ensureHost();
            this.bindInfoEvents();
            this.bindEventButton();
            this.inited = true;
        }

        /**获取管理器 */
        getManager() {
            return (window.top && window.top.EleManager) ? window.top.EleManager : window.EleManager;
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
    z-index: 5008;
    pointer-events: none;
}
#${this.hostId}.is-open {
    pointer-events: auto;
}
#${this.hostId} .info-mask {
    position: absolute;
    inset: 0;
    background: rgba(2, 6, 23, 0.36);
    opacity: 0;
    transition: opacity .22s ease;
}
#${this.hostId} .info-drawer {
    position: absolute;
    top: 0;
    right: 0;
    width: min(860px, calc(100vw - 24px));
    height: 100%;
    display: flex;
    flex-direction: column;
    background: linear-gradient(180deg, rgba(2, 6, 23, 0.98), rgba(3, 16, 80, 0.98));
    border-left: 1px solid rgba(56, 189, 248, 0.38);
    box-shadow: -16px 0 40px rgba(2, 6, 23, 0.46);
    transform: translateX(100%);
    transition: transform .22s ease;
    overflow: hidden;
}
#${this.hostId}.is-open .info-mask {
    opacity: 1;
}
#${this.hostId}.is-open .info-drawer {
    transform: translateX(0);
}
#${this.hostId} .info-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    padding: 14px 16px 12px;
    border-bottom: 1px solid rgba(56, 189, 248, 0.18);
}
#${this.hostId} .info-head-main {
    min-width: 0;
    flex: 1;
    display: flex;
    align-items: center;
    gap: 14px;
}
#${this.hostId} .info-title {
    color: #f8fafc;
    font-size: 18px;
    font-weight: 700;
    letter-spacing: .02em;
    white-space: nowrap;
}
#${this.hostId} .info-tabs {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
}
#${this.hostId} .info-tab {
    min-width: 92px;
    height: 34px;
    padding: 0 14px;
    border: 1px solid rgba(56, 189, 248, 0.22);
    border-radius: 999px;
    background: rgba(15, 23, 42, 0.46);
    color: rgba(226, 232, 240, 0.9);
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    transition: all .18s ease;
}
#${this.hostId} .info-tab:hover {
    border-color: rgba(56, 189, 248, 0.5);
    background: rgba(14, 66, 146, 0.42);
    color: #fff;
}
#${this.hostId} .info-tab.active {
    border-color: rgba(56, 189, 248, 0.8);
    background: rgba(14, 116, 144, 0.56);
    color: #f8fafc;
    box-shadow: inset 0 0 0 1px rgba(125, 211, 252, 0.18);
}
#${this.hostId} .info-actions {
    display: inline-flex;
    align-items: center;
    gap: 8px;
}
#${this.hostId} .info-action-btn {
    width: 34px;
    height: 34px;
    border: 1px solid rgba(56, 189, 248, 0.26);
    border-radius: 999px;
    background: rgba(15, 23, 42, 0.55);
    color: #cbd5e1;
    cursor: pointer;
}
#${this.hostId} .info-action-btn:hover {
    color: #fff;
    border-color: rgba(56, 189, 248, 0.58);
    background: rgba(14, 66, 146, 0.6);
}
#${this.hostId} .info-body {
    flex: 1;
    min-height: 0;
    background: rgba(2, 6, 23, 0.3);
}
#${this.hostId} .info-frame {
    width: 100%;
    height: 100%;
    border: 0;
    background: #fff;
}
@media (max-width: 900px) {
    #${this.hostId} .info-drawer {
        width: calc(100vw - 12px);
    }
    #${this.hostId} .info-head {
        align-items: flex-start;
        flex-direction: column;
    }
    #${this.hostId} .info-head-main,
    #${this.hostId} .info-actions {
        width: 100%;
    }
    #${this.hostId} .info-head-main {
        flex-direction: column;
        align-items: flex-start;
        gap: 10px;
    }
}
`;
            document.head.appendChild(style);
        }

        /**创建宿主 */
        ensureHost() {
            if (this.host) return;

            const host = document.createElement('div');
            host.id = this.hostId;
            host.innerHTML = `
<div class="info-mask"></div>
<aside class="info-drawer" aria-label="信息抽屉">
    <div class="info-head">
        <div class="info-head-main">
            <span class="info-title">信息</span>
            <div class="info-tabs" role="tablist" aria-label="信息分类"></div>
        </div>
        <div class="info-actions">
            <button type="button" class="info-action-btn info-refresh" aria-label="刷新当前页面" title="刷新">
                <i class="fa-solid fa-rotate-right"></i>
            </button>
            <button type="button" class="info-action-btn info-close" aria-label="关闭信息抽屉" title="关闭">
                <i class="fa-solid fa-xmark"></i>
            </button>
        </div>
    </div>
    <div class="info-body">
        <iframe class="info-frame" title="信息内容"></iframe>
    </div>
</aside>`;

            document.body.appendChild(host);
            this.host = host;
            this.panel = host.querySelector('.info-drawer');
            this.iframe = host.querySelector('.info-frame');
            this.renderTabs();
        }

        /**渲染页签 */
        renderTabs() {
            const tabsHost = this.host?.querySelector('.info-tabs');
            if (!tabsHost) return;

            tabsHost.innerHTML = this.tabs.map(tab => `
<button type="button" class="info-tab" data-tab-key="${tab.key}" role="tab" aria-selected="false">${tab.label}</button>
            `).join('');

            this.tabEls = Array.from(tabsHost.querySelectorAll('.info-tab'));
        }

        /**绑定信息按钮事件 */
        bindInfoEvents() {
            if (this.btn) {
                this.btn.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    this.toggle();
                });
            }

            this.host.querySelector('.info-mask').addEventListener('click', () => this.close());
            this.host.querySelector('.info-close').addEventListener('click', () => this.close());
            this.host.querySelector('.info-refresh').addEventListener('click', () => this.reloadActiveFrame());
            this.panel.addEventListener('click', (e) => e.stopPropagation());

            this.tabEls.forEach(btn => {
                btn.addEventListener('click', () => {
                    const key = btn.dataset.tabKey || 'sites';
                    this.switchTab(key);
                });
            });

            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape') this.close();
            });
        }

        /**绑定事件入口 */
        bindEventButton() {
            if (!this.eventBtn) return;

            this.eventBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.openEventDrawer();
            });
        }

        /**打开事件抽屉 */
        openEventDrawer() {
            const manager = this.getManager();
            if (!manager || typeof manager.openDrawer !== 'function') {
                window.open('/Tasks/Events', '_blank', 'noopener,noreferrer');
                return;
            }

            manager.openDrawer({
                title: '事件',
                url: '/Tasks/Events',
                size: '50%',
                direction: 'rtl',
                showFooter: false,
                closeAction: 'none'
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
        open(tabKey = 'sites') {
            this.host.classList.add('is-open');
            if (this.btn) this.btn.classList.add('active');
            this.opened = true;
            this.switchTab(tabKey);
        }

        /**关闭 */
        close() {
            if (!this.host) return;
            this.host.classList.remove('is-open');
            if (this.btn) this.btn.classList.remove('active');
            this.opened = false;
        }

        /**切换标签 */
        switchTab(tabKey) {
            const next = this.tabs.find(t => t.key === tabKey) || this.tabs[0];
            if (!next) return;

            this.activeTab = next.key;
            this.tabEls.forEach(btn => {
                const isActive = btn.dataset.tabKey === next.key;
                btn.classList.toggle('active', isActive);
                btn.setAttribute('aria-selected', isActive ? 'true' : 'false');
            });

            if (this.iframe && this.iframe.getAttribute('src') !== next.url) {
                this.iframe.setAttribute('src', next.url);
            }
        }

        /**刷新当前页 */
        reloadActiveFrame() {
            if (!this.iframe) return;
            try {
                this.iframe.contentWindow?.location?.reload();
            } catch {
                const src = this.iframe.getAttribute('src') || '';
                this.iframe.setAttribute('src', src);
            }
        }
    }

    const drawer = new GisInfoDrawer();
    window.addEventListener('gis:index-ready', () => drawer.init());
})();

/**
 * GIS 顶部入口：事件抽屉 / AI 抽屉
 */
(function () {
    const EVENT_TABS = [
        { key: 'events', label: '应急事件', url: '/Events/Events?typeName=突发事件' },
        { key: 'duty', label: '值班表', url: '/oa/dutySchedules?md=view' },
        { key: 'contacts', label: '联系人', url: '/crm/ContactManager' },
        { key: 'sites', label: '常用网址', url: '/Open/Sites' }
    ];

    /**获取管理器 */
    function getManager() {
        return (window.top && window.top.EleManager) ? window.top.EleManager : window.EleManager;
    }

    /**绑定点击 */
    function bindClick(buttonId, handler) {
        const button = document.getElementById(buttonId);
        if (!button) return;
        button.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            handler();
        });
    }

    /**打开多标签抽屉 */
    function openTabsDrawer(title, tabs, fallbackUrl) {
        const manager = getManager();
        if (!manager || typeof manager.openDrawer !== 'function') {
            window.open(fallbackUrl || tabs?.[0]?.url || '/', '_blank', 'noopener,noreferrer');
            return;
        }

        manager.openDrawer({
            title,
            tabs,
            size: '50%',
            direction: 'rtl',
            showFooter: false,
            closeAction: 'none'
        });
    }

    /**打开 AI 聊天抽屉 */
    function openChatDrawer() {
        const manager = getManager();
        if (!manager || typeof manager.openDrawer !== 'function') {
            window.open('/AI/Chat', '_blank', 'noopener,noreferrer');
            return;
        }

        manager.openDrawer({
            title: 'AI 助手',
            url: '/AI/Chat?t=' + Date.now(),
            size: '50%',
            direction: 'rtl',
            showFooter: false,
            closeAction: 'none'
        });
    }

    /**初始化 */
    function init() {
        bindClick('btn-event-toggle', () => openTabsDrawer('应急处置', EVENT_TABS, '/Events/Events?typeName=突发事件'));
        bindClick('btn-chat-toggle', () => openChatDrawer());
    }

    window.addEventListener('gis:index-ready', init);
})();

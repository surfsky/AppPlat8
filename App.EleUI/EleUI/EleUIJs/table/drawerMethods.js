export function initDrawerState(table, vueApi) {
    const { ref } = vueApi;

    table.drawerSize = ref(window.innerWidth < 768 ? '100%' : '50%');
    table.drawerPosition = ref('rtl');
    table.isResizing = ref(false);
}

export const drawerMethods = {
    openForm(id, urlBase, drawerTitle = null) {
        this.openDataDrawer(id, urlBase, 'edit', drawerTitle);
    },

    openView(id, urlBase, drawerTitle = null) {
        this.openDataDrawer(id, urlBase, 'view', drawerTitle);
    },

    openDataDrawer(id, urlBase, modeParam, drawerTitle = null) {
        const resolvedBase = this.resolveDrawerUrlBase(urlBase);

        const mode = (id === 0 || id === '0') ? 'new' : modeParam;
        const selectedId = this.selectedIds.value.length > 0 ? this.selectedIds.value[0] : null;
        const effectiveSelectId = selectedId ?? (id && id !== 0 && id !== '0' ? id : null);
        const query = new URLSearchParams({ id: `${id}`, md: mode });
        if (effectiveSelectId !== null && typeof effectiveSelectId !== 'undefined') {
            query.set('selectId', `${effectiveSelectId}`);
        }

        const separator = resolvedBase.includes('?') ? '&' : '?';
        const url = `${resolvedBase}${separator}${query.toString()}`;
        const configuredSize = this.config?.formDrawerSize || null;
        this.openDrawer(url, configuredSize, 'rtl', drawerTitle);
    },

    resolveDrawerUrlBase(urlBase) {
        if (!urlBase) return '';
        try {
            const absolute = new URL(urlBase, window.location.href);
            return `${absolute.pathname}${absolute.search}${absolute.hash}`;
        } catch (e) {
            return urlBase;
        }
    },

    openDrawer(url, width = null, position = 'rtl', titleOverride = null) {
        const size = (typeof width === 'string' && width.trim()) ? width.trim() : null;
        const title = titleOverride || this.config?.drawerTitle || this.config?.title || '编辑';
        this.drawerSize.value = size;
        this.drawerPosition.value = position || 'rtl';
        EleManager.openDrawer({
            title,
            url,
            direction: this.drawerPosition.value,
            size,
            resizable: true,
            closeOnClickModal: false,
            destroyOnClose: true,
            closeHandler: () => this.handleDrawerClose()
        });
    },

    handleDrawerClose() {
        this.loadData();
    },

    triggerChildSave() {
        try {
            this.formFrame.value?.contentWindow?.postMessage('RequestSave', '*');
        } catch {}
    },

    navigateTo(url) {
        window.location.href = url;
    },

    startResize(e) {
        e.preventDefault();
        this.isResizing.value = true;

        const startX = e.clientX;
        const drawerEl = e.target.closest('.el-drawer') || document.querySelector('.el-drawer.open') || document.querySelector('.el-drawer');
        const startWidth = drawerEl ? drawerEl.offsetWidth : 600;

        const overlay = document.createElement('div');
        Object.assign(overlay.style, {
            position: 'fixed', top: '0', left: '0', width: '100vw', height: '100vh',
            zIndex: '99999', cursor: 'col-resize', backgroundColor: 'transparent'
        });
        document.body.appendChild(overlay);

        const doResize = (moveEvent) => {
            if (!this.isResizing.value) return;
            let offset;
            if (this.drawerPosition.value === 'ltr') {
                offset = moveEvent.clientX - startX;
            } else {
                offset = startX - moveEvent.clientX;
            }

            const newWidth = startWidth + offset;
            if (newWidth > 300 && newWidth < window.innerWidth - 50) {
                this.drawerSize.value = `${newWidth}px`;
            }
        };

        const stopResize = () => {
            this.isResizing.value = false;
            document.removeEventListener('mousemove', doResize);
            document.removeEventListener('mouseup', stopResize);
            if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
        };

        document.addEventListener('mousemove', doResize);
        document.addEventListener('mouseup', stopResize);
    }
};

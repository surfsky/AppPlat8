export const imageViewerMethods = {
    openImageViewer(imageUrls = [], initialIndex = 0) {
        const hostWindow = this._uiWindow || window;
        const hostDocument = hostWindow.document || document;
        const ep = hostWindow.ElementPlus || window.ElementPlus;
        const vueRuntime = hostWindow.Vue || window.Vue;

        const list = (Array.isArray(imageUrls) ? imageUrls : [imageUrls])
            .map(item => (typeof item === "string" ? item.trim() : ""))
            .filter(item => !!item);

        if (!list.length) {
            this.showWarning("暂无可预览图片");
            return false;
        }

        const ElImageViewer = ep?.ElImageViewer;
        const createVNode = vueRuntime?.createVNode || vueRuntime?.h;
        const render = vueRuntime?.render;
        if (!ElImageViewer || !createVNode || !render) {
            hostWindow.open(list[0], "_blank", "noopener");
            return false;
        }

        this.closeImageViewer();

        const indexNum = Number.isFinite(Number(initialIndex)) ? Math.max(0, Math.floor(Number(initialIndex))) : 0;
        const normalizedIndex = indexNum >= list.length ? 0 : indexNum;

        const container = hostDocument.createElement("div");
        container.className = "ele-image-viewer-host";
        hostDocument.body.appendChild(container);

        const vnode = createVNode(ElImageViewer, {
            urlList: list,
            initialIndex: normalizedIndex,
            teleported: false,
            hideOnClickModal: true,
            closeOnPressEscape: true,
            zIndex: 3000,
            onClose: () => this.closeImageViewer()
        });

        render(vnode, container);
        this._imageViewerContainer = container;
        this._imageViewerRender = render;
        return true;
    },

    closeImageViewer() {
        if (!this._imageViewerContainer) return;
        try {
            if (this._imageViewerRender) {
                this._imageViewerRender(null, this._imageViewerContainer);
            }
        } catch {}

        if (this._imageViewerContainer.parentNode) {
            this._imageViewerContainer.parentNode.removeChild(this._imageViewerContainer);
        }

        this._imageViewerContainer = null;
        this._imageViewerRender = null;
    }
};

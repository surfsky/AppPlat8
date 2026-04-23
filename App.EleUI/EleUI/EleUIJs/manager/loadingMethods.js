export const loadingMethods = {
    showLoading(text = "加载中...", options = {}) {
        this.closeLoading();
        this._ensureLoadingStyle();

        const { customClass: optionClass, ...restOptions } = options || {};
        const customClass = optionClass
            ? `ele-manager-loading ${optionClass}`
            : "ele-manager-loading";

        this._loading = this.ElLoading.service({
            lock: true,
            text,
            background: "rgba(0, 0, 0, 0.35)",
            customClass,
            ...restOptions
        });
        return this._loading;
    },

    _ensureLoadingStyle() {
        if (this._loadingStyleInjected) return;
        this._loadingStyleInjected = true;

        const hostDocument = this._uiWindow?.document || document;

        const styleId = "ele-manager-loading-style";
        if (hostDocument.getElementById(styleId)) return;

        const style = hostDocument.createElement("style");
        style.id = styleId;
        style.textContent = `
.el-loading-mask.ele-manager-loading .el-loading-spinner .el-loading-text {
    color: #fff !important;
    text-shadow: 0 1px 2px rgba(0, 0, 0, 0.25);
}
.el-loading-mask.ele-manager-loading .el-loading-spinner .circular {
    --el-color-primary: #fff;
}
.el-loading-mask.ele-manager-loading .el-loading-spinner .path {
    stroke: #fff !important;
}
`;
        hostDocument.head.appendChild(style);
    },

    closeLoading() {
        if (this._loading && typeof this._loading.close === "function") {
            this._loading.close();
        }
        this._loading = null;
    }
};

import { Utils } from "../Utils.js";

export const navigationMethods = {
    goto(url) {
        if (!url || typeof url !== "string") {
            console.warn("EleManager.goto: url is required");
            return;
        }
        window.location.href = url;
    },

    changeMode(mode) {
        if (!mode || typeof mode !== "string") {
            console.warn("EleManager.changeMode: mode is required");
            return;
        }
        const url = new URL(window.location.href);
        url.searchParams.set("md", mode.toLowerCase().trim());
        window.location.href = url.toString();
    },

    closePage(data = {}) {
        const result = {
            code: 0,
            message: "closed",
            data: (data && typeof data === "object") ? data : {},
            __elePageClose: true
        };

        const inFrame = (window.parent && window.parent !== window) || (window.top && window.top !== window);

        try {
            if (window.parent && window.parent !== window) {
                window.parent.postMessage(result, "*");
            }
        } catch {}

        try {
            if (window.top && window.top !== window && window.top !== window.parent) {
                window.top.postMessage(result, "*");
            }
        } catch {}

        if (inFrame) {
            return true;
        }

        try {
            this.closeDrawer();
            if (window.top && window.top.EleManager && window.top !== window) {
                window.top.EleManager.closeDrawer();
            }
        } catch {
            this.closeDrawer();
        }

        return true;
    },

    handleDrawerCloseAction(closeAction, payload = null) {
        const action = Utils.safeText(closeAction, 30).toLowerCase();
        if (!action || action === "none") {
            return;
        }

        if (action === "refreshpage") {
            try {
                window.location.reload();
            } catch (e) {
                console.error("refresh page failed:", e);
            }
            return;
        }

        if (action === "refreshdata") {
            const msg = {
                __eleRefreshData: true,
                payload: payload && typeof payload === "object" ? payload : null
            };

            try { window.postMessage(msg, "*"); } catch {}
            try {
                if (window.parent && window.parent !== window) {
                    window.parent.postMessage(msg, "*");
                }
            } catch {}
            try {
                if (window.top && window.top !== window) {
                    window.top.postMessage(msg, "*");
                }
            } catch {}
        }
    }
};

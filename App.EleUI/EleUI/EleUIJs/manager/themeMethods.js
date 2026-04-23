import { Utils } from "../Utils.js";

export const themeMethods = {
    _normalizeTheme(themeName) {
        const normalized = Utils.safeText(themeName, 20).toLowerCase();
        if (normalized === "light" || normalized === "dark" || normalized === "system") {
            return normalized;
        }
        return "system";
    },

    _getThemeStorage() {
        try {
            return (this._uiWindow && this._uiWindow.localStorage) ? this._uiWindow.localStorage : window.localStorage;
        } catch {
            return null;
        }
    },

    _isSystemDark() {
        return !!window.matchMedia("(prefers-color-scheme: dark)").matches;
    },

    _getThemeRootElements() {
        const roots = [];
        const hostRoot = this._uiWindow?.document?.documentElement;
        const localRoot = document?.documentElement;

        if (hostRoot) roots.push(hostRoot);
        if (localRoot && localRoot !== hostRoot) roots.push(localRoot);
        return roots;
    },

    _initThemeObserver() {
        if (this._themeListenerBound || !window.matchMedia) return;
        this._themeListenerBound = true;
        this._themeMediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
        this._themeMediaQuery.addEventListener("change", () => {
            if (this.getTheme() === "system") {
                this.setTheme("system");
            }
        });
    },

    setTheme(themeName = "system") {
        const theme = this._normalizeTheme(themeName);
        const isDark = theme === "dark" || (theme === "system" && this._isSystemDark());

        this._getThemeRootElements().forEach(root => {
            root.classList.toggle("dark", isDark);
        });

        const storage = this._getThemeStorage();
        if (storage) {
            storage.setItem(this._themeStorageKey, theme);
        }

        return theme;
    },

    getTheme() {
        const storage = this._getThemeStorage();
        const stored = storage ? storage.getItem(this._themeStorageKey) : null;
        return this._normalizeTheme(stored || "system");
    }
};

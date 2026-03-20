/**
 * Utility functions
 */
export class Utils {
    /**
     * Convert value to boolean with fallback
     * @param {*} value 
     * @param {*} fallback 
     * @returns 
     */
    static toBool(value, fallback = true) {
        if (typeof value === 'boolean') return value;
        if (typeof value === 'string') {
            const v = value.trim().toLowerCase();
            if (v === 'true') return true;
            if (v === 'false') return false;
        }
        return fallback;
    }

    /**
     * Safely get text with a maximum length
     * @param {*} value 
     * @param {number} maxLen 
     * @returns {string}
     */
    static safeText(value, maxLen = 300) {
        if (typeof value !== 'string') return '';
        return value.trim().slice(0, maxLen);
    }

    /**
     * Safely get a value from a set of allowed values
     * @param {*} value 
     * @param {*} allowed 
     * @param {*} fallback 
     * @returns 
     */
    static safeType(value, allowed, fallback) {
        const t = typeof value === 'string' ? value.toLowerCase() : '';
        return allowed.includes(t) ? t : fallback;
    }

    /**
     * Get Global Function by Path
     * @param {*} path 
     * @returns 
     */
    static getGlobalFunction(path) {
        if (typeof path !== 'string' || !path.trim()) return null;
        const keys = path.split('.').map((s) => s.trim()).filter(Boolean);
        let cur = window;
        for (const k of keys) {
            cur = cur?.[k];
        }
        return typeof cur === 'function' ? cur : null;
    }

    /**
     * Resolve Handler Function
     * @param {*} handler 
     * @returns 
     */
    static resolveHandler(handler) {
        if (typeof handler === 'function') return handler;
        if (typeof handler === 'string') return Utils.getGlobalFunction(handler);
        return null;
    }

    /**
     * Format Date String
     * @param {*} s 
     * @param {*} type 
     * @returns 
     */
    static formatDate(s, type) {
        if (!s) return '';
        try {
            const d = new Date(s);
            if (isNaN(d.getTime())) return s;

            const pad = (n) => String(n).padStart(2, '0');
            const y = d.getFullYear();
            const m = pad(d.getMonth() + 1);
            const dd = pad(d.getDate());
            const hh = pad(d.getHours());
            const mm = pad(d.getMinutes());
            const ss = pad(d.getSeconds());

            if (type === 'Date') return `${y}-${m}-${dd}`;
            if (type === 'Time') return `${hh}:${mm}:${ss}`;
            if (type === 'DateTime') return `${y}-${m}-${dd} ${hh}:${mm}:${ss}`;

            return `${y}-${m}-${dd} ${hh}:${mm}`;
        } catch {
            return s;
        }
    }


    /**
     * Format Enum Value
     * @param {*} val 
     * @param {*} options 
     * @returns 
     */
    static formatEnum(val, options) {
        if (val === null || val === undefined) return '';
        if (!options || !Array.isArray(options)) return val;
        const item = options.find((o) => o.Id == val || o.Value == val || o === val);
        return item ? item.Title : val;
    }

    /**
     * Get CSRF Token from current document or parent document
     * @returns 
     */
    static getCsrfToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value
            || window.parent?.document?.querySelector('input[name="__RequestVerificationToken"]')?.value
            || window.top?.document?.querySelector('input[name="__RequestVerificationToken"]')?.value
            || '';
    }

    /**
     * Generic request helper that uses axios if available, otherwise falls back to fetch
     * @param {*} url 
     * @param {*} data 
     * @param {*} method 
     * @returns 
     */
    static async request(url, data = null, method = 'POST') {
        const token = Utils.getCsrfToken();
        const headers = {
            RequestVerificationToken: token,
            'X-Requested-With': 'XMLHttpRequest'
        };

        // Use axios if available
        if (window.axios) {
            const response = await window.axios({
                url,
                method,
                data,
                headers
            });
            return response.data;
        }

        // Use fetch
        const res = await fetch(url, {
            method,
            headers: {
                ...headers,
                'Content-Type': 'application/json'
            },
            body: data ? JSON.stringify(data) : undefined
        });
        if (!res.ok) throw new Error(res.statusText);
        return await res.json();
    }
}

// Expose Utils globally
if (typeof globalThis !== 'undefined') {
    globalThis.Utils = Utils;
}

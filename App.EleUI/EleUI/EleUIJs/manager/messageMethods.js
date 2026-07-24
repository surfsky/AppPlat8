export const messageMethods = {
    message(msg, type = "info", options = {}) {
        const popupOptions = this.resolvePopupOptions(options, 8000, {
            appendToBody: true,
            offset: 68
        });
        return this.ElMessage({ message: msg, type, ...popupOptions });
    },
    showSuccess(msg, options = {}) { return this.message(msg || "操作成功", "success", options); },
    showError(msg, options = {}) { return this.message(msg || "操作失败", "error", options); },
    showWarning(msg, options = {}) { return this.message(msg || "请注意当前操作", "warning", options); },
    showInfo(msg, options = {}) { return this.message(msg || "提示信息", "info", options); },
    toast(msg, type = "info", options = {}) { return this.message(msg, type, options); },

    notify(msgOrOptions, type = "info", options = {}) {
        if (typeof msgOrOptions === "object" && msgOrOptions !== null) {
            const popupOptions = this.resolvePopupOptions(msgOrOptions, 8001, {
                appendToBody: true,
                offset: 68
            });
            return this.ElNotification({ ...popupOptions });
        }
        const popupOptions = this.resolvePopupOptions(options, 8001, {
            appendToBody: true,
            offset: 68
        });
        return this.ElNotification({
            title: options.title || "提示",
            message: msgOrOptions,
            type,
            ...popupOptions
        });
    },

    notifySuccess(msg, options = {}) { return this.notify(msg || "操作成功", "success", options); },
    notifyError(msg, options = {}) { return this.notify(msg || "操作失败", "error", options); },
    notifyWarning(msg, options = {}) { return this.notify(msg || "请注意当前操作", "warning", options); },
    notifyInfo(msg, options = {}) { return this.notify(msg || "提示信息", "info", options); },

    alert(msg, title = "提示", options = {}) {
        const popupOptions = this.resolvePopupOptions(options, 6002, { appendToBody: true });
        return this.ElMessageBox.alert(msg, title, {
            confirmButtonText: "确定",
            ...popupOptions
        });
    },

    confirm(msg, title = "提示", options = {}) {
        const popupOptions = this.resolvePopupOptions(options, 6002, { appendToBody: true });
        return this.ElMessageBox.confirm(msg, title, {
            confirmButtonText: "确定",
            cancelButtonText: "取消",
            type: "warning",
            ...popupOptions
        });
    },

    prompt(msg, title = "请输入", options = {}) {
        const popupOptions = this.resolvePopupOptions(options, 6002, { appendToBody: true });
        return this.ElMessageBox.prompt(msg, title, {
            confirmButtonText: "确定",
            cancelButtonText: "取消",
            ...popupOptions
        });
    }
};

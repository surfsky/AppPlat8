export const messageMethods = {
    message(msg, type = "info", options = {}) { return this.ElMessage({ message: msg, type, zIndex: 6000, ...options }); },
    showSuccess(msg, options = {}) { return this.message(msg || "操作成功", "success", options); },
    showError(msg, options = {}) { return this.message(msg || "操作失败", "error", options); },
    showWarning(msg, options = {}) { return this.message(msg || "请注意当前操作", "warning", options); },
    showInfo(msg, options = {}) { return this.message(msg || "提示信息", "info", options); },
    toast(msg, type = "info", options = {}) { return this.message(msg, type, options); },

    notify(msgOrOptions, type = "info", options = {}) {
        if (typeof msgOrOptions === "object" && msgOrOptions !== null) {
            return this.ElNotification({ zIndex: 6001, ...msgOrOptions });
        }
        return this.ElNotification({
            title: options.title || "提示",
            message: msgOrOptions,
            type,
            zIndex: 6001,
            ...options
        });
    },

    notifySuccess(msg, options = {}) { return this.notify(msg || "操作成功", "success", options); },
    notifyError(msg, options = {}) { return this.notify(msg || "操作失败", "error", options); },
    notifyWarning(msg, options = {}) { return this.notify(msg || "请注意当前操作", "warning", options); },
    notifyInfo(msg, options = {}) { return this.notify(msg || "提示信息", "info", options); },

    alert(msg, title = "提示", options = {}) {
        return this.ElMessageBox.alert(msg, title, {
            confirmButtonText: "确定",
            zIndex: 6002,
            ...options
        });
    },

    confirm(msg, title = "提示", options = {}) {
        return this.ElMessageBox.confirm(msg, title, {
            confirmButtonText: "确定",
            cancelButtonText: "取消",
            type: "warning",
            zIndex: 6002,
            ...options
        });
    },

    prompt(msg, title = "请输入", options = {}) {
        return this.ElMessageBox.prompt(msg, title, {
            confirmButtonText: "确定",
            cancelButtonText: "取消",
            zIndex: 6002,
            ...options
        });
    }
};

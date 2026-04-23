export const messageMethods = {
    message(msg, type = "info", options = {}) { return this.ElMessage({ message: msg, type, ...options }); },
    showSuccess(msg) { this.ElMessage.success(msg || "操作成功"); },
    showError(msg) { this.ElMessage.error(msg || "操作失败"); },
    showWarning(msg) { this.ElMessage.warning(msg || "请注意当前操作"); },
    showInfo(msg) { this.ElMessage.info(msg || "提示信息"); },
    toast(msg, type = "info", options = {}) { return this.message(msg, type, options); },

    notify(msgOrOptions, type = "info", options = {}) {
        if (typeof msgOrOptions === "object" && msgOrOptions !== null) {
            return this.ElNotification(msgOrOptions);
        }
        return this.ElNotification({
            title: options.title || "提示",
            message: msgOrOptions,
            type,
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
            ...options
        });
    },

    confirm(msg, title = "提示") {
        return this.ElMessageBox.confirm(msg, title, {
            confirmButtonText: "确定",
            cancelButtonText: "取消",
            type: "warning"
        });
    },

    prompt(msg, title = "请输入", options = {}) {
        return this.ElMessageBox.prompt(msg, title, {
            confirmButtonText: "确定",
            cancelButtonText: "取消",
            ...options
        });
    }
};

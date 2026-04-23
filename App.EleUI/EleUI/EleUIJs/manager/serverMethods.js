import { Utils } from "../Utils.js";

export const serverMethods = {
    openMessageBoxFromServer(args = {}) {
        const text = Utils.safeText(args.text || args.message, 500) || "确认执行此操作吗？";
        const title = Utils.safeText(args.title, 80) || "提示";
        const type = Utils.safeType(args.type, ["success", "warning", "info", "error"], "info");
        const comfirmButtonText = Utils.safeText(args.comfirmButtonText || args.confirmButtonText, 20) || "确定";
        const cancelButtonText = Utils.safeText(args.cancelButtonText, 20) || "取消";
        const isAlert = !!args.isAlert || !cancelButtonText;
        const clientHandler = Utils.safeText(args.clientHandler, 120);
        const serverHandler = Utils.safeText(args.serverHandler, 80);

        const onConfirm = async () => {
            const clientFn = Utils.getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn("confirm", args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this.postServerHandler(serverHandler, {
                    action: "confirm",
                    clientHandler,
                    text,
                    title,
                    type
                });
            }
            return true;
        };

        const onCancel = async () => {
            const clientFn = Utils.getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn("cancel", args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this.postServerHandler(serverHandler, {
                    action: "cancel",
                    clientHandler,
                    text,
                    title,
                    type
                });
            }
            return false;
        };

        if (isAlert) {
            return this.ElMessageBox.alert(text, title, {
                type,
                confirmButtonText: comfirmButtonText
            }).then(onConfirm);
        }

        return this.ElMessageBox.confirm(text, title, {
            type,
            confirmButtonText: comfirmButtonText,
            cancelButtonText
        }).then(onConfirm).catch(onCancel);
    },

    openInputBoxFromServer(args = {}) {
        const text = Utils.safeText(args.text || args.message, 500) || "请输入内容";
        const title = Utils.safeText(args.title, 80) || "请输入";
        const type = Utils.safeType(args.type, ["success", "warning", "info", "error"], "info");
        const comfirmButtonText = Utils.safeText(args.comfirmButtonText || args.confirmButtonText, 20) || "确定";
        const cancelButtonText = Utils.safeText(args.cancelButtonText, 20) || "取消";
        const inputPlaceholder = Utils.safeText(args.inputPlaceholder, 120) || "请输入内容";
        const inputValue = Utils.safeText(args.inputValue, 500);
        const clientHandler = Utils.safeText(args.clientHandler, 120);
        const serverHandler = Utils.safeText(args.serverHandler, 80);
        const inputPattern = Utils.safeText(args.inputPattern, 300);
        const inputErrorMessage = Utils.safeText(args.inputErrorMessage, 200);

        const onConfirm = async (result) => {
            const value = typeof result?.value === "string" ? result.value : "";
            const clientFn = Utils.getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn("confirm", value, args); } catch (err) { console.error(err); }
            }
            if (serverHandler) {
                await this.postServerHandler(serverHandler, {
                    action: "confirm",
                    value,
                    clientHandler,
                    text,
                    title,
                    type
                });
            }
            return result;
        };

        const onCancel = async (err) => {
            const action = Utils.safeText(err?.action, 20) || "cancel";
            const clientFn = Utils.getGlobalFunction(clientHandler);
            if (clientFn) {
                try { clientFn(action, "", args); } catch (error) { console.error(error); }
            }
            if (serverHandler) {
                await this.postServerHandler(serverHandler, {
                    action,
                    value: "",
                    clientHandler,
                    text,
                    title,
                    type
                });
            }
            return false;
        };

        const promptOptions = {
            type,
            confirmButtonText: comfirmButtonText,
            cancelButtonText,
            inputPlaceholder,
            inputValue
        };
        if (inputPattern) {
            promptOptions.inputPattern = new RegExp(inputPattern);
        }
        if (inputErrorMessage) {
            promptOptions.inputErrorMessage = inputErrorMessage;
        }
        return this.ElMessageBox.prompt(text, title, promptOptions).then(onConfirm).catch(onCancel);
    },

    async postServerHandler(serverHandler, payload) {
        if (!serverHandler || typeof serverHandler !== "string") return null;
        const url = "?handler=" + encodeURIComponent(serverHandler.trim());
        const res = await Utils.request(url, payload, "POST");
        if (res && (res.code === 0 || res.code === "0")) {
            const cmdPayload = res.data;
            if (cmdPayload && typeof cmdPayload === "object" && cmdPayload.command) {
                this.executeServerCommand(cmdPayload);
            }
        }
        return res;
    },

    invoke(url, successMsg = "操作成功") {
        Utils.request(url).then(res => {
            if (res.success || res.code === 0) {
                this.showSuccess(successMsg);
            } else {
                this.showError(res.msg || "操作失败");
            }
        }).catch(() => {
            this.showError("网络请求错误");
        });
    },

    executeServerCommand(payload) {
        try {
            if (!payload || typeof payload !== "object") {
                this.notifyWarning("无效的服务端命令");
                return false;
            }

            const commandName = Utils.safeText(payload.command, 50).toLowerCase();
            const args = payload.args && typeof payload.args === "object" ? payload.args : {};
            if (!commandName) {
                this.notifyWarning("服务端命令为空");
                return false;
            }

            const handler = this._serverCommandHandlers[commandName];
            if (!handler) {
                this.notifyWarning(`不支持的服务端命令: ${commandName}`);
                return false;
            }

            handler(args);
            return true;
        } catch (err) {
            console.error("executeServerCommand failed:", err);
            this.notifyError("服务端命令执行失败");
            return false;
        }
    },

    normalizeControlTarget(target) {
        const value = Utils.safeText(target, 120);
        if (!value) return "";
        const lower = value.toLowerCase();
        if (lower.startsWith("field:") || lower.startsWith("controlid:")) {
            return value;
        }
        return `field:${value}`;
    },

    getControlState(target) {
        const key = this.normalizeControlTarget(target);
        if (!key) return null;
        return this._controlState[key] || null;
    },

    setControl(args = {}) {
        const items = Array.isArray(args?.items) ? args.items : [];
        if (!items.length) return false;

        const patchedTargets = [];

        for (const patch of items) {
            const key = this.normalizeControlTarget(patch?.target);
            if (!key) continue;
            patchedTargets.push(key);

            const current = this._controlState[key] || {};
            const merged = { ...current };

            if (typeof patch.enabled === "boolean") merged.enabled = patch.enabled;
            if (typeof patch.visible === "boolean") merged.visible = patch.visible;
            if (Object.prototype.hasOwnProperty.call(patch, "data")) merged.data = patch.data;
            if (Object.prototype.hasOwnProperty.call(patch, "value")) merged.value = patch.value;

            this._controlState[key] = merged;

            if (Object.prototype.hasOwnProperty.call(patch, "value")) {
                const msg = {
                    __eleSetControlValue: true,
                    target: key,
                    value: patch.value
                };
                try { window.postMessage(msg, "*"); } catch {}
            }
        }

        try {
            window.postMessage({
                __eleControlPatched: true,
                targets: patchedTargets
            }, "*");
        } catch {}

        return true;
    }
};

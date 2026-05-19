export function initUploadState(form, vueApi) {
    const { ref } = vueApi;
    form.showViewer = ref(false);
    form.previewUrl = ref('');
    form.previewList = ref([]);
    form.previewIndex = ref(0);
}

export const uploadMethods = {
    resolveUploadFileUrl(value) {
        if (!value) return '';

        if (typeof value === 'object') {
            if (typeof value.url === 'string' && value.url.trim()) return value.url.trim();
            return '';
        }

        if (typeof value !== 'string') return '';
        const text = value.trim();
        if (!text) return '';

        if (text.startsWith('{')) {
            try {
                const parsed = JSON.parse(text);
                if (parsed && typeof parsed === 'object') {
                    if (typeof parsed.url === 'string' && parsed.url.trim()) return parsed.url.trim();
                    // Client-side payload has only name/type/data, and cannot be previewed by server viewer.
                    if (typeof parsed.data === 'string' && parsed.data.trim()) return '';
                }
            } catch {
                // fallback below
            }
        }

        if (text.startsWith('data:')) return '';
        if (text.startsWith('~/')) return `/${text.substring(2).replace(/^\/+/, '')}`;
        return text;
    },

    openFileViewer(value, viewerUrlTemplate = '') {
        const fileUrl = this.resolveUploadFileUrl(value);
        if (!fileUrl) {
            EleManager.showWarning('该文件尚未上传，无法预览');
            return;
        }

        let targetUrl = String(viewerUrlTemplate || '').trim();
        if (targetUrl && targetUrl.includes('{0}')) {
            targetUrl = targetUrl.replaceAll('{0}', encodeURIComponent(fileUrl));
        } else if (!targetUrl) {
            targetUrl = fileUrl;
        }

        const title = this.getUploadedFileName(value) || '文件预览';
        const manager = (window.top && window.top.EleManager) ? window.top.EleManager : window.EleManager;

        if (manager && typeof manager.openDrawer === 'function') {
            manager.openDrawer({
                title,
                url: targetUrl,
                direction: 'rtl',
                size: window.innerWidth < 768 ? '100%' : '72%',
                resizable: true,
                closeOnClickModal: false,
                destroyOnClose: true
            });
            return;
        }

        window.open(targetUrl, '_blank');
    },

    getUploadedFileName(value) {
        if (!value) return '';

        if (typeof value === 'object') {
            return value.name || value.fileName || '已选择文件';
        }

        if (typeof value !== 'string') return '';

        const text = value.trim();
        if (!text) return '';

        if (text.startsWith('{')) {
            try {
                const parsed = JSON.parse(text);
                if (parsed && typeof parsed === 'object' && parsed.name) {
                    return parsed.name;
                }
            } catch {
                // Ignore parse errors and fallback below.
            }
        }

        if (text.startsWith('data:')) {
            return '已选择文件';
        }

        const normalized = text.split('?')[0].split('#')[0];
        const idx = normalized.lastIndexOf('/');
        if (idx >= 0 && idx < normalized.length - 1) {
            const fileName = normalized.substring(idx + 1);
            try {
                return decodeURIComponent(fileName);
            } catch {
                return fileName;
            }
        }

        return text;
    },

    isFileExtensionAllowed(fileName, exts) {
        if (!exts) return true;
        const name = (fileName || '').toLowerCase();
        const dot = name.lastIndexOf('.');
        const ext = dot >= 0 ? name.substring(dot) : '';
        if (!ext) return false;

        const accepted = exts
            .split(',')
            .map(x => x.trim().toLowerCase())
            .filter(x => !!x)
            .map(x => x.startsWith('.') ? x : `.${x}`);

        if (accepted.length === 0) return true;
        return accepted.includes(ext);
    },

    buildClientFilePayload(file, dataUrl) {
        return JSON.stringify({
            __eleFileUpload: true,
            name: file?.name || '',
            type: file?.type || '',
            data: dataUrl || ''
        });
    },

    async handleClientFileUpload(event, propName, exts, maxSizeMb) {
        const file = event.target.files?.[0];
        if (!file) return;

        if (maxSizeMb && maxSizeMb > 0) {
            const maxBytes = maxSizeMb * 1024 * 1024;
            if (file.size > maxBytes) {
                EleManager.showError(`文件大小不能超过 ${maxSizeMb}MB`);
                event.target.value = '';
                return;
            }
        }

        if (!this.isFileExtensionAllowed(file.name, exts)) {
            EleManager.showError(`仅支持以下扩展名：${exts}`);
            event.target.value = '';
            return;
        }

        try {
            const reader = new FileReader();
            reader.onload = (e) => {
                const dataUrl = e.target?.result;
                if (typeof dataUrl !== 'string' || !dataUrl.startsWith('data:')) {
                    EleManager.showError('文件读取失败');
                    event.target.value = '';
                    return;
                }

                if (propName && this.form.value) {
                    this.form.value[propName] = this.buildClientFilePayload(file, dataUrl);
                }
                EleManager.showSuccess('文件已加载');
                event.target.value = '';
            };

            reader.onerror = () => {
                EleManager.showError('文件读取失败');
                event.target.value = '';
            };

            reader.readAsDataURL(file);
        } catch (e) {
            console.error('File upload error:', e);
            EleManager.showError('文件处理失败');
            event.target.value = '';
        }
    },

    handleUploadSuccess(res, file, prop) {
        if (res.code === 0) {
            if (prop && this.form.value) {
                this.form.value[prop] = res.data.url;
            }
            EleManager.showSuccess('上传成功');
        } else {
            EleManager.showError(res.msg);
        }
    },

    handlePreview(url, urlList = null, startIndex = 0) {
        const list = Array.isArray(urlList) && urlList.length > 0
            ? urlList
            : (Array.isArray(url) ? url : [url]);
        this.previewList.value = list.filter(x => !!x);
        this.previewIndex.value = Math.max(0, Math.min(startIndex || 0, this.previewList.value.length - 1));
        this.showViewer.value = this.previewList.value.length > 0;
    },

    openImageViewerTop(url, urlList = null, startIndex = 0) {
        const list = Array.isArray(urlList) && urlList.length > 0
            ? urlList
            : (Array.isArray(url) ? url : [url]);
        const cleanedList = list.filter(x => !!x);
        if (cleanedList.length === 0) return;

        const manager = (window.top && window.top.EleManager)
            ? window.top.EleManager
            : window.EleManager;

        if (manager && typeof manager.openImageViewer === 'function') {
            manager.openImageViewer(cleanedList, startIndex || 0);
            return;
        }

        this.handlePreview(url, urlList, startIndex);
    },

    getImageList(value) {
        if (Array.isArray(value)) return value.filter(x => !!x);
        if (value) return [value];
        return [];
    },

    triggerFileInput(inputId) {
        const fileInput = document.getElementById(inputId);
        if (fileInput) {
            fileInput.click();
        }
    },

    async handleClientImageUpload(event, propName, maxWidth) {
        if (!maxWidth) maxWidth = 1024;

        const file = event.target.files?.[0];
        if (!file) return;

        try {
            const reader = new FileReader();
            reader.readAsDataURL(file);

            reader.onload = (e) => {
                const img = new Image();
                img.src = e.target.result;

                img.onload = () => {
                    let width = img.width;
                    let height = img.height;

                    if (width > maxWidth) {
                        height = Math.round(height * maxWidth / width);
                        width = maxWidth;
                    }

                    const canvas = document.createElement('canvas');
                    canvas.width = width;
                    canvas.height = height;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);

                    const base64Data = canvas.toDataURL(file.type || 'image/jpeg', 0.8);
                    if (propName && this.form.value) {
                        this.form.value[propName] = base64Data;
                    }
                    EleManager.showSuccess('图片已加载');
                };

                img.onerror = () => {
                    EleManager.showError('图片加载失败');
                };
            };

            reader.onerror = () => {
                EleManager.showError('文件读取失败');
            };
        } catch (e) {
            console.error('Image upload error:', e);
            EleManager.showError('图片处理失败');
        }
    },

    async handleMultiImageUpload(event, propName, maxWidth, multiLimit) {
        if (!maxWidth) maxWidth = 1024;
        if (!multiLimit) multiLimit = 0;

        const files = event.target.files;
        if (!files || files.length === 0) return;

        const currentValue = this.form && this.form.value ? this.form.value[propName] : null;
        const currentList = this.getImageList(currentValue);
        const totalAfterUpload = currentList.length + files.length;

        if (multiLimit > 0 && totalAfterUpload > multiLimit) {
            EleManager.showError(`最多只能上传${multiLimit}个图片，当前已有${currentList.length}个`);
            event.target.value = '';
            return;
        }

        try {
            const processOneFile = (file) => new Promise((resolve) => {
                const reader = new FileReader();
                reader.onload = (e) => {
                    const img = new Image();
                    img.src = e.target.result;

                    img.onload = () => {
                        let width = img.width;
                        let height = img.height;

                        if (width > maxWidth) {
                            height = Math.round(height * maxWidth / width);
                            width = maxWidth;
                        }

                        const canvas = document.createElement('canvas');
                        canvas.width = width;
                        canvas.height = height;
                        const ctx = canvas.getContext('2d');
                        ctx.drawImage(img, 0, 0, width, height);
                        resolve(canvas.toDataURL(file.type || 'image/jpeg', 0.8));
                    };

                    img.onerror = () => resolve(null);
                };
                reader.onerror = () => resolve(null);
                reader.readAsDataURL(file);
            });

            const newImages = await Promise.all(Array.from(files).map(processOneFile));
            const appended = newImages.filter(x => !!x);
            const merged = [...currentList, ...appended];

            if (propName && this.form.value) {
                this.form.value[propName] = merged;
            }

            if (appended.length === files.length) {
                EleManager.showSuccess(`已加载${appended.length}个图片`);
            } else if (appended.length > 0) {
                EleManager.showWarning(`成功${appended.length}个，失败${files.length - appended.length}个`);
            } else {
                EleManager.showError('图片处理失败');
            }
            event.target.value = '';
        } catch (e) {
            console.error('Multi image upload error:', e);
            EleManager.showError('图片处理失败');
            event.target.value = '';
        }
    },

    async handleBeforeUpload(file, maxWidth) {
        if (!maxWidth) maxWidth = 1024;
        return new Promise((resolve) => {
            const reader = new FileReader();
            reader.readAsDataURL(file);
            reader.onload = (e) => {
                const img = new Image();
                img.src = e.target.result;
                img.onload = () => {
                    let width = img.width;
                    let height = img.height;

                    if (width > maxWidth) {
                        height = Math.round(height * maxWidth / width);
                        width = maxWidth;
                    }

                    const canvas = document.createElement('canvas');
                    canvas.width = width;
                    canvas.height = height;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);

                    canvas.toBlob((blob) => {
                        if (blob) {
                            const newFile = new File([blob], file.name, { type: file.type });
                            resolve(newFile);
                        } else {
                            resolve(file);
                        }
                    }, file.type, 0.8);
                };
                img.onerror = () => resolve(file);
            };
            reader.onerror = () => resolve(file);
        });
    }
};

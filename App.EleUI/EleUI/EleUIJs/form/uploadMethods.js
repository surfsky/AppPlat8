export function initUploadState(form, vueApi) {
    const { ref } = vueApi;
    form.showViewer = ref(false);
    form.previewUrl = ref('');
    form.previewList = ref([]);
    form.previewIndex = ref(0);
}

export const uploadMethods = {
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

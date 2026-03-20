// Encapsulates common logic for Form pages using Vue 3 + Element Plus

/***********************************************************************
 * EleForm Class
 **********************************************************************/
export class EleForm {
    constructor(defaultForm = {}, config = {}) {
        const { ref, computed } = Vue;
        this.config = config;
        
        this.form = ref({ ...defaultForm });
        this.formRef = ref(null); // Track original state
        this.originalForm = ref(null); // Track original state
        this.saving = ref(false);
        this.error = ref('');
        this.success = ref('');
        this.readOnly = ref(false);
        this.dataHandler = config.dataHandler || '?handler=Data';
        this.saveHandler = config.saveHandler || '?handler=Save';

        // Computed
        this.isDirty = computed(() => {
            if (!this.originalForm.value) return false;
            return JSON.stringify(this.form.value) !== JSON.stringify(this.originalForm.value);
        });

        // Selector Popup Logic
        this.selectorVisible = ref(false);
        this.selectorUrl = ref('');
        this.selectorTitle = ref('');
        this.selectorTargetId = ref('');
        this.selectorTargetText = ref('');
        this.selectorMulti = ref(false);
        
        // Responsive Drawer Size
        this.selectorDrawerSize = computed(() => {
            return window.innerWidth < 768 ? '100%' : '600px';
        });

        // Dynamic Options (for TreeSelect etc)
        this.options = ref({}); // Stores options data by key
        
        // Image Upload Logic
        this.showViewer = ref(false);
        this.previewUrl = ref(''); // For single image preview
        this.previewList = ref([]); // For multi-image preview
        this.previewIndex = ref(0); // start index when previewing list
    }

    // 加载表单数据
    async load() {
        const url = new URL(window.location.href);
        const id = parseInt(url.searchParams.get('id') || '0', 10);
        // Unified rule: form is read-only only when md=view.
        this.readOnly.value = (url.searchParams.get('md') || '').toLowerCase() === 'view';
        
        const params = { id };
        for (const [key, value] of url.searchParams.entries()) {
            if (key !== 'id' && key !== 'handler') {
                params[key] = value;
            }
        }

        try {
            // console.log('Loading form data from:', this.dataHandler, params);
            const res = await axios.get(this.dataHandler, { params });
            if (res.data.code === 0 || res.data.code === '0') {
                const d = res.data.data || {};
                this.form.value = { ...this.form.value, ...d };

                // ensure all Id fields are strings; this makes value comparisons
                // in TreeSelect/Select simpler because option data is also coerced below
                for (const key of Object.keys(this.form.value)) {
                    if (key.endsWith('Id')) {
                        const v = this.form.value[key];
                        if (v !== null && typeof v !== 'undefined') {
                            this.form.value[key] = v.toString();
                        }
                    }
                }

                // Handle isTop boolean conversion if needed
                if (typeof d.isTop !== 'undefined') this.form.value.isTop = !!d.isTop;
                this.sanitizeAllStaticTreeSelectValues();
                this.originalForm.value = JSON.parse(JSON.stringify(this.form.value));
            } else {
                console.error('Load failed:', res.data);
                this.error.value = res.data.msg || '加载失败';
            }
        } catch (e) {
            console.error('Load exception:', e);
            this.error.value = '加载异常: ' + (e.message || e);
        }
    }

    async close() {
        if (this.isDirty.value && !this.readOnly.value && !this.success.value) { 
            try {
                await EleManager.confirm('数据已修改，确定关闭？');
            } catch {
                return; 
            }
        }
        try { window.parent.postMessage('CloseForm', '*'); } catch {}
        try { window.top.postMessage('CloseForm', '*'); } catch {}
    }

    async save() {
        if (this.readOnly.value) return;
        
        // Client-side validation
        if (this.formRef.value) {
            try {
                await this.formRef.value.validate();
            } catch (e) {
                return;
            }
        }

        this.error.value = '';
        this.success.value = '';
        this.saving.value = true;
        try {
            const res = await axios.post(this.saveHandler, this.form.value, {
                headers: { 'RequestVerificationToken': EleManager.getCsrfToken() }
            });
            if (res.data.code === 0 || res.data.code === '0') {
                this.success.value = '保存成功';
                EleManager.showSuccess(res.data.msg || '保存成功');
                this.originalForm.value = JSON.parse(JSON.stringify(this.form.value));
                try { window.parent.postMessage('ItemSaved', '*'); } catch {}
                try { window.top.postMessage('ItemSaved', '*'); } catch {}
                try { window.parent.postMessage('AnnouncementSaved', '*'); } catch {} 
            } else {
                this.error.value = res.data.msg || '保存失败';
                EleManager.showError(res.data.msg || '保存失败');
            }
        } catch (e) {
            this.error.value = '请求失败';
        } finally {
            this.saving.value = false;
        }
    }

    // invoke generic commands that map to PageModel OnPost{Command}
    async invokeCommand(commandName) {
        if (!commandName) return;
        const name = commandName;
        
        // Special case: Save is handled differently (client-side validation + form submission)
        if (name === 'Save') return this.save();
        
        // Special case: Close/Cancel should just close without confirmation in this context
        if (name === 'Close' || name === 'Cancel') return this.close();

        // Default: POST to ?handler=CommandName with form data
        await this.postHandler(name);
    }

    /**Post to page hander 
     * @param {string} name - Handler name, e.g. "Export", "Delete"
     * Default: POST to ?handler=CommandName with form data
     * Used for: Data, Export, Delete, Update, and any custom commands
    */
    async postHandler(name) {
        this.error.value = '';
        this.success.value = '';
        this.saving.value = true;
        try {
            const res = await axios.post('?handler=' + name, this.form.value, {
                headers: { 'RequestVerificationToken': EleManager.getCsrfToken() }
            });
            if (res && (res.data && (res.data.code === 0 || res.data.code === '0'))) {
                this.success.value = res.data.msg || '操作成功';
                EleManager.showSuccess(res.data.msg || '操作成功');
            } else {
                this.error.value = (res && res.data && res.data.msg) || '操作失败';
                EleManager.showError(this.error.value);
            }
        } catch (e) {
            this.error.value = '请求失败';
        } finally {
            this.saving.value = false;
        }
    }

    messageHandler(e) {
        if (e && e.data === 'RequestSave') {
            this.save();
        }
    }

    // Open selector drawer
    openSelector(propId, propText, url, multi, title) {
        this.selectorTargetId.value = propId;
        this.selectorTargetText.value = propText;
        this.selectorUrl.value = url;
        this.selectorMulti.value = multi;
        this.selectorTitle.value = title || '选择';
        this.selectorVisible.value = true;
    }

    // Handle messages from selector drawer
    handleSelectorMessage(event) {
        if (!event.data) return;
        
        // 兼容处理：event.data 可能是 { type: ..., data: ... } 或者直接是数据（如果其他地方发送不规范）
        // 我们的 IconChooser 发送的是 { type: 'EleSelector', data: { id: '...', name: '...' } }
        const msgType = event.data.type;
        if (msgType !== 'EleSelector' && msgType !== 'user-selected') return;
        const data = event.data.data || event.data;
        
        let rows = [];
        if (Array.isArray(data)) rows = data;
        else if (data.rows) rows = data.rows; // 某些分页返回结构
        else if (data.id) rows = [data]; // 单个对象
        else if (data.data && data.data.id) rows = [data.data]; // 嵌套 data
        
        // 如果 rows 为空，可能是数据结构解析失败，打印日志
        // console.log('Selector Data:', data, 'Parsed Rows:', rows);
        if (!this.selectorVisible.value) return; 
        const keyId = this.selectorTargetId.value.charAt(0).toLowerCase() + this.selectorTargetId.value.slice(1);
        const keyText = this.selectorTargetText.value.charAt(0).toLowerCase() + this.selectorTargetText.value.slice(1);
        
        // console.log('Updating Keys:', keyId, keyText);
        if (this.selectorMulti.value) {
            this.form.value[keyId] = rows.map(r => r.id).join(','); 
            this.form.value[keyText] = rows.map(r => r.name).join(',');
        } else {
            if (rows.length > 0) {
                // 更新值
                this.form.value[keyId] = rows[0].id;
                this.form.value[keyText] = rows[0].name;
                // 强制触发响应式更新（虽然 Vue ref 应该是自动的，但有时深层属性需要）
                // 如果 keyId 和 keyText 相同（如图标选择），赋值两次没问题
            }
        }

        this.selectorVisible.value = false;
    }

    // 
    clearSelector(propId, propText) {
        const keyId = propId.charAt(0).toLowerCase() + propId.slice(1);
        const keyText = propText.charAt(0).toLowerCase() + propText.slice(1);
        this.form.value[keyId] = null;
        this.form.value[keyText] = null;
    }

    // recursively convert any object tree so that fields ending in "Id" are strings
    normalizeIds(obj) {
        if (Array.isArray(obj)) {
            return obj.map(o => this.normalizeIds(o));
        }
        if (obj && typeof obj === 'object') {
            const out = {};
            for (const k of Object.keys(obj)) {
                const v = obj[k];
                if (k.toLowerCase().endsWith('id')) {
                    out[k] = v !== null && v !== undefined ? v.toString() : v;
                } else {
                    out[k] = this.normalizeIds(v);
                }
            }
            return out;
        }
        return obj;
    }

    hasTreeValue(nodes, targetValue, idField = 'id', childrenField = 'children') {
        if (!Array.isArray(nodes) || nodes.length === 0 || targetValue === null || targetValue === undefined || targetValue === '') {
            return false;
        }

        const target = targetValue.toString();
        const walk = (list) => {
            for (const node of (list || [])) {
                if (!node || typeof node !== 'object') continue;
                const nodeValue = node[idField];
                if (nodeValue !== null && nodeValue !== undefined && nodeValue.toString() === target) {
                    return true;
                }
                const children = node[childrenField];
                if (Array.isArray(children) && children.length > 0 && walk(children)) {
                    return true;
                }
            }
            return false;
        };

        return walk(nodes);
    }

    sanitizeTreeModelValue(modelKey, treeData, idField = 'id', childrenField = 'children') {
        if (!modelKey || !this.form.value) return;
        const current = this.form.value[modelKey];
        if (current === null || current === undefined || current === '') return;

        if (!this.hasTreeValue(treeData, current, idField, childrenField)) {
            this.form.value[modelKey] = null;
        }
    }

    sanitizeAllStaticTreeSelectValues() {
        const nodes = document.querySelectorAll('[data-tree-model][data-static-items]');
        nodes.forEach(el => {
            const modelKey = el.getAttribute('data-tree-model');
            const idField = el.getAttribute('data-tree-id-field') || 'id';
            const childrenField = el.getAttribute('data-tree-children-field') || 'children';
            const json = el.getAttribute('data-static-items');
            if (!modelKey || !json) return;
            try {
                const treeData = JSON.parse(json);
                this.sanitizeTreeModelValue(modelKey, treeData, idField, childrenField);
            } catch (e) {
                console.warn('parse static tree items failed', e);
            }
        });
    }

    async fetchOptions(key, url, idField = 'id', childrenField = 'children') {
        if (this.options.value[key]) return;
        try {
            const res = await axios.get(url);
            if (res.data.code === 0 || res.data.code === '0') {
                let data = res.data.data;
                data = this.normalizeIds(data);
                this.options.value[key] = data;
                this.sanitizeTreeModelValue(key, data, idField, childrenField);
            }
        } catch (e) {
            console.error('Fetch options failed:', url, e);
        }
    }

    handleUploadSuccess(res, file, prop) {
        if (res.code === 0) {
            if (prop && this.form.value) {
                this.form.value[prop] = res.data.url;
            }
            EleManager.showSuccess('上传成功');
        } else {
            EleManager.showError(res.msg);
        }
    }

    handlePreview(url, urlList = null, startIndex = 0) {
        const list = Array.isArray(urlList) && urlList.length > 0
            ? urlList
            : (Array.isArray(url) ? url : [url]);
        this.previewList.value = list.filter(x => !!x);
        this.previewIndex.value = Math.max(0, Math.min(startIndex || 0, this.previewList.value.length - 1));
        this.showViewer.value = this.previewList.value.length > 0;
    }

    getImageList(value) {
        if (Array.isArray(value)) return value.filter(x => !!x);
        if (value) return [value];
        return [];
    }

    // Trigger file input click using HTML id
    triggerFileInput(inputId) {
        const fileInput = document.getElementById(inputId);
        if (fileInput) {
            fileInput.click();
        }
    }

    // Handle client-side image upload with resize and base64 conversion
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
                    
                    // Resize if needed
                    if (width > maxWidth) {
                        height = Math.round(height * maxWidth / width);
                        width = maxWidth;
                    }

                    const canvas = document.createElement('canvas');
                    canvas.width = width;
                    canvas.height = height;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);
                    
                    // Convert to base64 using the original file type
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
    }

    // Handle multi-file image upload with resize and base64 conversion (returns array)
    async handleMultiImageUpload(event, propName, maxWidth, multiLimit) {
        if (!maxWidth) maxWidth = 1024;
        if (!multiLimit) multiLimit = 0; // 0 means unlimited
        
        const files = event.target.files;
        if (!files || files.length === 0) return;

        const currentValue = this.form && this.form.value ? this.form.value[propName] : null;
        const currentList = this.getImageList(currentValue);
        const totalAfterUpload = currentList.length + files.length;

        // Check file count limit
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

            //
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
    }

    // Handle file upload before submission - resize image if needed
    async handleBeforeUpload(file, maxWidth) {
        if (!maxWidth) maxWidth = 1024;
        return new Promise((resolve, reject) => {
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
                            resolve(file); // Fallback
                        }
                    }, file.type, 0.8);
                };
                img.onerror = () => resolve(file);
            };
            reader.onerror = () => resolve(file);
        });
    }
}



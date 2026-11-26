/**
 * EleTable 列控制模块：表头拖动排序 + 表格列显隐设置
 *
 * 实现思路：
 * 1) prepareTableColumns()：在 Vue mount 之前，把服务端渲染（SSR）出来的
 *    <el-table-column> 收集成列描述，并改写为「数据驱动」的模板：
 *
 *      <template v-for="col in eleTableColumns" :key="col.key">
 *        <template v-if="col.key === 'ecol0'">  … 原 el-table-column 标记 … </template>
 *        <template v-if="col.key === 'ecol1'">  … </template>
 *      </template>
 *
 *    这样列的显隐（visible）与顺序都由响应式数组 eleTableColumns 决定，
 *    Element Plus 内部通过 MutationObserver 监听 .hidden-columns 子节点变化，
 *    会自动重排序/重算列宽，无需手工操作表格内部结构。
 * 2) columnMethods：运行时的拖动排序（pointer 事件，不依赖第三方拖拽库）、
 *    列显隐切换、以及 localStorage 持久化。
 */

const COLUMN_STORAGE_PREFIX = 'ele-table-columns:';
const COLUMN_KEY_CLASS_PATTERN = /^ecol\d+$/;
const TYPE_LABELS = { selection: '选择', index: '序号', expand: '展开' };

/** 读取元素属性（统一去空白，null → ''） */
function readAttr(el, name) {
    if (!el || typeof el.getAttribute !== 'function') return '';
    const value = el.getAttribute(name);
    return value === null || value === undefined ? '' : String(value).trim();
}

/** 规范化标签名（HTML 解析后自定义元素名均为小写） */
function tagNameOf(el) {
    const tag = (el && (el.localName || el.tagName)) || '';
    return String(tag).toLowerCase();
}

/** 规范化列的 fixed 属性：'' | 'left' | 'right' */
function normalizeFixed(el) {
    if (!el || typeof el.hasAttribute !== 'function' || !el.hasAttribute('fixed')) return '';
    const value = readAttr(el, 'fixed').toLowerCase();
    if (!value || value === 'true' || value === 'left') return 'left';
    if (value === 'false') return '';
    return 'right';
}

/**
 * mount 前调用：把 <el-table> 的直接子列节点改写成 v-for + v-if 结构。
 * @returns {Array|null} 列描述数组；没有找到列时返回 null
 */
export function prepareTableColumns(rootEl) {
    if (!rootEl || typeof rootEl.querySelector !== 'function') return null;

    const tableEl = rootEl.querySelector('el-table');
    if (!tableEl) return null;

    const columnEls = Array.from(tableEl.children || [])
        .filter(el => el && tagNameOf(el) === 'el-table-column');
    if (columnEls.length === 0) return null;

    const wrapper = document.createElement('template');
    wrapper.setAttribute('v-for', 'col in eleTableColumns');
    wrapper.setAttribute('v-bind:key', 'col.key');

    const columns = [];
    columnEls.forEach((el, index) => {
        const key = `ecol${index}`;
        const type = readAttr(el, 'type').toLowerCase();
        const prop = readAttr(el, 'prop');
        const fixed = normalizeFixed(el);
        const label = readAttr(el, 'label') || prop || TYPE_LABELS[type] || `列${index + 1}`;

        // 把内部 key 通过 label-class-name 带到表头 th 上，运行时即可从 DOM 反查列
        const existedLabelClass = readAttr(el, 'label-class-name');
        el.setAttribute('label-class-name', existedLabelClass ? `${existedLabelClass} ${key}` : key);

        columns.push({
            key,
            label,
            prop,
            type,
            fixed,
            // 固定列 / 选择列 / 序号列 / 展开列不参与拖动排序（Element Plus 固定列始终贴边）
            draggable: !fixed && type !== 'selection' && type !== 'index' && type !== 'expand',
            visible: true
        });

        const branch = document.createElement('template');
        branch.setAttribute('v-if', `col.key === '${key}'`);
        branch.content.appendChild(el);
        wrapper.content.appendChild(branch);
    });

    tableEl.appendChild(wrapper);
    return columns;
}

/** 初始化列状态（在 EleTable 构造函数中调用） */
export function initColumnState(table, vueApi, options = {}) {
    const { ref, computed } = vueApi || {};
    const defaults = (Array.isArray(options.columns) ? options.columns : []).map(c => ({ ...c }));

    table.enableColumnDrag = options.enableColumnDrag !== false;
    table.enableColumnSetting = options.enableColumnSetting !== false;
    table.columnStateTitle = options.columnStateTitle || '';
    table._columnDefaults = defaults;

    table.columnOptions = ref([]);
    table.eleTableColumns = computed(() => (table.columnOptions.value || []).filter(c => c.visible));

    table._columnDragState = null;
    table._columnDragTarget = null;
    table._columnDragBound = false;
    table._suppressColumnSortClickUntil = 0;

    table.loadColumnState();
}

export const columnMethods = {
    //--------------------------------------------------------------
    //  持久化（localStorage）
    //--------------------------------------------------------------
    getColumnStateStorageKey() {
        let path = '';
        try { path = (window.location && window.location.pathname) || ''; } catch (_) { path = ''; }
        return `${COLUMN_STORAGE_PREFIX}${path}#${this.columnStateTitle || ''}`;
    },

    loadColumnState() {
        const defaults = (this._columnDefaults || []).map(c => ({ ...c }));

        let saved = null;
        try {
            const raw = window.localStorage.getItem(this.getColumnStateStorageKey());
            if (raw) saved = JSON.parse(raw);
        } catch (_) {
            saved = null;
        }

        // 顺序：以保存顺序为主，代码中新增的列追加到末尾
        let ordered = defaults;
        if (saved && Array.isArray(saved.order) && saved.order.length > 0) {
            const map = new Map(defaults.map(c => [c.key, c]));
            const list = [];
            saved.order.forEach(key => {
                const item = map.get(key);
                if (item) {
                    list.push(item);
                    map.delete(key);
                }
            });
            defaults.forEach(item => {
                if (map.has(item.key)) list.push(item);
            });
            if (list.length > 0) ordered = list;
        }

        const hidden = new Set(Array.isArray(saved && saved.hidden) ? saved.hidden : []);
        let list = ordered.map(c => ({ ...c, visible: !hidden.has(c.key) }));
        // 兜底：不允许所有列都被隐藏
        if (list.length > 0 && list.every(c => !c.visible))
            list = list.map(c => ({ ...c, visible: true }));

        this.columnOptions.value = list;
    },

    saveColumnState() {
        try {
            const list = (this.columnOptions && this.columnOptions.value) || [];
            const payload = {
                order: list.map(c => c.key),
                hidden: list.filter(c => !c.visible).map(c => c.key)
            };
            window.localStorage.setItem(this.getColumnStateStorageKey(), JSON.stringify(payload));
        } catch (_) {
            // localStorage 不可用（隐私模式等）时忽略
        }
    },

    /** 重置列顺序与显隐（同时清除本地存储） */
    resetColumnSetting() {
        try { window.localStorage.removeItem(this.getColumnStateStorageKey()); } catch (_) { /* ignore */ }
        this.columnOptions.value = (this._columnDefaults || []).map(c => ({ ...c, visible: true }));
    },

    //--------------------------------------------------------------
    //  显隐
    //--------------------------------------------------------------
    toggleColumnVisible(key, visible) {
        const list = ((this.columnOptions && this.columnOptions.value) || []).map(c => ({ ...c }));
        const item = list.find(c => c.key === key);
        if (!item) return;

        const next = !!visible;
        if (!next && list.filter(c => c.visible).length <= 1) {
            try { window.EleManager && window.EleManager.showWarning && window.EleManager.showWarning('至少需要保留一列'); } catch (_) { /* ignore */ }
            return;
        }
        if (item.visible === next) return;

        item.visible = next;
        this.columnOptions.value = list;
        this.saveColumnState();
    },

    showAllColumns() {
        this.columnOptions.value = ((this.columnOptions && this.columnOptions.value) || [])
            .map(c => ({ ...c, visible: true }));
        this.saveColumnState();
    },

    //--------------------------------------------------------------
    //  表头拖动排序
    //--------------------------------------------------------------
    /** 在表格 DOM 就绪后绑定拖动事件（事件委托，表头重渲染后依然有效） */
    bindColumnDrag(tableEl) {
        if (!tableEl) return;
        this._tableEl = tableEl;
        if (this._columnDragBound) return;
        this._columnDragBound = true;

        this._onColumnPointerDown = (e) => this.onColumnHeaderPointerDown(e);
        this._onColumnPointerMove = (e) => this.onColumnHeaderPointerMove(e);
        this._onColumnPointerUp = (e) => this.onColumnHeaderPointerUp(e);
        // 拖动结束后紧跟着触发的 click 会误触发排序，这里在捕获阶段吞掉
        this._onColumnClickCapture = (e) => {
            if (Date.now() >= (this._suppressColumnSortClickUntil || 0)) return;
            e.preventDefault();
            e.stopPropagation();
            if (typeof e.stopImmediatePropagation === 'function') e.stopImmediatePropagation();
        };

        tableEl.addEventListener('pointerdown', this._onColumnPointerDown, true);
        tableEl.addEventListener('click', this._onColumnClickCapture, true);
        window.addEventListener('pointermove', this._onColumnPointerMove, true);
        window.addEventListener('pointerup', this._onColumnPointerUp, true);
        window.addEventListener('pointercancel', this._onColumnPointerUp, true);
    },

    destroyColumnDrag() {
        const tableEl = this._tableEl;
        if (tableEl) {
            try { tableEl.removeEventListener('pointerdown', this._onColumnPointerDown, true); } catch (_) { /* ignore */ }
            try { tableEl.removeEventListener('click', this._onColumnClickCapture, true); } catch (_) { /* ignore */ }
        }
        try { window.removeEventListener('pointermove', this._onColumnPointerMove, true); } catch (_) { /* ignore */ }
        try { window.removeEventListener('pointerup', this._onColumnPointerUp, true); } catch (_) { /* ignore */ }
        try { window.removeEventListener('pointercancel', this._onColumnPointerUp, true); } catch (_) { /* ignore */ }
        this._columnDragBound = false;
        this._columnDragState = null;
        this._columnDragTarget = null;
    },

    /** 多级表头（列分组）暂不支持拖动排序 */
    isGroupTableHeader() {
        try {
            const rows = this._tableEl ? this._tableEl.querySelectorAll('.el-table__header thead tr') : [];
            return rows.length > 1;
        } catch (_) {
            return false;
        }
    },

    /** 从表头 th 反查列 key（prepareTableColumns 已把 key 写入 label-class-name） */
    resolveColumnKeyFromTh(th) {
        if (!th || !th.classList) return '';
        const classes = Array.from(th.classList);
        for (const name of classes) {
            if (COLUMN_KEY_CLASS_PATTERN.test(name)) return name;
        }
        return '';
    },

    findColumnOption(key) {
        if (!key) return null;
        return ((this.columnOptions && this.columnOptions.value) || []).find(c => c.key === key) || null;
    },

    onColumnHeaderPointerDown(e) {
        if (!this.enableColumnDrag) return;
        if (typeof e.button === 'number' && e.button !== 0) return;

        const target = e.target;
        const th = target && typeof target.closest === 'function' ? target.closest('th.el-table__cell') : null;
        if (!th) return;
        if (this.isGroupTableHeader()) return;

        const key = this.resolveColumnKeyFromTh(th);
        const option = this.findColumnOption(key);
        if (!option || !option.draggable) return;

        this._columnDragState = {
            th,
            key,
            startX: Number(e.clientX) || 0,
            startY: Number(e.clientY) || 0,
            moved: false
        };
    },

    onColumnHeaderPointerMove(e) {
        const state = this._columnDragState;
        if (!state) return;

        if (!state.moved) {
            if (Math.abs((Number(e.clientX) || 0) - state.startX) < 4) return;
            state.moved = true;
            try { this._tableEl && this._tableEl.classList.add('ele-col-dragging'); } catch (_) { /* ignore */ }
            try { state.th.classList.add('ele-col-drag-source'); } catch (_) { /* ignore */ }
        }

        this.clearColumnDropIndicator();

        const hovered = typeof document.elementFromPoint === 'function'
            ? document.elementFromPoint(e.clientX, e.clientY)
            : null;
        const th = hovered && typeof hovered.closest === 'function' ? hovered.closest('th.el-table__cell') : null;
        const key = th ? this.resolveColumnKeyFromTh(th) : '';
        const option = this.findColumnOption(key);

        if (!th || !option) {
            this._columnDragTarget = null;
            return;
        }

        // 普通列：插到它的左侧或右侧
        if (option.draggable && key !== state.key) {
            const rect = th.getBoundingClientRect();
            const insertBefore = (Number(e.clientX) || 0) < (rect.left + rect.width / 2);
            th.classList.add(insertBefore ? 'ele-col-drop-before' : 'ele-col-drop-after');
            this._columnDragTarget = { key, insertBefore };
            if (typeof e.preventDefault === 'function') e.preventDefault();
            return;
        }

        // 固定列作为"边界"：拖到右侧固定列 = 移到最末，拖到左侧固定列 = 移到最前
        if (option.fixed === 'right') {
            th.classList.add('ele-col-drop-before');
            this._columnDragTarget = { mode: 'end' };
            if (typeof e.preventDefault === 'function') e.preventDefault();
            return;
        }
        if (option.fixed === 'left') {
            th.classList.add('ele-col-drop-after');
            this._columnDragTarget = { mode: 'start' };
            if (typeof e.preventDefault === 'function') e.preventDefault();
            return;
        }

        this._columnDragTarget = null;
    },

    onColumnHeaderPointerUp() {
        const state = this._columnDragState;
        const target = this._columnDragTarget;

        this._columnDragState = null;
        this._columnDragTarget = null;
        try { this._tableEl && this._tableEl.classList.remove('ele-col-dragging'); } catch (_) { /* ignore */ }
        if (state && state.th) {
            try { state.th.classList.remove('ele-col-drag-source'); } catch (_) { /* ignore */ }
        }
        this.clearColumnDropIndicator();

        if (!state || !state.moved) return;   // 未发生拖动 → 交给默认的排序点击

        this._suppressColumnSortClickUntil = Date.now() + 400;
        if (!target) return;
        if (target.mode === 'end' || target.mode === 'start') {
            this.moveColumnToEdge(state.key, target.mode === 'end');
            return;
        }
        if (target.key && target.key !== state.key)
            this.moveColumnTo(state.key, target.key, target.insertBefore);
    },

    clearColumnDropIndicator() {
        try {
            const nodes = this._tableEl
                ? this._tableEl.querySelectorAll('th.ele-col-drop-before, th.ele-col-drop-after')
                : [];
            nodes.forEach(th => th.classList.remove('ele-col-drop-before', 'ele-col-drop-after'));
        } catch (_) { /* ignore */ }
    },

    /** 把 sourceKey 列移动到 targetKey 列的左侧（insertBefore=true）或右侧 */
    moveColumnTo(sourceKey, targetKey, insertBefore = true) {
        const list = ((this.columnOptions && this.columnOptions.value) || []).slice();
        const fromIndex = list.findIndex(c => c.key === sourceKey);
        if (fromIndex < 0 || sourceKey === targetKey) return false;

        const [moved] = list.splice(fromIndex, 1);
        let targetIndex = list.findIndex(c => c.key === targetKey);
        if (targetIndex < 0) return false;
        if (!insertBefore) targetIndex += 1;

        list.splice(targetIndex, 0, moved);
        this.columnOptions.value = list;
        this.saveColumnState();
        return true;
    },

    /** 把 sourceKey 列移动到非固定列分组的最前（toEnd=false）或最末（toEnd=true） */
    moveColumnToEdge(sourceKey, toEnd = true) {
        const list = ((this.columnOptions && this.columnOptions.value) || []).slice();
        const fromIndex = list.findIndex(c => c.key === sourceKey);
        if (fromIndex < 0) return false;

        const [moved] = list.splice(fromIndex, 1);
        let insertIndex = list.length;
        if (toEnd) {
            for (let i = list.length - 1; i >= 0; i -= 1) {
                if (!list[i].fixed) {
                    insertIndex = i + 1;
                    break;
                }
            }
        } else {
            const first = list.findIndex(c => !c.fixed);
            insertIndex = first < 0 ? 0 : first;
        }

        list.splice(insertIndex, 0, moved);
        this.columnOptions.value = list;
        this.saveColumnState();
        return true;
    }
};

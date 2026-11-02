// GIS3 控件类：PopupControl / IconButtonControl

/** 通用弹出面板控件（按钮 + 下拉选项） */
export class PopupControl {
    constructor({ faIcon, title, options, currentValue, onChange }) {
        this._faIcon = faIcon;
        this._title = title;
        this._options = options;
        this._currentValue = currentValue;
        this._onChange = onChange;
    }
    onAdd(map) {
        this._map = map;
        this._container = document.createElement('div');
        this._container.className = 'maplibregl-ctrl maplibregl-ctrl-group';
        this._container.style.position = 'relative';

        const btn = document.createElement('button');
        btn.className = 'gis-ctrl-btn';
        btn.title = this._title;
        btn.innerHTML = `<i class="${this._faIcon}"></i>`;
        btn.onclick = (e) => {
            e.stopPropagation();
            const wasOpen = panel.classList.contains('gis-open');
            document.querySelectorAll('.gis-popup-panel.gis-open').forEach(p => p.classList.remove('gis-open'));
            if (!wasOpen) panel.classList.add('gis-open');
        };

        const panel = document.createElement('div');
        panel.className = 'gis-popup-panel';
        panel.innerHTML = `<div class="gis-popup-title">${this._title}</div>`;
        const row = document.createElement('div');
        row.className = 'gis-popup-row';
        this._options.forEach(opt => {
            const item = document.createElement('div');
            item.className = 'gis-popup-item' + (opt.value === this._currentValue ? ' gis-active' : '');
            item.textContent = opt.label;
            item.dataset.value = opt.value;
            item.onclick = () => {
                this._currentValue = opt.value;
                this._onChange(opt.value);
                panel.querySelectorAll('.gis-popup-item').forEach(i => i.classList.remove('gis-active'));
                item.classList.add('gis-active');
                panel.classList.remove('gis-open');
            };
            row.appendChild(item);
        });
        panel.appendChild(row);

        this._container.appendChild(btn);
        this._container.appendChild(panel);

        this._outsideHandler = (e) => {
            if (!this._container.contains(e.target)) panel.classList.remove('gis-open');
        };
        document.addEventListener('click', this._outsideHandler);

        return this._container;
    }
    onRemove() {
        document.removeEventListener('click', this._outsideHandler);
        this._container?.remove();
        this._container = undefined;
        this._map = undefined;
    }
}

/** 简单按钮控件（无弹出面板） */
export class IconButtonControl {
    constructor({ faIcon, title, onClick }) {
        this._faIcon = faIcon;
        this._title = title;
        this._onClick = onClick;
    }
    onAdd(map) {
        this._map = map;
        const container = document.createElement('div');
        container.className = 'maplibregl-ctrl maplibregl-ctrl-group';
        const btn = document.createElement('button');
        btn.className = 'gis-ctrl-btn';
        btn.title = this._title;
        btn.innerHTML = `<i class="${this._faIcon}"></i>`;
        btn.onclick = () => this._onClick();
        container.appendChild(btn);
        return container;
    }
    onRemove() {
        this._container?.remove();
        this._container = undefined;
        this._map = undefined;
    }
}

/** Toggle 按钮（点击切换图标+标题） */
export class ToggleButtonControl {
    constructor({ faIconA, titleA, faIconB, titleB, initialState, onToggle }) {
        this._iconA = faIconA; this._titleA = titleA;
        this._iconB = faIconB; this._titleB = titleB;
        this._state = !!initialState;  // true 显示 B, false 显示 A
        this._onToggle = onToggle;
    }
    onAdd(map) {
        this._map = map;
        const container = document.createElement('div');
        container.className = 'maplibregl-ctrl maplibregl-ctrl-group';
        const btn = document.createElement('button');
        btn.className = 'gis-ctrl-btn';
        btn.innerHTML = `<i class="${this._state ? this._iconB : this._iconA}"></i>`;
        btn.title = this._state ? this._titleB : this._titleA;
        btn.onclick = () => {
            this._state = !this._state;
            btn.innerHTML = `<i class="${this._state ? this._iconB : this._iconA}"></i>`;
            btn.title = this._state ? this._titleB : this._titleA;
            this._onToggle?.(this._state);
        };
        container.appendChild(btn);
        this._btn = btn;
        return container;
    }
    /** 外部强制设置状态 */
    setState(state) {
        this._state = !!state;
        if (this._btn) {
            this._btn.innerHTML = `<i class="${this._state ? this._iconB : this._iconA}"></i>`;
            this._btn.title = this._state ? this._titleB : this._titleA;
        }
    }
    onRemove() {
        this._container?.remove();
        this._container = undefined;
        this._btn = undefined;
        this._map = undefined;
    }
}
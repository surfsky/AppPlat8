// Gis3Floating - 浮动按钮组
class Gis3Floating {
    constructor(actions) {
        this.actions = actions; // { layer, terrain, scene, toolbar, compass }
    }
    init() {
        const bind = (id, fn) => {
            const el = document.getElementById(id);
            if (el) el.addEventListener('click', fn);
        };
        bind('btn-layer-toggle',  () => this.actions.layer  && this.actions.layer());
        bind('btn-terrain-toggle', () => this.actions.terrain && this.actions.terrain());
        bind('btn-scene-toggle',   () => this.actions.scene   && this.actions.scene());
        bind('btn-toolbar-toggle', () => this.actions.toolbar && this.actions.toolbar());
        bind('btn-compass-toggle', () => this.actions.compass && this.actions.compass());
    }
}
window.Gis3Floating = Gis3Floating;
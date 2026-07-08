(function () {
    function create() {
        function setDisplay(el, visible) {
            if (!el) return;
            el.style.display = visible ? '' : 'none';
        }

        function setClass(el, cls, enabled) {
            if (!el || !cls) return;
            el.classList.toggle(cls, !!enabled);
        }

        function createPointMarker(ctx) {
            const map = ctx.map;
            const gps = ctx.gps;
            if (!map || !gps || !Number.isFinite(Number(gps.lng)) || !Number.isFinite(Number(gps.lat))) return null;

            const labelText = String(ctx.labelText || '').trim();
            const title = String(ctx.title || labelText || '').trim();
            const iconPath = String(ctx.iconPath || '').trim();
            const clickable = ctx.clickable !== false;
            const showLabel = ctx.showLabel !== false && labelText;
            const markerClass = ctx.markerClass || 'geometry-point-marker';
            const labelClass = ctx.labelClass || 'geometry-point-label';
            const selectedClass = ctx.selectedClass || 'is-selected';
            const labelOffset = Array.isArray(ctx.labelOffset) ? ctx.labelOffset : [0, 14];
            const labelAnchor = ctx.labelAnchor || 'top';
            const clickHandler = typeof ctx.onClick === 'function' ? ctx.onClick : null;

            const iconEl = document.createElement('div');
            iconEl.className = markerClass;
            if (title) iconEl.title = title;
            if (iconPath) {
                const img = document.createElement('img');
                img.className = 'marker-icon';
                img.src = iconPath;
                img.alt = labelText || '点位图标';
                img.onerror = () => {
                    img.remove();
                    if (!iconEl.querySelector('.dot-fallback')) {
                        const dot = document.createElement('span');
                        dot.className = 'dot-fallback';
                        iconEl.appendChild(dot);
                    }
                };
                iconEl.appendChild(img);
            } else {
                const dot = document.createElement('span');
                dot.className = 'dot-fallback';
                iconEl.appendChild(dot);
            }

            let labelEl = null;
            if (showLabel) {
                labelEl = document.createElement('div');
                labelEl.className = labelClass;
                labelEl.textContent = labelText;
            }

            const onClick = evt => {
                evt?.stopPropagation?.();
                if (!clickable || !clickHandler) return;
                clickHandler(evt);
            };
            iconEl.addEventListener('click', onClick);
            labelEl?.addEventListener('click', onClick);

            const lngLat = [Number(gps.lng), Number(gps.lat)];
            const iconMarker = new mapboxgl.Marker({
                element: iconEl,
                anchor: ctx.iconAnchor || 'center',
                draggable: !!ctx.draggable,
                offset: ctx.iconOffset || [0, 0]
            })
                .setLngLat(lngLat)
                .addTo(map);

            const labelMarker = labelEl
                ? new mapboxgl.Marker({
                    element: labelEl,
                    anchor: labelAnchor,
                    offset: labelOffset
                })
                    .setLngLat(lngLat)
                    .addTo(map)
                : null;

            const api = {
                remove() {
                    labelMarker?.remove();
                    iconMarker.remove();
                },
                getElement() {
                    return iconEl;
                },
                getLabelElement() {
                    return labelEl;
                },
                getLngLat() {
                    return iconMarker.getLngLat();
                },
                setLngLat(value) {
                    iconMarker.setLngLat(value);
                    labelMarker?.setLngLat(value);
                    return api;
                },
                setDraggable(value) {
                    if (typeof iconMarker.setDraggable === 'function') {
                        iconMarker.setDraggable(!!value);
                    }
                    return api;
                },
                isDraggable() {
                    return typeof iconMarker.isDraggable === 'function' ? iconMarker.isDraggable() : false;
                },
                on(eventName, handler) {
                    iconMarker.on(eventName, handler);
                    return api;
                },
                setVisible(value) {
                    setDisplay(iconEl, !!value);
                    setDisplay(labelEl, !!value);
                    return api;
                },
                setSelectable(value) {
                    const enabled = !!value;
                    iconEl.style.pointerEvents = enabled ? 'auto' : 'none';
                    iconEl.style.cursor = enabled ? '' : 'default';
                    iconEl.style.opacity = enabled ? '' : '0.92';
                    if (labelEl) {
                        labelEl.style.pointerEvents = enabled ? 'auto' : 'none';
                        labelEl.style.cursor = enabled ? '' : 'default';
                        labelEl.style.opacity = enabled ? '' : '0.92';
                    }
                    return api;
                },
                setSelected(value, cssClass) {
                    const cls = cssClass || selectedClass;
                    setClass(iconEl, cls, value);
                    setClass(labelEl, cls, value);
                    return api;
                }
            };

            return api;
        }

        return {
            createPointMarker
        };
    }

    window.GisPointMarker = { create };
})();

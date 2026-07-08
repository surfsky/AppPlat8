(function () {
    function create() {
        function normalizeLabelText(value) {
            return String(value || '')
                .replace(/<br\s*\/?>/gi, '\n')
                .replace(/\/n/g, '\n')
                .replace(/\\n/g, '\n')
                .replace(/\r\n/g, '\n')
                .trim();
        }

        function getPointLabelText(item) {
            if (!item) return '';
            const id = item.id ?? item.Id ?? '';
            return normalizeLabelText(item.alias || item.Alias || item.name || item.Name || (id !== '' ? `点位${id}` : ''));
        }

        function getPointTitleText(item) {
            if (!item) return '';
            const id = item.id ?? item.Id ?? '';
            return normalizeLabelText(item.name || item.Name || item.alias || item.Alias || (id !== '' ? `点位#${id}` : ''))
                .replace(/\n+/g, ' ');
        }

        function normalizeScale(value) {
            const n = Number(value);
            if (!Number.isFinite(n) || n <= 0) return 1;
            return Math.max(0.1, Math.min(10, n));
        }

        function normalizeColor(value, fallback) {
            const text = String(value || '').trim();
            return text || fallback;
        }

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

            const labelText = normalizeLabelText(ctx.labelText || '');
            const title = normalizeLabelText(ctx.title || labelText || '').replace(/\n+/g, ' ');
            const iconPath = String(ctx.iconPath || '').trim();
            const clickable = ctx.clickable !== false;
            const showLabel = ctx.showLabel !== false && labelText;
            const markerClass = ctx.markerClass || 'geometry-point-marker';
            const labelClass = ctx.labelClass || 'geometry-point-label';
            const selectedClass = ctx.selectedClass || 'is-selected';
            const scale = normalizeScale(ctx.scale);
            const labelColor = normalizeColor(ctx.labelColor, '#0f172a');
            const labelOffset = Array.isArray(ctx.labelOffset)
                ? ctx.labelOffset.map(v => Number(v) * scale)
                : [0, 14 * scale];
            const labelAnchor = ctx.labelAnchor || 'top';
            const clickHandler = typeof ctx.onClick === 'function' ? ctx.onClick : null;
            const zIndex = Number.isFinite(Number(ctx.zIndex)) ? Number(ctx.zIndex) : null;
            const markerSize = 20 * scale;
            const dotSize = 12 * scale;
            const dotBorderWidth = Math.max(1, Math.min(3, 2 * scale));
            const labelFontSize = Math.max(10, Math.min(28, 12 * scale));

            const iconEl = document.createElement('div');
            iconEl.className = markerClass;
            if (title) iconEl.title = title;
            iconEl.style.width = `${markerSize}px`;
            iconEl.style.height = `${markerSize}px`;
            if (zIndex !== null) iconEl.style.zIndex = String(zIndex);
            if (iconPath) {
                const img = document.createElement('img');
                img.className = 'marker-icon';
                img.src = iconPath;
                img.alt = labelText || '点位图标';
                img.style.width = `${markerSize}px`;
                img.style.height = `${markerSize}px`;
                img.onerror = () => {
                    img.remove();
                    if (!iconEl.querySelector('.dot-fallback')) {
                        const dot = document.createElement('span');
                        dot.className = 'dot-fallback';
                        dot.style.width = `${dotSize}px`;
                        dot.style.height = `${dotSize}px`;
                        dot.style.borderWidth = `${dotBorderWidth}px`;
                        iconEl.appendChild(dot);
                    }
                };
                iconEl.appendChild(img);
            } else {
                const dot = document.createElement('span');
                dot.className = 'dot-fallback';
                dot.style.width = `${dotSize}px`;
                dot.style.height = `${dotSize}px`;
                dot.style.borderWidth = `${dotBorderWidth}px`;
                iconEl.appendChild(dot);
            }

            let labelEl = null;
            if (showLabel) {
                labelEl = document.createElement('div');
                labelEl.className = labelClass;
                labelEl.textContent = labelText;
                labelEl.style.fontSize = `${labelFontSize}px`;
                labelEl.style.color = labelColor;
                labelEl.style.whiteSpace = 'pre-line';
                if (zIndex !== null) labelEl.style.zIndex = String(zIndex + 1);
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

        function createGeometryPointMarker(ctx) {
            const item = ctx?.item || {};
            return createPointMarker({
                ...ctx,
                iconPath: ctx?.iconPath ?? '',
                labelText: ctx?.labelText ?? getPointLabelText(item),
                title: ctx?.title ?? getPointTitleText(item),
                scale: ctx?.scale ?? item.scale ?? item.Scale,
                labelColor: ctx?.labelColor ?? item.labelColor ?? item.LabelColor
            });
        }

        return {
            createPointMarker,
            createGeometryPointMarker,
            getPointLabelText,
            getPointTitleText,
            normalizeLabelText
        };
    }

    window.GisPointMarker = { create };
})();

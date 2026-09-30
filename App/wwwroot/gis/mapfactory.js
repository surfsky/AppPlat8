/**
 * GIS 地图公共工厂：统一底图注册表、默认样式、天地图/Mapbox/高德 Key 管理与地图实例化。
 *
 * 使用方式（两步）：
 *   1) 先设置后端下发的 Key（通常放在页面 <script> 里，最先执行）：
 *        MapFactory.setKeys({
 *            mapboxKey:   "@SiteConfig.Instance.MapboxKey",
 *            tiandituKey: "@(SiteConfig.Instance.TiandituKey ?? "")",
 *            amapKey:     "@(SiteConfig.Instance.AmapKey ?? "")"
 *        });
 *   2) 再创建地图：
 *        const map = MapFactory.createMap({
 *            container: 'map',
 *            center: [120.6034, 27.5686],
 *            zoom: 11,
 *            defaultStyle: 'TiandituStreets'   // 可选，不传时默认天地图街道
 *        });
 */
(function (global) {
    'use strict';

    var _keys = { mapboxKey: '', tiandituKey: '', amapKey: '' };

    //-----------------------------------------------------------------
    // Keys
    //-----------------------------------------------------------------
    function setKeys(keys) {
        _keys.mapboxKey   = keys.mapboxKey || '';
        _keys.tiandituKey = keys.tiandituKey || '';
        _keys.amapKey     = keys.amapKey || '';
        applyMapboxGlobalAccessToken();
        return getKeys();
    }

    function getKeys() {
        return { mapboxKey: _keys.mapboxKey, tiandituKey: _keys.tiandituKey, amapKey: _keys.amapKey };
    }
    function getTiandituKey() {
        if (_keys.tiandituKey) return _keys.tiandituKey;
        var fromWindow = typeof global.TIANDITU_KEY !== 'undefined' ? String(global.TIANDITU_KEY) : '';
        return fromWindow.trim();
    }

    function getMapboxKey() {
        if (_keys.mapboxKey) return _keys.mapboxKey;
        var fromWindow = typeof global.MAPBOX_ACCESS_TOKEN !== 'undefined' ? String(global.MAPBOX_ACCESS_TOKEN) : '';
        if (fromWindow.trim()) return fromWindow.trim();
        try {
            if (global.mapboxgl && global.mapboxgl.accessToken) return String(global.mapboxgl.accessToken).trim();
        } catch (_) { }
        return '';
    }

    function applyMapboxGlobalAccessToken() {
        mapboxgl.accessToken = _keys.mapboxKey;
        try { mapboxgl.setTelemetryEnabled(false); } catch (_) { }
    }

    //-----------------------------------------------------------------
    // Styles
    //-----------------------------------------------------------------
    function normalizeStyleKey(value) {
        return String(value || '').trim().toLowerCase();
    }

    function getBuiltInStyles() {
        return [
            { name: 'TiandituStreets',   title: '天地图街道', path: 'tianditu://streets',
              tileTemplate: 'https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              labelTemplate: 'https://t{s}.tianditu.gov.cn/cva_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cva&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              subdomains: ['0','1','2','3','4','5','6','7'], tileSize: 256, maxZoom: 18, attribution: '矢量：国家基础地理信息中心 Tianditu',
              aliases: ['tianditustreets','tianditu-streets','tianditu-vector','tianditu-vec','tianditulayers','street'] },
            { name: 'TiandituSatellite', title: '天地图遥感', path: 'tianditu://satellite',
              tileTemplate: 'https://t{s}.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=img&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              labelTemplate: 'https://t{s}.tianditu.gov.cn/cia_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cia&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              subdomains: ['0','1','2','3','4','5','6','7'], tileSize: 256, maxZoom: 18, attribution: '影像：国家基础地理信息中心 Tianditu',
              aliases: ['tianditusatellite','tianditu-satellite','tianditu-yingxiang','tianditu-img','satellite','sat'] },
            { name: 'OpenStreetMap',    title: 'OpenStreetMap', path: 'osm://default',
              tileTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png', subdomains: [], tileSize: 256, maxZoom: 19, attribution: '© OpenStreetMap contributors',
              aliases: ['osm','openstreetmap'] },
            { name: 'SatelliteStreets', title: 'Mapbox 卫星街道', path: 'mapbox://styles/mapbox/satellite-streets-v12', aliases: ['satellitestreets','satellite-streets','satellite_streets'] },
            { name: 'Streets',          title: 'Mapbox 街道', path: 'mapbox://styles/mapbox/streets-v11', aliases: ['mapbox-streets','mapbox-streets-v11','mapbox-streets-v12'] },
            { name: 'Satellite',        title: 'Mapbox 卫星', path: 'mapbox://styles/mapbox/satellite-v9', aliases: ['mapbox-satellite','mapbox-terrain'] },
            { name: 'Dark',             title: 'Mapbox 暗色', path: 'mapbox://styles/mapbox/dark-v10', aliases: ['dark'] },
            { name: 'Light',            title: 'Mapbox 明亮', path: 'mapbox://styles/mapbox/light-v10', aliases: ['light'] },
            { name: 'Outdoors',         title: 'Mapbox 户外', path: 'mapbox://styles/mapbox/outdoors-v11', aliases: ['outdoors'] },
            { name: 'Navigation',       title: 'Mapbox 导航', path: 'mapbox://styles/mapbox/navigation-v1', aliases: ['navigation','night'] }
        ];
    }

    function getAllStyles(customStyles) {
        var builtIn = getBuiltInStyles();
        if (!Array.isArray(customStyles) || customStyles.length === 0) return builtIn;
        var merged = [];
        var seen = new Set();
        customStyles.forEach(function (s) {
            if (!s || !s.name) return;
            merged.push(s);
            seen.add(normalizeStyleKey(s.name));
        });
        builtIn.forEach(function (s) {
            if (!seen.has(normalizeStyleKey(s.name))) merged.push(s);
        });
        return merged;
    }

    function pickStyle(s) {
        return {
            name: s.name,
            title: s.title || s.name,
            path: s.path || '',
            tileTemplate: s.tileTemplate || '',
            labelTemplate: s.labelTemplate || '',
            subdomains: Array.isArray(s.subdomains) && s.subdomains.length > 0 ? s.subdomains.slice() : [],
            tileSize: Number(s.tileSize) || 256,
            maxZoom: Number(s.maxZoom) || 18,
            attribution: s.attribution || ''
        };
    }

    function resolveStyle(styleKey, customStyles) {
        var key = normalizeStyleKey(styleKey);
        var styles = getAllStyles(customStyles);
        var match = styles.find(function (item) {
            return normalizeStyleKey(item.name) === key
                || normalizeStyleKey(item.path || '') === key;
        });
        if (!match) {
            match = styles.find(function (item) {
                var aliases = Array.isArray(item.aliases) ? item.aliases : [];
                return aliases.some(function (a) { return normalizeStyleKey(a) === key; });
            });
        }
        if (match) return pickStyle(match);
        if (styles.length > 0) return pickStyle(styles[0]);
        return null;
    }

    function fillTileTemplate(template, tk, defaultSubdomain) {
        if (!template) return '';
        var s = String(template);
        if (tk) s = s.replace(/\{tk\}/g, encodeURIComponent(String(tk)));
        if (s.indexOf('{s}') >= 0 && defaultSubdomain !== null && defaultSubdomain !== undefined) {
            var sub = (typeof defaultSubdomain === 'string' && defaultSubdomain.length > 0) ? defaultSubdomain : '0';
            s = s.replace(/\{s\}/g, sub);
        }
        return s;
    }


    function pickDefaultStyleName() {
        if (getTiandituKey()) return 'TiandituStreets'; // 默认底图：天地图街道
        return 'OpenStreetMap';
    }

    function buildTileStyle(styleInfo) {
        if (!styleInfo) return 'mapbox://styles/mapbox/streets-v11';
        var tk = getTiandituKey();
        var subdomains = Array.isArray(styleInfo.subdomains) && styleInfo.subdomains.length > 0 ? styleInfo.subdomains : null;
        if (styleInfo.tileTemplate) {
            var fill = function (tpl, sd) {
                var s = fillTileTemplate(tpl, tk, null);
                if (s.indexOf('{s}') >= 0 && sd) s = s.replace(/\{s\}/g, String(sd));
                return s;
            };
            var templates = subdomains
                ? subdomains.map(function (sd) { return fill(styleInfo.tileTemplate, sd); })
                : [fill(styleInfo.tileTemplate, '0')];
            var labelTemplates = styleInfo.labelTemplate
                ? (subdomains
                    ? subdomains.map(function (sd) { return fill(styleInfo.labelTemplate, sd); })
                    : [fill(styleInfo.labelTemplate, '0')])
                : [];
            var tileSize = Number(styleInfo.tileSize) || 256;
            var maxZoom = Number(styleInfo.maxZoom) || 18;
            var sources = {
                'raster-tiles': { type: 'raster', tiles: templates, tileSize: tileSize, maxzoom: maxZoom }
            };
            var layers = [{ id: 'raster-tiles', type: 'raster', source: 'raster-tiles', minzoom: 0, maxzoom: 24 }];
            if (labelTemplates.length > 0) {
                sources['raster-labels'] = { type: 'raster', tiles: labelTemplates, tileSize: tileSize, maxzoom: maxZoom };
                layers.push({ id: 'raster-labels', type: 'raster', source: 'raster-labels', minzoom: 0, maxzoom: 24 });
            }
            return {
                version: 8,
                glyphs: 'mapbox://fonts/mapbox/{fontstack}/{range}.pbf',
                sources: sources,
                layers: layers
            };
        }
        if (styleInfo.path) return styleInfo.path;
        return 'mapbox://styles/mapbox/streets-v11';
    }

    // ???
    function resolveStyleAndBuildObject(preferredKey, customStyles) {
        var styleName = preferredKey && String(preferredKey).trim() ? String(preferredKey).trim() : pickDefaultStyleName();
        var styleInfo = resolveStyle(styleName, customStyles);
        return {
            styleName: styleInfo ? styleInfo.name : styleName,
            styleInfo: styleInfo,
            styleObject: buildTileStyle(styleInfo)
        };
    }

    //-----------------------------------------------------------------
    // Tools
    //-----------------------------------------------------------------
    function normalizeCenter(center, fallback) {
        if (Array.isArray(center) && center.length >= 2) {
            var c1 = Number(center[0]);
            var c2 = Number(center[1]);
            if (isFinite(c1) && isFinite(c2)) return [c1, c2];
        }
        if (center && (center.lng !== undefined || center.lon !== undefined)) {
            var lng = Number(center.lng !== undefined ? center.lng : center.lon);
            var lat = Number(center.lat !== undefined ? center.lat : center.latitude);
            if (isFinite(lng) && isFinite(lat)) return [lng, lat];
        }
        return Array.isArray(fallback) && fallback.length >= 2 ? [Number(fallback[0]), Number(fallback[1])] : [120.6034, 27.5686];
    }

    function normalizeZoom(zoom, fallback) {
        var z = Number(zoom);
        if (isFinite(z) && z >= 0) return z;
        var fz = Number(fallback);
        if (isFinite(fz) && fz >= 0) return fz;
        return 11;
    }

    function createMap(options) {
        var opts = options || {};
        var container = opts.container || opts.mapContainerId || 'map';
        var preferredStyle = opts.defaultStyle || opts.styleName || opts.defaultStyleName || '';
        var customStyles = Array.isArray(opts.mapStyles) ? opts.mapStyles : null;
        var resolved = resolveStyleAndBuildObject(preferredStyle, customStyles);
        var center = normalizeCenter(opts.center, opts.defaultCenter);
        var zoom = normalizeZoom(opts.zoom, opts.defaultZoom);

        // setKeys 已经设置了 Mapbox Token；这里再兜底一次，保证调用方即便忘记先 setKeys 也能拿到老的 window 全局 token
        applyMapboxGlobalAccessToken();

        var mapbox = opts.mapboxgl || global.mapboxgl;
        if (!mapbox || typeof mapbox.Map !== 'function') {
            throw new Error('[MapFactory] mapbox-gl 未加载，请先引入 mapbox-gl.js');
        }

        var mapOptions = Object.assign(
            {
                container: container,
                style: resolved.styleObject,
                center: center,
                zoom: zoom
            },
            (opts.extraMapOptions && typeof opts.extraMapOptions === 'object') ? opts.extraMapOptions : {}
        );
        if (typeof opts.pitch === 'number' && !('pitch' in mapOptions)) mapOptions.pitch = opts.pitch;
        if (typeof opts.bearing === 'number' && !('bearing' in mapOptions)) mapOptions.bearing = opts.bearing;
        if (opts.projection && !('projection' in mapOptions)) mapOptions.projection = opts.projection;
        if (!('attributionControl' in mapOptions)) mapOptions.attributionControl = false;
        if (!('localIdeographFontFamily' in mapOptions)) {
            mapOptions.localIdeographFontFamily = "'Microsoft YaHei','PingFang SC','Hiragino Sans GB','Noto Sans CJK SC',sans-serif";
        }

        var map = new mapbox.Map(mapOptions);
        map.__mapMeta = {
            currentStyleName: resolved.styleName,
            currentStyleInfo: resolved.styleInfo,
            getFactoryApi: function () { return api; }
        };
        return map;
    }

    //-----------------------------------------------------------------
    // Exports
    //-----------------------------------------------------------------
    var api = {
        setKeys: setKeys,
        getKeys: getKeys,
        getBuiltInStyles: getBuiltInStyles,
        getAllStyles: getAllStyles,
        resolveStyle: resolveStyle,
        buildTileStyle: buildTileStyle,
        resolveStyleAndBuildObject: resolveStyleAndBuildObject,
        pickDefaultStyleName: pickDefaultStyleName,
        getTiandituKey: getTiandituKey,
        getMapboxKey: getMapboxKey,
        createMap: createMap
    };

    global.MapFactory = api;
})(typeof window !== 'undefined' ? window : (typeof globalThis !== 'undefined' ? globalThis : this));

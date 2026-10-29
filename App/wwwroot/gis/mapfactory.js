/**
 * GIS 地图公共工厂：统一底图注册表、默认样式、天地图/高德 Key 管理与 MapLibre 地图实例化。
 * 所有资源（maplibre-gl、glyphs、DEM）均自托管，不依赖境外 CDN。
 * mapboxKey 仅作为站点配置保留，MapLibre 不使用。
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

    // 自托管资源与可选数据源；DEM 瓦片由 Data/download_dem_tiles.py 生成，buildingTiles 为 OpenMapTiles 结构的矢量瓦片（可空）
    var _cfg = {
        glyphsBase: '/lib/maplibre-gl/glyphs/',
        demTiles: '/tiles/dem/{z}/{x}/{y}.png',
        demEncoding: 'terrarium',
        demMaxZoom: 11,
        buildingTiles: ''
    };

    function configure(cfg) {
        Object.assign(_cfg, cfg || {});
        return Object.assign({}, _cfg);
    }

    function absUrl(u) {
        if (/^[a-z][a-z0-9+.-]*:\/\//i.test(u)) return u;
        return global.location.origin + (u.charAt(0) === '/' ? '' : '/') + u;
    }

    //-----------------------------------------------------------------
    // Keys
    //-----------------------------------------------------------------
    function setKeys(keys) {
        _keys.mapboxKey   = keys.mapboxKey || '';
        _keys.tiandituKey = keys.tiandituKey || '';
        _keys.amapKey     = keys.amapKey || '';
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
        return _keys.mapboxKey;
    }

    //-----------------------------------------------------------------
    // 自定义协议与资源
    //-----------------------------------------------------------------
    var _invertReady = false;

    // invert://<url>：拉取栅格瓦片并翻转亮度（保持色相与饱和度），用于生成暗色底图
    function ensureInvertProtocol() {
        if (_invertReady || !global.maplibregl) return;
        _invertReady = true;
        global.maplibregl.addProtocol('invert', function (params, abortController) {
            var url = params.url.replace(/^invert:\/\//, '');
            return fetch(url, { signal: abortController.signal })
                .then(function (resp) {
                    if (!resp.ok) throw new Error('HTTP ' + resp.status);
                    return resp.blob();
                })
                .then(function (blob) { return createImageBitmap(blob); })
                .then(function (bmp) {
                    var canvas = new OffscreenCanvas(bmp.width, bmp.height);
                    var ctx = canvas.getContext('2d');
                    ctx.drawImage(bmp, 0, 0);
                    var img = ctx.getImageData(0, 0, bmp.width, bmp.height);
                    var d = img.data;
                    for (var i = 0; i < d.length; i += 4) {
                        var r = d[i], g = d[i + 1], b = d[i + 2];
                        var shift = 255 - (Math.max(r, g, b) + Math.min(r, g, b));
                        d[i]     = Math.max(0, Math.min(255, r + shift));
                        d[i + 1] = Math.max(0, Math.min(255, g + shift));
                        d[i + 2] = Math.max(0, Math.min(255, b + shift));
                    }
                    ctx.putImageData(img, 0, 0);
                    return canvas.convertToBlob({ type: 'image/png' });
                })
                .then(function (out) { return out.arrayBuffer(); })
                .then(function (data) { return { data: data }; });
        });
    }

    // 样式里沿用的字体名（Open Sans 等）统一重定向到自托管的 Noto Sans 字形
    function transformRequest(url, resourceType) {
        if (resourceType !== 'Glyphs') return undefined;
        var m = url.match(/\/glyphs\/([^/]+)\/([^/?]+\.pbf)/);
        if (!m) return undefined;
        var stack = /bold/i.test(decodeURIComponent(m[1])) ? 'noto-sans-bold' : 'noto-sans-regular';
        return { url: absUrl(_cfg.glyphsBase) + stack + '/' + m[2] };
    }

    function getGlyphsUrl() {
        return absUrl(_cfg.glyphsBase) + '{fontstack}/{range}.pbf';
    }

    function getTerrainSource() {
        return {
            type: 'raster-dem',
            tiles: [absUrl(_cfg.demTiles)],
            encoding: _cfg.demEncoding,
            tileSize: 256,
            maxzoom: _cfg.demMaxZoom
        };
    }

    var _demProbe = null;
    function hasDem() {
        if (!_demProbe) {
            var u = absUrl(_cfg.demTiles).replace('{z}', '0').replace('{x}', '0').replace('{y}', '0');
            _demProbe = fetch(u, { method: 'HEAD' }).then(function (r) { return r.ok; }).catch(function () { return false; });
        }
        return _demProbe;
    }

    function getBuildingSource() {
        if (!_cfg.buildingTiles) return null;
        return { type: 'vector', tiles: [absUrl(_cfg.buildingTiles)], maxzoom: 14 };
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
            { name: 'TiandituTerrain',  title: '天地图地形', path: 'tianditu://terrain',
              tileTemplate: 'https://t{s}.tianditu.gov.cn/ter_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=ter&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              labelTemplate: 'https://t{s}.tianditu.gov.cn/cta_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cta&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              subdomains: ['0','1','2','3','4','5','6','7'], tileSize: 256, maxZoom: 14, attribution: '地形：国家基础地理信息中心 Tianditu',
              aliases: ['tianditu-terrain','terrain','outdoors'] },
            { name: 'Light',            title: '明亮', path: 'tianditu://light', effect: 'light',
              tileTemplate: 'https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              labelTemplate: 'https://t{s}.tianditu.gov.cn/cva_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cva&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              subdomains: ['0','1','2','3','4','5','6','7'], tileSize: 256, maxZoom: 18, attribution: '矢量：国家基础地理信息中心 Tianditu',
              aliases: ['light'] },
            { name: 'Dark',             title: '暗色', path: 'tianditu://dark', effect: 'dark',
              tileTemplate: 'https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              labelTemplate: 'https://t{s}.tianditu.gov.cn/cva_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cva&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILECOL={x}&TILEROW={y}&TILEMATRIX={z}&tk={tk}',
              subdomains: ['0','1','2','3','4','5','6','7'], tileSize: 256, maxZoom: 18, attribution: '矢量：国家基础地理信息中心 Tianditu',
              aliases: ['dark'] }
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
            attribution: s.attribution || '',
            effect: s.effect || ''
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

    // MapLibre 仅支持 mercator / globe
    function normalizeProjection(p) {
        var k = String(p === null || p === undefined ? '' : p).trim().toLowerCase();
        return (k === 'globe' || k === '1') ? 'globe' : 'mercator';
    }

    function buildTileStyle(styleInfo, options) {
        if (!styleInfo || !styleInfo.tileTemplate) return null;
        var tk = getTiandituKey();
        var subdomains = Array.isArray(styleInfo.subdomains) && styleInfo.subdomains.length > 0 ? styleInfo.subdomains : null;
        var dark = styleInfo.effect === 'dark';
        if (dark) ensureInvertProtocol();
        var fill = function (tpl, sd) {
            var s = fillTileTemplate(tpl, tk, null);
            if (s.indexOf('{s}') >= 0 && sd) s = s.replace(/\{s\}/g, String(sd));
            return dark ? 'invert://' + s : s;
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
            'raster-tiles': { type: 'raster', tiles: templates, tileSize: tileSize, maxzoom: maxZoom, attribution: styleInfo.attribution || '' }
        };
        var baseLayer = { id: 'raster-tiles', type: 'raster', source: 'raster-tiles', minzoom: 0, maxzoom: 24 };
        if (styleInfo.effect === 'light') {
            baseLayer.paint = { 'raster-saturation': -1, 'raster-contrast': -0.1, 'raster-brightness-min': 0.08 };
        }
        var layers = [baseLayer];
        if (labelTemplates.length > 0) {
            sources['raster-labels'] = { type: 'raster', tiles: labelTemplates, tileSize: tileSize, maxzoom: maxZoom };
            layers.push({ id: 'raster-labels', type: 'raster', source: 'raster-labels', minzoom: 0, maxzoom: 24 });
        }
        return {
            version: 8,
            glyphs: getGlyphsUrl(),
            projection: { type: normalizeProjection(options && options.projection) },
            sources: sources,
            layers: layers
        };
    }

    function resolveStyleAndBuildObject(preferredKey, customStyles, options) {
        var styleName = preferredKey && String(preferredKey).trim() ? String(preferredKey).trim() : pickDefaultStyleName();
        var styleInfo = resolveStyle(styleName, customStyles);
        return {
            styleName: styleInfo ? styleInfo.name : styleName,
            styleInfo: styleInfo,
            styleObject: buildTileStyle(styleInfo, options)
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
        var resolved = resolveStyleAndBuildObject(preferredStyle, customStyles, { projection: opts.projection });
        var center = normalizeCenter(opts.center, opts.defaultCenter);
        var zoom = normalizeZoom(opts.zoom, opts.defaultZoom);

        var maplibre = opts.maplibregl || global.maplibregl;
        if (!maplibre || typeof maplibre.Map !== 'function') {
            throw new Error('[MapFactory] maplibre-gl 未加载，请先引入 /lib/maplibre-gl/maplibre-gl.js');
        }

        var mapOptions = Object.assign(
            {
                container: container,
                style: resolved.styleObject,
                center: center,
                zoom: zoom,
                transformRequest: transformRequest,
                maxPitch: 85
            },
            (opts.extraMapOptions && typeof opts.extraMapOptions === 'object') ? opts.extraMapOptions : {}
        );
        if (typeof opts.pitch === 'number' && !('pitch' in mapOptions)) mapOptions.pitch = opts.pitch;
        if (typeof opts.bearing === 'number' && !('bearing' in mapOptions)) mapOptions.bearing = opts.bearing;
        if (!('attributionControl' in mapOptions)) mapOptions.attributionControl = false;
        if (!('localIdeographFontFamily' in mapOptions)) {
            mapOptions.localIdeographFontFamily = "'Microsoft YaHei','PingFang SC','Hiragino Sans GB','Noto Sans CJK SC',sans-serif";
        }

        var map = new maplibre.Map(mapOptions);
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
        configure: configure,
        normalizeProjection: normalizeProjection,
        getTerrainSource: getTerrainSource,
        hasDem: hasDem,
        getBuildingSource: getBuildingSource,
        createMap: createMap
    };

    global.MapFactory = api;
})(typeof window !== 'undefined' ? window : (typeof globalThis !== 'undefined' ? globalThis : this));

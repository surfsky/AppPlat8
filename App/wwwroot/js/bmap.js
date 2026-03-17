/**
百度地图相关方法封装，以及示例代码。
仅收录常用的百度地图方法，复杂的请直接参考百度API文档：
    http://developer.baidu.com/map/jsdemo.htm#a1_2
    http://lbsyun.baidu.com/cms/jsapi/class/jsapi_reference.html#a0b0
使用
    <script type="text/javascript" src="http://api.map.baidu.com/api?v=2.0&ak=A60cece53af65e45e359f578c9e70d32"></script>
    <script type="text/javascript" src="/res/js/bmap.js"></script>
    var map = new BMap.Map("fullMap");
    map.centerAndZoom(point, 15);
History:
    2017-08-28 INIT
    2017-08-30 add getCity()
    2017-10-23 删除默认参数。经测试，某些浏览器不支持js方法的默认参数，故都取消了。
 */

//-------------------------------------------------
// 地图基本方法（供参考）
//-------------------------------------------------
// 异步加载地图
// window.onload = loadMapScript;
function loadBMapScript() {
    var script = document.createElement("script");
    script.type = "text/javascript";
    script.src = "http://api.map.baidu.com/api?v=2.0&ak=A60cece53af65e45e359f578c9e70d32&callback=init";
    document.body.appendChild(script);
}

// 创建地图
function createMap(div, x, y, scale) {
    var map = new BMap.Map("allmap");
    map.centerAndZoom(new BMap.Point(x, y), scale);
    map.enableScrollWheelZoom(true);
    map.disableDragging();
    return map;
}

// 放大倍数
function setZoom(map, n) {
    map.setZoom(map.getZoom() + n);
}

// 定位到城市
function setCity(city) {
    map.centerAndZoom(city, 15);
}

// 定位到GPS位置
function setPosition(point) {
    map.centerAndZoom(point);
}

// 获取当前城市
function getCity() {
    return new BMap.LocalCity();
}


//-------------------------------------------------
// 定位相关
//-------------------------------------------------
// 定位到当前位置
function setCurrentLocation(map) {
    var geolocation = new BMap.Geolocation();
    geolocation.getCurrentPosition(function (r) {
        if (this.getStatus() == BMAP_STATUS_SUCCESS) {
            var marker = new BMap.Marker(r.point);
            map.addOverlay(marker);
            map.panTo(r.point);
            marker.setAnimation(BMAP_ANIMATION_BOUNCE);
            return marker;
        }
        else {
            alert('failed' + this.getStatus());
        }
    }, { enableHighAccuracy: true })
}

/*
GPS -> ADDR
result
    Point point;                            // 坐标点
    string address;                         // 地址文本
    AddressComponent addressComponents;     // 地址结构体
    Array<LocalResultPoi> surroundingPois;  // 附近的poi点
    string business;                        // 归属商圈
*/
function getAddr(point, callback) {
    new BMap.Geocoder().getLocation(point, callback);
}

// Addr -> GPS
function getGPS(city, addr, callback) {
    new BMap.Geocoder().getPoint(addr, callback, city);
}

//-------------------------------------------------
// Marker 标注相关（覆盖物的一种）
//-------------------------------------------------
// 创建标注
function addMarker(map, point) {
    var marker = new BMap.Marker(point);
    map.addOverlay(marker);
    return marker;
}

// 给标签添加文本
function addMarkerLaber(marker, text, textColor) {
    var point = marker.getPosition();
    var label = new BMap.Label(text, { position: point, offset: new BMap.Size(30, -30) });
    label.setStyle({
        color: color,
        fontSize: "12px",
        height: "20px",
        lineHeight: "20px",
        fontFamily: "微软雅黑"
    });
    marker.addLaber(label);
    return label;
}

// 创建文本标注对象
function addMarkerText(map, point, text, color) {
    var label = new BMap.Label(text, { position: point, offset: new BMap.Size(30, -30) });
    label.setStyle({
        color: color,
        fontSize: "12px",
        height: "20px",
        lineHeight: "20px",
        fontFamily: "微软雅黑"
    });
    map.addOverlay(label);
    return label;
}

// 创建标注（带文本，点击事件）
function addMarkerWithText(map, point, text, color, clickEvent) {
    var marker = addMarker(map, point);
    var label = addMarkerLabel(marker, text);
    marker.addEventListener("click", clickEvent);
    return marker;
}

// 添加图标标注
// http://lbsyun.baidu.com/jsdemo/img/fox.gif, new BMap.Size(300, 157)
function addMarkerIcon(map, point, iconUrl, iconSize) {
    var myIcon = new BMap.Icon(iconUrl, iconSize);
    var marker = new BMap.Marker(point, { icon: myIcon });  // 创建标注
    map.addOverlay(marker);
    return marker;
}

// 添加水滴形标注
function addMarkerSymbol(map, point) {
    var marker = new BMap.Marker(
        new BMap.Point(point.lng, point.lat - 0.03), {
            icon: new BMap.Symbol(BMap_Symbol_SHAPE_POINT, {
                scale: 1,//图标缩放大小
                fillColor: "red",//填充颜色
                fillOpacity: 0.8//填充透明度
            })
        });
    map.addOverlay(marker);
    return marker;
}

// 创建聚合标签
function addMarkerCluster(map, points) {
    var markers = [];
    for (var i = 0; i < points.length; i++)
        markers.push(new BMap.Marker(points[i]));
    var markerClusterer = new BMapLib.MarkerClusterer(map, { markers: markers });
    return markerClusterer;
}

//-------------------------------------------------
// 其它覆盖物（此处都统一成为Marker）
//-------------------------------------------------
// 创建点
function addShapePoint(map) {
    var marker = new BMap.Marker(new BMap.Point(116.404, 39.915));
    map.addOverlay(marker);
    return marker;
}

// 创建折线
/*
var points = [
        new BMap.Point(116.399, 39.910),
        new BMap.Point(116.405, 39.920),
        new BMap.Point(116.425, 39.900)
    ];
*/
function addShapePolyline(map, points, borderColor) {
    var polyline = new BMap.Polyline(points, { strokeColor: borderColor, strokeWeight: 2, strokeOpacity: 0.5 });
    map.addOverlay(polyline);
    return polyline;
}

// 创建圆
function addShapeCircle(map, point, radius, borderColor) {
    var circle = new BMap.Circle(point, radius, { strokeColor: borderColor, strokeWeight: 2, strokeOpacity: 0.5 });
    map.addOverlay(circle);
    return circle;
}

/*
创建多边形
var points = [
        new BMap.Point(116.387112, 39.920977),
        new BMap.Point(116.385243, 39.913063),
        new BMap.Point(116.394226, 39.917988),
        new BMap.Point(116.401772, 39.921364),
        new BMap.Point(116.41248, 39.927893)
    ];
*/
function addShapePolygon(map, points, borderColor) {
    var polygon = new BMap.Polygon(points, { strokeColor: borderColor, strokeWeight: 2, strokeOpacity: 0.5 });
    map.addOverlay(polygon);
    return polygon;
}

// 创建矩形
// var pStart = new BMap.Point(116.392214, 39.918985);
// var pEnd = new BMap.Point(116.41478, 39.911901);
function addShapeRectangle(map, pStart, pEnd, borderColor) {
    var rectangle = new BMap.Polygon([
        new BMap.Point(pStart.lng, pStart.lat),
        new BMap.Point(pEnd.lng, pStart.lat),
        new BMap.Point(pEnd.lng, pEnd.lat),
        new BMap.Point(pStart.lng, pEnd.lat)
    ], { strokeColor: borderColor, strokeWeight: 2, strokeOpacity: 0.5 });
    map.addOverlay(rectangle);
    return rectangle;
}

// 创建行政区域边界。如"北京市海淀区"。
function addShapeRegion(regionName, borderColor) {
    var bdary = new BMap.Boundary();
    bdary.get(regionName, function (rs) {
        // 获取行政区域（包括多个多边形）
        // arr[0] = "x1, y1; x2, y2; x3, y3; ..." arr[1] = "x1, y1; x2, y2; x3, y3; ..." arr[2] = "x1, y1; x2, y2; ..."
        map.clearOverlays();        //清除地图覆盖物
        var count = rs.boundaries.length;
        if (count === 0) {
            alert('未能获取当前输入行政区域');
            return;
        }

        // 生成多个多边形
        var pointArray = [];
        for (var i = 0; i < count; i++) {
            var ply = new BMap.Polygon(rs.boundaries[i], { strokeWeight: 2, strokeColor: borderColor });
            map.addOverlay(ply);
            pointArray = pointArray.concat(ply.getPath());
        }

        // 调整视野到中央
        map.setViewport(pointArray);
    });
}

// 西南角和东北角
// var SW = new BMap.Point(116.29579, 39.837146);
// var NE = new BMap.Point(116.475451, 39.9764);
// http://lbsyun.baidu.com/jsdemo/img/si-huan.png
function addImage(map, imageUrl, southWestPt, northEastPt) {
    var groundOverlay = new BMap.GroundOverlay(
        new BMap.Bounds(southWestPt, northEastPt), {
            opacity: 1,
            displayOnMinLevel: 10,
            displayOnMaxLevel: 14
        });
    groundOverlay.setImageURL(imageUrl);
    map.addOverlay(groundOverlay);
    return groundOverlay;
}

// 删除单个覆盖物
function deleteOverlay(map, overlay) {
    map.removeOverlay(overlay);
}

// 清除所有覆盖物
function deleteOverlays(map) {
    map.clearOverlays();
}

// 遍历打印覆盖物（带label）
function printOverlays(map) {
    var overlays = map.getOverlays();
    for (var i = 0; i < overlays.length - 1; i++) {
        var txt = overlays[i].getLabel().content;
        console.log(txt);
    }
}


//-------------------------------------------------
// 控件相关（浮于地图上方，固定不动的控件）
//-------------------------------------------------
// 增加地图类型控件（地图、卫星、三维）
function addControlMapType(map) {
    var control1 = new BMap.MapTypeControl({ mapTypes: [BMAP_NORMAL_MAP, BMAP_HYBRID_MAP] });
    var control2 = new BMap.MapTypeControl({ anchor: BMAP_ANCHOR_TOP_LEFT });
    map.addControl(control1);
    map.addControl(control12);
}

// 添加导航控件
function addControlNavigation(map) {
    var navigationControl = new BMap.NavigationControl({
        anchor: BMAP_ANCHOR_TOP_LEFT,              // 靠左上角位置
        type: BMAP_NAVIGATION_CONTROL_LARGE,       // LARGE类型
        enableGeolocation: true                    // 启用显示定位
    });
    map.addControl(navigationControl);
    return navigationControl;
}

// 添加定位控件
function addControlGeolocation(map, locatedEvent) {
    var geolocationControl = new BMap.GeolocationControl({
        showAddressBar: false,
        anchor: BMAP_ANCHOR_BOTTOM_RIGHT
        //icon : new Icon(iconUrl, size);
    });
    geolocationControl.addEventListener("locationSuccess", function (e) {
        locatedEvent(e.point, e.addressComponent);
    });
    geolocationControl.addEventListener("locationError", function (e) {
        alert(e.message);
    });
    map.addControl(geolocationControl);
    return geolocationControl;
}



// 添加自定义控件
function addControlCustom(map, text, anchor, clickEvent) {
    // 创建div
    function createDiv(text) {
        var div = document.createElement("div");
        div.appendChild(document.createTextNode(text));
        div.style.cursor = "pointer";
        div.style.border = "1px solid gray";
        div.style.backgroundColor = "white";
        return div;
    }
    return addControlCustom(map, createDiv(text), anchor, clickEvent);
}
function addControlCustom(map, element, anchor, clickEvent) {
    // 定义一个控件类
    function MyControl() {
        this.defaultAnchor = anchor == null ? BMAP_ANCHOR_TOP_LEFT : anchor;
        this.defaultOffset = new BMap.Size(10, 10);
    }
    MyControl.prototype = new BMap.Control();
    MyControl.prototype.initialize = function (map) {
        element.onclick = clickEvent;
        map.getContainer().appendChild(element);
        return element;
    }

    // 创建控件,添加到地图当中
    var ctrl = new MyControl();
    map.addControl(ctrl);
    return ctrl;
}

// 添加城市列表控件
function addControlCity(map, anchor) {
    var size = new BMap.Size(10, 20);
    map.addControl(new BMap.CityListControl({
        anchor: anchor == null ? BMAP_ANCHOR_TOP_LEFT : anchor,
        offset: size,
        // 切换城市之间事件
        // onChangeBefore: function(){
        //    alert('before');
        // },
        // 切换城市之后事件
        // onChangeAfter:function(){
        //   alert('after');
        // }
    }));
}



//-------------------------------------------------
// 其它
//-------------------------------------------------
// 创建右键菜单
function addContextMenu(marker, text, clickEvent) {
    var menu = new BMap.ContextMenu();
    menu.addItem(new BMap.MenuItem(text, clickEvent));
    marker.addContextMenu(menu);
    return menu;
}


//-------------------------------------------
// DOM 操作辅助类
// 2023-11 SURFSKY
//-------------------------------------------
var dom = new function () {
    //-------------------------------------------
    // Element, Attribute, Event
    //-------------------------------------------
    // 获取元素
    function getEl(cssSelector = '', id = '') {
        if (id != '')          return document.getElementById(id);
        if (cssSelector != '') return document.querySelector(cssSelector);
    }
    // 获取元素列表
    function getEls(name = '', className = '', tagName = '', cssSelector = '') {
        if (name != '')        return document.getElementsByName(name);
        if (className != '')   return document.getElementsByClassName(className);
        if (tagName != '')     return document.getElementsByTagName(tagName);
        if (cssSelector != '') return document.querySelectorAll(cssSelector);
    }


    // 属性及样式
    function getAttr(el, name)         { return el.name; }
    function setAttr(el, name, value)  { el.name = value; }
    function getStyle(el, name)        { return el.style.name; }
    function setStyle(el, name, value) { el.style.name = value; }

    // 事件
    function on(el, eventName, handler) {
        el.addEventLisenter(eventName, handler);
    }

    //-------------------------------------------
    // Utils
    //-------------------------------------------
    // 获取根对象
    function getRoot(item) {
        if (item.parent != 'undefined')
            return getRoot(item.parent);
    }

    //-------------------------------------------
    // Full Screen
    //-------------------------------------------
    // 切换全屏(ie8不支持)
    function switchFullScreen() {
        if (isFullscreen())
            exitFullScreen();
        else
            enterFullScreen();
    }

    // 是否全屏
    function isFullscreen() {
        return document.fullscreenElement ||
            document.msFullscreenElement ||
            document.mozFullScreenElement ||
            document.webkitFullscreenElement ||
            false;
    }
    //进入全屏
    function enterFullScreen() {
        var de = document.documentElement;
        if (de.requestFullscreen)            de.requestFullscreen();
        else if (de.mozRequestFullScreen)    de.mozRequestFullScreen();
        else if (de.webkitRequestFullScreen) de.webkitRequestFullScreen();
        else if (de.msRequestFullscreen)     de.msRequestFullscreen();
    }
    //退出全屏
    function exitFullScreen() {
        var de = document;
        if (de.exitFullscreen)               de.exitFullscreen();
        else if (de.mozCancelFullScreen)     de.mozCancelFullScreen();
        else if (de.webkitCancelFullScreen)  de.webkitCancelFullScreen();
        else if (de.msExitFullscreen)        de.msExitFullscreen();
    }


}();

//-------------------------------------------
// String
//-------------------------------------------
// 字符串格式化。eg："hello {0}".format("Kevin")
String.prototype.format = function () {
    var str = this;
    for (var i = 0; i < arguments.length; i++) {
        str = str.replace(eval("/\\{" + i + "\\}/g"), arguments[i]);
    }
    return str;
}



//-------------------------------------------
// 语言特性
//-------------------------------------------
// 获取属性
function getProperty(o, propertyName) {
    if (o.hasOwnProperty(propertyName))
        return o.propertyName;
    return null;
}




//-------------------------------------------
// IO
//-------------------------------------------
// 根据相对路径获取绝对路径
function getPath(relativePath, absolutePath) {
    var reg = new RegExp("\\.\\./", "g");
    var uplayCount = 0;     // 相对路径中返回上层的次数。
    var m = relativePath.match(reg);
    if (m) uplayCount = m.length;

    var lastIndex = absolutePath.length - 1;
    for (var i = 0; i <= uplayCount; i++) {
        lastIndex = absolutePath.lastIndexOf("/", lastIndex);
    }
    return absolutePath.substr(0, lastIndex + 1) + relativePath.replace(reg, "");
}      



//-------------------------------------------
// Utils
//-------------------------------------------
// 生成 GUID
function newGuid() {
    var guid = "";
    for (var i = 1; i <= 32; i++) {
        var n = Math.floor(Math.random() * 16.0).toString(16);
        guid += n;
        if ((i == 8) || (i == 12) || (i == 16) || (i == 20))
            guid += "-";
    }
    return guid;
}

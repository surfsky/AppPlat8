using Microsoft.AspNetCore.Http;
using System.ComponentModel;
using System;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using App.Components;
using App.HttpApi;
using App.DAL;
using App.Utils;
using App.Web;

namespace App.API
{
    [Scope("Base")]
    [Description("Test api")]
    public class Test
    {
        //----------------------------------------------
        // 测试
        //----------------------------------------------
        [HttpApi("测试-上传图片", false, AuthVerbs = "POST")]
        [HttpParam("image1", "base64字符串")]
        public static APIResult TestUpload(string image1, string image2 = "", string image3 = "")
        {
            return Uploader.SaveBase64Images("Tests", image1, image2, image3).ToResult(ExportMode.Detail, "上传成功");
        }

        [HttpApi.HttpApi("测试-生成base64图片", Type = ResponseType.ImageBase64)]
        public static object TestImage()
        {
            var o = VerifyPainter.Draw(100, 40);
            return o.ImageData ?? o.Image;
        }
        
    }
}

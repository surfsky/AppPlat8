using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Reflection;
using System.Web;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Serialization;

using SkiaSharp;

namespace App.HttpApi
{
    /// <summary>
    /// 序列化方法类
    /// </summary>
    internal class SerializeHelper
    {
        //----------------------------------------------------
        // 序列化转换
        //----------------------------------------------------
        // 转化为字符串
        public static string ToText(object obj)
        {
            return (obj == null) ? "" : obj.ToString();
        }

        // 转化为json字符串
        public static string ToJson(object obj)
        {
            if (obj == null)
                return "{}";
            else
                return System.Text.Json.JsonSerializer.Serialize(obj, HttpApiConfig.Instance.JsonOptions);
        }

        // 转化为xml（对于未知类型会转化出错，考虑用三方类库）
        public static string ToXml(object obj)
        {
            if (obj == null)
                return "";
            else
            {
                // 用自己写的xml序列化类（未完善）
                var cfg = HttpApiConfig.Instance;
                var txt = new XmlSerializer(
                    cfg.FormatLowCamel, 
                    cfg.FormatEnum, 
                    cfg.FormatDateTime, 
                    cfg.FormatIndented==Formatting.Indented
                    ).ToXml(obj);
                return txt;

                /*
                // 用 Json 转为 xml，优点是统一
                // Bug: 数组无法正确序列化（类别信息会丢失）
                */

                /*
                // 用微软官方的序列化类: 要求写一堆的[XmlInclude][XmlIgnore]等标签，对于未知的类是无能无力的，没法玩
                MemoryStream stream = new MemoryStream();
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    var xs = new System.Xml.Serialization.XmlSerializer(obj.GetType());
                    xs.Serialize(writer, obj);
                    writer.Close();
                }
                return UnicodeEncoding.UTF8.GetString(stream.GetBuffer());
                */
            }
        }

        // 转化为base64编码的图像字符串
        public static string ToImageBase64(object obj)
        {
            Bitmap img = obj as Bitmap;
            if (img == null)
                return "";
            else
            {
                MemoryStream ms = new MemoryStream();
                img.Save(ms, ImageFormat.Png);
                byte[] bytes = ms.GetBuffer();
                string str = "data:image/png;base64," + Convert.ToBase64String(bytes);
                return str;
            }
        }

        // 转化为二进制字节数组
        public static byte[] ToBinary(object obj)
        {
            if (obj == null)
                return null;
            else
            {
                //MemoryStream ms = new MemoryStream();
                //BinaryFormatter ser = new BinaryFormatter();
                //ser.Serialize(ms, obj);
                //byte[] bytes = ms.ToArray();
                //ms.Close();
                //return bytes;
                using var memoryStream = new MemoryStream();
                var ser = new DataContractSerializer(typeof(object));
                ser.WriteObject(memoryStream, obj);
                var bytes = memoryStream.ToArray();
                return bytes;
            }
        }

        // 转化为二进制图像字节数组
        public static byte[] ToImageBytes(object obj)
        {
            if (obj is byte[] bytes)
                return bytes;

            if (obj is SKBitmap skBmp)
            {
                using (var image = SKImage.FromBitmap(skBmp))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }
            else if (obj is SKImage skImg)
            {
                using (var data = skImg.Encode(SKEncodedImageFormat.Png, 100))
                {
                    return data.ToArray();
                }
            }

            Bitmap img = obj as Bitmap;
            if (img == null)
                return null;
            else
            {
                MemoryStream ms = new MemoryStream();
                img.Save(ms, ImageFormat.Png);
                bytes = ms.ToArray();
                ms.Close();
                return bytes;
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using SkiaSharp;

namespace App.Utils
{
    /// <summary>
    /// 验证码图片
    /// </summary>
    public class VerifyImage
    {
        public string Code { get; set; }
        public object Image { get; set; } // 兼容旧代码，但实际存储 byte[]
        public byte[] ImageData { get; set; }

        public VerifyImage(string code, object image)
        {
            this.Code = code;
            this.Image = image;
            if (image is byte[])
                this.ImageData = (byte[])image;
        }
    }

    /// <summary>
    /// 校验码绘制器 (SkiaSharp 实现)
    /// </summary>
    public class VerifyPainter
    {
        /// <summary>生成验证码图片</summary>
        /// <returns>验证码和图片元组对象</returns>
        public static VerifyImage Draw(int w = 80, int h = 40)
        {
            // 字符集
            char[] chars = { '2', '3', '4', '5', '6', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'M', 'N', 'P', 'R', 'S', 'T', 'W', 'X', 'Y' };
            
            // 生成验证码字符串 
            Random rnd = new Random();
            string code = string.Empty;
            for (int i = 0; i < 4; i++)
                code += chars[rnd.Next(chars.Length)];

            // 创建画布
            var info = new SKImageInfo(w, h);
            using (var surface = SKSurface.Create(info))
            {
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.White);

                // 画噪点 
                using (var paint = new SKPaint { Color = SKColors.Gray, StrokeWidth = 2 })
                {
                    for (int i = 0; i < 40; i++)
                    {
                        int x = rnd.Next(w);
                        int y = rnd.Next(h);
                        canvas.DrawPoint(x, y, paint);
                    }
                }

                // 画验证码字符串
                // 尝试加载字体，如果失败使用默认
                SKTypeface typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold);
                if (typeface == null) typeface = SKTypeface.Default;

                using (var paint = new SKPaint
                {
                    Color = SKColors.Green,
                    Typeface = typeface,
                    TextSize = 32,
                    IsAntialias = true,
                    TextAlign = SKTextAlign.Center
                })
                {
                    float charWidth = w / (code.Length + 1.0f);
                    for (int i = 0; i < code.Length; i++)
                    {
                        var c = code[i];
                        float x = (i + 1) * charWidth; // 居中分布
                        float y = h / 2 + 8; // 垂直居中稍微向下
                        
                        var angle = rnd.Next(-30, 30);
                        if (angle == 0) angle = 15;

                        canvas.Save();
                        canvas.Translate(x, y);
                        canvas.RotateDegrees(angle);
                        canvas.DrawText(c.ToString(), 0, 0, paint);
                        canvas.Restore();
                    }
                }

                // 简单的扭曲效果 (可选，SkiaSharp做扭曲比较复杂，这里先省略，或者画几条干扰线)
                // 画干扰线
                using (var paint = new SKPaint { Color = SKColors.LightGray, StrokeWidth = 1, IsAntialias = true })
                {
                    for (int i = 0; i < 3; i++)
                    {
                        canvas.DrawLine(rnd.Next(w), rnd.Next(h), rnd.Next(w), rnd.Next(h), paint);
                    }
                }

                // 导出为 PNG 数据
                using (var image = surface.Snapshot())
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    byte[] bytes = data.ToArray();
                    // 这里传入 bytes 给 Image 属性，配合我们修改过的 VerifyImage 类
                    return new VerifyImage(code, bytes);
                }
            }
        }
    }
}

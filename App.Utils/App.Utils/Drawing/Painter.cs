using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using SkiaSharp;

namespace App.Utils
{
    /// <summary>
    /// 绘图相关辅助方法 (SkiaSharp)
    /// </summary>
    public static class Painter
    {
        /// <summary>叠加绘制图标</summary>
        public static SKBitmap DrawIcon(this SKBitmap img, string iconUrl)
        {
            if (iconUrl.IsNotEmpty())
            {
                var icon = HttpHelper.GetNetworkImage(iconUrl);
                if (icon != null)
                {
                    int s = img.Width / 5;
                    using (var thumb = Painter.Thumbnail(icon, s, s))
                    {
                        var point = new SKPoint((img.Width - s) / 2, (img.Height - s) / 2);
                        img = Painter.Merge(img, thumb, 0.95f, point);
                    }
                    icon.Dispose();
                }
            }
            return img;
        }

        /// <summary>加载图片</summary>
        public static SKBitmap LoadImage(string path)
        {
            if (!File.Exists(path)) return null;
            using (var stream = File.OpenRead(path))
            {
                return SKBitmap.Decode(stream);
            }
        }

        /// <summary>绘制缩略图</summary>
        public static void Thumbnail(string sourceImagePath, string targetImagePath, int width, int? height=null)
        {
            using (var bmp = Thumbnail(sourceImagePath, width, height))
            {
                if (bmp != null)
                {
                    using (var data = bmp.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        using (var stream = File.OpenWrite(targetImagePath))
                        {
                            data.SaveTo(stream);
                        }
                    }
                }
            }
        }


        /// <summary>创建缩略图</summary>
        public static SKBitmap Thumbnail(string filePath, int w, int? h)
        {
            if (!File.Exists(filePath)) return null;
            using (var stream = File.OpenRead(filePath))
            {
                var img = SKBitmap.Decode(stream);
                if (img == null) return null;
                var result = Thumbnail(img, w, h);
                img.Dispose();
                return result;
            }
        }

        /// <summary>创建缩略图</summary>
        public static SKBitmap Thumbnail(this SKBitmap img, int width, int? height=null)
        {
            if (img == null) return null;
            // 计算图片的尺寸
            if (height == null)
                height = img.Height * width / img.Width;

            var info = new SKImageInfo(width, height.Value);
            var bmp = img.Resize(info, SKFilterQuality.High);
            return bmp;
        }

        /// <summary>
        /// 合并两张图片。第二张图片可指定不透明度以及粘贴位置。
        /// 注意：img 会被释放。
        /// </summary>
        public static SKBitmap Merge(this SKBitmap img, SKBitmap img2, float opacity, params SKPoint[] points)
        {
            if (img == null || img2 == null)
                return null;

            SKBitmap bmp = new SKBitmap(img.Width, img.Height);
            using (SKCanvas canvas = new SKCanvas(bmp))
            {
                canvas.DrawBitmap(img, 0, 0);

                using (var paint = new SKPaint())
                {
                    paint.Color = paint.Color.WithAlpha((byte)(opacity * 255));
                    
                    foreach (var pt in points)
                    {
                        canvas.DrawBitmap(img2, pt, paint);
                    }
                }
            }
            img.Dispose(); 
            return bmp;
        }


        /// <summary>图片颜色反相叠加（未完成）</summary>
        public static SKBitmap Reverse(this SKBitmap img, SKBitmap img2, params SKPoint[] points)
        {
             if (img == null || img2 == null) return null;
             
             SKBitmap bmp = new SKBitmap(img.Width, img.Height);
             using (SKCanvas canvas = new SKCanvas(bmp))
             {
                 canvas.DrawBitmap(img, 0, 0);
                 
                 using (var paint = new SKPaint())
                 {
                     foreach (var pt in points)
                     {
                         canvas.DrawBitmap(img2, pt, paint);
                     }
                 }
             }
             img.Dispose();
             return bmp;
        }

        /// <summary>旋转图片</summary>
        public static SKBitmap Rotate(this SKBitmap bmp, float angle)
        {
            int w = (int)(bmp.Width * 1.5);
            int h = (int)(bmp.Height * 1.5);
            SKBitmap returnBitmap = new SKBitmap(w, h);
            using (SKCanvas canvas = new SKCanvas(returnBitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(w / 2, h / 2);
                canvas.RotateDegrees(angle);
                canvas.Translate(-bmp.Width / 2, -bmp.Height / 2);
                canvas.DrawBitmap(bmp, 0, 0);
            }
            return returnBitmap;
        }

        /// <summary>TODO:三维贴图扭曲图片（未完成）</summary>
        public static SKBitmap Twist3D(this SKBitmap img, string model3DRes)
        {
            throw new NotImplementedException();
        }

        /// <summary>正弦扭曲图片</summary>  
        public static SKBitmap Twist(this SKBitmap img, double range = 3, double phase = 0, bool direction = false)
        {
            double PI2 = 6.283185307179586476925286766559;
            SKBitmap destBmp = new SKBitmap(img.Width, img.Height);
            
            using (SKCanvas canvas = new SKCanvas(destBmp))
            {
                canvas.Clear(SKColors.White);
            }

            double baseAxisLen = direction ? (double)destBmp.Height : (double)destBmp.Width;
            for (int i = 0; i < destBmp.Width; i++)
            {
                for (int j = 0; j < destBmp.Height; j++)
                {
                    double dx = 0;
                    dx = direction ? (PI2 * (double)j) / baseAxisLen : (PI2 * (double)i) / baseAxisLen;
                    dx += phase;
                    double dy = Math.Sin(dx);

                    int nOldX = 0, nOldY = 0;
                    nOldX = direction ? i + (int)(dy * range) : i;
                    nOldY = direction ? j : j + (int)(dy * range);

                    if (nOldX >= 0 && nOldX < img.Width && nOldY >= 0 && nOldY < img.Height)
                    {
                        var color = img.GetPixel(i, j);
                        destBmp.SetPixel(nOldX, nOldY, color);
                    }
                }
            }
            return destBmp;
        }

        public static string ToBase64(this SKBitmap image)
        {
            if (image == null) return "";
            using (var img = SKImage.FromBitmap(image))
            using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
            {
                 return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
            }
        }

        public static bool IsBase64Image(this string text)
        {
            if (text.IsNotEmpty() && text.Contains("base64"))
                return true;
            return false;
        }

        public static SKBitmap ParseImage(this string base64Image)
        {
            try
            {
                var txt = base64Image.Split(',')[1];
                var bytes = Convert.FromBase64String(txt);
                return SKBitmap.Decode(bytes);
            }
            catch
            {
                return null;
            }
        }
    }
}

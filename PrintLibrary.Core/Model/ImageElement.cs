using SkiaSharp;
using System;

namespace PrintLibrary.Model
{
    /// <summary>
    /// 图片缩放模式。
    /// </summary>
    public enum ImageScaleMode
    {
        /// <summary>拉伸：强制填满元素矩形，不保持宽高比。</summary>
        Stretch,
        /// <summary>等比缩放（Letterbox）：等比缩放至完全可见，可能留白边。</summary>
        Uniform,
        /// <summary>等比填充（Crop）：等比缩放至填满元素矩形，超出部分被裁剪。</summary>
        UniformToFill
    }

    /// <summary>
    /// 图片元素。支持从本地文件路径、字节数组或 Base64 字符串加载图片，
    /// 并以指定缩放模式绘制到标签。
    /// </summary>
    public class ImageElement : LabelElement
    {
        // ── 图片来源（三选一）────────────────────────────────────────────

        /// <summary>
        /// 本地文件路径（绝对路径或相对于运行目录的路径）。
        /// 与 <see cref="ImageBase64"/> 二选一提供。
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Base64 编码的图片数据（可直接嵌入 JSON 模板中，无需额外文件）。
        /// 支持带前缀："data:image/png;base64,xxxx" 或纯 Base64 字符串。
        /// </summary>
        public string? ImageBase64 { get; set; }

        // ── 布局 ──────────────────────────────────────────────────────────

        /// <summary>图片缩放模式，默认 Uniform（等比缩放，保持原图比例）。</summary>
        public ImageScaleMode ScaleMode { get; set; } = ImageScaleMode.Uniform;

        // ── 绘制实现 ──────────────────────────────────────────────────────

        /// <inheritdoc />
        public override void Draw(SKCanvas canvas, SKMatrix mmToPixelMatrix, PrintData data)
        {
            if (!IsVisible) return;

            // 1. 加载图片为 SKBitmap
            using var bitmap = LoadBitmap();
            if (bitmap is null) return;

            // 2. 画布已设置 mm→px 矩阵，使用毫米坐标作为目标矩形
            var dest = new SKRect(X, Y, X + Width, Y + Height);

            // 3. 图片原始尺寸（像素），需要在 mm 空间下换算显示尺寸
            //    将原图像素尺寸换算为 mm，以便在 mm 坐标系下计算缩放
            float pxPerMm = mmToPixelMatrix.ScaleX;
            float srcWmm = bitmap.Width / pxPerMm;
            float srcHmm = bitmap.Height / pxPerMm;
            var srcSize = new SKSize(srcWmm, srcHmm);

            // 4. 根据缩放模式计算实际绘制矩形（mm 坐标）
            var drawRect = CalcDrawRect(srcSize, dest, ScaleMode);

            // 5. 若是 UniformToFill 需要裁剪超出部分
            canvas.Save();
            if (ScaleMode == ImageScaleMode.UniformToFill)
                canvas.ClipRect(dest);

            // 6. 绘制：源矩形是整个位图像素，目标矩形是 mm 坐标
            var srcRect = new SKRect(0, 0, bitmap.Width, bitmap.Height);
            canvas.DrawBitmap(bitmap, srcRect, drawRect);

            canvas.Restore();
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────

        /// <summary>
        /// 从 FilePath 或 ImageBase64 加载 SKBitmap。
        /// 优先使用 Base64，其次文件路径，两者都为空时返回 null。
        /// </summary>
        private SKBitmap? LoadBitmap()
        {
            // 优先从 Base64 加载
            if (!string.IsNullOrEmpty(ImageBase64))
            {
                // 去除可能存在的 Data URL 前缀
                var base64 = ImageBase64.Contains(',')
                    ? ImageBase64.Substring(ImageBase64.IndexOf(',') + 1)
                    : ImageBase64;
                try
                {
                    var bytes = Convert.FromBase64String(base64);
                    return SKBitmap.Decode(bytes);
                }
                catch { /* 解码失败，继续尝试文件路径 */ }
            }

            // 从文件路径加载
            if (!string.IsNullOrEmpty(FilePath) && System.IO.File.Exists(FilePath))
            {
                try { return SKBitmap.Decode(FilePath); }
                catch { /* 文件读取失败 */ }
            }

            return null;
        }

        /// <summary>
        /// 根据缩放模式，计算在目标矩形中的实际绘制区域（mm 坐标）。
        /// </summary>
        private static SKRect CalcDrawRect(SKSize srcSize, SKRect dest, ImageScaleMode mode)
        {
            return mode switch
            {
                // 拉伸：直接用目标矩形
                ImageScaleMode.Stretch => dest,

                // 等比缩放：按最小比例缩放，居中，可能留白
                ImageScaleMode.Uniform => ScaleUniform(srcSize, dest, fillMode: false),

                // 等比填充：按最大比例缩放，居中，超出部分由 ClipRect 裁剪
                ImageScaleMode.UniformToFill => ScaleUniform(srcSize, dest, fillMode: true),

                _ => dest
            };
        }

        /// <summary>
        /// 等比缩放计算。
        /// <paramref name="fillMode"/> 为 true 时取较大比例（填充），为 false 时取较小比例（letterbox）。
        /// </summary>
        private static SKRect ScaleUniform(SKSize srcSize, SKRect dest, bool fillMode)
        {
            float scaleX = dest.Width  / srcSize.Width;
            float scaleY = dest.Height / srcSize.Height;
            float scale  = fillMode ? MathF.Max(scaleX, scaleY) : MathF.Min(scaleX, scaleY);

            float scaledW = srcSize.Width  * scale;
            float scaledH = srcSize.Height * scale;

            // 居中对齐
            float left = dest.Left + (dest.Width  - scaledW) / 2f;
            float top  = dest.Top  + (dest.Height - scaledH) / 2f;

            return new SKRect(left, top, left + scaledW, top + scaledH);
        }
    }
}

using PrintLibrary.Model;
using PrintLibrary.Rendering;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace PrintLibrary.Printer
{
    /// <summary>
    /// 混合 PDF 打印机。
    /// 
    /// 工作原理：
    /// 1. 使用 SkiaSharp 的高质量位图渲染（解决 PdfSharp 不支持特殊字符的问题）
    /// 2. 将渲染后的位图嵌入到 PdfSharp 生成的 PDF 中（解决 SkiaSharp PDF 体积大的问题）
    /// 
    /// 与其他方案的对比：
    /// | 方案 | 文件体积 | 文字清晰度 | 特殊字符支持 |
    /// |------|----------|------------|--------------|
    /// | PdfPrinter (SkiaSharp 向量) | 大 (10-23MB) | 无限清晰 | 依赖系统字体 |
    /// | PdfPrinter (Rasterized) | 小 (~100KB) | 取决于 DPI | 完美 |
    /// | PdfSharpPrinter | 小 (~50-200KB) | 清晰 | 部分不支持 |
    /// | HybridPdfPrinter | 小 (~100-300KB) | 清晰 | 完美 |
    /// 
    /// 适用场景：
    /// - 需要小体积 PDF 但又要完美支持所有 Unicode 字符
    /// - 跨平台部署（Linux/macOS）且不想依赖系统字体
    /// </summary>
    public class HybridPdfPrinter
    {
        /// <summary>
        /// 位图渲染 DPI。推荐 150-300，300 DPI 可满足大部分打印需求。
        /// </summary>
        public float Dpi { get; set; } = 300f;

        /// <summary>
        /// PDF 元数据：标题。
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// PDF 元数据：作者。
        /// </summary>
        public string? Author { get; set; } = "PrintLibrary";

        // ── 核心 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 将单页模板输出为 PDF 字节数组。
        /// </summary>
        public byte[] PrintToBytes(LabelTemplate template, PrintData? data = null)
        {
            return PrintToBytes(template, new List<PrintData> { data ?? new PrintData() });
        }

        /// <summary>
        /// 将多页模板输出为多页 PDF 字节数组。
        /// </summary>
        public byte[] PrintToBytes(LabelTemplate template, List<PrintData> pages)
        {
            using var ms = new MemoryStream();
            SaveToStream(template, pages, ms);
            return ms.ToArray();
        }

        /// <summary>
        /// 将单页模板输出为 PDF 文件。
        /// </summary>
        public void SaveToFile(LabelTemplate template, string filePath, PrintData? data = null)
        {
            SaveToFile(template, filePath, new List<PrintData> { data ?? new PrintData() });
        }

        /// <summary>
        /// 将多页模板输出为 PDF 文件。
        /// </summary>
        public void SaveToFile(LabelTemplate template, string filePath, List<PrintData> pages)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            if (filePath is null) throw new ArgumentNullException(nameof(filePath));

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var fs = File.Create(filePath);
            SaveToStream(template, pages, fs);
        }

        // ── 核心渲染逻辑 ──────────────────────────────────────────────────

        private void SaveToStream(LabelTemplate template, List<PrintData> pages, Stream output)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            if (pages is null) throw new ArgumentNullException(nameof(pages));

            using var document = new PdfDocument();
            document.Info.Title = Title ?? template.Name;
            document.Info.Author = Author ?? "PrintLibrary";
            document.Info.Creator = "PrintLibrary v2.0 (Hybrid)";

            // 计算位图尺寸
            var (bmpWidth, bmpHeight) = SkiaDrawingHelper.CalcBitmapSize(template.Width, template.Height, Dpi);
            var matrix = SkiaDrawingHelper.CreateMmToPixelMatrix(Dpi);

            foreach (var pageData in pages)
            {
                // 1. 使用 SkiaSharp 渲染高质量位图
                using var bitmap = RenderToBitmap(template, pageData, bmpWidth, bmpHeight, matrix);

                // 2. 将位图转换为 PdfSharp 可用的格式
                var xImage = ConvertToXImage(bitmap);

                // 3. 创建 PDF 页面并嵌入位图
                double widthPt = template.Width * 72.0 / 25.4;
                double heightPt = template.Height * 72.0 / 25.4;

                var page = document.AddPage();
                page.Width = new XUnit(widthPt, XGraphicsUnit.Point);
                page.Height = new XUnit(heightPt, XGraphicsUnit.Point);

                var gfx = XGraphics.FromPdfPage(page);

                // 绘制位图到整个页面
                gfx.DrawImage(xImage, 0, 0, widthPt, heightPt);
            }

            document.Save(output);
        }

        /// <summary>
        /// 使用 SkiaSharp 将模板渲染为高质量位图。
        /// </summary>
        private SKBitmap RenderToBitmap(LabelTemplate template, PrintData data, int width, int height, SKMatrix matrix)
        {
            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);
            canvas.SetMatrix(matrix);
            ElementRenderer.RenderTemplate(canvas, template, data, matrix);
            canvas.Flush();
            return bitmap;
        }

        /// <summary>
        /// 将 SkiaSharp 位图转换为 PdfSharp XImage。
        /// </summary>
        private XImage ConvertToXImage(SKBitmap bitmap)
        {
            // 将 SKBitmap 转换为 PNG 字节数组
            using var image = SKImage.FromBitmap(bitmap);
            using var pngData = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = pngData.ToArray();

            // PdfSharp 从 MemoryStream 创建 XImage
            return XImage.FromStream(() => new MemoryStream(bytes));
        }
    }
}
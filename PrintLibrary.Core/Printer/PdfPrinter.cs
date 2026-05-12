using PrintLibrary.Model;
using PrintLibrary.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace PrintLibrary.Printer
{
    /// <summary>
    /// 位图模式下的编码格式。
    /// </summary>
    public enum RasterEncoding
    {
        /// <summary>JPEG 有损压缩，体积小，适合照片/条码。</summary>
        Jpeg,
        /// <summary>PNG 无损压缩，体积大，文字清晰。</summary>
        Png
    }

    /// <summary>
    /// PDF 输出打印机。
    /// 使用 SkiaSharp 的 PDF 文档 API 将一个或多个模板页输出为 PDF 文件。
    /// 
    /// 用途：
    ///   - 在 Linux/macOS 上生成 PDF 后，通过系统命令（lp/lpr）发送到打印机。
    ///   - 提供打印预览的高保真留档（向量 PDF，不依赖光栅化 DPI）。
    ///   - 在 Web 服务中将标签输出为 PDF 供浏览器下载。
    /// 
    /// 实现说明：
    ///   默认使用 SkiaSharp 的向量 PDF 输出（清晰但 CJK 字体子集化较大）。
    ///   设置 <see cref="RasterDpi"/> 后，将以高分辨率位图方式渲染每页后嵌入 PDF，
    ///   文件体积大幅减小（JPEG 压缩），适合对文件大小敏感的场景。
    /// </summary>
    public class PdfPrinter
    {
        // ── 配置属性 ──────────────────────────────────────────────────────

        /// <summary>
        /// PDF 元数据：标题（可选）。
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// PDF 元数据：作者（可选）。
        /// </summary>
        public string? Author { get; set; } = "PrintLibrary";

        /// <summary>
        /// 位图渲染 DPI。设置后使用高分辨率位图渲染模式生成 PDF，
        /// 而非默认的向量模式。
        /// <para>
        /// 向量模式：文字为向量图形，清晰但 CJK 字体嵌入可能导致 PDF 体积较大（10+ MB）。
        /// 位图模式：以指定 DPI 渲染为 JPEG 后嵌入，体积小（通常几十~几百 KB），
        /// 适合屏幕浏览和文件传输；设为 null（默认）使用向量模式。
        /// </para>
        /// <para>推荐值：150~300（屏幕浏览 150，打印留档 300）。</para>
        /// </summary>
        public float? RasterDpi { get; set; } = null;

    /// <summary>
    /// 位图模式下的压缩编码格式。
    /// 默认 Jpeg（体积小，适合照片/条码），
    /// 若内容以文字为主，建议改为 Png（无损，文字清晰）。
    /// </summary>
    public RasterEncoding RasterEncoding { get; set; } = RasterEncoding.Jpeg;

    /// <summary>
    /// 位图模式下的 JPEG 压缩质量（0~100），默认 95。
    /// 仅在 <see cref="RasterEncoding"/> 为 Jpeg 时有效。
    /// </summary>
    public int JpegQuality { get; set; } = 95;

        // ── 核心 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 将单页模板输出为 PDF 字节数组。
        /// </summary>
        /// <param name="template">标签模板</param>
        /// <param name="data">页面数据（可选）</param>
        /// <returns>PDF 字节数组</returns>
        public byte[] PrintToBytes(LabelTemplate template, PrintData? data = null)
        {
            return PrintToBytes(template, new List<PrintData> { data ?? new PrintData() });
        }

        /// <summary>
        /// 将多页模板输出为多页 PDF 字节数组。
        /// 每个 <see cref="PrintData"/> 对应 PDF 中的一页。
        /// </summary>
        /// <param name="template">模板（所有页共用同一布局）</param>
        /// <param name="pages">每页数据列表</param>
        /// <returns>PDF 字节数组</returns>
        public byte[] PrintToBytes(LabelTemplate template, List<PrintData> pages)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            if (pages    is null) throw new ArgumentNullException(nameof(pages));

            using var stream = new SKDynamicMemoryWStream();
            WriteToStream(template, pages, stream);
            return stream.DetachAsData().ToArray();
        }

        /// <summary>
        /// 将单页模板输出为 PDF 文件。
        /// </summary>
        /// <param name="template">标签模板</param>
        /// <param name="filePath">输出 PDF 文件路径</param>
        /// <param name="data">页面数据（可选）</param>
        public void SaveToFile(LabelTemplate template, string filePath, PrintData? data = null)
        {
            SaveToFile(template, filePath, new List<PrintData> { data ?? new PrintData() });
        }

        /// <summary>
        /// 将多页模板输出为 PDF 文件。
        /// </summary>
        /// <param name="template">标签模板</param>
        /// <param name="filePath">输出 PDF 文件路径</param>
        /// <param name="pages">每页数据列表</param>
        public void SaveToFile(LabelTemplate template, string filePath, List<PrintData> pages)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            if (filePath  is null) throw new ArgumentNullException(nameof(filePath));

            // 确保输出目录存在
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var stream = new SKFileWStream(filePath);
            WriteToStream(template, pages, stream);
        }

        // ── 核心渲染逻辑 ──────────────────────────────────────────────────

        /// <summary>
        /// 将多页模板渲染并写入 SkiaSharp WStream。
        /// </summary>
        private void WriteToStream(LabelTemplate template, List<PrintData> pages, SKWStream stream)
        {
            if (RasterDpi.HasValue && RasterDpi.Value > 0)
            {
                WriteToStreamRasterized(template, pages, stream, RasterDpi.Value, JpegQuality);
            }
            else
            {
                WriteToStreamVector(template, pages, stream);
            }
        }

        /// <summary>
        /// 向量模式：使用 SkiaSharp 原生 PDF API，文字为向量图形。
        /// CJK 字体子集化可能较大，适合对清晰度要求高的打印场景。
        /// </summary>
        private void WriteToStreamVector(LabelTemplate template, List<PrintData> pages, SKWStream stream)
        {
            // PDF 坐标单位：磅（pt，1pt = 25.4/72 mm ≈ 0.3528 mm）
            // 模板尺寸（mm）换算为 pt
            float widthPt  = template.Width  * 72f / 25.4f;
            float heightPt = template.Height * 72f / 25.4f;

            // 构建 PDF 元数据
            var metadata = new SKDocumentPdfMetadata
            {
                Title    = Title  ?? template.Name,
                Author   = Author ?? "PrintLibrary",
                Creator  = "PrintLibrary v1.0"
            };

            // 创建 PDF 文档（坐标单位：pt）
            using var document = SKDocument.CreatePdf(stream, metadata);

            // 构造毫米→磅的变换矩阵（用于 PDF 内部坐标）
            // scale = 72/25.4 pt/mm
            float pxPerMm = 72f / 25.4f;
            var matrix = SKMatrix.CreateScale(pxPerMm, pxPerMm);

            // 逐页绘制
            foreach (var pageData in pages)
            {
                // BeginPage 返回该页的 SKCanvas，坐标单位为磅
                using var canvas = document.BeginPage(widthPt, heightPt);

                // 设置变换矩阵，使元素可以使用毫米坐标
                canvas.SetMatrix(matrix);

                // 渲染所有元素
                ElementRenderer.RenderTemplate(canvas, template, pageData, matrix);

                // 完成本页（必须调用，否则页面内容不会写入 PDF）
                document.EndPage();
            }

            // 关闭 PDF 文档（写入 EOF 等结构）
            document.Close();
        }

        /// <summary>
        /// 位图模式：将每页以高 DPI 渲染为 JPEG 位图后嵌入 PDF。
        /// 体积远小于向量模式（CJK 字体不嵌入），适合屏幕浏览和文件传输。
        /// </summary>
        private void WriteToStreamRasterized(LabelTemplate template, List<PrintData> pages, SKWStream stream, float dpi, int jpegQuality)
        {
            // PDF 页面尺寸（pt）
            float widthPt  = template.Width  * 72f / 25.4f;
            float heightPt = template.Height * 72f / 25.4f;

            var metadata = new SKDocumentPdfMetadata
            {
                Title   = Title  ?? template.Name,
                Author  = Author ?? "PrintLibrary",
                Creator = "PrintLibrary v1.0 (Raster)"
            };

            using var document = SKDocument.CreatePdf(stream, metadata);

            // 计算位图像素尺寸
            var (widthPx, heightPx) = SkiaDrawingHelper.CalcBitmapSize(template.Width, template.Height, dpi);
            var matrix = SkiaDrawingHelper.CreateMmToPixelMatrix(dpi);

            foreach (var pageData in pages)
            {
                // 1. 在内存中渲染高分辨率位图
                using var bitmap = new SKBitmap(widthPx, heightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var bitmapCanvas = new SKCanvas(bitmap);
                bitmapCanvas.SetMatrix(matrix);
                ElementRenderer.RenderTemplate(bitmapCanvas, template, pageData, matrix);
                bitmapCanvas.Flush();

                // 2. 编码为指定格式（JPEG 或 PNG）
                using var image = SKImage.FromBitmap(bitmap);
                using var encodedData = RasterEncoding == RasterEncoding.Png
                    ? image.Encode(SKEncodedImageFormat.Png, 100)
                    : image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

                // 3. 将位图嵌入 PDF 页面
                using var pdfCanvas = document.BeginPage(widthPt, heightPt);
                using var pdfBitmap = SKBitmap.Decode(encodedData);
                var destRect = new SKRect(0, 0, widthPt, heightPt);
                pdfCanvas.DrawBitmap(pdfBitmap, destRect);
                document.EndPage();
            }

            document.Close();
        }

        // ── 系统打印辅助 ──────────────────────────────────────────────────

        /// <summary>
        /// 在 Linux/macOS 上通过系统 lp 命令将 PDF 发送到打印机。
        /// 这是在非 Windows 系统上实现物理打印的推荐方式。
        /// </summary>
        /// <param name="template">要打印的模板</param>
        /// <param name="pages">每页数据</param>
        /// <param name="printerName">打印机名称（lp -d 参数），为 null 时使用默认打印机</param>
        /// <param name="copies">副本数</param>
        public void PrintViaCups(
            LabelTemplate    template,
            List<PrintData>  pages,
            string?          printerName = null,
            int              copies = 1)
        {
            // 1. 渲染为 PDF 临时文件
            string tmpFile = Path.Combine(Path.GetTempPath(), $"printlibrary_{Guid.NewGuid():N}.pdf");
            try
            {
                SaveToFile(template, tmpFile, pages);

                // 2. 构建 lp 命令参数
                var args = new System.Text.StringBuilder();
                if (!string.IsNullOrEmpty(printerName))
                    args.Append($"-d \"{printerName}\" ");
                if (copies > 1)
                    args.Append($"-n {copies} ");
                args.Append($"\"{tmpFile}\"");

                // 3. 调用系统打印命令
                var psi = new System.Diagnostics.ProcessStartInfo("lp", args.ToString())
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var process = System.Diagnostics.Process.Start(psi)
                    ?? throw new InvalidOperationException("无法启动 lp 进程。");
                process.WaitForExit(30_000); // 最多等待 30 秒

                if (process.ExitCode != 0)
                {
                    string err = process.StandardError.ReadToEnd();
                    throw new InvalidOperationException($"lp 命令执行失败（退出码 {process.ExitCode}）：{err}");
                }
            }
            finally
            {
                // 清理临时文件
                if (File.Exists(tmpFile))
                    File.Delete(tmpFile);
            }
        }
    }
}

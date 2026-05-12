using PrintLibrary.Model;
using PrintLibrary.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace PrintLibrary.Printer
{
    /// <summary>
    /// 打印机配置选项，传递给 <see cref="PrintDocumentPrinter"/>。
    /// </summary>
    public class PrintOptions
    {
        /// <summary>
        /// 打印机名称。为 null 时使用系统默认打印机。
        /// </summary>
        public string? PrinterName { get; set; }

        /// <summary>副本数，默认 1。</summary>
        public int Copies { get; set; } = 1;

        /// <summary>
        /// 是否显示打印对话框（仅 Windows 支持）。
        /// 在 Linux/macOS 上此选项无效，始终直接发送任务。
        /// </summary>
        public bool ShowPrintDialog { get; set; } = false;
    }

    /// <summary>
    /// 基于 System.Drawing.Printing.PrintDocument 的打印机实现。
    /// 内部通过 SkiaSharp 将模板渲染到打印机设备上下文（DC）。
    ///
    /// 跨平台说明：
    ///   - Windows：通过 GDI 打印路径，经 HDC 创建 SKSurface 直接绘制。
    ///   - Linux/macOS：System.Drawing.Printing 在 .NET 6+ 通过 CUPS 发送任务，
    ///     但 HDC 互操作不可用，此时回退到"渲染位图 → 绘制位图到 Graphics"方式。
    /// </summary>
    public class PrintDocumentPrinter : IDisposable
    {
        // ── 字段 ──────────────────────────────────────────────────────────

        private PrintDocument?  _printDocument;
        private LabelTemplate?  _currentTemplate;
        private List<PrintData> _dataPages = new();
        private int             _currentPageIndex;
        private bool            _disposed;

        /// <summary>
        /// 打印任务完成时触发（仅用于通知，不含输出数据）。
        /// </summary>
        public event EventHandler? PrintCompleted;

        /// <summary>
        /// 打印过程中发生错误时触发。
        /// </summary>
        public event EventHandler<Exception>? PrintError;

        // ── 公共方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 获取当前系统中已安装打印机的名称列表。
        /// 在 Windows 上通过 WMI/GDI 枚举，在 Linux/macOS 上通过 CUPS 枚举。
        /// </summary>
        /// <returns>打印机名称数组（可能为空）</returns>
        public static string[] GetInstalledPrinters()
        {
            var list = new List<string>();
            foreach (string name in PrinterSettings.InstalledPrinters)
                list.Add(name);
            return list.ToArray();
        }

        /// <summary>
        /// 打印单页模板。
        /// </summary>
        /// <param name="template">模板定义</param>
        /// <param name="data">页面数据绑定</param>
        /// <param name="options">打印选项（可选）</param>
        public void Print(LabelTemplate template, PrintData data, PrintOptions? options = null)
        {
            Print(template, new List<PrintData> { data }, options);
        }

        /// <summary>
        /// 打印多页（每个 <see cref="PrintData"/> 对应一页）。
        /// </summary>
        /// <param name="template">模板定义（所有页共用同一布局）</param>
        /// <param name="pages">每页的数据列表</param>
        /// <param name="options">打印选项（可选）</param>
        public void Print(LabelTemplate template, List<PrintData> pages, PrintOptions? options = null)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            if (pages is null || pages.Count == 0) throw new ArgumentException("页面数据不能为空", nameof(pages));

            options ??= new PrintOptions();

            // 保存当前任务状态
            _currentTemplate  = template;
            _dataPages        = pages;
            _currentPageIndex = 0;

            // 创建 PrintDocument 并配置
            _printDocument = BuildPrintDocument(template, options);

            // 注册页面绘制事件
            _printDocument.PrintPage += OnPrintPage;

            try
            {
                _printDocument.Print();
                PrintCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                PrintError?.Invoke(this, ex);
                throw;
            }
            finally
            {
                _printDocument.PrintPage -= OnPrintPage;
            }
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────

        /// <summary>
        /// 构建并配置 PrintDocument 对象（纸张尺寸、打印机名、副本数等）。
        /// </summary>
        private static PrintDocument BuildPrintDocument(LabelTemplate template, PrintOptions options)
        {
            var doc = new PrintDocument();

            // 设置打印机名称
            if (!string.IsNullOrEmpty(options.PrinterName))
                doc.PrinterSettings.PrinterName = options.PrinterName;

            // 设置副本数
            doc.PrinterSettings.Copies = (short)Math.Max(1, options.Copies);

            // 设置纸张尺寸（毫米 → 百分之一英寸，Windows 打印 API 单位）
            int wHundredths = SkiaDrawingHelper.MmToHundredthsOfInch(template.Width);
            int hHundredths = SkiaDrawingHelper.MmToHundredthsOfInch(template.Height);

            doc.DefaultPageSettings.PaperSize = new PaperSize("Custom", wHundredths, hHundredths);
            doc.DefaultPageSettings.Margins   = new Margins(0, 0, 0, 0); // 无页边距

            return doc;
        }

        /// <summary>
        /// PrintDocument.PrintPage 事件处理。
        /// 此方法在每次需要输出一页时被调用，内部通过 SkiaSharp 进行绘制。
        /// </summary>
        private void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            if (_currentTemplate is null || e.Graphics is null) return;

            // 获取当前页数据
            var data = _currentPageIndex < _dataPages.Count
                ? _dataPages[_currentPageIndex]
                : new PrintData();

            // ⚠ 重要：在 GetHdc() 之前提取 DPI，之后不能再访问 Graphics 的任何属性
            float dpiX = e.Graphics.DpiX;

            // 使用位图模式渲染（比 HDC 互操作更可靠）
            // HDC 模式在 GetHdc/ReleaseHdc 后调用 GDI+ DrawImage 会报错
            DrawViaBitmap(e.Graphics, dpiX, data);

            // 决定是否还有后续页
            _currentPageIndex++;
            e.HasMorePages = _currentPageIndex < _dataPages.Count;
        }

        /// <summary>
        /// Windows 专用渲染路径：通过 HDC 创建 SKSurface，直接在打印机 DC 上绘制。
        /// 优点：无需中间位图，速度快，内存消耗低。
        /// </summary>
        private void DrawViaHdc(Graphics gdi, SKMatrix matrix, float dpiX, PrintData data)
        {
            IntPtr hdc = IntPtr.Zero;
            try
            {
                // 尺寸取打印机像素尺寸（在 GetHdc 之前计算）
                var (widthPx, heightPx) = SkiaDrawingHelper.CalcBitmapSize(
                    _currentTemplate!.Width, _currentTemplate.Height,
                    dpiX);

                hdc = gdi.GetHdc();

                // 从 HDC 创建 SkiaSharp 绘图表面
                var info    = new SKImageInfo(widthPx, heightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create(info);
                if (surface is null)
                {
                    // HDC 创建 surface 失败，回退到位图模式
                    gdi.ReleaseHdc(hdc);
                    hdc = IntPtr.Zero;
                    DrawViaBitmap(gdi, dpiX, data);
                    return;
                }

                var canvas = surface.Canvas;
                canvas.SetMatrix(matrix);
                ElementRenderer.RenderTemplate(canvas, _currentTemplate, data, matrix);
                canvas.Flush();

                // 将 SKSurface 的像素读出，再绘制到 GDI DC
                using var snap = surface.Snapshot();
                using var bmp  = SKBitmap.FromImage(snap);
                DrawSkBitmapToGdi(gdi, bmp, _currentTemplate);
            }
            finally
            {
                if (hdc != IntPtr.Zero)
                    gdi.ReleaseHdc(hdc);
            }
        }

        /// <summary>
        /// 跨平台兼容渲染路径：先将模板渲染到 SKBitmap，再将位图绘制到 GDI Graphics。
        /// 在 Linux/macOS 上使用此路径（无 HDC 互操作）。
        /// </summary>
        private void DrawViaBitmap(Graphics gdi, float dpi, PrintData data)
        {
            // 使用打印机 DPI 渲染高分辨率位图（而非默认 96 DPI）
            // 这样在打印机 DC 上绘制时画质不会损失
            var renderer = new Preview.TemplateRenderer { Dpi = dpi };
            using var bitmap = renderer.Render(_currentTemplate!, data);

            // 将 SKBitmap 像素写入 System.Drawing.Bitmap，再通过 GDI 缩放绘制到打印机可打印区域
            DrawSkBitmapToGdi(gdi, bitmap, _currentTemplate!);
        }

        /// <summary>
        /// 将 <see cref="SKBitmap"/> 的像素数据桥接到 <see cref="System.Drawing.Graphics"/>。
        /// 位图会被缩放到打印页面的可打印区域，确保内容铺满纸张。
        /// </summary>
        private static unsafe void DrawSkBitmapToGdi(Graphics gdi, SKBitmap skBitmap, LabelTemplate template)
        {
            // 获取打印机可打印区域（已扣除硬件边距）
            var area = gdi.VisibleClipBounds;

            // 将 SKBitmap 编码为 PNG 字节数组，再用 System.Drawing 加载
            using var image    = SKImage.FromBitmap(skBitmap);
            using var pngData  = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms       = new System.IO.MemoryStream(pngData.ToArray());
            using var gdiBitmap = new System.Drawing.Bitmap(ms);

            // 设置位图的 DPI 分辨率（让 GDI+ 知道源图像的物理尺寸）
            float srcDpi = skBitmap.Width > 0
                ? skBitmap.Width / (template.Width / SkiaDrawingHelper.MmPerInch)
                : 96f;
            gdiBitmap.SetResolution(srcDpi, srcDpi);

            // 关键：将位图缩放绘制到打印机可打印区域
            // 如果用 DrawImage(bitmap, Point(0,0)) 会按 1:1 像素绘制，
            // 在高 DPI 打印机上只占左上角一小块
            gdi.DrawImage(gdiBitmap, area);
        }

        // ── IDisposable ──────────────────────────────────────────────────

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                _printDocument?.Dispose();
                _printDocument = null;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}

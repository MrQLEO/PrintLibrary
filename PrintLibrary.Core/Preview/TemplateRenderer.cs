using PrintLibrary.Model;
using PrintLibrary.Rendering;
using SkiaSharp;
using System;
using System.IO;

namespace PrintLibrary.Preview
{
    /// <summary>
    /// 模板预览渲染器。
    /// 将 <see cref="LabelTemplate"/> 渲染到内存中的 <see cref="SKBitmap"/>，
    /// 可指定 DPI、是否叠加调试网格/参考线。
    /// 
    /// 典型用途：
    ///   - 在 UI 中实时预览标签效果（绑定 SKBitmap 到 Image 控件）
    ///   - 导出为 PNG/JPEG 图片文件保存留档
    ///   - 单元测试时验证渲染输出
    /// </summary>
    public class TemplateRenderer
    {
        // ── 默认参数 ──────────────────────────────────────────────────────

        /// <summary>默认预览 DPI（96 为屏幕常用分辨率，打印预览建议用 300）。</summary>
        public const float DefaultDpi = 96f;

        // ── 配置属性 ──────────────────────────────────────────────────────

        /// <summary>
        /// 渲染 DPI，影响输出位图尺寸和元素清晰度。
        /// DPI 越高，输出图像越大越清晰，适合打印预览使用 203 / 300 / 600 DPI。
        /// </summary>
        public float Dpi { get; set; } = DefaultDpi;

        /// <summary>是否叠加调试网格（灰色格线）。默认关闭。</summary>
        public bool ShowGrid { get; set; } = false;

        /// <summary>调试网格间距（毫米），默认 5 mm。</summary>
        public float GridSpacingMm { get; set; } = 5f;

        /// <summary>是否绘制模板边界框（红色边框）。默认关闭。</summary>
        public bool ShowBorder { get; set; } = false;

        // ── 渲染方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 将模板渲染为 <see cref="SKBitmap"/>。
        /// 调用方负责 Dispose 返回的位图（using 语句）。
        /// </summary>
        /// <param name="template">要渲染的模板</param>
        /// <param name="data">数据绑定（可选，传 null 时使用空数据）</param>
        /// <returns>渲染好的位图（BGRA8888 格式）</returns>
        public SKBitmap Render(LabelTemplate template, PrintData? data = null)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            data ??= new PrintData();

            // 1. 计算位图像素尺寸
            var (widthPx, heightPx) = SkiaDrawingHelper.CalcBitmapSize(template.Width, template.Height, Dpi);

            // 2. 创建位图和画布
            var bitmap = new SKBitmap(widthPx, heightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKCanvas(bitmap);

            // 3. 构造毫米→像素变换矩阵并设置到画布
            var matrix = SkiaDrawingHelper.CreateMmToPixelMatrix(Dpi);
            canvas.SetMatrix(matrix);

            // 4. 渲染所有元素（含背景填充）
            ElementRenderer.RenderTemplate(canvas, template, data, matrix);

            // 5. 可选：叠加调试图层（网格/边框）
            //    调试图层在元素之上叠加，不影响实际打印内容
            if (ShowGrid)
                ElementRenderer.DrawDebugGrid(canvas, template, matrix, GridSpacingMm);

            if (ShowBorder)
                ElementRenderer.DrawDebugBorder(canvas, template, matrix);

            // 6. 刷新画布确保绘制完成
            canvas.Flush();
            return bitmap;
        }

        /// <summary>
        /// 将模板渲染并导出为 PNG 字节数组（适合 Web API 返回或内存传输）。
        /// </summary>
        /// <param name="template">要渲染的模板</param>
        /// <param name="data">数据绑定</param>
        /// <returns>PNG 格式的字节数组</returns>
        public byte[] RenderToPngBytes(LabelTemplate template, PrintData? data = null)
        {
            using var bitmap = Render(template, data);
            using var image  = SKImage.FromBitmap(bitmap);
            using var png    = image.Encode(SKEncodedImageFormat.Png, quality: 100);
            return png.ToArray();
        }

        /// <summary>
        /// 将模板渲染并保存为 PNG 文件。
        /// </summary>
        /// <param name="template">要渲染的模板</param>
        /// <param name="filePath">输出文件路径（含 .png 扩展名）</param>
        /// <param name="data">数据绑定</param>
        public void SaveToPng(LabelTemplate template, string filePath, PrintData? data = null)
        {
            byte[] pngBytes = RenderToPngBytes(template, data);
            // 确保目录存在
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(filePath, pngBytes);
        }

        /// <summary>
        /// 将模板渲染并保存为 JPEG 文件。
        /// </summary>
        /// <param name="template">要渲染的模板</param>
        /// <param name="filePath">输出文件路径（含 .jpg 扩展名）</param>
        /// <param name="quality">JPEG 质量 0-100，默认 90</param>
        /// <param name="data">数据绑定</param>
        public void SaveToJpeg(LabelTemplate template, string filePath, int quality = 90, PrintData? data = null)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            data ??= new PrintData();

            using var bitmap = Render(template, data);
            using var image  = SKImage.FromBitmap(bitmap);
            using var jpeg   = image.Encode(SKEncodedImageFormat.Jpeg, quality);

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(filePath, jpeg.ToArray());
        }

        // ── 静态快捷方法 ──────────────────────────────────────────────────

        /// <summary>
        /// 使用默认配置（96 DPI，无调试层）快速预览模板。
        /// </summary>
        /// <param name="template">要渲染的模板</param>
        /// <param name="data">数据绑定（可选）</param>
        public static SKBitmap QuickRender(LabelTemplate template, PrintData? data = null)
        {
            return new TemplateRenderer().Render(template, data);
        }

        /// <summary>
        /// 以指定 DPI 渲染到 PNG 字节数组（静态快捷版本）。
        /// </summary>
        public static byte[] QuickRenderToPng(LabelTemplate template, float dpi = DefaultDpi, PrintData? data = null)
        {
            return new TemplateRenderer { Dpi = dpi }.RenderToPngBytes(template, data);
        }
    }
}

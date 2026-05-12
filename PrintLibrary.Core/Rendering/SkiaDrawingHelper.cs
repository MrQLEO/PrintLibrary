using SkiaSharp;

namespace PrintLibrary.Rendering
{
    /// <summary>
    /// SkiaSharp 坐标辅助类。
    /// 负责在毫米物理坐标系与像素坐标系之间进行相互换算，
    /// 并提供构造 <see cref="SKMatrix"/>（毫米→像素变换矩阵）的工厂方法。
    ///
    /// 设计说明：
    ///   - 物理单位：毫米（mm），与打印机 DPI / 分辨率无关。
    ///   - 像素单位：由 DPI 决定，公式为 px = mm * (DPI / 25.4)。
    ///   - 变换矩阵是一个各向同性缩放矩阵（无旋转/错切），ScaleX == ScaleY。
    /// </summary>
    public static class SkiaDrawingHelper
    {
        /// <summary>
        /// 1 英寸 = 25.4 毫米，是 mm ↔ inch 互换的基准常量。
        /// </summary>
        public const float MmPerInch = 25.4f;

        // ── 单位换算 ──────────────────────────────────────────────────────

        /// <summary>
        /// 将毫米换算为指定 DPI 下的像素数（浮点）。
        /// </summary>
        /// <param name="mm">物理长度（毫米）</param>
        /// <param name="dpi">输出分辨率（每英寸像素数）</param>
        /// <returns>对应的像素长度</returns>
        public static float MmToPx(float mm, float dpi) => mm * dpi / MmPerInch;

        /// <summary>
        /// 将像素数换算为毫米（浮点）。
        /// </summary>
        /// <param name="px">像素数</param>
        /// <param name="dpi">输出分辨率（每英寸像素数）</param>
        public static float PxToMm(float px, float dpi) => px * MmPerInch / dpi;

        /// <summary>
        /// 将磅（pt，1pt = 1/72 英寸）换算为像素数。
        /// </summary>
        public static float PtToPx(float pt, float dpi) => pt * dpi / 72f;

        /// <summary>
        /// 将毫米换算为百分之一英寸（0.01 inch），
        /// 用于 <see cref="System.Drawing.Printing.PaperSize"/> 的单位（Windows 打印 API 要求）。
        /// </summary>
        public static int MmToHundredthsOfInch(float mm) =>
            (int)System.Math.Round(mm / MmPerInch * 100f);

        // ── 变换矩阵工厂 ──────────────────────────────────────────────────

        /// <summary>
        /// 构造一个"毫米 → 像素"的各向同性缩放矩阵。
        /// <para>
        /// 矩阵形式为：
        /// <code>
        ///   [ scale  0      0 ]
        ///   [ 0      scale  0 ]
        ///   [ 0      0      1 ]
        /// </code>
        /// 其中 scale = DPI / 25.4。
        /// </para>
        /// 使用方法：通过 <see cref="SKCanvas.SetMatrix"/> 设置此矩阵后，
        /// 所有后续绘图调用均可直接使用毫米作为坐标单位。
        /// </summary>
        /// <param name="dpi">目标输出分辨率（DPI）</param>
        /// <returns>毫米→像素变换矩阵</returns>
        public static SKMatrix CreateMmToPixelMatrix(float dpi)
        {
            float scale = dpi / MmPerInch;
            return SKMatrix.CreateScale(scale, scale);
        }

        /// <summary>
        /// 根据变换矩阵，将毫米尺寸的模板转换为像素尺寸的位图宽高。
        /// </summary>
        /// <param name="widthMm">模板宽度（毫米）</param>
        /// <param name="heightMm">模板高度（毫米）</param>
        /// <param name="dpi">目标 DPI</param>
        /// <returns>位图宽（像素），位图高（像素）</returns>
        public static (int widthPx, int heightPx) CalcBitmapSize(float widthMm, float heightMm, float dpi)
        {
            int w = System.Math.Max(1, (int)System.Math.Ceiling(MmToPx(widthMm, dpi)));
            int h = System.Math.Max(1, (int)System.Math.Ceiling(MmToPx(heightMm, dpi)));
            return (w, h);
        }

        /// <summary>
        /// 将以毫米为单位的 <see cref="SKRect"/> 通过变换矩阵映射到像素坐标的 SKRect。
        /// </summary>
        public static SKRect MmRectToPx(SKRect mmRect, SKMatrix matrix)
        {
            var tl = matrix.MapPoint(mmRect.Left, mmRect.Top);
            var br = matrix.MapPoint(mmRect.Right, mmRect.Bottom);
            return new SKRect(tl.X, tl.Y, br.X, br.Y);
        }
    }
}

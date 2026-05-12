using SkiaSharp;

namespace PrintLibrary.Model
{
    /// <summary>
    /// 线条元素。在标签上绘制一条直线，支持颜色、线宽和虚线样式。
    /// 线条起点为 (X, Y)，终点为 (X + Width, Y + Height)，均以毫米为单位。
    /// 若需绘制水平线，令 Height=0；竖直线令 Width=0；斜线则两者均不为零。
    /// </summary>
    public class LineElement : LabelElement
    {
        // ── 样式 ──────────────────────────────────────────────────────────

        /// <summary>线条颜色，ARGB 十六进制，默认黑色 "#FF000000"。</summary>
        public string Color { get; set; } = "#FF000000";

        /// <summary>
        /// 线条宽度，单位毫米。绘制时会换算为像素。
        /// 默认 0.5 mm（约 1.4 pt，打印时约 1-2 像素）。
        /// </summary>
        public float LineWidthMm { get; set; } = 0.5f;

        /// <summary>
        /// 虚线间隔数组（单位：mm）。偶数下标为线段长，奇数下标为间隙长。
        /// 为 null 或空数组时绘制实线。
        /// 示例：[3f, 1f] 表示 3mm 线段 + 1mm 间隙交替。
        /// </summary>
        public float[]? DashPattern { get; set; }

        // ── 绘制实现 ──────────────────────────────────────────────────────

        /// <inheritdoc />
        public override void Draw(SKCanvas canvas, SKMatrix mmToPixelMatrix, PrintData data)
        {
            if (!IsVisible) return;

            // 画布已通过 SetMatrix 设置了 mm→px 变换，坐标和尺寸均使用 mm 单位。
            // SkiaSharp 的 StrokeWidth 会随矩阵一起缩放，因此直接使用 mm 值即可，
            // 无需手动乘 pxPerMm（否则线宽会被双重缩放，导致线条过粗）。

            // 起点/终点（毫米坐标）
            var start = new SKPoint(X, Y);
            var end   = new SKPoint(X + Width, Y + Height);

            using var paint = new SKPaint
            {
                Color       = ParseColor(Color),
                StrokeWidth = LineWidthMm,  // mm 单位，矩阵缩放后自动转为正确像素宽
                IsAntialias = true,
                Style       = SKPaintStyle.Stroke
            };

            // 设置虚线（若有）— 间距单位为 mm，矩阵缩放后自动正确
            if (DashPattern is { Length: > 0 })
            {
                paint.PathEffect = SKPathEffect.CreateDash(DashPattern, 0f);
            }

            canvas.DrawLine(start, end, paint);
        }

        /// <summary>解析 ARGB 十六进制颜色字符串，失败时返回黑色。</summary>
        private static SKColor ParseColor(string hex)
        {
            if (SKColor.TryParse(hex, out var c)) return c;
            return SKColors.Black;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 矩形元素。绘制空心或实心矩形框，支持圆角、线宽、填充色和边框色。
    /// </summary>
    public class RectangleElement : LabelElement
    {
        // ── 样式 ──────────────────────────────────────────────────────────

        /// <summary>边框颜色，ARGB 十六进制。默认黑色 "#FF000000"。</summary>
        public string BorderColor { get; set; } = "#FF000000";

        /// <summary>
        /// 填充颜色，ARGB 十六进制。默认透明 "#00000000"（仅绘制边框）。
        /// 若需实心矩形，设为不透明颜色，如 "#FFCCCCCC"。
        /// </summary>
        public string FillColor { get; set; } = "#00000000";

        /// <summary>边框线宽，单位毫米，默认 0.5 mm。</summary>
        public float BorderWidthMm { get; set; } = 0.5f;

        /// <summary>
        /// 圆角半径，单位毫米，默认 0（直角）。
        /// 正值时绘制圆角矩形。
        /// </summary>
        public float CornerRadiusMm { get; set; } = 0f;

        // ── 绘制实现 ──────────────────────────────────────────────────────

        /// <inheritdoc />
        public override void Draw(SKCanvas canvas, SKMatrix mmToPixelMatrix, PrintData data)
        {
            if (!IsVisible) return;

            // 画布已通过 SetMatrix 设置了 mm→px 变换，坐标和尺寸均使用 mm 单位。
            // SkiaSharp 的 StrokeWidth 和圆角半径会随矩阵一起缩放，
            // 因此直接使用 mm 值即可，无需手动乘 pxPerMm。

            var rect = new SKRect(X, Y, X + Width, Y + Height);
            float borderMm = BorderWidthMm;
            float cornerMm = CornerRadiusMm;

            // 填充（若填充色不透明）
            var fillColor = ParseColor(FillColor);
            if (fillColor.Alpha > 0)
            {
                using var fillPaint = new SKPaint
                {
                    Color = fillColor,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true
                };
                DrawRect(canvas, rect, cornerMm, fillPaint);
            }

            // 边框（若边框色不透明且线宽 > 0）
            var borderColor = ParseColor(BorderColor);
            if (borderColor.Alpha > 0 && borderMm > 0)
            {
                using var strokePaint = new SKPaint
                {
                    Color       = borderColor,
                    Style       = SKPaintStyle.Stroke,
                    StrokeWidth = borderMm,  // mm 单位，矩阵缩放后自动正确
                    IsAntialias = true
                };
                DrawRect(canvas, rect, cornerMm, strokePaint);
            }
        }

        /// <summary>根据圆角大小，选择绘制普通矩形或圆角矩形。</summary>
        private static void DrawRect(SKCanvas canvas, SKRect rect, float cornerPx, SKPaint paint)
        {
            if (cornerPx > 0)
                canvas.DrawRoundRect(rect, cornerPx, cornerPx, paint);
            else
                canvas.DrawRect(rect, paint);
        }

        /// <summary>解析 ARGB 十六进制颜色字符串，失败时返回透明。</summary>
        private static SKColor ParseColor(string hex)
        {
            if (SKColor.TryParse(hex, out var c)) return c;
            return SKColors.Transparent;
        }
    }
}

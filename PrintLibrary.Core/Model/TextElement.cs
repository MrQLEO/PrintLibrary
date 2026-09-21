using SkiaSharp;
using System.IO;

namespace PrintLibrary.Model
{
    /// <summary>
    /// 文字水平对齐方式。
    /// </summary>
    public enum TextAlignment
    {
        /// <summary>左对齐</summary>
        Left,
        /// <summary>居中对齐</summary>
        Center,
        /// <summary>右对齐</summary>
        Right
    }

    /// <summary>
    /// 文本元素。支持字体、字号、颜色、对齐、自动换行/裁剪，以及数据绑定占位符。
    /// <para>字体来源优先级：</para>
    /// <para>  1. <see cref="FontPath"/> — 指定字体文件路径（.ttf/.otf），跨平台最可靠</para>
    /// <para>  2. <see cref="FontFamily"/> — 系统已安装字体名称</para>
    /// <para>  3. 回退到 SkiaSharp 默认字体</para>
    /// </summary>
    public class TextElement : LabelElement
    {
        // ── 内容 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 显示文本，支持占位符语法 {字段名} 或 {字段名:格式化字符串}。
        /// 示例："产品：{ProductName}  数量：{Qty}"
        /// </summary>
        public string Text { get; set; } = string.Empty;

        // ── 字体 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 字体族名称，例如 "Arial"、"SimSun"（宋体）、"Microsoft YaHei"（微软雅黑）。
        /// 若 <see cref="FontPath"/> 已指定，则优先使用字体文件，本字段仅作回退。
        /// 默认值 "Microsoft YaHei" 以确保中文环境下的文字渲染正常。
        /// </summary>
        public string FontFamily { get; set; } = "Microsoft YaHei";

        /// <summary>
        /// 字体文件路径（.ttf 或 .otf），优先级高于 <see cref="FontFamily"/>。
        /// <para>使用场景：Linux 容器中无中文字体时，可指定 "/usr/share/fonts/simsun.ttf" 等路径；
        /// 或在应用目录下附带字体文件以确保跨平台渲染一致。</para>
        /// </summary>
        public string? FontPath { get; set; }

        /// <summary>
        /// 字体大小，单位为磅（pt）。渲染时将根据输出 DPI 换算为像素。
        /// </summary>
        public float FontSize { get; set; } = 12f;

        /// <summary>是否加粗。</summary>
        public bool Bold { get; set; } = false;

        /// <summary>是否斜体。</summary>
        public bool Italic { get; set; } = false;

        // ── 颜色（ARGB 十六进制字符串）────────────────────────────────────

        /// <summary>
        /// 前景色（文字颜色），ARGB 十六进制，例如 "#FF000000"（不透明黑色）。
        /// </summary>
        public string ForeColor { get; set; } = "#FF000000";

        // ── 布局行为 ──────────────────────────────────────────────────────

        /// <summary>水平对齐方式。</summary>
        public TextAlignment Alignment { get; set; } = TextAlignment.Left;

        /// <summary>
        /// 是否启用自动换行。为 true 时，文字超出元素宽度后会自动折行。
        /// </summary>
        public bool WordWrap { get; set; } = false;

        /// <summary>
        /// 是否裁剪超出区域的内容。为 true 时，超出元素矩形的文字将被剪切。
        /// 通常与 WordWrap 同时为 true。
        /// </summary>
        public bool ClipContent { get; set; } = true;

        // ── 绘制实现 ──────────────────────────────────────────────────────

        /// <inheritdoc />
        public override void Draw(SKCanvas canvas, SKMatrix mmToPixelMatrix, PrintData data)
        {
            if (!IsVisible) return;

            // 1. 解析数据绑定，将占位符替换为实际值
            string resolvedText = data.Resolve(Text);
            if (string.IsNullOrEmpty(resolvedText)) return;

            // 2. 构建字体（SKFont 是 SkiaSharp 2.88+ 推荐的字体 API）
            using var font = CreateFont(mmToPixelMatrix, resolvedText);

            // 3. 构建画笔（仅负责颜色/抗锯齿，不再承载字体信息）
            using var paint = new SKPaint
            {
                Color       = ParseColor(ForeColor),
                IsAntialias = true,
                Style       = SKPaintStyle.Fill
            };

            // 4. 计算对齐方式
            var textAlign = Alignment switch
            {
                TextAlignment.Center => SKTextAlign.Center,
                TextAlignment.Right  => SKTextAlign.Right,
                _                    => SKTextAlign.Left
            };

            // 5. 元素矩形（毫米坐标，画布已通过 SetMatrix 将 mm 映射到 px）
            var rect = new SKRect(X, Y, X + Width, Y + Height);

            // 6. 保存画布状态，如需裁剪则启用剪裁区域
            // 宽或高为 0 时矩形退化：ClipRect 会裁掉全部内容（与 PdfSharpPrinter 中带 padding 的行为不一致）。
            // 常见写法是只设 X/Y/Width 表示单行，此时不裁剪。
            canvas.Save();
            bool clipToBounds = ClipContent && rect.Width > 0 && rect.Height > 0;
            if (clipToBounds)
                canvas.ClipRect(rect);

            // 7. 绘制文字
            string normalized = NormalizeNewlines(resolvedText);
            if (WordWrap)
            {
                DrawWrapped(canvas, font, paint, textAlign, normalized, rect);
            }
            else if (normalized.IndexOf('\n') >= 0)
            {
                DrawMultiline(canvas, font, paint, textAlign, normalized, rect, clipToBounds);
            }
            else
            {
                // 单行绘制
                float baseX = Alignment switch
                {
                    TextAlignment.Center => rect.MidX,
                    TextAlignment.Right  => rect.Right,
                    _                    => rect.Left
                };

                // SKFont.Metrics.Ascent 为负值，baseline Y = Top - Ascent
                var metrics = font.Metrics;
                float baseY = rect.Top - metrics.Ascent;

                canvas.DrawText(normalized, baseX, baseY, textAlign, font, paint);
            }

            canvas.Restore();
        }

        /// <summary>将 \r\n、单独 \r 规范为 \n，便于统一处理换行。</summary>
        private static string NormalizeNewlines(string text) =>
            text.Replace("\r\n", "\n").Replace('\r', '\n');

        /// <summary>多行绘制（显式换行，不含自动按宽度折行）。</summary>
        private static void DrawMultiline(
            SKCanvas canvas, SKFont font, SKPaint paint, SKTextAlign textAlign,
            string text, SKRect rect, bool clipToBounds)
        {
            var metrics = font.Metrics;
            float lineSpacing = (metrics.Descent - metrics.Ascent) * 1.15f;
            float bottomLimit = rect.Height > 0 ? rect.Bottom : float.MaxValue;
            string[] lines = text.Split('\n');

            float baseX = textAlign switch
            {
                SKTextAlign.Center => rect.MidX,
                SKTextAlign.Right  => rect.Right,
                _                    => rect.Left
            };

            float y = rect.Top - metrics.Ascent;
            foreach (var line in lines)
            {
                if (clipToBounds && rect.Height > 0 && y - metrics.Ascent > rect.Bottom + 0.01f)
                    break;
                canvas.DrawText(line, baseX, y, textAlign, font, paint);
                y += lineSpacing;
            }
        }

        // ── 字体构建 ──────────────────────────────────────────────────────

        /// <summary>
        /// 根据配置构建 SKFont 实例。
        /// 优先使用 FontPath 加载字体文件，其次用 FontFamily 从系统字体匹配。
        /// 当文本包含 CJK 字符但指定字体不支持 CJK 时，自动回退到 CJK 友好字体。
        /// </summary>
        private SKFont CreateFont(SKMatrix mmToPixelMatrix, string text)
        {
            SKTypeface? typeface = null;

            // 优先级 1：从字体文件路径加载
            if (!string.IsNullOrEmpty(FontPath) && File.Exists(FontPath))
            {
                try { typeface = SKTypeface.FromFile(FontPath); }
                catch { /* 文件加载失败，回退到系统字体 */ }
            }

            // 优先级 2：从系统字体族名匹配
            if (typeface is null)
            {
                var style = SKFontStyle.Normal;
                if (Bold && Italic)  style = SKFontStyle.BoldItalic;
                else if (Bold)       style = SKFontStyle.Bold;
                else if (Italic)     style = SKFontStyle.Italic;

                typeface = SKTypeface.FromFamilyName(FontFamily, style);
            }

            // 优先级 3：如果指定字体是纯西文字体且文本包含 CJK 字符，回退到 CJK 字体
            // 避免在用户只写英文时强制替换字体，导致 PDF 嵌入不必要的 CJK 字体子集
            if (typeface is not null && (ContainsCjkCharacter(text) && IsWesternOnlyFont(typeface)) || (ContainsCjkCharacter(text) && typeface?.FamilyName == null))
            {
                var style = SKFontStyle.Normal;
                if (Bold && Italic) style = SKFontStyle.BoldItalic;
                else if (Bold) style = SKFontStyle.Bold;
                else if (Italic) style = SKFontStyle.Italic;

                var cjkTypeface = TryGetCjkTypeface(style);
                if (cjkTypeface is not null)
                    typeface = cjkTypeface;
            }

            // 最终回退
            typeface ??= SKTypeface.Default;

            // 字号换算：pt → mm 空间下的尺寸
            // 画布已经设置了 mm→px 矩阵，我们在 mm 坐标系下绘制
            // pt → mm：1pt = 25.4/72 mm ≈ 0.3528 mm
            // SKFont.Size 的单位是像素，但画布矩阵会把 mm 坐标缩放到 px
            // 所以 SKFont.Size 应该是字号在 mm 空间下的值
            float fontSizeMm = FontSize * (25.4f / 72f);

            return new SKFont(typeface, fontSizeMm);
        }

        /// <summary>
        /// 检查文本中是否包含 CJK（中日韩）字符。
        /// 只有实际存在 CJK 字符时才触发字体回退，避免对纯英文文本嵌入 CJK 字体。
        /// </summary>
        private static bool ContainsCjkCharacter(string text)
        {
            foreach (char c in text)
            {
                // CJK Unified Ideographs: U+4E00..U+9FFF
                // CJK Unified Ideographs Extension A: U+3400..U+4DBF
                // CJK Compatibility Ideographs: U+F900..U+FAFF
                // CJK Symbols and Punctuation: U+3000..U+303F
                // Hiragana: U+3040..U+309F, Katakana: U+30A0..U+30FF
                // CJK Fullwidth Forms: U+FF00..U+FFEF
                if ((c >= '\u4E00' && c <= '\u9FFF') ||
                    (c >= '\u3400' && c <= '\u4DBF') ||
                    (c >= '\uF900' && c <= '\uFAFF') ||
                    (c >= '\u3000' && c <= '\u303F') ||
                    (c >= '\u3040' && c <= '\u309F') ||
                    (c >= '\u30A0' && c <= '\u30FF') ||
                    (c >= '\uFF00' && c <= '\uFFEF'))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查指定的 Typeface 是否为不含 CJK 字形的西文字体。
        /// 通过已知的西文字体族名列表判断。
        /// </summary>
        private static bool IsWesternOnlyFont(SKTypeface typeface)
        {
            var westernFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Arial", "Helvetica", "Times New Roman", "Courier New",
                "Verdana", "Tahoma", "Georgia", "Comic Sans MS", "Impact"
            };

            return westernFamilies.Contains(typeface.FamilyName);
        }

        /// <summary>
        /// 按 CJK 友好优先级尝试加载支持中日韩字符的系统字体。
        /// Windows → macOS → Linux 依次尝试。
        /// </summary>
        private static SKTypeface? TryGetCjkTypeface(SKFontStyle style)
        {
            // 按优先级排列的 CJK 字体族名（Windows / macOS / Linux）
            string[] cjkFamilies =
            {
                "Microsoft YaHei",    // Windows 微软雅黑
                "SimSun",             // Windows 宋体
                "SimHei",             // Windows 黑体
                "PingFang SC",        // macOS 苹方-简
                "Hiragino Sans GB",   // macOS 冬青黑体
                "Noto Sans CJK SC",   // Linux 思源黑体
                "WenQuanYi Micro Hei",// Linux 文泉驿微米黑
                "Droid Sans Fallback" // Android
            };

            foreach (var family in cjkFamilies)
            {
                var tf = SKTypeface.FromFamilyName(family, style);
                if (tf is not null)
                    return tf;
            }

            return null;
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────

        /// <summary>
        /// 将文字自动换行后逐行绘制在给定矩形内。
        /// </summary>
        private static void DrawWrapped(
            SKCanvas canvas, SKFont font, SKPaint paint,
            SKTextAlign textAlign, string text, SKRect rect)
        {
            text = NormalizeNewlines(text);
            var metrics = font.Metrics;
            float lineSpacing = (metrics.Descent - metrics.Ascent) * 1.2f;

            float x = textAlign switch
            {
                SKTextAlign.Center => rect.MidX,
                SKTextAlign.Right  => rect.Right,
                _                  => rect.Left
            };
            float y = rect.Top - metrics.Ascent; // 首行 baseline

            // 高度为 0 时表示未限制垂直范围，避免首行 baseline 已低于 rect.Bottom 导致一行都不画
            float bottomLimit = rect.Height > 0 ? rect.Bottom : float.MaxValue;
            bool hasWidthLimit = rect.Width > 0;

            var paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (y > bottomLimit) break;

                var para = paragraphs[p];
                if (para.Length == 0)
                {
                    y += lineSpacing * 0.35f;
                    continue;
                }

                if (p > 0)
                    y += lineSpacing;

                DrawWrappedParagraph(canvas, font, paint, textAlign, para, rect, ref y, lineSpacing, bottomLimit, hasWidthLimit);
            }
        }

        /// <summary>对单段文字按空格折行（不含段落内的 \n，已由上层拆分）。</summary>
        private static void DrawWrappedParagraph(
            SKCanvas canvas, SKFont font, SKPaint paint, SKTextAlign textAlign,
            string para, SKRect rect, ref float y, float lineSpacing, float bottomLimit, bool hasWidthLimit)
        {
            float x = textAlign switch
            {
                SKTextAlign.Center => rect.MidX,
                SKTextAlign.Right  => rect.Right,
                _                  => rect.Left
            };

            var words = para.Split(' ');
            string currentLine = "";

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                float w = hasWidthLimit ? font.MeasureText(testLine) : 0f;

                if (hasWidthLimit && w > rect.Width && !string.IsNullOrEmpty(currentLine))
                {
                    canvas.DrawText(currentLine, x, y, textAlign, font, paint);
                    y += lineSpacing;
                    currentLine = word;
                    if (y > bottomLimit) return;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine) && y <= bottomLimit)
                canvas.DrawText(currentLine, x, y, textAlign, font, paint);
        }

        /// <summary>
        /// 将 ARGB 十六进制颜色字符串解析为 <see cref="SKColor"/>。
        /// 解析失败时返回黑色。
        /// </summary>
        private static SKColor ParseColor(string hex)
        {
            if (SKColor.TryParse(hex, out var color)) return color;
            return SKColors.Black;
        }
    }
}

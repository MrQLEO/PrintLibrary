using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace PrintLibrary.Model
{
    /// <summary>
    /// 表格列对齐方式。
    /// </summary>
    public enum TableColumnAlign
    {
        /// <summary>左对齐</summary>
        Left,
        /// <summary>居中对齐</summary>
        Center,
        /// <summary>右对齐</summary>
        Right
    }

    /// <summary>
    /// 表格列定义。
    /// 描述一列的标题、宽度、对齐方式以及绑定到数据对象的哪个属性/字段。
    /// </summary>
    public class TableColumn
    {
        /// <summary>
        /// 列标题（表头文字）。
        /// </summary>
        public string Header { get; set; } = string.Empty;

        /// <summary>
        /// 列宽（毫米）。所有列宽之和应 ≤ 表格总宽度。
        /// 设为 0 时将自动均分剩余宽度。
        /// </summary>
        public float Width { get; set; }

        /// <summary>
        /// 列对齐方式，默认左对齐。
        /// </summary>
        public TableColumnAlign Align { get; set; } = TableColumnAlign.Left;

        /// <summary>
        /// 数据绑定字段名。
        /// 对于 <see cref="TableElement"/> 的 <see cref="TableElement.Rows"/>，
        /// 每行是一个 <see cref="Dictionary{String,Object}"/>，此字段作为 Key 取值。
        /// 若行中不包含此 Key，则显示空字符串。
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// 数值格式化字符串（可选）。
        /// 若列值为 <see cref="IFormattable"/> 且此字段非空，则用此格式化字符串输出。
        /// 示例："N2"、"yyyy-MM-dd"、"0.##"。
        /// </summary>
        public string? Format { get; set; }
    }

    /// <summary>
    /// 表格元素。传入列定义和数据行集合，自动计算行列布局并绘制表格。
    /// <para>
    /// 设计目标：免去手算坐标的痛苦，只需声明"有哪些列、数据是什么"，
    /// 表格自动完成行高计算、网格线绘制、表头样式区分。
    /// </para>
    /// <para>
    /// 典型用法：
    /// <code>
    /// var table = new TableElement
    /// {
    ///     X = 10f, Y = 50f, Width = 190f,
    ///     Columns = new()
    ///     {
    ///         new TableColumn { Header = "序号", Field = "No",    Width = 20f, Align = TableColumnAlign.Center },
    ///         new TableColumn { Header = "品名", Field = "Name", Width = 80f },
    ///         new TableColumn { Header = "数量", Field = "Qty",  Width = 40f, Align = TableColumnAlign.Right },
    ///         new TableColumn { Header = "单价", Field = "Price", Width = 50f, Align = TableColumnAlign.Right, Format = "N2" },
    ///     },
    ///     Rows = new()
    ///     {
    ///         new() { ["No"] = 1, ["Name"] = "螺丝 M3×8", ["Qty"] = 500, ["Price"] = 0.05m },
    ///         new() { ["No"] = 2, ["Name"] = "螺母 M6",   ["Qty"] = 300, ["Price"] = 0.12m },
    ///     }
    /// };
    /// </code>
    /// </para>
    /// </summary>
    public class TableElement : LabelElement
    {
        // ── 列定义与数据 ──────────────────────────────────────────────

        /// <summary>
        /// 列定义列表。每列指定标题、宽度、对齐方式、数据绑定字段名。
        /// </summary>
        public List<TableColumn> Columns { get; set; } = new();

        /// <summary>
        /// 数据行集合。每行是一个字典，Key 为 <see cref="TableColumn.Field"/>，
        /// Value 为该列的值（ToString 后显示，支持 IFormattable 格式化）。
        /// </summary>
        public List<Dictionary<string, object?>> Rows { get; set; } = new();

        // ── 表头样式 ──────────────────────────────────────────────────

        /// <summary>
        /// 表头背景色，ARGB 十六进制，默认浅灰 "#FFE8E8E8"。
        /// </summary>
        public string HeaderBackColor { get; set; } = "#FFE8E8E8";

        /// <summary>
        /// 表头文字颜色，ARGB 十六进制，默认深灰 "#FF1A1A2E"。
        /// </summary>
        public string HeaderForeColor { get; set; } = "#FF1A1A2E";

        /// <summary>
        /// 表头字体大小（磅），默认 9pt。
        /// </summary>
        public float HeaderFontSize { get; set; } = 9f;

        /// <summary>
        /// 表头是否加粗，默认 true。
        /// </summary>
        public bool HeaderBold { get; set; } = true;

        // ── 数据行样式 ──────────────────────────────────────────────

        /// <summary>
        /// 数据行文字颜色，ARGB 十六进制，默认黑色 "#FF000000"。
        /// </summary>
        public string RowForeColor { get; set; } = "#FF000000";

        /// <summary>
        /// 数据行字体大小（磅），默认 8pt。
        /// </summary>
        public float RowFontSize { get; set; } = 8f;

        /// <summary>
        /// 奇数行背景色（1、3、5...），ARGB 十六进制，默认透明（不填充）。
        /// </summary>
        public string OddRowBackColor { get; set; } = "#00000000";

        /// <summary>
        /// 偶数行背景色（2、4、6...），ARGB 十六进制，默认浅灰 "#FFF5F5F5"。
        /// </summary>
        public string EvenRowBackColor { get; set; } = "#FFF5F5F5";

        // ── 网格线样式 ──────────────────────────────────────────────

        /// <summary>
        /// 外边框线宽（毫米），默认 0.5mm。设为 0 则不画外边框。
        /// </summary>
        public float BorderWidthMm { get; set; } = 0.5f;

        /// <summary>
        /// 内部分割线线宽（毫米），默认 0.25mm。设为 0 则不画内部分割线。
        /// </summary>
        public float GridLineWidthMm { get; set; } = 0.25f;

        /// <summary>
        /// 网格线颜色，ARGB 十六进制，默认灰色 "#FFCCCCCC"。
        /// </summary>
        public string GridColor { get; set; } = "#FFCCCCCC";

        // ── 行高与字体 ──────────────────────────────────────────────

        /// <summary>
        /// 行高（毫米），默认 7mm。所有行（含表头）使用相同行高。
        /// </summary>
        public float RowHeight { get; set; } = 7f;

        /// <summary>
        /// 字体族名，默认 "Microsoft YaHei" 以确保中文正常显示。
        /// </summary>
        public string FontFamily { get; set; } = "Microsoft YaHei";

        // ── 绘制实现 ──────────────────────────────────────────────

        /// <inheritdoc />
        public override void Draw(SKCanvas canvas, SKMatrix mmToPixelMatrix, PrintData data)
        {
            if (!IsVisible || Columns.Count == 0) return;

            // 1. 计算列宽（未指定的列自动均分剩余宽度）
            float[] colWidths = CalcColumnWidths();

            // 2. 计算行数（表头 + 数据行）
            int totalRows = 1 + Rows.Count;

            // 3. 画布已设置 mm→px 矩阵，直接用毫米坐标绘制
            float tableX = X;
            float tableY = Y;
            float tableW = Width;
            float tableH = RowHeight * totalRows;

            // 4. 绘制背景（奇偶行交替色）
            DrawRowBackgrounds(canvas, tableX, tableY, totalRows);

            // 5. 绘制网格线
            DrawGridLines(canvas, tableX, tableY, tableW, tableH, colWidths);

            // 6. 绘制表头文字
            DrawHeaderTexts(canvas, tableX, tableY, colWidths);

            // 7. 绘制数据行文字
            DrawRowTexts(canvas, tableX, tableY, colWidths);
        }

        // ── 私有计算方法 ──────────────────────────────────────────────

        /// <summary>
        /// 计算每列的实际宽度（mm）。
        /// 未指定宽度的列自动均分剩余宽度。
        /// </summary>
        private float[] CalcColumnWidths()
        {
            float[] widths = new float[Columns.Count];
            float specified = 0f;
            int autoCount = 0;

            for (int i = 0; i < Columns.Count; i++)
            {
                if (Columns[i].Width > 0)
                {
                    widths[i] = Columns[i].Width;
                    specified += Columns[i].Width;
                }
                else
                {
                    autoCount++;
                }
            }

            // 自动均分剩余宽度
            float remaining = System.Math.Max(0, Width - specified);
            float autoWidth = autoCount > 0 ? remaining / autoCount : 0f;

            for (int i = 0; i < Columns.Count; i++)
            {
                if (widths[i] <= 0)
                    widths[i] = autoWidth;
            }

            return widths;
        }

        /// <summary>
        /// 绘制奇偶行交替背景色。
        /// </summary>
        private void DrawRowBackgrounds(SKCanvas canvas, float tableX, float tableY, int totalRows)
        {
            // 表头背景
            var headerBg = ParseColor(HeaderBackColor);
            if (headerBg.Alpha > 0)
            {
                using var paint = new SKPaint { Color = headerBg, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRect(tableX, tableY, Width, RowHeight, paint);
            }

            // 数据行背景
            for (int i = 0; i < Rows.Count; i++)
            {
                var bgColor = (i % 2 == 0) ? ParseColor(OddRowBackColor) : ParseColor(EvenRowBackColor);
                if (bgColor.Alpha > 0)
                {
                    float rowY = tableY + (i + 1) * RowHeight;
                    using var paint = new SKPaint { Color = bgColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawRect(tableX, rowY, Width, RowHeight, paint);
                }
            }
        }

        /// <summary>
        /// 绘制外边框和内部网格线。
        /// </summary>
        private void DrawGridLines(SKCanvas canvas, float tableX, float tableY, float tableW, float tableH, float[] colWidths)
        {
            var gridClr = ParseColor(GridColor);
            int totalRows = 1 + Rows.Count;

            // 外边框
            if (BorderWidthMm > 0)
            {
                using var borderPaint = new SKPaint
                {
                    Color = gridClr,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = BorderWidthMm,
                    IsAntialias = true
                };
                canvas.DrawRect(tableX, tableY, tableW, tableH, borderPaint);
            }

            // 内部网格线
            if (GridLineWidthMm <= 0) return;

            using var gridPaint = new SKPaint
            {
                Color = gridClr,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = GridLineWidthMm,
                IsAntialias = true
            };

            // 水平线（表头与数据行之间、数据行之间）
            for (int row = 1; row < totalRows; row++)
            {
                float lineY = tableY + row * RowHeight;
                canvas.DrawLine(tableX, lineY, tableX + tableW, lineY, gridPaint);
            }

            // 垂直线（列之间）
            float colX = tableX;
            for (int col = 0; col < colWidths.Length - 1; col++)
            {
                colX += colWidths[col];
                canvas.DrawLine(colX, tableY, colX, tableY + tableH, gridPaint);
            }
        }

        /// <summary>
        /// 绘制表头文字。
        /// </summary>
        private void DrawHeaderTexts(SKCanvas canvas, float tableX, float tableY, float[] colWidths)
        {
            using var typeface = LoadTypeface(HeaderBold);
            float fontSizeMm = HeaderFontSize * (25.4f / 72f);
            using var font = new SKFont(typeface, fontSizeMm);
            using var paint = new SKPaint
            {
                Color = ParseColor(HeaderForeColor),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            float colX = tableX;
            for (int i = 0; i < Columns.Count; i++)
            {
                var rect = new SKRect(colX, tableY, colX + colWidths[i], tableY + RowHeight);
                DrawCellText(canvas, font, paint, Columns[i].Header, rect, Columns[i].Align);
                colX += colWidths[i];
            }
        }

        /// <summary>
        /// 绘制数据行文字。
        /// </summary>
        private void DrawRowTexts(SKCanvas canvas, float tableX, float tableY, float[] colWidths)
        {
            using var typeface = LoadTypeface(false);
            float fontSizeMm = RowFontSize * (25.4f / 72f);
            using var font = new SKFont(typeface, fontSizeMm);
            using var paint = new SKPaint
            {
                Color = ParseColor(RowForeColor),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            for (int rowIdx = 0; rowIdx < Rows.Count; rowIdx++)
            {
                var row = Rows[rowIdx];
                float rowY = tableY + (rowIdx + 1) * RowHeight;
                float colX = tableX;

                for (int colIdx = 0; colIdx < Columns.Count; colIdx++)
                {
                    var col = Columns[colIdx];
                    string cellText = GetCellValue(row, col);

                    var rect = new SKRect(colX, rowY, colX + colWidths[colIdx], rowY + RowHeight);
                    DrawCellText(canvas, font, paint, cellText, rect, col.Align);
                    colX += colWidths[colIdx];
                }
            }
        }

        /// <summary>
        /// 在单元格矩形内绘制文字（含对齐和垂直居中）。
        /// </summary>
        private static void DrawCellText(SKCanvas canvas, SKFont font, SKPaint paint,
            string text, SKRect cellRect, TableColumnAlign align)
        {
            if (string.IsNullOrEmpty(text)) return;

            canvas.Save();
            canvas.ClipRect(cellRect);

            var metrics = font.Metrics;
            float baseX = align switch
            {
                TableColumnAlign.Center => cellRect.MidX,
                TableColumnAlign.Right  => cellRect.Right - 1f, // 1mm 右内边距
                _                       => cellRect.Left + 1f    // 1mm 左内边距
            };
            float baseY = cellRect.Top + (cellRect.Height - (metrics.Descent - metrics.Ascent)) / 2f - metrics.Ascent;

            var textAlign = align switch
            {
                TableColumnAlign.Center => SKTextAlign.Center,
                TableColumnAlign.Right  => SKTextAlign.Right,
                _                       => SKTextAlign.Left
            };

            canvas.DrawText(text, baseX, baseY, textAlign, font, paint);
            canvas.Restore();
        }

        /// <summary>
        /// 从数据行字典中获取格式化后的单元格文本。
        /// </summary>
        private static string GetCellValue(Dictionary<string, object?> row, TableColumn col)
        {
            if (!row.TryGetValue(col.Field, out var value) || value is null)
                return string.Empty;

            if (!string.IsNullOrEmpty(col.Format) && value is IFormattable formattable)
                return formattable.ToString(col.Format, null);

            return value.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 加载字体 Typeface。
        /// </summary>
        private SKTypeface LoadTypeface(bool bold)
        {
            var style = bold ? SKFontStyle.Bold : SKFontStyle.Normal;
            var tf = SKTypeface.FromFamilyName(FontFamily, style);
            return tf ?? SKTypeface.Default;
        }

        /// <summary>解析 ARGB 十六进制颜色字符串，失败时返回黑色。</summary>
        private static SKColor ParseColor(string hex)
        {
            if (SKColor.TryParse(hex, out var c)) return c;
            return SKColors.Black;
        }
    }
}

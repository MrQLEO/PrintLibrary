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
    /// 表格单元格。可直接替代普通值放入 <see cref="TableElement.Rows"/> 字典，
    /// 用于指定合并（ColSpan / RowSpan）、对齐覆盖、前景色、加粗等。
    /// <para>普通值（string / int / decimal 等）等价于 ColSpan=1、RowSpan=1 的单元格，完全向后兼容。</para>
    /// </summary>
    public class TableCell
    {
        /// <summary>单元格文字（已格式化或原始文本均可）。</summary>
        public string? Text { get; set; }

        /// <summary>横向合并列数（colspan），默认 1 表示不跨列。</summary>
        public int ColSpan { get; set; } = 1;

        /// <summary>纵向合并行数（rowspan），默认 1 表示不跨行。</summary>
        public int RowSpan { get; set; } = 1;

        /// <summary>对齐方式覆盖；为空时继承所在列的 <see cref="TableColumn.Align"/>。</summary>
        public TableColumnAlign? Align { get; set; }

        /// <summary>前景色（ARGB 十六进制）覆盖；为空时继承 <see cref="TableElement.RowForeColor"/>。</summary>
        public string? ForeColor { get; set; }

        /// <summary>
        /// 合并区域背景色（ARGB 十六进制）。仅 ColSpan/RowSpan &gt; 1 时生效。
        /// 默认 null 表示不填充（奇偶行色带不会进入合并区域，避免跨行文字压在色带分界上）。
        /// </summary>
        public string? BackColor { get; set; }

        /// <summary>是否加粗覆盖；为空时按数据行默认（不加粗）。</summary>
        public bool? Bold { get; set; }
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
        /// 表头跨列数（colspan）。默认 1 表示不跨列。
        /// 设为 N（&gt;1）时，该列表头向右合并 N 列，与 HTML table 的 colspan 语义一致；
        /// 被合并列的表头文字不再单独绘制（由本列统一覆盖）。
        /// </summary>
        public int HeaderColSpan { get; set; } = 1;

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

            // 2. 行数（表头 + 数据行）
            int dataRows = Rows.Count;
            int totalRows = 1 + dataRows;

            // 3. 画布已设置 mm→px 矩阵，直接用毫米坐标绘制
            float tableX = X;
            float tableY = Y;
            float tableW = Width;
            float tableH = RowHeight * totalRows;

            // 4. 构建合并单元格占用图（含表头 ColSpan 与数据行 ColSpan/RowSpan）
            BuildSpanMaps(colWidths, out bool[,] covered, out int[,] colSpan,
                          out int[,] rowSpan, out int[,] anchorR, out int[,] anchorC);

            // 5. 绘制背景（奇偶行交替色；合并区域默认不填充，可由 TableCell.BackColor 覆盖）
            DrawRowBackgrounds(canvas, tableX, tableY, colWidths,
                               covered, colSpan, rowSpan, anchorR, anchorC);

            // 6. 绘制网格线（避让合并单元格）
            DrawGridLines(canvas, tableX, tableY, tableW, tableH, colWidths,
                          dataRows, covered, colSpan, rowSpan, anchorR, anchorC);

            // 7. 绘制表头文字（支持表头 ColSpan）
            DrawHeaderTexts(canvas, tableX, tableY, colWidths);

            // 8. 绘制数据行文字（支持 ColSpan/RowSpan）
            DrawRowTexts(canvas, tableX, tableY, colWidths,
                         covered, colSpan, rowSpan, anchorR, anchorC);
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
        private void DrawRowBackgrounds(SKCanvas canvas, float tableX, float tableY, float[] colWidths,
            bool[,] covered, int[,] colSpan, int[,] rowSpan, int[,] anchorR, int[,] anchorC)
        {
            // 表头背景（整行填充；表头合并区同色，无需区分）
            var headerBg = ParseColor(HeaderBackColor);
            if (headerBg.Alpha > 0)
            {
                using var paint = new SKPaint { Color = headerBg, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRect(tableX, tableY, Width, RowHeight, paint);
            }

            // 数据行背景：合并区域默认不填充奇偶色（避免跨行文字压在色带分界上），
            // 需要背景时通过 TableCell.BackColor 指定
            for (int r = 0; r < Rows.Count; r++)
            {
                var bgColor = (r % 2 == 0) ? ParseColor(OddRowBackColor) : ParseColor(EvenRowBackColor);
                float rowY = tableY + (r + 1) * RowHeight;

                // 连续的普通格子按段填充，减少相邻矩形接缝
                float? runStart = null;
                float runWidth = 0f;

                for (int c = 0; c < Columns.Count; c++)
                {
                    bool isCovered = covered[r + 1, c];
                    bool isMerged = colSpan[r + 1, c] > 1 || rowSpan[r + 1, c] > 1;

                    if (!isCovered && !isMerged)
                    {
                        runStart ??= tableX + SumWidths(colWidths, 0, c);
                        runWidth += colWidths[c];
                        continue;
                    }

                    FlushRun(canvas, bgColor, runStart, rowY, runWidth);
                    runStart = null;
                    runWidth = 0f;

                    // 合并锚点：显式指定了 BackColor 才填充整个合并区域
                    if (isMerged)
                    {
                        var cell = ResolveCell(Rows[r], Columns[c]);
                        if (!string.IsNullOrEmpty(cell.BackColor))
                        {
                            float mx = tableX + SumWidths(colWidths, 0, c);
                            float mw = SumWidths(colWidths, c, colSpan[r + 1, c]);
                            float mh = RowHeight * rowSpan[r + 1, c];
                            using var mp = new SKPaint
                            {
                                Color = ParseColor(cell.BackColor),
                                Style = SKPaintStyle.Fill,
                                IsAntialias = true
                            };
                            canvas.DrawRect(mx, rowY, mw, mh, mp);
                        }
                    }
                }

                FlushRun(canvas, bgColor, runStart, rowY, runWidth);
            }
        }

        /// <summary>填充一段连续的奇偶行背景（透明或零宽时跳过）。</summary>
        private void FlushRun(SKCanvas canvas, SKColor color, float? x, float y, float w)
        {
            if (x is null || w <= 0 || color.Alpha == 0) return;
            using var paint = new SKPaint { Color = color, Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRect(x.Value, y, w, RowHeight, paint);
        }

        /// <summary>从 start 起 count 列的宽度之和。</summary>
        private static float SumWidths(float[] colWidths, int start, int count)
        {
            float sum = 0f;
            for (int i = 0; i < count; i++) sum += colWidths[start + i];
            return sum;
        }

        /// <summary>
        /// 绘制外边框和内部网格线。
        /// </summary>
        private void DrawGridLines(SKCanvas canvas, float tableX, float tableY, float tableW, float tableH,
            float[] colWidths, int dataRows,
            bool[,] covered, int[,] colSpan, int[,] rowSpan, int[,] anchorR, int[,] anchorC)
        {
            var gridClr = ParseColor(GridColor);
            int nCols = Columns.Count;
            int nRows = 1 + dataRows;   // 行 0 = 表头

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

            if (GridLineWidthMm <= 0) return;

            using var gridPaint = new SKPaint
            {
                Color = gridClr,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = GridLineWidthMm,
                IsAntialias = true
            };

            // 每列左边界 x 坐标
            float[] colLeftX = new float[nCols];
            float acc = tableX;
            for (int c = 0; c < nCols; c++) { colLeftX[c] = acc; acc += colWidths[c]; }

            // 水平内部分割线：逐行边界、逐列分段，避让纵向合并单元格
            // 最后一列的分段右边界是表格右缘（colLeftX 只有每列左边界）
            for (int rb = 1; rb < nRows; rb++)
            {
                float lineY = tableY + rb * RowHeight;
                for (int c = 0; c < nCols; c++)
                {
                    // 上方单元格是否纵向跨过此边界（即该边界位于其合并区域内）
                    int ar = anchorR[rb - 1, c], ac = anchorC[rb - 1, c];
                    int rs = rowSpan[ar, ac];
                    bool crosses = (ar <= rb - 1 && ar + rs - 1 >= rb);
                    if (crosses) continue;
                    float x1 = colLeftX[c];
                    float x2 = (c == nCols - 1) ? tableX + tableW : colLeftX[c + 1];
                    canvas.DrawLine(x1, lineY, x2, lineY, gridPaint);
                }
            }

            // 垂直内部分割线：逐列边界、逐行分段
            // 只需判断左侧单元格是否横向跨过此边界（RowSpan 不影响垂直线，
            // 合并格的左右边框仍要沿整个合并高度绘制）
            // 最后一行的分段下边界是表格底缘
            for (int cb = 0; cb < nCols - 1; cb++)
            {
                float lineX = colLeftX[cb] + colWidths[cb];   // = colLeftX[cb + 1]
                for (int r = 0; r < nRows; r++)
                {
                    int ar = anchorR[r, cb], ac = anchorC[r, cb];
                    int cs = colSpan[ar, ac];
                    bool crossesH = (ac <= cb && ac + cs - 1 >= cb + 1);
                    if (crossesH) continue;
                    float topY = tableY + r * RowHeight;
                    float botY = (r == nRows - 1) ? tableY + tableH : tableY + (r + 1) * RowHeight;
                    canvas.DrawLine(lineX, topY, lineX, botY, gridPaint);
                }
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

            // 表头 ColSpan：仅锚点列绘制文字，被合并列跳过
            bool[] headerCovered = new bool[Columns.Count];
            for (int i = 0; i < Columns.Count; i++)
            {
                if (headerCovered[i]) continue;
                int cs = Math.Clamp(Columns[i].HeaderColSpan, 1, Columns.Count - i);
                for (int dc = 1; dc < cs; dc++) headerCovered[i + dc] = true;

                float w = 0f;
                for (int dc = 0; dc < cs; dc++) w += colWidths[i + dc];
                var rect = new SKRect(tableX, tableY, tableX + w, tableY + RowHeight);
                DrawCellText(canvas, font, paint, Columns[i].Header, rect, Columns[i].Align);
                tableX += w;   // 注意：此处移动的是局部副本 tableX
            }
        }

        /// <summary>
        /// 绘制数据行文字。
        /// </summary>
        private void DrawRowTexts(SKCanvas canvas, float tableX, float tableY, float[] colWidths,
            bool[,] covered, int[,] colSpan, int[,] rowSpan, int[,] anchorR, int[,] anchorC)
        {
            int nCols = Columns.Count;
            float fontSizeMm = RowFontSize * (25.4f / 72f);

            for (int r = 0; r < Rows.Count; r++)
            {
                float rowY = tableY + (r + 1) * RowHeight;
                float colX = tableX;

                for (int c = 0; c < nCols; c++)
                {
                    float cellW = colWidths[c];
                    // 行 0 是表头，数据行 r 对应合并图中的第 r+1 行
                    if (covered[r + 1, c])
                    {
                        colX += cellW;
                        continue;   // 被合并覆盖的格子，不绘制
                    }

                    var cell = ResolveCell(Rows[r], Columns[c]);
                    int cs = colSpan[r + 1, c];
                    int rs = rowSpan[r + 1, c];

                    // 合并区域矩形（跨 cs 列、rs 行）
                    float w = 0f;
                    for (int dc = 0; dc < cs; dc++) w += colWidths[c + dc];
                    float h = RowHeight * rs;
                    var rect = new SKRect(colX, rowY, colX + w, rowY + h);

                    var color = ParseColor(cell.ForeColor ?? RowForeColor);
                    bool bold = cell.Bold ?? false;
                    using var tf = LoadTypeface(bold);
                    using var font = new SKFont(tf, fontSizeMm);
                    using var paint = new SKPaint
                    {
                        Color = color,
                        IsAntialias = true,
                        Style = SKPaintStyle.Fill
                    };
                    DrawCellText(canvas, font, paint, cell.Text, rect, cell.Align);

                    colX += cellW;
                }
            }
        }

        /// <summary>
        /// 在单元格矩形内绘制文字（含对齐和垂直居中）。
        /// 支持 \n / \r\n 换行。
        /// </summary>
        private static void DrawCellText(SKCanvas canvas, SKFont font, SKPaint paint,
            string text, SKRect cellRect, TableColumnAlign align)
        {
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (string.IsNullOrEmpty(text)) return;

            canvas.Save();
            canvas.ClipRect(cellRect);

            var metrics = font.Metrics;
            float lineH = metrics.Descent - metrics.Ascent;
            float lineSpacing = lineH * 1.12f;

            string[] lines = text.Split('\n');
            float span = lines.Length == 1 ? lineH : (lines.Length - 1) * lineSpacing + lineH;
            float firstBaseline = cellRect.Top + (cellRect.Height - span) / 2f - metrics.Ascent;

            float baseX = align switch
            {
                TableColumnAlign.Center => cellRect.MidX,
                TableColumnAlign.Right  => cellRect.Right - 1f,
                _                       => cellRect.Left + 1f
            };

            var textAlign = align switch
            {
                TableColumnAlign.Center => SKTextAlign.Center,
                TableColumnAlign.Right  => SKTextAlign.Right,
                _                       => SKTextAlign.Left
            };

            for (int i = 0; i < lines.Length; i++)
            {
                float lineY = firstBaseline + i * lineSpacing;
                canvas.DrawText(lines[i], baseX, lineY, textAlign, font, paint);
            }

            canvas.Restore();
        }

        /// <summary>
        /// 从数据行字典中获取格式化后的单元格文本。
        /// </summary>
        /// <summary>
        /// 解析单元格为统一结构，支持普通值（按列 Format 自动格式化）与
        /// <see cref="TableCell"/>（自带跨列/跨行/对齐/颜色/加粗）。
        /// </summary>
        internal ResolvedCell ResolveCell(Dictionary<string, object?> row, TableColumn col)
        {
            if (!row.TryGetValue(col.Field, out var value) || value is null)
                return new ResolvedCell { Text = string.Empty, Align = col.Align };

            if (value is TableCell tc)
            {
                return new ResolvedCell
                {
                    Text = tc.Text ?? string.Empty,
                    ColSpan = tc.ColSpan,
                    RowSpan = tc.RowSpan,
                    Align = tc.Align ?? col.Align,
                    ForeColor = tc.ForeColor,
                    BackColor = tc.BackColor,
                    Bold = tc.Bold
                };
            }

            string text = (!string.IsNullOrEmpty(col.Format) && value is IFormattable f)
                ? f.ToString(col.Format, null)
                : value.ToString() ?? string.Empty;
            return new ResolvedCell { Text = text, Align = col.Align };
        }

        /// <summary>
        /// 构建合并单元格占用图。行 0 为表头（仅支持 ColSpan），
        /// 行 1..N 为数据行（支持 ColSpan + RowSpan）。
        /// <para>covered[r,c] 为 true 表示该格被其它单元格合并覆盖（自身不绘制）；
        /// anchorR/anchorC 记录每格所属合并区的左上角锚点，便于网格线避让判断。</para>
        /// </summary>
        internal void BuildSpanMaps(float[] colWidths,
            out bool[,] covered, out int[,] colSpan, out int[,] rowSpan,
            out int[,] anchorR, out int[,] anchorC)
        {
            int nCols = Columns.Count;
            int nRows = 1 + Rows.Count;   // 行 0 = 表头
            covered = new bool[nRows, nCols];
            colSpan = new int[nRows, nCols];
            rowSpan = new int[nRows, nCols];
            anchorR = new int[nRows, nCols];
            anchorC = new int[nRows, nCols];

            for (int r = 0; r < nRows; r++)
                for (int c = 0; c < nCols; c++)
                {
                    colSpan[r, c] = 1;
                    rowSpan[r, c] = 1;
                    anchorR[r, c] = r;
                    anchorC[r, c] = c;
                }

            // 表头行（r = 0）：仅 ColSpan
            for (int c = 0; c < nCols; c++)
            {
                if (covered[0, c]) continue;
                int cs = Math.Clamp(Columns[c].HeaderColSpan, 1, nCols - c);
                colSpan[0, c] = cs;
                for (int dc = 1; dc < cs; dc++)
                    covered[0, c + dc] = true;
            }

            // 数据行（r = 1..nRows-1）
            for (int r = 1; r < nRows; r++)
            {
                int dataR = r - 1;
                for (int c = 0; c < nCols; c++)
                {
                    if (covered[r, c]) continue;
                    var cell = ResolveCell(Rows[dataR], Columns[c]);
                    int cs = Math.Clamp(cell.ColSpan, 1, nCols - c);
                    int rs = Math.Clamp(cell.RowSpan, 1, nRows - r);
                    colSpan[r, c] = cs;
                    rowSpan[r, c] = rs;
                    for (int dr = 0; dr < rs; dr++)
                        for (int dc = 0; dc < cs; dc++)
                            if (dr != 0 || dc != 0)
                            {
                                covered[r + dr, c + dc] = true;
                                anchorR[r + dr, c + dc] = r;
                                anchorC[r + dr, c + dc] = c;
                            }
                }
            }
        }

        /// <summary>
        /// 解析后的单元格统一结构（供绘制时取用）。
        /// </summary>
        internal struct ResolvedCell
        {
            public string Text;
            public int ColSpan;
            public int RowSpan;
            public TableColumnAlign Align;
            public string? ForeColor;
            public string? BackColor;
            public bool? Bold;
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

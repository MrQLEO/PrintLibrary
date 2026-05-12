using PrintLibrary.Model;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

using ModelBarcode = PrintLibrary.Model.BarcodeFormat;

namespace PrintLibrary.Printer
{
    /// <summary>
    /// 基于 PdfSharp 的向量 PDF 输出打印机。
    ///
    /// 与 <see cref="PdfPrinter"/>（基于 SkiaSharp）的区别：
    ///   - 字体子集化：PdfSharp 自动子集化，CJK 字体只嵌入使用到的字形，体积极小
    ///   - 体积：A4 中文页约 50-200KB（vs SkiaSharp 向量 PDF 10-23MB）
    ///   - 字体限制：不支持 .ttc（TrueType Collection），如微软雅黑(MSYH.TTC)，
    ///     内置字体映射表自动回退到 .ttf 格式字体
    ///
    /// 适用场景：
    ///   - 需要小体积向量 PDF 的场景（屏幕浏览、邮件传输、Web 下载）
    ///   - 中文内容为主的标签/报表
    /// </summary>
    public class PdfSharpPrinter
    {
        // ── 常量 ──────────────────────────────────────────────────────────

        /// <summary>毫米 → 磅(pt) 换算因子：1mm = 72/25.4 pt</summary>
        private const double MmToPt = 72.0 / 25.4;

        // ── 配置属性 ──────────────────────────────────────────────────────

        /// <summary>PDF 元数据：标题。</summary>
        public string? Title { get; set; }

        /// <summary>PDF 元数据：作者。</summary>
        public string? Author { get; set; } = "PrintLibrary (PdfSharp)";

        /// <summary>
        /// 字体映射表。键为模板中使用的字体族名，值为 PdfSharp 可用的字体族名。
        /// <para>默认映射：Microsoft YaHei → SimHei（因为 PdfSharp 不支持 .ttc 格式）</para>
        /// <para>用户可自定义添加更多映射。</para>
        /// </summary>
        public Dictionary<string, string> FontMap { get; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Microsoft YaHei"] = "SimHei",
            ["微软雅黑"] = "SimHei",
        };

        /// <summary>
        /// 字体回退链：当主字体缺少某字符的 glyph 时，按此顺序尝试回退字体。
        /// <para>PdfSharp 不像 SkiaSharp 那样自动回退字体，遇到缺失 glyph 的字符会渲染为方框（□）。</para>
        /// <para>默认回退链：Arial Unicode MS（覆盖极广）→ Segoe UI Symbol（覆盖大部分 Unicode 符号）→ Arial（基本拉丁）</para>
        /// <para>用户可自定义添加更多回退字体。</para>
        /// </summary>
        public List<string> FontFallbackChain { get; } = new()
        {
            "Arial Unicode MS",
            "Segoe UI Symbol",
            "Arial"
        };

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
            document.Info.Title  = Title ?? template.Name;
            document.Info.Author = Author ?? "PrintLibrary";
            document.Info.Creator = "PrintLibrary v1.0 (PdfSharp)";

            foreach (var pageData in pages)
            {
                // 页面尺寸（pt）
                double widthPt  = template.Width  * MmToPt;
                double heightPt = template.Height * MmToPt;

                var page = document.AddPage();
                page.Width  = new XUnit(widthPt, XGraphicsUnit.Point);
                page.Height = new XUnit(heightPt, XGraphicsUnit.Point);

                var gfx = XGraphics.FromPdfPage(page);

                // 设置坐标系变换：mm → pt，后续绘制可直接使用毫米坐标
                var transform = new XMatrix();
                transform.ScaleAppend(MmToPt, MmToPt);
                gfx.MultiplyTransform(transform);

                // 背景色
                var bgColor = ParseColor(template.BackgroundColor);
                if (bgColor.A > 0)
                {
                    var bgBrush = new XSolidBrush(bgColor);
                    gfx.DrawRectangle(bgBrush, 0, 0, template.Width, template.Height);
                }

                // 渲染所有元素
                foreach (var element in template.Elements)
                {
                    if (!element.IsVisible) continue;
                    RenderElement(gfx, element, pageData);
                }
            }

            document.Save(output);
        }

        // ── 元素渲染分派 ──────────────────────────────────────────────────

        private void RenderElement(XGraphics gfx, LabelElement element, PrintData data)
        {
            switch (element)
            {
                case TextElement te:      RenderText(gfx, te, data); break;
                case LineElement le:      RenderLine(gfx, le); break;
                case RectangleElement re: RenderRectangle(gfx, re); break;
                case BarcodeElement be:   RenderBarcode(gfx, be, data); break;
                case TableElement tbl:    RenderTable(gfx, tbl); break;
                case ImageElement ie:     RenderImage(gfx, ie, data); break;
            }
        }

        // ── TextElement ────────────────────────────────────────────────────

        private void RenderText(XGraphics gfx, TextElement te, PrintData data)
        {
            string resolvedText = data.Resolve(te.Text);
            if (string.IsNullOrEmpty(resolvedText)) return;

            // 字号换算：画布已设置 mm→pt 缩放变换，XFont 字号单位为 pt，
            // 在缩放后的画布上直接用 pt 值会导致双重放大。
            // 正确做法：pt → mm，让画布缩放还原为真实 pt 值。
            // 与 SkiaSharp 版 TextElement.CreateFont() 逻辑一致。
            double fontSizeMm = te.FontSize / MmToPt;

            var font = CreateXFont(te.FontFamily, fontSizeMm, te.Bold, te.Italic);
            var brush = new XSolidBrush(ParseColor(te.ForeColor));

            var rect = new XRect(te.X, te.Y, te.Width, te.Height);

            // 计算文字位置
            var format = new XStringFormat
            {
                LineAlignment = XLineAlignment.Center  // 垂直居中
            };

            switch (te.Alignment)
            {
                case TextAlignment.Center:
                    format.Alignment = XStringAlignment.Center;
                    break;
                case TextAlignment.Right:
                    format.Alignment = XStringAlignment.Far;
                    break;
                default:
                    format.Alignment = XStringAlignment.Near;
                    break;
            }

            if (te.WordWrap)
            {
                DrawWrappedText(gfx, font, brush, resolvedText, rect, format, te, fontSizeMm);
            }
            else
            {
                // 单行绘制：为避免符号/标点等字符被矩形边界裁剪导致显示不全，
                // 给绘制矩形增加左右和上下余量，让文字完整渲染。
                // 余量取字号的 20%（约等于典型 glyph 的侧 bearing）。
                double padMm = fontSizeMm * 0.2;
                var drawRect = new XRect(
                    rect.X - padMm,
                    rect.Y - padMm,
                    rect.Width + padMm * 2,
                    rect.Height + padMm * 2);
                DrawStringWithFallback(gfx, resolvedText, font, brush, drawRect, format, fontSizeMm);
            }
        }

        private void DrawWrappedText(XGraphics gfx, XFont font, XBrush brush,
            string text, XRect rect, XStringFormat format, TextElement te, double fontSizeMm)
        {
            // 字号已在 RenderText 中转为 mm 值传入 XFont
            // font.GetHeight() 返回的是 XFont 字号单位的行高（当前为 mm 坐标系下的值）
            double lineHtMm = font.GetHeight();
            double lineSpacingMm = lineHtMm * 1.2;

            double y = rect.Y;
            string currentLine = "";

            // 按空格/字符逐词拆分
            var words = text.Split(' ');

            foreach (var word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var size = gfx.MeasureString(testLine, font);
                // MeasureString 在 mm 坐标系下返回的宽度已经是 mm 单位
                double widthMm = size.Width;

                if (widthMm > te.Width && !string.IsNullOrEmpty(currentLine))
                {
                    // 当前行已满，先绘制
                    var drawRect = new XRect(rect.X, y, te.Width, lineHtMm);
                    var lineFormat = new XStringFormat
                    {
                        Alignment = format.Alignment,
                        LineAlignment = XLineAlignment.Near
                    };
                    DrawStringWithFallback(gfx, currentLine, font, brush, drawRect, lineFormat, fontSizeMm);
                    y += lineSpacingMm;
                    currentLine = word;
                    if (y + lineHtMm > rect.Y + rect.Height) break;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            // 绘制最后一行
            if (!string.IsNullOrEmpty(currentLine) && y + lineHtMm <= rect.Y + rect.Height)
            {
                var drawRect = new XRect(rect.X, y, te.Width, lineHtMm);
                var lineFormat = new XStringFormat
                {
                    Alignment = format.Alignment,
                    LineAlignment = XLineAlignment.Near
                };
                DrawStringWithFallback(gfx, currentLine, font, brush, drawRect, lineFormat, fontSizeMm);
            }
        }

        // ── LineElement ────────────────────────────────────────────────────

        private static void RenderLine(XGraphics gfx, LineElement le)
        {
            var pen = new XPen(ParseColor(le.Color), le.LineWidthMm);

            if (le.DashPattern is { Length: > 0 })
            {
                pen.DashStyle = XDashStyle.Custom;
                pen.DashPattern = Array.ConvertAll(le.DashPattern, p => (double)p);
            }

            gfx.DrawLine(pen, le.X, le.Y, le.X + le.Width, le.Y + le.Height);
        }

        // ── RectangleElement ───────────────────────────────────────────────

        private static void RenderRectangle(XGraphics gfx, RectangleElement re)
        {
            var fillColor = ParseColor(re.FillColor);
            if (fillColor.A > 0)
            {
                var fillBrush = new XSolidBrush(fillColor);
                if (re.CornerRadiusMm > 0)
                    gfx.DrawRoundedRectangle(fillBrush, re.X, re.Y, re.Width, re.Height, re.CornerRadiusMm, re.CornerRadiusMm);
                else
                    gfx.DrawRectangle(fillBrush, re.X, re.Y, re.Width, re.Height);
            }

            var borderColor = ParseColor(re.BorderColor);
            if (borderColor.A > 0 && re.BorderWidthMm > 0)
            {
                var pen = new XPen(borderColor, re.BorderWidthMm);
                if (re.CornerRadiusMm > 0)
                    gfx.DrawRoundedRectangle(pen, re.X, re.Y, re.Width, re.Height, re.CornerRadiusMm, re.CornerRadiusMm);
                else
                    gfx.DrawRectangle(pen, re.X, re.Y, re.Width, re.Height);
            }
        }

        // ── BarcodeElement ─────────────────────────────────────────────────

        private void RenderBarcode(XGraphics gfx, BarcodeElement be, PrintData data)
        {
            string content = data.Resolve(be.Value);
            if (string.IsNullOrEmpty(content)) return;

            var bitMatrix = EncodeToBitMatrix(content, be.Format, be.QrErrorLevel);
            if (bitMatrix is null) return;

            int mw = bitMatrix.Width;
            int mh = bitMatrix.Height;
            double moduleW = be.Width / mw;
            double moduleH = be.Height / mh;

            // 背景
            var bgBrush = new XSolidBrush(ParseColor(be.BackColor));
            gfx.DrawRectangle(bgBrush, be.X, be.Y, be.Width, be.Height);

            // 前景模块
            var fgBrush = new XSolidBrush(ParseColor(be.ForeColor));
            for (int row = 0; row < mh; row++)
            {
                for (int col = 0; col < mw; col++)
                {
                    if (bitMatrix[col, row])
                    {
                        double rx = be.X + col * moduleW;
                        double ry = be.Y + row * moduleH;
                        gfx.DrawRectangle(fgBrush, rx, ry, moduleW, moduleH);
                    }
                }
            }

            // 一维码下方文本
            if (be.ShowText && be.Format != ModelBarcode.QrCode &&
                be.Format != ModelBarcode.DataMatrix &&
                be.Format != ModelBarcode.Aztec)
            {
                double textHeightMm = be.Height * 0.12; // 文本占条码高度的 12%
                // 画布在 mm 坐标系下，XFont 字号需传 mm 值（画布缩放会还原为 pt）
                double fontSizeMm = textHeightMm;
                var font = CreateXFont("SimHei", fontSizeMm, false, false);
                var textBrush = new XSolidBrush(ParseColor(be.ForeColor));
                var textRect = new XRect(be.X, be.Y + be.Height - textHeightMm * 1.5, be.Width, textHeightMm * 2);
                var fmt = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
                DrawStringWithFallback(gfx, content, font, textBrush, textRect, fmt, fontSizeMm);
            }
        }

        private static BitMatrix? EncodeToBitMatrix(string content, ModelBarcode format, QrErrorCorrectionLevel qrLevel)
        {
            var hints = new Dictionary<EncodeHintType, object>();
            if (format == ModelBarcode.QrCode)
            {
                hints[EncodeHintType.ERROR_CORRECTION] = qrLevel switch
                {
                    QrErrorCorrectionLevel.L => ErrorCorrectionLevel.L,
                    QrErrorCorrectionLevel.Q => ErrorCorrectionLevel.Q,
                    QrErrorCorrectionLevel.H => ErrorCorrectionLevel.H,
                    _ => ErrorCorrectionLevel.M
                };
            }

            var zxingFormat = MapFormat(format);
            var writer = new MultiFormatWriter();

            try { return writer.encode(content, zxingFormat, 0, 0, hints); }
            catch { return null; }
        }

        private static ZXing.BarcodeFormat MapFormat(ModelBarcode format) => format switch
        {
            ModelBarcode.Code128    => ZXing.BarcodeFormat.CODE_128,
            ModelBarcode.Code39     => ZXing.BarcodeFormat.CODE_39,
            ModelBarcode.Ean13      => ZXing.BarcodeFormat.EAN_13,
            ModelBarcode.Ean8       => ZXing.BarcodeFormat.EAN_8,
            ModelBarcode.UpcA       => ZXing.BarcodeFormat.UPC_A,
            ModelBarcode.QrCode     => ZXing.BarcodeFormat.QR_CODE,
            ModelBarcode.DataMatrix => ZXing.BarcodeFormat.DATA_MATRIX,
            ModelBarcode.Pdf417     => ZXing.BarcodeFormat.PDF_417,
            ModelBarcode.Aztec      => ZXing.BarcodeFormat.AZTEC,
            ModelBarcode.Itf        => ZXing.BarcodeFormat.ITF,
            _                       => ZXing.BarcodeFormat.CODE_128
        };

        // ── TableElement ───────────────────────────────────────────────────

        private void RenderTable(XGraphics gfx, TableElement tbl)
        {
            if (tbl.Columns.Count == 0) return;

            float[] colWidths = CalcColumnWidths(tbl);
            int totalRows = 1 + tbl.Rows.Count;
            float tableX = tbl.X, tableY = tbl.Y;

            // 1. 绘制奇偶行背景
            DrawTableRowBackgrounds(gfx, tbl, tableX, tableY, totalRows);

            // 2. 绘制网格线
            DrawTableGridLines(gfx, tbl, tableX, tableY, colWidths, totalRows);

            // 3. 绘制表头文字
            DrawTableHeader(gfx, tbl, tableX, tableY, colWidths);

            // 4. 绘制数据行文字
            DrawTableRows(gfx, tbl, tableX, tableY, colWidths);
        }

        private static float[] CalcColumnWidths(TableElement tbl)
        {
            float[] widths = new float[tbl.Columns.Count];
            float specified = 0f;
            int autoCount = 0;

            for (int i = 0; i < tbl.Columns.Count; i++)
            {
                if (tbl.Columns[i].Width > 0)
                {
                    widths[i] = tbl.Columns[i].Width;
                    specified += tbl.Columns[i].Width;
                }
                else
                {
                    autoCount++;
                }
            }

            float remaining = Math.Max(0, tbl.Width - specified);
            float autoWidth = autoCount > 0 ? remaining / autoCount : 0f;

            for (int i = 0; i < tbl.Columns.Count; i++)
            {
                if (widths[i] <= 0)
                    widths[i] = autoWidth;
            }

            return widths;
        }

        private static void DrawTableRowBackgrounds(XGraphics gfx, TableElement tbl,
            float tableX, float tableY, int totalRows)
        {
            // 表头背景
            var headerBg = ParseColor(tbl.HeaderBackColor);
            if (headerBg.A > 0)
            {
                var brush = new XSolidBrush(headerBg);
                gfx.DrawRectangle(brush, tableX, tableY, tbl.Width, tbl.RowHeight);
            }

            // 数据行背景
            for (int i = 0; i < tbl.Rows.Count; i++)
            {
                var bgColor = (i % 2 == 0) ? ParseColor(tbl.OddRowBackColor) : ParseColor(tbl.EvenRowBackColor);
                if (bgColor.A > 0)
                {
                    float rowY = tableY + (i + 1) * tbl.RowHeight;
                    var brush = new XSolidBrush(bgColor);
                    gfx.DrawRectangle(brush, tableX, rowY, tbl.Width, tbl.RowHeight);
                }
            }
        }

        private static void DrawTableGridLines(XGraphics gfx, TableElement tbl,
            float tableX, float tableY, float[] colWidths, int totalRows)
        {
            float tableH = tbl.RowHeight * totalRows;
            var gridClr = ParseColor(tbl.GridColor);

            // 外边框
            if (tbl.BorderWidthMm > 0)
            {
                var borderPen = new XPen(gridClr, tbl.BorderWidthMm);
                gfx.DrawRectangle(borderPen, tableX, tableY, tbl.Width, tableH);
            }

            // 内部网格线
            if (tbl.GridLineWidthMm <= 0) return;
            var gridPen = new XPen(gridClr, tbl.GridLineWidthMm);

            // 水平线
            for (int row = 1; row < totalRows; row++)
            {
                float lineY = tableY + row * tbl.RowHeight;
                gfx.DrawLine(gridPen, tableX, lineY, tableX + tbl.Width, lineY);
            }

            // 垂直线
            float colX = tableX;
            for (int col = 0; col < colWidths.Length - 1; col++)
            {
                colX += colWidths[col];
                gfx.DrawLine(gridPen, colX, tableY, colX, tableY + tableH);
            }
        }

        private void DrawTableHeader(XGraphics gfx, TableElement tbl,
            float tableX, float tableY, float[] colWidths)
        {
            var font = CreateXFont(tbl.FontFamily, tbl.HeaderFontSize / MmToPt, tbl.HeaderBold, false);
            var brush = new XSolidBrush(ParseColor(tbl.HeaderForeColor));

            float colX = tableX;
            for (int i = 0; i < tbl.Columns.Count; i++)
            {
                var cellRect = new XRect(colX, tableY, colWidths[i], tbl.RowHeight);
                DrawCellText(gfx, font, brush, tbl.Columns[i].Header, cellRect, tbl.Columns[i].Align);
                colX += colWidths[i];
            }
        }

        private void DrawTableRows(XGraphics gfx, TableElement tbl,
            float tableX, float tableY, float[] colWidths)
        {
            var font = CreateXFont(tbl.FontFamily, tbl.RowFontSize / MmToPt, false, false);
            var brush = new XSolidBrush(ParseColor(tbl.RowForeColor));

            for (int rowIdx = 0; rowIdx < tbl.Rows.Count; rowIdx++)
            {
                var row = tbl.Rows[rowIdx];
                float rowY = tableY + (rowIdx + 1) * tbl.RowHeight;
                float colX = tableX;

                for (int colIdx = 0; colIdx < tbl.Columns.Count; colIdx++)
                {
                    var col = tbl.Columns[colIdx];
                    string cellText = GetCellValue(row, col);
                    var cellRect = new XRect(colX, rowY, colWidths[colIdx], tbl.RowHeight);
                    DrawCellText(gfx, font, brush, cellText, cellRect, col.Align);
                    colX += colWidths[colIdx];
                }
            }
        }

        private void DrawCellText(XGraphics gfx, XFont font, XBrush brush,
            string text, XRect cellRect, TableColumnAlign align)
        {
            if (string.IsNullOrEmpty(text)) return;

            var fmt = new XStringFormat
            {
                LineAlignment = XLineAlignment.Center
            };

            // 添加内边距
            double padding = 1.0; // 1mm 内边距
            var drawRect = new XRect(cellRect.X + padding, cellRect.Y,
                cellRect.Width - padding * 2, cellRect.Height);

            switch (align)
            {
                case TableColumnAlign.Center:
                    fmt.Alignment = XStringAlignment.Center;
                    break;
                case TableColumnAlign.Right:
                    fmt.Alignment = XStringAlignment.Far;
                    break;
                default:
                    fmt.Alignment = XStringAlignment.Near;
                    break;
            }

            // fontSizeMm 从 XFont 的 Size 属性获取（已经过 pt→mm 换算）
            double fontSizeMm = font.Size;
            DrawStringWithFallback(gfx, text, font, brush, drawRect, fmt, fontSizeMm);
        }

        private static string GetCellValue(Dictionary<string, object?> row, TableColumn col)
        {
            if (!row.TryGetValue(col.Field, out var value) || value is null)
                return string.Empty;

            if (!string.IsNullOrEmpty(col.Format) && value is IFormattable formattable)
                return formattable.ToString(col.Format, null);

            return value.ToString() ?? string.Empty;
        }

        // ── ImageElement ───────────────────────────────────────────────────

        private static void RenderImage(XGraphics gfx, ImageElement ie, PrintData data)
        {
            XImage? xImage = null;
            try
            {
                // 从 Base64 加载
                if (!string.IsNullOrEmpty(ie.ImageBase64))
                {
                    var base64 = ie.ImageBase64.Contains(',')
                        ? ie.ImageBase64.Substring(ie.ImageBase64.IndexOf(',') + 1)
                        : ie.ImageBase64;
                    try
                    {
                        var bytes = Convert.FromBase64String(base64);
                        xImage = XImage.FromStream(() => new MemoryStream(bytes));
                    }
                    catch { /* 解码失败，尝试文件路径 */ }
                }

                // 从文件加载
                if (xImage is null && !string.IsNullOrEmpty(ie.FilePath) && File.Exists(ie.FilePath))
                {
                    try { xImage = XImage.FromFile(ie.FilePath); }
                    catch { /* 文件读取失败 */ }
                }

                if (xImage is null) return;

                // 根据 ScaleMode 计算绘制区域
                var destRect = CalcImageDrawRect(xImage, ie);
                gfx.DrawImage(xImage, destRect.X, destRect.Y, destRect.Width, destRect.Height);
            }
            finally
            {
                xImage?.Dispose();
            }
        }

        private static XRect CalcImageDrawRect(XImage xImage, ImageElement ie)
        {
            // 原图尺寸换算为 mm（假设 96 DPI）
            double srcWmm = xImage.PixelWidth * 25.4 / 96.0;
            double srcHmm = xImage.PixelHeight * 25.4 / 96.0;

            return ie.ScaleMode switch
            {
                ImageScaleMode.Stretch => new XRect(ie.X, ie.Y, ie.Width, ie.Height),

                ImageScaleMode.Uniform => ScaleUniform(
                    srcWmm, srcHmm, new XRect(ie.X, ie.Y, ie.Width, ie.Height), fillMode: false),

                ImageScaleMode.UniformToFill => ScaleUniform(
                    srcWmm, srcHmm, new XRect(ie.X, ie.Y, ie.Width, ie.Height), fillMode: true),

                _ => new XRect(ie.X, ie.Y, ie.Width, ie.Height)
            };
        }

        private static XRect ScaleUniform(double srcW, double srcH, XRect dest, bool fillMode)
        {
            double scaleX = dest.Width / srcW;
            double scaleY = dest.Height / srcH;
            double scale = fillMode ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);

            double scaledW = srcW * scale;
            double scaledH = srcH * scale;

            double left = dest.X + (dest.Width - scaledW) / 2.0;
            double top = dest.Y + (dest.Height - scaledH) / 2.0;

            return new XRect(left, top, scaledW, scaledH);
        }

        // ── 辅助方法 ──────────────────────────────────────────────────────

        // ── 字体回退绘制 ──────────────────────────────────────────────────

        /// <summary>
        /// 带字体回退的 DrawString。PdfSharp 不像 SkiaSharp 那样自动回退字体，
        /// 遇到缺失 glyph 的字符会渲染为方框（□）。此方法将文本按字符分组，
        /// 主字体不支持的特殊符号字符使用回退字体渲染。
        /// </summary>
        private void DrawStringWithFallback(XGraphics gfx, string text, XFont primaryFont,
            XBrush brush, XRect rect, XStringFormat format, double fontSizeMm)
        {
            // 快速路径：如果文本中没有需要回退的字符，直接用主字体绘制
            if (!HasFallbackChars(text, primaryFont))
            {
                gfx.DrawString(text, primaryFont, brush, rect, format);
                return;
            }

            // 慢路径：按字符分组绘制
            // 1. 将文本分为连续段：主字体段 / 回退字体段
            var segments = SplitByFontSupport(text, primaryFont, fontSizeMm);
            if (segments.Count == 1 && segments[0].Font == primaryFont)
            {
                // 分组后实际只有主字体，直接绘制
                gfx.DrawString(text, primaryFont, brush, rect, format);
                return;
            }

            // 2. 计算整体文本在 rect 中的起始位置（根据对齐方式）
            double totalWidthMm = 0;
            foreach (var seg in segments)
                totalWidthMm += gfx.MeasureString(seg.Text, seg.Font).Width;

            double x;
            switch (format.Alignment)
            {
                case XStringAlignment.Center:
                    x = rect.X + (rect.Width - totalWidthMm) / 2;
                    break;
                case XStringAlignment.Far:
                    x = rect.X + rect.Width - totalWidthMm;
                    break;
                default:
                    x = rect.X;
                    break;
            }

            // 垂直居中：用 rect 的 Y + (Height - lineHeight) / 2
            double lineHtMm = primaryFont.GetHeight();
            double y;
            switch (format.LineAlignment)
            {
                case XLineAlignment.Center:
                    y = rect.Y + (rect.Height - lineHtMm) / 2;
                    break;
                case XLineAlignment.Far:
                    y = rect.Y + rect.Height - lineHtMm;
                    break;
                default:
                    y = rect.Y;
                    break;
            }

            // 3. 逐段绘制，每段用对应字体
            foreach (var seg in segments)
            {
                gfx.DrawString(seg.Text, seg.Font, brush, x, y + lineHtMm);
                x += gfx.MeasureString(seg.Text, seg.Font).Width;
            }
        }

        /// <summary>
        /// 检查文本中是否包含主字体可能不支持、需要回退的字符。
        /// 基于 Unicode 范围启发式判断：特殊符号区域的 glyph 在大多数字体中缺失。
        /// </summary>
        private static bool HasFallbackChars(string text, XFont primaryFont)
        {
            foreach (char c in text)
            {
                if (IsSpecialSymbolChar(c))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 判断字符是否属于"特殊符号"Unicode 范围——这些范围在 CJK 字体
        /// （如 SimHei）中通常缺少 glyph，需要回退字体支持。
        /// <para>注意：CJK 相关范围的字符（U+4E00..U+9FFF、U+FF00..U+FFEF 等）
        /// CJK 字体本身就有 glyph，不需要回退。这里只列出 CJK 字体确实缺少的范围。</para>
        /// </summary>
        private static bool IsSpecialSymbolChar(char c)
        {
            // 以下 Unicode 范围的字符在 CJK 字体（SimHei、SimSun 等）中通常缺少 glyph：
            // - Latin-1 Supplement: U+0080..U+00FF（Øø Åå Ææ ß ± ÷ © ® ° 等）
            //   注：不含基本拉丁（U+0020..U+007F），那些字符所有字体都有
            // - Latin Extended-A/B: U+0100..U+024F
            // - General Punctuation: U+2000..U+206F（— – † ‡ • … ‰ ※ 等）
            // - Currency Symbols: U+20A0..U+20CF（€ ₹ ₽ ₩ 等，注意 ¥ U+00A5 在 Latin-1 里）
            // - Letterlike Symbols: U+2100..U+214F（℃ ℉ № ™ ℠ © ®）
            // - Arrows: U+2190..U+21FF（← → ↑ ↓ ⇐ ⇒）
            // - Math Operators: U+2200..U+22FF（∀ ∂ ∃ ∑ √ ∞ ≈ ≠ ≤ ≥ × ÷）
            // - Misc Technical: U+2300..U+23FF（⌀ ⌐ ⌠ ⌡）
            // - Enclosed Alphanumerics: U+2460..U+24FF（① ② ⑴ ⒈）
            // - Box Drawing: U+2500..U+257F（─ │ ┌ ┐ └ ┘ ├ ┤ ┬ ┴ ┼）
            // - Block Elements: U+2580..U+259F（▀ ▄ █ ▌ ▐）
            // - Geometric Shapes: U+25A0..U+25FF（■ □ ▲ △ ◆ ◇ ● ○ ★ ☆）
            // - Misc Symbols: U+2600..U+26FF（☀ ☁ ☂ ☃ ★ ☎ ☑ ✔ ✘ ♀ ♂ ♠ ♣ ♥ ♦）
            // - Dingbats: U+2700..U+27BF（✁ ✂ ✈ ✉ ✔ ✘ ✦ ✧ ❝ ❞）
            // - CJK Compatibility: U+3300..U+33FF（㈱ ㈲ ㈳ —— 这些 SimHei 可能也没有）
            // - Alphabetic Presentation Forms: U+FB00..U+FB4F（连字 ﬀ ﬁ 等）
            // - Specials: U+FFF0..U+FFFD
            //
            // 注意：以下范围 CJK 字体本身就有 glyph，不需要回退：
            // - U+4E00..U+9FFF (CJK Unified Ideographs)
            // - U+FF00..U+FFEF (Halfwidth/Fullwidth Forms，如 ￥ U+FFE5)
            // - U+3000..U+303F (CJK Symbols and Punctuation)
            // - U+3040..U+30FF (Hiragana + Katakana)
            return (c >= '\u0080' && c <= '\u024F') ||  // Latin-1 Supplement + Latin Extended
                   (c >= '\u2000' && c <= '\u206F') ||  // General Punctuation
                   (c >= '\u20A0' && c <= '\u20CF') ||  // Currency Symbols
                   (c >= '\u2100' && c <= '\u214F') ||  // Letterlike Symbols
                   (c >= '\u2190' && c <= '\u21FF') ||  // Arrows
                   (c >= '\u2200' && c <= '\u22FF') ||  // Math Operators
                   (c >= '\u2300' && c <= '\u23FF') ||  // Misc Technical
                   (c >= '\u2460' && c <= '\u24FF') ||  // Enclosed Alphanumerics
                   (c >= '\u2500' && c <= '\u257F') ||  // Box Drawing
                   (c >= '\u2580' && c <= '\u259F') ||  // Block Elements
                   (c >= '\u25A0' && c <= '\u25FF') ||  // Geometric Shapes
                   (c >= '\u2600' && c <= '\u26FF') ||  // Misc Symbols
                   (c >= '\u2700' && c <= '\u27BF') ||  // Dingbats
                   (c >= '\u3300' && c <= '\u33FF') ||  // CJK Compatibility
                   (c >= '\uFB00' && c <= '\uFB4F') ||  // Alphabetic Presentation Forms
                   (c >= '\uFFF0' && c <= '\uFFFD');    // Specials
        }

        /// <summary>
        /// 将文本按字体支持情况分段：主字体支持的字符归主字体段，
        /// 需要回退的字符归回退字体段。
        /// </summary>
        private List<TextSegment> SplitByFontSupport(string text, XFont primaryFont, double fontSizeMm)
        {
            var result = new List<TextSegment>();
            var sb = new StringBuilder();
            bool currentIsPrimary = true;  // 当前段是否使用主字体

            foreach (char c in text)
            {
                bool needsFallback = IsSpecialSymbolChar(c);

                if (needsFallback == currentIsPrimary)
                {
                    // 同一段，继续追加
                    sb.Append(c);
                }
                else
                {
                    // 字体切换，先提交当前段
                    if (sb.Length > 0)
                    {
                        result.Add(new TextSegment(
                            sb.ToString(),
                            currentIsPrimary ? primaryFont : GetFallbackFont(fontSizeMm)));
                        sb.Clear();
                    }
                    currentIsPrimary = !currentIsPrimary;
                    sb.Append(c);
                }
            }

            // 提交最后一段
            if (sb.Length > 0)
            {
                result.Add(new TextSegment(
                    sb.ToString(),
                    currentIsPrimary ? primaryFont : GetFallbackFont(fontSizeMm)));
            }

            return result;
        }

        /// <summary>
        /// 获取回退字体。按 FontFallbackChain 配置顺序尝试，第一个能成功创建的字体即为回退字体。
        /// </summary>
        private XFont GetFallbackFont(double fontSizeMm)
        {
            foreach (var family in FontFallbackChain)
            {
                try
                {
                    return new XFont(family, fontSizeMm, XFontStyle.Regular);
                }
                catch { /* 该字体不可用，尝试下一个 */ }
            }

            // 最终回退到 Arial
            return new XFont("Arial", fontSizeMm, XFontStyle.Regular);
        }

        /// <summary>
        /// 文本分段：一段文本 + 对应字体。
        /// </summary>
        private sealed record TextSegment(string Text, XFont Font);

        /// <summary>
        /// 创建 XFont，处理字体映射（.ttc → .ttf 回退）。
        /// </summary>
        /// <param name="fontFamily">字体族名</param>
        /// <param name="fontSizeInMmSpace">字号值，在 mm 坐标系下传给 XFont（XFont 以为它是 pt，
        /// 但经过画布 mm→pt 缩放后效果等同于真实 pt 字号）</param>
        /// <param name="bold">是否加粗</param>
        /// <param name="italic">是否斜体</param>
        private XFont CreateXFont(string fontFamily, double fontSizeInMmSpace, bool bold, bool italic)
        {
            string resolvedFamily = ResolveFontFamily(fontFamily);

            var style = XFontStyle.Regular;
            if (bold && italic) style = XFontStyle.BoldItalic;
            else if (bold) style = XFontStyle.Bold;
            else if (italic) style = XFontStyle.Italic;

            try
            {
                return new XFont(resolvedFamily, fontSizeInMmSpace, style);
            }
            catch (Exception ex) when (ex is System.IO.FileNotFoundException or ArgumentException or InvalidOperationException)
            {
                // 字体加载失败，回退到 SimHei（最通用的中文 .ttf 字体）
                try { return new XFont("SimHei", fontSizeInMmSpace, style); }
                catch { return new XFont("Arial", fontSizeInMmSpace, style); }
            }
        }

        /// <summary>
        /// 解析字体族名，处理 .ttc 兼容性问题。
        /// </summary>
        private string ResolveFontFamily(string fontFamily)
        {
            if (string.IsNullOrEmpty(fontFamily)) return "SimHei";

            // 检查字体映射表
            if (FontMap.TryGetValue(fontFamily, out var mapped))
                return mapped;

            return fontFamily;
        }

        /// <summary>
        /// 将 ARGB 十六进制颜色字符串解析为 XColor。
        /// </summary>
        private static XColor ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return XColors.Black;

            try
            {
                // 格式：#AARRGGBB 或 #RRGGBB
                string h = hex.TrimStart('#');
                if (h.Length == 8)
                {
                    byte a = Convert.ToByte(h.Substring(0, 2), 16);
                    byte r = Convert.ToByte(h.Substring(2, 2), 16);
                    byte g = Convert.ToByte(h.Substring(4, 2), 16);
                    byte b = Convert.ToByte(h.Substring(6, 2), 16);
                    return XColor.FromArgb(a, r, g, b);
                }
                if (h.Length == 6)
                {
                    byte r = Convert.ToByte(h.Substring(0, 2), 16);
                    byte g = Convert.ToByte(h.Substring(2, 2), 16);
                    byte b = Convert.ToByte(h.Substring(4, 2), 16);
                    return XColor.FromArgb(255, r, g, b);
                }
            }
            catch { /* 解析失败，返回黑色 */ }

            return XColors.Black;
        }
    }
}

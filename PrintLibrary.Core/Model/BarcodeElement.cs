using SkiaSharp;
using System;
using ZXing;
using ZXing.Common;
using ZXing.QrCode.Internal;

namespace PrintLibrary.Model
{
    /// <summary>条码格式枚举（一维码 + 二维码）。</summary>
    public enum BarcodeFormat
    {
        /// <summary>Code 128（通用一维条码，支持所有 ASCII）</summary>
        Code128,
        /// <summary>Code 39（工业常用一维条码）</summary>
        Code39,
        /// <summary>EAN-13（商品条码）</summary>
        Ean13,
        /// <summary>EAN-8</summary>
        Ean8,
        /// <summary>UPC-A</summary>
        UpcA,
        /// <summary>QR Code 二维码</summary>
        QrCode,
        /// <summary>Data Matrix 二维码</summary>
        DataMatrix,
        /// <summary>PDF 417 条码</summary>
        Pdf417,
        /// <summary>Aztec 二维码</summary>
        Aztec,
        /// <summary>ITF（交叉 25 码）</summary>
        Itf
    }

    /// <summary>
    /// 二维码容错等级（QR Code 专用）。
    /// </summary>
    public enum QrErrorCorrectionLevel
    {
        /// <summary>约 7%</summary>
        L,
        /// <summary>约 15%</summary>
        M,
        /// <summary>约 25%</summary>
        Q,
        /// <summary>约 30%</summary>
        H
    }

    /// <summary>
    /// 条码元素（一维码 / 二维码）。
    /// 使用 ZXing.Net 生成位矩阵（BitMatrix），然后通过 SkiaSharp 向量路径（SKPath）
    /// 绘制到画布——不生成位图，PDF 输出为向量图形，体积小且无限清晰。
    /// </summary>
    public class BarcodeElement : LabelElement
    {
        // ── 数据 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 条码内容，支持占位符语法。示例："{SerialNo}" 或 "固定前缀-{Id}"。
        /// </summary>
        public string Value { get; set; } = string.Empty;

        // ── 格式与样式 ────────────────────────────────────────────────────

        /// <summary>条码格式，默认 Code128。</summary>
        public BarcodeFormat Format { get; set; } = BarcodeFormat.Code128;

        /// <summary>
        /// QR Code 容错等级（仅对 QrCode 格式有效）。默认 M（约 15% 容错）。
        /// </summary>
        public QrErrorCorrectionLevel QrErrorLevel { get; set; } = QrErrorCorrectionLevel.M;

        /// <summary>
        /// 条码前景色（模块颜色），ARGB 十六进制，默认黑色 "#FF000000"。
        /// </summary>
        public string ForeColor { get; set; } = "#FF000000";

        /// <summary>
        /// 条码背景色，ARGB 十六进制，默认白色 "#FFFFFFFF"。
        /// </summary>
        public string BackColor { get; set; } = "#FFFFFFFF";

        /// <summary>是否在条码下方显示可读文本（仅一维码有效）。默认 false。</summary>
        public bool ShowText { get; set; } = false;

        // ── 绘制实现 ──────────────────────────────────────────────────────

        /// <inheritdoc />
        public override void Draw(SKCanvas canvas, SKMatrix mmToPixelMatrix, PrintData data)
        {
            if (!IsVisible) return;

            // 1. 解析数据绑定
            string content = data.Resolve(Value);
            if (string.IsNullOrEmpty(content)) return;

            // 2. 用 ZXing 编码为 BitMatrix
            var bitMatrix = EncodeToBitMatrix(content);
            if (bitMatrix is null) return;

            // 3. 在 mm 坐标系下向量绘制条码（清晰 + PDF 体积小）
            DrawVectorBarcode(canvas, bitMatrix, content);
        }

        // ── 私有辅助 ──────────────────────────────────────────────────────

        /// <summary>
        /// 使用 ZXing 将内容编码为 BitMatrix。
        /// </summary>
        private BitMatrix? EncodeToBitMatrix(string content)
        {
            var hints = new Dictionary<EncodeHintType, object>();
            if (Format == BarcodeFormat.QrCode)
            {
                hints[EncodeHintType.ERROR_CORRECTION] = QrErrorLevel switch
                {
                    QrErrorCorrectionLevel.L => ErrorCorrectionLevel.L,
                    QrErrorCorrectionLevel.M => ErrorCorrectionLevel.M,
                    QrErrorCorrectionLevel.Q => ErrorCorrectionLevel.Q,
                    QrErrorCorrectionLevel.H => ErrorCorrectionLevel.H,
                    _                        => ErrorCorrectionLevel.M
                };
            }

            // 一维码左右静音区（模块数）：过小会导致扫码枪/手机难以识别
            if (IsLinear1DBarcode(Format))
                hints[EncodeHintType.MARGIN] = 10;

            var zxingFormat = MapFormat(Format);
            var writer = new MultiFormatWriter();

            try { return writer.encode(content, zxingFormat, 0, 0, hints); }
            catch { return null; }
        }

        /// <summary>
        /// 向量绘制条码：直接将 BitMatrix 的每个模块作为矩形路径绘入画布。
        /// 无位图生成，PDF 输出为纯向量图形，体积小、无限缩放不失真。
        /// </summary>
        private void DrawVectorBarcode(SKCanvas canvas, BitMatrix matrix, string resolvedContent)
        {
            int mw = matrix.Width;
            int mh = matrix.Height;

            // 目标区域（mm 坐标）
            float destX = X, destY = Y, destW = Width, destH = Height;

            // 一维码底部可读文字（与 PdfSharpPrinter 行为对齐）
            float textBandMm = 0f;
            if (ShowText && IsLinear1DBarcode(Format))
                textBandMm = Math.Clamp(Height * 0.14f, 1.6f, 6f);

            float barTop = destY;
            float barH = Math.Max(0.5f, destH - textBandMm);

            // 每个模块的 mm 尺寸（用相邻列/行边界相减，避免浮点缝隙）
            float moduleW = destW / mw;
            float moduleH = barH / mh;

            // 绘制背景（整块区域含文字带）
            using var bgPaint = new SKPaint { Color = ParseColor(BackColor), IsAntialias = false, Style = SKPaintStyle.Fill };
            canvas.DrawRect(new SKRect(destX, destY, destX + destW, destY + destH), bgPaint);

            // 绘制前景模块
            using var fgPaint = new SKPaint { Color = ParseColor(ForeColor), IsAntialias = false, Style = SKPaintStyle.Fill };
            for (int row = 0; row < mh; row++)
            {
                float ry = barTop + row * moduleH;
                float ryNext = barTop + (row + 1) * moduleH;
                float rh = ryNext - ry;
                for (int col = 0; col < mw; col++)
                {
                    if (!matrix[col, row]) continue;

                    float rx = destX + col * moduleW;
                    float rxNext = destX + (col + 1) * moduleW;
                    canvas.DrawRect(rx, ry, rxNext - rx, rh, fgPaint);
                }
            }

            if (textBandMm <= 0.01f || string.IsNullOrEmpty(resolvedContent)) return;

            using var tf = SKTypeface.FromFamilyName("Microsoft YaHei", SKFontStyle.Normal) ?? SKTypeface.Default;
            float textSizeMm = Math.Clamp(textBandMm * 0.55f, 0.75f, 4f);
            using var tfont = new SKFont(tf, textSizeMm);
            using var tpaint = new SKPaint { Color = ParseColor(ForeColor), IsAntialias = true, Style = SKPaintStyle.Fill };
            var tm = tfont.Metrics;
            float bandTop = barTop + barH;
            float bandBot = destY + destH;
            float bandMidY = (bandTop + bandBot) * 0.5f;
            float textBaseY = bandMidY - (tm.Ascent + tm.Descent) / 2f;
            canvas.DrawText(resolvedContent, destX + destW * 0.5f, textBaseY, SKTextAlign.Center, tfont, tpaint);
        }

        /// <summary>是否为一维线性条码（需左右静音区、可配底部可读文字）。</summary>
        private static bool IsLinear1DBarcode(BarcodeFormat format) => format switch
        {
            BarcodeFormat.Code128 or BarcodeFormat.Code39 or BarcodeFormat.Ean13 or BarcodeFormat.Ean8
                or BarcodeFormat.UpcA or BarcodeFormat.Itf => true,
            _ => false
        };

        /// <summary>将本库的 <see cref="BarcodeFormat"/> 映射到 ZXing 的格式枚举。</summary>
        private static ZXing.BarcodeFormat MapFormat(BarcodeFormat format) => format switch
        {
            BarcodeFormat.Code128    => ZXing.BarcodeFormat.CODE_128,
            BarcodeFormat.Code39     => ZXing.BarcodeFormat.CODE_39,
            BarcodeFormat.Ean13      => ZXing.BarcodeFormat.EAN_13,
            BarcodeFormat.Ean8       => ZXing.BarcodeFormat.EAN_8,
            BarcodeFormat.UpcA       => ZXing.BarcodeFormat.UPC_A,
            BarcodeFormat.QrCode     => ZXing.BarcodeFormat.QR_CODE,
            BarcodeFormat.DataMatrix  => ZXing.BarcodeFormat.DATA_MATRIX,
            BarcodeFormat.Pdf417     => ZXing.BarcodeFormat.PDF_417,
            BarcodeFormat.Aztec      => ZXing.BarcodeFormat.AZTEC,
            BarcodeFormat.Itf        => ZXing.BarcodeFormat.ITF,
            _                        => ZXing.BarcodeFormat.CODE_128
        };

        /// <summary>解析 ARGB 十六进制颜色字符串，失败时返回黑色。</summary>
        private static SKColor ParseColor(string hex)
        {
            if (SKColor.TryParse(hex, out var c)) return c;
            return SKColors.Black;
        }
    }
}

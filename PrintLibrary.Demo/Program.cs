// ============================================================
//  PrintLibrary.Demo - 完整使用示例
//  演示以下场景：
//    1. 构建条码标签模板（100×50 mm）并绑定数据后预览导出 PNG
//    2. 序列化模板为 JSON 再反序列化验证一致性
//    3. 输出 PDF 文件（跨平台留档 / Linux 打印）
//    4. 多种元素组合（文本、矩形、线条、图片、二维码）
//    5. 表格自动布局——传数据集合，不用手算坐标
//    6. 物理打印测试（使用 PrintDocumentPrinter）
// ============================================================

using PrintLibrary.Model;
using PrintLibrary.Preview;
using PrintLibrary.Printer;
using PrintLibrary.Serialization;


// ── 输出目录 ────────────────────────────────────────────────
// 每次运行先清空上次的产物，避免反复调试后 output 目录堆积大量旧文件。
// 注意：Core 库全程使用内存流（SKDynamicMemoryWStream / 直写 FileStream），
// 本身不产生中间临时文件，这里只清理 Demo 自己生成的最终产物目录。
string outputDir = Path.Combine(AppContext.BaseDirectory, "output");
if (Directory.Exists(outputDir))
    Directory.Delete(outputDir, recursive: true);
Directory.CreateDirectory(outputDir);
Console.WriteLine($"输出目录：{outputDir}");
Console.WriteLine();

// ═══════════════════════════════════════════════════════════
// 示例 1：条码标签（100×50 mm）
// ═══════════════════════════════════════════════════════════
Console.WriteLine("=== 示例1：条码标签 ===");

// 1a. 构建模板
var barcodeTemplate = new LabelTemplate
{
    Name   = "入库条码标签",
    Width  = 100f,   // 100 mm
    Height = 50f,    // 50 mm
    BackgroundColor = "#FFFFFFFF"
};

// 外边框（0.5mm 黑色边框）
barcodeTemplate.Add(new RectangleElement
{
    Name          = "border",
    X             = 0.5f, Y = 0.5f,
    Width         = 99f,  Height = 49f,
    BorderColor   = "#FF000000",
    BorderWidthMm = 0.3f
});

// 产品名称（顶部居中，微软雅黑 14pt）
barcodeTemplate.Add(new TextElement
{
    Name       = "productName",
    X          = 2f, Y = 2f,
    Width      = 96f, Height = 10f,
    Text       = "{ProductName}",
    FontFamily = "Microsoft YaHei",
    FontSize   = 14f,
    Bold       = true,
    Alignment  = TextAlignment.Center,
    ForeColor  = "#FF1A1A2E"
});

// 分隔线
barcodeTemplate.Add(new LineElement
{
    Name        = "divider",
    X           = 2f, Y = 13f,
    Width       = 96f, Height = 0f,
    Color       = "#FFAAAAAA",
    LineWidthMm = 0.2f
});

// 条形码（Code128，高 25mm，居中）
barcodeTemplate.Add(new BarcodeElement
{
    Name     = "barcode",
    X        = 10f, Y = 15f,
    Width    = 80f, Height = 25f,
    Format   = BarcodeFormat.Code128,
    Value    = "{Barcode}",
    ShowText = false
});

// 序列号文本（底部居中）
barcodeTemplate.Add(new TextElement
{
    Name       = "serialNo",
    X          = 2f, Y = 42f,
    Width      = 60f, Height = 6f,
    Text       = "S/N: {SerialNo}",
    FontFamily = "Arial",
    FontSize   = 8f,
    ForeColor  = "#FF444444"
});

// 日期文本（底部右对齐）
barcodeTemplate.Add(new TextElement
{
    Name       = "date",
    X          = 62f, Y = 42f,
    Width      = 36f, Height = 6f,
    Text       = "{Date:yyyy-MM-dd}",
    FontFamily = "Arial",
    FontSize   = 8f,
    Alignment  = TextAlignment.Right,
    ForeColor  = "#FF444444"
});

// 1b. 绑定数据
var barcodeData = new PrintData()
    .Set("ProductName", "精密螺丝 M3×8")
    .Set("Barcode",     "1234567890128")
    .Set("SerialNo",    "SN-2026051201")
    .Set("Date",        DateTime.Today);

// 1c. 渲染为 PNG（300 DPI，带调试网格和边框）
var renderer = new TemplateRenderer
{
    Dpi        = 300f,
    ShowGrid   = true,
    ShowBorder = true,
    GridSpacingMm = 5f
};
string pngPath = Path.Combine(outputDir, "barcode_label.png");
renderer.SaveToPng(barcodeTemplate, pngPath, barcodeData);
Console.WriteLine($"  [OK] 预览图片已保存：{pngPath}");

// ═══════════════════════════════════════════════════════════
// 示例 2：JSON 序列化/反序列化
// ═══════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("=== 示例2：JSON 序列化 ===");

string jsonPath = Path.Combine(outputDir, "barcode_template.json");
TemplateSerializer.Save(barcodeTemplate, jsonPath);
Console.WriteLine($"  [OK] 模板已序列化：{jsonPath}");

// 重新加载验证
var reloaded = TemplateSerializer.Load(jsonPath);
Console.WriteLine($"  [OK] 反序列化成功，元素数量：{reloaded.Elements.Count}（原始：{barcodeTemplate.Elements.Count}）");

// ═══════════════════════════════════════════════════════════
// 示例 3：输出 PDF（跨平台留档）
// ═══════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("=== 示例3：导出 PDF ===");

var pdfPrinter = new PdfPrinter
{
    Title = "入库条码标签"
    // 向量模式（默认 RasterDpi=null）：文字为矢量图形，无限放大都清晰
    // 如需位图模式（体积小但文字模糊），设置 RasterDpi = 300f
};

// 模拟多页：3 个不同零件
var multiPageData = new System.Collections.Generic.List<PrintData>
{
    new PrintData().Set("ProductName", "精密螺丝 M3×8")
                   .Set("Barcode", "1234567890128")
                   .Set("SerialNo", "SN-2026051201")
                   .Set("Date", DateTime.Today),
    new PrintData().Set("ProductName", "六角螺母 M6")
                   .Set("Barcode", "9876543210987")
                   .Set("SerialNo", "SN-2026051202")
                   .Set("Date", DateTime.Today),
    new PrintData().Set("ProductName", "弹簧垫圈 Ø8")
                   .Set("Barcode", "1122334455667")
                   .Set("SerialNo", "SN-2026051203")
                   .Set("Date", DateTime.Today),
};

string pdfPath = Path.Combine(outputDir, "barcode_labels_3pages.pdf");
pdfPrinter.SaveToFile(barcodeTemplate, pdfPath, multiPageData);
Console.WriteLine($"  [OK] PDF 已保存（3页）：{pdfPath}");

// ═══════════════════════════════════════════════════════════
// 示例 4：A4 工单报表（使用 TableElement 自动布局）
// ═══════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("=== 示例4：A4 工单报表（表格自动布局）===");

var a4Template = LabelTemplate.A4Portrait();
a4Template.Name = "工单报表";

// 标题区域
a4Template.Add(new RectangleElement
{
    X = 5f, Y = 5f, Width = 200f, Height = 20f,
    FillColor   = "#FF1A1A2E",
    BorderColor = "#00000000"  // 无边框
});
a4Template.Add(new TextElement
{
    X = 5f, Y = 7f, Width = 200f, Height = 16f,
    Text       = "工  单  报  表",
    FontFamily = "Microsoft YaHei",
    FontSize   = 18f,
    Bold       = true,
    Alignment  = TextAlignment.Center,
    ForeColor  = "#FFFFFFFF"
});

// 基础信息行（双列布局）
float infoY = 32f;
a4Template.Add(new TextElement { X = 5f,   Y = infoY, Width = 95f, Height = 7f, Text = "工单号：WO-20260512-001",  FontSize = 10f });
a4Template.Add(new TextElement { X = 110f, Y = infoY, Width = 95f, Height = 7f, Text = "日  期：2026年05月12日",     FontSize = 10f });
a4Template.Add(new TextElement { X = 5f,   Y = infoY + 8f, Width = 95f, Height = 7f, Text = "产品名：高精度轴承座",  FontSize = 10f });
a4Template.Add(new TextElement { X = 110f, Y = infoY + 8f, Width = 95f, Height = 7f, Text = "数  量：500 件",         FontSize = 10f });

// 分隔线
a4Template.Add(new LineElement { X = 5f, Y = infoY + 18f, Width = 200f, Height = 0f, Color = "#FF888888", LineWidthMm = 0.2f });

// ★ 核心亮点：用 TableElement 传入数据集合，自动布局绘制表格
// 不需要手算每个单元格的坐标，只需声明列定义和数据行
a4Template.Add(new TableElement
{
    Name  = "detailTable",
    X     = 5f,
    Y     = 55f,
    Width = 200f,
    RowHeight = 8f,
    HeaderFontSize = 10f,
    RowFontSize    = 9f,
    BorderWidthMm  = 0.3f,
    GridLineWidthMm = 0.3f,  // 提高线宽，避免打印时丢失
    Columns = new()
    {
        new TableColumn { Header = "序号",  Field = "No",    Width = 20f,  Align = TableColumnAlign.Center },
        new TableColumn { Header = "零件名称", Field = "Name",  Width = 80f },
        new TableColumn { Header = "数量",  Field = "Qty",   Width = 30f,  Align = TableColumnAlign.Right },
        new TableColumn { Header = "单价(元)", Field = "Price", Width = 35f,  Align = TableColumnAlign.Right, Format = "N2" },
        new TableColumn { Header = "小计(元)", Field = "Total", Width = 35f,  Align = TableColumnAlign.Right, Format = "N2" },
    },
    Rows = new()
    {
        new() { ["No"] = 1, ["Name"] = "轴承内圈",     ["Qty"] = 200, ["Price"] = 12.50m, ["Total"] = 2500.00m },
        new() { ["Name"] = "轴承外圈",     ["Qty"] = 200, ["Price"] = 15.80m, ["Total"] = 3160.00m, ["No"] = 2 },
        new() { ["No"] = 3, ["Name"] = "密封圈",       ["Qty"] = 400, ["Price"] = 1.20m,  ["Total"] = 480.00m },
        new() { ["No"] = 4, ["Name"] = "保持架组件",    ["Qty"] = 200, ["Price"] = 8.60m,  ["Total"] = 1720.00m },
        new() { ["No"] = 5, ["Name"] = "滚珠 Ø6.5mm",  ["Qty"] = 1200, ["Price"] = 0.35m, ["Total"] = 420.00m },
        new() { ["No"] = 6, ["Name"] = "密封圈 ￥8/件", ["Qty"] = 100, ["Price"] = 8.00m, ["Total"] = 800.00m },
    }
});

// 汇总信息
float tableBottom = 55f + 8f * 7; // 表头 + 6行数据 = 7行 × 8mm
a4Template.Add(new TextElement
{
    X = 120f, Y = tableBottom + 3f, Width = 85f, Height = 7f,
    Text = "合计：¥8,280.00",
    FontSize = 11f, Bold = true,
    Alignment = TextAlignment.Right,
    ForeColor = "#FF1A1A2E"
});

// QR 码（右下角）
a4Template.Add(new BarcodeElement
{
    X = 165f, Y = tableBottom + 15f, Width = 35f, Height = 35f,
    Format       = BarcodeFormat.QrCode,
    Value        = "ORDER:WO-20260512-001",
    QrErrorLevel = QrErrorCorrectionLevel.M
});

// 备注区
a4Template.Add(new TextElement
{
    X = 5f, Y = tableBottom + 15f, Width = 155f, Height = 35f,
    Text       = "备注：此批零件用于一号产线装配，优先级高，请安排今日下午完成检验并入库。注意防潮防尘，码放整齐。",
    FontFamily = "Microsoft YaHei",
    FontSize   = 9f,
    WordWrap   = true,
    ClipContent = true,
    ForeColor  = "#FF333333"
});

// 渲染 A4 工单
var a4Data = new PrintData();

string a4PdfPath = Path.Combine(outputDir, "work_order.pdf");
new PdfPrinter { Title = "工单报表" }.SaveToFile(a4Template, a4PdfPath, a4Data);
Console.WriteLine($"  [OK] 工单 PDF 已保存：{a4PdfPath}");

string a4PngPath = Path.Combine(outputDir, "work_order_preview.png");
new TemplateRenderer { Dpi = 150f }.SaveToPng(a4Template, a4PngPath, a4Data);
Console.WriteLine($"  [OK] 工单预览图已保存：{a4PngPath}");

// ═══════════════════════════════════════════════════════════
// 示例 5：纯表格打印（最简用法）
// ═══════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("=== 示例5：纯表格打印（最简用法）===");

var tableOnlyTemplate = new LabelTemplate
{
    Name = "零件清单",
    Width = 100f, Height = 80f
};

tableOnlyTemplate.Add(new TableElement
{
    X = 2f, Y = 2f, Width = 96f,
    RowHeight = 6f,
    HeaderFontSize = 8f,
    RowFontSize = 7f,
    BorderWidthMm = 0.2f,
    GridLineWidthMm = 0.2f,  // 提高线宽，避免打印时丢失
    Columns = new()
    {
        new TableColumn { Header = "编号",  Field = "Id",   Width = 20f, Align = TableColumnAlign.Center },
        new TableColumn { Header = "名称",  Field = "Name", Width = 46f },
        new TableColumn { Header = "数量",  Field = "Qty",  Width = 30f, Align = TableColumnAlign.Right },
    },
    Rows = new()
    {
        new() { ["Id"] = "A001", ["Name"] = "螺丝 M3×8",  ["Qty"] = 500 },
        new() { ["Id"] = "A002", ["Name"] = "螺母 M6",     ["Qty"] = 300 },
        new() { ["Id"] = "A003", ["Name"] = "弹簧垫圈 Ø8", ["Qty"] = 1000 },
        new() { ["Id"] = "A004", ["Name"] = "平垫圈 Ø8",   ["Qty"] = 800 },
    }
});

string tablePdfPath = Path.Combine(outputDir, "parts_table.pdf");
new PdfPrinter { Title = "零件清单" }.SaveToFile(tableOnlyTemplate, tablePdfPath, new PrintData());
Console.WriteLine($"  [OK] 零件清单 PDF 已保存：{tablePdfPath}");

string tablePngPath = Path.Combine(outputDir, "parts_table_preview.png");
new TemplateRenderer { Dpi = 300f }.SaveToPng(tableOnlyTemplate, tablePngPath, new PrintData());
Console.WriteLine($"  [OK] 零件清单预览图已保存：{tablePngPath}");

// ═══════════════════════════════════════════════════════════
// 示例 5b：PdfSharp 向量 PDF 输出（体积对比）
// ═══════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("=== 示例5b：PdfSharp 向量 PDF（体积对比）===");

// 工单报表 - PdfSharp 版
string a4PdfSharpPath = Path.Combine(outputDir, "work_order_pdfsharp.pdf");
new HybridPdfPrinter { Title = "工单报表" }.SaveToFile(a4Template, a4PdfSharpPath, a4Data);
var a4SkiaSize = new FileInfo(a4PdfPath).Length;
var a4PdfSharpSize = new FileInfo(a4PdfSharpPath).Length;
Console.WriteLine($"  [OK] 工单 PdfSharp PDF 已保存：{a4PdfSharpPath}");
Console.WriteLine($"  [对比] SkiaSharp 向量 PDF: {a4SkiaSize / 1024.0:F0} KB  |  PdfSharp 向量 PDF: {a4PdfSharpSize / 1024.0:F0} KB");

// 条码标签 - PdfSharp 版
string barcodePdfSharpPath = Path.Combine(outputDir, "barcode_labels_3pages_pdfsharp.pdf");
new HybridPdfPrinter { Title = "入库条码标签" }.SaveToFile(barcodeTemplate, barcodePdfSharpPath, multiPageData);
var barcodeSkiaSize = new FileInfo(pdfPath).Length;
var barcodePdfSharpSize = new FileInfo(barcodePdfSharpPath).Length;
Console.WriteLine($"  [OK] 条码 PdfSharp PDF 已保存：{barcodePdfSharpPath}");
Console.WriteLine($"  [对比] SkiaSharp 向量 PDF: {barcodeSkiaSize / 1024.0:F0} KB  |  PdfSharp 向量 PDF: {barcodePdfSharpSize / 1024.0:F0} KB");

// 零件清单（含 Ø 符号）- PdfSharp 版
string partsPdfSharpPath = Path.Combine(outputDir, "parts_table_pdfsharp.pdf");
new HybridPdfPrinter { Title = "零件清单" }.SaveToFile(tableOnlyTemplate, partsPdfSharpPath, new PrintData());
Console.WriteLine($"  [OK] 零件清单 PdfSharp PDF 已保存：{partsPdfSharpPath}（含 Ø 符号字体回退测试）");

// ═══════════════════════════════════════════════════════════
// 示例 5c：表格合并单元格（ColSpan / RowSpan + 表头 ColSpan）
// ═══════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("=== 示例5c：表格合并单元格（ColSpan / RowSpan）===");

var spanTemplate = new LabelTemplate
{
    Name = "合并单元格示例",
    Width = 100f, Height = 70f,
    BackgroundColor = "#FFFFFFFF"
};
spanTemplate.Add(new TableElement
{
    X = 2f, Y = 2f, Width = 96f,
    RowHeight = 8f,
    HeaderFontSize = 8f,
    RowFontSize = 7f,
    BorderWidthMm = 0.3f,
    GridLineWidthMm = 0.2f,
    Columns = new()
    {
        new TableColumn { Header = "类别", Field = "Cat",   Width = 18f, Align = TableColumnAlign.Center },
        new TableColumn { Header = "项目", Field = "Item",  Width = 38f },
        new TableColumn { Header = "数量", Field = "Qty",   Width = 20f, Align = TableColumnAlign.Right },
        new TableColumn { Header = "单价", Field = "Price", Width = 20f, Align = TableColumnAlign.Right, Format = "N2" },
    },
    Rows = new()
    {
        // RowSpan 演示：类别「紧固件」纵向跨两行（下方行的 Cat 列无需再填）
        new() {
            ["Cat"]   = new TableCell { Text = "紧固件", RowSpan = 2, Align = TableColumnAlign.Center, Bold = true },
            ["Item"]  = "螺丝 M3×8",
            ["Qty"]   = 500,
            ["Price"] = 0.05m
        },
        new() {
            ["Item"]  = "螺母 M6",
            ["Qty"]   = 300,
            ["Price"] = 0.12m
        },
        // ColSpan 演示：合计行首格跨 3 列，金额单独居右
        new() {
            ["Cat"]   = new TableCell { Text = "合计", ColSpan = 3, Align = TableColumnAlign.Center, Bold = true },
            ["Price"] = 61.00m
        },
    }
});

string spanPngPath = Path.Combine(outputDir, "table_span_preview.png");
new TemplateRenderer { Dpi = 300f }.SaveToPng(spanTemplate, spanPngPath, new PrintData());
Console.WriteLine($"  [OK] 合并单元格预览图已保存：{spanPngPath}");

string spanPdfPath = Path.Combine(outputDir, "table_span.pdf");
new PdfPrinter { Title = "合并单元格示例" }.SaveToFile(spanTemplate, spanPdfPath, new PrintData());
Console.WriteLine($"  [OK] 合并单元格 PDF（SkiaSharp）已保存：{spanPdfPath}");

string spanPdfSharpPath = Path.Combine(outputDir, "table_span_pdfsharp.pdf");
new HybridPdfPrinter { Title = "合并单元格示例" }.SaveToFile(spanTemplate, spanPdfSharpPath, new PrintData());
Console.WriteLine($"  [OK] 合并单元格 PDF（PdfSharp）已保存：{spanPdfSharpPath}");

// ═══════════════════════════════════════════════════════════
// 示例 6：物理打印测试（使用 PrintDocumentPrinter）
// ═══════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("=== 示例6：物理打印测试 ===");

try
{
    var printers = PrintDocumentPrinter.GetInstalledPrinters();
    if (printers.Length == 0)
    {
        Console.WriteLine("  [跳过] 未检测到已安装的打印机，跳过打印测试。");
    }
    else
    {
        Console.WriteLine($"  检测到 {printers.Length} 台打印机：");
        foreach (var p in printers)
            Console.WriteLine($"    - {p}");

        // 使用默认打印机打印示例 1 的条码标签
        using var printer = new PrintDocumentPrinter();
        printer.PrintCompleted += (s, e) => Console.WriteLine("  [OK] 打印任务已提交。");
        printer.PrintError   += (s, e) => Console.WriteLine($"  [错误] 打印失败：{e}");

        //var printData = new PrintData()
        //    .Set("ProductName", "精密螺丝 M3×8")
        //    .Set("Barcode",     "1234567890128")
        //    .Set("SerialNo",    "SN-2026051201")
        //    .Set("Date",        DateTime.Today);

        // 不显示对话框，直接打印到默认打印机
        //printer.Print(a4Template, a4Data, new PrintOptions {PrinterName= "HP LaserJet MFP M232dw (AF51EA)",  ShowPrintDialog = false });
        Console.WriteLine("  [OK] 打印任务已发送到默认打印机。");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"  [警告] 打印测试失败（非致命）：{ex.Message}");
}

// ═══════════════════════════════════════════════════════════
// 完成
// ═══════════════════════════════════════════════════════════
Console.WriteLine();
Console.WriteLine("所有示例已完成，请查看输出目录中的文件。");
Console.WriteLine($"  → {outputDir}");




var template = LabelTemplate.A4Landscape();
template.Name = "完整复刻装箱/报验单";

// --- 2. 顶部标题与 Logo 区域 ---
// 预留 Logo 位置
template.Add(new ImageElement
{
    X = 10f,
    Y = 8f,
    Width = 35f,
    Height = 12f,
    FilePath = @"logo.png", // 示例 Logo 占位路径，可替换为实际文件（缺失时该区域留空）
    ScaleMode = ImageScaleMode.Uniform
});

template.Add(new TextElement
{
    X = 0f,
    Y = 10f,
    Width = 297f,
    Height = 10f,
    Text = "Packing/Checking List 装箱/报验单",
    FontSize = 16f,
    Bold = true,
    Alignment = TextAlignment.Center
});

// --- 3. 页眉基础信息 (双列布局复刻) ---
float headerY = 25f;
float lineHeight = 5.5f;

// 左侧：发货与供应商信息
AddInfoRow(template, 10f, headerY, "ASN / 发货通知:", "ASN-DEMO-20260921-001");
AddInfoRow(template, 10f, headerY + lineHeight, "IDN / 内向交货单:", "IDN-DEMO-000123");
AddInfoRow(template, 10f, headerY + lineHeight * 2, "Supplier Code / 供应商代码:", "SUP-DEMO-001");
AddInfoRow(template, 10f, headerY + lineHeight * 3, "Supplier Name / 供应商名称:", "示例汽车内饰材料有限公司");
AddInfoRow(template, 10f, headerY + lineHeight * 4, "Shipping Date / 发货日期:", "2026-09-21");

// 右侧：收货工厂、地址、联系人
float rightColumnX = 155f;
AddInfoRow(template, rightColumnX, headerY, "Plant / 收货工厂:", "示例泡沫工厂（演示）");
AddInfoRow(template, rightColumnX, headerY + lineHeight, "Loading Dock / 收货道口:", "101");
AddInfoRow(template, rightColumnX, headerY + lineHeight * 2, "Delivery ADD / 收货地址:", "示例市示例区示范大道 1 号");
AddInfoRow(template, rightColumnX, headerY + lineHeight * 3, "Contact / 收货联系人:", "张三 / 李四 - 13800000000");
AddInfoRow(template, rightColumnX, headerY + lineHeight * 4, "ETA / 预计到货时间:", "2026-09-21 14:00:00");

template.Add(new BarcodeElement
{
    X = 230f,
    Y = 10f,
    Width = 55f,
    Height = 12f,
    Format = BarcodeFormat.Code128,
    Value = "ASN-DEMO-20260921-001",
    ShowText = true
});

// --- 4. 明细表格 (TableElement) ---
var detailsTable = new TableElement
{
    X = 10f,
    Y = 55f,
    Width = 277f,
    RowHeight = 9f,
    HeaderFontSize = 8f,
    RowFontSize = 8f,
    HeaderBackColor = "#FFE8E8E8",
    BorderWidthMm = 0.2f,
    GridLineWidthMm = 0.2f,
    Columns = new()
                {
                    new TableColumn { Header = "项\nLine", Field = "Line", Width = 10f, Align = TableColumnAlign.Center },
                    new TableColumn { Header = "零件号 \n Part Number", Field = "PartNo", Width = 35f },
                    new TableColumn { Header = "零件名称 \n Part Description", Field = "Desc", Width = 60f },
                    new TableColumn { Header = "单位\nUM", Field = "UM", Width = 10f, Align = TableColumnAlign.Center },
                    new TableColumn { Header = "订单数量 \n PO Qty", Field = "POQty", Width = 20f, Align = TableColumnAlign.Center },
                    new TableColumn { Header = "发货数量\nShip Qty", Field = "ShipQty", Width = 20f, Align = TableColumnAlign.Right },
                    new TableColumn { Header = "标包数量\nPkg Qty", Field = "PkgQty", Width = 20f, Align = TableColumnAlign.Right },
                    new TableColumn { Header = "是否报检\nQualify", Field = "Qualify", Width = 20f },
                    new TableColumn { Header = "检验结果\nResult", Field = "Result", Width = 20f },
                    new TableColumn { Header = "实收数量\nActual Qty", Field = "ActualQty", Width = 22f },
                    new TableColumn { Header = "采购单号\nPO No", Field = "PONo", Width = 40f }
                }
};

detailsTable.Rows = new List<Dictionary<string, object?>>
            {
                new() { ["Line"] = 1, ["PartNo"] = "DEMO-PN-001", ["Desc"] = "示例头枕面套总成 A", ["UM"] = "EA", ["POQty"] = 446, ["ShipQty"] = 20, ["PkgQty"] = 20, ["PONo"] = "DEMO-PO-001" },
                new() { ["Line"] = 2, ["PartNo"] = "DEMO-PN-002", ["Desc"] = "示例头枕面套总成 B", ["UM"] = "EA", ["POQty"] = 528, ["ShipQty"] = 125, ["PkgQty"] = 28, ["PONo"] = "DEMO-PO-002" },
                new() { ["Line"] = 3, ["PartNo"] = "DEMO-PN-001", ["Desc"] = "示例头枕面套总成 A", ["UM"] = "EA", ["POQty"] = 520, ["ShipQty"] = 16, ["PkgQty"] = 16, ["PONo"] = "DEMO-PO-002" },
                new() { ["Line"] = 4, ["PartNo"] = "DEMO-PN-003", ["Desc"] = "示例头枕面套总成 C", ["UM"] = "EA", ["POQty"] = 513, ["ShipQty"] = 168, ["PkgQty"] = 45, ["PONo"] = "DEMO-PO-002" }
            };

template.Add(detailsTable);

// --- 5. 底部区域 (合计、备注与签字) ---
float tableBottomY = 55f + (1 + detailsTable.Rows.Count) * 9f;

// 备注框
template.Add(new RectangleElement { X = 10f, Y = tableBottomY + 5f, Width = 180f, Height = 15f, BorderWidthMm = 0.2f });
template.Add(new TextElement { X = 12f, Y = tableBottomY + 7f, Width = 175f, Text = "备注: {Remark}", FontSize = 9f });

// 送货总箱数
template.Add(new TextElement
{
    X = 200f,
    Y = tableBottomY + 5f,
    Width = 87f,
    Text = "送货总箱数 / TOTAL PALLETS: 12",
    FontSize = 11f,
    Bold = true,
    Alignment = TextAlignment.Right
});

// 签字横线
float signY = 185f;
template.Add(new LineElement { X = 10f, Y = signY, Width = 277f, Height = 0f, LineWidthMm = 0.5f });
template.Add(new TextElement { X = 10f, Y = signY + 2f, Width = 100f, Text = "供应商签字 / Supplier Signed By: ________________", FontSize = 9f });
template.Add(new TextElement { X = 197f, Y = signY + 2f, Width = 100f, Text = "收货人签字 / Receiver Signed By: ________________", FontSize = 9f, Alignment = TextAlignment.Right });

// --- 6. 绑定数据并生成预览 ---
var data = new PrintData().Set("Remark", "此批次为示例演示数据，仅供功能展示，请勿用于实际业务。");

new TemplateRenderer { Dpi = 300f }.SaveToPng(template, "PackingList_Full.png", data);

new HybridPdfPrinter().SaveToFile(template, "PackingList_Full.pdf", data);

// 辅助方法：快速添加页眉行
static void AddInfoRow(LabelTemplate template, float x, float y, string label, string value)
{
    // 值起点 x+48 要在标签框（47mm）之外，否则"Supplier Name / 供应商名称:"这类长标签会与值重叠
    template.Add(new TextElement { X = x, Y = y, Width = 47f, Text = label, FontSize = 8.5f, Bold = true });
    template.Add(new TextElement { X = x + 48f, Y = y, Width = 90f, Text = value, FontSize = 8.5f });
}

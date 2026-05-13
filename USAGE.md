# PrintLibrary 使用说明

> 文档版本：2026-05-13  
> 适用框架：.NET 6 / .NET 8  
> 核心类库：`PrintLibrary.Core`

---

## 目录

1. [快速上手](#1-快速上手)
2. [引用类库](#2-引用类库)
3. [核心概念](#3-核心概念)
4. [LabelTemplate 模板](#4-labeltemplate-模板)
5. [PrintData 数据绑定](#5-printdata-数据绑定)
6. [元素类型详解](#6-元素类型详解)
   - [TextElement 文本](#61-textelement-文本)
   - [BarcodeElement 条码](#62-barcodeelement-条码)
   - [ImageElement 图片](#63-imageelement-图片)
   - [LineElement 线条](#64-lineelement-线条)
   - [RectangleElement 矩形](#65-rectangleelement-矩形)
   - [TableElement 表格](#66-tableelement-表格)
7. [预览与导出图片](#7-预览与导出图片)
8. [输出 PDF](#8-输出-pdf)
   - [SkiaSharp PDF（高保真）](#81-skiasharp-pdfpdfprinter)
   - [PdfSharp PDF（小体积）](#82-pdfsharp-pdfpdfsharpprinter)
9. [物理打印](#9-物理打印)
10. [模板序列化](#10-模板序列化)
11. [完整示例：A4 工单报表](#11-完整示例a4-工单报表)
12. [常见问题](#12-常见问题)

---

## 1. 快速上手

**10 分钟跑通第一个打印输出：**

```csharp
using PrintLibrary.Model;
using PrintLibrary.Preview;
using PrintLibrary.Printer;

// 1. 建模板（100×50mm 条码标签）
var template = LabelTemplate.BarLabel100x50();

template.Add(new TextElement
{
    X = 2f, Y = 2f, Width = 96f, Height = 10f,
    Text = "{ProductName}",
    FontFamily = "Microsoft YaHei",
    FontSize = 14f,
    Bold = true,
    Alignment = TextAlignment.Center
});

template.Add(new BarcodeElement
{
    X = 10f, Y = 15f, Width = 80f, Height = 25f,
    Format = BarcodeFormat.Code128,
    Value = "{Barcode}"
});

// 2. 绑定数据
var data = new PrintData()
    .Set("ProductName", "精密螺丝 M3×8")
    .Set("Barcode", "1234567890128");

// 3a. 导出 PNG 预览（300 DPI）
new TemplateRenderer { Dpi = 300f }
    .SaveToPng(template, "output/label.png", data);

// 3b. 导出小体积向量 PDF
new PdfSharpPrinter { Title = "条码标签" }
    .SaveToFile(template, "output/label.pdf", data);
```

---

## 2. 引用类库

项目引用方式（`.csproj`）：

```xml
<ProjectReference Include="..\PrintLibrary.Core\PrintLibrary.Core.csproj" />
```

命名空间：
```csharp
using PrintLibrary.Model;         // 模板、元素、数据
using PrintLibrary.Preview;       // TemplateRenderer
using PrintLibrary.Printer;       // PdfPrinter, PdfSharpPrinter, PrintDocumentPrinter
using PrintLibrary.Serialization; // TemplateSerializer
```

---

## 3. 核心概念

| 概念 | 类 | 说明 |
|---|---|---|
| **模板** | `LabelTemplate` | 定义纸张尺寸和元素布局，不含运行时数据 |
| **元素** | `LabelElement` 及子类 | 文本、条码、图片、形状、表格 |
| **数据** | `PrintData` | 键值对，填充模板中的 `{占位符}` |
| **预览渲染** | `TemplateRenderer` | 模板 → PNG/JPEG 位图 |
| **PDF 输出** | `PdfPrinter` / `PdfSharpPrinter` | 模板 → PDF 文件 |
| **物理打印** | `PrintDocumentPrinter` | 发送打印任务到打印机 |
| **序列化** | `TemplateSerializer` | 模板 ↔ JSON |

**坐标系：**
```
原点(0,0)在纸张左上角，X 向右，Y 向下，单位：毫米（mm）
```

---

## 4. LabelTemplate 模板

### 属性

| 属性 | 类型 | 说明 |
|---|---|---|
| `Width` | `float` | 纸张宽度（mm） |
| `Height` | `float` | 纸张高度（mm） |
| `Name` | `string` | 模板名称 |
| `BackgroundColor` | `string` | 背景色，ARGB 十六进制（`"#FFFFFFFF"` 为白色） |
| `PreferredPrinterName` | `string?` | 首选打印机名（可选） |
| `Copies` | `int` | 打印副本数（默认 1） |
| `Elements` | `List<LabelElement>` | 元素列表（按顺序绘制，后绘制的可覆盖前面的） |

### 工厂方法

```csharp
LabelTemplate.A4Portrait()       // 210×297 mm，A4 竖向
LabelTemplate.A4Landscape()      // 297×210 mm，A4 横向
LabelTemplate.BarLabel100x50()   // 100×50 mm，常见条码标签
```

### 添加元素

```csharp
// 方式 1：链式调用
template.Add(new TextElement { ... })
        .Add(new BarcodeElement { ... });

// 方式 2：直接操作 Elements 列表
template.Elements.Add(new TextElement { ... });
```

---

## 5. PrintData 数据绑定

```csharp
var data = new PrintData();

// 设置单个字段（支持链式）
data.Set("ProductName", "精密螺丝 M3×8")
    .Set("Qty", 500)
    .Set("Price", 12.50m)
    .Set("Date", DateTime.Today);

// 批量设置
data.SetRange(new Dictionary<string, object?> {
    ["Key1"] = "Value1",
    ["Key2"] = 42
});

// 读取字段
object? v = data["ProductName"];

// 检查字段是否存在
bool has = data.Contains("ProductName");
```

### 占位符语法

| 语法 | 说明 | 示例输出 |
|---|---|---|
| `{Field}` | 直接替换为字段的 `ToString()` | `{ProductName}` → `精密螺丝 M3×8` |
| `{Field:format}` | 调用 `IFormattable.ToString(format)` | `{Date:yyyy-MM-dd}` → `2026-05-13` |
| 字段不存在 | 原样保留占位符，不报错 | `{Missing}` → `{Missing}` |

**常用格式化示例：**
```
{Date:yyyy年MM月dd日}     → 2026年05月13日
{Price:N2}                → 12.50
{Price:C}                 → ¥12.50（依系统区域）
{Qty:000}                 → 500
{Rate:P1}                 → 85.0%（IFormattable 数值）
```

---

## 6. 元素类型详解

### 6.1 TextElement 文本

```csharp
new TextElement
{
    // 位置（mm）
    X = 5f, Y = 10f, Width = 100f, Height = 8f,

    // 内容（支持占位符）
    Text = "产品：{ProductName}  数量：{Qty}",

    // 字体
    FontFamily = "Microsoft YaHei",  // 系统字体名
    FontSize = 12f,                   // 单位：磅（pt）
    Bold = true,
    Italic = false,

    // 颜色
    ForeColor = "#FF000000",  // ARGB 十六进制，黑色

    // 对齐
    Alignment = TextAlignment.Left,   // Left / Center / Right

    // 换行与裁剪
    WordWrap = false,      // 是否自动换行
    ClipContent = true,    // 是否裁剪超出区域

    // （可选）字体文件路径，优先级高于 FontFamily
    // FontPath = "/usr/share/fonts/simhei.ttf"
}
```

**字体说明：**
- `FontFamily` 默认 `"Microsoft YaHei"`，中文环境首选
- 若系统无指定字体，SkiaSharp 会自动回退
- 含 CJK 字符但指定的是纯西文字体（Arial 等），会自动换用系统 CJK 字体
- 跨平台部署时建议使用 `FontPath` 指定字体文件，确保效果一致

**常用颜色值：**
```
"#FF000000"  黑色（不透明）
"#FFFFFFFF"  白色（不透明）
"#FF1A1A2E"  深蓝黑
"#FFCCCCCC"  浅灰
"#00000000"  透明
```

---

### 6.2 BarcodeElement 条码

```csharp
new BarcodeElement
{
    X = 10f, Y = 15f, Width = 80f, Height = 25f,

    // 条码内容（支持占位符）
    Value = "{Barcode}",

    // 条码格式
    Format = BarcodeFormat.Code128,  // 见下方枚举

    // QR Code 专用：容错等级
    QrErrorLevel = QrErrorCorrectionLevel.M,  // L/M/Q/H

    // 颜色
    ForeColor = "#FF000000",  // 条码前景色（模块颜色）
    BackColor = "#FFFFFFFF",  // 条码背景色

    // 是否在一维码下方显示文本（仅一维码有效）
    ShowText = false
}
```

**支持的条码格式（BarcodeFormat）：**

| 枚举值 | 说明 |
|---|---|
| `Code128` | 通用一维条码，支持全 ASCII，工厂场景首选 |
| `Code39` | 工业用，仅字母/数字/部分特殊字符 |
| `Ean13` | 商品条码（13位数字） |
| `Ean8` | 商品条码（8位数字） |
| `UpcA` | 美国商品条码 |
| `QrCode` | 二维码，支持中文、URL 等 |
| `DataMatrix` | 小尺寸二维码，工业/医疗 |
| `Pdf417` | 堆叠式二维条码 |
| `Aztec` | 小型二维码 |
| `Itf` | 交叉 25 码（物流箱/纸箱常用） |

> **注意：** 条码内容必须符合该格式规范，否则编码失败后不绘制（不报错）。

---

### 6.3 ImageElement 图片

```csharp
// 方式 1：从文件路径加载
new ImageElement
{
    X = 5f, Y = 5f, Width = 30f, Height = 30f,
    FilePath = @"C:\logos\company_logo.png",
    ScaleMode = ImageScaleMode.Uniform  // 等比缩放
}

// 方式 2：从 Base64 字符串加载（可嵌入 JSON 模板）
new ImageElement
{
    X = 5f, Y = 5f, Width = 30f, Height = 30f,
    ImageBase64 = "data:image/png;base64,iVBORw0KGgoAAAANS...",
    // 或纯 Base64（不带前缀）：
    // ImageBase64 = "iVBORw0KGgoAAAANS..."
    ScaleMode = ImageScaleMode.Stretch
}
```

**缩放模式（ImageScaleMode）：**

| 模式 | 说明 |
|---|---|
| `Stretch` | 拉伸填满元素矩形，不保持比例 |
| `Uniform` | 等比缩放至完全可见（Letterbox），可能留白边 |
| `UniformToFill` | 等比缩放至填满元素矩形，超出部分裁剪 |

---

### 6.4 LineElement 线条

```csharp
new LineElement
{
    // 起点 (X, Y)，终点 (X+Width, Y+Height)
    X = 5f, Y = 20f,
    Width = 100f, Height = 0f,  // 水平线

    Color = "#FF888888",       // 线条颜色
    LineWidthMm = 0.3f,        // 线宽（mm）

    // 虚线（可选）：偶数下标为线段长，奇数下标为间隙长（mm）
    // DashPattern = new float[] { 3f, 1f }  // 3mm 线 + 1mm 间隙
}
```

| 线条方向 | 设置方式 |
|---|---|
| 水平线 | `Height = 0f` |
| 垂直线 | `Width = 0f` |
| 斜线 | `Width` 和 `Height` 都不为 0 |

---

### 6.5 RectangleElement 矩形

```csharp
new RectangleElement
{
    X = 5f, Y = 5f, Width = 200f, Height = 20f,

    FillColor = "#FF1A1A2E",     // 填充色（透明则不填充）
    BorderColor = "#FF000000",   // 边框颜色
    BorderWidthMm = 0.5f,        // 边框线宽（mm），0 则不画边框
    CornerRadiusMm = 2f          // 圆角半径（mm），0 为直角
}
```

**只画边框（透明填充）：**
```csharp
FillColor = "#00000000",   // 透明
BorderColor = "#FF000000"
```

**只填充（无边框）：**
```csharp
FillColor = "#FFCCCCCC",
BorderColor = "#00000000"  // 透明，或者 BorderWidthMm = 0
```

---

### 6.6 TableElement 表格

TableElement 是本库的核心亮点——**只需声明列结构和数据行，免手算坐标，自动完成表格布局**。

```csharp
new TableElement
{
    // 表格位置和尺寸
    X = 5f, Y = 55f, Width = 200f,

    // 行高（所有行，含表头）
    RowHeight = 8f,

    // 字体
    FontFamily = "Microsoft YaHei",
    HeaderFontSize = 10f,
    RowFontSize = 9f,

    // 表头样式
    HeaderBackColor = "#FFE8E8E8",  // 表头背景色
    HeaderForeColor = "#FF1A1A2E",  // 表头文字色
    HeaderBold = true,

    // 数据行样式
    RowForeColor = "#FF000000",
    OddRowBackColor = "#00000000",   // 奇数行背景（透明 = 不填充）
    EvenRowBackColor = "#FFF5F5F5",  // 偶数行背景（浅灰）

    // 网格线
    BorderWidthMm = 0.3f,      // 外边框线宽（0 = 不画）
    GridLineWidthMm = 0.3f,    // 内部分割线宽（0 = 不画）
    GridColor = "#FFCCCCCC",

    // 列定义
    Columns = new()
    {
        new TableColumn
        {
            Header = "序号",
            Field = "No",        // 对应数据行字典的 Key
            Width = 20f,         // 列宽（mm），设 0 则自动均分
            Align = TableColumnAlign.Center
        },
        new TableColumn { Header = "零件名称", Field = "Name", Width = 80f },
        new TableColumn { Header = "数量",  Field = "Qty",   Width = 30f, Align = TableColumnAlign.Right },
        new TableColumn
        {
            Header = "单价(元)",
            Field = "Price",
            Width = 35f,
            Align = TableColumnAlign.Right,
            Format = "N2"  // 格式化：保留两位小数
        },
    },

    // 数据行（每行是一个字典）
    Rows = new()
    {
        new() { ["No"] = 1, ["Name"] = "轴承内圈", ["Qty"] = 200, ["Price"] = 12.50m },
        new() { ["No"] = 2, ["Name"] = "密封圈",   ["Qty"] = 400, ["Price"] = 1.20m },
    }
}
```

**合计行位置计算：**

```csharp
// tableBottom = 表格起始 Y + (表头行 + 数据行) × 行高
float tableBottom = tableY + (1 + rows.Count) * rowHeight;
// 例如：表头+6行数据，行高 8mm，起始 Y=55
// tableBottom = 55 + 7 * 8 = 111mm
```

**列宽自动均分：**
- 列宽设为 `0f` 的列，宽度 = 剩余宽度 / 自动列数量
- 例如：表格宽 200mm，3 列分别为 30f/0f/0f，则后两列各 85mm

---

## 7. 预览与导出图片

```csharp
var renderer = new TemplateRenderer
{
    Dpi = 300f,         // 渲染分辨率（96=屏幕，300=打印品质，600=高精度）
    ShowGrid = true,    // 调试模式：叠加毫米网格
    ShowBorder = true,  // 调试模式：显示红色模板边界
    GridSpacingMm = 5f  // 网格间距（mm）
};

// 保存为 PNG 文件
renderer.SaveToPng(template, "output/preview.png", data);

// 保存为 JPEG 文件
renderer.SaveToJpeg(template, "output/preview.jpg", quality: 90, data);

// 获取 PNG 字节数组（适合 Web API 返回）
byte[] pngBytes = renderer.RenderToPngBytes(template, data);

// 获取 SKBitmap（适合在 UI 中显示）
using var bitmap = renderer.Render(template, data);

// 静态快捷方法（使用默认 96 DPI）
using var bmp = TemplateRenderer.QuickRender(template, data);
byte[] bytes = TemplateRenderer.QuickRenderToPng(template, dpi: 150f, data);
```

**DPI 选择建议：**

| 场景 | 推荐 DPI |
|---|---|
| UI 预览显示 | 96 ~ 150 |
| 普通报表打印预览 | 150 ~ 200 |
| 标签/条码打印留档 | 300 |
| 高精度输出 | 600 |

---

## 8. 输出 PDF

### 8.1 SkiaSharp PDF（PdfPrinter）

SkiaSharp 向量 PDF，文字为矢量图形，放大不失真。

```csharp
// 向量模式（默认）：文字清晰，体积较大（CJK 约 23MB）
var printer = new PdfPrinter { Title = "工单报表" };
printer.SaveToFile(template, "output/report.pdf", data);

// 位图模式：体积小，清晰度受 DPI 限制
var printer = new PdfPrinter
{
    Title = "工单报表",
    RasterDpi = 300f,                  // 非 null 即启用位图模式
    RasterEncoding = RasterEncoding.Png, // Jpeg（默认）或 Png（文字更清晰）
    JpegQuality = 95                   // Jpeg 压缩质量（0-100）
};

// 多页 PDF
var pages = new List<PrintData> { data1, data2, data3 };
printer.SaveToFile(template, "output/multi.pdf", pages);

// 输出为字节数组
byte[] pdfBytes = printer.PrintToBytes(template, data);
```

### 8.2 PdfSharp PDF（PdfSharpPrinter）

PdfSharpCore 字体子集化，中文 A4 页约 30-50KB，适合邮件/Web 下载。

```csharp
var printer = new PdfSharpPrinter
{
    Title = "工单报表",
    Author = "MyApp"
};

// 单页
printer.SaveToFile(template, "output/report_small.pdf", data);

// 多页
printer.SaveToFile(template, "output/multi.pdf", pages);

// 字节数组
byte[] bytes = printer.PrintToBytes(template, data);
```

**字体注意事项：**

| 字体 | 支持 | 说明 |
|---|---|---|
| SimHei（黑体） | ✓ | .ttf 格式，推荐 |
| SimSun（宋体） | ✓ | .ttf 格式 |
| Microsoft YaHei（微软雅黑） | 自动映射 | .ttc 格式不支持，自动映射为 SimHei |
| 微软雅黑 | 自动映射 | 同上 |

**自定义字体映射：**
```csharp
var printer = new PdfSharpPrinter();
printer.FontMap["NotoSansSC"] = "NotoSansSC-Regular";  // 自定义映射
```

**自定义回退字体链：**
```csharp
var printer = new PdfSharpPrinter();
printer.FontFallbackChain.Insert(0, "MyCustomFont");  // 插入最高优先级
```

**两种 PDF 方案对比：**

| 对比项 | PdfPrinter | PdfSharpPrinter |
|---|---|---|
| 引擎 | SkiaSharp | PdfSharpCore |
| CJK A4 体积 | ~23MB（向量），~200KB（位图） | ~34KB |
| 文字质量 | 向量，无限清晰 | 向量，无限清晰 |
| 特殊符号（Ø, →）| 自动回退（系统级） | 需回退字体支持 |
| .ttc 字体 | ✓ 支持 | ✗ 不支持（自动映射）|
| 跨平台 | ✓ | ✓ |

---

## 9. 物理打印

> **注意：** `PrintDocumentPrinter` 依赖 `System.Drawing.Printing`，在 Linux/macOS 上需要 CUPS。

```csharp
using var printer = new PrintDocumentPrinter();

// 订阅事件（可选）
printer.PrintCompleted += (s, e) => Console.WriteLine("打印完成");
printer.PrintError     += (s, e) => Console.WriteLine($"打印错误：{e.Message}");

// 枚举已安装打印机
string[] printers = PrintDocumentPrinter.GetInstalledPrinters();
foreach (var name in printers)
    Console.WriteLine(name);

// 单页打印（默认打印机，无弹窗）
printer.Print(template, data, new PrintOptions
{
    PrinterName = "HP LaserJet",    // null = 使用系统默认打印机
    Copies = 2,
    ShowPrintDialog = false         // true = 弹出打印对话框（仅 Windows）
});

// 多页打印
printer.PrintPages(template, pages, new PrintOptions());
```

---

## 10. 模板序列化

### 保存模板到 JSON

```csharp
// 保存到文件
TemplateSerializer.Save(template, "templates/barcode_label.json");

// 序列化为字符串
string json = TemplateSerializer.Serialize(template);

// 保存到 Stream（Web API 场景）
using var stream = response.OutputStream;
TemplateSerializer.SaveToStream(template, stream);
```

### 从 JSON 加载模板

```csharp
// 从文件加载
var template = TemplateSerializer.Load("templates/barcode_label.json");

// 从字符串反序列化
var template = TemplateSerializer.Deserialize(jsonString);

// 从 Stream 加载
var template = TemplateSerializer.LoadFromStream(stream);
```

### JSON 格式示例

```json
{
  "Name": "条码标签",
  "Width": 100.0,
  "Height": 50.0,
  "BackgroundColor": "#FFFFFFFF",
  "Elements": [
    {
      "$type": "TextElement",
      "Text": "{ProductName}",
      "FontFamily": "Microsoft YaHei",
      "FontSize": 14.0,
      "Bold": true,
      "Alignment": "Center",
      "X": 2.0, "Y": 2.0, "Width": 96.0, "Height": 10.0
    },
    {
      "$type": "BarcodeElement",
      "Value": "{Barcode}",
      "Format": "Code128",
      "ShowText": false,
      "X": 10.0, "Y": 15.0, "Width": 80.0, "Height": 25.0
    }
  ]
}
```

**多态类型标识（$type）：**

| `$type` 值 | 对应类 |
|---|---|
| `TextElement` | `TextElement` |
| `BarcodeElement` | `BarcodeElement` |
| `ImageElement` | `ImageElement` |
| `LineElement` | `LineElement` |
| `RectangleElement` | `RectangleElement` |
| `TableElement` | `TableElement` |

---

## 11. 完整示例：A4 工单报表

```csharp
// ── 1. 创建 A4 模板 ─────────────────────────────────────
var template = LabelTemplate.A4Portrait();
template.Name = "工单报表";

// ── 2. 标题区域 ─────────────────────────────────────────
template.Add(new RectangleElement
{
    X = 5f, Y = 5f, Width = 200f, Height = 20f,
    FillColor = "#FF1A1A2E"
});
template.Add(new TextElement
{
    X = 5f, Y = 7f, Width = 200f, Height = 16f,
    Text = "工  单  报  表",
    FontFamily = "Microsoft YaHei",
    FontSize = 18f, Bold = true,
    Alignment = TextAlignment.Center,
    ForeColor = "#FFFFFFFF"
});

// ── 3. 基础信息（双列布局）──────────────────────────────
float infoY = 32f;
template.Add(new TextElement
    { X = 5f, Y = infoY, Width = 95f, Height = 7f,
      Text = "工单号：{WorkOrderNo}", FontSize = 10f });
template.Add(new TextElement
    { X = 110f, Y = infoY, Width = 95f, Height = 7f,
      Text = "日  期：{Date:yyyy年MM月dd日}", FontSize = 10f });

// 分隔线
template.Add(new LineElement
    { X = 5f, Y = infoY + 18f, Width = 200f, Height = 0f,
      Color = "#FF888888", LineWidthMm = 0.2f });

// ── 4. 明细表格 ─────────────────────────────────────────
var rows = new List<Dictionary<string, object?>>
{
    new() { ["No"] = 1, ["Name"] = "轴承内圈", ["Qty"] = 200, ["Price"] = 12.50m, ["Total"] = 2500.00m },
    new() { ["No"] = 2, ["Name"] = "轴承外圈", ["Qty"] = 200, ["Price"] = 15.80m, ["Total"] = 3160.00m },
    new() { ["No"] = 3, ["Name"] = "密封圈",   ["Qty"] = 400, ["Price"] = 1.20m,  ["Total"] = 480.00m },
};

template.Add(new TableElement
{
    X = 5f, Y = 55f, Width = 200f,
    RowHeight = 8f,
    HeaderFontSize = 10f, RowFontSize = 9f,
    BorderWidthMm = 0.3f, GridLineWidthMm = 0.3f,
    Columns = new()
    {
        new TableColumn { Header = "序号",   Field = "No",    Width = 20f, Align = TableColumnAlign.Center },
        new TableColumn { Header = "零件名称", Field = "Name",  Width = 80f },
        new TableColumn { Header = "数量",   Field = "Qty",   Width = 30f, Align = TableColumnAlign.Right },
        new TableColumn { Header = "单价(元)", Field = "Price", Width = 35f, Align = TableColumnAlign.Right, Format = "N2" },
        new TableColumn { Header = "小计(元)", Field = "Total", Width = 35f, Align = TableColumnAlign.Right, Format = "N2" },
    },
    Rows = rows
});

// ── 5. 合计行（关键：行数要和数据对得上！）───────────────
// tableBottom = 表格起始Y + (1表头 + N数据行) × 行高
float tableBottom = 55f + (1 + rows.Count) * 8f;  // = 55 + 4*8 = 87mm

template.Add(new TextElement
{
    X = 120f, Y = tableBottom + 3f, Width = 85f, Height = 7f,
    Text = "合计：¥9,140.00",
    FontSize = 11f, Bold = true,
    Alignment = TextAlignment.Right,
    ForeColor = "#FF1A1A2E"
});

// ── 6. 二维码（右下角）──────────────────────────────────
template.Add(new BarcodeElement
{
    X = 165f, Y = tableBottom + 15f, Width = 35f, Height = 35f,
    Format = BarcodeFormat.QrCode,
    Value = "ORDER:{WorkOrderNo}",
    QrErrorLevel = QrErrorCorrectionLevel.M
});

// ── 7. 备注区（左下角，自动换行）───────────────────────
template.Add(new TextElement
{
    X = 5f, Y = tableBottom + 15f, Width = 155f, Height = 35f,
    Text = "备注：{Remark}",
    FontFamily = "Microsoft YaHei",
    FontSize = 9f, WordWrap = true, ClipContent = true,
    ForeColor = "#FF333333"
});

// ── 8. 绑定数据并输出 ────────────────────────────────────
var data = new PrintData()
    .Set("WorkOrderNo", "WO-20260513-001")
    .Set("Date", DateTime.Today)
    .Set("Remark", "此批零件用于一号产线装配，优先级高，请安排今日下午完成检验并入库。");

// PNG 预览
new TemplateRenderer { Dpi = 150f }.SaveToPng(template, "output/work_order.png", data);

// 小体积 PDF
new PdfSharpPrinter { Title = "工单报表" }.SaveToFile(template, "output/work_order.pdf", data);
```

---

## 12. 常见问题

### Q1：汉字在 PDF 中显示为方框（□）

**PdfPrinter（SkiaSharp）：** SkiaSharp 有系统级字体回退，通常不会出现此问题。若出现，检查 `FontFamily` 是否填写了系统已安装的 CJK 字体。

**PdfSharpPrinter：** 常见原因：
1. 使用了 `.ttc` 格式字体（如微软雅黑）→ 在 `FontMap` 中添加映射到 SimHei
2. 主字体本身不包含 CJK → 确认模板使用 `Microsoft YaHei`（会自动映射为 SimHei）

---

### Q2：特殊符号（Ø, →, ★, ℃）在 PdfSharp PDF 中是方框

PdfSharpPrinter 内置 Unicode 范围回退机制，回退到 Segoe UI Symbol（Windows 系统内置）。

若系统无 Segoe UI Symbol，可在 `FontFallbackChain` 中添加其他字体：
```csharp
var printer = new PdfSharpPrinter();
printer.FontFallbackChain.Insert(0, "DejaVu Sans");  // Linux 常见字体
```

---

### Q3：表格底部元素位置偏高/偏低

检查 `tableBottom` 的计算：
```csharp
// 正确：表格起始 Y + (1行表头 + 数据行数) × 行高
float tableBottom = tableY + (1 + table.Rows.Count) * rowHeight;
```
注意每次添加或删除数据行后同步更新 `tableBottom` 的行数。

---

### Q4：条码渲染后无法被扫描

- Code128：内容可包含所有 ASCII 字符，但避免首尾空格
- EAN-13：必须为 13 位纯数字（含校验位），条码长度要足够（建议 `Width >= 40mm`）
- QR Code：内容过长时建议提高容错等级（`QrErrorLevel = QrErrorCorrectionLevel.H`）并增大尺寸

---

### Q5：图片显示为空白

检查：
1. `FilePath` 是否为绝对路径，文件是否存在
2. `ImageBase64` 是否是有效的 Base64 图片数据
3. `Width` 和 `Height` 是否都大于 0

---

### Q6：模板序列化后反序列化失败

确保 JSON 中每个元素都有 `"$type"` 字段，且值为对应的类型名（大小写敏感）：
```json
{ "$type": "TextElement", ... }   // ✓
{ "$type": "textelement", ... }   // ✗ 可能失败
```

---

### Q7：多页 PDF 某页数据没有正确绑定

确认每页有独立的 `PrintData` 对象，传入的 `List<PrintData>` 长度与期望页数一致：
```csharp
var pages = new List<PrintData>
{
    new PrintData().Set("No", 1).Set("Name", "第一页"),
    new PrintData().Set("No", 2).Set("Name", "第二页"),
};
printer.SaveToFile(template, "output/multi.pdf", pages);
```

---

### Q8：物理打印只输出到左上角一小块

已知问题（已修复）：旧版 `DrawViaBitmap` 使用 `Point(0,0)` 绘制位图，导致未缩放到打印区域。当前版本使用 `Graphics.VisibleClipBounds` 自动缩放到可打印区域。若仍出现问题，检查 `PrintDocumentPrinter` 是否为最新版本。

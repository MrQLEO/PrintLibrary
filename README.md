# PrintLibrary — 跨平台工厂/企业打印类库

基于 **SkiaSharp** + **ZXing.Net** 的跨平台（Windows / Linux / macOS）打印类库，支持条码标签、A4 报表、PDF 输出与实时预览。

---

## 快速开始

### 1. 安装依赖包

在 `PrintLibrary.Core` 项目中添加以下 NuGet 包：

```bash
dotnet add package SkiaSharp
dotnet add package SkiaSharp.NativeAssets.Linux.NoDependencies  # Linux
dotnet add package SkiaSharp.NativeAssets.macOS                 # macOS
dotnet add package ZXing.Net
dotnet add package System.Text.Json
dotnet add package System.Drawing.Common                        # 仅 Windows 实体打印
```

### 2. 最简示例（10 分钟内打出第一张标签）

```csharp
using PrintLibrary.Model;
using PrintLibrary.Preview;
using PrintLibrary.Printer;

// 1. 定义模板（100×50 mm 条码标签）
var template = new LabelTemplate { Width = 100f, Height = 50f };

template.Add(new TextElement
{
    X = 5f, Y = 5f, Width = 90f, Height = 10f,
    Text = "{ProductName}", FontSize = 14f, Bold = true,
    Alignment = TextAlignment.Center
});

template.Add(new BarcodeElement
{
    X = 10f, Y = 18f, Width = 80f, Height = 25f,
    Format = BarcodeFormat.Code128,
    Value  = "{Barcode}"
});

// 2. 绑定数据
var data = new PrintData()
    .Set("ProductName", "精密螺丝 M3×8")
    .Set("Barcode",     "1234567890128");

// 3a. 预览 → 导出 PNG（300 DPI）
new TemplateRenderer { Dpi = 300f }.SaveToPng(template, "label.png", data);

// 3b. 输出 PDF（跨平台）
new PdfPrinter().SaveToFile(template, "label.pdf", data);

// 3c. 实体打印（Windows，需 PrintDocument 支持）
using var printer = new PrintDocumentPrinter();
printer.Print(template, data, new PrintOptions { PrinterName = "打印机名称" });
```

---

## 项目结构

```
PrintLibrary.Core/
├── Model/
│   ├── PrintData.cs          - 数据绑定容器，支持 {Field} 和 {Field:格式化} 占位符
│   ├── LabelTemplate.cs      - 模板定义（宽高 + 元素列表）
│   ├── LabelElement.cs       - 所有元素的抽象基类
│   ├── TextElement.cs        - 文本元素（字体/颜色/对齐/换行）
│   ├── BarcodeElement.cs     - 条码元素（Code128/Code39/QR/DataMatrix等）
│   ├── ImageElement.cs       - 图片元素（文件路径/Base64，支持缩放模式）
│   └── ShapeElements.cs      - 线条（LineElement）+ 矩形（RectangleElement）
├── Rendering/
│   ├── SkiaDrawingHelper.cs  - mm↔px 坐标换算和变换矩阵工厂
│   └── ElementRenderer.cs   - 批量渲染引擎 + 调试网格/边框
├── Serialization/
│   ├── TemplateSerializer.cs - JSON 序列化/反序列化（Load/Save）
│   └── LabelElementConverter.cs - 多态元素转换器（$type 字段）
├── Preview/
│   └── TemplateRenderer.cs  - 渲染到 SKBitmap / PNG / JPEG
└── Printer/
    ├── PrintDocumentPrinter.cs - 实体打印（System.Drawing.Printing）
    └── PdfPrinter.cs          - PDF 输出（SkiaSharp SKDocument）
```

---

## 核心概念

### 坐标系

- 所有坐标和尺寸均使用 **毫米（mm）** 为单位
- 原点在**左上角**，X 向右，Y 向下
- 无需关心打印机 DPI，库内部自动换算

### 元素类型

| 类型               | 用途                              |
|--------------------|-----------------------------------|
| `TextElement`      | 文字（支持多字体、换行、对齐）     |
| `BarcodeElement`   | 一/二维条码（10+ 格式）           |
| `ImageElement`     | 图片（本地文件/Base64）            |
| `LineElement`      | 直线/斜线（支持虚线）              |
| `RectangleElement` | 矩形框（支持圆角/填充）            |

### 数据绑定

```csharp
// 占位符语法
"{字段名}"           → 直接替换
"{Date:yyyy-MM-dd}"  → 格式化 IFormattable
"{ProductName}"      → 普通字符串替换
```

### 序列化

```csharp
// 保存模板到文件
TemplateSerializer.Save(template, "my_template.json");

// 从文件加载
var template = TemplateSerializer.Load("my_template.json");

// JSON 中的元素用 "$type" 字段区分类型
// { "$type": "TextElement", "Text": "{ProductName}", ... }
```

---

## 跨平台说明

| 功能             | Windows | Linux  | macOS  |
|------------------|---------|--------|--------|
| 预览/PNG导出     | ✅      | ✅     | ✅     |
| PDF 输出         | ✅      | ✅     | ✅     |
| 实体打印（GDI）   | ✅      | ⚠️ CUPS | ⚠️ CUPS |

**Linux/macOS 打印方式**：

```csharp
// 先输出 PDF，再通过 lp 命令发送到打印机
var printer = new PdfPrinter();
printer.PrintViaCups(template, pages, printerName: "my-printer", copies: 2);
```

---

## 扩展新元素类型

只需继承 `LabelElement` 并实现 `Draw` 方法：

```csharp
public class MyCustomElement : LabelElement
{
    public string MyProperty { get; set; } = string.Empty;

    public override void Draw(SKCanvas canvas, SKMatrix mmToPixelMatrix, PrintData data)
    {
        if (!IsVisible) return;
        var rect = GetPixelRect(mmToPixelMatrix);
        // 用 SKCanvas API 绘制你的内容
        using var paint = new SKPaint { Color = SKColors.Red };
        canvas.DrawCircle(rect.MidX, rect.MidY, rect.Width / 2, paint);
    }
}

// 同时在 LabelElementConverter.ResolveType() 中注册：
// nameof(MyCustomElement) => typeof(MyCustomElement)
```

---

## 性能参考

| 场景                          | 目标性能   |
|-------------------------------|------------|
| 单页 10 元素渲染（96 DPI）    | ≤ 80 ms    |
| JSON 序列化/反序列化          | ≤ 15 ms    |
| 300 DPI 高精度预览            | ≤ 300 ms   |

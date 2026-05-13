# PrintLibrary 代码库架构说明

> 文档版本：2026-05-13  
> 描述的代码状态：PrintLibrary.Core 当前主干

---

## 1. 整体定位

PrintLibrary 是一套**以毫米为统一物理单位**的跨平台 .NET 打印类库。核心目标：

- 绝对坐标布局，元素坐标全部以毫米表达，与分辨率无关
- 多打印后端：PNG 预览图、SkiaSharp 向量/位图 PDF、PdfSharp 小体积向量 PDF、Windows 物理打印
- 丰富元素：文本、条码（一维/二维）、图片、线条、矩形、表格
- 数据绑定：占位符 `{Field}` 和格式化 `{Date:yyyy-MM-dd}`
- 模板 JSON 序列化，可跨平台共享模板文件

---

## 2. 项目结构

```
PrintLibrary/
├── PrintLibrary.Core/          # 核心类库（NuGet 包）
│   ├── Model/                  # 数据模型层
│   │   ├── LabelElement.cs     # 所有元素的抽象基类
│   │   ├── LabelTemplate.cs    # 模板容器（宽高 + 元素列表）
│   │   ├── PrintData.cs        # 数据绑定容器（占位符引擎）
│   │   ├── TextElement.cs      # 文本元素（含字体回退逻辑）
│   │   ├── BarcodeElement.cs   # 条码元素（ZXing 编码 + 向量绘制）
│   │   ├── ImageElement.cs     # 图片元素（File/Base64，三种缩放模式）
│   │   ├── ShapeElements.cs    # 图形元素（LineElement + RectangleElement）
│   │   └── TableElement.cs     # 表格元素（列定义 + 自动布局）
│   ├── Rendering/
│   │   ├── ElementRenderer.cs  # 统一元素绘制调度 + 调试图层
│   │   └── SkiaDrawingHelper.cs# mm→px 矩阵计算工具
│   ├── Preview/
│   │   └── TemplateRenderer.cs # 渲染到 SKBitmap，导出 PNG/JPEG
│   ├── Printer/
│   │   ├── PdfPrinter.cs       # SkiaSharp 向量/位图 PDF 输出
│   │   ├── PdfSharpPrinter.cs  # PdfSharpCore 小体积向量 PDF 输出
│   │   └── PrintDocumentPrinter.cs  # System.Drawing.Printing 物理打印
│   └── Serialization/
│       ├── TemplateSerializer.cs    # JSON 序列化/反序列化（静态 API）
│       └── LabelElementConverter.cs # 多态元素 JSON 转换器（$type 字段）
│
├── PrintLibrary.Demo/          # 完整示例程序（Console）
│   └── Program.cs              # 6 个使用示例（从条码标签到 A4 报表）
│
├── global.json                 # SDK 版本锁定（8.0.420）
├── NuGet.Config                # NuGet 源配置
└── PrintLibrary.slnx           # 解决方案文件
```

---

## 3. 核心模型层（Model/）

### 3.1 坐标系约定

```
原点(0,0)
   ┌────────────────→ X（毫米）
   │
   │   元素绘制区域
   │
   ↓
   Y（毫米）
```

所有元素的 `X, Y, Width, Height` 均为**毫米**单位，渲染时统一换算到像素。

### 3.2 元素继承体系

```
LabelElement（抽象基类）
│  + X, Y, Width, Height : float    // 位置与尺寸（mm）
│  + IsVisible : bool
│  + Name : string?
│  + abstract Draw(canvas, matrix, data)
│  # GetPixelRect(matrix) : SKRect   // 辅助方法
│
├── TextElement              // 文本（字体、字号、换行、数据绑定）
├── BarcodeElement           // 条码（Code128/QR 等，向量绘制）
├── ImageElement             // 图片（File/Base64，三种缩放模式）
├── LineElement              // 线条（颜色、宽度、虚线）
├── RectangleElement         // 矩形（填充色、边框、圆角）
└── TableElement             // 表格（列定义、数据行、网格线）
```

### 3.3 PrintData（数据绑定引擎）

```csharp
// 内部存储：Dictionary<string, object?>（大小写不敏感）
var data = new PrintData()
    .Set("ProductName", "精密螺丝 M3×8")
    .Set("Date", DateTime.Today);

// 占位符替换：
// "{ProductName}" → "精密螺丝 M3×8"
// "{Date:yyyy-MM-dd}" → "2026-05-13"
string resolved = data.Resolve("{ProductName} 生产日期：{Date:yyyy-MM-dd}");
```

**替换规则：**
- 字段不存在：占位符原样保留（不报错）
- 值为 null：占位符原样保留
- 有格式化字符串且值实现 `IFormattable`：调用 `ToString(format, null)`

### 3.4 LabelTemplate（模板容器）

```csharp
// 工厂方法
var template = LabelTemplate.A4Portrait();   // 210×297 mm
var template = LabelTemplate.A4Landscape();  // 297×210 mm
var template = LabelTemplate.BarLabel100x50();  // 100×50 mm

// 链式添加元素
template
    .Add(new TextElement { ... })
    .Add(new BarcodeElement { ... });

// 属性
template.BackgroundColor = "#FFFFFFFF";  // ARGB 十六进制
template.PreferredPrinterName = "HP LaserJet";
template.Copies = 2;
```

---

## 4. 渲染层（Rendering/）

### 4.1 坐标变换原理

所有渲染后端的关键步骤相同：

```
mm 坐标 × pxPerMm = px 坐标
pxPerMm = DPI / 25.4
```

`SkiaDrawingHelper.CreateMmToPixelMatrix(dpi)` 生成 `SKMatrix`：

```csharp
// 等效于：
// canvas.Scale(pxPerMm, pxPerMm);
// 之后所有绘制坐标直接用 mm 值即可
```

### 4.2 ElementRenderer

```
ElementRenderer.RenderTemplate(canvas, template, data, matrix)
│
├── 填充背景色
└── 遍历 Elements
    └── element.Draw(canvas, matrix, data)  // 多态分派
```

调试图层（独立于正式内容之上叠加）：
- `DrawDebugGrid()`：灰色毫米格线
- `DrawDebugBorder()`：红色模板边界框

---

## 5. 打印后端（Printer/）

### 5.1 三种后端对比

| 后端 | 类 | 引擎 | 特点 | 适用场景 |
|------|-----|------|------|----------|
| PNG/JPEG 预览 | `TemplateRenderer` | SkiaSharp | 高质量位图 | UI 预览、调试 |
| SkiaSharp PDF | `PdfPrinter` | SkiaSharp | 向量模式清晰，CJK 字体嵌入体积大（~23MB） | 高保真留档 |
| PdfSharp PDF | `PdfSharpPrinter` | PdfSharpCore | 字体子集化，CJK 页仅 ~34KB | 邮件传输、Web 下载 |
| 物理打印 | `PrintDocumentPrinter` | System.Drawing | 发送到真实打印机 | 打印机直出 |

### 5.2 PdfPrinter（SkiaSharp PDF）

```
PdfPrinter
├── 向量模式（RasterDpi = null，默认）
│   └── SKDocument.CreatePdf() → SKCanvas 直接绘制
│       文字为向量 → 无限清晰
│       缺点：CJK 字体全量嵌入，A4 约 23MB
│
└── 位图模式（RasterDpi = 300f）
    └── 每页用 SkiaSharp 渲染为 JPEG/PNG → 嵌入 PDF
        体积小（几十~几百 KB）
        缺点：清晰度受 DPI 限制
```

### 5.3 PdfSharpPrinter（PdfSharpCore 小体积 PDF）

PdfSharpCore 的核心优势：**字体子集化**——只嵌入实际用到的字形，中文 A4 页仅 30-50KB。

**字体限制与处理：**
- 不支持 `.ttc` 格式（微软雅黑 MSYH.TTC）→ 内置字体映射表自动回退到 SimHei
- 不自动回退字体 → 用自实现的 Unicode 范围启发式回退机制

**字体回退链（FontFallbackChain）：**
```
Segoe UI Symbol（系统可用，覆盖大量符号）
   → Arial（基本拉丁）
```

**IsSpecialSymbolChar 判断逻辑：**

| Unicode 范围 | 字符示例 | SimHei 是否有 | 是否回退 |
|---|---|---|---|
| U+0080..U+00BF | `¥ ± ° © ®` | ✓ 有 | 否 |
| U+00C0..U+00FF（除 `×÷`） | `Ø å æ ß À É` | ✗ 无 | ✓ 是 |
| U+0100..U+024F | 拉丁扩展字母 | ✗ 无 | ✓ 是 |
| U+2000..U+27BF | `— • → ∑ ★ ✔` | ✗ 无 | ✓ 是 |
| U+FF00..U+FFEF | `￥ ！ ＡＢ` | ✓ 有 | 否 |
| U+4E00..U+9FFF | `汉字` | ✓ 有 | 否 |

**DrawStringWithFallback 工作流程：**
```
输入文本 "滚珠 Ø6.5mm"
    ↓
HasFallbackChars() 检测到 Ø(U+00D8) 需要回退
    ↓
SplitByFontSupport() 分段：
    ["滚珠 ", SimHei]  →  ["Ø", Segoe UI Symbol]  →  ["6.5mm", SimHei]
    ↓
逐段绘制，拼接水平位置
```

### 5.4 PrintDocumentPrinter（物理打印）

```
PrintDocumentPrinter
├── GetInstalledPrinters()  // 枚举已安装打印机
├── Print(template, data, options)  // 单页打印
└── PrintPages(template, pages, options)  // 多页打印

内部流程：
    PrintDocument.PrintPage 事件
        → SkiaSharp 渲染位图（打印机 DPI）
        → 将位图绘制到 Graphics.VisibleClipBounds（自动缩放到可打印区域）
```

---

## 6. 序列化层（Serialization/）

### 6.1 多态 JSON 格式

JSON 中使用 `"$type"` 字段区分元素类型：

```json
{
  "Name": "示例模板",
  "Width": 100.0,
  "Height": 50.0,
  "Elements": [
    {
      "$type": "TextElement",
      "Text": "产品：{ProductName}",
      "FontSize": 12.0,
      "X": 5.0,
      "Y": 5.0,
      "Width": 90.0,
      "Height": 10.0
    },
    {
      "$type": "BarcodeElement",
      "Value": "{Barcode}",
      "Format": "Code128",
      "X": 10.0,
      "Y": 20.0,
      "Width": 80.0,
      "Height": 20.0
    }
  ]
}
```

### 6.2 LabelElementConverter

自定义 `JsonConverter<LabelElement>`，读取 JSON 时：
1. 先解析 `$type` 字段
2. 映射到对应 .NET 类型（TextElement、BarcodeElement 等）
3. 用 `JsonSerializer.Deserialize<具体类型>()` 完成反序列化

---

## 7. TextElement 字体回退逻辑（SkiaSharp 版）

SkiaSharp 本身**支持自动 glyph 回退**（系统级字体回退），但为了确保跨平台行为可预期，TextElement 内置了显式回退：

```
CreateFont(mmToPixelMatrix, text)
    ↓
① 优先 FontPath（.ttf 文件路径）
    ↓
② 其次 FontFamily（系统字体名）
    ↓
③ 若字体为纯西文（Arial、Helvetica 等）且文本含 CJK 字符
    → 自动换用 CJK 字体（Windows: 微软雅黑 > 宋体 > 黑体；
                          macOS: 苹方; Linux: 思源黑体）
    ↓
④ 最终回退 SKTypeface.Default
```

---

## 8. NuGet 依赖

| 包 | 版本 | 用途 |
|---|---|---|
| `SkiaSharp` | 3.119.2 | 跨平台 2D 渲染引擎（核心） |
| `SkiaSharp.NativeAssets.Linux.NoDependencies` | 3.119.2 | Linux 本机资源 |
| `SkiaSharp.NativeAssets.macOS` | 3.119.2 | macOS 本机资源 |
| `ZXing.Net` | 0.16.11 | 条码编码（BitMatrix） |
| `System.Text.Json` | 10.0.7 | JSON 序列化 |
| `System.Drawing.Common` | 10.0.7 | Windows 物理打印（仅 Windows） |
| `PdfSharpCore` | 1.3.67 | 小体积向量 PDF 输出 |

---

## 9. 关键设计决策记录

| 决策 | 原因 |
|---|---|
| 全部坐标用毫米 | 与打印机 DPI 解耦，物理精度 ±0.2mm |
| SkiaSharp 为主渲染引擎 | 跨平台（Windows/Linux/macOS），无 GDI+ 依赖 |
| BarcodeElement 向量绘制 | 不生成位图，PDF 输出纯向量，缩放不失真 |
| PdfSharp 做小体积 PDF | SkiaSharp PDF 的 CJK 字体嵌入导致体积过大（23MB），PdfSharp 子集化后约 34KB |
| `$type` 字段多态 | System.Text.Json 原生多态需要 `[JsonDerivedType]` 在所有子类上标注，改用自定义 Converter 更灵活 |
| 字体映射表（FontMap） | PdfSharpCore 不支持 .ttc 格式（微软雅黑），需显式映射到 SimHei(.ttf) |
| U+00C0..U+00FF 细分 | Latin-1 区段中 SimHei 只缺带重音字母，¥ × ÷ ± 等 SimHei 都有，粗暴全段回退会误伤汉字 |

---

## 10. 扩展指南

### 新增元素类型

1. 在 `PrintLibrary.Core/Model/` 创建新文件，继承 `LabelElement`
2. 实现 `Draw(SKCanvas, SKMatrix, PrintData)` 方法
3. 在 `LabelElementConverter.cs` 的 `_typeMap` 中注册 `["NewElement"] = typeof(NewElement)`
4. 在 `PdfSharpPrinter.cs` 的 `RenderElement()` switch 中添加 case

### 新增打印后端

1. 在 `PrintLibrary.Core/Printer/` 创建新文件
2. 参考 `PdfPrinter.cs`：构造 mm→pt/px 矩阵 → 调用 `ElementRenderer.RenderTemplate()`
3. 核心 API 建议保持：`SaveToFile(template, path, data)` 和 `PrintToBytes(template, data)`

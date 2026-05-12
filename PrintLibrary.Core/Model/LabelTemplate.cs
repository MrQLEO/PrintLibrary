using SkiaSharp;
using System.Collections.Generic;

namespace PrintLibrary.Model
{
    /// <summary>
    /// 标签/纸张模板。
    /// 定义了物理尺寸（毫米）、打印机设置提示，以及若干页的元素列表。
    /// 模板本身不包含运行时数据，数据通过 <see cref="PrintData"/> 在打印/渲染时注入。
    /// </summary>
    public class LabelTemplate
    {
        // ── 物理尺寸（mm）────────────────────────────────────────────────

        /// <summary>标签/纸张宽度，单位毫米。例如 A4 横向宽 297 mm。</summary>
        public float Width { get; set; }

        /// <summary>标签/纸张高度，单位毫米。例如 A4 纵向高 210 mm。</summary>
        public float Height { get; set; }

        // ── 模板元信息 ────────────────────────────────────────────────────

        /// <summary>模板名称（人类可读），用于在模板管理器中标识。</summary>
        public string Name { get; set; } = "未命名模板";

        /// <summary>模板描述（可选）。</summary>
        public string? Description { get; set; }

        // ── 打印机提示 ────────────────────────────────────────────────────

        /// <summary>
        /// 首选打印机名称（可选）。
        /// 若不为空，LabelPrinter 会尝试使用此打印机；若未找到，则使用系统默认打印机。
        /// </summary>
        public string? PreferredPrinterName { get; set; }

        /// <summary>
        /// 打印副本数量，默认为 1。
        /// </summary>
        public int Copies { get; set; } = 1;

        // ── 元素列表 ──────────────────────────────────────────────────────

        /// <summary>
        /// 此模板包含的所有元素，按顺序绘制（后面的元素可覆盖前面的）。
        /// 支持多态序列化，详见 <see cref="Serialization.TemplateSerializer"/>。
        /// </summary>
        public List<LabelElement> Elements { get; set; } = new();

        // ── 背景设置 ──────────────────────────────────────────────────────

        /// <summary>
        /// 背景颜色（ARGB 十六进制字符串，如 "#FFFFFFFF"）。
        /// 默认为白色。渲染器在绘制元素前先填充背景色。
        /// </summary>
        public string BackgroundColor { get; set; } = "#FFFFFFFF";

        // ── 便捷工厂方法 ──────────────────────────────────────────────────

        /// <summary>
        /// 创建一个标准 A4 竖向模板（210×297 mm）。
        /// </summary>
        public static LabelTemplate A4Portrait() =>
            new LabelTemplate { Name = "A4竖向", Width = 210f, Height = 297f };

        /// <summary>
        /// 创建一个标准 A4 横向模板（297×210 mm）。
        /// </summary>
        public static LabelTemplate A4Landscape() =>
            new LabelTemplate { Name = "A4横向", Width = 297f, Height = 210f };

        /// <summary>
        /// 创建一个常见的条码标签模板（100×50 mm）。
        /// </summary>
        public static LabelTemplate BarLabel100x50() =>
            new LabelTemplate { Name = "条码标签100x50", Width = 100f, Height = 50f };

        /// <summary>
        /// 向模板添加一个元素，返回自身以支持链式调用。
        /// </summary>
        public LabelTemplate Add(LabelElement element)
        {
            Elements.Add(element);
            return this;
        }

        /// <summary>
        /// 解析背景颜色字符串为 SKColor。解析失败时返回白色。
        /// </summary>
        public SKColor GetBackgroundColor()
        {
            if (SKColor.TryParse(BackgroundColor, out var color))
                return color;
            return SKColors.White;
        }
    }
}

using SkiaSharp;
using System.Text.Json.Serialization;

namespace PrintLibrary.Model
{
    /// <summary>
    /// 所有标签元素的抽象基类。
    /// 约定坐标系：原点左上角，X 向右，Y 向下，单位为毫米（mm）。
    /// 子类需实现 <see cref="Draw"/> 方法以在指定 SKCanvas 上完成绘制。
    /// </summary>
    public abstract class LabelElement
    {
        // ── 位置与尺寸（单位：mm）────────────────────────────────────────

        /// <summary>元素左上角 X 坐标（毫米）。</summary>
        public float X { get; set; }

        /// <summary>元素左上角 Y 坐标（毫米）。</summary>
        public float Y { get; set; }

        /// <summary>元素宽度（毫米）。</summary>
        public float Width { get; set; }

        /// <summary>元素高度（毫米）。</summary>
        public float Height { get; set; }

        // ── 可见性控制 ────────────────────────────────────────────────────

        /// <summary>
        /// 是否显示该元素。为 false 时，Draw 方法内部直接返回，不参与渲染。
        /// 默认值为 true。
        /// </summary>
        public bool IsVisible { get; set; } = true;

        // ── 元素标识（可选，便于模板管理）────────────────────────────────

        /// <summary>元素唯一名称（可选），方便程序中按名称查找或更新数据绑定。</summary>
        public string? Name { get; set; }

        // ── 抽象绘制接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 将此元素绘制到 SKCanvas。
        /// </summary>
        /// <param name="canvas">目标画布（坐标已通过 mmToPixelMatrix 变换，单位为像素）</param>
        /// <param name="mmToPixelMatrix">毫米→像素的变换矩阵，由 LabelPrinter 或 TemplateRenderer 构造并传入。
        /// 调用方已通过 canvas.SetMatrix 预设，子类一般无需再次 SetMatrix，
        /// 直接用毫米坐标绘制即可。</param>
        /// <param name="data">当前页的数据绑定，用于占位符替换</param>
        public abstract void Draw(SKCanvas canvas, SKMatrix mmToPixelMatrix, PrintData data);

        /// <summary>
        /// 根据毫米坐标和矩阵，返回对应的像素矩形（SKRect）。
        /// 子类可直接调用此辅助方法以避免重复的坐标换算代码。
        /// </summary>
        protected SKRect GetPixelRect(SKMatrix mmToPixelMatrix)
        {
            // 将左上角点和右下角点分别映射到像素坐标
            var topLeft     = mmToPixelMatrix.MapPoint(X, Y);
            var bottomRight = mmToPixelMatrix.MapPoint(X + Width, Y + Height);
            return new SKRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        }
    }
}

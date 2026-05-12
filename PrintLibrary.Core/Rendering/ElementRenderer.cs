using PrintLibrary.Model;
using SkiaSharp;
using System.Collections.Generic;

namespace PrintLibrary.Rendering
{
    /// <summary>
    /// 元素渲染器。
    /// 负责将 <see cref="LabelTemplate"/> 中的所有元素按顺序绘制到指定的 <see cref="SKCanvas"/>。
    /// 本类是无状态的静态类，所有方法均为线程安全。
    ///
    /// 扩展说明：若需新增元素类型，只需继承 <see cref="LabelElement"/> 并实现 Draw 方法即可，
    /// 本类无需修改（开放/封闭原则）。
    /// </summary>
    public static class ElementRenderer
    {
        /// <summary>
        /// 将模板中所有可见元素渲染到画布。
        /// 元素按列表顺序绘制，后绘制的元素可覆盖先绘制的元素。
        /// </summary>
        /// <param name="canvas">目标 SkiaSharp 画布（已通过 SetMatrix 设置 mm→px 变换）</param>
        /// <param name="template">要渲染的标签模板</param>
        /// <param name="data">数据绑定容器（用于占位符替换）</param>
        /// <param name="mmToPixelMatrix">毫米 → 像素变换矩阵</param>
        public static void RenderTemplate(
            SKCanvas       canvas,
            LabelTemplate  template,
            PrintData      data,
            SKMatrix       mmToPixelMatrix)
        {
            // 1. 先填充背景（毫米坐标，画布矩阵会自动缩放）
            canvas.Clear(template.GetBackgroundColor());

            // 2. 逐个绘制元素
            foreach (var element in template.Elements)
            {
                RenderElement(canvas, element, data, mmToPixelMatrix);
            }
        }

        /// <summary>
        /// 渲染单个元素到画布。
        /// 每次绘制都会保存/恢复画布状态，防止元素间状态污染。
        /// </summary>
        public static void RenderElement(
            SKCanvas      canvas,
            LabelElement  element,
            PrintData     data,
            SKMatrix      mmToPixelMatrix)
        {
            if (!element.IsVisible) return;

            // 保存画布状态（防止元素 Draw 内部的 Save/Restore 失衡影响后续元素）
            int saveCount = canvas.Save();
            try
            {
                element.Draw(canvas, mmToPixelMatrix, data);
            }
            finally
            {
                // 无论元素绘制是否抛出异常，都恢复画布状态
                canvas.RestoreToCount(saveCount);
            }
        }

        /// <summary>
        /// 批量渲染多个元素（有序集合），用于需要局部刷新的场景。
        /// </summary>
        public static void RenderElements(
            SKCanvas                  canvas,
            IEnumerable<LabelElement> elements,
            PrintData                 data,
            SKMatrix                  mmToPixelMatrix)
        {
            foreach (var el in elements)
                RenderElement(canvas, el, data, mmToPixelMatrix);
        }

        /// <summary>
        /// 绘制调试用网格覆盖层（仅用于预览模式）。
        /// 在画布上绘制以指定间距（mm）排列的灰色网格线。
        /// </summary>
        public static void DrawDebugGrid(
            SKCanvas      canvas,
            LabelTemplate template,
            SKMatrix      mmToPixelMatrix,
            float         gridSpacingMm = 5f)
        {
            float pxPerMm = mmToPixelMatrix.ScaleX;

            using var gridPaint = new SKPaint
            {
                Color       = new SKColor(200, 200, 200, 128), // 半透明灰
                StrokeWidth = 0.1f,  // mm 单位，矩阵缩放后自动正确
                Style       = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            // 画布已有 mm→px 矩阵，直接用毫米坐标绘制
            for (float xMm = 0; xMm <= template.Width; xMm += gridSpacingMm)
            {
                canvas.DrawLine(xMm, 0, xMm, template.Height, gridPaint);
            }

            for (float yMm = 0; yMm <= template.Height; yMm += gridSpacingMm)
            {
                canvas.DrawLine(0, yMm, template.Width, yMm, gridPaint);
            }
        }

        /// <summary>
        /// 绘制调试用参考线（模板边界框）。
        /// 在模板四周绘制红色边框，帮助确认模板实际尺寸。
        /// </summary>
        public static void DrawDebugBorder(
            SKCanvas      canvas,
            LabelTemplate template,
            SKMatrix      mmToPixelMatrix)
        {
            float pxPerMm = mmToPixelMatrix.ScaleX;

            using var borderPaint = new SKPaint
            {
                Color       = new SKColor(255, 0, 0, 180), // 半透明红色
                StrokeWidth = 0.5f,  // mm 单位，矩阵缩放后自动正确
                Style       = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            // 画布已有 mm→px 矩阵，直接用毫米坐标绘制
            canvas.DrawRect(new SKRect(0, 0, template.Width, template.Height), borderPaint);
        }
    }
}

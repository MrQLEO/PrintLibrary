using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PrintLibrary.Model
{
    /// <summary>
    /// 打印数据容器。
    /// 以键值对形式持有模板所需的运行时数据，支持占位符替换与格式化。
    /// 占位符语法：{字段名} 或 {字段名:格式化字符串}，例如 {Date:yyyy-MM-dd}。
    /// </summary>
    public class PrintData
    {
        // ── 内部存储（大小写不敏感，与模板字段名保持宽松匹配）──────────────
        private readonly Dictionary<string, object?> _data =
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        /// <summary>所有已设置的字段名。</summary>
        public IEnumerable<string> Keys => _data.Keys;

        // ── 索引器 ────────────────────────────────────────────────────────

        /// <summary>按字段名读写值。写入 null 会保留键名但值为空。</summary>
        public object? this[string key]
        {
            get => _data.TryGetValue(key, out var v) ? v : null;
            set => _data[key] = value;
        }

        // ── 链式设置方法 ───────────────────────────────────────────────────

        /// <summary>
        /// 设置一个字段值，返回自身以支持链式调用。
        /// </summary>
        /// <param name="key">字段名（与模板占位符中的名称对应）</param>
        /// <param name="value">字段值，可以是任何对象；ToString() 将在替换时被调用</param>
        public PrintData Set(string key, object? value)
        {
            _data[key] = value;
            return this;
        }

        /// <summary>
        /// 批量设置多个字段，返回自身以支持链式调用。
        /// </summary>
        public PrintData SetRange(IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var kv in pairs)
                _data[kv.Key] = kv.Value;
            return this;
        }

        // ── 核心替换逻辑 ──────────────────────────────────────────────────

        /// <summary>
        /// 将文本中的所有占位符替换为对应的字段值。
        /// <para>支持格式：{字段名} 和 {字段名:格式化字符串}</para>
        /// <para>示例："订单日期：{OrderDate:yyyy-MM-dd}" → "订单日期：2025-01-15"</para>
        /// 若字段不存在，占位符保留原样（不报错）。
        /// </summary>
        /// <param name="template">含占位符的原始文本</param>
        /// <returns>替换后的文本</returns>
        public string Resolve(string? template)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            // 正则：匹配 {字段名} 或 {字段名:格式化字符串}
            // 捕获组 1 = 字段名，捕获组 2 = 格式化字符串（可为空）
            return Regex.Replace(template, @"\{(\w+)(?::([^}]*))?\}", match =>
            {
                var fieldName = match.Groups[1].Value;
                var format    = match.Groups[2].Value; // 可能为空字符串

                if (!_data.TryGetValue(fieldName, out var rawValue) || rawValue is null)
                    return match.Value; // 字段不存在，保留原占位符

                // 若指定了格式化字符串且值实现 IFormattable，调用 ToString(format)
                if (!string.IsNullOrEmpty(format) && rawValue is IFormattable formattable)
                    return formattable.ToString(format, null);

                return rawValue.ToString() ?? string.Empty;
            });
        }

        /// <summary>
        /// 检查指定字段是否存在且值不为 null。
        /// </summary>
        public bool Contains(string key) =>
            _data.TryGetValue(key, out var v) && v is not null;

        /// <summary>
        /// 将所有字段以 {key}={value} 格式输出，便于调试。
        /// </summary>
        public override string ToString()
        {
            return string.Join(", ", _data.Count == 0
                ? new[] { "(empty)" }
                : System.Linq.Enumerable.Select(_data, kv => $"{{{kv.Key}}}={kv.Value}"));
        }
    }
}

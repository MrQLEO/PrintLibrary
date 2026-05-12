using PrintLibrary.Model;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintLibrary.Serialization
{
    /// <summary>
    /// 标签模板序列化/反序列化器。
    /// 使用 System.Text.Json，支持 <see cref="LabelElement"/> 的多态反序列化。
    /// 
    /// 多态策略：在 JSON 中通过 "$type" 字段区分派生类型。
    /// 示例：{ "$type": "TextElement", "Text": "Hello", ... }
    /// </summary>
    public static class TemplateSerializer
    {
        // ── JSON 选项（单例，重复利用）────────────────────────────────────

        private static readonly JsonSerializerOptions _defaultOptions = BuildOptions();

        /// <summary>
        /// 构建 JSON 序列化选项：
        ///   - 支持多态（通过自定义转换器）
        ///   - 格式化输出（人类可读）
        ///   - 忽略 null 属性
        ///   - 宽松数字读取
        /// </summary>
        private static JsonSerializerOptions BuildOptions()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented         = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                NumberHandling        = JsonNumberHandling.AllowReadingFromString,
                PropertyNameCaseInsensitive = true
            };
            // 注册多态元素转换器
            options.Converters.Add(new LabelElementConverter());
            return options;
        }

        // ── 序列化（Save）────────────────────────────────────────────────

        /// <summary>
        /// 将模板序列化为 JSON 字符串。
        /// </summary>
        /// <param name="template">要序列化的模板</param>
        /// <returns>格式化 JSON 字符串</returns>
        public static string Serialize(LabelTemplate template)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            return JsonSerializer.Serialize(template, _defaultOptions);
        }

        /// <summary>
        /// 将模板序列化并保存到文件。
        /// </summary>
        /// <param name="template">要保存的模板</param>
        /// <param name="filePath">目标文件路径（含文件名）</param>
        public static void Save(LabelTemplate template, string filePath)
        {
            if (template  is null) throw new ArgumentNullException(nameof(template));
            if (filePath  is null) throw new ArgumentNullException(nameof(filePath));

            // 确保目录存在
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = Serialize(template);
            File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        // ── 反序列化（Load）──────────────────────────────────────────────

        /// <summary>
        /// 从 JSON 字符串反序列化模板。
        /// </summary>
        /// <param name="json">JSON 字符串</param>
        /// <returns>反序列化后的 <see cref="LabelTemplate"/></returns>
        /// <exception cref="InvalidOperationException">JSON 格式不正确时抛出</exception>
        public static LabelTemplate Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));

            var result = JsonSerializer.Deserialize<LabelTemplate>(json, _defaultOptions);
            return result ?? throw new InvalidOperationException("JSON 反序列化结果为 null，请检查输入格式。");
        }

        /// <summary>
        /// 从文件加载并反序列化模板。
        /// </summary>
        /// <param name="filePath">模板 JSON 文件路径</param>
        /// <returns>反序列化后的 <see cref="LabelTemplate"/></returns>
        public static LabelTemplate Load(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"模板文件未找到：{filePath}", filePath);

            string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return Deserialize(json);
        }

        // ── 流式 API（异步/Stream 场景）──────────────────────────────────

        /// <summary>
        /// 将模板序列化到 Stream（UTF-8 编码）。
        /// </summary>
        public static void SaveToStream(LabelTemplate template, Stream stream)
        {
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            JsonSerializer.Serialize(writer, template, _defaultOptions);
        }

        /// <summary>
        /// 从 Stream 加载模板（UTF-8 编码）。
        /// </summary>
        public static LabelTemplate LoadFromStream(Stream stream)
        {
            var result = JsonSerializer.Deserialize<LabelTemplate>(stream, _defaultOptions);
            return result ?? throw new InvalidOperationException("Stream 中的 JSON 数据无效。");
        }
    }
}

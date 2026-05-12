using PrintLibrary.Model;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PrintLibrary.Serialization
{
    /// <summary>
    /// <see cref="LabelElement"/> 的多态 JSON 转换器。
    /// 
    /// 序列化时：在 JSON 对象中插入 "$type" 字段（值为具体类名，如 "TextElement"）。
    /// 反序列化时：读取 "$type" 字段，按名称实例化对应的派生类。
    /// 
    /// 若需新增元素类型，仅需在 <see cref="ResolveType"/> 方法中注册新的类名映射即可。
    /// </summary>
    public sealed class LabelElementConverter : JsonConverter<LabelElement>
    {
        /// <summary>JSON 中用于标识具体类型的字段名。</summary>
        private const string TypeDiscriminator = "$type";

        // ── 反序列化 ──────────────────────────────────────────────────────

        /// <inheritdoc />
        public override LabelElement Read(
            ref Utf8JsonReader    reader,
            Type                  typeToConvert,
            JsonSerializerOptions options)
        {
            // 先将整个 JSON 对象读取为 JsonDocument（允许随机访问字段）
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            // 1. 读取类型标识符
            if (!root.TryGetProperty(TypeDiscriminator, out var typeProp))
                throw new JsonException(
                    $"JSON 元素缺少 \"{TypeDiscriminator}\" 字段，无法确定元素类型。" +
                    "请确保模板由本库序列化生成。");

            string typeName = typeProp.GetString()
                ?? throw new JsonException($"\"{TypeDiscriminator}\" 字段值为 null。");

            // 2. 按类型名映射到具体 CLR 类型
            var targetType = ResolveType(typeName)
                ?? throw new JsonException(
                    $"未知的元素类型：\"{typeName}\"。" +
                    "请在 LabelElementConverter.ResolveType() 中注册该类型。");

            // 3. 将 JsonElement 重新序列化为 UTF-8 字节再反序列化为目标类型
            //    这样可以复用目标类型已注册的所有属性映射，无需手动逐字段读取。
            string rawJson = root.GetRawText();
            var element = JsonSerializer.Deserialize(rawJson, targetType, options) as LabelElement;
            return element
                ?? throw new JsonException($"反序列化 {typeName} 返回 null，JSON 内容：{rawJson}");
        }

        // ── 序列化 ────────────────────────────────────────────────────────

        /// <inheritdoc />
        public override void Write(
            Utf8JsonWriter        writer,
            LabelElement          value,
            JsonSerializerOptions options)
        {
            // 1. 先将对象序列化到临时 JsonDocument（不带 $type）
            var tempDoc = JsonSerializer.SerializeToDocument(value, value.GetType(), options);

            // 2. 手动写出 JSON 对象，在最前面插入 "$type" 字段
            writer.WriteStartObject();

            // 写入类型标识符
            writer.WriteString(TypeDiscriminator, value.GetType().Name);

            // 将临时 Document 的所有属性依次写出
            foreach (var prop in tempDoc.RootElement.EnumerateObject())
            {
                // 跳过已有的 $type 字段（避免重复）
                if (prop.Name == TypeDiscriminator) continue;
                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        // ── 类型映射注册表 ────────────────────────────────────────────────

        /// <summary>
        /// 将 JSON 中的类型名称字符串映射到对应的 CLR 类型。
        /// 若需新增自定义元素类型，在此方法添加新的 case 即可。
        /// </summary>
        /// <param name="typeName">JSON 中 "$type" 字段的值</param>
        /// <returns>对应的 CLR 类型，不存在时返回 null</returns>
        private static Type? ResolveType(string typeName) => typeName switch
        {
            nameof(TextElement)      => typeof(TextElement),
            nameof(BarcodeElement)   => typeof(BarcodeElement),
            nameof(ImageElement)     => typeof(ImageElement),
            nameof(LineElement)      => typeof(LineElement),
            nameof(RectangleElement) => typeof(RectangleElement),
            nameof(TableElement)     => typeof(TableElement),
            // ↓ 扩展点：添加新类型时在此处追加
            // nameof(MyCustomElement) => typeof(MyCustomElement),
            _                        => null
        };
    }
}

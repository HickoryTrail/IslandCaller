using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace IslandCaller.Plugin2.Helpers
{
    internal enum JsonCharacterEncoding
    {
        Utf8,
        Utf16LittleEndian,
        Utf16BigEndian,
        Ascii
    }

    internal static class JsonHelper
    {
        private static readonly JsonSerializerOptions EscapedOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin)
        };

        private static readonly JsonSerializerOptions UnescapedOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>将 JSON 文本递归解析为字典、基本类型和强类型数组。</summary>
        internal static object? Parse(string json)
        {
            ArgumentNullException.ThrowIfNull(json);

            using var document = JsonDocument.Parse(json);
            return ConvertElement(document.RootElement);
        }

        private static object? ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    return element.EnumerateObject().ToDictionary(
                        property => property.Name,
                        property => ConvertElement(property.Value));
                case JsonValueKind.Array:
                    return ConvertArray(element.EnumerateArray().Select(ConvertElement).ToArray());
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var integer))
                    {
                        return integer;
                    }

                    if (element.TryGetSingle(out var floatingPoint))
                    {
                        return floatingPoint;
                    }

                    throw new JsonException("JSON 数值超出 Int32 和 Single 的表示范围。");
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    throw new JsonException($"不支持的 JSON 值类型：{element.ValueKind}。");
            }
        }

        private static Array ConvertArray(object?[] values)
        {
            if (values.Length == 0)
            {
                return values;
            }

            if (values.All(value => value is string))
            {
                return values.Cast<string>().ToArray();
            }

            if (values.All(value => value is int))
            {
                return values.Cast<int>().ToArray();
            }

            if (values.All(value => value is float))
            {
                return values.Cast<float>().ToArray();
            }

            if (values.All(value => value is bool))
            {
                return values.Cast<bool>().ToArray();
            }

            return values;
        }

        /// <summary>按指定字符编码生成 JSON 字节；ASCII 模式始终将非 ASCII 字符写为 \u 转义。</summary>
        internal static byte[] Encode(object value, JsonCharacterEncoding encoding, bool escapeUnicode)
        {
            ArgumentNullException.ThrowIfNull(value);

            var options = escapeUnicode || encoding == JsonCharacterEncoding.Ascii
                ? EscapedOptions
                : UnescapedOptions;
            var json = JsonSerializer.Serialize(value, options);

            return encoding switch
            {
                JsonCharacterEncoding.Utf8 => Encoding.UTF8.GetBytes(json),
                JsonCharacterEncoding.Utf16LittleEndian => new UnicodeEncoding(false, false).GetBytes(json),
                JsonCharacterEncoding.Utf16BigEndian => new UnicodeEncoding(true, false).GetBytes(json),
                JsonCharacterEncoding.Ascii => Encoding.ASCII.GetBytes(json),
                _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "不支持的 JSON 字符编码。")
            };
        }

        /// <summary>以 UTF-8 语义生成 JSON 字符串，可自行选择是否将非 ASCII 字符转义。</summary>
        internal static string Encode(object value, bool escapeUnicode)
        {
            ArgumentNullException.ThrowIfNull(value);
            return JsonSerializer.Serialize(value, escapeUnicode ? EscapedOptions : UnescapedOptions);
        }

    }
}

using System.Buffers;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace StreamingCaptureBot.Impl.Tencent.Json.Serialization;
/// <summary>
/// <see cref="JsonConverterFactory"/> to convert <see cref="IPAddress"/> to and from strings.
/// </summary>
public class JsonIpAddressConverter : JsonConverter<IPAddress>
{
    /// <inheritdoc/>
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Type {reader.TokenType} can't be serialize to IpAddress.");

        Span<char> charData = stackalloc char[45];
        var count = Encoding.UTF8.GetChars(
            reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan,
            charData);
        return !IPAddress.TryParse(charData[..count], out var value)
            ? throw new JsonException($"String [{charData[..count]}] could not be parsed to IpAddress")
            : value;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
#pragma warning disable CA1062 // Don't perform checks for performance. Trust our callers will be nice.
    {
        var data = value.AddressFamily == AddressFamily.InterNetwork
            ? stackalloc char[15]
            : stackalloc char[45];
        if (!value.TryFormat(data, out var charsWritten))
            throw new JsonException($"IPAddress [{value}] could not be written to JSON.");
        writer.WriteStringValue(data[..charsWritten]);
    }
#pragma warning restore CA1062 // Validate arguments of public methods
}
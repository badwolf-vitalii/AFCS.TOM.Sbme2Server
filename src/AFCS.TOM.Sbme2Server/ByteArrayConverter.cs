using System.Text.Json;
using System.Text.Json.Serialization;

internal class ByteArrayConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        byte[]? value = null;
        try
        {
            var sByteArray = JsonSerializer.Deserialize<short[]>(ref reader);
            var size = sByteArray?.Length ?? 0;
            value = new byte[size];
            for (int i = 0; i < size; ++i)
                value[i] = (byte)(short)(sByteArray?.GetValue(i) ?? 0);
        }
        catch
        {
            try
            {
                var sByteArray = JsonSerializer.Deserialize<byte[]>(ref reader);
                var size = sByteArray?.Length ?? 0;
                value = new byte[size];
                for (int i = 0; i < size; ++i)
                    value[i] = (byte)(sByteArray?.GetValue(i) ?? 0);
            }
            catch
            {
                try
                {
                    var sByteArray = JsonSerializer.Deserialize<int[]>(ref reader);
                    var size = sByteArray?.Length ?? 0;
                    value = new byte[size];
                    for (int i = 0; i < size; ++i)
                        value[i] = (byte)(int)(sByteArray?.GetValue(i) ?? 0);
                }
                catch
                {
                    return null;
                }
            }
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var val in value)
        {
            writer.WriteNumberValue(val);
        }

        writer.WriteEndArray();
    }
}
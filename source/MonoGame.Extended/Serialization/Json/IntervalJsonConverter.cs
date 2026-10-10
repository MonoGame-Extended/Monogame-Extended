using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MonoGame.Extended.Serialization.Json;

/// <summary>
/// Converts a <see cref="Interval{T}"/> value to or from JSON.
/// </summary>
public class IntervalJsonConverter<T> : JsonConverter<Interval<T>> where T : IComparable<T>
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Interval<T>);

    /// <inheritdoc />
    public override Interval<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Span<T> values = reader.ReadAsMultiDimensional<T>(options);

        if (values.Length == 2)
        {
            if (values[0].CompareTo(values[1]) < 0)
            {
                return new Interval<T>(values[0], values[1]);
            }

            return new Interval<T>(values[1], values[0]);
        }

        if (values.Length == 1)
        {
            return new Interval<T>(values[0], values[0]);
        }

        throw new InvalidOperationException("Invalid interval");
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Throw if <paramref name="writer"/> is <see langword="null"/>.
    /// </exception>
    public override void Write(Utf8JsonWriter writer, Interval<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartArray();

        if (options?.TypeInfoResolver != null
            && options.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
        {
            JsonSerializer.Serialize(writer, value.Min, typeInfo);
            JsonSerializer.Serialize(writer, value.Max, typeInfo);
        }
        else
        {
            LegacyWrite(writer, value, options);
        }

        writer.WriteEndArray();
    }

    [UnconditionalSuppressMessage("AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Fallback for JIT scenarios where TypeInfoResolver is not configured.")]
    [UnconditionalSuppressMessage("Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Fallback for JIT scenarios where TypeInfoResolver is not configured.")]
    private static void LegacyWrite(Utf8JsonWriter writer, Interval<T> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.Min, options);
        JsonSerializer.Serialize(writer, value.Max, options);
    }
}

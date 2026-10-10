using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MonoGame.Extended.Serialization.Json;

/// <summary>
/// Provides extension methods for working with <see cref="Utf8JsonReader"/>.
/// </summary>
public static class Utf8JsonReaderExtensions
{
    private static readonly Dictionary<Type, Func<string, object>> s_stringParsers = new Dictionary<Type, Func<string, object>>
    {
        {typeof(int), s => int.Parse(s, CultureInfo.InvariantCulture.NumberFormat)},
        {typeof(float), s => float.Parse(s, CultureInfo.InvariantCulture.NumberFormat)},
        {typeof(HslColor), s => HslColor.FromRgb(ColorHelper.FromHex(s))}
    };

    /// <summary>
    /// Reads a multi-dimensional JSON array and converts it to an array of the specified type using source-generated type metadata.
    /// </summary>
    /// <typeparam name="T">The type of the array elements.</typeparam>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="typeInfo">The metadata for the type being deserialized.</param>
    /// <returns>An array of the specified type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="typeInfo"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">Thrown when the token type is not supported.</exception>
    public static T[] ReadAsMultiDimensional<T>(this ref Utf8JsonReader reader, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        var tokenType = reader.TokenType;

        var result = tokenType switch
        {
            JsonTokenType.StartArray => reader.ReadAsJArray(typeInfo),
            JsonTokenType.String => reader.ReadAsDelimitedString<T>(),
            JsonTokenType.Number => reader.ReadAsSingleValue(typeInfo),
            _ => throw new NotSupportedException(
                $"{tokenType} is not currently supported in the multi-dimensional parser")
        };

        return result;
    }

    /// <summary>
    /// Reads a multi-dimensional JSON array and converts it to an array of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the array elements.</typeparam>
    /// <param name="reader">The <see cref="Utf8JsonReader"/> to read from.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>An array of the specified type.</returns>
    /// <exception cref="NotSupportedException">Thrown when the token type is not supported.</exception>
    public static T[] ReadAsMultiDimensional<T>(this ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        T[] result;

        if (options?.TypeInfoResolver != null
            && options.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> typeInfo)
        {
            result = reader.ReadAsMultiDimensional(typeInfo);
        }
        else
        {
            result = reader.LegacyReadAsMultiDimensional<T>(options);
        }

        return result;
    }

    private static T[] ReadAsSingleValue<T>(this ref Utf8JsonReader reader, JsonTypeInfo<T> typeInfo)
    {
        var value = JsonSerializer.Deserialize(ref reader, typeInfo);

        return [value];
    }

    private static T[] ReadAsJArray<T>(this ref Utf8JsonReader reader, JsonTypeInfo<T> typeInfo)
    {
        var items = new List<T>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            items.Add(JsonSerializer.Deserialize(ref reader, typeInfo));
        }

        return items.ToArray();
    }

    private static T[] ReadAsDelimitedString<T>(this ref Utf8JsonReader reader)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<T>();
        }

        Span<string> values = value.Split(' ');
        var result = new T[values.Length];
        var parser = s_stringParsers[typeof(T)];

        for (int i = 0; i < values.Length; i++)
        {
            result[i] = (T)parser(values[i]);
        }

        return result;
    }

    [UnconditionalSuppressMessage("AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Fallback for JIT scenarios where TypeInfoResolver is not configured.")]
    [UnconditionalSuppressMessage("Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Fallback for JIT scenarios where TypeInfoResolver is not configured.")]
    private static T[] LegacyReadAsMultiDimensional<T>(this ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var tokenType = reader.TokenType;

        T[] result = tokenType switch
        {
            JsonTokenType.StartArray => reader.LegacyReadAsJArray<T>(options),
            JsonTokenType.String => reader.ReadAsDelimitedString<T>(),
            JsonTokenType.Number => reader.LegacyReadAsSingleValue<T>(options),
            _ => throw new NotSupportedException(
                $"{tokenType} is not currently supported in the multi-dimensional parser")
        };

        return result;
    }

    [UnconditionalSuppressMessage("AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Fallback for JIT scenarios where TypeInfoResolver is not configured.")]
    [UnconditionalSuppressMessage("Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Fallback for JIT scenarios where TypeInfoResolver is not configured.")]
    private static T[] LegacyReadAsSingleValue<T>(this ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var token = JsonDocument.ParseValue(ref reader).RootElement;
        var value = JsonSerializer.Deserialize<T>(token.GetRawText(), options);

        return [value!];
    }

    [UnconditionalSuppressMessage("AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Fallback for JIT scenarios where TypeInfoResolver is not configured.")]
    [UnconditionalSuppressMessage("Trimming",
        "IL2026:RequiresUnreferencedCode",
        Justification = "Fallback for JIT scenarios where TypeInfoResolver is not configured.")]
    private static T[] LegacyReadAsJArray<T>(this ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var items = new List<T>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            items.Add(JsonSerializer.Deserialize<T>(ref reader, options));
        }

        return [.. items];
    }
}

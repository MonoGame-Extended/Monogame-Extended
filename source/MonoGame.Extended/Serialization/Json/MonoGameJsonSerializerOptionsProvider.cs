using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Xna.Framework.Content;
using MonoGame.Extended.Content.TexturePacker;

namespace MonoGame.Extended.Serialization.Json;

public static class MonoGameJsonSerializerOptionsProvider
{
    /// <summary>
    /// Gets the default <see cref="JsonSerializerOptions"/> using reflection-based serialization.
    /// </summary>
    [Obsolete("GetOptions uses reflection which is incompatible with Native AOT. For Native AOT, use ExtendedJsonSerializerContext.Default or JsonContentLoader<T> with source generation.")]
    [RequiresUnreferencedCode("Reflection-based JSON serialization/deserialization is not compatible with Native AOT.")]
    [RequiresDynamicCode("Reflection-based JSON serialization/deserialization is not compatible with Native AOT.")]
    public static JsonSerializerOptions GetOptions(ContentManager contentManager, string contentPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        options.AddConverters(contentManager, contentPath);

        return options;
    }

    /// <summary>
    /// Gets <see cref="JsonSerializerOptions"/> configured with source-generated type resolvers for Native AOT.
    /// </summary>
    public static JsonSerializerOptions GetNativeAotOptions(ContentManager contentManager, string contentPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                ExtendedJsonSerializerContext.Default,
                TexturePackerJsonSerializerContext.Default
            )
        };

        options.AddConverters(contentManager, contentPath);

        return options;
    }

    private static void AddConverters(this JsonSerializerOptions options, ContentManager contentManager, string contentPath)
    {
        options.Converters.Add(new IntervalJsonConverter<int>());
        options.Converters.Add(new IntervalJsonConverter<float>());
        options.Converters.Add(new IntervalJsonConverter<HslColor>());
        options.Converters.Add(new ThicknessJsonConverter());
        options.Converters.Add(new RectangleFJsonConverter());
        options.Converters.Add(new TextureAtlasJsonConverter(contentManager, contentPath));
        options.Converters.Add(new Size2JsonConverter());
    }
}

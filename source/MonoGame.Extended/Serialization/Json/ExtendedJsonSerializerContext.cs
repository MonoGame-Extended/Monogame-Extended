using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Graphics;

namespace MonoGame.Extended.Serialization.Json;

/// <summary>
/// Source-generated <see cref="System.Text.Json.JsonSerializer"/> context for common MonoGame or MonoGame.Extended data structures.
/// <para>
/// Provides Native AOT compatibility.
/// </para>
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    Converters = new[]
    {
        typeof(ColorJsonConverter),
        typeof(HslColorJsonConverter),
        typeof(Vector2JsonConverter),
        typeof(RectangleFJsonConverter),
        typeof(ThicknessJsonConverter),
        typeof(SizeJsonConverter),
        typeof(Size2JsonConverter),
        typeof(IntervalJsonConverter<int>),
        typeof(IntervalJsonConverter<float>),
        typeof(IntervalJsonConverter<HslColor>)
    })]
#region MonoGame Framework Types with custom converters
[JsonSerializable(typeof(Color))]
[JsonSerializable(typeof(Point))]
[JsonSerializable(typeof(Vector2))]
#endregion MonoGame Framework Types with custom converters
#region MonoGame.Extended Types
[JsonSerializable(typeof(HslColor))]
[JsonSerializable(typeof(RectangleF))]
[JsonSerializable(typeof(Thickness))]
[JsonSerializable(typeof(Size))]
[JsonSerializable(typeof(SizeF))]
[JsonSerializable(typeof(Interval<int>))]
[JsonSerializable(typeof(Interval<float>))]
[JsonSerializable(typeof(Interval<HslColor>))]
[JsonSerializable(typeof(Texture2DAtlas))]
[JsonSerializable(typeof(Texture2DRegion))]
#endregion MonoGame.Extended Types
#region Primitive types
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(float))]
#endregion Primitive types
public partial class ExtendedJsonSerializerContext : JsonSerializerContext
{
}

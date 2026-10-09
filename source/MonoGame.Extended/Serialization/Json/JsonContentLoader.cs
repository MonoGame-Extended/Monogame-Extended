using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Xna.Framework.Content;
using MonoGame.Extended.Content;

namespace MonoGame.Extended.Serialization.Json
{
    [Obsolete("JsonContentLoader uses reflection which is incompatible with Native AOT. Use JsonContentLoader<T> with source-generated JsonTypeInfo<T> instead.")]
    public class JsonContentLoader : IContentLoader
    {
        public T Load<T>(ContentManager contentManager, string path)
        {
            using var stream = contentManager.OpenStream(path);
            var nativeAotOptions = MonoGameJsonSerializerOptionsProvider.GetNativeAotOptions(contentManager, path);

            if (nativeAotOptions.TypeInfoResolver?.GetTypeInfo(typeof(T), nativeAotOptions) is JsonTypeInfo<T> typeInfo)
            {
                // Use source-generated JsonTypeInfo<T> if exists.
                return JsonSerializer.Deserialize(stream, typeInfo);
            }

            // Fall back to refletion.
            return LegacyDeserialize<T>(stream, contentManager, path)!;
        }

        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Fallback for the legacy JIT legacy scenario without JsonTypeInfo.")]
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Fallback for the legacy JIT scenario without JsonTypeInfo.")]
        private static T? LegacyDeserialize<T>(Stream stream, ContentManager contentManager, string path)
        {
            var options = MonoGameJsonSerializerOptionsProvider.GetOptions(contentManager, path);
            return JsonSerializer.Deserialize<T>(stream, options);
        }
    }

    public class JsonContentLoader<T> : IContentLoader<T>
    {
        private readonly JsonTypeInfo<T> _typeInfo;

        public JsonContentLoader(JsonTypeInfo<T> typeInfo)
        {
            ArgumentNullException.ThrowIfNull(typeInfo);
            _typeInfo = typeInfo;
        }

        public T Load(ContentManager contentManager, string path)
        {
            using var stream = contentManager.OpenStream(path);

            return JsonSerializer.Deserialize(stream, _typeInfo);
        }
    }
}

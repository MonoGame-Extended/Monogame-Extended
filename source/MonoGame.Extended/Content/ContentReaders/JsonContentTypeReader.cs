using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Xna.Framework.Content;

namespace MonoGame.Extended.Content.ContentReaders
{
    public class JsonContentTypeReader<T> : ContentTypeReader<T>
    {
        internal static readonly string NativeAotRegistrationKey =
            $"{typeof(JsonContentTypeReader<T>).FullName}, {typeof(JsonContentTypeReader<T>).Assembly.GetName().Name}";

        private readonly JsonTypeInfo<T>? _jsonTypeInfo;

        public JsonContentTypeReader()
        {
        }

        public JsonContentTypeReader(JsonTypeInfo<T> jsonTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(jsonTypeInfo);
            _jsonTypeInfo = jsonTypeInfo;
        }

#if !FNA && !KNI
        /// <summary>
        /// Registers <see cref="JsonContentTypeReader{T}"/> for the type <typeparamref name="T"/> with the
        /// <see cref="ContentTypeReaderManager"/> so it is resolved without reflection.
        /// </summary>
        /// <remarks>
        /// Call this method once per concrete type during application startup when publishing with
        /// <c>PublishAot</c> or <c>PublishTrimmed</c>.
        /// </remarks>
        [Obsolete("Use Register(JsonTypeInfo<T> jsonTypeInfo) overload for Native AOT compatibility.")]
        public static void Register()
        {
            // Preferred registration.
            ContentTypeReaderManager.AddTypeCreator(
                NativeAotRegistrationKey,
                () => new JsonContentTypeReader<T>());

            // Backwards compatibility.
            ContentTypeReaderManager.AddTypeCreator(
                typeof(JsonContentTypeReader<T>).AssemblyQualifiedName,
                () => new JsonContentTypeReader<T>());
        }

        /// <summary>
        /// Registers <see cref="JsonContentTypeReader{T}"/> for the type <typeparamref name="T"/> with the
        /// <see cref="ContentTypeReaderManager"/> using source-generated type metadata for Native AOT compatibility.
        /// </summary>
        /// <param name="jsonTypeInfo">The source-generated type metadata for <typeparamref name="T"/>.</param>
        public static void Register(JsonTypeInfo<T> jsonTypeInfo)
        {
            ArgumentNullException.ThrowIfNull(jsonTypeInfo);

            // Preferred registration.
            ContentTypeReaderManager.AddTypeCreator(
                NativeAotRegistrationKey,
                () => new JsonContentTypeReader<T>(jsonTypeInfo));

            // Backwards compatibility.
            ContentTypeReaderManager.AddTypeCreator(
                typeof(JsonContentTypeReader<T>).AssemblyQualifiedName,
                () => new JsonContentTypeReader<T>(jsonTypeInfo));
        }
#endif

        protected override T Read(ContentReader reader, T existingInstance)
        {
            var json = reader.ReadString();

            return _jsonTypeInfo != null
                ? JsonSerializer.Deserialize(json, _jsonTypeInfo)
                : LegacyDeserialize(json);
        }

        [UnconditionalSuppressMessage("AOT",
            "IL3050:RequiresDynamicCode",
            Justification = "Fallback for JIT scenarios when registered without JsonTypeInfo.")]
        [UnconditionalSuppressMessage("Trimming",
            "IL2026:RequiresUnreferencedCode",
            Justification = "Fallback for JIT scenarios when registered without JsonTypeInfo.")]
        private static T? LegacyDeserialize(string json) => JsonSerializer.Deserialize<T>(json);
    }
}

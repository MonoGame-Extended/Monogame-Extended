using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoGame.Extended.Serialization.Json
{
    [RequiresUnreferencedCode($"{nameof(BaseTypeJsonConverter<T>)} uses reflection to inspect types and properties and is not compatible with Native AOT.")]
    [RequiresDynamicCode($"{nameof(BaseTypeJsonConverter<T>)} requires dynamic code generation for polymorphic serialization and is not compatible with Native AOT.")]
    [Obsolete($"{nameof(BaseTypeJsonConverter<T>)} is deprecated. Use System.Text.Json polymorphic type discrimination ([JsonPolymorphic], [JsonDerivedType]) instead.")]
    public abstract class BaseTypeJsonConverter<T> : JsonConverter<T>
    {
        private readonly string _suffix;
        private readonly Dictionary<string, Type> _namesToTypes;
        private readonly Dictionary<Type, string> _typesToNames;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly JsonNamingPolicy _namingPolicy = JsonNamingPolicy.CamelCase;

        protected BaseTypeJsonConverter(IEnumerable<TypeInfo> supportedTypes, string suffix)
        {
            _suffix = suffix;
            _namesToTypes = supportedTypes
                .ToDictionary(t => TrimSuffix(t.Name, suffix), t => t.AsType(), StringComparer.OrdinalIgnoreCase);
            _typesToNames = _namesToTypes.ToDictionary(i => i.Value, i => i.Key);

            _serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = _namingPolicy,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            _serializerOptions.Converters.Add(this);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert) =>
            _namesToTypes.ContainsValue(typeToConvert) || typeof(T) == typeToConvert;

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException" />
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Class is annotated with RequiresDynamicCode.")]
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Class is annotated with RequiresUnreferencedCode.")]
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
            {
                var jObject = doc.RootElement;
                var key = jObject.GetProperty("type").GetString();

                if (_namesToTypes.TryGetValue(key, out Type type))
                {
                    var value = JsonSerializer.Deserialize(jObject.GetRawText(), type, options);
                    return (T)value;
                }

                throw new InvalidOperationException($"Unknown {_suffix} type '{key}'");
            }
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">
        /// Throw if <paramref name="writer"/> is <see langword="null"/>.
        /// </exception>
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Class is annotated with RequiresDynamicCode.")]
        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Class is annotated with RequiresUnreferencedCode.")]
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var type = value.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            writer.WriteStartObject();
            writer.WriteString("type", _typesToNames[type]);

            foreach (var property in properties)
            {
                var propertyName = _namingPolicy.ConvertName(property.Name);
                writer.WritePropertyName(propertyName);
                JsonSerializer.Serialize(writer, property.GetValue(value), property.PropertyType, options);
            }

            writer.WriteEndObject();
        }

        private static string TrimSuffix(string input, string suffix)
        {
            if (input.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return input.Substring(0, input.Length - suffix.Length);

            return input;
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Content;
using MonoGame.Extended.Content.TexturePacker;
using MonoGame.Extended.Graphics;

namespace MonoGame.Extended.Serialization.Json
{
    public class TextureAtlasJsonConverter : JsonConverter<Texture2DAtlas>
    {
        private readonly ContentManager _contentManager;
        private readonly string _path;

        public TextureAtlasJsonConverter(ContentManager contentManager, string path)
        {
            _contentManager = contentManager;
            _path = path;
        }

        /// <inheritdoc />
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Texture2DAtlas);

        public override Texture2DAtlas Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                // TODO: (Aristurtle 05/20/2024) What is this for? It's just an if block that throws an exception. Need
                // to investigate.
                var textureAtlasAssetName = reader.GetString();
                var contentPath = GetContentPath(textureAtlasAssetName);
                var texturePackerFile = _contentManager.Load<TexturePackerFileContent>(contentPath,
                    new JsonContentLoader<TexturePackerFileContent>(TexturePackerJsonSerializerContext.Default.TexturePackerFileContent));
                var texture = _contentManager.Load<Texture2D>(texturePackerFile.Meta.Image);
                //return TextureAtlas.Create(texturePackerFile.Metadata.Image, texture );
                throw new NotImplementedException();
            }
            else
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException($"Expected {nameof(JsonTokenType.StartObject)} token");
                }

                string textureProperty = string.Empty;
                int regionWidth = 0;
                int regionHeight = 0;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        break;
                    }

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propertyName = reader.GetString();
                        reader.Read();

                        if (string.Equals(propertyName, "texture", StringComparison.OrdinalIgnoreCase))
                        {
                            textureProperty = reader.GetString() ?? string.Empty;
                        }
                        else if (string.Equals(propertyName, "regionWidth", StringComparison.OrdinalIgnoreCase))
                        {
                            regionWidth = reader.GetInt32();
                        }
                        else if (string.Equals(propertyName, "regionHeight", StringComparison.OrdinalIgnoreCase))
                        {
                            regionHeight = reader.GetInt32();
                        }
                        else
                        {
                            Trace.TraceWarning($"Ignoring unexpected property: {propertyName}");
                            reader.Skip();
                        }
                    }
                }

                var (texture, assetName) = LoadTexture(textureProperty);

                return Texture2DAtlas.Create(
                    assetName,
                    texture,
                    regionWidth,
                    regionHeight);
            }
        }

        private (Texture2D Texture, string AssetName) LoadTexture(string textureProperty)
        {
            var textureAtlasDirectory = Path.GetDirectoryName(_path) ?? string.Empty;
            var textureDirectory = Path.GetDirectoryName(textureProperty)?.TrimStart('/', '\\') ?? string.Empty;
            var textureName = Path.GetFileNameWithoutExtension(textureProperty);

            var rootDirectory = string.IsNullOrEmpty(_contentManager.RootDirectory)
                ? "."
                : _contentManager.RootDirectory;

            var fullRoot = Path.GetFullPath(rootDirectory);
            var fullTexturePath = Path.GetFullPath(Path.Combine(fullRoot, textureAtlasDirectory, textureDirectory, textureName));
            var resolvedAssetName = Path.GetRelativePath(fullRoot, fullTexturePath).Replace('\\', '/');

            (Texture2D Texture, string AssetName) result;

            try
            {
                var texture = _contentManager.Load<Texture2D>(resolvedAssetName);
                result = (texture, resolvedAssetName);
            }
            catch (Exception ex)
            {
                var fallbackName = string.IsNullOrEmpty(textureDirectory)
                    ? textureName
                    : Path.Combine(textureDirectory, textureName).Replace('\\', '/');

                if (!string.Equals(resolvedAssetName, fallbackName, StringComparison.OrdinalIgnoreCase))
                {
                    Trace.TraceWarning(
                        $"Failed to load texture at resolved path '{resolvedAssetName}'. Attempting fallback path '{fallbackName}'. Exception: {ex.Message}");

                    var texture = _contentManager.Load<Texture2D>(fallbackName);
                    result = (texture, fallbackName);
                }
                else
                {
                    // Bubble up the original exception if no fallback was attempted.
                    throw;
                }
            }

            return result;
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Texture2DAtlas value, JsonSerializerOptions options) { }

        private string GetContentPath(string relativePath)
        {
            var directory = Path.GetDirectoryName(_path);
            return Path.Combine(directory, relativePath);
        }
    }
}

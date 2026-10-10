// Copyright (c) Craftwork Games. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using System.IO;
using System.Text.Json;

namespace MonoGame.Extended.Content.TexturePacker;

internal static class TexturePackerFileReader
{
    internal static TexturePackerFileContent Read(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    internal static TexturePackerFileContent Read(Stream stream)
    {
        var tpFile = JsonSerializer.Deserialize(stream, TexturePackerJsonSerializerContext.Default.TexturePackerFileContent);
        return tpFile;
    }
}

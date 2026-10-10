// Copyright (c) Craftwork Games. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MonoGame.Extended.Tests.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(TestPoco))]
internal partial class CustomTestPocoSerializerContext : JsonSerializerContext
{
}

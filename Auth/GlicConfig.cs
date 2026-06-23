// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GLic.Auth;

public record GlicConfig(
    [property: JsonPropertyName("customer_id")] string CustomerId,
    [property: JsonPropertyName("admin_email")] string AdminEmail,
    [property: JsonPropertyName("credential_path")] string? CredentialPath = null)
{
    public static GlicConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Config file not found: {path}", path);

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GlicConfig>(json)
                ?? throw new InvalidOperationException($"Failed to parse {path}: result was null");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse {path}: {ex.Message}", ex);
        }
    }
}

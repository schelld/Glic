// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Text.Json;

namespace GLic.Helpers;

public record SkuEntry(string ProductId, string ProductName, string SkuId, string SkuName, bool Active = true);

public static class SkuCatalog
{
    public static IReadOnlyList<SkuEntry> Resolve(string? skuIdsFlag, string skuFilePath)
    {
        IReadOnlyList<SkuEntry> resolved = skuIdsFlag is not null
            ? ParseSkuIds(skuIdsFlag)
            : File.Exists(skuFilePath)
                ? LoadFromFile(skuFilePath)
                : EmbeddedDefaults;

        // Filter applies to all three source paths; ParseSkuIds always produces Active=true entries.
        return resolved.Where(e => e.Active).ToList();
    }

    public static IReadOnlyList<SkuEntry> LoadAll(string skuFilePath) =>
        File.Exists(skuFilePath) ? LoadFromFile(skuFilePath) : [];

    private static IReadOnlyList<SkuEntry> ParseSkuIds(string skuIds) =>
        skuIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
              .Select(pair =>
              {
                  var parts = pair.Split(new[] { ':' }, 2);
                  if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
                      throw new InvalidOperationException(
                          $"Invalid --sku-ids entry '{pair}'. Expected format: productId:skuId");
                  return new SkuEntry(parts[0], parts[0], parts[1], parts[1]);
              })
              .ToList();

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static IReadOnlyList<SkuEntry> LoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<SkuEntry>>(json, _jsonOptions)
                   ?? throw new InvalidOperationException($"'{path}' deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse '{path}': {ex.Message}", ex);
        }
    }

    // Verify this list against the official Google reference before shipping:
    // https://developers.google.com/admin-sdk/licensing/v1/how-tos/products
    public static readonly IReadOnlyList<SkuEntry> EmbeddedDefaults =
    [
        new("Google-Apps", "Google Workspace (Legacy)",           "Google-Apps-Unlimited",         "Business Starter"),
        new("Google-Apps", "Google Workspace (Legacy)",           "Google-Apps-For-Business",       "Legacy G Suite Basic"),
        new("Google-Apps", "Google Workspace (Legacy)",           "Google-Apps-Lite",               "Essentials Starter"),
        new("Google-Apps", "Google Workspace Enterprise (Legacy)","1010020020",                     "Enterprise Plus"),
        new("Google-Apps", "Google Workspace Business Plus",      "1010020025",                     "Business Plus"),
        new("Google-Apps", "Google Workspace Business Starter",   "1010020026",                     "Business Starter"),
        new("Google-Apps", "Google Workspace Business Standard",  "1010020028",                     "Business Standard"),
        new("Google-Apps", "Google Workspace Business Plus",      "1010020030",                     "Business Plus"),
        new("101001",      "Cloud Identity",                      "1010010001",                     "Cloud Identity Free"),
        new("101005",      "Cloud Identity Premium",              "1010050001",                     "Cloud Identity Premium"),
        new("Google-Apps", "Google Workspace Frontline",          "1010020031",                     "Frontline Starter"),
        new("Google-Apps", "Google Workspace Frontline",          "1010020033",                     "Frontline Standard"),
        new("Google-Apps", "Google Workspace Education",          "1010020034",                     "Education Fundamentals"),
        new("Google-Apps", "Google Workspace Education",          "1010020035",                     "Education Standard"),
        new("Google-Apps", "Google Workspace Education",          "1010020037",                     "Teaching and Learning Upgrade"),
        new("Google-Apps", "Google Workspace Education",          "1010020038",                     "Education Plus"),
        new("Google-Apps", "Google Voice",                        "1010060001",                     "Voice Starter"),
        new("Google-Apps", "Google Voice",                        "1010060002",                     "Voice Standard"),
        new("Google-Apps", "Google Voice",                        "1010060003",                     "Voice Premier"),
    ];
}

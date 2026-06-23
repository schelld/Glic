// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Net;
using System.Text.Json;
using GLic.Auth;
using GLic.Helpers;

namespace GLic.Cmdlets;

[Cmdlet(VerbsLifecycle.Invoke, "GlicDiscover")]
[OutputType(typeof(DiscoverChangeRow))]
public sealed class InvokeGlicDiscoverCmdlet : GlicCmdletBase
{
    protected override async Task RunAsync(CancellationToken ct)
    {
        var cfg = LoadConfig();
        var clients = await BuildClientsAsync(cfg);

        WriteVerbose("Probing SKU catalog...");
        var foundSkuIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sku in ProbeCatalog)
        {
            try
            {
                var req = clients.Licensing.LicenseAssignments.ListForProductAndSku(
                    sku.ProductId, sku.SkuId, cfg.CustomerId);
                req.MaxResults = 1;
                var resp = await req.ExecuteAsync(ct);
                if (resp.Items?.Count > 0)
                    foundSkuIds.Add(sku.SkuId);
            }
            catch (Google.GoogleApiException ex)
                when (ex.HttpStatusCode is HttpStatusCode.NotFound
                                        or HttpStatusCode.Forbidden
                                        or HttpStatusCode.BadRequest)
            {
                // SKU not available on this tenant — expected, silent
            }
            catch (Google.GoogleApiException ex)
            {
                WriteVerbose($"Warning — {sku.SkuId}: {ex.Message}");
            }
        }

        // Read prefers an earlier discover result in the config dir, falling back
        // to the bundled module copy; write always targets the config dir.
        var skuFilePath = ConfigLocator.ResolveSkuWritePath(ResolvedConfigPath);
        var existing = SkuCatalog.LoadAll(ConfigLocator.ResolveSkuReadPath(ResolvedConfigPath));
        var merged = MergeResults(existing, foundSkuIds, ProbeCatalog);

        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        var json = JsonSerializer.Serialize(merged, writeOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(skuFilePath)!);
        await Task.Run(() => File.WriteAllText(skuFilePath, json), ct);

        EmitChangeSummary(existing, merged);
    }

    private void EmitChangeSummary(IReadOnlyList<SkuEntry> before, IReadOnlyList<SkuEntry> after)
    {
        var beforeMap = before.ToDictionary(e => (e.ProductId, e.SkuId));
        var changeRows = new List<DiscoverChangeRow>();

        foreach (var entry in after)
        {
            var key = (entry.ProductId, entry.SkuId);
            bool existed = beforeMap.TryGetValue(key, out var prev);

            if (!existed && entry.Active)
                changeRows.Add(new DiscoverChangeRow(entry.SkuName, entry.SkuId, "now active"));
            else if (existed && prev!.Active != entry.Active)
                changeRows.Add(new DiscoverChangeRow(entry.SkuName, entry.SkuId,
                    entry.Active ? "now active" : "set inactive"));
        }

        if (changeRows.Count == 0)
        {
            WriteVerbose("No changes — skus.json is up to date.");
            return;
        }

        foreach (var row in changeRows)
            BufferObject(row);

        int nowActive   = after.Count(e => e.Active);
        int setInactive = changeRows.Count(r => r.Status == "set inactive");
        int newCount    = changeRows.Count(r => r.Status == "now active");
        WriteVerbose($"skus.json updated — {nowActive} active, {setInactive} set inactive, {newCount} new");
    }

    internal static readonly IReadOnlyList<SkuEntry> ProbeCatalog =
    [
        new("Google-Apps", "Google Workspace for Education",         "1010310003", "Education Fundamentals"),
        new("Google-Apps", "Google Workspace for Education",         "1010310008", "Education Fundamentals - Archived User"),
        new("Google-Apps", "Google Workspace for Education",         "1010310009", "Education Gmail Only User"),
        new("Google-Apps", "Google Workspace for Education",         "1010310010", "Education Plus"),
        new("Google-Apps", "Google Workspace for Education",         "1010310004", "Education Plus (Staff)"),
        new("Google-Apps", "Google Workspace Education",             "1010020034", "Education Fundamentals (Alt)"),
        new("Google-Apps", "Google Workspace Education",             "1010020035", "Education Standard"),
        new("Google-Apps", "Google Workspace Education",             "1010020037", "Teaching and Learning Upgrade"),
        new("Google-Apps", "Google Workspace Education",             "1010020038", "Education Plus (Alt)"),
        new("Google-Apps", "Google Workspace Business Starter",      "1010020026", "Business Starter"),
        new("Google-Apps", "Google Workspace Business Standard",     "1010020028", "Business Standard"),
        new("Google-Apps", "Google Workspace Business Plus",         "1010020030", "Business Plus"),
        new("Google-Apps", "Google Workspace Enterprise",            "1010020020", "Enterprise Plus (Legacy)"),
        new("Google-Apps", "Google Workspace Enterprise Starter",    "1010020025", "Enterprise Starter"),
        new("Google-Apps", "Google Workspace Enterprise Standard",   "1010020027", "Enterprise Standard"),
        new("Google-Apps", "Google Workspace Enterprise Plus",       "1010020029", "Enterprise Plus"),
        new("Google-Apps", "Google Workspace Enterprise Essentials", "1010020021", "Enterprise Essentials"),
        new("Google-Apps", "Google Workspace Enterprise Essentials", "1010020032", "Enterprise Essentials Plus"),
        new("Google-Apps", "Google Workspace Frontline",             "1010020031", "Frontline Starter"),
        new("Google-Apps", "Google Workspace Frontline",             "1010020033", "Frontline Standard"),
        new("101001",      "Cloud Identity",                         "1010010001", "Cloud Identity Free"),
        new("101005",      "Cloud Identity Premium",                 "1010050001", "Cloud Identity Premium"),
        new("Google-Apps", "Google Voice",                           "1010060001", "Voice Starter"),
        new("Google-Apps", "Google Voice",                           "1010060002", "Voice Standard"),
        new("Google-Apps", "Google Voice",                           "1010060003", "Voice Premier"),
        new("Google-Apps", "Google Workspace (Legacy)",              "Google-Apps-Unlimited",    "Business Starter (Legacy)"),
        new("Google-Apps", "Google Workspace (Legacy)",              "Google-Apps-For-Business", "Legacy G Suite Basic"),
        new("Google-Apps", "Google Workspace (Legacy)",              "Google-Apps-Lite",         "Essentials Starter (Legacy)"),
    ];

    internal static IReadOnlyList<SkuEntry> MergeResults(
        IReadOnlyList<SkuEntry> existing,
        ICollection<string> foundSkuIds,
        IReadOnlyList<SkuEntry> probeCatalog)
    {
        var result = existing.ToDictionary(e => (e.ProductId, e.SkuId));
        foreach (var catalogEntry in probeCatalog)
        {
            var key = (catalogEntry.ProductId, catalogEntry.SkuId);
            if (foundSkuIds.Contains(catalogEntry.SkuId))
                result[key] = catalogEntry with { Active = true };
            else if (result.ContainsKey(key))
                result[key] = result[key] with { Active = false };
        }
        return [.. result.Values];
    }
}

public sealed class DiscoverChangeRow
{
    public string SkuName { get; }
    public string SkuId   { get; }
    public string Status  { get; }

    public DiscoverChangeRow(string SkuName, string SkuId, string Status)
    {
        this.SkuName = SkuName;
        this.SkuId   = SkuId;
        this.Status  = Status;
    }
}

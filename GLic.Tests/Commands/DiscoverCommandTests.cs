// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Cmdlets;
using GLic.Helpers;

namespace GLic.Tests.Commands;

public class DiscoverCommandTests
{
    [Fact]
    public void MergeResults_NewFound_AddsActiveTrue()
    {
        var catalog = new[] { new SkuEntry("Google-Apps", "Workspace", "sku-new", "New SKU") };

        var result = InvokeGlicDiscoverCmdlet.MergeResults([], new HashSet<string> { "sku-new" }, catalog);

        Assert.Single(result);
        Assert.Equal("sku-new", result[0].SkuId);
        Assert.True(result[0].Active);
    }

    [Fact]
    public void MergeResults_ExistingFound_SetsActiveTrue()
    {
        var existing = new[] { new SkuEntry("Google-Apps", "Workspace", "sku-existing", "Existing", false) };
        var catalog  = new[] { new SkuEntry("Google-Apps", "Workspace", "sku-existing", "Existing") };

        var result = InvokeGlicDiscoverCmdlet.MergeResults(existing, new HashSet<string> { "sku-existing" }, catalog);

        Assert.Single(result);
        Assert.True(result[0].Active);
    }

    [Fact]
    public void MergeResults_ExistingNotFound_SetsActiveFalse()
    {
        var existing = new[] { new SkuEntry("Google-Apps", "Workspace", "sku-lapsed", "Lapsed") };
        var catalog  = new[] { new SkuEntry("Google-Apps", "Workspace", "sku-lapsed", "Lapsed") };

        var result = InvokeGlicDiscoverCmdlet.MergeResults(existing, new HashSet<string>(), catalog);

        Assert.Single(result);
        Assert.False(result[0].Active);
    }

    [Fact]
    public void MergeResults_NotInCatalog_ActiveUnchanged()
    {
        // sku-manual: in existing, NOT in catalog → Active stays true (default)
        // sku-other:  in catalog, not found, not in existing → not added
        var existing = new[] { new SkuEntry("Google-Apps", "Workspace", "sku-manual", "Manual") };
        var catalog  = new[] { new SkuEntry("Google-Apps", "Workspace", "sku-other",  "Other") };

        var result = InvokeGlicDiscoverCmdlet.MergeResults(existing, new HashSet<string>(), catalog);

        Assert.Single(result);
        Assert.Equal("sku-manual", result[0].SkuId);
        Assert.True(result[0].Active);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Helpers;

namespace GLic.Tests.Helpers;

public class SkuCatalogTests
{
    [Fact]
    public void Resolve_WithSkuIdsFlag_ParsesTwoPairs()
    {
        var result = SkuCatalog.Resolve("prod1:sku1,prod2:sku2", "nonexistent.json");

        Assert.Equal(2, result.Count);
        Assert.Equal("prod1", result[0].ProductId);
        Assert.Equal("sku1", result[0].SkuId);
        Assert.Equal("prod2", result[1].ProductId);
        Assert.Equal("sku2", result[1].SkuId);
    }

    [Fact]
    public void Resolve_WithSkuIdsFlag_SetsNamesToIds()
    {
        var result = SkuCatalog.Resolve("prod1:sku1", "nonexistent.json");

        Assert.Equal("prod1", result[0].ProductName);
        Assert.Equal("sku1", result[0].SkuName);
    }

    [Fact]
    public void Resolve_WithValidJsonFile_LoadsEntries()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            [
              { "productId": "Google-Apps", "productName": "Workspace", "skuId": "Google-Apps-Unlimited", "skuName": "Business" }
            ]
            """);
        try
        {
            var result = SkuCatalog.Resolve(null, path);
            Assert.Single(result);
            Assert.Equal("Google-Apps", result[0].ProductId);
            Assert.Equal("Workspace", result[0].ProductName);
            Assert.Equal("Google-Apps-Unlimited", result[0].SkuId);
            Assert.Equal("Business", result[0].SkuName);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Resolve_WithMissingFile_ReturnsEmbeddedDefaults()
    {
        var result = SkuCatalog.Resolve(null, "definitely-missing.json");

        Assert.NotEmpty(result);
        Assert.All(result, e =>
        {
            Assert.NotEmpty(e.ProductId);
            Assert.NotEmpty(e.SkuId);
        });
    }

    [Fact]
    public void Resolve_WithMalformedJson_ThrowsInvalidOperationException()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ this is not valid json }");
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => SkuCatalog.Resolve(null, path));
            Assert.Contains("Failed to parse", ex.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Resolve_WithMalformedSkuIdsFlag_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SkuCatalog.Resolve("prod1-no-colon", "nonexistent.json"));
        Assert.Contains("Invalid --sku-ids entry", ex.Message);
    }

    [Fact]
    public void Resolve_WithJsonMissingActiveField_DefaultsToTrue()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            [
              { "productId": "Google-Apps", "productName": "Workspace", "skuId": "sku-no-active", "skuName": "Test" }
            ]
            """);
        try
        {
            var result = SkuCatalog.Resolve(null, path);
            Assert.Single(result);
            Assert.True(result[0].Active);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Resolve_FiltersOutInactiveEntries()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            [
              { "productId": "Google-Apps", "productName": "Workspace", "skuId": "sku-active",   "skuName": "Active",   "active": true  },
              { "productId": "Google-Apps", "productName": "Workspace", "skuId": "sku-inactive", "skuName": "Inactive", "active": false }
            ]
            """);
        try
        {
            var result = SkuCatalog.Resolve(null, path);
            Assert.Single(result);
            Assert.Equal("sku-active", result[0].SkuId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void LoadAll_WithMissingFile_ReturnsEmpty()
    {
        var result = SkuCatalog.LoadAll("definitely-missing.json");

        Assert.Empty(result);
    }

    [Fact]
    public void LoadAll_WithValidFile_ReturnsAllEntriesIncludingInactive()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            [
              { "productId": "Google-Apps", "productName": "Workspace", "skuId": "sku-active",   "skuName": "Active",   "active": true  },
              { "productId": "Google-Apps", "productName": "Workspace", "skuId": "sku-inactive", "skuName": "Inactive", "active": false }
            ]
            """);
        try
        {
            var result = SkuCatalog.LoadAll(path);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, e => e.SkuId == "sku-active"   && e.Active == true);
            Assert.Contains(result, e => e.SkuId == "sku-inactive" && e.Active == false);
        }
        finally { File.Delete(path); }
    }
}

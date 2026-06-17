// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using GLic.Auth;

namespace GLic.Tests.Auth;

public class ConfigLocatorTests : IDisposable
{
    private readonly string _root = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "GLicTests-" + Guid.NewGuid().ToString("N"))).FullName;

    private string MakeDir(string name)
    {
        var dir = Path.Combine(_root, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string MakeFile(string dir, string name, string content = "{}")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // --- ResolveConfigPath ---

    [Fact]
    public void ResolveConfigPath_ExplicitPath_WinsOverEverything()
    {
        var probeDir = MakeDir("probe");
        MakeFile(probeDir, "glic.json");

        var result = ConfigLocator.ResolveConfigPath(
            explicitPath: @"C:\explicit\glic.json", envPath: @"C:\env\glic.json", probeDirs: new[] { probeDir });

        // Returned verbatim even if it does not exist — GlicConfig.Load reports the miss.
        Assert.Equal(@"C:\explicit\glic.json", result);
    }

    [Fact]
    public void ResolveConfigPath_EnvPath_WinsOverProbeDirs()
    {
        var probeDir = MakeDir("probe");
        MakeFile(probeDir, "glic.json");

        var result = ConfigLocator.ResolveConfigPath(
            explicitPath: null, envPath: @"C:\env\glic.json", probeDirs: new[] { probeDir });

        Assert.Equal(@"C:\env\glic.json", result);
    }

    [Fact]
    public void ResolveConfigPath_FirstProbeDirWithFile_Wins()
    {
        var first = MakeDir("first");   // no glic.json here
        var second = MakeDir("second");
        var expected = MakeFile(second, "glic.json");

        var result = ConfigLocator.ResolveConfigPath(null, null, new[] { first, second });

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolveConfigPath_NothingFound_ThrowsListingAllCandidates()
    {
        var first = MakeDir("first");
        var second = MakeDir("second");

        var ex = Assert.Throws<FileNotFoundException>(
            () => ConfigLocator.ResolveConfigPath(null, null, new[] { first, second }));

        Assert.Contains(Path.Combine(first, "glic.json"), ex.Message);
        Assert.Contains(Path.Combine(second, "glic.json"), ex.Message);
        Assert.Contains("GLIC_CONFIG", ex.Message);
    }

    // --- ResolveCredentialPath ---

    [Fact]
    public void ResolveCredentialPath_ExplicitPath_Wins()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");
        MakeFile(configDir, "service-account.json");

        var result = ConfigLocator.ResolveCredentialPath(
            explicitPath: @"C:\explicit\sa.json", configuredPath: null,
            configPath: configPath, moduleDir: MakeDir("module"));

        Assert.Equal(@"C:\explicit\sa.json", result);
    }

    [Fact]
    public void ResolveCredentialPath_ConfiguredRelativePath_ResolvesAgainstConfigDir()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");

        var result = ConfigLocator.ResolveCredentialPath(
            null, configuredPath: "keys\\sa.json", configPath: configPath, moduleDir: MakeDir("module"));

        Assert.Equal(Path.Combine(configDir, "keys", "sa.json"), result);
    }

    [Fact]
    public void ResolveCredentialPath_ConfiguredRootedPath_ReturnedAsIs()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");

        var result = ConfigLocator.ResolveCredentialPath(
            null, configuredPath: @"D:\keys\sa.json", configPath: configPath, moduleDir: MakeDir("module"));

        Assert.Equal(@"D:\keys\sa.json", result);
    }

    [Fact]
    public void ResolveCredentialPath_SiblingOfConfig_PreferredOverModuleDir()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");
        var sibling = MakeFile(configDir, "service-account.json");
        var moduleDir = MakeDir("module");
        MakeFile(moduleDir, "service-account.json");

        var result = ConfigLocator.ResolveCredentialPath(null, null, configPath, moduleDir);

        Assert.Equal(sibling, result);
    }

    [Fact]
    public void ResolveCredentialPath_ModuleDirFallback_WhenNoSibling()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");
        var moduleDir = MakeDir("module");
        var moduleCred = MakeFile(moduleDir, "service-account.json");

        var result = ConfigLocator.ResolveCredentialPath(null, null, configPath, moduleDir);

        Assert.Equal(moduleCred, result);
    }

    [Fact]
    public void ResolveCredentialPath_NothingFound_ThrowsListingAllCandidates()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");
        var moduleDir = MakeDir("module");

        var ex = Assert.Throws<FileNotFoundException>(
            () => ConfigLocator.ResolveCredentialPath(null, null, configPath, moduleDir));

        Assert.Contains(Path.Combine(configDir, "service-account.json"), ex.Message);
        Assert.Contains(Path.Combine(moduleDir, "service-account.json"), ex.Message);
        Assert.Contains("-ServiceAccountPath", ex.Message);
    }

    // --- SKU paths ---

    [Fact]
    public void ResolveSkuWritePath_AlwaysConfigDir()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");

        var result = ConfigLocator.ResolveSkuWritePath(configPath);

        Assert.Equal(Path.Combine(configDir, "skus.json"), result);
    }

    [Fact]
    public void ResolveSkuReadPath_ConfigDirCopy_PreferredOverModuleDir()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");
        var configSkus = MakeFile(configDir, "skus.json", "[]");
        var moduleDir = MakeDir("module");
        MakeFile(moduleDir, "skus.json", "[]");

        var result = ConfigLocator.ResolveSkuReadPath(configPath, moduleDir);

        Assert.Equal(configSkus, result);
    }

    [Fact]
    public void ResolveSkuReadPath_ModuleDirFallback_WhenConfigDirHasNone()
    {
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");
        var moduleDir = MakeDir("module");
        var moduleSkus = MakeFile(moduleDir, "skus.json", "[]");

        var result = ConfigLocator.ResolveSkuReadPath(configPath, moduleDir);

        Assert.Equal(moduleSkus, result);
    }

    [Fact]
    public void ResolveSkuReadPath_NeitherExists_ReturnsConfigDirPathWithoutThrowing()
    {
        // SkuCatalog.Resolve falls back to EmbeddedDefaults for a missing file,
        // so a non-existent path is valid here — it must not throw.
        var configDir = MakeDir("cfg");
        var configPath = MakeFile(configDir, "glic.json");

        var result = ConfigLocator.ResolveSkuReadPath(configPath, MakeDir("module"));

        Assert.Equal(Path.Combine(configDir, "skus.json"), result);
    }

}

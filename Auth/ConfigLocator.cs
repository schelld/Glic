// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

namespace GLic.Auth;

/// <summary>
/// Owns all runtime file-path resolution so cmdlets never assume files
/// live in the module directory (which is read-only and version-suffixed
/// under Install-Module).
/// </summary>
public static class ConfigLocator
{
    public const string ConfigFileName = "glic.json";
    public const string CredentialFileName = "service-account.json";
    public const string SkuFileName = "skus.json";

    internal static string ModuleDir =>
        Path.GetDirectoryName(typeof(ConfigLocator).Assembly.Location) ?? AppContext.BaseDirectory;

    /// <summary>Resolution order: -Config param → GLIC_CONFIG → %ProgramData%\GLic → %APPDATA%\GLic → module dir.</summary>
    public static string ResolveConfigPath(string? explicitPath) =>
        ResolveConfigPath(
            explicitPath,
            Environment.GetEnvironmentVariable("GLIC_CONFIG"),
            DefaultConfigDirs());

    internal static string ResolveConfigPath(string? explicitPath, string? envPath, IReadOnlyList<string> probeDirs)
    {
        // Explicit and env paths are returned verbatim — if they point at a
        // missing file, GlicConfig.Load reports exactly which file is missing.
        if (!string.IsNullOrWhiteSpace(explicitPath)) return explicitPath!;
        if (!string.IsNullOrWhiteSpace(envPath)) return envPath!;

        var candidates = probeDirs.Select(d => Path.Combine(d, ConfigFileName)).ToList();
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                "glic.json not found. Searched:"
                + string.Concat(candidates.Select(c => $"\n  {c}"))
                + "\nPass -Config <path>, set the GLIC_CONFIG environment variable, "
                + "or create glic.json in one of the locations above.");
    }

    internal static IReadOnlyList<string> DefaultConfigDirs() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GLic"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GLic"),
        ModuleDir,
    ];

    /// <summary>Resolution order: -ServiceAccountPath param → credential_path in glic.json (relative paths
    /// resolve against the config file's directory) → sibling of glic.json → module dir.</summary>
    public static string ResolveCredentialPath(string? explicitPath, string? configuredPath, string configPath) =>
        ResolveCredentialPath(explicitPath, configuredPath, configPath, ModuleDir);

    internal static string ResolveCredentialPath(
        string? explicitPath, string? configuredPath, string configPath, string moduleDir)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath)) return explicitPath!;

        var configDir = ConfigDir(configPath);
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.IsPathRooted(configuredPath)
                ? configuredPath!
                : Path.Combine(configDir, configuredPath!);

        var candidates = new[]
        {
            Path.Combine(configDir, CredentialFileName),
            Path.Combine(moduleDir, CredentialFileName),
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                "service-account.json not found. Searched:"
                + string.Concat(candidates.Select(c => $"\n  {c}"))
                + "\nPass -ServiceAccountPath <path> or set \"credential_path\" in glic.json.");
    }

    /// <summary>skus.json is always written next to the resolved glic.json — never into the
    /// module directory, which is read-only for non-admins and replaced on Update-Module.</summary>
    public static string ResolveSkuWritePath(string configPath) =>
        Path.Combine(ConfigDir(configPath), SkuFileName);

    /// <summary>Read order: config dir → bundled module copy → (missing path; SkuCatalog
    /// then falls back to EmbeddedDefaults).</summary>
    public static string ResolveSkuReadPath(string configPath) => ResolveSkuReadPath(configPath, ModuleDir);

    internal static string ResolveSkuReadPath(string configPath, string moduleDir)
    {
        var configCandidate = Path.Combine(ConfigDir(configPath), SkuFileName);
        if (File.Exists(configCandidate)) return configCandidate;

        var moduleCandidate = Path.Combine(moduleDir, SkuFileName);
        return File.Exists(moduleCandidate) ? moduleCandidate : configCandidate;
    }

    // configPath is expected to be absolute — the cmdlet layer normalizes user input
    // before calling in; a relative path here would resolve against the process CWD.
    private static string ConfigDir(string configPath) =>
        Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";
}

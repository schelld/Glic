// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using GLic.Auth;

namespace GLic.Cmdlets;

public abstract class GlicCmdletBase : PSCmdlet, IDisposable
{
    internal static string ModuleDir =>
        Path.GetDirectoryName(typeof(GlicCmdletBase).Assembly.Location) ?? AppContext.BaseDirectory;

    /// <summary>Explicit config path. When omitted, ConfigLocator probes
    /// GLIC_CONFIG → %ProgramData%\GLic → %APPDATA%\GLic → module dir.</summary>
    [Parameter] public string? Config { get; set; }

    /// <summary>Explicit service-account key path. When omitted, resolved via
    /// credential_path in glic.json, then siblings of glic.json, then module dir.</summary>
    [Parameter] public string? ServiceAccountPath { get; set; }

    private string? _resolvedConfigPath;
    protected string ResolvedConfigPath =>
        _resolvedConfigPath ??= ConfigLocator.ResolveConfigPath(NormalizePath(Config));

    protected GlicConfig LoadConfig()
    {
        if (GlicSession.IsConnected && Config == null)
            return GlicSession.Config!;
        return GlicConfig.Load(ResolvedConfigPath);
    }

    protected Task<ApiClients> BuildClientsAsync(GlicConfig cfg)
    {
        if (GlicSession.IsConnected && Config == null && ServiceAccountPath == null)
            return Task.FromResult(GlicSession.Clients!);
        return ChromeServiceFactory.BuildAsync(
            cfg.AdminEmail,
            ConfigLocator.ResolveCredentialPath(
                NormalizePath(ServiceAccountPath), cfg.CredentialPath, ResolvedConfigPath));
    }

    // Relative user input must resolve against the PowerShell location, not the
    // process CWD — they routinely differ in PS 5.1. Requires the pipeline thread;
    // cmdlets call LoadConfig/BuildClientsAsync at the top of RunAsync, before any await.
    private string? NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : GetUnresolvedProviderPathFromPSPath(path);

    protected CancellationTokenSource Cts { get; } = new CancellationTokenSource();

    private readonly List<object> _outputBuffer = new List<object>();
    private readonly List<(bool IsWarning, string Text)> _messageBuffer = new List<(bool, string)>();

    internal IReadOnlyList<object> PendingOutput => _outputBuffer;
    internal IReadOnlyList<(bool IsWarning, string Text)> PendingMessages => _messageBuffer;

    protected new void WriteVerbose(string text) => _messageBuffer.Add((false, text));
    protected new void WriteWarning(string text) => _messageBuffer.Add((true, text));
    protected void BufferObject(object obj) => _outputBuffer.Add(obj);

    protected override void StopProcessing() => Cts.Cancel();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) Cts.Dispose();
    }

    protected abstract Task RunAsync(CancellationToken ct);

    protected override void ProcessRecord()
    {
        try
        {
            if (Config == null && ServiceAccountPath == null)
                TryAutoConnectAsync(Cts.Token).GetAwaiter().GetResult();
            RunAsync(Cts.Token).GetAwaiter().GetResult();
        }
        catch (FileNotFoundException ex)
        {
            WriteError(new ErrorRecord(ex, "ConfigNotFound", ErrorCategory.ObjectNotFound, this));
            return;
        }
        catch (InvalidOperationException ex)
        {
            WriteError(new ErrorRecord(ex, "ConfigError", ErrorCategory.InvalidData, this));
            return;
        }
        catch (Google.GoogleApiException ex)
        {
            WriteError(new ErrorRecord(ex, "ApiError", ErrorCategory.ConnectionError, this));
            return;
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "UnknownError", ErrorCategory.NotSpecified, this));
            return;
        }

        // Now on the pipeline thread — safe to call Write* methods.
        foreach (var (isWarning, text) in _messageBuffer)
            if (isWarning) base.WriteWarning(text);
            else base.WriteVerbose(text);
        foreach (var obj in _outputBuffer)
            WriteObject(obj);
    }

    protected async Task EmitRowsAsync<T>(IAsyncEnumerable<T> rows, CancellationToken ct)
    {
        await foreach (var row in rows.WithCancellation(ct))
            _outputBuffer.Add(row!);
    }

    // internal for testability (InternalsVisibleTo GLic.Tests)
    internal async Task TryAutoConnectAsync(
        CancellationToken ct, IReadOnlyList<string>? probeDirs = null)
    {
        if (GlicSession.IsConnected) return;

        probeDirs ??= ConfigLocator.DefaultConfigDirs();

        foreach (var dir in probeDirs)
        {
            var configPath = Path.Combine(dir, ConfigLocator.ConfigFileName);
            if (!File.Exists(configPath)) continue;

            var cfg = GlicConfig.Load(configPath);

            // Prefer DPAPI blob (written by Connect-Glic)
            var dpapiPath = ConfigLocator.ResolveDpapiPath(dir);
            if (File.Exists(dpapiPath))
            {
#if NET5_0_OR_GREATER
#pragma warning disable CA1416 // Validate platform compatibility — runs only on Windows in practice
#endif
                var json = DpapiStore.Unprotect(File.ReadAllBytes(dpapiPath));
#if NET5_0_OR_GREATER
#pragma warning restore CA1416
#endif
                var clients = await ChromeServiceFactory.BuildAsync(cfg.AdminEmail, json);
                GlicSession.Set(clients, cfg);
                return;
            }

            // Fall back to plain service-account.json (AllUsers / manual install)
            var credPath = Path.Combine(dir, ConfigLocator.CredentialFileName);
            if (File.Exists(credPath))
            {
                var clients = await ChromeServiceFactory.BuildAsync(cfg.AdminEmail, credPath);
                GlicSession.Set(clients, cfg);
                return;
            }
        }

        throw new InvalidOperationException(
            "No GLic session. Run Connect-Glic to authenticate.");
    }
}

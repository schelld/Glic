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

    protected GlicConfig LoadConfig() => GlicConfig.Load(ResolvedConfigPath);

    protected Task<ApiClients> BuildClientsAsync(GlicConfig cfg) =>
        ChromeServiceFactory.BuildAsync(
            cfg.AdminEmail,
            ConfigLocator.ResolveCredentialPath(NormalizePath(ServiceAccountPath), cfg.CredentialPath, ResolvedConfigPath));

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
}

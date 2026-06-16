// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLic.Auth;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommunications.Connect, "Glic")]
public sealed class ConnectGlicCmdlet : PSCmdlet
{
    [Parameter] public string? AdminEmail         { get; set; }
    [Parameter] public string? ServiceAccountPath { get; set; }
    [Parameter] public SwitchParameter Force      { get; set; }

    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    protected override void StopProcessing() => _cts.Cancel();

    protected override void ProcessRecord()
    {
        if (GlicSession.IsConnected && !Force.IsPresent) return;

        try
        {
            ConnectAsync(_cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ThrowTerminatingError(new ErrorRecord(ex, "ConnectGlicFailed", ErrorCategory.AuthenticationError, this));
        }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        // --- Collect admin email ---
        var email = AdminEmail?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            Host.UI.Write("Admin email (e.g. admin@domain.com): ");
            email = Host.UI.ReadLine()?.Trim();
        }
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Admin email is required.");

        // --- Collect service-account.json path ---
        var saPath = ServiceAccountPath?.Trim();
        while (string.IsNullOrWhiteSpace(saPath) || !File.Exists(saPath))
        {
            if (!string.IsNullOrWhiteSpace(saPath))
                Host.UI.WriteLine($"File not found: {saPath}");
            Host.UI.Write("Path to service-account.json: ");
            saPath = Host.UI.ReadLine()?.Trim();
        }

        // --- Validate JSON ---
        var saBytes = File.ReadAllBytes(saPath!);
        ValidateServiceAccountJson(saBytes, saPath!);

        // --- Authenticate and derive customer ID ---
        Host.UI.WriteLine("Connecting to Google Workspace...");
        var clients = await ChromeServiceFactory.BuildAsync(email!, saBytes);
        var customer = await clients.Directory.Customers.Get("my_customer").ExecuteAsync(ct);
        var customerId = customer.Id
            ?? throw new InvalidOperationException(
                "customers.get returned no customer ID. Verify admin_email has admin rights.");

        // --- Write glic.json ---
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GLic");
        WriteGlicJson(configDir, customerId, email!);

        // --- Write DPAPI blob ---
        var dpapiPath = ConfigLocator.ResolveDpapiPath(configDir);
#if NET5_0_OR_GREATER
#pragma warning disable CA1416
#endif
        File.WriteAllBytes(dpapiPath, DpapiStore.Protect(saBytes));
#if NET5_0_OR_GREATER
#pragma warning restore CA1416
#endif

        // --- Set session ---
        GlicSession.Set(clients, new GlicConfig(customerId, email!));

        Host.UI.WriteLine($"Connected: {email} ({customerId})");
    }

    internal static void WriteGlicJson(string configDir, string customerId, string adminEmail)
    {
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, ConfigLocator.ConfigFileName);
        var cfg = new GlicConfig(customerId, adminEmail);
        var writeOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        File.WriteAllText(configPath, JsonSerializer.Serialize(cfg, writeOptions), Encoding.UTF8);
    }

    /// <summary>Validates that <paramref name="json"/> is a service account key file.
    /// Internal so unit tests can call it directly.</summary>
    internal static void ValidateServiceAccountJson(byte[] json, string path)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("type", out var typeEl)
            || typeEl.GetString() != "service_account")
            throw new InvalidOperationException(
                $"\"{path}\" does not look like a service account key — "
                + "expected \"type\": \"service_account\". "
                + "Download a key from Google Cloud Console → IAM → Service Accounts → Keys.");
    }
}

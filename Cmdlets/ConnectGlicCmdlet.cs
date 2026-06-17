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
        // Try SecretStore vault first (must run before any await — requires the pipeline thread).
        var vaultKey = GlicCmdletBase.TryReadVaultSecret(this, "GlicServiceAccountKey");
        byte[]? vaultBytes = string.IsNullOrEmpty(vaultKey)
            ? null
            : System.Text.Encoding.UTF8.GetBytes(vaultKey!);

        var email = AdminEmail?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            Host.UI.Write("Admin email (e.g. admin@domain.com): ");
            email = Host.UI.ReadLine()?.Trim();
        }
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Admin email is required.");

        byte[] saBytes;
        string saSource;

        if (vaultBytes != null)
        {
            saBytes  = vaultBytes;
            saSource = "GlicVault";
        }
        else
        {
            var saPath = ServiceAccountPath?.Trim();
            while (string.IsNullOrWhiteSpace(saPath) || !File.Exists(saPath))
            {
                if (!string.IsNullOrWhiteSpace(saPath))
                    Host.UI.WriteLine($"File not found: {saPath}");
                Host.UI.Write("Path to service-account.json: ");
                saPath = Host.UI.ReadLine()?.Trim();
            }

            saBytes = File.ReadAllBytes(saPath!);
            // Strip UTF-8 BOM that some editors/tools prepend; JsonDocument rejects 0xEF at position 0.
            if (saBytes.Length >= 3 && saBytes[0] == 0xEF && saBytes[1] == 0xBB && saBytes[2] == 0xBF)
            {
                var stripped = new byte[saBytes.Length - 3];
                Array.Copy(saBytes, 3, stripped, 0, stripped.Length);
                saBytes = stripped;
            }
            saSource = saPath!;
        }

        ValidateServiceAccountJson(saBytes, saSource);

        Host.UI.WriteLine("Connecting to Google Workspace...");
        var clients = await ChromeServiceFactory.BuildAsync(email!, saBytes);
        var customer = await clients.Directory.Customers.Get("my_customer").ExecuteAsync(ct);
        var customerId = customer.Id
            ?? throw new InvalidOperationException(
                "customers.get returned no customer ID. Verify admin_email has admin rights.");

        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GLic");
        WriteGlicJson(configDir, customerId, email!);

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

    // internal for testability (InternalsVisibleTo GLic.Tests)
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

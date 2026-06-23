// SPDX-License-Identifier: MIT
// Copyright (c) 2026 D. Schell <schelld@live.com>

using System.Management.Automation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLic.Auth;

namespace GLic.Cmdlets;

[Cmdlet(VerbsCommunications.Connect, "Glic")]
public sealed class ConnectGlicCmdlet : PSCmdlet, IDisposable
{
    [Parameter] public string? AdminEmail         { get; set; }
    [Parameter] public string? ServiceAccountPath { get; set; }

    /// <summary>Path to a service-account JSON file. When provided, the key is stored in GlicVault
    /// (SecretStore) so subsequent sessions reconnect silently without this file.</summary>
    [Parameter] public string? KeyPath { get; set; }

    [Parameter] public SwitchParameter Force { get; set; }

    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    protected override void StopProcessing() => _cts.Cancel();
    public void Dispose() => _cts.Dispose();

    protected override void ProcessRecord()
    {
        // Skip if already connected unless -Force is set or new credentials are being stored.
        if (GlicSession.IsConnected && !Force.IsPresent && string.IsNullOrWhiteSpace(KeyPath)) return;

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
        // All InvokeCommand / pipeline-thread calls must happen before any await.

        // If -KeyPath is supplied, validate and store to vault before reading from it.
        if (!string.IsNullOrWhiteSpace(KeyPath))
        {
            var resolved  = ResolvePath(KeyPath!);
            var keyBytes  = ReadAndStripBom(resolved);
            ValidateServiceAccountJson(keyBytes, resolved);
            var rawJson     = Encoding.UTF8.GetString(keyBytes);
            var clientEmail = ExtractClientEmail(keyBytes);
            StoreInVault(rawJson, clientEmail);
            Host.UI.WriteLine($"Credential stored in GlicVault ({clientEmail}).");
        }

        // Try SecretStore vault (must run before any await — requires the pipeline thread).
        var vaultKey  = GlicCmdletBase.TryReadVaultSecret(this, "GlicServiceAccountKey");
        byte[]? vaultBytes = string.IsNullOrEmpty(vaultKey)
            ? null
            : Encoding.UTF8.GetBytes(vaultKey!);

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
                if (saPath == null)
                    throw new InvalidOperationException(
                        "Cannot prompt for service-account.json in a non-interactive session. Use -ServiceAccountPath.");
            }

            saBytes = ReadAndStripBom(saPath!);
            ValidateServiceAccountJson(saBytes, saPath!);
            saSource = saPath!;
        }

        Host.UI.WriteLine("Connecting to Google Workspace...");
        var clients  = await ChromeServiceFactory.BuildAsync(email!, saBytes);
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

    private string ResolvePath(string path) =>
        GetUnresolvedProviderPathFromPSPath(path.Trim());

    private static byte[] ReadAndStripBom(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            var stripped = new byte[bytes.Length - 3];
            Array.Copy(bytes, 3, stripped, 0, stripped.Length);
            return stripped;
        }
        return bytes;
    }

    private static string ExtractClientEmail(byte[] json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("client_email", out var el)
            ? el.GetString() ?? ""
            : "";
    }

    private void StoreInVault(string rawJson, string clientEmail)
    {
        foreach (var mod in new[] { "Microsoft.PowerShell.SecretManagement", "Microsoft.PowerShell.SecretStore" })
        {
            var found = InvokeCommand.InvokeScript(
                $"Get-Module -Name '{mod}' -ListAvailable -ErrorAction SilentlyContinue");
            if (found == null || found.Count == 0)
                throw new InvalidOperationException(
                    $"Required module '{mod}' is not installed. Run: Install-Module {mod} -Scope CurrentUser");
        }

        var vaultExists = InvokeCommand.InvokeScript(
            "Get-SecretVault -Name 'GlicVault' -ErrorAction SilentlyContinue");
        if (vaultExists == null || vaultExists.Count == 0)
        {
            Host.UI.WriteLine("Configuring GlicVault (passwordless, current user)...");
            InvokeCommand.InvokeScript(
                "Set-SecretStoreConfiguration -Scope CurrentUser -Authentication None -Interaction None -Confirm:$false -ErrorAction Stop");
            InvokeCommand.InvokeScript(
                "Register-SecretVault -Name 'GlicVault' -ModuleName 'Microsoft.PowerShell.SecretStore' -ErrorAction Stop");
        }

        // Pass values via $args[0] to avoid JSON escaping issues.
        InvokeCommand.InvokeScript(
            "Set-Secret -Name 'GlicServiceAccountKey' -Secret $args[0] -Vault 'GlicVault' -ErrorAction Stop",
            rawJson);
        InvokeCommand.InvokeScript(
            "Set-Secret -Name 'GlicServiceAccountEmail' -Secret $args[0] -Vault 'GlicVault' -ErrorAction Stop",
            clientEmail);
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

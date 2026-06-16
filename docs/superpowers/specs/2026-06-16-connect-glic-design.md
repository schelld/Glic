# Connect-Glic Design

**Date:** 2026-06-16
**Status:** Approved

## Summary

Add a `Connect-Glic` cmdlet that handles all credential setup interactively: prompts for admin email and service-account.json path, derives the Google customer ID via the Directory API, encrypts the key with Windows DPAPI, and stores everything in the user's config dir. All ten existing cmdlets gain silent auto-connect with no changes to their own files. `install.ps1` and `uninstall.ps1` are removed — module installation is native (`Install-Module` / manual folder copy) and `Connect-Glic` completes first-time setup.

---

## Session Model

A new static class `GlicSession` holds live state for the current PowerShell process:

```csharp
internal static class GlicSession
{
    static ApiClients? Clients
    static GlicConfig?  Config
    static bool         IsConnected
    static void Set(ApiClients clients, GlicConfig config)
    static void Clear()
}
```

`GlicCmdletBase.BuildClientsAsync()` is replaced by a new `EnsureSessionAsync(ct)` with this decision tree:

1. `GlicSession.IsConnected` → return immediately (zero I/O)
2. `service-account.dpapi` + `glic.json` found in ConfigLocator probe dirs → `DpapiStore.Unprotect()` → build `ApiClients` → `GlicSession.Set()`
3. `service-account.json` + `glic.json` found (legacy / AllUsers path) → existing `ChromeServiceFactory.BuildAsync()` → `GlicSession.Set()`
4. Nothing found → throw `"No GLic session. Run Connect-Glic to authenticate."`

Existing cmdlets gain auto-connect via this change to the base class only — no changes to any of the ten cmdlet files.

---

## Storage

`Connect-Glic` writes two files to `%APPDATA%\GLic` (the same directory `ConfigLocator` already probes):

| File | Contents | Protection |
|---|---|---|
| `glic.json` | `{ "admin_email": "...", "customer_id": "..." }` | Inherits `%APPDATA%` per-user ACL |
| `service-account.dpapi` | DPAPI-encrypted bytes of service-account.json | `DataProtectionScope.CurrentUser` — only decryptable by same Windows user on same machine |

The existing `service-account.json` path (AllUsers installs via manual copy) remains supported as step 3 in the session decision tree above. The two storage options coexist.

---

## `Connect-Glic` Cmdlet

**Parameters:**

| Parameter | Type | Required | Notes |
|---|---|---|---|
| `-AdminEmail` | `string` | No | Prompted if omitted |
| `-ServiceAccountPath` | `string` | No | Prompted if omitted |
| `-Force` | `switch` | No | Re-authenticate even if session already active |

**Flow:**

1. If `GlicSession.IsConnected` and `-Force` not set → return silently (no-op)
2. Prompt for any missing parameters
3. Validate `ServiceAccountPath` is a readable file containing `"type": "service_account"`
4. Build `DirectoryService` with DWD for `AdminEmail` → call `customers.get("my_customer")` → extract `customerId`
5. Write `glic.json` (`admin_email` + `customer_id`) to `%APPDATA%\GLic`
6. DPAPI-encrypt service-account JSON bytes → write `service-account.dpapi` to `%APPDATA%\GLic`
7. Build full `ApiClients` → `GlicSession.Set()`
8. `Write-Host "Connected: admin@domain.com (C03xxxxx)"` (host only, not pipeline)

**Output:** Nothing on the pipeline. Errors are terminating with actionable messages.

---

## New Files

| File | Purpose |
|---|---|
| `Auth/GlicSession.cs` | Static session holder |
| `Auth/DpapiStore.cs` | Thin wrapper: `Protect(byte[])` / `Unprotect(byte[])` |
| `Cmdlets/ConnectGlicCmdlet.cs` | The new cmdlet |

## Modified Files

| File | Change |
|---|---|
| `Auth/ConfigLocator.cs` | Add `DpapiCredentialFileName` constant and `ResolveDpapiPath()` |
| `Auth/ChromeServiceFactory.cs` | New overload accepting `byte[]` (raw JSON) instead of file path — used by the DPAPI auto-load path so decrypted bytes never touch disk |
| `Cmdlets/GlicCmdletBase.cs` | Replace `BuildClientsAsync` with `EnsureSessionAsync` decision tree |
| `GLic.psd1` | Add `Connect-Glic` to `CmdletsToExport` |

## Removed Files

| File | Reason |
|---|---|
| `module/GLic/install.ps1` | All credential setup moved to `Connect-Glic`; module install is native |
| `module/GLic/uninstall.ps1` | Native `Uninstall-Module` handles removal |

---

## Usage After This Change

```powershell
# First time (or new machine)
Install-Module GLic          # or: copy folder to $env:PSModulePath
Connect-Glic                 # prompts for admin email + service-account.json path

# Every subsequent session — auto-connects silently
Get-GlicDevices -Status active
Get-GlicLicenses | Where-Object Suspended -eq 'True'

# Re-authenticate (new key, new admin account)
Connect-Glic -Force

# Scriptable / silent
Connect-Glic -AdminEmail admin@domain.com -ServiceAccountPath C:\keys\sa.json
```

---

## Testing

- `DpapiStore`: round-trip test (protect → unprotect → bytes match)
- `GlicSession`: Set/Clear/IsConnected state transitions
- `ConnectGlicCmdlet`: validate error on bad JSON, validate customer ID written to glic.json
- `GlicCmdletBase.EnsureSessionAsync`: unit test each of the four decision-tree branches with synthetic data

# GLic — Google Workspace License and Device Inventory

Native PowerShell binary module that queries Google Workspace APIs (Chrome Management, Licensing, Admin Directory) and emits typed objects to the pipeline for ingestion into Enterprise IT Asset Management systems. This repository is not an official Google product.

## Installation

### PowerShell Gallery Not Yet Implemented

### Manual (ZIP or network share)

Copy the `GLic` folder to any location in your `$env:PSModulePath`, then run `Connect-Glic` once to complete setup:

```powershell
# First-time setup — prompts for admin email and service-account.json path
Import-Module GLic
Connect-Glic

# Scriptable / silent
Connect-Glic -AdminEmail admin@yourdomain.com -ServiceAccountPath C:\keys\sa.json
```

`Connect-Glic` calls the Directory API to derive the customer ID, encrypts the service-account key with Windows DPAPI, and writes both `glic.json` and `service-account.dpapi` to `%APPDATA%\GLic\`. Subsequent PowerShell sessions auto-connect silently — no further configuration is needed.

To update credentials (new key or new admin account), run `Connect-Glic -Force`.

### Uninstall

Remove the module folder from `$env:PSModulePath` and optionally delete `%APPDATA%\GLic\` to remove stored credentials.

## Quick Start

```powershell
Import-Module GLic
Connect-Glic                                            # one-time setup
Get-GlicDevices | Export-Csv devices.csv -NoTypeInformation
Get-GlicUsers | Where-Object { $_.OrgUnitPath -like '/Staff/*' }
Get-GlicHardware | ConvertTo-Json | Out-File hardware.json
```

## Getting Started (build from source)

1. **Build** — run `dotnet build` in the repo root. This compiles the project and stages the module under `module\GLic\`.

2. **Create a service account** — in Google Cloud Console, create a service account and download its JSON key.

3. **Grant Domain-Wide Delegation** — in Google Admin Console → Security → API Controls → Domain-wide Delegation, add the service account's client ID with the [scopes listed in Requirements](#requirements).

4. **Connect and run** —
   ```powershell
   Import-Module .\module\GLic
   Connect-Glic             # prompts for admin email + path to service-account.json key
   Invoke-GlicDiscover      # populates skus.json; run once before Get-GlicLicenses
   Get-GlicDevices          # verify the setup works
   ```

## Requirements

**.NET build:** `dotnet build` stages `module\GLic\` automatically via the MSBuild post-build target.

**Credential files** are written to `%APPDATA%\GLic\` by `Connect-Glic` and resolved at runtime as described in [Configuration resolution](#configuration-resolution):

| File | Purpose | Created by |
|---|---|---|
| `service-account.dpapi` | DPAPI-encrypted service-account key | `Connect-Glic` |
| `glic.json` | Workspace config: `customer_id` and `admin_email` | `Connect-Glic` |
| `skus.json` | License SKU list — bundled defaults; refresh with `Invoke-GlicDiscover` | `Invoke-GlicDiscover` |

A plain `service-account.json` is also accepted for legacy or AllUsers installs (see [Configuration resolution](#configuration-resolution)).

The service account must have **Domain-Wide Delegation** granted in Google Admin Console → Security → API Controls → Domain-wide Delegation with these scopes:

| Scope | Used by |
|---|---|
| `https://www.googleapis.com/auth/chrome.management.reports.readonly` | `Get-GlicApps`, `Get-GlicDeviceApps`, `Get-GlicBrowserExtensions` |
| `https://www.googleapis.com/auth/chrome.management.telemetry.readonly` | `Get-GlicHardware`, `Get-GlicTelemetry` |
| `https://www.googleapis.com/auth/chrome.management.profiles.readonly` | `Get-GlicManagedBrowsers` |
| `https://www.googleapis.com/auth/admin.directory.device.chromeos.readonly` | `Get-GlicDevices`, `Get-GlicHardware`, `Get-GlicTelemetry` |
| `https://www.googleapis.com/auth/admin.directory.user.readonly` | `Get-GlicUsers`, `Get-GlicLicenses` |
| `https://www.googleapis.com/auth/admin.directory.orgunit.readonly` | `Get-GlicManagedBrowsers`, `Get-GlicDeviceApps`, `Get-GlicBrowserExtensions` (OrgUnit path resolution) |
| `https://www.googleapis.com/auth/apps.licensing` | `Get-GlicLicenses`, `Invoke-GlicDiscover` |

## Configuration resolution

`glic.json` is located by trying, in order:

1. `-Config <path>` parameter
2. `GLIC_CONFIG` environment variable
3. `%ProgramData%\GLic\glic.json` (machine-wide)
4. `%APPDATA%\GLic\glic.json` (per-user)
5. The module directory (xcopy/dev layouts)

The service-account credential is located by trying, in order:

1. `-ServiceAccountPath <path>` parameter
2. `service-account.dpapi` next to the resolved `glic.json` (DPAPI-encrypted, written by `Connect-Glic`)
3. `credential_path` key in `glic.json` (relative paths resolve against the `glic.json` directory)
4. `service-account.json` next to the resolved `glic.json`
5. The module directory

`skus.json` is read from the `glic.json` directory first, then the bundled module copy, then
built-in defaults. `Invoke-GlicDiscover` always writes it next to `glic.json` — never into the
module directory, so refreshes work without admin rights and survive module updates.

## Build and Import

```powershell
dotnet build                     # compiles + stages module\GLic\
Import-Module .\module\GLic      # loads all 11 cmdlets
Connect-Glic                     # first-time credential setup
Get-Command -Module GLic         # verify
```

## Cmdlet Reference

All data cmdlets share two base parameters:

| Parameter | Default | Description |
|---|---|---|
| `-Config` | auto-resolved (see [Configuration resolution](#configuration-resolution)) | Path to `glic.json` |
| `-ServiceAccountPath` | auto-resolved | Path to the service-account key file |

---

### `Connect-Glic`

Authenticates to Google Workspace and stores credentials for the current user. Must be run once before any `Get-Glic*` cmdlet. All subsequent cmdlets auto-connect silently from stored credentials.

```powershell
Connect-Glic                                          # interactive — prompts for email + key path
Connect-Glic -AdminEmail admin@domain.com `
             -ServiceAccountPath C:\keys\sa.json     # scriptable
Connect-Glic -Force                                   # re-authenticate (new key or admin account)
```

| Parameter | Type | Description |
|---|---|---|
| `-AdminEmail` | string | Google Workspace admin email — prompted if omitted |
| `-ServiceAccountPath` | string | Path to `service-account.json` — prompted if omitted |
| `-Force` | switch | Re-authenticate even if a session is already active |

**Output:** Nothing on the pipeline. Writes a confirmation line to the host.

---

### `Get-GlicApps`

Fleet-wide Chrome OS app inventory — one aggregate row per app across all enrolled devices.

Uses `countInstalledApps`, which returns device-count totals per app rather than a per-device breakdown. `BrowserDeviceCount` tells you how many devices have each app installed. For the per-device breakdown (which specific device has which app), use `Get-GlicDeviceApps`.

Use this when you need to answer: *Which apps are installed across the fleet? Which are most widespread? What mix of app types (extensions, Android apps, hosted apps) is running?*

```powershell
Get-GlicApps
Get-GlicApps | Export-Csv apps.csv -NoTypeInformation

# Most-deployed apps by device count
Get-GlicApps | Sort-Object BrowserDeviceCount -Descending | Select-Object -First 20

# Installed app count by type (EXTENSION, APP, ANDROID_APP, HOSTED_APP, THEME)
Get-GlicApps | Group-Object AppType | Select-Object Name, Count
```

**Output:** `AppRow` — `ReportDate`, `CustomerId`, `DisplayName`, `AppId`, `AppType`, `Publisher`, `BrowserDeviceCount`

---

### `Get-GlicDevices`

Chrome OS device inventory — 23-column enrollment and status record per device.

Uses the Directory API's full projection to capture identity, assignment, network, and lifecycle fields in a single lightweight pull. This is the authoritative source for device identity and enrollment state. For hardware specs (CPU/RAM/disk) or OS patch compliance, use `Get-GlicHardware` or `Get-GlicTelemetry`.

Use this when you need to answer: *What devices are enrolled? Which are active, disabled, or deprovisioned? When did a device last sync? What is assigned to a particular user or location?*

```powershell
Get-GlicDevices                          # active devices (default)
Get-GlicDevices -Status all              # all statuses
Get-GlicDevices -Status deprovisioned

# Devices that haven't synced in 90 days
Get-GlicDevices | Where-Object { $_.LastSync -lt (Get-Date).AddDays(-90) }

# Devices with no assigned user (unallocated inventory)
Get-GlicDevices | Where-Object { $_.AnnotatedUser -eq '' }
```

| Parameter | Default | Values |
|---|---|---|
| `-Status` | `active` | `all`, `active`, `deprovisioned`, `disabled` |

**Output:** `DeviceRow` — `DeviceId`, `SerialNumber`, `Model`, `Status`, `OrgUnitPath`, `AnnotatedUser`, `OsVersion`, `MacAddress`, `EthernetMacAddress`, `AnnotatedAssetId`, `OrderNumber`, `LastSync`, `EnrollmentTime`, and more.

---

### `Get-GlicTelemetry`

OS patch compliance snapshot — purpose-built for security audits. Requests only OS update fields from the Chrome Management API (lightweight payload), making it suitable for frequent compliance checks and SIEM feeds.

Use this when you need to answer: *Which devices are past EOL? Which are unpatched or haven't rebooted?*

```powershell
Get-GlicTelemetry
Get-GlicTelemetry -Status all -OrgUnit '/Schools/East'

# Devices past auto-update expiration (EOL — no more security patches)
Get-GlicTelemetry | Where-Object { $_.AutoUpdateExpiration -lt (Get-Date) }

# Devices not up to date
Get-GlicTelemetry | Where-Object { $_.UpdateState -ne 'OS_UP_TO_DATE' }

# Devices that haven't rebooted in 30+ days (pending updates not applied)
Get-GlicTelemetry | Where-Object { $_.LastRebootTime -lt (Get-Date).AddDays(-30) }
```

| Parameter | Default | Values |
|---|---|---|
| `-Status` | `active` | `all`, `active`, `deprovisioned`, `disabled` |
| `-OrgUnit` | *(all)* | OU path string |

**Output:** `TelemetryRow` — `DeviceId`, `SerialNumber`, `Status`, `OrgUnitPath`, `AnnotatedUser`, `OsVersion`, `PlatformVersion`, `FirmwareVersion`, `AutoUpdateExpiration`, `UpdateState`, `LastUpdateCheckTime`, `LastUpdateTime`, `LastRebootTime`, `NewPlatformVersion`

---

### `Get-GlicHardware`

Full ITAM hardware CI record — joins Directory enrollment data with the full Chrome Management telemetry payload (CPU, RAM, disk, battery, GPU, network). Use this for asset management, procurement planning, and lifecycle tracking.

Use this when you need to answer: *What hardware do we own? What are the specs? When does each device EOL?*

```powershell
Get-GlicHardware                                      # all statuses
Get-GlicHardware -Status active -OrgUnit '/Staff'

# Devices nearing EOL in the next 12 months
Get-GlicHardware | Where-Object { $_.AutoUpdateExpiration -lt (Get-Date).AddYears(1) }

# Join with licenses to see if licensed users have hardware
$hw  = Get-GlicHardware
$lic = Get-GlicLicenses
$hw | Where-Object { $lic.UserEmail -contains $_.AnnotatedUser }
```

| Parameter | Default | Values |
|---|---|---|
| `-Status` | `all` | `all`, `active`, `deprovisioned`, `disabled` |
| `-OrgUnit` | *(all)* | OU path string |

**Output:** `HardwareRow` — `DeviceId`, `SerialNumber`, `Model`, `OrgUnitPath`, `AnnotatedUser`, `AnnotatedAssetId`, `EnrollmentTime`, `AutoUpdateExpiration`, `CpuModel`, `CpuArchitecture`, `RamTotalBytes`, `TotalDiskBytes`, `DiskModels`, `DiskHealths`, `BatteryHealth`, `BatteryCycleCount`, `GpuAdapter`, `NetworkMacAddresses`, `OsUpdateState`, `OsLastRebootTime`, and more (43 columns total).

---

### `Get-GlicLicenses`

Google Workspace license assignments with user details — one row per user per SKU.

Joins the Licensing API with the user directory to add display name, OU, and suspension state to each assignment. Requires `skus.json` to be populated — run `Invoke-GlicDiscover` first. Domain-wide (non-per-user) licenses cannot be reported via this API; a warning is emitted for each.

Use this when you need to answer: *Which users have licenses? How many are assigned per SKU? Are suspended users still consuming licenses? Who hasn't logged in recently?*

```powershell
Get-GlicLicenses                                     # all SKUs in skus.json
Get-GlicLicenses -SkuIds '1010310003','1010310010'  # specific SKUs only

# License count by SKU name
Get-GlicLicenses | Group-Object SkuName | Select-Object Name, Count

# Suspended users still holding licenses (license reclamation candidates)
Get-GlicLicenses | Where-Object { $_.Suspended -eq $true }

# Licensed users who haven't logged in for 90 days
Get-GlicLicenses | Where-Object { $_.LastLoginTime -lt (Get-Date).AddDays(-90) }
```

| Parameter | Default | Description |
|---|---|---|
| `-SkuIds` | *(from skus.json)* | Array of SKU IDs to query |

**Output:** `LicenseRow` — `UserEmail`, `FullName`, `OrgUnit`, `IsAdmin`, `Suspended`, `LastLoginTime`, `ProductId`, `ProductName`, `SkuId`, `SkuName`, `AssignmentStatus`, and more.

> SKU IDs are managed in `skus.json`. Run `Invoke-GlicDiscover` to populate it.

---

### `Get-GlicUsers`

Google Workspace user directory export — 23-column record per account.

Uses the Directory API's full user projection, capturing identity, org structure, 2SV enrollment, and suspension state in a single pull. Default is active users only — set `-Suspended All` to include suspended accounts.

Use this when you need to answer: *Who has accounts? Which users are suspended or archived? Which accounts haven't logged in recently? Who isn't enrolled in 2-step verification?*

```powershell
Get-GlicUsers                              # active users (default)
Get-GlicUsers -Suspended All              # active + suspended
Get-GlicUsers -Suspended Suspended        # suspended only
Get-GlicUsers -OrgUnit '/Staff'

# Users not enrolled in 2-step verification
Get-GlicUsers | Where-Object { $_.IsEnrolledIn2Sv -ne $true }

# Accounts that haven't logged in for 180 days (stale account review)
Get-GlicUsers | Where-Object { $_.LastLoginTime -lt (Get-Date).AddDays(-180) }
```

| Parameter | Default | Values |
|---|---|---|
| `-Suspended` | `Active` | `All`, `Active`, `Suspended` |
| `-OrgUnit` | *(all)* | OU path string |

> Default is `Active` — differs from the old CLI which defaulted to `All`.

**Output:** `UserRow` — `PrimaryEmail`, `FullName`, `OrgUnit`, `IsAdmin`, `IsDelegatedAdmin`, `IsEnrolledIn2Sv`, `Suspended`, `Archived`, `LastLoginTime`, `Department`, `JobTitle`, `ManagerEmail`, `Aliases`, and more.

---

### `Get-GlicManagedBrowsers`

CBCM-enrolled Chrome browser inventory — one row per enrolled browser profile.

Queries the Chrome Management Profiles API. Each row is a single profile, not a machine — one physical device can appear multiple times if multiple profiles are enrolled. `LastActivityTime` reflects the most recent user activity; `LastPolicySyncTime` reflects policy freshness.

Use this when you need to answer: *What browser versions are deployed on managed Windows and Mac machines? Which profiles are on an outdated version? Who hasn't received a policy update recently?*

```powershell
Get-GlicManagedBrowsers
Get-GlicManagedBrowsers -OrgUnit '/IT/Managed'

# Version distribution across managed profiles
Get-GlicManagedBrowsers | Group-Object BrowserVersion | Sort-Object Count -Descending | Select-Object -First 10

# Profiles with stale policy sync (30+ days)
Get-GlicManagedBrowsers | Where-Object { $_.LastPolicySyncTime -lt (Get-Date).AddDays(-30) }
```

| Parameter | Default | Description |
|---|---|---|
| `-OrgUnit` | *(all)* | OU path string; falls back to all profiles if filter unsupported |

**Output:** `ManagedBrowserRow` — `ProfilePermanentId`, `ProfileId`, `UserEmail`, `BrowserVersion`, `BrowserChannel`, `OsPlatformType`, `OsVersion`, `Hostname`, `Machine`, `ExtensionCount`, `LastActivityTime`, `LastPolicySyncTime`, and more.

---

### `Get-GlicDeviceApps`

Per-device app inventory — one row per app per enrolled Chrome OS device.

A nested query: first fetches all apps via `countInstalledApps`, then calls `findInstalledAppDevices` for each one to list which devices have it. This produces a high row-count result and is slower than `Get-GlicApps` on large fleets — scope with `-OrgUnit` when possible. Use `Get-GlicApps` for fleet-wide aggregate counts.

Use this when you need to answer: *Which devices have a specific app installed? What apps are on a particular device? Are any policy-prohibited apps present on school or lab machines?*

```powershell
Get-GlicDeviceApps
Get-GlicDeviceApps -OrgUnit '/Schools/West'

# All devices where a specific extension is installed
Get-GlicDeviceApps | Where-Object { $_.AppId -eq 'aapbdbdomjkkjkaonfhkkikfgjllcleb' }

# App count per device (outlier detection)
Get-GlicDeviceApps | Group-Object DeviceId | Select-Object Name, Count | Sort-Object Count -Descending
```

| Parameter | Default | Description |
|---|---|---|
| `-OrgUnit` | *(all)* | OU path string |

**Output:** `DeviceAppRow` — `ReportDate`, `CustomerId`, `DeviceId`, `Machine`, `AppId`, `AppType`, `DisplayName`

---

### `Get-GlicBrowserExtensions`

Per-profile extension inventory for CBCM-enrolled Chrome browsers — one row per extension per profile.

A nested query similar to `Get-GlicDeviceApps`, but resolves extensions to browser profiles rather than Chrome OS devices. Each row identifies which user profile has which extension installed. Use this for extension security audits on Windows and Mac browser fleets.

Use this when you need to answer: *What extensions are installed in managed browsers? Who has a specific extension? Are any policy-prohibited or high-risk extensions in use?*

```powershell
Get-GlicBrowserExtensions
Get-GlicBrowserExtensions -OrgUnit '/IT'

# Profiles where a specific extension is installed
Get-GlicBrowserExtensions | Where-Object { $_.AppId -eq 'cjpalhdlnbpafiamejdnhcphjbkeiagm' }

# Extension count per user (high counts may indicate unmanaged installs)
Get-GlicBrowserExtensions | Group-Object Email | Select-Object Name, Count | Sort-Object Count -Descending
```

| Parameter | Default | Description |
|---|---|---|
| `-OrgUnit` | *(all)* | OU path string |

**Output:** `BrowserExtensionRow` — `ReportDate`, `CustomerId`, `ProfilePermanentId`, `ProfileId`, `Email`, `ProfileOrgUnitId`, `AppId`, `AppType`, `DisplayName`

---

### `Invoke-GlicDiscover`

Probes the SKU catalog against your tenant and writes `skus.json` with active license SKUs.

Tests each known SKU ID via the Licensing API and marks it active or inactive based on whether your tenant has assignments. Only emits pipeline output when something changed — `-Verbose` shows the full probe progress.

Use this when you need to refresh the license catalog — after initial setup, after new Google Workspace SKUs are purchased, or when `Get-GlicLicenses` returns no rows for a SKU you know is active.

```powershell
Invoke-GlicDiscover
Invoke-GlicDiscover -Verbose              # show probe progress

# Review what changed
Invoke-GlicDiscover | Where-Object Status -eq 'now active'
Invoke-GlicDiscover | Where-Object Status -eq 'set inactive'
```

**Output:** `DiscoverChangeRow` — `SkuName`, `SkuId`, `Status` (`now active` or `set inactive`)

---

## Pipeline Examples

```powershell
# Export all active devices to CSV
Get-GlicDevices | Export-Csv devices.csv -NoTypeInformation

# Staff hardware to JSON
Get-GlicHardware -OrgUnit '/Staff' | ConvertTo-Json | Out-File hardware.json

# Find devices not updated in 90 days
Get-GlicTelemetry | Where-Object {
    $_.LastUpdateCheckTime.HasValue -and
    $_.LastUpdateCheckTime.Value -lt [System.DateTimeOffset]::UtcNow.AddDays(-90)
}

# License count by SKU
Get-GlicLicenses | Group-Object SkuName | Select-Object Name, Count

# Discover new SKUs then pull licenses
Invoke-GlicDiscover -Verbose
Get-GlicLicenses | Export-Csv licenses.csv -NoTypeInformation
```

## Architecture

```
Connect-Glic
  → prompts for admin email + service-account.json path
  → calls customers.get("my_customer") to derive customer ID
  → writes glic.json + service-account.dpapi to %APPDATA%\GLic\
  → sets GlicSession (process-scoped singleton)

PowerShell pipeline
  → [Cmdlet] ProcessRecord()
      → TryAutoConnectAsync()             GlicSession → DPAPI blob → legacy SA.json → error
      → GetRowsAsync()                    IAsyncEnumerable<XxxRow> via Paginator
      → EmitRowsAsync()                   buffers rows; WriteObject per row on pipeline thread
```

**`GlicCmdletBase`** is the bridge between all 10 data cmdlets. It owns: auto-connect via `TryAutoConnectAsync`, `-Config` / `-ServiceAccountPath` resolution, `ProcessRecord` error routing, `StopProcessing` → `CancellationToken`, collect-then-emit pipeline thread safety.

**`GlicSession`** holds live `ApiClients` + `GlicConfig` for the process lifetime. Set by `Connect-Glic`; read by all data cmdlets.

**`Paginator.FetchAllAsync`** provides generic page-token iteration as `IAsyncEnumerable<T>` with cancellation threading throughout.

**`ChromeServiceFactory.BuildAsync`** builds DWD credentials via `.CreateWithUser(adminEmail)` and returns an `ApiClients` record bundling `ChromeManagementService`, `LicensingService`, and `DirectoryService`. Accepts either a file path or raw bytes (from DPAPI decryption).

## Testing

```powershell
dotnet test GLic.Tests
dotnet test GLic.Tests --filter "FullyQualifiedName~GlicCmdletBaseTests"
```

Tests covering: cmdlet base (pipeline-thread safety, cancellation), per-cmdlet field mapping, paginator pagination and cancellation, SKU catalog loading, GlicConfig loading.

No mocks — tests construct real helper instances with synthetic data.

## License

MIT — see [LICENSE](LICENSE).

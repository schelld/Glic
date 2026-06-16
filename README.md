# Glic

# GLic — Google Workspace License and Device Inventory

Native PowerShell binary module that queries Google Workspace APIs (Chrome Management, Licensing, Admin Directory) and emits typed objects to the pipeline for ingestion into Enterprise IT Asset Management systems. This repository is not an official Google product.

```powershell
Import-Module .\module\GLic
Get-GlicDevices | Export-Csv devices.csv -NoTypeInformation
Get-GlicUsers | Where-Object { $_.OrgUnitPath -like '/Staff/*' }
Get-GlicHardware | ConvertTo-Json | Out-File hardware.json
```

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

**Three files** are resolved at runtime as described in [Configuration resolution](#configuration-resolution):

| File | Purpose |
|---|---|
| `service-account.json` | GCP service account key (downloaded from Google Cloud Console) |
| `glic.json` | Workspace config: `customer_id` and `admin_email` |
| `skus.json` | License SKU list — bundled defaults shipped with the module; refresh with `Invoke-GlicDiscover` |

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

`service-account.json` is located by trying, in order:

1. `-ServiceAccountPath <path>` parameter
2. `credential_path` key in `glic.json` (relative paths resolve against the `glic.json` directory)
3. Next to the resolved `glic.json`
4. The module directory

`skus.json` is read from the `glic.json` directory first, then the bundled module copy, then
built-in defaults. `Invoke-GlicDiscover` always writes it next to `glic.json` — never into the
module directory, so refreshes work without admin rights and survive module updates.

## Build and Import

```powershell
dotnet build                     # compiles + stages module\GLic\
Import-Module .\module\GLic      # loads all 10 cmdlets
Get-Command -Module GLic         # verify
```

## Cmdlet Reference

All data cmdlets share two base parameters:

| Parameter | Default | Description |
|---|---|---|
| `-Config` | auto-resolved (see [Configuration resolution](#configuration-resolution)) | Path to `glic.json` |
| `-ServiceAccountPath` | auto-resolved | Path to the service-account key file |

---

### `Get-GlicApps`

Chrome OS app inventory — apps installed across the fleet.

```powershell
Get-GlicApps
Get-GlicApps | Export-Csv apps.csv -NoTypeInformation
```

**Output:** `AppRow` — `ReportDate`, `CustomerId`, `DisplayName`, `AppId`, `AppType`, `Publisher`, `BrowserDeviceCount`

---

### `Get-GlicDevices`

Chrome OS device inventory — 23-column hardware and status record per device.

```powershell
Get-GlicDevices                          # active devices (default)
Get-GlicDevices -Status all              # all statuses
Get-GlicDevices -Status deprovisioned
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

Google Workspace license assignments — joins license assignments with user directory.

```powershell
Get-GlicLicenses                                     # all SKUs in skus.json
Get-GlicLicenses -SkuIds '1010310003','1010310010'  # specific SKUs only
```

| Parameter | Default | Description |
|---|---|---|
| `-SkuIds` | *(from skus.json)* | Array of SKU IDs to query |

**Output:** `LicenseRow` — `UserEmail`, `FullName`, `OrgUnit`, `IsAdmin`, `Suspended`, `LastLoginTime`, `ProductId`, `ProductName`, `SkuId`, `SkuName`, `AssignmentStatus`, and more.

> SKU IDs are managed in `skus.json`. Run `Invoke-GlicDiscover` to populate it.

---

### `Get-GlicUsers`

User directory export — 23-column record per user.

```powershell
Get-GlicUsers                              # active users (default)
Get-GlicUsers -Suspended All              # active + suspended
Get-GlicUsers -Suspended Suspended        # suspended only
Get-GlicUsers -OrgUnit '/Staff'
```

| Parameter | Default | Values |
|---|---|---|
| `-Suspended` | `Active` | `All`, `Active`, `Suspended` |
| `-OrgUnit` | *(all)* | OU path string |

> Default is `Active` — differs from the old CLI which defaulted to `All`.

**Output:** `UserRow` — `PrimaryEmail`, `FullName`, `OrgUnit`, `IsAdmin`, `IsDelegatedAdmin`, `Suspended`, `Archived`, `LastLoginTime`, `Department`, `JobTitle`, `ManagerEmail`, `Aliases`, and more.

---

### `Get-GlicManagedBrowsers`

Per-profile inventory of CBCM-enrolled Chrome browsers.

```powershell
Get-GlicManagedBrowsers
Get-GlicManagedBrowsers -OrgUnit '/IT/Managed'
```

| Parameter | Default | Description |
|---|---|---|
| `-OrgUnit` | *(all)* | OU path string; falls back to all profiles if filter unsupported |

**Output:** `ManagedBrowserRow` — `ProfilePermanentId`, `ProfileId`, `UserEmail`, `BrowserVersion`, `BrowserChannel`, `OsPlatformType`, `OsVersion`, `Hostname`, `Machine`, `ExtensionCount`, `LastActivityTime`, and more.

---

### `Get-GlicDeviceApps`

Per-device app and extension inventory — one row per app per device.

```powershell
Get-GlicDeviceApps
Get-GlicDeviceApps -OrgUnit '/Schools/West'
```

| Parameter | Default | Description |
|---|---|---|
| `-OrgUnit` | *(all)* | OU path string |

**Output:** `DeviceAppRow` — `ReportDate`, `CustomerId`, `DeviceId`, `Machine`, `AppId`, `AppType`, `DisplayName`

---

### `Get-GlicBrowserExtensions`

Per-profile extension inventory for CBCM-enrolled browsers.

```powershell
Get-GlicBrowserExtensions
Get-GlicBrowserExtensions -OrgUnit '/IT'
```

| Parameter | Default | Description |
|---|---|---|
| `-OrgUnit` | *(all)* | OU path string |

**Output:** `BrowserExtensionRow` — `ReportDate`, `CustomerId`, `ProfilePermanentId`, `ProfileId`, `Email`, `ProfileOrgUnitId`, `AppId`, `AppType`, `DisplayName`

---

### `Invoke-GlicDiscover`

Probes the SKU catalog against your tenant and updates `skus.json`. Emits a change summary to the pipeline.

```powershell
Invoke-GlicDiscover
Invoke-GlicDiscover -Verbose    # show probe progress
Invoke-GlicDiscover | Where-Object Status -eq 'now active'
```

> `skus.json` is the output. Run this first to populate license SKUs before using `Get-GlicLicenses`.

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
PowerShell pipeline
  → [Cmdlet] ProcessRecord()
      → GlicConfig.Load(-Config)          reads glic.json
      → ChromeServiceFactory.BuildAsync() DWD-scoped Google API clients
      → GetRowsAsync()                    IAsyncEnumerable<XxxRow> via Paginator
      → EmitRowsAsync()                   buffers rows; WriteObject per row on pipeline thread
```

**`GlicCmdletBase`** (25 edges — highest-connected node in the graph) is the bridge between all 10 data cmdlets. It owns: `-Config` / `-ServiceAccountPath` resolution, `ProcessRecord` error routing, `StopProcessing` → `CancellationToken`, collect-then-emit pipeline thread safety.

**`Paginator.FetchAllAsync`** provides generic page-token iteration as `IAsyncEnumerable<T>` with cancellation threading throughout.

**`ChromeServiceFactory.BuildAsync`** reads `service-account.json`, builds DWD credentials via `.CreateWithUser(adminEmail)`, and returns an `ApiClients` record bundling `ChromeManagementService`, `LicensingService`, and `DirectoryService`.

## Testing

```powershell
dotnet test GLic.Tests
dotnet test GLic.Tests --filter "FullyQualifiedName~GlicCmdletBaseTests"
```

Tests covering: cmdlet base (pipeline-thread safety, cancellation), per-cmdlet field mapping, paginator pagination and cancellation, SKU catalog loading, GlicConfig loading.

No mocks — tests construct real helper instances with synthetic data.

## License

MIT — see [LICENSE](LICENSE).

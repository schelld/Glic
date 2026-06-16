# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-06-12

### Added
- Ten cmdlets: `Get-GlicApps`, `Get-GlicDevices`, `Get-GlicTelemetry`, `Get-GlicHardware`,
  `Get-GlicLicenses`, `Get-GlicUsers`, `Get-GlicManagedBrowsers`, `Get-GlicDeviceApps`,
  `Get-GlicBrowserExtensions`, `Invoke-GlicDiscover`.
- Domain-Wide Delegation authentication via `service-account.json` + `glic.json`.
- Config resolution chain: `-Config`/`-ServiceAccountPath` parameters, `GLIC_CONFIG` env var,
  `%ProgramData%\GLic`, `%APPDATA%\GLic`, module directory (first match wins).
- Parameterized `install.ps1` (CurrentUser/AllUsers scopes, versioned module folder,
  icacls key hardening in `%ProgramData%\GLic`).
- `uninstall.ps1` with scope selection and optional config removal.
- Default table views (`GLic.format.ps1xml`) for `DeviceRow`, `HardwareRow`, `UserRow`,
  `LicenseRow`.
- MAML external help (`en-US\GLic.dll-Help.xml`) for all ten cmdlets.
- `about_GLic` help topic covering auth model and config resolution order.

### Breaking Changes
- `admin.directory.orgunit.readonly` scope added to the DWD token request. Existing
  installs must add this scope in Google Admin Console (Security > API Controls >
  Domain-wide Delegation) before upgrading, or all cmdlets will return 403.
  Affected cmdlets when -OrgUnit is used: `Get-GlicManagedBrowsers`, `Get-GlicDeviceApps`,
  `Get-GlicBrowserExtensions`.

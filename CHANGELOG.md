# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0] - 2026-06-22

### Added
- `Connect-Glic` cmdlet: interactive first-time setup. Prompts for admin email and
  `service-account.json` path (or reads from `-ServiceAccountPath`). Derives the Google
  customer ID via the Directory API and writes `glic.json` to `%APPDATA%\GLic\`.
  `-KeyPath` stores the service-account key in a passwordless `GlicVault` (SecretStore)
  so subsequent sessions reconnect silently without the key file.
  `-Force` re-authenticates an existing session.
- Ten data cmdlets: `Get-GlicApps`, `Get-GlicDevices`, `Get-GlicTelemetry`,
  `Get-GlicHardware`, `Get-GlicLicenses`, `Get-GlicUsers`, `Get-GlicManagedBrowsers`,
  `Get-GlicDeviceApps`, `Get-GlicBrowserExtensions`, `Invoke-GlicDiscover`.
- Silent auto-connect: all data cmdlets reconnect automatically in a new session using
  stored credentials — no manual credential placement required after first run.
- Domain-Wide Delegation authentication via `service-account.json` + `glic.json`.
- Config resolution chain: `-Config`/`-ServiceAccountPath` parameters, `GLIC_CONFIG` env var,
  `%ProgramData%\GLic`, `%APPDATA%\GLic`, module directory (first match wins).
- Default table views (`GLic.format.ps1xml`) for `DeviceRow`, `HardwareRow`, `UserRow`,
  `LicenseRow`.
- MAML external help (`en-US\GLic.dll-Help.xml`) for all eleven cmdlets.
- `about_GLic` help topic covering auth model and config resolution order.

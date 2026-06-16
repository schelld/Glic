<#
.SYNOPSIS
    Installs the GLic module and its configuration.
.DESCRIPTION
    Copies the module payload (the folder containing this script) into a
    versioned PowerShell module path and writes glic.json and
    service-account.json to the GLic config directory:

        AllUsers      %ProgramFiles%\WindowsPowerShell\Modules\GLic\<version>
                      %ProgramData%\GLic   (key ACL-restricted to SYSTEM + Administrators)
        CurrentUser   <Documents>\WindowsPowerShell\Modules\GLic\<version>
                      %APPDATA%\GLic

    Values not supplied as parameters are reused from an existing glic.json,
    then prompted for. Re-running the installer upgrades in place: it reuses
    the existing config without prompting and removes older version folders.

    Without -Scope, an elevated session installs machine-wide (AllUsers) and a
    non-elevated session falls back to CurrentUser with a notice.
.EXAMPLE
    .\install.ps1
    Interactive install - prompts only for values it cannot find.
.EXAMPLE
    .\install.ps1 -CustomerId C03xxxxx -AdminEmail glic-svc@contoso.org -ServiceAccountPath C:\keys\sa.json -Scope AllUsers -NoVerify
    Fully silent machine-wide install (elevated session required).
.EXAMPLE
    .\install.ps1 -Scope AllUsers -ReadAccessAccount 'CONTOSO\svc-itam'
    Machine-wide install that also lets a scheduled-task identity read the key.
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [string]$CustomerId,
    [string]$AdminEmail,
    [string]$ServiceAccountPath,
    [ValidateSet('AllUsers', 'CurrentUser')]
    [string]$Scope,
    # Extra account granted read access to the service-account key
    # (AllUsers only) - e.g. the identity a scheduled task runs as.
    [string]$ReadAccessAccount,
    [switch]$NoVerify
)

$ErrorActionPreference = 'Stop'

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    ([Security.Principal.WindowsPrincipal]$identity).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

# --- Resolve scope -----------------------------------------------------------
$isAdmin = Test-IsAdministrator
if (-not $Scope) {
    if ($isAdmin) {
        $Scope = 'AllUsers'
    } else {
        $Scope = 'CurrentUser'
        Write-Host 'Not elevated - installing for the current user only.'
        Write-Host 'Run from an elevated session (or pass -Scope AllUsers there) for a machine-wide install.'
    }
}
if ($Scope -eq 'AllUsers' -and -not $isAdmin) {
    # -ErrorAction Continue: EAP is Stop, which would otherwise turn this into
    # a terminating throw and skip the explicit exit code.
    Write-Error '-Scope AllUsers requires an elevated PowerShell session.' -ErrorAction Continue
    exit 1
}

# --- Resolve paths -----------------------------------------------------------
$manifest = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'GLic.psd1')
$version  = $manifest.ModuleVersion

if ($Scope -eq 'AllUsers') {
    $moduleBase = Join-Path $env:ProgramFiles 'WindowsPowerShell\Modules\GLic'
    $configDir  = Join-Path $env:ProgramData 'GLic'
} else {
    # GetFolderPath honours OneDrive-redirected Documents, matching where
    # PS 5.1 itself roots the per-user module path.
    $documents  = [Environment]::GetFolderPath('MyDocuments')
    $moduleBase = Join-Path $documents 'WindowsPowerShell\Modules\GLic'
    $configDir  = Join-Path $env:APPDATA 'GLic'
}
$moduleDir  = Join-Path $moduleBase $version
$configPath = Join-Path $configDir 'glic.json'
$keyPath    = Join-Path $configDir 'service-account.json'

# --- Collect config values: parameters > existing glic.json > prompt ----------
if (Test-Path $configPath) {
    try {
        $existing = Get-Content $configPath -Raw | ConvertFrom-Json
        if (-not $CustomerId) { $CustomerId = $existing.customer_id }
        if (-not $AdminEmail) { $AdminEmail = $existing.admin_email }
    }
    catch {
        Write-Warning "Existing glic.json at $configPath is unreadable; ignoring it."
    }
}
while (-not $CustomerId) { $CustomerId = Read-Host 'Customer ID (e.g. C03xxxxx)' }
while (-not $AdminEmail) { $AdminEmail = Read-Host 'Admin email (e.g. glic-service@yourdomain.com)' }

# --- Locate the key: parameter > existing key > payload sibling > prompt ------
if (-not $ServiceAccountPath) {
    if (Test-Path $keyPath) {
        $ServiceAccountPath = $keyPath
        Write-Host "Reusing existing key: $keyPath"
    } elseif (Test-Path (Join-Path $PSScriptRoot 'service-account.json')) {
        $ServiceAccountPath = Join-Path $PSScriptRoot 'service-account.json'
        Write-Host 'Using service-account.json found next to the installer.'
    }
}
while (-not $ServiceAccountPath -or -not (Test-Path $ServiceAccountPath)) {
    if ($ServiceAccountPath) { Write-Warning "File not found: $ServiceAccountPath" }
    Write-Host 'Download a service-account key from https://console.cloud.google.com/ (IAM > Service Accounts > Keys).'
    $ServiceAccountPath = Read-Host 'Path to service-account.json'
}

# --- Write config + key ------------------------------------------------------
$null = New-Item -ItemType Directory -Force $configDir
@{ customer_id = $CustomerId; admin_email = $AdminEmail } | ConvertTo-Json |
    Set-Content -Path $configPath -Encoding utf8

# Compare file identity via Get-Item, not raw path strings - Resolve-Path
# does not expand 8.3 short paths, and copying a file onto itself throws.
$resolvedKey = (Resolve-Path $ServiceAccountPath).Path
$sameFile = $false
if (Test-Path $keyPath) {
    $sameFile = (Get-Item $resolvedKey).FullName -eq (Get-Item $keyPath).FullName
}
if (-not $sameFile) {
    Copy-Item $resolvedKey $keyPath -Force
}

# --- Harden the key ACL (machine-wide installs only) ---------------------------
if ($Scope -eq 'AllUsers') {
    # SID form is locale-proof: S-1-5-18 = SYSTEM, S-1-5-32-544 = Administrators.
    $grants = @('*S-1-5-18:R', '*S-1-5-32-544:F')
    if ($ReadAccessAccount) { $grants += "${ReadAccessAccount}:R" }
    $icaclsArgs = @($keyPath, '/inheritance:r')
    foreach ($g in $grants) { $icaclsArgs += '/grant:r'; $icaclsArgs += $g }
    & icacls @icaclsArgs | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Error "icacls failed to restrict $keyPath (exit $LASTEXITCODE). Fix the ACL before using this key." -ErrorAction Continue
        exit 1
    }
    $aclNote = 'SYSTEM + Administrators'
    if ($ReadAccessAccount) { $aclNote += " + $ReadAccessAccount" }
    Write-Host "Key ACL restricted to: $aclNote"
    if (-not $ReadAccessAccount) {
        Write-Host 'Note: standard users cannot read the key. Re-run with -ReadAccessAccount <account>'
        Write-Host 'to allow a task/service identity, or have users install with -Scope CurrentUser.'
    }
}

# --- Copy the module into a fresh versioned folder ----------------------------
Get-Module GLic | Remove-Module -Force -ErrorAction SilentlyContinue
$null = New-Item -ItemType Directory -Force $moduleDir
Copy-Item (Join-Path $PSScriptRoot '*') $moduleDir -Recurse -Force `
    -Exclude 'install.ps1', 'service-account.json', 'glic.json'

# --- Remove legacy layouts ----------------------------------------------------
# Pre-versioned installs put files directly under Modules\GLic\, which makes
# PowerShell ignore version subfolders - they must go.
Get-ChildItem $moduleBase -File -ErrorAction SilentlyContinue | ForEach-Object {
    try { Remove-Item $_.FullName -Force }
    catch { Write-Warning "Could not remove legacy file $($_.FullName) - close other PowerShell sessions and delete it manually." }
}
Get-ChildItem $moduleBase -Directory -ErrorAction SilentlyContinue |
    Where-Object Name -ne $version | ForEach-Object {
        try { Remove-Item $_.FullName -Recurse -Force; Write-Host "Removed old version: $($_.Name)" }
        catch { Write-Warning "Could not remove old version $($_.FullName) - close other PowerShell sessions and delete it manually." }
    }
if ($Scope -eq 'AllUsers') {
    $legacyPs7 = Join-Path $env:ProgramFiles 'PowerShell\Modules\GLic'
    if (Test-Path $legacyPs7) {
        try { Remove-Item $legacyPs7 -Recurse -Force; Write-Host "Removed legacy PowerShell 7 copy: $legacyPs7" }
        catch { Write-Warning "Could not remove legacy PS7 copy $legacyPs7 - delete it manually." }
    }
}

Write-Host ''
Write-Host "GLic $version installed for $Scope"
Write-Host "  Module: $moduleDir"
Write-Host "  Config: $configDir"

# --- Verify -------------------------------------------------------------------
if ($NoVerify) { exit 0 }

Write-Host 'Verifying API access (Invoke-GlicDiscover)...'
try {
    Import-Module (Join-Path $moduleDir 'GLic.psd1') -Force
    $null = Invoke-GlicDiscover -ErrorAction Stop
    Write-Host 'Verification succeeded - GLic is ready.'
    exit 0
}
catch {
    Write-Warning "Install completed, but verification failed: $_"
    Write-Host 'Checklist:'
    Write-Host '  1. Domain-Wide Delegation: Google Admin Console > Security > API Controls >'
    Write-Host '     Domain-wide Delegation - the service account client ID must be granted'
    Write-Host '     the six scopes listed in the README.'
    Write-Host '  2. admin_email must be an admin (or delegated-admin) user in your domain.'
    Write-Host '  3. customer_id must match your tenant (Admin Console > Account settings).'
    Write-Host 'Re-run verification any time: Import-Module GLic; Invoke-GlicDiscover'
    exit 1
}

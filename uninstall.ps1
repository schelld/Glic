<#
.SYNOPSIS
    Removes the GLic module and (optionally) its configuration.
.DESCRIPTION
    Removes GLic from the machine-wide and/or per-user module paths. The
    config directories (%ProgramData%\GLic, %APPDATA%\GLic) hold your
    service-account key and tenant config; they are kept unless you pass
    -RemoveConfig or answer the prompt with y.

    Exit codes: 0 = success, 1 = invalid arguments, 2 = an AllUsers install
    was found but the session was not elevated (re-run elevated).
.EXAMPLE
    .\uninstall.ps1
    Removes every GLic install it can, prompting about each config folder.
.EXAMPLE
    .\uninstall.ps1 -Scope CurrentUser -RemoveConfig
    Silently removes the per-user module and per-user config/key.
#>
#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('AllUsers', 'CurrentUser', 'All')]
    [string]$Scope = 'All',
    [switch]$RemoveConfig,
    [switch]$KeepConfig
)

$ErrorActionPreference = 'Stop'

if ($RemoveConfig -and $KeepConfig) {
    # -ErrorAction Continue: EAP is Stop, which would otherwise turn this into
    # a terminating throw and skip the explicit exit code.
    Write-Error 'Use either -RemoveConfig or -KeepConfig, not both.' -ErrorAction Continue
    exit 1
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    ([Security.Principal.WindowsPrincipal]$identity).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

Get-Module GLic | Remove-Module -Force -ErrorAction SilentlyContinue

$targets = @()
if ($Scope -in 'AllUsers', 'All') {
    $targets += [pscustomobject]@{
        Name       = 'AllUsers'
        ModuleDirs = @(
            (Join-Path $env:ProgramFiles 'WindowsPowerShell\Modules\GLic'),
            (Join-Path $env:ProgramFiles 'PowerShell\Modules\GLic')   # legacy PS7 copy
        )
        ConfigDir  = Join-Path $env:ProgramData 'GLic'
        NeedsAdmin = $true
    }
}
if ($Scope -in 'CurrentUser', 'All') {
    $targets += [pscustomobject]@{
        Name       = 'CurrentUser'
        ModuleDirs = @(Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'WindowsPowerShell\Modules\GLic')
        ConfigDir  = Join-Path $env:APPDATA 'GLic'
        NeedsAdmin = $false
    }
}

$removedAnything = $false
$skippedNeedsAdmin = $false
foreach ($t in $targets) {
    $present       = @($t.ModuleDirs | Where-Object { Test-Path $_ })
    $configPresent = Test-Path $t.ConfigDir
    if ($present.Count -eq 0 -and -not $configPresent) { continue }

    if ($t.NeedsAdmin -and -not (Test-IsAdministrator)) {
        Write-Warning "$($t.Name): found an install but this session is not elevated - skipped. Re-run elevated to remove it."
        $skippedNeedsAdmin = $true
        continue
    }

    foreach ($dir in $present) {
        try {
            Remove-Item $dir -Recurse -Force
            Write-Host "Removed module: $dir"
            $removedAnything = $true
        }
        catch {
            Write-Warning "Could not remove $dir - close other PowerShell sessions and retry. ($_)"
        }
    }

    if ($configPresent) {
        $remove = [bool]$RemoveConfig
        if (-not $RemoveConfig -and -not $KeepConfig) {
            $answer = Read-Host "Also delete config + service-account key at $($t.ConfigDir)? [y/N]"
            $remove = $answer -match '^[Yy]'
        }
        if ($remove) {
            try {
                Remove-Item $t.ConfigDir -Recurse -Force
                Write-Host "Removed config: $($t.ConfigDir)"
                $removedAnything = $true
            }
            catch {
                Write-Warning "Could not remove config $($t.ConfigDir) - a file may be locked or ACL-protected. ($_)"
            }
        } else {
            Write-Host "Kept config: $($t.ConfigDir)"
        }
    }
}

if ($removedAnything) {
    Write-Host 'GLic uninstall complete. Open a new PowerShell window for module changes to take effect.'
} else {
    Write-Host 'Nothing found to remove.'
}
if ($skippedNeedsAdmin) { exit 2 }
exit 0

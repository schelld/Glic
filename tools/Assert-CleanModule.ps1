#Requires -Version 5.1
<#
.SYNOPSIS
    Fails (exit > 0) if the staged module folder has credential files, a broken
    manifest, or PSScriptAnalyzer errors in its .ps1 files.
.DESCRIPTION
    Run before Publish-Module or zipping a release. Intended as a mandatory
    gate in the release pipeline: a service-account.json in the staging dir
    means a real GCP key is about to be published.
#>
[CmdletBinding()]
param(
    [string]$Path
)

# $PSScriptRoot is empty inside param() default expressions under Windows
# PowerShell 5.1 — it is only populated for the script body — so the default
# must be resolved here instead.
if (-not $Path) {
    $Path = Join-Path (Split-Path $PSScriptRoot -Parent) 'module\GLic'
}

$failed = 0

# --- Credential check --------------------------------------------------------
$forbidden = 'service-account.json', 'glic.json'

if (-not (Test-Path $Path)) {
    Write-Error "Staged module folder not found: $Path (run 'dotnet build' first)"
    exit 1
}

$hits = Get-ChildItem -Path $Path -Recurse -Force -File |
    Where-Object { $forbidden -contains $_.Name }

if ($hits) {
    foreach ($hit in $hits) {
        Write-Error "CREDENTIAL FILE IN STAGING DIR - do not publish: $($hit.FullName)"
    }
    $failed++
} else {
    Write-Host "Clean: no credential files under $Path"
}

# --- Test-ModuleManifest -----------------------------------------------------
Write-Host 'Testing module manifest...'
try {
    $manifest = Test-ModuleManifest (Join-Path $Path 'GLic.psd1') -ErrorAction Stop
    if ($manifest.ExportedCmdlets.Count -ne 10) {
        Write-Error "Manifest exports $($manifest.ExportedCmdlets.Count) cmdlet(s); expected 10."
        $failed++
    } else {
        Write-Host "  Manifest ok: $($manifest.Name) $($manifest.Version), $($manifest.ExportedCmdlets.Count) cmdlets"
    }
} catch {
    Write-Error "Test-ModuleManifest failed: $_"
    $failed++
}

# --- PSScriptAnalyzer --------------------------------------------------------
if (Get-Module PSScriptAnalyzer -ListAvailable) {
    Write-Host 'Running PSScriptAnalyzer on staged .ps1 files...'
    $ps1Files = Get-ChildItem $Path -Filter '*.ps1' -Recurse
    if ($ps1Files) {
        $findings = $ps1Files | ForEach-Object {
            Invoke-ScriptAnalyzer -Path $_.FullName -Severity Error, Warning
        }
        if ($findings) {
            $findings | ForEach-Object {
                Write-Warning "$($_.ScriptName):$($_.Line) [$($_.Severity)] $($_.RuleName) - $($_.Message)"
            }
            Write-Error "PSScriptAnalyzer found $($findings.Count) finding(s)."
            $failed++
        } else {
            Write-Host "  PSScriptAnalyzer: no findings in $($ps1Files.Count) script(s)"
        }
    } else {
        Write-Host '  PSScriptAnalyzer: no .ps1 files to check'
    }
} else {
    Write-Warning 'PSScriptAnalyzer not installed - skipping (run: Install-Module PSScriptAnalyzer -Scope CurrentUser)'
}

exit $failed

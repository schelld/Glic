#Requires -Version 5.1
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$Version
)
$content = Get-Content $Path -Raw
$patched = $content -replace "ModuleVersion\s*=\s*'[^']*'", "ModuleVersion     = '$Version'"
if ($content -eq $patched) {
    Write-Error "ModuleVersion key not found in $Path" -ErrorAction Continue
    exit 1
}
Set-Content $Path $patched -Encoding UTF8
Write-Host "Stamped ModuleVersion = '$Version' into $Path"

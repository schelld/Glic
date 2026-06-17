$fwSubdir = if ($PSEdition -eq 'Core') { 'net8.0' } else { 'net472' }
$fwDir = Join-Path $PSScriptRoot $fwSubdir

# Store the framework-specific dir so the delegate can find dependency DLLs.
# PS 5.1 ignores GLic.dll.config binding redirects, so we resolve them here.
[System.AppDomain]::CurrentDomain.SetData('GLic.ModuleDir', $fwDir)

$null = [System.AppDomain]::CurrentDomain.add_AssemblyResolve(
    [System.ResolveEventHandler] {
        param ($sender, $e)
        $dir = [System.AppDomain]::CurrentDomain.GetData('GLic.ModuleDir')
        $asmName = ([System.Reflection.AssemblyName] $e.Name).Name
        $candidate = Join-Path $dir "$asmName.dll"
        if (Test-Path $candidate) {
            return [System.Reflection.Assembly]::LoadFrom($candidate)
        }
        return $null
    }
)

Import-Module (Join-Path $fwDir 'GLic.dll')

function Initialize-GlicAuth {
<#
.SYNOPSIS
    One-time setup: validates a Google service-account key file and stores it in the GlicVault SecretStore.

.DESCRIPTION
    Registers the GlicVault (backed by Microsoft.PowerShell.SecretStore with passwordless CurrentUser
    authentication) if it does not already exist, then stores the raw service-account JSON and the
    extracted client_email as SecretStore secrets.

    Run this once per machine/user before calling Connect-Glic. Requires the
    Microsoft.PowerShell.SecretManagement and Microsoft.PowerShell.SecretStore modules:

        Install-Module Microsoft.PowerShell.SecretManagement, Microsoft.PowerShell.SecretStore -Scope CurrentUser

.PARAMETER KeyPath
    Path to the Google service-account JSON key file downloaded from Google Cloud Console
    (IAM & Admin > Service Accounts > Keys > Add Key > JSON).

.EXAMPLE
    Initialize-GlicAuth -KeyPath C:\keys\my-sa.json

.EXAMPLE
    Initialize-GlicAuth -KeyPath .\my-sa.json -Verbose
#>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)]
        [ValidateScript({ Test-Path $_ -PathType Leaf })]
        [string]$KeyPath
    )

    $resolvedPath = (Resolve-Path -LiteralPath $KeyPath -ErrorAction Stop).Path
    Write-Verbose "Reading service-account file: $resolvedPath"

    $rawContent = Get-Content -LiteralPath $resolvedPath -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($rawContent)) {
        throw "File '$resolvedPath' is empty."
    }

    try {
        $json = $rawContent | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "Invalid JSON in '${resolvedPath}': $_"
    }

    if ($json.type -ne 'service_account') {
        throw "'${resolvedPath}' does not appear to be a service account key (expected `"type`": `"service_account`"). Download a JSON key from Google Cloud Console > IAM & Admin > Service Accounts > Keys."
    }

    $clientEmail = $json.client_email
    if ([string]::IsNullOrWhiteSpace($clientEmail)) {
        throw "No 'client_email' field found in '${resolvedPath}'."
    }
    Write-Verbose "Service account email: $clientEmail"

    foreach ($mod in 'Microsoft.PowerShell.SecretManagement', 'Microsoft.PowerShell.SecretStore') {
        if (-not (Get-Module -Name $mod -ListAvailable -ErrorAction SilentlyContinue)) {
            throw "Required module '$mod' is not installed. Run: Install-Module $mod -Scope CurrentUser"
        }
    }

    if (-not (Get-SecretVault -Name 'GlicVault' -ErrorAction SilentlyContinue)) {
        Write-Verbose "Configuring SecretStore for passwordless access..."
        # Must run BEFORE Register-SecretVault; otherwise vault init prompts for a password first.
        Set-SecretStoreConfiguration -Scope CurrentUser -Authentication None -Interaction None -Confirm:$false -ErrorAction Stop
        Write-Verbose "Registering GlicVault..."
        Register-SecretVault -Name 'GlicVault' -ModuleName 'Microsoft.PowerShell.SecretStore' -ErrorAction Stop
        Write-Verbose "GlicVault registered (passwordless, current user)."
    } else {
        Write-Verbose "GlicVault already exists - skipping registration."
    }

    if ($PSCmdlet.ShouldProcess('GlicVault', 'Store GlicServiceAccountKey and GlicServiceAccountEmail')) {
        Set-Secret -Name 'GlicServiceAccountKey'   -Secret $rawContent  -Vault 'GlicVault' -ErrorAction Stop
        Set-Secret -Name 'GlicServiceAccountEmail' -Secret $clientEmail -Vault 'GlicVault' -ErrorAction Stop
        Write-Verbose "Secrets stored in GlicVault."
        Write-Host "GLic vault configured for $clientEmail. Run Connect-Glic -AdminEmail admin@domain.com to complete setup."
    }
}

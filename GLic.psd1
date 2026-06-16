@{
    ModuleVersion          = '1.0.0'
    GUID                   = 'de3505a7-d885-4aaf-9672-8656fbdd236d'
    Author                 = 'D. Schell'
    Copyright              = 'Copyright (c) 2026 D. Schell'
    Description            = 'Google Workspace inventory and licensing cmdlets for Flexera ITAM ingestion'
    RootModule             = if ($PSEdition -eq 'Core') { 'net8.0\GLic.dll' } else { 'net472\GLic.dll' }
    PowerShellVersion      = '5.1'
    DotNetFrameworkVersion = '4.7.2'
    CompatiblePSEditions   = @('Desktop', 'Core')

    FormatsToProcess       = @('GLic.format.ps1xml')
    FunctionsToExport      = @()
    AliasesToExport        = @()

    CmdletsToExport        = @(
        'Connect-Glic',
        'Get-GlicApps',
        'Get-GlicDevices',
        'Get-GlicTelemetry',
        'Get-GlicDeviceApps',
        'Get-GlicBrowserExtensions',
        'Get-GlicManagedBrowsers',
        'Get-GlicHardware',
        'Get-GlicLicenses',
        'Get-GlicUsers',
        'Invoke-GlicDiscover'
    )

    PrivateData = @{
        PSData = @{
            Tags         = @('Google', 'Workspace', 'ChromeOS', 'ITAM', 'Licensing', 'Admin', 'Directory', 'Chrome')
            ReleaseNotes = 'Initial public release. Provides Get-Glic* and Invoke-GlicDiscover cmdlets for Google Workspace Chrome Management, Licensing, and Admin Directory APIs.'
            LicenseUri   = 'https://github.com/schelld/GLIC/blob/main/LICENSE'
            ProjectUri   = 'https://github.com/schelld/GLIC'
        }
    }
}

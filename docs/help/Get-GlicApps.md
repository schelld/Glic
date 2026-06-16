---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicApps

## SYNOPSIS
Returns Chrome app inventory across all ChromeOS devices in the Workspace fleet.

## SYNTAX

```
Get-GlicApps [-Config <String>] [-ServiceAccountPath <String>] [<CommonParameters>]
```

## DESCRIPTION
Get-GlicApps queries the Chrome Management Reports API and returns one AppRow per installed app per device. Each row includes the app ID, display name, type (extension, app, theme), version, installation source, and the device and user it belongs to. Use this cmdlet to audit fleet-wide app coverage, identify unapproved extensions, or feed app data into an ITAM system.

All credential and config resolution follows the standard order: -Config / -ServiceAccountPath parameters, then GLIC_CONFIG env var, then %ProgramData%\GLic (AllUsers install), then %APPDATA%\GLic (CurrentUser install), then the module directory.

## EXAMPLES

### Example 1: Export all installed apps to CSV
```powershell
Get-GlicApps | Export-Csv -Path apps.csv -NoTypeInformation
```

Exports every installed Chrome app across the entire fleet.

### Example 2: Find devices with a specific extension
```powershell
Get-GlicApps | Where-Object { $_.AppId -eq 'cjpalhdlnbpafiamejdnhcphjbkeiagm' } |
    Select-Object DeviceId, DisplayName, Version
```

## PARAMETERS

### -Config
Path to glic.json. When omitted, resolved automatically from the GLIC_CONFIG environment variable, %ProgramData%\GLic, %APPDATA%\GLic, or the module directory (first match wins).

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ServiceAccountPath
Path to the GCP service-account key JSON file. When omitted, resolved from the same config-directory chain as -Config.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### GLic.Cmdlets.AppRow

## NOTES
Requires chrome.management.reports.readonly DWD scope.

## RELATED LINKS
[Get-GlicDeviceApps]()
[Get-GlicBrowserExtensions]()
[about_GLic]()

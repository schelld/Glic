---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicDeviceApps

## SYNOPSIS
Returns per-device app inventory from the Chrome Management Reports API.

## SYNTAX

```
Get-GlicDeviceApps [-OrgUnit <String>] [-Config <String>] [-ServiceAccountPath <String>] [<CommonParameters>]
```

## DESCRIPTION
Get-GlicDeviceApps queries the Chrome Management Reports API and returns one DeviceAppRow per app per device. Each row includes the report date, customer ID, device ID, machine name, app ID, app type, and display name. Use this cmdlet to audit app deployments per device. Use -OrgUnit to scope the query to a specific organizational unit path (requires admin.directory.orgunit.readonly DWD scope).

## EXAMPLES

### Example 1: Export all device app data
```powershell
Get-GlicDeviceApps | Export-Csv -Path device-apps.csv -NoTypeInformation
```

### Example 2: Find all devices with a specific app
```powershell
Get-GlicDeviceApps | Where-Object { $_.AppId -eq 'com.example.myapp' } |
    Select-Object DeviceId, Machine, DisplayName
```

### Example 3: Top 10 apps in a school OU
```powershell
Get-GlicDeviceApps -OrgUnit '/Schools/West' |
    Group-Object AppId | Sort-Object Count -Descending | Select-Object -First 10 Name, Count
```

## PARAMETERS

### -Config
Path to glic.json. When omitted, resolved automatically from the config-directory chain.

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

### -OrgUnit
Filters devices to those in the specified organizational unit path. Requires admin.directory.orgunit.readonly to be granted in the DWD configuration. When omitted, returns data for all devices.

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
Path to the GCP service-account key JSON file. When omitted, resolved from the config-directory chain.

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

### GLic.Cmdlets.DeviceAppRow

## NOTES
Requires chrome.management.reports.readonly DWD scope. The -OrgUnit parameter additionally requires admin.directory.orgunit.readonly.

## RELATED LINKS
[Get-GlicApps]()
[Get-GlicBrowserExtensions]()
[about_GLic]()

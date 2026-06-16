---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicTelemetry

## SYNOPSIS
Returns OS update compliance snapshots for ChromeOS devices.

## SYNTAX

```
Get-GlicTelemetry [-Status <String>] [-OrgUnit <String>] [-Config <String>] [-ServiceAccountPath <String>]
 [<CommonParameters>]
```

## DESCRIPTION
Get-GlicTelemetry queries the Chrome Management Telemetry API and returns one TelemetryRow per device containing OS version, platform version, firmware version, auto-update expiration date, update state, and reboot/update timestamps. Use this cmdlet to identify devices at or past their auto-update expiration (EOL), devices on outdated OS versions, or devices that have not rebooted recently. Use -OrgUnit to scope the query to a specific organizational unit path. Use -Status to filter by device lifecycle state.

## EXAMPLES

### Example 1: Find EOL devices
```powershell
Get-GlicTelemetry | Where-Object { $_.AutoUpdateExpiration -lt (Get-Date) } |
    Select-Object DeviceId, SerialNumber, AutoUpdateExpiration | Sort-Object AutoUpdateExpiration
```

### Example 2: Devices expiring within 12 months
```powershell
$cutoff = (Get-Date).AddMonths(12)
Get-GlicTelemetry -Status active |
    Where-Object { $_.AutoUpdateExpiration -gt (Get-Date) -and $_.AutoUpdateExpiration -lt $cutoff } |
    Export-Csv -Path expiring-soon.csv -NoTypeInformation
```

### Example 3: Scope to an OU
```powershell
Get-GlicTelemetry -OrgUnit '/Schools/East' | Select-Object DeviceId, OsVersion, UpdateState
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
Filters devices to those in the specified organizational unit path (e.g. '/Schools/East'). When omitted, returns devices across all OUs.

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

### -Status
Filters devices by lifecycle state.

```yaml
Type: String
Parameter Sets: (All)
Aliases:
Accepted values: all, active, deprovisioned, disabled

Required: False
Position: Named
Default value: active
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### GLic.Cmdlets.TelemetryRow

## NOTES
Requires chrome.management.telemetry.readonly and admin.directory.device.chromeos.readonly DWD scopes.

## RELATED LINKS
[Get-GlicHardware]()
[Get-GlicDevices]()
[about_GLic]()

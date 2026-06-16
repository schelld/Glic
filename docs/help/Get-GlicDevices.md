---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicDevices

## SYNOPSIS
Returns a list of ChromeOS devices from the Google Workspace Admin Directory.

## SYNTAX

```
Get-GlicDevices [-Status <String>] [-Config <String>] [-ServiceAccountPath <String>] [<CommonParameters>]
```

## DESCRIPTION
Get-GlicDevices queries the Admin Directory API and returns one DeviceRow per enrolled ChromeOS device. Each row includes the device ID, serial number, model, status, annotated user and location, OS version, network MAC addresses, enrollment time, and last sync time. Use -Status to filter by lifecycle state. Pipe the output to Export-Csv or Where-Object for asset reporting.

All credential and config resolution follows the standard order: -Config / -ServiceAccountPath parameters, then GLIC_CONFIG env var, then %ProgramData%\GLic (AllUsers install), then %APPDATA%\GLic (CurrentUser install), then the module directory.

## EXAMPLES

### Example 1: Export all active devices to CSV
```powershell
Get-GlicDevices -Status active | Export-Csv -Path devices.csv -NoTypeInformation
```

Exports all active ChromeOS devices to a CSV file.

### Example 2: Find devices not synced in 30 days
```powershell
$cutoff = (Get-Date).AddDays(-30)
Get-GlicDevices | Where-Object { $_.LastSync -lt $cutoff } |
    Select-Object DeviceId, AnnotatedUser, LastSync
```

Returns devices that have not checked in with Google in the past 30 days.

### Example 3: Count devices by status
```powershell
Get-GlicDevices -Status all | Group-Object Status | Select-Object Name, Count
```

## PARAMETERS

### -Config
Path to glic.json. When omitted, the file is resolved automatically from the GLIC_CONFIG environment variable, %ProgramData%\GLic, %APPDATA%\GLic, or the module directory (first match wins).

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

### -Status
Filters devices by lifecycle state. Defaults to 'active'.

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

### GLic.Cmdlets.DeviceRow

## NOTES
Requires the admin.directory.device.chromeos.readonly DWD scope.

## RELATED LINKS
[Get-GlicHardware]()
[Get-GlicTelemetry]()
[about_GLic]()

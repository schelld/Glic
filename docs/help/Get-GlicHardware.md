---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicHardware

## SYNOPSIS
Returns ITAM hardware records for ChromeOS devices, combining Directory and Telemetry API data.

## SYNTAX

```
Get-GlicHardware [-Status <String>] [-OrgUnit <String>] [-Config <String>] [-ServiceAccountPath <String>]
 [<CommonParameters>]
```

## DESCRIPTION
Get-GlicHardware joins the Admin Directory and Chrome Management Telemetry APIs to produce a comprehensive hardware record per device. Each HardwareRow includes serial number, model, manufacturer, CPU model and architecture, RAM (RamGb computed property), total disk capacity (TotalDiskGb), disk health, battery health and cycle count, GPU adapter, network MAC addresses, OS update state, auto-update expiration, and all standard device fields. This is the primary cmdlet for ITAM hardware ingestion.

Use -Status to filter by device lifecycle state. Use -OrgUnit to scope to a specific organizational unit.

## EXAMPLES

### Example 1: Export all hardware to CSV for ITAM
```powershell
Get-GlicHardware | Export-Csv -Path hardware.csv -NoTypeInformation
```

### Example 2: Find devices with less than 4 GB RAM
```powershell
Get-GlicHardware -Status active | Where-Object { $_.RamGb -lt 4 } |
    Select-Object SerialNumber, Model, RamGb | Sort-Object RamGb
```

### Example 3: Scope to a staff OU and export
```powershell
Get-GlicHardware -Status active -OrgUnit '/Staff' | Export-Csv -Path staff-hardware.csv -NoTypeInformation
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
Filters devices to those in the specified organizational unit path (e.g. '/Staff'). When omitted, returns all devices.

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
Filters devices by lifecycle state. Defaults to 'all' to capture deprovisioned assets for ITAM reconciliation.

```yaml
Type: String
Parameter Sets: (All)
Aliases:
Accepted values: all, active, deprovisioned, disabled

Required: False
Position: Named
Default value: all
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### GLic.Cmdlets.HardwareRow

## NOTES
Requires chrome.management.telemetry.readonly and admin.directory.device.chromeos.readonly DWD scopes.

HardwareRow exposes computed properties RamGb (double?), TotalDiskGb (double?), and DiskSizeGb (string) in addition to the raw byte fields RamTotalBytes and TotalDiskBytes.

## RELATED LINKS
[Get-GlicDevices]()
[Get-GlicTelemetry]()
[about_GLic]()

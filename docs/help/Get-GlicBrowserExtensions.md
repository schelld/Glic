---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicBrowserExtensions

## SYNOPSIS
Returns per-profile browser extension inventory from the Chrome Management Reports API.

## SYNTAX

```
Get-GlicBrowserExtensions [-OrgUnit <String>] [-Config <String>] [-ServiceAccountPath <String>]
 [<CommonParameters>]
```

## DESCRIPTION
Get-GlicBrowserExtensions queries the Chrome Management Reports API and returns one BrowserExtensionRow per extension per browser profile. Each row includes the report date, customer ID, profile permanent ID, profile ID, user email, profile org unit ID, app ID, app type, and display name. Use -OrgUnit to scope the query to a specific organizational unit path (requires admin.directory.orgunit.readonly DWD scope).

## EXAMPLES

### Example 1: Export all extension data
```powershell
Get-GlicBrowserExtensions | Export-Csv -Path extensions.csv -NoTypeInformation
```

### Example 2: Find users with a specific extension installed
```powershell
Get-GlicBrowserExtensions | Where-Object { $_.AppId -eq 'cjpalhdlnbpafiamejdnhcphjbkeiagm' } |
    Select-Object Email, DisplayName | Sort-Object Email
```

### Example 3: Top 20 extensions in an IT OU
```powershell
Get-GlicBrowserExtensions -OrgUnit '/IT' |
    Group-Object AppId | Sort-Object Count -Descending | Select-Object -First 20 Name, Count
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
Filters profiles to those in the specified organizational unit path. Requires admin.directory.orgunit.readonly to be granted in the DWD configuration. When omitted, returns data for all profiles.

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

### GLic.Cmdlets.BrowserExtensionRow

## NOTES
Requires chrome.management.reports.readonly DWD scope. The -OrgUnit parameter additionally requires admin.directory.orgunit.readonly.

## RELATED LINKS
[Get-GlicDeviceApps]()
[Get-GlicApps]()
[about_GLic]()

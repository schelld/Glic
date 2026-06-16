---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicManagedBrowsers

## SYNOPSIS
Returns managed Chrome browser profiles from the Chrome Management Profiles API.

## SYNTAX

```
Get-GlicManagedBrowsers [-OrgUnit <String>] [-Config <String>] [-ServiceAccountPath <String>]
 [<CommonParameters>]
```

## DESCRIPTION
Get-GlicManagedBrowsers queries the Chrome Management Profiles API and returns one ManagedBrowserRow per browser profile. Each row includes the profile permanent ID, profile ID, user email, browser version, channel, OS platform, OS version, hostname, machine name, extension count, and last activity time. Use -OrgUnit to scope the query to a specific organizational unit path (requires admin.directory.orgunit.readonly DWD scope).

## EXAMPLES

### Example 1: Export all managed browser profiles
```powershell
Get-GlicManagedBrowsers | Export-Csv -Path managed-browsers.csv -NoTypeInformation
```

### Example 2: Find profiles on outdated browser versions
```powershell
Get-GlicManagedBrowsers | Where-Object { [version]$_.BrowserVersion -lt [version]'120.0' } |
    Select-Object UserEmail, Hostname, BrowserVersion | Sort-Object BrowserVersion
```

### Example 3: Scope to an OU
```powershell
Get-GlicManagedBrowsers -OrgUnit '/IT/Managed' | Select-Object UserEmail, BrowserVersion, ExtensionCount
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
Filters profiles to those in the specified organizational unit path. Requires admin.directory.orgunit.readonly to be granted in the DWD configuration. When omitted, returns profiles across all OUs.

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

### GLic.Cmdlets.ManagedBrowserRow

## NOTES
Requires chrome.management.profiles.readonly DWD scope. The -OrgUnit parameter additionally requires admin.directory.orgunit.readonly.

## RELATED LINKS
[Get-GlicBrowserExtensions]()
[Get-GlicDeviceApps]()
[about_GLic]()

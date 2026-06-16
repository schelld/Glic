---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicUsers

## SYNOPSIS
Returns user accounts from the Google Workspace Admin Directory.

## SYNTAX

```
Get-GlicUsers [-OrgUnit <String>] [-Suspended <String>] [-Config <String>] [-ServiceAccountPath <String>]
 [<CommonParameters>]
```

## DESCRIPTION
Get-GlicUsers queries the Admin Directory API and returns one UserRow per user. Each row includes primary email, given name, family name, org unit, admin status, suspension and archive state, last login time, department, job title, cost center, employee ID, manager email, and aliases. Use -Suspended to filter by suspension state; the default is 'Active'. Use -OrgUnit to scope to a specific organizational unit path.

## EXAMPLES

### Example 1: Export all active users
```powershell
Get-GlicUsers | Export-Csv -Path users.csv -NoTypeInformation
```

### Example 2: Find suspended users
```powershell
Get-GlicUsers -Suspended Suspended | Select-Object PrimaryEmail, FullName, OrgUnit, LastLoginTime
```

### Example 3: Users in a specific OU who have never logged in
```powershell
Get-GlicUsers -OrgUnit '/Staff' | Where-Object { $null -eq $_.LastLoginTime } |
    Select-Object PrimaryEmail, GivenName, FamilyName
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
Filters users to those in the specified organizational unit path (e.g. '/Staff'). When omitted, returns users across all OUs.

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

### -Suspended
Filters users by suspension state. Defaults to 'Active'.

```yaml
Type: String
Parameter Sets: (All)
Aliases:
Accepted values: Active, All, Suspended

Required: False
Position: Named
Default value: Active
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### GLic.Cmdlets.UserRow

## NOTES
Requires admin.directory.user.readonly DWD scope.

## RELATED LINKS
[Get-GlicLicenses]()
[about_GLic]()

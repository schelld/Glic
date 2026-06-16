---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Get-GlicLicenses

## SYNOPSIS
Returns Google Workspace license assignments for all users.

## SYNTAX

```
Get-GlicLicenses [-SkuIds <String[]>] [-Config <String>] [-ServiceAccountPath <String>] [<CommonParameters>]
```

## DESCRIPTION
Get-GlicLicenses queries the Google Licensing API and Admin Directory API to return one LicenseRow per user-SKU assignment. Each row includes the user's email, name, org unit, admin status, suspension state, last login time, and the assigned product name, SKU name, and assignment status. Use -SkuIds to limit results to specific license SKUs; when omitted, all SKUs from skus.json are queried. Run Invoke-GlicDiscover first to populate skus.json with the active SKUs in your Workspace tenant.

## EXAMPLES

### Example 1: Export all license assignments
```powershell
Get-GlicLicenses | Export-Csv -Path licenses.csv -NoTypeInformation
```

### Example 2: Find suspended users with active licenses
```powershell
Get-GlicLicenses | Where-Object { $_.Suspended -eq $true } |
    Select-Object UserEmail, ProductName, SkuName, AssignmentStatus
```

### Example 3: Count assignments per SKU
```powershell
Get-GlicLicenses | Group-Object SkuName | Select-Object Name, Count | Sort-Object Count -Descending
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

### -SkuIds
One or more Google Workspace SKU IDs to query. When omitted, all SKUs defined in skus.json are queried. Run Invoke-GlicDiscover to populate skus.json with your tenant's active SKUs.

```yaml
Type: String[]
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

### GLic.Cmdlets.LicenseRow

## NOTES
Requires apps.licensing and admin.directory.user.readonly DWD scopes.

## RELATED LINKS
[Invoke-GlicDiscover]()
[Get-GlicUsers]()
[about_GLic]()

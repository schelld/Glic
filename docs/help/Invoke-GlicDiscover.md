---
external help file: GLic.dll-Help.xml
Module Name: GLic
online version:
schema: 2.0.0
---

# Invoke-GlicDiscover

## SYNOPSIS
Discovers active Google Workspace license SKUs and writes a local skus.json catalog.

## SYNTAX

```
Invoke-GlicDiscover [-Config <String>] [-ServiceAccountPath <String>] [<CommonParameters>]
```

## DESCRIPTION
Invoke-GlicDiscover queries the Google Licensing API for all SKUs assigned in the Workspace tenant and writes the results to skus.json in the GLic config directory (%ProgramData%\GLic for AllUsers installs or %APPDATA%\GLic for CurrentUser installs). The resulting file is used by Get-GlicLicenses to know which SKUs to query. Run this cmdlet once after installation and again whenever new license SKUs are purchased or removed. Returns one DiscoverChangeRow per SKU that was added, removed, or changed relative to the previous skus.json.

## EXAMPLES

### Example 1: Refresh the SKU catalog
```powershell
Invoke-GlicDiscover
```

Updates skus.json and displays a summary of any changes since the last run.

### Example 2: Check what changed
```powershell
Invoke-GlicDiscover | Format-Table SkuName, Status
```

### Example 3: Alert on new SKUs in a scheduled job
```powershell
Invoke-GlicDiscover | Where-Object { $_.Status -eq 'Added' } | ForEach-Object {
    Write-Host "New SKU detected: $($_.SkuName) ($($_.SkuId))"
}
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None

## OUTPUTS

### GLic.Cmdlets.DiscoverChangeRow

## NOTES
Requires apps.licensing DWD scope. Always writes skus.json to the config directory, never to the module folder, so refreshes work without admin rights and survive module updates.

## RELATED LINKS
[Get-GlicLicenses]()
[about_GLic]()

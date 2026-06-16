# Manual Test: AllUsers Elevated Install

**When to run:** Before any release that touches `install.ps1` or `uninstall.ps1`.
**Prerequisite:** An elevated PowerShell 5.1 session on a machine with a working
`service-account.json` and `glic.json` (or supply all params directly).

## Steps

1. Build and stage the module:
   ```powershell
   dotnet build --nologo
   ```

2. Run the AllUsers install (adjust paths):
   ```powershell
   powershell -NoProfile -File module\GLic\install.ps1 `
       -Scope AllUsers `
       -CustomerId C03xxxxx `
       -AdminEmail glic-svc@yourdomain.com `
       -ServiceAccountPath C:\keys\sa.json `
       -NoVerify
   ```
   **Expected:** `GLic <version> installed for AllUsers`, exit 0.

3. Verify the versioned module folder:
   ```powershell
   $ver = (Import-PowerShellDataFile module\GLic\GLic.psd1).ModuleVersion
   Test-Path "C:\Program Files\WindowsPowerShell\Modules\GLic\$ver\GLic.psd1"
   Test-Path "C:\Program Files\WindowsPowerShell\Modules\GLic\$ver\uninstall.ps1"
   ```
   **Expected:** both `True`.

4. Verify key placement and ACL:
   ```powershell
   Test-Path "C:\ProgramData\GLic\service-account.json"
   icacls "C:\ProgramData\GLic\service-account.json"
   ```
   **Expected:** file exists; icacls output shows SYSTEM and BUILTIN\Administrators with F/R,
   and NO entry for Authenticated Users or the current non-admin user.

5. Verify install.ps1 and credentials are NOT in the module folder:
   ```powershell
   Get-ChildItem "C:\Program Files\WindowsPowerShell\Modules\GLic" -Recurse |
       Where-Object Name -in 'install.ps1','service-account.json','glic.json'
   ```
   **Expected:** no output (install.ps1 is excluded by the installer; credentials go to ProgramData).

6. Import and smoke-test from a fresh non-elevated shell:
   ```powershell
   powershell -NoProfile -Command "
       Import-Module GLic
       (Get-Module GLic).Version.ToString()
       Get-GlicDevices | Select-Object -First 1 | Format-List DeviceId, Status
   "
   ```
   **Expected:** version string, one device record. Standard user can import because the module
   DLLs are readable; the key ACL prevents them from reading service-account.json (which is
   correct — GLic reads it as the elevated service identity, not the interactive user).

   NOTE: If standard users need to run GLic against the machine-wide key, the admin must
   re-run `install.ps1 -Scope AllUsers -ReadAccessAccount 'DOMAIN\username'`.

7. Uninstall and verify cleanup:
   ```powershell
   # Must be run elevated
   powershell -NoProfile -File uninstall.ps1 -Scope AllUsers -RemoveConfig
   Test-Path "C:\Program Files\WindowsPowerShell\Modules\GLic"
   Test-Path "C:\ProgramData\GLic"
   ```
   **Expected:** both `False`.

## CI Coverage (Future — Spec F)

A GitHub Actions job with `runs-on: windows-latest` and `shell: powershell` can exercise
steps 1–6 above using a mock service-account.json and -NoVerify. The icacls check (step 4)
requires the runner to have admin rights, which hosted runners on windows-latest do provide.
Tracking: add `smoke-allUsers` job to the PR workflow in spec F.

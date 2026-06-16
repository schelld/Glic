$moduleDir = $PSScriptRoot

# Store module dir in AppDomain so the delegate can read it without
# relying on PowerShell scope capture (which is unreliable in PS 5.1 delegates).
[System.AppDomain]::CurrentDomain.SetData('GLic.ModuleDir', $moduleDir)

# PowerShell ignores GLic.dll.config binding redirects; handle them here.
$null = [System.AppDomain]::CurrentDomain.add_AssemblyResolve(
    [System.ResolveEventHandler] {
        param ($sender, $e)
        $dir = [System.AppDomain]::CurrentDomain.GetData('GLic.ModuleDir')
        $asmName = ([System.Reflection.AssemblyName] $e.Name).Name
        $candidate = Join-Path $dir "$asmName.dll"
        if (Test-Path $candidate) {
            return [System.Reflection.Assembly]::LoadFrom($candidate)
        }
        return $null
    }
)

Import-Module (Join-Path $moduleDir 'GLic.dll')

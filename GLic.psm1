$fwSubdir = if ($PSEdition -eq 'Core') { 'net8.0' } else { 'net472' }
$fwDir = Join-Path $PSScriptRoot $fwSubdir

# Store the framework-specific dir so the delegate can find dependency DLLs.
# PS 5.1 ignores GLic.dll.config binding redirects, so we resolve them here.
[System.AppDomain]::CurrentDomain.SetData('GLic.ModuleDir', $fwDir)

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

Import-Module (Join-Path $fwDir 'GLic.dll')

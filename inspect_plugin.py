import subprocess

script = r'''
$core = 'c:\000\projects\compositum\bin\Release\net8.0-windows\Vantuz.Core.dll'
$net = 'c:\000\projects\compositum\bin\Release\net8.0-windows\plugins\Vantuz.Plugins.Net.dll'

function Load-Assembly($path) {
    try { return [System.Reflection.Assembly]::LoadFrom($path) } catch { return $null }
}

Load-Assembly $core | Out-Null
$asm = Load-Assembly $net
if (-not $asm) { Write-Output 'FAIL load'; exit 1 }
$names = @()
foreach ($type in $asm.GetTypes()) {
    $prop = $type.GetProperty('Name')
    if ($prop -and $prop.PropertyType -eq [string]) {
        try {
            $ctor = $type.GetConstructor([Type[]]@())
            if ($ctor) {
                $inst = $ctor.Invoke($null)
                $names += $prop.GetValue($inst)
            }
        } catch {}
    }
}
ConvertTo-Json $names
'''
r = subprocess.run(['powershell.exe','-ExecutionPolicy','Bypass','-Command',script], capture_output=True, text=True)
print('STDOUT:', r.stdout)
print('STDERR:', r.stderr)
print('EXIT:', r.returncode)

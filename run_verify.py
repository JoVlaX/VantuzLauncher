import subprocess, sys
r = subprocess.run(
    ['powershell.exe', '-ExecutionPolicy', 'Bypass', '-File',
     'c:/000/projects/compositum/verify-plugin-names.ps1',
     '-BootJsonPath', 'c:/000/projects/compositum/bin/Release/net8.0-windows/boot.json',
     '-PluginsDir', 'c:/000/projects/compositum/bin/Release/net8.0-windows/plugins'],
    capture_output=True, text=True, cwd='c:/000/projects/compositum'
)
print(r.stdout)
print(r.stderr, file=sys.stderr)
print('EXIT:', r.returncode)

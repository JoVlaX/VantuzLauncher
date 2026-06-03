import subprocess, sys
r = subprocess.run(
    ['powershell.exe', '-ExecutionPolicy', 'Bypass', '-File',
     'c:/000/projects/compositum/verify-plugin-names.ps1',
     '-BootJsonPath', 'c:/000/projects/compositum/bin/Release/net8.0-windows/boot.json',
     '-PluginsDir', 'c:/000/projects/compositum/bin/Release/net8.0-windows/plugins'],
    capture_output=True, text=True, cwd='c:/000/projects/compositum'
)
with open('c:/000/projects/compositum/verify_stdout.txt','w',encoding='utf-8') as f:
    f.write(r.stdout)
with open('c:/000/projects/compositum/verify_stderr.txt','w',encoding='utf-8') as f:
    f.write(r.stderr)
print('EXIT', r.returncode)

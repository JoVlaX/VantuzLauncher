import subprocess, os
builder_dir = 'c:/000/projects/compositum/Vantuz.Builder'
r = subprocess.run(
    ['dotnet', 'run', '--', 'verify',
     'c:/000/projects/compositum/bin/Release/net8.0-windows/boot.json',
     'c:/000/projects/compositum/bin/Release/net8.0-windows/plugins'],
    cwd=builder_dir
)
print('exit', r.returncode)
print(r.stdout)
print(r.stderr)

import subprocess
r = subprocess.run(
    ['dotnet', 'run', '--project', 'c:/000/projects/compositum/Vantuz.Builder/Vantuz.Builder.csproj',
     '--', 'verify',
     'c:/000/projects/compositum/bin/Release/net8.0-windows/boot.json',
     'c:/000/projects/compositum/bin/Release/net8.0-windows/plugins'],
    cwd='c:/000/projects/compositum'
)
print('exit', r.returncode)
print(r.stdout)
print(r.stderr)

import subprocess
r = subprocess.run(
    ['dotnet', 'run', '-c', 'Release', '--project', 'c:/000/projects/compositum/Vantuz.Builder/Vantuz.Builder.csproj',
     '--', 'verify',
     'c:/000/projects/compositum/bin/Release/net8.0-windows/boot.json',
     'c:/000/projects/compositum/bin/Release/net8.0-windows/plugins'],
    cwd='c:/000/projects/compositum', capture_output=True, text=True
)
with open('c:/000/projects/compositum/verify_release_out.txt','w',encoding='utf-8') as f:
    f.write(r.stdout)
with open('c:/000/projects/compositum/verify_release_err.txt','w',encoding='utf-8') as f:
    f.write(r.stderr)
print('exit', r.returncode)

import subprocess
r = subprocess.run(
    ['dotnet', 'run', '-c', 'Release', '--project', 'c:/000/projects/compositum/Vantuz.Builder/Vantuz.Builder.csproj'],
    cwd='c:/000/projects/compositum', capture_output=True, text=True
)
with open('c:/000/projects/compositum/noverify_out.txt','w',encoding='utf-8') as f:
    f.write(r.stdout)
with open('c:/000/projects/compositum/noverify_err.txt','w',encoding='utf-8') as f:
    f.write(r.stderr)
print('exit', r.returncode)

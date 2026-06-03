import subprocess
r = subprocess.run(
    ['dotnet', 'build', 'c:/000/projects/compositum/Vantuz.Builder/Vantuz.Builder.csproj', '-c', 'Release'],
    cwd='c:/000/projects/compositum', capture_output=True, text=True
)
with open('c:/000/projects/compositum/build_builder_out.txt','w',encoding='utf-8') as f:
    f.write(r.stdout)
with open('c:/000/projects/compositum/build_builder_err.txt','w',encoding='utf-8') as f:
    f.write(r.stderr)
print('exit', r.returncode)

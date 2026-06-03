import subprocess
r = subprocess.run(
    ['dotnet', 'build', 'c:/000/projects/compositum/Vantuz.Builder/Vantuz.Builder.csproj', '-c', 'Release'],
    cwd='c:/000/projects/compositum', stdout=subprocess.PIPE, stderr=subprocess.PIPE
)
with open('c:/000/projects/compositum/build_builder_stdout.txt','wb') as f:
    f.write(r.stdout or b'')
with open('c:/000/projects/compositum/build_builder_stderr.txt','wb') as f:
    f.write(r.stderr or b'')
print('exit', r.returncode)

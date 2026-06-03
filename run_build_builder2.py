import subprocess, sys
r = subprocess.run(
    ['dotnet', 'build', 'c:/000/projects/compositum/Vantuz.Builder/Vantuz.Builder.csproj', '-c', 'Release'],
    cwd='c:/000/projects/compositum', stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True
)
print(r.stdout)
print(r.stderr, file=sys.stderr)
print('exit', r.returncode)

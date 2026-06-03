import subprocess
r = subprocess.run(
    ['dotnet', 'clean', 'c:/000/projects/compositum/VantuzLauncher.sln', '-c', 'Release'],
    cwd='c:/000/projects/compositum'
)
print('clean exit', r.returncode)

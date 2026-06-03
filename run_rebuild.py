import subprocess
r = subprocess.run(
    ['dotnet', 'build', 'c:/000/projects/compositum/VantuzLauncher.sln', '-c', 'Release'],
    cwd='c:/000/projects/compositum'
)
print('build exit', r.returncode)

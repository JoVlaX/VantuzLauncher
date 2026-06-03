import subprocess, sys
r = subprocess.run(
    ['dotnet', 'build', 'c:/000/projects/compositum/VantuzLauncher.sln', '-c', 'Release'],
    capture_output=True, text=True, cwd='c:/000/projects/compositum'
)
with open('c:/000/projects/compositum/build_out.txt','w',encoding='utf-8') as f:
    f.write(r.stdout)
with open('c:/000/projects/compositum/build_err.txt','w',encoding='utf-8') as f:
    f.write(r.stderr)
print('EXIT', r.returncode)

import subprocess
r = subprocess.run(['dotnet', '--version'], cwd='c:/000/projects/compositum/Vantuz.Builder', capture_output=True, text=True)
print('ver:', r.stdout)
print('err:', r.stderr)
print('exit:', r.returncode)

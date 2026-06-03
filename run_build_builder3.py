import subprocess, sys
r = subprocess.run(
    ['dotnet', 'build', 'c:/000/projects/compositum/Vantuz.Builder/Vantuz.Builder.csproj', '-c', 'Release'],
    cwd='c:/000/projects/compositum', stdout=subprocess.PIPE, stderr=subprocess.PIPE
)
print('STDOUT:', r.stdout.decode('utf-8', errors='replace') if r.stdout else '')
print('STDERR:', r.stderr.decode('utf-8', errors='replace') if r.stderr else '', file=sys.stderr)
print('exit', r.returncode)

import subprocess
r = subprocess.run(['dotnet', 'run'], cwd='c:/000/projects/compositum/Vantuz.Builder', capture_output=True, text=True)
with open('c:/000/projects/compositum/dotnet_run_raw_out.txt','w',encoding='utf-8') as f:
    f.write(r.stdout)
with open('c:/000/projects/compositum/dotnet_run_raw_err.txt','w',encoding='utf-8') as f:
    f.write(r.stderr)
print('exit', r.returncode)

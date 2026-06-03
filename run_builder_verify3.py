import subprocess, os, sys
builder_dir = 'c:/000/projects/compositum/Vantuz.Builder'
print('DIR EXISTS:', os.path.isdir(builder_dir))
print('FILES:', os.listdir(builder_dir)[:10])
cmd = ['dotnet', 'run', '--', 'verify',
       'c:/000/projects/compositum/bin/Release/net8.0-windows/boot.json',
       'c:/000/projects/compositum/bin/Release/net8.0-windows/plugins']
print('CMD:', cmd)
print('CWD:', builder_dir)
r = subprocess.run(cmd, cwd=builder_dir, capture_output=True, text=True)
with open('c:/000/projects/compositum/verify3_out.txt','w',encoding='utf-8') as f:
    f.write(r.stdout)
with open('c:/000/projects/compositum/verify3_err.txt','w',encoding='utf-8') as f:
    f.write(r.stderr)
print('EXIT', r.returncode)

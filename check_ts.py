import os, datetime
p = 'c:/000/projects/compositum/Vantuz.Builder/bin/Release/net8.0/Vantuz.Builder.dll'
print('exists', os.path.exists(p))
if os.path.exists(p):
    print('mtime', datetime.datetime.fromtimestamp(os.path.getmtime(p)))
    print('size', os.path.getsize(p))

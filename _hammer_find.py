import os

out = []
def log(s):
    out.append(s)

root = 'C:/hades/Hecton8'
# Focus on the two files of interest + BuoyancyJobs
targets = ['HectonFluidEngine', 'HectonVoxelEngine', 'Buoyancy', 'BuoyancyJobs']
for r, d, fs in os.walk(root):
    # skip heavy/unrelated dirs
    skip = ('/Library/', '/Temp/', '/obj/', '/node_modules/', '/Packages/',
            '/ProjectSettings/', '/Logs/', '/Build/', '\\Library\\', '\\Temp\\',
            '\\obj\\', '\\node_modules\\', '\\Packages\\')
    if any(s in r for s in skip):
        continue
    for f in fs:
        if f.endswith('.cs'):
            for t in targets:
                if t.lower() in f.lower():
                    log(os.path.join(r, f))
                    break

result = '\n'.join(logged for logged in out) if out else 'NO MATCHES'
open('C:/hades/Hecton8/_hammer_find_out.txt', 'w').write(result)

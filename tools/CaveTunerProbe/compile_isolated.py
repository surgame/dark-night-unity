from pathlib import Path
import subprocess, json, sys
original, candidate, output = map(Path, sys.argv[1:4])
output.mkdir(parents=True, exist_ok=True)
rspdir = original / 'Game/Library/Bee/artifacts/1900b0aE.dag'
sdk = subprocess.check_output(['dotnet','--version'],text=True).strip()
csc = Path('C:/Program Files/dotnet/sdk') / sdk / 'Roslyn/bincore/csc.dll'
results = []
for name in (sys.argv[4:] or ['DarkNights.Core','DarkNights.View','DarkNights.Editor']):
    lines = []
    for line in (rspdir/(name+'.rsp')).read_text(encoding='utf-8-sig').splitlines():
        if line.startswith('-out:'): line = '-out:"'+str(output/(name+'.dll'))+'"'
        elif line.startswith('-refout:'): line = '-refout:"'+str(output/(name+'.ref.dll'))+'"'
        elif line.startswith('"Assets/') and line.endswith('.cs"'):
            line = '"'+str(candidate/'Game'/line.strip('"'))+'"'
        elif line.startswith('-r:'):
            for dep in ['DarkNights.Core','DarkNights.View']:
                if (output/(dep+'.ref.dll')).exists() and line.endswith('/'+dep+'.ref.dll"'):
                    line = '-r:"'+str(output/(dep+'.ref.dll'))+'"'
    
        lines.append(line)
    rsp = output/(name+'.rsp'); rsp.write_text('\n'.join(lines),encoding='utf-8')
    completed = subprocess.run(['dotnet',str(csc),'@'+str(rsp)],cwd=original/'Game',capture_output=True,text=True,encoding='utf-8',errors='replace')
    (output/(name+'.log')).write_text(completed.stdout+completed.stderr,encoding='utf-8')
    results.append({'assembly':name,'exit':completed.returncode})
    if completed.returncode: break
(output/'compile.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print(json.dumps(results))

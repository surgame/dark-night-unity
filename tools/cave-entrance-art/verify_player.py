"""Reuse one Mono Player for real-camera background Ready, late join and reconnect."""
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import traceback

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('journey_probe', ROOT/'tools/space-planet-flow/test_network.py')
journey_probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(journey_probe)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--player',type=Path,required=True)
    parser.add_argument('--weak',action='store_true')
    parser.add_argument('--port',type=int,default=29560)
    parser.add_argument('--max-active-clients',type=int,choices=(1,2),default=2)
    args = parser.parse_args()
    args.backend='mono'; args.clients=1; args.driver='host'; args.save_version=15
    args.output_root=ROOT/'artifacts/art-layer-entrance-20261003'
    args.interactive=False; args.keep_open=None; args.phase_hook=False
    run = journey_probe.NetworkRun(args)
    error = None
    try:
        if args.weak:
            run.relay = subprocess.Popen([sys.executable,str(ROOT/'tools/lan-netem.py'),
                '--listen',str(args.port+1),'--target',str(args.port),'--loss','.05',
                '--delay','.1','--jitter','.025','--duration','900','--report',str(run.run/'relay.json')],
                creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
        run.start('host'); run.start('client1')
        run.consistency('orbit',journey_probe.PHASES['Orbit'])
        run.walk('host',journey_probe.ship_x(run.ready('host'))+96)
        run.expect('host','select actual planet',**run.request('host'))
        run.wait('host',lambda r:r['ready'] and journey_probe.phase(r)==journey_probe.PHASES['Descent'],'arrival',150)
        run.consistency('surface-background-ready',journey_probe.PHASES['Descent'])
        run.automatic_landing('existing-layer landing')
        run.walk('host',journey_probe.ship_x(run.ready('host'))-192)
        run.capture('existing-layers-landed')
        if args.max_active_clients == 1:
            run.stop('client1'); run.roles.remove('client1')
        run.roles.append('client2'); run.start('client2')
        run.consistency('late-join-landed',journey_probe.PHASES['Landed'])
        run.reconnect('client2' if args.max_active_clients == 1 else 'client1','landed',journey_probe.PHASES['Landed'])
        run.consistency('reconnected-background',journey_probe.PHASES['Landed'])
        for path in run.run.glob('*.log'):
            failures=[line for line in path.read_text(encoding='utf-8',errors='replace').splitlines()
                      if any(term in line for term in ('Exception:','MissingMethodException','TypeLoadException'))]
            run.record(path.name+' no runtime exception',not failures,failures[:6])
    except Exception:
        error=traceback.format_exc()
    finally:
        for role in list(run.processes): run.stop(role)
        if run.relay:
            (run.run/'relay.json.stop').write_text('',encoding='utf-8')
            try: run.relay.wait(8)
            except subprocess.TimeoutExpired:
                run.relay.terminate(); run.relay.wait(5)
        result={'passed':error is None,'checks':run.checks,'error':error,'weak':args.weak,
                'player':str(run.player),'player_sha256':journey_probe.sha256(run.player),
                'driver_sha256':journey_probe.sha256(Path(__file__)), 'max_active_players':args.max_active_clients+1,
                'managed_sha256':{p.name:journey_probe.sha256(p) for p in (run.player.parent/'DarkNights_Data/Managed').glob('DarkNights.*.dll')},
                'not_covered':['IL2CPP','two machines','foreground performance','whole campaign']}
        target=run.run/'result.json'
        target.write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
        run.write_status('complete' if error is None else 'failed',result=str(target))
        print(json.dumps({'passed':result['passed'],'checks':len(run.checks),'error':error,'result':str(target)},ensure_ascii=False),flush=True)
    return 0 if error is None else 1

if __name__=='__main__': sys.exit(main())

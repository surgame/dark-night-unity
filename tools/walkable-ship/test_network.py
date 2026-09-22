"""Exercise a single Mono Player in independent Host/Client processes; keep all saves isolated.

The second half loads an explicitly labelled purchased-module fixture to test real deployment;
that fixture does not count as earning the module price through gameplay.
"""
import argparse
import hashlib
import json
import subprocess
import sys
import time
import traceback
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--weak', action='store_true')
p.add_argument('--port', type=int, default=29060)
p.add_argument('--player', type=Path, help='Reuse the same checks against an explicitly selected Mono build')
p.add_argument('--output-root', type=Path, help='Keep this run separate from frozen ship evidence')
a = p.parse_args()
repo = Path(__file__).resolve().parents[2]
player = a.player.resolve() if a.player else repo / 'artifacts/walkable-ship/player-mono/DarkNights.exe'
output_root = a.output_root.resolve() if a.output_root else repo / 'artifacts/walkable-ship'
run = output_root / ('network-' + time.strftime('%Y%m%d-%H%M%S') + ('-weak' if a.weak else '-normal'))
run.mkdir(parents=True)
saves = run / 'saves'
processes = {}
checks = {}
relay = None
error = None

def report(role):
    for attempt in range(10):
        try:
            data = json.loads((run / (role + '.json')).read_text(encoding='utf-8-sig'))
            if data.get('error'): raise RuntimeError(data['error'])
            return data
        except (OSError, json.JSONDecodeError): time.sleep(.02)
    return None

def wait(role, predicate, description, seconds=60):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        value = report(role)
        if value and predicate(value): return value
        if processes[role].poll() is not None: raise RuntimeError(role + ' exited: ' + str(processes[role].returncode))
        time.sleep(.2)
    raise TimeoutError(role + ': ' + description)

def start(role):
    (run / (role + '.commands')).write_text('', encoding='utf-8')
    endpoint = a.port + 1 if a.weak and role != 'host' else a.port
    args = [str(player), '-batchmode', '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
        '-logFile', str(run / (role + '.log')), '--dn-role', role, '--dn-input-replay', '--dn-port', str(endpoint), '--dn-map-seed', 'SHIP-0922',
        '--dn-save-dir', str(saves), '--dn-report', str(run / (role + '.json')), '--dn-commands', str(run / (role + '.commands'))]
    if role != 'host': args.append('-nographics')
    processes[role] = subprocess.Popen(args, creationflags=subprocess.CREATE_NO_WINDOW)
    return wait(role, lambda r: r['ready'] and r['terrain']['visible'], 'initial Ready')

def send(role, **command):
    with (run / (role + '.commands')).open('a', encoding='utf-8') as f: f.write(json.dumps(command) + '\n')

def actor(r, slot=None):
    return next(v for v in r['frame']['World']['Actors'] if v['ControllerSlot'] == (r['slot'] if slot is None else slot))

def flight(r): return r['frame']['World']['Expedition']['Ship']
def device(r): return next(v for v in r['frame']['World']['Expedition']['Devices'] if v['Id'] == flight(r)['Id'])
def shipx(r): return next(v['X'] for v in r['frame']['World']['Buildings'] if v['Kind'] == 'ship')
def check(name, ok):
    if name in checks:
        base = name; occurrence = 2
        while name in checks:
            name = base + '_' + str(occurrence); occurrence += 1
    checks[name] = bool(ok)
    if not ok: raise AssertionError(name)

def receipt(role, **command):
    before = wait(role, lambda r: r['ready'], 'command Ready')
    generation = max((v['ConnectionGeneration'] for v in before['feedback'] if v['ReadyReply']), default=1)
    def current(v):
        return not v['ReadyReply'] and v['Epoch'] == before['epoch'] and v['ConnectionGeneration'] == generation
    prior = max((v['Sequence'] for v in before['feedback'] if current(v)), default=0)
    send(role, **command)
    after = wait(role, lambda r: any(current(v) and v['Sequence'] > prior for v in r['feedback']), 'receipt ' + str(command))
    return next(v for v in after['feedback'] if current(v) and v['Sequence'] > prior)

def action(role, kind, expected='Applied', name=None):
    r = wait(role, lambda r: r['ready'], 'personal command'); person = actor(r)
    result = receipt(role, operation='Expedition', kind=kind, actors=[person['Id']], lease=person['ControlLease'])
    check(name or role + '_' + kind, result['Code'] == expected)
    return result

def hold(role, horizontal=0, up=False, down=False):
    r = wait(role, lambda r: r['ready'], 'input Ready')
    send(role, operation='input-hold', actor=actor(r)['Id'], horizontal=horizontal, jumpHeld=up, dropHeld=down)

def walk(role, x):
    deadline = time.monotonic() + 55
    direction = None
    while time.monotonic() < deadline:
        r = wait(role, lambda v: v['ready'] and bool(v.get('frame')), 'walk frame'); dx = x - actor(r)['X']
        wanted = 0 if abs(dx) < 10 else (1 if dx > 0 else -1)
        if direction != wanted: hold(role, wanted); direction = wanted
        if wanted == 0:
            time.sleep(.8)
            stable = wait(role, lambda v: v['ready'] and bool(v.get('frame')), 'walk stop frame')
            if abs(x - actor(stable)['X']) <= 15: return
        time.sleep(.2)
    raise TimeoutError('walk ' + role + ' to ' + str(x))

def snapshot(name):
    send('host', operation='capture', file=name + '.png')
    wait('host', lambda r: (run / (name + '.png')).exists(), 'capture ' + name)

try:
    if a.weak:
        relay = subprocess.Popen([sys.executable, str(repo / 'tools/lan-netem.py'), '--listen', str(a.port + 1), '--target', str(a.port),
            '--loss', '.05', '--delay', '.1', '--jitter', '.025', '--duration', '900', '--report', str(run / 'relay.json')], creationflags=subprocess.CREATE_NO_WINDOW)
    h = start('host'); c = start('client')
    check('same_current_terrain', h['terrain']['sha256'] == c['terrain']['sha256'] and bool(h['terrain']['backgroundHash']))
    x = shipx(h)
    snapshot('00-docked-exterior')
    action('client', 'pilot', 'NoEffect', 'seat_requires_actual_cockpit')
    walk('host', x - 40); walk('client', x + 96)
    action('client', 'pilot')
    c = wait('client', lambda r: flight(r)['PilotId'] == actor(r)['Id'], 'guest holds seat')
    check('guest_can_drive_shared_ship', flight(c)['PilotId'] == actor(c)['Id'])
    bad = actor(c)
    check('foreign_lease_rejected', receipt('host', operation='Expedition', kind='takeoff', actors=[bad['Id']], lease=bad['ControlLease'])['Code'] == 'PermissionDenied')
    action('client', 'takeoff'); wait('host', lambda r: flight(r)['Phase'] == 3, 'closed doors')
    hold('client', up=True); wait('host', lambda r: device(r)['Height'] > 30, 'actual lift')
    send('client', operation='input-stop')
    h = wait('host', lambda r: flight(r)['VelocityY'] == 0 and device(r)['Height'] > 30, 'input expiry brakes')
    check('passenger_moves_with_ship', abs(actor(h)['Height'] - device(h)['Height'] - 40) < .1)
    check('timeout_stops_thrust', flight(h)['VelocityY'] == 0)
    snapshot('01-airborne-cutaway')
    late = start('late')
    check('late_join_inside_moving_ship', abs(actor(late)['Height'] - device(late)['Height'] - 40) < .1)
    old_id = actor(report('client'))['Id']
    send('client', operation='disconnect'); wait('client', lambda r: not r['ready'], 'disconnect')
    h = wait('host', lambda r: flight(r)['PilotId'] == 0 and flight(r)['VelocityY'] == 0, 'driver lease revoked')
    check('disconnect_releases_driver', flight(h)['PilotId'] == 0)
    send('client', operation='connect'); c = wait('client', lambda r: r['ready'], 'reconnect')
    check('reconnect_keeps_passenger_identity', actor(c)['Id'] == old_id)
    action('client', 'pilot')
    check('airborne_departure_rejected', action('client', 'pilot', 'NoEffect', 'no_midair_disembark')['Code'] == 'NoEffect')
    receipt('host', operation='SetPaused', value=1); receipt('host', operation='Save', value=0)
    saved_path = saves / 'v10/slot-00.dnsave.json'
    h = wait('host', lambda r: not r['storageBusy'] and saved_path.exists(), 'airborne disk save')
    saved_height = device(h)['Height']; epoch = h['epoch']
    send('host', operation='BeginLoad', value=0)
    h = wait('host', lambda r: r['ready'] and r['epoch'] > epoch, 'airborne load')
    check('load_keeps_ship_position', abs(device(h)['Height'] - saved_height) < .01)
    check('load_clears_driver_and_velocity', flight(h)['PilotId'] == 0 and flight(h)['VelocityY'] == 0)
    receipt('host', operation='SetPaused', value=0)
    wait('client', lambda r: r['ready'] and r['epoch'] == h['epoch'], 'client after load')
    action('client', 'pilot'); hold('client', down=True)
    wait('host', lambda r: device(r)['Height'] < 1, 'ground descent')
    hold('client'); wait('host', lambda r: flight(r)['VelocityY'] == 0, 'descent stop')
    action('client', 'land'); action('client', 'pilot', name='leave_seat_after_landing')
    snapshot('02-landed-cutaway')
    # A purchased-module fixture isolates deployment from economic progression.
    receipt('host', operation='SetPaused', value=1); receipt('host', operation='Save', value=1)
    module_path = saves / 'v10/slot-01.dnsave.json'
    wait('host', lambda r: not r['storageBusy'] and module_path.exists(), 'module seed save')
    fixture = json.loads(module_path.read_text(encoding='utf-8-sig'))
    fixture['world']['expedition']['RobotModule'] = 1
    module_path.write_text(json.dumps(fixture, ensure_ascii=False), encoding='utf-8')
    epoch = report('host')['epoch']; send('host', operation='BeginLoad', value=1)
    wait('host', lambda r: r['ready'] and r['epoch'] > epoch, 'module fixture load')
    receipt('host', operation='SetPaused', value=0)
    check('deploy_command_accepted', receipt('host', operation='Expedition', kind='depart')['Code'] == 'Applied')
    h = wait('host', lambda r: any(v['Role'] == 4 and not v['Boarded'] and v['TaskPhase'] == 2 for v in r['frame']['World']['Expedition']['Crew']), 'drone exits hatch')
    h = wait('host', lambda r: any(v['Id'] != flight(r)['Id'] and v['Stage'] == 3 for v in r['frame']['World']['Expedition']['Devices']), 'robot deploys device')
    check('robot_and_drone_work_outside', any(v['Role'] == 1 and not v['Boarded'] for v in h['frame']['World']['Expedition']['Crew']))
    snapshot('03-robot-and-scout-working')
    wait('client', lambda r: r['ready'] and r['epoch'] == h['epoch'], 'driver after module load')
    walk('client', x + 96); action('client', 'pilot'); action('client', 'takeoff', name='recall_working_units')
    h = wait('host', lambda r: flight(r)['Phase'] == 3, 'real device recall', seconds=180)
    check('all_devices_stowed_before_flight', all(v['Stage'] == 6 for v in h['frame']['World']['Expedition']['Devices'] if v['Id'] != flight(h)['Id']))
    check('robot_and_drone_boarded_before_flight', all(v['Boarded'] for v in h['frame']['World']['Expedition']['Crew'] if v['Role'] in (1, 4)))
    for role in processes:
        text = (run / (role + '.log')).read_text(encoding='utf-8', errors='replace')
        check(role + '_no_runtime_exception', not any(v in text for v in ['Exception:', 'InvalidKeyException', 'MissingMethodException', 'TypeLoadException']))
except Exception:
    error = traceback.format_exc()
finally:
    for process in processes.values():
        if process.poll() is None: process.terminate()
        try: process.wait(5)
        except subprocess.TimeoutExpired: process.kill()
    if relay:
        (run / 'relay.json.stop').write_text('')
        try: relay.wait(5)
        except subprocess.TimeoutExpired: relay.terminate()
    result = dict(passed=error is None, checks=checks, error=error, weak=a.weak, player=str(player),
        player_sha256=hashlib.sha256(player.read_bytes()).hexdigest(), purchased_module_fixture=True,
        managed_sha256={v.name: hashlib.sha256(v.read_bytes()).hexdigest() for v in (player.parent / 'DarkNights_Data/Managed').glob('DarkNights.*.dll')})
    (run / 'result.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
    print(json.dumps(dict(passed=error is None, checks=len(checks), error=error, report=str(run / 'result.json')), ensure_ascii=False))
sys.exit(0 if error is None else 1)

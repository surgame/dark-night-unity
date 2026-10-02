"""Bounded hidden, silent startup probe using the shipped read-only AMP1 bridge."""
import argparse
import json
from pathlib import Path
import secrets
import subprocess
import sys
import threading
import time

from background_network import HiddenParent, OffscreenRun
from diagnose_startup import read_bridge


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--player', required=True, type=Path)
    parser.add_argument('--output-root', required=True, type=Path)
    parser.add_argument('--port', type=int, default=29540)
    args = parser.parse_args()
    args.backend, args.clients, args.driver = 'mono', 1, 'client'
    args.weak, args.interactive, args.phase_hook = True, False, True
    args.save_version, args.skip_restarts, args.keep_open = 16, True, None
    run = OffscreenRun(args)
    parent = HiddenParent()
    token = secrets.token_hex(24)
    original = subprocess.Popen
    stopped = threading.Event()
    bridges = {}

    def launch(command, *values, **options):
        if Path(command[0]).resolve() == args.player.resolve():
            role = command[command.index('--dn-role') + 1]
            port = args.port + 100 + (0 if role == 'host' else 1)
            command = [*command, '-parentHWND', str(parent.window), 'delayed',
                       '--ard-map-debug-port', str(port), '--ard-map-debug-token', token]
            startup = subprocess.STARTUPINFO()
            startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            startup.wShowWindow = 0
            options['startupinfo'] = startup
            options['creationflags'] = options.get('creationflags', 0) | subprocess.BELOW_NORMAL_PRIORITY_CLASS
            process = original(command, *values, **options)
            bridges[role] = port
            threading.Thread(target=parent.monitor, args=(process,), daemon=True).start()
            return process
        return original(command, *values, **options)

    def sample():
        with (run.run / 'amp1.jsonl').open('w', encoding='utf-8') as stream:
            while not stopped.wait(1):
                for role, port in list(bridges.items()):
                    stream.write(json.dumps({'elapsed': round(time.monotonic() - started, 2),
                        'role': role, 'bridge': read_bridge(port, token)}) + '\n')
                stream.flush()

    subprocess.Popen = launch
    started = time.monotonic()
    worker = threading.Thread(target=sample, daemon=True)
    worker.start()
    error = None
    print(json.dumps({'stage': 'running', 'output': str(run.run)}), flush=True)
    try:
        run.relay = subprocess.Popen([sys.executable, str(run.repo / 'tools/lan-netem.py'),
            '--listen', str(args.port + 1), '--target', str(args.port), '--loss', '.05',
            '--delay', '.1', '--jitter', '.025', '--duration', '180',
            '--report', str(run.run / 'relay.json')], creationflags=subprocess.CREATE_NO_WINDOW)
        run.start('host')
        run.start('client1')
        run.record('weak startup ready', True)
    except Exception as exception:
        error = repr(exception)
    finally:
        for role in list(run.processes):
            run.save_report(role, 'diagnostic-final')
            run.stop(role)
        if run.relay:
            (run.run / 'relay.json.stop').write_text('', encoding='utf-8')
            try:
                run.relay.wait(8)
            except subprocess.TimeoutExpired:
                run.relay.terminate()
                run.relay.wait(5)
        stopped.set()
        worker.join(3)
        subprocess.Popen = original
        parent.close()
        result = {'passed': error is None, 'error': error, 'duration': round(time.monotonic() - started, 2),
            'player': str(args.player.resolve()), 'checks': run.checks, 'foreground_visual_acceptance': False,
            'hidden_failure': parent.failure, 'offscreen_requests': run.offscreen_frames,
            'netem': '200 ms RTT + 5% loss + 25 ms jitter', 'diagnostic_only': True}
        (run.run / 'diagnostic.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        print(json.dumps({'stage': 'completed', 'passed': result['passed'], 'error': error,
                          'output': str(run.run)}), flush=True)
    return 0 if error is None else 1


if __name__ == '__main__':
    raise SystemExit(main())

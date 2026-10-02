"""Run the existing real-network matrix with hidden, non-activating Unity windows.

Uses Unity's documented -parentHWND <HWND> delayed mode, not batchmode or
nographics. Only windows owned by newly launched test Players are parented.
No foreground activation, keyboard/mouse injection, or visual acceptance.
"""
import argparse
import ctypes
from ctypes import wintypes
import json
from pathlib import Path
import subprocess
import sys
import threading
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'space-planet-flow'))
from test_network import NetworkRun


class OffscreenRun(NetworkRun):
    def __init__(self, args):
        super().__init__(args)
        self.capture_clock = {}
        self.offscreen_frames = 0

    def wait(self, role, predicate, description, seconds=100):
        def capture(peer, report):
            terrain = report.get('terrain') or {}
            now = time.monotonic()
            if ((terrain.get('mapEpoch') or 0) > 0 or terrain.get('dataReady')) and not terrain.get('visible') and now - self.capture_clock.get(peer, 0) > 2:
                self.capture_clock[peer] = now
                self.offscreen_frames += 1
                # Shipped automation renders the real scene camera and restores its state.
                # Readiness is still measured by the original camera-completion gate.
                self.send(peer, operation='capture', file=f'offscreen-ready-{self.offscreen_frames}.png')
        def rendered(report):
            capture(role, report)
            # A map swap waits for every participant; keep all hidden cameras progressing,
            # including peers the existing matrix is not currently polling.
            for peer in list(self.paths):
                if peer == role or peer not in self.processes or self.processes[peer].poll() is not None:
                    continue
                other = self.read(peer)
                if other: capture(peer, other)
            return predicate(report)
        return super().wait(role, rendered, description, seconds)


class HiddenParent:
    def __init__(self):
        self.api = ctypes.WinDLL('user32', use_last_error=True)
        self.kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        self.api.CreateWindowExW.restype = wintypes.HWND
        self.api.CreateWindowExW.argtypes = [wintypes.DWORD, wintypes.LPCWSTR, wintypes.LPCWSTR,
            wintypes.DWORD, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int,
            wintypes.HWND, wintypes.HMENU, wintypes.HINSTANCE, wintypes.LPVOID]
        self.kernel.GetModuleHandleW.restype = wintypes.HINSTANCE
        self.kernel.GetModuleHandleW.argtypes = [wintypes.LPCWSTR]
        self.api.SetParent.restype = wintypes.HWND
        self.api.SetParent.argtypes = [wintypes.HWND, wintypes.HWND]
        self.api.GetParent.restype = wintypes.HWND
        self.api.GetParent.argtypes = [wintypes.HWND]
        self.api.GetWindowLongW.argtypes = [wintypes.HWND, ctypes.c_int]
        self.api.SetWindowLongW.argtypes = [wintypes.HWND, ctypes.c_int, ctypes.c_long]
        self.api.ShowWindow.argtypes = [wintypes.HWND, ctypes.c_int]
        self.api.IsWindowVisible.argtypes = [wintypes.HWND]
        self.api.DestroyWindow.argtypes = [wintypes.HWND]
        self.api.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
        self.callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        self.api.EnumWindows.argtypes = [self.callback_type, wintypes.LPARAM]
        self.api.EnumChildWindows.argtypes = [wintypes.HWND, self.callback_type, wintypes.LPARAM]
        self.api.PeekMessageW.argtypes = [ctypes.POINTER(wintypes.MSG), wintypes.HWND,
                                        wintypes.UINT, wintypes.UINT, wintypes.UINT]
        self.api.TranslateMessage.argtypes = [ctypes.POINTER(wintypes.MSG)]
        self.api.DispatchMessageW.argtypes = [ctypes.POINTER(wintypes.MSG)]
        self.ready = threading.Event()
        self.stop = threading.Event()
        self.window = None
        self.failure = None
        self.observed = set()
        self.thread = threading.Thread(target=self.pump, daemon=True)
        self.thread.start()
        if not self.ready.wait(5) or not self.window:
            raise RuntimeError('Hidden parent creation failed')

    def pump(self):
        self.window = self.api.CreateWindowExW(0x08000000, 'STATIC', 'Dark Nights hidden verification',
            0x80000000, 0, 0, 1280, 720, None, None, self.kernel.GetModuleHandleW(None), None)
        self.ready.set()
        msg = wintypes.MSG()
        while not self.stop.wait(.02):
            while self.api.PeekMessageW(ctypes.byref(msg), None, 0, 0, 1):
                self.api.TranslateMessage(ctypes.byref(msg))
                self.api.DispatchMessageW(ctypes.byref(msg))
        if self.window:
            self.api.DestroyWindow(self.window)

    def monitor(self, process):
        def visit(hwnd, _):
            pid = wintypes.DWORD()
            self.api.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            if pid.value == process.pid:
                first = int(hwnd) not in self.observed
                self.observed.add(int(hwnd))
                # delayed mode keeps startup hidden; preserve that and attach only our own Player.
                if self.api.IsWindowVisible(hwnd):
                    self.failure = 'A test Player became visible; aborting instead of disturbing the desktop'
                    process.terminate()
                    return False
                ctypes.set_last_error(0)
                if first:
                    style = self.api.GetWindowLongW(hwnd, -16) & 0xffffffff
                    self.api.SetWindowLongW(hwnd, -16, (style & ~0x80000000) | 0x40000000)
                self.api.SetParent(hwnd, self.window)
                if ctypes.get_last_error():
                    self.failure = 'Unable to embed a hidden test Player'
                    process.terminate()
                    return False
                if first:
                    if self.api.GetParent(hwnd) != self.window or self.api.IsWindowVisible(self.window):
                        self.failure = 'Hidden parent invariant failed'
                        process.terminate()
                        return False
                    # Enable the child surface for rendering beneath an invisible parent.
                    # SW_SHOWNA never activates it; the parent is never shown.
                    self.api.ShowWindow(hwnd, 8)
            return True
        callback = self.callback_type(visit)
        while process.poll() is None and not self.stop.wait(.05):
            self.api.EnumWindows(callback, 0)
            self.api.EnumChildWindows(self.window, callback, 0)

    def close(self):
        self.stop.set()
        self.thread.join(3)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--player', required=True, type=Path)
    parser.add_argument('--output-root', required=True, type=Path)
    parser.add_argument('--weak', action='store_true')
    parser.add_argument('--port', type=int, default=29420)
    args = parser.parse_args()
    args.backend = 'mono'
    args.clients = 1
    args.driver = 'client'
    args.save_version = 16
    args.interactive = False
    args.phase_hook = True
    args.skip_restarts = False
    args.keep_open = None
    args.keep_seconds = 3600
    parent = HiddenParent()
    original = subprocess.Popen
    player = args.player.resolve()
    def launch(command, *values, **options):
        if Path(command[0]).resolve() == player:
            command = [*command, '-parentHWND', str(parent.window), 'delayed']
            startup = subprocess.STARTUPINFO()
            startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            startup.wShowWindow = 0
            options['startupinfo'] = startup
            options['creationflags'] = options.get('creationflags', 0) | subprocess.BELOW_NORMAL_PRIORITY_CLASS
            process = original(command, *values, **options)
            threading.Thread(target=parent.monitor, args=(process,), daemon=True).start()
            return process
        return original(command, *values, **options)
    subprocess.Popen = launch
    run = None
    try:
        run = OffscreenRun(args)
        code = run.execute()
        return code if parent.failure is None else 1
    finally:
        subprocess.Popen = original
        if run:
            (run.run / 'background.json').write_text(json.dumps({
                'hidden': True, 'mode': 'parentHWND-delayed', 'parent_no_activate': True,
                'observed_player_windows': len(parent.observed), 'failure': parent.failure,
                'offscreen_camera_requests': run.offscreen_frames,
                'foreground_visual_acceptance': False}, indent=2), encoding='utf-8')
        parent.close()


if __name__ == '__main__':
    sys.exit(main())

"""Collect a separate 60-second rendered workload using the shipped OS-aware metrics recorder."""
import argparse
import ctypes
from ctypes import wintypes
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import time
import traceback


def working_set(pid):
    """Windows process memory query only; does not change OS window focus."""
    class Counters(ctypes.Structure):
        _fields_ = [("cb", wintypes.DWORD), ("PageFaultCount", wintypes.DWORD)] + [
            (name, ctypes.c_size_t) for name in ("PeakWorkingSetSize", "WorkingSetSize", "QuotaPeakPagedPoolUsage",
            "QuotaPagedPoolUsage", "QuotaPeakNonPagedPoolUsage", "QuotaNonPagedPoolUsage", "PagefileUsage", "PeakPagefileUsage")]
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    query = ctypes.WinDLL("psapi", use_last_error=True).GetProcessMemoryInfo
    kernel.OpenProcess.argtypes = (wintypes.DWORD, wintypes.BOOL, wintypes.DWORD)
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = (wintypes.HANDLE,)
    query.argtypes = (wintypes.HANDLE, ctypes.POINTER(Counters), wintypes.DWORD)
    handle = kernel.OpenProcess(0x0410, False, pid)
    if not handle:
        return None
    try:
        value = Counters()
        value.cb = ctypes.sizeof(value)
        return int(value.WorkingSetSize) if query(handle, ctypes.byref(value), value.cb) else None
    finally:
        kernel.CloseHandle(handle)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", required=True, type=Path)
    parser.add_argument("--backend", choices=("mono", "il2cpp"), default="mono")
    parser.add_argument("--clients", choices=(0, 1, 3), type=int, default=0)
    parser.add_argument("--phase", choices=("orbit", "descent", "landed"), default="orbit")
    parser.add_argument("--workload", choices=("idle", "walk"), default="idle")
    parser.add_argument("--seconds", type=int, default=60)
    parser.add_argument("--port", type=int, default=29460)
    options = parser.parse_args()
    if options.seconds < 60 or options.seconds > 300:
        parser.error("--seconds must be 60..300 recorded seconds, after the recorder's two-second warmup")
    source = Path(__file__).with_name("test_network.py")
    spec = importlib.util.spec_from_file_location("journey_network_driver", source)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    repo = source.resolve().parents[2]
    args = argparse.Namespace(player=options.player, backend=options.backend, clients=options.clients, driver="host",
                              weak=False, port=options.port, output_root=repo / "artifacts/space-planet-flow/performance",
                              interactive=False, metrics=True, phase_hook=False, skip_restarts=True, keep_open=None, keep_seconds=3600)
    run = module.NetworkRun(args)
    error = None
    exports = {}
    timeline = []
    started = datetime.now(timezone.utc).isoformat()
    try:
        for role in run.roles:
            run.start(role)
        run.consistency("performance-orbit-setup", module.PHASES["Orbit"])
        if options.phase != "orbit":
            run.walk("host", module.ship_x(run.ready("host")) + 96)
            run.expect("host", "performance setup destination", **run.request("host"))
            run.wait("host", lambda r: r["ready"] and module.phase(r) == module.PHASES["Descent"], "performance arrival", 150)
            run.all_ready(module.PHASES["Descent"])
            if options.phase == "landed":
                run.automatic_landing("performance landing setup")
            else:
                run.personal("host", "pilot", name="performance staging leaves cockpit")
                run.hover_without_pilot("performance airborne staging")
        for role in run.roles:
            run.consumed(role, run.send(role, operation="metrics-reset"))
        # PlayerPerformanceCapture intentionally excludes its first two seconds.
        time.sleep(2.2)
        clock = time.monotonic()
        last_direction = None
        while time.monotonic() - clock < options.seconds:
            elapsed = time.monotonic() - clock
            if options.workload == "walk":
                direction = -1 if int(elapsed / 1.5) % 2 == 0 else 1
                if direction != last_direction:
                    run.hold("host", direction)
                    last_direction = direction
            row = dict(elapsed=round(elapsed, 3), roles={})
            for role in run.roles:
                report = run.ready(role)
                row["roles"][role] = dict(pid=run.processes[role].pid, workingSetBytes=working_set(run.processes[role].pid),
                                          epoch=report["epoch"], phase=module.phase(report), ready=report["ready"],
                                          terrain=report["terrain"], heroX=module.actor(report)["X"])
            timeline.append(row)
            run.write_status("performance-sampling", seconds=round(elapsed, 1), targetSeconds=options.seconds)
            time.sleep(1)
        for role in run.roles:
            run.send(role, operation="input-stop")
            path = run.run / (role + "-metrics.json")
            run.send(role, operation="metrics", file=path.name)
            run.wait(role, lambda r: path.is_file() and path.stat().st_size > 0, "metrics export")
            exports[role] = json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception:
        error = traceback.format_exc()
    finally:
        for role in list(run.processes):
            run.stop(role)
        report = dict(mode="separate-performance-sample", started=started, finished=datetime.now(timezone.utc).isoformat(),
                      player=str(run.player), player_sha256=module.sha256(run.player), backend=options.backend,
                      managed_sha256={p.name: module.sha256(p) for p in (run.player.parent / "DarkNights_Data/Managed").glob("DarkNights.*.dll")},
                      game_assembly_sha256=module.sha256(run.player.parent / "GameAssembly.dll") if (run.player.parent / "GameAssembly.dll").exists() else None,
                      phase=options.phase, workload=options.workload, requestedRecordedSeconds=options.seconds,
                      clients=options.clients, exports=exports, timeline=timeline, error=error,
                      foregroundSampleCounts={role: value["foregroundFrameMilliseconds"]["samples"] for role, value in exports.items()},
                      foregroundSource="Unchanged PlayerPerformanceCapture GetForegroundWindow/GetWindowThreadProcessId checks",
                      focusWasForced=False, fullNetworkAcceptance=False, performanceAcceptance=False,
                      note="仅采样；前台样本覆盖和性能指标须据实际结果评估，不以背景或空前台样本替代。")
        path = run.run / "performance-result.json"
        path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        run.write_status("performance-complete" if error is None else "performance-failed", result=str(path), error=error)
        print(json.dumps(dict(result=str(path), foregroundSampleCounts=report["foregroundSampleCounts"], error=error), ensure_ascii=False), flush=True)
    return 0 if error is None else 1


if __name__ == "__main__":
    raise SystemExit(main())

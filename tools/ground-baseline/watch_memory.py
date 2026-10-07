"""单次 Unity 验证的独立内存保护；只停止指定 Editor 的测试，不处理其他应用。"""
import argparse
import ctypes as c
from ctypes import wintypes as w
import datetime
import json
from pathlib import Path
import shutil
import subprocess
import time


class Performance(c.Structure):
    _fields_ = [("size", w.DWORD), ("commit", c.c_size_t), ("limit", c.c_size_t),
                ("peak", c.c_size_t), ("physical", c.c_size_t), ("available", c.c_size_t),
                ("cache", c.c_size_t), ("kernel", c.c_size_t), ("paged", c.c_size_t),
                ("nonpaged", c.c_size_t), ("page_size", c.c_size_t), ("handles", w.DWORD),
                ("processes", w.DWORD), ("threads", w.DWORD)]


class ProcessMemory(c.Structure):
    _fields_ = [("size", w.DWORD), ("faults", w.DWORD), ("peak_working", c.c_size_t),
                ("working", c.c_size_t), ("peak_paged", c.c_size_t), ("paged", c.c_size_t),
                ("peak_nonpaged", c.c_size_t), ("nonpaged", c.c_size_t),
                ("pagefile", c.c_size_t), ("peak_pagefile", c.c_size_t), ("private", c.c_size_t)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--seconds", type=int, default=1800)
    parser.add_argument("--once", action="store_true")
    args = parser.parse_args()
    cli = shutil.which("unity")
    if not cli:
        raise RuntimeError("Unity CLI unavailable")
    # 通过 CLI 验证 PID 与项目的对应关系；不保存含认证参数的进程命令行。
    status = json.loads(subprocess.check_output([cli, "status", "--json"], text=True))
    if not any(x["pid"] == args.pid and Path(x["project"]).resolve() == args.project.resolve()
               for x in status["data"]["instances"]):
        raise RuntimeError("PID does not belong to the requested Unity project")
    kernel, psapi = c.WinDLL("kernel32", use_last_error=True), c.WinDLL("psapi", use_last_error=True)
    kernel.OpenProcess.restype = w.HANDLE
    kernel.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]
    kernel.CloseHandle.argtypes = [w.HANDLE]
    psapi.GetProcessMemoryInfo.argtypes = [w.HANDLE, c.POINTER(ProcessMemory), w.DWORD]
    psapi.GetPerformanceInfo.argtypes = [c.POINTER(Performance), w.DWORD]
    handle = kernel.OpenProcess(0x410, False, args.pid)
    if not handle:
        raise c.WinError(c.get_last_error())
    args.output.mkdir(parents=True, exist_ok=True)
    stop_file = args.output / "stop.flag"
    if stop_file.exists() or (args.output / "breach.json").exists():
        raise RuntimeError("Use a fresh monitor output directory")
    deadline = time.monotonic() + args.seconds
    samples, peak, minimum_ram, minimum_commit = 0, 0, float("inf"), float("inf")
    state = "completed"
    try:
        with (args.output / "samples.jsonl").open("w", encoding="utf-8") as log:
            while time.monotonic() < deadline and not stop_file.exists():
                performance, memory = Performance(), ProcessMemory()
                if not psapi.GetPerformanceInfo(c.byref(performance), c.sizeof(performance)):
                    raise c.WinError(c.get_last_error())
                if not psapi.GetProcessMemoryInfo(handle, c.byref(memory), c.sizeof(memory)):
                    state = "editor_exited"
                    break
                gib = 1024 ** 3
                sample = {"utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "pid": args.pid,
                          "private_gib": memory.private / gib, "working_gib": memory.working / gib,
                          "free_ram_gib": performance.available * performance.page_size / gib,
                          "free_commit_gib": (performance.limit - performance.commit) * performance.page_size / gib}
                samples += 1
                peak = max(peak, sample["private_gib"])
                minimum_ram = min(minimum_ram, sample["free_ram_gib"])
                minimum_commit = min(minimum_commit, sample["free_commit_gib"])
                log.write(json.dumps(sample) + "\n")
                log.flush()
                # 32 GiB 主机保留明确余量，远早于本次 16.7 GiB/92% 的事故点。
                if sample["private_gib"] >= 8 or sample["free_ram_gib"] < 6 or sample["free_commit_gib"] < 4:
                    state = "breach"
                    (args.output / "breach.json").write_text(json.dumps(sample, indent=2), encoding="utf-8")
                    try:
                        stopped = subprocess.run([cli, "command", "--caller", "plugin", "--skill", "unity-cli",
                                                  "editor_stop", "--project-path", str(args.project), "--json"],
                                                 capture_output=True, text=True, timeout=15)
                        (args.output / "stop-result.json").write_text(stopped.stdout, encoding="utf-8")
                    except subprocess.TimeoutExpired:
                        (args.output / "stop-result.json").write_text('{"stop_timeout":true}', encoding="utf-8")
                    # 不自动结束 Editor 或其他应用；阈值越界后禁止继续提交验证。
                    break
                if args.once:
                    break
                time.sleep(1)
    finally:
        kernel.CloseHandle(handle)
        summary = {"state": state, "samples": samples, "peak_private_gib": peak,
                   "minimum_free_ram_gib": minimum_ram, "minimum_free_commit_gib": minimum_commit,
                   "limits": {"editor_private_gib": 8, "minimum_free_ram_gib": 6, "minimum_free_commit_gib": 4}}
        (args.output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
        print(json.dumps(summary))
    return 2 if state == "breach" else 0


if __name__ == "__main__":
    raise SystemExit(main())

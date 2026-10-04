"""Run an explicit native-map fixture over the game's real FishNet connection.

The fixture uses a separate world ID. It never mutates formal mineral or actor state.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import time
import traceback


def execute(args):
    root = args.output.resolve()
    root.mkdir(parents=True, exist_ok=False)
    processes, checks = {}, []
    reports = {role: root / (role + "-mineral.json") for role in ("host", "client1")}

    def read(role):
        try:
            value = json.loads(reports[role].read_text(encoding="utf-8-sig"))
            if value.get("error"):
                raise RuntimeError(value["error"])
            return value
        except (OSError, json.JSONDecodeError):
            return None

    def wait(role, predicate, label, timeout=100):
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            value = read(role)
            if value and predicate(value):
                checks.append({"name": label, "passed": True})
                return value
            if role in processes and processes[role].poll() is not None:
                raise RuntimeError(role + " exited")
            time.sleep(.25)
        raise TimeoutError(label)

    def command(role, value):
        reports[role].with_suffix(".json.command").write_text(value, encoding="utf-8")

    error = None
    try:
        for role in reports:
            commands = root / (role + "-game.commands")
            commands.write_text("", encoding="utf-8")
            command_line = [str(args.player.resolve()), "-screen-width", "960", "-screen-height", "540",
                            "-screen-fullscreen", "0", "-logFile", str(root / (role + ".log")),
                            "--dn-role", role, "--dn-port", str(args.port), "--dn-map-seed", "MINERAL-P0-1005",
                            "--dn-save-dir", str(root / "saves"), "--dn-input-replay",
                            "--dn-report", str(root / (role + "-game.json")),
                            "--dn-commands", str(commands),
                            "--dn-map-probe-report", str(reports[role])]
            processes[role] = subprocess.Popen(command_line, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            wait(role, lambda r: r["foregroundReady"] and r["mineralReady"] and r["reserves"] == 3,
                 role + " receives independent foreground and mineral baselines")
        command("host", "hit")
        for role in reports:
            wait(role, lambda r: r["reserves"] == 2 and r["foregroundReady"], role + " receives reserve delta")
        previous = read("client1")["generation"]
        command("client1", "right")
        wait("client1", lambda r: r["target"] == "Unknown" and r["right"] == "Present" and r["generation"] > previous,
             "dynamic region retires previous cells")
        command("host", "clear")
        wait("host", lambda r: r["target"] == "Empty", "host receives explicit depletion")
        command("client1", "left")
        wait("client1", lambda r: r["target"] == "Empty" and r["foregroundReady"], "return receives current depletion")
        for role in reports:
            checks.append({"name": role + " no rejected mineral packets", "passed": read(role)["rejected"] == 0})
    except BaseException:
        error = traceback.format_exc()
    finally:
        for process in processes.values():
            if process.poll() is None:
                process.terminate()
        for process in processes.values():
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
        result = {"passed": error is None and all(c["passed"] for c in checks), "checks": checks, "error": error,
                  "player": str(args.player.resolve()), "sha256": hashlib.sha256(args.player.read_bytes()).hexdigest(),
                  "scope": "Explicit protocol fixture, independent graphics processes, real FishNet; no formal harvesting or artwork acceptance."}
        (root / "result.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps({"passed": result["passed"], "checks": len(checks), "result": str(root / "result.json")}, ensure_ascii=False))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--port", type=int, default=29510)
    raise SystemExit(execute(parser.parse_args()))

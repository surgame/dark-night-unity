"""Bounded development Player startup diagnosis through existing report and read-only AMP1 bridge."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import secrets
import socket
import subprocess
import time


def utc():
    return datetime.now(timezone.utc).isoformat()


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_report(path):
    try:
        report = json.loads(path.read_text(encoding="utf-8-sig"))
        frame = report.get("frame") or {}
        expedition = (frame.get("World") or {}).get("Expedition") or {}
        return dict(utc=report.get("utc"), ready=report.get("ready"), status=report.get("status"),
                    clientStatus=report.get("clientStatus"), error=report.get("error"),
                    epoch=report.get("epoch"), readyCount=report.get("readyCount"),
                    framePresent=bool(frame), phase=(expedition.get("Journey") or {}).get("Phase"),
                    terrain=report.get("terrain"), terrainPresentation=report.get("terrainPresentation"))
    except (OSError, json.JSONDecodeError):
        return None


def read_bridge(port, token):
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=.3) as client:
            client.settimeout(.3)
            client.sendall(token.encode("ascii") + b"\n")
            body = bytearray()
            while len(body) <= 262144:
                part = client.recv(8192)
                if not part:
                    break
                body.extend(part)
                if b"\n" in part:
                    break
            if len(body) > 262144:
                return {"diagnosticError": "bridge response budget exceeded"}
            return json.loads(body.decode("utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        return {"diagnosticError": type(error).__name__ + ": " + str(error)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", required=True, type=Path)
    parser.add_argument("--mode", choices=("batch-replay", "window-replay", "window-input"), default="batch-replay")
    parser.add_argument("--port", type=int, default=29260)
    parser.add_argument("--debug-port", type=int, default=29360)
    parser.add_argument("--seconds", type=int, default=25)
    args = parser.parse_args()
    if not 5 <= args.seconds <= 25:
        parser.error("--seconds must be 5..25 including shutdown allowance")
    player = args.player.resolve()
    if not player.is_file():
        raise FileNotFoundError(player)
    repo = Path(__file__).resolve().parents[2]
    output = repo / "artifacts/space-planet-flow" / ("startup-" + datetime.now().strftime("%Y%m%d-%H%M%S-%f") + "-" + args.mode)
    output.mkdir(parents=True, exist_ok=False)
    report_path = output / "host.json"
    command_path = output / "host.commands"
    command_path.write_text("", encoding="utf-8")
    token = secrets.token_hex(24)
    command = [str(player), "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
               "-logFile", str(output / "host.log"), "--dn-role", "host", "--dn-port", str(args.port),
               "--dn-map-seed", "SPACE-PLANET-0926", "--dn-save-dir", str(output / "saves"),
               "--dn-report", str(report_path), "--dn-commands", str(command_path),
               "--ard-map-debug-port", str(args.debug_port), "--ard-map-debug-token", token]
    if args.mode == "batch-replay":
        command.append("-batchmode")
    if args.mode != "window-input":
        command.append("--dn-input-replay")
    started = utc()
    process = subprocess.Popen(command, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    start = time.monotonic()
    observed = []
    error = None
    shutdown = "quit"
    print(json.dumps(dict(stage="running", pid=process.pid, mode=args.mode, output=str(output)), ensure_ascii=False), flush=True)
    try:
        with (output / "snapshots.jsonl").open("w", encoding="utf-8") as stream:
            while time.monotonic() - start < args.seconds - 2 and process.poll() is None:
                sample = dict(elapsed=round(time.monotonic() - start, 3), report=read_report(report_path),
                              bridge=read_bridge(args.debug_port, token))
                observed.append(sample)
                stream.write(json.dumps(sample, ensure_ascii=False) + "\n")
                stream.flush()
                time.sleep(.5)
    except Exception as exception:
        error = repr(exception)
    finally:
        if process.poll() is None:
            command_path.write_text('{"operation":"quit"}\n', encoding="utf-8")
            try:
                process.wait(max(.1, args.seconds - (time.monotonic() - start)))
            except subprocess.TimeoutExpired:
                shutdown = "terminate bounded own process"
                process.terminate()
                try:
                    process.wait(2)
                except subprocess.TimeoutExpired:
                    shutdown = "kill bounded own process"
                    process.kill()
                    process.wait(2)
        projected = [s for s in observed if (s.get("report") or {}).get("framePresent")]
        ready = [s for s in observed if (s.get("report") or {}).get("ready")]
        bridges = [s for s in observed if (s.get("bridge") or {}).get("status") == "active"]
        managed = player.parent / "DarkNights_Data/Managed"
        summary = dict(started=started, finished=utc(), mode=args.mode, player=str(player), player_sha256=digest(player),
                       game_assembly_sha256=digest(player.parent / "GameAssembly.dll") if (player.parent / "GameAssembly.dll").exists() else None,
                       managed_sha256={p.name: digest(p) for p in managed.glob("*.dll")
                                       if p.name.startswith(("DarkNights.", "AnyRules.", "FishNet"))},
                       duration=round(time.monotonic() - start, 3), pid=process.pid, exitCode=process.returncode,
                       shutdown=shutdown, sampleCount=len(observed), readyObserved=bool(ready), bridgeObserved=bool(bridges),
                       lastProjected=projected[-1] if projected else None, lastBridge=bridges[-1] if bridges else None,
                       error=error, fullNetworkAcceptance=False)
        (output / "result.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
        print(json.dumps(dict(stage="completed", readyObserved=bool(ready), bridgeObserved=bool(bridges),
                              samples=len(observed), result=str(output / "result.json"), error=error), ensure_ascii=False), flush=True)
    return 0 if error is None else 1


if __name__ == "__main__":
    raise SystemExit(main())

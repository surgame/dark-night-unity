"""Basic protocol-22 ship equipment smoke test using independent Mono Players.

Reuses the existing journey driver for process/command transport. All purchases,
movement and saves go through SessionClient; no authority state is injected.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import sys
import traceback

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "space-planet-flow"))
from test_network import NetworkRun, actor, ship, ship_x, world, sha256


def payload(run, role, key, **changes):
    report = run.ready(role)
    person = actor(report)
    result = dict(operation="BuyEquipment", actors=[person["Id"]], target=ship(report)["Id"],
                  kind=key, value=person["InventoryRevision"], lease=person["ControlLease"])
    result.update(changes)
    return result


def credits(report):
    return world(report)["Camp"]["Credits"]


def inventory(report):
    keys = ("Slot0", "Slot1", "Slot2", "Slot3", "JetpackOwned", "JetpackEquipped", "InventoryRevision", "Slot0Definition", "Slot1Definition", "Slot2Definition", "Slot3Definition")
    return {a["Id"]: [a[key] for key in keys] for a in world(report)["Actors"] if a["ManualControl"]}


def smoke(run):
    host = run.start("host")
    run.record("new host starts with empty equipment and 30 credits",
               credits(host) == 30 and actor(host)["Slot0"] == 0 and not actor(host)["JetpackOwned"])
    run.walk("host", ship_x(host) + 32)
    run.expect("host", "host buys one pickaxe", **payload(run, "host", "pickaxe"))
    host = run.wait("host", lambda r: actor(r)["Slot0"] == 2 and credits(r) == 26, "host pickaxe projection")
    client = run.start("client1")
    run.record("late client sees host equipment and current shared credits",
               credits(client) == 26 and actor(client, host["slot"])["Slot0"] == 2)
    run.record("late client receives no free equipment or credits", actor(client)["Slot0"] == 0 and
               not actor(client)["JetpackOwned"] and credits(client) == 26)
    run.expect("client1", "remote purchase rejected outside terminal", "NoEffect", **payload(run, "client1", "pistol"))
    run.walk("client1", ship_x(client) + 32)
    command = payload(run, "client1", "pistol")
    operation = command.pop("operation")
    frozen = run.next_raw("client1", operation, **command)
    run.expect("client1", "client buys pistol", **frozen)
    run.wait("client1", lambda r: actor(r)["Slot0"] == 1 and credits(r) == 16, "client pistol projection")
    run.expect("client1", "duplicate purchase returns cached receipt", **frozen)
    run.record("duplicate purchase does not charge twice", credits(run.ready("host")) == 16)
    run.expect("client1", "stale inventory revision rejected", "NoEffect", **payload(run, "client1", "jetpack", value=0))
    run.pause(True)
    run.expect("client1", "purchase while paused rejected", "InvalidRequest", **payload(run, "client1", "jetpack"))
    run.record("paused rejection preserves credits and equipment", credits(run.ready("host")) == 16 and
               not any(a["JetpackOwned"] for a in world(run.ready("host"))["Actors"]))
    run.pause(False)
    markers = {role: run.begin_receipt(role, **payload(run, role, "jetpack")) for role in run.roles}
    results = [run.finish_receipt(role, marker)["Code"] for role, marker in markers.items()]
    run.record("two players competing for last affordable jetpack only charge once",
               sorted(results) == ["Applied", "NoEffect"], results)
    for role in run.roles:
        run.wait(role, lambda r: credits(r) == 2 and sum(a["JetpackOwned"] for a in world(r)["Actors"]) == 1,
                 "atomic jetpack result synchronized")
    run.record("both peers converge on one jetpack and two credits", True)
    before = inventory(run.ready("host"))
    run.reconnect("client1", "client reconnect", 0)
    run.record("reconnect preserves equipment without another payment", inventory(run.ready("host")) == before and
               credits(run.ready("client1")) == 2)
    path = run.saves / "v15/slot-00.dnsave.json"
    run.expect("host", "v15 save request accepted", operation="Save", value=0)
    run.wait("host", lambda r: path.exists() and not r["storageBusy"], "v15 disk save")
    saved = json.loads(path.read_text(encoding="utf-8-sig"))
    run.record("save is format v15", saved["format_version"] == 15)
    slots = [a[f"slot_{i}"] for a in saved["world"]["actors"] for i in range(4)]
    run.record("saved equipment uses canonical Definition identities", all(isinstance(v,str) and (v == "" or len(v) == 32) for v in slots) and any(slots))
    run.saved[0] = dict(path=str(path), sha256=sha256(path), inventory=before, credits=2)
    for role in run.roles:
        run.save_report(role, "before-process-restart")
        run.stop(role)
    host = run.start("host")
    run.send("host", operation="BeginLoad", value=0)
    run.wait("host", lambda r: r["ready"] and r["epoch"] > host["epoch"], "fresh process loads v15")
    run.start("client1")
    for role in run.roles:
        report = run.ready(role)
        run.record(role + " actual restart restores exact equipment and credits", inventory(report) == before and
                   credits(report) == 2)
        run.save_report(role, "restored")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", required=True, type=Path)
    parser.add_argument("--port", type=int, default=29380)
    parser.add_argument("--weak", action="store_true")
    args = parser.parse_args()
    args.backend = "mono"
    args.clients = 1
    args.driver = "client"
    args.save_version = 15
    args.interactive = False
    args.output_root = Path(__file__).resolve().parents[2] / "artifacts/tool-definition-harvesting-20261002"
    run = NetworkRun(args)
    print(json.dumps(dict(run=str(run.run)), ensure_ascii=False), flush=True)
    error = None
    if args.weak:
        import subprocess
        run.relay = subprocess.Popen([sys.executable, str(run.repo / "tools/lan-netem.py"), "--listen", str(args.port + 1),
            "--target", str(args.port), "--loss", ".05", "--delay", ".1", "--jitter", ".025", "--duration", "5400",
            "--report", str(run.run / "relay.json")], creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    try:
        smoke(run)
    except Exception:
        error = traceback.format_exc()
    finally:
        for role in list(run.processes):
            run.stop(role)
        if run.relay:
            (run.run / "relay.json.stop").write_text("", encoding="utf-8")
            run.relay.wait(15)
    for log in run.run.glob("*.log"):
        failures = [line for line in log.read_text(encoding="utf-8", errors="replace").splitlines()
                    if any(word in line for word in ("Exception:", "InvalidKeyException", "No Theme Style Sheet"))]
        run.checks.append(dict(name=log.name + " has no runtime exception or missing theme", passed=not failures,
                               detail=failures[:8]))
        if failures and error is None:
            error = "Runtime log contains errors"
    result = dict(passed=error is None, error=error, checks=run.checks, saves=run.saved,
                  started=run.started_at, finished=datetime.now(timezone.utc).isoformat(),
                  player=str(run.player), backend="mono", protocol=22, save_version=15,
                  player_sha256=sha256(run.player),
                  managed_sha256={p.name: sha256(p) for p in (run.player.parent / "DarkNights_Data/Managed").glob("DarkNights.*.dll")},
                  weak=args.weak, netem="200 ms RTT + 5% loss + 25 ms jitter" if args.weak else None,
                  not_covered=[ "four players", "mining and sale in Player", "IL2CPP", "two machines"],
                  visual_acceptance=False)
    target = run.run / "result.json"
    target.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    run.write_status("complete" if result["passed"] else "failed", result=str(target), error=error)
    print(json.dumps(dict(passed=result["passed"], checks=len(run.checks), error=error, result=str(target)),
                     ensure_ascii=False), flush=True)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())

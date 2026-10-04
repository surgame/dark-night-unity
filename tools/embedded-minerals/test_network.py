"""Exercise native mineral cells through independent graphics Players and the real input Gateway.

The explicit quick preset only chooses a supported initial spawn on the generated map.
No authority state is injected; all mining, pause, save and load actions use shipped commands.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import subprocess
import sys
import time
import traceback

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "space-planet-flow"))
from test_network import NetworkRun, actor, world, sha256


def minerals(report):
    probe = report.get("mineralProbe")
    return None if not probe else {key: probe[key] for key in ("u", "v", "remaining", "durability")}


def cell(report, target):
    probe = report.get("mineralProbe")
    if not probe or probe["u"] != target["u"] or probe["v"] != target["v"]:
        return {"Remaining": -1, "Durability": 1000000, "ContentVersion": -1}
    return {"Remaining": probe["remaining"], "Durability": probe["durability"], "ContentVersion": probe["version"]}


class MineralRun(NetworkRun):
    def live_roles(self):
        return [role for role, process in self.processes.items() if process.poll() is None]

    def pause(self, value):
        self.expect("host", "pause=" + str(value), operation="SetPaused", value=int(value))
        host = self.wait("host", lambda r: r["frame"]["Paused"] == bool(value), "pause reflected")
        for role in self.live_roles():
            if role != "host":
                self.wait(role, lambda r: r["ready"] and r["epoch"] == host["epoch"] and
                          r["frame"]["Paused"] == bool(value), "peer receives pause and renewed lease")
        return host

    def peers_match(self, reference, label, roles=None):
        expected = minerals(reference)
        for role in roles or self.live_roles():
            value = self.wait(role, lambda r: r["ready"] and r.get("frame") and minerals(r) == expected and
                              r["terrain"]["visible"], label + " " + role)
            self.record(label + " " + role, world(value)["MineralWorldId"] == value["terrain"]["worldId"].replace("-", "") and
                        world(value)["MineralMapEpoch"] == value["terrain"]["mapEpoch"])

    def mine(self, target):
        report = self.ready("host")
        self.send("host", operation="input-hold", actor=actor(report)["Id"], useHeld=True,
                  usePressed=True, aimAngle=target["aim"], mining={key: target[key] for key in
                  ("entity", "u", "v", "version", "foregroundVersion")})

    def stop_mining(self):
        report = self.ready("host")
        count = self.send("host", operation="input-hold", actor=actor(report)["Id"], useHeld=False, cancelUse=True)
        self.consumed("host", count)
        self.send("host", operation="input-stop")

    def observe(self, role, target):
        report = self.ready(role)
        count = self.send(role, operation="input-hold", actor=actor(report)["Id"], useHeld=False, aimAngle=target["aim"])
        self.consumed(role, count)
        value = self.wait(role, lambda r: r["ready"] and bool(r.get("mineralProbe")) and
                          r["mineralProbe"]["u"] == target["u"] and r["mineralProbe"]["v"] == target["v"],
                          "current local mineral region readable " + role)
        self.consumed(role, self.send(role, operation="input-stop"))
        return value

    def smoke(self):
        host = self.start("host")
        host = self.wait("host", lambda r: bool(r.get("mineralProbe")), "supported mineral ray")
        target = dict(host["mineralProbe"])
        self.record("quick preset exposes real mineral cells", not world(host)["MineralDeposits"] and target["capacity"] > 0 and host["terrain"]["mineralObjects"] == 0)
        self.start("client1")
        self.observe("client1", target)
        self.peers_match(self.ready("host"), "initial mineral baseline", ["client1"])
        self.capture("initial-mineral-diagnostic")
        before = self.ready("host"); batches = before["terrainPresentation"]["mineralInputBatches"]
        self.mine(target)
        damaged = self.wait("host", lambda r: cell(r, target)["Durability"] < target["durability"], "authoritative cell damage")
        self.stop_mining(); self.pause(True)
        checkpoint = self.ready("host")
        self.record("damage preserves occupancy and content version", cell(checkpoint, target)["Remaining"] == target["remaining"] and
                    cell(checkpoint, target)["ContentVersion"] == target["version"] and checkpoint["terrainPresentation"]["mineralBuiltPages"] == before["terrainPresentation"]["mineralBuiltPages"])
        self.peers_match(checkpoint, "damage synchronized")
        for role in self.roles[2:]:
            self.start(role)
        for role in self.live_roles():
            report = self.ready(role)
            self.record("foreground loads at most 25 local chunks " + role,
                        0 < report["terrainPresentation"]["loadedTerrainChunks"] <= 25 and
                        report["terrain"]["digestScope"] == "localSubscribedChunks")
        self.peers_match(checkpoint, "late join receives partial durability")
        count = self.send("client1", operation="input-raw", actor=actor(checkpoint)["Id"], lease=actor(checkpoint)["ControlLease"],
                          sequence=10000, useHeld=True, usePressed=True, aimAngle=target["aim"], mining=target)
        self.consumed("client1", count)
        self.record("remote input cannot modify another player while paused", minerals(self.ready("host")) == minerals(checkpoint))
        save = self.saves / "v19" / "QuickTests" / self.args.quick_test / "slot-00.dnsave.json"
        self.expect("host", "partial mineral save accepted", operation="Save", value=0)
        self.wait("host", lambda r: save.is_file() and not r["storageBusy"], "real mineral save")
        document = json.loads(save.read_text(encoding="utf-8-sig"))
        self.record("v19 stores native mineral map and no mineral entities", document["format_version"] == 19 and
                    not document["world"]["mineral_deposits"] and bool(document["world"]["terrain"]["mineral_map"]) and
                    not any(value["kind"] == "mineral-deposit" for value in document["world"]["worksites"]))
        self.pause(False); self.mine(target)
        depleted = self.wait("host", lambda r: cell(r, target)["Remaining"] == 0, "one mineral cell depleted", seconds=120)
        self.stop_mining(); self.pause(True); depleted = self.ready("host")
        self.record("depletion clears native cell and keeps entity budget", cell(depleted, target)["ContentVersion"] > target["version"] and
                    not world(depleted)["MineralDeposits"] and depleted["terrain"]["mineralObjects"] == 0)
        cargo = next(value for value in world(depleted)["Expedition"]["Crew"] if value["Id"] == actor(depleted)["Id"])
        self.record("harvest credits cargo once", cargo["Iron"] + cargo["Gold"] == target["capacity"])
        self.record("mineral removal refreshes mineral input without rebuilding background",
                    depleted["terrainPresentation"]["mineralInputBatches"] > batches and
                    depleted["terrainPresentation"]["backgroundBuilds"] == checkpoint["terrainPresentation"]["backgroundBuilds"])
        self.peers_match(depleted, "empty-cell neighborhood synchronized")
        self.capture("depleted-mineral-diagnostic")
        old_client = self.ready("client1")
        self.send("client1", operation="disconnect")
        self.wait("client1", lambda r: not r["ready"], "mineral disconnect")
        if self.args.weak:
            self.wait("host", lambda r: any(value["Id"] == actor(old_client)["Id"] and value["ControllerSlot"] < 0
                      for value in world(r)["Actors"]), "old client lease released", seconds=35)
        self.send("client1", operation="connect")
        self.ready("client1")
        self.peers_match(depleted, "reconnect receives current tombstone", ["client1"])
        old_epoch = depleted["epoch"]
        self.send("host", operation="BeginLoad", value=0)
        loaded = self.wait("host", lambda r: r["ready"] and r["epoch"] > old_epoch, "restore partial mineral state")
        self.pause(False)
        for role in self.live_roles():
            self.observe(role, target)
        self.pause(True)
        loaded = self.ready("host")
        self.record("load restores partial cell durability and keeps bed identity", minerals(loaded) == minerals(checkpoint))
        self.peers_match(loaded, "loaded cells synchronized")
        self.saved[0] = dict(path=str(save), sha256=sha256(save))
        for role in reversed(list(self.processes)):
            self.stop(role)
        restarted = self.start("host")
        self.send("host", operation="BeginLoad", value=0)
        restarted = self.wait("host", lambda r: r["ready"] and r["epoch"] > restarted["epoch"], "real process restart loads mineral cells")
        self.pause(False)
        restarted = self.observe("host", target)
        self.pause(True)
        self.record("process restart restores saved cells", minerals(restarted) == minerals(checkpoint))
        self.start("client1"); self.observe("client1", target)
        self.peers_match(restarted, "late join after restart", ["client1"])

    def execute(self):
        try:
            if self.args.weak:
                self.relay = subprocess.Popen([sys.executable, str(self.repo / "tools/lan-netem.py"), "--listen", str(self.args.port + 1),
                    "--target", str(self.args.port), "--loss", ".05", "--delay", ".1", "--jitter", ".025", "--duration", "1800",
                    "--report", str(self.run / "relay.json")], creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            self.smoke()
        except BaseException:
            self.error = traceback.format_exc()
        finally:
            for role in reversed(list(self.processes)):
                self.stop(role)
            diagnostics = []
            for log in self.run.glob("*.log"):
                for line in log.read_text(encoding="utf-8-sig", errors="replace").splitlines():
                    if re.match(r"^(?:[\w.]*Exception\s*:|Assertion failed|Crash!!!|Error:)", line):
                        diagnostics.append(dict(log=log.name, message=line[:500]))
            if diagnostics and self.error is None:
                self.error = "Player log contains exceptions or errors: " + json.dumps(diagnostics[:10])
            if self.relay:
                (self.run / "relay.json.stop").write_text("", encoding="utf-8")
                try:
                    self.relay.wait(8)
                except subprocess.TimeoutExpired:
                    self.relay.terminate(); self.relay.wait(5)
            result = dict(passed=self.error is None, error=self.error, checks=self.checks, clients=self.args.clients,
                          weak=self.args.weak, finished=datetime.now(timezone.utc).isoformat(), player=str(self.player),
                          player_sha256=sha256(self.player), script_sha256=sha256(Path(__file__)),
                          managed_sha256={p.name: sha256(p) for p in (self.player.parent / "DarkNights_Data/Managed").glob("DarkNights.*.dll")},
                          saved=self.saved, visual_acceptance=False, fixture=self.args.quick_test)
            result["player_diagnostics"] = diagnostics
            path = self.run / "result.json"; path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
            self.write_status("complete" if result["passed"] else "failed", result=str(path), error=self.error)
            print(json.dumps(dict(passed=result["passed"], checks=len(self.checks), result=str(path), error=self.error), ensure_ascii=False), flush=True)
        return 0 if self.error is None else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", required=True, type=Path)
    parser.add_argument("--clients", type=int, choices=(1, 3), default=1)
    parser.add_argument("--weak", action="store_true")
    parser.add_argument("--background", action="store_true", help="Start graphics Players without requesting window activation on Windows")
    parser.add_argument("--port", type=int, default=29440)
    parser.add_argument("--output-root", type=Path, required=True)
    args = parser.parse_args()
    args.backend = "mono"; args.driver = "host"; args.interactive = False; args.keep_open = None
    args.save_version = 19; args.quick_test = "landed-embedded-minerals"
    return MineralRun(args).execute()


if __name__ == "__main__":
    sys.exit(main())

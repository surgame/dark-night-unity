"""Bounded current-Player LAN smoke test, including logs after graceful shutdown.

Reuses shipped automation commands and existing process orchestration. No world,
asset, save, or dependency state is edited by this driver.
"""
import argparse
from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import sys
import time


ROOT = Path(__file__).resolve().parents[2]


def module(name, relative):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


base = module("local_network_base", "tools/space-planet-flow/test_network.py")
guard = module("local_network_memory", "tools/ship-expedition/memory_guard.py")


class LocalNetworkRun(base.NetworkRun):
    """Checks real independent peers without running the historical flight matrix."""

    def read(self, role):
        guard.check_memory(self.run)
        return super().read(role)

    def membership(self, roles, label):
        reports = {}
        for role in roles:
            reports[role] = self.wait(
                role, lambda r: r["ready"] and r.get("frame") and
                r["readyCount"] == len(roles) and r["terrain"]["visible"] and
                r["terrain"]["dataReady"] and r["terrain"]["mineralDataReady"] and
                len([a for a in base.world(r)["Actors"] if a["ControllerSlot"] >= 0]) == len(roles),
                label + " membership", 90)
        host = reports["host"]
        ids = {base.actor(r)["Id"] for r in reports.values()}
        slots = {r["slot"] for r in reports.values()}
        self.record(label + " distinct actors and slots",
                    len(ids) == len(roles) == len(slots))
        for role, report in reports.items():
            self.record(label + " " + role + " shared world and dual-map Ready",
                        report["epoch"] == host["epoch"] and
                        report["terrain"]["worldId"] == host["terrain"]["worldId"] and
                        base.journey(report)["MapId"] == base.journey(host)["MapId"])
            self.save_report(role, label, report)
        return reports

    def move(self, role, observers):
        before = self.ready(role)
        person = base.actor(before)
        self.hold(role, horizontal=-1)
        try:
            self.wait("host", lambda r: base.actor(r, before["slot"])["X"] < person["X"] - 8,
                      role + " trusted movement", 12)
        finally:
            self.hold(role)
        stopped = self.wait("host", lambda r: not base.actor(r, before["slot"])["Walking"],
                            role + " authority stops", 12)
        expected = base.actor(stopped, before["slot"])["X"]
        for peer in observers:
            report = self.wait(
                peer, lambda r: abs(base.actor(r, before["slot"])["X"] - expected) < 1 and
                not base.actor(r, before["slot"])["Walking"], role + " movement on " + peer, 15)
            self.record(role + " movement projects to " + peer,
                        base.actor(report, before["slot"])["Id"] == person["Id"],
                        dict(before=person["X"], after=expected))

    def light(self, role, observers):
        slot = self.ready(role)["slot"]
        for enabled in (False, True):
            person = base.actor(self.ready(role))
            self.expect(role, role + " light request " + str(enabled),
                        operation="SetHeroLight", actors=[person["Id"]],
                        value=int(enabled), lease=person["ControlLease"])
            for peer in observers:
                self.wait(peer, lambda r: base.actor(r, slot)["LightEnabled"] == enabled,
                          role + " light on " + peer, 15)
                self.record(role + " light " + str(enabled) + " projects to " + peer, True)

    def initial_and_competition(self):
        self.start("host")
        self.start("client1")
        early = ["host", "client1"]
        self.membership(early, "two-player")
        for role in early:
            self.move(role, early)
        self.light("client1", early)
        for role in self.roles[2:]:
            self.start(role)
        self.membership(self.roles, "all-joined")
        for role in self.roles[2:]:
            self.move(role, self.roles)
            self.light(role, self.roles)
        for peer in self.roles[1:]:
            self.expect(peer, peer + " cannot enable Host debugging", "PermissionDenied",
                        operation="DebugSetEnabled", value=1)
        self.capture("all-peers-host")

    def transient_cases(self):
        self.pause(True)
        before = {role: self.ready(role)["frame"]["Elapsed"] for role in self.roles}
        time.sleep(1.2)
        for role in self.roles:
            report = self.ready(role)
            self.record(role + " pause holds simulation with active connection",
                        report["frame"]["Paused"] and
                        abs(report["frame"]["Elapsed"] - before[role]) < .001)
        self.pause(False)
        role = self.roles[-1]
        before = self.ready(role)
        person = base.actor(before)
        self.send(role, operation="disconnect")
        self.wait(role, lambda r: not r["ready"], "explicit disconnect", 25)
        self.wait("host", lambda r: r["readyCount"] == len(self.roles) - 1,
                  "Host notices disconnect", 25)
        self.send(role, operation="connect")
        after = self.wait(role, lambda r: r["ready"] and r.get("frame") and
                          any(a["ControllerSlot"] == r["slot"] for a in base.world(r)["Actors"]),
                          "same-process reconnect", 60)
        self.record("reconnect recovers original slot actor and fresh control lease",
                    after["slot"] == before["slot"] and
                    base.actor(after)["Id"] == person["Id"] and
                    base.actor(after)["ControlLease"] > person["ControlLease"])
        self.membership(self.roles, "reconnected")
        self.move(role, self.roles)
        self.light(role, self.roles)

    def flight_landing(self):
        for role in self.roles:
            self.save_report(role, "final")
        self.send("host", operation="disconnect")
        for role in self.roles[1:]:
            report = self.wait(role, lambda r: not r["ready"], "Host closes room", 30)
            self.record(role + " loses Ready after Host closes room", True,
                        dict(status=report["status"], clientStatus=report["clientStatus"]))
        self.uncovered("flight cargo mining save/load weak-network performance",
                       "minimum local networking request; no unrelated gameplay matrix")
        self.uncovered("Editor stop-Play graphics teardown",
                       "separate Editor reproduction; Player exit is not equivalent")

    def execute(self):
        source_hash = base.sha256(Path(__file__))
        # The base summary precedes process shutdown; only publish our final verdict.
        with redirect_stdout(io.StringIO()):
            code = super().execute()
        path = self.run / "result.json"
        result = json.loads(path.read_text(encoding="utf-8"))
        result["network_checks_passed"] = code == 0
        result["graphics_api"] = self.args.graphics_api
        shutdown = []
        for role, process in self.processes.items():
            shutdown.append(dict(role=role, exit_code=process.returncode))
        failures = {}
        for log in self.run.glob("*.log"):
            lines = log.read_text(encoding="utf-8", errors="replace").splitlines()
            found = [line for line in lines if any(word in line for word in
                     ("Exception:", "InvalidKeyException", "MissingMethodException",
                      "TypeLoadException", "Assertion failed", "Crash!!!",
                      "GarbageCollector disposing of ComputeBuffer"))]
            if found:
                failures[log.name] = found[:12]
        result["post_shutdown"] = dict(processes=shutdown, runtime_errors=failures)
        result["driver_sha256"] = source_hash
        if failures or any(p["exit_code"] != 0 for p in shutdown):
            result["passed"] = False
            result["error"] = result["error"] or "Player shutdown failed; see post_shutdown."
            code = 1
        path.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
        self.write_status("complete" if result["passed"] else "failed",
                          result=str(path), post_shutdown=result["post_shutdown"])
        print(json.dumps(dict(final_passed=result["passed"], checks=len(self.checks),
                              network_checks_passed=result["network_checks_passed"], result=str(path),
                              post_shutdown=result["post_shutdown"])), flush=True)
        return code


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--clients", type=int, choices=(1, 3), default=1)
    parser.add_argument("--port", type=int, default=29822)
    parser.add_argument("--graphics-api", choices=("d3d11", "d3d12"), default="d3d12")
    args = parser.parse_args()
    args.driver = "client"
    args.backend = "mono"
    args.save_version = 27
    args.weak = False
    args.background = True
    args.interactive = False
    args.quick_test = None
    args.metrics = False
    args.keep_open = None
    args.skip_restarts = True
    return LocalNetworkRun(args).execute()


if __name__ == "__main__":
    sys.exit(main())

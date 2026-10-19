"""Three local Players exercise the actual reliable navigation projection, with bounded evidence.

This first slice checks shared directory, coordinates, risk data, pause and late reconnect.
It does not claim automatic flight, cargo transfer or settlement acceptance.
"""
import argparse
import importlib.util
from pathlib import Path
import sys
from memory_guard import check_memory

BASE = Path(__file__).resolve().parents[1] / "space-planet-flow/test_network.py"
spec = importlib.util.spec_from_file_location("existing_space_network", BASE)
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


class NavigationRun(base.NetworkRun):
    def read(self, role):
        check_memory(self.run)
        return super().read(role)

    def initial_and_competition(self):
        for role in self.roles:
            self.start(role)
        reports = self.all_ready(5)
        nav = base.journey(reports["host"])["Navigation"]
        self.record("three nodes arrive through real session projection", len(nav["Definition"]["Nodes"]) == 3)
        self.record("preview nodes are not playable", sum(n["PreviewOnly"] for n in nav["Definition"]["Nodes"]) == 2)
        for role, report in reports.items():
            current = base.journey(report)["Navigation"]
            self.record(role + " shares catalog position danger and run identity", current == nav)
            self.record(role + " has real terrain and controlled hero", report["terrain"]["visible"] and base.actor(report) is not None)
            self.save_report(role, "navigation-initial", report)
        self.navigation = nav

    def transient_cases(self):
        self.pause(True)
        for role in self.roles:
            self.record(role + " pause preserves navigation", base.journey(self.ready(role))["Navigation"] == self.navigation)
        self.pause(False)
        role = self.roles[-1]
        before = self.ready(role)
        identity = base.actor(before)["Id"]
        boarded = base.crew(before)["Boarded"]
        self.send(role, operation="disconnect")
        self.wait(role, lambda r: not r["ready"], "navigation disconnect")
        self.send(role, operation="connect")
        after = self.wait(role, lambda r: r["ready"] and r.get("frame") and base.phase(r) == 5 and
                          any(a["ControllerSlot"] == r["slot"] for a in base.world(r)["Actors"]), "navigation reconnect")
        self.record("reconnect recovers controlled identity", base.actor(after)["Id"] == identity)
        self.record("reconnect preserves ground or cabin location", base.crew(after)["Boarded"] == boarded)
        self.record("reconnect receives current shared navigation", base.journey(after)["Navigation"] == self.navigation)
        self.save(1, "navigation stable ground")
        self.load(1)
        for peer, report in self.all_ready(5).items():
            self.record(peer + " save load preserves navigation", base.journey(report)["Navigation"] == self.navigation)

    def flight_landing(self):
        self.capture("navigation-hud")
        for role in self.roles:
            self.save_report(role, "navigation-final")
        self.uncovered("automatic flight cargo settlement", "first navigation slice; gameplay decisions remain pending")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--clients", type=int, default=2, choices=(1, 2, 3))
    parser.add_argument("--port", type=int, default=27957)
    args = parser.parse_args()
    args.backend = "mono"
    args.driver = "client"
    args.save_version = 26
    args.weak = False
    args.background = True
    args.interactive = False
    args.quick_test = None
    args.metrics = False
    args.keep_open = None
    args.skip_restarts = True
    args.graphics_api = "d3d11"
    run = NavigationRun(args)
    run.driver_sha256 = base.sha256(Path(__file__))
    return run.execute()


if __name__ == "__main__":
    sys.exit(main())

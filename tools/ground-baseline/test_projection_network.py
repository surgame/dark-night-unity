"""Bounded real Host + one client projection regression on the reviewed map.

Uses only existing Player commands and trusted movement. Hidden graphics windows
are not foreground visual/performance acceptance; no authority pose edits occur.
"""
import argparse
import importlib.util
from pathlib import Path
import sys


ROOT = Path(__file__).resolve().parents[2]


def load(name, relative):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


base = load("projection_network_base", "tools/space-planet-flow/test_network.py")
guard = load("projection_memory_guard", "tools/ship-expedition/memory_guard.py")


class ProjectionNetworkRun(base.NetworkRun):
    """Reuse process lifecycle/evidence without the unrelated expedition matrix."""

    def read(self, role):
        guard.check_memory(self.run)
        return super().read(role)

    def stable(self, role):
        return self.wait(role, lambda r: r["ready"] and r["terrain"]["visible"] and
                         r["terrain"]["mineralDataReady"] and not r["terrainPresentation"]["refreshing"],
                         "projection fully drawn", 90)

    def initial_and_competition(self):
        for role in self.roles:
            self.start(role)
        reports = self.all_ready(5)
        self.record("two distinct controlled actors", len({base.actor(r)["Id"] for r in reports.values()}) == 2)
        initial_world = reports["host"]["terrain"]["worldId"]
        for role in self.roles:
            self.record(role + " uses reviewed geology map", len(base.world(reports[role])["Geology"]) == 1)
            self.save_report(role, "initial")
            regions = []
            for index, x in enumerate((1600, 4000, 2304, 1600)):
                self.walk(role, x, tolerance=18, seconds=70)
                report = self.stable(role)
                regions.append(report["terrain"]["mineralRegion"]["u"])
                self.record(role + f" real walk {index} retains session and rendered terrain",
                            report["terrain"]["worldId"] == initial_world and
                            report["terrain"]["dataReady"] and base.actor(report)["Hp"] > 0,
                            dict(x=base.actor(report)["X"], regions=regions,
                                 wait=report["terrainPresentation"]["waitReason"]))
                authority = base.actor(self.ready("host"), report["slot"])
                self.record(role + f" walk {index} authority agrees", abs(authority["X"] - base.actor(report)["X"]) < 4)
                self.save_report(role, "walk-" + str(index), report)
            self.record(role + " crosses near/far subscriptions and returns", regions[1] == 160 and regions[2] == 64,
                        regions)
        self.record("Host and client stay in same world", self.ready("client1")["terrain"]["worldId"] == initial_world)

    def transient_cases(self):
        previous = base.actor(self.ready("client1"))["ControlLease"]
        self.stop("client1")
        self.start("client1")
        report = self.stable("client1")
        self.record("client reconnect renders fresh baseline", base.actor(report)["ControlLease"] > previous)
        self.save_report("client1", "reconnected", report)

    def flight_landing(self):
        self.uncovered("formal underground collapse and all rubble", "separate Editor controlled-stance probe; surface-only Player path")
        self.uncovered("weak network and save/load epoch", "not exercised by this minimum local two-process movement run")
        self.uncovered("foreground or two-machine acceptance", "background graphical Players on one machine only")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--port", type=int, default=27929)
    args = parser.parse_args()
    args.clients = 1
    args.driver = "client"
    args.backend = "mono"
    args.weak = False
    args.background = True
    args.interactive = False
    args.quick_test = "landed-ship-cargo"
    args.save_version = 26
    args.metrics = False
    args.graphics_api = "d3d11"
    args.keep_open = None
    args.skip_restarts = True
    return ProjectionNetworkRun(args).execute()


if __name__ == "__main__":
    sys.exit(main())

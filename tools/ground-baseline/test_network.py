"""Verify the ground baseline through independent Players and trusted client input."""
import argparse
from pathlib import Path
import sys
import time

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "space-planet-flow"))
from test_network import NetworkRun, actor, crew, phase, sha256, ship, ship_height, ship_x, world


class GroundRun(NetworkRun):
    """Reuse process transport while verifying only the currently retained gameplay."""

    def initial_and_competition(self):
        for role in self.roles:
            report = self.start(role)
            self.record(role + " starts outside on the planet", phase(report) == 5 and
                        ship(report)["Phase"] == 0 and not crew(report)["Boarded"])
            self.record(role + " starts without equipment", actor(report)["Slot0"] == 0 and
                        not actor(report)["JetpackOwned"])
        self.all_ready(5)
        self.assert_baseline("initial baseline")
        initial = self.ready("host")
        self.send("host", operation="input-hold", actor=actor(initial)["Id"],
                  jumpHeld=True, jumpPressed=True, horizontal=0)
        self.wait("host", lambda r: actor(r)["Height"] > actor(initial)["Height"] + 8, "ground jump")
        self.send("host", operation="input-stop")
        self.wait("host", lambda r: abs(actor(r)["Height"] - actor(initial)["Height"]) < 1 and
                  abs(actor(r)["VerticalSpeed"]) < .01, "jump lands")
        self.record("basic ground jump and landing", True)
        for role in self.roles:
            self.expect(role, role + " purchase outside shop rejected", "NoEffect", **self.buy(role, "pickaxe"))
            self.walk(role, ship_x(self.ready(role)) + 32)
            self.expect(role, role + " purchases pickaxe", **self.buy(role, "pickaxe"))
            self.wait(role, lambda r: actor(r)["Slot0"] == 2, role + " inventory received")
        report = self.ready("host")
        balance = world(report)["Camp"]["Credits"]
        frozen = self.next_raw("host", "BuyEquipment", **{k: v for k, v in self.buy("host", "jetpack").items() if k != "operation"})
        self.expect("host", "jetpack purchase accepted", **frozen)
        self.wait("host", lambda r: actor(r)["JetpackOwned"] and world(r)["Camp"]["Credits"] == balance - 14,
                  "jetpack inventory and payment")
        self.expect("host", "duplicate purchase returns cached receipt", **frozen)
        self.record("duplicate purchase does not charge twice", world(self.ready("host"))["Camp"]["Credits"] == balance - 14)
        for role in self.roles:
            r = self.ready(role)
            self.record(role + " purchased jetpack is usable", all(a["JetpackEquipped"] == a["JetpackOwned"] and
                        (a["JetpackFuel"] > 0 if a["JetpackOwned"] else a["JetpackFuel"] == 0) for a in world(r)["Actors"]))
        for command in ("depart", "unload", "board", "recall", "launch", "emergency", "robot", "cargo", "crew", "mine", "resupply", "deploy"):
            self.personal(self.driver, command, "InvalidRequest", "retired " + command + " rejected")
        for operation in ("Recruit", "StartNight", "SelectDestination", "CancelJourney", "SellCarriedOre", "UseHeroItem"):
            person = actor(self.ready(self.driver))
            self.expect(self.driver, "retired " + operation + " rejected", ("InvalidRequest", "PermissionDenied", "NoEffect"),
                        operation=operation, actors=[person["Id"]], target=ship(report)["Id"], kind="pickaxe", lease=person["ControlLease"])
        for role in self.roles:
            self.send(role, operation="input-hold", actor=actor(self.ready(role))["Id"], useHeld=True, usePressed=True)
        time.sleep(1.5)
        for role in self.roles:
            self.send(role, operation="input-stop")
        self.assert_baseline("held empty-target pickaxe does not enable old expedition gameplay")
        self.capture("ground-baseline")

    def buy(self, role, key):
        r = self.ready(role)
        person = actor(r)
        return dict(operation="BuyEquipment", actors=[person["Id"]], target=ship(r)["Id"], kind=key,
                    value=person["InventoryRevision"], lease=person["ControlLease"])

    def assert_baseline(self, label):
        r = self.ready("host")
        e = world(r)["Expedition"]
        self.record(label, len(world(r)["Actors"]) == len(self.roles) and len(world(r)["Buildings"]) == 1 and
                    not world(r)["Worksites"] and not world(r)["Projectiles"] and e["Risk"] == e["Clock"] == 0 and
                    e["Phase"] == 0 and not e["Settled"] and e["RobotModule"] == e["CargoModule"] == e["CrewModule"] == 0)

    def transient_cases(self):
        self.pause(True)
        self.expect(self.driver, "paused purchase rejected", "InvalidRequest", **self.buy(self.driver, "pistol"))
        self.pause(False)
        self.reconnect(self.late_role, "client reconnect", 5)
        self.save(0, "ground-purchase")
        self.snapshot_inventory = self.inventory(self.ready("host"))
        self.snapshot_credits = world(self.ready("host"))["Camp"]["Credits"]

    @staticmethod
    def inventory(report):
        return sorted((a["Id"], a["Slot0Definition"], a["Slot1Definition"], a["Slot2Definition"], a["Slot3Definition"],
                       a["JetpackOwned"], a["InventoryRevision"]) for a in world(report)["Actors"])

    def flight_landing(self):
        self.walk(self.driver, ship_x(self.ready(self.driver)) + 96, tolerance=6)
        self.personal(self.driver, "pilot")
        self.wait(self.driver, lambda r: ship(r)["PilotId"] == actor(r)["Id"], "pilot and lease received")
        self.personal(self.driver, "takeoff")
        self.wait("host", lambda r: ship(r)["Phase"] == 3, "ship closes ramp")
        baseline = ship_height(self.ready("host"))
        self.hold(self.driver, up=True)
        self.wait("host", lambda r: ship_height(r) > baseline + 36, "ship gains height")
        self.send(self.driver, operation="input-stop")
        self.wait("host", lambda r: abs(ship(r)["VelocityY"]) < .01, "ship hovers")
        self.record("retained driving flies and hovers", True)
        self.save(1, "flying-purchase")
        self.hold(self.driver, down=True)
        self.wait("host", lambda r: ship_height(r) < baseline + 8, "ship approaches ground")
        self.send(self.driver, operation="input-stop")
        self.wait("host", lambda r: abs(ship(r)["VelocityY"]) < .01, "ship descent brakes")
        self.personal(self.driver, "land")
        self.wait("host", lambda r: ship(r)["Phase"] == 0, "safe landing")
        self.personal(self.driver, "pilot")
        self.wait("host", lambda r: ship(r)["PilotId"] == 0, "leave pilot seat")
        self.record("retained landing and seat release", True)

    def restart_saved_stages(self):
        self.load(0)
        for role in self.roles:
            report = self.ready(role)
            self.record(role + " load restores purchase inventory", self.inventory(report) == self.snapshot_inventory and
                        world(report)["Camp"]["Credits"] == self.snapshot_credits)
        before = self.ready("host")
        person = actor(before)
        self.expect("host", "previous epoch command rejected", "EpochChanged", **self.next_raw("host", "Expedition",
                    actors=[person["Id"]], kind="pilot", lease=person["ControlLease"], epoch=before["epoch"] - 1))
        for role in reversed(self.roles):
            self.stop(role)
        self.start("host")
        self.send("host", operation="BeginLoad", value=0)
        self.wait("host", lambda r: r["ready"] and r["epoch"] > 1, "fresh process loads ground save")
        for role in self.roles[1:]:
            self.start(role)
        for role in self.roles:
            report = self.ready(role)
            self.record(role + " process restart restores purchase inventory", self.inventory(report) == self.snapshot_inventory and
                        world(report)["Camp"]["Credits"] == self.snapshot_credits)
        self.assert_baseline("restored ground baseline")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", type=Path, required=True)
    parser.add_argument("--clients", type=int, choices=(1, 3), default=1)
    parser.add_argument("--weak", action="store_true")
    parser.add_argument("--port", type=int, default=29470)
    args = parser.parse_args()
    args.backend = "mono"
    args.driver = "client"
    args.background = True
    args.interactive = False
    args.keep_open = None
    args.keep_seconds = 3600
    args.save_version = 21
    args.output_root = Path(__file__).resolve().parents[2] / "artifacts/ground-baseline-20261007"
    args.phase_hook = False
    args.skip_restarts = False
    run = GroundRun(args)
    run.driver_sha256 = sha256(Path(__file__))
    print(str(run.run), flush=True)
    return run.execute()


if __name__ == "__main__":
    sys.exit(main())

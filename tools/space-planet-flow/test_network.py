"""Exercise the real space/planet flow in independent development Players.

Commands only use the shipped SessionAutomation/SessionClient path. Each run owns
its reports and versioned saves. Neither fixture files nor successful old evidence are
used as a substitute for gameplay. Screenshots here are diagnostic hidden-player
captures, not foreground visual acceptance. Invoke again with --weak and with
--clients 3; every run records the exact executable/managed binary hashes.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import traceback


PHASES = {"Orbit": 0, "Preparing": 1, "Transit": 2, "ArrivalSync": 3, "Descent": 4, "Landed": 5}


def phase(report):
    value = journey(report)["Phase"]
    return PHASES.get(value, value)


def world(report):
    return report["frame"]["World"]


def journey(report):
    return world(report)["Expedition"]["Journey"]


def actor(report, slot=None):
    slot = report["slot"] if slot is None else slot
    return next(a for a in world(report)["Actors"] if a["ControllerSlot"] == slot)


def ship(report):
    return world(report)["Expedition"]["Ship"]


def ship_height(report):
    return next(d["Height"] for d in world(report)["Expedition"]["Devices"] if d["Id"] == ship(report)["Id"])


def ship_x(report):
    return next(b["X"] for b in world(report)["Buildings"] if b["Id"] == ship(report)["Id"])


def crew(report, person=None):
    person = actor(report) if person is None else person
    return next(a for a in world(report)["Expedition"]["Crew"] if a["Id"] == person["Id"])


def enabled_planets(report):
    return [p for p in journey(report)["Planets"] if p["Enabled"]]


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class NetworkRun:
    """Bounded process orchestration and evidence; never edits authority state."""

    def __init__(self, args):
        self.args = args
        self.repo = Path(__file__).resolve().parents[2]
        self.player = args.player.resolve()
        if not self.player.is_file():
            raise FileNotFoundError(self.player)
        root = args.output_root.resolve() if args.output_root else self.repo / "artifacts/space-planet-flow"
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S-%f")
        self.run = root / f"network-{stamp}-{args.backend}-{'weak' if args.weak else 'normal'}-{args.clients + 1}p-{args.driver}"
        self.run.mkdir(parents=True, exist_ok=False)
        self.saves = self.run / "saves"
        self.processes = {}
        self.attempts = Counter()
        self.paths = {}
        self.command_counts = Counter()
        self.checks = []
        self.not_covered = []
        self.trace_keys = {}
        self.last_projected = {}
        self.relay = None
        self.roles = ["host"] + [f"client{i}" for i in range(1, args.clients + 1)]
        self.driver = "host" if args.driver == "host" else "client1"
        self.late_role = self.roles[-1]
        self.saved = {}
        self.save_version = getattr(args, "save_version", 11)
        self.error = None
        self.driver_sha256 = sha256(Path(__file__))
        self.raw_sequence = 10000
        self.started_at = datetime.now(timezone.utc).isoformat()
        self.write_status("prepared")

    def write_status(self, stage, **extra):
        value = dict(stage=stage, checks=len(self.checks), failed=sum(not c["passed"] for c in self.checks),
                     utc=datetime.now(timezone.utc).isoformat(), **extra)
        temporary = self.run / "status.tmp"
        temporary.write_text(json.dumps(value, ensure_ascii=False), encoding="utf-8")
        os.replace(temporary, self.run / "status.json")

    def record(self, name, ok, detail=None):
        entry = dict(name=name, passed=bool(ok), detail=detail, utc=datetime.now(timezone.utc).isoformat())
        self.checks.append(entry)
        self.write_status(name)
        if not ok:
            raise AssertionError(f"{name}: {detail}")

    def uncovered(self, name, reason):
        self.not_covered.append(dict(name=name, reason=reason))

    def read(self, role):
        path = self.paths[role]["report"]
        for _ in range(10):
            try:
                result = json.loads(path.read_text(encoding="utf-8-sig"))
                if result.get("error"):
                    raise RuntimeError(role + ": " + result["error"])
                if result.get("frame"):
                    self.last_projected[role] = result
                self.trace(role, result)
                return result
            except (OSError, json.JSONDecodeError):
                time.sleep(.02)
        return None

    def trace(self, role, report):
        if not report.get("frame") or not world(report).get("Expedition"):
            return
        j = journey(report)
        terrain = report.get("terrain") or {}
        key = (report["epoch"], phase(report), report["ready"], report["readyCount"], j["Revision"], ship(report)["PilotId"],
               terrain.get("dataReady"), terrain.get("visible"), terrain.get("worldId"), terrain.get("mapEpoch"))
        if self.trace_keys.get(role) == key:
            return
        self.trace_keys[role] = key
        row = dict(role=role, utc=report["utc"], epoch=report["epoch"], phase=phase(report),
                   ready=report["ready"], readyCount=report["readyCount"], journey=j,
                   terrain=report.get("terrain"), ship=ship(report), shipX=ship_x(report), shipHeight=ship_height(report))
        with (self.run / "transitions.jsonl").open("a", encoding="utf-8") as output:
            output.write(json.dumps(row, ensure_ascii=False) + "\n")

    def wait(self, role, predicate, description, seconds=100):
        end = time.monotonic() + seconds
        last = None
        while time.monotonic() < end:
            last = self.read(role)
            if last and predicate(last):
                return last
            process = self.processes[role]
            if process.poll() is not None:
                raise RuntimeError(f"{role} exited {process.returncode}: {description}")
            time.sleep(.15)
        if last:
            self.save_report(role, "timeout-" + description.replace(" ", "-")[:50], last)
        if not last or not last.get("frame"):
            projected = self.last_projected.get(role)
            if projected:
                self.save_report(role, "last-projection-before-timeout", projected)
        raise TimeoutError(role + ": " + description)

    def ready(self, role):
        return self.wait(role, lambda r: r["ready"] and r.get("frame") and r["terrain"]["visible"], "ready")

    def save_report(self, role, name, report=None):
        report = report or self.read(role)
        (self.run / f"{name}-{role}.snapshot.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    def start(self, role):
        self.attempts[role] += 1
        prefix = f"{role}-{self.attempts[role]}"
        self.paths[role] = {"report": self.run / (prefix + ".json"), "commands": self.run / (prefix + ".commands"),
                            "log": self.run / (prefix + ".log")}
        self.paths[role]["commands"].write_text("", encoding="utf-8")
        self.command_counts[role] = 0
        self.last_projected.pop(role, None)
        endpoint = self.args.port + 1 if self.args.weak and role != "host" else self.args.port
        command = [str(self.player), "-screen-width", "1280", "-screen-height", "720",
                   "-screen-fullscreen", "0", "-logFile", str(self.paths[role]["log"]),
                   "--dn-role", role, "--dn-port", str(endpoint),
                   "--dn-map-seed", "SPACE-PLANET-0926", "--dn-save-dir", str(self.saves),
                   "--dn-report", str(self.paths[role]["report"]), "--dn-commands", str(self.paths[role]["commands"])]
        if not self.args.interactive:
            command.append("--dn-input-replay")
        if role == "host" and getattr(self.args, "quick_test", None):
            command.extend(["--dn-quick-test", self.args.quick_test])
        if getattr(self.args, "metrics", False):
            command.append("--dn-metrics")
        # SpaceReady requires a real camera completion; batchmode receives the map but never renders it.
        # Both automated and native-input runs keep a graphics window. Do not use -batchmode/-nographics.
        startup = None
        if os.name == "nt" and getattr(self.args, "background", False):
            startup = subprocess.STARTUPINFO()
            startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            startup.wShowWindow = 4  # SW_SHOWNOACTIVATE: retain graphics without requesting focus.
        self.processes[role] = subprocess.Popen(command, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
                                                startupinfo=startup)
        self.write_status("starting " + role)
        return self.wait(role, lambda report: report["ready"] and report.get("frame") and report["terrain"]["visible"] and
                         any(person["ControllerSlot"] == report["slot"] for person in world(report)["Actors"]),
                         "ready with controlled actor")

    def send(self, role, **command):
        with self.paths[role]["commands"].open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(command, ensure_ascii=False) + "\n")
        self.command_counts[role] += 1
        return self.command_counts[role]

    def send_batch(self, role, commands):
        text = "".join(json.dumps(command, ensure_ascii=False) + "\n" for command in commands)
        with self.paths[role]["commands"].open("a", encoding="utf-8") as stream:
            stream.write(text)
        self.command_counts[role] += len(commands)
        return self.command_counts[role]

    def consumed(self, role, count):
        return self.wait(role, lambda r: r["commandsConsumed"] >= count, "automation command consumed")

    @staticmethod
    def feedback_key(value):
        return tuple(value.get(k) for k in ("Epoch", "ConnectionGeneration", "Sequence", "Code", "ReadyReply"))

    def begin_receipt(self, role, **command):
        if command.get("operation") != "raw":
            payload = dict(command)
            intent = payload.pop("operation")
            command = self.next_raw(role, intent, **payload)
        before = self.ready(role)
        counts = Counter(self.feedback_key(v) for v in before["feedback"] if not v["ReadyReply"])
        consumed = self.send(role, **command)
        return before["epoch"], counts, consumed, command.get("sequence")

    def finish_receipt(self, role, marker, seconds=100):
        epoch, previous, consumed, sequence = marker
        found = []
        def changed(report):
            if report["commandsConsumed"] < consumed:
                return False
            counts = Counter()
            for feedback in report["feedback"]:
                epoch_changed = (sequence is not None and feedback["Code"] == "EpochChanged" and
                                 feedback["Epoch"] > epoch and feedback["Epoch"] == report["epoch"])
                if feedback["ReadyReply"] or feedback["Epoch"] != epoch and not epoch_changed:
                    continue
                if sequence is not None and feedback["Sequence"] != sequence:
                    continue
                key = self.feedback_key(feedback)
                counts[key] += 1
                if counts[key] > previous[key]:
                    found[:] = [feedback]
                    return True
            return False
        self.wait(role, changed, "command receipt", seconds)
        return found[0]

    def receipt(self, role, **command):
        return self.finish_receipt(role, self.begin_receipt(role, **command))

    def expect(self, role, name, expected="Applied", **command):
        result = self.receipt(role, **command)
        self.record(name, result["Code"] in ([expected] if isinstance(expected, str) else expected), result)
        return result

    def request(self, role, operation="SelectDestination", planet=None, report=None, **changes):
        r = report or self.ready(role)
        person = actor(r)
        result = dict(operation=operation, actors=[person["Id"]], target=ship(r)["Id"],
                      kind="" if operation == "CancelJourney" else planet or enabled_planets(r)[0]["Id"],
                      value=journey(r)["Revision"], lease=person["ControlLease"])
        result.update(changes)
        return result

    def personal(self, role, kind, expected="Applied", name=None):
        person = actor(self.ready(role))
        return self.expect(role, name or role + " " + kind, expected, operation="Expedition", kind=kind,
                           actors=[person["Id"]], lease=person["ControlLease"])

    def hold(self, role, horizontal=0, up=False, down=False):
        person = actor(self.ready(role))
        self.send(role, operation="input-hold", actor=person["Id"], horizontal=horizontal,
                  jumpHeld=up, dropHeld=down)

    def hover_without_pilot(self, name, seconds=1.3):
        before = self.wait("host", lambda r: phase(r) == PHASES["Descent"] and ship(r)["PilotId"] == 0 and
                           ship(r)["VelocityX"] == ship(r)["VelocityY"] == 0, name + " revokes thrust")
        time.sleep(seconds)
        after = self.ready("host")
        self.record(name + " safely hovers without pilot", phase(after) == PHASES["Descent"] and
                    ship(after)["PilotId"] == 0 and ship(after)["VelocityX"] == ship(after)["VelocityY"] == 0 and
                    abs(ship_height(after) - ship_height(before)) < .001 and abs(ship_x(after) - ship_x(before)) < .001)
        return after

    def automatic_landing(self, name):
        """Move through trusted input, release it above the platform, then await authority landing and opening."""
        before = self.wait(self.driver, lambda r: r["ready"] and r.get("frame") and
                           ship(r)["PilotId"] == actor(r)["Id"], name + " driver projection catches pilot")
        lease = actor(before)["ControlLease"]
        self.record(name + " begins with designated pilot", ship(before)["PilotId"] == actor(before)["Id"])
        self.hold(self.driver, down=True)
        self.wait("host", lambda r: ship_height(r) - ship(r)["DockHeight"] < 64,
                  name + " active downward thrust", 100)
        self.send(self.driver, operation="input-stop")
        self.wait("host", lambda r: phase(r) == PHASES["Landed"], name + " automatic safe landing", 120)
        h = self.consistency(name, PHASES["Landed"])
        self.record(name + " releases seat and stale input", ship(h)["PilotId"] == 0 and
                    ship(h)["VelocityX"] == ship(h)["VelocityY"] == 0 and
                    actor(self.ready(self.driver))["ControlLease"] > lease)
        self.record(name + " snaps to supported platform", abs(ship_x(h) - ship(h)["DockX"]) < .001 and
                    abs(ship_height(h) - ship(h)["DockHeight"]) < .001)
        self.wait("host", lambda r: ship(r)["Phase"] == 0 and ship(r)["DoorClock"] == 0,
                  name + " ramp opening completes")
        return self.ready("host")

    def walk(self, role, x, tolerance=12, seconds=50):
        end = time.monotonic() + seconds
        braking_distance = 50 if self.args.weak else 28
        while time.monotonic() < end:
            r = self.ready(role)
            distance = x - actor(r)["X"]
            if abs(distance) > tolerance:
                direction = 1 if distance > 0 else -1
                person = actor(r)
                count = self.send(role, operation="input-hold", actor=person["Id"], horizontal=direction,
                                  jumpHeld=False, dropHeld=False)
                self.consumed(role, count)
                while time.monotonic() < end:
                    local = self.ready(role)
                    authoritative = actor(self.ready("host"), local["slot"])
                    if direction * (x - authoritative["X"]) <= braking_distance:
                        break
                    time.sleep(.04)
            else:
                direction = 0
            local = self.ready(role)
            count = self.send(role, operation="input-hold", actor=actor(local)["Id"], horizontal=0,
                              jumpHeld=False, dropHeld=False)
            self.consumed(role, count)
            time.sleep(.7 if self.args.weak else .35)
            local = self.ready(role)
            authoritative = actor(self.ready("host"), local["slot"])
            if (abs(actor(local)["X"] - x) <= tolerance and
                    abs(authoritative["X"] - x) <= tolerance and
                    not authoritative["Walking"]):
                return
            if direction:
                braking_distance = max(0, braking_distance - direction * (x - authoritative["X"]))
        self.hold(role)
        raise TimeoutError(f"walk {role} to {x}")

    def all_ready(self, expected_phase=None):
        reports = {role: self.ready(role) for role in self.roles}
        epoch = reports["host"]["epoch"]
        for role in self.roles:
            reports[role] = self.wait(role, lambda r: r["ready"] and r["epoch"] == epoch and
                                      (expected_phase is None or phase(r) == expected_phase), "shared epoch/phase")
        return reports

    def consistency(self, name, expected_phase):
        reports = self.all_ready(expected_phase)
        host = reports["host"]
        identity = (journey(host)["JourneyId"], journey(host)["MapId"], journey(host)["PlanetId"], journey(host)["ContentFingerprint"],
                    host["terrain"]["sha256"], host["terrain"]["backgroundHash"])
        for role, value in reports.items():
            current = (journey(value)["JourneyId"], journey(value)["MapId"], journey(value)["PlanetId"], journey(value)["ContentFingerprint"],
                       value["terrain"]["sha256"], value["terrain"]["backgroundHash"])
            self.record(name + " " + role + " synchronized", current == identity and
                        value["terrain"]["epoch"] == value["epoch"] and value["terrain"]["dataReady"] and
                        value["terrain"]["visible"], current)
            if expected_phase >= PHASES["ArrivalSync"]:
                actual = value["terrain"].get("worldId")
                if actual is None:
                    self.uncovered(name + " actual terrain identity", "development report lacks terrain.worldId/mapEpoch")
                else:
                    self.record(name + " " + role + " actual map identity",
                                str(actual).replace("-", "") == journey(value)["MapId"].replace("-", "") and
                                value["terrain"].get("mapEpoch", 0) > 0, value["terrain"])
            self.save_report(role, name, value)
        return host

    def pause(self, value):
        self.expect("host", "pause=" + str(value), operation="SetPaused", value=int(value))
        host = self.wait("host", lambda r: r["frame"]["Paused"] == bool(value), "pause reflected")
        # SetPaused renews every controlled actor's lease; wait for each peer's corresponding projection.
        for role in self.roles[1:]:
            self.wait(role, lambda r: r["ready"] and r["epoch"] == host["epoch"] and
                      r["frame"]["Paused"] == bool(value), "peer receives pause and renewed lease")
        return host

    def save(self, slot, label):
        path = self.saves / f"v{self.save_version}/slot-{slot:02d}.dnsave.json"
        modified = path.stat().st_mtime_ns if path.exists() else -1
        self.expect("host", label + " save accepted", operation="Save", value=slot)
        r = self.wait("host", lambda v: not v["storageBusy"] and path.exists() and
                      path.stat().st_mtime_ns != modified, label + " disk save")
        frozen = json.loads(path.read_text(encoding="utf-8-sig"))
        self.record(label + f" v{self.save_version} disk format", frozen["format_version"] == self.save_version)
        self.saved[slot] = dict(label=label, phase=phase(r), journey=journey(r), ship=ship(r),
                               height=ship_height(r), x=ship_x(r), terrain=r["terrain"],
                               actorIds=sorted(a["Id"] for a in world(r)["Actors"]),
                               stock=world(r)["Camp"]["Stock"], path=str(path), sha256=sha256(path))
        self.save_report("host", label + "-saved", r)

    def load(self, slot):
        before = self.ready("host")
        self.send("host", operation="BeginLoad", value=slot)
        self.wait("host", lambda r: r["ready"] and r["epoch"] > before["epoch"], "load completed")
        reports = self.all_ready(self.saved[slot]["phase"])
        return reports["host"]

    def reconnect(self, role, label, expected_phase):
        before = self.ready(role)
        person_id = actor(before)["Id"]
        self.send(role, operation="disconnect")
        self.wait(role, lambda r: not r["ready"], "disconnect")
        if self.args.weak:
            # UDP may lose the close packet. The server must retire the old lease before
            # the same recovery token can claim its slot without pre-empting an active peer.
            released_at = time.monotonic()
            self.wait("host", lambda r: any(a["Id"] == person_id and a["ControllerSlot"] < 0
                for a in world(r)["Actors"]), "server releases " + label, seconds=35)
            self.record(label + " old authority released", True,
                        {"wait_seconds": round(time.monotonic() - released_at, 2)})
        self.send(role, operation="connect")
        after = self.wait(role, lambda r: r["ready"] and r.get("frame") and phase(r) == expected_phase and
                          any(a["ControllerSlot"] == r["slot"] for a in world(r)["Actors"]),
                          "reconnect " + label)
        self.record(label + " identity recovered", actor(after)["Id"] == person_id)
        self.record(label + " role inside ship", crew(after)["Boarded"])
        return after

    def capture(self, name):
        self.send("host", operation="capture", file=name + ".png")
        self.wait("host", lambda r: (self.run / (name + ".png")).is_file(), "diagnostic capture")

    def next_raw(self, role, intent, **payload):
        r = self.ready(role)
        highest = max((f["Sequence"] for f in r["feedback"]), default=0)
        self.raw_sequence = max(self.raw_sequence + 10, highest + 10)
        return dict(operation="raw", intent=intent, sequence=self.raw_sequence, **payload)

    def initial_and_competition(self):
        self.write_status("initial orbit and concurrency")
        self.start("host")
        for role in self.roles[1:]:
            self.start(role)
        h = self.consistency("orbit", PHASES["Orbit"])
        self.record("orbit has no mineral deposits", not any(w["IsMineralDeposit"] for w in world(h)["Worksites"]))
        self.record("orbit ground expedition inactive", world(h)["Expedition"]["Phase"] == 0)
        self.record("one distinct actor per connected player", len({actor(self.ready(r))["Id"] for r in self.roles}) == len(self.roles))
        for role in self.roles:
            self.record(role + " starts boarded", crew(self.ready(role))["Boarded"])
        self.expect("client1", "distant navigation rejected", "NoEffect", **self.request("client1"))
        self.hold("client1", -1)
        time.sleep(1.5)
        self.hold("client1")
        c = self.ready("client1")
        self.record("closed space door bounds actor", crew(c)["Boarded"] and actor(c)["X"] >= ship_x(c) - 72 - .1)
        for role in ("host", "client1"):
            self.walk(role, ship_x(self.ready(role)) + 96)
        self.capture("00-space-cockpit")
        foreign = actor(self.ready("client1"))
        self.expect("host", "foreign hero cannot select", "PermissionDenied",
                    **self.request("host", actors=[foreign["Id"]], lease=foreign["ControlLease"]))
        self.expect("client1", "unknown planet rejected", "NoEffect", **self.request("client1", planet="missing-planet"))
        self.expect("client1", "stale journey revision rejected", "NoEffect",
                    **self.request("client1", value=journey(self.ready("client1"))["Revision"] - 1))
        self.pause(True)
        self.save(0, "orbit")
        paused = self.ready("client1")
        # ObjectSessionCommands.Valid rejects paused navigation before SessionJourneyControl is invoked.
        self.expect("client1", "paused navigation rejected", "InvalidRequest", **self.request("client1"))
        current = self.ready("client1")
        self.record("paused navigation does not alter journey or seat", journey(current) == journey(paused) and
                    ship(current)["PilotId"] == ship(paused)["PilotId"])
        self.pause(False)
        planets = enabled_planets(self.ready("host"))
        if len(planets) < 2:
            self.uncovered("different-destination competition", "formal catalog has one enabled planet; same-destination race still tested")
        before = {role: self.ready(role) for role in ("host", "client1")}
        choices = {"host": planets[0]["Id"], "client1": planets[-1]["Id"]}
        markers = {}
        for role in ("host", "client1"):
            markers[role] = self.begin_receipt(role, **self.request(role, planet=choices[role], report=before[role]))
        outcomes = {role: self.finish_receipt(role, marker) for role, marker in markers.items()}
        winners = [role for role, result in outcomes.items() if result["Code"] == "Applied"]
        self.record("concurrent confirmation exactly one winner", len(winners) == 1, outcomes)
        winner = winners[0]
        h = self.wait("host", lambda r: r["ready"] and phase(r) == PHASES["Descent"], "competition arrives", 150)
        self.record("winner and destination atomic", ship(h)["PilotId"] == actor(before[winner])["Id"] and
                    journey(h)["PlanetId"] == choices[winner])
        self.record("journey replaces epoch once", h["epoch"] == before["host"]["epoch"] + 1)
        self.record("original crew preserved across swap", all(actor(before[r])["Id"] in [a["Id"] for a in world(h)["Actors"]]
                    for r in before))
        self.consistency("concurrent-arrival", PHASES["Descent"])

    def transient_cases(self):
        self.write_status("transient states, cancellation and disconnect")
        self.load(0)
        self.pause(False)
        self.reconnect(self.late_role, "orbit reconnect", PHASES["Orbit"])
        if not self.args.phase_hook:
            self.uncovered("Preparing/Transit/ArrivalSync network cases", "--phase-hook not enabled; authority lifecycle tests cover transient phases")
            return
        for stage in ("Preparing", "Transit", "ArrivalSync"):
            if phase(self.ready("host")) != PHASES["Orbit"]:
                self.load(0)
                self.pause(False)
            self.send("host", operation="pause-on-journey-phase", value=PHASES[stage])
            selected_by = "client1" if stage == "Transit" else "host"
            if selected_by != "host":
                current = self.ready("host")
                self.wait(selected_by, lambda r: r["ready"] and phase(r) == PHASES["Orbit"] and
                          journey(r)["Revision"] == journey(current)["Revision"] and
                          actor(r)["ControlLease"] == next(a["ControlLease"] for a in world(current)["Actors"]
                          if a["Id"] == actor(r)["Id"]), stage + " replica catches current orbit", 40)
            request = self.request(selected_by)
            request.pop("operation")
            frozen = self.next_raw(selected_by, "SelectDestination", **request)
            self.expect(selected_by, stage + " selection", **frozen)
            held = self.wait("host", lambda r: r.get("frame") and
                             (r["frame"]["Paused"] and phase(r) == PHASES[stage] or phase(r) > PHASES[stage]),
                             stage + " pause or documented miss", 150)
            if phase(held) != PHASES[stage] or not held["frame"]["Paused"]:
                self.send("host", operation="pause-on-journey-phase", value=-1)
                self.uncovered(stage + " network suspension", "phase passed before host pause hook took effect")
                self.wait("host", lambda r: r["ready"] and phase(r) == PHASES["Descent"], "missed stage reaches stable descent", 150)
                continue
            if not held["ready"]:
                held = self.ready("host")
            self.expect("host", stage + " refuses unstable save", "Loading", operation="Save", value=8)
            self.record(stage + " does not overwrite save", not (self.saves / f"v{self.save_version}/slot-08.dnsave.json").exists())
            if stage != "Transit" or self.late_role != selected_by:
                self.reconnect(self.late_role, stage + " reconnect", PHASES[stage])
            baseline = journey(self.ready("host"))["PhaseElapsed"]
            time.sleep(.8)
            self.record(stage + " phase timer pauses", abs(journey(self.ready("host"))["PhaseElapsed"] - baseline) < .0001)
            self.save_report("host", stage.lower() + "-paused")
            if stage == "Preparing":
                revision = journey(self.ready("host"))["Revision"]
                self.expect(selected_by, "select retransmit reuses receipt", **frozen)
                self.record("select retransmit does not create new journey", journey(self.ready("host"))["Revision"] == revision)
                conflict = dict(frozen, kind="different-payload")
                self.expect(selected_by, "conflicting reused sequence rejected", "SequenceConflict", **conflict)
                stale = dict(frozen, sequence=frozen["sequence"] - 1)
                self.expect(selected_by, "out of order older sequence rejected", "SequenceExpired", **stale)
                current = self.ready("host")
                counts = Counter(self.feedback_key(v) for v in current["feedback"] if not v["ReadyReply"])
                request = self.request("host", "CancelJourney", report=current)
                request.pop("operation")
                # The immediately preceding trusted SetPaused(false) renews this lease once in the same command batch.
                request["lease"] += 1
                cancel = self.next_raw("host", "CancelJourney", **request)
                consumed = self.send_batch("host", [dict(operation="SetPaused", value=0), cancel])
                result = self.finish_receipt("host", (current["epoch"], counts, consumed, cancel["sequence"]))
                self.record("preparing cancel accepted", result["Code"] == "Applied", result)
                h = self.wait("host", lambda r: phase(r) == PHASES["Orbit"], "cancel returns orbit")
                self.record("cancel clears destination seat and candidate", ship(h)["PilotId"] == 0 and
                            not journey(h)["PlanetId"] and not journey(h)["MapId"])
            elif stage == "Transit":
                old_epoch = held["epoch"]
                person_id = actor(self.ready(selected_by))["Id"]
                self.send(selected_by, operation="disconnect")
                self.wait(selected_by, lambda r: not r["ready"], "driver disconnect during transit")
                aborted = self.wait("host", lambda r: phase(r) == PHASES["Orbit"], "precommit driver abort")
                self.record("precommit disconnect preserves orbit epoch", aborted["epoch"] == old_epoch and ship(aborted)["PilotId"] == 0)
                self.send(selected_by, operation="connect")
                restored = self.wait(selected_by, lambda r: r["ready"] and phase(r) == PHASES["Orbit"], "aborted driver reconnect")
                self.record("aborted driver restores original actor", actor(restored)["Id"] == person_id)
                self.pause(False)
            else:
                self.pause(False)
                self.wait("host", lambda r: phase(r) == PHASES["Descent"], "arrival sync releases descent")

    def flight_landing(self):
        self.write_status("designated driver, suspension, descent and landing")
        self.load(0)
        self.pause(False)
        before = self.ready(self.driver)
        old_epoch = before["epoch"]
        old_lease = actor(before)["ControlLease"]
        self.expect(self.driver, "designated driver selects", **self.request(self.driver))
        h = self.wait("host", lambda r: r["ready"] and phase(r) == PHASES["Descent"], "designated arrival", 150)
        self.all_ready(PHASES["Descent"])
        current = self.ready(self.driver)
        self.record("arrival begins genuinely above ground", ship_height(h) > ship(h)["DockHeight"] + 30)
        self.record("arrival changes control lease", actor(current)["ControlLease"] > old_lease)
        self.personal(self.driver, "pilot", name="arrival pilot leaves seat to hold staging altitude")
        self.hover_without_pilot("arrival seat release")
        self.expect(self.driver, "old epoch command rejected", "EpochChanged", **self.next_raw(self.driver, "Expedition",
                    epoch=old_epoch, actors=[actor(current)["Id"]], kind="pilot", lease=old_lease))
        self.expect(self.driver, "old lease command rejected", "PermissionDenied", operation="Expedition", kind="pilot",
                    actors=[actor(current)["Id"]], lease=old_lease)
        self.personal(self.driver, "pilot", name="driver explicitly takes over airborne ship")
        passenger = "client1" if self.driver == "host" else "host"
        passenger_before = self.ready(passenger)
        relative = actor(passenger_before)["Height"] - ship_height(passenger_before)
        altitude = ship_height(self.ready("host"))
        self.hold(self.driver, up=True)
        lifted = self.wait("host", lambda r: ship_height(r) >= altitude + 24, "pilot real upward thrust")
        self.send(self.driver, operation="input-stop")
        h = self.wait("host", lambda r: ship(r)["VelocityY"] < 0, "released vertical input begins slow descent")
        descending_height = ship_height(h)
        h = self.wait("host", lambda r: ship_height(r) < descending_height - 4, "neutral input really lowers altitude")
        self.record("passenger carried with moving ship", abs(actor(h, passenger_before["slot"])["Height"] - ship_height(h) - relative) < .1)
        self.record("released input uses slower descent than powered ascent", -ship(h)["VelocityY"] < ship(lifted)["VelocityY"])
        self.capture("01-planet-airborne")
        self.personal(self.driver, "pilot", name="pilot can leave seat airborne")
        self.hover_without_pilot("midair seat release")
        self.record("midair leaving seat remains boarded", crew(self.ready(self.driver))["Boarded"])
        self.walk(self.driver, ship_x(self.ready(self.driver)) + 96)
        self.personal(self.driver, "pilot", name="pilot can reclaim airborne")
        self.pause(True)
        self.save(1, "descent")
        self.pause(False)
        reconnect_driver = self.driver
        if reconnect_driver == "host":
            self.personal("host", "pilot", name="host yields for guest disconnect case")
            reconnect_driver = "client1"
            self.walk(reconnect_driver, ship_x(self.ready(reconnect_driver)) + 96)
            self.personal(reconnect_driver, "pilot", name="guest temporarily pilots for disconnect case")
        self.reconnect(reconnect_driver, "airborne driver reconnect", PHASES["Descent"])
        self.hover_without_pilot("driver disconnect and reconnect")
        self.personal(self.driver, "pilot", name="reconnected driver explicitly reclaims")
        # Change policy after obtaining a guest seat, proving control revocation through the trusted path.
        if self.driver != "host":
            self.expect("host", "switch HostOnly", operation="SetControlMode", value=1)
            self.hover_without_pilot("HostOnly policy revokes guest seat")
            self.expect("host", "restore SharedCamp", operation="SetControlMode", value=0)
            self.wait(self.driver, lambda r: any(a["ControllerSlot"] == r["slot"] for a in world(r)["Actors"]), "guest hero restored")
            self.personal(self.driver, "pilot", name="shared control reacquires guest driver")
        h = self.automatic_landing("landed")
        self.record("ground phase starts once", world(h)["Expedition"]["Phase"] == 1 and ship(h)["Phase"] == 0 and
                    abs(ship_height(h) - ship(h)["DockHeight"]) < .001)
        self.personal(self.driver, "land", "NoEffect", "retired manual landing cannot restart exploration")
        self.walk(self.driver, ship_x(self.ready(self.driver)) - 184)
        time.sleep(1)
        outside = self.ready(self.driver)
        self.record("actor walks down ramp onto supported planet", not crew(outside)["Boarded"] and
                    abs(actor(outside)["Height"] - ship(outside)["DockHeight"]) < .1)
        self.capture("02-landed-outside")
        self.walk(self.driver, ship_x(self.ready(self.driver)) - 40)
        self.record("actor walks back into landed ship", crew(self.ready(self.driver))["Boarded"])
        self.reconnect(self.late_role, "landed reconnect", PHASES["Landed"])
        self.pause(True)
        self.save(2, "landed")

    def restart_saved_stages(self):
        self.write_status("real process restarts for stable saves")
        for slot in (0, 1, 2):
            expected = self.saved[slot]
            for role in reversed(list(self.processes)):
                self.stop(role)
            self.start("host")
            # Load the frozen world before clients arrive; no bootstrap replacements count as restored actors.
            before = self.ready("host")["epoch"]
            self.send("host", operation="BeginLoad", value=slot)
            h = self.wait("host", lambda r: r["ready"] and r["epoch"] > before, "restart load " + expected["label"])
            for role in self.roles[1:]:
                self.start(role)
            h = self.consistency("restart-" + expected["label"], expected["phase"])
            self.record(expected["label"] + " restores ship pose", abs(ship_x(h) - expected["x"]) < .01 and
                        abs(ship_height(h) - expected["height"]) < .01)
            self.record(expected["label"] + " clears stale driver and velocity", ship(h)["PilotId"] == 0 and
                        ship(h)["VelocityX"] == ship(h)["VelocityY"] == 0)
            self.record(expected["label"] + " restores final terrain hash", h["terrain"]["sha256"] == expected["terrain"]["sha256"] and
                        h["terrain"]["backgroundHash"] == expected["terrain"]["backgroundHash"])
            self.record(expected["label"] + " restores generation fingerprint",
                        journey(h)["ContentFingerprint"] == expected["journey"]["ContentFingerprint"] and
                        journey(h)["MapId"] == expected["journey"]["MapId"] and
                        journey(h)["Seed"] == expected["journey"]["Seed"])
            self.record(expected["label"] + " restores original actors", sorted(a["Id"] for a in world(h)["Actors"]) == expected["actorIds"])
            self.record(expected["label"] + " restores stock", world(h)["Camp"]["Stock"] == expected["stock"])
            if expected["phase"] == PHASES["Descent"]:
                self.pause(False)
                self.hover_without_pilot("restored descent without reclaimed pilot")

    def stop(self, role):
        process = self.processes.get(role)
        if process is None:
            return
        if process.poll() is None:
            try:
                self.send(role, operation="quit")
                process.wait(8)
            except (OSError, subprocess.TimeoutExpired):
                process.terminate()
                try:
                    process.wait(5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(5)

    def keep_open(self):
        """A separate manual observation mode; never claims the full matrix passed."""
        for role in self.roles:
            self.start(role)
        target = PHASES[self.args.keep_open.capitalize()]
        self.consistency("manual-start-orbit", PHASES["Orbit"])
        if target > PHASES["Orbit"]:
            self.walk(self.driver, ship_x(self.ready(self.driver)) + 96)
            self.expect(self.driver, "manual setup destination", **self.request(self.driver))
            self.wait("host", lambda r: r["ready"] and phase(r) == PHASES["Descent"], "manual setup arrival", 150)
            self.all_ready(PHASES["Descent"])
        if target == PHASES["Landed"]:
            self.automatic_landing("manual setup landing")
        elif target == PHASES["Descent"]:
            self.personal(self.driver, "pilot", name="manual setup releases seat to hold altitude")
            self.hover_without_pilot("manual airborne staging")
        for role in self.roles:
            self.send(role, operation="input-stop")
        stop_file = self.run / "stop.flag"
        details = dict(mode="manual-observation", phase=self.args.keep_open, stop_file=str(stop_file),
                       interactive=self.args.interactive,
                       paths={role: {key: str(value) for key, value in paths.items()} for role, paths in self.paths.items()})
        self.write_status("manual-ready", **details)
        print(json.dumps(dict(status="manual-ready", run=str(self.run), **details), ensure_ascii=False), flush=True)
        self.uncovered("full automated matrix", "manual --keep-open mode selected; observations require separate human evidence")
        deadline = time.monotonic() + self.args.keep_seconds
        while time.monotonic() < deadline and not stop_file.exists():
            for role in self.roles:
                self.read(role)
                if self.processes[role].poll() is not None:
                    raise RuntimeError(role + " exited during manual observation")
            time.sleep(.5)
        for role in self.roles:
            self.save_report(role, "manual-final")

    def execute(self):
        try:
            if self.args.weak:
                self.relay = subprocess.Popen([sys.executable, str(self.repo / "tools/lan-netem.py"),
                    "--listen", str(self.args.port + 1), "--target", str(self.args.port), "--loss", ".05",
                    "--delay", ".1", "--jitter", ".025", "--duration", "5400", "--report", str(self.run / "relay.json")],
                    creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            if self.args.keep_open:
                self.keep_open()
            else:
                self.initial_and_competition()
                self.transient_cases()
                self.flight_landing()
                if self.args.skip_restarts:
                    self.uncovered("stable saves actual process restart", "explicit --skip-restarts")
                else:
                    self.restart_saved_stages()
            for path in self.run.glob("*.log"):
                text = path.read_text(encoding="utf-8", errors="replace")
                failures = [line for line in text.splitlines() if any(word in line for word in
                            ("Exception:", "InvalidKeyException", "MissingMethodException", "TypeLoadException"))]
                self.record(path.name + " has no runtime exception", not failures, failures[:8])
        except Exception:
            self.error = traceback.format_exc()
        finally:
            for role in reversed(list(self.processes)):
                self.stop(role)
            if self.relay:
                (self.run / "relay.json.stop").write_text("", encoding="utf-8")
                try:
                    self.relay.wait(8)
                except subprocess.TimeoutExpired:
                    self.relay.terminate()
                    self.relay.wait(5)
            result = dict(passed=self.error is None, started=self.started_at, finished=datetime.now(timezone.utc).isoformat(),
                          backend=self.args.backend, player=str(self.player), player_sha256=sha256(self.player),
                          driver_sha256=self.driver_sha256,
                          managed_sha256={p.name: sha256(p) for p in (self.player.parent / "DarkNights_Data/Managed").glob("DarkNights.*.dll")},
                          game_assembly_sha256=sha256(self.player.parent / "GameAssembly.dll") if (self.player.parent / "GameAssembly.dll").exists() else None,
                          weak=self.args.weak, netem="200 ms RTT + 5% loss + 25 ms jitter" if self.args.weak else None,
                          clients=self.args.clients, save_version=self.save_version, designated_driver=self.driver, mode="manual-observation" if self.args.keep_open else "automated-network",
                          checks=self.checks, not_covered=self.not_covered,
                          saves=self.saved, error=self.error, visual_acceptance=False, command=sys.argv)
            target = self.run / "result.json"
            target.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
            self.write_status("complete" if self.error is None else "failed", result=str(target), error=self.error)
            print(json.dumps(dict(passed=result["passed"], checks=len(self.checks), not_covered=len(self.not_covered),
                                  error=self.error, result=str(target)), ensure_ascii=False), flush=True)
        return 0 if self.error is None else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--player", required=True, type=Path)
    parser.add_argument("--backend", choices=("mono", "il2cpp"), default="mono")
    parser.add_argument("--clients", choices=(1, 3), type=int, default=1)
    parser.add_argument("--driver", choices=("host", "client"), default="client")
    parser.add_argument("--weak", action="store_true")
    parser.add_argument("--port", type=int, default=29260)
    parser.add_argument("--save-version", type=int, default=19)
    parser.add_argument("--background", action="store_true", help="Start graphics Players without requesting window activation")
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--phase-hook", action="store_true", help="Build includes official pause-on-journey-phase automation hook")
    parser.add_argument("--skip-restarts", action="store_true", help="Explicitly report stable process restarts as not covered")
    parser.add_argument("--keep-open", choices=("orbit", "descent", "landed"), help="Separate manual observation run, stop via run/stop.flag")
    parser.add_argument("--keep-seconds", type=int, default=3600, help="Bounded maximum manual observation lifetime")
    parser.add_argument("--interactive", action="store_true", help="Visible native input Players, only with --keep-open orbit")
    args = parser.parse_args()
    if args.interactive and args.keep_open != "orbit":
        parser.error("--interactive requires --keep-open orbit; automatic movement must not compete with native keyboard input")
    if args.keep_seconds <= 0:
        parser.error("--keep-seconds must be positive")
    if args.save_version <= 0:
        parser.error("--save-version must be positive")
    return NetworkRun(args).execute()


if __name__ == "__main__":
    sys.exit(main())

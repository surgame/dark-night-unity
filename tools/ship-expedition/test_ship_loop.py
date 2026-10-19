"""Quick real Host + two clients: cargo, policies, navigation, departure, arrival and recovery.

The explicit landed-ship-cargo preset supplies near-full ship stock and bag ore on
the reviewed map. It does NOT prove mining routes or extraction. Every operation
after startup uses shipped UI events / trusted client commands; no authority edits.
"""
import argparse
import importlib.util
import json
import math
from pathlib import Path
import sys
import time
from memory_guard import check_memory

spec = importlib.util.spec_from_file_location('ship_network_base', Path(__file__).parents[1] / 'space-planet-flow/test_network.py')
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)


def money(report):
    return base.world(report)['Camp']['Credits']


def cargo(report):
    return next(d for d in base.world(report)['Expedition']['Devices'] if d['Id'] == base.ship(report)['Id'])


class ShipLoopRun(base.NetworkRun):
    def read(self, role):
        check_memory(self.run)
        return super().read(role)

    def __init__(self, args):
        super().__init__(args)
        self.rules = json.loads((self.repo / 'Game/Assets/DarkNights/Res/Config/balance.json').read_text(encoding='utf-8-sig'))['expedition']

    def personal(self, role, kind, expected='Applied', name=None):
        r = self.ready(role)
        return self.expect(role, name or role + ' ' + kind, expected, operation='Expedition', kind=kind,
                           actors=[base.actor(r)['Id']], target=base.ship(r)['Id'], value=base.journey(r)['Revision'],
                           lease=base.actor(r)['ControlLease'])

    def ui(self, role, key, predicate=None):
        count = self.send(role, operation='navigation-ui', key=key)
        self.consumed(role, count)
        return self.wait(role, predicate, key) if predicate else self.ready(role)

    def buy(self, role, key):
        r = self.ready(role)
        return self.expect(role, role + ' buys ' + key, operation='BuyEquipment', kind=key,
                           actors=[base.actor(r)['Id']], target=base.ship(r)['Id'],
                           value=base.actor(r)['InventoryRevision'], lease=base.actor(r)['ControlLease'])

    def save(self, slot, label):
        path = self.saves / 'v26/QuickTests/landed-ship-cargo' / f'slot-{slot:02d}.dnsave.json'
        modified = path.stat().st_mtime_ns if path.exists() else -1
        self.expect('host', label + ' save accepted', operation='Save', value=slot)
        r = self.wait('host', lambda r: not r['storageBusy'] and path.exists() and path.stat().st_mtime_ns != modified, label + ' disk save')
        frozen = json.loads(path.read_text(encoding='utf-8-sig'))
        self.record(label + ' v26 quick fixture disk format', frozen['format_version'] == 26)
        self.saved[slot] = dict(label=label, phase=base.phase(r), path=str(path), sha256=base.sha256(path))
        self.save_report('host', label + '-saved', r)

    def initial_and_competition(self):
        for role in self.roles:
            self.start(role)
        self.all_ready(5)
        self.initial = self.consistency('initial reviewed map', 5)
        self.old_run = base.journey(self.initial)['Navigation']['RunId']
        self.old_map = base.journey(self.initial)['MapId']
        self.record('three distinct controlled heroes', len({base.actor(self.ready(r))['Id'] for r in self.roles}) == 3)
        for role in self.roles:
            r = self.wait(role, lambda r: r.get('navigationUI', {}).get('visible'), role + ' navigation HUD visible')
            self.record(role + ' sees target and mock danger', bool(r['navigationUI']['status']) and '危险' in r['navigationUI']['status'])
            self.record(role + ' ship marker has no node text overlap', not r['navigationUI']['markerOverlapsNode'])
        host_selection = self.ready('host')['navigationUI']['selected']
        revision = base.journey(self.ready('host'))['Revision']
        self.ui('client1', 'open-navigation', lambda r: r['navigationUI']['open'])
        self.record('landed open map ship marker avoids nodes', not self.ready('client1')['navigationUI']['markerOverlapsNode'])
        self.ui('client1', 'star-node-deep-preview', lambda r: r['navigationUI']['selected'] == 'planet-row-deep-preview')
        self.record('preview cannot be confirmed', not self.ready('client1')['navigationUI']['confirmEnabled'])
        self.record('personal selection leaves host and journey unchanged', self.ready('host')['navigationUI']['selected'] == host_selection and base.journey(self.ready('host'))['Revision'] == revision)
        self.ui('client1', 'close-navigation', lambda r: not r['navigationUI']['open'])
        c = self.ready('client1')
        foreign = base.actor(c)
        self.expect('host', 'foreign cargo actor rejected', 'PermissionDenied', operation='Expedition', kind='unload', actors=[foreign['Id']], target=base.ship(c)['Id'], value=base.journey(c)['Revision'], lease=foreign['ControlLease'])
        self.personal('host', 'unload', 'NoEffect', 'cargo terminal distance enforced')
        packet = self.next_raw('client1', 'Expedition', kind='unload', actors=[foreign['Id']], target=base.ship(c)['Id'], value=base.journey(c)['Revision'], lease=foreign['ControlLease'])
        self.expect('client1', 'partial deposit accepted', **packet)
        self.expect('client1', 'identical deposit packet safely replayed', **packet)
        self.personal('client1', 'unload', 'NoEffect', 'full hold does not destroy remaining bag')
        c = self.ready('client1')
        self.record('ship capacity respected', cargo(c)['Iron'] == self.rules['ShipCapacity'] and cargo(c)['Gold'] == 0)
        self.record('unaccepted ore retained', base.crew(c)['Iron'] == 1 and base.crew(c)['Gold'] == 2)
        self.record('deposit gives no immediate money', money(c) == money(self.initial))
        self.expected_income = (self.rules['ShipCapacity'] + 1) * self.rules['Trade']['IronPrice'] + 2 * self.rules['Trade']['GoldPrice']

    def transient_cases(self):
        self.expect('host', 'host-only policy enabled', operation='SetControlMode', value=1)
        self.wait('client1', lambda r: r['frame']['HostOnly'] and not any(a['ControllerSlot'] == r['slot'] for a in base.world(r)['Actors']), 'guest control revoked')
        self.ui('client1', 'open-navigation', lambda r: r['navigationUI']['open'])
        self.record('HostOnly guest still sees navigation HUD', self.ready('client1')['navigationUI']['visible'])
        self.record('HostOnly guest cannot confirm destination', not self.ready('client1')['navigationUI']['confirmEnabled'])
        self.expect('client1', 'HostOnly cargo write denied', 'PermissionDenied', operation='Expedition', kind='unload', actors=[base.actor(self.initial, 1)['Id']], target=base.ship(self.initial)['Id'], value=base.journey(self.ready('host'))['Revision'], lease=1)
        self.expect('host', 'shared policy restored', operation='SetControlMode', value=0)
        for role in self.roles:
            self.wait(role, lambda r: any(a['ControllerSlot'] == r['slot'] for a in base.world(r)['Actors']), 'control returned')
        self.ui('client1', 'close-navigation')
        self.pause(True)
        self.save(1, 'loaded ship cargo')
        self.load(1)
        self.record('save load keeps ship stock and bag remainder', cargo(self.ready('host'))['Iron'] == self.rules['ShipCapacity'] and base.crew(self.ready('client1'))['Iron'] == 1 and base.crew(self.ready('client1'))['Gold'] == 2)
        self.pause(False)
        self.personal('host', 'depart')
        self.wait('host', lambda r: base.phase(r) == 6, 'gathering')
        time.sleep(1.3)
        self.record('outside player prevents lift and payment', base.phase(self.ready('host')) == 6 and money(self.ready('host')) == money(self.initial))
        self.expect('host', 'transitional save rejected', 'Loading', operation='Save', value=4)
        self.personal('host', 'cancel-flight')
        self.record('cancelled gathering stays on same map', base.phase(self.ready('host')) == 5 and base.journey(self.ready('host'))['MapId'] == self.old_map)
        self.personal('host', 'depart')
        self.walk('client2', base.ship_x(self.ready('client2')) - 60, tolerance=6)
        self.wait('host', lambda r: base.phase(r) == 0 and r['ready'], 'safe departure and settlement', 120)
        self.consistency('safe orbit', 0)
        h = self.ready('host')
        self.record('all ship and onboard bag ore sold once', cargo(h)['Iron'] == cargo(h)['Gold'] == 0 and all(c['Iron'] == c['Gold'] == 0 for c in base.world(h)['Expedition']['Crew']))
        self.record('shared account receives exact value', money(h) == money(self.initial) + self.expected_income)
        self.record('run settlement identity is committed', base.journey(h)['Navigation']['SettledRunId'] == self.old_run and base.journey(h)['Navigation']['LastIncome'] == self.expected_income)
        self.save(2, 'settled orbit')
        self.load(2)
        self.pause(False)
        self.reconnect('client2', 'settled late reconnect', 0)
        self.record('reconnect and restore do not pay twice', money(self.ready('host')) == money(self.initial) + self.expected_income)

    def flight_landing(self):
        self.walk('client1', base.ship_x(self.ready('client1')) + 32, tolerance=6)
        self.buy('client1', 'pickaxe')
        before = self.ready('host')
        self.expect('host', 'mock destination rejected', 'NoEffect', **self.request('host', planet='deep-preview'))
        self.expect('host', 'old journey revision rejected', 'NoEffect', **self.request('host', value=base.journey(before)['Revision'] - 1))
        self.send('host', operation='pause-on-journey-phase', value=2)
        self.expect('host', 'automatic destination selected', **self.request('host'))
        self.wait('host', lambda r: base.phase(r) == 2 and r['frame']['Paused'], 'cruise frozen at initial position')
        first = base.journey(self.ready('host'))['Navigation']
        for role in self.roles:
            r = self.wait(role, lambda r: r['frame']['Paused'] and base.phase(r) == 2, 'shared paused cruise')
            self.record(role + ' shares current ETA inputs', base.journey(r)['Navigation'] == first)
        self.pause(False)
        self.buy('client1', 'pistol')
        self.wait('host', lambda r: base.journey(r)['Navigation']['Y'] < first['Y'] - 3, 'ship position advances')
        self.pause(True)
        second = base.journey(self.ready('host'))['Navigation']
        self.record('approaching A makes deep preview farther', math.hypot(second['X'],second['Y']) < math.hypot(first['X'],first['Y']) and math.hypot(second['X']-45,second['Y']-70) > math.hypot(first['X']-45,first['Y']-70))
        expected_money = money(self.initial) + self.expected_income - self.rules['Trade']['PickaxePrice'] - self.rules['Trade']['PistolPrice']
        self.record('transit purchase uses shared credits', money(self.ready('host')) == expected_money)
        self.pause(False)
        self.expect('host', 'retarget from current position', **self.request('host'))
        self.wait('host', lambda r: base.phase(r) == 5 and r['ready'] and base.journey(r)['MapId'] != self.old_map, 'automatic landing', 120)
        h = self.consistency('new expedition landed', 5)
        self.wait('host', lambda r: base.ship(r)['DoorClock'] == 0, 'automatic ramp opens')
        self.record('new expedition identity distinct', base.journey(h)['Navigation']['RunId'] not in ('', self.old_run))
        self.record('retarget never repeats sale', money(h) == expected_money and base.journey(h)['Navigation']['SettledRunId'] == self.old_run)
        self.record('automatic flight never occupies pilot', base.ship(h)['PilotId'] == 0)
        self.save(3, 'new expedition')
        self.load(3)
        self.pause(False)
        self.record('equipment survives new expedition and restore', 2 in [base.actor(self.ready('client1'))['Slot'+str(i)] for i in range(4)] and 1 in [base.actor(self.ready('client1'))['Slot'+str(i)] for i in range(4)])
        self.personal('host', 'depart')
        self.wait('host', lambda r: base.phase(r) == 0 and r['ready'], 'second empty departure', 120)
        h = self.consistency('empty sale orbit', 0)
        self.record('empty next run pays zero', money(h) == expected_money and base.journey(h)['Navigation']['LastIncome'] == 0)
        self.ui('host', 'open-navigation', lambda r: r['navigationUI']['open'])
        self.record('orbit marker has no node text overlap', not self.ready('host')['navigationUI']['markerOverlapsNode'])
        count=self.send('host', operation='capture-frame', file='ship-navigation.png')
        self.wait('host', lambda r: (self.run/'ship-navigation.png').is_file(), 'navigation screenshot')
        self.ui('host', 'close-navigation')
        for role in self.roles:
            self.save_report(role, 'loop-final')
        self.uncovered('mining route and initial cargo acquisition', 'explicit cargo initial fixture; deposit and all later operations use real authority')
        self.uncovered('other machine LAN', 'user has no other machine')
        self.uncovered('emergency departure downed crew and persistent personal growth', 'outside first approved ordinary loop')


def main():
    p=argparse.ArgumentParser()
    p.add_argument('--player',type=Path,required=True)
    p.add_argument('--output-root',type=Path,required=True)
    p.add_argument('--port',type=int,default=27957)
    a=p.parse_args()
    a.backend='mono'; a.driver='host'; a.clients=2; a.save_version=26
    a.weak=False; a.background=True; a.interactive=False; a.quick_test='landed-ship-cargo'
    a.metrics=False; a.keep_open=None; a.skip_restarts=True
    a.graphics_api='d3d11'
    run=ShipLoopRun(a); run.driver_sha256=base.sha256(Path(__file__))
    return run.execute()

if __name__=='__main__':
    sys.exit(main())

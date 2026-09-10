"""Test-only UDP relay: loss and jitter affect actual reliable transport packets too.
One upstream socket per downstream peer preserves independent FishNet connections.
"""
import argparse
import heapq
import json
import random
import select
import socket
import time
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--listen', type=int, required=True)
parser.add_argument('--target', type=int, required=True)
parser.add_argument('--loss', type=float, default=0.05)
parser.add_argument('--delay', type=float, default=0.05)
parser.add_argument('--jitter', type=float, default=0.025)
parser.add_argument('--report', required=True)
args = parser.parse_args()
rng = random.Random(29101)
listener = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
if hasattr(socket, 'SIO_UDP_CONNRESET'):
    listener.ioctl(socket.SIO_UDP_CONNRESET, False)
listener.bind(('127.0.0.1', args.listen))
peers, reverse, pending = {}, {}, []
counts = dict(received=0, dropped=0, forwarded=0, reordered=0, peer_resets=0)
last_forwarded = {}
serial = 0
deadline = time.monotonic() + 240
while time.monotonic() < deadline and not Path(args.report + '.stop').exists():
    readable, _, _ = select.select([listener] + list(reverse), [], [], 0.002)
    for sock in readable:
        try:
            data, address = sock.recvfrom(65535)
        except ConnectionResetError:
            # Windows reports ICMP from a just-closed client as recvfrom failure.
            # The relay remains alive for this peer's subsequent reconnect.
            counts['peer_resets'] += 1
            continue
        if sock is listener:
            if address not in peers:
                upstream = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                if hasattr(socket, 'SIO_UDP_CONNRESET'):
                    upstream.ioctl(socket.SIO_UDP_CONNRESET, False)
                upstream.bind(('127.0.0.1', 0))
                peers[address] = upstream
                reverse[upstream] = address
            output, target = peers[address], ('127.0.0.1', args.target)
        else:
            output, target = listener, reverse[sock]
        counts['received'] += 1
        if rng.random() < args.loss:
            counts['dropped'] += 1
            continue
        serial += 1
        due = time.monotonic() + max(0, args.delay + rng.uniform(-args.jitter, args.jitter))
        heapq.heappush(pending, (due, serial, output, target, data))
    while pending and pending[0][0] <= time.monotonic():
        _, order, output, target, data = heapq.heappop(pending)
        key = (output, target)
        if order < last_forwarded.get(key, 0):
            counts['reordered'] += 1
        last_forwarded[key] = max(order, last_forwarded.get(key, 0))
        output.sendto(data, target)
        counts['forwarded'] += 1
    Path(args.report).write_text(json.dumps(counts), encoding='utf-8')
for sock in [listener] + list(reverse):
    sock.close()

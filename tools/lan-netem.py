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
parser.add_argument('--duration', type=float, default=240)
parser.add_argument('--report', required=True)
args = parser.parse_args()
if args.duration <= 0:
    parser.error('--duration must be positive')
rng = random.Random(29101)
listener = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
if hasattr(socket, 'SIO_UDP_CONNRESET'):
    listener.ioctl(socket.SIO_UDP_CONNRESET, False)
listener.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 4 * 1024 * 1024)
listener.bind(('127.0.0.1', args.listen))
listener.setblocking(False)
peers, reverse, pending = {}, {}, []
counts = dict(received=0, dropped=0, forwarded=0, reordered=0, peer_resets=0)
counts.update(received_bytes=0, forwarded_bytes=0, client_to_server_bytes=0, server_to_client_bytes=0)
started = time.monotonic()
next_report = started
last_forwarded = {}
serial = 0
deadline = started + args.duration
while time.monotonic() < deadline and not Path(args.report + '.stop').exists():
    readable, _, _ = select.select([listener] + list(reverse), [], [], 0.002)
    for sock in readable:
        # Drain a bounded batch; per-packet report writes artificially throttle large frames.
        for _ in range(256):
            try:
                data, address = sock.recvfrom(65535)
            except BlockingIOError:
                break
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
                    upstream.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 4 * 1024 * 1024)
                    upstream.bind(('127.0.0.1', 0))
                    upstream.setblocking(False)
                    peers[address] = upstream
                    reverse[upstream] = address
                output, target = peers[address], ('127.0.0.1', args.target)
            else:
                output, target = listener, reverse[sock]
            counts['received'] += 1
            counts['received_bytes'] += len(data)
            counts['client_to_server_bytes' if sock is listener else 'server_to_client_bytes'] += len(data)
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
        counts['forwarded_bytes'] += len(data)
    now = time.monotonic()
    if now >= next_report:
        counts['elapsed_seconds'] = now - started
        Path(args.report).write_text(json.dumps(counts), encoding='utf-8')
        next_report = now + 0.5
counts['elapsed_seconds'] = time.monotonic() - started
Path(args.report).write_text(json.dumps(counts), encoding='utf-8')
for sock in [listener] + list(reverse):
    sock.close()

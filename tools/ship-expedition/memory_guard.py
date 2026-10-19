"""Read-only Windows memory gate for the owned local Player run; raises so its finally closes only those Players."""
import ctypes as c
from ctypes import wintypes as w
import importlib.util
import json
from pathlib import Path
import time


spec = importlib.util.spec_from_file_location('dn_memory_gate', Path(__file__).resolve().parents[1] / 'ground-baseline/watch_memory.py')
memory_types = importlib.util.module_from_spec(spec)
spec.loader.exec_module(memory_types)
Performance = memory_types.Performance


next_check = 0
minimum_ram = float('inf')


def check_memory(directory):
    global next_check, minimum_ram
    if time.monotonic() < next_check:
        return
    next_check = time.monotonic() + 1
    sample = Performance()
    psapi = c.WinDLL('psapi', use_last_error=True)
    psapi.GetPerformanceInfo.argtypes = [c.POINTER(Performance), w.DWORD]
    if not psapi.GetPerformanceInfo(c.byref(sample), c.sizeof(sample)):
        raise c.WinError()
    ram = sample.available * sample.page_size / 1024 ** 3
    minimum_ram = min(minimum_ram, ram)
    value = dict(free_ram_gib=ram, minimum_ram_gib=minimum_ram, memory_gate='available_physical_only',
                 minimum_free_ram_gib=memory_types.LIMITS['minimum_free_ram_gib'])
    (directory / 'memory.json').write_text(json.dumps(value), encoding='utf-8')
    if memory_types.memory_breach(ram):
        raise MemoryError('Local Player memory reserve reached: ' + json.dumps(value))

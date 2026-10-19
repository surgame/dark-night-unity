"""Read-only Windows memory gate for the owned local Player run; raises so its finally closes only those Players."""
import ctypes as c
from ctypes import wintypes as w
import json
import time


class Performance(c.Structure):
    _fields_ = [("size", w.DWORD), ("commit", c.c_size_t), ("limit", c.c_size_t),
                ("peak", c.c_size_t), ("physical", c.c_size_t), ("available", c.c_size_t),
                ("cache", c.c_size_t), ("kernel", c.c_size_t), ("paged", c.c_size_t), ("nonpaged", c.c_size_t),
                ("page_size", c.c_size_t), ("handles", w.DWORD), ("processes", w.DWORD), ("threads", w.DWORD)]


next_check = 0
minimum_ram = minimum_commit = float('inf')


def check_memory(directory):
    global next_check, minimum_ram, minimum_commit
    if time.monotonic() < next_check:
        return
    next_check = time.monotonic() + 1
    sample = Performance()
    psapi = c.WinDLL('psapi', use_last_error=True)
    psapi.GetPerformanceInfo.argtypes = [c.POINTER(Performance), w.DWORD]
    if not psapi.GetPerformanceInfo(c.byref(sample), c.sizeof(sample)):
        raise c.WinError()
    ram = sample.available * sample.page_size / 1024 ** 3
    commit = (sample.limit - sample.commit) * sample.page_size / 1024 ** 3
    minimum_ram = min(minimum_ram, ram)
    minimum_commit = min(minimum_commit, commit)
    value = dict(free_ram_gib=ram, free_commit_gib=commit, minimum_ram_gib=minimum_ram,
                 minimum_commit_gib=minimum_commit, reserve_gib=1)
    (directory / 'memory.json').write_text(json.dumps(value), encoding='utf-8')
    if ram < 1.1 or commit < 1.1:
        raise MemoryError('Local Player memory reserve reached: ' + json.dumps(value))

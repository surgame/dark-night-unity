"""只读核对清理相关 Windows 进程的工作目录，不输出命令行中的凭据。"""

import ctypes
from ctypes import wintypes
import json
import subprocess


def working_directory(pid):
    """读取同位宽进程 PEB 的 CurrentDirectory；读取失败时保留错误而非猜测。"""
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    ntdll = ctypes.WinDLL("ntdll")
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.ReadProcessMemory.argtypes = [wintypes.HANDLE, ctypes.c_void_p,
                                        ctypes.c_void_p, ctypes.c_size_t,
                                        ctypes.POINTER(ctypes.c_size_t)]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    ntdll.NtQueryInformationProcess.argtypes = [wintypes.HANDLE, wintypes.ULONG,
                                              ctypes.c_void_p, wintypes.ULONG,
                                              ctypes.c_void_p]
    handle = kernel.OpenProcess(0x410, False, pid)
    if not handle:
        return {"error": "OpenProcess: " + str(ctypes.get_last_error())}
    try:
        basic = (ctypes.c_void_p * 6)()
        code = ntdll.NtQueryInformationProcess(handle, 0, basic, ctypes.sizeof(basic), None)
        if code:
            return {"error": "NtQueryInformationProcess: " + str(code)}

        def read(address, count):
            buffer = ctypes.create_string_buffer(count)
            received = ctypes.c_size_t()
            if not kernel.ReadProcessMemory(handle, address, buffer, count, ctypes.byref(received)):
                raise OSError(ctypes.get_last_error(), "ReadProcessMemory")
            if received.value != count:
                raise OSError("Short process-memory read")
            return buffer.raw

        parameters = int.from_bytes(read(basic[1] + 0x20, 8), "little")
        directory = read(parameters + 0x38, 16)
        length = int.from_bytes(directory[:2], "little")
        address = int.from_bytes(directory[8:], "little")
        return {"cwd": read(address, length).decode("utf-16-le")}
    except OSError as error:
        return {"error": str(error)}
    finally:
        kernel.CloseHandle(handle)


def snapshot():
    command = """$all = @(Get-CimInstance Win32_Process)
$names = @('Unity.exe','bee_backend.exe','dotnet.exe','MSBuild.exe',
    'DNights.exe','DarkNights.exe','LanCoop.exe','Rider.Backend.exe',
    'python.exe','7z.exe','UnityPackageManager.exe')
$all | Where-Object { $_.Name -in $names } | ForEach-Object {
    $parent = $all | Where-Object ProcessId -eq $_.ParentProcessId
    $project = ''
    if ($_.CommandLine -match '(?i)-projectpath\\s+"([^"]+)"') { $project = $Matches[1] }
    [pscustomobject]@{ pid=$_.ProcessId; parent=$_.ParentProcessId;
        parentPresent=($null -ne $parent); name=$_.Name;
        created=$_.CreationDate.ToUniversalTime().ToString('o');
        executable=$_.ExecutablePath; project=$project }
} | ConvertTo-Json -Depth 3 -Compress
"""
    result = subprocess.run(["pwsh", "-NoProfile", "-Command", command],
                            capture_output=True, text=True, encoding="utf-8", check=True)
    rows = json.loads(result.stdout)
    if isinstance(rows, dict):
        rows = [rows]
    for row in rows:
        row.update(working_directory(row["pid"]))
    return rows


if __name__ == "__main__":
    print(json.dumps(snapshot(), ensure_ascii=False, indent=2))

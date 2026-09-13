# Campaign and capacity acceptance sample their own Player processes through Windows/.NET.
# The Unity Mono Process implementation can return zero, so OS memory is collected outside the Player.
$script:playerMemorySamples = [Collections.Generic.List[object]]::new()
$script:playerMemoryClock = [Diagnostics.Stopwatch]::StartNew()
$script:nextPlayerMemorySample = 0.0

function Sample-PlayerMemory([hashtable]$Processes, [switch]$Force) {
    $seconds = $script:playerMemoryClock.Elapsed.TotalSeconds
    if (!$Force -and $seconds -lt $script:nextPlayerMemorySample) { return }
    $script:nextPlayerMemorySample = $seconds + 5
    foreach ($role in $Processes.Keys) {
        $process = $Processes[$role]
        $process.Refresh()
        if ($process.HasExited) { continue }
        $workingSet = $process.WorkingSet64
        if ($workingSet -le 0) { throw "Windows working set is unavailable for $role (PID $($process.Id))." }
        $script:playerMemorySamples.Add([ordered]@{
            utc = [DateTime]::UtcNow.ToString('O'); elapsedSeconds = $seconds; role = $role
            processId = $process.Id; workingSetBytes = $workingSet; privateBytes = $process.PrivateMemorySize64
        })
    }
}

function Get-PlayerMemorySamples([string]$Role) {
    return @($script:playerMemorySamples | Where-Object { $_.role -eq $Role })
}

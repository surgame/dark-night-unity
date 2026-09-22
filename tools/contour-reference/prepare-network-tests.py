"""Derive this slice's regression driver from the existing four-process expedition harness."""
from pathlib import Path
root = Path(__file__).resolve().parents[2]
original = (root / 'tools/expedition/test-network.ps1').read_text(encoding='utf-8-sig')
text = original.replace('artifacts/expedition/', 'artifacts/contour/').replace('v8/', 'v9/').replace('player-mono/', 'player-mono-r3/')
text = text.replace("'--dn-role', $Role,", "'--dn-contour-static', '--dn-role', $Role,")
text = text.replace("    Check 'host_client_same_map'", "    $backgroundHash = $h.terrain.backgroundHash\n    Check 'initial_reference_matches' ($backgroundHash.Length -eq 64 -and $backgroundHash -eq $c.terrain.backgroundHash)\n    Check 'candidate_background_uploaded' ($h.terrainPresentation.backgroundBuilds -gt 0 -and $c.terrainPresentation.backgroundBuilds -gt 0)\n    Check 'host_client_same_map'")
text = text.replace("    if (!$SmokeOnly) {\n        $hero", "    Check 'late_initial_reference_matches' ($late.terrain.backgroundHash -eq $backgroundHash)\n    if (!$SmokeOnly) {\n        $hero", 1)
text = text.replace("    Check 'reconnect_same_cargo_owner'", "    Check 'reconnect_reference_matches' ($c.terrain.backgroundHash -eq $backgroundHash)\n    Check 'reconnect_same_cargo_owner'")
text = text.replace("    Check 'restart_restores_final_map'", "    Check 'restart_reference_matches' ($h.terrain.backgroundHash -eq $backgroundHash)\n    Check 'restart_restores_final_map'")
# WorkReach is 16px; stop at -14 with command/report latency, then verify the actual pose.
text = text.replace('$deposit.X - 20', '$deposit.X - 14')
text = text.replace("        for ($i=0; $i -lt 10; $i++) {", "        $h = Wait-Report 'host' {param($r) !(Actor $r 0).Walking} 'stop beside ore'\n        Check 'mining_position_in_reach' ([Math]::Abs((Actor $h 0).X - $deposit.X) -le 16 -and [Math]::Abs((Actor $h 0).Height - (632 - ($deposit.Y + 0.5) * 16)) -le 16)\n        for ($i=0; $i -lt 10; $i++) {")
# A return path may be airborne beside the dock lip. Hold the actual finite-fuel jetpack,
# instead of issuing ground-only jump taps that cannot launch from an unsupported state.
text = text.replace("operation='input';actor=$hero.Id;horizontal=-1;jumpPressed=$true", "operation='input-hold';actor=$hero.Id;horizontal=-1;jumpPressed=$true;jumpHeld=$true")
(root / 'tools/contour-reference/test-network.ps1').write_text(text, encoding='utf-8')
print('Prepared contour four-process matrix')
header = text.split('try {\n    if ($Weak)')[0]
header = header.replace("param([int]$Port = 28940, [switch]$Weak, [switch]$SmokeOnly)", "param([int]$Port = 28960, [switch]$Weak, [switch]$SmokeOnly)")
header = header.replace("'artifacts/contour/network-'", "'artifacts/contour/background-network-'")
body = (root / 'tools/contour-reference/background-network-body.ps1').read_text(encoding='utf-8')
relay = text.split('try {\n')[1].split("    Start-Player 'host'")[0]
body = body.replace('try {\n', 'try {\n' + relay, 1)
(root / 'tools/contour-reference/test-background-network.ps1').write_text(header + body, encoding='utf-8')

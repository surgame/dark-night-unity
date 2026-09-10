# LAN Co-op Sample

Open `Content/LanCoop.unity` in Unity 6000.4.9f1. Choose Host, or enter the host's LAN IP and Join (UDP 17877).

The camp starts paused. Two to four players share 10 test coins and one worksite. Try simultaneous purchases, claiming a worksite, repeat requests, invalid targets, Host pause/reset, late join and reconnect.

Build: `Dark Nights > Samples > LAN > Build Windows Player`.

This is an isolated sample scene, not the Dark Nights game. Do not load it alongside the formal Bootstrap. Native Prefabs remain editable; Create Initial Assets refuses to overwrite Content. Remove this folder to remove the sample. The project's FishNet default prefab list excludes this folder.

Chinese setup, framework decisions, constraints and verified test evidence: [docs/LAN_SAMPLE.md](../../../../docs/LAN_SAMPLE.md).

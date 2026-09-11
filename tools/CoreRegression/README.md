# Core regression

Run from the repository root after preparing locked dependencies and importing `Game` in Unity:

```powershell
dotnet run --project tools/CoreRegression -- .
```

The .NET 8 executable references the actual C#9 / netstandard2.1 Core assembly, actual Runtime configuration/save JSON source, and Newtonsoft from the locked Unity package cache. It executes the same scenario source as `DarkNights.Tests.CoreRegressionTests`; no game source, assets or assemblies are loaded from `../projects` at runtime. Output is `artifacts/migration/core-regression.json`; failures return exit code 1.

`Fixtures` contains byte-for-byte frozen inputs from the Godot baseline. `legacy-v1.json` and `legacy-v1-after-20s.json` originated from its earlier `ad5f0da` implementation, and the layout/content fixtures from `bd040e1`. `godot-gameplay-validation.json` records the baseline's complete defended and unattended campaigns. Input hashes and the read-only source commit are in `docs/evidence/core-migration-2026-09-11.json`. Never regenerate these inputs from the migrated implementation.

`godot-rng-vectors.json` was captured independently with the actual Godot 4.7.2 binary. To review that experiment, copy `GodotProbe` to a new empty artifact directory and run the original engine with `--headless --path <copied-directory> --script probe.gd`. Its output stays in that copied directory. Ordinary regression never runs the probe or replaces frozen vectors.

The suite also exercises the actual new-format save codec and atomic file store: content/RNG compatibility, frozen continuation, commit sharing violations, cancellation, corrupt/oversized input and concurrent saves. Each file run uses a unique directory under `artifacts/migration/save-store`; diagnostic files remain there and player saves are never accessed. Current total: 1164 checks, including 60 new save checks.

This tool validates the portable rules, JSON boundary and local file adapter; it does not validate production scene authoring, YYGC registration, Player builds, network sessions or art. See `docs/CORE_MIGRATION.md` and `docs/SAVE_FORMAT.md` for those outstanding boundaries.

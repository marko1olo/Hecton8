# APEX Pass 9 - CSV Scratch Vault Trim - Agent 1302

## Scope

- Domain: `Assets/_Project/Scripts/Physics`, excluding Tether/Cable lanes.
- Trigger: Pass 8 fenced player-compiled CSV/File IO, but editor-only CSV scratch `GlobalDataVault` byte buffers still registered in player cold boot.
- Build policy: no dotnet/build launched. CPU probe was 87%, and user policy forbids build/dotnet under >50% CPU load or while dotnet/csc is active.

## Code Changes

- `AnalyticalGerstnerWaveRuntime.cs`: `_csvScratchHandle` field and CSV scratch ensure/validate/release paths are now `UNITY_EDITOR` only.
- `AsyncBuoyancyReadbackRuntime.cs`: `_csvScratchHandle` field and `CsvScratch` descriptor readiness are now `UNITY_EDITOR` only.
- `BuoyancyDisplacementRuntime.cs`: `_csvScratchHandle` field, `CsvScratch` descriptor, readiness, release, and reset are now `UNITY_EDITOR` only.
- `AbyssalCavitationRuntime.cs`: static CSV scratch handle, byte buffer ensure, and readiness descriptor are now `UNITY_EDITOR` only.
- `ExosuitKinematicsRuntime.cs`: `CsvScratchCapacity`, `_csvScratchHandle`, release, ensure, and editor CSV lock/read usage are now `UNITY_EDITOR` only.
- `SeaglideHydrodynamicsRuntime.cs`: CSV scratch handle, descriptor ensure, editor read view, and release are now `UNITY_EDITOR` only.
- `VehicleComponentDamageRuntime.cs`: CSV scratch handle, ensure, readiness, lock/read, and unlock are now `UNITY_EDITOR` only.
- `SubmarineDynamicsRuntime_Gyroscopes.cs`: gyro CSV scratch handle, ensure, readiness, lock/read, and unlock are now `UNITY_EDITOR` only.
- `SubmarineAutopilotSdfNavigator.cs`: `CsvScratchBytes`, `AutopilotCsvScratch`, `MaxCsvBytes`, `_csvScratchHandle`, ensure, readiness, release, lock/read, and unlock are now `UNITY_EDITOR` only.

## Line Evidence

- `AnalyticalGerstnerWaveRuntime.cs`: lines 60, 488, 523, 752 are editor-guarded.
- `AsyncBuoyancyReadbackRuntime.cs`: lines 110, 722, 750, 1192, 1195, 1236 are editor-guarded.
- `BuoyancyDisplacementRuntime.cs`: lines 84, 829, 855, 881, 981, 1022, 1630, 1677 are editor-guarded.
- `AbyssalCavitationRuntime.cs`: lines 57, 153-155, 255, 967 are editor-guarded.
- `ExosuitKinematicsRuntime.cs`: lines 31, 157, 483, 536, 1350, 1352, 1391 are editor-guarded.
- `SeaglideHydrodynamicsRuntime.cs`: lines 56, 738, 1207, 1419 are editor-guarded.
- `VehicleComponentDamageRuntime.cs`: lines 71, 410, 479, 486, 829, 842, 889 are editor-guarded.
- `SubmarineDynamicsRuntime_Gyroscopes.cs`: lines 42, 77, 88, 606, 644 are editor-guarded.
- `SubmarineAutopilotSdfNavigator.cs`: lines 36, 76, 1406, 1446, 1894-1896, 1935, 2007, 2427, 2436, 2454, 2466 are editor-guarded.

## Static Scan Results

- `Docs/Reports/CSV_SCRATCH_PLAYER_ALLOCATION_SCAN_1302_PASS9.json`
  - Scanned files: 9.
  - CSV scratch symbol hits: 65.
  - Editor-guarded scratch hits: 65.
  - Unguarded player scratch hits: 0.
  - Unguarded player scratch allocation-like hits: 0.
- `Docs/Reports/RUNTIME_IO_GUARD_SCAN_1302_PASS9.json`
  - Scanned modified in-scope source files: 30.
  - IO/path hits: 110.
  - Editor-guarded IO/path hits: 110.
  - Unguarded player/runtime IO/path hits: 0.
- `Docs/Reports/PATCH_FULL_PHYSICS_DIFF_AUDIT_1302_PASS9.json`
  - In-scope player forbidden added token lines: 0.
  - Excluded Tether/Cable forbidden token lines: 17.

## Verification

- JSON parse passed for Pass 9 CSV scratch scan, IO guard scan, diff token audit, and vault exorcism v10.
- Preprocessor balance passed for the 9 patched CSV scratch source files: every `#if` count equals `#endif` count.
- `git diff --check` passed for touched source/report/status/log paths; output contained LF-to-CRLF warnings only.
- Final process probe: CPU load 100%, dotnet/csc-like process count 0. No dotnet/build was launched.

## DTO / ARM64 Evidence

- Pass 9 did not modify DTO layout.
- Last target offset artifact remains `Docs/Reports/DTO_OFFSET_MAP_1302_PASS7_TARGETS.json`: 17 target DTOs found, 0 missing, 0 size multiple-of-8 violations.
- Example evidence:
  - `FluidIncursionTelemetryEntry`: 64 bytes, offsets 0..60, 8-byte size multiple.
  - `VehicleDamageStateDTO`: 128 bytes, `double3 LastImpactAup` at offset 64, total size 128.
  - `BuoyancyTelemetryEntry`: 64 bytes, `_pad0` at offset 60.
  - `SimdTelemetryEntry`: 64 bytes, `_pad0` at offset 48, `_pad1` at offset 56.

## AUP / Fail-Closed / Overengineering

- AUP math is unchanged in Pass 9. Existing correction remains: object/sea/root deltas are computed in double first, then local/presentation values are cast to float.
- Fail-closed behavior: player builds no longer register editor CSV scratch buffers. Missing authored CSV data in player falls back to existing deterministic vault/default/generated profiles instead of attempting file IO or allocating scratch.
- Cinematic cheat: release player keeps the "Dear Lie" path: use deterministic defaults/vault data; keep CSV tuning only for editor authoring.
- Overengineering verdict: no new solver, no new job, no new LUT required. This was a cold boot descriptor trim, not a runtime simulation rewrite.

## Residuals

- Core `GlobalTelemetryBus.TryDumpBlackboxNow` still uses managed IO internally. A true native dump writer remains a Core/native bridge task.
- Excluded Tether/Cable lanes still contain managed/thread/file tokens and are outside agent 1302 scope by prompt.
- Unity compile/import was not run in this pass because CPU policy blocked it.

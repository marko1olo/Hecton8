# APEX PASS 8 - RELEASE IO FENCE - 1302

Generated: 2026-05-25
Agent: 1302
Domain: `Assets/_Project/Scripts/Physics`, excluding Tethers/Cable/Harpoon lanes.
Build policy: no `dotnet`, no Unity build. Last CPU probe was 67%; user forbids build/dotnet under load >50%.

## Verdict

- Player-compiled IO in the 1302 touched runtime set: **0 hits**.
- Current full-file IO scan: `Docs/Reports/RUNTIME_IO_GUARD_SCAN_1302_PASS8.json`.
- Diff forbidden-token scan: `Docs/Reports/PATCH_FULL_PHYSICS_DIFF_AUDIT_1302_PASS8.json`.
- DTO map remains unchanged: `Docs/Reports/DTO_OFFSET_MAP_1302_PASS7_TARGETS.json`, 17/17 found, 0 size/offset violations.
- Core residual remains real: `GlobalTelemetryBus.TryDumpBlackboxNow` is still a Core-managed IO bridge internally.

## Static Scan Results

`RUNTIME_IO_GUARD_SCAN_1302_PASS8.json`:

- Scanned files: 18
- Missing files: 0
- IO/path tokens found: 112
- `UNITY_EDITOR` guarded IO/path tokens: 112
- Player/runtime unguarded IO/path tokens: 0

`PATCH_FULL_PHYSICS_DIFF_AUDIT_1302_PASS8.json`:

- Added Physics diff lines scanned: 1557
- In-scope player forbidden token lines: 0
- In-scope editor-guarded forbidden token lines: 0
- Excluded token lines: 24, all in excluded Tether/Cable lanes.

## Source Fences Added

- `Buoyancy/AnalyticalGerstnerWaveRuntime.cs:2-4`, `44-49`, `412-416`, `823-840`: `System.IO`, CSV flags, path resolution, and scratch file load are editor-only.
- `Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs:2-4`, `89-95`, `316-317`, `1168-1197`: vehicle sampling CSV is editor-only.
- `Buoyancy/BuoyancyDisplacementRuntime.cs:2-4`, `52-70`, `819-888`, `1819-1837`: material/settling/SIMD CSV and path helpers are editor-only.
- `Cavitation/AbyssalCavitationRuntime.cs:2-4`, `927-970`, `1085-1086`: ordnance CSV API and `IsCsvLoaded` are editor-only.
- `Exosuit/ExosuitKinematicsRuntime.cs:2-4`, `171-174`, `190-192`, `1319-1406`, `1957-1967`: performance CSV path/load helpers are editor-only.
- `Seaglide/SeaglideHydrodynamicsRuntime.cs:2-4`, `1194-1267`: hydrodynamics CSV resolution/load is editor-only.
- `Vehicles/VehicleComponentDamageRuntime.cs:2-4`, `91-105`, `800-842`, `1050-1054`: layout CSV state/load/root path is editor-only.
- `Vehicles/SubmarineDynamicsRuntime.cs:2-4`, `110-145`, `696-697`, `1066-1152`, `1261-1406`, `2039-2085`: legacy mass/drag/hull CSV and binary file loaders are editor-only; player fallback uses `GenerateEmergencyMockProfiles`.
- `Vehicles/SubmarineDynamicsRuntime_Gyroscopes.cs:2-4`, `47-57`, `560-623`: gyro CSV state/path/load is editor-only.
- `Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs:2-4`, `1461-1480`, `2390-2433`, `2445-2472`, `2824-2837`: handling profile CSV state/path/load is editor-only.

## Runtime Behavior After Fence

- Player runtime does not compile `System.IO`, `FileStream`, `FileInfo`, `DirectoryInfo`, `Path`, `File`, or `Directory` access in the scanned touched set.
- Editor keeps CSV tuning loaders for authoring and diagnostics.
- Submarine runtime no longer attempts cold binary/CSV file reads in player. If prehydrated profiles are absent, it uses deterministic generated mock profiles.
- This is a deliberate "Dear Lie": runtime pays no file IO tax; authoring fidelity stays in Editor; proper release data must arrive through the Data Monolith/Vault route, not per-component file reads.

## AUP / DTO Evidence

- AUP authority formula remains: `double3 local = objectAup - originAup; float3 presentation = (float3)local;`.
- No source changed in Pass 8 casts absolute AUP to float.
- DTO byte-offset artifact remains `DTO_OFFSET_MAP_1302_PASS7_TARGETS.json`: 17 target DTOs, sizes 64/128/192-class explicit maps, all multiples of 8, violation count 0.

## Residuals

- Core blackbox writer still uses managed IO internally. 1302 Physics now calls the Core bridge but does not own that implementation.
- Tether/Cable diff still contains managed/thread/file tokens. Those files are explicitly excluded from 1302 scope by the batch prompt.
- No Unity compile was run in Pass 8. Static preprocessor balance check passed for 10 patched source files.

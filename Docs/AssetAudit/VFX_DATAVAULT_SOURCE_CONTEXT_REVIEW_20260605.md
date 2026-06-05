# VFX DataVault Source Context Review - 2026-06-05

Status: `STATIC_SOURCE_CONTEXT / PENDING SOURCE REPAIR AND UNITY PROOF`.
Evidence class: `STATIC_SOURCE_READBACK`.

CSV: `Docs/AssetAudit/VFX_DATAVAULT_SOURCE_CONTEXT_REVIEW_20260605.csv`.

## Scope

This report refines `Docs/AssetAudit/VFX_DATAVAULT_SOVEREIGNTY_STATIC_REVIEW_20260605.md` with direct source context for the three VFX files in Owner 08. It does not mutate source and does not prove compile, Unity Console, Play Mode, profiler, GC, dump artifacts, or visual quality.

Process gate at review time: CPU reported `100`; no Unity/build/profiler/source mutation was allowed.

## Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `telemetry.md`
- `systems.md`
- `performance.md`
- `vfx.md`
- `rendering.md`
- `presentation.md`

## Source Context Corrections

### MarineSnow Runtime Scratch

`HectonMarineSnowRenderer.cs:1347` is runtime-reachable, not editor/offline-only:

- `_mockWakeScratch` and `_propwashEventScratch` are declared at lines `673-674`.
- `EnsureOwnedNativeState()` calls `EnsureRuntimeScratchBuffers()` at line `1320`.
- `EnsureRuntimeScratchBuffers()` calls `EnsureNativeArrayScratch(...)` at lines `1323-1332`.
- `EnsureNativeArrayScratch(...)` allocates `Allocator.Persistent` at line `1347`.
- Runtime paths write and copy these scratch buffers at lines `2590-2688`, then use them for vault and GPU upload paths around `2978-2982`.

Owner 08 should treat this as runtime persistent scratch debt. `HectonMarineSnowRenderer.cs:2005` is the editor wake-profile parse scratch inside `#if UNITY_EDITOR` and must not be used as the runtime constructor anchor.

### Biolum Black-Box Mirrors

`BiolumPulseSyncRuntime.cs` owns DataVault handles for runtime truth and dump scratch, and current source readback now includes an owner-local decision comment for the crash-dump mirrors:

- `SOURCE DECISION BIOLUM_BLACKBOX_OWNER_LOCAL_20260605` at lines `311-315` declares Session lifetime, owner disposal, no gameplay authority, no cross-domain snapshot contract, and no blind DataVault migration.
- `BlackBoxDumpSnapshotOwner.Entries` at line `319`, allocated at `336`.
- `_blackBoxDumpWriteBytes` at line `384`, allocated at `3993`.
- DataVault dump scratch handle `_blackBoxDumpScratchHandle` exists at line `382` and is resolved in dump serialization at lines `3928` and `4176`.
- `CopyBlackBoxDumpSnapshot()` starts at line `3883` and copies the DataVault black-box ring into the local snapshot.
- `WriteQueuedBlackBoxDump()` starts at line `4159` and copies DataVault scratch bytes into `_blackBoxDumpWriteBytes` before background file write.

These are runtime diagnostic mirrors, not gameplay authority. The source decision fields are present by static source readback. Remaining blockers are compile proof, Unity proof, GC/profiler proof, and a deterministic runtime dump artifact; do not create a global route card for this purely owner-local diagnostic scratch.

### Editor/Offline Scratch

The following are editor/offline scratch and should not be migrated as runtime gameplay state:

- `BiolumPulseSyncRuntime.cs:3018` `_csvOverrideReadBytes` under `#if UNITY_EDITOR`.
- `HectonMarineSnowRenderer.cs:2005` `_wakeProfileParseScratch` under `#if UNITY_EDITOR` between lines `1952` and `2243`.

They still need an editor/offline owner route or relocation under an editor-only surface if scanner noise keeps polluting runtime reports.

### PlasmaBeam Fault Payload

`ShinobuPlasmaBeamRuntime.cs:1483` allocates a `NativeArray<byte>` with `Allocator.Temp` inside `DumpTelemetry(...)`, then writes through `NativeFaultDumpWriter.TryWriteAll(...)`.

This is not visible per-frame VFX work, but it is a fault/export route. Future repair should either use the shared `NativeFaultDumpWriter.CreateTransientPayload(...)` / dispose helper pattern or document an approved telemetry exception. Do not convert it to a persistent owner-local field.

### Fixed Dump Paths

The three files still use fixed dump paths:

- `Dump_SHINOBU_238.bin` / `.h8dump` in `BiolumPulseSyncRuntime.cs`.
- `Dump_SILT_VFX.h8dump` / `.bin` in `HectonMarineSnowRenderer.cs`.
- `Dump_LASER_SURGEON.bin` in `ShinobuPlasmaBeamRuntime.cs`.

Fixed system dump names are deterministic but risk overwrite and legacy owner ambiguity. If touched during repair, prefer owner/system/timestamp routes or document why a fixed singleton artifact is required.

## Required Next Owner Actions

- Correct Owner 08 and any human summary that labels `HectonMarineSnowRenderer.cs:2005` as the runtime constructor anchor. The audit JSON already separates `1347` as Runtime and `2005` as Editor.
- Treat MarineSnow line `1347` plus declarations `673-674` as runtime persistent scratch debt.
- Treat Biolum black-box snapshot/write arrays as owner-local diagnostic mirrors with source decision fields present; remaining blockers are compile, Unity, GC/profiler, scanner recheck, and dump artifact proof.
- Keep editor/offline scratch separate from runtime debt.
- Re-run `Tools/DataVaultSovereigntyAudit.py` only when process load is acceptable; current CPU gate was red.

Final status: `STATIC_SOURCE_CONTEXT / BIOLUM SOURCE DECISION FIELDS PRESENT / MARINESNOW AND PLASMA SOURCE REPAIR OR REVIEW PENDING / SCANNER RECHECK, COMPILE, UNITY, GC, PROFILER, AND DUMP ARTIFACT PROOF ABSENT`.

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

### MarineSnow DataVault Rewrite

Current `HectonMarineSnowRenderer.cs` disk state supersedes the older audit-anchor wording:

- Runtime scratch fields `_mockWakeScratch` and `_propwashEventScratch` are not present.
- Runtime DataVault handles are `_dynamicWakeDtoHandle` at line `429`, `_propwashEventHandle` at line `432`, and `_propwashWakeProfileHandle` at line `436`.
- Mock wake writes through `TryWriteMockWakeVaultAndGpu(...)` at line `2560` and acquires `BufferID.MarineSnowDynamicWakes` at lines `2564-2568`.
- Mock propwash writes through `TryBuildAndPublishMockPropwashEvents(...)` at line `2763` and acquires `BufferID.PropwashGpuEventRing` at lines `2775-2779`.
- Procedural wake-source bridge reads `WakeSource` and appends propwash at lines `2984`, `3007-3011`, and `3021`.

Owner 08 should preserve this DataVault rewrite and prove it with scanner re-run, compile, Unity, GC/profiler, and runtime dump artifacts. The older audit JSON still records historical `1347`/`2005` anchors; current disk source no longer has runtime scratch at `1347`, and editor wake-profile parse scratch is currently allocated at line `1948`.

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
- `HectonMarineSnowRenderer.cs:1948` `_wakeProfileParseScratch` under `#if UNITY_EDITOR`.

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

- Correct Owner 08 and any human summary that treats old MarineSnow audit anchors `1347`/`2005` as current disk source anchors.
- Preserve the current MarineSnow DataVault rewrite for dynamic wakes and propwash; do not reintroduce runtime scratch fields.
- Treat Biolum black-box snapshot/write arrays as owner-local diagnostic mirrors with source decision fields present; remaining blockers are compile, Unity, GC/profiler, scanner recheck, and dump artifact proof.
- Keep editor/offline scratch separate from runtime debt.
- Re-run `Tools/DataVaultSovereigntyAudit.py` only when process load is acceptable; current CPU gate was red.

Final status: `STATIC_SOURCE_CONTEXT / BIOLUM SOURCE DECISION FIELDS PRESENT / MARINESNOW AND PLASMA SOURCE REPAIR OR REVIEW PENDING / SCANNER RECHECK, COMPILE, UNITY, GC, PROFILER, AND DUMP ARTIFACT PROOF ABSENT`.

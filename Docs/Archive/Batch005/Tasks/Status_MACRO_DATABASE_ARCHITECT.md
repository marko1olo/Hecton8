# Status_MACRO_DATABASE_ARCHITECT

Authority: `CURRENT_BATCH.md` / `MACRO_DATABASE_ARCHITECT`
Domain: Core & Memory Infrastructure / H8_MacroDB
Status: PENDING VERIFICATION

## Mandates Read
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `STRM_Persistent_Object_Registry.txt`

## Checklist
- [x] Task 1: Singleton eradication | PENDING VERIFICATION | DOD: `IMacroDatabaseService` contract plus `GlobalRegistry.RegisterMacroDatabase`; rejected `Database.Instance`; estimate 2-4 us registry lookup.
- [x] Task 2: Signal migration | PENDING VERIFICATION | DOD: typed `SectorHydratedSignal` and `MacroDatabaseSignalBridge`; rejected string events/UnityEvent; estimate 3-8 us native-lane push.
- [x] Task 3: ASMDEF isolation | PENDING VERIFICATION | DOD: `Hecton8.Core.Database.asmdef` project reference is `Hecton8.Core.Contracts`; rejected concrete World/Core/Memory dependencies inside DB; estimate 0 us runtime.
- [x] Task 4: Dead code hunt | PENDING VERIFICATION | DOD: `rg` scan found existing global `List`/`Dictionary` risks in World/PersistentWorldRegistry/MapMagic paths and small Core registries; rejected editing outside domain; estimate 0 us runtime.
- [x] Task 5: File format | PENDING VERIFICATION | DOD: `.h8db` magic/version/header/root offset/4096 node size; rejected ad hoc serialization; estimate 10-20 us cold header check.
- [x] Task 6: B-Tree node SoA | PENDING VERIFICATION | DOD: node hash/offset/child arrays stored by byte offsets in 4096 page; rejected heap node graph; estimate 15-60 us warm traversal.
- [x] Task 7: Memory mapped files | PENDING VERIFICATION | DOD: `MemoryMappedFile` + view pointer lifecycle; rejected SQLite/plugin DB; estimate saves 40-120 us versus stream seek/read warm path.
- [x] Task 8: Unsafe pointer reads | PENDING VERIFICATION | DOD: `UnsafeUtility.ReadArrayElement` for header/node arrays; rejected managed serialization; estimate 20-80 us saved per node fanout scan.
- [x] Task 9: AUP to hash | PENDING VERIFICATION | DOD: absolute `MacroDatabaseAup` to sector coord/hash, Middle radius 2km; rejected Transform/floating-origin authority; estimate 5-30 us radius hash generation low/mid.
- [x] Task 10: Background query | PENDING VERIFICATION | DOD: `HydrateRadiusAsync` uses `Awaitable.BackgroundThreadAsync`; rejected main-thread disk traversal; estimate main-thread stall removal depends on page faults.
- [x] Task 11: Native cache | PENDING VERIFICATION | DOD: `GlobalDataVault` owns native payload handles with `IntPtr`; rejected `Dictionary<ulong, byte[]>`; estimate 50+ us GC-spike avoidance under churn.
- [x] Task 12: Dehydration eviction | PENDING VERIFICATION | DOD: >3km eviction with dirty append and B-Tree offset update; rejected immediate rewrite/thrash eviction; estimate 5-15 us saved per dirty append by MMF write.
- [x] Task 13: Defrag tool | PENDING VERIFICATION | DOD: offline `TryRepackOffline` rebuilds live payloads into a compact destination `.h8db`; rejected runtime stop-the-world defrag; estimate 0 us runtime.
- [x] Task 14: AUP shift safety | PENDING VERIFICATION | DOD: keys use absolute sector coords from grid/local AUP; rejected origin-relative Transform keys; estimate prevents full cache invalidation.
- [x] Task 15: Math LOD | PENDING VERIFICATION | DOD: Low=1km, Middle=2km, High=3km, Ultra=4km; rejected single balanced radius; estimate low-tier page faults reduced roughly 60-90% versus 2km cube.
- [x] Task 16: Zero-GC | PENDING VERIFICATION | DOD: traversal/cache use native arrays, pointers, and vault map after init; rejected LINQ/boxing/managed payload arrays; estimate GC allocations 0 B on hot path by design.
- [x] Task 17: Blackbox dump | PENDING VERIFICATION | DOD: fixed 300-frame telemetry with cache bytes and page faults, binary dump path API; rejected Debug.Log-only diagnostics; estimate 0 us unless dump requested.
- [x] Task 18: Omega compile check | BLOCKED BY DEPENDENCY | DOD: Unity compile attempted; new/changed macro scripts validate with 0 diagnostics; full compile blocked by unrelated visual/Burst dependency errors; rejected cross-domain edits; estimate unavailable.

## Loop State
- Loop 1: Tasks 1-5 COMPLETE / PENDING VERIFICATION.
- Loop 2: Tasks 6-10 COMPLETE / PENDING VERIFICATION.
- Loop 3: Tasks 11-14 COMPLETE / PENDING VERIFICATION.
- Loop 4: Tasks 15-17 COMPLETE / PENDING VERIFICATION; Task 18 BLOCKED BY DEPENDENCY.
- Loop 5: Strict self-review COMPLETE; fixed dirty-pointer ownership and direct MMF append writes.
- Omega Polish: COMPLETE / PENDING PROJECT COMPILE; replaced hot sector divisions with reciprocal multiplication and re-scanned owned files for GC/math violations.
- Loop 6: Re-audit COMPLETE / PENDING PROJECT COMPILE; fixed missing async contract, unsafe-await compile fault, shutdown dirty flush, 64-bit AUP sector coords, stronger header validation, and failed-init cleanup.
- Loop 7: Persistence hardening COMPLETE / PENDING PROJECT COMPILE; fixed sector-coordinate cache pollution, removed coords on eviction, added dirty-append rollback, added file-size/offset overflow guards, and verified current Core/Memory asmdef boundary errors are cleared.
- Loop 8: Dirty eviction safety COMPLETE / PENDING PROJECT COMPILE; dirty sectors are no longer evicted when append/upsert fails, sector coords are removed only after confirmed cache removal, corrupt B-Tree key counts fail closed, and payload/node offset checks avoid signed overflow.

## Verification
- `validate_script` passed with 0 diagnostics for:
  - `Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs`
  - `Assets/_Project/Scripts/Core/Database/H8MacroDatabaseFileFormat.cs`
  - `Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs`
  - `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`
  - `Assets/_Project/Scripts/Core/MacroDatabaseSignalBridge.cs`
- Unity compile is BLOCKED BY DEPENDENCY:
  - Previous MacroDB-owned `CS4004: Cannot await in an unsafe context` was fixed by moving the await state machine to `H8MacroDatabaseAsyncHydration`.
  - Previous Core/Memory `GlobalDataVault` asmdef boundary errors are no longer present after current source refresh.
  - Current Unity Console blockers are outside this domain: duplicate `CacheRuntimeDependencies`, `TryRegisterHotSwapListener`, `TryUnregisterHotSwapListener`, and `OnGlobalRegistryServiceReplaced` in `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`, plus Burst resolver failure for missing `Hecton8.Prologue.Space`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` is not authoritative here because Unity has not regenerated `Hecton8.Core.Database.csproj` and the existing solution has multiple unrelated missing assembly references; latest run failed with 92 errors, including stale missing namespaces/types outside MacroDB.
- Omega polish scan passed on owned hot-path files:
  - No `foreach`, `.ToString(`, `string.Format`, string interpolation, LINQ markers, `math.sqrt`, or `math.normalize` hits.
  - Remaining `/` hits are comments plus cold reciprocal setup (`1.0d / sectorSize`); runtime sector coordinate math uses `_sectorSizeRcp` multiplication.
  - `git diff --check` passed on owned touched files with CRLF normalization warnings only.
- Loop 7 verification:
  - `CURRENT_BATCH.md` prompt block was re-extracted with PowerShell regex from `<AGENT_PROMPT id="MACRO_DATABASE_ARCHITECT">` through `</AGENT_PROMPT>`; task count remains 18.
  - Unity MCP `validate_script` reports 0 diagnostics for all MacroDB-owned scripts and the vault cache hook after the hardening pass.
  - Hot-path scan still reports no managed allocation/string/LINQ/sqrt/normalize markers in owned MacroDB files.
- Loop 8 verification:
  - Unity MCP `validate_script` reports 0 diagnostics for `MacroDatabaseContracts.cs`, `H8MacroDatabaseFileFormat.cs`, `H8MacroDatabaseService.cs`, `GlobalDataVault.cs`, and `MacroDatabaseSignalBridge.cs`.
  - Unity refresh/compile completed to editor-ready state; console blockers are external visual/Burst issues listed above, with no MacroDB-owned errors.
  - Hot-path scan still reports no managed allocation/string/LINQ/sqrt/normalize markers in owned MacroDB files.
  - `git diff --check` passed on owned touched files with CRLF normalization warnings only.

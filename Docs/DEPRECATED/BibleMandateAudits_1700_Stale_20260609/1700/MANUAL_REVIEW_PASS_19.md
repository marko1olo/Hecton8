# Manual Review Pass 19 - Persistence, Streaming, Release, Platform, And Modding Boundary

Status: STATIC METHOD REVIEW - NO UNITY IMPORT, PLAYER BUILD, PROFILER, GC, MEMORY, OR DEVICE PROOF RUN
Date: 2026-06-02

## Scope

This pass reviewed the persistence/streaming/release/platform/modding audit group against the current root bibles and selected mandate registry files:

- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `persistence.md`
- `streaming.md`
- `release.md`
- `platform.md`
- `performance.md`
- `modding.md`

## Reviewed Files

- `Assets/_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs`
- `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs`
- `Assets/_Project/Scripts/Optimization/PreInitAssetIdMap.cs`
- `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs`
- `Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs`
- `Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs`
- `Assets/_Project/Scripts/Networking/HectonRollbackNetcodeRuntime.cs`
- `Assets/_Project/Scripts/QA/Headless/HeadlessStressFractureBot.cs`

## What Exists

- `ModLoader.ShouldForceFutureCommandEnvelopeOnly()` currently returns `true`. This is the strongest current modding fact: the public route is envelope-only at runtime, and legacy managed mods, legacy AssetBundle registration, legacy resource proxy resolution, managed projected event install, and raw texture load routes should be unreachable while that invariant holds.
- `ModCommandDispatcher.LegacyCommandSurfaceEnabled` is `false`. The old `ModCommand` queue, managed command kernels, AUP command lane, render command lane, raycast lane, and legacy queue allocations are quarantined behind a hard false.
- `FutureCommandSandboxValidator` is the intended active route. Its scratch buffers use H8Memory/DataVault-style fixed native storage and explicit release handles. It has validation, reject routing, kernel telemetry, and fault dump shape.
- `HectonEventBus` explicitly documents itself as mod/API/cold managed isolation. First-party gameplay is supposed to use `SignalBus<T>` or native event lanes. It blocks managed mod subscriptions while envelope-only mode is enforced.
- `ModEventProjectionBridge` has a bounded projection cap controlled by continuous `GlobalQualityWeight`, late-frame dispatch, mod stall watchdog, GC quota culling, exception culling, and a 300-entry cull telemetry ring.
- `ModAssetManager` includes path containment checks, exact mod directory containment, project prefab allowlist ledger scanning, raw texture byte cap at 8 MB, raw texture dimension cap at 2048, and bundle/texture caches. Those are real security measures, not complete release proof.
- `ModRuntimeState` isolates mod save payloads into capped sub-sector payload windows and owner-scoped keys.
- `H8BinaryWorldPager.NativeState.EnsureAll()` allocates persistent pager arrays in a single owner native-state initialization method and registers them with `NativeMemorySentinel`.
- `PreInitAssetIdMap` uses a generated sorted GUID-id native array, registers it with `NativeMemorySentinel`, clears thread-local caches, and tears down on subsystem reset.
- `EntityDeltaCompressionArchitecture.TryDumpTelemetryRing(...)`, `AssetLifecycleGovernor.DumpHeapTelemetryToFile(...)`, `FutureCommandSandboxValidator.DumpKernelTelemetry(...)`, and `HectonRollbackNetcodeRuntime.DumpNetcodeBlackBox(...)` allocate `Allocator.Temp` byte payloads only inside explicit dump/fault/export methods.
- `HeadlessStressFractureBot` is a QA/headless route. Its log lines are not gameplay hot-path proof or gameplay defects by themselves.

## What Is Missing / Not Proven

- No current runtime artifact proves envelope-only mode was active in a player build, that legacy mod loading never installs, and that no config/define can flip `ShouldForceFutureCommandEnvelopeOnly()` in release.
- No `Runtime_Verification_Playbook` completion exists for accepted envelopes, rejected envelopes, owner mismatch, unknown opcode, AUP violation, asset CRC mismatch, mod memory violation, quota pressure, and rollback suppression.
- No profiler proof shows that `FutureCommandSandboxValidator` validation, kernel routing, telemetry writes, fault dumps, and watchdog paths stay inside the compact hardware budget.
- No memory proof shows that H8Memory/DataVault mod sandbox buffers are prewarmed and do not grow after boot.
- No release artifact proves legacy `ModAssetManager` raw PNG route is unreachable. If it becomes reachable, it performs synchronous `File.ReadAllBytes`, `new Texture2D(...)`, and `ImageConversion.LoadImage(...)`, which is not a gameplay-safe content route.
- No release artifact proves legacy `AssetBundle.LoadFromFile(...)` cannot be invoked during gameplay in a non-envelope or test build.
- No proof shows that `HectonEventBus` managed callback surfaces are absent from first-party gameplay call chains. The code tries to enforce this, but acceptance needs a static call graph and runtime counters.
- No proof shows that `ModEventProjectionBridge.DispatchLateFrame()` callback watchdog and GC quota prevent frame spikes when hostile or expensive callbacks are present. The design has guards; the runtime cost is unmeasured.
- No save/load proof exists for mod payload sub-sectors, owner mismatch rejection, internal key spoof rejection, payload size rejection, and missing package behavior.
- No proof shows black-box dumps emitted to the required paths under synthetic failure and that dump byte sizes remain bounded.

## Method Classifications

| File / Route | Classification | Evidence | Closure Required |
|---|---|---|---|
| `ModLoader.ShouldForceFutureCommandEnvelopeOnly()` | `GREENISH_STATIC_ENVELOPE_ONLY_FLAG` | Returns `true` directly. Install/load paths check it before managed factory, legacy asset, localization, event bridge, and resource registry routes. | Player-build proof that no build symbol/config override changes this, plus runtime verification playbook. |
| `ModCommandDispatcher` legacy queues | `LEGAL_QUARANTINED_LEGACY_ROUTE` | `LegacyCommandSurfaceEnabled` is `false`; initialize/register/request/drain routes return early. | Keep hard disabled or remove; if enabled, create a new release blocker and runtime proof packet. |
| `FutureCommandSandboxValidator` | `YELLOW_ACTIVE_ENVELOPE_VALIDATOR_PROOF_REQUIRED` | Fixed envelope path, DataVault/H8Memory buffers, rejection telemetry, kernel telemetry, fault dumps. | Accepted/rejected command proof, quota proof, memory proof, profiler proof, black-box dump proof. |
| `HectonEventBus` | `YELLOW_MOD_API_COLD_MANAGED_ISOLATION_PROOF_REQUIRED` | Comments and guards say first-party hot traffic belongs to `SignalBus<T>`; envelope-only mode blocks managed event surface. | Static call graph proving first-party gameplay does not use it as hot bus, plus runtime counters for mod callback culling. |
| `ModEventProjectionBridge` | `YELLOW_MANAGED_PROJECTION_WATCHDOG_PROOF_REQUIRED` | Late-frame managed callback dispatch with `Stopwatch`, `GC.GetAllocatedBytesForCurrentThread()`, timeout cull, GC quota cull, black-box ring. | Compact/high stress with hostile callbacks, cull telemetry dump, 0 B first-party gameplay proof, no callback storm. |
| `ModAssetManager.LoadRawTexture()` | `P1_LEGACY_SYNC_RAW_ASSET_ROUTE_MUST_REMAIN_UNREACHABLE` | If legacy mode is reachable, performs `File.ReadAllBytes`, `new Texture2D`, and `ImageConversion.LoadImage`. Has caps and path checks, but still synchronous decode/load. | Prove envelope-only in release or replace with offline/imported approved content route. |
| `IModResourceProxy` | `LEGAL_ONLY_WHILE_ENVELOPE_ONLY_DENIES_RUNTIME_HANDLES` | Proxy returns hashes, but `TryResolve*` calls `ModAssetManager.Load*` if legacy mode is not envelope-only. | Keep envelope-only or prove resolved assets are preapproved/preloaded and never loose runtime files. |
| `ModRuntimeState.TryCommitMmfPayloads()` / `TryLoadMmfPayloads()` | `YELLOW_MOD_SAVE_IO_WINDOW_PROOF_REQUIRED` | Uses `Allocator.Temp` byte window for capped mod payload sub-sectors and string decode/encode in save/load methods. | Save/load roundtrip, owner mismatch, internal key spoof, oversize payload, and corruption proof. |
| `EntityDeltaCompressionArchitecture.TryDumpTelemetryRing()` | `LEGAL_FAULT_DUMP_PAYLOAD` | Temp byte payload built only for dump/export path and passed to `NativeFaultDumpWriter`. | Fault-trigger artifact and bounded dump size proof. |
| `AssetLifecycleGovernor.DumpHeapTelemetryToFile()` | `LEGAL_FAULT_DUMP_PAYLOAD_WITH_ADDRESSABLE_PROOF_REQUIRED` | Temp byte payload only in dump path; called on leak/failure/teardown conditions. | Addressables handle/release proof plus dump artifact. |
| `H8BinaryWorldPager.NativeState.EnsureAll()` | `YELLOW_PAGER_NATIVE_OWNER_INIT_PROOF_REQUIRED` | Persistent arrays registered with `NativeMemorySentinel` in owner native state. | Boot/init proof, no repeated reallocation during sector IO, worker shutdown proof. |
| `PreInitAssetIdMap.Initialize()` | `GREENISH_PREINIT_ASSET_ID_MAP_PROOF_REQUIRED` | Generated table copied into persistent native array and torn down on subsystem reset. | Build table generation proof, boot prewarm proof, no first asset request hitch. |
| `HeadlessStressFractureBot` | `LEGAL_HEADLESS_QA_ROUTE` | Headless policy mutates app frame/audio/camera state, writes results, quits. | Do not count as gameplay proof; use only as QA artifact when executed. |

## Required New Release Gate

Add one P1 gate:

- `RB-132`: mod envelope, legacy asset, managed event, and mod-save quarantine proof. This binds the current modding implementation to the `modding.md` envelope-only bible and prevents future agents from treating legacy raw PNG/bundle loads or managed event callbacks as public gameplay routes.

## Bible Update Applied

`modding.md` was strengthened with a "Legacy Quarantine And Raw Asset Boundary" section. The section explicitly states that `ModAssetManager`, `IModResourceProxy`, `HectonEventBus`, `ModEventProjectionBridge`, `ModCommandDispatcher`, and legacy managed mod loading are audited quarantine context unless envelope-only is disabled by a documented release route. It also forbids counting raw PNG caps/path checks as permission for gameplay-time loose asset loading.

## Current Verdict

`YELLOW_ENVELOPE_ONLY_AND_MOD_QUARANTINE_PROOF_REQUIRED`.

The current code is directionally aligned with the root modding bible because envelope-only is hard-coded and legacy command queues are disabled. It is not release-verified. The release claim remains blocked until the envelope validator, legacy quarantine boundaries, mod save IO windows, managed callback isolation, Addressables/content proof, build/import/player proof, and device/profiler evidence exist.

## Required Next Proof

- Run `Docs/Modding/Validate_Mod_API_Static.ps1`.
- Complete `Docs/Modding/Runtime_Verification_Playbook.md` in Unity.
- Capture accepted and rejected `FutureCommandEnvelope` scenarios.
- Prove owner mismatch, unknown opcode, invalid AUP, CRC mismatch, oversized asset, missing memory lease, and command-flood rejection.
- Prove legacy managed/event/asset routes are unreachable in release player.
- Prove no first-party gameplay uses `HectonEventBus` as hot broadcast path.
- Prove mod save payload roundtrip and rejection cases.
- Prove compact hardware profiler/GC/memory behavior for mod envelope validation and projection sampling.

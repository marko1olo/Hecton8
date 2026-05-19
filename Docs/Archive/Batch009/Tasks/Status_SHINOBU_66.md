# Status_SHINOBU_66

Prompt: `SHINOBU_66`
Domain: `MOD_SANDBOX_AND_OPCODE_VALIDATOR`
Task Count: 20
Status: PENDING VERIFICATION - managed event/resource bridges and filesystem content ingress are quarantined in envelope-only mode; scoped compile gate pending.

## Hygiene

- [HYGIENE_VIOLATION] Existing file contained stale `DYNAMIC_RESOLUTION_AND_FSR_DIRECTOR` work from the earlier duplicate `SHINOBU_66` block in `CURRENT_BATCH.md`.
- Resolution: user explicitly identified this run as `SHINOBU_MOD_SANDBOX_VALIDATOR`; active prompt is the later `<AGENT_PROMPT id="SHINOBU_66" role="MOD_SANDBOX_AND_OPCODE_VALIDATOR">` block.

## Relevant Mandates

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `ARCH_Execution_Phases.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## State Machine

- [x] Task 01 BINARY_GRAVEYARD_RECONNAISSANCE | DOD: scanned archive/docs for `allowed_mod_opcodes.h8bin` and found only incompatible `FutureCommandEnvelope64`; added 16-byte emergency opcode records. Alternative rejected: claiming the old ABI. Estimate: 2-8 us cold init.
- [x] Task 02 HARMONY_PATCH_ERADICATION_PASS | DOD: managed mod entries disabled; future command packets route through `RequestFuture`; legacy `Request`/`RequestAup`/`RequestRenderInstance` wrappers return false and old dispatcher boot does not allocate command queues while envelope-only mode is active. Alternative rejected: Harmony/BepInEx/runtime method replacement or keeping managed command callbacks alive. Estimate: removes managed callback frame cost and old command-lane allocator boot.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD: `FutureCommandEnvelope` uses public fields only, no `{ get; set; }`; dormant legacy `ModCommand` now uses explicit field overlays for `ModHash`/`RequestId` instead of properties. Alternative rejected: property wrappers over NativeArray DTOs. Estimate: 0 copy/boxing overhead per packet.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD: explicit 64B envelope, 64B signal/telemetry structs, no `Pack=1`. Alternative rejected: compact unaligned payload. Estimate: cache-line stable, no alignment trap risk.
- [x] Task 05 BLIND_DEPENDENCY_MOCKING | DOD: `MockModQueue` wraps caller-owned external queues and `MockMaliciousEnvelopeInjectionJob` injects corrupted packets without Inventory/AI dependencies. Alternative rejected: validator-owned mock queue allocation or direct gameplay mocks. Estimate: self-audit injection <10 us.
- [x] Task 06 BURST_OPCODE_VALIDATION_KERNEL | DOD: Burst `ValidateFutureCommandEnvelopeJob` uses `CompileSynchronously=true`, deterministic float mode, `[NoAlias]` Vault views, allowlist, XXHash3, counters, AUP, CRC gates; bulk ingress resolves Vault once per stream/queue drain. Alternative rejected: managed reflection dispatch, per-packet Vault lookup, and private allocator hash tables. Estimate: 0.02-0.12 us/envelope static.
- [x] Task 07 ROUTING_TO_SIGNAL_BUS | DOD: valid packets emit unmanaged `ModSpawnRequestSignal`, `ModAssetReferenceSignal`, `MockAcousticSignal`, `MockDamageSignal`. Alternative rejected: invoking C# mod callbacks. Estimate: queue push only.
- [x] Task 08 THE_DEAR_LIE_UNCLAIMED_SEAMS | DOD: alter health/gravity/subtitle and memory read future seams route to DevNullQueue plus DevNull signal. Alternative rejected: crashing on unclaimed owners. Estimate: queue push only.
- [x] Task 09 MOD_MEMORY_ISOLATION | DOD: `BufferID.ShinobuModSandboxBlackboxMemory` Vault arena with per-signature open-address chunk leases. Alternative rejected: mutating core DTOs or heap dictionaries. Estimate: 1-4 byte writes per accepted memory op.
- [x] Task 10 DENIAL_OF_SERVICE_PROTECTION | DOD: 64B per-signature frame counters drop packets above scaled budget; pending Vault ring evicts oldest and thermal shed drops backlog when quality <0.3. Alternative rejected: unbounded drain. Estimate: constant open-address probe per packet.
- [x] Task 11 CONTINUOUS_SCALABILITY_THROTTLING | DOD: effective quality combines `GlobalQualityWeight`, optional override, and `CpuThermalPressure01` through a smooth polynomial, then continuously scales command budget from 10 to tuner max and controls overheat packet shedding. Alternative rejected: low/high binary quality switch. Estimate: one lerp plus overflow shed curve per frame.
- [x] Task 12 AUP_LOCALIZATION_SECURITY | DOD: Burst rejects non-finite `double3` and absolute coordinates outside +/-50km. Alternative rejected: float-casting world positions. Estimate: 3 abs + finite check per packet.
- [x] Task 13 SYNC_WITH_ROLLBACK_NETCODE | DOD: validator freezes during rollback resimulation using local 64B Vault flag view at buffer `70752` or explicit override. Alternative rejected: direct `Hecton8.Networking` runtime reference. Estimate: one Vault state read per frame.
- [x] Task 14 CRC32_ASSET_VERIFICATION | DOD: asset opcode checks declared CRC32 and declared byte count against fixed approved native manifest and max bytes. Alternative rejected: arbitrary asset load or size-blind CRC gate. Estimate: open-address table probe per asset packet.
- [x] Task 15 FAUNA_AGGRESSION_SANDBOX | DOD: fauna opcodes emit acoustic/damage stimulus signals, not AI commands. Alternative rejected: direct Leviathan control. Estimate: signal queue push only.
- [x] Task 16 ZERO_INIT_OVERHEAD_BYPASS | DOD: persistent runtime buffers are requested from GlobalDataVault with `NativeArrayOptions.UninitializedMemory` and explicit `UnsafeUtility.MemClear`; validator keeps only `VaultBufferHandle<T>` fields; legacy dispatcher initialization exits before allocating old `NativeQueue`/`NativeHashMap` lanes. Alternative rejected: private persistent `NativeQueue`/`NativeHashMap`/`NativeArray` allocations. Estimate: cold memset only for active Vault buffers.
- [x] Task 17 TELEMETRY_QUARANTINE_RECORDER | DOD: 300-frame telemetry ring and `Dump_QUARANTINE_SURGEON.bin` on NaN/memory/layout faults. Alternative rejected: Debug.Log-only audit. Estimate: 64B write per frame.
- [x] Task 18 QUARANTINE_TUNER_EDITOR_WINDOW | DOD: `HECTON-8/Mod API Sandbox Tuner` editor sliders/toggles/buttons. Alternative rejected: runtime HUD. Estimate: editor-only.
- [x] Task 19 CSV_OVERRIDE_INGESTOR | DOD: `NativeArray<byte>` parser hashes opcode tokens or hex values into the allowlist without `string.Split`/LINQ. Alternative rejected: managed parser in runtime path. Estimate: O(bytes) cold reload.
- [x] Task 20 LIVE_TRAFFIC_MONITOR_GIZMO | DOD: EditorWindow uses `EditorGUI.DrawRect` histogram for incoming/rejected traffic. Alternative rejected: in-game allocation UI. Estimate: editor-only.

## Iteration Log

- Loop 1: Extracted correct MOD prompt despite duplicate `SHINOBU_66`; read domain, AGENTS, and relevant mandates; found existing `ModCommand` ABI is not the requested `FutureCommandEnvelope`.
- Loop 2: Implemented 64B envelope, emergency opcode records, queue ingress, Burst validation job, DevNull path, signal routing, and managed-entry quarantine.
- Loop 3: Added Vault-backed blackbox memory, rollback freeze, CRC asset manifest, telemetry ring, binary dump, and H8 memory IDs.
- Loop 4: Added editor tuner, CSV parser, live histogram, self-audit injection, and docs update.
- Loop 5: Static audit pass: `git diff --check` passed; grep found no Harmony/BepInEx/reflection path in ModdingAPI changes, no `Pack=1`, no `FutureCommandEnvelope` properties, no `string.Split`/LINQ parser, and no direct Inventory/AI gameplay dependency.
- Loop 6: Ultra polish pass: removed validator-owned persistent `NativeQueue`, `NativeHashSet`, `NativeHashMap`, and private `NativeArray` fields; replaced them with Vault handles plus fixed rings/open-address tables; added 64B padded counters/stats/ring state; replaced `Stopwatch.StartNew()` with allocation-free timestamp calls.
- Loop 7: Compile-wall pass: removed direct `Hecton8.Networking` reference from the sandbox validator and read rollback resimulation through a local 64B Vault flag view. Legacy ModdingAPI files still contain pre-existing sibling usings outside the new validator surface.
- Loop 8: Ingress hardening pass: rewrote `RequestRawEnvelopeStream` to resolve Vault once per stream instead of once per envelope; added external `NativeQueue` drain without validator-owned queue state; added endian-normalizing overload and asset byte-length manifest validation.
- Loop 9: Scoped compile retry after ingress hardening: CPU gate was clear and no compiler was active; minimal Roslyn probe still stopped on non-owned `PlayerBuilder.cs`, `HectonNetworkManager.cs`, and `ThermalGeyser.cs` dependency errors before any validator error.
- Loop 10: Mock ownership purge: removed `MockModQueue.Initialize(int)` persistent allocator path; mocks now attach to caller-owned queues so the validator owns no mock allocator state. Scoped Roslyn retry after this patch still stopped on the same non-owned compile wall before any validator error.
- Loop 11: JobHandle chain seam: added `TrySchedulePreSimulation(dependsOn, out JobHandle)` and `TryFinalizeScheduledPreSimulation(forceComplete)` so integrators can chain validator Burst work without a forced same-frame fence. Legacy void dispatcher path remains isolated in-domain.
- Loop 12: Property/facade purge: removed the remaining property-style validator seams (`MockModQueue.IsCreated`, validator state flags, pending/devnull counters) and replaced the editor telemetry private `NativeArray` scratch with direct Vault entry reads.
- Loop 13: Self-audit and thermal polish: `RunSelfAudit()` now runs a direct single-envelope Burst validation probe and requires `InvalidAup` rejection; `FutureCommandSandboxTuning` now exposes `CpuThermalPressure01`, folded into effective quality with a polynomial curve and `math.lerp` before thermal backlog shedding.
- Loop 14: Legacy command surface quarantine: `HectonAPI.Commands.Request`, `RequestAup`, and `RequestRenderInstance` now return false; `ModCommandDispatcher.Initialize` boots only the future-envelope validator while `LegacyCommandSurfaceEnabled` is false; PRE_SIM/LateFrame drains skip legacy command queues in envelope-only mode.
- Loop 15: Legacy property overlay purge: changed dormant legacy `ModCommand` from sequential layout with `ModHash`/`RequestId` accessors to explicit 64B layout with field overlays at offsets 8 and 12; this removes the last unmanaged command property seam without changing call sites.
- Loop 16: Managed factory perimeter purge: `ModLoader.RegisterManagedFactory` now returns false while envelope-only mode is active; manifest parsing no longer performs conventional `.dll` path resolution in that mode; stale API strings now route users to `FutureCommandEnvelope`.
- Loop 17: Filesystem content ingress purge: manifest parsing no longer scans `.bundle` or `lang_*.json` in envelope-only mode; content-only packages are disabled before bundle/localization registration; `ModAssetManager` and `ModLocalizationBridge` return early if called anyway.
- Loop 18: Unity import hygiene: added `.meta` files for `FutureCommandSandboxValidator.cs` and `ModApiSandboxTunerWindow.cs` after scoped Roslyn evidence showed the stale Bee response omitted the new validator source.
- Loop 19: Payload NaN firewall: added opcode-aware payload validation for numeric lanes, sanitized DevNull/spawn float payload forwarding, removed an unnecessary `NativeDisableParallelForRestriction`, and expanded self-audit to reject both NaN AUP and NaN payload packets.
- Loop 20: Managed bridge guillotine: `ModLoader` no longer installs `ModEventProjectionBridge` or `ModResourceRegistry` in envelope-only mode; game-ready/load events no longer publish managed mod events; public event subscriptions/publishes throw or no-op behind the gate; the projection bridge is lazy; resource proxy/registry returns false before allocating its legacy `NativeHashMap`.

## Verification

- PASS: `git diff --check` on touched files.
- PASS: static source grep for banned patterns listed in Loop 5.
- PASS: static source grep found no `NativeParallel*`, private persistent `NativeArray`/`NativeQueue`/`NativeHashMap`/`NativeHashSet`, bare `[BurstCompile]`, `Stopwatch.StartNew`, Harmony/BepInEx/reflection, `Pack=1`, LINQ, or direct sibling using in `FutureCommandSandboxValidator.cs`.
- PASS: static source grep confirmed bulk ingress APIs `RequestRawEnvelopeStream(..., sourceBigEndian)` and `RequestFromExternalQueue(...)` are present.
- PASS: scheduled integration API present for `TrySchedulePreSimulation` and non-blocking `TryFinalizeScheduledPreSimulation(false)`.
- PASS: post-JobHandle `git diff --check` and static grep found only resolver return-type false positives for `NativeArray<T>`.
- PASS: post-property purge static grep found no property/arrow expression seams in `FutureCommandSandboxValidator.cs` or `ModApiSandboxTunerWindow.cs`.
- PASS: post-property purge static grep found no private persistent native containers, `NativeParallel*`, owned `NativeQueue`, `Allocator.Persistent`, bare `[BurstCompile]`, `Stopwatch.StartNew`, Harmony/BepInEx/reflection, `Pack=1`, LINQ, `string.Format`, or hot-path `foreach` in the validator/editor facade.
- PASS: post-property purge `git diff --check` on touched files; only Git CRLF conversion warnings for pre-existing tracked files.
- PASS: self-audit no longer returns enqueue success; static source shows direct `ValidateFutureCommandEnvelopeJob.Run()` on a malicious NaN AUP and verifies `InvalidAup` rejection.
- PASS: explicit CPU thermal pressure ingress present through `ReportCpuThermalPressure(float)` and editor slider; effective quality collapse uses polynomial pressure curve plus `math.lerp`, while backlog shed uses a `math.step` zero gate.
- PASS: legacy command surface hard-quarantine static check confirmed `Request`/`RequestAup`/`RequestRenderInstance` return false and dispatcher boot/drain exits before old command queue/hash-map lanes while `LegacyCommandSurfaceEnabled` is false.
- PASS: legacy `ModCommand` property overlay purge static check found no `{ get; }`, `readonly get`, or property accessors in `FutureCommandSandboxValidator.cs`, `ModApiSandboxTunerWindow.cs`, or `ModCommandDispatcher.cs`; only non-DTO facade expression properties remain in `HectonAPI.cs`.
- PASS: managed factory perimeter static check confirmed `RegisterManagedFactory` returns false in envelope-only mode, `TryCreateRegisteredManagedMod` has the same guard, and manifest parsing does not call `ResolveAssemblyPath` when `ShouldForceFutureCommandEnvelopeOnly()` is true.
- PASS: stale public-message grep found no remaining `submit a ModCommand`, `validated ModCommand`, or `Use ModCommandDispatcher.Request` guidance in `Assets/_Project/Scripts/ModdingAPI` or `Docs/Modding`.
- PASS: post-managed-factory-perimeter `git diff --check` on touched files; only Git CRLF conversion warnings for pre-existing tracked files.
- PASS: filesystem content ingress static check confirmed `.bundle` and `lang_*.json` discovery are skipped behind `envelopeOnly`, content-only candidates are disabled before `RegisterBundlePath`/`RegisterLocalizationFiles`, and AssetBundle/raw PNG/localization loaders return early in envelope-only mode.
- PASS: post-filesystem-ingress-purge `git diff --check` on touched files; only Git CRLF conversion warnings for pre-existing tracked files.
- PASS: Unity import metadata exists for `FutureCommandSandboxValidator.cs` and `ModApiSandboxTunerWindow.cs`, preventing new-source omission from refreshed assembly response generation.
- PASS: guarded scoped Roslyn probe with `FutureCommandSandboxValidator.cs` explicitly added no longer reports `HectonAPI.cs` missing `FutureCommandEnvelope`; it stops only on non-owned compile walls in `PlayerBuilder`, `IBabelLocalization`, `HectonNetworkManager`, and `ThermalGeyser`.
- PASS: payload NaN firewall static check: fauna numeric payloads reject non-finite lanes, spawn validates finite non-hash lanes, DevNull/spawn signal float4 forwarding is sanitized, and `ModderBlackboxMemory` no longer disables parallel-for safety checks.
- PASS: managed bridge quarantine static check: event projection install/resource registry init are guarded by `ShouldForceFutureCommandEnvelopeOnly()`, `HectonAPI.Events` rejects public subscribe/publish calls, `HectonEventBus` blocks direct managed event subscription/publishing, and `ModResourceRegistry.Initialize/TryRegister/TryResolve` exit before allocator use in envelope-only mode.
- BLOCKED: post-filesystem-ingress-purge scoped compile launched under the stale `Hecton8.Core.rsp`; the response file omitted `FutureCommandSandboxValidator.cs`, causing `HectonAPI.cs` to miss `FutureCommandEnvelope`, while the same non-owned walls (`PlayerBuilder`, `IBabelLocalization`, `HectonNetworkManager`, `ThermalGeyser`) remained.
- BLOCKED: follow-up scoped compile with `FutureCommandSandboxValidator.cs` manually added to Roslyn inputs was not launched because CPU sampled 66% with no compiler process.
- BLOCKED: post-managed-bridge-guillotine scoped compile was not launched because CPU sampled 97% with active `csc.exe`/`dotnet.exe`. Build gate remained closed by project rule.
- BLOCKED: post-managed-factory-perimeter scoped compile was not launched because CPU sampled 87% with no compiler process. Build gate remained closed by project rule.
- BLOCKED: post-legacy-quarantine scoped compile was not launched because CPU sampled 70% with no compiler process. Build gate remained closed by project rule.
- BLOCKED: post-legacy-property-purge scoped compile was not launched because CPU sampled 99%, then 65%, with no compiler process at guarded Roslyn commands. Build gate remained closed by project rule.
- BLOCKED: initial `dotnet csc` was invalid in this repo and exited without compiling.
- BLOCKED: `dotnet exec ... csc.dll @Assembly-CSharp.rsp` used the wrong assembly response file and produced expected missing `Hecton8.Core` reference errors for the new validator; not a project compile result.
- BLOCKED: correct scoped `Hecton8.Core.rsp` compile reached pre-existing/non-owned errors in `PlayerBuilder.cs`, `HectonNetworkManager.cs`, and `ThermalGeyser.cs`; no `FutureCommandSandboxValidator.cs` errors appeared before the external dependency wall.
- BLOCKED: post-ingress scoped Roslyn retry under `Hecton8.Core.rsp` repeated the same non-owned dependency wall; first emitted errors remain `Hecton8.Construction.MockWorldSampler`, `HectonRollbackNetcodeRuntime`, `VolcanicUpdraftDirector`, and missing construction DTOs, not `FutureCommandSandboxValidator.cs`.
- BLOCKED: post-mock-ownership scoped Roslyn retry under `Hecton8.Core.rsp` repeated the same non-owned dependency wall; no `FutureCommandSandboxValidator.cs` error appeared before the wall.
- BLOCKED: post-JobHandle scoped compile was not launched because CPU sampled 100% with active external `dotnet.exe`/`csc.exe`, then 85% after those processes exited, then 100% with no compiler process; build gate remained closed.
- BLOCKED: post-property-purge scoped compile was not launched because CPU sampled 79% with one active external `dotnet.exe`; a later gate sampled 100% with no compiler process. Build gate remained closed by project rule.
- BLOCKED: post-self-audit/thermal polish scoped compile was not launched because CPU sampled 100% with no compiler process. Build gate remained closed by project rule.
- RECOVERY: Roslyn child compiler processes left after failed attempts were terminated/verified gone; CPU after cleanup fluctuated from 37% to 54%, with no `dotnet`/`csc` child process remaining.

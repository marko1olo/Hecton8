# Status_SHINOBU_66

Prompt: `SHINOBU_66`
Domain: `MOD_SANDBOX_AND_OPCODE_VALIDATOR`
Task Count: 20
Status: POLISH STATIC - scoped compile gated by CPU/compiler load after property purge.

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
- [x] Task 02 HARMONY_PATCH_ERADICATION_PASS | DOD: managed mod entries disabled; future command packets route through `RequestFuture`. Alternative rejected: Harmony/BepInEx/runtime method replacement. Estimate: removes managed callback frame cost.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD: `FutureCommandEnvelope` uses public fields only, no `{ get; set; }`. Alternative rejected: property wrappers over NativeArray DTOs. Estimate: 0 copy/boxing overhead per packet.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD: explicit 64B envelope, 64B signal/telemetry structs, no `Pack=1`. Alternative rejected: compact unaligned payload. Estimate: cache-line stable, no alignment trap risk.
- [x] Task 05 BLIND_DEPENDENCY_MOCKING | DOD: `MockModQueue` wraps caller-owned external queues and `MockMaliciousEnvelopeInjectionJob` injects corrupted packets without Inventory/AI dependencies. Alternative rejected: validator-owned mock queue allocation or direct gameplay mocks. Estimate: self-audit injection <10 us.
- [x] Task 06 BURST_OPCODE_VALIDATION_KERNEL | DOD: Burst `ValidateFutureCommandEnvelopeJob` uses `CompileSynchronously=true`, deterministic float mode, `[NoAlias]` Vault views, allowlist, XXHash3, counters, AUP, CRC gates; bulk ingress resolves Vault once per stream/queue drain. Alternative rejected: managed reflection dispatch, per-packet Vault lookup, and private allocator hash tables. Estimate: 0.02-0.12 us/envelope static.
- [x] Task 07 ROUTING_TO_SIGNAL_BUS | DOD: valid packets emit unmanaged `ModSpawnRequestSignal`, `ModAssetReferenceSignal`, `MockAcousticSignal`, `MockDamageSignal`. Alternative rejected: invoking C# mod callbacks. Estimate: queue push only.
- [x] Task 08 THE_DEAR_LIE_UNCLAIMED_SEAMS | DOD: alter health/gravity/subtitle and memory read future seams route to DevNullQueue plus DevNull signal. Alternative rejected: crashing on unclaimed owners. Estimate: queue push only.
- [x] Task 09 MOD_MEMORY_ISOLATION | DOD: `BufferID.ShinobuModSandboxBlackboxMemory` Vault arena with per-signature open-address chunk leases. Alternative rejected: mutating core DTOs or heap dictionaries. Estimate: 1-4 byte writes per accepted memory op.
- [x] Task 10 DENIAL_OF_SERVICE_PROTECTION | DOD: 64B per-signature frame counters drop packets above scaled budget; pending Vault ring evicts oldest and thermal shed drops backlog when quality <0.3. Alternative rejected: unbounded drain. Estimate: constant open-address probe per packet.
- [x] Task 11 CONTINUOUS_SCALABILITY_THROTTLING | DOD: `GlobalQualityWeight` continuously scales command budget from 10 to tuner max and controls overheat packet shedding. Alternative rejected: low/high binary quality switch. Estimate: one lerp plus overflow shed curve per frame.
- [x] Task 12 AUP_LOCALIZATION_SECURITY | DOD: Burst rejects non-finite `double3` and absolute coordinates outside +/-50km. Alternative rejected: float-casting world positions. Estimate: 3 abs + finite check per packet.
- [x] Task 13 SYNC_WITH_ROLLBACK_NETCODE | DOD: validator freezes during rollback resimulation using local 64B Vault flag view at buffer `70752` or explicit override. Alternative rejected: direct `Hecton8.Networking` runtime reference. Estimate: one Vault state read per frame.
- [x] Task 14 CRC32_ASSET_VERIFICATION | DOD: asset opcode checks declared CRC32 and declared byte count against fixed approved native manifest and max bytes. Alternative rejected: arbitrary asset load or size-blind CRC gate. Estimate: open-address table probe per asset packet.
- [x] Task 15 FAUNA_AGGRESSION_SANDBOX | DOD: fauna opcodes emit acoustic/damage stimulus signals, not AI commands. Alternative rejected: direct Leviathan control. Estimate: signal queue push only.
- [x] Task 16 ZERO_INIT_OVERHEAD_BYPASS | DOD: persistent runtime buffers are requested from GlobalDataVault with `NativeArrayOptions.UninitializedMemory` and explicit `UnsafeUtility.MemClear`; validator keeps only `VaultBufferHandle<T>` fields. Alternative rejected: private persistent `NativeQueue`/`NativeHashMap`/`NativeArray` allocations. Estimate: cold memset only.
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
- BLOCKED: initial `dotnet csc` was invalid in this repo and exited without compiling.
- BLOCKED: `dotnet exec ... csc.dll @Assembly-CSharp.rsp` used the wrong assembly response file and produced expected missing `Hecton8.Core` reference errors for the new validator; not a project compile result.
- BLOCKED: correct scoped `Hecton8.Core.rsp` compile reached pre-existing/non-owned errors in `PlayerBuilder.cs`, `HectonNetworkManager.cs`, and `ThermalGeyser.cs`; no `FutureCommandSandboxValidator.cs` errors appeared before the external dependency wall.
- BLOCKED: post-ingress scoped Roslyn retry under `Hecton8.Core.rsp` repeated the same non-owned dependency wall; first emitted errors remain `Hecton8.Construction.MockWorldSampler`, `HectonRollbackNetcodeRuntime`, `VolcanicUpdraftDirector`, and missing construction DTOs, not `FutureCommandSandboxValidator.cs`.
- BLOCKED: post-mock-ownership scoped Roslyn retry under `Hecton8.Core.rsp` repeated the same non-owned dependency wall; no `FutureCommandSandboxValidator.cs` error appeared before the wall.
- BLOCKED: post-JobHandle scoped compile was not launched because CPU sampled 100% with active external `dotnet.exe`/`csc.exe`, then 85% after those processes exited, then 100% with no compiler process; build gate remained closed.
- BLOCKED: post-property-purge scoped compile was not launched because CPU sampled 79% with one active external `dotnet.exe`; a later gate sampled 100% with no compiler process. Build gate remained closed by project rule.
- RECOVERY: Roslyn child compiler processes left after failed attempts were terminated/verified gone; CPU after cleanup fluctuated from 37% to 54%, with no `dotnet`/`csc` child process remaining.

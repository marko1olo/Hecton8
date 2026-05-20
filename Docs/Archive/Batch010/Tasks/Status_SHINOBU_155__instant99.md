# SHINOBU_155 Status - Player Death And Reconciliation Sequence

Status: PENDING_COMPILE_RUNTIME_PROOF_EXTERNAL_BRIDGE_ERRORS
Domain: ECHELON 5 - Combat & Survival Physiology
Prompt Source: Docs/Tasks/CURRENT_BATCH.md
Task Count: 20

## Mandates Read

- CORE_Global_State_Reset_NonReload_Transitions.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- MATH_AUP_Determinism_Sync.txt
- ARCH_Signal_Lane_Segregation.txt
- ARCH_Execution_Phases.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Task Checklist

- [x] Task 01 SCENE_RELOAD_ERADICATION | DOD: static scan found no `LoadScene`, `LoadSceneAsync`, `Application.LoadLevel`, or coroutine reload in touched death route; fatal death now routes to `PlayerDeathReconciliationBridge` | Rejected: scene teardown/reload | Estimate: removes 15 s stall, hot-path scan cost 0 us.
- [x] Task 02 GAMEOBJECT_RESPAWN_PURGE | DOD: static scan found no `Destroy(player)`/respawn prefab instantiate path; player component survives and health/survival state is reconciled | Rejected: prefab churn and broken references | Estimate: avoids object init/GC spike, per-death bridge cost under 20 us target.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD: SHINOBU respawn DTOs expose raw public fields only; `rg` found no `{ get; }` DTO properties in respawn data/jobs/runtime | Rejected: wrapper properties and managed state containers | Estimate: avoids defensive copies in Vault rows.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD: `RespawnStateDTO` is `[StructLayout(LayoutKind.Explicit, Size = 32)]`, field offsets 0/24/28 verified by `UnsafeUtility.GetFieldOffset` guard | Rejected: implicit layout and `Pack=1` | Estimate: exact 32-byte row, 0 unaligned ARM64 reads.
- [x] Task 05 EMERGENCY_MOCK_MEDICAL_BAY | DOD: `GenerateMockRespawnPointsJob` writes deterministic mock med-bay AUPs into `71605` using Burst deterministic mode | Rejected: waiting for Base Logistics Graph | Estimate: cold boot only; death lookup O(8).
- [x] Task 06 FATAL_DAMAGE_INTERCEPTION | DOD: `HectonPlayerHealth.Die()` and `HectonSurvivalSystem.CheckLethalConditions()` emit `PlayerRespawnSignal`; Core now owns the lane capacity, direct flush/clear, finite guard, size validation, and IL2CPP preserve entry; critical health/survival frame stamps now use `TimeSliceScheduler.CurrentFrameId`; reconciled deaths skip legacy managed `OnDeath`/`PlayerDiedEvent` fallback side effects; Physiology transforms the same-frame snapshot after med-bay resolution; `HydrodynamicKccRuntime` consumes requested or committed suspend packets and skips capsulecast/collision resolution for exactly one snapshot generation | Rejected: managed GameManager reload callback, fallback-only signal routing, Unity `Time.frameCount`, managed death-event side effects on reconciled death, next-frame duplicate commit signal, and direct Physiology->Physics call | Estimate: one SignalBus enqueue plus one in-place snapshot transform and one skipped Capsulecast batch on death frame.
- [x] Task 07 BURST_STATE_RECONCILIATION_KERNEL | DOD: `ResetPlayerPhysiologyJob` resets physiology/metabolism/decompression/kinematic Vault pointers and emits `InventoryCommandSignal`; it consumes the med-bay target staged by PreSimulation and scans med-bay rows only as a fail-closed fallback; `ScheduleSimulation` refuses to stack a second writer while the prior active handle is incomplete | Rejected: direct managed inventory mutation, overlapping Vault writers, and duplicate med-bay authority inside the Simulation job | Estimate: normal death path removes the second O(medBayCount) target search from the Burst job; fallback remains bounded.
- [x] Task 08 THE_DEAR_LIE_DEATH_TRANSITION | DOD: `ResetPlayerPhysiologyJob` writes `RespawnFadeDTO`, then `ShinobuRespawnReconciliationRuntime` publishes `_HectonRespawnDearLieParams`/`_HectonDeathFadeIntensity` from VisualSync only; UberNoir applies blackout/grain/chromatic cover | Rejected: UI overlay prefab and Gameplay-phase shader writes | Estimate: CPU one VisualSync `float4`, shader math quality-scaled.
- [x] Task 09 AUP_ATOMIC_TELEPORTATION | DOD: `ResetPlayerPhysiologyJob.WriteKinematic()` overwrites `LockstepPlayerKinematicState` sector/local AUP truth, velocity zeroed | Rejected: `Transform.position` interpolation | Estimate: one 96-byte kinematic row mutation.
- [x] Task 10 ASYNCHRONOUS_SHADER_FADE_IN | DOD: `UpdateRespawnFadeJob` decays fade scalar via dispatcher job and clears active flag; VisualSync reads `RespawnFadeDTO` only after the active job fence is already completed; no coroutine string found | Rejected: coroutine fade and unconditional VisualSync `Complete()` stalls | Estimate: O(1), one DTO write plus one non-blocking `IsCompleted` gate.
- [x] Task 11 CONTINUOUS_SCALABILITY_FADE_RATE | DOD: fade rate is `math.lerp(highRate, lowRate, 1f - quality)` and shader complexity consumes quality scalar | Rejected: low/high binary branch | Estimate: low tier exits visual cover faster; high tier spends GPU only.
- [x] Task 12 ECOSYSTEM_AGGRO_RESET | DOD: `PredatorCognitionDomain` reads requested/committed `PlayerRespawnSignal` snapshot in its existing data stage and zeroes player target/sets idle for mesofauna | Rejected: direct Physiology->AI call and extra job fence for a same-stage data mutation | Estimate: bounded snapshot scan plus active mesofauna loop, target under 10 us current slots.
- [x] Task 13 AUP_PRECISION_RESPAWN_VALIDATION | DOD: medical bay validation subtracts `double3` AUPs before casting to `float3` for local distance; fallback lifepod used on invalid target | Rejected: absolute world float distance | Estimate: O(8) double subtracts, no 100 km jitter path.
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE | DOD: jobs use `FloatMode.Deterministic`, DTOs are blittable explicit-layout, kinematic row is memcpy-safe | Rejected: nondeterministic job mode | Estimate: rollback can overwrite same Vault rows; no managed state dependency.
- [x] Task 15 ZERO_INIT_OVERHEAD_BYPASS | DOD: all SHINOBU-owned handles request `NativeArrayOptions.UninitializedMemory`; `EnsureVaultState()` now resolves created handles after allocation lock | Rejected: local zeroed NativeArrays | Estimate: avoids cold zero fill for 300x64 telemetry ring and scratch buffers.
- [x] Task 16 TELEMETRY_DEATH_RECORDER | DOD: 300-entry `RespawnTelemetryEntry` ring and 64-byte cursor live in Vault; fault dump writes `Docs/AgentLogs/Dump_SHINOBU_155.bin` and XML alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin` | Rejected: `Debug.Log` black box | Estimate: one 64-byte telemetry write per death.
- [x] Task 17 RESPAWN_TUNER_EDITOR_WINDOW | DOD: `RespawnReconciliationTunerWindow` under `#if UNITY_EDITOR` exposes fade/tuning sliders, uses a cold fade-readout LUT, and writes Vault tuning directly | Rejected: runtime UI tuning surface and per-refresh string formatting | Estimate: editor-only, 0 gameplay us.
- [x] Task 18 CSV_PENALTY_RULES_INGESTOR | DOD: cold parser reads bytes into Vault scratch, slices `ReadOnlySpan<byte>`, writes `InventoryDeathPenaltyRuleDTO` rows, supports numeric hashes or LocHash-compatible UTF-8 tokens, and Inventory consumes the Vault rule table through `InventoryCommandSignal` payload fields for item-level drop/retain. The XML's NativeHashMap request is replaced by a fixed Vault row table because GlobalDataVault owns typed buffers, the table is capped at 64, and the row set remains blittable/memcpy-safe | Rejected: `string.Split`, per-death parse, persistent NativeHashMap ownership, and coarse global drop-only command | Estimate: 0 hot-path parsing allocation; per-death scan bounded by inventory cells * ruleCount.
- [x] Task 19 LIVE_SPAWN_DEBUG_GIZMO | DOD: `OnDrawGizmos` reads med-bay Vault rows and draws green wire cylinders via Handles in editor only | Rejected: debug GameObject spawn | Estimate: editor-only, 0 gameplay us.
- [!] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: static scans and layout math pass; docs/rationale/ledger/log are updated; current-frame respawn snapshot repair is applied; AUP sector division and local AUP clamp helpers now guard `HectonPhysicsContract.AupSectorSizeMetersDouble` locally; Simulation now trusts the PreSimulation-staged `RespawnStateDTO` med-bay target before falling back to a bounded scan; SHINOBU job bodies, Simulation job scheduling, VisualSync shader payload publish, cold default DTO writes, CSV rule row writes, death-adjacent mutable GlobalSignal publishing, and AUP helper returns no longer use literal `new`/object-initializer value construction; `SurvivalDatabaseItemRecord` no longer uses `Pack=1` and is explicit 24 bytes with manual padding; VisualSync now publishes the Dear Lie shader payload only while active or while issuing the final zero-clear, and it passes cached `_dataVault` into a Core bridge overload instead of using the bridge's legacy `GlobalRegistry.DataVault` lookup path; PreSimulation, Simulation, VisualSync, default hydration, and fault dump paths now use `HasHotVaultState()` in hot-facing code and cannot request Vault buffers from dispatcher phases; reconciled health/survival deaths now skip legacy `GlobalTelemetryBus.PublishPlayerDeath`, human-readable `RecordDeathTelemetry`, managed `OnDeath`, and `PlayerDiedEvent` side effects; health death AUP fallback now finite-gates `CurrentAup`; remaining `new`/`Complete()` hits are documented cold boot/editor/file-dump/teardown paths or pre-existing readonly HUD signal constructors outside respawn truth; guarded compile proof remains blocked outside SHINOBU after the stale deleted Construction source include was shielded by `Directory.Build.targets` and the follow-up Core compile advanced to external missing contract/source bridge semantic errors; Unity import/profiler proof still pending | Rejected: fabricating cross-domain bridge stubs, editing generated project files by hand, duplicating med-bay authority in the primary job route, hiding hot value construction behind initializers, keeping `Pack=1`, publishing shader globals every idle VisualSync frame, allocating Vault buffers from dispatcher ticks, keeping legacy telemetry/log/event side effects in reconciled death, or launching repeated builds under uncertain load | Estimate: SHINOBU static verification complete; compile/runtime proof blocked by external dependency.

## Loop Log

### Loop 0 - Prompt Extraction And Mandates

- Extracted `<AGENT_PROMPT id="SHINOBU_155">` from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell regex over full raw file.
- Read domain file and selected task-relevant mandates before gameplay edits.

### Loop 1 - Source Archaeology

- Verified player death routes lacked scene reload and prefab respawn, but also lacked authoritative Vault/AUP reconciliation.
- Verified physiology and kinematic truth already live in Vault-backed unmanaged DTOs.
- Verified shader globals use `HectonShaderGlobalDataVaultBridge` and `GlobalShaderDispatcher`.

### Loop 2 - Fatal Route And Vault State

- Added/kept `PlayerRespawnSignal` contract lane and `PlayerDeathReconciliationBridge`.
- Patched `HectonPlayerHealth` and `HectonSurvivalSystem` to request reconciliation before legacy death fallback.
- Reworked SHINOBU buffer IDs to `71604..71613` and corrected docs/rationale.

### Loop 3 - Simulation Kernels

- Verified `ResetPlayerPhysiologyJob`, `UpdateRespawnFadeJob`, deterministic Burst flags, NoAlias pointer fields, AUP local delta validation, kinematic overwrite, inventory command emission, and telemetry ring.
- Repaired `EnsureVaultState()` so allocation lock does not disable already-created handles.

### Loop 4 - Visual Lie And Cross-Domain Consumers

- Added shader globals/slot 19 and UberNoir Dear Lie blackout/grain/chromatic cover.
- Added inventory drop command consumer, mesofauna aggro clear, and KCC one-frame collision bypass through `PlayerRespawnSignal`, without direct Physiology dependency.

### Loop 5 - Human Control And Verification

- Added editor-only Reconciliation Tuner, cold span-based CSV parser route, and med-bay gizmo cylinders.
- Ran static scans for reload/destroy/instantiate/coroutine/Pack=1/DTO properties/direct sibling imports/hot-path `Time.deltaTime`/LINQ/string formatting plus `git diff --check`.
- Build was not launched: `Get-Process dotnet,csc` returned no compiler process, CPU guard samples were `100`, `72.039`, `29.782`, and the user explicitly said not to launch build until needed.

### Loop 6 - Penalty Rule Contract Repair

- Moved death penalty rows to `InventoryDeathPenaltyRuleDTO` in Core contracts so Physiology and Inventory read the same Vault buffer type without sibling coupling.
- Extended `InventoryCommandSignal` inside its existing 32-byte layout with payload fields for the Vault rule buffer ID, rule count, capacity, and source hash.
- Repaired CSV item hashing from lowercased byte FNV to LocHash-compatible UTF-8-as-UTF-16 FNV, with numeric hash token support for authored `0x...`/decimal IDs.
- Inventory now enforces per-item `DropOnDeath` and `RetainIfEquipped` instead of treating the command as an unconditional non-equipped resource drop.
- Inventory resolves penalty rules only through its cached `IDataVault`; it does not poll `GlobalRegistry` from the command-consumption path and fails closed if a command claims a Vault rule table that cannot be resolved.
- `ShinobuRespawnLayoutGuards` now validates the extended `InventoryCommandSignal` size and payload offsets `14/16/20/24/28`.
- Static scans found no old `RespawnPenaltyRuleDTO` source references, no `string.Split`/LINQ/scene reload/coroutine/instantiate/destroy in the SHINOBU route, and `git diff --check` reported only CRLF normalization warnings. Build remains blocked: CPU 82% and multiple active `dotnet` processes.

### Loop 7 - Core Signal Lane Authority Repair

- Added `PlayerRespawnSignal` to Core direct SignalBus authority: `FlushDirectSignalLane`, `ClearPostSimulation`, `ResolveDirectRegistryDispatch`, size validation, finite guard, stable `HectonSignalLaneContract` hash, and `SignalBusAotPreserve`.
- Moved lane shape numbers into the payload contract (`ExpectedCapacity=8`, `MaxFrameSignals=16`, `LowTierFrameSignals=4`, `MaxSuspendCollisionFrames=4`) and changed Gameplay/Physiology local boot calls to reuse those constants.
- Static scans found the core route entries present and no `Pack=1`/DTO property/LINQ/string-format/Unity time/random/private NativeArray additions in SHINOBU respawn files. `git diff --check` reported only CRLF normalization warnings. Build not launched per user instruction.

### Loop 8 - Visual Sync Phase Discipline

- Removed the immediate `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie` call from `PlayerDeathReconciliationBridge`; Gameplay now emits only `PlayerRespawnSignal`.
- Kept Dear Lie publication in `ShinobuRespawnReconciliationRuntime` VisualSync, after the simulation job has accepted the request and written `RespawnFadeDTO`.
- Static scan now finds `PublishRespawnDearLie` only in Physiology VisualSync and the rendering bridge definition, not in Gameplay.

### Loop 9 - Visual Sync Fence Repair

- Added a non-blocking `_activeHandle.IsCompleted` gate before VisualSync reads `RespawnFadeDTO`.
- VisualSync now returns without publishing when the fade job is still running; if the handle is already complete, it reclaims the job before reading.
- Rejected unconditional `Complete()` in VisualSync because that would turn a slow respawn job into a main-thread render-phase stall.

### Loop 10 - Cross-Frame Writer Fence Repair

- Added the same non-blocking active-handle gate to `ScheduleSimulation`.
- If the previous reset/fade job is still running, the system returns `JobHandle.CombineDependencies(dependsOn, _activeHandle)` and does not schedule another writer over the same Vault rows.
- If the handle is already complete, it is reclaimed with `CompleteActiveJobIfReady(false)` before new work is scheduled.

### Loop 11 - Deterministic Frame Source Sweep

- Replaced remaining critical health/survival signal timestamps in the SHINOBU death vicinity with `TimeSliceScheduler.CurrentFrameId`.
- `VitalWarningSignal`, `PhysiologyStateSignal`, `SurvivalVitalsChangedSignal`, and the physiology signal freshness check now share the dispatcher frame domain instead of Unity `Time.frameCount`.
- Static scope: change is limited to already-touched Combat/Survival Physiology files and does not introduce sibling runtime references.

### Loop 12 - Guarded Compile Attempt And Compile Wall

- CPU/process guard passed (`LoadPercentage=30`, no `dotnet`/`csc` process), so `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` was attempted.
- Build stopped at `CSC : error CS2001: Source file 'C:\hades\Hecton8\Assets\_Project\Scripts\Construction\LogisticsPipeEvents.cs' could not be found.`
- Verified that the missing file and `.meta` are deleted in the worktree and still referenced at `Hecton8.Core.csproj:981`; this is outside SHINOBU_155 ownership and blocks compile proof before SHINOBU code is reached.

### Loop 13 - Cached Vault Authority Tightening

- Removed hot dispatcher fallback lookups from `ShinobuRespawnReconciliationRuntime`; PreSimulation, Simulation, PostSimulation fault dump, and VisualSync now use cached `_dataVault` only and fail closed when it is absent.
- Kept `GlobalRegistry.DataVault` / latest-Vault fallback inside `ResolveVaultCold()` for Awake/Start/editor-only utility paths, not per-frame dispatcher work.
- Rejected polling `GlobalRegistry` from runtime ticks because cold service injection and hot-swap callbacks are the sanctioned route.

### Loop 14 - Legacy Managed Death Event Ejection

- Moved `HectonPlayerHealth.OnDeath` and `HectonSurvivalSystem.OnDeath` invocation to the unreconciled fallback path.
- Reconciled deaths now perform telemetry/signal/Vault reset only; PDA/logbook/meta `PlayerDiedEvent` side effects are not run inside the zero-GC death frame.
- Static scan still finds boot/menu scene loads and scene services, but no death-route reload in touched player health/survival/respawn files.

### Loop 15 - Current-Frame RespawnAUP Snapshot Repair

- Verified `PlayerDeathReconciliationBridge` intentionally emits the first request with `RespawnAUP = DeathAUP` because Physiology owns med-bay selection.
- Kept `ShinobuRespawnReconciliationRuntime` as the sole med-bay resolver and patched the current `SignalBus<PlayerRespawnSignal>` snapshot in-place after target selection: `RespawnAUP`, `MedicalBayHashID`, `Requested`, `Committed`, `SuspendCollision`, translated med-bay flags, and clamped suspend frame count are visible to same-frame consumers.
- Patched `HydrodynamicKccRuntime` to accept both request and committed respawn packets when latching its one-snapshot collision bypass, removing hidden dependence on the original producer flag.
- Static scans over SHINOBU respawn files found no scene reload, coroutine, instantiate/destroy, LINQ, `string.Format`, Unity random/time, `Pack=`, or DTO properties; `git diff --check` reported only CRLF normalization warnings.
- Current architecture docs now match `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`: the deleted Construction include was shielded externally, and remaining guarded compile proof is blocked by external contract/source bridge semantic errors outside this lane.

### Loop 16 - Kinematic Sector Denominator Guard

- Re-read `ResetPlayerPhysiologyJob.WriteKinematic()` after the NaN mandate pass and found `target / sectorSize` depended on the contract constant staying nonzero.
- Patched the denominator to `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)` before sector calculation.
- Static math scan now shows the only SHINOBU respawn division is guarded by that local denominator; `math.rsqrt` is already guarded with `math.max(lengthSq, 0.0001f)`.
- Editor-only fade readout still contains cold LUT string construction; it is outside gameplay and not refreshed per frame.

### Loop 17 - Local AUP Clamp Guard Sweep

- Static math scan after Loop 16 still found two local-delta clamp helpers using `HectonPhysicsContract.AupSectorSizeMetersDouble` directly as the clamp range.
- Patched `ShinobuRespawnJobs.SafeLocal()` and `ShinobuRespawnReconciliationRuntime.AupDeltaToFloat3()` to route through `SafeAupClampMeters() = math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`.
- Re-ran focused math and forbidden-pattern scans. Result: guarded sector division, guarded clamp range, guarded `math.rsqrt`; no scene reload, coroutine, instantiate/destroy, LINQ, `string.Format`, Unity random/time, `Pack=`, DTO properties, or private persistent NativeArray fields in SHINOBU respawn files. Focused `git diff --check` reports only the existing CRLF warning in KCC.

### Loop 18 - Med-Bay Authority De-Duplication

- Found `PreSimulation` already resolves the nearest valid med-bay AUP and transforms the current `PlayerRespawnSignal` snapshot, while `ResetPlayerPhysiologyJob` still repeated the med-bay scan as its primary path.
- Patched `ResetPlayerPhysiologyJob` to consume the staged `RespawnStateDTO.TargetAUP`/`MedicalBayHashID` when the pending request is already resolved; staged route flags apply only on accepted staged targets, committed request rows preserve `MockMedicalBay` for telemetry, and the med-bay buffer scan now runs only if staged state is missing, non-finite, or unresolved.
- Re-ran focused forbidden-pattern and math scans. Result: no reload/coroutine/instantiate/destroy/LINQ/string-format/Unity random/time/Pack/DTO-property/private persistent NativeArray hits in SHINOBU respawn files; math scan still shows guarded sector division, guarded clamp range, and guarded `math.rsqrt`. Build was not launched; `Get-Process dotnet,csc` found active `dotnet` PID 32468.

### Loop 19 - Hot-Path Literal New Erasure

- Re-read `ShinobuRespawnJobs.cs` and `ShinobuRespawnReconciliationRuntime.cs` for value-type `new` initializers after the zero-GC mandate pass.
- Patched job bodies to use `default` plus field assignment for `double3`, `float3`, respawn DTOs, physiology DTOs, fade DTOs, telemetry rows, and fallback vectors.
- Patched `ScheduleSimulation` to create `ResetPlayerPhysiologyJob` and `UpdateRespawnFadeJob` via `default` field assignment before scheduling; VisualSync now creates the shader `Vector4` payload via field assignment; cold default fade, mock job setup, CSV penalty rule row, fallback AUP, runtime AUP helper, and editor gizmo offsets use the same pattern.
- Static result: `rg` finds no literal `new` in `ShinobuRespawnJobs.cs` and no typed `new double3/float3/Vector3/Respawn*/Physiology*/Metabolic*/InventoryDeath*/Vector4/*Job` in the runtime. Remaining `new` hits are cold runtime host/dispatcher adapter creation, cold `FileStream`/`BinaryWriter`, stack-only `Span<byte>` construction for cold CSV ingest, and teardown/service-replacement `JobHandle.Complete()` fences. Forbidden-pattern scan returned no output; focused code/log/route `git diff --check` returned no output, while the architecture ledger reports only the existing LF->CRLF normalization warning. Build was not launched.

### Loop 20 - Death-Vicinity Signal Initializer And Pack Purge

- Scanned already-touched death-adjacent health/survival files for mutable signal object-initializer `new` and ARM64-hostile `Pack=1`.
- Patched `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in `HectonPlayerHealth`, `HectonSurvivalSystem`, and `ShinobuPhysiologyRuntime` to `default` plus field assignment.
- Replaced `SurvivalDatabaseItemRecord` `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]` with `[StructLayout(LayoutKind.Explicit, Size = 24)]`, offsets `0/4/8/12/16`, and `uint _pad0` at offset `20`.
- Static result: no `Pack=` and no mutable `new SurvivalVitalsChangedSignal`/`new VitalWarningSignal`/`new PhysiologyStateSignal` remain in the touched death/survival/respawn route. Broad scan still reports existing readonly `TraumaHudSignal` constructors and cold CSV/FileStream/DateTime routes outside respawn truth; those were not widened into this patch. Build was not launched.

### Loop 21 - VisualSync Idle Publish And Registry-Poll Cull

- Found `VisualSyncTick` always published `_HectonRespawnDearLieParams` even after `RespawnFadeDTO.DeathFadeIntensity` reached zero. That meant idle frames could keep touching the shader global bridge.
- Added `_respawnDearLieVisualActive` as a one-bit dirty latch. VisualSync now returns when fade is inactive and already cleared; if the previous frame was active, it publishes exactly one zero payload and then stops.
- Added `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` and a shared `TryPrepareSlotsVault()` helper. SHINOBU passes its cached `_dataVault` into the bridge, so this route does not use the bridge's no-argument `GlobalRegistry.DataVault` path during VisualSync.
- Rewrote `ToFiniteFloat4()`, `ToVector4()`, fallback constants, mask packing, and water-extinction reset vectors in the bridge to `default` plus field assignment helpers, removing typed `new float4`/`new Vector4` from the whole bridge file.
- Static result: `PublishRespawnDearLie(payload)` no longer exists; SHINOBU VisualSync calls `PublishRespawnDearLie(vault, payload)`; no typed `new float4`/`new Vector4` remains in `HectonShaderGlobalDataVaultBridge.cs`; the only `GlobalRegistry.DataVault` hit in the bridge is the legacy no-argument path for other publishers, and the only `GlobalRegistry.DataVault` hit in the runtime is cold `ResolveVaultCold()`. `git diff --check` reports only the existing LF->CRLF warning on the bridge file. Build was not launched.

### Loop 22 - Hot Dispatcher Vault Allocation Gate

- Found `EnsureVaultState(vault)` still reachable from `PreSimulationTick`, `ScheduleSimulation`, `VisualSyncTick`, and fault-dump entry paths. That method can request Vault buffers if handles were not created, so it is not acceptable as a hot dispatcher guard.
- Added `HasHotVaultState(IDataVault)` as a pure handle-created check and moved the hot-facing phases plus default hydration/dump reads to that gate. Allocation-capable `EnsureVaultState(...)` remains only in cold Awake/Start/hot-swap/editor utility paths.
- Static result: no `EnsureVaultState(vault)` call remains in dispatcher phases; the only strict match is the cold wrapper body. Forbidden-pattern scans over SHINOBU respawn files still return no scene reload, coroutine, instantiate/destroy, LINQ, `string.Format`, Unity random/time, `Pack=`, DTO properties, typed hot `new`, or private persistent NativeArray fields. `git diff --check` reports only existing LF->CRLF warnings on the bridge/ledger files. Build was not launched.

### Loop 23 - Reconciled Death Legacy Telemetry Ejection

- Found `HectonPlayerHealth.Die()` calling `GlobalTelemetryBus.PublishPlayerDeath()` before the respawn bridge, and `HectonSurvivalSystem.CheckLethalConditions()` calling `RecordDeathTelemetry()` before the bridge. Both are managed legacy telemetry/log paths and are not authoritative reconciliation truth.
- Moved both to unreconciled fallback after `PlayerDeathReconciliationBridge.RequestRespawn(...)` fails. Reconciled death now routes through SignalBus plus SHINOBU Vault black-box only, then returns.
- Added a finite AUP guard to health death AUP resolution and changed health/survival runtime-position fallbacks to `default` field assignment for `Vector3` values.
- Static result: `GlobalTelemetryBus.PublishPlayerDeath`, `RecordDeathTelemetry`, `OnDeath`, and `PlayerDiedEvent` remain present only after the `RequestRespawn` fallback branch. Focused `git diff --check` reports only LF->CRLF warnings. Build was not launched.

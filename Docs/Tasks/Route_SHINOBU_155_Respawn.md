# Route_SHINOBU_155_Respawn

Date: 2026-05-19
Status: PENDING VERIFICATION - COMPILE BLOCKED BY EXTERNAL CONTRACT BRIDGE ERRORS

Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Owner domain: ECHELON 5 - Combat & Survival Physiology
Owning file/system: `ShinobuRespawnReconciliationRuntime`

Problem:
Fatal player death must not reload a scene, instantiate a replacement player, or run a managed cutscene. It must reconcile player AUP, physiology, metabolism, inventory penalty, shader fade, and AI target state through deterministic unmanaged routes.

Why owner-local data is insufficient:
The death event originates in gameplay/survival, but the authoritative mutation touches Physiology Vault rows, player kinematic AUP, rendering shader globals, inventory command traffic, and mesofauna target state. Keeping that state in one MonoBehaviour would create direct sibling dependencies and stale local ownership.

Why direct caller/owner interface is insufficient:
There are multiple consumers and they live behind different asmdef/domain boundaries. A direct interface from Gameplay to Physiology plus Fauna plus Rendering would break the Compile Wall and would not provide a same-frame black-boxable contract payload.

Instrument:
  [ ] GlobalRegistry cold service/interface
  [x] SignalBus<T> first-party broadcast
  [x] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault
  [x] Black-box/telemetry route

Producer phase:
Gameplay/survival fatal damage emits only `SignalBus<PlayerRespawnSignal>` when the lethal state is detected. It does not write shader globals. Death-adjacent health/survival signal timestamps use `TimeSliceScheduler.CurrentFrameId`, not Unity `Time.frameCount`.
On successful reconciliation, legacy managed `GlobalTelemetryBus.PublishPlayerDeath`, `SurvivalVitalsChangedSignalFlags.Death`, human-readable `RecordDeathTelemetry`, legacy last-death-record capture, health `OnHealthChanged`, health `OnDamageTaken`, vital warning side effects, zero-health combat target sync, post-damage trauma HUD/leviathan advisory fan-out, `OnDeath`, and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure or non-respawn health changes. Lethal `TakeDamage()` and `Kill()` attempt `PlayerDeathReconciliationBridge.RequestRespawn(...)` before observer callbacks, so a successful one-frame rebirth cannot leak managed health/damage delegate traffic before the shader cover. `ReceiveDamage()` and `TakeLeviathanDamage()` also return before normal trauma presentation when that same call accepted a respawn. Health and survival death AUP resolution finite-gates movement/snapshot AUP and passes only `double3` absolute coordinates to the bridge; producer files do not import `Hecton8.World`, and survival no longer fabricates a death packet from runtime `Transform.position`. The bridge seam also fails closed on non-finite death AUP instead of synthesizing `double3.zero`. Survival reconciliation clears stale `_hasLastDeathRecord`/`_lastDeathRecord` so PDA/HUD last-loss consumers cannot read a successful rebirth as a legacy loss.

Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes the request in `PreSimulation`, admits only uncommitted `PlayerRespawnSignalPhase.Request` packets that carry `PlayerRespawnSignalFlags.Requested` and a nonzero sequence into the single-row request/state Vault truth, rejects `InvalidDeathAup` or non-finite `DeathAUP` before resolving any request/state Vault arrays, resolves the med-bay AUP, writes Vault request/state rows, mutates the current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see the resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count, then stops after the first accepted packet in that snapshot. `Committed` phase, `Committed`-flag packets, phase-only request packets without `Requested`, and zero-sequence packets are output/malformed facts, not Vault input, and cannot create a new Vault request. Invalid or unresolved packets return false and allow the loop to continue looking for a valid packet; accepted packets are one current rebirth fact for the single-row request/state Vault buffers. The snapshot transformer also refuses same-sequence packets marked `InvalidDeathAup` or carrying non-finite `DeathAUP` before writing committed phase data. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as the primary med-bay truth; staged route flags are accepted only with that staged target, and the job scans `MedicalBayRespawnPointDTO` rows only if the staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after the active job fence is already completed. Runtime Vault identity is stored only as 16-byte `VaultGenerationHandle<T>` descriptors; every `NativeArray<T>` view is resolved method-locally through `IDataVault.TryResolveHandle` in the phase that uses it. Dispatcher phases consume cached `_dataVault` only and use `HasHotVaultState()` so they never request Vault buffers; allocation-capable `EnsureVaultState(...)` is restricted to Awake/Start/DataVault hot-swap/editor utility paths. On disable, DataVault replacement, or partial handle acquisition failure, SHINOBU releases only owner-local respawn descriptors `71604..71613` and clears shared Physiology/Metabolism/Kinematic descriptors without `ReleaseBuffer`. The Dear Lie shader publish uses `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` with cached `_dataVault`, and a local dirty latch publishes only while active plus one zero-clear frame after fade end. Simulation also refuses to schedule another writer over the same Vault rows while a prior active handle is incomplete; it returns the combined dependency instead. Mesofauna consumes only coherent request/commit snapshots through the existing predator cognition signal stage: `Request` must carry `Requested` and must not carry `Committed`, `Committed` phase must carry `Committed`, and packets marked `InvalidDeathAup` or carrying sequence zero are ignored. `HydrodynamicKccRuntime` applies the same coherent phase+flag gate before accepting `SuspendCollision`; request-phase packets must not carry `Committed`, committed-phase packets must carry `Committed`, and packets marked `InvalidDeathAup` or carrying sequence zero are ignored before capsulecast/collision bypass.
Loop 61 owner-boundary correction: allocation-capable descriptor requests are limited to SHINOBU-owned respawn buffers `71604..71613`; shared Physiology, Decompression, Tissue, PhysiologyScalar, Metabolism, and PlayerKinematic lanes are acquired only with `IDataVault.TryGetGenerationHandle` and must already exist. Missing shared descriptors release any partial owner-local respawn descriptors and fail closed; SHINOBU does not synthesize shared body or kinematic truth.

Loop 62 allocation-lock recovery: owner-local descriptor acquisition now first tries existing `IDataVault.TryGetGenerationHandle` plus `TryResolveHandle` row-count proof before reading `IDataVault.IsAllocationLocked`. If a SHINOBU-owned buffer is missing or undersized and the Vault is locked, the route releases any partial owner-local descriptors and fails closed; if the buffer already exists, locked allocation does not prevent descriptor recovery.

Cadence:
Dirty-only on death requests. Fade update runs only while a pending request or active fade exists. VisualSync shader publication is also dirty-only: active fade frames publish payload, the first inactive frame after an active fade publishes zero, and later idle frames return before the bridge.

Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, phase/flag normalization, 128-byte layout validation, and AOT preservation. If Core must sanitize a non-finite death AUP, it preserves that lost evidence with `PlayerRespawnSignalFlags.InvalidDeathAup` so owner and external consumers fail closed. Valid `Request` phase packets gain `Requested`, valid `Committed` phase packets gain `Committed`, and invalid phase packets fall back to request plus `Requested`. Normal expected traffic is 0 or 1 player death request per frame.

GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation. `H8UberNoirApplyRespawnDearLie` no longer uses an `_MATH_LOD_LOW` compile-time branch; respawn mask frequency, grain, chroma, and abyss tint scale through continuous `detailWeight = smoothrange(0.18, 0.72, quality) * highCostAllowed`. Existing UberNoir LOD branches outside this respawn function are not SHINOBU_155 ownership.

Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads the contract lane, accepts only request-phase packets with `Requested` present and `Committed` absent, or committed-phase packets with `Committed` present, then requires nonzero sequence, no `InvalidDeathAup`, and `SuspendCollision` present. Accepted packets latch one bypass frame by `SignalBus<PlayerRespawnSignal>.SnapshotGeneration`, skip `CapsulecastCommand.ScheduleBatch`, bypass hit extraction, and mark `FlagRespawnCollisionBypass` in debug/telemetry flags. The snapshot-generation latch is written only after acceptance, so malformed packets cannot consume the generation before a valid transformed packet is visible. The accepted-generation latch prevents duplicate extension.

Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 128 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and explicit tail padding through two 64-byte cache lines. `InvalidDeathAup` uses bit 7 in the existing 32-bit flags field and does not change layout. SHINOBU's cold layout guard validates the signal size plus offsets `0/24/48/52/56/60/64/68/72/73/74/76/80/88/96/104/112/120` before allocating respawn Vault handles. The same guard validates `PlayerRespawnSignalPhase.Request == 1`, `PlayerRespawnSignalPhase.Committed == 2`, and `PlayerRespawnSignalFlags` as exactly bits `0..7` with mask `0xFF`, so invalid death-AUP evidence cannot collide with accepted side-effect flags or phase semantics. Vault DTOs are explicit 16/32/64-byte unmanaged rows.

Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.

Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after the polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. Hot dispatcher phases use `HasHotVaultState()` and cannot allocate/request Vault buffers; cold `EnsureVaultState(...)` remains boot/editor/hot-swap only and runs `ShinobuRespawnLayoutGuards.ValidateRespawnLayouts()` before any Vault generation descriptor request. `ShinobuRespawnReconciliationRuntime` no longer stores legacy `VaultBufferHandle<T>`, persistent `NativeArray<T>`, `NativeSlice<T>`, or raw Vault pointer state. Failed cold descriptor acquisition releases the ten owner-local respawn buffers before clearing handles, preventing partial Vault residency without touching shared live-state lanes. `HectonShaderGlobalDataVaultBridge.cs` now has no typed `new float4`/`new Vector4`; its vector constants, mask packing, conversion helpers, and reset payloads use explicit field assignment helpers. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and a stack-only `Span<byte>` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.
Loop 61 allocation proof: cold `EnsureVaultState(...)` creates/grows only owner-local buffers `71604..71613`; shared Physiology/Metabolism/Kinematic descriptors are read through `TryGetGenerationHandle` and absence fails closed before any dispatcher phase can schedule a reset.

Loop 62 allocation proof: cold `EnsureVaultState(...)` reacquires existing owner-local descriptors before allocation-lock rejection, so domain-reload-disabled descriptor loss and DataVault hot-swap recovery do not require new allocation when buffers already exist.
The death-adjacent survival scalar sidecar now builds `SurvivalPhysiologyScalarJob` through `default` field assignment and writes a `[NoAlias] NativeArray<SurvivalPhysiologyScalarResult>` output rather than constructing a `NativeSlice` wrapper in `UpdatePhysiologyScalars`.
`ShinobuPhysiologyRuntime.PublishVisualSyncScalars()` also builds its decompression shader `Vector4` payload through `default` field assignment before the shader bridge publish.

Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in the health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with a manual `uint _pad0` at offset `20`.
`SurvivalPhysiologyScalarResult` is explicit 32 bytes and the scalar job uses deterministic Burst/standard precision/synchronous compile flags. The one-row result buffer is requested with `UninitializedMemory` because the row is fully overwritten before consumption.
`HectonSurvivalSystem.TryResolvePhysiologyScalarBuffer()` runs a cold `UnsafeUtility.SizeOf/GetFieldOffset` guard before requesting that one-row Vault handle; layout drift returns false and leaves the buffer uncreated.
Legacy telemetry/log/event/last-loss side effects are fallback-only after `PlayerDeathReconciliationBridge.RequestRespawn(...)` fails. Reconciled death does not enter `GlobalTelemetryBus.PublishPlayerDeath`, `SurvivalVitalsChangedSignalFlags.Death`, `CaptureDeathRecord`, `RecordDeathTelemetry`, managed health `OnHealthChanged`, managed health `OnDamageTaken`, vital warning emission, zero-health combat target sync, post-damage trauma HUD/advisory fan-out, managed `OnDeath`, or `PlayerDiedEvent`; it also clears stale last-loss state during survival reconciliation.
Compile-wall hygiene: focused scan over SHINOBU death route files finds no direct `Hecton8.World|Physics|Rendering|Inventory|AI|Fauna|Construction` imports. The existing `HectonHazardManager` compatibility bridge owns the `double3` absolute-point to World AUP conversion for hazard queries so `HectonSurvivalSystem` can preserve AUP precision without importing World.

Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 128 bytes: two `double3` AUP values consume `48` bytes, scalar fields consume `28` bytes at offsets `48..75`, `Reserved1..Reserved7` consume `52` bytes of explicit 4/8-byte aligned padding/extension lanes at offsets `76..127`, and the total is exactly two 64-byte cache lines. Executable offset proof covers `DeathAUP=0`, `RespawnAUP=24`, `PlayerHash=48`, `MedicalBayHashID=52`, `DamageHash=56`, `Frame=60`, `Sequence=64`, `Flags=68`, `Phase=72`, `SuspendCollisionFrames=73`, `Reserved0=74`, `Reserved1=76`, `Reserved2=80`, `Reserved3=88`, `Reserved4=96`, `Reserved5=104`, `Reserved6=112`, `Reserved7=120`. Executable phase proof covers `Request=1` and `Committed=2`. Executable flag proof covers `Requested=1`, `Committed=2`, `SuspendCollision=4`, `MockMedicalBay=8`, `FallbackLifepod=16`, `InvalidTargetAup=32`, `PenaltyApplied=64`, `InvalidDeathAup=128`, mask `0xFF`. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.
`SurvivalPhysiologyScalarResult` is 32 bytes: `NitrogenLoad` offset `0`, `Narcosis01` offset `4`, `MovementStaminaDrain` offset `8`, `StatusMask` offset `12`, `BendsDamageRequested` offset `16`, padding byte `17`, padding ushort `18..19`, padding uint `20..23`, padding ulong `24..31`.
The respawn layout guard is executable cold code, not a documentation-only claim: boot/hot-swap allocation refuses to create handles when these sizes, critical offsets, or respawn flag bit positions drift.

Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives the rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. The XML NativeHashMap wording is implemented as a fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.

Overflow/failure mode:
If the signal lane refuses the request, health reconciliation is not applied. If medical bay validation fails, the target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus the XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.
If the bridge receives a non-finite death AUP, it returns false before `PlayerRespawnSignal` push; the caller falls through to legacy death handling rather than emitting a synthetic origin packet. If another producer bypasses the bridge and Core sanitizes a malformed packet, `InvalidDeathAup` makes SHINOBU drop it before Vault resolve and makes KCC/Mesofauna ignore it before side effects. If another producer bypasses the bridge with sequence `0`, SHINOBU drops it at the same private admission predicate and KCC/Mesofauna ignore it before side effects because zero is the sentinel/non-emitted sequence.

NaN guard status:
`ResetPlayerPhysiologyJob.WriteKinematic()` guards `target / sectorSize` with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Both SHINOBU local AUP conversion helpers now use `SafeAupClampMeters()` before clamping and casting AUP deltas to `float3`. `math.rsqrt` is guarded with `math.max(lengthSq, 0.0001f)`.

Telemetry fields:
Death AUP, respawn AUP, cause hash, frame, schedule/reconcile microseconds, flags.

Black-box fields:
300-frame `RespawnTelemetryEntry` ring plus `RespawnTelemetryCursor64` false-sharing padded cursor.

Profiler marker:
No explicit ProfilerMarker in this patch. Runtime proof must capture dispatcher/Burst job timing before acceptance.

GC proof required:
Unity Profiler/GCMonitor 0 B/frame during no-death steady state and during one death reconciliation frame.

Shutdown/disposal rule:
Vault owns native buffers. Runtime owns only generation descriptors and cold dispatcher adapters. After the active job fence, SHINOBU releases owner-local respawn buffers `71604..71613` on disable, DataVault replacement, and failed cold acquisition, then clears all descriptors. Shared Physiology, Decompression, Tissue, PhysiologyScalar, Metabolism, and PlayerKinematic buffers are never released by SHINOBU. Active job fence is registered with `H8Memory`.

Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.

Stale-handle behavior:
On DataVault replacement, active work is fenced, the previous Vault receives release calls for only owner-local respawn descriptors, generation descriptors are cleared, defaults are rehydrated from the new Vault, and fault dump state is reset. Stale descriptor generation resolves fail closed through `IDataVault.TryResolveHandle`.
Missing shared owner descriptors and stale descriptor generations resolve fail closed through `IDataVault.TryGetGenerationHandle`/`IDataVault.TryResolveHandle`; SHINOBU does not synthesize shared Physiology, Metabolism, or PlayerKinematic truth.
Existing owner-local descriptor recovery resolves fail closed through the same generation path. Locked Vault state permits recovery of already-created SHINOBU buffers but blocks any missing or undersized owner-local buffer from being created or grown.
Loop 63 stale-generation proof: `EnsureVaultState(...)` treats nonzero descriptors as insufficient. It resolves every cached descriptor and proves row count before returning true; stale descriptors are cleared and reacquired through `TryGetGenerationHandle` before any allocation-capable owner-local request.
Stale cached descriptor behavior: any cached SHINOBU/shared descriptor that fails `IDataVault.TryResolveHandle` or resolves below required row count is discarded before med-bay validation, job scheduling, CSV/editor access, black-box dump, or shader publish can read it.
Loop 64 shared fresh-acquisition proof: initial acquisition of shared Physiology, Decompression, Tissue, PhysiologyScalar, Metabolism, and PlayerKinematic lanes now requires descriptor lookup plus row resolve and `Length >= 1`. The route cannot return true on shared descriptor metadata alone.
Loop 65 hot Vault gate proof: dispatcher-facing gates reject active Vault compaction fences and per-buffer generation drift through `IDataVault.TryGetBufferGeneration` without allocating or reacquiring handles. Row-zero reads, CSV/file dump access, VisualSync fade reads, editor reads, and unsafe job pointer extraction all require explicit `HasRequiredLength(...)` proof at the access seam.
Loop 66 compile-wall/Burst proof: Physiology runtime asmdef references Core/Core.Contracts/Core.Memory plus Unity packages and no sibling runtime domains; SHINOBU respawn jobs are deterministic Burst jobs with `[NoAlias]` on every NativeArray/pointer lane, and Simulation chains `dependsOn -> reset -> fade` without hot blocking.
Loop 67 shader bridge descriptor proof: `HectonShaderGlobalDataVaultBridge` now stores `ShaderGlobalState` as `VaultGenerationHandle<float4>` and resolves a method-local `NativeArray<float4>` before Dear Lie slot writes. SHINOBU VisualSync continues to call the cached-vault overload, which now disallows allocation and falls back if the slot buffer is absent; the generic `ResolveSlotsVault()` overload remains only for legacy non-SHINOBU bridge callers.
Loop 68 med-bay radius proof: `RespawnTuningDTO.MedicalBaySearchRadiusMeters` is now consumed by both the PreSimulation resolver and the Burst fallback scan. Candidate AUP deltas are still computed in double precision, converted only for local bounded checks, and rejected when `distanceSq > radius * radius`. Rejected candidate fault bits stay local unless the final route falls back to the deterministic lifepod; a valid selected med bay publishes only selected-route flags.
Loop 69 corrupt-row proof: non-finite bay AUP, non-finite death delta, non-finite local distance, invalid terrain-clearance delta, or zero medical-bay hash now enters the rejected-candidate mask. The final `InvalidTargetAup` flag appears only when no valid bay is selected and the route falls back to lifepod. Cold mock bay generation now uses `GenerateMockRespawnPointsJob.Run(bays.Length)` instead of direct `Execute(i)`.
Loop 70 source/proof correction: subagent audit found the Loop 69 mock-job claim was still ahead of disk source. `ShinobuRespawnReconciliationRuntime` now actually calls `mockJob.Run(bays.Length)`, and focused source scan returns no `mockJob.Execute` hits.
Loop 71 cold-handle drift correction: the mock hydration block now contains no `mockJob.Schedule`, no `mockHandle`, and no orphan `DispatcherJobFence.TryComplete` after `Run`; default hydration is synchronous cold row seeding only.

Rejected alternatives:
  [x] owner-local field
  [x] cached owner interface
  [x] existing SignalBus lane
  [x] existing Vault buffer
  [x] cold HectonEventBus hook
  [x] no global route needed

Why this does not increase global monolith risk:
The only new contract payload is one unmanaged signal with a narrow death-reconciliation purpose. Persistent data stays in owner-local Vault IDs and does not modify global `BufferID` enum or sibling asmdef references.

H-Phi impact expected:
Persistent death-reconciliation state moves out of scene objects and into Vault-owned unmanaged rows. Runtime logic remains a stateless dispatcher owner around Burst jobs.

Runtime proof required before acceptance:
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, survival scalar sidecar import proof, and black-box dump validation on injected invalid AUP.

Reviewer: SHINOBU_155 self-review
Status: YELLOW

Global authority review:
Result: YELLOW
Route ID: PLAYER_DEATH_RECONCILIATION_SEQUENCE
Owner: SHINOBU_155
Instrument: `SignalBus<PlayerRespawnSignal>` + `GlobalDataVault`
Reason: Route is narrow and statically bounded, but runtime/Unity/profiler proof is absent.
Required fixes: Fresh Unity import/Console, profiler, and GC proof before `GREEN`.
Proof still missing: Unity import, Burst compile, Play Mode, KCC one-frame bypass capture, shader visual proof, GCMonitor, player-build proof.
Reviewer: SHINOBU_155 self-review
Date: 2026-05-19

Compile blocker:
Earlier guarded `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed before SHINOBU code on `CS2001` for deleted `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, then current architecture ledger recorded that `Directory.Build.targets` shields that stale generated include. The follow-up guarded Core compile now advances to external missing contract/source bridge semantic errors outside SHINOBU_155 ownership. No cross-domain stubs or generated project edits were made by this lane.

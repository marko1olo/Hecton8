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
On successful reconciliation, legacy managed `OnDeath` and `PlayerDiedEvent` fallback side effects are skipped; they remain only for unreconciled failure.

Consumer phase:
`ShinobuRespawnReconciliationRuntime` consumes the request in `PreSimulation`, resolves the med-bay AUP, writes Vault request/state rows, and mutates the current `PlayerRespawnSignal` snapshot in-place so same-frame consumers see the resolved `RespawnAUP`, `MedicalBayHashID`, request/commit flags, and clamped collision-suspend count. `ResetPlayerPhysiologyJob` then consumes that staged `RespawnStateDTO` as the primary med-bay truth; staged route flags are accepted only with that staged target, and the job scans `MedicalBayRespawnPointDTO` rows only if the staged target is missing, non-finite, or unresolved. It mutates Vault in `Simulation`, records fault/dump state in `PostSimulation`, and publishes shader scalars in `VisualSync` only after the active job fence is already completed. Dispatcher phases consume cached `_dataVault` only; cold `GlobalRegistry.DataVault` fallback is restricted to Awake/Start/editor utility paths. The Dear Lie shader publish uses `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` with cached `_dataVault`, and a local dirty latch publishes only while active plus one zero-clear frame after fade end. Simulation also refuses to schedule another writer over the same Vault rows while a prior active handle is incomplete; it returns the combined dependency instead. Mesofauna consumes the request/commit snapshot through the existing predator cognition signal stage. `HydrodynamicKccRuntime` consumes requested or committed `SuspendCollision` packets and skips capsulecast/collision resolution for one snapshot generation.

Cadence:
Dirty-only on death requests. Fade update runs only while a pending request or active fade exists. VisualSync shader publication is also dirty-only: active fade frames publish payload, the first inactive frame after an active fade publishes zero, and later idle frames return before the bridge.

Expected max events/reads per frame:
Core `GlobalSignals` configures `PlayerRespawnSignal` for expected capacity 8, max frame signals 16, low-tier frame signals 4, stable hash `0x5253504E`, direct pre-simulation flush, post-simulation clear, finite guard, 96-byte layout validation, and AOT preservation. Normal expected traffic is 0 or 1 player death request per frame.

GlobalQualityWeight behavior:
`RespawnFadeDTO` consumes continuous `GlobalQualityWeight`: low weight accelerates fade decay and collapses shader detail to blackout/grain scalars; high weight preserves longer chromatic/grain cover for Visual Overkill without changing authoritative simulation.

Physics collision suspend:
`PlayerRespawnSignalFlags.SuspendCollision` does not call Physiology from Physics. KCC reads the contract lane, accepts request or committed respawn packets, latches one bypass frame by `SignalBus<PlayerRespawnSignal>.SnapshotGeneration`, skips `CapsulecastCommand.ScheduleBatch`, bypasses hit extraction, and marks `FlagRespawnCollisionBypass` in debug/telemetry flags. The snapshot-generation latch prevents duplicate extension.

Payload/data shape:
`PlayerRespawnSignal` is unmanaged, explicit 96 bytes: death AUP, respawn AUP, hashes, frame, sequence, flags, phase bytes, and padding. Vault DTOs are explicit 16/32/64-byte unmanaged rows.

Payload managed fields present: no. Runtime managed fields are cold-only path strings, dispatcher adapter objects, and cached service handles; no persistent managed collection owns gameplay state.
UnityEngine.Object fields present: no serialized or hot-path object reference fields.

Hot-path allocation proof:
`ShinobuRespawnJobs.cs` contains no literal `new` after the polish pass. `ShinobuRespawnReconciliationRuntime.ScheduleSimulation` and `VisualSyncTick` now build job structs and shader payloads through `default` field assignment. `HectonShaderGlobalDataVaultBridge.cs` now has no typed `new float4`/`new Vector4`; its vector constants, mask packing, conversion helpers, and reset payloads use explicit field assignment helpers. Remaining runtime `new` hits are cold host/dispatcher adapter creation, cold file IO for CSV/dump, and a stack-only `Span<byte>` constructor in cold CSV ingest. Remaining `JobHandle.Complete()` calls are cold mock-medbay boot generation and teardown/service-replacement fences, not per-frame VisualSync or Simulation stalls.

Death-vicinity hygiene:
Mutable `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers in the health/survival vicinity use `default` field assignment, not object-initializer `new`. `SurvivalDatabaseItemRecord` no longer uses `Pack=1`; it is explicit 24 bytes with a manual `uint _pad0` at offset `20`.

Layout proof:
`RespawnStateDTO` 32 bytes, `RespawnRequestDTO` 64 bytes, `MedicalBayRespawnPointDTO` 64 bytes, `RespawnFadeDTO` 32 bytes, `RespawnTuningDTO` 64 bytes, `InventoryDeathPenaltyRuleDTO` 16 bytes, `RespawnTelemetryEntry` 64 bytes, `RespawnTelemetryCursor64` 64 bytes. `PlayerRespawnSignal` is 96 bytes because it carries two `double3` AUP values. `InventoryCommandSignal` remains 32 bytes and now uses offsets `14/16/20/24/28` for penalty payload metadata.

Capacity:
Vault IDs `71604..71613`: state[1], med bays[8], fade[1], telemetry[300], cursor[1], tuning[1], `InventoryDeathPenaltyRuleDTO` penalty rules[64], rule count[1], CSV scratch[32768], request[1]. Inventory receives the rule table through `InventoryCommandSignal.Payload0=71610`, `Payload1=ruleCount`, `Payload2=capacity`, and `Payload3=SHINOBU_155 source hash`. The XML NativeHashMap wording is implemented as a fixed Vault row table to preserve Vault ownership, deterministic bounded lookup, and blittable rollback/memcpy behavior.

Overflow/failure mode:
If the signal lane refuses the request, health reconciliation is not applied. If medical bay validation fails, the target falls back to deterministic lifepod AUP and flags `FallbackLifepod`/`InvalidTargetAup`. NaN/invalid AUP writes set black-box fault flags and trigger `Docs/AgentLogs/Dump_SHINOBU_155.bin` plus the XML compatibility alias `Docs/AgentLogs/Dump_RECONCILIATION_SURGEON.bin`.

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
Vault owns native buffers. Runtime owns only handles and cold dispatcher adapters; handles are cleared on disable/hot-swap. Active job fence is registered with `H8Memory`.

Scene unload behavior:
No scene reload or unload is requested by this route. Runtime host is `DontSave`; scene unload must unregister dispatcher adapters and clear cached handles.

Stale-handle behavior:
On DataVault replacement, active work is fenced, handles are cleared, defaults are rehydrated from the new Vault, and fault dump state is reset.

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
Unity import/Console, Burst compile, Play Mode death trigger, one-frame KCC collision-bypass proof, GCMonitor 0 B/frame, Profiler timing, shader fade visual capture, and black-box dump validation on injected invalid AUP.

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

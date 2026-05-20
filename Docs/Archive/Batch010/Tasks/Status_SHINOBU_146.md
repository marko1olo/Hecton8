# SHINOBU_146 Status - Mesofauna Behavioral State Machine

Date: 2026-05-19
Status: HARDENED / EXTERNAL COMPILE WALL
Domain: Echelon 3 Flora/Fauna/Biota - individual mid-level predator cognition
Prompt task count: 20

## Mandates Loaded

- AI_Creature_Cognition_States.txt
- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- REND_GPU_Driven_Animation_VAT.txt

## Loop 0 - Prompt Extraction / Hygiene

- [x] Extracted `SHINOBU_146` block from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell over the full file | DOD: strict batch prompt isolation | Alternative rejected: basic/truncated read | Estimate: 900 us
- [x] Re-read `Docs/AgentLogs/Rationale_SHINOBU_146.md` before status updates | DOD: anti-amnesia protocol | Alternative rejected: chat memory | Estimate: 350 us
- [x] Re-read `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `/Docs`, and relevant `.agents-skills` mandates during preflight | DOD: source-of-truth anchoring | Alternative rejected: invented contracts | Estimate: 4200 us

## Loop 1 - Tasks 01-05

- [x] Task 01 OOP_STATE_MACHINE_ERADICATION | DOD: `rg` scan found no `IState`, `State_Wander`, `State_Attack`, or virtual `UpdateState` class in first-party fauna AI; the remaining `FaunaStateMachine` is a serialized struct/legacy cache, not a heap polymorphic FSM. New mesofauna authority is byte state in `MesofaunaStateDTO` plus Burst `switch(CurrentState)` | Alternative rejected: deleting `FaunaBrain` serialized facade, which would break unrelated fauna authoring and compile wall | Estimate: saves 35-70 us/frame for 50 predators vs managed virtual dispatch
- [x] Task 02 NAVMESH_AGENT_PURGE | DOD: `rg "NavMeshAgent|UnityEngine.AI|m_AgentTypeID"` across source/prefabs/scenes/data returned no first-party underwater creature hit. No component purge required. New navigation is steering plus SDF repulsion | Alternative rejected: adding any `NavMeshAgent` adapter | Estimate: avoids unbounded NavMesh query/bake stalls
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD: hot DTO fields are public fields, no `get; set;`; `MesofaunaStateDTO.AsMutableRef(void*)` uses `UnsafeUtility.AsRef<T>` in the Burst job | Alternative rejected: C# properties and defensive struct copies | Estimate: 8-14 us/frame at 256 slots
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD: `MesofaunaStateDTO` is `[StructLayout(LayoutKind.Explicit, Size = 64)]` with offsets 0/24/36/40/41/42/44 and explicit pad bytes 48-63; `ValidateLayout()` checks `UnsafeUtility.SizeOf` and offsets | Alternative rejected: `Pack=1` and sequential layout | Estimate: prevents unaligned ARM64 line splits; expected 1 cache line/state
- [x] Task 05 EMERGENCY_MOCK_TARGET_DATA | DOD: `GenerateMesofaunaMockTargetsJob` writes deterministic `MesofaunaTargetDTO` records into Vault-backed mock target buffer using frame/slot/species hash math | Alternative rejected: waiting for player/prey GameObjects or managed mocks | Estimate: 10-25 us saved in isolated profiling setup
- Verification gate after loop 1: static source checks passed; compile not run because build gate later detected CPU/dotnet pressure.

## Loop 2 - Tasks 06-10

- [x] Task 06 BURST_FSM_EVALUATION_KERNEL | DOD: `MesofaunaBehaviorJob : IJobParallelFor` is Burst deterministic, uses `[NoAlias]`, raw state pointer, and an authoritative `switch(CurrentState)` for Idle/Search/Hunt/Flee/TrackScent transitions | Alternative rejected: if-chain masquerading as FSM; OOP state classes | Estimate: 45-95 us/frame saved at 50 predators
- [x] Task 07 SPATIAL_HASH_TARGET_ACQUISITION | DOD: `BuildMesofaunaTargetSpatialHashJob` builds flat bucket heads/next arrays in Vault; search reads 27 adjacent buckets and handles empty buckets by `-1` sentinel | Alternative rejected: `Physics.OverlapSphere`, managed dictionaries, private persistent `NativeHashMap` | Estimate: O(k) local candidates instead of O(n); 60-180 us/frame at 256 slots depending density
- [x] Task 08 THE_DEAR_LIE_ANIMATION_STATE | DOD: `MesofaunaVisualSyncDTO` outputs state byte, speed scalar, scent signal, obstacle pressure and desired velocity for VAT/IK/shader consumers; no Animator parameter writes | Alternative rejected: Unity Animator blend tree per predator | Estimate: 30-80 us/frame CPU saved, shifted to shader/VAT
- [x] Task 09 SDF_OBSTACLE_AVOIDANCE | DOD: job samples published threat/SDF voxel payload and computes gradient repulsion with guarded reciprocal/rsqrt; no raycast/path node | Alternative rejected: raycasts, MeshCollider, pathfinding nodes | Estimate: 25-120 us/frame saved under terrain clutter
- [x] Task 10 CONTINUOUS_SCALABILITY_TIME_SLICING | DOD: `GlobalQualityWeight` smoothstep drives vision radius 22-104m and slice modulo 10->1; continuity frames still write smooth velocity/output | Alternative rejected: binary low/high quality switch | Estimate: low quality evaluates about 10% of brains/frame; 0.1-0.4 ms saved at dense predator counts
- Verification gate after loop 2: static Burst/NoAlias scan passed; compile not run due later build gate.

## Loop 3 - Tasks 11-15

- [x] Task 11 CHEMICAL_SCENT_TRACKING | DOD: Search/Hunt fallback samples `ChemicalInfluenceGrid.ChemicalBreadcrumbWaypoint` runtime positions through AUP-local conversion, expiry/radius/channels, falloff, and scent sensitivity to enter `StateTrackScent` | Alternative rejected: GameObject scent emitters or Physics queries | Estimate: 15-45 us/frame vs managed trigger volumes
- [x] Task 12 AUP_PRECISION_INTERCEPTION_MATH | DOD: direct targets, spatial targets, scent, and intercept all subtract target AUP from predator AUP before converting to local `float3`; intercept lead uses local delta plus target velocity | Alternative rejected: absolute float world positions at 100 km scale | Estimate: prevents edge-of-world steering jitter; CPU neutral, correctness critical
- [x] Task 13 DAMAGE_AND_FLEE_ROUTING | DOD: `BeginDispatcherFrame` consumes `SignalBus<CombatDamageSignal>` in pre-sim, matches mesofauna hash/short id, writes `StateFlee`, source hash, due flag, and override threat position | Alternative rejected: per-creature MonoBehaviour callbacks | Estimate: O(active * damageSignals) only on signal frames; no hot managed event fanout
- [x] Task 14 ROLLBACK_NETCODE_STATE_FENCE | DOD: all mesofauna authoritative DTOs are blittable, fixed-size, and Burst jobs use `FloatMode.Deterministic`; no `Time.deltaTime` in FSM transitions | Alternative rejected: managed state objects and UnityEngine.Random | Estimate: enables blind memcpy snapshot; no rollback marshalling heap cost
- [x] Task 15 ZERO_INIT_OVERHEAD_BYPASS | DOD: Vault buffers are requested with `NativeArrayOptions.UninitializedMemory` and cold overwritten by `InitializeMesofaunaStateJob` | Alternative rejected: OS zero-fill plus managed initialization loop | Estimate: cold boot only, avoids full zero-fill for 10 Vault lanes
- Verification gate after loop 3: static source checks passed; compile not run due later build gate.

## Loop 4 - Tasks 16-19

- [x] Task 16 TELEMETRY_AI_RECORDER | DOD: 300-entry `MesofaunaTelemetryEntry` ring in Vault records active/hunt/flee counts, estimated hash/FSM microseconds, quality, slice modulo, hashes, and dumps `.bin` + `.h8dump` on non-finite/fault | Alternative rejected: Debug.Log spam or managed List history | Estimate: bounded 64B * 300 = 19.2 KB; post-eval only
- [x] Task 17 BEHAVIOR_TUNER_EDITOR_WINDOW | DOD: `MesofaunaAiTunerWindow` uses UI Toolkit sliders/pie chart reading/writing Vault tuning DTO; editor-only allocations are static and cold | Alternative rejected: ScriptableObject recompiles or play-mode inspector polling heap | Estimate: gameplay hot path 0 us/0 B
- [x] Task 18 CSV_SPECIES_PARAMETERS_INGESTOR | DOD: cold parser reads `mesofauna_species_profiles.csv` into Vault byte scratch, slices `ReadOnlySpan<byte>`, FNV-1a hashes names, and writes fixed flat profile table | Alternative rejected: `string.Split`, LINQ, private persistent `NativeHashMap`; literal NativeHashMap requirement superseded by H-PHI Vault law | Estimate: hot path 0 B; cold reload bounded to 4096 B scratch
- [x] Task 19 LIVE_FSM_DEBUG_GIZMO | DOD: `MesofaunaFsmDebugGizmo` and tuner SceneView draw editor-only colored state/velocity/target vectors from copied telemetry buffers | Alternative rejected: runtime LineRenderer/GameObject debug entities | Estimate: gameplay hot path 0 us/0 B; editor-only draw cost
- Verification gate after loop 4: editor API static scan passed; compile not run due later build gate.

## Loop 5 - Task 20 / Self-Audit

- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: static scans found no forbidden `foreach`, LINQ, `UnityEngine.Random`, `Pack=1`, or DTO properties in new SHINOBU files; NavMesh scan empty; Burst/NoAlias scan confirms mesofauna jobs annotated; self-audit queued for `LOG_SHINOBU_146.md` | Alternative rejected: chat-only report | Estimate: prevents untracked architecture drift
- [x] Re-read prompt after task loop boundary | DOD: PowerShell extraction of `SHINOBU_146` XML block | Alternative rejected: relying on summary memory | Estimate: 1600 us
- [x] Build gate evaluated | DOD: first sample CPU `100`, `87.9`, `39.1` with live `dotnet.exe`; second and third samples CPU `100`, `100`, `100` with no `csc.exe`; per AGENTS, build is not launched while CPU >50% | Alternative rejected: violating developer hardware gate | Estimate: protects parallel agents from compile wall contention
- [x] Final forensic report appended to `Docs/AgentLogs/LOG_SHINOBU_146.md` | DOD: includes `<SELF_AUDIT>` with task reconciliation, layout, Vault IDs, dependency graph, compile guard, Dear Lie proof | Alternative rejected: chat-only report | Estimate: 1800 us

## Loop 6 - Compile-Wall Hardening

- [x] Removed SHINOBU_146 `BufferID` enum dependency from `H8Memory.cs` | DOD: `PredatorCognitionDomain` now declares owner-local numeric Vault IDs `(BufferID)71180..71189`; global core enum no longer needs mesofauna symbols | Alternative rejected: global enum churn across 20+ parallel agents | Estimate: prevents unnecessary core rebuild/merge contention; runtime cost 0 us
- [x] Re-scanned for stale global mesofauna symbols | DOD: `rg "BufferID\\.Mesofauna|Mesofauna.*= 7118"` over `H8Memory.cs` and `PredatorCognitionDomain.cs` returned no hits | Alternative rejected: assuming patch success | Estimate: 250 us verification
- [x] Re-ran static hot-path checks | DOD: NavMesh scan empty, forbidden syntax scan empty, Burst/NoAlias scan confirms deterministic jobs, `git diff --check` reports only repo LF->CRLF warnings | Alternative rejected: build-before-static while CPU gate is closed | Estimate: 1800 us
- [x] Patched mesofauna Vault lifecycle coverage | DOD: created-check, dispose default reset, and failure-path `ReleaseCoreCognitionVaultHandles()` now include species profile, species profile count, and CSV scratch lanes | Alternative rejected: letting cold CSV buffers survive partial allocation failure | Estimate: prevents stale-handle state; runtime hot path 0 us
- [x] Removed unproven chemical breadcrumb field dependency | DOD: `TryAcquireScent` now uses only existing-contract fields proven by current code (`RuntimePosition`, `RadiusMeters`, `ExpiresAt`, `Channels`) and converts runtime waypoint positions to AUP-local deltas before float steering | Alternative rejected: relying on nonexistent `AbsolutePositionDouble` field | Estimate: avoids compile break; runtime cost unchanged
- [x] Wired designer timeout slider into deterministic FSM ticks | DOD: `ResolveStateTimeoutTicks()` consumes `MesofaunaTuningDTO.StateTimeoutSeconds` and `GlobalQualityWeight` to cap Search/Flee tick thresholds without `Time.deltaTime` | Alternative rejected: editor-only value with no runtime effect | Estimate: 0 allocations; low-quality search states expire sooner, reducing stale target pursuit
- [x] Rechecked build gate after hardening | DOD: `typeperf` CPU samples improved to `17.7/17.3` and no compiler process was active after waiting, so a minimal `dotnet build Hecton8.Core.csproj --no-restore` was allowed | Alternative rejected: launching a second build while another compile is live | Estimate: preserves parallel-agent machine time
- [x] Build attempt hit external compile wall before SHINOBU_146 code analysis | DOD: `csc` failed with CS2001 missing tracked files `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`; both `Test-Path` false, both `git ls-files` true | Alternative rejected: restoring or recreating World/Construction files from fauna ownership | Estimate: integration blocker only, 0 us/frame

## Loop 7 - Polish Mandate Re-Audit

- [x] Re-extracted `SHINOBU_146` prompt after polish mandate | DOD: PowerShell line-range extraction from `<AGENT_PROMPT id="SHINOBU_146"...>` to `</AGENT_PROMPT>` captured all 20 tasks | Alternative rejected: brittle one-line regex over multiline attributes | Estimate: 950 us
- [x] Re-read architecture ledgers before editing | DOD: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `GLOBAL_AUTHORITY_BOUNDARIES.md`, and `SYSTEMS_CONTRACTS.md` checked for payload/global authority constraints | Alternative rejected: relying on stale chat memory | Estimate: 4100 us
- [x] Origin-shift hardened direct and scent target hashes | DOD: direct prey/player target hash now derives from AUP-local `toTarget`; scent hash now derives from AUP-local breadcrumb delta, not raw runtime float position | Alternative rejected: runtime-position hashes that drift across origin shifts | Estimate: correctness critical; CPU neutral
- [x] Strengthened SDF reciprocal repulsion contract | DOD: obstacle pressure now explicitly computes `rcp(max(0.1, sdfDistance))` with guarded mean-cell distance before blending repulsion | Alternative rejected: occupancy-only pressure that did not match Task 09 math | Estimate: no extra memory; constant ALU on evaluated slices
- [x] Preserved non-behavioral output flags | DOD: mesofauna output now retains both `RetinalBlind` and `EcoHeadless` while clearing stale attack/threat flags | Alternative rejected: clobbering headless/retinal state when rewriting behavior output | Estimate: prevents false motion on headless lanes; runtime cost 1 bitmask
- [x] Audited asmdef compile-wall route | DOD: SHINOBU_146 did not add a new asmdef or new assembly reference; `MesofaunaBehavioralStateMachine.cs` is compiled by existing `Hecton8.Core`. Existing `Hecton8.Core.asmdef` already directly references sibling runtime assemblies (`Hecton8.AI.Cognition`, `Hecton8.Logistics`, `Hecton8.Cartography`, etc.) outside this domain | Alternative rejected: editing shared root asmdef from fauna ownership | Estimate: 0 us runtime; records pre-existing compile-wall debt
- [x] Audited editor facade compile route | DOD: `Hecton8.Editor.csproj` includes `MesofaunaAiTunerWindow.cs`, `Hecton8.Core.csproj` includes `MesofaunaFsmDebugGizmo.cs` behind `#if UNITY_EDITOR`, and `AssemblyInfo.cs` exposes internals to `Hecton8.Editor` | Alternative rejected: making hot DTOs public solely for editor access | Estimate: runtime 0 us; prevents editor facade drift
- [x] Static polish scans passed | DOD: no `HashFloat3(targetPosition)`, no `HashFloat3(waypoint.RuntimePosition)`, no retinal-only flag clobber, no `Pack=1`, no `UnityEngine.Random`, no `Time.deltaTime`, no `NavMeshAgent`; `git diff --check` reports only LF->CRLF warning | Alternative rejected: launching another build while external CS2001 files remain missing | Estimate: 1300 us verification

## Verification Log

- Static NavMesh scan: PASS, no first-party hits.
- Static OOP state scan: PASS for `IState`/`State_Wander`/`State_Attack`/virtual `UpdateState`; legacy `FaunaStateMachine` struct remains as serialized facade/cache.
- Static forbidden hot-path scan: PASS for new files (`foreach`, LINQ, `UnityEngine.Random`, `Pack=1`, auto-properties not found).
- Static Burst/NoAlias scan: PASS for new jobs.
- Static compile-wall scan: PASS, no `BufferID.Mesofauna*` references and no mesofauna lines remain in `H8Memory.cs`.
- Static chemical-contract scan: PASS, no `AbsolutePositionDouble` dependency remains.
- Static AUP hash scan: PASS, no runtime-position direct/scent hash remains in SHINOBU_146 code.
- Static output flag scan: PASS, mesofauna output preserves `RetinalBlind | EcoHeadless`.
- Static asmdef scan: PASS for no new SHINOBU asmdef refs; FAIL_PREEXISTING for root `Hecton8.Core.asmdef` direct sibling references outside this task boundary.
- Compile: FAILED BEFORE SHINOBU_146 CODE. External CS2001 missing tracked files: `World/ChemicalInfluenceGrid.cs`, `Construction/LogisticsPipeEvents.cs`.
- Unity runtime/profiler/GCMonitor: not available in this shell context.

## Loop 8 - Dependency And Target Identity Hardening

- [x] Removed hidden frame-lane `Complete()` path | DOD: mesofauna hash/mock helper jobs are now scheduled only after swarm admission succeeds; if admission fails, no mesofauna helper job exists and no main-thread completion is needed | Alternative rejected: scheduling helper jobs before admission and blocking to clean them up | Estimate: prevents admission-failure stall; worst-case saved cost is one hash clear plus mock target pass on rejected frames
- [x] Fixed direct prey/player target identity split | DOD: `TryResolveDirectTarget()` now uses `selectedPlayer`, so prey position cannot be paired with player AUP/hash when both targets exist | Alternative rejected: using broad `hasPlayer` after target selection | Estimate: correctness critical; CPU cost one bool
- [x] Closed CSV stale-count failure mode | DOD: `_mesofaunaSpeciesProfileCount[0]` is reset before parsing mutates the profile table; malformed or empty CSV now fails closed to zero profiles instead of stale count over cleared rows | Alternative rejected: preserving stale designer count after clearing the table | Estimate: hot path 0 us; cold reload integrity
- [x] Re-scanned job completion paths | DOD: remaining `.Complete()` calls are disposal/cold init only; no mesofauna admission-failure completion remains | Alternative rejected: treating `Complete()` grep as noise | Estimate: 700 us static verification

## Loop 9 - Blackbox Target AUP Hardening

- [x] Repaired target AUP evidence path | DOD: `MesofaunaVisualSyncDTO` is now 64 bytes and carries `TargetAup`, `TargetDistanceMeters`, and `TargetFlags`; `MesofaunaTelemetryEntry.ProbeAup` now records the validated target AUP when a hunt/flee/scent target exists | Alternative rejected: writing predator self-position as target proof | Estimate: +32 bytes/active visual sync slot; 0 new Vault lanes
- [x] Preserved target AUP through continuity frames | DOD: time-sliced continuity reads prior per-slot visual sync target AUP and writes it back if finite, keeping smooth movement and forensic target history while logic is skipped | Alternative rejected: clearing target proof on skipped frames | Estimate: one same-slot visual read on continuity frames
- [x] Added scent target position authority | DOD: `TryAcquireScent()` returns the selected breadcrumb target position in AUP-local runtime space, so scent tracking emits a concrete target AUP instead of only a direction vector | Alternative rejected: treating scent as anonymous steering with no blackbox target | Estimate: one float3 assignment on improved-score breadcrumbs
- [x] Removed float re-quantization from target proof | DOD: direct, spatial-hash, and scent acquisition now return `double3 targetAup` directly to `ResolveInterceptDirection()` and `WriteVisualAndOutput()`; the writer no longer reconstructs target proof from localized `float3 targetPosition` | Alternative rejected: rehydrating target AUP from runtime float after doing precise AUP math | Estimate: correctness critical; no extra allocation
- [x] Rewired editor gizmo target vectors to target AUP | DOD: debug vectors now use `visual.TargetAup` when valid and fall back to legacy prey/velocity only when no target exists | Alternative rejected: editor drawing prey vector while FSM actually follows scent/flee target | Estimate: editor-only cost
- [x] Static post-patch scans passed | DOD: no old `TryAcquireScent` call signature remains, no `VisualSyncDtoSizeBytes = 32` remains, `git diff --check` reports only LF->CRLF warnings | Alternative rejected: running build while external missing tracked files still abort compile before SHINOBU code | Estimate: 950 us verification
- [x] Static exact-AUP rescan passed | DOD: no old acquisition signature, no `ResolveInterceptDirection(... targetPosition ...)`, no `ProbeAup = state.AUP_Position`, no forbidden NavMesh/Random/Time.deltaTime/Pack=1/property pattern in SHINOBU-owned files | Alternative rejected: relying on visual inspection | Estimate: 1200 us verification

## Loop 10 - Telemetry Budget Dump Hardening

- [x] Added over-budget blackbox dump path | DOD: `UpdateMesofaunaPostEvaluationTelemetry()` now flags `_mesofaunaLastChainMicroseconds > 1000f`, writes telemetry flag bit 2, sets `DumpReasonOverBudgetHash`, and dumps `.bin/.h8dump` without resetting slots | Alternative rejected: recording over-budget timing without forensic dump | Estimate: 0 hot job cost; post-eval branch only
- [x] Preserved NaN/fault semantics | DOD: fault bit 1 still resets bad slots and uses `DumpReasonFaultHash`; over-budget bit does not masquerade as nonfinite fallback | Alternative rejected: collapsing performance and NaN into one fault counter | Estimate: improves blackbox diagnosis, no runtime allocation

## Loop 11 - Flag And Spatial Hash Contract Hardening

- [x] Replaced mesofauna magic flag bits with named constants | DOD: `VisualTargetFlagValid`, `VisualFlagHunt`, `TelemetryFlagFault`, and `TelemetryFlagOverBudget` now drive visual sync, gizmo target vectors, and blackbox telemetry flags | Alternative rejected: leaving `1u`/`2` forensic semantics implicit across files | Estimate: runtime 0 us, compile-time constants only
- [x] Removed hard-coded mesofauna target hash query cell size | DOD: `MesofaunaBehaviorJob.TargetHashCellSizeMeters` is scheduled from `SwarmBucketCellSize`, matching the builder's `CellSizeMeters` route | Alternative rejected: silent builder/searcher drift if the 8m cell size changes | Estimate: runtime 0 us versus old literal after constant propagation
- [x] Static scans after patch passed | DOD: `rg` found no `TargetFlags & 1u`, `telemetryFlags |= 1`, `telemetryFlags |= 2`, or `ResolveBucket(input.Position, SwarmBoundsMin, 8f)` in SHINOBU-owned code | Alternative rejected: relying on visual diff | Estimate: 600 us verification

## Loop 12 - Target DTO Spatial Hash Authority

- [x] Bound mesofauna spatial hash buckets to target DTO positions | DOD: `BuildMesofaunaTargetSpatialHashJob` now reads `MockTargets[slot].AUP_Position`, converts it to runtime-local coordinates with the slot floating-origin offset, and hashes that target position instead of the owning creature `input.Position` | Alternative rejected: bucket membership from one fact and returned target DTO from another fact | Estimate: prevents false-negative acquisition after mock/prey target offsets; extra cold hash job read is one 64B DTO per active slot
- [x] Corrected helper job dependency order | DOD: `GenerateMesofaunaMockTargetsJob` now runs before `BuildMesofaunaTargetSpatialHashJob`; mesofauna FSM depends on predator evaluation plus hash, and the hash already fences mock generation | Alternative rejected: parallel mock/hash with hash reading stale target DTOs | Estimate: trades parallelism for correctness; hash build is one IJob clear/build, still time-sliced at consumer side
- [x] Added explicit target DTO valid flag | DOD: `TargetFlagValid` now marks `MesofaunaTargetDTO.Flags`; visual target AUP validity remains a separate `VisualTargetFlagValid` constant | Alternative rejected: reusing visual forensic flag semantics for target DTO validity | Estimate: runtime 0 us after constant propagation
- [x] Rechecked build gate after target-hash patch | DOD: CPU sampled at `100`, no compiler process output was active, and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` is still absent on disk while tracked by git | Alternative rejected: launching `dotnet build` into a known external CS2001 and CPU-saturated machine | Estimate: protects parallel-agent hardware; verification remains static

## Loop 13 - Damage Flee And AUP Target Packet Hardening

- [x] Routed direct prey/mock AUP through existing AUP packets | DOD: prey uses `PackTargetAup` fallback-to-runtime and player uses `PlayerTargetAup` fallback-to-runtime in both direct acquisition and mock target generation | Alternative rejected: first-class runtime+origin target reconstruction when an AUP packet already exists | Estimate: correctness critical at origin shifts; runtime cost a few scalar checks per evaluated target
- [x] Connected damage-source flee vector to mesofauna FSM | DOD: `MesofaunaBehaviorJob` now reads Vault-backed `CognitionControl` as `[ReadOnly, NoAlias]`; `ResolveThreatPosition()` honors `HasOverrideThreatPosition` while `OverrideUntilTime > CurrentTime` | Alternative rejected: writing control override in PRE_SIMULATION but ignoring it in Burst FSM | Estimate: one control read only when resolving flee threat; no new allocation
- [x] Preserved damage source hash through Flee state | DOD: ongoing `StateFlee` keeps nonzero `state.TargetHashID` written by `CombatDamageSignal.SourceHash` instead of overwriting with generic FLEE hash | Alternative rejected: losing source-of-damage identity on first evaluated flee frame | Estimate: runtime 0 us after branchless hash helper path
- [x] Static scans after patch passed | DOD: no `ResolveThreatPosition(in input)`, no `NavMeshAgent`, no `UnityEngine.Random`, no `Time.deltaTime`, no `Pack=1`; `git diff --check` reports only CRLF warnings | Alternative rejected: build while CPU and external CS2001 gate are still blocked | Estimate: 900 us verification

## Loop 14 - Deterministic Mock RNG Explicitness

- [x] Replaced implied hash-only mock variation with explicit deterministic RNG | DOD: fallback mock target generation now creates `Unity.Mathematics.Random` from AUP-derived sector hash, `FrameId`, and stable slot/species salt; seed is forced nonzero | Alternative rejected: relying on trigonometric hash phases as implicit RNG proof | Estimate: +3 RNG draws per fallback mock target only
- [x] Kept mock motion smooth while adding RNG proof | DOD: random jitter is bounded to small angle/radius/vertical offsets and layered on the existing continuous orbit phase | Alternative rejected: frame-seeded full target randomization that would pop target positions | Estimate: preserves profiler target continuity under time slicing

## Loop 15 - Layout And Scheduler Compile Guard

- [x] Removed stale mesofauna chemical-grid initializer fields | DOD: `MesofaunaBehaviorJob` receives only the breadcrumb chemical contract it declares; obsolete `ChemicalFrontGrid`, `ChemicalOverlayGrid`, dimensions, origin, and cell-size assignments were removed from the mesofauna scheduler initializer | Alternative rejected: re-adding unused grid fields to the mesofauna FSM job and widening its dependency surface | Estimate: runtime 0 us; compile-break avoided
- [x] Restored damage flee control lane to the correct job | DOD: `Controls` is present as `[ReadOnly, NoAlias]` only on `MesofaunaBehaviorJob`, where `ResolveThreatPosition()` reads override threat position; the target hash builder has no phantom control dependency | Alternative rejected: carrying an unused control lane in the hash builder | Estimate: removes one unused job field from hash builder; flee path keeps one needed read
- [x] Expanded runtime DTO layout validation | DOD: `ValidateLayout()` now checks offsets for `MesofaunaTargetDTO`, full `MesofaunaVisualSyncDTO`, full `MesofaunaTelemetryEntry`, `MesofaunaTuningDTO`, and species profile pad endpoints in addition to primary `MesofaunaStateDTO` | Alternative rejected: self-audit stronger than executable assertions | Estimate: cold validation only; hot path 0 us
- [x] Rechecked static forbidden/scheduler scans | DOD: no stale chemical-grid mesofauna assignments, no missing behavior `Controls` field, no NavMesh/UnityEngine.Random/Time.deltaTime/Pack=1/LINQ/foreach in SHINOBU-owned files; `git diff --check` reports only LF->CRLF warnings | Alternative rejected: launching build into external CS2001 | Estimate: 1600 us verification
- [x] Rechecked build gate | DOD: `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` remains tracked but absent; CPU sampled `100/100`; no `dotnet/csc/MSBuild` process active | Alternative rejected: build under >50% CPU and known missing foreign-domain file | Estimate: protects shared machine and avoids meaningless compile wall

## Loop 16 - CSV Fail-Closed Reload Guard

- [x] Moved mesofauna species table clear before CSV path/file validation | DOD: `TryLoadMesofaunaSpeciesProfilesCsvCold()` now resets `SpeciesProfileCount` and clears the profile table before resolving the CSV path or reading bytes, so missing/empty/oversized files cannot preserve stale designer profiles | Alternative rejected: returning false while old species multipliers remain active | Estimate: cold reload only, 64 DTO clears
- [x] Kept parser allocation shape unchanged | DOD: CSV parsing still uses Vault scratch bytes plus `ReadOnlySpan<byte>` and FNV/hash numeric parsing; no `string.Split`, LINQ, `foreach`, `ToArray`, or `ToList` | Alternative rejected: managed CSV convenience parser | Estimate: gameplay hot path 0 B / 0 us
- [x] Static CSV guard scan passed | DOD: clear occurs before `ResolveMesofaunaSpeciesProfilesPathCold()` and `ReadMesofaunaSpeciesProfilesFileCold()`; forbidden parser scan returned no hits | Alternative rejected: relying on editor reload manual testing while build gate is closed | Estimate: 700 us static verification

## Loop 17 - Damage Override Stale-Vector Guard

- [x] Cleared stale damage threat override before decoding a new damage point | DOD: `ProcessMesofaunaDamageSignals()` now clears `HasOverrideThreatPosition` and zeroes `OverrideThreatPosition` before `CombatDamageSignalCodec.TryToRuntimePoint()` can set a fresh valid value | Alternative rejected: extending `OverrideUntilTime` while carrying an old threat vector | Estimate: one bit clear and one float3 zero on damage signal frames only
- [x] Preserved fallback flee behavior when damage point is absent | DOD: if no valid runtime point exists, the Burst FSM falls back through `ThreatPosition`, player position, or backward vector instead of consuming stale control data | Alternative rejected: forcing an invalid/default point as authoritative | Estimate: correctness critical; no steady-frame cost
- [x] Re-extracted original prompt after three polish loops | DOD: full `SHINOBU_146` XML block read again from `Docs/Tasks/CURRENT_BATCH.md` | Alternative rejected: relying on compressed chat state | Estimate: 950 us

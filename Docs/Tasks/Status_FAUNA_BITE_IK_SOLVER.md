# Status_FAUNA_BITE_IK_SOLVER

Prompt: FAUNA_BITE_IK_SOLVER
Role: ANIMATION_LEAD
Domain: ANIMATION/IK
Authoritative code domain: Assets/_Project/Scripts/Animation/Fauna/
Status hygiene: fresh file created 2026-05-16 after missing-file check.

## Loop 0 - Assignment Intake
- [x] Extract XML assignment from CURRENT_BATCH.md | Justification: batch prompt protocol requires agent-local extraction by ID before work. | Alternatives rejected: neighboring prompt scan and MCP-only read because truncated context can contaminate architecture. | Estimate: 40 us
- [x] Read AGENTS.md and domain map | Justification: authority spine and domain boundary must precede edits. | Alternatives rejected: direct coding from prompt because public API and folder ownership are constrained. | Estimate: 55 us
- [x] Identify and read relevant mandates | Justification: selected 8 mandate files matching IK, physics-contact truth, AUP, telemetry, signal lanes, and GC. | Alternatives rejected: reading entire registry creates context noise; reading only prompt-required two misses signal/DataVault/blackbox constraints. | Estimate: 80 us
- [x] Grep existing animation/fauna/core systems | Justification: located existing spine IK, FaunaKinematicsRuntime, GlobalDataVault, GlobalSignals lanes, and verified no BiteManager/first-party animation-event damage target. | Alternatives rejected: new isolated solver without integration because it would not mutate existing LeviathanBones before GPU upload. | Estimate: 110 us
- [x] Output mandatory pre-code [ANALYSIS] block | Justification: AGENTS.md demands explicit target/zero-GC/state/rule block before code generation. | Alternatives rejected: silent implementation because code is rejected without the block. | Estimate: 30 us

## Loop 1 - Tasks 1-5
- [x] 1. PURGE_SINGLETONS | Justification: `rg "BiteManager\.Instance"` found no first-party dependency to remove. | Alternatives rejected: inventing a replacement manager would violate decoupled registry/signal architecture. | Estimate: 10 us
- [x] 2. DEBT_CLEANUP | Justification: scanned `.anim` event function names for bite/damage triggers; no first-party damage event target was present. | Alternatives rejected: broad third-party animation import rewrite because no matching event payload existed. | Estimate: 18 us
- [x] 3. DATA_EVICTION | Justification: added `JawIkTargets`, `CurrentJawPos`, `BiteIkSolveEvents`, and `BiteIkTelemetryCursor` `BufferID`s and vault-owned buffers in `FaunaKinematicsRuntime`. | Alternatives rejected: local managed fields and per-frame allocations because hot path must be DataVault/NativeArray. | Estimate: 55 us
- [x] 4. BURST_ALGORITHM | Justification: implemented `ProceduralBiteJob` with bounded AABB closest-point descent, deterministic miss/contact flags, and no Unity physics query. | Alternatives rejected: `Animator.SetIKPosition`, collision overlaps, LINQ, and Transform mutation inside solver. | Estimate: 88 us
- [x] 5. AUP_INTEGRITY | Justification: job converts target AUP to predator-local delta before float math, limiting precision loss. | Alternatives rejected: world-space float subtraction at large origin offsets. | Estimate: 24 us
- [x] Compile verification after Tasks 1-5 [BLOCKED BY DEPENDENCY] | Justification: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` was run; current failures are outside the bite IK kernel. | Alternatives rejected: editing unrelated missing ladder/voxel/contract systems outside domain. | Estimate: 0 us runtime

## Loop 2 - Tasks 6-10
- [x] 6. DOD_SOA_LAYOUT | Justification: `ProceduralBiteJob` mutates existing `LeviathanBones` after terrain IK and before GPU upload. | Alternatives rejected: side-channel jaw mesh buffer that the existing renderer would ignore. | Estimate: 42 us
- [x] 7. SIGNAL_FLOW | Justification: added `FaunaStateChangedSignalKinds.Strike`, typed-lane publishing, and consumption from `SignalBus<FaunaStateChangedSignal>`. | Alternatives rejected: singleton `BiteManager` state and direct cross-agent object coupling. | Estimate: 26 us
- [x] 8. LOW_TIER_FAKE | Justification: low tier and stress fallback write only head bone scale/rotation, skipping mandible/tentacle writes. | Alternatives rejected: full IK on MX350 because it spends frame time on detail hidden by LOD. | Estimate: 8 us
- [x] 9. HIGH_END_OVERKILL | Justification: high/ultra tiers resolve cylindrical wrap anchors and write independent mandible/tentacle bones. | Alternatives rejected: middle-ground single jaw target for every device tier. | Estimate: 72 us
- [x] 10. REACTIVE_VFX | Justification: contact flag publishes `DebrisSpawnSignal` spark payload and `HapticRequest` crush payload from completed pose. | Alternatives rejected: damage animation events and physics collision callbacks. | Estimate: 14 us
- [x] Compile verification after Tasks 6-10 [BLOCKED BY DEPENDENCY] | Justification: rebuild still fails in unrelated systems; no emitted error targets `ProceduralBiteIkJobs.cs`. | Alternatives rejected: speculative global compile surgery. | Estimate: 0 us runtime

## Loop 3 - Tasks 11-15
- [x] 11. STP_STABILIZATION | Justification: jaw tip and head rotation use at least a 3-frame blend; rotation uses `FastNlerp`. | Alternatives rejected: instant head snap that ghosts under TAA. | Estimate: 11 us
- [x] 12. NAN_VACCINATION | Justification: acos inputs are clamped to `[-1,1]`, reach is bounded, and all pose outputs pass finite guards. | Alternatives rejected: trusting authored lengths and target bounds. | Estimate: 9 us
- [x] 13. BLACKBOX_LOGGING | Justification: every solve writes a fixed 300-entry `BiteIkSolveEvents` ring and invalid pose dumps to `Docs/AgentLogs/Dump_FAUNA_BITE_IK_SOLVER.bin`. | Alternatives rejected: chat-only failure reporting and dynamic log collections. | Estimate: 31 us
- [x] 14. TRIPLE_STRIKE_REPAIR | Justification: all bone writes validate indices against `LeviathanBones.Length` before mutation. | Alternatives rejected: assuming bone count from content authoring. | Estimate: 6 us
- [x] 15. HOMEOSTASIS_ADAPTATION | Justification: `SystemStress01 > 0.8` forces the low-tier fake path immediately. | Alternatives rejected: gradual quality fade under thermal/load spike. | Estimate: 5 us
- [x] Compile verification after Tasks 11-15 [BLOCKED BY DEPENDENCY] | Justification: third build retry hit cross-agent dependency failures in contracts, world, VFX, voxel, player, and bootstrap code. | Alternatives rejected: touching non-domain systems without ownership. | Estimate: 0 us runtime

## Loop 4 - Tasks 16-18
- [x] 16. AUDIO_SYNC | Justification: pose distance under 2m flags `AcousticPingSignal(ChannelJawSnap)` with frame throttling. | Alternatives rejected: audio animation event keys. | Estimate: 7 us
- [x] 17. RELEASE_LOGIC | Justification: out-of-reach target sets miss flag and drives a deterministic local-space snap-miss recoil pose. | Alternatives rejected: canned recovery clip and unbounded lunge extension. | Estimate: 13 us
- [x] 18. FINAL_VALIDATION [BLOCKED BY DEPENDENCY] | Justification: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` exits 1 due unrelated missing symbols/references; no bite IK compile error is emitted. | Alternatives rejected: fake success report or cross-domain patching. | Estimate: 0 us runtime
- [x] Compile verification after Tasks 16-18 [BLOCKED BY DEPENDENCY] | Justification: final compile attempt reports 66 external errors including `JobAdmissionLane` references, missing visual signals, voxel debris fields, player motor helpers, and `HectonShaderGlobalDataVaultBridge`. | Alternatives rejected: breaking domain boundary to fix other agents' slices. | Estimate: 0 us runtime

## Loop 5 - Strict Self-Read
- [x] Re-read assignment from CURRENT_BATCH.md | Justification: XML was extracted again with a raw PowerShell regex and confirmed 18-task scope. | Alternatives rejected: relying on compressed chat memory. | Estimate: 25 us
- [x] Re-read changed code for hot-path allocations, bounds, NaN, and domain drift | Justification: self-read found and fixed dead signal filter plus weak miss recovery; `rg` found no forbidden Animator IK, BiteManager, or physics overlap in the bite solver. | Alternatives rejected: accepting first pass without strict loop. | Estimate: 90 us
- [x] Read OMEGA POLISH MANDATE after all tasks are done or blocked | Justification: read after core tasks were checked or dependency-blocked; mandate requires `VERIFIED MASTER GRADE`. | Alternatives rejected: reading polish before core status. | Estimate: 5 us
- [x] Append final report to Docs/AgentLogs/LOG_FAUNA_BITE_IK_SOLVER.md | Justification: final report is on disk with wrong/done/cheats/microsecond estimates and compile wall. | Alternatives rejected: chat-only report. | Estimate: 20 us

## Loop 6 - Multiplatform Inquisition Pass
- [x] Re-read assignment from CURRENT_BATCH.md | Justification: XML block was extracted again with the correct role-bearing tag pattern after chat compaction. | Alternatives rejected: trusting compressed chat state or neighboring `LEVIATHAN_BITE_IK` prompt. | Estimate: 25 us
- [x] ARM64/Quest layout audit | Justification: bite target, pose, solve-event, and Leviathan terrain telemetry packets are all `LayoutKind.Explicit, Pack = 1`; no private NativeArray fields remain in `FaunaKinematicsRuntime`. | Alternatives rejected: relying on default struct packing or hidden MonoBehaviour-owned arrays. | Estimate: 18 us
- [x] H-Phi DataVault eviction pass | Justification: persistent spine/bone/telemetry state now lives behind `VaultBufferHandle<T>` and resolves from `GlobalDataVault` with `SystemID.AnimationFauna`. | Alternatives rejected: cached private `NativeArray<T>` fields and stale `SystemID.AICognition` ownership for animation IK buffers. | Estimate: 34 us
- [x] Stale feedback purge | Justification: inactive strike clears the vault bite target/rest pose and feedback is frame/target gated before debris, haptic, dent, or audio emission. | Alternatives rejected: allowing previous contact flags to re-fire after a strike ends. | Estimate: 9 us
- [x] Deterministic Burst pass | Justification: `LeviathanTerrainIkJob` now uses `FloatMode.Deterministic` to keep the shared bone stream predictable across desktop, Mac, ARM64, and Steam Deck targets. | Alternatives rejected: `FloatMode.Fast` for an IK stream consumed by the bite solver. | Estimate: 6 us
- [x] Managed delegate purge | Justification: removed unused `Action<AIState> OnStateChanged` and its invocation from `FaunaBrain`; in-repo search found no subscribers. | Alternatives rejected: leaving a managed delegate hook in the fauna hot path. | Estimate: 3 us
- [x] Compile verification after Loop 6 [BLOCKED BY DEPENDENCY] | Justification: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /clp:ErrorsOnly` was rerun again; latest failure is external `GlobalDataVault.ValidateAbiLayout` duplicate in core memory, with no emitted bite IK file error. | Alternatives rejected: fake build-green status or cross-domain core memory surgery. | Estimate: 0 us runtime

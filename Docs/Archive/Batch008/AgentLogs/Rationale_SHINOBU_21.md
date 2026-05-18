# Rationale_SHINOBU_21

Status: CORE TASKS DONE; GLOBAL COMPILE BLOCKED BY NON-PHYSIOLOGY DEPENDENCIES

## Decision 01 - Local physiology authority instead of editing legacy health graph

Problem: The project already contains `HectonPlayerHealth` and many cross-domain references. Directly deleting it would trigger a compile wall and break unrelated combat/audio/UI owners during a concurrent batch.

Solution: Add SHINOBU physiology as a vault-owned data authority: `PhysiologyDTO`, decompression state, scalars, telemetry, mock signals, and editor facade. Legacy float health is audited and demoted conceptually; new health truth derives from oxygen, temperature, nitrogen, and trauma mask.

Rejected Alternatives: Rewriting `HectonPlayerHealth` in-place was rejected because it is a public compatibility facade used by many existing systems. Adding a new sibling runtime dependency was rejected because it would expand compile-wall surface. A single `float Health` replacement was rejected by the prompt.

Scalability potential: Low tier uses one Haldane compartment and scalar exports. Middle/High/Ultra keep the 16 compartments and provide richer visual/audio scalar outputs without simulating organs.

Hardware Impact: Expected low-tier saving is 10-35 us versus an OOP health/event graph under 50 bodies; exact profiler proof absent.

## Decision 02 - DataVault buffers and local generic SignalBus lane

Problem: Physiology needs persistent mutable state, but private `NativeArray` ownership violates H-Phi. Heartbeat requires typed broadcast, but editing `GlobalSignals.cs` would touch a massive Core file and increase compile-wall risk.

Solution: Request all persistent physiology buffers from `GlobalDataVault` via `VaultBufferHandle<T>`. Configure `SignalBus<CardiacPulseSignal>` locally from the physiology runtime and publish unmanaged pulse packets only after the simulation fence completes.

Rejected Alternatives: Private `new NativeArray` fields were rejected as data-sovereignty breach. Adding `CardiacPulseSignal` to `GlobalSignals.cs` was rejected as unnecessary Core surface churn while a typed generic signal lane already exists. Unity events/delegates were rejected for GC and string/event overhead.

Scalability potential: Toaster mode still emits only scalar heartbeat packets. Ultra can consume the same pulse lane to drive richer DSP/haptic/visor effects without changing biology truth.

Hardware Impact: Avoids recurring managed event fan-out; expected saving 2-8 us/frame on low tier when pulse consumers exist, unmeasured.

## Decision 03 - Dear Lie physiology model

Problem: Real organ/blood-flow simulation is not playable-budget physiology. Nitrogen narcosis and bends must be believable without volumetric body simulation.

Solution: Use 16 fixed-buffer Haldane tissue tensions for gameplay truth, then export scalar narcosis, shiver, fatigue, swim bonus, toxemia, and bends risk for UI/audio/shader fakes.

Rejected Alternatives: Volumetric blood flow, organ state classes, fake creature hallucination spawning, and per-trauma polymorphic classes were rejected as expensive and harder to verify.

Scalability potential: Low = fastest tissue only. Middle = 16 tissues. High/Ultra = same truth plus histogram/finer scalar telemetry for presentation overkill.

Hardware Impact: 16 tissues x 50 entities is about 800 scalar updates per tick; fallback reduces to 50 updates. Expected low-tier saving 15-45 us/frame versus full Haldane on all entities, unmeasured.

## Decision 04 - Fixed-buffer alignment over managed tissue arrays

Problem: A 16-tissue Haldane model needs per-row tissue state, but managed arrays inside structs are illegal for Burst and `[StructLayout(Pack=1)]` would violate ARM64 alignment.

Solution: `DecompressionStateDTO` uses `unsafe fixed float TissueTensions[16]`, then `float AmbientPressure`, `float AscentRate`, and `ulong _pad0`. `PhysiologyDTO` is explicit 32 bytes with the prompt's exact offsets. Fixed-buffer pointer access was corrected after targeted csc caught CS0213.

Rejected Alternatives: `float[]`, `NativeArray<float>` per body, `List<float>`, and packed structs were rejected. SoA-only tissue buffers were rejected because the task explicitly required a DTO fixed buffer.

Scalability potential: Low uses one compartment from the same layout; Middle/High/Ultra keep all 16 and can expose richer visual scalars without changing memory contracts.

Hardware Impact: Avoids pointer chasing and ARM64 unaligned traps. Expected low-tier gain is 5-20 us versus managed or per-body array state, unmeasured.

## Decision 05 - True 300-frame blackbox instead of entity-row telemetry churn

Problem: The first implementation wrote `TelemetryCursor + entityIndex`, which would reduce a 300-entry buffer to about five frames at 64 entities.

Solution: Telemetry now records player row 0 once per completed simulation frame and advances the ring by one. Fatal oxygen or invalid math dumps the full ring to `Docs/AgentLogs/Dump_AUTOPSY_REPORT.bin`.

Rejected Alternatives: Per-entity telemetry in the same 300 slots was rejected because it violates the 300-frame requirement. Allocating a larger private telemetry array was rejected as H-Phi breach.

Scalability potential: Low tier keeps the same 300-frame truth. Ultra can consume the same ring for richer autopsy tools without changing gameplay.

Hardware Impact: Reduces telemetry writes from N bodies to one high-level row per frame; estimated 1-4 us saved at 64 entities, unprofiled.

## Decision 06 - Editor facade in an editor-only asmdef

Problem: Designers need tuning and histograms, but `UnityEditor` must not leak into the runtime physiology assembly.

Solution: Added `Hecton8.Physiology.Editor.asmdef` with `includePlatforms: Editor`, referencing runtime physiology and required Core contracts. `MetabolicControlCenterWindow` edits the unmanaged tuning row and draws the 16 tissue histogram only in the editor.

Rejected Alternatives: Runtime MonoBehaviour debug UI was rejected because it would add in-game text/UI overhead. Putting the EditorWindow under the runtime asmdef was rejected because it risks player-build contamination.

Scalability potential: Toaster/runtime pays zero cost. High-end editor users get live decompression visualization for balancing.

Hardware Impact: 0 us runtime impact; editor-only allocations are acceptable and isolated.

## Decision 07 - Compile-wall containment

Problem: Full `Hecton8.Core.csproj` compile currently fails in unrelated concurrent domains: ecosystem, global telemetry, drone fleet, and spatial audio.

Solution: Ran one global build to prove the wall, then stopped rebuild spam. Used Unity Bee response files to run targeted csc for `Hecton8.Physiology` and `Hecton8.Physiology.Editor`; both pass after local pointer/reference fixes.

Rejected Alternatives: Repeated full Unity/batch recompiles were rejected as hardware abuse. Reverting other agents' files was rejected by worktree protocol.

Scalability potential: Isolated asmdefs keep physiology iterations cheap while other domains stabilize.

Hardware Impact: Avoids repeated multi-minute compiles. No runtime microsecond claim.

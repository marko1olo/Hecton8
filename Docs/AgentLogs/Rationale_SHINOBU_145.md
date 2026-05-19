# Rationale_SHINOBU_145

Date: 2026-05-19
Agent: SHINOBU_145
Status: PENDING VERIFICATION

## Decision 001: Owner-Local Metabolism With Vault-Backed Tables

Problem: Hunger, hydration, core temperature, and toxicity need to apply to thousands of living entities without per-object Update loops or managed collections.

Solution: Store authoritative state in `NativeArray<MetabolicStateDTO>` acquired from `GlobalDataVault`, execute Burst `IJobParallelFor` from `ISlowTickable`, and complete in the late-frame swap window.

Rejected Alternatives: Standard Unity MonoBehaviour-per-creature logic was rejected because slow biology does not justify per-frame GameObject dispatch, transform access, or heap-scattered state. Managed `List<SurvivalStats>` was rejected because the task requires contiguous unmanaged DTOs.

Scalability potential: Low uses the same authoritative math at a longer cadence. Middle tightens cadence. High and Ultra keep cadence near 0.5s and spend saved cost on thermal/toxic sampling and presentation scalar fidelity.

Hardware Impact: Estimated low-end i3/MX350 gain is removal of thousands of Update dispatches and managed object dereferences. Expected hot cost target remains under 10 us per SlowTick cycle for 5000 entities pending profiler proof.

## Decision 002: Existing Global Lanes, No New Signal Lane

Problem: Starvation/dehydration and toxicity damage must notify other systems without direct dependency on combat, player health, or UI.

Solution: Use existing `SignalBus<PhysiologyStateSignal>` and `SignalBus<CombatDamageSignal>`. Extend `PhysiologyStateSignal` only with compatible constants and an unused 32-bit `EntityHashID` field in its existing 64-byte explicit layout.

Rejected Alternatives: New global SignalBus lane was rejected because existing physiology and combat lanes already own this fact class. Direct calls into combat/player health were rejected as cross-domain coupling.

Scalability potential: Low devices emit only authoritative state signals. High/Ultra consumers can add richer presentation downstream without changing survival truth.

Hardware Impact: NativeQueue push is O(1). Avoids managed event fanout and avoids per-entity direct component calls.

## Decision 003: AUP Thermal Sampling By Relative Delta

Problem: Absolute double3 AUP values lose precision if cast to float before grid mapping.

Solution: Use entity double3 AUP minus thermal grid root double3 AUP, then cast only the relative delta to float3 before cell division. Reuse the existing thermodynamics mapping contract where assembly boundaries allow.

Rejected Alternatives: `Transform.position`, world-space `Vector3`, or absolute AUP float cast were rejected because they break at map edges and violate the prompt.

Scalability potential: Low can use last thermal grid snapshot or fallback ambient. Middle/High/Ultra can sample every cadence without changing authoritative formulas.

Hardware Impact: Constant-time integer/float math per entity, no physics queries, no scene search.

## Decision 004: Dear Lie Presentation

Problem: Cold stress needs feedback without CPU particles, per-entity UI, or post-process volume churn.

Solution: Publish one global frost scalar derived from aggregated core temperature. The scalar is presentation-only and does not own gameplay truth.

Rejected Alternatives: Particle systems, screen overlay prefab spawning, and per-status post-process volume manipulation were rejected for CPU overhead and GC risk.

Scalability potential: Low reads one scalar. Middle/High/Ultra can bind the same scalar into richer visor shaders or a constant buffer without extra physiology simulation cost.

Hardware Impact: One global shader value after job completion; no per-frame GameObject work.

## Decision 005: Black Box Ring First

Problem: NaN/infinite state in an authoritative survival system must be reconstructable.

Solution: Allocate a 300-entry telemetry ring in the Vault and dump it to `Docs/AgentLogs/Dump_METABOLISM_SURGEON.bin` on NaN detection.

Rejected Alternatives: Debug.Log and editor-only console output were rejected because they allocate strings and do not survive player crashes.

Scalability potential: Ring size is fixed across tiers. Higher tiers can expose more visualization downstream without expanding the black box payload.

Hardware Impact: One fixed-size write per completed SlowTick, expected below 1 us.

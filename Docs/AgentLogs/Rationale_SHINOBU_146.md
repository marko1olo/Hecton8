# SHINOBU_146 Rationale - Mesofauna Behavioral State Machine

Date: 2026-05-19
Status: PENDING VERIFICATION

## Decision 0 - Architecture Entry

Problem: Existing assignment targets mid-predator AI previously described as `NavMeshAgent` plus OOP state classes. That model creates managed heap pointer chasing, virtual dispatch, and invalid 2.5D navigation assumptions in underwater 3D space.

Solution: Start with source archaeology, then implement only source-backed replacements: explicit 64-byte unmanaged DTO, byte FSM state, Burst jobs, AUP-local steering, spatial hash target acquisition, SDF repulsion, continuous quality-weight time slicing, VAT/IK visual-sync data, and 300-frame telemetry.

Rejected Alternatives: Standard Unity `NavMeshAgent` is rejected because it is not volumetric and imposes bake/update costs. Classic `State_Wander` / `State_Attack` classes are rejected because polymorphic managed state is not Burst-compatible and breaks cache locality. `Physics.OverlapSphere` is rejected because target lookup must use spatial hash snapshots.

Scalability potential: Low uses small search radius, sparse target refresh, cheaper visual/scent sampling, and smooth velocity continuation. Middle increases acquisition cadence and SDF checks. High adds richer scoring and target prediction cadence. Ultra spends saved CPU on broader vision radius, more frequent scent gradient reads, and richer VAT/IK state output without bloating authoritative DTOs.

Hardware Impact: Expected low-end i3/MX350 gain comes from replacing managed state dispatch and NavMesh queries with linear 64-byte DTO iteration. Static estimate only until profiler proof: 50 predators avoid OOP/Unity navigation stalls and keep brain updates sliced to a controllable fraction of the frame.

Evidence: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; relevant mandates loaded from `.agents-skills`; no runtime/profiler proof yet.

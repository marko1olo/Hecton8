# SHINOBU_225 Status

Date: 2026-05-20
Agent: SHINOBU_225
Role: LASER_CUTTER_DOD_REWRITE
Domain: ECHELON 4 Player, Kinematics & Tools / Equipment Runtime Tools
Task Count: 20
Status: PENDING VERIFICATION

## Mandates Read

- CORE_Tools_Equipment_Interaction_Raycast_Heat.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- ARCH_Execution_Phases.txt
- ARCH_Signal_Lane_Segregation.txt

## Assignment

Replace laser cutter synchronous Physics.Raycast / CPU mesh mutation / prefab spark spawning with deferred raycast packets, unmanaged DTOs, Burst-compatible processing, shader-driven Dear Lie deformation, visual-only GPU staged sparks, battery drain staging, decal staging, cooldown fencing, AUP precision, deterministic telemetry, and static inquisition tooling.

First-20-minutes route blocker: unsafe cutter path can stall gameplay when the player uses equipment on salvage/module surfaces; this work removes synchronous tool-hit and prefab-spawn hazards from the tool route. Runtime route proof remains absent until Unity import, Play Mode, profiler, and GCMonitor artifacts exist.

## State Machine

### Loop 1: Tasks 01-05

- [ ] Task 01 REALTIME_RAYCAST_INQUISITION | DOD: static source scan before mutation; rejects sync Physics.Raycast hot path and replaces with deferred math/batch architecture | Alternative rejected: leaving RaycastNonAlloc as steady-state because mandate requires RaycastCommand/SDF for primary path | Estimate: 40-90 us saved per active cutter frame, PENDING PROFILER
- [ ] Task 02 SPARK_PREFAB_SPAWN_ERADICATION | DOD: static source scan and no Instantiate in laser cutter hot path; staged VFX signal/buffer only | Alternative rejected: pooled ParticleSystem bursts because task requires GPU procedural staging/no prefab spawn | Estimate: 80-300 us plus GC/batcher risk saved per impact burst, PENDING PROFILER
- [ ] Task 03 CS1612_METADATA_STATE_ANNIHILATION | DOD: unmanaged DTO public fields only; no get/set in Burst-facing cutter records | Alternative rejected: auto-properties on structs due defensive copy/CS1612 risk | Estimate: 1-5 us under load, PENDING PROFILER
- [ ] Task 04 ARM64_LASER_LAYOUT_VALIDATION | DOD: explicit 64-byte LaserCutRequestDTO with offset validator | Alternative rejected: sequential layout because task mandates exact cache-line contract | Estimate: 2-8 us under request batch pressure, PENDING PROFILER
- [ ] Task 05 EMERGENCY_MOCK_CUTTER_TRIGGERS | DOD: deterministic synthetic request generator for isolated profiling | Alternative rejected: manual player equip test because it blocks kernel profiling and adds scene dependency | Estimate: no runtime saving; enables deterministic stress proof, PENDING VERIFICATION

### Loop 2: Tasks 06-10

- [ ] Task 06 DEFERRED_RAYCAST_BATCHING_KERNEL | DOD: schedule RaycastCommand batch without same-frame blocking | Alternative rejected: synchronous Physics.Raycast/NonAlloc as primary path | Estimate: 60-500 us stall avoided per batch, PENDING PROFILER
- [ ] Task 07 BURST_SDF_RAYMARCH_SOLVER | DOD: deterministic POST_SIMULATION solver writes carving DTOs, finite guards | Alternative rejected: CPU mesh edit/rebuild in cutter path | Estimate: 200-2000 us main-thread spike avoided, PENDING PROFILER
- [ ] Task 08 THE_DEAR_LIE_HULL_DENTING | DOD: write deformation DTO only; shader owns dent illusion | Alternative rejected: runtime mesh vertex mutation | Estimate: 300-3000 us avoided per bulkhead cut, PENDING PROFILER
- [ ] Task 09 ASYNCHRONOUS_INVENTORY_DRAIN | DOD: unmanaged battery drain request to equipment owner | Alternative rejected: direct inventory/battery mutation from cutter | Estimate: ownership correctness; microseconds PENDING PROFILER
- [ ] Task 10 THE_DEAR_LIE_GLOW_DECAL | DOD: decal request signal/buffer for scorch glow | Alternative rejected: geometry scar mesh generation | Estimate: 100-1000 us avoided, PENDING PROFILER

### Loop 3: Tasks 11-15

- [ ] Task 11 CONTINUOUS_SCALABILITY_SPARK_COUNT | DOD: GlobalQualityWeight scalar drives continuous spark multiplier | Alternative rejected: low/high binary tier branch | Estimate: GPU/CPU load shed PENDING PROFILER
- [ ] Task 12 CRITICAL_CUTTING_COOLDOWN_FENCE | DOD: tick cooldown suppresses duplicate damage writes | Alternative rejected: frame-rate-dependent MonoBehaviour timer | Estimate: queue overflow avoided, PENDING PROFILER
- [ ] Task 13 AUP_PRECISION_EPICENTER_MATH | DOD: double3 subtraction before float cast | Alternative rejected: world float absolute math | Estimate: correctness at 100 km; no microsecond claim
- [ ] Task 14 ROLLBACK_NETCODE_STATE_FENCE | DOD: deterministic Burst flags and memcpy-friendly DTOs | Alternative rejected: managed mutable state for cutter progress | Estimate: deterministic snapshot path, PENDING VERIFICATION
- [ ] Task 15 TELEMETRY_CUTTER_RECORDER | DOD: 300-entry ring and dump path on non-finite state | Alternative rejected: Debug.Log telemetry | Estimate: crash forensic coverage, PENDING VERIFICATION

### Loop 4: Tasks 16-20

- [ ] Task 16 CUTTER_TUNER_EDITOR_WINDOW | DOD: Editor-only UI Toolkit facade over tuning DTOs | Alternative rejected: runtime GUI/OnGUI | Estimate: no runtime cost; editor-only
- [ ] Task 17 CSV_CUTTER_SPECS_INGESTOR | DOD: cold span/byte parser and hashed profiles | Alternative rejected: string Split/managed CSV in gameplay | Estimate: avoids cold garbage spikes; PENDING MEASURE
- [ ] Task 18 LIVE_BEAM_DEBUG_GIZMO | DOD: Editor-only gizmo reads request buffer | Alternative rejected: runtime debug renderer | Estimate: editor-only
- [ ] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | DOD: static inquisition script writes construction report JSON | Alternative rejected: manual grep-only report | Estimate: static enforcement, no runtime cost
- [ ] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: source scans, layout offsets, no Instantiate/Raycast hot path evidence, final log XML audit | Alternative rejected: chat-only completion claim | Estimate: verification discipline, no runtime cost

### Loop 5: Strict Iteration

- [ ] Pass 1 read existing tool/cutter code
- [ ] Pass 2 implement bounded runtime DTO/jobs
- [ ] Pass 3 implement editor/static tooling
- [ ] Pass 4 scan for forbidden patterns and compile if gate allows
- [ ] Pass 5 self-review changed files and append final log

## Verification Notes

- Unity runtime proof: PENDING VERIFICATION.
- GCMonitor proof: PENDING VERIFICATION.
- Profiler microsecond proof: PENDING VERIFICATION.
- Compile proof: PENDING until CPU/build guard passes and a serial target build is run.

# Status_PREDATOR_STALK_DIRECTOR

Agent: PREDATOR_STALK_DIRECTOR
Domain: AI/COGNITION
Task count: 18
Status: PENDING VERIFICATION - AI STALKING KERNEL COMPILES, GLOBAL UNITY/DOTNET BUILD BLOCKED BY UNRELATED DEPENDENCIES

## Extraction Evidence

- [x] Read `AGENTS.md` | Justification: authority spine and compile rules required before code. DOD practice: document-first gate. Alternative rejected: coding from launcher text alone. Estimate: 1200 us.
- [x] Extract own XML from `Docs/Tasks/CURRENT_BATCH.md` with PowerShell regex tolerant of tag attributes | Justification: batch prompt protocol requires cover-to-cover ID extraction. DOD practice: strict ID lookup. Alternative rejected: using the stale missing-prompt status. Estimate: 900 us.
- [x] Read `Docs/Actual Domains of Project.txt` | Justification: domain boundary verified as AI/Cognition inside Echelon 3. DOD practice: domain proof before edits. Alternative rejected: editing Fauna runtime stalking code without cross-domain authority. Estimate: 650 us.
- [x] Read required mandates `AI_Creature_Cognition_States.txt` and `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt` | Justification: task names them explicitly. DOD practice: registry mandate gate. Alternative rejected: straight Transform/NavMesh steering. Estimate: 1800 us.
- [x] Read supporting mandates `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` and `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` | Justification: GlobalDataVault and no-hot-path-allocation are core task constraints. DOD practice: zero-GC/data-sovereignty gate. Alternative rejected: local persistent NativeArrays or singleton polling. Estimate: 1800 us.

## Loop 1 - Phase 1/2

- [x] 1. PURGE_PATHFINDING | Justification: focused `rg` found no `NavMesh`, `UnityEngine.AI`, `AStar`, or `AIManager.Instance` in `Assets/_Project/Scripts/AI/Cognition`; new kernel uses tangent/vector/SDF math only. DOD practice: forbidden dependency static scan. Alternative rejected: 3D NavMesh or A* path query. Estimate: 0 us hot path versus path solver; static only.
- [x] 2. SINGLETON_KILL | Justification: no `AIManager.Instance` dependency exists in AI/Cognition and new vault bridge accepts cached `IDataVault` from caller. DOD practice: GlobalRegistry/DataVault injection boundary. Alternative rejected: AI singleton polling. Estimate: 0 us saved from avoiding registry/singleton reads in job.
- [x] 3. DATA_EVICTION | Justification: added DataVault buffer IDs for Alpha Leviathan state, sensory stimulus, steering output, telemetry ring, and cursor; state stores `AgressionLevel01`, `CurrentPhase`, and `TargetAnchorAup`. DOD practice: DataVault sovereignty. Alternative rejected: local MonoBehaviour fields or local persistent NativeArrays. Estimate: avoids per-instance managed owner cost; runtime measurement absent.
- [x] 4. TANGENT_ORBIT_MATH | Justification: added Burst `LeviathanStalkJob`; tangent is based on `math.cross(float3(0,1,0), normalize(anchor - leviathan))` with fallback when vertical/same-position. DOD practice: Burst logic kernel, safe normalize. Alternative rejected: Transform.RotateAround, NavMeshAgent orbit, quaternion look-at loops. Estimate: <10 us static estimate for 64 slots on MX350; unmeasured.
- [x] 5. STALKING_STEER | Justification: desired ring distance is `max(8, FogDistance - 5)` and steering applies radial correction to lock the ring. DOD practice: deterministic scalar ring correction. Alternative rejected: loose chase radius or animation-authored circle. Estimate: ~1 reciprocal + dot/cross cost per slot; unmeasured.
- [x] Loop 1 compile verification | `dotnet build` without target failed because root has multiple project files; broad `Hecton8.Core.csproj` and `Assembly-CSharp.csproj` probes exceeded 120s. Targeted Roslyn probe over `H8Memory.cs`, `GlobalDataVault.cs`, and AI/Cognition files exits 0. Unity rebuilt `Hecton8.AI.Cognition.dll`. DOD practice: fail-fast compile isolation. Alternative rejected: claiming whole-project green. Estimate: 0 us runtime.

## Loop 2 - Phase 2/3

- [x] 6. SENSORY_INTEGRATION | Justification: `AlphaLeviathanSensoryStimulus` supplies noise and threshold; job increments `AgressionLevel01 += dt * 0.1` only when noise exceeds threshold. DOD practice: DataVault sensory row. Alternative rejected: direct submarine/audio manager reads. Estimate: one compare + multiply per slot; unmeasured.
- [x] 7. AUP_INTEGRITY | Justification: `AlphaLeviathanAup` stores grid/local AUP and job uses `double3` for anchor-to-leviathan distance. DOD practice: AUP double-distance authority. Alternative rejected: raw `Transform.position`/float distance across origin shifts. Estimate: extra double ALU buys deep-world stability; unmeasured.
- [x] 8. LOW_TIER_FAKE | Justification: low-tier flag/system stress path uses cheap radial fallback and 0.2 steering blend for caller-side 5Hz interpolation. DOD practice: Math LOD fake. Alternative rejected: full SDF contouring on MX350. Estimate: avoids SDF contour branch math on stressed frames; unmeasured.
- [x] 9. HIGH_END_OVERKILL | Justification: high-tier flag enables SDF gradient tangent contouring so the orbit glides along cave walls. DOD practice: performance savings spent on visual overkill. Alternative rejected: same low-tier radial steering on RTX. Estimate: one extra cross/normalize/lerp per slot; unmeasured.
- [x] 10. REACTIVE_VFX | Justification: steering output bioluminescence is 0.05 in non-charge phases and 10.0 during charge. DOD practice: state-driven presentation scalar. Alternative rejected: separate VFX poll or animation event. Estimate: no extra managed dispatch; unmeasured.
- [x] 11. STP_STABILIZATION | Justification: steering direction blends from previous direction and resets only on shift fence, preventing snap rotation output. DOD practice: temporal steering clamp via blend. Alternative rejected: instant direction replacement every job tick. Estimate: one lerp/normalize per slot; unmeasured.

## Loop 3 - Stability/Telemetry

- [x] 12. NAN_VACCINATION | Justification: all normalize paths use finite/epsilon guards; same-position fallback uses Up or previous direction. DOD practice: NaN-safe math.select fallbacks. Alternative rejected: raw normalize. Estimate: fault path avoids render/physics NaN cascade; unmeasured.
- [x] 13. BLACKBOX_LOGGING | Justification: telemetry ring writes `LeviathanAgressivity01`, `Phase`, distance, ring distance, positions, desired direction, flags, and hash. DOD practice: 300-frame circular black box. Alternative rejected: Debug.Log/string diagnostics. Estimate: one fixed struct write per slot; unmeasured.
- [x] 14. TRIPLE_STRIKE_REPAIR | No `FaunaStateChangedSignal` signature was touched and no AI/Cognition compile failure referenced it. DOD practice: avoid cross-domain repair without a failing signature. Alternative rejected: speculative Fauna signal edit. Estimate: 0 us runtime.
- [x] 15. HOMEOSTASIS_ADAPTATION | Justification: `SystemStress01 > 0.8` disables SDF contouring and uses low-tier radial fallback. DOD practice: stress load-shed. Alternative rejected: always-on SDF. Estimate: avoids high-tier contour math under stress; unmeasured.
- [x] 16. LIGHT_AVOIDANCE | Justification: stimulus contains pre-consumed headlight dot; `>0.9` forces Retreat. DOD practice: decoupled signal-to-vault ingestion boundary. Alternative rejected: direct light component polling in job. Estimate: one compare/select per slot; unmeasured.
- [x] 17. ACOUSTIC_LURE | Justification: active sonar ping selects `PingAup` as orbit anchor for 10 seconds. DOD practice: signal state in sensory row. Alternative rejected: direct sonar singleton lookup. Estimate: one flag/age/intensity gate; unmeasured.
- [x] 18. FINAL_VALIDATION [BLOCKED BY DEPENDENCY] | `dotnet build` root fails with MSB1011 because multiple project files exist; broad dotnet project probes exceeded 120s; Unity batch compile rebuilt `Hecton8.AI.Cognition.dll` but whole-project compile still fails in unrelated `Physics.Tethers.Contracts`, `Audio.Virtualization`, and editor tooling assemblies. DOD practice: 3-strike dependency wall. Alternative rejected: editing unrelated assemblies outside domain. Estimate: 0 us runtime.

## Loop 4 - Self-Review

- [x] Static scan: no NavMesh/AStar/AIManager in AI/Cognition | Justification: Great Purge proof. DOD practice: forbidden token scan. Alternative rejected: manual eyeballing only. Estimate: 400 us scan time.
- [x] Static scan: no LINQ/foreach/GameObject.Find/FindObjectOfType in AI/Cognition | Justification: zero-GC hot path proof. DOD practice: forbidden token scan. Alternative rejected: profiler claim without static gate. Estimate: 500 us scan time.
- [x] Compile pass | Targeted Roslyn compile exits 0; Unity log shows `Hecton8.AI.Cognition.dll` Csc/ILPostProcess/CopyFiles completed with `ExitCode: 0`; global Tundra build still fails outside AI/Cognition. DOD practice: assembly-local verification plus explicit global blocker. Estimate: 0 us runtime.

## Loop 5 - Omega Gate

- [x] OMEGA_POLISH_MANDATE | Removed conditional branch/ternary selections from `LeviathanStalkJob` hot selection paths using `math.select` and bit-mask selection; AUP shift handling uses `ObservedShiftFrameId`/`LastShiftFrameId` to reset steering and flag telemetry. DOD practice: branchless Burst polish and shift snap-fence. Alternative rejected: stale pre-shift interpolation. Estimate: avoids one conditional AUP target branch per slot; unmeasured.

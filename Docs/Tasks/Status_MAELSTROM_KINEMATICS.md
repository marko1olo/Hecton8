# Status_MAELSTROM_KINEMATICS

Agent: LOCOMOTION_ENGINEER  
Prompt: MAELSTROM_KINEMATICS  
Domain: ECHELON 4 - PLAYER, KINEMATICS & TOOLS / LOCOMOTION  
Status: PENDING VERIFICATION

## Pre-Code Analysis
Target: replace collider/trigger whirlpool authority with a deterministic maelstrom field sampled by kinematics and visual systems.  
Affected systems: HectonFluidEngine, PlayerKinematicsRuntime, SubmarineAutoLevelBallastController, marine snow compute/renderer, visor uber post, SargassumMicroFaunaBoids.  
Zero GC proof: fixed-capacity native/array state, bounded for loops, no LINQ, no collider triggers, no AreaEffector/PointEffector path.  
State check: status/rationale were missing at session start; scan found no WhirlpoolManager.Instance, no Tornado.cs, no first-party AreaEffector/PointEffector.  
Rule quote: "Default solution is a deterministic presentation fake" and "Direct Rigidbody.AddForce calls are owned by PhysicsApplySystem after force packet gather."

## Mandates Read
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- CORE_Submarine_Vehicles_Kinematics_AUP.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## Checklist
- [ ] 01. SINGLETON ERADICATION | DOD pending: static scan only; no WhirlpoolManager.Instance found | Alternative rejected: introduce WhirlpoolManager singleton | Estimate: 0.0 us hot-path cost
- [ ] 02. SIGNAL MIGRATION | DOD pending: consume maelstrom spawn through existing decoupled runtime path without touching dirty GlobalSignals.cs | Alternative rejected: editing dirty GlobalSignals.cs | Estimate: bounded drain, sub-5 us per signal
- [ ] 03. ASMDEF ISOLATION | DOD pending: avoid concrete cross-domain dependency; note if contracts cannot be added safely | Alternative rejected: cyclic Core <-> anomaly assembly | Estimate: 0.0 us hot-path cost
- [ ] 04. DEAD CODE HUNT | DOD pending: scan first-party prefabs/assets for AreaEffector/PointEffector/Tornado | Alternative rejected: raw YAML mutation without proof | Estimate: 0.0 us runtime cost
- [ ] 05. VORTEX S.O.A. | DOD pending: fixed-capacity active maelstrom NativeArray<float4> | Alternative rejected: List<T> or collider volumes | Estimate: 0.5-2.0 us per sampled body
- [ ] 06. BURST EVALUATOR | DOD pending: player and submarine jobs use squared-distance loop | Alternative rejected: sqrt falloff | Estimate: 1-4 us per controlled body
- [ ] 07. SUCTION & TANGENT | DOD pending: suction vector plus optional tangent cross(up,toCenter) | Alternative rejected: PhysX AddForce trigger | Estimate: 1-3 us per active maelstrom
- [ ] 08. FORCE APPLICATION | DOD pending: velocity delta/queued force through existing authority | Alternative rejected: direct Rigidbody.AddForce outside owner | Estimate: bounded by active count
- [ ] 09. GPU PARTICLE SYNC | DOD pending: bind maelstrom buffer to Hecton_MarineSnow.compute | Alternative rejected: CPU per-particle swirl | Estimate: GPU-only cost
- [ ] 10. POST-PROCESS WARP | DOD pending: scalar/center/radius to visor uber post | Alternative rejected: camera trigger volumes | Estimate: one material scalar/vector update
- [ ] 11. AUDIO RUMBLE | DOD pending: AcousticPingSignal from center AUP via existing lane or blocked by dirty GlobalSignals.cs | Alternative rejected: AudioSource.PlayOneShot | Estimate: one signal per slow cadence
- [ ] 12. AUP SHIFT SAFETY | DOD pending: shift active maelstrom runtime centers on origin shift | Alternative rejected: stale world-space centers | Estimate: active count loop on shift only
- [ ] 13. ESCAPE VELOCITY | DOD pending: clamp suction/velocity delta below submarine escape band | Alternative rejected: unbounded inverse-square pull | Estimate: clamp only
- [ ] 14. MATH LOD | DOD pending: low tier cap 1 and suction-only | Alternative rejected: identical high-tier swirl on MX350 | Estimate: saves tangent math
- [ ] 15. ZERO-GC | DOD pending: static scan after edits | Alternative rejected: managed allocations in Tick/FixedTick | Estimate: 0 B/frame target
- [ ] 16. BLACKBOX DUMP | DOD pending: active count/hash into telemetry/ring | Alternative rejected: no post-mortem state | Estimate: one 64B entry/frame
- [ ] 17. EVENT BUS DAMAGE | DOD pending: center event horizon emits CombatDamageSignal or documents block | Alternative rejected: direct health calls | Estimate: rare event, bounded
- [ ] 18. CROSS-DOMAIN AUDIT | DOD pending: boids read maelstrom array or reuse massive threat path | Alternative rejected: concrete fauna coupling from locomotion | Estimate: GPU threat pass
- [ ] 19. OMEGA COMPILE CHECK | DOD pending: compile/static cross-product verification | Alternative rejected: unverified Burst float3 math | Estimate: no runtime delta

## Loop Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md. Relevant mandates read. Initial scan found existing analytical whirlpool support inside HectonFluidEngine but no maelstrom-specific field yet.

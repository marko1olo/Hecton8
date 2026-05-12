# Status_BIOLUMINESCENCE_DIRECTOR

Date: 2026-05-12
Agent Role: LIGHTING_TECH
Prompt: BIOLUMINESCENCE_DIRECTOR
Domain: Domain 29 - Bioluminescence Sync
Status: PENDING VERIFICATION

Mandates loaded:
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- REND_URP_Graphics_HotPath_Optimization_HLOD.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt

## Checklist

- [ ] Task 1 - Singleton eradication | PENDING | DOD: registry/bootstrap ownership. Rejected: static Instance coupling. Estimate: 15 us/frame saved if Update singleton path removed.
- [ ] Task 2 - Signal migration | PENDING | DOD: GlobalRegistry or signal consumption. Rejected: direct Player.Instance polling. Estimate: 5 us/frame saved.
- [ ] Task 3 - ASMDEF isolation | PENDING | DOD: lighting assembly depends only on Core.Contracts where present. Rejected: broad Core dependency. Estimate: 0 us/frame, compile isolation gain.
- [ ] Task 4 - Dead code hunt | PENDING | DOD: remove legacy MPB glow scripts. Rejected: material property block on standard geometry. Estimate: 20 us/frame saved under dense flora.
- [ ] Task 5 - Global pulse sine | PENDING | DOD: central SlowTick global phase. Rejected: per-object Update sine. Estimate: 40 us/frame saved under dense coral.
- [ ] Task 6 - Global shader vars | PENDING | DOD: global shader property push. Rejected: per-material mutation. Estimate: 30 us/frame saved.
- [ ] Task 7 - Material audit culling | PENDING | DOD: shader reads globals, no per-material pulse props. Rejected: material clone/property churn. Estimate: 15 us/frame saved.
- [ ] Task 8 - Day/night suppression | PENDING | DOD: registry/celestial contract if available, fail-safe otherwise. Rejected: unbounded biolum in daytime shallows. Estimate: 10 us/frame GPU ALU saved when suppressed.
- [ ] Task 9 - Proximity blackout | PENDING | DOD: deterministic predator blackout with 2s fade. Rejected: per-plant predator checks. Estimate: 50 us/frame saved in plant fields.
- [ ] Task 10 - Touch ripple wake | PENDING | DOD: fixed-cap ripple data and global buffer. Rejected: spawned ripple objects. Estimate: 35 us/frame saved.
- [ ] Task 11 - Burst distance job | PENDING | DOD: IJobParallelFor / no LINQ. Rejected: managed sorting. Estimate: 25 us/frame saved.
- [ ] Task 12 - Shader ripple math | PENDING | DOD: dot(diff,diff) falloff. Rejected: distance()/sqrt. Estimate: 10 us/frame GPU ALU saved.
- [ ] Task 13 - Wave synchronization | PENDING | DOD: flow scalar modulation via contract/fallback. Rejected: new flow dependency. Estimate: 0 us/frame, visual coherence gain.
- [ ] Task 14 - AUP shift safety | PENDING | DOD: shift delta applies to ripple positions. Rejected: stale world trails. Estimate: correctness guard.
- [ ] Task 15 - Math LOD | PENDING | DOD: low-tier ripple shader branch disabled. Rejected: uniform full path on MX350. Estimate: 60 us/frame GPU saved on low tier.
- [ ] Task 16 - Zero-GC | PENDING | DOD: no managed hot-path allocation. Rejected: LINQ, strings, per-frame containers. Estimate: GC 0 B/frame target.
- [ ] Task 17 - Omega compile check | PENDING | DOD: local compile attempt and diagnostics. Rejected: stale proof. Estimate: verification only.
- [ ] Task 18 - Telemetry | PENDING | DOD: active ripple count sent to available blackbox path or documented blocker. Rejected: chat-only telemetry claim. Estimate: crash diagnosis gain.

## Iteration Log

- Loop 0: Prompt extracted. Mandates loaded. No code changed.

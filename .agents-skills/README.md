# HECTON-8 Mandate Registry

Date: 2026-05-11
Status: ENFORCED REGISTRY / PENDING RUNTIME VERIFICATION

Purpose: stable index for `.agents-skills`. This folder contains technical mandates, not brainstorming notes.

Current inventory: `75` `.txt` mandates plus this `README.md` registry index.

## Authority

- `AGENTS.md` defines global rejection rules and the current authority spine.
- This file defines how to read the mandate registry.
- Task-relevant mandate files define local rules.
- Dated reports are evidence snapshots only. If a report changes policy, the policy must be promoted into `AGENTS.md`, this registry, or a stable `Docs/*.md` authority file.
- Current source and fresh logs still outrank prose claims.

## Read Rule

Before coding or writing a technical report, read `2-8` mandates that match the task domain. Do not bulk-load the whole registry as context noise.

Minimum examples:

- physics, movement, vehicles, collision: `PHYS_Physics_Integrity_Determinism_ForceMode.txt`, `CORE_Submarine_Vehicles_Kinematics_AUP.txt`, `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- rendering, fog, light, particles: `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`, `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`, `REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- save, streaming, persistence: `DATA_Save_Persistence_Binary_Delta_Checksum.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `STRM_Persistent_Object_Registry.txt`
- UI/audio/presentation: `UI_Data_Streaming_ZeroGC_Optimization.txt`, `UI_Diegetic_Physical_Interfaces.txt`, `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`, `AUDIO_Hrtf_Binaural_Spatialization.txt`
- tooling/procedural/world: `TOOL_Procedural_Wreckage_Generator.txt`, `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`, `VOX_Voxel_World_Logic_Carving_Persistence.txt`

## Current Doctrine

- Visual-realistic fake first. Simulate only gameplay truth.
- Any runtime system over `0.1ms` is suspicious until profiler proof and load-shed behavior exist.
- No per-proton, per-droplet, per-bubble, per-cable-segment, or per-flora-blade truth by default.
- Zero GC in hot paths remains non-negotiable.
- Unity import, Console, Play Mode, profiler, GCMonitor, player-build, memory, frame-time, scene wiring, and visual quality are `PENDING VERIFICATION` unless fresh artifacts prove them.

## Engineering Data

| Fact | Current Value | Enforcement |
|---|---:|---|
| Mandate files | 75 | Registry audit must update this value when files are added or removed. |
| Runtime coroutine tolerance | 0 | `IEnumerator`, `yield return`, and `StartCoroutine` are rejected in gameplay hot paths. |
| Unity 6000 render path | RenderGraph | New URP renderer features use `RecordRenderGraph`; Compatibility Mode is legacy debt. |
| Async Unity object path | `UnityEngine.Awaitable` | `Task` is reserved for owned persistent workers or non-Unity background work. |

## Conflict Resolution

When mandate text conflicts:

1. `AGENTS.md` wins.
2. A dated `2026-05-11` override in a mandate wins over older body text.
3. `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` wins over simulate-first wording for water, light, deformation, pressure, flow, ambience, cable sag, particles, flora motion, and distant motion.
4. `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` wins on frame, VRAM, RAM, quality-tier, and load-shed budgets.
5. `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` wins on allocation policy.
6. Current source/log evidence wins over undocumented assumptions.

## Registry Buckets

- `ARCH_*`: bootstrap, registry, service ownership.
- `OPT_*`: performance, zero-GC, native memory, cinematic-cheat doctrine.
- `PHYS_*`: physics truth, contacts, kinematics, tether, fluid/incursion.
- `REND_*`, `GPU_*`, `VOX_*`: rendering, VFX, shader, compute, voxel, MapMagic.
- `CORE_*`: player-facing systems, submarine, tools, weather, damage, survival.
- `AI_*`, `ANIM_*`: navigation, cognition, boids, IK, animation.
- `DATA_*`, `STRM_*`, `LOGI_*`, `NET_*`: data, save, streaming, logistics, reconciliation.
- `AUD_*`, `AUDIO_*`, `UI_*`, `CTRL_*`: audio, UI, haptics, presentation.
- `TOOL_*`, `DBG_*`, `PROJECT_*`: tooling, telemetry, compatibility.

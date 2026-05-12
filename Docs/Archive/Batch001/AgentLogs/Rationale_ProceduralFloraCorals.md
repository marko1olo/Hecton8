# ProceduralFloraCorals Rationale

Status: PENDING VERIFICATION

## 2026-05-11 Initial Boundary Decision

Problem: The assignment asks for a "Living Biota" engine, but project documents already name a flora runtime owner stack and forbid parallel runtime stacks.

Solution: Patch existing shader/runtime owners: `FloraInteractionManager` publishes interaction globals; kelp/coral master shaders consume them. Use deterministic visual fakes for motion/reaction.

Rejected Alternatives: New `LivingBiotaEngine` MonoBehaviour, per-plant controllers, physics joints, or collision-triggered leaf bending. These add scheduler ambiguity, GC risk, and main-thread cost.

Scalability potential: Low uses one-to-two wave terms and existing globals; Middle uses full shader interaction; High/Ultra can keep extra vertex harmonics and richer biolum response in the same material contract.

Hardware Impact: MX350 keeps work on GPU vertex/fragment ALU with no managed hot-path allocation. Exact microseconds require Unity profiler; local status remains PENDING VERIFICATION.

## 2026-05-11 Mandate Selection

Problem: Flora motion touches rendering, submarine motion, weather/current fields, and celestial lighting.

Solution: Follow these 8 mandates: `REND_Instanced_Flora_Physics`, `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `GPU_Compute_Kernels_Kernels_Optimization_MX350`, `CORE_Submarine_Vehicles_Kinematics_AUP`, `CORE_Weather_Abyssal_FlowField_Currents`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`.

Rejected Alternatives: Reading all mandates or only AGENTS.md. All mandates would waste context and risk cross-domain drift; AGENTS-only would miss shader and vehicle constraints.

Scalability potential: Low/MX350 path is shader fake and global fields; High/Ultra can spend saved CPU on denser scatter and more visible glow, not per-blade physics.

Hardware Impact: Keeps CPU changes to existing global publication and moves visual work to batched instanced shaders. Measurements absent until Unity profiler/console logs exist.

## 2026-05-11 Loop 1 Decisions

Problem: Kelp and coral were visually static because motion/reaction was either triangle-pulse only or absent from coral vertices.

Solution: Implemented bounded multi-octave sine-parabola vertex displacement in kelp shaders, with root pinning via `uv.y * uv.y`, current/wind scalar influence, player flutter, and submarine propwash vector bending. Coral uses a cheap flashlight cone/range morph plus player proximity retraction.

Rejected Alternatives: Unity joints, Rigidbody leaves, per-anemone Animator states, collision trigger volumes, and direct KCC references. These violate visual-fake-first, add main-thread dependencies, or allocate/dispatch per plant.

Scalability potential: Low/MX350 keeps the same shader path with reduced amplitude under `_QUALITY_MX350`; Middle/High/Ultra retain extra harmonics and brighter lunar biolum in the same material variants.

Hardware Impact: CPU addition is one cached shader property ID plus one `Shader.SetGlobalVector` in an existing publish path. GPU impact is vertex ALU only; no new textures, buffers, GameObjects, MPBs, or managed hot-path allocations.

Problem: Full project compile could not verify due unrelated input assembly errors.

Solution: Ran an isolated `Assembly-CSharp` compile with project references disabled to validate touched C# syntax and references, then recorded the full-build dependency wall in status.

Rejected Alternatives: Editing `Hecton8.Input` from the flora domain or claiming compile readiness from a failed full build.

Scalability potential: No domain-crossing workaround added; integrator can fix input assembly separately without flora coupling.

Hardware Impact: No runtime impact from this verification fallback. Full Unity import/profiler proof remains PENDING VERIFICATION.

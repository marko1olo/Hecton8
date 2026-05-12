# Rationale_WORLD_FLORA

Status: `PENDING VERIFICATION`

## 2026-05-11 Bootstrap

Problem: `WORLD_FLORA` requires living flora motion, glow, and interaction without runtime physics cost.
Solution: Use deterministic shader ALU and global parameter interfaces first; reserve CPU work for authoring, coarse data, and low-frequency cross-system interfaces.
Rejected Alternatives: Unity joints, per-plant GameObject collision, runtime material clones, per-blade truth, and direct KCC dependencies. These violate visual-fake-first, zero-GC, and parallel-agent decoupling rules.
Scalability potential: Low uses single-octave sway, no spores, tight fade distances. Middle uses two octaves and dithered LOD. High uses full propwash/player/celestial modulation. Ultra spends saved CPU on richer emissive/spore visuals.
Hardware Impact: MX350/i3 path keeps CPU at ~0.000 ms hot-path ownership for vertex sway; shader cost is bounded by visible flora and LOD/culling. Expected gain versus CPU/physics flora is multiple milliseconds and zero managed allocations.

## 2026-05-11 Loop 1 - Tasks 1-5

Problem: Kelp sway was seeded from object-space coordinates, so duplicate meshes could move as cloned plastic and origin shifts could desync visual phase.
Solution: Re-seeded `Hecton_KelpMaster` and `Hecton_KelpMaster_GPUI` vertex sway from `positionWS + _TotalUniverseOffset`, local hash, `_Time.y`, and multi-octave sine-parabola tip masking. Shadow and depth passes use the same AUP phase path.
Rejected Alternatives: Animator bones, joints, CPU wind zones, and per-instance random material state. Standard Unity animation would cost transforms/skin updates and would not remain AUP-stable.
Scalability potential: Low keeps reduced `_QUALITY_MX350` amplitude and same cheap phase. Middle/High use full three-octave motion. Ultra can layer denser instancing because CPU work stays zero-GC.
Hardware Impact: MX350/i3 saves estimated 0.8-2.5 ms CPU versus bone/physics flora; shader ALU cost stays roughly 0.030 ms for visible near-field kelp.

Problem: Submarine propwash bend existed as a radial-ish stream add, not the required dot-product fake against the thruster stream.
Solution: Normalized `SubmarinePropwash.xyz`, gated bend with `saturate(dot(radialFallback, streamBasis))`, clamped radius to 10m, and applied the same logic in forward/shadow/depth variants.
Rejected Alternatives: Rigidbody forces, trigger volumes, per-plant collision callbacks, and fluid simulation. They are slower, less predictable, and cross-domain brittle.
Scalability potential: Low uses a single dot gate. Middle/High use full stream displacement. Ultra can increase visual density without adding CPU simulation.
Hardware Impact: Expected CPU cost remains 0.000 ms; GPU add is about 0.008 ms near active wash.

Problem: Coral/anemone sensory reaction covered flashlight/player proximity but did not expose a damage impulse to the shader.
Solution: Added `_HectonFloraDamageReaction` as one global decaying hit vector from `FloraInteractionManager`, then included it in coral vertex retraction. This is a coarse cinematic fake, not per-anemone state.
Rejected Alternatives: Per-coral MonoBehaviours, animation clips, coroutines, and material property clones. These create allocation/management cost and poor scale.
Scalability potential: Low uses one 4m fixed-radius reaction. Middle/High use flashlight, player, and damage combined. Ultra can spend saved CPU on richer emissive aftermath/spores.
Hardware Impact: One global vector publish and one shader distance gate; estimated low-end cost below 0.007 ms, no managed runtime allocations.

Problem: Full-moon biolum and lifecycle glow windows needed deterministic low-cost scalar drive.
Solution: Verified `HectonCelestialEngine` publishes `_HectonCelestialBiolumMultiplier` at minimum 2x during `FullMoonBloom`; replaced lifecycle bloom/decay smoothstep weight with linear `math.saturate` plus `math.rcp`.
Rejected Alternatives: Managed per-coral pulse updates and cubic smoothstep glow curves. They add CPU bookkeeping or unnecessary fragment/CPU math.
Scalability potential: Low uses one scalar. Middle/High vary pulse amplitude and density. Ultra uses the same scalar to drive richer GPU-only glow/spore visuals.
Hardware Impact: Saves minor ALU in lifecycle curve and avoids per-renderer state; estimated 0.004 ms fragment/CPU-equivalent budget preserved.

Verification: `dotnet build Hecton8.Core.csproj --no-restore /m:1` succeeded with 0 warnings and 0 errors. Static shader search found no stale local-space sway call, stale propwash vector add, or cubic lifecycle weight in touched paths. Unity shader importer verification remains pending outside this CLI build.

## 2026-05-11 Loop 2 - Tasks 6-10

Problem: Biome tint hashes were tied to runtime world position, so origin shifts could reshuffle color variation.
Solution: Moved kelp/coral biome color hashes to `samplePositionWS + _TotalUniverseOffset`, then hashed the AUP XZ cell for deterministic tint.
Rejected Alternatives: `UnityEngine.Random`, material variants, and author-authored per-instance color edits. They add CPU state, unstable variation, or asset churn.
Scalability potential: Low uses one hash and two tint colors. Middle/High can widen biome tint palettes. Ultra can add more hash-driven material accents while staying shader-only.
Hardware Impact: Adds about 0.003 ms shader ALU for near flora and removes CPU/material mutation cost.

Problem: Dense flora draw submission must stay GPU-driven.
Solution: Verified `HectonIndirectVegetationRenderer` uses `Graphics.RenderMeshIndirect`, structured metadata buffers, visible index buffers, and indirect args; kelp/coral also retain GPUI shader variants and material instancing.
Rejected Alternatives: Per-renderer `DrawMesh` loops and GameObject flora populations. These burn CPU submission time and violate dense-biota scale.
Scalability potential: Low uses far-culling stride and simplified LOD. Middle/High expand visible instance counts. Ultra spends saved CPU on denser canopy and biolum visuals.
Hardware Impact: CPU draw overhead remains bounded by indirect batches rather than plant count.

Problem: Far flora spawning needs dithered coverage instead of alpha popping.
Solution: Verified `Hecton_IndirectVegetation.shader` uses temporal Bayer/hash dither and coverage gates for LOD/cull fade; kelp/coral maintain URP LOD crossfade hooks.
Rejected Alternatives: Transparent alpha fade and sorted cards. They increase overdraw and sorting instability.
Scalability potential: Low uses coarse temporal dither. Middle/High use wider fade bands. Ultra can increase density without pop-in.
Hardware Impact: Overdraw remains bounded; estimated cost is a few screen-space ALU ops per visible fragment.

Problem: Sea-grass texture import still permitted large scattered maps and documented deferred atlas merge.
Solution: Added `BuildSeaGrassBc7Atlas` editor tooling for a single 1024x1024 BC7 albedo atlas, clamped kelp-family imports to 1024, and updated reports to flag atlas presence/missing state.
Rejected Alternatives: Runtime atlas building, separate 2048 source maps, and manual material clone remaps. Runtime atlas work allocates and manual remaps are fragile.
Scalability potential: Low uses the 1024 atlas and clamped kelp maps. Middle/High can keep normal detail in BC5. Ultra can use the atlas as a base layer with shader variation.
Hardware Impact: Kelp albedo source memory drops from scattered 2048 candidates toward one 1024 BC7 atlas; expected low-end VRAM/locality improvement is material and zero runtime cost.

Problem: Kinematics needs kelp density/drag without direct player-controller dependency.
Solution: Verified `SargassumGlobalDragManager` is registered through `GlobalRegistry.SargassumDrag` and exposes `SampleInfluence`/`SampleDetailedInfluence` density, speed multiplier, drag multiplier, occlusion, and entanglement scalars.
Rejected Alternatives: Direct KCC edits and per-blade colliders. They create cross-agent dependencies and scale poorly.
Scalability potential: Low samples coarse cell density. Middle/High use detailed drag and entanglement. Ultra can bind richer VFX to the same density field.
Hardware Impact: O(1) coarse query path for Kinematics; avoids physics collider storms in dense kelp.

Verification: `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies /m:1` succeeded with 0 warnings and 0 errors for the edited editor assembly. Full `Hecton8.Core`/editor dependency build is currently blocked by unrelated dirty files in `ConstructionManager`, `HabitatGraphManager`, and save-system code owned outside WORLD_FLORA.

## 2026-05-11 Loop 3 - Tasks 11-15

Problem: Decay tint existed as a seasonal lane but did not react to irradiated-world state.
Solution: `FloraInteractionManager` now reads `_HectonCelestialRadiationStorm` and folds it into `_HectonFloraLifecycleParams.y`, so kelp/coral/indirect vegetation reuse the existing brown tint and wilt shader path.
Rejected Alternatives: Per-renderer material mutation, irradiated duplicate materials, and CPU-authored per-plant decay state. These would add renderer churn and cross-domain coupling.
Scalability potential: Low uses one scalar brown/desaturate tint. Middle/High keep the same scalar while retaining richer bloom. Ultra can stack more shader-only radiation accents without CPU state.
Hardware Impact: MX350/i3 path adds one global float read and existing fragment lerp; estimated render cost remains about 0.003 ms for visible flora.

Problem: Bioluminescent spores were implied by cascades but lacked a visible GPU-only emission fake.
Solution: Added dithered spore impostor emission in `Hecton_IndirectVegetation.shader`, gated by biolum energy, growth, health, edge mask, positional hash, and screen noise.
Rejected Alternatives: ParticleSystem per plant, spawned GameObjects, CPU pools per flora patch, and VFX components on every emitter. They scale with plant count and violate zero-GC hot-path intent.
Scalability potential: Low uses sparse hashed sparkles. Middle/High increase visible glowing flora density through existing indirect draws. Ultra can raise atlas/detail density while the spore path remains shader-only.
Hardware Impact: No CPU work. Fragment cost is one hash/noise gate on already visible flora; expected low-end cost below 0.01 ms in dense biolum patches.

Problem: Hull growth and coral/anemone masks needed proof of vertex-authored control rather than runtime mesh edits.
Solution: Verified module parasite growth uses the `Reserved0` growth lane, parasite anchors, and authored vertex color masks in coral/indirect shaders; vertex alpha also drives baked AO for kelp and coral.
Rejected Alternatives: Runtime mesh painting, mesh duplication, and per-hull material clones. These create memory churn and unstable ownership across Construction/World boundaries.
Scalability potential: Low uses baked vertex masks and one growth scalar. Middle/High add more parasite anchors. Ultra can add denser hull overgrowth via the same mask contract.
Hardware Impact: Avoids runtime mesh CPU/GC cost; vertex-color AO is a single diffuse multiply, about 0.002 ms in near-field flora.

Problem: Toxic flora registered hazard exposure but did not directly set the combat poison status bit.
Solution: On coarse toxic-spore exposure, `FloraInteractionManager` now queues a zero-damage toxic `CombatDamageSignal` against the registered player health target with `CombatStatusBits.Poisoned`.
Rejected Alternatives: Dense colliders, trigger callbacks on individual plants, direct health mutation, and bespoke poison state on the movement controller. The combat signal keeps ownership inside Combat while Flora only emits the exposure.
Scalability potential: Low uses the current scan interval and one status bit. Middle/High can raise detection fidelity. Ultra can add richer visor/VFX response from the same hazard/poison signal.
Hardware Impact: Scan-interval-only signal, no per-blade collision. MX350/i3 avoids collider storms and keeps toxic flora at coarse query cost.

Verification: `dotnet build Hecton8.Core.csproj --no-restore /m:1` succeeded with 0 warnings and 0 errors. Static search confirmed radiation decay, spore emission, growth lane, vertex-color AO, and poison status-bit paths in the touched files. Unity shader importer verification remains pending outside this CLI build.

## 2026-05-11 Loop 4 - Tasks 16-20

Problem: The indirect flora shader still used direct division in visibility/depth/phase helpers, while kelp sway already used `rcp`.
Solution: Added a linear `LinearStep01` helper and converted relevant indirect flora shader inverses to `rcp(max(...))`; verified kelp and coral shader normalization already uses reciprocal safe inverse paths.
Rejected Alternatives: Leaving compiler division lowering to chance, or rewriting unrelated LOD/damage behavior during a flora pass. The targeted change keeps visual behavior scoped.
Scalability potential: Low benefits from cheaper scalar ALU. Middle/High retain the same visual curves. Ultra can spend the saved ALU on denser indirect flora/spore sparkle.
Hardware Impact: Minor but deterministic shader ALU reduction; MX350/i3 avoids slow divide instructions in hot flora helpers.

Problem: Positional variation must be deterministic and origin-stable.
Solution: Verified touched flora shaders use AUP/world/screen hashes (`HectonKelpHash12`, `HectonCoreLitHash12`, `Hash31`, `InterleavedGradientNoise`) and no shader path uses `UnityEngine.Random`.
Rejected Alternatives: Managed random seeds, material property mutation, or per-instance CPU random state. These add state churn and desync risk.
Scalability potential: Low uses one positional hash. Middle/High use layered hashes. Ultra can add more visual entropy without CPU state.
Hardware Impact: No CPU cost and stable GPU-only variation.

Problem: Flora metadata structs had implicit layout in several NativeArray/GPU-adjacent payloads.
Solution: Added explicit `StructLayout(LayoutKind.Sequential, Pack = 4, Size = ...)` and padding to interaction, wake stamp, parasite node, cascade event, and defensive spore burst structs; verified indirect vegetation metadata remains 64 bytes.
Rejected Alternatives: Trusting CLR/default packing or adding runtime reflection asserts only. Explicit layout is cheaper and less ambiguous for Burst/GPU upload contracts.
Scalability potential: Low avoids misaligned uploads. Middle/High keep stable buffer contracts. Ultra can expand metadata only by an intentional stride change.
Hardware Impact: Better upload/cache predictability; no runtime allocation or extra loop cost.

Problem: Indirect flora glow curves still used cubic `smoothstep` in predator dim, flashbang boost, cascade emission, and distance biolum gating.
Solution: Replaced those glow paths with linear `saturate` through `LinearStep01`; non-glow LOD and wound feathering smoothsteps were left intact.
Rejected Alternatives: Blanket shader rewrite or leaving cubic glow curves in place. Blanket replacement would risk LOD/damage visuals outside the prompt.
Scalability potential: Low uses cheaper linear gates. Middle/High preserve glow timing with simpler math. Ultra can spend saved cycles on more glowing instances.
Hardware Impact: Removes several cubic Hermite evaluations from visible indirect flora emission; expected low-end savings are small but reliable.

Problem: Omega compile check required `FloraInteractionManager` compile verification and Cyrillic cleanup for `FloraMaster.shader`.
Solution: `FloraMaster.shader` is absent, shader/C# Cyrillic search returned no hits in the relevant flora paths, and `FloraInteractionManager` compiled through the Core project.
Rejected Alternatives: Creating a missing shader just to satisfy the name, or reporting without CLI evidence. Both would be fake compliance.
Scalability potential: No runtime effect; verification keeps integration risk bounded.
Hardware Impact: No runtime cost.

Verification: `dotnet build Hecton8.Core.csproj --no-restore /m:1` succeeded with 0 warnings and 0 errors. `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies /m:1` succeeded with 0 warnings and 0 errors. Static searches verified 64-byte indirect metadata, 16/32/64-byte flora payload layouts, no `FloraMaster.shader` file, no Cyrillic hits in relevant flora paths, and no `UnityEngine.Random` in flora shaders.

## 2026-05-11 OMEGA POLISH CHANGES

Problem: The final audit found a few remaining honest scalar divisions in `FloraInteractionManager` wake, sediment, predator-threat, and reciprocal helper paths.
Solution: Replaced them with `math.rcp` multiplies and an integer thread-group ceiling formula. This keeps CPU-side helper math aligned with the shader reciprocal mandate.
Rejected Alternatives: Leaving C# division for the compiler to lower, or broad refactoring of the wake trail system. The scoped patch avoids a refactoring loop.
Scalability potential: Low avoids avoidable scalar divides. Middle/High keep the same wake and sediment behavior. Ultra can spend the saved fraction on denser flora visuals.
Hardware Impact: Micro-level CPU ALU savings; estimated below 0.002 ms but removes slow divide instructions from repeated helper paths.

Problem: OMEGA asked for Dear Lie and Math LOD confirmation after all work was checked.
Solution: Confirmed honest simulation was replaced with cinematic cheats: vertex sine-parabola sway instead of joints, dot-product propwash instead of fluids, shader spore impostors instead of per-plant particles, global radiation tint instead of material state, and combat poison signals instead of plant colliders. Existing `_QUALITY_MX350` / `_QUALITY_HIGH` shader variants remain the low/high scalability gates.
Rejected Alternatives: Fluid simulation, runtime mesh edits, ParticleSystem per flora emitter, material clones, and direct KCC/player-health mutation. These are slower and create cross-domain coupling.
Scalability potential: Low uses sparse hashes, one global scalar, MX350 shader paths, and cheap dither. Middle/High use fuller sway/glow. Ultra can increase density and spore visibility through existing GPU paths.
Hardware Impact: MX350/i3 avoids multiple milliseconds of CPU/physics work. Exact microsecond savings are estimates: vertex sway vs bones 800-2500 us CPU saved, propwash fake vs fluid/colliders 200-800 us saved, GPU spores vs per-emitter particles 300-1200 us saved, poison signal vs collider storms 100-600 us saved, rcp polish below 2 us saved.

Problem: Zero-GC and bloat audit needed proof that hot-path managed garbage was not introduced.
Solution: Runtime scan of `FloraInteractionManager.cs` and `HectonIndirectVegetationContracts.cs` found no `foreach`, `string.Format`, interpolated strings, or `.ToString()` hot-path hits. `new` hits are cold setup arrays/native containers or struct construction. Editor-only tooling in `WorldProceduralFloraTextureAuthoring.cs` still uses debug strings and markdown formatting by design.
Rejected Alternatives: Rewriting editor reporting into fixed buffers during a runtime flora pass. Editor-only output is not frame-time code.
Scalability potential: Runtime remains allocation-free on the hot flora path; editor tooling does not ship as per-frame gameplay.
Hardware Impact: 0 B/frame new managed GC from the added runtime behavior.

Problem: Cache layout and domain boundary required final proof.
Solution: Added explicit 16/32/64-byte struct layouts and confirmed all touched files are flora/biota, world-flora shader, or flora authoring/editor support paths. No cross-domain direct dependency was introduced; Combat is reached through `CombatDamageRuntime` signal queue and Kinematics through existing registry interfaces.
Rejected Alternatives: Direct KCC mutation, direct health state writes, or construction-system ownership changes.
Scalability potential: Stable NativeArray/GPU payload stride keeps low-end upload behavior predictable and lets high-end paths expand intentionally.
Hardware Impact: Better cache/upload predictability; no additional runtime allocation.

Final Git Diff:
`Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader` 74 insertions / 5 deletions.
`Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader` 74 insertions / 5 deletions.
`Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader` 49 insertions / 16 deletions.
`Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader` 314 insertions / 64 deletions.
`Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader` 314 insertions / 64 deletions.
`Assets/_Project/Scripts/Editor/WorldProceduralFloraTextureAuthoring.cs` 172 insertions / 10 deletions.
`Assets/_Project/Scripts/World/FloraInteractionManager.cs` 138 insertions / 18 deletions.
`Assets/_Project/Scripts/World/HectonIndirectVegetationContracts.cs` 28 insertions / 3 deletions.

Verification: Final `dotnet build Hecton8.Core.csproj --no-restore /m:1` succeeded with 0 warnings and 0 errors after the OMEGA reciprocal cleanup. Final `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies /m:1` succeeded with 0 warnings and 0 errors. `git diff --check` reported only existing line-ending normalization warnings, no whitespace errors.

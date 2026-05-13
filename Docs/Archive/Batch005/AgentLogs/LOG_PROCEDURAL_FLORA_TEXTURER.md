# LOG: PROCEDURAL_FLORA_TEXTURER

## 2026-05-13

What was wrong:
- Procedural L-System flora meshes lacked a UV-independent PBR surface path.
- Runtime UV recalculation was the wrong solution: CPU-heavy, allocation-prone, and unnecessary for generated organic surfaces.
- Existing HLOD culling contract conflicts with the prompt: `InstanceCulling.compute` uses transform `m31` as packed radius, not random seed.

What was done:
- Added `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader`.
- Added `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef`.
- Added `Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs`.
- Added mandated state files: `Docs/Tasks/Status_PROCEDURAL_FLORA_TEXTURER.md` and `Docs/AgentLogs/Rationale_PROCEDURAL_FLORA_TEXTURER.md`.
- Shader implements world-space triplanar Albedo/Normal/ORM atlas projection, vertex color R root-tip tint, seed UV breakup, biolum master phase emission, AUP offset subtraction, and `_MATH_LOD_LOW` MatCap fallback.
- Bridge consumes `BiomeChangedSignal` through `SignalBus<BiomeChangedSignal>.GetFrameSnapshot()` and writes `_HectonFloraBiomeTint` globals only on hash changes.

Cinematic cheats used:
- Triplanar projection replaces runtime UV unwrap.
- Vertex Color R root-to-tip gradient fakes SSS and ambient occlusion.
- `_MATH_LOD_LOW` MatCap replaces 9 atlas samples on low tier.
- Seeded UV offsets fake per-instance texture uniqueness without per-instance material data.
- Biolum uses global phase plus masks, not a local simulation clock.

Verification:
- Unity shader compiler logs report `ok=1` for `Hecton_ProceduralBio.shader` ForwardLit and ShadowCaster variants after polish.
- Unity console filter for `Hecton_ProceduralBio` returned 0 errors/warnings after polish.
- Unity MCP `validate_script` on `ProceduralFloraBiomeTintBridge.cs` returned 0 diagnostics.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` executed and failed with 151 external errors in existing Core/reference work. No errors referenced the new procedural flora shader or bridge.

Blocked:
- HLOD seed propagation is blocked by current `m31` radius packing in `InstanceCulling.compute`. Integrator/HLOD owner must define a separate seed channel or packed radius/seed contract.
- Full project compile remains blocked by unrelated missing references/types including `Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `Hecton8.Physics.CCD`, `MacroSwarm`, `BinaryBlittableSafe`, and related dependencies.

Exact microseconds saved:
- 0.000 us claimed as measured savings. No profiler/GPU capture was available.
- Static savings pending verification: low tier cuts 9 triplanar atlas samples to 1 MatCap sample; polish removed variable `pow`, blend divisions, and local shader normalize calls.

Final Git Diff:
- New `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader`
- New `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader.meta`
- New `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef`
- New `Assets/_Project/Scripts/Graphics/Materials/Hecton8.Graphics.Materials.asmdef.meta`
- New `Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs`
- New `Assets/_Project/Scripts/Graphics/Materials/ProceduralFloraBiomeTintBridge.cs.meta`
- New `Docs/Tasks/Status_PROCEDURAL_FLORA_TEXTURER.md`
- New `Docs/AgentLogs/Rationale_PROCEDURAL_FLORA_TEXTURER.md`
- New `Docs/AgentLogs/LOG_PROCEDURAL_FLORA_TEXTURER.md`

Status:
- VERIFIED MASTER GRADE at shader/import/script-validation level.
- PENDING VERIFICATION for runtime frame time, VRAM residency, dependent texture read stalls, and global project compile due external blockers.

## 2026-05-13 AUP Hardening Addendum

What was wrong:
- The shader originally obeyed the batch text by subtracting `_HectonFloatingOffset`, but current project code does not publish that global. Existing floating-origin systems publish `_TotalUniverseOffset` / `_HectonFloatingOriginOffset`.
- Without a fallback, live triplanar projection could still drift during origin shifts even though the mandated symbol existed in the shader.

What was done:
- Patched `ResolveProceduralBioProjectionPosition` in `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader`.
- The shader now subtracts finite nonzero `_HectonFloatingOffset` when an explicit publisher exists.
- When `_HectonFloatingOffset` is absent/zero, the shader falls back to `positionWS + _TotalUniverseOffset.xyz`, matching existing Hecton AUP-stable shader convention.
- Re-extracted the exact active `<AGENT_PROMPT id="PROCEDURAL_FLORA_TEXTURER" ...>` with an attribute-aware CLI regex and ignored neighboring prompt blocks.

Cinematic cheats used:
- Still shader-only. No runtime UV rebuild, no material instance mutation, no new floating-origin C# owner.
- Projection stability is achieved with a cheap branchless lerp/fallback, not a new runtime system.

Verification:
- Unity refresh requested; editor readiness timed out after 60s due the known global compile wall.
- Targeted Unity console filters returned 0 entries for `Hecton_ProceduralBio`, `ProceduralFloraBiomeTintBridge`, and `_TotalUniverseOffset`.
- Shader compiler logs report `ok=1` for ForwardLit vertex/fragment and ShadowCaster vertex/fragment variants after the AUP fallback patch.
- Unity MCP `validate_script` on `ProceduralFloraBiomeTintBridge.cs` returned 0 diagnostics.
- Owned-file hot-path scan found no `pow`, shader `normalize`/`SafeNormalize`, managed `foreach`, string formatting, `.ToString()`, or interpolated bridge strings.

Exact microseconds saved:
- 0.000 us claimed as measured savings. No GPU capture was available.
- Static visual-risk reduction: avoids origin-shift texture sliding in current scenes without adding per-renderer CPU work.

## 2026-05-13 Lifecycle And Mesh Robustness Addendum

What was wrong:
- `ProceduralFloraBiomeTintBridge` could keep a stale `_lastBiomeHash` across disable/re-enable and skip the first same-biome signal after reactivation.
- Generated flora meshes can legally be bad while the offline pipeline is still evolving; zero or non-finite normals would collapse triplanar blend weights and produce black PBR surfaces.

What was done:
- Reset `_lastBiomeHash` to `uint.MaxValue` on bridge enable and disable.
- Added finite/nonzero normal fallback in `Hecton_ProceduralBio.shader` ForwardLit vertex path.
- Added the same normal fallback in the ShadowCaster vertex path before shadow-bias calculation.
- Re-extracted the active `PROCEDURAL_FLORA_TEXTURER` prompt block from `CURRENT_BATCH.md` using an attribute-aware CLI regex.

Cinematic cheats used:
- Bad generated normals are handled as a visual fallback in shader space, not by runtime mesh repair.
- The bridge still writes globals only on lifecycle or signal changes; no polling owner or material mutation was added.

Verification:
- Unity MCP `validate_script` on `ProceduralFloraBiomeTintBridge.cs` returned 0 diagnostics after the lifecycle patch.
- Unity refresh requested; editor readiness timed out after 60s due the known global compile wall.
- Targeted Unity console filters returned 0 entries for `Hecton_ProceduralBio`, `ProceduralFloraBiomeTintBridge`, and `Hecton8.Graphics.Materials`.
- Shader compiler logs report `ok=1` for updated procedural bio shader imports and pass variants.
- Owned-file hot-path audit found no shader `pow`, no shader `normalize`/`SafeNormalize`, no managed `foreach`, no managed string formatting, no `.ToString()`, and no interpolated bridge strings.

Exact microseconds saved:
- 0.000 us claimed as measured savings. No profiler/GPU capture was available.
- Runtime benefit is visual correctness and stale-state prevention, not measured frame-time reduction. Added cost is two lifecycle integer assignments and small vertex-stage normal guards.

## 2026-05-13 Non-Low Shadow Integration Addendum

What was wrong:
- The shader carried main-light shadow variants but the full triplanar PBR path called `GetMainLight()` without shadow coordinates.
- Result: non-low generated flora would not respect authored main-light shadows, hurting contact, terrain grounding, and dense-flora readability.

What was done:
- Added `TransformWorldToShadowCoord(input.positionWS)` and `GetMainLight(shadowCoord)` in the non-low fragment path.
- Applied `distanceAttenuation * shadowAttenuation` to direct diffuse, specular, and fake subsurface terms.
- Kept `_MATH_LOD_LOW` on the cheap `GetMainLight()` path to avoid shadow-coordinate/sample cost on MX350/i3 settings.

Cinematic cheats used:
- Low tier remains unshadowed MatCap plus ambient/biolum.
- High tiers spend the saved low-tier budget on shadowed direct lighting, while rim and emission preserve noir silhouette readability.

Verification:
- Unity refresh requested; editor readiness timed out after 60s due the known global compile wall.
- MCP console retry failed with `no_unity_session`; no full Unity console claim is made for this pass.
- Local import log `Logs/CodexUnityRelaunch4_UI_DIEGETIC_INPUT.log` shows `Hecton_ProceduralBio.shader` imported after the shadow patch with no local shader import error text around the entry.
- `git diff --check` on owned files returned clean.
- Owned shader hot-path scan found no `pow`, no `normalize`, and no `SafeNormalize`.

Exact microseconds saved:
- 0.000 us claimed as measured savings. This is a quality upgrade, not a measured performance win.
- Added cost: one shadow-coordinate transform and main-light shadow attenuation sample in non-low variants only; low tier remains on the cheap path.

## 2026-05-13 Low-Tier Vertex And Registration Addendum

What was wrong:
- `_MATH_LOD_LOW` removed the expensive triplanar texture path, but the vertex stage still computed AUP projection and instance seed data that low tier does not consume.
- `ProceduralFloraBiomeTintBridge` only attempted dispatcher registration in `OnEnable`, leaving a boot-order hole if the dispatcher was not ready yet.

What was done:
- Gated ForwardLit vertex setup by `_MATH_LOD_LOW`: low tier now writes `projectWS = positionWS` and `seed = 0`.
- Non-low tiers still call `ResolveProceduralBioProjectionPosition()` and `ResolveProceduralBioInstanceSeed()`.
- Added `TryRegisterTick()` and called it from both `OnEnable()` and `Start()` in the tint bridge.

Cinematic cheats used:
- Low tier accepts runtime-space emission breakup rather than paying AUP projection/seed setup for a MatCap fake.
- Registration retry is cold lifecycle work only; no Unity `Update()` loop or biome polling was added.

Verification:
- Unity MCP remains unavailable; `validate_script` returned `no_unity_session`.
- Static bridge audit found no managed `foreach`, string formatting, `.ToString()`, or interpolated strings.
- Static shader audit found no `pow`, `normalize`, or `SafeNormalize`.
- `git diff --check` on owned files returned clean.

Exact microseconds saved:
- 0.000 us claimed as measured savings. No GPU/profiler capture was available.
- Static low-tier cost removed: AUP projection finite checks/offset selection and object-matrix seed/fallback hash work in `_MATH_LOD_LOW` vertex variants.

## 2026-05-13 Low-Tier Emission Hash Addendum

What was wrong:
- `_MATH_LOD_LOW` avoided triplanar texture sampling, but still fed runtime `projectWS` into emission and ran a spatial hash in the fragment path.
- That was unnecessary ALU for the cheap MatCap tier and could visibly shift under runtime origin movement.

What was done:
- Added `ResolveProceduralBioEmissionLow()` using `_BiolumMasterPhase` and `_BiolumIntensity` without world-space hash input.
- Low-tier vertex setup now writes `projectWS = 0`.
- Non-low triplanar tiers retain the spatial hash through `ResolveProceduralBioEmission(positionWS, ...)`.

Cinematic cheats used:
- Low tier uses a phase-only biolum pulse. It is cheaper and stable; high tiers retain spatial organic breakup.

Verification:
- Unity MCP console remains unavailable; `read_console` returned `ping not answered`.
- Static shader audit found no `pow`, no `normalize`, and no `SafeNormalize`.
- `git diff --check` on owned files returned clean.

Exact microseconds saved:
- 0.000 us claimed as measured savings. No GPU/profiler capture was available.
- Static low-tier fragment cost removed: one world-space hash path from MatCap emission.

## 2026-05-13 Low-Tier Interpolator Strip Addendum

What was wrong:
- `_MATH_LOD_LOW` had already stopped using triplanar projection and spatial seed data, but the shader still declared `projectWS` and `seed` varyings and wrote dummy values.
- That kept unnecessary vertex-to-fragment payload in the MX350/i3 path.

What was done:
- Guarded `projectWS` and `seed` fields in `Varyings` with `#if !defined(_MATH_LOD_LOW)`.
- Guarded the matching vertex assignments the same way.
- Left non-low triplanar paths untouched: albedo/ORM/normal atlas sampling and spatial biolum breakup still receive projection and seed data.

Cinematic cheats used:
- Low tier is now a stricter MatCap + global pulse fake with no unused triplanar payload.
- Higher tiers keep the expensive visual richness where it buys close-range material quality.

Verification:
- Unity refresh completed after compile request; editor returned idle.
- Unity console filters for `Hecton_ProceduralBio` and `ProceduralFloraBiomeTintBridge` returned 0 entries.
- Unity MCP `validate_script` on `ProceduralFloraBiomeTintBridge.cs` returned 0 diagnostics.
- Global Unity console still reports external `GlobalDataVault.cs` compile errors outside this domain.
- Static shader audit found no `pow`, no `normalize`, and no `SafeNormalize`.
- Static bridge audit found no managed `foreach`, string formatting, `.ToString()`, or interpolated strings.
- Owned shader trailing-whitespace scan returned clean.

Exact microseconds saved:
- 0.000 us claimed as measured savings. No GPU/profiler capture was available.
- Static low-tier cost removed: two unused vertex-to-fragment interpolators and two dummy vertex assignments in `_MATH_LOD_LOW` variants.

## 2026-05-13 Local Variant Bloat Addendum

What was wrong:
- `Hecton_ProceduralBio.shader` declared `_QUALITY_MX350` as a local shader feature.
- No HLSL branch used `_QUALITY_MX350`; low-tier behavior is already driven by `_MATH_LOD_LOW`.

What was done:
- Changed the pragma from `_QUALITY_MX350 _QUALITY_HIGH` to `_QUALITY_HIGH`.
- Kept the high-tier blend-sharpening path intact.
- Left `_MATH_LOD_LOW` as the only cheap-path owner for this shader.

Cinematic cheats used:
- Low tier remains one MatCap sample plus global biolum pulse.
- High tier keeps sharper triplanar blending without dead low-quality local variants.

Verification:
- Unity refresh timed out after 60s waiting for editor readiness due the active global compile wall.
- Unity console filters for `Hecton_ProceduralBio` and `ProceduralFloraBiomeTintBridge` returned 0 entries.
- Unity MCP `validate_script` on `ProceduralFloraBiomeTintBridge.cs` returned 0 diagnostics.
- Global Unity console still reports external `GlobalDataVault.cs` compile errors outside this domain.
- Static shader audit found no `pow`, no `normalize`, and no `SafeNormalize`.
- Static bridge audit found no managed `foreach`, string formatting, `.ToString()`, or interpolated strings.
- Owned file trailing-whitespace scan returned clean.

Exact microseconds saved:
- 0.000 us claimed as measured runtime savings. No shader variant collection stats or GPU capture were available.
- Static bloat removed: one unused local shader feature variant from `Hecton_ProceduralBio.shader`.

## 2026-05-13 Low-Tier Position/View Payload Addendum

What was wrong:
- `_MATH_LOD_LOW` no longer used world-position projection, shadow coordinates, specular half-vector, or rim lighting.
- The low variant still declared and assigned `positionWS` and `viewDirWS`.

What was done:
- Guarded `positionWS` and `viewDirWS` varyings with `#if !defined(_MATH_LOD_LOW)`.
- Guarded their vertex assignments the same way.
- Moved `viewDirWS` reconstruction into the non-low fragment path.
- Kept non-low shadowed PBR behavior intact.

Cinematic cheats used:
- Low tier remains a MatCap plus global biolum fake and spends no payload on unused world/view terms.
- High/Ultra keep world position and view direction for shadows, specular, rim, triplanar projection, and spatial biolum breakup.

Verification:
- Unity refresh initially timed out after 60s, then a follow-up refresh returned idle.
- Unity console filter for `Hecton_ProceduralBio` returned 0 entries after retry.
- Unity console filter for `ProceduralFloraBiomeTintBridge` returned 0 entries.
- Unity MCP `validate_script` on `ProceduralFloraBiomeTintBridge.cs` returned 0 diagnostics.
- Global Unity console currently reports external `HectonUnderwaterVisuals.cs(7393,1)` compile error outside this domain.
- Static shader audit found no `pow`, no `normalize`, and no `SafeNormalize`.
- Static bridge audit found no managed `foreach`, string formatting, `.ToString()`, or interpolated strings.
- `git diff --check` on owned files returned clean.

Exact microseconds saved:
- 0.000 us claimed as measured runtime savings. No GPU/profiler capture was available.
- Static low-tier cost removed: two additional unused vertex-to-fragment payloads and one low-tier vertex view-direction calculation.

## 2026-05-13 Bridge Authoring Metadata Addendum

What was wrong:
- `ProceduralFloraBiomeTintBridge` serialized fields had no inspector tooltips.
- Public `Tick(float deltaTime)` lacked XML documentation.

What was done:
- Added a `Biome Tint` inspector header.
- Added precise tooltips for default tint and tint strength.
- Added XML documentation for the public `Tick` implementation.
- Runtime signal-drain behavior was not changed.

Cinematic cheats used:
- None added; this is authoring hygiene for the shader-global presentation bridge.

Verification:
- Unity MCP `validate_script` on `ProceduralFloraBiomeTintBridge.cs` returned 0 diagnostics.
- Unity console filters for `ProceduralFloraBiomeTintBridge` and `Hecton_ProceduralBio` returned 0 entries.
- Static shader audit found no `pow`, no `normalize`, and no `SafeNormalize`.
- Static bridge audit found no managed `foreach`, string formatting, `.ToString()`, or interpolated strings.
- `git diff --check` on owned files returned clean.

Exact microseconds saved:
- 0.000 us runtime change. This pass improves scene authoring clarity, not frame time.

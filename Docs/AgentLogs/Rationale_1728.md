# Rationale 1728 - Particulate Flipbook Baker

## Decision 0 - Domain Boundary

Problem: User directive names `Docs/Actual Domains of Project.txt`, but no such file exists under `C:\hades`.
Solution: Use the extracted XML role `SILT_PARTICLE_FLIPBOOK_AND_SNOW_MASK_BAKER` as the operative boundary and keep edits inside the prompt-authorized editor baker/VFX audit surface.
Rejected Alternatives: Editing outside the prompt to infer a broader graphics domain is rejected because multi-agent collision risk is high and the domain file is absent.
Scalability potential: Low/Middle/High/Ultra all benefit from offline baked texture variants without runtime truth-route changes.
Hardware Impact: No runtime cost. Avoids accidental hot-path dependency churn on i3/MX350.

## Decision 1 - Mandate Set

Problem: The prompt spans editor tooling, particle VFX, texture compression, runtime purity, and GPU presentation.
Solution: Apply visual-fake-first, zero-GC, performance/VRAM, VFX fluid particle, GPU sovereignty, async upload/import, editor authoring bridge, and ARM64 layout mandates.
Rejected Alternatives: Reading only VFX docs is rejected because import settings and offline authoring rules are the actual acceptance gate.
Scalability potential: Compact uses smaller atlases and the same shader route; High/Ultra can bake denser 64-frame/4K flipbooks.
Hardware Impact: Offline bake buys runtime CPU savings by moving procedural noise and normal derivation out of gameplay.

## Decision 2 - Existing 1718 Baker Reuse Boundary

Problem: `ParticleFlipbookBaker1718.cs` already implements silt and marine snow atlas baking, but the 1728 assignment requires `ParticulateFlipbookBaker.cs` and cavitation bubble flipbooks as a named artifact.
Solution: Add a new scoped editor baker for 1728 and reuse the shared `ProceduralTextureBaker` utility methods for path validation, import settings, atomic writes, and asset finalization.
Rejected Alternatives: Mutating 1718 directly would collide with another agent's status/test surface and still would not create the requested 1728 file. Runtime shader rewrites are also rejected because existing shader atlas bindings already consume baked mask/normal textures.
Scalability potential: Low uses smaller atlas/frame count; Middle/High/Ultra use progressively larger static flipbooks and denser normal/emissive/flow information.
Hardware Impact: i3/MX350 avoids runtime CPU particle texture/noise work; GPU keeps two texture samples and one billboard path.

## Decision 3 - Prefab Particle Audit Scope

Problem: The prompt names `Assets/Prefabs/VFX`, but that folder is absent. Existing ParticleSystem hits are in world-support/construction prefabs, not a direct marine snow prefab folder.
Solution: Document the ParticleSystem locations and do not edit those prefabs in this task. The 1728 deliverable is the offline texture replacement path and proof report.
Rejected Alternatives: Bulk prefab removal is rejected because construction/world-support effects have different owners and may be leak/hazard consequence effects, not ambient silt.
Scalability potential: The baked atlas can replace ambient silt/snow/bubble visual layers without changing owned hazard prefabs.
Hardware Impact: Replacement candidates could save 20-80 us/frame where Shuriken ambient emitters are swapped for instanced atlas quads; exact value is PENDING PROFILER.

## Decision 4 - Texture Deconstruction Result

Problem: Runtime particle texture generation would violate zero-GC and texture import law.
Solution: Static sweep found no `new Texture2D`/`SetPixels` in target VFX runtime. Existing `LutArrayResolver` texture creation is a cold water extinction loader outside particulate scope.
Rejected Alternatives: Removing the LUT path is rejected because it is not particle noise generation and could break water extinction authority.
Scalability potential: Offline particulate atlases scale with bake quality; runtime consumes static imported textures at all tiers.
Hardware Impact: No current target call removed. Prevents future CPU texture churn in the particulate pipeline.

## Decision 5 - Registry And DataVault Audit

Problem: Hot registry polling and unsafe DataVault reads can stall or invalidate runtime VFX state.
Solution: `HectonMarineSnowRenderer` uses hot-swap cached services, checks `IsCompactionFenceActive` before resolving handles, and backs off when allocation is locked. No `GlobalRegistry.Get<` hot polling was found.
Rejected Alternatives: Refactoring cached GlobalRegistry property reads in service refresh is rejected; it is cold/hot-swap binding, not per-frame polling.
Scalability potential: Cached owner snapshots keep the renderer route stable while atlas quality changes stay offline.
Hardware Impact: 0 us changed; risk stays low because no new runtime dependency route is introduced.

## Decision 6 - Dedicated 1728 Editor Baker

Problem: The project already has a 1718 flipbook baker, but the prompt requires `ParticulateFlipbookBaker.cs`, cavitation bubbles, and a 1728 proof report.
Solution: Add `Assets/_Project/Editor/Bakers/ParticulateFlipbookBaker.cs` with `EditorWindow`/`MenuItem` entry points, three default profiles, and an offline `IJobParallelFor` pixel baker.
Rejected Alternatives: Extending 1718 would hide the new ownership boundary and risk multi-agent file collision. Runtime VFX edits are rejected because existing shaders already consume mask and normal atlases.
Scalability potential: Low bakes 4x4 1024 static variants; Middle/High scale frame size and grid; Ultra bakes 8x8 4096 with denser fBM/Worley detail.
Hardware Impact: i3/MX350 pays no runtime noise/normal derivation. CPU savings remain profile-dependent until adoption replaces live emitters; candidate ambient replacement is 20-80 us/frame.

## Decision 7 - Report Payload And Proof Route

SUPERSEDED BY DECISION 14.
Problem: The earlier CTO protocol required disk proof, not a chat claim, and the report had to survive context loss.
Solution: The earlier route wrote `Docs/Reports/PARTICLE_FLIPBOOK_BAKER_REPORT_1728.json`. The newer source-only directive removed that artifact and its `.meta`; source and tests now carry the proof.
Rejected Alternatives: Keeping stale JSON/LOG files after the newer directive is rejected.
Scalability potential: Atlas/frame configuration is now auditable in source and rationale without extra bake-time report I/O.
Hardware Impact: Runtime 0 us. Removed editor report write noise.

## Decision 8 - Packed Mask Channels

Problem: Separate opacity, emissive, flow, and AO masks would multiply texture fetches and disk/import surface.
Solution: Pack opacity into R, biolum/cavitation glint into G, flow distortion into B, and AO into A in one mask texture; keep normals in a BC5 normal atlas.
Rejected Alternatives: Four separate grayscale textures are rejected; they cost extra importer state, material slots, and fragment samples for no gameplay truth benefit.
Scalability potential: Low uses the same channel contract with smaller atlases; Ultra spends saved fetch bandwidth on denser visual masks.
Hardware Impact: On MX350 this preserves a two-texture sprite contract instead of five texture reads per particulate layer.

## Decision 9 - Periodic Noise And Finite Normals

Problem: A 64-frame flipbook must close without frame-63 to frame-0 pop, and normal gradients cannot produce NaN on uniform density.
Solution: Time is mapped to a unit circle through cos/sin in `PeriodicSimplex`; gradients use finite guards, positive Z clamp, and `math.normalizesafe`.
Rejected Alternatives: Linear animated noise plus blend frame is rejected because it costs authoring complexity and can still pop in derivatives. Zero-Z normals are rejected because uniform fields would collapse lighting.
Scalability potential: The same periodic function scales across 16/25/36/49/64 frame bakes; Ultra simply samples more frames.
Hardware Impact: Runtime does not blend or simulate; the GPU reads a stable atlas frame.

## Decision 10 - VRAM Budget Correction

Problem: The prompt asks to prove five 4096 mask+normal pairs under 130 MB. BC7 and BC5 are both 8 bpp. One 4096 texture is 16 MB without mips and about 21.33 MB with mips.
Solution: Record the factual constraint: three 4096 mask+normal pairs are about 128 MB with mips; five pairs are about 213.33 MB with mips and therefore exceed 130 MB. The 1728 baker creates three required pairs at full quality or lower-quality static variants.
Rejected Alternatives: Faking the requested proof is rejected because it would be numerically false and would break VRAM planning.
Scalability potential: Low/Middle variants reduce atlas size; High/Ultra can use three full 4K pairs within the stated 130 MB envelope.
Hardware Impact: i3/MX350 avoids an impossible 5-pair 4K allocation. The factual cap prevents roughly 85 MB of excess compressed texture residency.

## Decision 11 - 4096 Normal Dry Run

Problem: A uniform density region in a 4096 atlas could produce zero gradient, and bad normalization would poison the normal map with NaN.
Solution: For each pixel, density is sampled at center, +/-X, and +/-Y. If the field is uniform, dx=0 and dy=0. The Z component is `max(normalZ, 0.18)`, then `math.normalizesafe(..., float3(0,0,1))` guarantees a finite tangent-space normal. Padding writes neutral normal `(128,128,255,0)`.
Rejected Alternatives: Raw `normalize(float3(dx,dy,1))` and unguarded noise values are rejected; they do not prove finite output when noise or derivatives are invalid.
Scalability potential: Low through Ultra use identical finite math; higher tiers only increase sample count.
Hardware Impact: No runtime CPU impact. Prevents corrupted normal assets that would create spotlight artifacts on MX350 and stronger GPUs.

## Decision 12 - Compaction Race Audit

Problem: The prompt asks how the code behaves if a DataVault sector relocation begins one instruction before a particle manager reads water flow.
Solution: 1728 does not add any runtime DataVault access. Existing VFX readers checked in the audit guard `vault == null || vault.IsCompactionFenceActive` before resolving/reading and back off when allocation is locked. Correct behavior is to keep previous cached render state for that frame, not chase a stale pointer.
Rejected Alternatives: Adding `GlobalDataVault.TryGetLatestCreated()` or direct vault reads to the baker/material path is rejected; this task is offline authoring, not runtime state ownership.
Scalability potential: All quality tiers use static imported textures, so compaction fence pressure does not increase with atlas fidelity.
Hardware Impact: 0 us changed. Avoids hidden job completion or stale native pointer read on low-end silicon.

## Decision 13 - Build Gate Enforcement

Problem: Task 20 allows `dotnet build` only when CPU is below 50 percent and no other compiler/dotnet process is active.
Solution: First sample blocked the build: CPU 91.79 percent, `csc.exe` 0, `dotnet` 7. Second sample cleared the gate: CPU 21.98 percent, `csc.exe` 0, `dotnet` 0. Ran `dotnet build Assembly-CSharp-Editor.csproj --no-restore --nologo`.
Rejected Alternatives: Running during the first high-load sample is rejected. Editing `Assets/Editor/HectonMcpBridgeAutoConnect1428.cs` is also rejected because the failure is an unrelated MCPForUnity editor dependency owned outside 1728.
Scalability potential: No runtime quality impact. 1728 syntax was not named in compiler errors; full project build remains blocked by external dependency.
Hardware Impact: Build attempt cost editor/host time only. No game-frame impact.

Build result: failed with `CS0234` at `Assets/Editor/HectonMcpBridgeAutoConnect1428.cs:4-6` for missing `MCPForUnity.Editor.*`. No errors were reported for `Assets/_Project/Editor/Bakers/ParticulateFlipbookBaker.cs`.

## Decision 14 - Source-Only Proof And Neutral Volume Assets

Problem: The newer directive rejects JSON/LOG proof artifacts, and the marine snow renderer now depends on authored neutral `Texture3D` fields. Without generated neutral volume assets, empty serialized fields can disable the renderer before particulate atlases are even visible.
Solution: Remove obsolete 1728 report/log artifacts from the proof path, keep the source/tests as the evidence, and extend `ParticulateFlipbookBaker.cs` to bake two 1x1x1 neutral `Texture3D` assets beside the particulate atlases. `HectonMarineSnowRenderer` now has an editor-cold AssetDatabase fallback that fills empty serialized neutral volume fields from those authored assets.
Rejected Alternatives: Runtime `new Texture3D` fallback in `HectonMarineSnowRenderer` is rejected because it reintroduces the runtime texture generation pattern this domain is eliminating. Keeping JSON report artifacts is rejected because the current directive explicitly treats source as the proof.
Scalability potential: Low/Middle/High/Ultra all consume the same zero-cost neutral fallback when cave SDF or abyssal flow data is not authored; higher tiers still spend quality on baked mask/normal flipbooks, not CPU particles.
Hardware Impact: Prevents a renderer-disable path on low-end devices without adding steady-state allocation. The fallback load is editor-only and cold; player runtime remains serialized-asset driven.

## Decision 15 - CSV Stub Flattening

Problem: `HectonMarineSnowRenderer` used editor implementation methods plus non-editor empty stubs for CSV profile refresh. That is C# preprocessor-safe, but Unity MCP source validation does not preprocess and flagged duplicate method signatures as a corrupted edit.
Solution: Keep the editor implementations single-owner, remove the non-editor stub duplicates, and guard call sites with `#if UNITY_EDITOR`. Added a source-contract test requiring exactly one signature for each CSV refresh method.
Rejected Alternatives: Ignoring the validator finding is rejected because it leaves an integrator-facing false compile alarm. Moving CSV reading into a new helper class is rejected because this is an existing first-party renderer concern and does not need a new assembly surface.
Scalability potential: Low/Middle/High/Ultra retain identical player runtime behavior; CSV profile polling remains editor-only authoring support.
Hardware Impact: Player builds avoid the CSV reader calls entirely. Editor validation now returns 0 errors/warnings for the renderer.

## Decision 16 - Existing Meta Orphan Scan

Problem: A first Git-index-based `.meta` scan reported old tracked `.meta` paths whose paired assets were already absent.
Solution: Re-run the scan against physically existing `.meta` files via `rg --files -g '*.meta'`; result is `NO_EXISTING_ORPHAN_META`. The reported old paths are already deleted in the working tree and are unrelated to the 1728 baker.
Rejected Alternatives: Recreating deleted assets or manually staging unrelated cleanup is rejected because it would cross the 1728 domain boundary and collide with other agents.
Scalability potential: AssetDatabase hygiene stays deterministic for Low/Middle/High/Ultra texture variants.
Hardware Impact: 0 runtime us. Prevents false hygiene alarms without touching gameplay systems.

## Decision 17 - 1728 Frame Count Authority

Problem: The shared 1718 baker supports 4x4 low-quality flipbooks, but the 1728 assignment requires 64-frame cycles for silt, marine snow, and cavitation.
Solution: Force `TryResolveParticleBakeAssetPaths1718` through `forceRequiredFrameGrid: true` for all three 1728 profiles. `GlobalQualityWeight` still scales atlas size and profile detail, not the 8x8 frame count.
Rejected Alternatives: Letting low quality reduce to 16 frames is rejected because it violates the authored loop cadence and makes spotlight-lit flakes visibly step under slow motion.
Scalability potential: Low keeps 64 smaller frames; Middle/High/Ultra increase texture size/detail while preserving cadence.
Hardware Impact: 0 runtime CPU. Low-tier VRAM remains controlled by atlas size instead of frame count collapse.

# Rationale - PROCEDURAL_BIOME_BAKER_SHALLOWS

Status: PENDING VERIFICATION

## Decision 1 - Use Existing Bio-Forge Editor Owner

Problem: The prompt requires Safe Shallows L-system `BioRuleData` assets and generated LOD prefabs. Raw `.asset`/`.prefab` YAML edits would risk GUID/fileID corruption and violate the project YAML guard.

Solution: Use the existing editor-only `Hecton8.Editor.ProceduralGen` pipeline and add only the missing Safe Shallows automation surface if needed. This keeps runtime selection/scatter under `WorldProceduralScatterDirector` and keeps generation out of play mode.

Rejected Alternatives: Raw YAML asset creation was rejected because field names/GUID references are fragile. A new runtime scatter stack was rejected because `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` explicitly forbids parallel category scatter systems. Placeholder markdown/report output was rejected because `PROCEDURAL_ASSET_PIPELINE.md` forbids reports instead of production assets.

Scalability potential: Low uses static LOD prefabs, shared material, no per-plant CPU animation. Middle adds shader-driven sway/flow. High extends LOD residency and density. Ultra buys visual overkill through richer shader detail and emissions, still without per-flora Rigidbody or transform loops.

Hardware Impact: Expected runtime impact on i3/MX350 is mesh renderer + LODGroup cost only; procedural generation cost is editor-only. Approximate hot-path allocation saved versus runtime generation: all generator allocations, expected >100 us spike avoidance per streamed placement batch, exact profiler proof absent.

## Decision 2 - Visual Fake First For Kelp Motion And Coral Mass

Problem: Flora could be interpreted as physically simulated plants/coral, but the prompt only requires authored assets.

Solution: Bake static meshes with vertex color R height gradients for shader sway/biolum/motion masks. Coral bulk is SDF capsule blending. Kelp is thin upward L-system strips/branches, later animated by shader if material supports it.

Rejected Alternatives: Per-blade physics, Rigidbody collision, and runtime branch growth were rejected as waste. They do not add gameplay truth for scatter flora.

Scalability potential: Low disables expensive deformation and relies on LOD/impostor silhouettes. Middle uses one sway term. High/Ultra can add harmonics/emission masks in shader with unchanged CPU cost.

Hardware Impact: Avoids transform/physics loops for 200 generated prefabs; estimated savings versus 200 active per-object animation scripts is 200-600 us/frame on low-end silicon, pending profiler proof.

## Decision 3 - Deterministic Batch Seeds

Problem: Batch output must be reproducible while still producing unique variants.

Solution: Use deterministic integer seeds with fixed salts per family and variation index. This matches the slot-machine law: no wall-clock, no `UnityEngine.Random`, no object instance IDs as authority.

Rejected Alternatives: Unity random, time-based names, and manual duplicate-copy variants were rejected because they are not replayable and produce unstable assets.

Scalability potential: Low can keep fewer variants in scatter rules; High/Ultra can keep longer LOD residency and richer visible variety from the same deterministic set.

Hardware Impact: Deterministic offline generation has no runtime CPU impact. It prevents cache churn from runtime procedural variation and saves unpredictable streaming stalls; exact microsecond proof absent.

## Decision 4 - Isolated Unity Bake For ProceduralGen Only

Problem: The live main Unity project is locked by an open editor instance and currently fails compilation in unrelated Core/Database and shader domains, preventing normal menu execution through MCP or batchmode.

Solution: Stage a minimal Unity project under `.codexbuild/ShallowsBakeProject_20260514_030549` containing only the Bio-Forge editor assembly, copied script/shader `.meta` GUIDs, and the package manifest. Bake there, validate there, then copy only Shallows outputs back to the main project with their generated `.meta` files intact.

Rejected Alternatives: Killing the open Unity editor was rejected because other agents may have unsaved state. Editing Core/Database compile failures was rejected as cross-domain work. Raw prefab/mesh YAML authoring was rejected because Unity mesh serialization is fragile and would undercut the asset pipeline proof.

Scalability potential: Low devices receive static LOD prefabs with the same material contract. Middle keeps full authored variety. High/Ultra can spend density and shader detail budget because the bake cost is offline.

Hardware Impact: Keeps runtime procedural allocation at zero and prevents streamed generation stalls. Estimated low-end benefit remains the avoided 100+ us/batch procedural spike plus no active generation memory churn.

## Decision 5 - Final-Span Vertex Color Normalization

Problem: Initial validation found weak vertex color R gradients on narrow kelp and several rocks because the bake normalized against broad SDF bounds rather than the emitted mesh span.

Solution: Normalize vertex color R in `CreateUnityMesh` from final valid vertex Y min/max after triangle filtering and LOD decimation. This guarantees 0.0 root/low side to 1.0 tip/high side on the persisted mesh data.

Rejected Alternatives: Loosening validation was rejected because the shader wind/emission mask depends on the data being trustworthy. Per-family hand-authored color curves were rejected because they add manual drift and no runtime benefit.

Scalability potential: Low uses the R channel as a cheap shader mask. Middle can use single-term sway. High/Ultra can stack richer shader deformation and glow without CPU scripts.

Hardware Impact: Normalization is editor-only managed work. Runtime savings are indirect: no per-instance height probing or procedural mask generation, estimated 20-60 us/frame avoided at 200 visible assets on low-end silicon.

## Decision 6 - Parallel Decimation Safety Contract

Problem: Burst safety reported that the LOD decimation job writes three output vertices per triangle index, which violates default `IJobParallelFor` write-index restrictions despite non-overlapping ranges.

Solution: Add `NativeDisableParallelForRestriction` to the editor-only output buffer, while preserving deterministic triangle-index partitioning. Each job index writes exactly `dst`, `dst+1`, and `dst+2`; no overlap exists.

Rejected Alternatives: Switching the decimator to a single-threaded `IJob` was rejected because it wastes editor bake time for 600 meshes. Disabling Burst was rejected because the hot SDF path benefits from job compilation.

Scalability potential: Low hardware can still bake the asset library in finite time. High/Ultra authoring machines can scale to richer SDF resolutions or more variants without touching runtime.

Hardware Impact: Editor-only throughput fix. Runtime impact is 0 us/frame; expected bake-time stability gain is removal of Burst safety exception overhead and failed worker retries.

## Decision 7 - GUID-Stable Re-Bake And Rock Collider Alignment

Problem: The second-pass audit found two production defects after the first accepted bake: the texture root used `Textures` while the project path is `TEXTURES`, and the rock `MeshCollider` lived on the prefab root while visible LOD children were horizontally offset by `ResolveGeometryOffset`. That created a possible collision/render drift on rocks.

Solution: Correct the texture root casing to `Assets/_Project/Art/TEXTURES/WorldProceduralFlora`; move rock colliders onto `Collision_LOD2` child objects; set the collider child's local position to the same `geometryOffset` as LOD0/1/2 renderers; strengthen validation to check MatCap binding, one renderer per LOD, non-null meshes, zero shadow casting, convex collider count, and collider/render offset alignment. The bake now ensures output folders instead of deleting them, so deterministic re-bakes update existing assets and preserve GUIDs.

Rejected Alternatives: Root-space colliders were rejected because visual collision mismatch is a content bug. Deleting generated folders every bake was rejected because it risks GUID churn once scatter or placement tables reference generated prefabs. Manual YAML patching of 50 rock prefabs was rejected after isolated Unity batchmode recovered and completed a real re-bake.

Scalability potential: Low keeps the same 200 static prefabs, shared atlas, no runtime scripts, and aligned convex rock collision only where gameplay needs it. Middle can add shader sway via vertex color R without CPU work. High can increase scatter residency and density using the same GUID-stable library. Ultra can spend saved CPU/GPU budget on shader overkill and biome density, not simulation.

Hardware Impact: Runtime remains 0 us/frame for generation and 0 bytes procedural allocation. On i3/MX350, the concrete gain is avoided collision broadphase on 150 flora prefabs and avoided collider/render mismatch that would force later gameplay-side correction casts. Estimated saved runtime remains >100 us per streamed generation batch avoided, 200-600 us/frame versus per-object animation scripts, and 20-60 us/frame versus runtime mask reconstruction.

## Decision 8 - Batchmode Log Hygiene And Renderer Flag Lockdown

Problem: Re-reading the core XML showed it still requires `PENDING VERIFICATION`, while the status file had been promoted to polish wording. Separately, the isolated bake log showed one generated-prefab `Debug.Log` produced full stack traces for every generated asset, and validation did not explicitly reject extra renderers or renderer hot-path flag drift.

Solution: Restore disk status to `PENDING VERIFICATION` to match the extracted XML. Gate cancelable progress bars behind `!Application.isBatchMode`, so headless bakes skip UI/progress string work. Emit generated-prefab summaries with `LogOption.NoStacktrace`. Extend validation to require exactly three renderers per prefab and reject receive shadows, motion vectors, probe usage, dynamic occlusion, and cast shadows.

Rejected Alternatives: Leaving `VERIFIED MASTER GRADE` in the status header was rejected because the agent-specific XML is the active authority for handoff status. Suppressing all logs was rejected because bake operators still need compact variant progress. Full stack traces on successful generated-prefab logs were rejected because they add noise and editor I/O without diagnostic value.

Scalability potential: Low-end authoring machines get less batchmode log I/O and no progress UI work. Middle and high-end machines can run repeated deterministic re-bakes with cleaner logs. Runtime assets are unchanged: static prefabs, three renderers, shared material, no runtime scripts.

Hardware Impact: Runtime impact remains 0 us/frame because this is editor-only. Authoring impact on low-end i3/MX350 is reduced log formatting and file I/O during 200-prefab bakes; exact microseconds were not profiled, but the previous 681 KB stacktrace-heavy log is now avoided on future bakes.

Verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).` Main prefab scan found `Prefabs=200`, `MeshRenderers=600`, no bad shadow/probe/dynamic occlusion flags, `MeshColliders=50`, and `CollisionChildren=50`.

## Decision 9 - Stale Mesh Payload And Atlas Importer Validation

Problem: Preserving generated folders prevents GUID churn, but it also means a future count change or interrupted bake can leave stale mesh assets beside the correct prefabs. The previous validator proved prefab counts and renderer contracts but did not count generated mesh assets or enforce atlas importer settings after import.

Solution: Add mesh-library validation per family: expected mesh count is `prefabCount * 3`, and each family must have exactly one `_LOD0.asset`, `_LOD1.asset`, and `_LOD2.asset` per prefab. Add shared material validation for shader name, instancing, GI flags, and atlas bindings. Add atlas importer validation for wrap, mipmaps, readability, compression, sRGB policy, normal-map type, max size, and Standalone BC5/BC7 settings. Also gate `EditorUtility.ClearProgressBar()` behind `!Application.isBatchMode`.

Rejected Alternatives: Reintroducing folder deletion was rejected because it risks GUID churn. Relying only on prefab counts was rejected because stale mesh assets can survive even when prefabs are correct. Leaving importer validation to Unity defaults was rejected because atlas import drift can silently damage batching, memory, or normal decoding.

Scalability potential: Low-tier devices keep compressed shared atlas textures and exact LOD mesh sets. Middle and high tiers can increase scatter density without per-family material or texture churn. Ultra can spend saved CPU/GPU budget on shader overkill because asset contracts stay deterministic and compact.

Hardware Impact: Runtime remains 0 us/frame for generation. Validation protects MX350-class VRAM by enforcing compressed non-readable atlas textures and exact generated mesh payload size. Future batchmode bakes also skip progress-bar cleanup work; exact editor microseconds were not profiled.

Verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).` Mesh scan found TubeCoral `LOD0=50, LOD1=50, LOD2=50`, Kelp `LOD0=100, LOD1=100, LOD2=100`, PorousRock `LOD0=50, LOD1=50, LOD2=50`, all with `Other=0`. Prefab scan found `Prefabs=200`, `MaterialRefs=600`, `MeshColliders=50`, and `CollisionChildren=50`.

## Decision 10 - Exact Prefab-To-Mesh Reference Contract

Problem: The stale mesh payload pass proved mesh counts, but a prefab could still reference an older mesh asset with the right LOD count, or a mesh from the wrong family, if a re-bake or manual move went wrong.

Solution: Extend prefab validation to derive the prefab stem and require each LOD renderer mesh to resolve to the exact deterministic asset path: `MeshRoot/{Family}/{Stem}_LOD{i}.asset`. This ties prefab references, generated mesh assets, and deterministic naming into one contract.

Rejected Alternatives: Counting material references and mesh asset counts alone was rejected because those prove quantity, not identity. Hashing entire prefab YAML was rejected because Unity serialization order can churn and would create brittle validation noise. Re-baking again was rejected because no generated content changed; the defect was in validator coverage.

Scalability potential: Low/Middle/High/Ultra tiers all benefit from stable asset identity. Scatter tables can reference prefab GUIDs while the validator guarantees each prefab carries the intended three LOD meshes and no cross-family payload drift.

Hardware Impact: Runtime remains 0 us/frame. The impact is risk reduction: no accidental high-detail or wrong-family mesh can slip into a low-tier prefab without validation failure. That protects MX350-class geometry budgets, especially for LOD2 impostor use.

Verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).` A GUID text scan checked all 200 prefabs against their expected three mesh `.meta` GUIDs and found `BadReferenceCount=0`.

## OMEGA POLISH CHANGES

Problem: The polish mandate required a final anti-bloat pass after the checklist reached 100%, including bitmask checks, reciprocal/sqrt audit, GC audit, and a build probe.

Solution: Converted the Burst SDF mode selector from numeric mode values to explicit bit flags: `ModeFlagRock`, `ModeFlagRibbon`, and `ModeFlagPorous`. The generator now passes a precomputed bitmask into `BioForgeSdfBuildJob`; the job uses `(ModeFlags & MASK) != 0` checks. Existing `math.rsqrt` and `math.rcp` patterns were retained; no unconditional `math.sqrt()` or `math.normalize()` were found in the touched ProceduralGen files. The only `$"..."` and `.ToString()` findings are in editor-only assembly code, not runtime.

Rejected Alternatives: Rewriting the entire marching tetra extractor was rejected because it is editor-only and already deterministic. Replacing SDF noise with a LUT was rejected for this bake because the prompt explicitly requires L-system/SDF asset generation, while runtime receives only static prefabs.

Cinematic Cheats Used: Static offline SDF meshes instead of runtime growth; shader-readable vertex color R masks instead of runtime height probes; shared triplanar atlas instead of per-asset textures; LOD2 impostor meshes with shadows disabled; flora collision deleted entirely; rocks use convex LOD2 mesh collider only.

Scalability potential: Low uses static prefabs, LODGroup, shared material, no scripts. Middle uses vertex color R for cheap shader sway. High uses denser scatter and longer LOD residency. Ultra spends saved CPU on shader overkill and additional biome density, not simulation.

Hardware Impact: Runtime procedural generation remains 0 us/frame and 0 bytes allocation by design. Estimated low-end i3/MX350 gains: 100+ us/batch streamed-generation spikes removed, 200-600 us/frame avoided versus per-object animation scripts, 20-60 us/frame avoided versus runtime mask reconstruction, flora collider broadphase cost removed for 150 prefabs.

Build Probe: Direct Roslyn compile of the isolated `Hecton8.Editor.ProceduralGen` assembly returned `CscExit=0`. Isolated Unity 6000.4.1f1 batchmode re-bake after the collider/path/validator pass logged `Validation passed. Coral=50, Kelp=100, Rocks=50, Total=200, LOD2<150`. Main `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).`

Final Git Diff: source diff stat is `BioForgeGenerator.cs | 49 +++++++--`, `BioForgeJobs.cs | 14 ++-`, `ShallowsBioForgeBatchBaker.cs | 110 +++++++++++++++------`. Generated payload scan: `Rules=3`, `MeshAssets=600`, `Prefabs=200`, `AtlasPng=4`, `CollisionChildren=50`, `MeshColliders=50`, `FloraColliders=0`, `BadShadowLines=0`, `MaterialRefs=600`, plus `MAT_ProceduralBio_Shallows.mat`.

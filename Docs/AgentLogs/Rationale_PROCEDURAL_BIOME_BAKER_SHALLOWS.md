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

## Decision 11 - BioRuleData Contract Lockdown

Problem: The Safe Shallows validator proved generated prefab counts, mesh counts, atlas importers, material settings, and exact prefab-to-mesh references. It still did not prove that the source `BioRuleData` assets retained the intended authoring contract. A future manual edit could change the TubeCoral axiom, Kelp ribbon profile, PorousRock pore settings, LOD budgets, or output folders while leaving old generated payloads apparently valid.

Solution: Add rule-asset validation to `ShallowsBioForgeBatchBaker`. The validator now checks required generated folders, then validates all three Safe Shallows rule assets against literal expected data: asset prefix, material, axiom, one exact `F` replacement rule, SDF profile, iterations, max branches, SDF resolution, LOD budgets, branch shape values, ribbon scales, porous rock radius/noise/pore values, and mesh/prefab output folders. Non-rock rules now carry explicit rock-default expectations instead of helper fallback defaults, so the contract is literal.

Rejected Alternatives: Re-baking the payload was rejected because no generated content changed. Raw YAML mutation was rejected under the prefab/asset safety rule. Relying on mesh/prefab counts alone was rejected because counts prove output quantity, not the authoring source of future deterministic bakes.

Scalability potential: Low keeps static LOD prefabs, exact LOD2 budgets, shared atlas, and no runtime scripts. Middle/High keep deterministic variety and shader-readable masks. Ultra can spend saved CPU on denser flora and richer shader response because the source rules cannot silently drift into bloated SDF settings.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes allocation. The gain is prevention: MX350-class builds cannot silently inherit higher LOD budgets, wrong material paths, or non-porous rock settings from edited rule assets. Exact frame microseconds remain not profiled because this is editor validation.

Verification: Unity Bee response-file Roslyn compile for `Hecton8.Editor.ProceduralGen` exited 0. Literal rule asset scan passed: `RuleAssetScan=PASS Rules=3 ContractFields=25`. `git diff --check` exited 0 with only the existing LF-to-CRLF warning. Full `dotnet build Hecton8.Core.csproj` is currently blocked outside this domain by `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs` errors for missing `BufferReader.CanConsumeCollectionItems` and `ReadCustomArray` overloads.

## Decision 12 - Shared Material Scalar And Color Contract

Problem: Shared material validation proved shader identity, atlas bindings, instancing, and GI flags, but not the actual Safe Shallows color/scalar contract. A material drift could flatten bioluminescence, change culling, or enable the high-quality keyword while the generated prefab and mesh contracts still pass.

Solution: Add material property validation to `ShallowsBioForgeBatchBaker` for `_BaseColor`, `_RootTint`, `_TipTint`, `_EmissionColor`, every authored Shallows shader scalar, `_Cull=0`, and `_QUALITY_HIGH` disabled. The validator now treats the material as part of the baked asset contract, not a loose dependency.

Rejected Alternatives: Re-baking the asset payload was rejected because no mesh or prefab content changed. Relying on a one-off YAML scan was rejected because it does not protect future bakes. Runtime material correction was rejected because this is an editor-authored asset library and runtime mutation would add hidden state and cost.

Scalability potential: Low keeps cheap triplanar/MatCap tuning, no high-tier keyword, double-sided flora, and controlled emission values. Middle and High can raise density with the same material contract. Ultra can intentionally change material overkill values only by updating the bake contract and validator together.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes allocation. The gain is preventing silent shader keyword or scalar drift from increasing GPU cost or weakening the visual mask on i3/MX350-class hardware. Exact frame microseconds are not profiled because this is editor validation.

Verification: Unity Bee response-file Roslyn compile for `Hecton8.Editor.ProceduralGen` exited 0. Material text scan passed `MaterialContractScan=PASS Fields=26`. `git diff --check` exited 0 with only the repo LF-to-CRLF warning. Full `dotnet build Hecton8.Core.csproj --no-restore` is currently blocked outside this domain by missing generated `Temp/obj/Hecton8.Core/.NETStandard,Version=v2.1.AssemblyAttributes.cs`; restore-enabled retry timed out and the timed-out build process is no longer running.

## Decision 13 - LOD And Transform Contract Lockdown

Problem: The Safe Shallows validator locked counts, mesh references, materials, atlas importers, rule assets, renderer flags, and rock collider alignment. It still trusted implicit Unity prefab state for LOD transition heights, crossfade mode, root transform identity, LOD child naming, LOD child rotation/scale, and collision proxy transform identity. That weakens local H-Phi because prefab correctness depends on unvalidated Unity-object state.

Solution: Extend `ShallowsBioForgeBatchBaker` with transform and LOD contract validation. The validator now rejects non-identity root transforms, non-crossfade LODGroups, wrong LOD screen-relative heights or fade widths, LOD children not named `LOD0`/`LOD1`/`LOD2`, LOD children with rotated/scaled transforms, and rock collision proxies not named `Collision_LOD2` or not identity-rotated/scaled.

Rejected Alternatives: Re-baking the payload was rejected because the current prefab YAML already matches the contract. A project-wide H-Phi score run was rejected because the user explicitly prohibited dotnet rebuilds and previous global H-Phi scans have timed out under repo load. Runtime LOD manager code was rejected because this domain owns editor-authored assets, not runtime scalability switching.

Scalability potential: Low/MX350 keeps static MeshRenderer-owned prefabs with stable crossfade and no hidden transform scale that could distort culling, batching, or collider proxies. Middle/High retain longer LOD residency without transition drift. Ultra can spend saved runtime cost on denser Shallows dressing while the prefab contract remains deterministic.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes allocation. The gain is prevention: no accidental transform scale, disabled crossfade, or malformed collision child can silently increase visible popping, culling error, or collider/render mismatch on low-end hardware. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`. Scoped source scan found the new LOD/transform validators and no hot-path `Update`, `FindObject`, `GameObject.Find`, `SendMessage`, `UnityEvent`, public event, or renderer material mutation patterns in the touched ProceduralGen files. Prefab YAML scans found `BadLodTransitionYaml=0`, `BadTransformYaml=0`, and rock `BadCollisionNameYaml=0` across the 200 generated prefabs. Brace count is balanced.

## Decision 14 - Static Batching Flag Guard

Problem: The renderer/material contract enforces shared material and instancing-friendly setup, but the validator did not reject Unity static editor flags. If a generated flora prefab or child becomes `BatchingStatic`, it conflicts with the mandate that static batching and GPU instancing/GPU Resident Drawer ownership must not double-own the same renderer.

Solution: Add `ValidateStaticFlagsContract` to `ShallowsBioForgeBatchBaker`. The validator scans every transform under the generated prefab and rejects any nonzero `StaticEditorFlags`, reporting the child name and raw flag value.

Rejected Alternatives: Manually editing prefab YAML was rejected because the current prefabs already have zero static flags. Adding a runtime correction script was rejected because generated Shallows prefabs must remain scriptless and static-data-only. Project-wide H-Phi scoring was rejected because this pass is domain-local and the user prohibited dotnet rebuilds.

Scalability potential: Low/MX350 keeps MeshRenderer-owned flora eligible for the intended shared-material draw path without static-batching memory bloat. Middle/High/Ultra can increase density or LOD residency without hidden static-batching conflicts.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes allocation. The gain is prevention: no future prefab edit can silently increase memory through static batching or break the intended instancing/GPU-resident path. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`. Source scan found `ValidateStaticFlagsContract` and `GameObjectUtility.GetStaticEditorFlags`. Prefab YAML scans found `BadStaticFlagsYaml=0` for TubeCoral=50, Kelp=100, and PorousRock=50. Brace count is balanced.

## Decision 15 - Mesh Geometry And LOD Budget Contract Lockdown

Problem: Counts, mesh references, material state, rule assets, transforms, and static flags were validated, but the validator still trusted mesh payload integrity after load. A stale or hand-edited mesh asset could keep the expected path while carrying empty geometry, multiple submeshes, UInt32 indices, degenerate/NaN bounds, an over-budget LOD, or missing shader mask data on LOD1/LOD2.

Solution: Extend `ShallowsBioForgeBatchBaker` with mesh geometry validation and family-specific LOD triangle budget validation. The same constants now drive rule authoring, rule contract checks, and prefab LOD mesh checks. Every LOD mesh must be non-empty, one submesh, UInt16 index format, finite/non-degenerate bounds, within the exact family LOD budget, and carry a usable vertex color R gradient.

Rejected Alternatives: Raw YAML edits to 600 generated mesh assets were rejected because mesh serialization is fragile and current assets already satisfy the strengthened contract. A Unity batch re-bake was rejected for this pass because the user prohibited rebuild-style validation and no generated payload mutation was required. Runtime mesh correction was rejected because Shallows flora prefabs must stay scriptless and data-only.

Scalability potential: Low/MX350 receives verified compact UInt16 LOD meshes and strict far-LOD budgets. Middle and High keep the same deterministic payload with richer density/residency. Ultra can spend saved CPU on density and shader overkill while validation prevents silent geometry bloat from entering the asset library.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no accidental UInt32 index format, degenerate mesh, missing vertex color mask, or over-budget LOD can enter MX350-class scenes without editor validation failure. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scan found `ValidateMeshGeometryContract`, `ValidateLodTriangleBudget`, budget constants, and all-LOD `ValidateVertexColorGradient` calls. Mesh YAML scan found `Count=600`, `Bad=0`, with max triangles Kelp `LOD0=2200`, `LOD1=514`, `LOD2=94`; PorousRock `LOD0=3081`, `LOD1=581`, `LOD2=53`; TubeCoral `LOD0=2364`, `LOD1=342`, `LOD2=24`. Brace count is balanced.

## Decision 16 - Bounded Vertex Color Validation Scratch

Problem: The all-LOD vertex color contract improved payload coverage, but the validator used `mesh.colors`, which copies a managed `Color[]` for every mesh. This is editor-only, not runtime, but it is still unnecessary scale debt when validating 600 generated mesh assets and it weakens the zero-GC discipline around the bake pipeline.

Solution: Add one bounded reusable `List<Color>` scratch buffer sized from the largest accepted LOD budget (`RockLod0TriangleBudget * 3`). `ValidateVertexColorGradient` now rejects meshes that exceed the scratch capacity, uses `mesh.GetColors(VertexColorScratch)`, clears the list after use, and uses branch comparisons instead of `Mathf.Min/Max` inside the scan loop. `ValidateMeshGeometryContract` now also requires readable meshes because current validation depends on vertex color inspection.

Rejected Alternatives: Leaving `mesh.colors` was rejected because it scales linearly in managed allocations across every validator pass. Raw YAML mesh edits were rejected because mesh serialization is fragile and the current payload already satisfies the contract. Forcing non-readable meshes was rejected for this pass because it would require a new validation evidence path or a controlled re-bake; the user prohibited rebuild-style validation and no generated payload mutation was necessary.

Scalability potential: Low/MX350 still receives the same static LOD prefabs and compact triangle budgets. Middle/High/Ultra gain faster, bounded editor validation for future deterministic re-bakes while preserving shader-readable vertex masks. If a later bake moves to non-readable runtime meshes, the contract must add sidecar validation metadata or perform color validation before upload.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. Editor validation avoids one copied color array per checked LOD mesh; with 600 mesh assets, this prevents repeated transient managed arrays during validation. Exact editor microseconds are not profiled.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scan found `VertexColorScratch`, `mesh.GetColors`, `mesh.isReadable`, and no remaining `mesh.colors`. Mesh YAML scan found `Count=600`, `Bad=0`, `MaxVertices=9243`, `ScratchCapacity=9600`. Brace count is balanced.

## Decision 17 - Shader Source Contract And Fail-Closed Material Creation

Problem: Shared material validation locked shader name, material values, atlas bindings, and instancing flags, but not the shader asset path or source-level render contract. A shader could keep the same display name while losing opaque queue tags, SRP batcher CBUFFER, LOD crossfade, math LOD keyword, or instancing pragmas. The bake path also had a `Shader.Find` fallback, which weakened deterministic asset-path ownership.

Solution: Add `ShaderPath` as a single source of truth, remove the `Shader.Find` fallback, abort the bake if the authored shader asset is missing, and add `ValidateShaderSourceContract`. The validator now checks the exact shader asset path, required opaque/render/instancing/math-LOD/crossfade/SRP-batcher tokens, and rejects known alpha-blend or `ZWrite Off` drift.

Rejected Alternatives: Trusting shader name alone was rejected because names are not asset identity. Runtime shader keyword correction was rejected because this is an editor-authored asset library and runtime mutation would add hidden state. Editing the shader was rejected because the current shader already satisfies the contract; the missing work was validator coverage.

Scalability potential: Low/MX350 keeps opaque, ZWrite-on, SRP-batcher-friendly flora with math LOD support and no alpha blend overdraw. Middle/High/Ultra retain the same shader contract while intentionally enabling richer material tiers only through explicit asset/validator changes.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no accidental alpha blend, missing instancing, missing CBUFFER, or disabled LOD crossfade can silently enter the Shallows library. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`. Source scan found `ValidateShaderSourceContract`, `ShaderPath`, and no `Shader.Find` in the Shallows baker. Shader token scan returned `Missing=0`, `ForbiddenHits=0`. Brace count is balanced.

## Decision 18 - Validator Path Resolution And Readability Fail-Fast

Problem: The shader source validator used `Path.GetFullPath(ShaderPath)`, which assumes the Unity process working directory is the project root. That is usually true but not a contract. Separately, `ValidateMeshGeometryContract` could flag a non-readable mesh, then `ValidateVertexColorGradient` could still call `mesh.GetColors`, risking an exception instead of a clean validation failure. The touched source also contained a non-ASCII separator in a cold-allocation comment while nearby ProceduralGen comments use ASCII separators.

Solution: Resolve shader asset files from `Application.dataPath` by deriving the project root and combining the native asset path under it. Add an explicit `mesh.isReadable` fail-fast inside `ValidateVertexColorGradient` before calling `GetColors`. Normalize the touched cold-allocation comment to ASCII separators matching surrounding editor generator comments.

Rejected Alternatives: Keeping current-working-directory resolution was rejected because batchmode/editor launchers can vary. Relying on `ValidateMeshGeometryContract` alone was rejected because validators should fail cleanly even when earlier checks already reported a bad asset. Rewriting mesh import settings was rejected because current generated assets already satisfy readability and no re-bake was required.

Scalability potential: Low/MX350 runtime payload is unchanged. Middle/High/Ultra benefit through more reliable deterministic validation in future re-bakes. Editor validation now fails closed with deterministic paths and readable-mesh checks before expensive payload inspection.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: validation no longer depends on process CWD and no longer risks exception-driven aborts on non-readable mesh drift. Exact editor microseconds are not profiled.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`. Source scans found `ResolveProjectAssetAbsolutePath`, no Shallows `Shader.Find`, no `mesh.colors`, and `NonAscii=0`. Mesh readability YAML scan found `Count=600`, `Bad=0`, `MaxVertices=9243`, `ScratchCapacity=9600`. Shader token scan returned `Missing=0`, `ForbiddenHits=0`. Brace count is balanced.

## Decision 19 - Atlas Texture Asset Dimension Contract

Problem: Atlas importer validation proves compression, wrap, mipmaps, readability, sRGB policy, and platform format, while material validation proves texture binding identity. It still did not prove that the underlying atlas source assets are the exact expected `Texture2D` paths and dimensions. A wrong-size atlas could be imported down to max size or sampled differently while much of the prior contract still passed.

Solution: Add `ValidateAtlasTextureAsset` and call it for Albedo, Normal, ORM, and MatCap before importer validation. Each atlas must load as `Texture2D`, resolve to the exact expected project path, and report `width == 1024` and `height == 1024`.

Rejected Alternatives: Trusting importer `maxTextureSize` was rejected because it is a ceiling, not source truth. Rewriting or regenerating PNGs was rejected because current atlas files already satisfy the contract. Runtime texture validation was rejected because Shallows atlas payload must remain editor-authored and static.

Scalability potential: Low/MX350 gets predictable 1024 shared atlases with compressed import settings and no unique per-instance textures. Middle/High/Ultra can intentionally upgrade atlas dimensions only by changing the constant, generator, validator, and performance rationale together.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no oversized, undersized, or wrong-path atlas can silently affect VRAM or visual sampling. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`. Source scan found `ValidateAtlasTextureAsset`; brace count is balanced and source `NonAscii=0`. PNG IHDR scan found all four Shallows atlas files are `1024x1024`, `AtlasPngDimensionScan Count=4 Bad=0`.

## Decision 20 - Prefab Envelope, Material Queue, And Vertex Stream Contracts

Problem: The Safe Shallows validator proved counts, paths, LODs, geometry budgets, atlas dimensions, shader source, and material values. It still allowed three hidden drift classes: extra prefab components/children, extra material slots on a renderer while the first slot stayed valid, and mesh payloads that retained valid counts while losing shader-required normals, vertex colors, or UV0. Those are density killers because they raise renderer/component cost or break the visual fake without changing headline asset counts.

Solution: Add material asset identity validation for name/path and serialized default custom render queue. Add exact prefab hierarchy/component envelope validation: flora prefabs must be root + LOD0/LOD1/LOD2 only; rock prefabs must additionally contain `Collision_LOD2`; root must contain only `Transform` + `LODGroup`; LOD children only `Transform` + `MeshFilter` + `MeshRenderer`; collision only `Transform` + `MeshCollider`. Replace renderer `sharedMaterial` validation with reusable-list `GetSharedMaterials` validation requiring exactly one slot. Extend mesh geometry validation with `HasVertexAttribute` checks for `Position`, `Normal`, `Color`, and `TexCoord0`.

Rejected Alternatives: Runtime cleanup scripts were rejected because generated Shallows prefabs must remain scriptless data. Trusting `renderer.sharedMaterial` was rejected because it proves only the first material slot. Broad prefab rewriting or re-bake was rejected because the current generated payload already satisfies the stronger contract and the user prohibited rebuild-style validation. Project-wide H-Phi scoring was rejected because this pass is strictly editor-domain and source/YAML verified.

Scalability potential: Low/MX350 keeps component-minimal static prefabs, one shared material slot per renderer, and shader-complete vertex streams. Middle and High retain stable density with fewer hidden renderer/component surprises. Ultra can spend saved CPU/GPU headroom on more Shallows dressing or richer shader tiers only through explicit contract changes.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no extra components, extra material slots, transparent queue override, missing normals, missing color stream, or missing UV0 can enter the generated Shallows library unnoticed. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`. Source scans found the new material, prefab hierarchy, component envelope, renderer material slot, and mesh vertex attribute validators; source brace count remained balanced and `NonAscii=0`. Forbidden source scan found no Shallows `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, `Update`, `LateUpdate`, or `FixedUpdate` hits. YAML scans found `PrefabEnvelopeYamlScan Count=200 Bad=0 MaterialGuid=f669d8458f3703841b4ed34a8236b192`, material `m_CustomRenderQueue: -1` with instancing enabled and empty keyword arrays, and `MeshVertexChannelYamlScan Count=600 Bad=0 MaxNonZeroChannels=5`.

## Decision 21 - Material Sampling State Lockdown

Problem: The material validator locked shader identity, atlas object references, scalar/color values, default render queue, and renderer material slots. It still did not lock per-texture scale/offset or serialized material keyword arrays. A bad atlas transform would distort all triplanar sampling, and a stray keyword could create unwanted shader variants while every prior texture-reference check still passed.

Solution: During material creation, explicitly set `_AlbedoAtlas`, `_NormalAtlas`, `_ORMAtlas`, and `_MatCap` texture scale to `Vector2.one` and offset to `Vector2.zero`. During validation, reject any atlas texture transform that is not identity and reject non-empty serialized `m_ValidKeywords` or `m_InvalidKeywords`. Add `Approximately(Vector2)` for the same epsilon-based contract style used by existing color and transform checks.

Rejected Alternatives: Trusting Unity's default texture transform state was rejected because it is serialized mutable data. Runtime material correction was rejected because Shallows payloads must remain static and shared, with no material clone or runtime mutation. Adding shader-side compensation was rejected because it would spend ALU to hide an editor asset defect.

Scalability potential: Low/MX350 keeps one shared atlas layout with no accidental tiling or offset blowups. Middle/High keep deterministic sampling and SRP-batcher-friendly variant state. Ultra can intentionally add higher-tier material keywords only by changing the material contract and rationale together.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no accidental atlas transform or keyword drift can silently create visual instability, shader variant spread, or sampling mismatch across 200 generated prefabs. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`. Source scans found `SetMaterialTextureTransform`, `ValidateMaterialTextureTransform`, serialized keyword checks, and `Approximately(Vector2)`; source brace count remained balanced and `NonAscii=0`. Forbidden source scan found no Shallows `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, `Update`, `LateUpdate`, or `FixedUpdate` hits. Material YAML scan found `MaterialTextureTransformYamlScan Props=4 Bad=0 ValidKeywordEmpty=1 InvalidKeywordEmpty=1 DefaultQueue=1`.

## Decision 22 - Atlas Import Metadata Realignment

Problem: The atlas source PNGs are exactly `1024x1024` and the baker contract uses `AtlasSize = 1024`, but the current Unity importer metadata did not match: top-level max size was `2048`, Default/Standalone platform max size was `512`, and ORM was imported as sRGB. That creates a real fail condition for the existing validator and a real visual/VRAM ambiguity: authored 1024 data could be sampled as downscaled 512 in Standalone while ORM roughness/emission data goes through gamma space.

Solution: Patch only the four Shallows atlas `.png.meta` files. Top-level, DefaultTexturePlatform, and Standalone `maxTextureSize` now match `1024`. Albedo, ORM, and MatCap keep Standalone BC7; Normal keeps BC5. ORM is now linear (`sRGBTexture: 0`), matching the baker's `AtlasKind.Orm` importer contract.

Rejected Alternatives: Lowering `AtlasSize` to `512` was rejected because it would discard authored source detail and contradict the status-established 1024 atlas payload. Leaving Default/Standalone at `512` was rejected because it would make the validator fail and silently downscale the shipped PC atlas. Running Unity reimport or any dotnet rebuild was rejected under the user's explicit constraint.

Scalability potential: Low/MX350 gets predictable compressed 1024 shared atlases with no per-instance textures or material clones. Middle/High/Ultra preserve the same source detail and can intentionally change atlas size only through a contract update. ORM linear import keeps material response stable across tiers.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no Standalone 512 downsample, top-level 2048 ambiguity, or sRGB ORM mistake can silently alter visual quality or material math. Exact runtime microseconds are not profiled because this is import metadata validation.

Verification: No dotnet rebuild and no Unity import was run. `AtlasImporterYamlScan Count=4 Bad=0` and `AtlasPngDimensionScan Count=4 Bad=0`. `git diff --check` passed for the four edited Shallows atlas meta files.

## Decision 23 - Rock Collider Proxy Mesh Contract

Problem: The rock validation contract proved collider count, convex mode, transform name, and renderer alignment, but it did not prove the collider was enabled, non-trigger, or bound to the exact LOD2 render mesh. A rock prefab could silently use a stale or higher-cost collision mesh while passing most existing checks, weakening the visual-fake proxy rule.

Solution: Extend `ValidateRockCollider` to resolve the LOD2 renderer mesh and require the single `MeshCollider` to be enabled, non-trigger, convex, non-null, and sharing that exact LOD2 mesh object. This keeps rock collision tied to the cheapest already-authored visible proxy.

Rejected Alternatives: Adding runtime collider correction was rejected because generated Shallows prefabs must remain static data. Adding a new simplified collider bake was rejected because the current LOD2 mesh is already the deterministic cheap proxy and all 50 assets satisfy it. Adding flora colliders was rejected because flora remains visual-only and collider-free by mandate.

Scalability potential: Low/MX350 keeps only 50 rock convex proxies and no flora colliders. Middle/High/Ultra can increase Shallows dressing density without hidden collider mesh bloat. If higher-tier rock collision ever needs more precision, it must be authored as a separate explicit tier contract.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no stale, disabled, trigger-only, or high-cost mesh collider can enter the Shallows rock library unnoticed. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`. Source scans found `colliders[0].enabled`, `colliders[0].isTrigger`, and `colliders[0].sharedMesh != lod2Mesh`; source brace count remained balanced and `NonAscii=0`. YAML scans found `RockColliderLod2GuidYamlScan Count=50 Bad=0` and `ShallowsColliderCountYamlScan Count=200 Bad=0`.

## Decision 24 - Prefab Activation And LODGroup State Lockdown

Problem: The Shallows prefab validator proved exact hierarchy, component envelopes, material slots, mesh references, static editor flags, and renderer flags. It still did not reject hidden GameObject state drift. A generated prefab could keep all expected objects and references while a child was inactive, placed on a non-default layer, retagged, or had the root `LODGroup` disabled. That would produce invisible flora, bad collision/raycast layer behavior, missing LOD switching, or inconsistent GPU Resident Drawer ownership without changing mesh/material counts.

Solution: Add default layer/tag constants to `ShallowsBioForgeBatchBaker`, validate every generated transform through `ValidateGameObjectStateContract`, and require each GameObject to be `activeSelf`, layer `0`, and `CompareTag("Untagged")`. Also extend `ValidateLodGroupContract` to reject disabled `LODGroup` components before checking crossfade and transition values.

Rejected Alternatives: Runtime repair scripts were rejected because generated Shallows prefabs must remain scriptless static data. Layer/tag correction during play was rejected because it hides asset defects and risks cross-domain physics/render ownership. Re-baking or Unity import was rejected because current assets already satisfy the stronger contract and the user prohibited rebuild-style validation.

Scalability potential: Low/MX350 keeps active, default-layer, untagged MeshRenderer-owned prefab data with enabled LOD switching and no surprise physics-layer or culling state. Middle/High/Ultra can spend density and shader overkill on the same deterministic payload while validator coverage prevents hidden activation or LODGroup drift from corrupting tier behavior.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no inactive renderer object, wrong layer/tag, or disabled LODGroup can enter the Shallows library unnoticed and force runtime fix-up or visual/collision debugging on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `PrefabGameObjectStateYamlScan Count=200 Bad=0`; `LodGroupEnabledYamlScan Count=200 Bad=0`; `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `DefaultLayer`, `UntaggedTag`, `ValidateGameObjectStateContract`, and `lodGroup.enabled`; source brace count remained balanced and `NonAscii=0`. Forbidden source scan found no Shallows `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, `Update`, `LateUpdate`, or `FixedUpdate` hits.

## Decision 25 - LODGroup Bounds And Scratch Allocation Hygiene

Problem: The current Shallows validator carried two remaining editor-pipeline hygiene issues. First, only one reusable scratch list had a capacity/reason/owner cold-allocation comment, while the prefab/component/material scratch lists shared a generic comment. Second, the `LODGroup` contract rejected disabled state, crossfade drift, and transition drift, but did not reject invalid bounds state such as zero size or non-finite local reference data.

Solution: Convert the three generic scratch-list comments into explicit capacity/reason/owner cold-allocation annotations, and widen the small editor scratch capacities to tolerate common corrupted prefab inspection without immediate list growth. Extend `ValidateLodGroupContract` to require finite `localReferencePoint`, finite `size`, and `size > 0`.

Rejected Alternatives: Leaving the comments generic was rejected because the local mandate requires explicit allocation evidence. Runtime LOD bounds correction was rejected because generated Shallows prefabs must be authored correctly and remain scriptless. Re-baking was rejected because the existing prefab YAML already satisfies the stronger contract and the user prohibited rebuild-style validation.

Scalability potential: Low/MX350 gets deterministic LOD bounds data with no disabled or zero-size LODGroup surprises. Middle/High/Ultra can raise density or visual quality while the editor validator continues to reject bounds drift that would break LOD switching or renderer ownership.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention and editor hygiene: no invalid LOD bounds can enter the asset library unnoticed, and the validator scratch ownership is explicit. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `LodGroupBoundsYamlScan Count=200 Bad=0 MinSize=1.665000 MaxSize=18.835001`; `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found canonical scratch capacities `List<Transform>[8]`, `List<Component>[8]`, `List<Material>[4]`, `lodGroup.localReferencePoint`, and `lodGroup.size <= 0f`; source brace count remained balanced and `NonAscii=0`. Forbidden source scan remained clean.

## Decision 26 - Mesh Bounds Budget Contract

Problem: The mesh validator proved non-empty geometry, one submesh, UInt16 indices, readable data, required vertex streams, finite non-degenerate bounds, family triangle ceilings, and vertex color gradients. It still allowed a mesh to carry very large but finite bounds. Oversized bounds can make culling and LOD decisions conservative, keep flora rendered when it should be culled, and weaken GPU Resident Drawer/LOD efficiency without changing triangle counts or material references.

Solution: Add family-specific bounds extent-squared budgets to `ShallowsBioForgeBatchBaker`: TubeCoral `4`, Kelp `121`, PorousRock `9`. Extend `ValidateMeshGeometryContract` to receive `familyFolder`, resolve the family budget through `TryResolveMaxBoundsExtentSq`, and reject bounds with non-positive, non-finite, or over-budget extents.

Rejected Alternatives: Relying only on triangle counts was rejected because culling cost is driven by bounds as well as geometry. Runtime `mesh.RecalculateBounds` or renderer bounds correction was rejected because generated Shallows assets must be correct editor-authored data. Tighter exact per-mesh hashes were rejected because deterministic generation already owns identity; this pass needed a scalable culling budget guard, not brittle serialization hashes.

Scalability potential: Low/MX350 gets compact renderer bounds that preserve culling and LOD early-out behavior. Middle/High/Ultra can use the same deterministic payload with denser dressing while validation prevents malformed bounds from inflating visible renderer sets.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no oversized Shallows mesh bounds can silently inflate renderer visibility, LOD residency, or culling work on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `MeshBoundsBudgetYamlScan TotalBad=0`; family maxima were Kelp `93.313505/121`, TubeCoral `2.168438/4`, PorousRock `5.143031/9`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `CoralMaxBoundsExtentSq`, `KelpMaxBoundsExtentSq`, `RockMaxBoundsExtentSq`, `TryResolveMaxBoundsExtentSq`, and `MaxBoundsExtentSq`; source brace count remained balanced and `NonAscii=0`. Forbidden source scan remained clean.

## Decision 27 - Mesh Topology And Vertex Budget Contract

Problem: The mesh validator enforced triangle count ceilings but did not explicitly reject unused vertex-buffer bloat, malformed index counts, or non-triangle submesh topology. A mesh could remain under the triangle budget while carrying extra vertices that increase GPU memory/bandwidth cost, or a topology/index drift that breaks the assumption that index count maps cleanly to triangles.

Solution: Harden `ValidateMeshGeometryContract` to compute `indexCount` and `MeshTopology` only after confirming there is exactly one submesh, then reject zero index count, index counts not divisible by three, and any topology other than `MeshTopology.Triangles`. Add `ValidateLodVertexBudget`, deriving the per-LOD vertex ceiling as `triangleBudget * 3`, and call it for every generated LOD mesh.

Rejected Alternatives: Keeping triangle count as the only geometry budget was rejected because unused vertices still consume memory and vertex-processing bandwidth. Runtime mesh cleanup, `RecalculateBounds`, or topology repair was rejected because Shallows payloads must be authored and validated as static assets before runtime. A raw YAML rewrite was rejected because Unity mesh serialization is fragile and current assets already satisfy the stronger contract.

Scalability potential: Low/MX350 gets stricter control over mesh memory and vertex work in every LOD. Middle/High/Ultra can increase density or keep longer LOD residency while the validator prevents malformed or bloated mesh payloads from entering the authored library.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no extra unused vertices, non-triangle topology, or malformed index buffers can silently inflate geometry bandwidth or corrupt culling/LOD assumptions on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `MeshVertexIndexYamlScan TotalBad=0`; maximum vertex/index counts were Kelp `6600/1542/282`, TubeCoral `7092/1026/72`, PorousRock `9243/1743/159`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `ValidateLodVertexBudget`, `MeshTopology.Triangles`, `indexCount % 3ul`, `GetTopology`, and `IndexCount=`; source brace count remained balanced and `NonAscii=0`. Forbidden source scan remained clean.

## Decision 28 - Prefab Deterministic Name Contract

Problem: The Shallows validator proved generated prefab counts, hierarchy, component envelopes, materials, mesh references, LOD payloads, and renderer flags. It still trusted the prefab file stem and root `GameObject.name` as identity. A manually renamed or misclassified prefab could keep valid references while breaking deterministic traceability back to the BioForge family, variation index, kind, and seed hash.

Solution: Add `ValidatePrefabNameContract` before `LODGroup` validation. The contract requires root name equality with the asset stem and a family-specific deterministic stem shape: `GEN_Shallows_TubeCoral_###_Flora_HHHHHHHH`, `GEN_Shallows_Kelp_###_Flora_HHHHHHHH`, or `GEN_Shallows_PorousRock_###_Rock_HHHHHHHH`. The parser uses direct ordinal character checks for three decimal digits, separators, kind text, and eight uppercase hex digits.

Rejected Alternatives: Regex validation was rejected because direct character checks are more explicit, avoid regex dependency/allocations, and match the deterministic BioForge naming grammar exactly. Runtime name repair was rejected because prefab identity is an offline asset contract, not runtime behavior. Hashing full YAML names was rejected because the generator already owns deterministic stems and this check should reject malformed identity without becoming brittle to unrelated Unity serialization order.

Scalability potential: Low/MX350 keeps asset library traceability cheap and deterministic for culling, LOD, and content QA. Middle/High/Ultra can raise density or add richer variants while the validator still prevents misnamed payloads from entering Shallows dressing sets. Visual overkill remains an intentional asset-tier choice, not accidental identity drift.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no misnamed or misclassified Shallows prefab can silently enter the static library and force runtime lookup, repair, or QA ambiguity on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `PrefabNameContractYamlScan Count=200 Bad=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `ValidatePrefabNameContract`, `TryResolvePrefabNameContract`, `IsThreeDigitIndex`, `IsUpperHex8`, and `StringRangeEquals`; source brace count remained balanced and `NonAscii=0`. Case-sensitive forbidden source scan found no `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, `Update`, `LateUpdate`, `FixedUpdate`, or `Regex` hits.

## Decision 29 - Mesh Asset Name Contract

Problem: Prefab mesh references were validated by exact asset path, and mesh payloads were validated by streams, bounds, topology, triangle budget, vertex budget, and color gradient. The internal `Mesh.name` field was still trusted. A mesh asset could keep the correct file path while carrying a stale object name, weakening deterministic QA traceability and confusing editor-side diagnostics.

Solution: Extend `ValidatePrefabMeshReferences` to require every resolved mesh object name to equal `<prefab-stem>_LOD<i>`. This binds prefab identity, file path, mesh object name, and LOD index into one deterministic contract.

Rejected Alternatives: Full mesh YAML hashing was rejected because Unity serialization order and metadata can change without changing the usable payload. Runtime mesh renaming was rejected because generated BioForge meshes must be correct static editor assets. Adding a separate mesh registry was rejected because the existing prefab-to-mesh reference contract already gives the needed decoupled validation path.

Scalability potential: Low/MX350 keeps QA and LOD diagnostics deterministic without runtime lookup or fix-up. Middle/High/Ultra can add richer Shallows variants while mesh identity remains stable across asset folders, prefab references, and validation logs.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no stale mesh object name can create editor ambiguity or push runtime repair/lookup logic into the render path on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `MeshNameContractYamlScan Count=600 Bad=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `expectedMeshName`, `mesh.name`, and `LOD{i} mesh name mismatch`; source brace count remained balanced and `NonAscii=0`. Case-sensitive forbidden source scan found no `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, `Update`, `LateUpdate`, `FixedUpdate`, or `Regex` hits.

## Decision 30 - Family Index Completeness Contract

Problem: Family prefab counts and deterministic name shapes were validated, but count equality alone did not prove that every expected variation index was present exactly once. A library could contain 100 Kelp prefabs while missing `042` and duplicating `041` under a different seed hash.

Solution: Add `ValidateFamilyIndexContract` to `ValidateFamily`. It uses one fixed `bool[100]` scratch buffer sized for the largest Shallows family, parses the three-digit index with `TryParseThreeDigitIndex`, rejects out-of-range/duplicate slots, and then scans for missing indices.

Rejected Alternatives: Allocating a `HashSet<int>` per validation was rejected because the family size is bounded and a fixed scratch bitset is cheaper and clearer. Sorting prefab names was rejected because it adds unnecessary allocation/work for a simple presence contract. Runtime registry reconciliation was rejected because generated Shallows assets must be deterministic before play.

Scalability potential: Low/MX350 gets complete deterministic variation coverage without runtime indexing or missing-slot repair. Middle/High/Ultra can scale density or variant richness while content QA can rely on exact per-family coverage from the static asset library.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no missing or duplicate variation index can force runtime fallback, lookup ambiguity, or dressing-set holes on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `FamilyIndexContractYamlScan Count=200 Bad=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `FamilyIndexScratch`, `ValidateFamilyIndexContract`, `TryParseThreeDigitIndex`, `Array.Clear(FamilyIndexScratch`, and `index completeness contract failed`; source brace count remained balanced and `NonAscii=0`. Case-sensitive forbidden source scan found no `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, `Update`, `LateUpdate`, `FixedUpdate`, or `Regex` hits.

## Decision 31 - Mesh LOD Triplet Completeness Contract

Problem: Mesh family validation counted total mesh assets and LOD suffix distribution. That did not prove each generated variation index had a complete LOD0/LOD1/LOD2 triplet. A folder could hide a missing LOD1 for one index behind an orphan LOD1 from another index while the suffix counts still passed.

Solution: Add `ValidateMeshLodIndexContract` after the existing mesh count/distribution checks. It parses deterministic mesh stems with `TryParseMeshLodStem`, then uses one fixed `bool[300]` scratch buffer to mark `(variationIndex * 3) + lodIndex` slots and reject malformed, out-of-range, duplicate, or missing slots.

Rejected Alternatives: Keeping suffix counts was rejected because it proves totals, not coverage. Allocating a `HashSet` or sorting mesh names was rejected because Shallows family sizes are bounded and a fixed bitset is cheaper and simpler. Runtime mesh lookup fallback was rejected because BioForge output must be static and complete before gameplay.

Scalability potential: Low/MX350 gets guaranteed complete LOD triplets with no runtime lookup repair, fallback mesh binding, or culling uncertainty. Middle/High/Ultra can scale variant density and LOD residency while the static library keeps exact triplet coverage.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no missing or duplicated LOD mesh slot can force runtime fallback, renderer mutation, or diagnostic ambiguity on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `MeshLodIndexContractYamlScan Count=600 Bad=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `MeshLodIndexScratch`, `ValidateMeshLodIndexContract`, `TryParseMeshLodStem`, `Array.Clear(MeshLodIndexScratch`, and `mesh LOD index completeness contract failed`; source brace count remained balanced and `NonAscii=0`. Case-sensitive forbidden source scan found no `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, `Update`, `LateUpdate`, `FixedUpdate`, `Regex`, `HashSet<`, or `OrderBy` hits.

## Decision 32 - Renderer Serialized State Contract

Problem: Renderer validation rejected disabled renderers, shadow casting, received shadows, motion vectors, probe usage, dynamic occludee state, material-slot drift, static editor flags, and transform drift. It still did not lock several serialized renderer fields that can affect visibility, render ordering, or hidden lighting/probe hookups: static shadow caster, rendering layer mask, renderer priority, probe anchor, LPPV override, sorting layer, and sorting order.

Solution: Add `ValidateRendererSerializedStateContract` and serialized property helpers. The validator now requires `m_StaticShadowCaster=0`, `m_RenderingLayerMask=1`, `m_RendererPriority=0`, null `m_ProbeAnchor`, null `m_LightProbeVolumeOverride`, `m_SortingLayerID=0`, `m_SortingLayer=0`, and `m_SortingOrder=0` for every Shallows MeshRenderer. It also rejects `renderer.forceRenderingOff`.

Rejected Alternatives: Runtime renderer repair was rejected because Shallows flora/rocks are static MeshRenderer-owned assets intended for GPU Resident Drawer ownership. Relying only on public high-level flags was rejected because the remaining risk was serialized prefab drift. Adding custom render layers was rejected because this batch has no cross-domain lighting or visor ownership justification.

Scalability potential: Low/MX350 gets deterministic render-layer and ordering state with no surprise camera exclusion, priority sorting, or probe hookups. Middle/High/Ultra can raise density or visual tier while renderer state remains stable and batcher-friendly.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no hidden renderer serialized state can force runtime fix-up, unexpected render-layer filtering, or probe work on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `RendererSerializedStateYamlScan Renderers=600 Bad=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `ValidateRendererSerializedStateContract`, `SerializedIntEquals`, `SerializedObjectReferenceIsNull`, `m_RenderingLayerMask`, `m_RendererPriority`, `m_ProbeAnchor`, and `m_LightProbeVolumeOverride`; source brace count remained balanced and `NonAscii=0`. Case-sensitive forbidden source scan found no `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, `Update`, `LateUpdate`, `FixedUpdate`, `Regex`, `HashSet<`, or `OrderBy` hits.

## Decision 33 - Shader Pass Budget Contract

Problem: The shared Shallows material shader source was validated for opaque queue, instancing, LOD fade, material CBUFFER, and forbidden blend states. It did not explicitly lock the number and identity of shader passes. A future shader edit could add an extra pass, `UsePass`, `GrabPass`, or `Fallback` and increase draw/SetPass risk while material and renderer contracts still passed.

Solution: Add `ValidateShaderPassBudget` to the shader source contract. The validator now requires exactly two explicit line-start `Pass` blocks, zero `UsePass`, zero `GrabPass`, zero `Fallback`, and the expected `ForwardLit`/`UniversalForward` plus `ShadowCaster`/`ShadowCaster` identities. `CountShaderLineToken` uses direct line scanning rather than regex.

Rejected Alternatives: Relying only on `renderer.shadowCastingMode=Off` was rejected because shader-source pass drift is still render debt and can become active if renderer state changes. Removing the ShadowCaster pass was rejected because that is a shader asset design change outside this validator pass; current renderer assets disable shadow casting, so the cost path remains closed. Regex parsing was rejected because direct token scanning is simpler and avoids adding a regex dependency to the validator source.

Scalability potential: Low/MX350 keeps the Shallows shader pass footprint bounded and auditable. Middle/High/Ultra can add richer visuals only by explicitly changing this pass budget contract and the rationale, not by silent shader-source drift.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no extra shader pass, inherited pass, grab pass, or fallback can silently inflate draw/SetPass work on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `ShaderPassBudgetScan Pass=2 UsePass=0 GrabPass=0 Fallback=0 Forward=True Shadow=True`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs` with only the repo CRLF warning. Source scans found `ValidateShaderPassBudget` and `CountShaderLineToken`; source brace count remained balanced with `Delta=0`, `NonAscii=0`, and no literal brace char remained. Case-sensitive forbidden source scan remained clean.

## Decision 34 - Shader Pragma Budget Contract

Problem: Shader pass count was locked, but pragma expansion was still possible. A future edit could add extra `multi_compile` or `shader_feature` directives, increasing shader variant count and warmup/build pressure while the two-pass contract still passed.

Solution: Add `ValidateShaderPragmaBudget` and `CountSourceToken`. The shader source contract now requires exact counts for target, vertex, fragment, instancing, uniform-scaling instancing options, fog, main-light shadow, LOD fade, Math LOD, local high-quality, and skip-variant pragmas, plus zero `#pragma multi_compile _ _ADDITIONAL_LIGHTS`.

Rejected Alternatives: Keeping required-token-only validation was rejected because it proves presence, not budget. Regex parsing was rejected because ordinal token counting is sufficient for known source tokens and avoids adding a regex dependency. Runtime shader warmup compensation was rejected because variant debt must be prevented at source.

Scalability potential: Low/MX350 keeps shader variant fan-out bounded and auditable. Middle/High/Ultra can intentionally add visual overkill only by changing the pragma budget and rationale together.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no silent variant expansion, additional-light variant fan-out, or unexpected warmup/build pressure can enter the Shallows shared shader on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. Shader token scan found `target 4.5=1`, `target 3.5=1`, vertex/fragment entries each exactly once, instancing pragmas `2`, fog `1`, main-light shadows `1`, LOD fade `2`, Math LOD `1`, local high-quality `1`, skip variants `1`, and additional-lights multi-compile `0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 35 - BioRule Raw Serialization Contract

Problem: `ValidateRuleAsset` compared many `BioRuleData` public getters to expected values. Those getters can clamp or default raw serialized data. For boundary/default values, a corrupted `.asset` could still present the expected getter value while the raw authored payload drifted.

Solution: Extend `ValidateRuleAsset` to validate the raw serialized fields through `SerializedObject`: rule asset name, prefix, material reference, axiom, iterations, max branches, angle, step, taper, radii, SDF resolution/profile, bounds padding, smooth-min, ribbon scales, LOD budgets, rock shape/noise/pore settings, and output folders. Added `SerializedFloatEquals`, `SerializedStringEquals`, and `SerializedObjectReferenceEquals`.

Rejected Alternatives: Relying on public getters was rejected because getter clamps/defaults can mask authored payload drift. Rewriting or rebaking rule assets was rejected because the current assets already satisfy the stricter raw contract. Runtime rule normalization was rejected because these rules are editor-only bake inputs and must be correct before generation.

Scalability potential: Low/MX350 gets deterministic offline bake inputs with no hidden clamped values that could later regenerate heavier meshes. Middle/High/Ultra can adjust visual overkill only through explicit rule contract changes, not accidental serialized drift.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no raw rule drift can silently rebake heavier or malformed Shallows payloads and push renderer/mesh cost onto i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `RuleRawSerializedYamlScan Count=3 Bad=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`; source scans found `SerializedFloatEquals`, `SerializedStringEquals`, and `SerializedObjectReferenceEquals`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 36 - BioRule Folder Exactness Contract

Problem: The baker validated the three expected Shallows `BioRuleData` assets by path, but it did not reject extra `BioRuleData` assets in `Assets/_Project/Data/ProceduralGen/Shallows`. A stale rule in that folder could be selected manually or by future tooling and regenerate non-canonical payloads while the canonical three still passed.

Solution: Add `ValidateRuleFolderContract` before validating individual rules. It uses `AssetDatabase.FindAssets("t:BioRuleData", new[] { RuleFolder })`, requires exactly three results, requires the TubeCoral/Kelp/PorousRock paths, and rejects any unexpected rule asset.

Rejected Alternatives: Ignoring extra rule assets was rejected because stale authoring data is a bake-input risk. Deleting unknown assets automatically was rejected because the validator must fail closed and report, not destructively edit folders. Runtime rule filtering was rejected because Shallows generation is editor-offline.

Scalability potential: Low/MX350 gets canonical bake inputs only, preventing accidental heavier or malformed generated payloads. Middle/High/Ultra can add richer rule tiers only through explicit folder/contract expansion.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no extra Shallows rule asset can silently produce out-of-budget meshes or materials for i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `RuleFolderExactnessYamlScan Count=3 Bad=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`; source scans found `ValidateRuleFolderContract`, `t:BioRuleData`, and `Rule folder contract failed`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 37 - Shallows Atlas And Material Folder Exactness Contract

Problem: The validator proved that the four expected atlas textures and one shared material exist and are correctly bound. It did not reject extra Shallows-named atlas or material assets in the same authoring folders. Stale assets can be rebound manually or by later tooling and break the shared atlas/material contract while the canonical assets still pass.

Solution: Add `MaterialFolder`, `ValidateMaterialFolderContract`, and `ValidateAtlasFolderContract`. The material folder contract requires exactly one `MAT_ProceduralBio_Shallows` material at the canonical path. The atlas folder contract requires exactly four `TX_ProceduralBio_Shallows` texture assets: Albedo, Normal, ORM, and MatCap.

Rejected Alternatives: Ignoring extra assets was rejected because stale authoring payloads are a real rebinding risk. Auto-deleting unexpected assets was rejected because the validator must fail closed and report, not destructively mutate the project. Runtime texture/material fallback was rejected because Shallows uses one static shared material and offline atlas payloads.

Scalability potential: Low/MX350 keeps one shared material and four shared atlas textures, preserving batching and VRAM predictability. Middle/High/Ultra can add richer Shallows variants only by explicitly expanding the folder contract.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no extra Shallows material or atlas can silently split batching, variant state, or VRAM assumptions on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `AtlasFolderExactnessScan Count=4 Bad=0`; `MaterialFolderExactnessScan Count=1 Bad=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`; source scans found `ValidateMaterialFolderContract`, `ValidateAtlasFolderContract`, `TX_ProceduralBio_Shallows t:Texture2D`, and `MAT_ProceduralBio_Shallows t:Material`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 38 - Shader Pragma Line-Token Hardening

Problem: `ValidateShaderPragmaBudget` initially used broad source substring counts. That can be fooled by a commented pragma token or unrelated string literal, letting the validator pass while the actual shader directives drift.

Solution: Replace every shader pragma budget count with `CountShaderLineToken`, which scans each line after leading whitespace and only counts real line-start directives. Remove the broader `CountSourceToken` helper.

Rejected Alternatives: Keeping substring counts was rejected because comments must not satisfy a render/variant budget contract. Regex parsing was rejected because the existing line scanner is deterministic, allocation-free for the validator path, and already used for pass counting. Runtime variant auditing was rejected because variant budget drift must be caught in source before import/build.

Scalability potential: Low/MX350 gets stricter shader source validation against silent variant fan-out. Middle/High/Ultra can still add visual overkill only through explicit pragma budget changes and updated rationale.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: commented or stale pragma text cannot hide actual shader directive drift that would affect i3/MX350 variant/build pressure. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. Shader line-token scan found all expected pragma counts and zero additional-light multi_compile; source scan found `CountSourceToken Count=0`. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 39 - Mesh And Prefab Family Subfolder Exactness Contract

Problem: The validator checked TubeCoral, Kelp, and PorousRock mesh/prefab families by expected folder path and asset counts. It did not reject extra subfolders under the Shallows mesh or prefab roots. Stale generated family folders can become future tooling or manual selection hazards while the canonical three families still pass.

Solution: Add `ValidateFamilySubfolderContracts` and `ValidateFamilySubfolderContract`. The validator now requires exactly three subfolders under both `MeshRoot` and `PrefabRoot`: TubeCoral, Kelp, and PorousRock.

Rejected Alternatives: Ignoring extra subfolders was rejected because stale generated payloads can bypass the canonical batch contract. Automatic folder deletion was rejected because validation should fail closed and report, not destructively modify assets. Runtime family filtering was rejected because generated Shallows payload ownership is editor-offline.

Scalability potential: Low/MX350 gets canonical generated family roots only, reducing risk of stray out-of-budget content entering scenes. Middle/High/Ultra can add richer family tiers only through explicit root-contract expansion.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no stale Shallows generated family folder can silently add draw, mesh, material, or VRAM load on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `FamilySubfolderExactnessScan Bad=0`, with `Count=3` under both Shallows mesh and prefab roots. `git diff --check` passed for `ShallowsBioForgeBatchBaker.cs`; source scans found `ValidateFamilySubfolderContracts`, `ValidateFamilySubfolderContract`, `AssetDatabase.GetSubFolders`, and `family subfolder contract failed`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 40 - Shared Material Serialized Payload Envelope

Problem: The shared material validator checked shader identity, public material properties, texture bindings, keyword arrays, and custom render queue. It did not lock several raw serialized material fields: parent material inheritance, modified serialized properties, disabled shader passes, build texture stacks, saved property array counts, and raw instancing/GI flags. Those hidden fields can create render-state drift while public getters still return the expected values.

Solution: Add `ValidateMaterialSerializedPayloadContract` under `ValidateMaterialAssetContract`. The validator now requires null `m_Parent`, `m_ModifiedSerializedProperties=false`, `m_LightmapFlags=0`, `m_EnableInstancingVariants=true`, `m_DoubleSidedGI=false`, zero `disabledShaderPasses`, empty `m_LockedProperties`, exact saved property counts (`TexEnvs=4`, `Ints=0`, `Floats=14`, `Colors=4`), zero `m_BuildTextureStacks`, and `m_AllowLocking=true`. Added `SerializedBoolEquals` and `SerializedArraySizeEquals` helpers.

Rejected Alternatives: Relying only on public material getters was rejected because Unity can retain hidden material payload state that is not visible through scalar/texture checks. Runtime material normalization was rejected because Shallows uses one static shared material and the validator must fail closed before assets enter scenes. Rewriting the material asset was rejected because the existing YAML already matches the stricter envelope.

Scalability potential: Low/MX350 keeps one flat shared material with no hidden disabled passes, build texture stacks, or inherited override chain. Middle/High/Ultra can add visual overkill only by changing the material envelope contract and documenting the render-state cost.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no hidden material payload can silently split render state, add disabled pass confusion, or force runtime material repair on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `MaterialSerializedPayloadYamlScan TexEnvs=4 Floats=14 Colors=4 Bad=0`. `git diff --check` passed for the touched Shallows files with only repo CRLF warnings. Source scans found `ValidateMaterialSerializedPayloadContract`, `SerializedBoolEquals`, `SerializedArraySizeEquals`, `m_SavedProperties.m_TexEnvs`, `m_BuildTextureStacks`, and `disabledShaderPasses`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 41 - Shared Material Saved Property Key Contract

Problem: The serialized material envelope locked saved-property array sizes but not the exact keys in those arrays. A material could retain four texture entries, fourteen float entries, and four color entries while one serialized key was stale, duplicated, or swapped. Public getters can still make the visible contract look correct while stale serialized payload remains in the asset.

Solution: Add fixed cold arrays for the expected serialized texture, float, and color keys, then validate them with `SerializedSavedPropertyKeysEqual`. The contract now requires exact ordered keys in `m_SavedProperties.m_TexEnvs`, `m_SavedProperties.m_Floats`, and `m_SavedProperties.m_Colors`.

Rejected Alternatives: Relying on public getters plus array counts was rejected because it proves visible values and count, not serialized-key identity. Sorting or hash sets were rejected because Unity serializes the current material in stable key order and a direct ordered comparison is cheaper and stricter. Runtime material scrubbing was rejected because Shallows material state must be deterministic before runtime.

Scalability potential: Low/MX350 keeps a single flat material with no stale serialized property payload that could become a render-state or authoring hazard. Middle/High/Ultra can add visual overkill only through explicit property-key contract expansion.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no stale or duplicated saved material property key can silently force runtime repair, material cloning, or shader/property mismatch diagnosis on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `MaterialSavedPropertyKeyYamlScan Tex=_AlbedoAtlas,_MatCap,_NormalAtlas,_ORMAtlas Floats=14 Colors=_BaseColor,_EmissionColor,_RootTint,_TipTint Bad=0`. `git diff --check` passed for the touched Shallows files with only repo CRLF warnings. Source scans found `MaterialTexEnvNames`, `MaterialFloatNames`, `MaterialColorNames`, `SerializedSavedPropertyKeysEqual`, and `FindPropertyRelative("first")`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 42 - Atlas Streaming And Alpha Importer Contract

Problem: `ValidateAtlasImporter` locked wrap/filter, mipmaps, readability, compression, sRGB, texture type, max size, and Standalone BC format. It did not explicitly lock mip streaming or alpha-transparency import flags. Those flags can change residency behavior or importer semantics while the atlas still appears to satisfy size/compression checks.

Solution: Set and validate `streamingMipmaps=false`, `streamingMipmapsPriority=0`, and `alphaIsTransparency=false` in the Shallows atlas importer path.

Rejected Alternatives: Trusting current `.meta` defaults was rejected because defaults can drift through Unity upgrades or manual importer edits. Runtime texture streaming policy overrides were rejected because this batch owns offline atlas assets, not runtime texture managers. Turning mipmaps off was rejected because LOD flora still benefits from mip filtering; only streaming is rejected for deterministic residency.

Scalability potential: Low/MX350 keeps predictable 1024 atlas residency without hidden streaming churn or alpha-import semantic changes. Middle/High/Ultra can expand atlas tiering only through explicit importer-contract changes and documented VRAM cost.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. The gain is prevention: no hidden streaming mip flag or alpha-transparency import change can cause residency churn, unexpected texture policy, or debugging cost on i3/MX350. Exact runtime microseconds are not profiled because this is editor validation.

Verification: No dotnet rebuild and no Unity import was run. `AtlasStreamingAlphaMetaScan Count=4 Bad=0`. Project source already uses `alphaIsTransparency`, and the Shallows baker now sets/checks `streamingMipmaps`, `streamingMipmapsPriority`, and `alphaIsTransparency`. `git diff --check` passed for the touched Shallows files with only repo CRLF warnings. Source scans found one setter and one validator check each for `streamingMipmaps`, `streamingMipmapsPriority`, and `alphaIsTransparency`; source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

## Decision 43 - Atlas Bake Pixel Scratch Reuse

Problem: `CreateOrUpdateAtlas` allocated a fresh `Color32[AtlasSize * AtlasSize]` for every atlas. A full Shallows bake writes four 1024 atlases, so the editor path produced four large transient arrays even though atlas generation is sequential and deterministic.

Solution: Add one documented cold `AtlasPixelScratch` buffer and write all sampled atlas pixels into it before `Texture2D.SetPixels32`. The scratch is reused for Albedo, Normal, ORM, and MatCap bakes.

Rejected Alternatives: Keeping per-atlas arrays was rejected because it creates avoidable large transient editor allocations. `ArrayPool<Color32>` was rejected because the buffer size is fixed, the tool is editor-only, and a single owned scratch buffer has clearer lifetime and zero pool misuse risk. Reducing atlas size was rejected because visual budget and existing 1024 contract remain correct.

Scalability potential: Low/MX350 editor-side bake avoids repeated large allocations and GC pressure while preserving atlas quality. Middle/High/Ultra can increase atlas richness later by changing explicit atlas budgets, not by accepting transient allocation churn.

Hardware Impact: Runtime remains 0 us/frame and 0 bytes procedural allocation. Bake-time allocation prevention is approximately three avoided 4 MiB transient arrays per full four-atlas bake after the first cold scratch allocation; exact editor microseconds are not profiled.

Verification: No dotnet rebuild and no Unity import was run. `AtlasPixelScratchSourceScan LocalAlloc=0 ColdAlloc=1 Writes=1 SetPixels=1`. `git diff --check` passed for the touched Shallows files with only repo CRLF warnings. Source brace count remained `Delta=0` and `NonAscii=0`; case-sensitive forbidden source scan remained clean.

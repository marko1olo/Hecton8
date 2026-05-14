# LOG - PROCEDURAL_BIOME_BAKER_SHALLOWS

## 2026-05-14 Intake

What was wrong: Safe Shallows Bio-Forge rule assets and exact batch output were not yet proven in the current workspace.

What was done: Extracted the XML prompt via CLI, read AGENTS/domain docs, read 8 relevant mandates, and located the existing editor-only Bio-Forge generation owner.

Cinematic Cheats used: Static authored SDF meshes, vertex color R height masks, shared material/atlas path, LODGroup cross-fade. No runtime flora physics.

Exact Microseconds saved: PENDING VERIFICATION. Static estimate: removing runtime procedural generation and per-object flora physics avoids >100 us spikes per streamed placement batch and 200-600 us/frame if 200 animated plant scripts had existed.

## 2026-05-14 Safe Shallows Bio-Forge Bake

What was wrong: The vertical slice had a Bio-Forge generator but no Safe Shallows `BioRuleData` rules or exact 50/100/50 generated asset set. The existing generator also lacked exact batch counts, porous subtraction controls, broad-leaf ribbon SDF support, final-span vertex color normalization, rock-only collider baking, and clean Burst write-range annotation for LOD decimation.

What was done: Added/used Shallows rule authoring for `Rule_Shallows_TubeCoral.asset`, `Rule_Shallows_Kelp.asset`, and `Rule_Shallows_PorousRock.asset`; generated `MAT_ProceduralBio_Shallows` and four shared atlas textures; baked and copied back 50 TubeCoral prefabs, 100 Kelp prefabs, 50 PorousRock prefabs, and 600 LOD mesh assets. Main copied output scan: `LODGroup=200`, `m_LODs=200`, `badShadows=0`, `floraColliders=0`, `rockColliders=50`, `rockConvex=50`, `materialRefs=600`.

Cinematic Cheats used: Offline SDF meshes instead of runtime L-system growth; thick capsule SDF for coral mass; ribbon SDF for kelp leaves instead of cloth/physics; deterministic Simplex plus subtractive pore spheres for porous rocks; vertex color R height masks instead of runtime probes; shared triplanar atlas instead of per-prefab textures; LOD2 impostors under 150 triangles with cast shadows disabled; flora collision removed, rocks get convex LOD2 mesh colliders only.

Exact Microseconds saved: Runtime procedural generation path is 0 us/frame and 0 bytes because all generation remains editor-only. Estimated low-end i3/MX350 savings: >100 us per streamed placement batch by removing runtime generation; 200-600 us/frame avoided versus 200 active plant animation scripts; 20-60 us/frame avoided versus runtime height-mask reconstruction; broadphase/collider cost removed from 150 flora prefabs. Exact profiler capture is not available because the main project compile is currently blocked outside this domain.

Verification: Isolated Unity 6000.4.1f1 validation passed after OMEGA polish: `Validation passed. Coral=50, Kelp=100, Rocks=50, Total=200, LOD2<150`. `dotnet build Hecton8.Core.csproj` is blocked by unrelated `CS2001` missing `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs`; no ProceduralGen syntax error appeared in the isolated compile/validate path.

## 2026-05-14 Second-Pass Safe Shallows Upgrade

What was wrong: The first completed payload was functional, but the second-pass audit found two content-grade defects: `ShallowsBioForgeBatchBaker` used `Assets/_Project/Art/Textures/...` while the project folder is `TEXTURES`, and rock colliders were on the prefab root while visible LOD renderers were offset for pivot cleanup. That could create rock collision drift. The previous bake also deleted generated folders before every run, which risks GUID churn after other systems start referencing the flora library.

What was done: Patched the bake to use the correct `TEXTURES` path, preserve generated folders on re-bake, generate rock colliders as aligned `Collision_LOD2` children, and validate MatCap atlas binding, exact one-renderer-per-LOD contract, non-null meshes, zero cast shadows, aligned convex rock colliders, and zero flora colliders. Reran isolated Unity 6000.4.1f1 batchmode bake from source and copied the staged outputs back without deleting main target folders. Final main scan: `Rules=3`, `Prefabs=200`, `MeshAssets=600`, `Textures=4`, `RockPrefabs=50`, `CollisionChildren=50`, `MeshColliders=50`, `FloraColliders=0`, `LODGroups=200`, `MeshRenderers=600`, `BadShadowLines=0`, `MaterialRefs=600`.

Cinematic Cheats used: Static offline SDF/L-system meshes, deterministic variants, shader-readable vertex color R masks, shared triplanar atlas plus MatCap, no runtime plant scripts, no flora collision, convex LOD2 rock collision only, LOD2 shadow casting disabled across all assets.

Exact Microseconds saved: Runtime generation remains 0 us/frame and 0 bytes. Estimated low-end i3/MX350 savings retained: >100 us per streamed placement batch from no runtime generation, 200-600 us/frame versus per-object plant animation scripts, 20-60 us/frame versus runtime height-mask reconstruction, and broadphase/collider work removed from 150 flora prefabs. The collider alignment fix prevents later gameplay correction casts or duplicate proxy colliders.

Verification: Direct Roslyn compile of isolated `Hecton8.Editor.ProceduralGen` returned `CscExit=0`. Isolated Unity log `Logs/ShallowsBioForgeStageBake_Rerun4.log` contains `Validation passed. Coral=50, Kelp=100, Rocks=50, Total=200, LOD2<150`. Main `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).`

## 2026-05-14 Batchmode Log Hygiene Pass

What was wrong: The core XML prompt still requires `PENDING VERIFICATION`, so the previous `VERIFIED MASTER GRADE` status header was not aligned with the extracted agent assignment. The isolated Unity bake log also showed successful generated-prefab messages carrying full stack traces per asset. That is useless for green-path bakes and inflates editor log I/O. Validation also did not explicitly reject renderer count drift or hot-path renderer flag drift beyond shadow casting.

What was done: Restored `Status_PROCEDURAL_BIOME_BAKER_SHALLOWS.md` and rationale status to `PENDING VERIFICATION` while keeping the pass/fail evidence. Patched `BioForgeGenerator` so batchmode skips cancelable progress bar work and successful generated-prefab summaries use `LogOption.NoStacktrace`. Patched `ShallowsBioForgeBatchBaker` validation to require exactly three renderers per prefab and reject receive shadows, motion vectors, light probes, reflection probes, dynamic occlusion, and cast shadows.

Cinematic Cheats used: No runtime change. This preserves the existing static offline mesh cheat, shared atlas cheat, shader mask cheat, LOD2 no-shadow cheat, and rock-only convex collision cheat.

Exact Microseconds saved: Runtime remains 0 us/frame. Editor-authoring savings are from skipping headless progress UI/string work and avoiding green-path stack trace emission for 200 generated prefabs. Exact microseconds not profiled; prior proof log was 681 KB with repeated success stack traces, which future bakes now avoid.

Verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).` Main prefab scan returned `Prefabs=200`, `LodGroups=200`, `MeshRenderers=600`, `BadCastShadows=0`, `BadReceiveShadows=0`, `BadDynamicOccludee=0`, `BadLightProbeUsage=0`, `BadReflectionProbeUsage=0`, `MeshColliders=50`, `CollisionChildren=50`.

## 2026-05-14 Stale Payload And Importer Contract Pass

What was wrong: GUID-stable re-bakes preserve existing generated folders, which is correct for downstream references but creates one failure mode: stale mesh assets could remain after an interrupted or changed bake. The validator also did not explicitly enforce material shader/instancing/GI settings or atlas importer settings.

What was done: Added generated mesh-count validation per family and per LOD suffix. Added shared material validation for `Hecton8/Flora/ProceduralBio`, instancing, GI flags, and atlas bindings. Added atlas importer validation for wrap mode, mipmaps, readability, compression, sRGB policy, normal-map type, max size, and Standalone BC5/BC7 settings. Also skipped `EditorUtility.ClearProgressBar()` in batchmode.

Cinematic Cheats used: Static offline SDF meshes remain the core cheat. The pass protects the shared compressed atlas cheat and exact LOD payload cheat so runtime keeps zero procedural generation and avoids per-family texture/material churn.

Exact Microseconds saved: Runtime remains 0 us/frame. The concrete runtime protection is avoiding accidental uncompressed/readable atlas drift and stale mesh bloat. Editor-side gain is reduced batchmode UI cleanup/progress overhead; exact microseconds not profiled.

Verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).` Mesh scan found TubeCoral `50/50/50`, Kelp `100/100/100`, PorousRock `50/50/50`, all `Other=0`. Prefab scan found `Prefabs=200`, `MaterialRefs=600`, `MeshColliders=50`, `CollisionChildren=50`.

## 2026-05-14 Exact Mesh Reference Contract Pass

What was wrong: Counts alone could not prove identity. A prefab could theoretically contain three LOD renderers and the project could contain 600 mesh assets while one prefab referenced a stale or cross-family mesh from a prior bake.

What was done: Added exact prefab-to-mesh validation in `ShallowsBioForgeBatchBaker`: every prefab stem must reference `MeshRoot/{Family}/{Stem}_LOD0.asset`, `_LOD1.asset`, and `_LOD2.asset` through its LOD renderers. This is source-only validation; generated content did not need a re-bake.

Cinematic Cheats used: Preserved the deterministic static mesh library, exact LOD payload cheat, shared atlas cheat, and rock-only collider cheat. The change prevents asset drift from undermining those cheats.

Exact Microseconds saved: Runtime remains 0 us/frame. The gain is preventing accidental wrong-LOD or cross-family geometry from entering low-tier prefabs, which protects LOD2 geometry budgets. Exact runtime microseconds not profiled because this is validator coverage.

Verification: `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `Build succeeded. 0 Warning(s). 0 Error(s).` A GUID scan checked all 200 prefabs against their expected three mesh `.meta` GUIDs and found `BadReferenceCount=0`.

## 2026-05-14 Rule Asset Contract Lockdown

What was wrong: The validator proved the generated payload but did not prove the source `BioRuleData` assets still matched the Safe Shallows bake contract. A drifted axiom, SDF profile, LOD budget, output folder, ribbon width, or porous rock pore setting could survive until the next bake.

What was done: Added required-folder validation and exact rule-asset validation to `ShallowsBioForgeBatchBaker`. The validator now locks `Rule_Shallows_TubeCoral.asset`, `Rule_Shallows_Kelp.asset`, and `Rule_Shallows_PorousRock.asset` to the intended prefixes, material, axioms, one exact `F` replacement, SDF profiles, iteration/branch/SDF budgets, LOD budgets, ribbon scales, porous rock values, and deterministic output folders. The current live `CURRENT_BATCH.md` no longer contains this agent ID, so that drift was recorded and the preserved status/rationale prompt memory was used.

Cinematic Cheats used: No runtime simulation added. This protects the existing offline SDF mesh cheat, shared atlas cheat, vertex color mask cheat, static LOD cheat, and rock-only convex collision cheat by locking the source rule assets that future bakes depend on.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes allocation. The saved time is risk avoidance: no accidental high-budget rule setting can inflate future LOD meshes for low-tier devices without validation failure. Editor-only validation cost is cold-path only; exact microseconds not profiled.

Verification: Unity Bee response-file Roslyn compile for `Hecton8.Editor.ProceduralGen` exited 0. Literal rule asset scan passed: `RuleAssetScan=PASS Rules=3 ContractFields=25`. `git diff --check` exited 0 with only the repo LF-to-CRLF warning. Full `dotnet build Hecton8.Core.csproj` is currently blocked outside this flora domain by `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs` errors for missing `BufferReader.CanConsumeCollectionItems` and `ReadCustomArray` overloads.

## 2026-05-14 Shared Material Visual Contract Lockdown

What was wrong: The Safe Shallows validator locked generated prefabs, meshes, rule assets, atlas bindings, importer settings, instancing, and GI flags, but it still allowed shared material scalar/color drift. A changed tint, emission value, cull mode, or `_QUALITY_HIGH` keyword could damage the intended underwater read or raise shader cost without failing validation.

What was done: Added exact material contract validation in `ShallowsBioForgeBatchBaker` for `_BaseColor`, `_RootTint`, `_TipTint`, `_EmissionColor`, `_TriplanarScale`, `_TriplanarSharpness`, `_SeedOffsetScale`, `_NormalScale`, `_AmbientStrength`, `_SubsurfaceStrength`, `_RimStrength`, `_SmoothnessBoost`, `_MetallicBoost`, `_BiomeTintStrength`, `_EmissionStrength`, `_BiolumPulseSharpness`, `_MatCapStrength`, `_Cull`, and `_QUALITY_HIGH` disabled.

Cinematic Cheats used: No runtime simulation added. The pass protects the existing shared material cheat, triplanar atlas cheat, MatCap fake-lighting cheat, vertex color mask cheat, static LOD cheat, and rock-only convex collision cheat.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes allocation. The change prevents hidden shader keyword/scalar drift from spending GPU budget on low-end hardware or weakening bioluminescent readability. Exact runtime microseconds not profiled because the code executes only in editor validation.

Verification: Unity Bee response-file Roslyn compile for `Hecton8.Editor.ProceduralGen` exited 0. Material scan passed `MaterialContractScan=PASS Fields=26`. `git diff --check` exited 0 with only the repo LF-to-CRLF warning. Full `dotnet build Hecton8.Core.csproj --no-restore` failed outside this flora domain on missing generated `Temp/obj/Hecton8.Core/.NETStandard,Version=v2.1.AssemblyAttributes.cs`; restore-enabled retry timed out and the timed-out build process is no longer running.

## 2026-05-15 LOD And Transform Contract Lockdown

What was wrong: Safe Shallows validation still left several prefab invariants as implicit Unity state: LODGroup crossfade mode, screen-relative transition heights, fade widths, root identity transform, LOD child names, LOD child rotation/scale, and rock collision proxy transform identity. A manual prefab edit could create visible LOD popping, culling drift, or collision/render mismatch without failing the prior validator.

What was done: Extended `ShallowsBioForgeBatchBaker` validation to lock root transforms, LODGroup crossfade settings, exact LOD thresholds, exact fade widths, LOD child names, LOD child rotation/scale, and the `Collision_LOD2` proxy transform contract.

Cinematic Cheats used: Static offline SDF/L-system meshes remain the truth. The pass protects the LOD crossfade cheat, vertex-mask shader cheat, shared material/atlas cheat, no-flora-collision cheat, and rock-only convex collision proxy cheat.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes allocation. The saved cost is avoided regression: no accidental scaled LOD child, disabled crossfade, or malformed collision proxy can force runtime correction casts, extra scripts, or visible transition cleanup later. Exact runtime microseconds not profiled because the validator executes only in editor.

Verification: No dotnet rebuild was run by user order. `git diff --check` passed for the touched source. Source scans found the new validators and no scoped hot-path debt patterns in ProceduralGen. Prefab YAML scans found TubeCoral=50, Kelp=100, PorousRock=50 with `BadLodTransitionYaml=0`, `BadTransformYaml=0`, and PorousRock `BadCollisionNameYaml=0`. Project-wide numeric H-Phi is not claimed; local H-Phi evidence improved by increasing editor contract density without adding runtime ownership or Update cadence.

## 2026-05-15 Static Batching Flag Guard

What was wrong: Generated Shallows prefabs were instancing/GPU-resident-friendly, but the validator did not reject Unity static editor flags. A future `BatchingStatic` flag would conflict with the shared-material flora draw strategy and can add static-batching memory cost.

What was done: Added `ValidateStaticFlagsContract` to `ShallowsBioForgeBatchBaker`. It walks every transform under each generated prefab and fails validation on any nonzero `StaticEditorFlags`.

Cinematic Cheats used: Static authored meshes, shared atlas/material, LOD crossfade, shader vertex-color masks, no flora collision, and rock-only convex collision proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes allocation. The change prevents hidden static-batching memory/draw-path regression; exact runtime microseconds not profiled because validation is editor-only.

Verification: No dotnet rebuild was run. `git diff --check` passed for source/docs with CRLF warnings only. Source scan found `ValidateStaticFlagsContract`; prefab YAML scans found `BadStaticFlagsYaml=0` for all 200 generated prefabs. No global H-Phi score is claimed.

## 2026-05-15 Mesh Geometry And LOD Budget Contract Lockdown

What was wrong: The Safe Shallows validator still trusted mesh payload internals once the path/reference was correct. That left room for empty meshes, extra submeshes, UInt32 index drift, degenerate bounds, LOD budget drift, or missing LOD1/LOD2 vertex-color masks to survive until runtime visual/culling cost appeared.

What was done: Added centralized family LOD budget constants to `ShallowsBioForgeBatchBaker` and reused them for rule authoring, rule validation, and prefab mesh validation. Added `ValidateMeshGeometryContract`, `ValidateLodTriangleBudget`, and all-LOD vertex color R gradient checks. Every LOD mesh now has to be non-empty, one submesh, UInt16, finite/non-degenerate, within its family budget, and shader-mask ready.

Cinematic Cheats used: Static offline SDF/L-system meshes, exact LOD payloads, shared atlas/material, vertex-color shader masks, no flora collision, and rock-only convex collision remain unchanged. The pass makes those cheats harder to corrupt without adding runtime systems.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. The saved cost is avoided regression: no accidental high-index-format mesh, over-budget LOD, or missing shader mask can silently force heavier geometry or runtime correction. Exact runtime microseconds were not profiled because validation is editor-only.

Verification: No dotnet rebuild was run. `git diff --check` passed for the touched source with only CRLF warnings. Source scans found the mesh/budget validators and all-LOD vertex color validation. Brace count is balanced. Mesh YAML scan found `Count=600`, `Bad=0`; max triangles were Kelp `2200/514/94`, PorousRock `3081/581/53`, TubeCoral `2364/342/24`.

## 2026-05-15 Vertex Color Validator Allocation Clamp

What was wrong: The strengthened all-LOD vertex color validator used `mesh.colors`, which copies a managed `Color[]` every time a mesh is checked. It is editor-only, but validating 600 generated meshes should still use bounded memory behavior.

What was done: Added a single bounded reusable `List<Color>` scratch buffer to `ShallowsBioForgeBatchBaker`, sized from the largest accepted LOD mesh budget. Replaced `mesh.colors` with `mesh.GetColors(VertexColorScratch)`, added a capacity guard, cleared the scratch buffer after use, used branch min/max comparisons, and made readable mesh data an explicit validator requirement while vertex color inspection is required.

Cinematic Cheats used: Static offline SDF/L-system meshes, vertex-color shader masks, shared material/atlas, LOD payloads, no flora collision, and rock-only convex collision remain unchanged. This pass improves the editor validation path that protects those cheats.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. Editor validation avoids one transient copied color array per checked LOD mesh. Exact editor microseconds were not profiled.

Verification: No dotnet rebuild was run. `git diff --check` passed with only CRLF warnings. Source scan found `VertexColorScratch`, `mesh.GetColors`, `mesh.isReadable`, and no `mesh.colors`. Brace count is balanced. Mesh YAML scan found `Count=600`, `Bad=0`, `MaxVertices=9243`, `ScratchCapacity=9600`.

## 2026-05-15 Shader Source Contract And Fail-Closed Bake

What was wrong: The Shallows material contract validated shader name and material values, but not the exact shader asset path or the source tokens that keep the shader opaque, SRP-batcher-friendly, instanced, LOD-crossfade-compatible, and math-LOD capable. The bake path also had a `Shader.Find` fallback.

What was done: Added `ShaderPath` as the single shader authority, removed the `Shader.Find` fallback, aborted the bake when the authored shader asset is missing, and added source-token validation for required opaque/render-state/instancing/LOD/SRP-batcher tokens plus forbidden alpha-blend and `ZWrite Off` tokens.

Cinematic Cheats used: Opaque static flora meshes, shader math LOD, LOD crossfade, shared atlas/material, vertex-color masks, no flora collision, and rock-only convex collision remain unchanged. This pass protects the shader fake that carries visual richness without runtime simulation.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. The saved cost is avoided regression: no silent alpha blend, missing instancing, or lost crossfade path can add overdraw or draw-path churn later. Exact runtime microseconds were not profiled.

Verification: No dotnet rebuild was run. `git diff --check` passed. Source scan found `ValidateShaderSourceContract`, `ShaderPath`, and no `Shader.Find` in the Shallows baker. Shader token scan returned `Missing=0`, `ForbiddenHits=0`; brace count is balanced.

## 2026-05-15 Validator Path And Readability Fail-Fast

What was wrong: Shader source validation resolved `Assets/...` through process current directory, which is weaker than deriving the project root from Unity. Vertex color validation also lacked its own non-readable mesh guard and could reach `GetColors` after another validator had already reported the readability failure.

What was done: Added `ResolveProjectAssetAbsolutePath` using `Application.dataPath`, switched shader source reads to that path, added a `mesh.isReadable` early return in `ValidateVertexColorGradient`, and normalized the touched cold-allocation comment to ASCII style.

Cinematic Cheats used: No runtime change. Static offline L-system/SDF meshes, shader masks, opaque math-LOD shader path, shared atlas/material, no flora collision, and rock-only convex collision remain the visual-fake strategy.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This is validation reliability work; exact editor microseconds were not profiled.

Verification: No dotnet rebuild was run. `git diff --check` passed for the touched source. Source scans found `ResolveProjectAssetAbsolutePath`, no Shallows `Shader.Find`, no `mesh.colors`, and `NonAscii=0`. Mesh readability YAML scan found `Count=600`, `Bad=0`, `MaxVertices=9243`, `ScratchCapacity=9600`; shader token scan returned `Missing=0`, `ForbiddenHits=0`.

## 2026-05-15 Atlas Asset Dimension Contract

What was wrong: The validator locked atlas importers and material bindings but did not verify that the actual texture assets are the expected paths and exact `1024x1024` source payloads.

What was done: Added `ValidateAtlasTextureAsset` for Albedo, Normal, ORM, and MatCap. Each loaded atlas must be a `Texture2D`, resolve to the exact expected path, and report `1024x1024` dimensions before importer checks run.

Cinematic Cheats used: Shared atlas/material, triplanar shader projection, MatCap fake lighting, vertex-color masks, static LOD payloads, no flora collision, and rock-only convex collision remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents texture-size or path drift from silently increasing VRAM or weakening visual sampling; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild was run. `git diff --check` passed. Source scan found `ValidateAtlasTextureAsset`; brace count is balanced and source `NonAscii=0`. PNG IHDR scan found all four atlases are `1024x1024`, `AtlasPngDimensionScan Count=4 Bad=0`.

## 2026-05-15 Prefab Envelope And Vertex Stream Contract

What was wrong: Generated Shallows prefabs could still drift by gaining extra components/children, renderers could carry extra material slots while the first material remained valid, and meshes could keep correct paths/triangle counts while losing normals, vertex colors, or UV0 required by the procedural bio shader.

What was done: Added material asset identity/default render-queue validation, exact flora/rock prefab hierarchy and component envelope validation, enabled-renderer validation, single shared-material-slot validation through reusable `GetSharedMaterials` scratch, and mesh vertex stream checks for `Position`, `Normal`, `Color`, and `TexCoord0`.

Cinematic Cheats used: Static L-system/SDF meshes, shared atlas/material, triplanar projection, vertex-color height masks, opaque math LOD, no flora collision, and rock-only convex collision remain the protected visual-fake strategy.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. Prevented cost class is hidden renderer/component/material-slot drift; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild was run. `git diff --check` passed. Source scan found `ValidateMaterialAssetContract`, `ValidatePrefabHierarchyContract`, `ValidateComponentEnvelope`, `ValidateRendererMaterialContract`, and `HasVertexAttribute`; forbidden scan found no Shallows `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, or hot-path update methods. `PrefabEnvelopeYamlScan Count=200 Bad=0`; material YAML confirms `m_CustomRenderQueue: -1`, instancing enabled, empty keyword arrays; `MeshVertexChannelYamlScan Count=600 Bad=0 MaxNonZeroChannels=5`.

## 2026-05-15 Material Sampling State Contract

What was wrong: The Shallows material locked atlas references and shader values, but not atlas texture scale/offset or serialized keyword arrays. That left a silent path for triplanar sampling drift and unintended shader variants.

What was done: Added explicit identity atlas texture transforms during material creation, validation for identity scale/offset on all four atlases, validation for empty serialized `m_ValidKeywords` and `m_InvalidKeywords`, and a `Vector2` approximate comparison helper.

Cinematic Cheats used: Shared atlas/material, triplanar projection, vertex-color height masks, opaque math LOD, no flora collision, static LOD meshes, and rock-only convex collision remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents asset drift from creating shader variant spread or sampling mismatch; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild was run. `git diff --check` passed. Source scan found `SetMaterialTextureTransform`, `ValidateMaterialTextureTransform`, serialized keyword checks, and `Approximately(Vector2)`; forbidden scan found no Shallows `Shader.Find`, `mesh.colors`, `renderer.sharedMaterial`, `.material`, or hot-path update methods. `MaterialTextureTransformYamlScan Props=4 Bad=0 ValidKeywordEmpty=1 InvalidKeywordEmpty=1 DefaultQueue=1`.

## 2026-05-15 Atlas Import Metadata Alignment

What was wrong: Shallows atlas PNGs are `1024x1024`, but their importer metadata still had top-level `maxTextureSize: 2048`, Default/Standalone `maxTextureSize: 512`, and ORM imported as sRGB.

What was done: Patched the four Shallows atlas `.png.meta` files so top-level, DefaultTexturePlatform, and Standalone max size are `1024`; kept Standalone BC7 for Albedo/ORM/MatCap and BC5 for Normal; corrected ORM to linear import.

Cinematic Cheats used: Shared 1024 atlases, compressed PC formats, triplanar shader projection, MatCap fake lighting, vertex-color masks, opaque math LOD, and static LOD payloads remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents silent Standalone downsample and gamma-space ORM errors; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `AtlasImporterYamlScan Count=4 Bad=0`; `AtlasPngDimensionScan Count=4 Bad=0`; `git diff --check` passed for the edited atlas meta files.

## 2026-05-15 Rock Collision Proxy Mesh Contract

What was wrong: Rock prefabs validated one convex collider and transform alignment, but did not prove the collider was enabled, non-trigger, or using the exact LOD2 mesh as the cheap physics proxy.

What was done: Strengthened `ValidateRockCollider` to require exactly one enabled, non-trigger, convex collider whose `sharedMesh` equals the resolved LOD2 renderer mesh.

Cinematic Cheats used: Rock collision stays a coarse LOD2 convex proxy. Flora remains visual-only with no colliders. No runtime physics correction, collider generation, or per-asset script was added.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents hidden collider mesh bloat or disabled/trigger-only proxy drift; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `RockColliderLod2GuidYamlScan Count=50 Bad=0`; `ShallowsColliderCountYamlScan Count=200 Bad=0`; `git diff --check` passed for the baker; source forbidden scan stayed clean.

## 2026-05-15 Prefab Activation And LODGroup State Contract

What was wrong: Generated Shallows prefabs could keep correct hierarchy, components, renderer flags, material slots, mesh references, static flags, and collider proxies while hidden GameObject state drift made a child inactive, moved it to a non-default layer, retagged it, or disabled the root `LODGroup`.

What was done: Added `DefaultLayer`, `UntaggedTag`, and `ValidateGameObjectStateContract` to `ShallowsBioForgeBatchBaker`; every generated transform must now be active, layer `0`, and tagged `Untagged`. Added an explicit `LODGroup.enabled` validation branch before existing crossfade and transition checks.

Cinematic Cheats used: Static offline L-system/SDF prefabs, MeshRenderer-owned GPU Resident Drawer-friendly objects, shared atlas/material, vertex-color masks, dithered LOD crossfade, no flora colliders, and LOD2 convex rock proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents future runtime fix-up scripts, wrong-layer collision/filtering work, invisible inactive payloads, and disabled LOD switching from entering the asset library. Exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `PrefabGameObjectStateYamlScan Count=200 Bad=0`; `LodGroupEnabledYamlScan Count=200 Bad=0`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

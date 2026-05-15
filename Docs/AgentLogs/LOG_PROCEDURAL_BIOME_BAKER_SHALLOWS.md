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

## 2026-05-15 LODGroup Bounds And Scratch Allocation Hygiene

What was wrong: The baker still had generic scratch-list allocation comments for prefab/component/material validation, and `LODGroup` bounds data was trusted implicitly even though zero-size or non-finite bounds would break LOD switching without changing renderer or material counts.

What was done: Converted scratch-list comments to explicit capacity/reason/owner cold-allocation annotations, widened the small scratch capacities for corrupted prefab inspection, and added finite positive `LODGroup.size` plus finite `localReferencePoint` validation.

Cinematic Cheats used: Static authored LOD prefabs, dithered crossfade, shader masks, shared atlas/material, no flora colliders, and rock-only LOD2 convex proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents invalid LOD bounds or ambiguous editor scratch ownership from entering the Shallows asset library; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `LodGroupBoundsYamlScan Count=200 Bad=0 MinSize=1.665000 MaxSize=18.835001`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Mesh Bounds Budget Contract

What was wrong: Mesh geometry validation allowed finite but oversized bounds. That can keep flora visible too long, break LOD residency assumptions, and reduce culling efficiency while triangle budgets and mesh references still look correct.

What was done: Added family-specific max mesh bounds extent-squared budgets and `TryResolveMaxBoundsExtentSq`. `ValidateMeshGeometryContract` now receives the family name and rejects zero, non-finite, or over-budget mesh bounds.

Cinematic Cheats used: Static authored LOD meshes, dithered crossfade, shader masks, shared atlas/material, no flora colliders, and rock-only LOD2 convex proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents malformed bounds from inflating renderer visibility and LOD residency; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `MeshBoundsBudgetYamlScan TotalBad=0`; family maxima were Kelp `93.313505/121`, TubeCoral `2.168438/4`, PorousRock `5.143031/9`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Mesh Topology And Vertex Budget Contract

What was wrong: Generated mesh validation allowed a payload with valid triangle counts but unused vertex-buffer bloat, malformed index count divisibility, or non-triangle topology.

What was done: Hardened mesh geometry validation to reject non-triangle topology and index counts not divisible by three, and added `ValidateLodVertexBudget` so every LOD mesh must stay inside a vertex ceiling derived from its triangle budget.

Cinematic Cheats used: Static authored LOD meshes, dithered crossfade, shader masks, shared atlas/material, no flora colliders, and rock-only LOD2 convex proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents hidden vertex-buffer bloat and malformed topology from entering the Shallows mesh library; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `MeshVertexIndexYamlScan TotalBad=0`; maxima were Kelp `6600/1542/282`, TubeCoral `7092/1026/72`, PorousRock `9243/1743/159`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Prefab Deterministic Name Contract

What was wrong: Generated Shallows prefabs could keep valid counts, hierarchy, materials, meshes, LODs, and renderer flags while the asset stem or root name drifted away from the deterministic BioForge family/index/kind/hash identity.

What was done: Added `ValidatePrefabNameContract` to `ShallowsBioForgeBatchBaker`, with family-specific prefix/kind resolution and direct ordinal checks for three decimal index digits, separators, kind text, and eight uppercase hex hash digits. The validator also requires root `GameObject.name` to equal the prefab file stem.

Cinematic Cheats used: Static offline L-system/SDF prefabs, MeshRenderer-owned GPU Resident Drawer-friendly data, shared atlas/material, vertex-color masks, dithered LOD crossfade, no flora colliders, and rock-only LOD2 convex proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents runtime lookup/repair work and QA ambiguity from malformed prefab identity; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `PrefabNameContractYamlScan Count=200 Bad=0`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and case-sensitive forbidden source scan stayed clean.

## 2026-05-15 Mesh Asset Name Contract

What was wrong: Shallows mesh references were locked by path, but the internal `Mesh.name` field could drift away from the deterministic `<prefab-stem>_LOD#` identity while the file reference still passed.

What was done: Extended `ValidatePrefabMeshReferences` so each resolved LOD mesh object name must match the prefab stem and LOD index.

Cinematic Cheats used: Static offline L-system/SDF meshes, strict LOD payload budgets, shared atlas/material, vertex-color masks, dithered LOD crossfade, no flora colliders, and rock-only LOD2 convex proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents runtime repair/lookup logic and editor ambiguity from stale mesh object names; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `MeshNameContractYamlScan Count=600 Bad=0`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and case-sensitive forbidden source scan stayed clean.

## 2026-05-15 Family Index Completeness Contract

What was wrong: Shallows family counts could pass while one variation index was missing and another index was duplicated under a different hash.

What was done: Added `ValidateFamilyIndexContract` with a fixed `bool[100]` scratch buffer and three-digit index parser. Each family must now cover exactly `000..expectedCount-1` once.

Cinematic Cheats used: Static offline L-system/SDF prefab libraries, strict deterministic variation identity, shared atlas/material, dithered LOD crossfade, no flora colliders, and rock-only LOD2 convex proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents runtime registry/fallback logic and dressing-set holes from missing or duplicated variation indices; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `FamilyIndexContractYamlScan Count=200 Bad=0`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and case-sensitive forbidden source scan stayed clean.

## 2026-05-15 Mesh LOD Triplet Completeness Contract

What was wrong: Mesh family validation counted total LOD suffixes but did not prove that each generated variation had exactly one LOD0, LOD1, and LOD2 mesh.

What was done: Added `ValidateMeshLodIndexContract`, `TryParseMeshLodStem`, and a fixed `bool[300]` scratch buffer so mesh triplets are validated by family, variation index, and LOD index.

Cinematic Cheats used: Static offline L-system/SDF mesh triplets, strict deterministic LOD identity, shared atlas/material, dithered LOD crossfade, no flora colliders, and rock-only LOD2 convex proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents runtime fallback or renderer mutation for missing/duplicate LOD mesh slots; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `MeshLodIndexContractYamlScan Count=600 Bad=0`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and case-sensitive forbidden source scan stayed clean.

## 2026-05-15 Renderer Serialized State Contract

What was wrong: Renderer public flags were validated, but serialized render-layer, renderer priority, probe anchor, LPPV override, sorting, and static-shadow-caster fields could still drift.

What was done: Added `ValidateRendererSerializedStateContract` and serialized property helpers. Every generated Shallows MeshRenderer now has default render layer, priority, probe, sorting, and static shadow caster state enforced.

Cinematic Cheats used: Static MeshRenderer-owned flora/rock payload, shared atlas/material, dithered LOD crossfade, no runtime renderer repair, no flora colliders, and rock-only LOD2 convex proxies remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents runtime renderer fix-up and hidden probe/render-layer work; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `RendererSerializedStateYamlScan Renderers=600 Bad=0`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and case-sensitive forbidden source scan stayed clean.

## 2026-05-15 Shader Pass Budget Contract

What was wrong: The shared Shallows shader source contract did not lock explicit pass count or reject inherited/grab/fallback passes.

What was done: Added `ValidateShaderPassBudget` and direct line-token counting. The shader must keep exactly `ForwardLit` and `ShadowCaster`, with zero `UsePass`, `GrabPass`, or `Fallback`.

Cinematic Cheats used: Opaque shared material, dithered LOD crossfade, GPU Resident Drawer-friendly MeshRenderers, and no runtime material mutation remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents future hidden shader pass bloat; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `ShaderPassBudgetScan Pass=2 UsePass=0 GrabPass=0 Fallback=0 Forward=True Shadow=True`; `git diff --check` passed for the baker with only the repo CRLF warning; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Shader Pragma Budget Contract

What was wrong: Shader pass count was locked, but pragma directives could still silently expand shader variants.

What was done: Added `ValidateShaderPragmaBudget` and direct token counting for exact pragma counts, including instancing, fog, main-light shadows, LOD fade, Math LOD, local high-quality, skip variants, and zero additional-light multi_compile.

Cinematic Cheats used: Bounded opaque shader variants, dithered LOD crossfade, shared material/atlas, and no runtime shader warmup compensation remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents future shader variant fan-out and warmup/build pressure; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. Shader pragma token counts matched the locked budget; `git diff --check` passed for the baker; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 BioRule Raw Serialization Contract

What was wrong: Public `BioRuleData` getters could hide raw serialized asset drift through clamps or defaults.

What was done: Extended `ValidateRuleAsset` to validate raw serialized rule fields directly, including identity, material, axiom, generation numbers, SDF profile, LOD budgets, rock settings, and output folders.

Cinematic Cheats used: Static offline L-system/SDF generation remains the contract. No runtime rule normalization, no runtime mesh generation, and no renderer repair were added.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents future heavier/malformed rebakes from hidden raw rule drift; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `RuleRawSerializedYamlScan Count=3 Bad=0`; `git diff --check` passed for the baker; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 BioRule Folder Exactness Contract

What was wrong: Extra Shallows `BioRuleData` assets could exist beside the three canonical bake rules without failing validation.

What was done: Added `ValidateRuleFolderContract`, requiring exactly the TubeCoral, Kelp, and PorousRock rule assets and zero unexpected rule payloads in the Shallows rule folder.

Cinematic Cheats used: Static offline bake-input discipline remains the contract. No runtime rule filtering, runtime bake path, or destructive folder cleanup was added.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents stale rule assets from producing non-canonical generated payloads; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `RuleFolderExactnessYamlScan Count=3 Bad=0`; `git diff --check` passed for the baker; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Shallows Atlas And Material Folder Exactness Contract

What was wrong: Extra Shallows-named atlas textures or shared materials could exist beside the canonical payloads without failing validation.

What was done: Added `ValidateAtlasFolderContract` and `ValidateMaterialFolderContract`, requiring exactly four Shallows atlas textures and exactly one Shallows shared material.

Cinematic Cheats used: One shared material, four shared atlases, opaque shader path, dithered LOD crossfade, and no runtime material/texture fallback remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents stale assets from splitting batching or VRAM assumptions; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `AtlasFolderExactnessScan Count=4 Bad=0`; `MaterialFolderExactnessScan Count=1 Bad=0`; `git diff --check` passed for the baker; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Shader Pragma Line-Token Hardening

What was wrong: Shader pragma budget validation used substring counts, so commented pragma text could satisfy the contract.

What was done: Switched all pragma budget checks to `CountShaderLineToken` and removed the broad source-token counter.

Cinematic Cheats used: Bounded shader variant budget, opaque shared material, dithered LOD crossfade, and no runtime shader compensation remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents hidden shader-source validation false positives; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. Shader line-token pragma scan matched the locked budget, `CountSourceToken Count=0`, `git diff --check` passed for the baker, and forbidden source scan stayed clean.

## 2026-05-15 Mesh And Prefab Family Subfolder Exactness Contract

What was wrong: Extra generated-family subfolders could exist under Shallows mesh or prefab roots while the three canonical families still validated.

What was done: Added `ValidateFamilySubfolderContracts`, requiring exactly TubeCoral, Kelp, and PorousRock under both generated mesh and prefab roots.

Cinematic Cheats used: Static offline generated families remain the contract. No runtime family filtering, registry repair, or destructive asset cleanup was added.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents stale generated families from adding hidden mesh/material/VRAM load; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `FamilySubfolderExactnessScan Bad=0`, `git diff --check` passed for the baker, source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Shared Material Serialized Payload Envelope

What was wrong: Public material checks did not reject hidden serialized payload drift such as material inheritance, disabled shader passes, build texture stacks, extra saved properties, or raw instancing/GI flag drift.

What was done: Added `ValidateMaterialSerializedPayloadContract`, `SerializedBoolEquals`, and `SerializedArraySizeEquals`. The shared material now requires a flat non-inherited serialized envelope with exact saved texture/float/color array counts and no disabled pass/build-stack payload.

Cinematic Cheats used: One opaque shared material, four shared atlases, dithered LOD crossfade, and no runtime material normalization remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents hidden material payload drift from splitting render state or forcing runtime repair; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `MaterialSerializedPayloadYamlScan TexEnvs=4 Floats=14 Colors=4 Bad=0`; `git diff --check` passed with only repo CRLF warnings; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Shared Material Saved Property Key Contract

What was wrong: The material envelope checked saved-property array sizes but not the exact serialized texture/float/color keys.

What was done: Added fixed cold key arrays and `SerializedSavedPropertyKeysEqual`, requiring exact saved-property keys for the Shallows shared material payload.

Cinematic Cheats used: One opaque shared material, four shared atlases, bounded shader properties, and no runtime material normalization remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents stale serialized material keys from forcing runtime repair or material clones; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `MaterialSavedPropertyKeyYamlScan Tex=_AlbedoAtlas,_MatCap,_NormalAtlas,_ORMAtlas Floats=14 Colors=_BaseColor,_EmissionColor,_RootTint,_TipTint Bad=0`; `git diff --check` passed with only repo CRLF warnings; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Atlas Streaming And Alpha Importer Contract

What was wrong: Atlas importer validation did not explicitly lock mip streaming or alpha-transparency import flags.

What was done: Set and validate `streamingMipmaps=false`, `streamingMipmapsPriority=0`, and `alphaIsTransparency=false` in the Shallows atlas importer contract.

Cinematic Cheats used: Fixed 1024 shared atlases, mipmapped bilinear sampling, BC7/BC5 Standalone compression, and no runtime texture streaming policy remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents hidden texture residency or alpha-import drift; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `AtlasStreamingAlphaMetaScan Count=4 Bad=0`; `git diff --check` passed with only repo CRLF warnings; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Atlas Bake Pixel Scratch Reuse

What was wrong: Each atlas bake allocated a fresh 1024x1024 `Color32[]`, creating four large transient editor arrays per full Shallows atlas bake.

What was done: Added one documented cold `AtlasPixelScratch` buffer and reused it for Albedo, Normal, ORM, and MatCap atlas writes.

Cinematic Cheats used: Procedural atlas fakes, fixed 1024 atlas size, mipmapped bilinear sampling, and no runtime atlas generation remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. Bake-time allocation prevention is approximately three avoided 4 MiB transient arrays per full four-atlas bake after the first cold scratch allocation; exact editor microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `AtlasPixelScratchSourceScan LocalAlloc=0 ColdAlloc=1 Writes=1 SetPixels=1`; `git diff --check` passed with only repo CRLF warnings; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Atlas Default Platform Override Contract

What was wrong: DefaultTexturePlatform could be manually overridden or assigned a non-automatic format while the existing importer checks still passed.

What was done: Set and validate `DefaultTexturePlatform.overridden=false` and `DefaultTexturePlatform.format=Automatic` for every Shallows atlas.

Cinematic Cheats used: Standalone BC7/BC5 atlas compression, fixed 1024 atlas size, and no runtime texture policy correction remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents hidden platform import drift; exact runtime microseconds were not profiled.

Verification: No dotnet rebuild and no Unity import was run. `AtlasDefaultPlatformMetaScan Count=4 Bad=0`; `git diff --check` passed with only repo CRLF warnings; source brace balance stayed `Delta=0`, `NonAscii=0`, and forbidden source scan stayed clean.

## 2026-05-15 Atlas Compression Quality And Alpha Split Contract

What was wrong: Compression quality and platform alpha-splitting flags were present in atlas meta files but were not owned by the baker contract.

What was done: Added `AtlasCompressionQuality=50` and validation for global/Default/Standalone compression quality plus Default/Standalone `allowsAlphaSplitting=false`.

Cinematic Cheats used: Fixed 1024 procedural atlas fakes, Standalone BC7/BC5 compression, no alpha split, and no runtime texture correction remain unchanged.

Exact Microseconds saved: Runtime remains 0 us/frame and 0 bytes procedural allocation. This prevents hidden import-quality or alpha-split drift; exact runtime microseconds were not profiled.

Verification: Pending static verification. No dotnet rebuild and no Unity import will be run.

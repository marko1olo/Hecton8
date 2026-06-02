# Rationale 1610 - FAUNA_SKINNING_AND_IK_SKELETON_FORGER

Status: APEX SOURCE HARDENED; CROSS-FILE AST GRAPH, HOT STRING/LINQ/DELEGATE/FOREACH-GC DETECTION, PRESET-AWARE H8LR GATING, UI BONE CLAMP, FUZZER ASSERTION, AND LATE-FRAME SHADER CLEAR PROXY ADDED; UNITY VALIDATION BLOCKED BY HOST CONTENTION

## Mandate Selection

Problem: Offline fauna rigging touches mesh topology, Burst jobs, unmanaged DTOs, VAT textures, and mobile GPU bone limits.
Solution: Use the following mandates as active constraints before coding:
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt: runtime hot paths must remain 0 B; all generator code is Editor-only.
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt: Burst jobs must be unmanaged, tracked, and data-local; no hidden managed references.
- DATA_Runtime_Struct_Layout_ARM64.txt: spine metadata DTOs must be unmanaged, aligned, and multiple-of-8 sized.
- REND_GPU_Driven_Animation_VAT.txt: swarm fish route is VAT, not SkinnedMeshRenderer or Animator.
- ANIM_IK_FABRIK_GroundSnapping_Procedural.txt: spine/appendage metadata must support solver-friendly lengths, constraints, and stable rest poses.
Rejected Alternatives: Manual Unity Animator clips and hand-painted weights. They create CPU/runtime cost and do not scale to large fauna or swarms.
Scalability potential: Low uses few bones or VAT silhouettes; middle keeps reduced spine chains; high increases VAT frames and wrinkle masks; ultra spends saved CPU on richer shader response and longer LOD residency.
Hardware Impact: i3/MX350 gains come from removing runtime rig generation and Animator evaluation; expected runtime savings are asset-dependent and require profiler proof.

## Decision 001 - Editor-Only Boundary

Problem: The assignment asks for complex rigging, but runtime architecture forbids generator work in Play Mode.
Solution: Place generator scripts under `Assets/_Project/Editor/Generators/Fauna/`; any runtime data carrier must be minimal, serializable, and safe to load without running editor math.
Rejected Alternatives: Runtime mesh analysis, runtime skeleton generation, or runtime VAT generation. Standard Unity approach is too slow and allocates during gameplay.
Scalability potential: Generated assets can bake multiple quality lanes without changing runtime truth ownership.
Hardware Impact: Low-end silicon avoids CPU spikes from import-time math during gameplay; high-tier devices use richer precomputed data.

## Decision 002 - Missing Raw Mesh Folder Is A Hard Fact

Problem: The assignment requires analyzing `Assets/_Project/Art/Fauna/Raw`, but that folder does not exist and no FBX/OBJ/Mesh fauna sources were found under `Assets/_Project/Art`.
Solution: Kept the scanner but removed report-file output; the generator now emits a cold Unity console summary and avoids synthetic proof files.
Rejected Alternatives: Generating fake creature records or pretending prefab output exists. That would poison downstream rig validation.
Scalability potential: Low, middle, high, and ultra lanes remain data-driven once real meshes arrive; no code path assumes a fixed species list.
Hardware Impact: i3/MX350 gain is deferred until assets exist; current gain is avoiding a useless bake over nonexistent input.

## Decision 003 - Segment Distance Weighting Over Animator Skinning

Problem: Vertex deformation needs stable bending without hand-painted weights or Animator overhead.
Solution: Implemented Burst inverse-distance weighting against bone line segments, preserving up to 4 `BoneWeight1` influences and forcing the fourth weight to close the sum to 1.0f.
Rejected Alternatives: Joint-point distance causes pinching near long bones; heat diffusion over mesh adjacency needs topology graph construction and is too expensive for the first offline pass without raw meshes to validate.
Scalability potential: Low uses 4 bones or VAT, middle uses 24-bone predators, high/ultra can spend editor time on denser Leviathan chains up to 96 bones without changing runtime route.
Hardware Impact: i3/MX350 avoids Animator evaluation and receives fixed skinned assets; exact frame gain requires Unity profiler on generated prefabs.

## Decision 004 - RGBAFloat VAT First Pass

Problem: Swarm VAT must survive `<0.001f` positional error without visible jitter.
Solution: Used `TextureFormat.RGBAFloat` for the generator and added a precision assertion menu path.
Rejected Alternatives: `RGBAHalf` is cheaper in VRAM but risky before measuring fauna scale ranges; compressed formats are invalid for positional offsets.
Scalability potential: Low can downshift frame count and shader cadence, middle keeps 30 frames, high/ultra can raise frames or add harmonics through `GlobalQualityWeight`.
Hardware Impact: Low-end silicon spends more VRAM but buys 0 CPU bones for swarms; high-end devices can use the same baked data at larger school counts.

## Decision 005 - Preset Names In Output Paths

Problem: Bone audit cannot infer correct limits if generated prefab names do not identify small fish, predator, or Leviathan lane.
Solution: Prefab and mesh names now include the rig preset token before the source asset token.
Rejected Alternatives: Trusting mesh names or creating a runtime audit component from an editor assembly. Both create brittle dependency routes.
Scalability potential: All quality tiers can be scanned by filename without loading custom runtime metadata.
Hardware Impact: Prevents accidental 96-bone assets from being treated as small fish in audit logic; protects mobile GPU skinning budgets.

## Decision 006 - Existing Runtime IK Bridge Only

Problem: The prompt requests Spine-IK metadata attached to prefabs, but new runtime components would cross domain ownership.
Solution: The editor best-effort injects existing `Hecton8.AI.FaunaKinematicsRuntime` serialized fields when that component type is present and logs a cold summary instead of writing JSON metadata.
Rejected Alternatives: Creating a new runtime MonoBehaviour from the editor task or forcing a dependency on a future IK component.
Scalability potential: Low uses short segment counts, middle/high/ultra increase segment residency and constraint richness through existing runtime owners.
Hardware Impact: Avoids extra runtime parsing or hierarchy scans on i3/MX350; high-tier devices can use richer existing solver settings.

## Decision 007 - DataVault Job Lock Flattening

Problem: `ProceduralBoneBlenderRuntime` and `StressDrivenSpawnDirector` pinned many DataVault buffers with sequential `TryLockBuffer` calls for scheduled jobs. That allows partial lock acquisition and creates a deadlock vector if another system changes lock order.
Solution: Replaced the per-buffer pin masks with one mutation guard mask per scheduled job path. Existing `try/finally` completion paths still release the guard after job completion or failed scheduling.
Rejected Alternatives: Keeping lock order discipline by convention, or wrapping each lock in nested `try/finally`. Both preserve multi-lock complexity and make review brittle.
Scalability potential: Low devices avoid rare stalls from lock inversion; middle/high/ultra can schedule larger fauna batches without expanding lock count.
Hardware Impact: i3/MX350 gain is stability, not claimed frame speed; worst-case deadlock risk is reduced by removing 11-12 individual lock acquisitions per scheduled job.

## Decision 008 - Presentation Phase Discipline

Problem: `CreatureDamageManager.OnEnable()` published wound shader globals immediately after lifecycle activation, before the late-frame presentation phase.
Solution: Removed the immediate publish and left `_woundsDirty` plus late-frame registration as the transfer route. Wound globals now publish from `LateFrameTick` after simulation state settles.
Rejected Alternatives: Publishing on enable for visual freshness. It violates phase discipline and can race against settled owner transforms.
Scalability potential: Low devices avoid extra lifecycle GPU writes; high/ultra keep the same visual scar fidelity with cleaner timing.
Hardware Impact: Saves one cold lifecycle shader publish per re-enable and prevents redundant vector-array upload on weak GPUs.

## Decision 009 - Source-Only APEX Verification

Problem: The user forbids `dotnet build`, and existing `dotnet` processes were already active during verification.
Solution: Added `FaunaApexIntegratorVerifier1610`, an editor-only Roslyn AST source verifier for hot lookup, lock, and presentation-phase violations. No JSON, binary dump, or build process is used.
Rejected Alternatives: Build spam, external JSON proof, or relying on chat assertions.
Scalability potential: Verification can be run before mobile or high-end content bakes without generating extra project artifacts.
Hardware Impact: Avoids build CPU contention on the host; exact saved microseconds are not claimed because the build was intentionally not started.

## Decision 010 - AST Over Lexical Scan

Problem: A lexical method-block scanner can miss generic invocations and false-positive on strings/comments.
Solution: Switched the APEX verifier to `CSharpSyntaxTree`, `MethodDeclarationSyntax`, `InvocationExpressionSyntax`, and `FinallyClauseSyntax`, with direct call-graph reachability for presentation writes from hot methods.
Rejected Alternatives: Shell-only `rg` proof or regex-only C# parsing. They are useful triage tools but not precise enough for the APEX protocol.
Scalability potential: The verifier can scan the fauna/procedural fauna roots in editor memory and does not create artifacts.
Hardware Impact: Avoids build CPU load; editor AST parse cost is cold and bounded by source file count.

## Decision 011 - Registry Lifecycle Route Is Not Dependency Lookup

Problem: The APEX verifier must reject `GlobalRegistry.Get*` and hot registry property reads, but lifecycle registration calls are not dependency reads.
Solution: `FaunaApexIntegratorVerifier1610` now allows `GlobalRegistry.Register*`, `TryRegister*`, `Unregister*`, and `TryUnregister*` while still rejecting hot `GlobalRegistry.Get*` and registry member access.
Rejected Alternatives: Treating every `GlobalRegistry.*` call as a hot lookup. That creates false positives for late-frame registration and hides real dependency violations in noise.
Scalability potential: Low devices keep sparse registration paths without per-frame polling; high/ultra can add presentation owners without weakening DI rules.
Hardware Impact: Prevents wasted editor review time and avoids unnecessary runtime registration rewrites on i3/MX350-class hardware.

## Decision 012 - Remove Legacy Scanner Report I/O

Problem: `OOP_Movement_Scanner` in the fauna runtime file wrote `Docs/Reports/*.json` optimization reports and refreshed the AssetDatabase after a cold menu scan.
Solution: Removed the JSON builders, upsert helpers, `File.WriteAllText`, report paths, and `AssetDatabase.Refresh()`; the scanner now emits one Unity console summary.
Rejected Alternatives: Keeping report files because the scanner is editor-only. It still violates the no-report route and wastes disk I/O without improving source correctness.
Scalability potential: Source scanners remain cold tools; they do not create extra artifacts on low, middle, high, or ultra content lanes.
Hardware Impact: Removes report-file write and asset refresh churn; exact microsecond gain is host/filesystem dependent and not claimed.

## Decision 013 - VAT Texture Width Must Fail Closed

Problem: VAT baking used `vertexCount` as texture width without checking `SystemInfo.maxTextureSize`; a dense mesh could generate an invalid texture and a broken swarm prefab.
Solution: Reject VAT bakes where `vertexCount > SystemInfo.maxTextureSize` and require lower-poly swarm meshes or a skeletal preset. Also author shader contract fields on the material: `_VatEnabled`, `_VatFrameCount`, `_VatVertexCount`, playback speed, normal blend, and position scale.
Rejected Alternatives: Silent texture creation failure or tiled VAT packing without a matching shader/runtime contract. Tiling would be a broader shader change and is not justified without consuming runtime code.
Scalability potential: Low devices get small valid VAT textures; middle/high/ultra can raise mesh detail only within hardware texture limits.
Hardware Impact: Prevents invalid GPU resource creation on i3/MX350-class devices and avoids runtime material state mismatch.

## Decision 014 - Isolated Vertices Are Bake Failures

Problem: The auto-skinning job previously logged isolated vertices as if falloff was adjusted, but no second pass happened and those vertices could be root-weighted.
Solution: Use a bounds-aware influence radius and reject remaining isolated vertices. A bad mesh must not produce a "successful" rig with detached fins or scales.
Rejected Alternatives: Root fallback as a quality path. It is acceptable as an internal guard, not as final authored content.
Scalability potential: All quality tiers receive predictable deformation instead of hidden bad weights; ultra assets can spend more offline authoring time but must still satisfy the same normalization contract.
Hardware Impact: No runtime cost; prevents wasted QA/profiling on corrupted generated prefabs.

## Decision 015 - Hot Route Allocation Verification

Problem: Dependency and lock checks do not prove zero-GC phase transfer.
Solution: `FaunaApexIntegratorVerifier1610` now walks hot methods and reachable helpers for managed arrays plus managed collection/string-builder/delegate allocation syntax.
Rejected Alternatives: Trusting comments that state transfer is zero-GC. Source must be mechanically checked before Unity profiler runs.
Scalability potential: Low devices avoid allocator spikes; high/ultra presentation complexity stays bounded by preallocated transfer fields.
Hardware Impact: Avoids GC stalls on weak CPUs; exact frame gain requires runtime profiler capture.

## Decision 016 - Editor-Only Hot Method Filtering

Problem: A runtime source file can contain `#if UNITY_EDITOR` tooling with method names such as `Tick`. The APEX verifier previously treated those editor callbacks as gameplay hot routes, creating a false dependency/allocation failure channel.
Solution: Added preprocessor line-range filtering in `FaunaApexIntegratorVerifier1610` for exact editor-only branches. `UNITY_EDITOR || DEVELOPMENT_BUILD` remains audited because that code can execute in development runtime builds.
Rejected Alternatives: Whitelisting specific editor class names or suppressing all files that contain editor code. Class whitelists rot; whole-file suppression would hide real gameplay violations beside editor tooling.
Scalability potential: Low, middle, high, and ultra lanes keep the same verifier rule: gameplay hot routes are audited, editor-only UI helpers are ignored.
Hardware Impact: Runtime cost is 0 us. Editor verification avoids one false failure review pass; full menu rerun is deferred because host CPU remains above the 50 percent throttle.

## Decision 017 - Per-Prefab H8LR Rig Metadata

Problem: The offline rigger claimed spine metadata but only logged a summary and set coarse `FaunaKinematicsRuntime` fields. That loses parent, chain, segment length, and bend-limit rows per generated prefab.
Solution: Generate a compact little-endian H8LR `.bytes` product asset under `Assets/_Project/Data/Fauna/Rigs1610`, assign it to `_generatedRigDefinitionBinary`, and parse it once in `FaunaKinematicsRuntime` before the global StreamingAssets/archive fallback.
Rejected Alternatives: Global `StreamingAssets/leviathan_rig_definitions.h8bin` overwrite, JSON metadata, or a new runtime component owned by the editor task. Global overwrite breaks multiple prefabs; JSON is not a runtime asset route; a new component expands domain ownership.
Scalability potential: Low prefabs carry 2-4 usable rows; middle predators carry 24-row constraints; high/ultra Leviathan assets can carry up to the runtime solver cap without changing hot loops.
Hardware Impact: i3/MX350 hot-path impact is 0 us. Cold load copies at most 4096 bytes once and then consumes existing DataVault rows.

## Decision 018 - VAT Total Memory Guard

Problem: The VAT route rejected textures wider than `SystemInfo.maxTextureSize` but still allowed `vertexCount * frameCount` to allocate oversized RGBAFloat payloads.
Solution: Reject VAT bakes above 32 MiB (`vertexCount * frameCount * 16`) and above `int.MaxValue` pixels before allocating `NativeArray<float4>` or creating the texture.
Rejected Alternatives: Trusting width-only validation or adding tiled VAT without shader/runtime contract. Width-only misses low-end VRAM pressure; tiling is a broader material/shader route and not justified without consuming runtime code.
Scalability potential: Low uses smaller mesh/frame lanes; middle can keep 30-frame swarms inside budget; high/ultra can spend budget deliberately by splitting schools or raising frame count within a known cap.
Hardware Impact: Prevents editor/player asset generation from creating 64-256 MiB VAT textures that would stall or fail on i3/MX350-class hardware.

## Decision 019 - Linear IK Rows Only

Problem: The generated H8LR asset initially included lateral fin rows, but `FaunaKinematicsRuntime.TryParseRigDefinitionBinary` consumes rows as a linear forward spine chain. Branch bones in that payload would become fake tail segments.
Solution: Emit only the first `SpineBoneCount` rows into H8LR and cap them to the existing runtime IK limit of 20 segments. Fins remain renderer/skinning bones, not procedural spine solver rows.
Rejected Alternatives: Encoding branch offsets into the existing H8LR parser or pretending chain IDs make branches work. The parser ignores local offsets and walks a forward cursor, so branch metadata would be false data.
Scalability potential: Low uses 2-4 spine rows; middle/high uses up to 20 procedural IK rows; ultra can still carry 96 renderer bones for skinning while procedural solver stays bounded.
Hardware Impact: Avoids wasted IK rows and wrong collider/bone matrices on i3/MX350. Hot-path impact remains 0 us because the cap is baked into the product asset.

## Decision 020 - VAT Backend Support Guard

Problem: VAT precision and bake paths created RGBAFloat textures without checking backend support or texture height against `SystemInfo.maxTextureSize`.
Solution: Reject unsupported `TextureFormat.RGBAFloat` before precision/bake texture creation and reject frame counts above max texture size.
Rejected Alternatives: Implicit fallback to RGBAHalf or letting Unity fail during texture creation. RGBAHalf violates the current `<0.001f` precision contract; deferred failure produces corrupt or missing generated assets.
Scalability potential: Low/mobile devices fail closed or require smaller/alternate VAT lanes; middle/high/ultra use RGBAFloat when supported.
Hardware Impact: Prevents unsupported texture allocation/import attempts and avoids generating content that cannot render on the target graphics backend.

## Decision 021 - Safe H8LR Product Write

Problem: Directly writing the final generated H8LR `.bytes` file can leave a truncated rig asset if the editor crashes or the host stalls during write.
Solution: Write to `*.tmp`, then replace or move into the final asset path only after the payload is fully written. Existing final assets use `File.Replace`; if that route is unavailable, generation fails and the temp file is cleaned instead of deleting the previous valid product.
Rejected Alternatives: Direct final-file overwrite, unsafe delete-then-move fallback, or report-only metadata. Direct overwrite can corrupt generated prefab inputs; delete-then-move can remove the previous valid rig asset; report-only metadata is not consumed by runtime.
Scalability potential: Low, middle, high, and ultra generated rigs all use the same product asset route without creating additional runtime logic.
Hardware Impact: Runtime impact is 0 us. Editor failure mode improves by avoiding partial final rig payloads on overloaded machines.

## Decision 022 - Mandatory H8LR TextAsset Injection

Problem: The generator could write an H8LR `.bytes` product asset and still save a skinned prefab if `AssetDatabase.LoadAssetAtPath<TextAsset>` returned null or if the runtime component did not expose the serialized binary field.
Solution: Treat missing/import-failed H8LR `TextAsset` and missing `_generatedRigDefinitionBinary` as generation failures. The prefab is not saved as a successful skinned output unless the runtime can consume the generated spine payload.
Rejected Alternatives: Logging a warning and saving the prefab anyway. That creates a visually rigged asset with no deterministic procedural IK metadata, forcing runtime fallback behavior.
Scalability potential: Low, middle, high, and ultra rigs all carry the same explicit product payload route; richer renderer bones do not alter the runtime metadata contract.
Hardware Impact: Runtime impact is 0 us. Low-end devices avoid cold fallback parsing and wrong default Leviathan rows for generated prefabs.

## Decision 023 - Empty Bone Audit Is Failure

Problem: The bone-limit audit returned success when the generated prefab folder was empty or contained no `SkinnedMeshRenderer`, producing a false proof artifact.
Solution: Count generated skinned renderers and return failure with `STATUS=NO_OUTPUT_TO_VERIFY` if no generated skinned fauna prefab exists.
Rejected Alternatives: Treating empty output as pass. It hides missing raw assets and makes mobile GPU bone compliance unverifiable.
Scalability potential: Low through ultra lanes receive a real audit only after skinned prefabs exist; VAT-only outputs remain outside the skinned-bone proof.
Hardware Impact: Runtime impact is 0 us. Prevents shipping unaudited skinned fauna to i3/MX350-class hardware.

## Decision 024 - Fuzzer Time Target Is A Real Assertion

Problem: The 1M vertex fuzzer logged a warning when the 500 ms target was missed but still returned success.
Solution: Return failure when elapsed time exceeds 500 ms. A performance assertion that passes on failure is not an assertion.
Rejected Alternatives: Warning-only timing proof. That can hide a Burst regression until content production.
Scalability potential: Low-tier content stays protected by an explicit offline timing gate; high/ultra meshes must prove the same weight-normalization and throughput contract.
Hardware Impact: Runtime impact is 0 us. Editor bake throughput regressions become visible before weak devices inherit expensive rigs.

## Decision 025 - H8LR Bridge Preflight Before Product Write

Problem: The generator could write an H8LR product asset before proving the target prefab had a runtime component and serialized field capable of consuming that product.
Solution: Preflight `FaunaKinematicsRuntime` and `_generatedRigDefinitionBinary` before writing H8LR bytes, then bind the imported `TextAsset` before creating mesh and prefab assets.
Rejected Alternatives: Writing metadata first and letting injection fail later. That can leave orphan product assets and makes success semantics ambiguous.
Scalability potential: Low, middle, high, and ultra generated rigs follow one route: bridge exists, metadata writes, metadata binds, renderer saves.
Hardware Impact: Runtime impact is 0 us. Low-end devices avoid fallback IK rows caused by prefabs that looked generated but lacked attached H8LR payloads.

## Decision 026 - Prefab Save Guards

Problem: Skinned and VAT routes filled metrics even if `PrefabUtility.SaveAsPrefabAsset` returned null.
Solution: Reject and log explicit skinned/VAT prefab save failure before metrics are emitted.
Rejected Alternatives: Returning `prefab != null` after writing metrics. That pollutes logs with successful-looking metrics for missing assets.
Scalability potential: All quality lanes now require an actual generated prefab before claiming bake metrics.
Hardware Impact: Runtime impact is 0 us. Prevents missing prefab references from entering low-end content packs.

## Decision 027 - Minimum Skinned Bone Count

Problem: The UI allowed a one-bone non-VAT skinned route, but the H8LR parser and generated spine metadata require at least two linear rows. That caused a late metadata failure after weight work had already run.
Solution: Clamp non-VAT skinned presets to at least two bones before skeleton generation; Leviathan clamps to at least eight bones to match the procedural solver's minimum useful spine.
Rejected Alternatives: Letting one-bone skinned fish fail during metadata write, or emitting fake two-row H8LR over a one-bone skeleton. Late failure wastes editor compute; fake metadata corrupts runtime IK truth.
Scalability potential: Low-tier small fish remain at 2-4 bones or VAT; middle predators keep 2-24; high/ultra Leviathans start from 8 and scale up to renderer cap 96 while procedural IK rows remain capped.
Hardware Impact: Runtime impact is 0 us. Editor avoids wasted Burst skinning and failed metadata write for invalid one-bone skinned content on i3/MX350-class authoring machines.

## Decision 028 - H8LR Only Where Runtime Contract Fits

Problem: `FaunaKinematicsRuntime` is a Leviathan spine solver with an eight-segment minimum, but the generator attached it to SmallFish skinned output capped at four bones. That produced false metadata and could expand a tiny fish into fallback Leviathan rows at runtime.
Solution: Require generated H8LR binding only for MediumPredator and Leviathan presets. SmallFish remains under its 2-4 bone GPU limit without the Leviathan runtime; swarm motion remains the VAT path.
Rejected Alternatives: Raising SmallFish to eight bones, or letting a four-bone fish carry a runtime that clamps active quality to eight segments. Both violate the mobile bone budget or runtime truth.
Scalability potential: Low uses VAT or 2-4 bone static skinned fish; middle predators carry 8 spine rows plus lateral bones; high and ultra Leviathans scale renderer bones up to 96 while procedural IK rows stay capped at 20.
Hardware Impact: Low-end i3/MX350 avoids extra FaunaBrain/FaunaKinematics components and eight-segment solver buffers on tiny fish; high-tier Leviathans keep deterministic H8LR metadata.

## Decision 029 - Preset-Aware Editor Bone Range

Problem: The EditorWindow allowed arbitrary 1-96 target bones for every preset, while the bake path silently clamped later. That made invalid author intent cheap to enter and expensive to reject.
Solution: The UI now disables bone count for VAT, clamps SmallFish to 2-4, MediumPredator to 12-24, and Leviathan to 8-96 before bake.
Rejected Alternatives: Leave the slider global and rely on console errors. That wastes editor compute and hides runtime solver requirements from artists.
Scalability potential: Low, middle, high, and ultra content lanes expose their actual budget before asset generation.
Hardware Impact: Runtime impact is 0 us. Authoring avoids failed or silently altered bakes on weak machines.

## Decision 030 - Late-Frame Shader Clear Proxy

Problem: `CreatureDamageManager` deferred wound publishing to `LateFrameTick`, but lifecycle teardown still called `ClearShaderGlobals()` directly.
Solution: Teardown now queues a static `ShaderClearLateFrameProxy` and the actual shader global clear executes from `LateFrameTick`.
Rejected Alternatives: Keeping lifecycle shader writes as an exception. That weakens the presentation phase contract and creates a second visual sync route.
Scalability potential: Low devices avoid unscheduled GPU state writes during enable/disable churn; high and ultra keep wound fidelity with one phase boundary.
Hardware Impact: Saves no claimed frame time; removes a race-prone lifecycle GPU write path and keeps phase ownership auditable.

## Decision 031 - Cross-File APEX Method Graph

Problem: `FaunaApexIntegratorVerifier1610` audited transitive hot calls per source file, so partial classes and nested helper calls could form an unverified edge outside the local method list.
Solution: Runtime methods are now accumulated across fauna runtime roots before dependency, allocation, and presentation reachability checks. Each method stores source path plus declaring type path, and nested/containing type calls are resolved without semantic compilation. DataVault write-lock checks now require an adjacent local `try/finally` release with matching handle and owner arguments instead of unrelated release text somewhere in the method.
Rejected Alternatives: Keep per-file graph analysis, or run `dotnet build` to compensate. Per-file analysis is a structural blind spot; build is throttled because CPU is above 50 percent and `dotnet` is active.
Scalability potential: Low, middle, high, and ultra content lanes share the same verifier; adding partial fauna modules cannot silently bypass hot-route dependency rules.
Hardware Impact: Runtime impact is 0 us. Editor verifier cost increases by one shared method list and bounded AST traversal; host build CPU remains untouched under contention.

## Decision 032 - Hot String-GC Source Detection

Problem: The verifier caught arrays, managed collections, delegates, and `StringBuilder`, but string interpolation, `ToString()`, and literal string concatenation could still allocate in hot-reachable helpers without being classified as managed allocation.
Solution: Extend `FaunaApexIntegratorVerifier1610` to mark interpolated strings, `ToString()` calls, and literal string concatenation as hot-reachable managed-allocation violations. Also recognize `VISUAL_SYNC`/`Visual_Sync` as valid presentation phase spellings so deferred visual code is audited by phase intent instead of naming accident.
Rejected Alternatives: Rely on broad token grep or wait for profiler evidence. Grep is triage only and profiler comes too late if source already permits hot managed strings.
Scalability potential: Low devices avoid GC spikes in hot fauna phases; middle/high/ultra can add richer debug and visual code only if it stays outside `Tick`, `FixedUpdate`, `Execute`, and non-visual hot transfer paths.
Hardware Impact: Runtime impact is 0 us from the verifier itself. i3/MX350-class devices are protected from a class of allocator stalls before build/profiler time is spent.

## Decision 033 - Hot Deferred Allocation Source Detection

Problem: The verifier still allowed hot-reachable LINQ/deferred query syntax, lambdas, anonymous delegates, anonymous objects, `yield`, and `await` to pass unless they also contained a previously-known allocation token.
Solution: Extend `FaunaApexIntegratorVerifier1610` to mark query syntax, LINQ/deferred invocations, anonymous functions, anonymous object construction, `yield`, and `await` as hot-reachable allocation/control-flow violations. Narrow static string factory detection to `string`, `String`, and `System.String` so unrelated `Create()` factories are not misclassified as string allocation.
Rejected Alternatives: Broad grep-only rejection and broad `.Create` suffix rejection. Grep is not transitive and `.Create` catches too many legitimate factory names without semantic context.
Scalability potential: Low devices avoid allocator/deferred-enumerator spikes; middle/high/ultra keep richer fauna systems only when their hot transfer surfaces stay explicit and allocation-free.
Hardware Impact: Runtime impact is 0 us from the verifier. i3/MX350-class builds avoid spending profiler time on avoidable LINQ/delegate drift.

## Decision 034 - Hot Foreach Source Detection

Problem: HECTON-8 bans `foreach` over dictionary/interface/deferred enumerables in hot paths, but without a semantic model the verifier cannot prove a `foreach` target is a safe array/list case.
Solution: Add a fail-closed AST guard for `foreach` and deconstruction `foreach` syntax in hot-reachable methods. A direct hot-body scan found no current fauna-domain `foreach` hits before enabling this guard, so the new rule does not mask an existing known violation.
Rejected Alternatives: Allow `foreach` until semantic compilation is available, or attempt type inference with regex. The first leaves an allocation blind spot; the second is fake precision.
Scalability potential: Low devices avoid enumerator boxing/deferred iteration drift; middle/high/ultra keep fauna hot loops index-based and cache-friendly.
Hardware Impact: Runtime impact is 0 us from the verifier. i3/MX350-class stability improves by preventing future boxed enumerator paths before profiler time is spent.

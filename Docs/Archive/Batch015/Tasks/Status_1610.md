# Status 1610 - FAUNA_SKINNING_AND_IK_SKELETON_FORGER

Status: APEX SOURCE HARDENED; CROSS-FILE AST GRAPH, HOT STRING/LINQ/DELEGATE/FOREACH-GC DETECTION, PRESET-AWARE H8LR GATING, UI BONE CLAMP, FUZZER ASSERTION, AND LATE-FRAME SHADER CLEAR PROXY ADDED; UNITY VALIDATION BLOCKED BY HOST CONTENTION
Batch source: Docs/Tasks/CURRENT_BATCH.md `<AGENT_PROMPT id="1610" ...>`
Domain: Echelon 3 Flora, Fauna & Biota - offline fauna rigging, VAT swarms, Leviathan spine metadata.
Task count: 20

## Mandates Selected Before Coding

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- REND_GPU_Driven_Animation_VAT.txt
- ANIM_IK_FABRIK_GroundSnapping_Procedural.txt

## State Machine

- [x] Task 01 - EXHAUSTIVE_FAUNA_MESH_ANALYSIS
  - DOD: Added source-only static scanner and Unity console summary; raw folder `Assets/_Project/Art/Fauna/Raw` is absent and no FBX/OBJ/Mesh inputs exist under `Assets/_Project/Art`.
  - Rejected: Silent success with no meshes; status records `NO_RAW_FAUNA_MESH_INPUTS`.
  - Estimate: 0 us runtime; editor scan cost asset-count dependent.
- [x] Task 02 - PROCEDURAL_BONE_CHAIN_MATH_MODELING
  - DOD: Implemented bounds-axis skeleton generation with spine and lateral fin chains inside `CreateBoneHierarchy`.
  - Rejected: Animator rigs and hand-authored bone counts.
  - Estimate: 0 us runtime generation; bind metadata read cost only.
- [x] Task 03 - SMOOTH_SKINNING_WEIGHT_ALGORITHM_DESIGN
  - DOD: Implemented inverse-distance segment weighting with 4 `BoneWeight1` influences and exact final-slot normalization.
  - Rejected: Joint-point distance and managed main-thread loops.
  - Estimate: 0 us runtime; editor Burst job target under 500000 us for 1M mock vertices.
- [x] Task 04 - VAT_ENCODING_MATRIX_MATH
  - DOD: Implemented `Width=VertexCount`, `Height=FrameCount`, RGBAFloat offset packing.
  - Rejected: RGBAHalf for first pass because 0.001m precision assertion is stricter than half-float safety on large fauna offsets.
  - Estimate: 0 us CPU runtime; GPU samples one texture per vertex in shader path.
- [x] Task 05 - TELEMETRY_AND_REPORTING_ARCHITECTURE
  - DOD: Removed JSON report writers; generator now emits cold Unity console summaries and writes only real Unity assets.
  - Rejected: JSON ledger churn, unmanaged binary dumps, and chat-only proof.
  - Estimate: 0 us runtime; no report I/O remains in the generator.
- [x] Task 06 - UNMANAGED_DTO_AND_SKELETON_MATERIALIZATION
  - DOD: Added explicit-layout DTOs sized 32/64 bytes and editor-only skeleton materialization.
  - Rejected: Runtime DTO construction and reflection on hot paths.
  - Estimate: 0 us runtime generation.
- [x] Task 07 - BURST_COMPILED_AUTO_SKINNING_JOB
  - DOD: Added `[BurstCompile] IJobParallelFor` using `NativeArray<float3>`, `NativeArray<float4x4>`, `NativeArray<BoneWeight1>`, and `[NoAlias]`.
  - Rejected: Managed `List<T>` inside kernel.
  - Estimate: Target 0.5 us/1000 vertices on high-end Burst after import; profiler proof pending.
- [x] Task 08 - ASSET_DATABASE_BIND_POSE_SERIALIZATION
  - DOD: Added bindpose calculation, `Mesh.SetBoneWeights`, dirty marking, and mesh asset save.
  - Rejected: Prefab-only transient meshes.
  - Estimate: 0 us runtime; editor serialization only.
- [x] Task 09 - VAT_BAKING_COMPUTE_PIPELINE
  - DOD: Added Burst VAT sine-wave bake into RGBAFloat `Texture2D` asset, material injection, backend format support rejection, hardware width/height rejection, and 32 MiB compact pixel-budget rejection.
  - Rejected: CPU boid skeletal animation for swarms, unsupported RGBAFloat backend creation, and unbounded textures that exceed low-end GPU memory budgets.
  - Estimate: 0 us CPU runtime for baked animation.
- [x] Task 10 - SPINE_IK_METADATA_INJECTION
  - DOD: Added per-prefab H8LR `.bytes` rig asset generation containing only the linear spine chain, capped to the existing 20-segment IK runtime, temp-written before replacement, plus cold `TextAsset` hydration before StreamingAssets fallback.
  - Rejected: Console-only spine summary, global StreamingAssets overwrite, direct final-file writes, and writing lateral fin bones into a linear procedural IK parser.
  - Estimate: 0 us hot runtime; cold hydration copies at most 4096 bytes once before DataVault rows are consumed.
- [x] Task 11 - PREFAB_ASSEMBLY_AND_RENDERER_ASSIGNMENT
  - DOD: Added skinned prefab path and VAT MeshRenderer/MeshFilter prefab path under `Assets/_Project/Prefabs/Nature/Fauna/Rigged1610`.
  - Rejected: Scene-only generated roots.
  - Estimate: 0 us generation during play.
- [x] Task 12 - DEFORMATION_NOISE_MASK_BAKING
  - DOD: Added vertex-color wrinkle/tension mask bake near spine joints.
  - Rejected: Extra geometry and runtime tension scans.
  - Estimate: 0 us CPU runtime; shader consumes vertex color.
- [x] Task 13 - BATCH_RIGGER_WINDOW_UI
  - DOD: Added `HECTON-8/Fauna/Abyssal Anatomy Studio 1610` EditorWindow with mesh, material, preset, bone count, VAT frames, and quality controls.
  - Rejected: Hidden CLI-only generator.
  - Estimate: Editor-only UI cost.
- [x] Task 14 - FAIL_CLOSED_GENERATION_SAFETY
  - DOD: Added readable-mesh, vertex-count, isolation, normalization, bone limit, and NaN-safe fallbacks.
  - Rejected: Creating corrupted zero-weight meshes.
  - Estimate: 0 us runtime; checks execute during editor bake.
- [x] Task 15 - BATCHED_COMPILATION_AND_SYNTAX_ASSERTION
  - DOD: Added `FaunaApexIntegratorVerifier1610` for Roslyn AST in-memory source checks; scoped `git diff --check` passed for touched files.
  - Rejected: `dotnet build`; user forbade it and two existing `dotnet` processes were active during verification.
  - Estimate: Build cost avoided; compiler proof not claimed.
- [x] Task 16 - MOCK_1M_VERTEX_SKINNING_FUZZER
  - DOD: Added menu harness `1610 Run 1M Skinning Fuzzer` creating 1,048,576 mock vertices and asserting normalized weights.
  - Rejected: Small toy mesh-only proof.
  - Estimate: Target under 500000 us editor Burst; not executed in this shell session.
- [x] Task 17 - VAT_ENCODING_PRECISION_ASSERTION
  - DOD: Added RGBAFloat encode/decode assertion with `<0.001f` tolerance.
  - Rejected: Trusting import texture defaults.
  - Estimate: Editor-only; not executed in this shell session.
- [x] Task 18 - ZERO_GC_EDITOR_HOT_PATH_VERIFICATION
  - DOD: Static scan confirms Burst kernels contain no managed reference allocation or string concatenation; managed allocations are labeled cold editor scratch.
  - Rejected: Moving List/StringBuilder into job kernels.
  - Estimate: 0 B runtime allocation from generator.
- [x] Task 19 - BONE_LIMIT_COMPLIANCE_AUDIT
  - DOD: Added generated-prefab audit counting `SkinnedMeshRenderer.bones` and enforcing 4/24/96 limits from preset-bearing prefab names.
  - Rejected: Manual inspector verification.
  - Estimate: Editor-only scan.
- [x] Task 20 - AUTOMATED_METRIC_VALIDATOR_REPORT
  - DOD: Kept SHA-256 source hash in the generator and replaced final metric file output with console-only real generated-output metrics.
  - Rejected: Fake benchmark JSON without generated assets.
  - Estimate: Editor-only summary; 0 us runtime.

## Iterative Loops

1. Extracted `<AGENT_PROMPT id="1610">`, read domain and mandates, created ledgers.
2. Built editor-only assembly and first-pass generator.
3. Static-reviewed compile risks; fixed unqualified VAT frame constant and destroyed-texture handling.
4. Re-read prompt; fixed `GlobalQualityWeight` propagation and preset-bearing prefab names for audit correctness.
5. Re-ran static scans: scoped `git diff --check` clean; no `dotnet` or `csc` process running; no raw fauna mesh inputs found.
6. APEX pass: flattened `ProceduralBoneBlenderRuntime` and `StressDrivenSpawnDirector` job buffer locks into one mutation guard per scheduled job.
7. APEX pass: removed immediate wound shader publication from `CreatureDamageManager.OnEnable`; deferred wound publication to `LateFrameTick`.
8. APEX pass: removed stale JSON helpers from the fauna generator and added source-only verifier menu path.
9. APEX pass: upgraded verifier from lexical method scan to Roslyn `CSharpSyntaxTree` AST scan with direct call-graph presentation reachability checks.
10. APEX pass: removed legacy JSON report upsert from `OOP_Movement_Scanner` and narrowed verifier registry logic so lifecycle registration is not treated as a hot dependency lookup.
11. APEX pass: removed JSON/report naming residue from the fauna generator, added hot-route managed-allocation AST checks, fail-closed VAT texture width validation, VAT material shader contract fields, and bounds-aware isolated-vertex rejection.
12. APEX pass: patched verifier preprocessor handling so exact editor-only blocks in runtime files are excluded from hot-route audits while `UNITY_EDITOR || DEVELOPMENT_BUILD` remains audited as runtime-capable code.
13. APEX pass: replaced spine summary-only route with generated H8LR `.bytes` metadata attached to `FaunaKinematicsRuntime` and added a 32 MiB VAT pixel budget guard.
14. APEX pass: corrected H8LR writer to emit only linear spine rows capped to the runtime IK segment limit and added RGBAFloat backend plus texture-height guards.
15. APEX pass: changed H8LR product asset output from direct final-file write to temp write plus replace/move.
16. APEX pass: made generated H8LR `TextAsset` injection mandatory for skinned prefab output and made bone audit fail closed when no generated skinned prefab exists.
17. APEX pass: made the 1M skinning fuzzer fail on >500 ms, added explicit skinned/VAT prefab save guards, moved H8LR bridge preflight before product asset write, and clamped non-VAT skinned presets to metadata-valid minimum bone counts.
18. APEX pass: stopped attaching Leviathan-only H8LR runtime metadata to 4-bone SmallFish output, raised MediumPredator minimum to 12 total bones so eight spine rows survive fin reservation, exposed preset-aware UI bone ranges, and moved wound shader teardown clear into a late-frame proxy.
19. APEX pass: upgraded `FaunaApexIntegratorVerifier1610` from per-file transitive checks to a shared runtime method graph, added partial/nested type path resolution, and replaced loose write-lock release counting with adjacent local `try/finally` verification that matches acquire/release handle and owner arguments.
20. APEX pass: extended `FaunaApexIntegratorVerifier1610` hot-reachable allocation checks to catch interpolated strings, `ToString()`, and literal string concatenation; recognized `VISUAL_SYNC` as an allowed presentation phase name.
21. APEX pass: extended `FaunaApexIntegratorVerifier1610` hot-reachable allocation checks to reject LINQ/deferred queries, query syntax, lambdas, anonymous delegates, anonymous objects, `yield`, and `await`; narrowed static string factory detection to `string/String/System.String`.
22. APEX pass: extended `FaunaApexIntegratorVerifier1610` hot-reachable allocation checks to reject `foreach` and deconstruction `foreach` syntax; direct hot body scan found no current `foreach` in 60 fauna hot methods before enabling the fail-closed guard.

## Verification Ledger

- Compile/build: NOT RUN. User forbids `dotnet build`; current CPU sample reached 100 percent with an active `dotnet` process during final source validation.
- Unity import/playmode/profiler: NOT RUN.
- Unity script validation: PREVIOUS PASS for `FaunaKinematicsRuntime.cs` and `AbyssalAnatomyStudio1610.cs` before the latest H8LR linear-chain and VAT backend guard patch. Retry was attempted once under low CPU for `AbyssalAnatomyStudio1610.cs`; Unity MCP returned `no_unity_session`, so no repeat loop was forced.
- Static source review: DONE for latest generator patch; scoped `git diff --check`, brace-balance scan, trailing-whitespace scan, token contract scan, and safe H8LR writer token scan passed.
- Report I/O purge: DONE for report routes; scan found no `File.WriteAllText`, `StreamWriter`, `Docs/Reports`, or `.json` report route outside verifier string constants. `File.WriteAllBytes` now exists only for generated H8LR product rig assets under `Assets/_Project/Data/Fauna/Rigs1610`.
- Hot lookup context: DONE; `TryGetComponent`/`GetComponentsInChildren` hits reviewed as `Awake`, `OnEnable`, `OnValidate`, or cold cache paths, not `Tick`, `FixedUpdate`, `LateFrameTick`, or `Execute`.
- DataVault lock context: DONE; remaining write locks in `StressDrivenSpawnDirector` are single-handle acquisitions with `finally` releases.
- VAT GPU contract: DONE; VAT bakes reject meshes wider than `SystemInfo.maxTextureSize` and author `_VatEnabled`, `_VatFrameCount`, `_VatVertexCount`, `_VatPlaybackSpeed`, `_VatNormalBlend`, and `_VatPositionScale` when the material supports them.
- VAT memory contract: DONE; VAT bakes reject unsupported `RGBAFloat`, width or height above `SystemInfo.maxTextureSize`, `vertexCount * frameCount * 16` above 32 MiB, and pixel counts above `int.MaxValue` before allocating `NativeArray<float4>`.
- Generated metadata contract: DONE; MediumPredator and Leviathan skinned prefabs require H8LR `TextAsset` binding, while SmallFish skinned output no longer receives the Leviathan-only runtime because its 4-bone GPU limit cannot satisfy the runtime's 8-segment minimum.
- Generated product ordering contract: DONE; skinned prefab generation now verifies the existing `FaunaKinematicsRuntime` bridge before writing the H8LR product asset, then binds the imported `TextAsset` before mesh/prefab save.
- Bone audit truth contract: DONE; generated-prefab audit now returns failure if no generated skinned fauna prefab exists instead of reporting empty success.
- Fuzzer truth contract: DONE; 1M vertex fuzzer now returns failure when the 500 ms target is missed instead of logging a warning and passing.
- Skinned minimum contract: DONE; SmallFish clamps 2-4 bones without H8LR, MediumPredator clamps to 12-24 total bones for 8 spine rows plus 4 lateral bones, and Leviathan clamps to 8-96.
- Presentation teardown phase contract: DONE; wound shader global clear is queued from lifecycle teardown and executed by `ShaderClearLateFrameProxy.LateFrameTick`, leaving shader writes in `CreatureDamageManager` on late-frame call paths.
- Zero-GC transfer verifier: DONE; AST verifier now checks hot methods and reachable helpers for managed arrays and managed collection/string-builder/delegate allocations.
- APEX source verifier: PATCHED at `Assets/_Project/Editor/Generators/Fauna/FaunaApexIntegratorVerifier1610.cs`; `validate_script` reports 0 diagnostics. Full Unity menu rerun is withheld because host CPU remains above the 50 percent build/verification throttle.
- APEX source verifier graph: DONE; runtime methods are now accumulated across fauna runtime roots before transitive dependency/allocation/presentation scans, so partial classes and nested helper calls are not treated as audit boundaries.
- DataVault lock verifier: DONE; each `TryAcquireWriteLock` must now be paired with a matching-handle local adjacent `try/finally` release block or a containing `try/finally`, instead of passing through unrelated release text in the same method.
- Lightweight static hot scan: DONE; 52 runtime fauna hot methods scanned, 0 direct hot lookup violations, 0 write-lock structure violations, 0 non-late presentation write violations.
- Lightweight static hot scan: DONE after metadata patch; changed runtime hot methods scanned, 0 direct hot lookup violations.
- Lightweight static hot scan: DONE after phase proxy patch; five touched runtime files scanned for direct hot `GlobalRegistry.Get`, `GetComponent`, `TryGetComponent`, `GetComponentsInChildren`, `TryAcquireWriteLock`, and non-late shader writes; 0 violations.
- Lightweight static hot scan: DONE after cross-file verifier hardening; five touched runtime files scanned again for direct hot lookup/lock tokens and non-late shader writes; 0 violations.
- Broad runtime hot scan: DONE; 33 fauna/procedural-fauna runtime files and 56 hot methods scanned for direct hot lookup/lock tokens plus non-late presentation write tokens; 0 violations.
- Write-lock adjacency scan: DONE; all `StressDrivenSpawnDirector` `TryAcquireWriteLock` sites have matching handle/owner `ReleaseWriteLock` calls in adjacent local `try/finally` release blocks.
- Process contention check: CPU reached 94 percent with active `dotnet` process 30740; no build launched and no Unity validation/menu rerun forced during contention.
- Process contention check: CPU reached 57 percent with active `dotnet` process 30740 after verifier graph hardening; no build launched and no Unity validation/menu rerun forced during contention.
- Hot string allocation proof: DONE; direct token scan over 33 fauna/procedural-fauna runtime files and 60 hot methods found 0 `$"..."`, `.ToString()`, or literal string-concat hits in direct hot bodies.
- APEX verifier string-GC guard: DONE; editor AST verifier now marks hot-reachable interpolated strings, `ToString()`, and literal string concatenation as managed-allocation violations.
- Hot deferred allocation proof: DONE; case-sensitive direct token scan over 33 fauna/procedural-fauna runtime files and 60 hot methods found 0 LINQ, lambda, anonymous delegate, `yield`, or `await` hits in direct hot bodies.
- APEX verifier LINQ/delegate guard: DONE; editor AST verifier now marks query syntax, LINQ/deferred invocations, anonymous functions, anonymous objects, `yield`, and `await` as managed-allocation/control-flow violations when reachable from hot methods.
- Hot foreach proof: DONE; direct token scan over 33 fauna/procedural-fauna runtime files and 60 hot methods found 0 direct `foreach` hits in hot bodies.
- APEX verifier foreach guard: DONE; editor AST verifier now marks `foreach` and deconstruction `foreach` syntax as hot-reachable managed-allocation/control-flow violations.
- Process contention check: CPU reached 68 percent with active `dotnet` process 31232; no build launched and no Unity validation/menu rerun forced during contention.
- Process contention check: CPU reached 100 percent with active `dotnet` processes 15112 and 16700; no build launched and no Unity validation/menu rerun forced during contention.
- Raw input availability: BLOCKED; no fauna FBX/OBJ/Mesh sources found.

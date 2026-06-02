# Rationale 1614 - BATCH_RENDERER_GROUP_SCATTER_POLISHER

Date: 2026-06-01
Status: CODE COMPLETE / RUNTIME BRG DATA VAULT BRIDGE ADDED / QUALITY PREFIX LIVE / COROUTINE-FREE URI LOAD / RUNTIME PAYLOAD CAP / BAKE GRID REFERENCE CAP / CULLING BOUNDS FINITE GATE / BOUNDS CAP SELF-TEST ROUTE / PAYLOAD LENGTH GUARD / GRID REF LONG SUM / SOURCE FOLDER BINDING VERIFIED / CULLING DATASET BOUNDS IMPORT / UNITY EXECUTION BLOCKED BY HOST CONTENTION

## Decision 00 - Scope Gate

Problem: Agent 1614 is assigned to offline scatter normal alignment, spatial culling, density decimation, and `.brgdata` baking. This touches Editor tooling, Burst jobs, binary layout, metadata assets, and prefab binding.

Solution: Confine all generation code to `Assets/_Project/Editor/Generators/World/` unless an existing runtime scatter loader contract already exists and must be referenced. Runtime changes require a critical justification and must avoid hot scatter calculation.

Rejected Alternatives: Runtime scatter alignment, per-instance GameObjects, scene hierarchy persistence, and binary quality tiers. These violate the prompt and HECTON-8 render doctrine.

Scalability potential: Low uses shorter draw prefix from the deduction map; Middle increases prefix and LOD residency; High uses dense near-field distribution; Ultra uses visual overkill density while gameplay truth remains unchanged.

Hardware Impact: i3/MX350 gain comes from moving scatter CPU work to offline bake and drawing a prefix slice of already packed GPU data. Expected runtime CPU impact is reduced by avoiding transform hierarchies; exact microseconds are PENDING VERIFICATION.

## Decision 01 - Normal Alignment Contract

Problem: Kelp/coral/rock instances must grow along voxel terrain normals while preserving random yaw. Direct Euler tilt can produce NaNs on vertical faces and loses yaw authority.

Solution: `AlignScatterToTerrainNormalJob.BuildNormalAlignedRotation` normalizes the sampled normal, projects a stable forward seed onto the normal plane, rotates that tangent by yaw around the normal axis, and calls `quaternion.LookRotationSafe`.

Rejected Alternatives: `Transform.LookAt`, per-prefab Editor transforms, and direct Euler slope offsets. They are scene-object paths, not flat BRG data paths, and they fail on cliff normals.

Scalability potential: Low/middle/high/ultra all share identical matrix truth; only draw prefix changes by `GlobalQualityWeight` metadata, so visual density scales without divergent placement.

Hardware Impact: i3/MX350 avoids runtime quaternion work for every scatter instance; saved runtime cost is projected per visible chunk, exact measured microseconds unavailable because bake execution was blocked.

## Decision 02 - Spatial Culling Route

Problem: Testing every instance against every base/wreck AABB would scale as O(N*M); 100,000 instances and 500 bounds would produce 50,000,000 tests per bake.

Solution: Build a cold Editor spatial grid once from bounds, write `NativeArray<int2>` cell ranges plus flat bound indices, then let `CullScatterInsideBoundsJob` test only the current cell range.

Rejected Alternatives: Full pairwise AABB scan and scene collider queries. Full scan wastes CPU; collider queries create managed/physics dependencies and are not deterministic binary bake inputs.

Scalability potential: Low devices receive the same no-intersection truth but draw fewer entries; ultra can consume denser forests because hidden/invalid base-intersecting entries are zeroed before GPU upload.

Hardware Impact: i3/MX350 runtime gain comes from not drawing vegetation hidden inside base modules and not instantiating culled GameObjects. Exact measured microseconds unavailable; static operation count drops from N*M to N*K where K is local cell occupancy.

## Decision 03 - Binary Layout

Problem: Runtime renderer needs flat GPU-ready payloads, not ScriptableObject truth or scene transforms.

Solution: `.brgdata` writer emits a 64-byte header, matrix block at 64-byte stride, 64-byte flora metadata block matching `GpuScatterFloraInstanceData`, and 4-byte quality index map.

Rejected Alternatives: JSON, prefab-per-instance, and runtime reflection of scatter payloads. These are cold/managed data routes and violate BRG/GPU sovereignty.

Scalability potential: Low/middle/high/ultra draw fractions are metadata floats, not binary switches; the runtime can choose a shorter quality-index prefix without recalculating placement.

Hardware Impact: i3/MX350 avoids Transform hierarchy cost and CPU scatter calculation; high-end machines can draw a longer prefix from the same file. Exact measured microseconds unavailable because Unity execution was blocked.

## Decision 04 - Non-Finite Audit

Problem: The first implementation used a shared non-finite counter written by parallel jobs. That is a race and not acceptable as proof.

Solution: Replace the counter with a per-instance `NativeArray<byte>` mask. Jobs write only their own index; the Editor cold path counts after completion.

Rejected Alternatives: Atomic increments or ignoring the metric. Atomic increments are unnecessary contention for an Editor metric; ignoring the metric weakens NaN forensic coverage.

Scalability potential: The mask is only allocated during bake. Runtime receives clean matrices and no telemetry dependency.

Hardware Impact: No runtime impact. Editor bake memory adds one byte per candidate during generation, then releases immediately.

## Decision 05 - Compilation Gate

Problem: Verification build is required by the original batch, but direct user instruction forbids routine `dotnet build`, and host contention is active.

Solution: Sampled CPU and compiler state. CPU was 100% on first sample and 54% on later sample, `dotnet` process `30740` was active both times, and Unity MCP had no active session. Build and Unity execution were blocked; static AST/text audits were used.

Rejected Alternatives: Launching another build under contention or claiming Unity bake results that did not execute.

Scalability potential: No effect on final runtime architecture; it prevents cluster starvation during parallel agent work.

Hardware Impact: Avoided adding a second compiler workload to a saturated host. Exact saved host time is not measured; policy condition was objectively violated.

## Decision 06 - APEX Integrator Verification Source

Problem: A verbal APEX proof does not prevent later drift: hot dependency lookups, phase drift, nested write locks, and accidental report/build process spawning can re-enter the scatter domain.

Solution: Added `AbyssalScatterApexIntegratorVerifier1614` as an Editor-only source verifier. It strips comments/strings, scans 1614 Editor files plus runtime scatter owners, rejects forbidden hot lookups/allocation tokens, enforces late-frame visual routes, checks DataVault write-lock release shape, rejects report writers, and rejects build-spawn tokens.

Rejected Alternatives: Relying on chat assertions, adding JSON reports, or launching `dotnet build` under host contention. Standard Unity play/build verification is still useful when the host is clear, but it is not the right tool for this small source-level compliance pass.

Scalability potential: Low/Middle/High/Ultra devices benefit indirectly because verifier protects the architecture that keeps runtime scatter on cached dependencies, late-frame presentation, and flattened lock scopes.

Hardware Impact: i3/MX350 avoids extra compiler contention and runtime stalls from hot scene/global lookups. Estimated saved runtime cost is scenario-dependent; direct compiler CPU saved this pass is one avoided `dotnet build` while a `dotnet` process was already active.

## Decision 07 - Verifier Shape and Prefab Binding Polish

Problem: The first APEX source verifier could treat `if`, `for`, `using`, and `lock` bodies as method bodies during DataVault lock flattening analysis. The prefab writer also created the scatter root before proving that the `.brgdata` payload existed and did not push baked draw bounds into the renderer.

Solution: `TryFindAnyMethodBody` now skips control-flow keywords before lock analysis. The prefab path validates the binary payload on disk, writes baked draw bounds from the culling grid into `GpuScatterLodManager.fallbackDrawBounds`, and configures continuous visual/culling scalars through serialized fields.

Rejected Alternatives: Leaving the verifier with noisy control-flow parsing, adding a runtime file loader outside the assigned Editor generator domain, or attaching an Editor-only manifest component to a runtime prefab. Those routes either weaken proof quality or create missing-script/runtime ownership risk.

Scalability potential: Low uses the same baked bounds and shorter draw prefix; Middle/High/Ultra expand draw fractions and cull distances without changing placement truth or prefab hierarchy.

Hardware Impact: i3/MX350 avoids rendering empty/hidden scatter inside authored exclusion bounds and avoids per-instance hierarchy cost. Compiler CPU impact remains zero for this pass because active `dotnet` PID 31232 blocked build execution.

## Decision 08 - Unsafe Removal and Runtime Metadata ABI Pin

Problem: The first 1614 Burst job pass used unsafe pointer writes and `NativeDisableParallelForRestriction` even though every job writes only its own `IJobParallelFor` index. The local `.brgdata` metadata DTO also matched the runtime renderer by design, but the source did not fail hard if `GpuScatterFloraInstanceData` drifted later.

Solution: Replace unsafe pointer writes with direct `NativeArray[index]` stores, disable `allowUnsafeCode` in the 1614 Editor asmdef, and add runtime ABI assertions for `GpuScatterFloraInstanceData` in `ValidateLayoutsOrThrow`. The APEX verifier now rejects unsafe bypass tokens and checks the runtime metadata field offsets/types from source.

Rejected Alternatives: Keeping unsafe writes as a habit, widening job safety suppressions, or adding a runtime conversion shim. All three increase maintenance risk without improving bake throughput because the write pattern is already one element per job index.

Scalability potential: Low/Middle/High/Ultra keep the same binary metadata contract. Device class changes only the draw prefix and scalar culling distances; metadata layout and placement truth do not fork.

Hardware Impact: Runtime microseconds unchanged by design; this is safety hardening. Editor/compiler impact remains 0 build microseconds because active `dotnet` PID 31232 blocked build execution under the mandated throttle.

## Decision 09 - Quality Deduction Map Bijection

Problem: The previous quality map job selected the best of a few seeded candidates per prefix slot. That can emit the same instance index more than once and skip other indices. On weak devices this corrupts the continuous density promise: a low draw fraction can waste GPU work on duplicate entries instead of showing a clean, thinned distribution.

Solution: Move the final quality map construction to the cold Editor bake path after culling completes. `BuildQualityDeductionMap` counts 16 continuous importance buckets, assigns bucket offsets from high to low importance, then walks all instance indices through a coprime permutation. `ValidateQualityDeductionMap` uses a temporary byte seen-mask and fails before serialization on any duplicate or out-of-range index. The Burst fallback job now emits only an overflow-safe permutation.

Rejected Alternatives: Managed sort of every candidate, random best-of-three candidate picking, and binary low/high density assets. Managed sort adds avoidable cold allocation pressure; best-of-three is not a bijection; binary assets violate continuous `GlobalQualityWeight`.

Scalability potential: Low draws a short high-importance prefix with no duplicate waste. Middle expands the same ordered prefix. High and Ultra draw deeper into the same deterministic map, buying visible overgrowth without changing placement truth or save identity.

Hardware Impact: i3/MX350 avoids duplicate overdraw in the lowest density prefix. Exact runtime microseconds are not measured because Unity execution remains unavailable; compiler CPU spent on this pass is 0 because active `dotnet` PID 23200 blocked build execution.

## Decision 10 - Runtime BRG DataVault Bootstrap

Problem: The generated prefab previously carried a `GpuScatterLodManager` and baked metadata, but no cold runtime route was responsible for reading the `.brgdata` payload and publishing matrices/metadata into `GlobalDataVault`. That left Task 12 dependent on an external producer and made the binary proof incomplete.

Solution: Add `AbyssalScatterBrgDataVaultBootstrap` in the runtime scatter namespace. The generated prefab configures it with the exact binary path, header hash, content hash, counts, and baked bounds. On cold enable or DataVault hot-swap, it reads the file from StreamingAssets, supports URI StreamingAssets via `UnityWebRequest`, validates header layout/hash/counts, validates the quality-index bijection, and writes DataVault matrices and metadata in two separate `try/finally` write-lock scopes.

Rejected Alternatives: Loading the binary from `GpuScatterLodManager.LateFrameTick`, polling `GlobalRegistry.Get<T>()` every frame, or using `GetComponent()` discovery. Those routes add hot dependencies or hidden scene searches. The renderer remains a presentation consumer; the bootstrap is the cold binary handoff owner.

Scalability potential: Low reads the same binary but draws the high-importance prefix. Middle expands the same ordered prefix. High and Ultra keep the exact same matrix truth and only increase visible prefix length.

Hardware Impact: i3/MX350 avoids runtime scatter generation and per-instance GameObjects. Exact runtime microseconds are not measured; compiler CPU spent is 0 because active `dotnet` PID 28892 blocked build execution.

## Decision 11 - Continuous Runtime Quality Prefix

Problem: The offline `.brgdata` quality map was a bijection, but the runtime renderer still drew the full active count. That made `GlobalQualityWeight` affect shader scalars and culling distance but not actual scatter density.

Solution: `GpuScatterLodManager.ResolveSafeActiveCount` now multiplies the safe active count by a continuous draw fraction from `0.08f` to `1.0f`. The loader reorders matrices and metadata by `payload.QualityIndices`, so shorter prefixes preserve the offline high-importance distribution.

Rejected Alternatives: Binary low/high assets, runtime sorting, or quality-dependent rebake. Binary tiers violate the continuous-quality mandate; runtime sorting adds allocation/CPU risk; rebake changes placement truth per device.

Scalability potential: Low keeps minimum survival density with no duplicate prefix waste. Middle and High increase coverage smoothly. Ultra draws the complete baked population.

Hardware Impact: i3/MX350 reduces BRG upload/cull/draw count on weak settings. Exact frame microseconds are pending Unity profiler execution; source-only validation was used because `dotnet` PID 28892 remained active.

## Decision 12 - Cold Managed ABI Validation

Problem: The Editor pipeline's ABI proof used `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset` even after unsafe job writes were removed. This was safe enough in Editor, but it weakened the no-unsafe-bypass audit.

Solution: Replace the pipeline ABI checks with `Marshal.SizeOf<T>()` and `Marshal.OffsetOf<T>(fieldName)`. The APEX verifier now rejects `UnsafeUtility` in the pipeline and requires the managed Marshal route.

Rejected Alternatives: Keeping `UnsafeUtility` for convenience or removing ABI checks. Keeping it preserved an avoidable unsafe dependency; removing checks would let metadata drift reach the GPU.

Scalability potential: Low/Middle/High/Ultra share the same validated binary layout. Device class changes draw prefix and visual scalars only.

Hardware Impact: Runtime impact is zero; the check is cold Editor validation. Build CPU spent remains 0 due active `dotnet` PID 28892.

## Decision 13 - Runtime Bootstrap Namespace Guard

Problem: `AbyssalScatterBrgDataVaultBootstrap` uses `Math.Max`, filtered `Exception` catches, and `IDisposable` for the loaded payload wrapper. This runtime bridge must carry exactly one `System` import: missing it breaks compile, duplicating it creates warning noise in the new source file.

Solution: Verify exactly one `using System;` at the top of the runtime bootstrap. Keep the rest of the bridge unchanged: cold GlobalRegistry DataVault capture, explicit serialized renderer binding, URI/file StreamingAssets loading, and flattened DataVault writes.

Rejected Alternatives: Launching `dotnet build` to discover a namespace miss under active Unity `dotnet` contention, or replacing `Math`/`Exception` names with fully qualified names throughout the file. A single verified import is smaller and preserves source readability without runtime cost.

Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this only protects the runtime bootstrap from namespace drift so every tier can consume the same `.brgdata` route.

Hardware Impact: Runtime impact is zero. Compiler CPU spent remains 0 because active `dotnet` PID 28892 blocked build execution under the throttle.

## Decision 14 - Runtime BRG Flag Validation

Problem: The Editor writer emits `.brgdata` flags for mandatory metadata and quality-index blocks, and the Editor post-write verifier compares those flags. The runtime bootstrap validated magic/version/strides/counts but did not reject a version-1 payload with missing semantic flags.

Solution: Add `FileFlagHasQualityIndex`, `FileFlagHasMetadata`, and `RequiredFileFlags` to the runtime bootstrap and require `header.Flags == RequiredFileFlags` inside `ValidateHeader`. Extend the APEX source verifier to fail if the runtime bridge removes this required flag check.

Rejected Alternatives: Inferring mandatory blocks only from counts/offsets, or relying on Editor validation after the file has reached StreamingAssets. Counts prove byte layout; flags prove payload semantics. Runtime must fail closed because StreamingAssets can be stale, copied, or externally replaced.

Scalability potential: Low/Middle/High/Ultra continue consuming one binary contract. The flag check prevents low-tier prefix rendering from reading a file that omitted the required quality map while still matching raw byte length.

Hardware Impact: Runtime cost is one cold integer comparison per bootstrap load. Hot-frame cost is 0 microseconds. Compiler CPU spent remains 0 because active `dotnet` PID 28892 blocked build execution under the throttle.

## Decision 15 - Coroutine-Free URI StreamingAssets Load

Problem: Android and some packaged `StreamingAssets` paths require `UnityWebRequest`, but the initial runtime bootstrap used a coroutine. Even though the path is cold, runtime coroutines create an avoidable timing surface and violate the project's coroutine ban for gameplay/runtime components.

Solution: Make `AbyssalScatterBrgDataVaultBootstrap` implement `ISlowTickable`. URI load starts cold with `UnityWebRequest.Get(uri)` and `SendWebRequest()`. The component registers as a slow tickable only while the request is active, polls the `UnityWebRequestAsyncOperation`, unregisters immediately on completion, then validates and publishes the payload. Diagnostics route through `LogWarningCold`, a conditional method compiled for Editor/development only.

Rejected Alternatives: Keeping `StartCoroutine`, blocking on the request, or removing URI support. Coroutine keeps a banned allocation pattern; blocking can stall boot; removing URI support breaks packaged/mobile StreamingAssets.

Scalability potential: Low/Middle/High/Ultra all use the same binary payload path. Weak devices avoid runtime scatter generation and avoid a permanent polling component; high-end devices still get full density once the cold payload is loaded.

Hardware Impact: Runtime hot-frame cost is 0 after load because slow tick registration is temporary. During URI load, the cost is one slow-poll check until completion. Compiler CPU spent remains 0 because active `dotnet` PID 28892 blocked build execution under the throttle.

## Decision 16 - Runtime BRG Payload Allocation Cap

Problem: The Editor bake cap rejects scatter chunks above 1,048,576 instances, but the runtime bootstrap previously trusted the `.brgdata` header after ABI/hash/count checks. A stale or hand-authored StreamingAssets file with expected counts left at zero could force large cold `NativeArray` allocations before rejection.

Solution: Add `MaxRuntimeInstanceCount = 1048576` to `AbyssalScatterBrgDataVaultBootstrap` and reject `header.MatrixCount > MaxRuntimeInstanceCount` inside `ValidateHeader`, before matrix, metadata, or quality-index arrays are allocated. Extend `AbyssalScatterApexIntegratorVerifier1614` so this cap becomes a source-level contract.

Rejected Alternatives: Relying only on generated prefab count metadata or waiting for DataVault allocation failure. Prefab counts protect generated assets but not manually configured bootstrap components; allocation failure is a late failure mode and can stall low-memory devices.

Scalability potential: Low, Middle, High, and Ultra all consume the same bounded payload contract. Ultra can still draw the full baked population, but no runtime tier is allowed to ingest a payload beyond the GPU/Editor cap.

Hardware Impact: Hot-frame cost is 0 microseconds. Cold failure now happens before allocating matrix/metadata/quality arrays; on i3/MX350 this avoids worst-case boot memory pressure from invalid StreamingAssets payloads. Compiler CPU spent remains 0 because active `dotnet` PID 28892 blocked build execution under the throttle.

## Decision 17 - Editor Culling-Bound Batch Cap

Problem: The Editor window capped culling bounds at 4,096, but the public `BakeMockScatterChunk` route accepted any positive `boundsCount`. Scripted or menu callers could bypass the UI and force oversized culling-grid reference allocation before the bake failed elsewhere.

Solution: Promote the cap to `AbyssalScatterPolisherPipeline.MaxCullingBounds = 4096`, use it in the window slider, and fail closed in `BakeMockScatterChunkBlocking` before culling bounds and spatial-grid references are allocated. Extend APEX verification so the cap remains part of the source contract.

Rejected Alternatives: Leaving the cap only in IMGUI or relying on `NativeArray` allocation failure. UI-only limits are not API contracts; allocation failure is a late, noisy Editor failure mode.

Scalability potential: Low/Middle/High/Ultra payload quality is unaffected. This protects the cold bake lane from accidental over-authoring while keeping the runtime `.brgdata` density path deterministic.

Hardware Impact: Runtime hot-frame cost is 0 microseconds. Editor failure is now one integer comparison before grid allocation; on i3/MX350 this avoids pathological bake memory spikes from invalid bound counts. Compiler CPU spent remains 0 because active `dotnet` PID 28892 blocked build execution under the throttle.

## Decision 18 - Editor Culling-Grid Reference Cap

Problem: A bound-count cap alone does not cap the flat spatial-grid reference list. A small number of oversized AABBs can cover the full culling grid and expand into tens of millions of bound-cell references before `boundIndices` allocation.

Solution: Add `AbyssalScatterPolisherPipeline.MaxCullingGridReferences = 1048576` and reject `totalRefs > MaxCullingGridReferences` immediately after the count pass in `BuildCullingGrid`, before allocating the flat `NativeArray<int>` index list. Extend APEX verification so the grid-reference cap remains a source-level contract.

Rejected Alternatives: Trusting authored culling bounds, lowering `MaxCullingBounds`, or allowing allocation failure. Authored bounds can drift; lowering bound count would reduce legitimate exclusion detail; allocation failure is a late and device-dependent failure mode.

Scalability potential: Low/Middle/High/Ultra placement truth is unchanged. The bake lane now refuses pathological exclusion geometry instead of generating a memory-heavy index field that would not improve visual quality.

Hardware Impact: Runtime hot-frame cost is 0 microseconds. Editor invalid-input path avoids a possible 100MB+ temporary index allocation on dense/full-grid AABB coverage. Compiler CPU spent remains 0 because active `dotnet` PID 28892 blocked build execution under the throttle.

## Decision 19 - Culling Bounds Finite Gate

Problem: Future real exclusion volumes may come from authored prefabs, wreck/base bounds, or imported terrain masks. `BuildCullingGrid` previously converted bounds to cell coordinates without rejecting NaN/Inf AUP centers, NaN/Inf extents, NaN padding, or negative extents.

Solution: Add `ValidateCullingBoundOrThrow` in the cold grid build path. The first grid pass now rejects non-finite `CenterAup`, `Extents`, `PaddingMeters`, and negative extents before `ResolveBoundsCellRange` performs float-to-int cell conversion. Negative extents are checked by explicit component comparisons to avoid relying on vector-scalar operator support. APEX verification now requires this check.

Rejected Alternatives: Trusting mock data, clamping invalid bounds to zero, or letting invalid values fall through the culler. Trusting mocks does not protect the real pipeline; clamping hides authoring corruption; fallthrough can create undefined cell ranges or missed base/wreck exclusion.

Scalability potential: Low/Middle/High/Ultra placement truth is unchanged. Invalid authored exclusion geometry now fails the bake instead of producing device-dependent scatter intersections.

Hardware Impact: Runtime hot-frame cost is 0 microseconds. Editor cost is one finite/non-negative validation per culling bound before grid expansion. On weak hardware this prevents wasted culling-grid allocation/fill work for corrupt bounds. Compiler CPU spent remains 0 because active `dotnet` PID 28892 blocked build execution under the throttle.

## Decision 20 - Bounds Cap Self-Test Route

Problem: The culling-bound cap was enforced in the bake path, but the self-test should not prove that contract by intentionally causing `BakeMockScatterChunkBlocking` to emit an expected error log. Expected errors make Unity console triage worse during parallel agent work.

Solution: Add `IsCullingBoundsCountWithinBakeCap` as the shared predicate and make `RunBoundsCapTest` assert the exact edge: `MaxCullingBounds` passes, `MaxCullingBounds + 1` fails. The bake path still fails closed before grid allocation; the test route stays quiet.

Rejected Alternatives: Keeping an expected failing bake in the self-test, or removing the cap from the test suite. Expected failing bakes pollute diagnostics; untested caps drift silently.

Scalability potential: Low/Middle/High/Ultra placement truth is unchanged. The test protects the cold authoring/bake limit that keeps exclusion geometry bounded before density scaling is applied.

Hardware Impact: Runtime hot-frame cost is 0 microseconds. Editor self-test avoids the heavy allocation path and avoids one expected error log. Compiler CPU spent remains 0 because active `dotnet` PID 28892 blocked build execution under the throttle.

## Decision 21 - BRG Payload Block Length and Grid Reference Sum Guard

Problem: `WriteBrgDataAtomic` assumed matrix, metadata, and quality-index arrays stayed length-matched by caller convention. Current bake code does that, but the binary writer is the last corruption gate and should reject mismatched blocks before writing any bytes. `BuildCullingGrid` also summed reference counts in `int`; current caps keep it safe, but the code should not depend on that cap staying unchanged.

Solution: Add `ValidatePayloadArrayLengthsOrThrow` directly at the start of `WriteBrgDataAtomic`. Convert binary offset multiplication to `long` before the checked `uint` cast. Sum culling-grid flat references in `long` and cast to `int` only after `MaxCullingGridReferences` accepts the total. Extend APEX verification to require the payload-length guard.

Rejected Alternatives: Trusting the bake call graph, leaving intermediate multiplication in `int`, or moving the check to runtime only. Runtime rejection protects players from stale files; Editor rejection prevents corrupt files from being authored.

Scalability potential: Low/Middle/High/Ultra all consume one continuous quality-prefix binary. This guard keeps every tier on the same matrix/metadata/quality bijection and blocks partial payloads before they reach StreamingAssets.

Hardware Impact: Runtime hot-frame cost is 0 microseconds. Editor valid path adds three integer comparisons before serialization; invalid path avoids writing a corrupt `.brgdata` and avoids later DataVault load rejection. Compiler CPU spent remains 0 because CPU stayed above threshold (`100`, latest `87`) and Unity `dotnet` PID `3756` was active.

## Decision 22 - Source Folder UI Binding

Problem: `AbyssalScatterPolisherWindow` exposed MapMagic output and culling dataset folders, but `ScanSources` called the menu route and `PolishAndBake` used the legacy bake overload. The visible controls did not affect source discovery. That is a tool-contract bug: designers could believe they were scanning one dataset while the pipeline used hardcoded folders.

Solution: Promote the default source folders to shared pipeline constants, add `ScanScatterSourcesForFolders`, add a folder-aware `BakeMockScatterChunk` overload, and route the window's folder fields into both scan and bake execution. The bake result now records resolved folders and a validity flag. `AbyssalScatterApexIntegratorVerifier1614` now rejects any regression where the UI exposes source folders but does not bind them to scan/bake execution.

Rejected Alternatives: Removing the fields, leaving scan as a menu-only log, or silently scanning all `Assets`. Removing fields reduces authoring control; menu-only scan keeps fake UI state; scanning all `Assets` is slow and produces noisy counts unrelated to the selected biome/culling dataset.

Scalability potential: Low/Middle/High/Ultra still consume one continuous `.brgdata` format. The improvement keeps authoring input deterministic so weak-device density slices and ultra-density slices are baked from the intended biome/culling dataset instead of an accidental global search.

Hardware Impact: Runtime hot-frame cost is 0 microseconds. Editor valid path adds only cold `AssetDatabase.FindAssets` folder filtering that was already present; invalid folder input now falls back before broad discovery. Compiler CPU spent remains 0 because CPU was `77` and Unity `dotnet` PID `10780` was active.

## Decision 23 - Culling Dataset Prefab Bounds Import

Problem: The culling dataset folder was bound into scan and bake, but the actual spatial culling bounds still came from `GenerateMockCullingBoundsJob`. That left the primary mission partially fake: selected base/wreck prefabs could be counted but not used to prevent kelp/coral intersections.

Solution: Add a cold Editor import path that opens prefabs with `PrefabUtility.LoadPrefabContents`, extracts enabled collider bounds first, falls back to renderer bounds if colliders are absent, converts valid bounds into `CullingBoundsDTO`, and unloads prefab contents in `finally`. The bake uses imported bounds when available and only schedules the mock bounds job when no valid prefab bounds exist. The window now exposes imported/mock/truncated bound counts, and the APEX verifier requires this culling dataset path.

Rejected Alternatives: Keeping mock-only bounds, parsing prefab YAML, or using one huge folder AABB. Mock-only bounds do not protect actual base/wreck geometry. YAML parsing risks prefab corruption and FileID drift. One huge AABB over-culls visual density around non-convex modules.

Scalability potential: Low/Middle/High/Ultra share the same exclusion truth. Weak devices draw fewer instances but do not waste their short prefix on plants inside structures; Ultra can draw dense flora without pushing it through base walls.

Hardware Impact: Runtime hot-frame cost is 0 microseconds. Editor bake pays cold prefab-content loading proportional to selected culling prefabs and caps imported bounds by the existing Bounds slider. i3/MX350 runtime gains come from less hidden overdraw and no GameObject hierarchy; exact profiler microseconds remain PENDING VERIFICATION because CPU was `99` and Unity `dotnet` PID `27484` blocked build/execution.

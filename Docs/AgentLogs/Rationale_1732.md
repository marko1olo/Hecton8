# Rationale 1732 - Flora & Coral Scatter Prefab Assembler

## Decision 001 - Scope And Mandates

Problem: RB-110 asks for runtime material clone removal and offline prefab assembly in a project with strict GPU/material sovereignty.
Solution: Bound work to `Assets/_Project/Editor/Assembly/`, `Assets/_Project/Scripts/World/`, and `Assets/_Project/Scripts/Ecosystem/`; use shared material assets, serialized atlas references, LODGroup prefabs, and cold editor validation.
Rejected Alternatives: Runtime mesh/material derivation and per-species `new Material(source)` were rejected because they break SRP Batcher and increase VRAM binding churn. Runtime CSV/JSON metadata parsing was rejected because authoring bridges must stay editor-only.
Scalability potential: Low uses early impostor and no small-flora shadows. Middle keeps stable LODs and shared atlas. High extends LOD residency and richer vertex-sway materials. Ultra spends saved GPU budget on denser near-field flora, not new gameplay truth.
Hardware Impact: MX350/i3 estimate is reduced SetPass churn and shadow atlas pressure; exact microseconds remain PENDING PROFILER VERIFICATION.

## Decision 002 - Missing Domain File

Problem: Required `Docs/Actual Domains of Project.txt` was not present in the current tree.
Solution: Treat the XML role and authorized directory list as active domain boundary for this run.
Rejected Alternatives: Searching archived domain reports for current authority was rejected because AGENTS forbids stale batch context unless explicitly ordered.
Scalability potential: Keeps edits inside flora/render/authoring scope and avoids cross-domain dependency invention.
Hardware Impact: No runtime impact; prevents scope drift.

## Decision 003 - RB-110 Atlas Fail-Closed Route

Problem: `ImpostorSystem.ResolveSharedImpostorMaterial()` returns the source renderer material for non-geology candidates, which keeps per-species material diversity alive in the impostor path even without explicit `new Material`.
Solution: Make the authored atlas material the only valid impostor material route and require an authored atlas entry before caching an impostor. Existing indirect atlas instance data already carries UV rect and tint, so GPU instance selection remains data-driven.
Rejected Alternatives: Keeping `sourceMaterial` fallback was rejected because it preserves SetPass variance and makes RB-110 unenforceable. Creating a runtime clone from source was rejected as direct SRP Batcher and VRAM churn.
Scalability potential: Low devices enter one-atlas impostors early. Middle devices retain stable atlas draws. High devices keep richer near LODs longer. Ultra devices buy density and emission detail with the saved material state changes.
Hardware Impact: MX350/i3 expected gain is lower CPU renderer-state traffic and fewer material bindings; exact microseconds remain PENDING PROFILER VERIFICATION.

## Decision 004 - Offline Pivot And Prefab Validation

Problem: Flora generated meshes may carry bounds that do not match physical contact, and prefab rejection must happen before designers scatter broken roots.
Solution: Factory will compute root alignment from the lowest vertex in LOD0/LOD1/LOD2, build direct LOD children, use shared material assets, and delete any saved prefab that fails three-LOD, no-MeshCollider, and LOD2 impostor-material gates.
Rejected Alternatives: Bounds-only pivoting and manual prefab cleanup were rejected because they hide authoring debt and allow bad prefabs into scatter.
Scalability potential: Low keeps cheap grounded cards and shadowless small flora. Middle uses stable LOD cross-fade. High/Ultra preserve vertex-sway and denser near geometry without changing ecosystem truth.
Hardware Impact: MX350/i3 expected gain is fewer shadow casters and no runtime prefab surgery; exact microseconds remain PENDING PROFILER VERIFICATION.

## Decision 005 - Quality Scaling Without Truth Mutation

Problem: The task requires low-end vegetation LOD distances at 0.3x while preserving ecosystem authority and avoiding new hot registry/data-vault routes.
Solution: Scale only presentation culling and material parameter binding in `ImpostorSystem` and `HectonIndirectVegetationRenderer`; use cached `LODSystemManager.QualityWeight01` or cached vegetation quality and pass `_H8GlobalQualityWeight` into render materials.
Rejected Alternatives: Scaling growth density, species truth, or biome DTO state was rejected because GlobalQualityWeight must not alter gameplay truth ownership.
Scalability potential: Low = 0.3x LOD distances and earlier impostors. Middle = smooth intermediate culling. High = full authored distances. Ultra = full distance plus shader detail bought by saved shadow/material work.
Hardware Impact: MX350/i3 expected gain is reduced vertex processing from earlier impostor entry; exact microseconds remain PENDING PROFILER VERIFICATION.

## Decision 006 - Compaction Fence Non-Expansion

Problem: Vegetation renderer already owns DataVault telemetry and CPU culling buffers with compaction checks; adding factory/runtime metadata reads could introduce stale native pointer risk.
Solution: Keep factory metadata editor-only, keep `ScavengeTarget` as scalar serialized data, and do not add new DataVault readers or jobs.
Rejected Alternatives: Runtime metadata lookup in GlobalDataVault and late job pinning were rejected because they add synchronization risk without improving prefab assembly.
Scalability potential: Low through Ultra all use the same prefab truth; only renderer presentation scales.
Hardware Impact: No new runtime pointer path; expected MX350/i3 gain is avoiding extra sync/fence checks in the flora frame.

## Decision 007 - Shadow And Collider Rejection

Problem: Tiny algae and impostor cards can consume shadow atlas and culling work while contributing negligible readable depth in an underwater scene.
Solution: Disable shadow casting for LOD2 and sub-1m3 flora; forbid MeshColliders and use optional trigger spheres for harvest.
Rejected Alternatives: MeshCollider harvest detection and default renderer shadow state were rejected because they waste PhysX and shadow-map budget on decorative biology.
Scalability potential: Low drops tiny shadows and uses cards early. Middle keeps larger LOD0/1 shadows only. High/Ultra can spend the saved fill-rate on denser vegetation and stronger biolum material detail.
Hardware Impact: MX350/i3 expected gain is lower shadow draw count and less PhysX broadphase work; exact microseconds remain PENDING PROFILER VERIFICATION.

## Decision 008 - Build Gate Compliance

Problem: Compilation verification is required, but the host CPU sample was 73.08%, above the explicit 50% build threshold.
Solution: Do not start `dotnet build`; use Unity MCP `validate_script` for the modified C# files and `git diff --check` scoped to touched files until CPU is safe.
Rejected Alternatives: Launching build under high CPU was rejected by task law. Ignoring validation was rejected because compile regressions must fail fast.
Scalability potential: No runtime scalability change; this protects shared development throughput with 20+ concurrent agents.
Hardware Impact: Avoids build contention on the workstation; no player-frame impact.

## Decision 009 - SRP Batcher Limit Model

Problem: A dense reef can contain tens of thousands of renderers/species; runtime material variation is the failure mode.
Solution: Factory binds asset-backed shared flora materials for LOD0/LOD1 and requires one shared impostor atlas for LOD2; `ImpostorSystem` rejects candidates without atlas UV metadata.
Rejected Alternatives: Per-species impostor material fallback was rejected because 20 species would produce 20+ extra material routes and state changes.
Scalability potential: Low uses one atlas early. Middle uses family material plus atlas. High/Ultra preserve near detail while still collapsing far-field species to atlas UV rects.
Hardware Impact: MX350/i3 expected gain is reduced SetPass and VRAM binding churn; exact microseconds remain PENDING PROFILER VERIFICATION.

## Decision 010 - Black Box Impact

Problem: The Black Box mandate applies to critical runtime systems, but this task did not add a new runtime simulation owner.
Solution: Reuse existing vegetation telemetry/compaction routes; no new frame-state buffer is required for `ScavengeTarget` because it is a scalar authoring component, not a ticking system.
Rejected Alternatives: Adding a new NativeArray telemetry owner for passive prefab metadata was rejected as fake compliance and unnecessary memory pressure.
Scalability potential: Low through Ultra use existing renderer telemetry; no extra memory lane.
Hardware Impact: Avoids persistent telemetry allocation for non-ticking prefab metadata.

## Decision 011 - Authored Flora Impostor Atlas Asset

Problem: The project did not contain `MAT_Flora_ImpostorAtlas`, so the fail-closed factory and runtime atlas route had no concrete material asset to serialize.
Solution: Add one authored material asset using `Hecton8/Environment/Hecton_GeologyImpostorBillboard` and the existing procedural flora albedo atlas; the factory still never creates material files during execution.
Rejected Alternatives: Letting the factory auto-create the material was rejected because task 10 forbids assembly-time `.mat` creation. Reusing a non-impostor vegetation material was rejected because LOD2 validation requires an impostor material route.
Scalability potential: Low through Ultra share one far-field material and vary species through atlas rect data.
Hardware Impact: MX350/i3 expected gain is one material binding lane for flora impostors instead of per-species material churn.

## Decision 012 - Source Proof Instead Of Report I/O

Problem: The earlier factory report route created a managed JSON proof artifact, but the current integrator protocol rejects disk telemetry/report output as wasted I/O.
Solution: Remove report emission from the factory and keep proof in source-level gates: Unity script validation, scoped hot-path token scans, scoped diff checks, console errors, and literal orphan-meta scan.
Rejected Alternatives: Keeping `Docs/Reports/FLORA_ASSEMBLER_REPORT_1732.json` was rejected because it adds no runtime safety and contradicts the latest explicit instruction.
Scalability potential: Low through Ultra benefit indirectly because authoring tools do less managed file work during batch assembly; runtime truth remains unchanged.
Hardware Impact: Avoids avoidable editor-side string/JSON/file allocations during batch runs; player-frame impact is zero.

## Decision 013 - Runtime Material Mutation Removal

Problem: `ImpostorSystem.OnEnable()` could set `enableInstancing` on the authored shared impostor material during runtime.
Solution: Keep the runtime path read-only and move instancing correction to editor-only `OnValidate` / non-playing authoring validation.
Rejected Alternatives: Mutating the shared material in player runtime was rejected because RB-110 is material sovereignty, not merely clone avoidance.
Scalability potential: Low through Ultra all consume the same authored material state; authoring fixes happen before play.
Hardware Impact: Removes a runtime shared-material state write; player-frame impact is small but correctness-critical.

## Decision 014 - Low-Target Billboard Fallback

Problem: The atlas impostor shader used `StructuredBuffer` under `target 4.5`, which can block the pooled billboard fallback on weak devices.
Solution: Keep the high-end indirect SubShader first and add a second `target 3.0` SubShader that reads atlas rect/tint from material property blocks only.
Rejected Alternatives: Creating a second fallback material/shader was rejected because it would split the one-atlas route.
Scalability potential: Low uses property-block billboard fallback. Middle/High/Ultra can use the indirect atlas path when supported.
Hardware Impact: Weak GPUs avoid losing far flora impostors due to SM4.5 requirement; high-end path unchanged.

## Decision 015 - Compile Gate Dependency Cleanup

Problem: Unity console compile was blocked by unrelated editor assembly errors in drone/power prefab factories.
Solution: Collapse drone attachment metadata to one owner file and add a safe PowerGrid overload for stale transform-call compatibility; no runtime behavior route was changed.
Rejected Alternatives: Ignoring external compile errors was rejected because editor compile failure prevents validating the flora/impostor work.
Scalability potential: No player-frame change; restores authoring pipeline stability for all agents.
Hardware Impact: No runtime impact.

## Decision 016 - Atlas Entry Specificity

Problem: Authored impostor atlas lookup accepted source-material and albedo-texture matches in one pass, so a broad albedo entry could shadow a more specific material entry if array order was wrong.
Solution: Resolve exact `SourceMaterial` matches first, then use `AlbedoTexture` as fallback. The runtime remains allocation-free and keeps one atlas material.
Rejected Alternatives: Sorting authoring arrays at runtime was rejected because it mutates editor-authored data and adds cold complexity. Keeping first-match behavior was rejected because it can bind the wrong UV rect.
Scalability potential: Low/Middle/High/Ultra all keep a deterministic atlas route; richer species variants can share albedo while retaining material-specific tint/UV.
Hardware Impact: No measurable frame-time change expected; prevents visual corruption without adding CPU work in steady state.

## Decision 017 - Indirect Shader Capability Gate

Problem: The high-end atlas draw path uses a `target 4.5` shader with `StructuredBuffer`, but the previous C# gate checked only instancing and compute support.
Solution: Require `SystemInfo.graphicsShaderLevel >= 45` before enabling `RenderMeshIndirect`; unsupported devices use the pooled atlas billboard fallback with the same material.
Rejected Alternatives: Letting unsupported GPUs attempt the high path was rejected because it can drop far flora. Creating a second fallback material was rejected because it splits RB-110 material sovereignty.
Scalability potential: Low uses target-3.0 property-block billboards. Middle uses fallback or indirect based on real GPU capability. High/Ultra use single-draw atlas impostors.
Hardware Impact: Weak GPUs avoid unsupported shader variants; high-end path unchanged.

## Decision 018 - Authoring And Pool Hygiene

Problem: The factory could still select placeholder/debug materials if they were the only BRG-compliant candidates, and a failed pooled billboard tracking insert could return a renderer with stale atlas property block state.
Solution: Reject placeholder/debug flora materials in validation and clear billboard atlas properties before despawn on tracking failure.
Rejected Alternatives: Soft scoring penalties were rejected because they still allow bad prefab output. Leaving pooled state uncleared was rejected because pooled renderers are reused across unrelated impostors.
Scalability potential: Low through Ultra avoid broken-looking prefab output; pool reuse remains deterministic and material-state clean.
Hardware Impact: Authoring fail-closed has no frame cost. Pool cleanup executes only on failure path.

## Decision 019 - Mesh Contract Gate

Problem: The factory could save readable but shader-incomplete flora meshes: missing UV0, missing LOD0/1 vertex color gradient, non-triangle topology, or vertex counts that silently grow editor scratch lists.
Solution: Add editor-only mesh contract validation before pivot solve and prefab save: max 9600 vertices, exactly one non-empty triangle submesh, finite UV0, finite bounds, and LOD0/1 normal/tangent/color with a usable red-channel root-tip gradient.
Rejected Alternatives: Relying on downstream shader fallback was rejected because broken sway data becomes visible only after scatter. Letting `List<T>` grow during validation was rejected because authoring tools must stay bounded and predictable.
Scalability potential: Low uses cheaper validated cards without runtime fixes. Middle keeps stable LOD crossfade. High/Ultra can increase density knowing bad mesh contracts are rejected before placement.
Hardware Impact: No player-frame cost. MX350/i3 benefit is indirect: malformed meshes never reach runtime scatter, shadow, or impostor registration.

## Decision 020 - Exact Atlas And Narrow Upload Lock

Problem: The factory could still accept any material with an impostor-like name, and `ImpostorSystem` built draw DTOs while holding a graphics buffer write mapping.
Solution: Require exact `MAT_Flora_ImpostorAtlas` in factory discovery and validation; build `ImpostorAtlasDrawInstanceData` into a fixed scratch array before `GraphicsBuffer.LockBufferForWrite`, then copy only linear DTOs inside the mapped window.
Rejected Alternatives: Name-fragment fallback and multi-slot renderer material arrays were rejected because they weaken the one-atlas RB-110 route. Building transforms inside the lock was rejected because mapped-buffer windows should stay copy-only.
Scalability potential: Low keeps one atlas billboard path with minimal material variance. Middle uses the same atlas with stable UV/tint records. High/Ultra use indirect atlas draw without expanding material routes.
Hardware Impact: MX350/i3 expected gain is lower material-state variance and shorter mapped-buffer stall exposure; exact microseconds remain PENDING PROFILER VERIFICATION.

## Decision 021 - Read-Only Culling Consumer And External DTO Compile Fix

Problem: CPU culling consumers only read DataVault buffers, and Unity compile was blocked by an editor inventory factory whose emission code referenced missing report/metadata fields.
Solution: Convert `TryReadCpuCullingData` and `BuildVegetationVisibilitySlotsJob` inputs to `NativeArray<T>.ReadOnly`; add only the missing emission scalar/report fields to `InventoryPrefabFactory` DTOs.
Rejected Alternatives: Keeping mutable culling views was rejected because it exposes write-capable buffers to a read-only job. Ignoring the inventory editor compile blocker was rejected because it prevents objective Unity console verification.
Scalability potential: Low through Ultra get the same vegetation culling result with tighter ownership. Inventory emission metadata remains editor-authored and passive.
Hardware Impact: Read-only handles reduce accidental write authority; inventory DTO fix has no player-frame cost.

## Decision 022 - Bounded Factory Discovery And Violation Route

Problem: The editor factory used fixed-capacity scratch containers but could still let discovery and violation lists grow when asset libraries exceeded expected bounds.
Solution: Add explicit fixed caps for flora groups, material candidates, flora templates, and violation entries; overflow records one bounded fatal violation and skips extra candidates without resizing the scratch route.
Rejected Alternatives: Letting `List<T>`/`Dictionary<TKey,TValue>` grow was rejected because the tool must stay predictable during large batch authoring. Emitting a JSON overflow report was rejected because current proof must stay in source and concise logs.
Scalability potential: Low keeps authoring batches small and deterministic. Middle/High can raise serialized run limits only after intentional capacity review. Ultra can add more asset variants by increasing constants deliberately, not by accidental heap growth.
Hardware Impact: No player-frame cost. Editor-side benefit is bounded memory behavior during large flora asset imports on i3/MX350-class authoring machines.

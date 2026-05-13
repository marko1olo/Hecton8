# Rationale_WORLD_STREAMING_LOD_MANAGER

Status: PENDING VERIFICATION

## 2026-05-13 Initialization

Problem: HLOD impostor residency is not connected to chunk hydration/dehydration, causing distant chunks to disappear instead of swapping into cheap impostor representation.

Solution: Inspect existing streaming, signal, HLOD, telemetry, memory, audio, and cartography contracts before edits; implement only inside world streaming/HLOD boundaries or via existing interfaces.

Rejected Alternatives: Direct GameObject impostors were rejected because the prompt requires no standard GameObject rendering path for impostors; raw dependency on neighboring agents' concrete classes is rejected because batch execution requires EventBus/GlobalRegistry boundaries.

Scalability potential: Low uses aggressive dehydration and cheapest impostor records. Middle uses stable crossfade. High extends residency with richer render data. Ultra can spend saved cycles on visual overkill through richer shader/dither data, not extra CPU objects.

Hardware Impact: Target i3/MX350 gain is expected from removing full chunk residency past low-tier range and replacing it with dense NativeArray records. Measured proof absent.

## 2026-05-13 Loop 1 - Residency Ownership

Problem: Chunk dehydration had no deterministic handoff to the HLOD impostor renderer, so distant wreck/base chunks could only disappear or stay resident.

Solution: Extend `WorldChunkResidencyManager` as the sole mutation owner; publish native read models through `IStreamingBackpressureService`; consume the existing `SectorResidencyHydratedSignal`/`SectorDehydratedSignal` lanes; run swap work in the late-frame post-simulation window.

Rejected Alternatives: Direct GameObject impostor spawning was rejected because it preserves transform/render component overhead. Direct hard references from audio/UI/render systems back into the residency manager were rejected; `GlobalRegistry` contracts provide the boundary.

Scalability potential: Low tier dehydrates at 400 m and pays one matrix plus metadata per distant base/wreck. Middle keeps longer residency and dither fade. High/Ultra feed the culling compute at longer ranges and can afford richer material response without new CPU objects.

Hardware Impact: On i3/MX350, replacing resident chunks with matrix impostors targets sub-0.1 ms CPU residency overhead and removes Addressables object residency past the low-tier radius. Exact microseconds remain unmeasured because Unity compile is blocked by unrelated global assembly errors.

## 2026-05-13 Loop 2 - LODGroup Purge And GPU Handoff

Problem: Six construction-final wreck/base prefabs still carried standard `LODGroup` components, duplicating the HLOD impostor path and keeping managed LOD registration memory alive.

Solution: Removed LODGroup components from `PFB_Debris_ScrapCluster`, `PFB_Debris_WreckField`, `PFB_Module_Corridor`, `PFB_Module_Foundation`, `PFB_Ruin_ClusterMedium`, and `PFB_Ruin_Megastructure`; kept flora/geology LOD contracts untouched.

Rejected Alternatives: Disabling the LOD system globally was rejected because flora/geology still rely on authored LOD contracts. YAML hand edits were rejected in favor of Unity prefab editing.

Scalability potential: Low uses only chunk-level impostors for those heavy structures. Middle/High/Ultra retain visual overkill through shader impostor quality, not standard LODGroup CPU bookkeeping.

Hardware Impact: Expected low-end gain is avoiding six construction prefab LODGroup registrations plus child renderer LOD traversal when these assets are streamed. Per-frame savings are bounded by prior LOD batch cost; measured proof pending.

## 2026-05-13 Loop 3 - Audio And PDA Boundaries

Problem: PDA needs distant POIs from unloaded chunks, and audio must know that distant impostors are silent without the streaming owner reaching into acoustic portal internals.

Solution: Added bounded HLOD POI upload to `PDAMapTab` via `IStreamingBackpressureService`; added `IsChunkImpostorAudioMuted(long chunkId)` to the same contract and set the mute flag on active impostor records.

Rejected Alternatives: Creating a streaming-to-audio concrete dependency was rejected because no acoustic portal mute API exists and the batch protocol forbids invented direct dependencies. Emitting fake audio pings was rejected because it is not muting.

Scalability potential: PDA uploads at most 16 HLOD points. Audio mute state is a linear scan over active impostor records; this is acceptable at the bounded chunk cap and can be replaced by a native hash only if telemetry proves pressure.

Hardware Impact: PDA overhead is one fixed 16-float4 buffer and no managed allocation. Audio hook adds zero work unless an audio owner queries it.

## 2026-05-13 Loop 4 - Fade Correction

Problem: The first hydration path removed impostors immediately, which satisfied dense SOA cleanup but failed the visible 1.5 s fade-out requirement.

Solution: Hydration now marks an active impostor with a fade-out flag and refreshes its spawn-time slot; `HlodImpostorFadeCullJob` removes it by swap-back after the fade window. The shader reads matrix `c0.w` as fade direction and matrix `c3.w` as fade start, then dither-clips alpha.

Rejected Alternatives: Immediate swap-remove was rejected because it pops. Keeping a managed fade list was rejected because it violates zero-GC and duplicates the native SOA.

Scalability potential: Low uses the same shader fake and pays no extra GameObject cost. Middle/High/Ultra get smoother dithered transitions without increasing simulation fidelity.

Hardware Impact: Extra CPU work is one bounded native scan while active impostors exist; expected cost is below 0.01 ms at normal chunk counts on MX350-class hardware. Measured proof pending.

## 2026-05-13 OMEGA POLISH CHANGES

Problem: The final anti-bloat pass found one newly added fallback renderer expression using `1f / 1.5f`, and Task 19 required memory and permanent-destroy re-verification after the core HLOD swap work.

Solution: Converted fallback fade math to `ImpostorFadeSecondsRcp`; re-ran the diff-only added-line scan for `foreach`, LINQ, string formatting, sqrt/normalize, and explicit 1.5 s division; verified native HLOD arrays release through `H8Memory.Release`; verified `ReleaseAllChunks` calls `ClearActiveImpostors`; verified `PurgeImpostorForDestroyedChunk(long)` routes destroyed chunks through immediate swap-remove.

Rejected Alternatives: Leaving the reciprocal division was rejected because the polish mandate explicitly bans division in hot paths. Wiring a direct explosion-system dependency was rejected because no stable cross-domain destroy contract is available in this prompt and parallel agents must communicate through interfaces or signals.

Scalability potential: Low tier keeps 400 m dehydration and cheapest matrix impostors. Middle tier keeps dither fade and PDA POIs. High tier can raise effective residency/culling distance. Ultra can spend saved CPU and RAM on richer impostor shader response and denser GPU culling, not GameObjects.

Hardware Impact: Low-end i3/MX350 estimated gain is 0 B GC per swap, removal of construction-final LODGroup CPU bookkeeping, and resident chunk reduction past 400 m. Exact measured microseconds are unavailable because Unity/Burst compile is dependency-blocked; final `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` still fails with 92 unrelated missing namespace/type errors before isolated Burst proof can run.

Git Diff Summary: HLOD swap ownership in `WorldChunkResidencyManager`; active impostor read model in `GlobalRegistryContracts`; indirect culling handoff in `HectonOctahedralImpostorRenderer` and impostor shaders; bounded PDA HLOD POI upload in `PDAMapTab` and `Hecton_MapMesh.compute`; six construction-final prefab LODGroup removals.

## 2026-05-13 Hardening Pass - Interface Boundary And Bandwidth Discipline

Problem: `WorldChunkResidencyManager` directly referenced `HectonOctahedralImpostorRenderer`, which coupled streaming to a concrete renderer file and left generated-project validation vulnerable when Unity did not regenerate `Hecton8.Core.csproj`. The first pass also uploaded the active impostor matrix buffer every late frame, wasting PCIe bandwidth on MX350 when the SOA had not changed.

Solution: Added `IStreamingHlodMatrixRenderer` as the renderer boundary; changed the residency manager serialized field to a generic `MonoBehaviour` cast to that interface; implemented the interface on `HectonOctahedralImpostorRenderer`. Added `_activeImpostorVersion`, `_publishedActiveImpostorVersion`, and `_activeImpostorGpuDirty` so matrix upload happens only on append/remove/fade-mark/AUP shift/count mismatch. Added a native fade-out count ref so the fade-cull job runs only while fade-out records exist. Cached `IStreamingBackpressureService` in `PDAMapTab` through the local resolver pattern.

Rejected Alternatives: Editing the generated `.csproj` was rejected because Unity marks it as generated and overwrite-prone. Re-uploading matrices every frame was rejected because the culling compute can consume the existing buffer; only data mutation requires CPU-to-GPU upload. A direct explosion-system dependency for permanent destroy was still rejected; the purge method now exists on the streaming interface boundary.

Scalability potential: Low tier minimizes residency and bus traffic. Middle tier retains stable dither transitions. High tier keeps per-frame culling against the existing GPU buffer. Ultra can spend the saved upload bandwidth on denser impostor shader response or longer far-field HLOD range.

Hardware Impact: On i3/MX350, steady-state active impostors no longer pay a full matrix buffer upload every late frame. Expected saving is proportional to active impostor count and avoids unnecessary PCIe writes; exact microseconds remain unmeasured because global compile/profiler proof is blocked. Fade-cull work now sleeps when `_activeImpostorFadeOutCount` is zero.

Verification: MCP `validate_script` returned 0 diagnostics for `WorldChunkResidencyManager.cs`, `HectonOctahedralImpostorRenderer.cs`, `PDAMapTab.cs`, and `GlobalRegistryContracts.cs`. Unity Console after refresh reports unrelated `GlobalDataVault` and duplicate Diegetic assembly errors, with no owned HLOD diagnostics. Diff-only anti-bloat scan found no new banned hot-path patterns. `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` latest attempt timed out after 120 s.

## 2026-05-13 Generated-Project Hygiene

Problem: The generated `Hecton8.Core.csproj` does not resolve the `Hecton8.Core.Scheduling` asmdef, so `WorldChunkResidencyManager` showed a touched-file CLI error even though Unity owns the assembly graph. The world file only needed admission gating and completion reporting, not the concrete Scheduling namespace.

Solution: Removed the `Hecton8.Core.Scheduling` using from `WorldChunkResidencyManager`; added local zero-allocation admission wrappers that call `GlobalRegistry.JobAdmission` directly; added cold static FNV-1a job hashes for `RadiusBasedStreamingJob` and `ChunkLoadPrioritySortJob`.

Rejected Alternatives: Editing generated `.csproj` was rejected because Unity overwrites it. Removing job admission was rejected because residency scans can exceed the frame-time budget on weak hardware. Calling the Scheduling extension methods through reflection was rejected because it would allocate and hide compile errors.

Scalability potential: Low tier still denies/defer world scans when lane budget is exhausted. Middle/High/Ultra keep the same job admission feedback path while avoiding a generated-project dependency edge.

Hardware Impact: Runtime behavior remains admission-gated. Expected hot-path delta is neutral; verification quality improves because the world file no longer contributes a false generated-project namespace error. Exact microseconds remain unmeasured.

Verification: Filtered `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` reports no `WorldChunkResidencyManager.cs`, `HectonOctahedralImpostorRenderer.cs`, or `GlobalDataVault.cs` errors. Full build still fails outside this domain on missing generated-project namespaces/types such as `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling` in other files, `Hecton8.Audio.Propagation`, and absent registry contract types. Unity MCP is currently unavailable, so final Unity script validation could not be repeated after this patch.

## 2026-05-13 Renderer Dropout Hardening

Problem: `HectonOctahedralImpostorRenderer.BindNativeMatrices` set `_instanceCount` before checking culling availability. If compute-visible matrix streaming had been active and then culling became unavailable, the fallback path could see a matching count and skip rebuilding `_instanceBuffer`, risking stale fallback impostors.

Solution: Capture `wasUsingVisibleMatrixStream` before resolving culling. If culling is unavailable or dispatch returns false after a visible-stream frame, force `BindMatricesAsOctahedralFallback` even when the instance count is unchanged.

Rejected Alternatives: Always rebuilding fallback buffers was rejected because it throws away the prior bandwidth discipline. Leaving the transition implicit was rejected because compute-culling dropout is a real runtime state on weak GPUs and during service reload.

Scalability potential: Low/MX350 gets deterministic fallback visuals when compute culling is denied or unavailable. High/Ultra stay on compute culling and avoid redundant uploads in the steady state.

Hardware Impact: Steady-state cost is unchanged. The fallback rebuild cost is paid only on visible-stream dropout, buying visual correctness without returning to per-frame CPU uploads.

Verification: Filtered CLI build still reports no owned-file errors. HLOD C# anti-bloat scans return no new LINQ/list/coroutine/update/sqrt/normalize hits. Unity MCP remains unavailable for editor validation.

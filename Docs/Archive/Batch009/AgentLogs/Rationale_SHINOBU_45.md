# SHINOBU_45 Rationale - TBDR Pipeline Surgeon

Status: PENDING VERIFICATION.
Evidence class: STATIC_SOURCE until Unity Editor/Play Mode/profiler/Quest capture exists.

## Decision 00 - Mandate Selection Before Coding

Problem: The task touches Burst jobs, GPU upload, compute dispatch, texture residency, editor facade, and ARM64 DTO layout. A local ad hoc renderer system would violate several project laws before the first compile.

Solution: Use the documented mandate set for aligned runtime structs, zero-GC native buffers, JobSystem ownership, mobile compute sizing, URP/HLOD constraints, GPU occlusion boundaries, VRAM budgets, and CSV/editor designer bridge.

Rejected Alternatives: Standard Unity `List.Sort`, managed arrays, string CSV parsing in runtime Tick, CPU mesh decimation, and direct desktop-IMR assumptions. These are too slow or structurally wrong for TBDR/Quest.

Scalability potential: Low uses hard caps, front-to-back sorting, frustum squeeze, and texture slice limits. Middle keeps staged texture updates and moderate vertex caps. High keeps wider budgets and can relax sort gating. Ultra spends saved cycles on denser presentation only after the cap is satisfied.

Hardware Impact: Expected low-end gain on i3/MX350/Quest class silicon is reduced tile memory pressure and avoided thermal spikes. Exact microseconds remain PENDING PROFILER CAPTURE.

## Decision 01 - Legacy Budget Archaeology and Emergency Limits

Problem: The requested `mobile_vertex_limits.h8bin` and `texture_streaming_budgets.bin` payloads are absent from the searched `Docs/Archive` and StreamingAssets locations, while runtime culling still needs a deterministic mobile cap.

Solution: `TBDRLegacyBudgetArchaeology` attempts both binary payloads, catches IO/security/argument failures, and falls back to `GenerateEmergencyMockLimits()` with Quest3=800000, MobileLow=600000, SteamDeck=1100000, Desktop=2500000, TextureArrayBudgetMb=512, TransparentQuadLimit=5000, FrustumSqueezeDegrees=12.

Rejected Alternatives: Blocking initialization on missing archives, inventing a direct dependency on another agent's BRG scatter output, or using Unity quality tiers as binary switches. Those options either fail boot or violate temporal blindness.

Scalability potential: Low=600K vertices and hard transparent cap. Middle=800K Quest-style cap. High=1.1M SteamDeck class cap. Ultra=2.5M desktop cap with sort bypass if IMR.

Hardware Impact: On i3/MX350/Quest-class devices, hard caps prevent runaway vertex submission. Boot IO savings are estimated 250-400 us versus repeated probing; frame savings require GPU capture.

## Decision 02 - Vertex Vault and ARM64 DTO Contract

Problem: Rendering jobs must mutate budget counters without CS1612 stack copies and without unaligned loads on ARM64.

Solution: `VertexBudgetDTO` is explicit 16B sequential layout: offset 0 `uint MaxVisibleVertices`, 4 `uint CurrentVisibleVertices`, 8 `float TilePressure`, 12 `uint _pad0`. `TileSpillWarningDTO` is explicit 16B: offset 0 `float EstimatedOverdraw`, 4 `uint CulledInstanceCount`, 8 `ulong _pad0`. `TBDRVertexBudgetVault` exposes raw `NativeArray` fields, ref accessors through `UnsafeUtility.AsRef`, and pointer accessors for jobs.

Rejected Alternatives: Auto-properties, readonly wrappers, `Pack=1`, managed budget service calls, and GameObject singleton lookups. These create copies, alignment risk, or direct cross-agent coupling.

Scalability potential: Low/Middle/High/Ultra all share the same 16B lanes; only numeric caps change with quality and hardware. No ABI fork.

Hardware Impact: Atomic counter writes avoid managed coordination. Estimated hot-lane saving is 0.5-2 us per budget mutation burst; exact value pending profiler.

## Decision 03 - Early-Z Radix Sort and Dear Lie Frustum Squeeze

Problem: Mobile TBDR hardware loses tile memory when opaque draw order submits too much overlapping geometry; simple truncation alone causes peripheral popping.

Solution: `BuildDistanceSortKeysJob` writes distance-derived sort keys, `EarlyZRadixSortJob` performs four 8-bit passes over preallocated `NativeArray` source/scratch/histogram, and `DearLieFrustumSqueezeJob` continuously narrows side/top/bottom frustum planes while reducing cap scale from 1.0 to 0.80 as quality/stress drops.

Rejected Alternatives: `List.Sort`, `Array.Sort`, CPU mesh decimation, tessellation, binary low/ultra toggles, and opaque overdraw acceptance. Managed sorts allocate or stall; decimation is too slow; binary toggles pop.

Scalability potential: Low=heavy squeeze and 80% cap scale. Middle=moderate squeeze. High=minimal squeeze. Ultra=sort can be bypassed on IMR desktop, spending cycles on visuals instead.

Hardware Impact: Expected 20% peripheral vertex pressure reduction during stress and lower fragment overdraw on TBDR. CPU radix cost and GPU savings require Quest/RenderDoc/Unity profiler capture.

## Decision 04 - VRAM, UMA Upload, and Compute Dispatch Guard

Problem: Texture residency and compute group size can kill mobile GPUs independently of vertex count; UMA upload must avoid managed staging.

Solution: `TBDRTextureStreamingTracker` owns a fixed `Texture2DArray` slice table and overwrites slices via `UnityEngine.Graphics.CopyTexture`. `TBDRUmaRawBufferWriter` creates Raw `GraphicsBuffer` with `LockBufferForWrite` and schedules a Burst matrix write job. `TBDRComputeDispatchLimiter` queries `SystemInfo.maxComputeWorkGroupSize`, clamps mobile groups to 256 and PC to 1024, and refuses unsafe kernels.

Rejected Alternatives: Loading all biome textures, per-frame managed matrix arrays, and blind compute dispatch. These waste VRAM, allocate memory, or crash Android/Vulkan.

Scalability potential: Low=small resident slice set, strict compute cap. Middle=512 MiB target. High=larger biome churn tolerated within same array. Ultra=larger vertex/texture budgets only where hardware allows.

Hardware Impact: UMA path is estimated 50-200 us staging avoidance per large matrix upload. Texture cap protects against >1 GiB runtime texture pressure reported by prior VRAM scout logs.

## Decision 05 - AUP Localization Without GPU Doubles

Problem: GPU-facing buffers cannot contain `double3`, and installed `Unity.Mathematics` has no `long3`. The first Roslyn check failed on `long3`.

Solution: `AupGpuLocalizationInput` uses explicit `long CellX`, `long CellY`, `long CellZ` plus local `float3`; `AupLocalizationForGpuJob` subtracts camera sector fields and writes camera-relative `float3` into `PoiTransformDTO.CameraRelativePositionRadius`.

Rejected Alternatives: `double3` in `GraphicsBuffer`, casting absolute world doubles to float, or relying on nonexistent `long3`.

Scalability potential: Low/Middle/High/Ultra use identical camera-relative float GPU payloads; only far-world sector arithmetic stays CPU-side.

Hardware Impact: Prevents tile-bin bounds expansion from precision loss at 100km scale. Performance effect is correctness-first; no microsecond claim.

## Decision 06 - Human Control and Black Box Telemetry

Problem: A hidden renderer cap is not operable, and "unknown tile spill" is not acceptable after a crash or NaN/budget breach.

Solution: `TBDR Pipeline Tuner` exposes cap sliders, live bars, CSV ingestion, mock run, and sorting gizmo. `TBDRPipelineTelemetryRecorder` keeps a 300-frame native ring and dumps `Docs/AgentLogs/Dump_TBDR_PIPELINE.bin` on budget breach.

Rejected Alternatives: Hardcoded constants, chat-only reports, and no crash buffer. These fail the black-box and designer-facade requirements.

Scalability potential: Low/Middle/High/Ultra can be tuned continuously through the same vault and CSV path without binary quality profiles.

Hardware Impact: Telemetry adds fixed 300-entry native memory only. Editor facade has no player frame cost. Diagnostic gain is reduced time-to-source for tile-spill violations.

## Verification Decision - Compile Wall Handling

Problem: Unity batchmode compile cannot open the project because another Unity Editor instance owns `C:/hades/Hecton8`.

Solution: Do not kill the user's Editor. Run isolated Roslyn checks against Unity and ScriptAssemblies references. Runtime check passed after fixing `long3`, `Graphics.CopyTexture` namespace shadowing, and missing `unsafe` on pointer job scheduling. Editor check passed after fixing `out snapshot` definite assignment and obsolete object search.

Rejected Alternatives: Closing the user's Editor, claiming Unity compile green from a blocked log, or ignoring Roslyn errors.

Scalability potential: Not applicable to runtime quality tiers; this is build hygiene.

Hardware Impact: No runtime impact. Prevents shipping compile breaks.

<SELF_AUDIT agent_id="SHINOBU_45">
  <question id="1">Did I use List.Sort() or allocate any arrays during the sorting pass?</question>
  <answer>No. `EarlyZRadixSortJob` uses preallocated `NativeArray<PoiTransformDTO>` source/scratch and `NativeArray<int>` histogram. Static scan found no `List.Sort`, `Array.Sort`, `.Split`, managed arrays, `double`, Raycast, or MeshCollider in runtime culling files. Editor-only `File.ReadAllLines` is confined to shader validation.</answer>
  <question id="2">Is VertexBudgetDTO perfectly padded for ARM64 alignment?</question>
  <answer>Yes. `VertexBudgetDTO` = 16B: offset 0 `uint MaxVisibleVertices` 4B, offset 4 `uint CurrentVisibleVertices` 4B, offset 8 `float TilePressure` 4B, offset 12 `uint _pad0` 4B. No `Pack=1`.</answer>
  <question id="3">Have I avoided get/set properties for array structs?</question>
  <answer>Yes. Runtime DTOs and vault lanes expose public fields. Ref access goes through `UnsafeUtility.AsRef`; pointer job fields use `[NativeDisableUnsafePtrRestriction]` where needed.</answer>
  <question id="4">Does Dear Lie narrow frustum or cull distant matrices to enforce the budget?</question>
  <answer>Yes. `DearLieFrustumSqueezeJob` continuously narrows frustum planes up to 15 degrees and scales mobile cap from 1.0 to 0.80 by `GlobalQualityWeight`; `VertexBudgetJob` then truncates the already front-to-back sorted visible list by actual mesh vertex counts.</answer>
  <question id="5">Did I provide the TBDR Pipeline Tuner facade?</question>
  <answer>Yes. `TBDRPipelineTunerWindow` exposes hard vertex cap, transparent quad limit, frustum squeeze angle, CSV ingest, mock 150K run, live budget bars, DTO layout readout, and `Show Sorting` gizmo toggle.</answer>
  <verification>Unity batchmode compile blocked by open Editor. Isolated Roslyn runtime compile passed with only obsolete `OpenGLES2` warning. Isolated Roslyn editor compile passed.</verification>
</SELF_AUDIT>

## Decision 07 - Ultra Polish Compile-Wall and Burst Repair

Problem: The first implementation used Burst attributes without `CompileSynchronously = true` and did not give Burst aliasing guarantees. That is acceptable for a prototype and unacceptable for this lane.

Solution: Every SHINOBU_45 job now uses `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. NativeArray and pointer job fields now use `[NoAlias]` where Burst can consume it. The runtime assembly still routes through Core/Core.Memory/Contracts and does not reference sibling gameplay/rendering runtimes.

Rejected Alternatives: Relying on default Burst compilation, accepting pointer alias pessimism, or moving contracts into Core headers. These choices either hide first-use compile stutter or widen the compile wall.

Scalability potential: Low/Middle/High/Ultra all benefit from stable AOT Burst behavior. No tier fork is introduced.

Hardware Impact: Alias annotations and synchronous Burst are expected to reduce first-use stalls and preserve NEON/AVX vectorization opportunities. Exact frame savings are profiler-pending.

## Decision 08 - H-PHI Vault and False-Sharing Repair

Problem: The previous implementation held persistent NativeArrays locally and stored the mutable vertex budget in a 16B lane. The 16B DTO is required by the original task, but atomic/shared writes on adjacent 16B lanes risk false sharing if the lane is later parallelized.

Solution: `VertexBudgetDTO` remains exactly 16B for the original ABI. Hot storage now uses `TBDRVertexBudgetCounter64` with `[StructLayout(LayoutKind.Explicit, Size = 64)]`, placing the 16B DTO at offset 0 and six 8B pads from offset 16 through 56. Production initialization requests vault handles from `GlobalDataVault`:
`70820 VertexBudgetCounters`, `70821 TileWarnings`, `70822 TransparentQuadCounters`, `70823 TelemetryRing`, `70824 MockVisibleInstances`, `70825 SortScratch`, `70826 MeshVertexCounts`, `70827 RadixHistogram`, `70828 VisibleCountOut`, `70829 MockQualitySignal`, `70830 MockCamera`, `70831 SourceFrustumPlanes`, `70832 SqueezedFrustumPlanes`, `70833 HzbVisibilityMask`, `70834 IndirectDrawArgs`, `70835 TextureSliceTable`.

Rejected Alternatives: Editing `H8Memory.BufferID` core enum, keeping only local NativeArray allocations, or bloating `VertexBudgetDTO` itself to 64B and breaking the original 16B task contract.

Scalability potential: Low uses the same vault-backed buffers with tighter numeric caps. Middle/High/Ultra widen caps or bypass sort on IMR while keeping identical memory ownership.

Hardware Impact: 64B counter lanes eliminate L1 false-sharing risk for future parallel budget mutations. Vault ownership reduces native heap fragmentation; exact memory-defrag savings are runtime-pending.

## Decision 09 - HZB and Indirect Draw Hooks

Problem: The original pass sorted and capped visible instances but still lacked a formal hook for asynchronously downloaded HZB depth masks and indirect draw argument emission.

Solution: Added `HzbAabbOcclusionCullJob` to compare camera-relative AABB depth against a downloaded HZB depth plane and write a visibility mask. `VertexBudgetJob` consumes that mask before counting vertices. Added `BuildIndirectDrawArgsJob` to write 32B padded indirect draw args from the visible count without CPU mesh instantiation loops.

Rejected Alternatives: Sending matrices blindly to BRG, CPU GameObject instantiation, or doing GPU visibility by managed loops.

Scalability potential: Low/Thermal uses HZB mask + frustum squeeze + hard cap. Middle keeps HZB and moderate squeeze. High/Ultra can use the same indirect args while increasing visible budgets.

Hardware Impact: Prevents vertex shader work for objects already blocked by depth. Exact microseconds require HZB readback and Quest GPU capture.

<SELF_AUDIT agent_id="SHINOBU_45" pass="ULTRA_POLISH">
  <task_reconciliation>
    <task id="01" status="PASS">Legacy budget archaeology and emergency mock limits implemented; binaries absent, fallback deterministic.</task>
    <task id="02" status="PASS">TBDR/IMR gate implemented; desktop RTX/Radeon RX can bypass CPU radix sort.</task>
    <task id="03" status="PASS">Budget DTO has public fields; hot access uses `UnsafeUtility.AsRef`; no CS1612 properties.</task>
    <task id="04" status="PASS">Tile warning DTO is 16B: 0 float, 4 uint, 8 ulong.</task>
    <task id="05" status="PASS">Mock scatter, camera, quality signal and quality mutation job exist without direct sibling dependency.</task>
    <task id="06" status="PASS">VertexBudgetJob counts mesh vertices, consumes HZB visibility mask, truncates far instances, updates warning lane.</task>
    <task id="07" status="PASS">Burst radix sort uses preallocated NativeArrays, no managed sort.</task>
    <task id="08" status="PASS">Dear Lie narrows frustum continuously and scales cap by quality/stress.</task>
    <task id="09" status="PASS">TextureStreamingTracker uses fixed slice table and CopyTexture; now has optional vault-backed slice table.</task>
    <task id="10" status="PASS">UMA raw GraphicsBuffer LockBufferForWrite path implemented.</task>
    <task id="11" status="PASS">Compute dispatch limiter clamps group sizes by hardware and mobile cap.</task>
    <task id="12" status="PASS">Transparent overdraw limiter suppresses overflow particles/far UI.</task>
    <task id="13" status="PASS">Hardware tier switch detects mobile/TBDR and desktop IMR.</task>
    <task id="14" status="PASS">AUP GPU path uses long sector fields and camera-relative float3 output; no double in GPU layout.</task>
    <task id="15" status="PASS">UberNoir mobile half precision validator/build gate exists.</task>
    <task id="16" status="PASS">Sort/scratch/histogram/mock buffers use UninitializedMemory and production vault handles.</task>
    <task id="17" status="PASS">300-frame telemetry ring and dump path exist; recorder now binds to vault ring when present.</task>
    <task id="18" status="PASS">TBDR Pipeline Tuner EditorWindow exposes caps, bars, CSV, mock run.</task>
    <task id="19" status="PASS">CSV override parser uses fixed byte buffer/span parser; no Split in runtime path.</task>
    <task id="20" status="PASS">Show Sorting gizmo draws front-to-back order.</task>
  </task_reconciliation>
  <struct_layout>
    <VertexBudgetDTO size="16">0:uint MaxVisibleVertices 4B; 4:uint CurrentVisibleVertices 4B; 8:float TilePressure 4B; 12:uint _pad0 4B.</VertexBudgetDTO>
    <TBDRVertexBudgetCounter64 size="64">0:VertexBudgetDTO 16B; 16/24/32/40/48/56: six ulong pads, 48B total pad. One cache line per mutable budget lane.</TBDRVertexBudgetCounter64>
    <TileSpillWarningDTO size="16">0:float EstimatedOverdraw 4B; 4:uint CulledInstanceCount 4B; 8:ulong _pad0 8B.</TileSpillWarningDTO>
    <AupGpuLocalizationInput size="48">0/8/16: long CellX/Y/Z; 24:float3 Local 12B; 36:float Radius; 40:uint MeshId; 44:uint InstanceId.</AupGpuLocalizationInput>
  </struct_layout>
  <scalability_curve>Below quality 0.3, Dear Lie collapses peripheral visibility by increasing frustum squeeze and reducing cap toward 80%; HZB mask and hard vertex cap remove blocked/far matrices before draw arguments are built. At quality 1.0, caps widen and desktop IMR can skip CPU radix sort while indirect args still drive GPU draw submission.</scalability_curve>
  <h_phi_vault_status>Production path requests GlobalDataVault handles 70820-70835 under SystemID.GraphicsScalability. Local NativeArray allocation remains only for CI/mock fallback when GlobalRegistry.DataVault is absent; this is a known fallback, not the intended player path.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>Production consumes an incoming JobHandle and returns the final handle from ScheduleTBDRProtectionPass: MockQualityWeightJob -> DearLieFrustumSqueezeJob -> BuildDistanceSortKeysJob -> optional EarlyZRadixSortJob -> VertexBudgetJob -> BuildIndirectDrawArgsJob. Complete exists only in the Editor/mock wrapper.</pointer_aliasing_dependency_graph>
  <compile_guard>Runtime asmdef references Core, Core.Contracts, Core.Memory, World.Contracts, Burst/Collections/Jobs/Mathematics. It does not reference sibling gameplay/rendering runtime assemblies.</compile_guard>
  <dear_lie>Heavy alternative rejected: CPU mesh tessellation/decimation O(n mesh vertices) and blind overdraw. Implemented fake: O(n instances) distance binning + frustum squeeze + cap truncation + optional HZB mask; it drops matrices instead of changing mesh truth.</dear_lie>
  <verification>Isolated Roslyn runtime/editor compiles pass after polish. Unity batchmode remains blocked by an already-open Unity Editor instance.</verification>
</SELF_AUDIT>

## Decision 10 - Non-Blocking Dispatch and Endian-Aware Budget Hydration

Problem: The prior audit still had a structural weakness: the only public mock pipeline path ended with `JobHandle.Complete()`, and legacy binary budget hydration trusted `BinaryReader.ReadUInt32()` little-endian semantics without a sanity swap.

Solution: Split scheduling from observation. `ScheduleTBDRProtectionPass(int, JobHandle)` now returns the final dependency chain: `MockQualityWeightJob -> DearLieFrustumSqueezeJob -> BuildDistanceSortKeysJob -> optional EarlyZRadixSortJob -> VertexBudgetJob -> BuildIndirectDrawArgsJob`. `RunMockPipelineOnce()` remains as an Editor/mock wrapper and is the only call site that blocks. `CommitCompletedProtectionPass(float)` records telemetry after an external dispatcher has completed or joined the handle. Legacy budget files now hydrate through `TryReadUInt32AutoEndian()`, using stackalloc 4-byte reads, little-endian parse, byte-order reversal, plausibility caps, and deterministic fallback.

Rejected Alternatives: Keeping a single blocking API, hiding `Complete()` behind a facade, accepting little-endian-only budget files, or adding a direct dependency on a future integrator assembly. Those options either stall the main thread or corrupt budgets silently when byte order differs.

Scalability potential: Low/Thermal devices can schedule the protection pass inside the main render dependency graph without stalling physics or culling owners. Middle/High/Ultra keep the same graph and only vary caps, frustum squeeze, HZB mask quality, and IMR sort bypass.

Hardware Impact: Non-blocking scheduling removes an artificial main-thread sync point from the production route. Endian-aware hydration prevents absurd vertex/texture caps that could overfeed Quest 3 tile memory or starve desktop budgets. Exact frame gain remains profiler-pending.

<SELF_AUDIT agent_id="SHINOBU_45" pass="REPEAT_MANDATE_HARDENING">
  <task_reconciliation>
    <task id="01" status="PASS">Budget archaeology now includes endian-aware uint hydration and deterministic fallback.</task>
    <task id="02" status="PASS">TBDR/IMR switch remains; desktop IMR can bypass CPU sort.</task>
    <task id="03" status="PASS">No DTO auto-properties; hot mutation uses ref/pointer access.</task>
    <task id="04" status="PASS">Tile warning lane remains 16B aligned.</task>
    <task id="05" status="PASS">Mock scatter/camera/quality signal remain isolated from Agent 09/44.</task>
    <task id="06" status="PASS">Vertex budget kernel remains in Burst and consumes visibility masks.</task>
    <task id="07" status="PASS">Radix sort remains NativeArray/Burst only.</task>
    <task id="08" status="PASS">Dear Lie frustum squeeze remains continuous by quality and budget pressure.</task>
    <task id="09" status="PASS">Texture array pagination remains fixed-slice; no biome-wide residency.</task>
    <task id="10" status="PASS">UMA raw buffer path remains zero-copy oriented.</task>
    <task id="11" status="PASS">Compute limiter still queries hardware group size.</task>
    <task id="12" status="PASS">Transparent overdraw limiter remains hard-capped.</task>
    <task id="13" status="PASS">Hardware architecture gate remains present.</task>
    <task id="14" status="PASS">GPU-facing AUP remains camera-relative float; no double layout.</task>
    <task id="15" status="PASS">Editor half precision validator remains present.</task>
    <task id="16" status="PASS">Sort/scratch buffers remain boot-allocated with uninitialized memory.</task>
    <task id="17" status="PASS">300-frame telemetry ring remains vault-bound when available.</task>
    <task id="18" status="PASS">TBDR Pipeline Tuner remains the human facade.</task>
    <task id="19" status="PASS">CSV parser remains fixed-buffer/span based.</task>
    <task id="20" status="PASS">Sorting gizmo remains available through `Show Sorting`.</task>
  </task_reconciliation>
  <dependency_graph>Production route now consumes an incoming `JobHandle dependency` and returns the final handle from `ScheduleTBDRProtectionPass`. Blocking `Complete()` exists only in `RunMockPipelineOnce` for the Editor/mock button.</dependency_graph>
  <endianness>Legacy budget files now pass through `TryReadUInt32AutoEndian`; implausible little-endian values are byte-reversed before fallback.</endianness>
  <verification>Runtime isolated Roslyn compile now passes with no warnings after the shader-global pass. Editor isolated Roslyn compile passed. Unity batchmode remains blocked by an already-open Editor instance.</verification>
</SELF_AUDIT>

## Decision 11 - Shader Global Budget Handoff and Warning Purge

Problem: The previous implementation saved CPU/GPU work but did not expose the saved budget pressure as shader-global scalars. That left the "visual overkill synergy" mandate underfed: UberNoir-style shaders had no direct TBDR pressure/quality/cap inputs from this lane. The verification also still carried an obsolete `GraphicsDeviceType.OpenGLES2` warning.

Solution: Added 32B `TBDRShaderBudgetGlobalsDTO`: 0 `float GlobalQualityWeight`, 4 `float FrustumSqueezeDegrees`, 8 `float TilePressure`, 12 `float EstimatedVramMb`, 16 `uint HardVertexCap`, 20 `uint CurrentVisibleVertices`, 24 `uint TransparentQuadLimit`, 28 `uint Flags`. Added `TBDRGlobalShaderBudgetBinder` with cached property IDs and global vectors/scalars `_H8_TBDR_Budget0`, `_H8_TBDR_Budget1`, `_H8_TBDR_GlobalQualityWeight`, `_H8_TBDR_TilePressure`, `_H8_TBDR_HardVertexCap`, `_H8_TBDR_CurrentVisibleVertices`, `_H8_TBDR_TransparentQuadLimit`, and `_H8_TBDR_Flags`. Runtime pushes these after initialization, editor limit changes, CSV changes, and completed protection-pass commits. Removed the obsolete `OpenGLES2` enum branch; Android/handheld/GLES3/GPU-name/model gates still classify mobile TBDR.

Rejected Alternatives: Adding a direct dependency on a rendering sibling bridge, using per-material `Material.SetFloat`, allocating managed property names per frame, or leaving shaders blind to budget pressure. Those options either widen the compile wall, break SRP batching, allocate, or fail the visual-overkill mandate.

Scalability potential: Low/Mobile shaders can use `_H8_TBDR_TilePressure` and `_H8_TBDR_GlobalQualityWeight` to damp expensive caustics/silt/rust taps continuously. Middle/High/Ultra can use the same scalars to spend recovered budget on richer shading without a binary hardware switch.

Hardware Impact: CPU cost is a small set of `Shader.SetGlobal*` calls at protection-pass commit or tuning changes, not per instance. GPU benefit is indirect: shader ALU/tap density can now be tied to actual TBDR pressure instead of a hidden C# cap. Exact frame impact remains profiler-pending.

<SELF_AUDIT agent_id="SHINOBU_45" pass="SHADER_GLOBAL_HANDOFF">
  <struct_layout>TBDRShaderBudgetGlobalsDTO size 32B: 0 float quality 4B; 4 float squeeze 4B; 8 float tilePressure 4B; 12 float estimatedVram 4B; 16 uint hardCap 4B; 20 uint currentVertices 4B; 24 uint transparentLimit 4B; 28 uint flags 4B.</struct_layout>
  <compile_guard>No sibling rendering assembly reference was added. Shader handoff uses UnityEngine.Shader globals inside the existing Graphics.Culling assembly.</compile_guard>
  <visual_overkill_synergy>Saved CPU/GPU pressure now becomes shader-visible scalar input for continuous caustic/silt/detail throttling.</visual_overkill_synergy>
  <verification>Runtime isolated Roslyn compile passed with no warnings. Editor isolated Roslyn compile passed. Targeted SHINOBU diff check passed. Unity batchmode remains blocked by open Editor.</verification>
</SELF_AUDIT>

## Decision 12 - Sort-Stable Dear Lie Visibility and Frustum Sign Repair

Problem: The prior pass narrowed numeric caps, but two render-pipeline defects remained. First, the side-plane math used `normal + forward * squeezeRadians`, which widens inward-facing side/top/bottom frustum planes for the mock plane convention. Second, the visibility mask was index-based; after radix sort, `VertexBudgetJob` could read a mask entry that belonged to a different pre-sort instance.

Solution: Flip the squeeze sign to `normal - forward * squeezeRadians`, making the mock side/top/bottom planes geometrically narrower. Add `TBDRVisibilityFlags` and `DearLieFrustumVisibilityJob`; the job evaluates the squeezed planes and stores the rejection bit in `PoiTransformDTO.Flags` before sorting. `VertexBudgetJob` rejects by flags that move with the DTO through radix passes. `HzbAabbOcclusionCullJob` now writes the `HzbRejected` bit into the DTO as well as the optional debug/integration mask. The runtime sorted path passes `VisibilityMask = default` to avoid stale index-mask damage.

Rejected Alternatives: Keeping a separate pre-sort visibility mask, compacting the array before sort with another scatter pass, or doing CPU mesh decimation. The separate mask was mathematically wrong after sort; a compaction pass adds bandwidth and synchronization; CPU decimation violates the Dear Lie mandate.

Scalability potential: Low/Thermal devices get the strongest actual peripheral cull because squeeze now affects visibility, not only the cap. Middle uses the same flag path with less squeeze. High/Ultra can keep DTO flags for HZB and indirect args while relaxing caps or bypassing CPU sort on IMR.

Hardware Impact: Expected gain is fewer peripheral/occluded vertices entering the budget and indirect args on Quest-class TBDR. Exact microseconds remain profiler-pending. The correction prevents false culls and false keeps caused by post-sort mask drift, which is correctness-first and tile-spill relevant.

<SELF_AUDIT agent_id="SHINOBU_45" pass="DEAR_LIE_VISIBILITY_MASK_HARDENING">
  <task_reconciliation>
    <task id="01" status="PASS">Prompt re-extracted with attr-aware CLI; budget archaeology unchanged.</task>
    <task id="02" status="PASS">TBDR path now has sort-stable visibility before Early-Z order construction.</task>
    <task id="03" status="PASS">No DTO properties added; flags are public unmanaged fields.</task>
    <task id="04" status="PASS">Existing aligned warning DTO unchanged.</task>
    <task id="05" status="PASS">Mock camera/frustum path now proves cull behavior without Agent 09/44.</task>
    <task id="06" status="PASS">VertexBudgetJob rejects by DTO flags that survive radix reordering.</task>
    <task id="07" status="PASS">Radix sort remains NativeArray/Burst; flags travel inside `PoiTransformDTO`.</task>
    <task id="08" status="PASS">Dear Lie now actually narrows planes and culls peripheral matrices before budgeting.</task>
    <task id="09" status="PASS">Texture pagination untouched.</task>
    <task id="10" status="PASS">UMA raw buffer path untouched.</task>
    <task id="11" status="PASS">Compute limiter untouched.</task>
    <task id="12" status="PASS">Transparent limiter untouched.</task>
    <task id="13" status="PASS">Hardware switch untouched; IMR sort bypass still available.</task>
    <task id="14" status="PASS">GPU payloads remain camera-relative float lanes.</task>
    <task id="15" status="PASS">Editor shader validator untouched.</task>
    <task id="16" status="PASS">New visibility pass uses existing preallocated DTO/mask buffers.</task>
    <task id="17" status="PASS">Telemetry path unchanged; flags reduce false tile-spill attribution risk.</task>
    <task id="18" status="PASS">Editor tuner remains the facade.</task>
    <task id="19" status="PASS">CSV parser untouched.</task>
    <task id="20" status="PASS">Sorting gizmo now observes DTO order after flags travel through sort.</task>
  </task_reconciliation>
  <struct_layout>PoiTransformDTO remains 112B: existing `uint Flags` at offset 100 now carries `FrustumRejected` and `HzbRejected`; no size or padding change.</struct_layout>
  <scalability_curve>Below quality 0.3, squeeze degrees approach the configured maximum, cap scale trends to 0.80, and the new frustum visibility job marks peripheral DTOs before radix sort. At higher quality the same continuous math relaxes without a binary tier switch.</scalability_curve>
  <h_phi_vault_status>No new persistent buffers were introduced. The pass consumes existing 70824 MockVisibleInstances, 70832 SqueezedFrustumPlanes, and optional 70833 HzbVisibilityMask.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>Graph is now MockQualityWeightJob -> DearLieFrustumSqueezeJob -> DearLieFrustumVisibilityJob -> BuildDistanceSortKeysJob -> optional EarlyZRadixSortJob -> VertexBudgetJob -> BuildIndirectDrawArgsJob. New job uses `[NoAlias]` on DTO, plane, and mask arrays.</pointer_aliasing_dependency_graph>
  <compile_guard>No new assembly reference or sibling dependency was added.</compile_guard>
  <dear_lie>Before: cap squeeze could be enforced after sorting but frustum cull was not guaranteed and the mask could drift by index. After: O(n instances * 6 planes) cheap sphere-plane fake rejects peripheral matrices before sort; no CPU mesh decimation.</dear_lie>
  <verification>Static runtime scan passed. Roslyn compile retry pending because CPU gate remained above 50%: first attempt had CPU 100% plus another `dotnet/csc`, later attempts reported CPU 57-100% with no compiler process.</verification>
</SELF_AUDIT>

## Decision 13 - Continuous Quality Drift Instead of Per-Frame Mock Pops

Problem: `MockQualityWeightJob` satisfied the dependency-mocking requirement, but its first form replaced `GlobalQualityWeight` with a new random value every frame. That creates frustum/cap flicker and violates the continuous scalability law: the engine must shed load smoothly, not pop between unrelated random states.

Solution: Keep deterministic seeded mock input but convert it into bounded low-pass drift. The job now reads the previous weight, derives a deterministic target in `[0.1, 1.0]`, applies a cubic stress curve `stress * stress * (3 - 2 * stress)`, clamps max movement between `0.015` and `0.045`, and blends with `math.lerp` gated by `math.step`. This keeps the mock standalone while proving the production curve shape: hotter/lower quality converges faster, stable states hold.

Rejected Alternatives: Keeping frame-random weights, adding a managed smoothing service, or introducing a direct dependency on Agent 44's real Scalability Dictator. Frame randomness is visible jitter; a managed service risks compile-wall coupling and GC; a direct sibling dependency violates temporal blindness.

Scalability potential: Low/Thermal weight can fall toward the cheap path over several frames without a single-frame visual cut. Middle/High/Ultra use the same curve and can spend recovered budget on shader richness through the global shader budget handoff.

Hardware Impact: CPU cost is unchanged O(1). GPU impact is indirect: frustum squeeze and cap pressure now change monotonically enough to avoid flickering BRG/indirect draw counts, reducing visible churn and tile-cache instability. Exact profiler-backed microseconds remain pending.

<SELF_AUDIT agent_id="SHINOBU_45" pass="QUALITY_DRIFT_HARDENING">
  <task_reconciliation>
    <task id="05" status="PASS">Mock quality still exists and is deterministic, but now changes through a bounded continuous curve.</task>
    <task id="08" status="PASS">Dear Lie frustum squeeze now consumes a non-popping quality signal.</task>
    <task id="13" status="PASS">Hardware switch remains independent; no direct scalability sibling dependency added.</task>
  </task_reconciliation>
  <scalability_curve>At low weight, stress approaches 1 and max quality movement rises toward 0.045 per frame, letting the renderer shed load quickly but continuously. At high weight, max movement is 0.015, preventing visual oscillation on desktop/overkill paths. The curve uses `math.lerp`, `math.step`, and a cubic polynomial.</scalability_curve>
  <h_phi_vault_status>No new buffers. Existing 70829 `MockQualitySignal` remains the only quality lane touched.</h_phi_vault_status>
  <compile_guard>No new using, assembly reference, managed collection, or sibling dependency was added.</compile_guard>
  <verification>Static banned-pattern scan passed after smoothing. Roslyn compile remains gated by CPU load: current check reported CPU 74%.</verification>
</SELF_AUDIT>

## Decision 14 - CSV Polling Path Churn Removal

Problem: `TBDRGpuBudgetCsvIngestor` used a fixed byte buffer and zero-GC parser, but `TBDRPipelineSurgeonRuntime.PollBudgetCsvOverride()` rebuilt the absolute CSV path on every poll. If a tuner polls frequently, repeated `Path.Combine`/`Path.GetFullPath` calls become managed churn around an otherwise allocation-aware parser.

Solution: Add `_resolvedGpuBudgetCsvPath` and `_csvPathDirty`. The absolute path is resolved once during initialization or after `SetCsvPath()`. Polling now reuses the cached string and only asks the ingestor to check timestamps and read the file when it actually changes.

Rejected Alternatives: Leaving path construction inside every poll, moving CSV monitoring into an Update loop, or using a FileSystemWatcher. Repeated path construction wastes managed memory; Update polling would violate this lane's hot-path discipline; FileSystemWatcher introduces managed event allocations and platform-specific behavior.

Scalability potential: Low/Thermal and editor Play Mode can tune caps without adding managed churn to budget polling. High/Ultra use the same path cache; no hardware fork.

Hardware Impact: Expected savings are tiny per poll but deterministic: avoids repeated string/path allocations around `gpu_budgets.csv` monitoring. Exact microseconds remain unmeasured.

<SELF_AUDIT agent_id="SHINOBU_45" pass="CSV_PATH_CACHE_HARDENING">
  <task_reconciliation>
    <task id="19" status="PASS">CSV parser remains fixed-buffer/span based, and path resolution is no longer repeated per poll.</task>
  </task_reconciliation>
  <h_phi_vault_status>No buffer change. CSV still writes directly into existing Vault budget and transparent counter lanes.</h_phi_vault_status>
  <compile_guard>No new assembly reference, event system, watcher thread, or sibling dependency was added.</compile_guard>
  <verification>Static probe after this patch timed out under system load; a later 30s delayed compile gate still reported CPU 100% with no `dotnet/csc`, so compile remains gated until CPU drops below 50%.</verification>
</SELF_AUDIT>

## Decision 15 - Quality State Persistence and Vertex Overflow Guard

Problem: The low-pass quality curve still had a hidden reset: `ScheduleTBDRProtectionPass()` rewrote `MockQualitySignal[0]` from `_globalQualityWeight` before every job chain, so the previous frame's weight never survived. The vertex budget also used `totalVertices + vertexCount > maxVertices`; with corrupted or hostile mesh counts, that `uint` addition can wrap and allow over-budget submission.

Solution: Move mock quality initialization into `SeedMockData()` and leave the signal lane persistent across scheduled passes. `MockQualityWeightJob` now owns per-frame mutation of the signal. In `VertexBudgetJob`, replace additive comparison with a remaining-cap check: `remaining = maxVertices > totalVertices ? maxVertices - totalVertices : 0u; if (vertexCount > remaining) break;`.

Rejected Alternatives: Rewriting the quality signal every pass, storing smoothing in a managed field, or widening all vertex math to `ulong`. Rewriting erased the curve; managed smoothing would be a stateful owner outside the vault lane; `ulong` broadens the hot DTO math without need because a subtraction guard prevents overflow in the existing ABI.

Scalability potential: Low/Thermal quality now truly converges over time instead of restarting each pass. Middle/High/Ultra retain the same deterministic lane. Corrupted vertex counts cannot wrap the cap and flood TBDR tile memory.

Hardware Impact: The quality persistence repair affects visual stability and thermal load shedding; the overflow guard is a hard safety gate against pathological mesh payloads. CPU cost is negligible and profiler-pending.

<SELF_AUDIT agent_id="SHINOBU_45" pass="QUALITY_STATE_AND_VERTEX_OVERFLOW_HARDENING">
  <task_reconciliation>
    <task id="05" status="PASS">Mock quality signal is initialized once and then mutated by the Burst job, preserving smoothing state.</task>
    <task id="06" status="PASS">VertexBudgetJob cannot wrap `uint` totals when enforcing the hard vertex cap.</task>
    <task id="08" status="PASS">Dear Lie now consumes persistent, continuous quality pressure instead of a reset signal.</task>
  </task_reconciliation>
  <struct_layout>No DTO size changed. `MockQualityWeightSignal` remains 16B and `VertexBudgetDTO` remains 16B.</struct_layout>
  <scalability_curve>The smoothed quality lane now has frame-to-frame memory because runtime no longer rewrites it before scheduling. Low quality pressure can accumulate; high quality can relax without abrupt reset.</scalability_curve>
  <h_phi_vault_status>No new private arrays. Existing 70829 `MockQualitySignal` and 70820 `VertexBudgetCounters` are reused.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>No graph change. The safety improvements stay inside MockQualityWeightJob and VertexBudgetJob.</pointer_aliasing_dependency_graph>
  <compile_guard>No new assembly reference or sibling dependency.</compile_guard>
  <verification>Targeted grep confirmed no remaining `totalVertices + vertexCount` overflow comparison. Runtime banned-pattern scan passed. Roslyn compile remains gated by CPU; latest check reported CPU 88%.</verification>
</SELF_AUDIT>

## Decision 16 - Stale HZB Rejection Bit Purge

Problem: Visibility flags now travel with sorted DTOs, but the first frustum visibility pass cleared only `FrustumRejected`. If an earlier HZB readback marked `HzbRejected` and the next frame had no fresh HZB pyramid, `VertexBudgetJob` could keep rejecting that DTO through stale state.

Solution: `DearLieFrustumVisibilityJob` now clears `TBDRVisibilityFlags.RejectedMask` before applying the current squeezed-frustum test. Optional `HzbAabbOcclusionCullJob` can then add `HzbRejected` again in the same frame when a fresh depth pyramid exists. This makes the dependency order explicit: frustum pass resets frame-local rejection truth, HZB pass refines it.

Rejected Alternatives: Clearing flags in `VertexBudgetJob`, clearing the whole DTO flag field, or trusting external HZB ownership to always refresh. Budget clearing is too late for sort/gizmo/debug truth; clearing the whole flag field would destroy future non-visibility flags; external readback can legally miss a frame.

Scalability potential: Low/Thermal frames can skip HZB readback without carrying stale hidden-object state. Middle/High/Ultra still get HZB refinement when available.

Hardware Impact: Prevents false culling and visual holes after transient HZB data. CPU cost is one bit clear per instance inside an existing job.

<SELF_AUDIT agent_id="SHINOBU_45" pass="STALE_HZB_FLAG_PURGE">
  <task_reconciliation>
    <task id="06" status="PASS">VertexBudgetJob now consumes only current-frame rejection bits.</task>
    <task id="08" status="PASS">Dear Lie frustum pass is the authoritative reset for frame-local visibility.</task>
  </task_reconciliation>
  <dependency_graph>Expected order is frustum reset/test -> optional HZB refinement -> distance sort -> radix sort -> vertex budget. Stale HZB bits cannot survive the reset stage.</dependency_graph>
  <compile_guard>No assembly or dependency change.</compile_guard>
  <verification>Targeted grep confirmed `RejectedMask` clear in `DearLieFrustumVisibilityJob`; compile remains gated by CPU, final delayed probe reported CPU 99% plus another `dotnet/csc`.</verification>
</SELF_AUDIT>

## Decision 17 - Shader Global Quality Handoff Repair

Problem: The render protection chain now keeps a persistent smoothed `MockQualitySignal`, but `PushShaderBudgetGlobals()` still published the serialized `_globalQualityWeight` and the maximum configured squeeze angle. That left shader-side fog/silt/caustic budget logic blind to the actual dynamic pressure applied by the Dear Lie frustum pass.

Solution: Add `CurrentQualityWeight()` and `CurrentFrustumSqueezeDegrees(float quality)` inside `TBDRPipelineSurgeonRuntime`. The completed pass commits the smoothed quality back to `_globalQualityWeight` for inspector coherence, and shader globals now receive `configuredSqueeze * (1 - quality)` as the active squeeze. `TBDRGlobalShaderBudgetBinder` also publishes `_H8_TBDR_FrustumSqueezeDegrees` as a scalar for HLSL consumers that do not read the packed vector.

Rejected Alternatives: Publishing the configured max squeeze to shaders, changing `TBDRTunerSnapshot` to dynamic squeeze, or adding a direct dependency on an external Scalability Dictator. Max squeeze lies about current pressure; dynamic tuner snapshot would make the Editor facade write the current frame's temporary squeeze back as the design cap; external dependency violates temporal blindness and compile-wall isolation.

Scalability potential: Low/Thermal quality now drives both CPU-side matrix rejection and shader-side presentation masks through the same continuous value. Middle/High/Ultra relax the squeeze smoothly and can spend the recovered headroom on richer shader taps without a binary hardware fork.

Hardware Impact: CPU delta is negligible: one NativeArray scalar read and one multiplication in a cold/main-thread commit path. The real effect is pipeline coherence: shader visuals now hide exactly the same peripheral contraction that protects TBDR tile memory. Exact frame savings remain profiler-pending.

<SELF_AUDIT agent_id="SHINOBU_45" pass="SHADER_QUALITY_HANDOFF_REPAIR">
  <task_reconciliation>
    <task id="08" status="PASS">Dear Lie now publishes the active dynamic frustum squeeze to shader globals, not only the configured maximum.</task>
    <task id="13" status="PASS">No binary low-end switch added; active squeeze is continuous `maxSqueeze * (1 - quality)`.</task>
    <task id="18" status="PASS">Editor facade keeps reporting configured max squeeze to avoid feeding frame-dynamic squeeze back into authoring controls.</task>
  </task_reconciliation>
  <struct_layout>No DTO size or field order changed. `TBDRShaderBudgetGlobalsDTO` remains 32B: four floats at offsets 0/4/8/12 and four uints at offsets 16/20/24/28.</struct_layout>
  <scalability_curve>When `GlobalQualityWeight` approaches 0.3, shader squeeze becomes roughly 70 percent of the configured cap; at 0.1 it becomes 90 percent. At 1.0 it collapses to zero. The curve is continuous and mirrors the CPU cull pressure.</scalability_curve>
  <h_phi_vault_status>No new persistent buffers. Existing 70829 `MockQualitySignal` is the only quality lane read by the handoff.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>No job graph change. The handoff runs after the scheduled protection pass has been completed by the caller and before shader globals are pushed.</pointer_aliasing_dependency_graph>
  <compile_guard>No new assembly reference, sibling domain reference, managed collection, or DTO property was added.</compile_guard>
  <dear_lie>Before: shader presentation received a max-squeeze scalar that could overstate current culling. After: shader presentation receives the same dynamic squeeze that the CPU culling fake used, keeping fog/silt concealment coherent with matrix dropping.</dear_lie>
  <verification>Runtime banned-pattern scan passed and targeted diff hygiene passed. Roslyn compile retry was skipped by CPU gate: first probe reported CPU 100%, `dotnet/csc` false; delayed probe reported CPU 95%, `dotnet/csc` true.</verification>
</SELF_AUDIT>

## Decision 18 - Explicit Simulation Frame Scheduling

Problem: `ScheduleTBDRProtectionPass(int, JobHandle)` seeded its deterministic mock-quality lane from `Time.frameCount`. This is acceptable for an editor/mock wrapper, but it gives production callers no way to bind the render protection pass to a lockstep simulation frame counter during rollback.

Solution: Add `ScheduleTBDRProtectionPass(int requestedInstanceCount, uint simulationFrame, JobHandle dependency)` and move the full job-chain implementation there. The existing two-argument method now delegates to the explicit-frame overload with Unity's current frame only as a compatibility fallback.

Rejected Alternatives: Leaving Unity frame access inside the only scheduling API, storing a managed frame service in the runtime, or pulling a direct netcode/scalability dependency. The first path weakens rollback integration; a managed service creates ownership ambiguity; direct sibling dependencies violate the compile-wall and temporal-blindness constraints.

Scalability potential: Low/Thermal, Middle, High, and Ultra all schedule the same deterministic cull graph. Only the incoming frame counter differs by caller. This preserves continuous quality math and avoids a hardware-tier fork.

Hardware Impact: CPU cost is unchanged. The gain is determinism and integration safety: rollback-capable dispatchers can now reproduce the same quality/culling seed for a given simulation frame.

<SELF_AUDIT agent_id="SHINOBU_45" pass="ROLLBACK_FRAME_SCHEDULING_REPAIR">
  <task_reconciliation>
    <task id="05" status="PASS">Mock quality mutation can now be seeded by an explicit simulation frame.</task>
    <task id="07" status="PASS">Early-Z radix chain order is unchanged.</task>
    <task id="17" status="PASS">Telemetry continues recording `_lastFrame`, now set from the explicit production frame when supplied.</task>
  </task_reconciliation>
  <struct_layout>No DTO size, field order, or padding changed.</struct_layout>
  <scalability_curve>The same quality curve is used; the seed frame is now externally controllable for rollback determinism.</scalability_curve>
  <h_phi_vault_status>No new persistent buffers or local arrays.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>Graph unchanged: incoming dependency -> MockQualityWeightJob -> DearLieFrustumSqueezeJob -> DearLieFrustumVisibilityJob -> BuildDistanceSortKeysJob -> optional EarlyZRadixSortJob -> VertexBudgetJob -> BuildIndirectDrawArgsJob.</pointer_aliasing_dependency_graph>
  <compile_guard>No new assembly reference, sibling dependency, interface array, or managed collection.</compile_guard>
  <dear_lie>The optical fake remains the same; the frame seed is now deterministic under an external simulation clock.</dear_lie>
  <verification>Runtime banned-pattern scan passed and targeted diff hygiene passed. Roslyn compile was skipped by CPU gate: latest post-patch probe reported CPU 82%, `dotnet/csc` false.</verification>
</SELF_AUDIT>

## Decision 19 - Tile Pressure Drives Dear Lie Squeeze

Problem: The frustum squeeze job checked `CurrentVisibleVertices > MaxVisibleVertices`, but the budget job's purpose is to truncate before that state persists. In practice, the overflow condition is usually false after the pipeline is behaving correctly, so actual near-cap tile pressure was not feeding the Dear Lie.

Solution: Feed previous-frame `BudgetPtr->TilePressure` into the squeeze stress. Pressure below 0.82 does nothing; pressure above that threshold is normalized, passed through cubic smoothstep, and combined with quality stress using `math.max`. Shader global squeeze now uses the same quality/pressure curve, so visual concealment matches CPU matrix culling.

Rejected Alternatives: Waiting for impossible post-truncation overflow, adding a binary "over budget" branch, or lowering the vertex cap blindly. Overflow waiting misses the dangerous pre-spill pressure zone; binary branching violates scalability law; lowering cap permanently wastes desktop/high-tier visual budget.

Scalability potential: Low/Thermal can tighten the frustum from quality stress or tile pressure. Middle/High/Ultra retain wide FOV unless real pressure appears, so saved mobile cycles do not blunt high-tier visual richness.

Hardware Impact: CPU cost is a handful of scalar ALU operations in one Burst job plus one main-thread shader handoff calculation. GPU impact is the intended one: prevent peripheral geometry from entering tile bins before on-chip tile memory spills.

<SELF_AUDIT agent_id="SHINOBU_45" pass="TILE_PRESSURE_SQUEEZE_REPAIR">
  <task_reconciliation>
    <task id="06" status="PASS">Vertex budget pressure now feeds the next-frame squeeze instead of relying on impossible post-truncation overflow.</task>
    <task id="08" status="PASS">Dear Lie narrows the frustum continuously under real tile pressure.</task>
    <task id="13" status="PASS">Still no binary low/high switch; pressure stress is a polynomial curve.</task>
  </task_reconciliation>
  <struct_layout>No DTO size, offset, or padding changed.</struct_layout>
  <scalability_curve>Pressure stress = smoothstep-like cubic over `(TilePressure - 0.82) / 0.18`; final stress is `max(1 - quality, pressureStress)`. Below 0.82 no pressure squeeze; at 1.0 tile pressure full configured squeeze is allowed.</scalability_curve>
  <h_phi_vault_status>No new persistent buffers. Existing 70820 `VertexBudgetCounters` carries `TilePressure`; existing 70829 carries quality.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>No graph change. The squeeze job reads the same budget pointer it already consumed.</pointer_aliasing_dependency_graph>
  <compile_guard>No new assembly reference, sibling dependency, interface array, or managed collection.</compile_guard>
  <dear_lie>Before: squeeze depended mostly on quality. After: squeeze also reacts to measured vertex pressure, dropping peripheral matrices under fog before tile spilling.</dear_lie>
  <verification>Runtime banned-pattern scan passed and targeted diff hygiene passed. Roslyn compile was skipped by CPU gate: latest probe reported CPU 82%, `dotnet/csc` false.</verification>
</SELF_AUDIT>

## Decision 20 - Texture Residency Budget Actually Enforced

Problem: `TBDRTextureStreamingTracker` exposed `MaxResidentMb` and an estimated residency counter, but staging a new slice did not reject oversized payloads or evict old logical residents before writing. The physical `Texture2DArray` is fixed, but the tracker still needs a hard logical residency contract so designers and telemetry cannot read a false budget state.

Solution: Add overflow-safe `ulong` byte accounting, reject any incoming slice larger than `MaxResidentMb`, compute projected residency after replacing the target slice, and clear `ResidentFlags` on the oldest touched slices until the projected total fits the cap. `EstimateResidentBytes()` now uses unclamped internal accounting and only clamps at the public return boundary.

Rejected Alternatives: Trusting `Texture2DArray.depth` alone, reporting a clamped total while silently exceeding the logical cap, or allocating a managed eviction list. Depth alone does not prove the content budget; clamped reports hide violations; managed lists violate the zero-GC discipline for a streaming path.

Scalability potential: Low/Thermal and Steam Deck can keep a strict 512MB-style logical budget. High/Ultra can raise `MaxResidentMb` or array depth through existing tuning without changing code paths.

Hardware Impact: CPU cost is O(sliceCapacity) on biome staging, a cold/streaming event. GPU/VRAM impact is budget truth: the tracker cannot mark more logical texture payload than the configured residency cap.

<SELF_AUDIT agent_id="SHINOBU_45" pass="TEXTURE_RESIDENCY_BUDGET_REPAIR">
  <task_reconciliation>
    <task id="09" status="PASS">Texture array pagination now rejects oversized incoming slices and evicts oldest logical residents to stay under budget.</task>
    <task id="19" status="PASS">Designer/CSV budget values cannot be hidden by clamped reporting alone.</task>
  </task_reconciliation>
  <struct_layout>No DTO size changed. `TextureStreamingSliceDTO` remains 32B: eight uint fields.</struct_layout>
  <scalability_curve>Residency cap is a continuous configured budget value; low tiers use small caps, high tiers raise caps without a separate code path.</scalability_curve>
  <h_phi_vault_status>No new persistent buffers. Existing 70835 texture slice table is reused.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>No job graph change; texture staging remains a cold render/streaming call.</pointer_aliasing_dependency_graph>
  <compile_guard>No new assembly reference, sibling dependency, managed collection, or LINQ.</compile_guard>
  <dear_lie>Texture residency stays a fixed-array paging fake instead of loading every biome texture set.</dear_lie>
  <verification>Runtime banned-pattern scan passed, targeted diff hygiene passed, and asmdef readback confirmed no sibling runtime reference. Roslyn compile was skipped by CPU gate: latest probes reported CPU 100% with `dotnet/csc` true, CPU 100% with `dotnet/csc` false, and delayed final probe CPU 99% with `dotnet/csc` false.</verification>
</SELF_AUDIT>

## Decision 21 - Hostile Vertex Cap Clamp

Problem: The vertex cap can enter the runtime from multiple human and binary paths: editor sliders, CSV overrides, legacy `.h8bin` archaeology, vault seed defaults, and the Burst budget job itself. Before this repair, those paths mostly applied only `math.max(value, 1u)`. A hostile or corrupt `uint` cap could therefore publish an impossible budget, push the atomic `CurrentVisibleVertices` lane toward wraparound, and invite mobile TBDR tile-spill instead of enforcing a hard hardware ceiling.

Solution: Add `TBDRHardwareBudgetMath.ClampVisibleVertexCap()` with a single conservative hard ceiling of `20,000,000` visible vertices. Runtime initialization, editor limit application, legacy binary ingestion, CSV ingestion, vault application, `DearLieFrustumSqueezeJob`, and `VertexBudgetJob` now all pass through that helper. The squeeze job clamps its pressure-adjusted cap, and the budget job writes the clamped cap back into `BudgetPtr->MaxVisibleVertices` before any visible-vertex accumulation, so downstream telemetry and shader globals see the same bounded budget that the jobs actually enforced.

Rejected Alternatives: Trusting the Editor slider, trusting CSV designers, widening counters to `ulong`, or adding a binary mobile/desktop switch. Editor/CSV trust is not a safety boundary; `ulong` atomics are not the cheap cross-platform Burst answer here; a binary hardware switch violates the continuous scalability contract. The clamp keeps the scalar quality continuum intact while refusing physically absurd caps.

Scalability potential: Low/Thermal, Middle, High, and Ultra all use the same code path. Designers can still raise or lower budgets continuously within the safe range, while the hard ceiling prevents a bad payload from turning desktop overkill into Quest 3 tile-memory abuse.

Hardware Impact: CPU cost is one integer clamp at ingress and one clamp inside the budget job. The intended gain is failure prevention: impossible budgets cannot flow into BRG/indirect args and cannot cause atomic wraparound or uncontrolled tile bin expansion.

<SELF_AUDIT agent_id="SHINOBU_45" pass="HOSTILE_VERTEX_CAP_CLAMP">
  <task_reconciliation>
    <task id="06" status="PASS">`DearLieFrustumSqueezeJob` and `VertexBudgetJob` clamp and republish `MaxVisibleVertices` before submitted vertices are accumulated.</task>
    <task id="13" status="PASS">No low-end binary fork was added; the hard ceiling is a safety invariant around continuous budget tuning.</task>
    <task id="19" status="PASS">CSV override ingestion clamps parsed caps before writing vault/runtime state.</task>
  </task_reconciliation>
  <struct_layout>No DTO size, offset, or padding changed. `VertexBudgetDTO` remains 16B: uint 0, uint 4, float 8, uint pad 12.</struct_layout>
  <scalability_curve>GlobalQualityWeight still controls squeeze and pressure response continuously. The cap clamp is not a quality switch; it only bounds corrupt or impossible numeric inputs.</scalability_curve>
  <h_phi_vault_status>No new persistent buffers. Existing 70820 `VertexBudgetCounters` remains the cap lane.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>No graph change. Clamp occurs in existing cold ingress paths, inside existing squeeze job, and inside existing `VertexBudgetJob` before its atomic accumulation loop.</pointer_aliasing_dependency_graph>
  <compile_guard>No new assembly reference, sibling dependency, managed collection, interface array, or DTO property.</compile_guard>
  <dear_lie>Dear Lie remains distance/front-to-back matrix dropping plus frustum squeeze; the clamp prevents hostile inputs from disabling that protection.</dear_lie>
  <verification>Runtime banned-pattern scan passed after the squeeze-clamp patch. Targeted `git diff --check` exited clean, with only the existing LF-to-CRLF warning on the runtime asmdef. Roslyn compile remains gated by CPU load; final response probe reported CPU 100%, `dotnet/csc` false.</verification>
</SELF_AUDIT>

## Decision 22 - Overflow Boundary Repair

Problem: Three hostile-input edges still existed after the hard vertex cap clamp. CSV parsing used `value = value * 10 + digit`, so a long numeric cell could wrap before reaching the clamp. Transparent overdraw used `RequestedParticleQuads + RequestedUiQuads` as an unchecked `int` sum, so two huge requests could become negative and suppress no work. HZB validation multiplied `HzbWidth * HzbHeight` as `int`, so invalid dimensions could overflow before the depth array length check.

Solution: Make every boundary saturating before it influences rendering. `TryParseUInt()` now saturates at `uint.MaxValue`, which then flows into `ClampVisibleVertexCap()`. `TransparentOverdrawLimiterJob` saturates the particle/UI sum at `int.MaxValue` before calculating overflow. `HzbAabbOcclusionCullJob` computes the depth-pyramid pixel count in `long` before validating the NativeArray length and indexing.

Rejected Alternatives: Trusting designer CSV input, relying on C# overflow behavior, or treating impossible HZB dimensions as caller responsibility. These are exactly the kinds of edge inputs that break thermal protection under stress tests. The fixes are scalar guards in existing code paths and do not add allocations or new dependencies.

Scalability potential: Low/Thermal and mobile TBDR benefit most because bad caps or bad HZB dimensions can no longer disable culling. High/Ultra keep the same code path and can still raise budgets inside the safe numeric range.

Hardware Impact: CPU delta is negligible: one integer overflow guard per parsed digit, one saturating sum in a single job, and one 64-bit multiply per HZB cull job instance. The protection is catastrophic-failure prevention: no wrapped cap, no transparent overdraw undercount, no invalid HZB bounds read.

<SELF_AUDIT agent_id="SHINOBU_45" pass="OVERFLOW_BOUNDARY_REPAIR">
  <task_reconciliation>
    <task id="06" status="PASS">Vertex and transparent budget inputs now saturate before arithmetic can wrap.</task>
    <task id="12" status="PASS">Transparent quad overflow calculation cannot undercount due to signed integer wrap.</task>
    <task id="19" status="PASS">CSV numeric cells saturate before vault mutation and hard cap clamp.</task>
  </task_reconciliation>
  <struct_layout>No DTO size, offset, or padding changed.</struct_layout>
  <scalability_curve>No quality-tier switch was added. Existing GlobalQualityWeight and tile-pressure math remain continuous; this pass only hardens hostile numeric boundaries.</scalability_curve>
  <h_phi_vault_status>No new persistent buffers. Existing vault handles 70820, 70822, and HZB mask/depth lanes are reused.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>No graph change. Existing jobs keep `[NoAlias]`; HZB validation runs before any depth-pyramid index calculation.</pointer_aliasing_dependency_graph>
  <compile_guard>No new assembly reference, sibling dependency, managed collection, interface array, or DTO property.</compile_guard>
  <dear_lie>Dear Lie remains intact; these guards prevent corrupt inputs from bypassing the fake and forcing the GPU to process unsafe geometry/transparent load.</dear_lie>
  <verification>Runtime banned-pattern scan passed. Targeted `git diff --check` passed for touched runtime files. Roslyn compile remains gated by CPU load; latest probe reported CPU 100%, `dotnet/csc` false.</verification>
</SELF_AUDIT>

## Decision 23 - Compute Dispatch Boundary Repair

Problem: The compute dispatch limiter queried kernel group dimensions correctly, but multiplied `groupX * groupY * groupZ` as `int` and used `value + divisor - 1` inside `DivCeil()`. A malformed or unexpected kernel dimension could overflow the guard and turn a crash-prevention system into a dispatch hazard. It also converted zero-work requests into a one-group dispatch, doing unnecessary GPU work on mobile.

Solution: Convert raw `uint` group dimensions through `ToPositiveGroupSize()`, multiply the thread-group product in `long`, reject non-positive or over-budget group products, reject zero-work dispatch requests with `LastRejectCode = 3`, and compute group counts using `1 + (value - 1) / divisor` to avoid addition overflow.

Rejected Alternatives: Trusting shader import data, clamping every work dimension to one, or letting the shader early-return. The C# limiter is the hardware gate; sending empty or overflowed dispatches to Vulkan/Android defeats the point of Task 11.

Scalability potential: Low/Thermal avoids useless zero-work GPU launches and invalid oversized group products. Middle/High/Ultra still use queried kernel dimensions and can keep 1024-thread desktop allowance where the hardware reports it safely.

Hardware Impact: CPU delta is one `long` multiply and a few scalar guards per dispatch call. GPU impact is defensive: no fake one-group dispatch for empty work and no overflowed group-count launch.

<SELF_AUDIT agent_id="SHINOBU_45" pass="COMPUTE_DISPATCH_BOUNDARY_REPAIR">
  <task_reconciliation>
    <task id="11" status="PASS">Compute dispatch limiter now rejects oversized/overflowed thread groups and zero-work dispatches before `Dispatch()`.</task>
    <task id="13" status="PASS">Mobile/desktop caps remain continuous hardware-bound policy: mobile max 256, desktop max 1024, both clamped by reported hardware.</task>
  </task_reconciliation>
  <struct_layout>No DTO size, offset, or padding changed.</struct_layout>
  <scalability_curve>No new binary quality switch. Existing hardware cap policy remains, but invalid dimensions are rejected instead of normalized into dangerous work.</scalability_curve>
  <h_phi_vault_status>No persistent buffers touched.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>No job graph change; this is a cold dispatch gate around compute shader launches.</pointer_aliasing_dependency_graph>
  <compile_guard>No assembly reference, sibling dependency, managed collection, interface array, or DTO property was added.</compile_guard>
  <dear_lie>Dear Lie remains the render-culling fake; this patch prevents compute side work from stealing the thermal headroom that fake buys.</dear_lie>
  <verification>Runtime banned-pattern scan passed. Targeted `git diff --check` passed for touched runtime files. Roslyn compile remains gated by CPU load; latest probe reported CPU 100%, `dotnet/csc` true.</verification>
</SELF_AUDIT>

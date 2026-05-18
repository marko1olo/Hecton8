# Rationale_SHINOBU_05
Date: 2026-05-17
Status: DATA-VAULT POLISH APPLIED / PARTIAL COMPILE PASS / CORE BLOCKED BY EXTERNAL PROJECT STATE

## Decision 00: Mandate Gate
Problem: The prompt demands voxel destruction and tens of thousands of falling stones without GameObjects or Rigidbodies while the existing project already has voxel, DataVault, SignalBus, and GPU debris code.
Solution: Reuse the existing SignalBus/DataVault/GPU indirect path and add only missing SHINOBU-owned DTO/job/editor seams. This follows fake-first, zero-GC, and registry boundaries.
Rejected Alternatives: Replacing `HectonVoxelEngine` or `VoxelDeltaProcessor` wholesale was rejected because mandates preserve the current Marching Cubes pipeline and first-party save delta path. Spawning Rigidbodies was rejected because it is the exact i3/MX350 failure mode.
Scalability potential: Low/MX350 uses bounded 500 debris and cheap plane/SDF collision fake. Middle uses higher particle count and flow. High/Ultra spends saved physics cost on denser debris, stronger material response, and shadow-capable rendering.
Hardware Impact: Expected i3/MX350 gain versus Rigidbody debris is multi-millisecond avoidance in heavy destruction scenes; exact measurement is PENDING PROFILER.

## Decision 01: Local Density Bit Depth
Problem: The prompt requires OSHINO density archaeology, but `Rationale_voxel_densities.md` was not found in Batch005-007 archive search.
Solution: Establish SHINOBU-local `sbyte` density for mock chunks and RLE jobs while leaving the authoritative half-based `VoxelDeltaProcessor` save path intact.
Rejected Alternatives: Rewriting existing half SDF persistence was rejected because it crosses Agent 03/voxel persistence ownership and risks save corruption.
Scalability potential: Low = 32 KB per 32^3 mock density chunk; Middle = direct RLE pairs; High/Ultra = saved CPU memory bandwidth can buy more debris rendering.
Hardware Impact: i3/MX350 benefits from byte-wide cache footprint; estimated 2x-4x lower local mock density bandwidth versus half/float scratch.

## Decision 02: Dear Lie Debris Path
Problem: Rigidbody debris is the stated FPS failure mode, and legacy editor-only object spawning exists elsewhere in the repo.
Solution: Keep the runtime path on existing DataVault SoA buffers (`CarveDebris`, `CarveDebrisVelocity`) plus GraphicsBuffer/indirect render. `DebrisParticleDTO` remains the Burst/editor proof DTO, while runtime avoids a new BufferID lane.
Rejected Alternatives: MeshCollider chunks, pooled prefab rocks, per-rock GameObjects, and a new `ShinobuDebrisParticles` BufferID were rejected because they move cost to Transform/PhysX or add unnecessary core enum churn.
Scalability potential: Low = 500 active chips and slag mask; Middle = 4096 chips; High/Ultra = 10,000 chips with denser visual feedback.
Hardware Impact: Avoids multi-ms PhysX spikes on i3/MX350 during heavy carving; exact profiler capture still blocked by project compile/runtime state.

## Decision 03: Safety Gate And Telemetry
Problem: A mock laser can request a carve against an unloaded chunk and corrupt native memory if bounds are trusted blindly.
Solution: `MockLaserCarveGateJob` checks `ChunkState == Ready`; rejected requests write a compact hash/flag to a fixed telemetry ring.
Rejected Alternatives: Silent drop was rejected because postmortem data would be missing; clamp-to-edge was rejected because it fabricates terrain writes.
Scalability potential: Low/Middle/High/Ultra all use the same O(1) gate; high-end gains do not justify unsafe speculative writes.
Hardware Impact: Estimated sub-5 us per rejected request, preventing catastrophic out-of-bounds crash cost.

## Decision 04: Burst RLE Contract
Problem: Sparse carve deltas must hand compact data to persistence without managed resize or GC.
Solution: `RleCompressSByteJob` and `RleDecompressSByteJob` use `NativeList<short>` pair format `[Value,Count]`, splitting runs at `short.MaxValue`.
Rejected Alternatives: Managed `List<T>`, JSON/string payloads, and raw full-chunk saves were rejected for allocation and I/O bandwidth.
Scalability potential: Low = uniform chunks compress to 4 bytes; Middle = fragmented chunks still bounded; High/Ultra = telemetry exposes bad ratios for later chunk compaction.
Hardware Impact: Worst-case RLE is still bounded by preallocated list; best-case chunk save bandwidth drops from 32 KB to 4 bytes in the local sbyte domain.

## Decision 05: Compile Evidence
Problem: CLI verification is blocked by existing project errors outside SHINOBU edits.
Solution: Ran `git diff --check` on touched files, attempted `Assembly-CSharp.csproj`, and attempted `Hecton8.Core.csproj` with no restore/analyzers. Recorded exact external blockers in status.
Rejected Alternatives: Claiming a clean compile was rejected. Running destructive cleanup/reset was rejected because multiple agents are active.
Scalability potential: Not runtime-facing; keeps integration truth explicit for the integrator.
Hardware Impact: No runtime impact; avoids hiding dependency debt that would waste integration time.

## Decision 06: Slag Mask Instead Of Terrain Stall
Problem: Marching Cubes rebuild can lag the laser impact by frames, exposing a visible mismatch.
Solution: Use the existing recent-cut heat impostor globals from `VoxelDeltaProcessor.PushRecentCutHeat`; this is already consumed by project shaders as a visual cover.
Rejected Alternatives: Runtime decal GameObjects and immediate mesh rebuild were rejected due allocation/CPU burst risk.
Scalability potential: Low = shader heat mask only; Middle = heat plus GPU debris; High/Ultra = denser heat stack and more debris while terrain catches up.
Hardware Impact: No new CPU allocation; saves potential per-impact GameObject/decal setup cost on i3/MX350.

## Decision 07: CPU Loot Decoupled From GPU Debris
Problem: Inventory yield must not wait for cosmetic falling rocks.
Solution: `VoxelDeltaProcessor` emits `ItemAcquiredSignal` immediately after CPU carve commit for Titanium material, with `ItemHash=Data_TitaniumScrap` and `OreHash=Titanium`.
Rejected Alternatives: GPU readback and debris-collision pickup were rejected because visual fake particles are non-authoritative.
Scalability potential: Low/Middle/High/Ultra all keep loot deterministic and independent from render load.
Hardware Impact: Avoids GPU sync stalls and preserves cheap-device responsiveness.

## Decision 08: Hardware LOD Is Binary, Not Balanced
Problem: The prompt demands toaster vs high-end behavior, not a middle-ground cap.
Solution: Low tier resolves to 500 debris; high/ultra resolves to up to 10,000; middle remains 4096. Runtime can override via DataVault tuning but clamp boundaries remain hard.
Rejected Alternatives: Single fixed cap and quality-only particle lifetime were rejected because they fail the i3/MX350 FPS problem.
Scalability potential: Low = slag hides missing debris; Middle = enough chips for readability; High/Ultra = visual overkill.
Hardware Impact: Up to 9500 particles avoided on low tier, preserving frame budget for terrain and gameplay.

## Decision 09: Chunk Boundary Dispatch
Problem: Sphere carves crossing chunk boundaries can leave hard walls if all writes are clamped to one chunk.
Solution: Add `ChunkBoundarySplitJob` for SHINOBU local dispatches and rely on existing runtime commit that resolves each active write to `ChunkAddress`.
Rejected Alternatives: Single owner chunk and edge clamp were rejected because both create seams.
Scalability potential: Low = only overlapped chunks get jobs; High/Ultra = parallel split dispatches scale with larger brush radii.
Hardware Impact: Avoids whole-region repair passes; cost is proportional to overlapped chunk count.

## Decision 10: Editor Control Surface
Problem: Burst/RLE/debris tuning needs proof without the missing laser tool and without Play Mode-only dependencies.
Solution: Added `Voxel Sculptor` editor window with camera-center carve, RLE live stats, debris count, DTO size, and DataVault tuning save through existing `CarveDebrisJobState` slots 5-9.
Rejected Alternatives: Console-only smoke test, serialized inspector fields, persistent editor NativeArray ownership, and a new `CarveDebrisTuning` BufferID were rejected because they either hide live tuning feedback or violate H-Phi/core-churn pressure.
Scalability potential: Low/Middle/High/Ultra presets can be explored by changing cap/gravity/bounce directly in the vault.
Hardware Impact: Editor-only; runtime tuning read is O(1) and allocation-free when the buffer exists.

## Decision 11: Final Audit And Missing Polish Tag
Problem: The batch instructions require reading `<POLISH_MANDATE>` after 100% checklist closure, but the tag is absent from `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Performed local anti-bloat inquisition anyway: grep for `Update()`, `string.Format`, `Instantiate`, `Rigidbody`, and `MeshCollider` in SHINOBU files. No new runtime offenders were found; existing unrelated `List<T>` fields in `VoxelDeltaProcessor` predate this task.
Rejected Alternatives: Inventing a polish mandate or editing unrelated systems was rejected because it would violate evidence-based scope.
Scalability potential: Keeps SHINOBU additions bounded to DataVault/Burst/GPU paths.
Hardware Impact: No runtime cost; avoids late-stage refactor churn.

## Decision 12: Ultra-Think Corrective Pass
Problem: User-supplied polish mandate identified architectural rot risk: new BufferID churn, persistent private editor NativeArrays, runtime `Pack=1`, and Titanium yield reading the request material instead of the old voxel material.
Solution: Removed SHINOBU-added `CarveDebrisTuning`/`ShinobuDebrisParticles` enum slots; packed editor tuning into the existing `CarveDebrisJobState` int segment with float bitcasts; converted editor scratch to per-action `Allocator.TempJob` locals; removed runtime `Pack=1` from debris request/telemetry structs; and counted Titanium yield from `state.MaterialIds[(int)localIndex]` before `SetCell`.
Rejected Alternatives: Keeping a clean-looking dedicated tuning buffer was rejected because it mutates the global enum under concurrent agents. Keeping persistent editor scratch was rejected because the mandate treats private NativeArray fields as data-sovereignty debt. GPU readback for Titanium proof was rejected because loot is CPU-authoritative.
Scalability potential: Low/MX350 keeps 500 active chips and slag cover. Middle keeps 4096 particles. High/Ultra preserves 10,000 GPU chips and dynamic wake payloads without changing gameplay truth.
Hardware Impact: Removed two core BufferID slots from SHINOBU scope, preserved 64-thread compute groups for mobile/Metal safety, removed ARM64 packed runtime layouts, and kept the expected Rigidbody avoidance at 2000-8000 us saved per 1000 debris as a static estimate pending profiler proof.

## Decision 13: L1 ABI And NaN Vaccination Pass
Problem: The second polish mandate forced another local audit. The mock density path only clamped to `[-127,127]`, RLE `AddNoResize` could throw on a bad caller capacity, and padded runtime structs relied on implicit `StructLayout(Size=...)` tail bytes.
Solution: Restored full sbyte density range `[-128,127]`, widened editor delta density to `-255..-1`, added explicit tail padding fields to the padded SHINOBU structs, guarded Burst jobs against missing Native containers and invalid radii/positions, capacity-checked RLE writes, and normalized mock collision normals before reflection.
Rejected Alternatives: Leaving `-127` as "close enough" was rejected because Task 08 explicitly describes `127 -> -128` mass removal. Letting `AddNoResize` fail was rejected because a telemetry path should flag fragmentation instead of crashing. Re-running full restore/build loops was rejected because the known failure is external `Temp/obj` plus non-SHINOBU code.
Scalability potential: Low/MX350 now tests complete density removal with the same 32 KB local chunk footprint. High/Ultra keeps the 10,000-particle visual path but with stricter NaN fail-closed behavior.
Hardware Impact: No fake microsecond gain claimed. The concrete gain is failure avoidance: no packed SHINOBU runtime structs, no unguarded RLE capacity exception, and fewer NaN propagation paths into the render pipeline.

## Decision 14: Voxel Snapshot ABI Alignment
Problem: The third local audit found remaining `[StructLayout(Pack=1)]` DTOs inside the touched `VoxelDeltaProcessor` native snapshot path. The worst offender was the delta-RLE header: a `ulong` payload hash sat at byte offset 28, which is an ARM64 misaligned 8-byte read risk.
Solution: Replaced the runtime packed structs with aligned `Pack=4` layouts: `NativeSnapshotHeader` = 16 bytes, `LegacyNativeSnapshotHeader` = 8 bytes, `NativeSnapshotChunkHeader` = 24 bytes, `NativeSnapshotChunkHeaderRle` = 32 bytes, and `NativeSnapshotChunkHeaderDeltaRle` = 40 bytes. New captures write `HXD5` (`NativeSnapshotDeltaRleAlignedMagic`). Old `HXD2/HXD3/HXD4` snapshots remain loadable through manual 4-byte reads and a split `uint low/high` hash combine, so no legacy packed struct or misaligned `ulong` read remains.
Rejected Alternatives: Keeping `Pack=1` was rejected because it violates the ARM64 mandate. Reusing the old `HXD4` magic with larger headers was rejected because it would silently corrupt cursor math for existing saves. Adding new global BufferID lanes was rejected because this pass is ABI repair, not memory-topology expansion.
Scalability potential: Low/MX350/ARM64 gets safer snapshot hydration without extra runtime debris cost. High/Ultra keeps the same delta-RLE payload and hash protection while using aligned headers.
Hardware Impact: No measured microsecond gain is claimed. The concrete result is removal of packed runtime DTOs and one ARM64 misaligned 64-bit hash read hazard while preserving old snapshot compatibility.

## Decision 15: HXD5 Chunk Record Padding And Unity Boundary Proof
Problem: The aligned HXD5 snapshot headers fixed struct layout, but a 1-byte uniform payload could still leave the next chunk header at an unaligned cursor. CLI `dotnet build` was also not a valid final proof boundary because generated Unity project assets were missing under `Temp/obj`.
Solution: Pad every new HXD5 chunk payload to a 4-byte cursor boundary and make the loader skip that padding only for aligned HXD5 snapshots. Legacy HXD2/HXD3/HXD4 cursor math remains unchanged through manual 4-byte parsing. Then ran Unity 6000.4.1f1 batchmode import/script compilation; Unity generated the two SHINOBU `.meta` files, Bee included all three debris source files in `Hecton8.VFX.Debris.rsp`, and `Hecton8.VFX.Debris.dll` was produced.
Rejected Alternatives: Reusing old HXD4 magic with padded records was rejected because it would silently corrupt legacy cursor math. Claiming `dotnet build Assembly-CSharp.csproj` as proof was rejected because it failed before source compilation on missing `Temp/obj/Assembly-CSharp/project.assets.json`. Moving the pre-existing `VoxelDeltaProcessor` private native rings into DataVault during this pass was rejected because the active compile wall is external and that migration needs a dedicated BufferID/contract integration slot.
Scalability potential: Low/MX350 and ARM64 targets get aligned snapshot hydration without extra debris cost. High/Ultra keep the same delta-RLE payload and 10,000-GPU-chip visual route. Steam Deck/MicroSD pressure stays bounded because HXD5 adds at most three padding bytes per chunk record while preserving RLE compression.
Hardware Impact: No new microsecond number is claimed. The concrete gain is ABI safety: HXD5 chunk headers are aligned after small payloads, the VFX debris assembly compiles in Unity Bee, and the remaining global compile errors are outside SHINOBU scope.

## Decision 16: AUP Truth, DataVault Ownership, And Designer CSV Bridge
Problem: The previous report overstated AUP safety for the mock laser path and still left voxel carve blackbox/write staging as local NativeArray fields. The human bridge also needed a real CSV-to-binary route instead of only IMGUI sliders.
Solution: `MockLaserFireSignal` now carries `double3 AupPosition` and is an explicitly padded 48-byte signal. `VoxelDeltaProcessor` now resolves its 300-frame carve blackbox and scheduled carve-write buffer through DataVault handles `ShinobuDeltaCrusherVoxelBlackBox` and `ShinobuDeltaCrusherCarveWrites`; job memory is locked/unlocked through the vault instead of owned as private arrays. The editor facade now exports/imports `Assets/_Project/Data/VFX/ShinobuDeltaCrusherTuning.csv` and bakes a compact `DXC5` binary tuning file while still writing live values to the existing `CarveDebrisJobState`.
Rejected Alternatives: Leaving local NativeArray ownership was rejected because it violates H-Phi data sovereignty. Runtime CSV/File I/O was rejected because designer bridges are editor-only and Steam Deck MicroSD stalls are forbidden. Adding a dead CSV scratch BufferID was rejected and removed after audit.
Scalability potential: Low/MX350 keeps 500 debris, no GPU readback, and slag impostor coverage. Middle keeps 4096 debris. High/Ultra keep the 10,000-GPU-chip visual route and can tune gravity/bounce without recompiling gameplay assemblies.
Hardware Impact: No new fake microsecond claim. The concrete result is ownership and ABI repair: the mock signal is AUP-truthful, runtime carve staging is vault-backed, editor File I/O is outside gameplay, and manual Unity Csc passes for `Hecton8.Core.Memory` plus `Hecton8.VFX.Debris`.

## Decision 17: AUP Blackbox Forensic Precision
Problem: The voxel carve blackbox field was named `LastHitAup` but stored `float3`, which destroyed the 64-bit forensic truth required for a 100x100 km world. The smoke tester also still tolerated the older 64-byte entry size instead of proving the corrected contract.
Solution: Changed `VoxelCarveTelemetryEntry` to an 80-byte `Pack=4` layout with `double3 LastHitAup` at offset 0, `ulong FocusVolumeId` at offset 24, 4-byte fields next, 2-byte counters next, byte flags next, and explicit `uint _pad0` tail padding. `WriteBlackBoxSample` now validates the pending request AUP with `IsFiniteDouble3` and writes default on non-finite input. `VoxelDeformationSmokeTester` now requires `VaultBufferHandle<VoxelCarveTelemetryEntry> _blackBoxHandle`, `BufferID.ShinobuDeltaCrusherVoxelBlackBox`, and `DebugVoxelBlackBoxEntryBytes == 80`.
Rejected Alternatives: Keeping `float3` because telemetry is "only debug" was rejected because blackbox data is the postmortem truth source. Widening the gameplay debris path to double precision was rejected because debris is cosmetic and local. Adding runtime file IO or a new signal lane was rejected because the existing DataVault blackbox is sufficient and avoids compile-wall churn.
Scalability potential: Low/MX350 keeps the same fake debris route and 500-particle cap; Middle keeps 4096 debris; High/Ultra keep 10,000 cosmetic chips. The corrected blackbox does not change visual LOD behavior, it only preserves AUP forensic precision.
Hardware Impact: The blackbox grows by 16 bytes per frame entry, from 64 to 80 bytes. At 300 frames this is +4800 bytes, fixed DataVault memory, no hot-path allocation. No microsecond saving is claimed; the gain is failure diagnosis accuracy without changing the debris simulation cost.

## Decision 18: Portable Dispatch Cap And Render Param Upload Cache
Problem: `CarveDebrisComputeRenderer` trusted `GetKernelThreadGroupSizes()` up to 1024 threads even though SHINOBU's actual compute shader uses 64 and the hardware mandate forbids a 1024 ceiling for portable/mobile-safe kernels. The render path also wrote `_CarveDebrisMotionParams` to the material every render call even when the vector was unchanged, increasing avoidable material state churn.
Solution: Added `ThreadGroupPortableMaxSize = 512` and capped kernel thread discovery with it. Added `_boundMotionParams` and `_boundMotionParamsValid` alongside the existing material-params cache so `_CarveDebrisMotionParams` only uploads when the vector changes or the material/buffer binding changes.
Rejected Alternatives: Hard-coding 64 in the renderer was rejected because the shader is already 64 today, but keeping a portable discovery path is less brittle if the compute asset changes to 256/512 later. Rewriting the shader to a separate frame CBUFFER was rejected for this pass because it touches shader ABI and requires Frame Debugger proof; the minimal cache removes redundant state writes without changing shader layout. Leaving the 1024 cap was rejected because it contradicts the Metal/MX350 dispatch mandate.
Scalability potential: Low/MX350 remains on 64-thread groups and 500 debris cap. Middle can tolerate 256/512 if the shader changes. High/Ultra still keep 10,000 cosmetic chips; the cap prevents accidental 1024-thread kernels from entering the portable path.
Hardware Impact: No measured microsecond claim. Static effect: one redundant `Material.SetVector` call is skipped whenever motion params are unchanged, and renderer discovery can no longer select a 1024-thread group on platforms where that would be unsafe.

## Decision 19: Clamped Mass Truth And Fail-Closed Proof Jobs
Problem: The SHINOBU local proof carve job accumulated removed mass from raw int accumulator values. After a cell had already reached `sbyte.MinValue`, another subtractive carve could drive the int accumulator lower and mint extra debris even though no additional voxel mass existed. Several proof jobs also trusted matching NativeArray lengths, and the fake debris job trusted the sampler to return finite distance/normal values.
Solution: `VoxelSphericalCarveJob` now clamps previous and next density to `[-128,127]` before calculating removed mass, so repeated empty-cell carving adds zero debris mass. `MockVoxelGridGeneratorJob`, `InitializeDensityAccumulatorJob`, and `ApplyCarveDensityDeltasJob` now reject invalid containers or mismatched lengths. `DebrisMassToCountJob` uses a `long` intermediate to avoid integer overflow, emission clamps count to particle capacity, and `DebrisPhysicsFakeJob` clears particles on non-finite sampler distance/normal.
Rejected Alternatives: Treating the proof jobs as editor-only and leaving caller-trust contracts was rejected because the prompt explicitly demands self-contained fallback mocks. Adding a more realistic rubble conservation model was rejected because the Dear Lie goal is a bounded visual fake, not granular truth. Throwing exceptions on invalid containers was rejected because Burst/hot proof paths must fail closed, not halt the frame.
Scalability potential: Low/MX350 keeps the 500-particle cap and now avoids duplicate rubble on repeated laser passes. Middle keeps 4096 visual chips with deterministic mass bounds. High/Ultra keep 10,000 chips but cannot inflate visual overkill from already-empty voxels.
Hardware Impact: No measured microsecond saving is claimed. Concrete gain is correctness and stability: no over-spawn from repeated empty-cell carves, bounded count math, and fewer NaN propagation paths into cosmetic debris.

## Decision 20: H8Dump Fault Contract
Problem: The SHINOBU blackbox buffers existed, but the fault export paths still used stale `.bin` names. That violates the current HECTON-8 crash contract and makes postmortem tooling ambiguous when multiple agents write dumps.
Solution: Debris blackbox export now writes `Docs/AgentLogs/Dump_SHINOBU_05_DEBRIS_PHYSICS_FAKE.h8dump` and prefixes the raw 300-frame ring with a fixed 20-byte little-endian header: magic, capacity, entry size, cursor, reason flags. Voxel carve blackbox export now writes `Docs/AgentLogs/Dump_SHINOBU_05_VOXEL_CARVE.h8dump` through the existing headerized writer. The smoke tester now asserts the `.h8dump` path so the contract cannot silently regress.
Rejected Alternatives: Leaving `.bin` was rejected because it breaks the blackbox mandate. Adding runtime parsing or background file workers was rejected because the dump is fault-path only and should not alter the hot debris/carve loop. Creating a new signal lane was rejected because the existing DataVault blackbox handles already contain the 300-frame truth.
Scalability potential: Low/MX350 still pays zero steady-frame cost; the write only occurs on invalid state. Middle/High/Ultra get the same deterministic forensic payload without changing visual LOD or debris cap behavior.
Hardware Impact: No microsecond saving is claimed. The concrete result is crash-diagnosis integrity: the debris `.h8dump` now contains enough fixed-size metadata to decode the ring order and entry stride without guessing. Compile proof remains bounded by external project state: `dotnet build Assembly-CSharp.csproj` fails on missing RealtimeCSG source files and non-SHINOBU `SaveBinaryStorage.cs(2423,65)` CS0841.

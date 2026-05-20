# Rationale_SHINOBU_133

Status: PENDING VERIFICATION

## Preflight Decisions

Problem: Cartography prompt demands persistent 1-bit voxel state, GPU visualization, save/net compatibility, editor control, and black-box telemetry without local native ownership.
Solution: Use owner-local cartography runtime files and request stable Vault buffer IDs for bitmasks, sectors, upload staging, telemetry, tuning, scanner profiles, and debug gizmos. Keep cross-domain SDF/save/network integration as contract seams unless matching interfaces already exist.
Rejected Alternatives: Direct `NativeArray<T>` fields in a MonoBehaviour, managed `List<Vector3>` point cloud, `Texture3D.SetPixels()`, and direct concrete calls into Voxel/Save sibling domains. These violate Vault law, GC law, and compile-wall routing.
Scalability potential: Low uses sparse sector paging, 1Hz upload cadence, coarse extraction stride, nearest-cell surface masks. Middle raises update cadence and extraction density. High keeps more sectors resident and richer hologram shader math. Ultra spends saved CPU on denser scanline/glow/noise shader work, not on point-cloud truth.
Hardware Impact: Estimated low-end i3/MX350 gain versus point cloud + SetPixels path: avoids megabytes of managed vector storage and synchronous texture copy spikes; target savings are in hundreds to thousands of microseconds per visual update, pending profiler proof.

Problem: ARM64 atomic `ulong` mutation can fault or stall when layout is ambiguous.
Solution: Primary DTOs will use explicit layout and multiples of 8/16 bytes; `CartographySectorDTO` must be exactly 32 bytes per prompt, telemetry exactly 64 bytes for cache-line writes.
Rejected Alternatives: `[StructLayout(Pack=1)]`, runtime `bool`, C# properties, or implicit padding.
Scalability potential: Stable layout serves Quest/ARM64 and desktop Burst/IL2CPP without bifurcated code paths.
Hardware Impact: Prevents unaligned 64-bit read-modify-write traps and cache-line split penalties on low-end ARM64; estimated hot atomic penalty avoided is unbounded relative to trap path, static proof pending.

Problem: The hologram map must look like a rich 3D map without real geometry.
Solution: Dear Lie path: CPU stores only 1-bit discovery truth; GPU shader raymarches a compact voxel texture/buffer and manufactures wireframe cells, scanlines, flicker, and chromatic offsets.
Rejected Alternatives: Instantiated mini-prefabs, CPU mesh cubes, MeshCollider/Physics.Raycast terrain truth for map pixels.
Scalability potential: Low collapses visual refresh and ray steps; Ultra increases shader steps, glow taps, and temporal noise while truth storage remains constant.
Hardware Impact: Avoids GameObject/mesh/renderer overhead entirely; estimated saved CPU per active map frame is >1000 us versus thousands of cube renderers, pending Frame Debugger/profiler proof.

## Implementation Decisions

Problem: `PlayerExplorationTracker` previously owned 3D cartography arrays directly, which violates the Vault law and made rollback/save/gizmo/upload paths compete for different owners.
Solution: Move 3D cartography truth to `GlobalDataVault` buffer IDs `71420..71436`; keep only pre-existing 2D PDA chunk mask as local tracker-owned legacy state because it is not the 3D sonar map truth. `TryGetDiscoveredSectorsPayload()` now returns Vault-resolved `NativeArray<ulong>`.
Rejected Alternatives: Hiding private `NativeArray<ulong>` fields behind accessors, adding a second singleton cartography service, or writing a broad GlobalRegistry slot. These would increase compile-wall surface and still leave state outside the Vault.
Scalability potential: Low resolves one resident sector upload with sparse visual decimation and long cadence. Middle keeps normal cadence and surface shell. High/Ultra can keep all 3x3 resident sectors, run denser mock/profile passes, and spend ALU in the shader without changing CPU truth storage.
Hardware Impact: Eliminates one private `NativeArray<ulong>[32768]`, one private POI native scratch array, one private dirty flag array, and one private telemetry ring from the 3D cartography path. Expected low-end gain is lower persistent fragmentation and no managed point-cloud growth; measured frame impact pending.

Problem: Atomic bit flips on `ulong` have no direct portable `Interlocked.Or(ref ulong)` API in the current Unity/C# surface.
Solution: Implement `AtomicOr(ulong*, int, ulong)` with `Interlocked.CompareExchange` over the same aligned 64-bit lane reinterpreted as `long`. The owner counter is 64-byte explicit layout and records changed state, delta, total discovered voxels, last bit, and sector hash.
Rejected Alternatives: Non-atomic `words[word] |= bit`, `NativeArray<bool>`, `NativeBitArray` as shared 3D truth, or managed lock. Non-atomic writes race under overlapping sonar pings; managed locks are not Burst-compatible.
Scalability potential: Low and Ultra both share the same deterministic truth update. Higher quality expands radius/cadence and shader work; it does not change the storage contract.
Hardware Impact: One cache-line RMW for newly discovered voxels; unchanged bits exit after compare. ARM64 alignment is guarded by explicit DTO sizing and Vault `ulong` element alignment.

Problem: The terrain masking task depends on another domain's SDF pipeline, but direct sibling runtime references would violate compile-wall routing and may not exist in the current checkout.
Solution: Add `SurfaceMaskWords` as the contract seam and a deterministic `BuildMockSurfaceMaskJob` fallback. `ApplySonarDiscoveryJob` checks the SDF/surface mask bit before revealing; when no producer has populated it, the fallback shell avoids solid-volume fill.
Rejected Alternatives: Direct calls into a concrete Voxel runtime, `Physics.Raycast`, `MeshCollider`, or filling all sphere voxels. These are slower and couple cartography to a sibling owner.
Scalability potential: Low widens the shell band and therefore reduces precise SDF work. High/Ultra can narrow the shell and feed real SDF mask words from the voxel owner.
Hardware Impact: Adds a single `ulong` mask load and bit test per candidate voxel instead of terrain raycasts; expected savings are unbounded relative to physics queries in dense pings.

Problem: Designers need scanner and visual tuning without C# recompiles, but runtime string parsing or managed dictionaries would break zero-GC policy.
Solution: Add `scanner_hardware_profiles.csv`, Vault byte scratch, fixed 32-slot profile table, FNV-1a byte hashing, and an Editor-only UI Toolkit tuner that writes `CartographyTuningDTO` directly.
Rejected Alternatives: `string.Split`, `Dictionary<string,...>`, ScriptableObject-only runtime lookups, or serialized constants requiring domain reload.
Scalability potential: Low scanner profiles can use larger cell resolution, lower glow, and slower upload cadence. Middle/High/Ultra profiles progressively increase ping radius, reduce surface thickness, and boost shader glow.
Hardware Impact: Runtime hot path remains byte/NativeArray based. Editor file read allocates cold only; no gameplay allocation is introduced.

Problem: Hologram visuals can become a hidden CPU cost if the CPU extracts visible cubes or point sprites.
Solution: Store 1-bit truth only, format a packed R8 buffer, and let `Hecton_HologramMap.shader` fabricate the 3D wireframe volume through quality-scaled ray steps, scanlines, flicker, and chromatic offset.
Rejected Alternatives: CPU mesh extraction, `GameObject` debug cubes for runtime, synchronous `Texture3D.SetPixels`, and point-cloud truth storage.
Scalability potential: Low: ~8 shader steps, nearest-cell reads, upload cadence near 54 frames at quality 0.1. Middle: 20-35 steps and moderate glow. High: 48 steps. Ultra: 64 steps with stronger glow/flicker while CPU truth is unchanged.
Hardware Impact: CPU work is reduced to bitmask mutation plus packed buffer copy. Saved CPU can be spent on shader presentation; actual GPU timing requires Frame Debugger/profiler proof.

Problem: The first pass produced a packed R8 upload seam but left the existing PDA map renderer bound to the legacy raw-`ulong` compute point-cloud path only.
Solution: Add a `PDAMapTab` hologram pass that allocates a cold `GraphicsBuffer<uint>` sized to `PackedUploadWordCount`, calls `TryPrepareCartographyUpload()` only when the continuous cadence expires or revision changes, uploads with `LockBufferForWrite`, and binds `_CartographyVoxelR8` to `Hecton_HologramMap.shader`.
Rejected Alternatives: Replacing the whole PDA point-cloud compute path in this batch, creating a real `Texture3D` via `SetPixelData/Apply`, or moving the renderer into a sibling world/runtime system. The point-cloud path remains as overlay/fallback; the new virtual 3D texture path owns the Fog-of-War volume presentation.
Scalability potential: Low quality holds packed upload for ~54 frames and shader raymarches 8 steps. Middle reduces cadence and raises shader steps continuously. High/Ultra upload more often and increase shader glow/step count without changing CPU truth storage.
Hardware Impact: Removes the missing render bridge without adding per-frame managed arrays. The upload is one packed R8 buffer copy instead of per-voxel objects or managed texture colors; GPU timing still requires Unity runtime proof.

Problem: Pending sonar reveal signals were still held in a private persistent `NativeQueue<MapRevealSignal>`, creating a local native owner and possible block growth if producers over-enqueued.
Solution: Route pending reveal signals through Vault buffer `71428 MockPings[16]` and store `PendingSignalCount` in the 64-byte `CartographyCounterDTO` at offset 28. `DrainMapRevealSignals()` consumes the fixed Vault lane and clears the count after processing.
Rejected Alternatives: Keeping the prewarmed `NativeQueue`, allocating a `NativeList`, or publishing a direct sibling event dependency. A fixed Vault lane matches the prompt's fallback mock path and avoids queue allocator behavior.
Scalability potential: Low drops excess pings deterministically when the 16-slot fixed lane is full. Middle/High/Ultra still scale by ping radius and upload cadence, not by unbounded queue growth.
Hardware Impact: Removes one persistent native queue from the 3D cartography path and prevents queue-block allocation under acoustic burst spam; expected gain is avoiding allocation spikes rather than steady-state ALU savings.

Problem: The hologram bridge honored Vault tuning but did not clamp against `HomeostasisBrain.GlobalQualityWeight`, and the legacy point-cloud overlay still used a binary low-tier branch.
Solution: Resolve effective quality as `min(HomeostasisBrain.GlobalQualityWeight, CartographyTuningDTO.GlobalQualityWeight)` in both `PDAMapTab` and `PlayerExplorationTracker`. Replace point-cloud `lowTier` booleans with polynomial-smoothed `math.lerp` budgets: word stride scales from 8 to 1 and max emitted bits per word scales from 1 to 4.
Rejected Alternatives: Keeping `HardwareTierDetector.SharedMemoryModeActive`, `HectonQualityTier.Low/Mx350`, or a hysteresis boolean around map extraction. Those switches create visible quality cliffs and violate the global scalar law.
Scalability potential: Low uses sparse overlay extraction and long packed-R8 upload cadence. Middle progressively reduces stride. High/Ultra emit denser overlay points and run the shader closer to full ray-step count.
Hardware Impact: Low-end/i3/MX350 path reduces compute dispatch work by up to 8x on the overlay and avoids per-frame packed buffer upload at low quality. Ultra path spends the recovered CPU budget on shader glow/raymarch density.

Problem: The build cannot be run safely under the current policy when CPU is saturated.
Solution: Sampled CPU before build; results were `100`, `68`, and `100`, so `dotnet build` was not launched. Static scans were completed instead.
Rejected Alternatives: Ignoring the CPU gate or launching a broad build while other agents/system load may be active.
Scalability potential: No runtime effect; preserves developer machine responsiveness during multi-agent batch work.
Hardware Impact: Avoids compounding CPU contention and Unity/dotnet compiler-server churn.

Problem: The previous cartography mutation route still depended on `ISlowTickable` synchronous `.Run()` jobs for the live simulation path, so the master dispatcher had no `JobHandle` seam for dependency ordering.
Solution: Added owner-local `CartographyDispatcherPhaseSystem` adapters for `PreSimulation`, `Simulation`, and `PostSimulation`. `PreSimulation` stages the player AUP and fixed Vault pending-signal count, `Simulation` schedules `ApplyCartographyFrameDiscoveryJob.Schedule(dependsOn)`, and `PostSimulation` consumes the 64-byte counter plus writes black-box telemetry. The legacy `SlowTick()` path now returns immediately when dispatcher registration succeeds and remains only as fallback for missing dispatcher bootstrap.
Rejected Alternatives: Keeping all mutation in `SlowTick()` was rejected because it hides cartography work from the Kahn dependency chain. Adding a broad new Core dispatcher API was rejected because interface mutation is forbidden during the batch.
Scalability potential: Low devices shed visual upload cadence while still recording authoritative bits in the scheduled simulation phase. Middle keeps normal ping cadence. High and Ultra can append denser POI/sonar pending signals and spend saved CPU on shader raymarch/glow without changing the dispatcher contract.
Hardware Impact: Removes the live-path main-thread `.Run()` bitmask mutation when the master dispatcher is registered. Exact microseconds require Unity profiler proof; static risk reduction is dependency ordering and fewer uncontrolled main-thread stalls.

Problem: A single `MockPings` lane with `CartographyCounterDTO.PendingSignalCount` was not concurrency-safe once live mutation moved to a scheduled job. Main-thread acoustic/sonar events could append a future-frame ping while the Burst job was writing `Counters[0]`, causing lost pending counts or stale signal consumption.
Solution: Split pending input from simulation output. `MockPings[16]` plus `PendingSignalCounts[1]` is now the producer lane. `PendingPings[16]` is the dispatcher-staged read lane consumed by `ApplyCartographyFrameDiscoveryJob`. `Counters[0]` remains an output/telemetry counter and no longer owns live producer counts.
Rejected Alternatives: Keeping one lane and relying on frame ordering was rejected because event timing is not a proof. Locking around the NativeArray was rejected because Burst jobs and main-thread event listeners cannot share managed locks in a zero-GC hot route.
Scalability potential: Low devices still cap pending pings at 16 and drop excess deterministically. Middle/High/Ultra scale the radius/cadence/shader work, not the lane count, preserving bounded memory.
Hardware Impact: Adds two tiny Vault buffers, `MapRevealSignal[16]` and `int[1]`, to remove counter races. The copy cost is at most 16 structs per dispatcher frame; expected cost is below profiler noise, runtime proof pending.

Problem: `CartographyLayoutVerifier` used reflection-backed offset checks in the runtime validation path. Reflection is not acceptable in gameplay bootstrap even if it is cold.
Solution: Player/runtime validation now checks blittable sizes with `UnsafeUtility.SizeOf<T>()` only. Exact offset checks remain inside `#if UNITY_EDITOR`, where `Marshal.OffsetOf<T>()` is an editor-only layout audit.
Rejected Alternatives: Leaving reflection in `ValidateRuntimeLayouts()` was rejected because it violates the runtime reflection ban. Removing offset validation entirely was rejected because the ARM64 layout proof still needs an editor facade.
Scalability potential: No visual scaling impact; keeps runtime bootstrap deterministic and editor proof strict.
Hardware Impact: Removes runtime reflection metadata access from the Vault boot path; expected gain is small but eliminates an avoidable managed/AOT risk.

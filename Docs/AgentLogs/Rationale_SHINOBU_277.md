# Rationale_SHINOBU_277

Status: STATIC IMPLEMENTATION COMPLETE / COMPILE BLOCKED BY CPU GUARD

## Initial Boundary

Problem: Existing task requests Crest shoreline foam grafting into the RenderGraph path without CPU particles or auxiliary orthographic cameras.
Solution: Treat foam as deterministic screen-space visual fake using depth versus localized water height. Read relevant mandates before implementation and preserve existing third-party Crest assets instead of rewriting or cloning them.
Rejected Alternatives: CPU particles, DecalProjector GameObjects, auxiliary shoreline cameras, runtime material cloning, and Graphics.Blit compatibility paths are rejected by zero-GC, SRP Batcher, and RenderGraph mandates.
Scalability potential: Low uses minimal shader loop and cheap depth-height falloff; Middle adds noise modulation; High adds richer normals/shore streaks; Ultra spends saved CPU/GPU budget on higher-frequency foam detail while keeping gameplay truth unchanged.
Hardware Impact: Removing extra cameras and CPU foam object spawning is expected to save submission/fill cost on i3/MX350. Exact microseconds remain PENDING VERIFICATION until static compile and Unity profiler evidence exist.

## Decision 001: DTO Size Conflict

Problem: The XML prompt contains contradictory layout directives. Mandatory constraints require `[StructLayout(LayoutKind.Explicit, Size = 32)]` with two float4 lanes. Self-reflection asks for Size = 80.
Solution: Use the mandatory constraints as primary for `ShorelineFoamParamsDTO`: 32 bytes, offset 0 `FoamIntensityAndFalloff`, offset 16 `QualityAndLimits`. If existing code already has an 80-byte decal matrix DTO, do not rename it into foam authority without proof.
Rejected Alternatives: Silently choosing 80 bytes would violate the hard constraint and bloat shader upload. Inventing a hybrid 48/64-byte layout would create an undocumented ABI.
Scalability potential: 32 bytes keeps low-tier bandwidth minimal; higher tiers can consume richer continuous parameters encoded inside the two float4 lanes or bind a separate cold profile table if later justified.
Hardware Impact: 32-byte stride halves or better the bandwidth versus 80-byte records. Estimated benefit is small per record but relevant when uploading 128 records on MX350. Exact microseconds PENDING VERIFICATION.

## Decision 002: Batch Hygiene

Problem: `Status_SHINOBU_277.md` and `Rationale_SHINOBU_277.md` were missing at session start.
Solution: Treat missing files as empty current-batch memory and create fresh files before code edits.
Rejected Alternatives: Reusing other agents' logs or reading archive data would violate batch hygiene and neighboring-prompt isolation.
Scalability potential: Disk-backed state allows recovery after context compaction without losing task boundaries.
Hardware Impact: None at runtime; editor/documentation-only.

## Decision 003: RenderGraph Graft Instead Of Crest Vendor Patch

Problem: Crest 5 contains foam and shoreline math, but active HECTON ocean already disables Crest foam/depth cameras and owns a RenderGraph depth-mask pass.
Solution: Leave `Assets/Crest/Crest/**` untouched, bind `_GlobalShorelineFoam` into `HectonSinglePassOceanFeature`, and evaluate foam in `Hidden/Hecton8/OceanDepthFoam.shader` by depth-buffer world reconstruction versus localized water height.
Rejected Alternatives: Re-enabling Crest `OceanDepthCache`, adding orthographic shoreline cameras, using CPU particles, and using `DecalProjector` were rejected because they add render submissions, scene objects, or hidden camera authority.
Scalability potential: Low uses one active row and a tiny shader loop; Middle expands ring rows and falloff; High increases active shoreline rows and reflection perturbation; Ultra spends extra loop budget on denser visual foam while keeping the same 32-byte ABI.
Hardware Impact: Avoided auxiliary camera render cost is estimated at 650-1800 microseconds on i3/MX350 depending on depth resolution and terrain fill. Current graft cost is estimated below 20 microseconds CPU submit plus 0.035-0.19 microseconds per shader row; profiler proof pending.

## Decision 004: Contract-Only Integrity Input

Problem: The prompt requests `ProcessFoamParametersJob` to read `IntegrityStateDTO`, but OceanSinglePass must not depend on concrete deformation runtimes or poll sibling systems in RenderGraph setup.
Solution: Add a reference only to `Hecton8.Habitat.Deformation.Contracts` and define `ProcessFoamParametersJob` as a pure Burst transform over caller-provided `NativeArray<IntegrityStateDTO>` into `NativeArray<ShorelineFoamParamsDTO>`. Runtime shoreline depth still runs from ocean VisualSync without searching habitat objects.
Rejected Alternatives: Pulling deformation runtime instances through scene search or `GlobalRegistry` in RenderGraph was rejected. Duplicating an `IntegrityStateDTO` shadow struct was rejected because two owners for one ABI creates silent layout drift.
Scalability potential: Low can run zero external integrity rows and keep global shoreline foam; Middle/High/Ultra can feed more integrity rows through the same job without changing shader layout.
Hardware Impact: Contract-only job adds no scene lookup cost. At 64 rows, Burst math cost is estimated under 15 microseconds on low-end CPU; exact profiler proof pending.

## Decision 005: LockBuffer Double Upload

Problem: Shoreline foam rows must reach the GPU during VisualSync without forcing synchronous stalls.
Solution: Allocate two `GraphicsBuffer.Target.Structured` buffers cold, map the inactive buffer with `LockBufferForWrite`, copy `ShorelineFoamParamsDTO` rows through `CopyShorelineFoamParamsToMappedBufferJob`, then publish the buffer for the next RenderGraph pass.
Rejected Alternatives: `GraphicsBuffer.SetData()` was rejected because it can force main-thread synchronization. Managed arrays and material property string setters were rejected by zero-GC and shader-ID mandates.
Scalability potential: Low uploads 1 row/32 bytes; Middle uploads roughly 24 rows/768 bytes; High uploads roughly 48 rows/1536 bytes; Ultra uploads 64 rows/2048 bytes.
Hardware Impact: Estimated main-thread stall avoidance is 20-80 microseconds versus `SetData()` for tiny dynamic buffers on MX350-class hardware. Exact GPU driver proof pending.

## Decision 006: Localized Water Height ABI

Problem: Sending absolute double precision ocean/world coordinates to GPU would violate AUP precision boundaries and lose accuracy near 100 km map edges.
Solution: Store water height as `waterSurfaceAupY - cameraAupY` in `ShorelineFoamParamsDTO.FoamIntensityAndFalloff.z`, send the camera-local origin Y in `_GlobalShorelineFoamRuntime.x`, and reconstruct the comparison height in shader.
Rejected Alternatives: Passing absolute `double3` to GPU is impossible in the shader path. Passing absolute float world sea level repeats the precision failure the AUP doctrine forbids.
Scalability potential: Low through Ultra all share the same localized ABI; quality only changes loop count, decay, intensity, and perturbation.
Hardware Impact: Same bandwidth as two float4 lanes. Precision repair has no measurable CPU cost; it prevents visual jitter that would otherwise require expensive camera/depth workarounds.

## Decision 007: Black-Box And Rollback Fence

Problem: Visual shoreline foam can fail via NaN, stale GPU upload, or capacity exhaustion, but it must never become gameplay truth or rollback authority.
Solution: Allocate a 300-row `ShorelineFoamTelemetryEntry` ring, write raw dumps to `Docs/AgentLogs/Dump_SHINOBU_277.bin` on upload spike, and document buffers `71940..71946` as presentation-only in architecture/report artifacts.
Rejected Alternatives: Serializing foam into `StateRingBuffer` or Merkle hashes was rejected because transient foam is a presentation lie. Per-frame managed logging was rejected because it allocates and hides the last-frame state.
Scalability potential: Low records the same telemetry fields with one row active; Ultra records the same bounded 300 rows with more active rows and richer perturbation.
Hardware Impact: Telemetry write is one 64-byte row per frame. Estimated cost is under 1 microsecond CPU memory bandwidth; dump is exceptional diagnostic I/O only.

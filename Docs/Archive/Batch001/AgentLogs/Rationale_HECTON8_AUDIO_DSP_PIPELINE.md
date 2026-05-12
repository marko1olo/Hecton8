# Rationale_HECTON8_AUDIO_DSP_PIPELINE

## Decision 1: High-tier cave convolution without full IR cost

Problem: The scalable reverb requirement asked for high-tier convolution, but full 2-second convolution is not acceptable for a global underwater audio path without profiler proof.

Solution: Added a fixed 32-tap pre-baked cave impulse response in `NativeArray<float>` plus a 128-sample masked delay ring. High/Ultra select `NativeConvolution`; lower tiers remain Unity profile/mixer driven. Acoustic density comes from `WorldSpatialHashGrid.TryGetAcousticDensityMap` and only modulates wet/damping scalars.

Rejected Alternatives: Full 2-second convolution was rejected because it would create hundreds of thousands of multiply-accumulates per block. Per-voxel reverb truth was rejected because density already exists as a cinematic scalar.

Scalability potential: Low uses generic reverb profile and RT60. Middle stays profile-only. High uses 32-tap native convolution. Ultra can later lengthen the IR behind the same enum path after profiler proof.

Hardware Impact: Low-end i3/MX350 avoids native convolution entirely, estimated 10-70 us/block saved. High tier avoids full IR, estimated 250-600 us/block saved versus long convolution while preserving cave coloration.

## Decision 2: Async high-tier occlusion with one raycast and AABB thickness proxy

Problem: Low tier cannot use physics queries, but high tier requested exactly one async physics raycast per source for obstacle-aware occlusion.

Solution: `AcousticOcclusionUtility` now queues one `RaycastCommand` per uncached path only when `GlobalRegistry.ScalabilityTier` is High/Ultra. Results are completed on a later `LateFrameTick`, then cached. Obstacle thickness is approximated from collider AABB projection along the ray direction.

Rejected Alternatives: `Physics.Raycast`, `RaycastNonAlloc`, and multi-hit ray chains were rejected. They either block the main thread or exceed the one-ray budget. Exact mesh thickness was rejected as geometry truth where a low-pass scalar is enough.

Scalability potential: Low/Mx350 sees only deterministic distance/flora muffles. High/Ultra buys more precise muffling from one async ray and a thicker low-pass response.

Hardware Impact: Low-end path remains 0 physics queries. High path saves roughly 12-30 us/source versus chained/multi-hit occlusion and avoids synchronous stalls.

## Decision 3: Power-of-two cache rotation

Problem: The occlusion result cache used modulo rotation and capacity 48 while the audio mandate prefers maskable Po2 structures where possible.

Solution: Raised cache capacity to 64, added `MaxQueuedRequestsMask`, compile-time guard, and replaced modulo cache write wrap with `& MaxQueuedRequestsMask`.

Rejected Alternatives: Keeping `%` was rejected because capacity is now structural and small enough to be Po2. Dynamic cache containers were rejected because they would add allocation risk.

Scalability potential: All tiers benefit from deterministic cache wrap. Ultra can queue more high-tier ray requests without changing layout.

Hardware Impact: Tiny direct gain, estimated 0.02 us/cache write, but it removes one avoidable integer division from the audio/acoustic path.

## Decision 4: External compile-wall fix

Problem: After restoring `project.assets.json`, core build failed in `ScannableFragment.cs` because `string.AsSpan()` extension resolution lacked `using System;`.

Solution: Added `using System;` to `ScannableFragment.cs`. No runtime behavior, no allocation, no public API change.

Rejected Alternatives: Rewriting the hash call was rejected because the existing span-based hash path is correct and zero-GC; the file only missed the namespace import.

Scalability potential: Not a scalability feature. It preserves zero-GC hash calculation by keeping the span overload.

Hardware Impact: No runtime gain. Build wall removed.

## Verification Boundary

Static scans and `dotnet build` passed after restore. Unity import, Unity Console, Play Mode, profiler, GCMonitor, mixer routing, clip import reimport, and audible quality are not verified in this session.

STATUS: PENDING VERIFICATION

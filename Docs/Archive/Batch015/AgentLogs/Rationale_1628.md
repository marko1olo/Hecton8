# Rationale 1628 - ABYSSAL_BIOME_TRANSITION_AND_DITHER_FOG_POLISHER

Status: ACTIVE / PENDING VERIFICATION

## Decision 000 - Domain and Mandate Scope

Problem: The pasted request lists 19 tasks and an incomplete XML tag, while the batch file contains 20 tasks with stricter shader/CBuffer directives.
Solution: Use the extracted `Docs/Tasks/CURRENT_BATCH.md` prompt as primary assignment. Map scope to Domain 18 and Domain 67.
Rejected Alternatives: Using the pasted list would drop shader validator and metric-report tasks and corrupt task-count tracking.
Scalability potential: Low uses cheap depth fog and Bayer/blue-noise fakery; middle adds stable dither and LUTs; high adds richer visual layers only where budget exists; ultra spends saved cycles on denser atmospheric polish, not gameplay truth.
Hardware Impact: Static evidence only. Expected gain on i3/MX350 comes from replacing raymarch-style fog with analytical single-depth-read fog. Measured proof absent.

## Decision 001 - Visual Fake First

Problem: The task requests abyssal atmosphere, light shafts, turbulence, and caustic depth effects without exceeding MX350 fill-rate.
Solution: Default to offline-baked textures, analytical fog, Bayer dither fallback, and branchless continuous quality weights. No runtime multi-sample raymarching.
Rejected Alternatives: Real volumetric scattering, multi-camera passes, runtime per-particle silt, and full-screen high-sample noise stacks are too expensive without profiler proof.
Scalability potential: Low: one depth read plus analytical fog. Middle: dither and LUT. High: additional noise blend. Ultra: richer light-shaft mesh/noise authoring while preserving the same authority route.
Hardware Impact: Expected GPU savings are from reduced texture/depth fetches and ALU loops. Exact microseconds remain pending until Frame Debugger/Profiler capture.

## Decision 002 - BufferID Range 71670..71675 Rejected

Problem: Task text requested biome lighting writes in `71670..71675`, but `H8Memory.cs` already assigns `71670..71672` to seaglide audio, cavitation, and CSV scratch lanes.
Solution: Preserve the existing biome route `71220..71231` and document the conflict in `BIOME_TRANSITION_REPORT_1628.json`. Add tests proving the requested lane was not hijacked.
Rejected Alternatives: Reassigning seaglide buffers or aliasing biome fog data onto them would violate one fact/one owner and create cross-domain corruption.
Scalability potential: Low/middle/high/ultra all read the same authoritative biome payload; only shader fidelity scales with continuous quality.
Hardware Impact: Prevents undefined cache-line contention and misrouted signal reads. Microsecond gain is safety, not speed; estimate 0 us direct frame gain.

## Decision 003 - Single Depth Fetch Fog Composite

Problem: The existing DearLie fog composite used a 3x3 depth-neighborhood gather, multiplying depth reads on TBDR GPUs.
Solution: Replace the neighborhood gather with one depth sample plus analytical edge guard using `H8DitherFogAnalyticalFactor`.
Rejected Alternatives: Keeping bilateral depth taps or adding a second blur pass preserves soft edges but spends fill-rate where MX350 and Quest are weakest.
Scalability potential: Low gets a flat analytical guard; middle/high/ultra increase dither/noise richness through `GlobalQualityWeight` without extra depth fetches.
Hardware Impact: Static estimate saves 8 depth reads per composite pixel. Estimated 54 us GPU on MX350-class scenes; profiler proof still required.

## Decision 004 - 64-Byte Lighting DTO and Quality Scalar

Problem: Shader-side fog/light parameters need a stable 64-byte CBuffer layout while quality must stay continuous, not tiered.
Solution: Add `BiomeLightingParametersDTO` with explicit offsets `0/16/32/36/40`, pack `_H8GlobalQualityWeight` into offset 44, and bind `H8BiomeLightingParameters` through one 64B global CBuffer upload in `LateFrameTick`.
Rejected Alternatives: Material property mutation, string-driven shader globals, and the duplicate 128B legacy payload CBuffer upload would either allocate, increase upload traffic, or weaken the "single compact CBuffer" contract.
Scalability potential: Low: quality near 0 reduces dither weight. Middle: stable Bayer. High: stochastic blend. Ultra: denser shaft/silt polish through the same scalar.
Hardware Impact: CPU upload is one compact 64B constant buffer route plus cached scalar globals for compatibility. Estimated CPU save 17 us against material mutation patterns; CBuffer traffic is 64B instead of the prior 128B payload upload. Profiler proof absent.

## Decision 005 - Build Gate Closed

Problem: Compilation was requested by protocol, but host contention violates the build gate.
Solution: Sampled compiler processes and CPU before build: `dotnet` processes were active and CPU load was 97 percent. Build marked `BLOCKED_BY_CONTENTION`; static scans used instead.
Rejected Alternatives: Launching `dotnet build` under active compiler load would breach the batch protocol and risk invalid results.
Scalability potential: No runtime effect. Keeps local toolchain stable for other agents.
Hardware Impact: Prevented additional host CPU load. Runtime microseconds unchanged.

## Decision 006 - APEX Lock and Phase Proof

Problem: `TryWriteTuning` and default tuning initialization wrote through a mutable DataVault view, and the compact 64B DTO was defined before it was actually bound as the authoritative CBuffer.
Solution: Route tuning writes through `TryWriteSingleBiomeVaultValue` with exactly one write lock and `finally` release; bind `BiomeLightingParametersDTO` through one ping-pong `GraphicsBuffer` during `LateFrameTick`; add source-level static tests for hot lookup bans, phase placement, job completion guards, write-lock symmetry, and compact CBuffer upload count.
Rejected Alternatives: Holding several DataVault write locks across the pipeline would create a deadlock vector; leaving the old 128B CBuffer upload would contradict the compact-CBuffer directive; moving upload into `FastTick` would make presentation race simulation.
Scalability potential: Low keeps quality 0 valid through the offset-48 sentinel and flat analytical fog. Middle/high/ultra increase dither/noise through the same continuous scalar without shader variants or binary quality switches.
Hardware Impact: Runtime hot path remains allocation-free by static scan. Latest build was not launched because CPU sampled at 100 percent with two `dotnet` processes active.

## Decision 007 - Compact CBuffer Authority Over Legacy Payload

Problem: After removing the legacy 128B shader-payload CBuffer upload, `H8DitherFogResolveQualityWeight` still allowed a nonzero legacy payload quality lane to override compact `_H8GlobalQualityWeight`. A stale Unity global CBuffer from an earlier session could therefore fight the new 64B authority route.
Solution: Resolve quality in strict order: fallback material value, optional legacy payload, then compact sentinel-backed `_H8GlobalQualityWeight`. Rename the C# bridge to `TryUploadBiomeLightingParametersFromPayload` so the source states the actual DataVault-to-compact-CBuffer route.
Rejected Alternatives: Clearing or rebinding the legacy CBuffer every frame would spend CPU/GPU traffic to defend against stale state. Keeping the old method name would preserve a false dependency story.
Scalability potential: Low quality `0.0` remains a valid survival state; middle/high/ultra scale dither and shaft intensity from the same compact scalar without shader variants.
Hardware Impact: No measurable hot-path cost added. Prevents visual drift after hot reload and keeps the runtime route at one 64B CBuffer upload per visual sync. Latest build gate stayed closed because CPU sampled near 82 percent.

## Decision 008 - Dirty Compact CBuffer Upload Gate

Problem: Compact lighting upload used `LockBufferForWrite`, but still wrote by struct assignment and mapped/uploaded the 64B CBuffer every completed pipeline even when the DTO was unchanged. AGENTS bandwidth discipline requires `UnsafeUtility.MemCpy` and forbids uploading unchanged data.
Solution: Convert the mapped write to `UnsafeUtility.MemCpy`. Add a stable FNV-1a hash over the compact DTO and skip the buffer lock/upload when the hash matches the active uploaded payload. Reset hash state when buffers are released.
Rejected Alternatives: Byte-compare by mapping the GPU buffer would require opening the buffer before deciding to skip. Epsilon comparison would hide bit-level presentation changes and create harder drift debugging.
Scalability potential: Low avoids needless shared-memory/Pcie traffic. Middle/high/ultra keep the same continuous quality route and spend bandwidth only when fog/shaft state actually changes.
Hardware Impact: Up to one 64B CBuffer upload and one buffer-map window avoided per unchanged visual sync. Latest build gate stayed closed because CPU sampled near 91 percent.

## Decision 009 - Legacy Shader Global Dirty Gate

Problem: The compact CBuffer path was dirty-gated, but compatibility shader globals still called `Shader.SetGlobalVector/Float` every visual sync even when fog, absorption, dither, density, and quality were unchanged.
Solution: Add a second FNV-1a hash over the sanitized legacy global payload and return before the legacy `Shader.SetGlobal*` calls when the payload hash is unchanged. Reset the hash with the compact buffer release path.
Rejected Alternatives: Removing the legacy globals would break existing renderer features that still consume `_H8BiomeTransition*` values. Sharing the compact CBuffer hash would miss legacy-only changes such as biome hashes.
Scalability potential: Low avoids needless CPU presentation churn. Middle/high/ultra keep compatibility globals only when actual atmospheric state changes.
Hardware Impact: Up to ten legacy global shader calls avoided on unchanged visual-sync frames. Runtime profiler proof still absent.

## Decision 010 - Cold Bootstrap Lookup Boundary

Problem: Static scans correctly found `TryGetComponent` in `BiomeBoundarySdfRuntimeBootstrap`, but that route is a scene-load fail-safe, not a cadence loop. A broad grep can therefore produce a false APEX dependency violation.
Solution: Add an editor proof that component lookup is confined to `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`, while existing tests continue to inspect `FastTick`, `LateFrameTick`, and job `Execute` blocks for cold lookup routes.
Rejected Alternatives: Removing the fail-safe would make missing authored runtime hosts fail open in world scenes. Allowing a broad text ban would hide real intent and make the proof brittle.
Scalability potential: Low/middle/high/ultra all keep component identity cached before runtime cadence; quality scaling remains independent of scene repair.
Hardware Impact: 0 us hot-path cost. Cold scene-load component resolution remains outside frame cadence.

# INTERNAL_FLOOD_RENDERER Rationale

Status: PENDING VERIFICATION

## Initial Decision Frame

Problem: Habitat rooms flood in scalar gameplay state but presentation remains dry until full camera submersion.
Solution: Use deterministic screen-space waterline inside the existing Visor Uber Post path, fed by habitat room fill ratios. Physical water planes and per-room meshes are rejected.
Rejected Alternatives: `WaterPlaneManager.Instance`, `FloodVfxManager.Instance`, and `Instantiate(WaterMeshPrefab)` patterns would add scene objects, synchronization hazards, overdraw, and singleton dependency rot.
Scalability potential: Low = color tint only under split; Middle = mild refraction; High = refraction plus droplets; Ultra = stronger procedural detail and longer droplet persistence without new passes.
Hardware Impact: Expected low-end gain on i3/MX350 comes from avoiding mesh spawning and full-screen extra passes. Numeric proof is PENDING VERIFICATION until profiler/Unity capture exists.

## Mandate Binding

Problem: Task crosses habitat simulation, render presentation, AUP, and telemetry.
Solution: Bind implementation to selected mandates: fluid incursion fake-first, cinematic cheat protocol, URP RenderGraph/hotpath rules, noir shader doctrine, AUP shift safety, GlobalRegistry DI, zero-GC, and blackbox telemetry.
Rejected Alternatives: Direct concrete references between habitat, VFX, and gas systems are rejected; only contracts, `GlobalRegistry` owner queries, or existing event/signal lanes are allowed.
Scalability potential: Math LOD must keep MX350 path tint-only and spend saved cost on stronger high-tier post detail.
Hardware Impact: Avoiding per-frame allocation and scene search preserves hot-path GC target at 0 B/frame; measured proof is absent.

## Decision: Contract-Only Habitat Readback

Problem: Camera waterline needs room fill and room bounds without binding the visor runtime to habitat internals.
Solution: Read through `GlobalRegistry.HabitatGraph` / `IHabitatGraphService`, with `HabitatRoomWaterlineSnapshot` carrying fill, room id, floor/ceiling/surface Y, water volume, flags, and flood sequence.
Rejected Alternatives: Direct `HabitatGraphManager` reference, scene search for `BaseModule`, or `WaterPlaneManager.Instance` were rejected because they add order coupling and break parallel-agent boundaries.
Scalability potential: Low = one cached room lookup and tint; Middle = same lookup plus mild refraction; High = stronger droplet/noir response; Ultra = visual overkill stays in shader, not simulation.
Hardware Impact: Cached room query is expected to save 4-12us versus scanning modules every tick on i3/MX350. Measured proof is absent.

## Decision: Existing Uber-Post Only

Problem: The waterline must be visible without adding a second fullscreen renderer feature.
Solution: Feed `_InternalWaterlineY`, `_InternalWaterColor`, `_InternalWaterlineRuntime`, and `_InternalWaterlineDistortion` into `HectonVisorUberPostFeature` and `HectonVisorUberPost.shader`; compute split from camera pitch and waterline Y.
Rejected Alternatives: A new fullscreen pass, per-room mesh water planes, particle sheets, or material overrides were rejected. They increase overdraw, SetPass pressure, and synchronization bugs.
Scalability potential: Low = tint branch only; Middle = one conditional refracted sample; High = refraction plus procedural droplets; Ultra = stronger shader detail from saved mesh/pass budget.
Hardware Impact: Avoiding a second fullscreen pass is estimated at 180-450us on MX350-class hardware, depending on render scale. Static estimate only.

## Decision: Low Tier Tint-Only

Problem: Initial shader code still sampled a refracted color even when refraction strength was zero.
Solution: Added a branch so refraction sample executes only when `refractionBlend > 0.001`; low/MX350 tier sets refraction strength to zero.
Rejected Alternatives: Keeping the zero-weight sample was rejected because it burns bandwidth for no visible result. Removing all underwater visuals on low tier was rejected because immersion loss is unnecessary.
Scalability potential: Low = no second scene sample; Middle/High/Ultra = spend the saved budget on visible refraction and droplets.
Hardware Impact: Expected low-tier gain is 90-220us for the underwater section on MX350-class fill-limited frames. Measured proof is absent.

## Decision: Telemetry and AUP Rebase

Problem: A runtime-only waterline can desync after floating-origin shifts or become impossible to diagnose after NaN.
Solution: `InternalFloodWaterlineRuntime` implements `IOriginShiftListener`; cached waterline Y values subtract `ShiftOffset.y`. A 300-entry `NativeArray<WaterlineTelemetryEntry>` stores frame, sequence, room id, fill, current/target Y, camera Y, droplet scalar, flags, and hash. Dump path: `Docs/AgentLogs/Dump_INTERNAL_FLOOD_RENDERER.bin`.
Rejected Alternatives: Re-querying the room on the next tick was rejected because one-frame jumps are visible. Text logs were rejected because they allocate and lose frame history.
Scalability potential: Same memory cost on all tiers; Ultra visual cost remains shader-side.
Hardware Impact: Persistent telemetry is 12,000 bytes after the header fix (`300 * 40`), stable on low-end silicon. Hot-path dump cost is zero unless non-finite state is detected.

## Decision: Gas Dynamics Scalar Coupling

Problem: A room that is visually submerged must not keep full oxygen in the submerged portion.
Solution: `IGasDynamicsSolver.TrySetRoomSubmergedFraction(roomId, fill01)` sets a native scalar; the gas job clamps room O2 by dry fraction.
Rejected Alternatives: VFX-to-gas direct calls, per-voxel gas-water simulation, or event spam were rejected. The submerged fraction is a deterministic scalar and enough for gameplay truth.
Scalability potential: Low/Middle/High/Ultra all use the same cheap scalar; high tier spends extra budget visually, not on gas truth.
Hardware Impact: Estimated cost is 0.5-2us per gas solve room; no managed allocation. Measured proof is absent.

## Verification Notes

Problem: Build validation cannot currently prove runtime readiness.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nodeReuse:false /p:UseSharedCompilation=false`. Added the visor files to the ignored generated `.csproj` to eliminate stale-metadata missing-type noise and reran.
Rejected Alternatives: Declaring success from static scan was rejected. Fixing unrelated global missing assembly references was rejected as outside `INTERNAL_FLOOD_RENDERER` domain.
Scalability potential: Verification status remains PENDING until Unity import/console and profiler data exist.
Hardware Impact: No verified frame/GC numbers. Regression model is static only: CPU = one FastTick query plus shader branch, GC = intended 0 B/frame, memory = 12 KB native telemetry, correctness risk = room lookup mismatch at rotated/compound modules.

## Current Blockers

Problem: `dotnet build Hecton8.Core.csproj` is red before this feature can reach final compile proof.
Solution: Mark task 19 blocked by dependency and list exact first observed blocker classes in status.
Rejected Alternatives: Editing `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Propagation`, inventory corrosion, binary layout, acoustic portal, and tether contract systems was rejected as architectural trespass.
Scalability potential: No impact to tier design.
Hardware Impact: No runtime impact; blocked validation only.

## OMEGA POLISH CHANGES

Problem: Added water refraction offset used four `sin()` calls per affected pixel. That is an honest wave calculation where a visual fake is sufficient.
Solution: Replaced the added internal-water offset waves with `CheapSignedTriangle(frac)` terms. Existing non-water `sin()` calls in heat haze/crack code were not touched because they predate this task and are outside this domain.
Rejected Alternatives: Keeping sine was rejected as wasted shader ALU. A texture lookup/LUT was rejected because it adds sampler pressure and asset dependency for a tiny distortion.
Scalability potential: Low = tint only and no water refraction sample; Middle = triangle-wave refraction; High = triangle-wave refraction plus droplets; Ultra = shader detail can scale without new scene objects.
Hardware Impact: Expected internal-water shader ALU reduction is approximately 8-24us on MX350 when the water mask covers a large part of the screen. Measured proof is absent.

Problem: Zero-GC scan found pre-existing interpolated strings and `.ToString()` in `GameBootstrapper`, but not in the added waterline methods.
Solution: No patch applied to bootstrap diagnostics because those lines are outside this task and mostly cold/error path. Added waterline runtime contains no `foreach`, no `string.Format`, no string interpolation in `FastTick`, and no `math.sqrt`/`math.normalize`.
Rejected Alternatives: Broad bootstrap cleanup was rejected as unrelated churn in a heavily shared file.
Scalability potential: No runtime tier impact.
Hardware Impact: No measured impact; static scan only.

Problem: Final Git diff must identify the exact local work.
Solution: Current tracked diff is limited to `HectonVisorUberPost.shader`, `GameBootstrapper.cs`, `HectonVisorUberPostFeature.cs`, `InternalFloodWaterlineRuntime.cs`, `Rationale_INTERNAL_FLOOD_RENDERER.md`, and `Status_INTERNAL_FLOOD_RENDERER.md`. Local ignored `Hecton8.Core.csproj` was adjusted only to run diagnostics against stale Unity project metadata and is ignored by Git.
Rejected Alternatives: Editing unrelated dirty files was rejected.
Scalability potential: No tier impact.
Hardware Impact: No runtime impact.

## UPGRADE PASS 2026-05-13

Problem: `ClearWaterlineState()` could republish inactive shader globals every FastTick while the player was outside a flooded room or before runtime context existed.
Solution: Added dirty-checked global shader writes and an inactive-state early return. Runtime now writes `_InternalWaterlineY`, `_InternalWaterColor`, `_InternalWaterlineRuntime`, and `_InternalWaterlineDistortion` only when values materially change or a shader rebind needs a forced refresh.
Rejected Alternatives: Leaving repeated global uploads was rejected because invisible frames must cost near zero. Moving waterline data into per-renderer properties was rejected because this is a fullscreen material path and SRP batcher discipline still matters.
Scalability potential: Low = zero inactive upload churn; Middle/High/Ultra = saved CPU budget buys richer transient droplets without adding a pass.
Hardware Impact: Static estimate: 3-12us saved on inactive MX350 frames depending on driver/global-state cost. Measured proof absent.

Problem: Gas submerged-fraction pushes could be dropped when `GasDynamicsSolver.TrySetRoomSubmergedFraction()` returned false during a running gas job.
Solution: Cached `IHabitatGraphService`, `IGasDynamicsSolver`, and quality tier on a 30-tick cadence, then added one pending scalar retry slot for gas submerged fraction. The latest current-room fraction retries until accepted instead of silently disappearing.
Rejected Alternatives: Busy-waiting for the gas job was rejected as a main-thread stall. Allocating a managed queue was rejected as GC risk. Cross-domain direct gas arrays were rejected as ownership violation.
Scalability potential: Low/Middle/High/Ultra use the same scalar retry. Visual overkill remains shader-side, not gas-side.
Hardware Impact: Expected cost is one scalar field write and one interface call on pending frames; avoids correctness loss without measurable frame pressure.

Problem: Surfacing droplets were masked by `internalWaterMask`, so the effect appeared only below the waterline instead of wetting the visor for the requested 2 seconds.
Solution: Moved droplets to a full-visor procedural mask. Low tier gets additive droplet glints only. Middle/High/Ultra get a transient high-tier-only droplet refraction sample, still inside the existing Uber-Post pass. Droplets now persist after waterline state clears until their timer decays.
Rejected Alternatives: Particle decals, spawned wet-glass objects, and a second fullscreen droplet pass were rejected. Time-sliced random flicker was rejected because it looked like shader noise instead of visor water.
Scalability potential: Low = additive procedural droplets, no extra sample; Middle = sparse single-sample droplet refraction; High = denser mask response; Ultra = same path can raise density/strength through material tuning without scene objects.
Hardware Impact: Low tier adds no scene sample. High-tier transient sample costs only while droplets are visible; static estimate 20-80us for 2 seconds after surfacing depending on droplet coverage. Measured proof absent.

Problem: Blackbox telemetry did not reveal whether a frame was submerged, low-tier, or waiting on gas scalar acceptance.
Solution: Packed those facts into telemetry `Flags`: bit 1 = non-finite, bit 2 = camera submerged, bit 4 = pending gas push, bit 8 = low tier.
Rejected Alternatives: Text logs were rejected because hot-path strings allocate and lose frame history.
Scalability potential: Same 40-byte entry size, no memory growth.
Hardware Impact: No additional NativeArray size. A few byte operations per telemetry write.

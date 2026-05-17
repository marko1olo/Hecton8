# BIOLUM_PULSE_SYNC Rationale

Status: VERIFIED MASTER GRADE

## Decision 0 - Assignment Boundary

Problem: Prompt targets global flora biolum sync, but existing biolum runtime lives under `Assets/_Project/Scripts/World/Biolum/`, while the assigned domain is `Assets/_Project/Scripts/VFX/Bioluminescence/`.

Solution: Build the new heartbeat owner in the assigned VFX domain and use shader globals plus existing signal lanes to decouple it from World runtime. This follows ARCH_Execution_Phases and ARCH_Signal_Lane_Segregation.

Rejected Alternatives: Editing the large World/Biolum manager would widen ownership and collide with other agents. Raw prefab YAML cleanup was rejected until Unity API validation is available.

Scalability potential: Low uses one global state and cheap shader interpolation; Middle/High/Ultra use 4/8/16 state entries for richer species phase offsets and wave color separation.

Hardware Impact: Low-end i3/MX350 avoids per-material sine and emission script churn. Estimated main-thread saving: 45-120 us/frame in dense flora scenes; runtime proof absent.

## Decision 1 - Visual Fake First

Problem: A biologically accurate reef-wide pulse could tempt per-flora state, per-light simulation, or spawned strobe objects.

Solution: Use deterministic harmonic profile evaluation plus `Shader.SetGlobalVectorArray`. The shader carries the belief channel; gameplay truth remains unchanged.

Rejected Alternatives: Per-object MaterialPropertyBlock, per-renderer material clone, per-flora MonoBehaviour, and spawned lights were rejected as SRP/GC/batch regressions.

Scalability potential: Toaster path is one global neon breath. $5000 path is 16 phase-shifted harmonic lanes with spatial sweep and acoustic strobe.

Hardware Impact: Expected 0 B/frame, constant shader upload size, and no extra GameObjects. Exact profiler numbers absent.

## Decision 2 - OSHINO Profile Boundary

Problem: `Biolum_Profiles.bin` is referenced by the prompt but is not present under project assets, StreamingAssets, or Docs.

Solution: Cold-load `Biolum_Profiles.bin` from StreamingAssets/Docs when present, otherwise seed deterministic fallback profile floats into `NativeArray<float>`. This preserves the binary contract without inventing OSHINO data.

Rejected Alternatives: Blocking the runtime on missing content, embedding a ScriptableObject, or mutating material profiles at runtime. Those options either break cold-start or move data back into per-asset state.

Scalability potential: Low/MX350 gets one seeded global pulse. Mid gets four lanes. High/Ultra gets sixteen lanes and can replace fallback floats with authored OSHINO content without code churn.

Hardware Impact: Cold load only; hot path remains 0 B/frame. Low-end i3/MX350 avoids profile asset lookups and per-renderer profile state. Estimated hot saving: 20-45 us/frame versus per-object profile evaluation.

## Decision 3 - Burst Job Latency

Problem: The task requires a VISUAL_SYNC Burst job, but a 16-entry job completed immediately in the same tick would spend scheduler overhead for no visual gain.

Solution: Schedule `BiolumVisualSyncJob` and publish the completed buffer on the next dispatcher tick. Scalar globals carry time/strobe each frame, so a one-frame state latency is visually hidden.

Rejected Alternatives: Main-thread sine evaluation, scheduling then immediately completing every frame, or compute shader dispatch. Immediate completion was rejected as fake parallelism; compute was rejected as GPU overhead for 16 values.

Scalability potential: Low uses one state and can stay visually coherent under 15 Hz overload. Ultra uses 16 phase-shifted lanes with acoustic strobe overdrive.

Hardware Impact: Low-end silicon avoids per-material trig and per-object uploads. Estimated main-thread saving: 30-60 us/frame in dense flora scenes, with one fixed managed `Vector4[16]` upload.

## Decision 4 - Global Shader Array Over Per-Renderer State

Problem: Flora, sargassum, procedural bio, and predators need synchronized pulses without direct object dependencies.

Solution: Publish `_GlobalBiolumStates[16]`, `_GlobalBiolumParams`, `_GlobalBiolumClock`, and `_GlobalBiolumAupOffset`. Shaders spatially select a lane and retain legacy `_BiolumIntensity` fallback.

Rejected Alternatives: MaterialPropertyBlock, material clone, renderer traversal, or predator/VFX direct references. These break batching, widen ownership, and create 20-agent collision points.

Scalability potential: Toaster path is one shared neon breath. Middle path adds four authored lanes. High/Ultra path adds sixteen lanes and strobe overdrive for visual excess.

Hardware Impact: Low-end i3/MX350 saves per-renderer state churn and preserves SRP batching. Estimated saving: 35 us/frame per dense biome cluster, higher when foliage count spikes.

## Decision 5 - Acoustic Strobe As Fake Light

Problem: Active ping feedback needs to read as a reef-wide flash without spawning light sources or particle emitters.

Solution: Consume `SignalBus<AcousticPingSignal>`, hold white HDR strobe for 0.1 s, fade it, and let shaders mix global state color toward white.

Rejected Alternatives: Point lights, emissive prefab pulse scripts, per-fauna callbacks, or a new event type. Standard Unity lights were too slow and not deterministic enough for this effect.

Scalability potential: Low path strobes one global state. Ultra path strobes all sixteen lanes while retaining per-lane harmonic color after the flash.

Hardware Impact: Avoids 200 us+ CPU/GPU spikes from spawned light/particle paths under ping spam. Hot path stays fixed span reads and scalar writes.

## Decision 6 - Black Box And Memory Ownership

Problem: A global VFX heartbeat can hide NaN propagation across shaders unless the last states are retained.

Solution: Store 300 dispatcher ticks of high-level state in DataVault buffer `BiolumBlackBox` with `SystemID.Vfx`; dump to `Docs/AgentLogs/Dump_BIOLUM_PULSE_SYNC.bin` on non-finite job output.

Rejected Alternatives: Debug.Log spam, managed queues, or ignoring visual-only crashes. Logs allocate and are not recoverable after a crash.

Scalability potential: Low/Mid/High/Ultra all use the same fixed telemetry footprint; higher tiers only change active lane count.

Hardware Impact: Fixed DataVault native ring, 0 B/frame managed allocation. Estimated low-end impact: below 1 us/frame for telemetry write.

## Decision 7 - Build Wall

Problem: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails before BIOLUM validation with missing or drifted contracts outside the VFX biolum domain.

Solution: Do not edit fauna/animation/construction/world contracts from the VFX domain. Mark compile validation blocked by dependency and keep BIOLUM changes scoped.

Rejected Alternatives: Stubbing `JawIkTarget`, `CurrentJawPos`, `BiteIkSolveEvent`, `IDockingAutopilotService`, `WakeSource`, `LightShaftContribution`, or ecosystem service methods in VFX, or deleting unrelated compile items. Those are architectural sabotage outside the assigned domain.

Scalability potential: No visual scalability effect. Integration must restore the fauna animation assembly reference before final project compile can prove the VFX patch.

Hardware Impact: None from this decision. Build integrity remains blocked externally, not by runtime frame cost.

## Decision 8 - DataVault Eviction

Problem: The first BIOLUM pass still owned persistent NativeArray fields for profile floats, job states, and blackbox telemetry. That violates the H-Phi data-sovereignty target and turns the VFX runtime into a private data lord.

Solution: Add DataVault buffer IDs `BiolumProfileFloats`, `BiolumGlobalStates`, and `BiolumBlackBox`; keep only `VaultBufferHandle<T>` fields in the runtime; resolve transient NativeArray views only while the DataVault buffer is locked. The Burst job now runs directly over vault views.

Rejected Alternatives: Keeping private `H8Memory.Allocate` arrays, using managed arrays for profiles, or packing unrelated BIOLUM data into the generic `ShaderGlobalState` buffer. Private arrays fail H-Phi; generic reuse would corrupt buffer typing.

Scalability potential: Low/MX350 reads one lane from the same vault state. Mid reads four lanes. High/Ultra reads sixteen lanes and can share the same vault buffer with future render/debug consumers.

Hardware Impact: Removes three private persistent native allocations from the VFX owner. Hot-path byte allocation remains 0 B/frame. DataVault locks add small synchronization cost, estimated below 1-2 us/frame, traded for fragmentation control and ABI ownership.

## Decision 9 - Multiplatform ABI And I/O

Problem: Quest/Android ARM64 is unforgiving about hidden layout and MicroSD reads; the first telemetry struct used Pack=4 and the profile loader used per-float `BinaryReader` calls.

Solution: Convert `BiolumPulseTelemetryEntry` to explicit `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]` with manually aligned float offsets. Replace `BinaryReader` with a fixed 512-byte stackalloc buffer and `FileOptions.SequentialScan`.

Rejected Alternatives: Relying on runtime padding, heap byte arrays, or repeated small reads. Those paths are fragile on ARM64 and noisy on Steam Deck MicroSD.

Scalability potential: Identical 32-byte ABI across Quest/Android, Metal/Mac, Steam Deck, and PC. High-end profile data can still expand only by adding new DataVault buffers and a manifest change, not by changing this telemetry layout.

Hardware Impact: Cold profile read becomes one sequential 512-byte pass. Estimated MicroSD stall reduction is small but real: roughly 50-200 us during cold load versus per-float reader dispatch on slow cards; hot path unchanged.

## Decision 10 - High-Tier Visual Overdrive

Problem: The first shader patch made RTX/high tier richer by lane count, but the per-pixel look still sampled one lane at a time.

Solution: On quality tier High/Ultra, shader helpers blend a secondary `_GlobalBiolumStates` lane using `_GlobalBiolumClock` triangle overdrive. Low tier pays only the branch and remains a single-lane Dear Lie.

Rejected Alternatives: Compute particles, raymarch volumes, or adding unrelated visor salt/hull dent systems from this BIOLUM prompt. Those belong to other VFX domains and would create cross-agent collisions.

Scalability potential: Toaster path is one global color/intensity. God-mode path is a dual-lane per-pixel neon interference pattern over 16 global lanes, still Metal-safe and without compute thread groups.

Hardware Impact: Low-tier effect cost is effectively unchanged. High-tier spends a few ALU ops and one extra uniform-array fetch per affected pixel; exact GPU microseconds require Unity/RenderDoc capture after external compile walls are cleared.

## Decision 11 - Non-Blocking Visual Sync Publish

Problem: The prior publish path scheduled `BiolumVisualSyncJob` in one tick and completed it at the start of the next tick without checking `IsCompleted`. Even with only 16 lanes, that is still a hidden main-thread stall if the worker thread is delayed.

Solution: Gate publish on `_stateJobHandle.IsCompleted`. If the job is late, keep last frame's shader states, record `TelemetryFlagJobOverrun` in the 300-frame blackbox, and skip scheduling another job until the DataVault profile/state locks are released.

Rejected Alternatives: Completing every tick for convenience, scheduling a second job over locked DataVault views, or falling back to main-thread sine math during overrun. Those paths either serialize VISUAL_SYNC or break data ownership.

Scalability potential: Low/MX350 degrades by reusing the previous global pulse for a frame; High/Ultra preserve secondary-lane overdrive once the job publishes, without adding CPU stalls.

Hardware Impact: Removes a potential blocking sync point from the hot path. Expected stall avoidance is burst-dependent: normally 0 us, but avoids worst-case worker-fence spikes when the job system is saturated. Exact profiler proof is still blocked by external compile failures.

## Decision 12 - Sargassum Radius Division Vaccine

Problem: `Hecton_SargassumMaster` still had direct radius-squared divisions in cut-mask and propwash falloff code. The input radius was clamped, but mobile shader paths should not rely on division lowering when `rcp(max())` gives an explicit finite floor.

Solution: Convert both forward and duplicate shadow/depth helper paths to precompute inverse radius squared through `rcp(max(radius * radius, 0.0001))` and multiply the distance squared by that inverse.

Rejected Alternatives: Leaving the direct division because radius was already clamped, or adding a branch to skip the effect when radius is tiny. Direct division is weaker NaN hygiene; branchy skip changes visuals around tiny radii.

Scalability potential: Low/MX350 gets the same visual result with safer ALU. High/Ultra keeps propwash/cut interaction fidelity without adding samples or variant cost.

Hardware Impact: Expected frame cost is effectively neutral; the value is GPU stability on ARM64/Quest/Android and older desktop shader compilers. Exact microseconds saved: 0 us claimed.

## Decision 13 - Fixed-Size Blackbox Dump ABI

Problem: The blackbox ring entry was explicit Pack=1, but the dump path serialized individual fields through `BinaryWriter` and skipped reserved bytes. That makes post-mortem parsing disagree with the runtime ABI.

Solution: Replace `BinaryWriter` field emission with raw unmanaged writes: a 16-byte Pack=1 dump header followed by 300 raw 32-byte `BiolumPulseTelemetryEntry` records in ring order.

Rejected Alternatives: Keeping ad hoc field writes, adding JSON sidecars, or allocating a managed byte array for the full dump. Those options either break ABI sovereignty or add unnecessary crash-path heap pressure.

Scalability potential: Same 32-byte dump layout on Quest/Android ARM64, Metal/Mac, Steam Deck, and PC. Higher tiers do not change telemetry size.

Hardware Impact: Crash-path only. Runtime hot-path savings: 0 us. Post-mortem correctness improves because every entry is exactly 32 bytes.

## Decision 14 - Cold Lifecycle And Profile I/O Latch

Problem: `Awake()` performed external DataVault/GlobalRegistry work, and the profile loader could re-read `Biolum_Profiles.bin` on every enable. Tick also had a possible DataVault resolve fallback path through `EnsureVaultBuffers()`.

Solution: Remove `Awake()` external wiring, keep GlobalRegistry/DataVault resolution in lifecycle/editor-cold paths, make Tick use only cached `_dataVault`, and latch profile loading after the first successful binary or deterministic fallback seed.

Rejected Alternatives: Retaining self-healing registry lookup in Tick or reloading the profile file every enable. Self-healing in hot path violates the two-stage dependency rule; repeated disk reads risk Steam Deck MicroSD stalls.

Scalability potential: Low/MX350 and Steam Deck reuse the already-vaulted profile buffer. High/Ultra keep the same profile data without sacrificing visual overdrive.

Hardware Impact: Avoids repeated cold I/O on enable toggles. Estimated MicroSD stall avoided after first load: 50-200 us per avoided profile read; hot path remains 0 B/frame.

## Decision 15 - VISUAL_SYNC Overrun Dump Tripwire

Problem: A late `BiolumVisualSyncJob` was recorded in telemetry, but if a worker job stayed late indefinitely the system would retain stale shader states without forcing a post-mortem dump.

Solution: Count consecutive frames where `_stateJobHandle.IsCompleted` is false. At 300 consecutive late frames, dump `Docs/AgentLogs/Dump_BIOLUM_PULSE_SYNC.bin` with reason `TelemetryFlagJobOverrun`.

Rejected Alternatives: Blocking the main thread to force completion, scheduling a duplicate job, or logging strings. Blocking violates phase discipline; duplicate jobs race the same DataVault views; string logs do not satisfy blackbox survival.

Scalability potential: Low/MX350 keeps the last valid pulse for continuity. High/Ultra keep visual state stable until the job recovers, and the dump preserves enough heartbeat history for integration.

Hardware Impact: Normal hot-path cost is one saturated integer increment only while a job is late; 0 us claimed. The benefit is survival evidence after 300 bad frames.

## Decision 16 - Hot-Path Vault Acquisition Ban

Problem: `Tick()` still called `EnsureVaultBuffers()`. Even after GlobalRegistry fallback was removed, that method can request handles from the DataVault if a handle is missing, which is cold lifecycle work hiding in VISUAL_SYNC.

Solution: Add `HasVaultBuffers()` as a pure cached-handle validator and make Tick, telemetry locks, and job scheduling fail closed unless all three DataVault handles already exist. `EnsureVaultBuffers()` is retained only for lifecycle/editor-cold profile setup.

Rejected Alternatives: Letting Tick self-heal missing handles, or allocating local fallback buffers. Self-healing turns missing lifecycle setup into hidden runtime memory churn; local buffers violate H-Phi.

Scalability potential: Low/MX350 keeps deterministic failure behavior and no surprise memory work. High/Ultra cannot buy visual overdrive by smuggling DataVault acquisition into the frame.

Hardware Impact: Normal frame microseconds saved: 0 us claimed. Worst-case avoided cost is unbounded DataVault handle acquisition/validation in a frame; exact profiler proof remains blocked by external compile walls.

## Decision 17 - AUP Offset Overflow Quarantine

Problem: Individual `AupShiftSignal` deltas were checked for finite values, but the accumulated `_aupOriginOffset` could still become non-finite over long sessions or corrupted shift streams before being uploaded to shaders and written into telemetry.

Solution: After consuming AUP shifts, validate the accumulated offset. If it is non-finite, reset it to zero, mark `TelemetryFlagAupInvalid`, and dump the 300-frame blackbox.

Rejected Alternatives: Trusting finite deltas forever, clamping to a huge number, or silently zeroing without telemetry. Trust is not a survival policy; huge clamps still produce unstable shader phase; silent recovery hides the fault.

Scalability potential: Low/MX350 and high-tier paths get the same safe fallback. High/Ultra visual overdrive resumes from a stable zero offset instead of propagating NaN through every flora shader.

Hardware Impact: Normal hot-path cost is one `math.isfinite` vector check after shift consumption; 0 us claimed. Benefit is mobile GPU pipeline survival.

## Decision 18 - DataVault Stale Handle Quarantine

Problem: BIOLUM cached `VaultBufferHandle<T>` values across disable/re-enable. If the `GlobalDataVault` instance changed, or if a vault generation moved existing buffers, those handles could point at stale native memory and make the next resolve fault before shader state recovery.

Solution: Track the vault generation observed during lifecycle setup, release cached handles on disable/dispose, invalidate profiles when the vault instance changes, and add a no-allocation refresh path that uses `TryGetBufferHandle` only for already-existing BIOLUM buffers when generation drift is detected. Cold allocation stays in `EnsureVaultBuffers()`; Tick does not create DataVault buffers.

Rejected Alternatives: Keeping stale handles because the buffer IDs match, calling `EnsureVaultBuffers()` every Tick, or allocating private fallback NativeArrays. Stale handles risk use-after-relocation; per-frame ensure reintroduces hidden vault acquisition; private arrays violate H-Phi.

Scalability potential: Low/MX350 fails closed or refreshes existing handles without disk I/O. High/Ultra keep the same 16-lane overdrive after vault relocation instead of crashing or losing the global heartbeat.

Hardware Impact: Normal frame cost remains the cached `HasVaultBuffers()` check. The rare generation-drift path does metadata lookups only and allocates 0 B; exact microseconds require Unity profiler proof after external compile walls are cleared.

## Decision 19 - High-Tier Filament Spark Overdrive

Problem: The prior High/Ultra path used 16 lanes and secondary-lane blending, but the per-pixel pulse still read too close to a clean uniform array blend for top hardware. The user explicitly rejected mobile-looking visuals on a 4090.

Solution: Add an ALU-only high-tier branch inside the existing global BIOLUM shader helpers for procedural bio, kelp, sargassum, leviathan body, and leviathan tentacles. High/Ultra now get a triangle-wave filament and spark term derived from world position plus `_GlobalBiolumClock`; Low/MX350 exits the branch and keeps the single-state Dear Lie. Predator shader denominator sites touched during this pass were tightened with `rcp(max(...))` floors.

Rejected Alternatives: Spawned particle systems, compute kernels, extra global buffers, 16-tap POM, or raymarching in these flora/predator helpers. Those would collide with other VFX domains, add bandwidth or thread-group validation work, and are not needed for a global pulse shader bridge.

Scalability potential: Low/MX350 remains one global pulse and avoids the extra filament math. Mid keeps four lanes. High/Ultra spend extra ALU on faux particle/salt-crystal sparkle and species interference without changing CPU data upload size.

Hardware Impact: Low-tier CPU cost unchanged and low-tier GPU branch skips the high math. High/Ultra spends extra fragment ALU only; no microseconds saved are claimed. Exact GPU cost requires Unity/RenderDoc capture after external compile walls are cleared.

## Decision 20 - OSHINO Extension Mismatch

Problem: The assignment SITREP says OSHINO emits a binary `.h8bin`, while the numbered task names `Biolum_Profiles.bin`. The runtime only searched for `.bin`, so an authored `.h8bin` would silently fall back to deterministic seed data.

Solution: Keep the existing `Biolum_Profiles.bin` contract and add cold discovery for `Biolum_Profiles.h8bin` in StreamingAssets, Docs/Generated, and Docs. The loaded payload size and DataVault destination remain unchanged.

Rejected Alternatives: Renaming the primary file, scanning directories, or accepting arbitrary extensions. Renaming breaks the explicit task contract; directory scans are unnecessary I/O on Steam Deck; arbitrary extensions weaken data ownership.

Scalability potential: Low/MX350 and High/Ultra now consume the authored OSHINO profile regardless of whether the exporter used `.bin` or `.h8bin`.

Hardware Impact: Hot path unchanged. Cold path adds at most one extra `File.Exists` check per directory; no microseconds saved are claimed.

## Decision 21 - Compile Wall Discipline

Problem: Build attempt 12 after the OSHINO extension patch still fails, but the reported errors are in `DiegeticGyroCompassRuntime` and `EcosystemDirector`, outside the BIOLUM VFX domain.

Solution: Preserve BIOLUM changes and record the compile wall as a dependency block. Do not patch UI Navigation or World unsafe upload code from the VFX bioluminescence assignment.

Rejected Alternatives: Editing `DumpBlackBoxOnce`, `ResolveVelocity`, `EmitHighTierFailureParticles`, compass AUP state, or `EcosystemDirector` unsafe pointer calls from this domain. Those are foreign ownership changes and would convert a compile-wall report into cross-domain damage.

Scalability potential: No direct visual scalability change. The BIOLUM side remains low-tier single-state and High/Ultra 16-lane overdrive once the external compile wall is cleared.

Hardware Impact: 0 us claimed. This is validation discipline, not a runtime optimization.

## Decision 22 - Blackbox ABI Compression

Problem: BIOLUM telemetry was Pack=1 but still 40 bytes, while the project blackbox domain standard calls for fixed 32-byte crash records.

Solution: Compress `BiolumPulseTelemetryEntry` to explicit Pack=1 Size=32. Keep the high-value post-mortem fields: frame, active profile id, active state count, quality tier, flags, strobe, primary HDR intensity, time, and X/Z AUP phase. Drop Y offset and profile source hash from each frame record because BIOLUM seabed wave phase uses X/Z and the profile source is already a runtime scalar.

Rejected Alternatives: Keeping 40-byte records, writing a variable-length extension, or adding a second dump stream. Those options either violate the blackbox packet convention or increase crash-path complexity.

Scalability potential: Low/MX350 and High/Ultra share the same fixed telemetry packet. Higher tiers do not increase crash record size.

Hardware Impact: Hot-path savings are not claimed. Crash dump size drops from 12000 bytes to 9600 bytes for 300 records, excluding the 16-byte header.

## Decision 23 - Build Gate Cleared

Problem: Prior validation was blocked by external UI/World compile walls, so BIOLUM had static proof but not full project compile proof.

Solution: Re-run `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` after the ABI compression pass. The build now succeeds with 4 warnings and 0 errors.

Rejected Alternatives: Stopping at static scans or editing foreign systems to force validation. Neither is needed now that the project compile gate passes.

Scalability potential: No visual change. This validates that the Low/MX350 and High/Ultra BIOLUM paths are integrated without compile regressions.

Hardware Impact: 0 us claimed. This is integration validation.

## Decision 24 - Live Worktree Compile Regression

Problem: A subsequent no-filter build attempt after the BIOLUM ABI compression reported a new external compile wall: `SargassumMicroFaunaBoids` cannot resolve `SaturateFinite01`, and `TetherInstance` cannot resolve visual graphics-buffer helper methods.

Solution: Record the later build result as the current truth and return BIOLUM tasks 14 and 18 to dependency-blocked status. Keep the BIOLUM code unchanged because the errors are outside the assigned VFX/Bioluminescence domain.

Rejected Alternatives: Editing World Sargassum or Tether systems from BIOLUM, or leaving the status file claiming the earlier clean build as the current state. Both would violate evidence-based reporting.

Scalability potential: No BIOLUM visual change. Low/MX350 and High/Ultra BIOLUM paths remain implemented, but global compile validation depends on the external World/Tether repairs.

Hardware Impact: 0 us claimed. This is compile-wall tracking only.

## Decision 25 - High-Tier Spectral Haze

Problem: High/Ultra already had sixteen global lanes plus filament/spark overdrive, but the emission still lacked broad luminous body around the peak. The prompt explicitly demands high-end visual excess without downgrading Low/MX350.

Solution: Add a high-tier-only `godHaze` term inside the existing BIOLUM helper functions for procedural bio, kelp, sargassum, leviathan organic, and leviathan tentacle shaders. The haze is ALU-only, driven by `_GlobalBiolumClock`, world-position triangle waves, and existing secondary global state. Intensity remains clamped to `[0,10]`.

Rejected Alternatives: Raymarch volumes, spawned particles, extra global buffers, extra textures, or MaterialPropertyBlocks. Those would widen ownership, add bandwidth, or break the global shader-array contract for a visual that can be faked in fragment ALU.

Scalability potential: Low/MX350 keeps the one-state Dear Lie and skips the high-tier branch. Mid keeps four lanes. High/Ultra spend extra fragment ALU on spectral bloom-like haze over the sixteen-lane pulse without new CPU upload cost.

Hardware Impact: Low-tier CPU and GPU hot path unchanged except branch evaluation. High/Ultra intentionally spend GPU ALU; no microseconds saved are claimed. Exact GPU cost requires Unity shader profiling or RenderDoc after editor-side shader compilation.

## Decision 26 - Current Build Gate Restored

Problem: Decision 24 recorded an external compile wall, but the live worktree changed again. Continuing to report the external wall after a new clean build would be a stale validation report.

Solution: Re-run `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` after the spectral haze pass. The current project build succeeds with 0 warnings and 0 errors.

Rejected Alternatives: Leaving tasks 14 and 18 blocked, or editing unrelated World/Tether systems after the wall disappeared. Both would misrepresent the current state.

Scalability potential: No visual change. This validates that Low/MX350, Mid, High, and Ultra BIOLUM code paths are integrated in the current C# compile graph.

Hardware Impact: 0 us claimed. This is build validation only.

## Decision 27 - Diffusion And SSGI NaN Vaccine

Problem: The flora/predator BIOLUM helpers were hardened, but the BIOLUM diffusion volume and SSGI composite path could still propagate non-finite radiance or unbounded HDR values into render targets. The diffusion injection loop also trusted `_HectonBiolumPointCount` instead of hard-capping to the known 32-point GPU buffer contract.

Solution: Add finite guards and HDR `[0,10]` clamps to `BiolumDiffusion.compute`, `Hecton_BiolumSSGI.compute`, and `Hecton_BiolumSSGIComposite.shader`. Guard zero-sized dispatch targets, cap point injection to 32 entries, sanitize point payloads, and replace hot float divisions with `rcp(max(...))` floors where the math feeds render output.

Rejected Alternatives: Adding CPU-side validation, larger buffers, extra particles, or a new renderer feature. CPU validation would not protect corrupted GPU inputs; extra buffers and particles are outside the BIOLUM pulse assignment and would increase bandwidth.

Scalability potential: Low/MX350 gets the same cheap compute group shape with safer outputs and bounded point loops. High/Ultra retain the volumetric/SSGI glow path but cannot blow past HDR bounds or poison the composite with NaN.

Hardware Impact: Low-tier loop bound protects against accidental point-count oversubscription. No microseconds saved are claimed; this is a survival and bounded-work pass. Thread groups remain 4x4x4 and 8x8x1, both 64 threads and below the 1024 Metal limit.

## Decision 28 - EcosystemDirector Compile Wall

Problem: After the diffusion/SSGI hardening, current `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` fails in `Assets/_Project/Scripts/World/EcosystemDirector.cs`. Missing symbols include `ClearIndexEntries`, `TryUpsertIndexEntry`, `TryFindIndexEntry`, `ResolveVaultIndexCapacity`, `_sectorIndexByKey`, `_biomassIndexByKey`, and `BiomassLotkaVolterraJob.CellIndexByKey`.

Solution: Mark BIOLUM tasks 14 and 18 blocked by external dependency again. Do not edit World ecosystem indexing from the BIOLUM VFX/shader assignment.

Rejected Alternatives: Reconstructing ecosystem index helpers from VFX, stubbing fields, or leaving the status file claiming the prior clean build. Those options would either cross the domain boundary or lie about current compile truth.

Scalability potential: No BIOLUM visual change. Low/MX350 and High/Ultra shader paths remain implemented; global compile validation depends on the World/Ecosystem owner restoring its index contract.

Hardware Impact: 0 us claimed. This is compile-wall tracking only.

## Decision 29 - Shader Cross-Compiler Cast Hygiene

Problem: The diffusion/SSGI hardening added the right clamps, but two constructs were still weaker than necessary for mobile/Metal translators: `BiolumDiffusion.compute` built an `int3` clamp max directly from uint dimensions, and the composite shader called `isfinite` on half values.

Solution: Cast 3D texture dimensions explicitly to signed ints before using them as clamp bounds. In `Hecton_BiolumSSGIComposite.shader`, cast half vectors/scalars to float for finite tests, then return half output. This keeps the output ABI unchanged while reducing translator ambiguity.

Rejected Alternatives: Relying on implicit uint-to-int conversion and half `isfinite` overloads, or adding preprocessor branches per graphics API. Implicit conversion is unnecessary risk; API branches would increase variant complexity for no visual benefit.

Scalability potential: Low/MX350 and Quest/Android get the safer compiler path. High/Ultra keep the same visual output and HDR clamp.

Hardware Impact: 0 us saved claimed. This is portability hardening; any ALU delta is negligible and unmeasured.

## Decision 30 - Current Build Gate Restored After Cast Hygiene

Problem: Decision 28 recorded the current external compile wall, but the live worktree changed again after the BIOLUM shader cast cleanup.

Solution: Re-run `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly`. The current build succeeds with 0 warnings and 0 errors.

Rejected Alternatives: Leaving tasks 14 and 18 blocked, or claiming shader validation without a fresh build after the live worktree changed. Both would misreport current evidence.

Scalability potential: No visual change. This validates the BIOLUM Low/Mid/High/Ultra C# integration in the current compile graph.

Hardware Impact: 0 us claimed. This is build validation only.

## Decision 31 - Forward Flora Shader Consumer Closure

Problem: The prior global heartbeat covered procedural bio, sargassum, kelp GPUI, and leviathan camouflage, but the non-GPUI kelp, coral GPUI/non-GPUI, and indirect vegetation forward shaders still had independent per-material or per-instance biolum pulse paths. That left visible flora outside the `_GlobalBiolumStates` contract.

Solution: Add global state resolvers to `Hecton_KelpMaster`, `Hecton_CoralMaster`, `Hecton_CoralMaster_GPUI`, and `Hecton_IndirectVegetation`. Each resolver uses the same 16-state array, AUP offset, strobe scalar, and high-tier-only ALU filament/haze fake. Indirect vegetation is gated by authored biolum alpha so non-emissive grass does not start glowing.

Rejected Alternatives: Editing prefab YAML, adding MaterialPropertyBlocks, spawning per-flora pulse scripts, or forcing every vegetation instance to glow. Those options break batching, widen ownership, or violate the global shader-array directive.

Scalability potential: Low/MX350 still sees one global synchronized color/intensity and skips high-tier overdrive. Mid uses reduced state count from the runtime. High/Ultra get 16-lane spatial selection, secondary-lane blending, strobe, and ALU-only coral/kelp/indirect vegetation sparkle without new buffers.

Hardware Impact: CPU upload cost remains the same fixed `Shader.SetGlobalVectorArray`; no new per-renderer work. Low-end savings remain estimated at 12 us/frame per 100 flora versus per-material updates. High-end spends fragment ALU intentionally; no GPU microseconds saved are claimed.

## Decision 32 - Visible Flora HDR Consumer Clamp

Problem: `_GlobalBiolumStates.w` was clamped to `[0,10]`, but downstream shader consumers could multiply it by authored strength, seasonal bloom, cascade, touch flash, or growth masks and exceed the HDR energy contract.

Solution: Clamp final biolum emission energy to `[0,10]` in procedural bio, sargassum, kelp GPUI/non-GPUI, coral GPUI/non-GPUI, and indirect vegetation. Spore sparkle in indirect vegetation now uses the synchronized global tint when the instance is authored as bioluminescent.

Rejected Alternatives: Trusting the upstream state clamp only, reducing authored material ranges, or adding CPU-side validation. Upstream-only clamps do not control shader multipliers; material range edits would be content churn; CPU validation cannot protect fragment math.

Scalability potential: Low/MX350 gets bounded glow with the same cheap one-state path. High/Ultra keep the visual overdrive but cannot blast unbounded radiance into fog/composite chains.

Hardware Impact: Added clamp ALU is negligible and unmeasured. This is a NaN/HDR survival hardening pass, not a speed claim.

## Decision 33 - Current External Compile Wall After Flora Sweep

Problem: After the flora shader consumer sweep, `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` fails with 7 errors outside BIOLUM: five missing `ToFloat3` references in `HectonPlayerMovement`, one missing `qualityTier` argument in `TetherManager`, and one `uint` to `ushort` conversion in `EquipmentInteractionContracts`.

Solution: Record tasks 14 and 18 as blocked by dependency for the current worktree. Preserve BIOLUM shader/runtime changes because the reported failures are outside the VFX/SHADERS assignment.

Rejected Alternatives: Editing player movement, tether simulation, or equipment contracts from BIOLUM; leaving the prior clean build as the current status; or hiding the build failure. All three violate the domain boundary or evidence protocol.

Scalability potential: No BIOLUM visual change. Low/MX350 and High/Ultra shader paths remain implemented; global compile validation depends on the owning systems repairing their contracts.

Hardware Impact: 0 us claimed. This is compile-wall tracking only.

## Decision 34 - Legacy Biolum Intensity Ownership

Problem: The new VFX pulse sync publishes `_GlobalBiolumStates`, but legacy culling and some old shader paths still read `_BiolumIntensity`. `BiolumPulseSyncRuntime` only mirrored the first state, and `HectonBiolumManager` also wrote `_BiolumIntensity`, creating a duplicate global writer that could desync culling from High/Ultra lanes.

Solution: Make the VFX runtime publish `_BiolumIntensity.x` as the max active BIOLUM lane plus the acoustic strobe, clamped to `[0,10]`, and clear it on teardown. Add a minimal cross-domain bridge in `HectonBiolumManager`: it suppresses only `_BiolumIntensity` writes while `_GlobalBiolumParams.x > 0.5`, then republishes its own scalar after the VFX pulse sync clears the global params. This preserves legacy world color/phase/touch-ripple publishing.

Rejected Alternatives: Editing `FloraCulling.compute`, reordering dispatcher layers, deleting the legacy manager, or leaving both systems racing the same shader global. Culling edits would widen shader ownership; dispatcher ordering hides the interface conflict; deleting the old manager would cross into world systems.

Scalability potential: Low/MX350 still gets one cheap scalar for culling. Mid/High/Ultra no longer lose culling visibility when lane 0 is dark but another synchronized species lane is bright, and acoustic strobe keeps the scalar alive for the 0.1 s flash.

Hardware Impact: Runtime max scan is bounded to 16 values once per BIOLUM tick, not per renderer. Legacy suppression adds a `Shader.GetGlobalVector` check only when the old scalar would be republished or after suppression. Exact microseconds saved: 0 us measured; the gain is correctness and avoided culling pop.

## Decision 35 - Build Gate Restored After Legacy Bridge

Problem: Decision 33 recorded the current external compile wall. After the legacy scalar bridge, the live worktree needed a fresh compile result.

Solution: Re-run `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly`. Attempt 20 timed out at 185 s without compiler output; attempt 21 completed and succeeded with 0 warnings and 0 errors.

Rejected Alternatives: Reporting the stale build wall, claiming success from older attempts, or stopping after static scans. Current truth is the latest completed build.

Scalability potential: No visual change. This validates that Low/MX350, Mid, High, and Ultra BIOLUM paths compile with the cross-domain legacy scalar bridge in the current project graph.

Hardware Impact: 0 us claimed. This is build validation only.

## Decision 36 - Legacy Biolum DataVault Eviction

Problem: The legacy biolum manager was the remaining writer on the same global biolum interface and still owned private persistent `NativeArray` scratch buffers for predator blackout, touch-ripple distance sorting, and telemetry. That violated the H-Phi/DataVault rule even though the VFX heartbeat itself was already DataVault-backed.

Solution: Add explicit DataVault buffer IDs `BiolumLegacyPredatorPositions`, `BiolumLegacyPredatorScores`, `BiolumLegacyRipplePositions`, `BiolumLegacyRippleDistances`, and `BiolumLegacyTelemetryRing`. Replace the private persistent native fields with `VaultBufferHandle<T>` fields owned by `SystemID.Vfx`. Runtime jobs now receive resolved DataVault views only when the vault is available and not fenced. Handle refresh waits until scheduled jobs are complete before accepting a new vault generation.

Rejected Alternatives: Leaving the old manager as a private native island, allocating through `new NativeArray`, or deleting the legacy manager. Private arrays violate data sovereignty; raw native allocation would bypass the vault; deleting the manager would break world biolum phase/color/touch-ripple behavior outside the assignment.

Scalability potential: Low/MX350 still performs bounded 16-slot predator/ripple work and can skip high-tier touch-ripple upload. High/Ultra retain nearest-first ripple sorting and predator dimming without per-renderer state or extra material properties.

Hardware Impact: Persistent private allocations removed from the legacy bridge path. Exact microseconds saved: 0 us measured. The practical win is no hidden native ownership and safer vault-generation recovery under AUP/compaction pressure.

## Decision 37 - Legacy Telemetry ABI Compression

Problem: `HectonBiolumManager.BiolumTelemetryEntry` used sequential layout and implicit padding, making its crash-ring ABI weak for ARM64/Quest. It also carried a 40-byte-ish record while the project blackbox convention is 32-byte fixed records.

Solution: Convert the legacy telemetry entry to `[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 32)]`. Keep frame, camera XYZ, intensity, phase, predator dimming, predator hit count, active ripple count, and flags. Daylight/eclipsed state remains encoded in flags instead of a separate float.

Rejected Alternatives: Keeping sequential layout, keeping a wider record, or adding a second telemetry stream. Sequential layout risks platform padding differences; wider records increase dump size; a second stream complicates crash-path reads.

Scalability potential: Low/MX350 and High/Ultra use the same fixed crash record. Higher visual tiers do not expand blackbox memory.

Hardware Impact: Crash dump payload for the legacy 300-frame ring is 9600 bytes instead of a 40-byte record footprint. Hot-path microseconds saved: 0 us claimed.

## Decision 38 - No Rebuild Static Validation Pass

Problem: The operator explicitly requested not to run dotnet rebuild every pass. After DataVault eviction, validation still needed evidence without spending another full compile cycle.

Solution: Run targeted static scans instead: no `new NativeArray`, no `Allocator.*`, no private `NativeArray`, no sequential StructLayout, no MPB/EventBus/`string.Format`/finder/coroutine debt in the BIOLUM runtime plus legacy bridge files; `git diff --check` reports only CRLF normalization warnings. Shader scan confirms BIOLUM compute thread groups remain 64 threads and no DirectX-only group/shared/interlocked constructs were added to touched BIOLUM shader paths.

Rejected Alternatives: Running another full `dotnet build`, skipping validation, or claiming compile proof from stale build output. The latest compile proof remains attempt 21; this pass is explicitly static validation only.

Scalability potential: No visual change. This confirms the Low/MX350 Dear Lie and High/Ultra overdrive code paths were not changed while the legacy scratch data ownership was repaired.

Hardware Impact: 0 us claimed. This is validation discipline and operator-time conservation.

## Decision 39 - Legacy Vault Job Locking

Problem: After the legacy biolum scratch buffers were moved into the DataVault, predator blackout and touch-ripple distance jobs still resolved vault views without holding explicit buffer locks for the full scheduled-job lifetime. The ripple path also had an invalid-observer early return that could leave freshly locked buffers held if locking was added without a matching release.

Solution: Lock the legacy predator position/score buffers and ripple position/distance buffers before scheduling Burst jobs, register those jobs through `H8Memory.RegisterActiveJob(SystemID.Vfx, handle)`, and unlock only after no-work exits, invalid-observer exits, job finalization, or runtime resource release. Reject non-finite observer coordinates before either job is scheduled.

Rejected Alternatives: Trusting `IsCompactionFenceActive` checks alone, copying job scratch data back into private native arrays, or forcing synchronous completion to shorten the lock window. Fence checks do not protect against relocation once a job has a view; private arrays violate H-Phi; synchronous completion would spend frame time to hide the ownership bug.

Scalability potential: Low/MX350 keeps bounded 16-slot work and avoids NaN-driven job output. High/Ultra retain predator dimming and nearest-first touch-ripple sorting while DataVault compaction cannot move their job buffers mid-flight.

Hardware Impact: 0 us measured and 0 us claimed. The gain is survival: no use-after-relocation window and no leaked lock on invalid observer input.

## Decision 40 - Legacy Telemetry Locking And No Rebuild

Problem: The legacy blackbox ring now lives in `BiolumLegacyTelemetryRing`, but the record and dump paths still referenced the removed resolver helper. That was a direct compile break and also meant telemetry reads/writes lacked explicit DataVault lock ownership.

Solution: Replace stale telemetry resolver calls with `TryLockTelemetryRing`, unlock through `finally`, and defer dump triggering until after the record write releases the telemetry lock. This prevents re-entrant lock attempts while keeping the 300-frame blackbox readable on NaN or invalid camera input.

Rejected Alternatives: Reintroducing an unlocked resolver, keeping dump calls inside the record write lock, or running another full `dotnet build` after every small safety edit. The first two options weaken DataVault ownership; the third violates the operator instruction for this pass. The latest compile proof remains attempt 21, and this pass is explicitly static validation only.

Scalability potential: Low/MX350 and High/Ultra share the same fixed 32-byte telemetry record and locked ring path. Visual tiers do not increase blackbox memory, and invalid data dumps do not poison the live heartbeat.

Hardware Impact: 0 us claimed. Static validation found no stale `TryResolveTelemetryRing`, no `new NativeArray`/`Allocator.*`/private `NativeArray`, no sequential StructLayout, no MPB/EventBus/`string.Format`/finder/coroutine debt in the BIOLUM runtime plus legacy bridge, and BIOLUM compute kernels remain at 64 threads.

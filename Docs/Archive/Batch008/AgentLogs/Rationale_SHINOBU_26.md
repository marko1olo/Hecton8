# Rationale_SHINOBU_26
Date: 2026-05-18
Agent: SHINOBU_26
Domain: BIOLUMINESCENCE_SYNC_AND_PULSE
Status: SHINOBU_26 PENDING VERIFICATION AFTER ULTRA_POLISH_R3; FULL PROJECT COMPILE BLOCKED BY EXTERNAL DOMAIN ERRORS

## Decision 00: Mandate Scope
Problem: 50,000 coral glow instances cannot use Unity Light components, renderer material mutation, or per-instance managed color updates without blowing CPU, SetPass, and VRAM budgets.
Solution: Use deterministic visual fake first: packed unmanaged glow state, Burst math, 4-group global shader fallback, dirty/page-level GPU upload boundary, and Black Box telemetry.
Rejected Alternatives: Unity Point Lights, Material.SetColor, renderer.material clones, per-object GameObjects, direct sibling-domain dependencies, and per-instance managed arrays are too slow or too leaky for MX350.
Scalability potential: Low uses 4 global groups only. Middle uses grouped colors plus ambient suppression. High uses spatial pulses. Ultra uses individual packed color buffers plus visual overkill in shader emission response.
Hardware Impact: Estimated low-end i3/MX350 gain is avoiding 50,000 Light components and avoiding 50,000 managed color writes; exact profiler proof remains PENDING VERIFICATION.

## Decision 01: Archive Archaeology And Mock Seed
Problem: Legacy biolum binary data is scattered and may be absent or schema-incompatible with the current prompt.
Solution: Extend runtime scan paths to include `StreamingAssets`, `Data/Visuals`, `Docs/Generated`, `Docs`, plus legacy archive names; always seed deterministic `GenerateEmergencyMockGlows()` if usable live data is absent.
Rejected Alternatives: Loading managed species objects or rebuilding historic authoring data in runtime; both add GC and do not prove the packed hot path.
Scalability potential: Low uses emergency neon-blue/cyan mock groups. Middle reads existing binary profiles. High/Ultra can replace mock tuning with real generated plant data without changing the buffer contract.
Hardware Impact: Cold initialization only; hot-frame cost remains 0 us for archive probing.

## Decision 02: Light Component Refusal
Problem: 50,000 Unity `Light` components or material color changes will destroy CPU, SetPass, and VRAM budgets.
Solution: Keep glow authority in `NativeArray<uint>` plus one fixed `GraphicsBuffer`; the shader owns visible emission/bloom/SSGI fake.
Rejected Alternatives: Point Lights, `Material.SetColor`, renderer material cloning, and per-flora MonoBehaviours are too slow for i3/MX350.
Scalability potential: Low renders four global groups. Ultra can consume per-instance packed colors while still avoiding lights.
Hardware Impact: Estimated low-end gain is eliminating tens of thousands of component transforms and light culling records; exact profiler capture remains blocked by external compile errors.

## Decision 03: DTO Ref Mutation
Problem: Struct copies create CS1612-style mutation failures and hidden copyback cost when the oscillator updates phase/color.
Solution: `GlowStateDTO` is four raw fields and `GetGlowStateRef()` uses `UnsafeUtility.AsRef` over the DataVault native pointer.
Rejected Alternatives: Properties, wrapper collections, or managed arrays would either fail mutation or allocate/copy.
Scalability potential: Same memory contract holds from 4-group toaster fallback to 50,000-instance ultra path.
Hardware Impact: Removes per-element defensive copying in the future Burst loop; expected cumulative saving matters only at 50k scale.

## Decision 04: Pulse Alignment
Problem: Spatial wave triggers need AUP precision without mobile unaligned-access risk.
Solution: `SyncPulseDTO` uses sequential layout exactly `double3 OriginAUP` + `float WaveSpeed` + `uint ColorOverride`, validated as 32 bytes at startup.
Rejected Alternatives: Absolute `float3` origins or `Pack=1` are precision loss or alignment traps.
Scalability potential: Low ignores pulses. Middle/High read the same 32-byte pulse records. Ultra can use more active pulses without schema change.
Hardware Impact: Prevents circular wave distortion far from origin; one cold validation cost only.

## Decision 05: Blind Mock Contracts
Problem: Predator, weather, combat, and oxygen systems may not exist or compile during parallel development.
Solution: Define local unmanaged mock signals in the biolum domain and feed them through DataVault buffers instead of sibling-domain references.
Rejected Alternatives: Direct assembly references to AI/Weather/Combat increase rebuild fan-out and fail when those agents are incomplete.
Scalability potential: Low mocks drive deterministic visuals. High/Ultra can bridge real signals into the same buffers later.
Hardware Impact: Mock data is one cache-line-scale buffer; no hot allocation and no object graph traversal.

## Compile Gate 1
Problem: `dotnet build Hecton8.Core.csproj --no-restore` initially lacked `project.assets.json`; after restore, compile stopped on unrelated symbols `MockNarrativeTriggerSignal` and `ShinobuLogisticsRouter`.
Solution: Mark Gate 1 dependency-blocked and continue within BIOLUM domain; do not patch Environment or PowerGrid ownership without explicit integrator instruction.
Rejected Alternatives: Cross-domain stubbing those symbols from this task would hide another agent's broken contract and violate domain boundaries.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.

## Decision 06: 50k Burst Oscillator
Problem: Legacy runtime only computed global lanes, not the requested 50,000 packed instance colors.
Solution: Schedule `BiolumVisualSyncJob` across `MaxGlowInstances`, mutate `GlowStateDTO.Phase` in place, and write packed uint output into the fixed GPU color vault buffer.
Rejected Alternatives: `AnimationCurve`, `UnityEngine.Color`, per-instance MonoBehaviours, and material mutation.
Scalability potential: Low can ignore the per-instance buffer and use groups. Ultra consumes all packed instance colors.
Hardware Impact: Target is <0.1 ms math on mobile-class GPU-facing path; profiler proof blocked by external compile errors.

## Decision 07: Fixed Pulse Slots Instead Of Dynamic NativeList
Problem: Predator waves need bounded spatial propagation without allocations or unknown AI contracts.
Solution: Mock predator job writes one unmanaged signal; runtime converts it into fixed `SyncPulseDTO` slots plus age buffer. Burst reads at most 16 active pulses.
Rejected Alternatives: Runtime `NativeList` growth, direct Leviathan references, or GameObject events.
Scalability potential: Low disables pulse reads. Middle/High use 16 pulses. Ultra can raise slot count with a buffer contract change.
Hardware Impact: Fixed 16-pulse loop is predictable; no heap traffic.

## Decision 08: Dear Lie Four-Group Matrix
Problem: Uploading 50,000 color changes every frame is unnecessary on weak hardware.
Solution: Calculate four group states in the same Burst job and publish `_GlobalBiolumDearLieGroups` as a `float4x4`; shaders can choose by `SpeciesHash % 4`.
Rejected Alternatives: Point lights, per-material colors, or mandatory full instance buffer upload.
Scalability potential: Toaster uses only four rows. Ultra uses both matrix and per-instance buffer for overkill detail.
Hardware Impact: Four-row upload is effectively negligible compared to 50,000 uint upload.

## Decision 09: Ambient Suppression
Problem: Shallow/daylight scenes waste emission work when glow is visually hidden by ambient light.
Solution: Read `MockWeatherSignal.AmbientLightLevel` and multiply intensity by `saturate(1 - AmbientLight)` in Burst.
Rejected Alternatives: Global weather singleton lookup or scene light sampling would create cross-domain dependency and object traversal.
Scalability potential: All tiers use the same scalar. Low tier benefits most because bloom can fade out early.
Hardware Impact: One multiply per active instance; likely cheaper than emitting invisible bloom.

## Decision 10: Biome Palette Smoothstep
Problem: Hard palette swaps create visible pops across chunk/biome boundaries.
Solution: Track biome hash change in runtime and feed a 10-second smoothstep blend into Burst packed-color lerp.
Rejected Alternatives: Managed palette lists, string keys in hot path, and instant color replacement.
Scalability potential: Low can use group color blend. Ultra blends individual packed colors.
Hardware Impact: Additional bit ops per instance; can be skipped by Dear Lie fallback when health is poor.

## Compile Gate 2
Problem: Second build stopped on external symbols in somatic, ecosystem, ambient, and seismic domains before a clean project compile could be proven.
Solution: Record dependency block and keep changes isolated to BIOLUM/Core buffer IDs.
Rejected Alternatives: Adding foreign stubs in this agent would cross the domain boundary.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.

## Decision 11: Strict Toaster Fallback
Problem: Weak hardware cannot afford spatial waves plus 50,000 packed color uploads every frame.
Solution: `SystemHealthIndex01 > 0.85` schedules only four group rows and skips the GPU color buffer upload.
Rejected Alternatives: Balanced degradation that still walks 50,000 instances is not acceptable for toaster mode.
Scalability potential: Low uses pure Dear Lie. Middle resumes individual colors. High/Ultra add spatial pulses and damage response.
Hardware Impact: Expected low-end gain is removing the dominant per-instance loop and upload when system health is critical.

## Decision 12: Double-First AUP Math
Problem: Waves can originate far from world origin; absolute float conversion bends circles.
Solution: Subtract `double3` plant and pulse/damage origins first, cast the local delta to `float3`, then compute distance.
Rejected Alternatives: `Vector3` world positions and float absolute AUPs lose precision at 50 km scale.
Scalability potential: Same code path for all tiers that permit waves.
Hardware Impact: No extra hot cost; better visual correctness under large-world offsets.

## Decision 13: Damage Flicker As Math
Problem: Hit feedback needs a visible short-circuit without particles, sparks, or spawned objects.
Solution: `MockCombatDamageSignal` feeds radius/color/age to Burst; affected plants get chaotic multiplier and phase override for 2 seconds.
Rejected Alternatives: Particle systems, GameObjects, and combat-domain callbacks.
Scalability potential: Low can ignore damage by Dear Lie fallback. Ultra gets per-instance localized flicker.
Hardware Impact: Bounded branch and hash-like sine only during the 2-second damage window.

## Decision 14: O2 Heartbeat
Problem: Survival stress must read visually in the forest without depending on player systems.
Solution: `MockWeatherSignal.O2Level01` below 10% injects red tint, heartbeat intensity, and pulls frequency toward a heartbeat value.
Rejected Alternatives: Player health domain coupling or UI-only warning.
Scalability potential: Low can tint group matrix. Ultra tints individual corals.
Hardware Impact: One conditional and scalar sine under warning state.

## Decision 15: Packed Color Lerp
Problem: `UnityEngine.Color` would drag managed-friendly APIs into a hot Burst path.
Solution: RGB10_A2 lerp isolates bitfields, blends scalar channels, and repacks a `uint`.
Rejected Alternatives: `Color`, `Color32`, managed palettes, and material color writes.
Scalability potential: Shared by archive fallback, biome shift, damage tint, and editor writes.
Hardware Impact: Predictable integer/float scalar work; no GC.

## Compile Gate 3
Problem: Third build stopped on Construction-domain missing `PathWaypointDTO` and `MockSdfGrid`.
Solution: Record dependency block; filtered build output showed no `BiolumPulseSyncRuntime` errors.
Rejected Alternatives: Stubbing construction DTOs from BIOLUM would violate ownership.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.

## Decision 16: Fixed Buffer And MemCpy Range Init
Problem: Dynamic GPU color buffers and per-chunk allocations would spike CPU/VRAM during flora streaming.
Solution: Maintain 50,000-slot DataVault color buffers and expose `TryMemCpyInitializeGlowRange()` for unmanaged template copy into streamed ranges.
Rejected Alternatives: Allocating new `NativeArray<uint>` or managed arrays per chunk.
Scalability potential: Low may never touch per-instance ranges. Ultra streams detailed instance states into the same fixed allocation.
Hardware Impact: Avoids OS allocation and GC; range init is bounded raw memory copy.

## Decision 17: Black Box Recorder
Problem: A glow system without the last 300 frames of state cannot explain spikes, NaNs, or wave storms.
Solution: Keep a 300-entry 32-byte telemetry ring with active glow count, pulse count, and oscillator compute time; dump to `Docs/AgentLogs/Dump_BIOLUM_SYNC.bin` and mirror to `.h8dump` on overrun/nonfinite fault.
Rejected Alternatives: Console logs, managed lists, or profiler-only evidence.
Scalability potential: All tiers write the same tiny record.
Hardware Impact: 32 bytes per frame plus rare binary dump.

## Decision 18: Editor Facade
Problem: Binary packed glow data is unusable by humans without a controlled facade.
Solution: Add `Bioluminescence Tuner` EditorWindow that reads/writes species color/frequency and mock weather directly through DataVault during Play Mode.
Rejected Alternatives: Runtime menus and ScriptableObject mirrors that diverge from live unmanaged memory.
Scalability potential: Human controls can tune low-tier group colors and ultra per-species color sources.
Hardware Impact: Editor-only; no player hot-path cost.

## Decision 19: CSV Override Parser
Problem: Designers need quick profile edits without rebuilding binaries, but string CSV parsing allocates and main-thread file polling can stutter on slow storage.
Solution: Watch `biolum_profiles.csv`, read bytes on a background worker, copy ready bytes into DataVault scratch via `UnsafeUtility.MemCpy`, parse tokens manually, hash keys, and overwrite species tuning.
Rejected Alternatives: `File.ReadAllText`, `string.Split`, LINQ, or managed CSV packages.
Scalability potential: Low updates four-group source colors. Ultra updates per-species base colors.
Hardware Impact: No steady-state parse; on-change parser is allocation-free except unavoidable file-system API overhead.

## Decision 20: Live Pulse Trigger
Problem: Wave propagation needs human test control without waiting for real predator AI.
Solution: EditorWindow button writes `SyncPulseDTO` into fixed pulse slots using SceneView/Main camera position as mock AUP.
Rejected Alternatives: Leviathan dependency or scene-only debug GameObjects.
Scalability potential: Tests all tiers that permit pulse propagation.
Hardware Impact: Editor-only; runtime slot write is fixed-size.

## Compile Gate 4
Problem: Fourth build stopped on Fauna-domain missing `MockDamageSignal`; filtered output showed no SHINOBU_26 runtime/editor errors.
Solution: Record dependency block and proceed to Polish Mandate only after all SHINOBU_26 tasks are checked.
Rejected Alternatives: Creating a global `MockDamageSignal` from BIOLUM would collide with Fauna ownership.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.

## Decision 21: Polish Collision Fix
Problem: Anti-bloat self-read found the dirty shared `H8Memory.cs` already had peer agents using BufferID values 611-618, colliding with the initial biolum buffer range.
Solution: Move SHINOBU_26 buffers to 70300-70310 and update `VaultBufferContract.MaxBufferId` to the current shared enum high-water mark so `BiolumCsvScratch` and later peer ranges are covered.
Rejected Alternatives: Leaving duplicate enum values would silently alias vault buffers across domains; moving or reverting peer IDs would sabotage unrelated work.
Scalability potential: Biolum buffers now occupy an isolated high range and can expand without trampling ToolKinematics/SaveWorld ranges.
Hardware Impact: No frame-time impact; prevents catastrophic memory aliasing and corrupted glow data.

## Polish Mandate Result
Problem: `<POLISH_MANDATE>` tag was absent from `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Performed local anti-bloat audit: searched SHINOBU_26 files for `Light`, `Material.SetColor`, `renderer.material`, `ReadAllText`, `Split`, `List<`, `new NativeArray`; no forbidden hits found. Re-read modified code and corrected BufferID collision.
Rejected Alternatives: Skipping polish because tag is absent would leave collision risk undiscovered.
Scalability potential: Low/Middle/High/Ultra paths remain explicit: Dear Lie, individual packed colors, spatial pulses, and visual-overkill per-instance response.
Hardware Impact: No direct runtime cost; collision fix prevents cross-domain data corruption.

## Decision 22: Ultra Polish Predator Stall Removal
Problem: The first implementation created a separate `MockPredatorSignalJob` and immediately called `Schedule().Complete()` in the runtime loop. That protected determinism but inserted a main-thread synchronization point for a one-record mock signal.
Solution: Fold predator mock decay/fire into `BiolumVisualSyncJob` at `index == 0`, using the same DataVault `BiolumMockPredatorSignal` buffer already locked for the main visual batch. The next frame consumes the signal into fixed `SyncPulseDTO` slots.
Rejected Alternatives: Keeping the standalone job would preserve a needless sync fence; updating the mock signal from managed C# would put gameplay-ish state mutation back on the main thread.
Scalability potential: Low tier still gets Dear Lie groups plus signal decay. Ultra tier gets the same predator source feeding 50,000 per-instance wave response without adding a second job fence.
Hardware Impact: Expected gain on i3/MX350 is removal of one hot-frame job scheduling/complete pair; exact microseconds are not claimed because the project compile is still blocked externally.

## Decision 23: Ultra Polish CSV And GPU Upload Boundary
Problem: CSV override ingest used main-thread timestamp polling/file reads, and the GPU instance buffer had a single upload target. Both are acceptable for prototype code and unacceptable for a frame-time dictatorship.
Solution: Route `biolum_profiles.csv` through `FileSystemWatcher` plus a background worker byte buffer, then copy ready bytes into DataVault scratch on the main thread before allocation-free parsing. Replace the single instance `GraphicsBuffer` with front/back buffers so upload writes never target the buffer currently bound to shaders.
Rejected Alternatives: `File.ReadAllText`, `string.Split`, timestamp polling from `Tick()`, and single-buffer upload are slower or more stall-prone under Steam Deck MicroSD and driver synchronization.
Scalability potential: Low tier can ignore the full instance buffer and use 4 Dear Lie rows. Ultra tier can stream per-instance packed colors while the shader reads the previous front buffer.
Hardware Impact: Expected low-end gain is elimination of steady file-system calls in `Tick()` and reduction of GPU read/write contention; exact profiler numbers remain blocked by external compile errors.

## Decision 24: Runtime Struct Alignment And Dump Mirror
Problem: The telemetry and dump header structs used `Pack = 1`, which violates the ARM64 runtime memory rule even though their sizes were already multiples of 8. The newer audit also requires `.h8dump` crash artifacts.
Solution: Keep explicit byte offsets and explicit sizes, remove `Pack = 1`, preserve 32B telemetry and 16B header, and mirror fault dumps to both `Dump_BIOLUM_SYNC.bin` and `Dump_BIOLUM_SYNC.h8dump`.
Rejected Alternatives: Relying on packed structs risks unaligned runtime reads on Quest/ARM64; writing only text logs would fail the blackbox requirement.
Scalability potential: All tiers share the same 300-frame binary ring. Cheap devices pay one 32B write per frame; high-end devices get the same forensic trail while running richer visuals.
Hardware Impact: Alignment avoids ARM64 penalties; dump I/O occurs only on fatal/nonfinite/overrun state, not in the hot path.

## Compile Gate 5
Problem: Ultra-polish build still fails before project success, but the errors are outside BIOLUM: missing GlobalTelemetryBus blackbox helper methods/constants, missing SpatialAudio virtual voice queues, and broken Ecosystem spatial hash job contracts.
Solution: Record dependency block and keep SHINOBU_26 changes isolated to BIOLUM plus declared Core memory contracts. Static scan found no `Schedule().Complete`, `Pack = 1`, `PollCsvOverrides`, `File.ReadAllText`, `.Split(`, `List<`, or `new NativeArray` in SHINOBU_26 files.
Rejected Alternatives: Stubbing CoreTelemetry or Ecosystem symbols from BIOLUM would violate domain ownership and create a compile-wall coverup.
Scalability potential: No runtime impact from the gate; BIOLUM remains decoupled through DataVault buffers.
Hardware Impact: No runtime impact.

## Decision 25: Signal Corridor Mirror
Problem: BIOLUM had local mock weather and damage buffers as required by the blind original task, but the project already has global light, survival-vitals, and combat-damage signal lanes. Leaving BIOLUM completely isolated would fragment the nervous system once those producers are live.
Solution: Add a zero-dequeue mirror step that reads `GlobalSignals.TryGetLatestLightLevelSignal`, `TryGetLatestSurvivalDeathSignal`, and `TryGetLatestDamageSignal`, then writes sanitized scalar/color data into BIOLUM-owned DataVault mock buffers. The mock buffers remain the job-facing contract, so sibling domains still do not become compile dependencies.
Rejected Alternatives: Direct references to Weather/Combat/Fauna classes would increase rebuild fan-out; consuming global queues with dequeue would steal events from other systems.
Scalability potential: Low tier gets ambient/O2 group tint through Dear Lie. Ultra tier gets real combat pulses feeding per-instance damage flicker.
Hardware Impact: Only sequence checks and occasional vault writes when latest-signal sequence changes; no per-instance cost.

## Decision 26: Job Safety Attribute Removal And CSV Memory Barriers
Problem: The job used `NativeDisableParallelForRestriction` without the required three-paragraph exception, and the CSV worker exchanged byte counts/timestamps with the main thread using ordinary field reads/writes.
Solution: Remove all `NativeDisableParallelForRestriction` attributes because every write is current-index bounded or `index == 0` guarded. Add an invariant comment to the unsafe ref mutation. Use `Volatile.Read/Write` around CSV worker byte/tick handoff, subscribe watcher events before enabling, and keep the thread reference if shutdown join times out.
Rejected Alternatives: Keeping unsafe attributes with a paper justification would hide a review smell; naked cross-thread reads are weak on ARM64.
Scalability potential: Same math tiers, but cleaner safety proof and less platform-specific risk.
Hardware Impact: No measurable frame cost; prevents ARM64 race bugs and review rejection.

## Compile Gate 6
Problem: The next build reached a new external wall: `DroneFleetManager.DroneFleetBlackBoxEntry` lacks `Reserved0` where the Construction domain reads/writes it.
Solution: Record dependency block. Do not patch Construction from BIOLUM. Static scan remains clean for SHINOBU_26 forbidden patterns.
Rejected Alternatives: Adding a `Reserved0` field to another agent's blackbox from BIOLUM would violate domain ownership.
Scalability potential: No runtime impact from this gate.
Hardware Impact: No runtime impact.

## Decision 27: R3 Base Color Truth And CSV Retry
Problem: The biome palette path blended toward the active biome and then wrote the transient result back into `GlowStateDTO.PackedColor`. That made the visual transition destructive: a coral's base species color drifted every frame and could permanently lose the source color needed by editor/CSV tuning. The CSV apply path also returned to Idle when a DataVault lock was busy, which could silently discard a ready designer edit.
Solution: Treat `GlowStateDTO.PackedColor` as the base species color. `BiolumVisualSyncJob` now resolves the biome blend into a local `basePacked` only and writes only the final emission to `GpuColors`. Editor tuning and CSV overrides cold-propagate changed species color/frequency to matching live `GlowStateDTO` rows. CSV lock contention now restores `CsvWorkerReady` so the same byte block retries after the current job releases vault locks.
Rejected Alternatives: Looking up species tuning for every one of 50,000 instances each frame would add avoidable cache pressure. Leaving editor/CSV changes in the species table only would make the human facade lie in per-instance mode. Dropping a CSV update on lock contention would be nondeterministic tooling behavior.
Scalability potential: Low tier still relies on the 4-group Dear Lie. Middle/High/Ultra preserve stable base colors for per-instance waves, damage flicker, and biome blends while allowing designer edits to take effect without C# rebuilds.
Hardware Impact: Hot-path cost decreases by removing one destructive write to `GlowStateDTO.PackedColor` per active instance. Live glow propagation is cold/on-change only; no steady frame cost is added.

## Compile Gate 7
Problem: `dotnet build Hecton8.Core.csproj --no-restore` is still blocked outside BIOLUM. Current filtered errors are missing Ecosystem population DTOs in `BinaryLayoutManifest`, an ambient biota contract mismatch in `WorldChunkResidencyManager`, missing `SignalBus` in `TerminalOsRuntime`, and missing SHINOBU_37 physics-culling helpers in `GlobalPhysicsStateManager`.
Solution: Record dependency block and keep SHINOBU_26 isolated. Do not patch Core/Ecosystem/UI/Physics ownership from a BIOLUM polish pass. Static SHINOBU scan found no `glow.PackedColor = basePacked`, `NativeDisableParallelForRestriction`, `Pack = 1`, `File.ReadAllText`, `.Split(`, `List<`, `new NativeArray`, hot lambdas, `foreach`, or `.ToString(` patterns.
Rejected Alternatives: Stubbing ecosystem or physics DTOs from BIOLUM would hide another agent's broken contract and create a compile-wall coverup.
Scalability potential: No runtime impact from the gate; BIOLUM data remains in DataVault and decoupled through GlobalRegistry/GlobalSignals.
Hardware Impact: No runtime impact.

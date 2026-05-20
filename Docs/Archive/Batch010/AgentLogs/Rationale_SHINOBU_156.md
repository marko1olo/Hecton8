# Rationale_SHINOBU_156

Status: POLISH LOOP 19 STATIC INTEGRATED / GUARDED BUILD BLOCKED BY UNRELATED MISSING SOURCES

## Decision 000 - Shockwaves Are Math, Not Unity Physics Queries

Problem: Underwater explosions require pressure propagation across AUP space without allocating collider arrays or blocking the main thread.
Solution: Use Burst jobs over explicit unmanaged shockwave DTOs and route results as force packets. Visual shockwave state is a separate shader-data lane.
Rejected Alternatives: Physics.OverlapSphere, Physics.OverlapSphereNonAlloc, Rigidbody.AddExplosionForce, runtime particle prefab instantiation. Unity query path is main-thread-oriented and not deterministic enough for this task.
Scalability potential: Low uses coarse entity stride and critical masks; Middle evaluates more gameplay entities; High adds more pressure fidelity; Ultra spends saved CPU on richer water distortion buffers, not extra gameplay truth.
Hardware Impact: i3/MX350 saves collider broadphase churn and prefab hierarchy rebuild spikes; estimated target saving is 250-900 us during detonation bursts versus object-oriented overlap explosions, pending source and profiler proof.

## Decision 001 - DTO Layout Must Be Explicit 64 Bytes

Problem: Burst and netcode snapshotting need a stable cache-line-friendly shockwave state with no CS1612 property-copy traps.
Solution: Define ShockwaveEventDTO with LayoutKind.Explicit, Size 64, raw public fields, double3 AUP at offset 0, scalar fields at required offsets, and named padding.
Rejected Alternatives: Sequential layout or auto-properties. Sequential layout can drift under future edits; auto-properties create property access and value-copy hazards.
Scalability potential: Low through Ultra share the same truth layout; higher tiers can add visual-only DTOs without bloating the authoritative 64-byte shockwave struct.
Hardware Impact: i3/MX350 benefits from linear 64-byte reads and direct MemCpy snapshot compatibility; estimated gain is 15-40 us per 256 active shockwave slots versus defensive property access/copy patterns.

## Decision 002 - SDF Dampening Is A Midpoint Sample, Not Ray Physics

Problem: Rock and seabed should muffle shockwaves, but raycasts would violate the task and add main-thread query cost.
Solution: Sample voxel SDF midpoint between epicenter and entity AUP through an interface discovered from existing code. Negative SDF applies continuous dampening.
Rejected Alternatives: Physics.Raycast/RaycastCommand for every candidate, collider-based occlusion, or binary blocked/unblocked switches.
Scalability potential: Low samples one midpoint for critical entities; Middle samples one midpoint for more entity classes; High can sample 2-3 points via quality weight; Ultra can add visual-only secondary ripples.
Hardware Impact: i3/MX350 avoids per-target physics scene queries; estimated saving is 80-350 us per large shockwave candidate set, pending source proof.

## Decision 003 - Cavitation Visuals Are Shader Buffer Data

Problem: Particles and fireballs are both physically wrong underwater and runtime allocation risks.
Solution: Feed active cavitation spheres to a global structured buffer consumed by water/post shaders. No particle instantiation in detonation path.
Rejected Alternatives: ParticleSystem.Instantiate, material clones, per-renderer MaterialPropertyBlock on standard geometry.
Scalability potential: Low emits few buffer entries and cheap refraction; Middle adds ripple collapse intensity; High increases sample quality; Ultra adds visual overkill in shader only.
Hardware Impact: i3/MX350 removes hierarchy rebuild and particle CPU simulation; estimated saving is 200-700 us at burst onset, while preserving visible impact through water distortion.

## Decision 004 - Continuous Quality Weight Controls Work, Not Tiers

Problem: The task forbids binary quality switches while requiring thermal load shedding.
Solution: Convert GlobalQualityWeight into a continuous stride and priority threshold. Critical entities always pass; debris/microfauna are probabilistically or stride-filtered by weight.
Rejected Alternatives: if lowEnd/else highEnd branches, hard LOD toggles, or cutting all non-critical responses at one threshold.
Scalability potential: Low, Middle, High, Ultra are points on the same curve, with hysteresis where persistent state is required.
Hardware Impact: i3/MX350 target can drop 60-85 percent of non-critical evaluations during stress; estimated saving is 300-1200 us for thousands of candidates, pending static integration.

## Decision 005 - Physics Bus Bridge Is Owner-Local, Not A Sibling Dependency

Problem: The prompt names a NativeQueue owned by PhysicsApplySystem, while the first route used a caller-owned Rigidbody-slot facade through PhysicsForceRouter.
Solution: Emit SHINOBU_156-owned unmanaged ShockwaveForcePacketDTO rows in the Vault, then drain those rows through a SHINOBU partial of PhysicsApplySystem. The primary drain resolves TargetEntityHash through GlobalPhysicsStateManager and queues deferred point-force packets via PhysicsApplySystem.QueueForceAtPosition. The Burst solver never touches Rigidbody.
Rejected Alternatives: exposing PhysicsApplySystem private queues, adding a direct sibling asmdef reference, keeping PhysicsForceRouter as the primary SHINOBU bridge, or calling Rigidbody.AddForce/AddExplosionForce in the solver.
Scalability potential: Low flushes only high-priority packets; Middle flushes vehicle/fauna/debris packets; High and Ultra raise the packet budget while preserving the same owner-local route.
Hardware Impact: i3/MX350 avoids main-thread overlap queries and direct force loops during pressure evaluation; estimated saving is 180-650 us per detonation burst versus collider enumeration, pending profiler proof.

## Decision 006 - Vault Handles Are The Only Native Memory Ownership

Problem: Persistent NativeArrays inside a MonoBehaviour would fragment allocator ownership and make rollback snapshots ambiguous.
Solution: Request every gameplay/telemetry/tuning buffer from GlobalDataVault during EnsureInitialized using owner-local BufferID values 71560 through 71568. Runtime classes store VaultBufferHandle values and GPU GraphicsBuffer objects only; no private NativeArray, NativeList, NativeHashMap, or NativeQueue owns authoritative state.
Rejected Alternatives: Allocator.Persistent fields, static NativeArray caches, or per-scene local collections.
Scalability potential: Low and Middle shrink effective counts through counters/quality while retaining allocated capacity; High and Ultra consume more of the same capacity without reallocating.
Hardware Impact: i3/MX350 avoids runtime collection creation and allocator churn; estimated saving is 40-160 us during scene boot/detonation spikes, pending profiler proof.

## Decision 007 - Cold CSV And Editor Facade Keep Designers Out Of C# Recompile

Problem: Pressure/radius/visual tuning must be adjustable without code edits, but string.Split and managed parsing are unacceptable in runtime paths.
Solution: Parse ordnance_specs.csv cold through ReadOnlySpan<byte> with FNV-1a lowercase hashing into 64-byte OrdnanceProfileDTO rows. UI Toolkit tuner mutates the Vault-backed tuning DTO directly and can inject deterministic mock detonations.
Rejected Alternatives: ScriptableObject-only tuning, LINQ/string.Split CSV parsing, or hard-coded explosive constants.
Scalability potential: Low profiles cap radius/pressure and visual count; Middle profiles can tune gameplay parity; High and Ultra can raise visual intensity while keeping authoritative math bounded.
Hardware Impact: i3/MX350 avoids domain recompiles and runtime parser allocations; estimated saving is workflow/boot-path only, no per-frame claim.

## Decision 008 - Shader Distortion Is The Visual Truth

Problem: Simulating underwater fireballs, bubble meshes, or particle swarms would waste CPU and create false visual language for cavitation.
Solution: Write CavitationVisualSphereDTO records to a global StructuredBuffer and distort UberNoir water refraction in HLSL using shell falloff plus curl-wave tangential offset. The CPU carries only spheres; the GPU performs the visual lie.
Rejected Alternatives: ParticleSystem instantiation, procedural mesh bubbles, or CPU Navier-Stokes/cavitation particle simulation.
Scalability potential: Low uploads 2 active visual spheres and cheap offsets; Middle uploads more spheres; High uses broader shader shell detail; Ultra spends saved CPU on denser shader distortion, not more gameplay pressure truth.
Hardware Impact: i3/MX350 avoids particle hierarchy rebuild and CPU particle simulation; estimated saving is 200-700 us at burst onset, with GPU cost proportional to shader active count.

## Decision 009 - Black Box Dumps Are Fault-Triggered, Not Chat Claims

Problem: NaN pressure, nonfinite AUP deltas, or saturated force vectors must leave forensic evidence after endurance failures.
Solution: Store 300 ShockwaveTelemetryEntry records in Vault and dump them to Docs/AgentLogs/Dump_SHINOBU_156.bin when NonFiniteRecovered or ForceSaturated flags surface through telemetry.
Rejected Alternatives: Debug.Log-only errors, managed per-frame string logs, or leaving telemetry outside the rollback/Vault lane.
Scalability potential: Low through Ultra share the same 300-frame ring; only the amount of evaluated packet evidence changes with quality.
Hardware Impact: i3/MX350 pays a fixed 300-entry memory footprint and a small frame recorder write; estimated runtime cost is below 10 us for normal frames, pending profiler proof.

## Decision 010 - Compile Wall Is External To SHINOBU_156

Problem: Guarded `dotnet build Hecton8.Core.csproj` failed before compiling SHINOBU_156 because the project still references deleted files `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.
Solution: Do not restore or remove unrelated files. Record the dependency break, keep SHINOBU_156 static scans and csproj inclusion intact, and leave the missing-source repair to the owning domains/integrator.
Rejected Alternatives: Reverting user/other-agent deletes, deleting unrelated Compile Include entries, or faking compile success with a partial report.
Scalability potential: No runtime scalability effect; preserves compile-wall ownership discipline.
Hardware Impact: Prevents additional wasted compile attempts on an already-known external source-discovery failure; estimated developer time saved is one or more 3-5 second failed build cycles in this local state.

## Decision 011 - SDF Dampening Must Not Import World Runtime

Problem: The first SDF route called `GlobalWorldSampler` from `Hecton8.World`, creating a sibling runtime dependency inside the cavitation runtime and violating the compile-wall mandate.
Solution: Replace direct world-sampler usage with a SHINOBU_156-owned Vault SDF snapshot: `71569` stores a 64-byte `AbyssalCavitationSdfVolumeDTO`, `71570` stores signed-distance bytes. The Burst pressure job samples the midpoint from those buffers when active, otherwise it falls back to the deterministic analytic seabed/pillar mock.
Rejected Alternatives: Keeping `using Hecton8.World`, calling a world SDF service from Burst, `Physics.Raycast`, MeshCollider tests, adding a new cross-domain asmdef reference, or creating a new cross-domain lock surface for SDF writes. SDF ingestion refuses mutation while cavitation jobs are scheduled.
Scalability potential: Low uses one nearest signed-distance byte per candidate; Middle blends into trilinear at the quality curve threshold; High and Ultra use the same authoritative pressure route while spending saved CPU on shader cavitation detail.
Hardware Impact: Low-end silicon skips seven extra SDF byte reads per low-quality candidate and avoids any Unity physics scene query. Estimate remains 80-350 us saved on large candidate sets versus ray/overlap occlusion, pending profiler proof.

## Decision 012 - Ordnance Profiles Are Fixed Open-Address Vault Rows

Problem: The CSV parser originally appended ordnance profiles densely, making profile lookup a small O(N) scan and only partially satisfying the XML's NativeHashMap intent.
Solution: Keep DataVault ownership as `NativeArray<OrdnanceProfileDTO>[32]`, but use it as a fixed open-address hash table keyed by FNV-1a profile hash. CSV hydration clears the table cold, probes deterministically, and updates duplicate hashes in place.
Rejected Alternatives: Owning a private `NativeHashMap`, adding a persistent NativeHashMap to the runtime class, using managed Dictionary/string keys, or keeping a linear scan on detonation.
Scalability potential: Low through Ultra share the same table; higher tiers add profile richness through CSV columns without adding gameplay allocations.
Hardware Impact: i3/MX350 avoids managed hash ownership and keeps lookup bounded to a few cache-line probes; estimated saving is 20-80 ns per detonation profile lookup on the current 32-slot table, pending profiler proof.

## Decision 013 - Padding Proof Must Be Executable

Problem: The SDF descriptor layout was documented as 64 bytes with `_pad0` at byte 60, but the runtime guard only checked the semantic fields through `Flags`.
Solution: Extend `AbyssalCavitationLayout.Validate()` to verify `_pad0` offset 60 so editor/CI layout validation catches accidental padding drift before Burst jobs read the descriptor.
Rejected Alternatives: Relying on the `[StructLayout(Size=64)]` declaration alone or leaving padding proof only in the log.
Scalability potential: No runtime scalability change; Low through Ultra use the same descriptor layout and the guard runs cold.
Hardware Impact: Prevents ARM64 alignment regressions from reaching runtime; no per-frame cost.

## Decision 014 - Cavitation Force Drain Belongs In PhysicsApplySystem

Problem: A caller-owned `Rigidbody[]` bridge made integration depend on scene-side slot discipline and left old documentation claiming `PhysicsForceRouter` as the SHINOBU force route.
Solution: Add `PhysicsApplySystem.DrainCavitationForcePackets` as a partial class in the SHINOBU source file. It consumes Vault force packet rows, resolves folded entity hashes through `GlobalPhysicsStateManager`, clamps force magnitudes, converts AUP application points relative to the caller origin, and enqueues deferred `ForceMode.Impulse` point-force packets into PhysicsApplySystem.
Rejected Alternatives: Direct `Rigidbody.AddForce`, `PhysicsForceRouter` as primary route, public exposure of PhysicsApplySystem internals, or a new unmanaged queue not owned by the existing physics service.
Scalability potential: Low drains a capped packet budget; Middle/High/Ultra raise `maxPackets` without changing the solver truth route.
Hardware Impact: No measured runtime saving claimed. The value is architectural: the main-thread bridge is now the existing deferred force owner, while Burst remains zero-GC and Rigidbody-free.

## Decision 015 - Pressure Falloff Must Be Literal Inverse-Square

Problem: The pressure evaluator used a quadratic normalized-radius falloff while the XML requires `Pressure = PeakPressure * (1 / max(1, distanceSq))`.
Solution: Replace radius-normalized falloff with `math.rcp(math.max(1f, distanceSq))`, preserving the expanding shell gate and SDF dampening as multipliers.
Rejected Alternatives: Keeping the smoother gameplay falloff, adding a binary near/far branch, or hiding the mismatch behind tuning values.
Scalability potential: Low through Ultra use the same pressure truth; quality controls candidate acceptance and SDF tap count, not the law of pressure attenuation.
Hardware Impact: Neutral to slightly cheaper ALU than the previous radius-normalized curve; no measured runtime saving claimed.

## Decision 016 - Mock RNG Must Include Frame Identity

Problem: Mock shockwave RNG used `SectorHash ^ FrameIndex`, while mock entity RNG used sector-only seed material.
Solution: Include `FrameIndex` in the entity mock RNG seed as well, so all deterministic fallback random streams are tied to sector and simulation frame identity.
Rejected Alternatives: Keeping sector-only entity placement or using UnityEngine.Random.
Scalability potential: No scalability change; the mock path remains cold CI/editor proof.
Hardware Impact: No runtime impact in live gameplay.

## Decision 017 - Vault Mutations Must Respect Scheduled Readers

Problem: SDF writes were fenced while jobs were scheduled, but entity snapshot writes and tuning writes could still mutate Vault rows during an active cavitation job chain.
Solution: Reject `TryWriteEntitySnapshot`, `TryClearEntitySnapshots`, and `TryApplyTuning` while `_jobScheduled` is true. Producers must write the next candidate window before scheduling or after the job chain is complete.
Rejected Alternatives: Locking across domains, blocking the writer with `Complete()`, or allowing undefined timing where pressure jobs read partially-updated candidate rows.
Scalability potential: No quality-path change; it protects all tiers from data races.
Hardware Impact: Prevents cache/data hazards without adding hot-path locks or main-thread waits.

## Decision 018 - Debug And Shader Reads Must Not Race Jobs

Problem: `SyncShaderVisuals` called a non-blocking completion check but still read visual rows when the job handle was incomplete. Telemetry and gizmo read paths also had no scheduled-reader fence.
Solution: `SyncShaderVisuals` now returns the last uploaded count if the job is still running, while telemetry sampling, blackbox dump, and gizmo reads reject active scheduled work. Shutdown/explicit paths can still force completion through `CompleteScheduledIfReady(true)`.
Rejected Alternatives: Blocking visual sync with `Complete()` or allowing editor/debug readers to race the same Vault rows written by jobs.
Scalability potential: Low through Ultra keep the same behavior; weak hardware may display one stale shader buffer for a frame instead of stalling.
Hardware Impact: Avoids main-thread blocking and data races; no measured runtime saving claimed.

## Decision 019 - Black Box Dump Path Must Use Agent Identity

Problem: The original XML named `Dump_CAVITATION_SURGEON.bin`, while AGENTS requires crash artifacts to use `Dump_[YourID].bin`.
Solution: Route the SHINOBU_156 black-box dump to `Docs/AgentLogs/Dump_SHINOBU_156.bin`. This preserves the same 300-frame telemetry payload and changes only the forensic artifact name.
Rejected Alternatives: Keeping only the older alias, writing two dump files on every fault, or adding a managed filename switch in the hot solver.
Scalability potential: No gameplay scalability change; Low through Ultra share the same crash artifact route.
Hardware Impact: Fault-path file name correction only; no normal-frame cost.

## Decision 020 - Shader Visual Sync Needs A Dirty Frame Key

Problem: `SyncShaderVisuals` recorded `_lastUploadedVisualCount` only when `uploadCount > 0`, so a zero-wave frame could leave stale state for a later non-blocking visual call. The same method also re-uploaded the same cavitation sphere buffer when called repeatedly in the same simulation frame.
Solution: Cache the last uploaded/bound visual buffer by `_frameIndex`, `uploadCount`, `GlobalQualityWeight`, and `VisualIntensityScale`. Zero-wave frames now bind the empty buffer and record count `0`. Duplicate same-frame calls reuse the last GraphicsBuffer instead of locking and memcpying the same NativeArray window.
Rejected Alternatives: Forcing `Complete()` in visual sync, clearing the GPU buffer with a fake element, uploading every call, or adding a new visual truth DTO. The shader count already makes the empty buffer authoritative.
Scalability potential: Low keeps one empty/low-count buffer bound without extra uploads; Middle/High/Ultra still upload richer visual sphere sets once per changed simulation frame, then spend saved bandwidth on shader-side refraction detail.
Hardware Impact: MX350/i3 avoids redundant `LockBufferForWrite` and memcpy work on duplicate VISUAL_SYNC/editor calls. Measured microseconds are pending profiler proof; expected saving is one avoided graphics-buffer lock plus up to `MaxVisualSpheres * sizeof(CavitationVisualSphereDTO)` bytes of duplicate CPU->GPU staging per duplicate call.

## Decision 021 - Fault Logging Must Not Format Strings In Release

Problem: `TryDumpBlackBox` caught exceptions and always concatenated `exception.Message` into `Debug.LogError`. This is fault-path only, but release builds should not allocate a diagnostic string after a dump failure.
Solution: Wrap the dump failure log in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and log a constant diagnostic only. The method still returns `false` in all builds.
Rejected Alternatives: Removing the catch and risking fault escalation, throwing in gameplay, or logging the exception string in player release builds.
Scalability potential: No quality-path change; Low through Ultra share the same dump behavior.
Hardware Impact: Release fault path avoids one managed diagnostic string allocation. Normal-frame impact is 0 us.

## Decision 022 - Editor Readout Uses Fixed Buffer, Not Numeric ToString Chains

Problem: The tuner facade refreshed telemetry through multiple numeric `.ToString()` calls plus string concatenation. It is editor-only, but the XML explicitly demanded a zero-GC-style readout discipline.
Solution: Cache the last telemetry values, format active count, candidate count, fixed-one-decimal floats, and flags into a fixed `char[192]`, and update the UI label only when telemetry changes. UI Toolkit `Label.text` still requires one managed `string`; this is an editor presentation boundary, not gameplay telemetry.
Rejected Alternatives: Leaving `.ToString()` chains, switching to IMGUI `OnGUI`, unsafe mutation of immutable strings, or claiming UI Toolkit label assignment is truly zero-GC. Runtime sampling remains unmanaged through the Vault ring.
Scalability potential: Low/editor stress avoids wasteful repeated formatting when telemetry is unchanged; High/Ultra editor inspection can watch richer solver output without touching runtime Burst math.
Hardware Impact: Gameplay impact is 0 us. Editor refresh avoids three float `ToString()` allocations, one hex `ToString()` allocation, and the concat chain per unchanged telemetry refresh; one UI Toolkit string remains when values actually change.

## Decision 023 - New Unity Assets Need Stable GUIDs

Problem: The new SHINOBU_156 C# files and ordnance CSV were present without `.meta` files. Unity would mint local GUIDs during import, creating workstation-specific identity drift.
Solution: Add stable `.meta` files for `Physics/Cavitation`, `AbyssalCavitationContracts.cs`, `AbyssalCavitationRuntime.cs`, `AbyssalCavitationTunerWindow.cs`, `Data/Combat`, and `ordnance_specs.csv`, then scan for the chosen GUID range. The CSV meta uses the same `TextScriptImporter` stanza as existing project CSV assets.
Rejected Alternatives: Allowing Unity to generate metas later, editing unrelated asmdefs to compensate, or relying on chat notes for asset identity.
Scalability potential: No runtime scalability change; this protects import determinism and editor collaboration.
Hardware Impact: 0 us runtime. Prevents import churn and broken references on other developer machines.

## Decision 024 - Runtime Layout Validation Must Be Cold Once

Problem: `EnsureInitialized()` called the reflection-backed `AbyssalCavitationLayout.ValidateOrThrow()` before checking whether SHINOBU_156 was already initialized against the current Vault generation. Any runtime facade repeatedly calling `EnsureInitialized()` could therefore pay layout reflection work after boot.
Solution: Add `_layoutValidated` and `ValidateLayoutColdOnce()`. `EnsureInitialized()` now resolves the Vault, fails closed if no Vault exists, returns immediately for the current initialized Vault generation, and only then performs one executable ARM64 layout validation before first handle hydration.
Rejected Alternatives: Removing runtime validation entirely, moving validation to docs only, or caching reflection output in a managed dictionary. The first two weaken the layout proof; the third adds a worse managed cache surface.
Scalability potential: Low through Ultra share the same authoritative DTO layout. Weak devices avoid repeated cold-path reflection in runtime accessors; high-end machines preserve the same layout proof without bloating gameplay truth.
Hardware Impact: Measured microseconds are pending profiler proof. Static impact is removal of repeated `System.Reflection` field-offset validation from initialized calls to `EnsureInitialized()`; initial boot validation remains.

## Decision 025 - Player Builds Must Not Carry Reflection Layout Probes

Problem: Even cold-once validation left `System.Reflection` field lookup compiled into the runtime assembly and callable from SHINOBU initialization in player builds.
Solution: Compile the reflection import, `FieldInfo` helper, field-offset validation body, and `_layoutValidated` state only under `UNITY_EDITOR || DEVELOPMENT_BUILD`. Player/release builds return `true` from `AbyssalCavitationLayout.Validate()` and `ValidateLayoutColdOnce()` compiles to an empty cold hook; editor/development still performs the executable ARM64 offset audit before handle hydration.
Rejected Alternatives: Keeping cold player reflection, moving validation to chat-only documentation, or making player boot throw on layout mismatch. Editor/development catches layout drift; release should not pay reflection or gameplay exceptions.
Scalability potential: Low/Middle hardware avoids even cold reflection in player builds; High/Ultra keep the same DTO and shader payloads with validation handled by editor/development gates.
Hardware Impact: Measured microseconds pending player proof. Static player surface removes reflection-backed field lookup and layout exception path from SHINOBU runtime initialization.

## Decision 026 - Default CSV Loading Must Not Poll From SlowTick

Problem: `AbyssalCavitationRuntimeHost.SlowTick()` called `TryLoadDefaultOrdnanceCsv()` whenever `_csvLoaded` was false. If the CSV file was missing or rejected, that path could repeat `Path.Combine`, `File.Exists`, and `FileStream` creation every slow tick.
Solution: Add `_defaultCsvLoadAttempted` and gate the default load to one attempt after Vault availability. New Vault generation hydration resets `_csvLoaded` and `_defaultCsvLoadAttempted` because the profile buffer has just been cold-initialized. The editor tuner uses the new forced reload overload for deliberate human retry.
Rejected Alternatives: Polling the filesystem until success, hiding failure behind `_csvLoaded`, or making the one-shot gate block the editor facade. Filesystem polling belongs to explicit editor/tooling actions, not recurring gameplay cadence.
Scalability potential: Low hardware avoids recurring slow-tick file/path overhead in missing-CSV states; Middle/High/Ultra retain explicit designer reload and the same Vault-backed profile table.
Hardware Impact: Measured microseconds pending profiler proof. Static saving removes repeated path and file IO setup from every failed auto-load slow tick after the first attempt.

## Decision 027 - World Namespace Import Is Still A Compile-Wall Breach

Problem: `AbyssalCavitationRuntime.cs` still imported `Hecton8.World` after the SDF route was moved to SHINOBU-owned Vault snapshots. The only remaining symbols were floating-origin calls that belong to Core, not World.
Solution: Remove `using Hecton8.World;`. AUP conversion and current-origin references resolve through the existing `Hecton8.Core` import and `HectonFloatingOrigin` authority.
Rejected Alternatives: Keeping a direct sibling namespace import because no concrete World sampler call remained, or adding a World assembly reference to satisfy an unnecessary using.
Scalability potential: No runtime quality change; the gain is compile-wall isolation across Low through Ultra builds.
Hardware Impact: Runtime 0 us. Developer impact is reduced assembly coupling and lower risk of World-domain recompiles invalidating SHINOBU iteration.

## Decision 028 - CSV Profile Hydration Must Respect Active Burst Readers

Problem: The default CSV cadence was one-shot, but `TryLoadOrdnanceCsv()` still wrote the profile table and CSV profile counter without checking `_jobScheduled`. Editor reload or late default load could mutate Vault profile/counter rows while scheduled jobs were reading adjacent SHINOBU state.
Solution: Add a scheduled-job fence to both default and explicit CSV load paths. The default path checks `_jobScheduled` before setting `_defaultCsvLoadAttempted`; explicit CSV load also rejects while jobs are active.
Rejected Alternatives: Calling `Complete()` from the file-load path, allowing profile table mutation under active readers, or burning the default one-shot attempt while jobs are still running.
Scalability potential: Low hardware avoids main-thread stalls; High/Ultra keep deterministic job chaining and explicit reload remains available after jobs retire.
Hardware Impact: Race prevention only; no measured frame-time saving claimed.

## Decision 029 - CSV File IO Must Fail Closed

Problem: `TryLoadOrdnanceCsv()` used `File.OpenRead` and `ReadByte` after `File.Exists`. The existence check does not protect against access denial, sharing violations, or file removal between check and open, so an auto-load reachable path could leak an exception.
Solution: Wrap only the file open/read block in `try/catch`. On failure, return false and log one constant warning under `UNITY_EDITOR || DEVELOPMENT_BUILD`.
Rejected Alternatives: Letting exceptions escape, logging exception strings in release, or adding a retry loop. The CSV is a cold tuning source, not a gameplay dependency.
Scalability potential: Low through Ultra fail closed the same way; editor/development gets a diagnostic without gameplay exception churn.
Hardware Impact: Normal path unchanged. Fault path avoids crash propagation; no frame-time saving claimed.

## Decision 030 - Initialized Runtime Must Not Query GlobalRegistry Per Tick

Problem: `EnsureInitialized()` resolved `GlobalRegistry.DataVault` before checking the cached `_vault` and generation. `ScheduleSimulation`, `SyncShaderVisuals`, `SlowTick`, and debug accessors call this path, so registry discovery could occur after boot.
Solution: Add a cached fast path at the top of `EnsureInitialized()`. When no explicit Vault is supplied, `_vault != null`, and the cached generation still matches, the method returns before touching `GlobalRegistry`. Explicit Vault callers still require reference equality and generation match.
Rejected Alternatives: Leaving registry lookup in every accessor, or blindly returning true for explicit Vault calls after any initialization.
Scalability potential: Low hardware avoids unnecessary registry discovery in fixed/visual cadence; High/Ultra keep the same Vault capacity and math fidelity without extra lookup churn.
Hardware Impact: Measured microseconds pending profiler proof. Static saving removes registry discovery from initialized fixed/visual/slow-tick paths.

## Decision 031 - Burst Fence Audit Remains Static Until Build Wall Clears

Problem: After multiple polish edits, Burst directive and job-completion assumptions needed fresh source proof.
Solution: Re-scan owned runtime source for Burst compile attributes, `[NoAlias]`, and completion calls. Current source has 7 deterministic Burst jobs, 20 `[NoAlias]` fields, and no direct `JobHandle.Complete()` call. `forceComplete:true` is limited to cold mock injection, cold uninitialized-buffer hydration, and explicit scheduled finalization.
Rejected Alternatives: Reporting compliance from stale scans or launching another build into the known unrelated missing-source wall.
Scalability potential: Low through Ultra share the same deterministic job graph; verification prevents accidental scheduler stalls from creeping into weak hardware paths.
Hardware Impact: Verification only; no new runtime saving claimed.

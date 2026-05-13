<AGENT_PROMPT id="CORE_EXECUTION_ORCHESTRATOR" role="CORE_ENGINEER" chat_name="Phase Dictator & Kill-Switch">
[I. CORE IDENTITY & ANTI-AMNESIA PROTOCOL]
You are the Core Engineer. Target: i3, MX350. Engine: Unity 6.
Context compression is inevitable. Do not rely on chat memory.
1. Use CLI to `cat Docs/Tasks/ANOTHER_BATCH.md` and extract this prompt.
2. Initialize `Docs/Tasks/Status_CORE_EXECUTION_ORCHESTRATOR.md` and `Docs/AgentLogs/Rationale_CORE_EXECUTION_ORCHESTRATOR.md`.
3. Re-extract and re-read this prompt every 4 tasks.

[II. SITREP: THE CHAOTIC TICK & ARCHITECTURAL ROT]
Currently, our `SystemDispatcher` runs systems in whatever order they were registered. This causes 1-frame lags (Physics resolving before Input). Furthermore, if a system stalls, we have no way to isolate it.
CRITICAL: You must completely rebuild the `SystemDispatcher` to implement strict EXECUTION PHASES, a Watchdog, and a 64-bit Hardware Kill-Switch Matrix.

[III. PRIMARY OBJECTIVES: 19 TITANIUM TASKS]
-- PHASE 1: THE GREAT PURGE (INFECTION NEUTRALIZATION) --
1. SINGLETON ERADICATION: Scan `Assets/_Project/Scripts/Core`. Eradicate any `Dispatcher.Instance`. The Dispatcher must be bound via `GameBootstrapper`.
2. OLD API DEPRECATION: Find all systems using the legacy `ITickable`. Migrate them to explicitly declare their Phase (e.g., `ITickablePhase`).
3. ASMDEF ISOLATION: `Hecton8.Core.Dispatcher` depends ONLY on Contracts.
4. DEAD CODE HUNT: Eradicate any `FixedUpdate` remaining in custom Kinematic scripts. Everything moves to your new Phase Orchestrator.

-- PHASE 2: EXECUTION PHASES & WATCHDOG --
5. PHASE ENUMERATION: Define `enum ExecutionPhase { PreSimulation, Simulation, PostSimulation, VisualSync }`.
6. TICKET REGISTRY: Modify the Dispatcher to maintain 4 separate `NativeList<IntPtr>` or arrays, one for each phase. Systems MUST register into a specific bucket.
7. SEQUENTIAL EXECUTION: In the main `Update` loop, rigorously enforce the execution order: Pre -> Sim -> Post -> Visual.
8. SYSTEM WATCHDOG (MICRO-PROFILER): Wrap the execution of each phase in a high-precision `Stopwatch` check. If any single system in `Simulation` takes > 0.2ms, log `[CRITICAL_BLOAT]: System [Hash] is stealing frame time`.
9. DYNAMIC DEMOTION: If the Watchdog detects a system violating the 0.2ms budget for 10 consecutive frames, forcefully demote it from `FastTick` to `SlowTick` natively.

-- PHASE 3: THE KILL-SWITCH MATRIX --
10. KILL-SWITCH MASK: Define a `public ulong SystemKillSwitchMask` inside the Dispatcher.
11. BITWISE SYSTEM IDs: Assign every major system a bit (Bit 0 = AI, Bit 1 = Voxels, Bit 2 = Fluids).
12. HOT-SWAP DISABLING: During execution, evaluate `if ((SystemKillSwitchMask & system.BitID) == 0) continue;`. This allows us to instantly shut down heavy systems at runtime for debugging or extreme battery saving on Steam Deck.
13. DEV-CONSOLE INTEGRATION: Expose an API so the UI Dev Console can flip these bits via text commands (e.g., `kill_sys 2`).

-- PHASE 4: SAFETY, LOD & TELEMETRY --
14. AUP PRE-SHIFT PAUSE: If `AupPreShiftSignal` fires, the Dispatcher must pause the `Simulation` phase for exactly 1 frame, but allow `VisualSync` to execute to hide the stutter.
15. MATH LOD: On Low Tier (MX350), activate the Kill-Switch for non-essential domains (e.g., `FloraSway` or `DebrisPhysics`) automatically if average FPS drops below 55.
16. ZERO-GC: The Dispatcher must allocate exactly 0 bytes per frame. Use unsafe pointers or interface arrays to loop through systems.
17. BLACKBOX DUMP: On Crash, the Dispatcher must push the exact `SystemID` that was executing at the time of death to the Telemetry buffer.
18. SYSTEM TIMING UI: Write a debug string formatter (Zero-GC `Span<char>`) that visually lists the MS duration of the 4 phases on screen if `ENABLE_PROFILER_UI` is defined.
19. OMEGA COMPILE CHECK: Verify the Watchdog stopwatch compiles in Burst (use `Unity.Burst.Intrinsics.X86.Rdtsc` if available, otherwise native Unity `Time`).

[IV. RECURSIVE RE-VERIFICATION PROTOCOL]
You cannot stop. Once Tasks 1-19 are "done":
1. Re-read this prompt.
2. Check your execution loops. Did you use `foreach`? BANNED. Use `for` loops on flat arrays to maintain L1 cache locality.
STATUS: MUST BE "PENDING VERIFICATION".
</AGENT_PROMPT>

<AGENT_PROMPT id="CORE_DATA_VAULT_WARDEN" role="SYSTEMS_ARCHITECT" chat_name="Global Data Vault & Sentinel">
[I. CORE IDENTITY & ANTI-AMNESIA PROTOCOL]
You are the Systems Architect. Target: i3, MX350. Engine: Unity 6.
Extract prompt from `ANOTHER_BATCH.md` every 3 tasks.
Log to `Status_CORE_DATA_VAULT_WARDEN.md`.

[II. SITREP: THE SCATTERED MEMORY & ARCHITECTURAL ROT]
Currently, systems own their own `NativeArray`s. If a system crashes, the data is lost. If a neuro-agent forgets to call `Dispose()`, the game memory leaks entirely.
CRITICAL: You must implement `GlobalDataVault` (Data Sovereignty) and `H8Memory.Allocate` (Memory Sentinel) to protect the C++ unmanaged heap.

[III. PRIMARY OBJECTIVES: 19 TITANIUM TASKS]
-- PHASE 1: THE GREAT PURGE (INFECTION NEUTRALIZATION) --
1. SINGLETON ERADICATION: Scan for any `MemoryManager.Instance`. Bind `IDataVault` via GameBootstrapper.
2. ALLOCATOR MIGRATION: Search the entire `Scripts/` folder for `new NativeArray<T>(... Allocator.Persistent)`. Replace all of them with `H8Memory.Allocate<T>()`.
3. ASMDEF ISOLATION: `Hecton8.Core.Memory` must be the lowest level assembly, depending on NOTHING.
4. DEAD CODE HUNT: Eradicate manual `Dispose()` calls inside `Update()` that cause main-thread sync stalls.

-- PHASE 2: DATA VAULT SOVEREIGNTY --
5. THE VAULT REGISTRY: Create `GlobalDataVault`. It holds `UnsafeHashMap<int, IntPtr>` mapping a `BufferID` to raw memory.
6. STATELESS SYSTEMS: Systems no longer declare `NativeArray` as private fields. They call `DataVault.GetBuffer<SiltParticle>(BufferID.Silt)`.
7. LIFETIME DECOUPLING: If the `MarineSnowCompute` system is disabled via Kill-Switch, the Vault keeps the Silt buffer alive in RAM. When re-enabled, the system picks up exactly where it left off.
8. VAULT DEFRAG (FROST TICK): Every 5 seconds, the Vault evaluates fragmented arrays and optionally resizes them using `UnsafeUtility.MemRealloc`.

-- PHASE 3: MEMORY SENTINEL --
9. H8MEMORY API: Create static `H8Memory`. Method: `NativeArray<T> Allocate<T>(int length, SystemID owner, Allocator type)`.
10. LEAK TRACKER: `H8Memory` stores a `NativeParallelHashMap<IntPtr, SystemID>` tracking every single allocation.
11. AUTO-DISPOSE SENTINEL: If a system is unregistered from `GlobalRegistry`, the Sentinel intercepts this. It scans the leak tracker. If the system left memory behind, the Sentinel automatically calls `UnsafeUtility.Free`, logs `[FATAL LEAK PREVENTED]`, and prevents the OOM crash.
12. ALIASING GUARDS: Write an API `H8Memory.CreateAlias<T>()` to allow two systems to read the same Vault buffer safely without creating copies.

-- PHASE 4: SAFETY & LOD --
13. BOUNDS CHECKING: In `DEVELOPMENT_BUILD`, wrap all Vault retrievals in length validation checks.
14. AUP SHIFT SAFETY: The Vault does not care about coordinates, but it MUST lock allocations during `AupPreShiftSignal` to prevent pointer tearing.
15. MATH LOD: On Low Tier (MX350), cap the maximum allowed global Vault allocation pool to 512MB to leave room for VRAM shared memory.
16. ZERO-GC: The Sentinel and Vault must allocate 0 bytes on the managed heap. Use `UnsafeUtility`.
17. BLACKBOX DUMP: On crash, dump the `H8Memory` allocation table to text format to see exactly which system caused the crash.
18. CROSS-DOMAIN AUDIT: Ensure the Physics system correctly requests its RigidbodyAUP arrays from the Vault.
19. OMEGA COMPILE CHECK: Verify `UnsafeUtility` logic uses correct alignment (`UnsafeUtility.AlignOf<T>()`).

[IV. RECURSIVE RE-VERIFICATION PROTOCOL]
Once Tasks 1-19 are "done": 1. Re-read prompt. 2. Verify that `H8Memory.Allocate` uses the correct `Allocator` lifecycle labels internally.
STATUS: MUST BE "PENDING VERIFICATION".
</AGENT_PROMPT>

<AGENT_PROMPT id="SIGNAL_ROUTING_ARCHITECT" role="CORE_ENGINEER" chat_name="Signal Lane Segregation">
[I. CORE IDENTITY & ANTI-AMNESIA PROTOCOL]
You are the Core Engineer. Target: i3, MX350. Engine: Unity 6.
Extract prompt from `ANOTHER_BATCH.md` every 3 tasks.
Log to `Status_SIGNAL_ROUTING_ARCHITECT.md`.

[II. SITREP: THE CACHE MISS NIGHTMARE & ARCHITECTURAL ROT]
A monolithic `EventBus` or `GlobalSignals` queue destroys CPU Cache Locality. If the Localization system has to iterate past 5,000 Combat Damage signals to find a Language change, the i3 will stall.
CRITICAL: You must shatter the monolithic EventBus into `SignalLanes` (Typed NativeQueues) and enforce Contract Pinning.

[III. PRIMARY OBJECTIVES: 19 TITANIUM TASKS]
-- PHASE 1: THE GREAT PURGE (INFECTION NEUTRALIZATION) --
1. SINGLETON ERADICATION: Destroy `EventBus.Instance`.
2. SIGNAL MIGRATION: Convert all usages of `GlobalSignals.Push(ISignal)` to the new `SignalBus<T>.Push(T data)`.
3. ASMDEF ISOLATION: `Hecton8.Core.Signals` depends on NOTHING.
4. DEAD CODE HUNT: Eradicate any C# `Action` or `delegate` used for cross-domain messaging. All events MUST be unmanaged structs.

-- PHASE 2: SIGNAL LANES --
5. LANE GENERICS: Implement a highly optimized `public static class SignalBus<T> where T : unmanaged, ISignal`.
6. NATIVE QUEUE BACKING: Inside `SignalBus<T>`, maintain a `NativeQueue<T>` and a `NativeList<T>` (for the flushed frame data).
7. CATEGORY LANES: Create specific structs: `CombatDamageSignal`, `WeatherChangedSignal`, `SystemPauseSignal`. Because of generics, each gets its own discrete memory lane automatically.
8. THE FLUSH (PRE-SIMULATION): In the `PRE_SIMULATION` Execution Phase, iterate all active SignalBuses and flush their `NativeQueue` into a `NativeArray` snapshot.

-- PHASE 3: CONTRACT PINNING & CONSUMPTION --
9. IINITIALIZABLE PROTOCOL: Create `IInitializable` with `OnRegister()` and `OnDependencyInject()`.
10. DECOUPLED READING: Systems read signals by asking for the snapshot: `ReadOnlySpan<CombatDamageSignal> damages = SignalBus<CombatDamageSignal>.GetFrameSnapshot()`.
11. BATCH PROCESSING: Because signals of the same type are contiguous in memory, systems can process 100 damage events in a single Burst `IJobParallelFor` perfectly utilizing SIMD.
12. CLEARING: At the end of the `POST_SIMULATION` phase, clear the snapshot arrays.

-- PHASE 4: SAFETY & LOD --
13. OVERFLOW PROTECTION: If a `NativeQueue` exceeds 10,000 items in a single frame, log a `[SIGNAL STORM DETECTED]` warning and drop the oldest signals to prevent memory exhaustion.
14. AUP SHIFT SAFETY: Signals containing coordinates (`float3`) must be intercepted by the OriginShift system. If `AupShiftSignal` fires, the Shift system must iterate the signal snapshot and offset the coordinates BEFORE other systems read them.
15. MATH LOD: Independent of signals, but processing limits apply. Low Tier handles max 1000 signals per lane per frame.
16. ZERO-GC: Generics and `NativeQueue` ensure absolutely 0 boxing and 0 GC allocations.
17. BLACKBOX DUMP: Dump the count of signals per lane to the Telemetry buffer.
18. CROSS-DOMAIN AUDIT: Update `PlayerRuntimeContext` to push to `SignalBus<CombatDamageSignal>` correctly.
19. OMEGA COMPILE CHECK: Verify `SignalBus<T>` strictly enforces unmanaged constraints.

[IV. RECURSIVE RE-VERIFICATION PROTOCOL]
Once Tasks 1-19 are "done": 1. Re-read prompt. 2. Audit the flush sequence. Ensure no data is lost between `PRE_SIMULATION` and `POST_SIMULATION`.
STATUS: MUST BE "PENDING VERIFICATION".
</AGENT_PROMPT>

<AGENT_PROMPT id="PHYSICS_DETERMINISM_SYNC" role="LOCOMOTION_ENGINEER" chat_name="Floating Drift & Sync-Fence">
[I. CORE IDENTITY & ANTI-AMNESIA PROTOCOL]
You are the Locomotion Engineer. Target: i3, MX350.
Extract prompt from `ANOTHER_BATCH.md` every 3 tasks.
Log to `Status_PHYSICS_DETERMINISM_SYNC.md`.

[II. SITREP: THE FLOATING DRIFT & ARCHITECTURAL ROT]
Burst compiler optimizations (SIMD) and standard float math cause "Floating Point Drift." After 2 hours, identical inputs yield different AUP coordinates on Steam Deck vs PC.
CRITICAL: You must implement the "Sync-Fence" pattern and mathematically lock KCC/Rigidbody logic to be 100% deterministic to prepare for Lockstep Multiplayer.

[III. PRIMARY OBJECTIVES: 19 TITANIUM TASKS]
-- PHASE 1: THE GREAT PURGE (INFECTION NEUTRALIZATION) --
1. SINGLETON ERADICATION: Purge `PhysicsManager.Instance`.
2. SIGNAL MIGRATION: Physics must consume `SignalBus<InputSignal>` for movement intent, not `Input.GetAxis()`.
3. ASMDEF ISOLATION: `Hecton8.Physics.Determinism` depends only on Contracts.
4. DEAD CODE HUNT: Eradicate ALL instances of `math.sqrt()` and `Vector3.magnitude` in physical solvers. Replace with `math.rsqrt()` and `distancesq`.

-- PHASE 2: DETERMINISM LOCK --
5. FIXED POINT MATH FAKE: We cannot afford true fixed-point math on i3. Instead, apply a "Quantization Snap" at the end of every KCC movement integration: `pos = math.round(pos * 1000f) / 1000f` (Snap to millimeter precision).
6. DIVISION BAN: Replace all floating-point divisions (`/ dt`) with precalculated reciprocals `* math.rcp(dt)` to ensure consistent IEEE 754 rounding across CPUs.
7. TRIGONOMETRY APPROXIMATION: Use polynomial approximations for `sin/cos` (e.g., Bhaskara I approximation or pre-baked LUT) inside Burst for critical trajectory math. Do NOT rely on hardware `math.sin` which varies by architecture.

-- PHASE 3: THE SYNC-FENCE --
8. THE 300-FRAME FENCE: Every 300 `FastTick` frames (5 seconds), calculate an FNV-1a hash of the Player's AUP, velocity, and rotation.
9. STATE COMPARISON: In a multiplayer context (or against a recorded Replay log), compare this Hash. If it diverges, emit `DesyncDetectedSignal`.
10. AUTHORITATIVE SNAP: Consume `StateCorrectionSignal`. If received, instantly overwrite the KCC state from the payload.
11. DOUBLE-BUFFER ISOLATION: Ensure KCC reads from `State_Read` and writes to `State_Write`. Swap buffers ONLY at the `POST_SIMULATION` execution phase to prevent mid-frame reading bugs.

-- PHASE 4: KINEMATIC PERFECTION --
12. AUP ORIGIN SHIFT PERFECTING: Origin shift MUST apply the exact same integer subtraction to ALL objects. No scaling or rotational modifiers during a shift.
13. ZERO-GC RAYCASTS: Ensure `RaycastCommand.ScheduleBatch` is used for KCC ground checks. No synchronous raycasts.
14. MATH LOD: On Low Tier, reduce the number of iteration steps in the KCC penetration resolver from 4 to 2.
15. NO MONOBEHAVIOUR UPDATE: All kinematics run in the `SIMULATION` phase of the new `SystemDispatcher`.
16. BLACKBOX DUMP: Push the Sync-Fence hash to Telemetry every 300 frames.
17. EVENT BUS DAMAGE: Push `SignalBus<ImpactSignal>` upon high-velocity wall collision natively.
18. CROSS-DOMAIN AUDIT: Ensure Submarine Autopilot also uses the Quantization Snap.
19. OMEGA COMPILE CHECK: Verify Burst compiles with `FloatMode = FloatMode.Deterministic` applied to the jobs.

[IV. RECURSIVE RE-VERIFICATION PROTOCOL]
Once Tasks 1-19 are "done": 1. Re-read prompt. 2. Verify quantization math.
STATUS: MUST BE "PENDING VERIFICATION".
</AGENT_PROMPT>

<AGENT_PROMPT id="ENVIRONMENT_CAVE_OCCLUSION" role="VFX_TECHNICAL_ARTIST" chat_name="SDF Portals & Anti-Overdraw">
[I. CORE IDENTITY & ANTI-AMNESIA PROTOCOL]
You are the VFX Technical Artist. Target: i3, MX350.
Extract prompt from `ANOTHER_BATCH.md` every 3 tasks.
Log to `Status_ENVIRONMENT_CAVE_OCCLUSION.md`.

[II. SITREP: THE PIXEL BOTTLENECK & ARCHITECTURAL ROT]
Deep sea caves and wrecks cause massive Overdraw on the MX350 because Unity renders geo behind walls. Frustum culling is not enough. We need SDF-based portal/occlusion culling.
CRITICAL: Purge `OcclusionCullingManager.Instance`. Tie the occlusion math to the `GlobalDataVault`.

[III. PRIMARY OBJECTIVES: 19 TITANIUM TASKS]
-- PHASE 1: THE GREAT PURGE (INFECTION NEUTRALIZATION) --
1. SINGLETON ERADICATION: Purge `CullingManager.Instance`.
2. SIGNAL MIGRATION: Consume `CameraPositionSignal` instead of accessing `Camera.main`.
3. ASMDEF ISOLATION: `Hecton8.Environment.Culling` -> Contracts.
4. DEAD CODE HUNT: Eradicate Unity `OcclusionPortal` components. They are too CPU heavy. We use GPU/Burst culling.

-- PHASE 2: SDF OCCLUSION MATH --
5. SDF SAMPLING JOB: Read the `VoxelSdfTexture3D` from the `GlobalDataVault`. 
6. SPHERICAL VISIBILITY: In a Burst job, raymarch 16 coarse rays outward from the Player's AUP along the camera frustum using the SDF.
7. OCCLUSION MASK: If a ray hits `SDF density > 0` (Solid rock) before max distance, mark that frustum sector as Occluded.
8. BRG CULLING INJECTION: Pass this 16-sector Occlusion Mask to the Compute Shader responsible for `BatchRendererGroup` instance culling.

-- PHASE 3: WRECK PORTALS & HIERARCHY --
9. WRECK ROOM CULLING: Read `NativeArray<ulong> RuinLootMask` (from WFC generation). Each room has an AABB.
10. PORTAL TRAVERSAL: If the player is inside a Wreck Room, mathematically calculate if the adjoining room portals are within the camera frustum. If not, cull the entire adjoining room's BRG matrices.
11. PREDATOR CULLING: Apply the SDF Occlusion Mask to the Fauna rendering matrices. Do not draw sharks that are behind cave walls.
12. LIGHT CULLING: Dynamically disable `PointLight` or `GlowPoint` data if their AUP falls within an Occluded sector.

-- PHASE 4: SAFETY & LOD --
13. AUP SHIFT SAFETY: SDF offsets and Wreck AABBs must flawlessly subtract the `AupShiftSignal` offset to prevent culling the room the player is standing in.
14. REPROJECTION (THE DEAR LIE): To save CPU, re-use the Occlusion Mask from frame N-1, only updating the rays that shifted significantly.
15. MATH LOD: On Low Tier (MX350), evaluate the SDF raymarch at 10Hz (SlowTick) instead of every frame, interpolating visibility flags.
16. ZERO-GC: The Burst raymarching and Portal culling must allocate 0 bytes.
17. BLACKBOX DUMP: Push `CulledInstances` and `OverdrawSavedPixels` metrics to Telemetry.
18. CROSS-DOMAIN AUDIT: Ensure the Cartography Map cubes also respect the Occlusion Mask when drawn.
19. OMEGA COMPILE CHECK: Verify the Burst Raymarch Job does not contain `while(true)` without a hard iteration limit.

[IV. RECURSIVE RE-VERIFICATION PROTOCOL]
Once Tasks 1-19 are "done": 1. Re-read prompt. 2. Verify BRG compute shader binds the mask correctly.
STATUS: MUST BE "PENDING VERIFICATION".
</AGENT_PROMPT>

<AGENT_PROMPT id="UI_HARDWARE_ADAPTABILITY" role="UX_ENGINEER" chat_name="Diegetic Scaling & Font Weights">
[I. CORE IDENTITY & ANTI-AMNESIA PROTOCOL]
You are the UX Engineer. Target: SteamDeck, VR, PC.
Extract prompt from `ANOTHER_BATCH.md` every 3 tasks.
Log to `Status_UI_HARDWARE_ADAPTABILITY.md`.

[II. SITREP: THE ILLEGIBLE TEXT & ARCHITECTURAL ROT]
The NASA-Punk UI looks great at 4K, but on Steam Deck (800p) or VR, thin lines vanish and text becomes unreadable. We need dynamic SDF font weighting and adaptive UI scaling tied to hardware profiles.
CRITICAL: Purge `UIManager.Instance`. UI scaling must read the `SystemKillSwitchMask` and Hardware tier via EventBus.

[III. PRIMARY OBJECTIVES: 19 TITANIUM TASKS]
-- PHASE 1: THE GREAT PURGE (INFECTION NEUTRALIZATION) --
1. SINGLETON ERADICATION: Purge `CanvasScalerManager.Instance`.
2. SIGNAL MIGRATION: Hardware resolution changes emit `ResolutionChangedSignal`. UI nodes react, not poll.
3. ASMDEF ISOLATION: `Hecton8.UI.Scalability` -> Contracts.
4. DEAD CODE HUNT: Eradicate Unity's default `CanvasScaler`. The UI is diegetic (3D), standard scalers are useless.

-- PHASE 2: SDF FONT ADAPTATION --
5. DYNAMIC THICKNESS: Write a `SlowTick` job that queries screen resolution and `GlobalRegistry.IsVRActive`.
6. MATERIAL OVERRIDE: If Resolution < 1080p or VR == True, modify the global `_HectonFontThickness` and `_HectonFontDilate` properties passed to the TMP SDF materials.
7. CONTRAST BOOST: On Steam Deck/Low Tier, mathematically increase the opacity of diegetic UI backgrounds (glass panels) to ensure contrast against dark ocean backgrounds.
8. BONE OFFSETTING: In VR, pull the Visor UI elements closer to the camera center using `CinematicMath.FastNlerp` to prevent eye strain at the periphery.

-- PHASE 3: PERFORMANCE DEGRADATION (KILL-SWITCH) --
9. KILL-SWITCH READING: Query the Dispatcher's `SystemKillSwitchMask`. If the "UI Blurs" bit is flipped off (due to low FPS), swap all diegetic glass materials from expensive `GrabPass` blurs to cheap semi-transparent solids.
10. TICK DEMOTION: If FPS drops, demote UI animation updates (blinking cursors, scrolling text) from `FastTick` to `SlowTick`.
11. HOLOGRAPHIC CULLING: If an object with UI (like a Fabricator) is > 10m away, disable its UI components completely to save Canvas rebuilds.
12. TEXTURE MIP BIAS: Force a higher mipmap bias on UI iconography textures if VRAM usage (from QA Bot telemetry) exceeds 90%.

-- PHASE 4: SAFETY & VALIDATION --
13. AUP SHIFT SAFETY: Ensure scaled diegetic UI elements maintain relative offsets during `AupShiftSignal`.
14. ZERO-GC FORMATTING: Dynamic UI text (like Distance to waypoint) MUST use `Span<char>` and `SetCharArray`. No `string.Format`.
15. MATH LOD: On Low Tier, never evaluate the background blur pass.
16. BLACKBOX DUMP: Push `ActiveUINodes` and `FontWeightScalar` to Telemetry.
17. CROSS-DOMAIN AUDIT: Ensure the Scanner Artifact Minigame UI respects the font thickness boost.
18. EVENT BUS COUPLING: Consume `DamageSignal`. Apply a glitchy offset to UVs in the UI shader based on hit severity.
19. OMEGA COMPILE CHECK: Verify no `FindObject` is used to locate UI canvases.

[IV. RECURSIVE RE-VERIFICATION PROTOCOL]
Once Tasks 1-19 are "done": 1. Re-read prompt. 2. Verify SDF material property updating does not allocate new Material instances (Use `MaterialPropertyBlock` or Global vars).
STATUS: MUST BE "PENDING VERIFICATION".
</AGENT_PROMPT>

<POLISH_MANDATE id="OMEGA_POLISH">
[AUTHORITY]: CTO / Lead Architect (T.A.R.S. MODE)
[CRITICALITY]: OMEGA (Final Polish & Code Burial)

You have reported task completion. In HECTON-8, "complete" means optimized beyond industry standards. Execute a brutal "Anti-Bloat Inquisition" of your own implementation.

[PHASE 1: THE "DEAR LIE" AUDIT]
1. Did you write an "honest" calculation that can be replaced by a 1D LUT, triangle-wave, or bitmask? If yes, replace it.
2. Are you respecting the SCALABILITY MATRIX? `if (Tier == High) -> expensive math; else -> dominant-axis snap / dither`.
3. Did you use `math.sqrt()` or `math.normalize()` unconditionally? Fallback to `math.rsqrt` where exact precision is not visually critical.

[PHASE 2: FRAME TIME DICTATORSHIP]
1. Ensure bitmasks `(flags & MASK) != 0` are used instead of boolean branches in Burst jobs.
2. Replace floating-point divisions with precomputed reciprocals (`math.rcp`) and multiplications.

[PHASE 3: ZERO-GC PURGE & SILO AUDIT]
1. Find any `foreach` on managed collections and replace with `for(int i)`.
2. Find `string.Format`, `$"..."`, or `.ToString()`. Delete or wrap in `#if UNITY_EDITOR`.
3. Did you edit a file outside your domain? Justify it in your Rationale log or move the logic to an EventBus signal.
4. Run `dotnet build Hecton8.Core.csproj`. (NOTE: The build may currently be red due to integrator work. Ensure YOUR specific scripts compile without syntax errors using Unity's validation).

[REPORTING REQUIREMENTS]:
Update `Docs/AgentLogs/Rationale_[ID].md` with "OMEGA POLISH CHANGES". List the exact cinematic cheats used. Provide the final Git Diff.
STATUS: MUST BE "VERIFIED MASTER GRADE" (Or PENDING if blocked by global compile dependencies).
</POLISH_MANDATE>
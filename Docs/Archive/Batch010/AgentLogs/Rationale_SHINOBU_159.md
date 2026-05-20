# Rationale_SHINOBU_159

Status: STATIC PATCHED / FIXED-SLOT AND QUALITY-CONTRACT POLISH / BUILD BLOCKED BY CPU GATE

## Decision 00: Domain Boundary And Route
Problem: Per-object flora glow mutation would instantiate material state, damage SRP Batcher behavior, and scale CPU work with plant count.
Solution: Use the DOD visual fake route: one 64-byte explicit DTO shaped as a float4x4, phase advanced by Burst, shader reads global matrix and local/world-relative coordinates.
Rejected Alternatives: Per-renderer MaterialPropertyBlock and Material.SetFloat are rejected because standard geometry MPB breaks SRP Batcher and SetFloat scales with material count.
Scalability potential: Low uses vertex pulse and cheap sine. Middle keeps vertex pulse plus darker activation. High blends in fragment refinement. Ultra spends saved CPU on richer interference/flicker in shader.
Hardware Impact: Estimated low-end i3/MX350 CPU saving is proportional to visible flora count; replacing 1000 per-material calls with one matrix upload targets tens to hundreds of microseconds saved and zero managed allocations. Exact profiler proof absent.

## Decision 01: Owner-Local Pulse Vault ID
Problem: Adding `BiolumPulseState` to the central `BufferID` enum churned a core memory header and would expand the compile wall while other agents are editing memory IDs.
Solution: Use an owner-local constant `private const BufferID BiolumPulseStateBufferId = (BufferID)70311;` inside the biolum runtime. Static scan over `Assets/_Project/Scripts` finds `70311` only in this owner file.
Rejected Alternatives: Editing `H8Memory.cs` was rejected after seeing unrelated active memory edits in the same file. Reusing `BufferID.BiolumGlobalStates` was rejected because that old vector array is not the single authoritative 64-byte matrix truth.
Scalability potential: One buffer ID owns one fact. Low through Ultra all consume the same matrix contract; only shader math changes with `GlobalQualityWeight`.
Hardware Impact: Prevents broader C# recompiles. Runtime impact is one 64-byte Vault allocation at boot, zero persistent private arrays for pulse truth.

## Decision 02: Explicit Matrix DTO
Problem: The shader requires a direct `float4x4` upload, while C# structs with properties or sequential padding can drift from the GPU contract and produce ARM64 unaligned reads.
Solution: Define `[StructLayout(LayoutKind.Explicit, Size = 64)] BiolumPulseStateDTO` with `float4` rows at offsets 0, 16, 32, and 48. `AreSyncLayoutsValid()` checks `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset` at boot.
Rejected Alternatives: Four separate `NativeArray<float4>` rows and DTO properties were rejected. They create more Vault handles and invite CS1612 defensive copies.
Scalability potential: Low/Middle/High/Ultra all upload the same 16 floats. Extra visual richness is shader-side, not a CPU data shape fork.
Hardware Impact: 64 bytes maps to one cache line and one matrix register payload; expected CPU cost for the global oscillator is sub-microsecond before upload.

## Decision 03: Deterministic Phase Job
Problem: Presentation pulses must remain stable for long sessions and should not rely on `Time.deltaTime` or managed per-object state.
Solution: `AdvanceBiolumPhasesJob` uses Burst deterministic float mode, clamps `DeltaTime`, advances `phase += frequency * dt * panicSpeed`, wraps with modulo `2*PI`, and writes back via `UnsafeUtility.AsRef`.
Rejected Alternatives: MonoBehaviour `Update()` per plant, shader-only unbounded `_Time`, and managed arrays were rejected. They either desync, lose designer control, or scale with instance count.
Scalability potential: At low quality the cadence stretches and the shader consumes the vertex pulse. At high/ultra the same phase rows drive fragment interference without additional CPU state.
Hardware Impact: CPU work is O(4) rows, independent of 100k flora instances.

## Decision 04: Dear Lie Shader Contract
Problem: A believable glowing forest does not require CPU knowledge of which plant is bright each frame.
Solution: `Hecton_IndirectVegetation.shader` reads `_GlobalBiolumDearLieGroups`; each row is Phase/Frequency/Amplitude/SpatialOffset. Vertex code computes the cheap wave once, fragment code blends toward pixel/interference/filament math using continuous `_GlobalBiolumParams.y` quality.
Rejected Alternatives: CPU-side per-instance emission writes and MaterialPropertyBlocks were rejected. Full light propagation or fluid simulation was rejected as visual waste.
Scalability potential: Low = vertex sine interpolation. Middle = fragment sine blend. High = secondary-row interference. Ultra = filament shimmer and tint mixing, all from the same matrix.
Hardware Impact: CPU cost drops from O(visible plants/materials) to O(4). GPU ALU scales continuously with quality.

## Decision 05: Material API Sanitization Scope
Problem: `HectonIndirectVegetationRenderer` used material float writes for vegetation runtime constants, direct keyword flips, and had a cold material-clone fallback path. These are not biolum oscillator truth and still add material mutation pressure.
Solution: Pack runtime draw/LOD scalars into two vectors, remove direct keyword APIs, remove the `HECTON_GPU_INDIRECT` variant from the vegetation/depth/shadow/motion shaders, and bind runtime buffers through preallocated `MaterialPropertyBlock` instances. Authored pass materials now live under `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_HectonIndirectVegetation*.mat`. Targeted scans show no `Material.SetFloat`, `material.SetFloat`, `sharedMaterial.SetFloat`, `EnableKeyword`, `DisableKeyword`, `SetKeyword`, or `new Material` in `Assets/_Project/Scripts/VFX/Bioluminescence` or `HectonIndirectVegetationRenderer.cs`.
Rejected Alternatives: Leaving float setters was rejected. Keeping `LocalKeyword.SetKeyword` was rejected because it still relies on runtime shader variant mutation. Rewriting all project-wide material users was rejected under domain boundary; static scan shows many off-domain owners. Keeping cold BRG clones was rejected in the polish pass; GPU indirect now fail-closes if authored pass materials or visible-index buffers are absent.
Scalability potential: Biolum pulse path is global-matrix driven. Low uses the same material contract as Ultra; only shader ALU scales via quality vectors and `_GlobalBiolumParams.y`.
Hardware Impact: Avoids per-frame/per-bind float scalar chatter and removes cold material clone allocation from the targeted flora renderer route. Exact gain requires Unity profiler; compile/run not executed due CPU gate.

## Decision 06: Human Control And Cold CSV
Problem: Designers need pulse tuning without recompiling C#, and CI needs a deterministic mock when celestial/apex systems are absent.
Solution: Add `Abyssal Glow Tuner` editor controls for frequency, spatial offset, darkness threshold, and predator panic speed. Add cold `biolum_pulse_profiles.csv` byte parser with legacy fallback. Add `GenerateMockLightingState()` seeded through a Burst job.
Rejected Alternatives: Managed string parsing in gameplay and hardcoded constants were rejected. Direct AI/Celestial assembly references were rejected; mock Vault signals preserve compile isolation.
Scalability potential: Low/Middle/High/Ultra values are the same profile rows; quality only changes cadence and shader richness.
Hardware Impact: Editor and CSV work is cold-path. Hot path remains Vault arrays plus Burst jobs.

## Decision 07: Blackbox And Verification Boundary
Problem: A NaN in phase math must be diagnosable, and false completion is worse than an honest blocked compile gate.
Solution: Telemetry writes a 300-entry ring with darkness, group0 phase, frequency multiplier, and compute time; NaN dumps `Docs/AgentLogs/Dump_BIOLUM_DIRECTOR.bin` and `.h8dump`. Static scans were run. Build was not run because CPU gate reported 97-100% load and no compiler processes.
Rejected Alternatives: Claiming runtime proof without Unity import/build was rejected. Arbitrary `dotnet build` under high CPU was rejected by AGENTS.md.
Scalability potential: The blackbox is fixed-size and quality-independent.
Hardware Impact: One 32-byte telemetry entry per frame. Build verification remains pending until CPU <=50% and no `dotnet`/`csc` is active.

## Decision 08: Authored Pass Materials And Runtime Branch
Problem: A shader keyword variant for indirect draws forces runtime material keyword management, and cloned BRG pass materials violate the `new Material()` prohibition even when cold.
Solution: Convert main/depth/shadow/motion vegetation shaders to a uniform runtime branch driven by `_HectonVegetationRuntimeDrawParams.w`. Use authored materials for the forward/depth/shadow/motion passes and bind pass-local buffers/scalars through preallocated `MaterialPropertyBlock` objects passed via `RenderParams.matProps`.
Rejected Alternatives: Runtime keyword mutation and material clones were rejected. Directly deleting pass separation was rejected because near/far/depth/shadow/motion need different visible-index buffers and pass mode values.
Scalability potential: Weak devices can skip optional depth/shadow/motion authored passes by nulling those materials or flags; high/ultra keeps all passes without changing shader variants.
Hardware Impact: Eliminates material clone allocation and shader keyword churn in the vegetation pulse path. GPU draw count is unchanged; CPU material state mutation moves to reusable MPBs.

## Decision 09: Matrix-Only Pulse Contract
Problem: The runtime still contained a legacy O(N) color sync path: `BiolumVisualSyncJob`, `_BiolumGpuColorBuffer`, two Vault GPU color handles, `GraphicsBuffer.LockBufferForWrite`, and `Shader.SetGlobalBuffer`. That contradicted the single float4x4 pulse-matrix mandate even though the path was quality-gated.
Solution: Remove the GPU color buffer handles, upload method, shader structured-buffer declaration, R10G10B10A2 decode function, and `BiolumVisualSyncJob`. `ScheduleStateJob` now schedules only `AdvanceBiolumPhasesJob`; VISUAL_SYNC publishes only `_GlobalBiolumDearLieGroups` plus scalar vectors. `_GlobalBiolumParams.w` and `_GlobalBiolumClock.w` are hard-zeroed so shaders cannot revive individual color weight. Fixed-slot `SyncPulseDTO` AUP events are consumed only as constant-count row perturbations after subtracting the local AUP reference; they never become a plant loop or GPU color buffer.
Rejected Alternatives: Keeping the per-instance path disabled by weight was rejected because dead switches rot into regressions and still force buffer lifetime/ABI assumptions. Moving individual colors into an MPB was rejected because it reintroduces per-renderer state. A compute shader color prepass was rejected because this task's visual fake is spatial waves from one matrix.
Scalability potential: Low/Middle/High/Ultra all consume the same 16-float matrix. Low collapses to vertex pulse and longer cadence; higher tiers spend GPU ALU on fragment interference and filament shimmer without increasing CPU state.
Hardware Impact: Removes one Burst IJobParallelFor over up to 50,000 glow records, one GPU upload/copy, and one structured buffer bind from the biolum sync frame path. Static target is CPU O(4 + fixed pulse slots) for pulse math and one matrix upload; profiler proof remains blocked by CPU_LOAD=100.

## Decision 10: Fixed-Slot Pulse Validity And Continuous Shader Quality Contract
Problem: `AdvanceBiolumPhasesJob` still trusted SHINOBU's private `_activeSyncPulseCount` to decide how many fixed `SyncPulseDTO` slots to read. That can hide valid Vault pulse data from another producer and can make clear-memory age slots look active when payload is empty. Separately, several shared visible biolum shaders still treated `_GlobalBiolumParams.y` as a tier index using `step(4.0, y)`, while SHINOBU publishes a continuous 0..1 quality weight.
Solution: Scan the fixed 16 sync-pulse slots every scheduled matrix update, reject non-finite or non-positive `WaveSpeed`, localize `OriginAUP - AupReference` before float math, and count telemetry active waves only when age and payload are valid. Replace stale shader `step(4.0, _GlobalBiolumParams.y)` gates in coral, kelp, sargassum, procedural bio, and leviathan visible biolum paths with saturate plus polynomial quality curves that multiply overdrive/haze/spark contributions.
Rejected Alternatives: Keeping the private active counter as a producer route was rejected because one fact needs one owner and one proof; the fixed slot itself is the proof. Keeping tier-index shader gates was rejected because High/Ultra detail was unreachable under the continuous quality contract. Adding direct cross-domain dependencies was rejected; the shared payload remains blittable fixed-slot data.
Scalability potential: Low uses the same matrix and suppresses overdrive through the quality curve. Middle gradually introduces secondary-row interference. High/Ultra reaches the existing richer shader haze/spark math without changing CPU state or adding buffers.
Hardware Impact: CPU stays O(4 + 16) and zero-GC. The shader change repairs a correctness bug in quality scaling; profiler proof remains blocked by CPU_LOAD=100 and active dotnet processes.

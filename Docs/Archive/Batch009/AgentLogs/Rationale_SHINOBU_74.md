# Rationale_SHINOBU_74

Status: GPU_PAGE_DEARLIE_RNG_FRAMECLOCK_DETERMINISTIC_BURST_TRIANGLE_PULSE_SQDIST_QUALITY_CADENCE_INTERPOLATOR_TELEMETRY_ASMDEF_SPLIT_HPHI_ARRAY_PURGE_ORPHAN_META_PURGED_GLOBAL_SHADER_CONSUMERS_MATRIX_PURGED_STATIC_VERIFIED_BUILD_PENDING
Evidence Class: STATIC_SOURCE after asmdef/H-PHI and shader-consumer matrix purge. Last narrow runtime C# build passed before this patch and is now stale for latest code. Unity import, Burst runtime, shader import, Play Mode, profiler, and Frame Debugger evidence still absent.

## Initial Technical Boundary

Problem: 50,000 bioluminescent flora instances cannot be synchronized through Unity `Light`, per-renderer material mutation, or material instance churn without breaking batching and frame time.

Solution: Build a deterministic presentation fake: packed `uint` emission colors, Burst oscillator, aligned unmanaged DTOs, AUP-safe pulse math, and a `GlobalQualityWeight` continuum that blends individual waves into 4 global shader pulses.

Rejected Alternatives: Unity `Light` components, `Material.SetColor`, `renderer.material`, MaterialPropertyBlock on standard flora geometry, per-plant GameObjects, runtime CSV/string parsers, and binary quality switches.

Scalability potential: Low uses 4 global sync colors and low cadence; Middle blends group pulses with sparse individual pages; High processes full spatial waves for active flora pages; Ultra keeps richer pulse overlays and damage/O2 harmonics while preserving packed buffer output.

Hardware Impact: On i3/MX350, expected gain comes from preserving SRP batching and replacing object/material mutation with one native buffer path. Exact frame-time savings are PENDING VERIFICATION.

## DTO Layout Commitments

Problem: Runtime DTOs feeding Burst/GPU must be stable on ARM64 and not trigger CS1612 property mutation failures.

Solution: `GlowStateDTO` fields remain public fields only: `uint PackedColor`, `float Phase`, `float Frequency`, `uint SpeciesHash` for 16 bytes. `SyncPulseDTO` uses `double3 OriginAUP`, `float WaveSpeed`, `uint ColorOverride` for 32 bytes.

Rejected Alternatives: auto-properties, `Pack=1`, managed colors, `UnityEngine.Color`, runtime `bool`, strings, arrays, and class wrappers.

Scalability potential: compact DTOs keep Low hardware cache-friendly; Ultra can add separate presentation-only buffers instead of bloating primary DTOs.

Hardware Impact: Aligned 16/32-byte records reduce cache-line waste and avoid ARM64 misaligned loads. Exact microsecond gain is a static estimate only until Burst profiling.

## Continuous Glow Budget Fix

Problem: The active runtime used a binary Dear Lie gate through `_dearLieOnlyActive` and `UseDearLieOnly`, keyed from `SystemHealthIndex01 > 0.85`. That violated SHINOBU_74 because the actual scalability authority is `GlobalQualityWeight`, and the transition from 50,000 spatial waves to 4 global pulses must be continuous.

Solution: Removed the binary gate and added `_globalQualityWeight`, `_individualGlowWeight01`, `_dearLieBlend01`, and `_scheduledGpuColorCount`. `RefreshGlobalQualityWeight()` reads `HomeostasisBrain.GlobalQualityWeight`; `ResolveIndividualGlowWeight()` smoothsteps the quality/stress continuum; `ResolveScheduledGlowCount()` maps the result to `4..50000` job iterations. The Burst job receives `GlobalQualityWeight`, fades expensive spatial pulses by `IndividualGlowWeight01`, boosts the 4 Dear Lie groups by `DearLieBlend01`, and only uploads packed GPU colors for the scheduled count.

Rejected Alternatives: Keeping `_dearLieOnlyActive`; hardware tier enums; `if low tier then skip all` switches; point lights; material swaps; per-renderer MPB updates.

Scalability potential: Low uses exactly 4 global pulses at `GlobalQualityWeight` 0.1. Middle schedules a partial per-plant page count with a smooth polynomial curve. High schedules near-full spatial waves. Ultra keeps the same 50,000 limit but spends saved cycles on pulse overlays, damage flicker, oxygen warning, and bloom-visible HDR emission.

Hardware Impact: On i3/MX350-class silicon, the 0.1 quality path statically avoids up to 49,996 per-plant iterations and skips the bulk packed-color upload. Exact frame-time savings require Unity Profiler/Frame Debugger capture and are not fabricated here.

## Verification And Self-Audit

Problem: The system needed proof that the batching killer was removed without inventing performance numbers.

Solution: Ran static scans for forbidden tokens in the assigned biolum runtime/editor files, re-read the SHINOBU_74 prompt count from `CURRENT_BATCH.md`, and compiled both runtime/editor C# projects with `--no-dependencies` to isolate this assembly.

Rejected Alternatives: Reporting completion from source inspection only; using the full dependency build timeout as a false failure; editing unrelated core systems.

Scalability potential: The shader-facing globals now carry the active quality/fallback blend for low, middle, high, and ultra behavior without changing material instances.

Hardware Impact: Static audit passed. Earlier runtime/editor C# compiles are historical and predate the asmdef/H-PHI patch. Unity import, fresh assembly build, Play Mode, GPU timing, and Frame Debugger evidence are still pending, so exact measured microseconds remain pending.

## Ultra Polish Correction: Aliasing, Endian, Cache Batch

Problem: The first pass fixed the visible binary Dear Lie violation but still left three forensic gaps: Burst aliasing proof was implicit, profile binary hydration used native-endian float reads, and the job batch size was a magic literal instead of a cache-line contract.

Solution: Added `[NoAlias]` to every disjoint `BiolumVisualSyncJob` NativeArray lane, kept read-only lanes marked `[ReadOnly, NoAlias]`, replaced `MemoryMarshal.Read<float>` profile hydration with explicit little-endian byte assembly plus `math.asfloat`, and named the schedule batch as `BiolumJobInnerLoopBatchCount = 64` with cache rationale.

Rejected Alternatives: Trusting Burst to infer aliasing from DataVault BufferIDs; assuming all future import tools are little-endian by accident; keeping `64` as an unexplained scheduling literal; claiming profiler savings without Burst Inspector evidence.

Scalability potential: Low and middle tiers benefit from fewer scheduled iterations and better SIMD eligibility; high and ultra keep the full 50,000 path while preserving disjoint data lanes for Burst optimization.

Hardware Impact: Compile passed after the correction. Runtime speedup is not measured here. The practical impact is removing blockers to NEON/AVX vectorization and preventing silent profile corruption on non-native endian readers.

## H-PHI Caveat

Problem: The polish mandate asked for all runtime arrays to be in the Vault. The earlier implementation still had two cold managed bridge arrays, so the caveat was real and had to be removed rather than documented forever.

Solution: Persistent simulation/upload data remains DataVault-owned through `VaultBufferHandle`s. The managed `Vector4[16]` shader bridge was replaced by `Matrix4x4 _dearLieGroupMatrix` and `_GlobalBiolumDearLieGroups`. The managed `byte[16384]` CSV staging buffer was removed; CSV reload now reads directly into `BiolumCsvScratch`.

Rejected Alternatives: Keeping shader array support because it was convenient; parsing CSV from managed strings; claiming zero private arrays before removing them.

Scalability potential: These bridges do not scale with plant count per frame; 50,000 plant state stays in native vault buffers.

Hardware Impact: Hot path remains 0 B/frame by static inspection. Profiler proof is still pending.

## Runtime Host Wiring

Problem: The binary ledger showed `Data/Visuals/Biolum_Profiles.bin` had a reader, but no scene/prefab/bootstrap proof that `BiolumPulseSyncRuntime` would actually exist in a clean play session. A binary reader with no boot path is dead integration.

Solution: Added a scene-local `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` fallback host with an atomic process ownership claim. If an authored scene instance has already enabled and claimed ownership, the fallback does nothing. If none exists, it creates one cold `H8_BiolumPulseSyncRuntime` host and attaches the runtime. The hot 50,000-plant path remains in native buffers.

Rejected Alternatives: Editing global Core bootstrap, scene/prefab assets, introducing a sibling-domain dependency, storing a static runtime instance pointer, using scene `FindObject` probes, or relying on documentation that a scene object will exist. Per-plant GameObjects remain rejected; this is one cold scene service host.

Scalability potential: Low, middle, high, and ultra tiers now reach the same profile payload path. The quality continuum remains `GlobalQualityWeight`-driven after boot, with 4 global pulses at the low end and 50,000 packed colors only when quality/stress permits.

Hardware Impact: No frame-time savings claimed. The impact is integration correctness: the profile payload path is statically wired. Runtime proof still requires Unity Play Mode plus Profiler/Frame Debugger capture.

## Unity Singleton Purge

Problem: The first runtime-host pass used `s_runtimeInstance` and `Awake()` duplicate suppression. That matched common Unity code, but it violated the project rule forbidding classic singleton/self-registration patterns.

Solution: Removed the static runtime instance pointer and deleted `Awake()`. Ownership is now a single `int s_runtimeClaimed` process claim reset at subsystem registration, acquired in `OnEnable()` with `Interlocked.CompareExchange`, and released through `ReleaseRuntimeOwnerClaim()`. The claim does not expose an instance accessor and does not become a service locator; real update/late-frame participation still goes through `GlobalRegistry`.

Rejected Alternatives: Keeping `s_runtimeInstance`; using `FindAnyObjectByType` in runtime; moving ownership into Core bootstrap without authorization; letting duplicate hosts race DataVault locks.

Scalability potential: No change to quality math. This preserves compile-wall and lifecycle discipline while keeping the low/middle/high/ultra glow continuum unchanged.

Hardware Impact: No runtime microsecond claim. The value is architectural: no classic singleton pointer, no `Awake()` self-registration, and no scene search allocation.

## Post-Host Compile Wall

Problem: After the runtime-host edit, isolated `Assembly-CSharp` and `Assembly-CSharp-Editor` builds could not resolve `Temp\bin\Debug\Hecton8.Core.dll`. A minimal `Hecton8.Core.csproj` compile was required to distinguish SHINOBU failure from dependency failure.

Solution: Ran the minimal Core dependency check once. It fails outside the SHINOBU domain at `Assets/_Project/Scripts/Core/GlobalSignals.cs(1119,26)` with CS0266: cannot implicitly convert `void*` to `T*`. Work stopped there and the status was marked as blocked by Core dependency, not silently reported as a clean compile.

Rejected Alternatives: Editing Core without authorization, repeatedly rebuilding into the same known dependency failure, or reverting SHINOBU code to hide a pre-existing Core compile wall.

Scalability potential: Not applicable to runtime quality. The architectural impact is preserving domain boundaries under parallel-agent work.

Hardware Impact: Build-time blocker only. No runtime microsecond claim.

## Packed Shader Bridge Correction

Problem: Static source review found a render-path disconnect after the CPU packed-color work. `BiolumPulseSyncRuntime` uploaded `_BiolumGpuColorBuffer`, but `Hecton_IndirectVegetation.shader` did not consume that buffer. A second defect existed in the global fallback: the Burst path writes four valid Dear Lie states, while `_GlobalBiolumParams.x` could still publish `_activeStateCount` up to 16, letting shaders sample zero-filled global slots.

Solution: Added `_publishedGlobalStateCount` so shader globals publish the actual valid state count. After the Burst job, that count is four. The indirect vegetation shader now declares `_BiolumGpuColorBuffer`, decodes RGB10_A2 `uint` colors by `sourceInstanceIndex`, and blends authored glow toward synced runtime glow only when a real uploaded GPU page exists.

Rejected Alternatives: Leaving the GPU buffer as dead upload bandwidth; switching to material properties or per-renderer MPBs; sampling all 50,000 packed colors after partial upload; publishing `_activeStateCount` while only four global states are valid.

Scalability potential: Low keeps four global pulses and no individual buffer sampling. Middle reads only the scheduled uploaded page. High and ultra read up to 50,000 packed instance colors while retaining the global Dear Lie layer for continuity.

Hardware Impact: No measured microsecond claim. The correction preserves batching and removes a stale-read/zero-state risk. Frame Debugger and shader import proof are still pending; the latest CPU guard read 100%, so no build/import attempt was launched under the user's build-throttle rule.

## Published GPU Page Guard

Problem: The shader bridge still had a cold-start hazard. A `GraphicsBuffer` can exist before any packed color page has been uploaded. Publishing the desired schedule count or a default individual weight would let the shader read uninitialized GPU memory during the first frame. The desired scheduled count can also exceed the actual upload count because upload is clamped by `_activeGlowInstanceCount` and the vault buffer length.

Solution: Added `_publishedGpuColorCount`, initialized and reset to zero whenever the GPU page becomes invalid. `TryUploadGpuColorBufferFromLockedVault()` sets it to the exact `count` passed to `UnlockBufferAfterWrite`. `UploadShaderScalars()` publishes this actual uploaded count in `_GlobalBiolumParams.w` and publishes individual shader weight only when the uploaded count is greater than the four Dear Lie groups. The shader renamed the guard to `packedBufferCount` and refuses reads outside that exact range.

Rejected Alternatives: Trusting `GraphicsBuffer` default contents; exposing `_scheduledGpuColorCount` before upload completion; keeping stale front-buffer reads after DataVault handle refresh; forcing a full 50,000 upload to avoid count tracking.

Scalability potential: Low and cold start use global four-state emission only. Middle tiers expose only the uploaded page. High and ultra can expose the full 50,000 packed color path once upload proof exists.

Hardware Impact: No measured microsecond claim. This removes a correctness hazard that could produce random glow artifacts without changing material batching or adding allocations.

## Dear Lie Species Group Correction

Problem: The indirect vegetation shader's global fallback picked `_GlobalBiolumStates` by position-derived noise. The original SHINOBU task requires the four Dear Lie colors to be selected by species hash modulo 4. Position selection is visually plausible, but it can make identical species disagree and it drifts from the batch contract.

Solution: Added a `biolumSyncGroup` varying. The vertex path derives it from finite-guarded `TemplateIndex` modulo four, with a stable finite-guarded `Type + Variation` fallback when no template is authored. `ResolveIndirectVegetationGlobalBiolum` now receives this group and selects the global state by group modulo active count. High-tier overdrive still uses local position and time for shimmer, but the base Dear Lie color is species/template-group stable.

Rejected Alternatives: Keeping position noise; adding a material keyword or per-renderer property; demanding a new cross-domain species hash buffer from the world renderer; widening runtime DTOs outside the SHINOBU domain.

Scalability potential: Low tier gets stable species-group pulse identity with only four global states. High and ultra still overlay per-instance packed colors once the published GPU page is valid.

Hardware Impact: The sync group is now packed into existing `TEXCOORD21` beside the spatial pulse offset, so no extra varying lane remains. No CPU cost and no material batching cost. Shader import still needs Unity validation.

## Deterministic RNG Correction

Problem: Mock predator proximity firing used a custom deterministic hash. That avoided `UnityEngine.Random`, but the mandate explicitly requires `Unity.Mathematics.Random` seeded from sector/frame state for deterministic rollback-compatible random streams.

Solution: Added `CreateDeterministicRandom(uint sectorHash, uint frameCounter, uint salt)`, seeding `Unity.Mathematics.Random` from biome/profile sector hash plus `_frameCounter` through `default` plus `InitState(seed)` rather than a gameplay-path `new` constructor. `AdvanceMockPredatorSignal` now uses that struct RNG for roll, origin angle, radius, and wave radius. This remains a value type and does not allocate heap memory.

Rejected Alternatives: `UnityEngine.Random`, `System.Random`, custom hash-only random stream, or managed random objects.

Scalability potential: No visual tier change. Low through ultra all get the same deterministic mock signal stream for a given sector/frame.

Hardware Impact: Correctness gate for deterministic replay. No measured runtime gain claimed.

## Quality Curve Math Tightening

Problem: The scheduled glow count already came from continuous `GlobalQualityWeight`, but the final count mapping used raw multiplication. The mandate explicitly asks for `math.lerp`, `math.step`, and polynomial curves to express quality shedding.

Solution: `ResolveScheduledGlowCount` now computes `activeWeight = SmoothStep01(weight) * math.step(0.0001f, weight)` and then uses `math.lerp(4, 50000, activeWeight)`. This keeps the near-zero path collapsed to the four global Dear Lie groups while preserving continuous scaling once quality/stress permits individual sampling.

Rejected Alternatives: Raw linear multiplication; binary quality branch; hardware-tier enum; always scheduling full 50,000 because the GPU buffer exists.

Scalability potential: Low collapses deterministically to four groups. Middle grows through a polynomial ramp. High and ultra reach the full packed buffer path without a visual pop.

Hardware Impact: No measured microsecond claim. The mapping is more stable around thermal collapse and should reduce upload thrash at tiny quality weights.

## HZB Culling Boundary

Problem: The global polish mandate mentions HZB occlusion for 50,000 kelp plants. SHINOBU_74 does not own draw-list culling or matrix dispatch; editing that path would violate the domain boundary.

Solution: Static source check shows `HectonIndirectVegetationRenderer.cs` owns BRG/indirect vegetation culling, and `FloraCulling.compute` declares `_HectonDepthPyramid`, `_HectonOcclusionEnabled`, depth bias, and visible-instance append buffers. SHINOBU's packed color buffer is presentation data consumed by the existing indirect vegetation shader, not a draw-submission path.

Rejected Alternatives: Duplicating HZB culling in the glow runtime; adding CPU AABB culling inside SHINOBU; touching the world renderer without authorization.

Scalability potential: SHINOBU low tier still collapses glow work to four global pulses. The existing renderer culling remains the appropriate owner for hiding occluded flora before vertex work.

Hardware Impact: No new runtime work. This is a domain-boundary proof, not a performance claim.

## Shader Interpolator Packing

Problem: The first species-group correction added a separate `TEXCOORD22` scalar for `biolumSyncGroup`. Functionally correct, but wasteful on the mobile shader path because the existing `TEXCOORD21` spatial pulse offset had enough scalar capacity.

Solution: Replaced the two scalar varyings with `half2 biolumPulseData : TEXCOORD21`; `.x` carries the spatial pulse offset and `.y` carries the four-state Dear Lie sync group. Spore sparkle, local pulse phase, and global Dear Lie state selection now read from this packed pair.

Rejected Alternatives: Keeping a dedicated varying for one 0..3 group id; deriving group again in fragment from world position; adding a material keyword.

Scalability potential: Low/mobile avoids a new interpolator lane while preserving the four global pulse fake. High/ultra still receive stable group selection plus optional packed per-instance colors.

Hardware Impact: Reduces interpolator pressure in the indirect vegetation shader. Exact timing remains pending Unity shader import/profiler.

## Blackbox Published Count Correction

Problem: `BiolumPulseTelemetryEntry.ActiveGlowingInstances` reported `_scheduledGpuColorCount`. After adding `_publishedGpuColorCount`, scheduled count can exceed what the shader is allowed to sample if upload has not completed or the GPU page is invalid.

Solution: Telemetry now records the shader-visible glow count: `_publishedGpuColorCount` only when a valid uploaded page exists and exceeds the four Dear Lie groups; otherwise it reports `SyncGroupCount` = 4.

Rejected Alternatives: Leaving scheduled count in the blackbox; adding another telemetry field and keeping the misleading old one; blocking the frame to force publication before telemetry.

Scalability potential: Low reports the actual four-state fake; middle/high reports the real uploaded page size; ultra reports up to 50,000 only after upload proof.

Hardware Impact: Forensic accuracy only; no runtime savings claim.

## Deterministic Frame Clock Correction

Problem: `_frameCounter` advanced inside `RecordTelemetry()`. That made simulation-visible frame state depend on how many blackbox entries were written. A NaN/fault path can call `RecordTelemetry()` outside the normal Tick tail, which would perturb deterministic RNG seeding, shader frame clock, and mock predator `FrameStamp`.

Solution: Added `AdvanceSimulationFrameCounter()` and call it exactly once in `Tick()` after delta sanitization and before RNG/signal work. `RecordTelemetry()` now records the current frame without incrementing. `MockPredatorProximitySignal.FrameStamp` now uses the current frame rather than predicting the telemetry-incremented next frame.

Rejected Alternatives: Keeping telemetry as the frame owner; deriving frame from Unity `Time.frameCount`; adding a Core dependency for a new frame service; blocking fault telemetry until the next Tick.

Scalability potential: Low through ultra use the same deterministic frame authority. This affects correctness, not visual tier richness.

Hardware Impact: No runtime savings claim. It removes a rollback/forensics desync vector.

## Deterministic Burst Mode Correction

Problem: `BiolumVisualSyncJob` still used `FloatMode.Fast`. The job mutates `GlowStateDTO.Phase` and packed GPU color DTOs, and those values are recorded in telemetry and can be replayed or compared during rollback investigations. Fast-math freedom is the wrong default for this stateful visual synchronization kernel.

Solution: Changed only `BiolumVisualSyncJob` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`. The shader-side presentation overkill remains outside this CPU state authority.

Rejected Alternatives: Keeping `FloatMode.Fast`; hand-quantizing every sine path before writing DTOs; moving phase state into shader time only and losing deterministic CPU telemetry.

Scalability potential: Low through ultra preserve identical CPU pulse state for the same frame/sector inputs. High-tier richness remains shader-side and does not demand per-plant Unity Lights or material instances.

Hardware Impact: Potential ALU cost increase is accepted for rollback correctness. Exact Burst timing remains pending.

## Build Guard Recheck

Problem: After C# edits, a compile check was justified. The first CPU guard read was below 50% and no `dotnet`/`csc` process was active, but the narrow build returned exit 1 with empty quiet/errors-only output.

Solution: Did not guess. Waited for the spawned `dotnet/csc` processes to exit, then rechecked guard state. CPU remained at 99%, so no diagnostic rerun was launched.

Rejected Alternatives: Running repeated builds while CPU was saturated; claiming compile pass from an empty failed build; editing unrelated Core files.

Scalability potential: Not runtime scalability. This preserves the build-throttle rule and prevents compile-wall churn during parallel-agent work.

Hardware Impact: Developer hardware protection; no runtime microsecond claim.

## Transcendental Oscillator Fake

Problem: `BiolumVisualSyncJob` still paid `math.sin` inside the 50,000-instance path for the base oscillator, damage flicker chaos, and O2 heartbeat. The visual requirement is a believable glow pulse, not trigonometric truth.

Solution: Replaced per-plant sine pulses with `ResolveSmoothedTrianglePulse01()`, a triangle wave shaped by a cubic smoothstep polynomial. Replaced damage chaos sine with deterministic uint hash noise seeded by instance index, damage frame stamp, and a fixed time bucket. Replaced O2 heartbeat sine with the same smoothed triangle phase.

Rejected Alternatives: Keeping sine per instance; moving all pulse work to Unity Lights; adding shader/material variants; adding a lookup texture that would need streaming/warmup proof.

Scalability potential: Low tier already collapses to four global Dear Lie states. Middle/high/ultra individual paths now spend less CPU ALU per active plant while keeping visually readable pulse/flicker/heartbeat behavior.

Hardware Impact: Expected ALU reduction on Quest/MX350-class CPUs in the active individual path. Exact timing remains pending Burst Inspector/profiler.

## Sqrt-Free Wavefront Fake

Problem: Spatial pulse propagation and damage flicker still used `math.length()` inside the active 50,000-instance job. Exact Euclidean distance is not required for an emissive ripple/flicker presentation fake.

Solution: Replaced distance sqrt with squared-distance math. Spatial pulses compare `distanceSq` against the squared wave radius and derive shell width in squared units. Damage falloff uses `lengthsq / radiusSq` shaped by `SmoothStep01`. Both paths now finite-check localized AUP deltas and clamp denominators with `math.max(..., 0.0001f)`.

Rejected Alternatives: Keeping sqrt per plant; reducing active pulse count only; moving ripple truth to physics; using a texture lookup that would need shader warmup/import proof.

Scalability potential: Low remains four global pulses. Middle/high/ultra individual paths keep visually circular-ish ripples while avoiding sqrt ALU when pulses or damage are active.

Hardware Impact: Expected ALU reduction on active pulse/damage frames. Exact timing remains pending.

## Quality-Weighted Update Cadence

Problem: The individual count scaled with `GlobalQualityWeight`, but normal scheduling cadence was still `0` seconds, meaning per-frame job scheduling even when quality had collapsed to four Dear Lie groups. The mandate explicitly calls for update frequency to shed toward 5Hz under low quality.

Solution: Added `ResolveUpdateCadenceSeconds(globalQualityWeight, overloadHoldSeconds)`. It uses `SmoothStepRange01`, `math.lerp`, and a continuous overload scalar. Low quality maps toward `0.2s` (5Hz), high quality maps toward per-frame, and overload pressure blends toward the existing 15Hz interval. `UploadShaderScalars()` publishes the same cadence to `_GlobalBiolumClock.y`.

Rejected Alternatives: Fixed per-frame scheduling; binary low-end hardware branch; keeping overload as a hard boolean switch.

Scalability potential: Low/MX350 avoids up to 55 oscillator schedules per second. Middle scales smoothly. High/ultra can still schedule every rendered frame when quality permits.

Hardware Impact: Expected CPU scheduling/job overhead reduction on low quality. Exact profiler proof pending.

## Narrow Runtime Build Pass

Problem: The previous quality-cadence pass still had only static evidence and an older empty-output build failure in the log. That was not acceptable after code changes.

Solution: Rechecked the build guard, waited until no `dotnet`/`csc` process was active and CPU was below the user's 50% limit, then ran one narrow runtime build only: `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:minimal -m:1 /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly`. Result: PASS, 0 warnings, 0 errors, 00:00:16.04.

Rejected Alternatives: Running `dotnet rebuild`; running a full dependency build; launching an editor build without a fresh need; hiding the earlier failed quiet attempt.

Scalability potential: Not a runtime tier change. This confirms the runtime C# edits compile in the isolated Assembly-CSharp path while Unity import/profiler evidence remains pending.

Hardware Impact: Developer hardware protected by guard; no runtime microsecond saving claimed.

## H-PHI Array Purge And Matrix CBuffer

Problem: The runtime still carried two private managed bridge arrays: `_managedStates Vector4[16]` for `Shader.SetGlobalVectorArray` and `_csvWorkerScratch byte[16384]` for CSV file staging. That undermined the H-PHI claim and left a 16-slot shader array where the actual Dear Lie contract is four groups.

Solution: Removed `_managedStates`, `_GlobalBiolumStatesId`, and every `Shader.SetGlobalVectorArray` call. The four Dear Lie groups now live in `Matrix4x4 _dearLieGroupMatrix` and are published as `_GlobalBiolumDearLieGroups` (`float4x4`) to the shader. Removed the CSV worker thread and private byte array. CSV hot reload now locks `BiolumCsvScratch`, reads the file directly into the vault-owned NativeArray via `Span<byte>` over the native pointer, and parses that same buffer.

Rejected Alternatives: Keeping the managed `Vector4[]` because Unity's vector-array API was convenient; keeping `byte[]` because `FileStream.Read(byte[])` was familiar; expanding Core with a new buffer ID; duplicating shader state through both array and matrix paths.

Scalability potential: Low still uses four global pulses. Middle/high/ultra keep the individual packed color page path. The global fallback CBuffer is now exactly the four-group contract instead of a 16-slot managed upload bridge.

Hardware Impact: Removes one fixed 16KB managed CSV staging buffer and one fixed managed vector array from runtime ownership. No per-frame profiler claim until Unity capture.

## Compile Wall Assembly Split

Problem: SHINOBU runtime lived in Assembly-CSharp and the editor facade lived in the broad `Hecton8.Editor` assembly path. That contradicted the compile-wall mandate even though the source itself avoided sibling gameplay references.

Solution: Added `Assets/_Project/Scripts/VFX/Bioluminescence/Hecton8.VFX.Bioluminescence.Runtime.asmdef` and moved the editor facade under `Assets/_Project/Scripts/VFX/Bioluminescence/Editor` with `Hecton8.VFX.Bioluminescence.Editor.asmdef`. The editor assembly references only the SHINOBU runtime assembly and Unity.Mathematics. Runtime references Core.Contracts/Core/Core.Memory and Unity Burst/Collections/Jobs/Mathematics/Profiling; no AI, fauna, combat, world, physics, or sibling VFX assembly is referenced.

Rejected Alternatives: Modifying the global `Hecton8.Editor.asmdef`; leaving the files in predefined assemblies; adding direct references to flora/world renderer code.

Scalability potential: Not a runtime visual tier change. This protects iteration time and keeps the glow director isolated while retaining the GlobalRegistry/DataVault route.

Hardware Impact: Compile-wall hygiene only. Unity import/build proof is still pending because new asmdefs require Unity project regeneration.

## Orphan Meta Purge

Problem: After moving `BioluminescenceTunerWindow.cs` under `Assets/_Project/Scripts/VFX/Bioluminescence/Editor`, the old tracked `Assets/_Project/Scripts/Editor/BioluminescenceTunerWindow.cs.meta` remained on disk without the `.cs` file. Unity can preserve a stale script GUID and import noise from that orphan.

Solution: Deleted only the orphaned `.meta` file. The live editor facade keeps its domain-local `.meta` and lives under `Hecton8.VFX.Bioluminescence.Editor.asmdef`.

Rejected Alternatives: Recreating the old script path; leaving the dead GUID for Unity import; touching unrelated editor files already dirty from other agents.

Scalability potential: Not a runtime tier change. It protects compile/import hygiene for the SHINOBU editor facade.

Hardware Impact: Editor import hygiene only; no frame-time claim.

## Global Shader Consumer Compatibility

Problem: Runtime stopped publishing `_GlobalBiolumStates[16]` after the H-PHI purge, but active coral, kelp, sargassum, procedural-bio, GPUI, and leviathan shaders still sampled that retired vector array. That would leave old consumers reading stale/zero global pulse data while SHINOBU published only `_GlobalBiolumDearLieGroups`.

Solution: Converted every active biolum shader consumer under `Assets/_Project/Art/Shaders` from `_GlobalBiolumStates[16]` to `float4x4 _GlobalBiolumDearLieGroups`, and clamped active global state count to four rows. The runtime remains the single publisher through `Shader.SetGlobalMatrix`.

Rejected Alternatives: Reintroducing `Shader.SetGlobalVectorArray`; publishing both legacy and matrix globals; touching material instances; leaving stale fauna/flora shader reads because they were outside the narrow first shader file.

Scalability potential: Low stays O(4) with the Dear Lie matrix across all active biolum materials. Middle/high/ultra still layer per-instance packed color where the indirect vegetation shader has a valid published page.

Hardware Impact: 0us measured and no profiler claim. The fix prevents dead/stale glow presentation without adding CPU allocations, material mutation, or Unity Lights.

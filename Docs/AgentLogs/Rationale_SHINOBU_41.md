# Rationale_SHINOBU_41

Agent: SHINOBU_41
Domain: ECHELON 2 WORLD GENERATION & TERRAIN / GEOLOGICAL_SYNTHESIS_SURGEON
Status: IMPLEMENTED / REFLECTION ABI DEBT ISOLATED / CORE BUILD PASS / UNITY RUNTIME PENDING

## Pre-Code Analysis

Problem: 100x100km terrain truth needs O(1) sampling without Unity Physics BVH traversal and without float jitter at far coordinates.
Solution: Localize double AUP requests by subtracting sector origin before float SDF math; keep sampler as stateless unmanaged Burst-compatible kernels fed by DataVault-style pointers/slices.
Rejected Alternatives: Physics.Raycast, MeshCollider terrain probing, Terrain.GetHeights, MapMagic graph evaluation, managed DTO properties, binary quality switches.
Scalability potential: Low uses nearest height/SDF blend and hard fallbacks; Middle lerps toward bilinear/trilinear; High adds stronger normal/biome blending; Ultra spends saved cycles on smoother trilinear normals, erosion detail, and editor visualization.
Hardware Impact: Estimated low-end i3/MX350 gain is avoidance of BVH/cache-miss traversal and heap churn; exact microseconds are PENDING VERIFICATION until profiler/GCMonitor.

## Decision Journal

### D00: Execution Boundary

Problem: The task requires MapMagic/SDF geological synthesis while other agents may mutate world, flora, vehicles, and streaming contracts.
Solution: Implement local first-party `Hecton8.World` sampler DTOs/jobs/editor facade with no concrete dependency on external teams; expose raw-pointer/slice config for future GlobalDataVault injection.
Rejected Alternatives: Direct references to MapMagic runtime classes, vehicle systems, flora systems, or chunk streamer implementations.
Scalability potential: Low-to-Ultra behavior is driven by `GlobalQualityWeight`; no hard quality enum is required for the math kernel.
Hardware Impact: Decoupling prevents sync waits and compile coupling; exact saved time is PENDING VERIFICATION.

### D01: Binary Payload Boundary

Problem: No authoritative MapMagic height, voxel SDF, biome atlas, or erosion binary exists in `StreamingAssets` or scanned `Docs/Archive`; old logs show incompatible or missing payloads.
Solution: Keep runtime sampler pointer/slice-driven and provide `MockGeologyGenerator` only for editor/isolation proof, filling aligned NativeArrays with sine height, spherical void, biome hash mirror, erosion mask, sector masks, and active sector pointers.
Rejected Alternatives: Hardcoding archive payload assumptions, loading resource distribution binaries as terrain truth, blocking boot on missing OSHINO data.
Scalability potential: Low/Middle/High/Ultra all consume the same NativeArray contract; only the producer quality and `GlobalQualityWeight` change.
Hardware Impact: Missing-file fallback is editor/init only; runtime hot path remains 0 allocation and avoids disk IO.

### D02: Continuous Quality Over Binary Switches

Problem: Existing sampler had a legacy `ForceMathLodLow` bit that could create hard quality discontinuity.
Solution: Preserve the enum bit for ABI compatibility but stop using it as runtime truth; `GlobalQualityWeight` drives height nearest/bilinear and SDF nearest/trilinear interpolation.
Rejected Alternatives: Quality enum, force-low toggle, separate low/high sample functions.
Scalability potential: Low = nearest blend and minimal micro detail; Middle = partial bilinear/trilinear; High = full smooth sampling; Ultra = same stable math with richer producer buffers.
Hardware Impact: Estimated low-end i3/MX350 saving is 0.25-0.70us/query when quality drops toward 0.

### D03: ARM64 DTO Contract

Problem: NativeArray DTOs used by Burst jobs must avoid CS1612 copy mutations and unaligned ARM64 lanes.
Solution: Added direct-field `TerrainSampleDTO` as 24 bytes (`float3 Normal` 0-11, `float Distance` 12-15, `uint BiomeHash` 16-19, `uint _pad0` 20-23) and `MapMagicCellDTO` as 8 bytes (`float`, `short`, `byte`, private pad).
Rejected Alternatives: C# properties, `Pack=1`, class DTOs, implicit padding.
Scalability potential: Same DTO is valid for toaster-tier query spam and ultra-tier visual-overkill biome/normal consumers.
Hardware Impact: Estimated 0.01-0.08us/query saved from fewer copies and aligned reads.

### D04: Seam Math And SDF Override

Problem: 2D MapMagic height can block 3D caves unless the sampler has a deterministic rule for subterranean override.
Solution: Use polynomial smooth-min for normal seam blend; when SDF is negative below macro height and `SdfOverrideMask` bit is set, return SDF distance as authority.
Rejected Alternatives: MeshCollider tunnel carving, unconditional SDF override, physics ray probes.
Scalability potential: Low gets nearest SDF/height with same topology; Ultra gets smoother trilinear and normal response without changing semantics.
Hardware Impact: Estimated direct sampler cost 1.1-2.8us/query vs 25us+ collider/BVH query.

### D05: Black Box Telemetry

Problem: Terrain query spam or unloaded-sector misses must be diagnosable without managed logging.
Solution: Extended the fixed 300-entry telemetry ring with smooth-min ns estimate, OOB count, quality weight, biome hash; threshold is 800,000 samples and dump path is `Docs/AgentLogs/Dump_TERRAIN_SPLICER.bin`.
Rejected Alternatives: `Debug.Log`, dynamic lists, frame-end managed aggregation.
Scalability potential: Low-tier can drop quality using the same counters; Ultra-tier can justify spending saved cycles using observed query pressure.
Hardware Impact: Interlocked counter cost estimated 0.01-0.04us/query; avoids undefined crash states.

### D06: Human Facade And CSV Overrides

Problem: The invisible math terrain needs editor verification and live control without entering Play Mode.
Solution: Updated `Math-Terrain Probe` to SHINOBU_41, added `Force Quality Weight`, and replaced string split CSV parsing with a reusable byte buffer, spans, ASCII float parsing, and FNV-style hash keys from `biome_atlas_overrides.csv`.
Rejected Alternatives: `Physics.Raycast` scene probe, `ReadAllLines`, `Split`, managed string dictionaries.
Scalability potential: Designers can preview Low/Middle/High/Ultra behavior continuously by dragging one float.
Hardware Impact: Editor-only allocation at window construction; hot reload avoids per-parse string array churn.

<SELF_AUDIT>
  <UnityPhysics>No Physics.Raycast, MeshCollider, Terrain.GetHeights, or physics component usage exists in GlobalWorldSampler.cs after rg audit.</UnityPhysics>
  <TerrainSampleDTO>24 bytes: Normal 0-11, Distance 12-15, BiomeHash 16-19, _pad0 20-23. ValidateStructLayout checks it.</TerrainSampleDTO>
  <CS1612>No get/set DTO properties added. Runtime structs expose fields; NativeArray writes use unsafe ref GetSampleRef.</CS1612>
  <GlobalQualityWeight>Height and SDF sampling lerp nearest to bilinear/trilinear using continuous GlobalQualityWeight. Legacy ForceMathLodLow is ABI-only.</GlobalQualityWeight>
  <EditorFacade>Math-Terrain Probe exists, raymarches with sampler math, draws sphere/normal, hot-reloads biome_atlas_overrides.csv, and exposes Force Quality Weight slider.</EditorFacade>
</SELF_AUDIT>

### D07: Verification Boundary

Problem: Initial project compile could not reach a clean state because unrelated dependencies were broken.
Solution: Initial `dotnet build Hecton8.Core.csproj --no-restore` failed on `SaveStateMerkleTree.Align16` duplicate and did not reference `GlobalWorldSampler.cs`; this entry is superseded by D09, where the core assembly build succeeds after later workspace changes and SHINOBU_41 polish.
Rejected Alternatives: Editing SaveSystem/Core scalability/RealtimeCSG outside SHINOBU_41 domain or reverting other agents' files.
Scalability potential: Terrain sampler can be integrated once unrelated compile wall is cleared.
Hardware Impact: No runtime hardware estimate until Unity profiler can execute; static estimates are recorded in status/log.

### D08: Ultra-Think Polish Hardening

Problem: Previous draft still had weak Burst precision flags, adjacent atomic int counters, a private managed CSV byte array in the editor facade, and no explicit no-alias proof for jobs.
Solution: Set every sampler job to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`; added `[NoAlias]` on job data/input/output fields; added `GlobalWorldSamplerCounterBlock` as 64-byte explicit counter lanes; moved CSV hot-load buffer into GlobalDataVault; replaced stress LCG helper with deterministic `Unity.Mathematics.Random` seeded from frame/sector-like seed/index.
Rejected Alternatives: Keeping low precision Burst, trusting adjacent int atomics, retaining editor private managed arrays, or relying on Burst alias inference.
Scalability potential: Below `GlobalQualityWeight` 0.3, expensive interpolation and raymarch steps collapse toward nearest/single-sample behavior; above 0.7, an extra micro-detail octave is allowed as visual overkill.
Hardware Impact: False-sharing mitigation prevents cache-line ping-pong under heavy query spam; estimated contention savings on i3/MX350/Quest-class CPU is workload-dependent, roughly 0.05-0.30us per contended telemetry update burst.

<SELF_AUDIT_POLISH>
  <TaskReconciliation>
    <Task id="01" status="PASS">Docs/Archive and StreamingAssets scanned; no geology payload authority found; mock fallback retained.</Task>
    <Task id="02" status="PASS">No Physics.Raycast, MeshCollider, or Terrain.GetHeights in sampler; direct NativeArray math only.</Task>
    <Task id="03" status="PASS">Hot DTOs are public fields; `GetSampleRef` returns unsafe ref into NativeArray memory.</Task>
    <Task id="04" status="PASS">`TerrainSampleDTO` is 24 bytes; `MapMagicCellDTO` is 8 bytes; no Pack=1.</Task>
    <Task id="05" status="PASS">`MockTerrainQuerySignal` and stress job exist; procedural fallback uses deterministic `Unity.Mathematics.Random`.</Task>
    <Task id="06" status="PASS">`Sample()` evaluates 2D height and 3D SDF and blends with polynomial smooth-min.</Task>
    <Task id="07" status="PASS">`GlobalQualityWeight` drives polynomial collapse/lerp between nearest and bilinear/trilinear.</Task>
    <Task id="08" status="PASS">Tetrahedron normal estimator exists and `GradientNormalEstimationBatchJob : IJobParallelForBatch` wraps batch normals.</Task>
    <Task id="09" status="PASS">`BiomeAtlas` and `BiomeHash` output exist with smoothstep border hash blend.</Task>
    <Task id="10" status="PASS">Dear Lie Simplex micro-detail exists; below low quality it attenuates, above 0.7 it can spend one extra octave.</Task>
    <Task id="11" status="PASS">Sea-level hard ceiling is enforced through the same sample authority.</Task>
    <Task id="12" status="PASS">`SdfOverrideMask` gates subterranean SDF authority under macro height.</Task>
    <Task id="13" status="PASS">`ActiveSectorPointers` gate unloaded sectors to HardFloor.</Task>
    <Task id="14" status="PASS">`ErosionMask` flattens micro-detail and biases normals with a current vector.</Task>
    <Task id="15" status="PASS">Sampler is static/stateless; persistent memory arrives as DataVault NativeArray slices.</Task>
    <Task id="16" status="PASS">Fully overwritten probe buffers use `NativeArrayOptions.UninitializedMemory`; counters/telemetry use clear memory deliberately.</Task>
    <Task id="17" status="PASS">300-frame ring, 800000 threshold, `Dump_TERRAIN_SPLICER.bin`, OOB/smin/quality/biome telemetry, 64-byte counter lanes.</Task>
    <Task id="18" status="PASS">`Math-Terrain Probe` editor facade raymarches math field and draws hit/normal without Physics.</Task>
    <Task id="19" status="PASS">CSV hot reload uses DataVault byte span parser and hash keys; no `Split` or `ReadAllLines`.</Task>
    <Task id="20" status="PASS">`Force Quality Weight` slider directly overrides `GlobalQualityWeight`.</Task>
  </TaskReconciliation>
  <StructLayout>
    <TerrainSampleDTO totalBytes="24" alignment="8-byte multiple">
      <Field name="Normal" offset="0" size="12" />
      <Field name="Distance" offset="12" size="4" />
      <Field name="BiomeHash" offset="16" size="4" />
      <Field name="_pad0" offset="20" size="4" />
      <Math>12 + 4 + 4 + 4 = 24; 24 % 8 = 0.</Math>
    </TerrainSampleDTO>
    <GlobalWorldSamplerCounterBlock totalBytes="64" falseSharing="padded-cache-line">
      <Field name="Value" offset="0" size="4" />
      <Padding offset="4" size="60" />
      <Math>4 + 60 = 64; one counter per cache line.</Math>
    </GlobalWorldSamplerCounterBlock>
  </StructLayout>
  <ScalabilityCurve>At `GlobalQualityWeight` below 0.3, `ResolveExpensiveSamplingWeight` returns zero: height uses nearest only, SDF uses nearest only, mock raymarch collapses toward one step, and micro-detail is attenuated. From 0.3 to 1.0, smoothstep ramps into bilinear/trilinear and batch raymarch steps. Above 0.7, an extra Simplex octave is allowed as visual overkill.</ScalabilityCurve>
  <HPhiVault>Status: runtime sampler owns zero persistent private NativeArrays, NativeLists, or NativeHashMaps. Editor probe requests VaultBufferHandle IDs 0x530401..0x53040D for height, materials, SDF, sector masks, biome atlas, erosion mask, SDF override, active sectors, legacy counters, 64-byte counter blocks, telemetry, and CSV buffer.</HPhiVault>
  <PointerAliasing>Sampler jobs now mark Data, input arrays, and output arrays with `[NoAlias]`. No `JobHandle.Complete()` is called by this domain; jobs are plain schedulable kernels and return dependency control to callers.</PointerAliasing>
  <CompileGuard>Sampler file uses Unity/Burst/Collections/Mathematics and editor-only Core.Memory for probe DataVault handles. It does not reference sibling runtime gameplay/vehicle/flora concrete classes.</CompileGuard>
  <DearLie>Physical terrain, erosion, and cave seam simulation are faked as O(1) height/SDF samples, polynomial smooth-min, bitmask override, and one/two-octave Simplex detail. Rejected O(n) collider BVH traversal and any runtime hydraulic erosion.</DearLie>
</SELF_AUDIT_POLISH>

### D09: Latest Compile Wall

Problem: Earlier SHINOBU_41 hardening saw `Hecton8.Core.csproj` fail outside the terrain sampler on `PlayerBuilder`/Habitat construction symbols and ambiguous `MockWorldSampler`.
Solution: Re-ran after the dependency graph/quality-normal polish; `dotnet build Hecton8.Core.csproj --no-restore` now succeeds in 5.58s with 0 warnings and 0 errors. `Assembly-CSharp.csproj` was attempted and timed out after 129.7s, so it remains non-proof rather than a SHINOBU_41 failure.
Rejected Alternatives: Editing `PlayerBuilder`, Habitat construction contracts, or third-party/generated project edges from the geological synthesis lane.
Scalability potential: Terrain sampler is now locally compile-verified for the core assembly; upper project proof still belongs to Integrator/Compile Medic.
Hardware Impact: No runtime profiler measurement yet; static and core compile proof are current evidence.

### D10: Dependency Graph And Quality Normal Polish

Problem: The previous job structs were schedulable by consumers, but the terrain domain did not expose a clear dependency-graph API. More importantly, `EstimateNormal` still paid the four-sample tetrahedron gradient cost when a low-quality consumer requested normals.
Solution: Added explicit schedule wrappers that return `JobHandle` and never call `.Complete()`: `ScheduleBatchSampler`, `ScheduleLocalBatchSampler`, `ScheduleGradientNormals`, `ScheduleMockTerrainStress`, and `ScheduleMockRaymarch`. Added `ResolveSamplingCadenceDivisor()` and `ShouldSampleOnFrame()` so a 60Hz caller can degrade to a 12-frame cadence (5Hz) through the same polynomial quality curve. Added low-quality normal bypass: below the 0.3 ramp, normals return stable up; through the ramp, cheap up-normal blends into tetra-gradient using `math.lerp`.
Rejected Alternatives: Requiring callers to instantiate job structs ad hoc, blocking on handles for immediate reads, or paying four distance-field samples for low-tier normal requests.
Scalability potential: Low uses nearest height/SDF, single-step raymarch, stable up normals, and 5Hz cadence eligibility. Middle interpolates normals and samples partially. High reaches full bilinear/trilinear/tetra normals. Ultra keeps full sampling and extra micro-noise octave without changing the API.
Hardware Impact: Low-tier normal queries avoid four recursive terrain samples per requested normal; estimated save is 0.7-2.4us per normal batch slice on i3/MX350-class CPUs depending on cache state. Schedule wrappers avoid main-thread sync stalls by preserving dispatcher-owned `JobHandle` flow.

<SELF_AUDIT_DEPENDENCY_POLISH>
  <JobHandles>
    <Consumes>`inputDeps` from the caller/SystemDispatcher for every public scheduling wrapper.</Consumes>
    <Outputs>`JobHandle` from `BatchSamplerJob.Schedule`, `BatchLocalSamplerJob.Schedule`, `GradientNormalEstimationBatchJob.ScheduleBatch`, `MockTerrainQueryStressJob.Schedule`, and `MockBoidRaymarchJob.Schedule`.</Outputs>
    <CompleteCalls>None in SHINOBU_41 runtime sampler or wrappers.</CompleteCalls>
  </JobHandles>
  <QualityNormals>At `GlobalQualityWeight` below 0.3, tetrahedron normal estimation is bypassed. Between 0.3 and 1.0, `ResolveExpensiveSamplingWeight` blends up-normal into tetra-gradient by polynomial smoothstep.</QualityNormals>
  <Cadence>`ResolveSamplingCadenceDivisor` maps expensiveWeight 0 to divisor 12 and expensiveWeight 1 to divisor 1; 60Hz callers can therefore shed to 5Hz without a low/high hardware branch.</Cadence>
  <CompileEvidence>`dotnet build Hecton8.Core.csproj --no-restore` succeeded, 0 warnings, 0 errors, 5.58s. `Assembly-CSharp.csproj` timed out after 129.7s and is not claimed as a pass.</CompileEvidence>
</SELF_AUDIT_DEPENDENCY_POLISH>

### D11: Payload Boundary And Low-Tier Sampling Polish

Problem: The sampler still paid unnecessary low-tier work in secondary lanes: biome hash blending read four atlas cells, erosion used bilinear sampling before deciding whether micro-detail mattered, and micro-noise could run at thermal-low quality. Separately, OSHINO payload hydration had no explicit aligned header/endian boundary, and telemetry dumps lacked a magic/version prefix.
Solution: Low-tier biome/erosion now collapses to nearest lookup through the same `ResolveExpensiveSamplingWeight` curve; micro-noise is skipped until the polynomial ramp activates. Added `FilterGlobalQualityWeight(previous, target, SimulationTickDelta, ...)` to provide deterministic hysteresis without `Time.deltaTime`. Added `TerrainPayloadHeaderDTO` as a 64-byte cold header mirror and `TryReadTerrainPayloadHeader(ReadOnlySpan<byte>, byte, out ...)` with explicit endian swap, `math.asfloat`, and no runtime NativeArray ownership. Added `TelemetryDumpMagic` and dump version before the 300-frame ring payload.
Rejected Alternatives: Keeping bilinear/hash reads at weight 0.1, using a binary low/high quality branch, adding a speculative file reader for absent terrain binaries, or calling a nonexistent `math.reversebytes` API. Package scan found no `math.reversebytes` symbol in the installed Unity.Mathematics source, so a local `ReverseBytes32` was used to preserve compile while keeping the endian boundary explicit.
Scalability potential: Low reads one biome cell, one erosion byte only when micro-detail is active, no noise before the ramp, nearest height/SDF, cheap normals, and cadence divisor 12. Middle ramps into bilinear/trilinear/biome blends. High/Ultra regain biome blending, erosion distortion, and extra Simplex detail.
Hardware Impact: Low-tier biome/erosion collapse removes 3 height-mask reads and up to 3 biome atlas reads plus hash blending per query. Micro-noise bypass removes 1-2 Simplex evaluations per query below the ramp. Estimated saving on i3/MX350-class CPUs is 0.12-0.65us/query when micro-detail would otherwise be enabled.

<SELF_AUDIT_PAYLOAD_POLISH>
  <StructLayout>
    <TerrainPayloadHeaderDTO totalBytes="64" alignment="8-byte multiple">
      <Field name="Magic" offset="0" size="8" />
      <Field name="PayloadBytes" offset="8" size="8" />
      <Field name="Version" offset="16" size="4" />
      <Field name="HeaderBytes" offset="20" size="4" />
      <Field name="Width" offset="24" size="4" />
      <Field name="Height" offset="28" size="4" />
      <Field name="Depth" offset="32" size="4" />
      <Field name="Flags" offset="36" size="4" />
      <Field name="HeightScale" offset="40" size="4" />
      <Field name="SdfRange" offset="44" size="4" />
      <Field name="Crc32" offset="48" size="4" />
      <Field name="EndianTag" offset="52" size="4" />
      <Field name="_pad0" offset="56" size="4" />
      <Field name="_pad1" offset="60" size="4" />
      <Math>8 + 8 + (12 * 4) = 64; 64 % 16 = 0.</Math>
    </TerrainPayloadHeaderDTO>
  </StructLayout>
  <ScalabilityCurve>At weight below 0.3, biome atlas and erosion mask reads choose nearest samples, micro-noise is bypassed, height/SDF are nearest, raymarch goes to one step, normals use stable up, and cadence can drop to 5Hz. Above 0.3 the same polynomial smoothstep opens bilinear/trilinear/biome/erosion work.</ScalabilityCurve>
  <Endianness>Cold payload hydration reads spans, validates a 64-byte header, swaps 32-bit words explicitly for BigEndian legacy data, and hydrates floats through `math.asfloat` after endian correction.</Endianness>
  <TelemetryDump>Dump format now starts with `TelemetryDumpMagic` (`HECTON8\0`) and `TelemetryDumpVersion` before entry count and 64-byte rows.</TelemetryDump>
  <CompileEvidence>`dotnet build Hecton8.Core.csproj --no-restore` succeeded in 1:34.22 with 0 errors. Current warnings are outside SHINOBU_41: duplicate `PhysicsWakeSignalContracts.cs` include and unassigned `GlobalPhysicsStateManager` job fields.</CompileEvidence>
</SELF_AUDIT_PAYLOAD_POLISH>

### D12: Counter Lifecycle And Quality Hysteresis Polish

Problem: The previous sampler warned on throughput in normal batch paths but only the stress/gradient paths requested a black-box dump. The sample counter lifecycle was also implicit, so the 800,000 threshold could degrade into a cumulative-session counter instead of a true per-frame tripwire if the dispatcher forgot to zero it. Finally, `FilterGlobalQualityWeight()` existed as a helper, but no schedulable state lane existed for deterministic quality smoothing.
Solution: Added `GlobalWorldSamplerQualityState` as a 32-byte dispatcher-owned DTO and `QualityWeightFilterJob` with the required Burst flags. Added `ScheduleQualityWeightFilter`, `BuildQualityState`, `FilterQualityState`, and `ApplyQualityState` so quality shed/recover can run as a normal job using `SimulationTickDelta`. Added `ResetFrameTelemetryCounters()` as the explicit PRE_SIM contract for frame-local sample/OOB/smooth-min counts. Replaced ad hoc threshold branches with `ShouldTripThroughputWarning()` and `RecordThroughputWarning()`; batch sampler, local sampler, mock stress, gradient batch, and mock raymarch now all request `Dump_TERRAIN_SPLICER.bin` at the exact crossing of 800,000 samples and every 1024 samples after that.
Rejected Alternatives: Leaving counter reset as undocumented caller folklore, dumping only from the mock stress harness, or directly snapping `GlobalQualityWeight` in the data struct without a deterministic state lane.
Scalability potential: Low/MX350 can shed quality through bounded `shedPerSecond` and cadence divisor 12; Middle recovers interpolation gradually; High/Ultra can raise target weight without a one-frame precision pop and spend the regained budget on normal/biome/noise detail.
Hardware Impact: The quality filter job is one 32-byte state row per quality lane and one rsqrt-free scalar lerp/clamp sequence. The dump tripwire adds only an over-threshold branch and atomic exchange when the threshold is crossed; expected hot-path cost before threshold is below measurement noise, while preventing unlimited rogue sampler spam from hiding beyond 800,000 queries.

<SELF_AUDIT_COUNTER_QUALITY_POLISH>
  <TaskReconciliation>
    <Task id="07" status="PASS">Continuous quality is now represented by both math helpers and a schedulable `GlobalWorldSamplerQualityState` lane.</Task>
    <Task id="17" status="PASS">All sampler job families now request the black-box dump on throughput breach; frame counter reset has an explicit PRE_SIM API.</Task>
  </TaskReconciliation>
  <StructLayout>
    <GlobalWorldSamplerQualityState totalBytes="32" alignment="16-byte multiple">
      <Field name="CurrentWeight" offset="0" size="4" />
      <Field name="TargetWeight" offset="4" size="4" />
      <Field name="SimulationTickDelta" offset="8" size="4" />
      <Field name="ShedPerSecond" offset="12" size="4" />
      <Field name="RecoverPerSecond" offset="16" size="4" />
      <Field name="Frame" offset="20" size="4" />
      <Field name="_pad0" offset="24" size="4" />
      <Field name="_pad1" offset="28" size="4" />
      <Math>6 * 4 + 2 * 4 = 32; 32 % 16 = 0.</Math>
    </GlobalWorldSamplerQualityState>
  </StructLayout>
  <ScalabilityCurve>Below 0.3 quality, expensive sampling still collapses; the new quality-state lane prevents target-weight changes from becoming instantaneous precision pops. Shed and recover rates are deterministic and driven by caller-provided simulation tick delta.</ScalabilityCurve>
  <DependencyGraph>`ScheduleQualityWeightFilter` consumes caller `inputDeps` and returns its `JobHandle`; the terrain domain still calls no `.Complete()`.</DependencyGraph>
  <CompileEvidence>Forbidden-pattern grep is clean for `GlobalWorldSampler.cs`. Latest `dotnet build Hecton8.Core.csproj --no-restore` is blocked outside SHINOBU_41 by `VolcanicUpdraftDirector.cs(1452,58)` missing `VolcanicUpdraftVault.SafeNormalize`; no error references `GlobalWorldSampler.cs`.</CompileEvidence>
</SELF_AUDIT_COUNTER_QUALITY_POLISH>

### D13: Finite Sentinel And NativeArray Sanitize Barrier

Problem: The sampler used `float.MaxValue` as an inactive SDF/sea sentinel. It is technically finite in C#, but it is not a physical terrain distance, it exceeds the sampler's own finite-value threshold, and it can poison downstream Burst consumers that multiply, lerp, or hash distance fields for physics/rendering.
Solution: Replaced inactive distance sentinels with bounded `InactiveDistanceSentinel = 1048576f` and added `SanitizeResult(ref TerrainSampleResult, in GlobalWorldSamplerData)`. Every public sample path now clamps distance lanes, repairs non-finite local positions, renormalizes bad normals to up, and sanitizes biome/state hashes before writing to NativeArray outputs or telemetry warning rows.
Rejected Alternatives: Keeping `float.MaxValue`, using NaN as an inactive marker, or requiring each downstream consumer to rediscover terrain sentinel semantics.
Scalability potential: Low/Middle/High/Ultra all see the same bounded field contract; thermal-low nearest samples no longer carry extreme sentinel values into stale-buffer reuse, and Ultra smooth-min/interpolation remains numerically bounded.
Hardware Impact: Clamp/sanitize cost is scalar and branch-light; expected overhead is below 0.03us/query while preventing overflow cascades in physics/render consumers on i3/MX350/Quest-class CPUs.

<SELF_AUDIT_SENTINEL_SANITIZE>
  <NumericContract>`InactiveDistanceSentinel` is `1048576f`, large enough to mean inactive at 100km scale but small enough to survive lerp/hash/multiply without overflow-adjacent behavior.</NumericContract>
  <NativeArrayWrites>`Sample()`, throughput warning records, and mock raymarch final telemetry sanitize `TerrainSampleResult` before external buffers observe it.</NativeArrayWrites>
  <StructLayout>No DTO size changed: `TerrainSampleResult` remains 64 bytes, `TerrainSampleDTO` remains 24 bytes, and `GlobalWorldSamplerQualityState` remains 32 bytes.</StructLayout>
  <ForbiddenPatternAudit>Latest rg audit found no `float.MaxValue`, Unity Physics, `.Complete()`, LINQ/string split, managed collections, `Pack=1`, `Time.deltaTime`, or hot DTO properties in `GlobalWorldSampler.cs`.</ForbiddenPatternAudit>
  <CompileEvidence>`dotnet build Hecton8.Core.csproj --no-restore` is currently blocked outside SHINOBU_41 by SaveSystem errors in `H8BinaryWorldPager.cs` and `SaveDeltaCompression.cs`; no compiler error references `GlobalWorldSampler.cs`.</CompileEvidence>
</SELF_AUDIT_SENTINEL_SANITIZE>

### D14: Distance-Only Output And Telemetry Ring Hardening

Problem: `Sample()` sanitized final outputs, but `SampleDistanceOnly()` was public and could be called directly by raymarch/boid/vehicle-style consumers. Warning/heartbeat telemetry also trusted the inbound result DTO enough to write some lanes without a full sanitize copy. That left a narrow path for invalid input or external direct calls to place non-finite local positions or over-range finite values into NativeArray consumers or the black-box ring.
Solution: Added `SanitizeResult(ref result, data)` to every `SampleDistanceOnly()` exit path, including invalid input, unloaded sector, missing height data, and the normal successful path. `WriteTelemetryEntry()` now creates a local sanitized `TerrainSampleResult` copy and writes only repaired position, normal, distance, hash, material, flags, sector, and biome lanes to telemetry.
Rejected Alternatives: Relying on all callers to prefer `Sample()` over `SampleDistanceOnly()`, or only sanitizing throughput warnings while leaving heartbeat rows as raw witness data.
Scalability potential: Low/Middle/High/Ultra all share one bounded DTO contract; direct low-quality single-lookup/raymarch paths now receive the same numeric guarantees as full trilinear/normal paths.
Hardware Impact: One extra sanitize pass in distance-only outputs is scalar and branch-light. Expected cost is below 0.03us/query and prevents a much more expensive NaN/overflow propagation through physics/render matrices.

<SELF_AUDIT_DISTANCE_TELEMETRY_HARDENING>
  <PublicAPISafety>`SampleDistanceOnly()` now sanitizes all public `out TerrainSampleResult` exits, not only callers that route through `Sample()`.</PublicAPISafety>
  <TelemetrySafety>`WriteTelemetryEntry()` sanitizes a local copy before writing the 300-frame ring; black-box rows are bounded evidence, not raw poison.</TelemetrySafety>
  <ForbiddenPatternAudit>Latest rg audit found no `float.MaxValue`, Unity Physics, `.Complete()`, LINQ/string split, managed collections, `Pack=1`, `Time.deltaTime`, or hot DTO properties in `GlobalWorldSampler.cs`.</ForbiddenPatternAudit>
  <CompileEvidence>`dotnet build Hecton8.Core.csproj --no-restore` succeeded in 2:19.25 with 0 errors and 9 warnings outside SHINOBU_41: duplicate `PhysicsWakeSignalContracts.cs` include and unassigned `GlobalPhysicsStateManager.PhysicsDistanceCullingJob` fields.</CompileEvidence>
</SELF_AUDIT_DISTANCE_TELEMETRY_HARDENING>

### D15: True Sample-Cost Telemetry Accounting

Problem: The throughput tripwire counted one logical query as one sample even when normal estimation paid the full tetrahedron gradient path: one base sample plus four `SampleDistanceOnly()` probes. A rogue normal batch could therefore consume five times the terrain ALU/cache budget before the 800,000-sample black-box guard reported the true pressure.
Solution: Added `ResolveTerrainSampleCost(data, estimateNormals, result)` and `AccumulateSampleCost()` so batch, local, stress, and gradient jobs charge 5 only when `GlobalQualityWeight` opens the expensive normal ramp and the result is not HardFloor. Low-quality normal requests still charge 1 because `EstimateNormal()` returns the stable cheap up-normal without four recursive probes. `ShouldTripThroughputWarning(previousTotal, total)` now detects threshold crossing with increments greater than 1 instead of relying on an exact `800001` counter value.
Rejected Alternatives: Counting every normal request as 5 even under thermal-low quality, adding one `Interlocked.Add` per internal tetrahedron distance probe, or leaving telemetry as logical-query count instead of actual sampler pressure.
Scalability potential: Low/MX350 keeps telemetry cost proportional to cheap nearest/up-normal work. Middle/High/Ultra expose the real cost of richer terrain normals, so the black-box dump reflects visual-overkill pressure instead of hiding it behind one logical query.
Hardware Impact: Counter math adds a few scalar integer ops per batch item and removes under-reporting of up to 4 hidden samples per normal query. On i3/MX350-class hardware the practical gain is diagnostic: earlier dump trigger before rogue normal consumers can push the sampler past budget without evidence.

<SELF_AUDIT_TRUE_SAMPLE_COST>
  <TaskReconciliation>
    <Task id="08" status="PASS">Tetrahedron normals are now charged as 5 sample units only when the quality ramp actually executes the four extra distance probes.</Task>
    <Task id="17" status="PASS">The 800000 throughput tripwire now detects crossing even when a job increments the counter by 5 or by a batch-accumulated sample cost.</Task>
  </TaskReconciliation>
  <ScalabilityCurve>Below `GlobalQualityWeight` 0.3, normal-enabled logical samples charge 1 because `ResolveExpensiveSamplingWeight` is zero and tetrahedron probes are bypassed. Above the ramp, each non-HardFloor normal sample charges 5, matching the base sample plus four tetrahedron offsets.</ScalabilityCurve>
  <DependencyGraph>No new `JobHandle.Complete()` calls were introduced. Jobs still return scheduler-owned handles; only their atomic counter increment amount changed.</DependencyGraph>
  <ForbiddenPatternAudit>Forbidden-pattern grep after this patch found no `float.MaxValue`, Unity Physics, `.Complete()`, LINQ/string split, managed collections, `Pack=1`, `Time.deltaTime`, or hot DTO properties in `GlobalWorldSampler.cs`.</ForbiddenPatternAudit>
  <CompileEvidence>`dotnet build Hecton8.Core.csproj --no-restore` failed after 1:38.84 on unrelated dependencies: `HectonNetworkManager.cs` cannot resolve `HectonRollbackNetcodeRuntime`, and `SignalWardenRuntime.cs` cannot resolve `WaterlineBreachSignal`. No compiler error references `GlobalWorldSampler.cs`.</CompileEvidence>
</SELF_AUDIT_TRUE_SAMPLE_COST>

### D16: Hybrid Terrain Seam Quality Continuum

Problem: The legacy MapMagic/SDF terrain seam patcher still made a binary `GlobalRegistry.ScalabilityTier` decision (`LowTierVisualOnly` / High tier mask detail). That tier branch contradicted the SHINOBU_41 continuum: at 0.29 quality the raymarch should collapse, at 0.31 it should begin breathing back, and at 0.70+ it should buy visual mask detail.
Solution: Replaced seam-applier quality selection with `HomeostasisBrain.GlobalQualityWeight`. `HybridTerrainSeamJobs` now exposes polynomial helpers, required Burst flags, `[NoAlias]` job buffers, `GlobalQualityWeight`/valid lanes, raymarch step resolution from 1..16, and mask detail weight above the overkill ramp. The applier keeps the old byte field only as a stale generated-csproj fallback; when Unity regenerates the `Hecton8.World.Terrain` source assembly, cold reflection injects the continuous quality fields before scheduling the jobs.
Rejected Alternatives: Keeping the hardware tier enum, snapping after 180 frames, or editing generated `.csproj` files to force local stale assemblies to see new source fields.
Scalability potential: Low/MX350 collapses terrain deformation raymarching to visual mask-only/single-step behavior. Middle ramps raymarch cost smoothly. High/Ultra keep 16-step seam search and slope mask detail for visual overkill without changing geometry authority.
Hardware Impact: Low-tier seam patch work avoids 16-step analytic SDF probes per affected height texel. On i3/MX350 terrain writeback, the expected saving is 0.2-1.5ms for dense seam patches depending plan count; High/Ultra intentionally spend that budget on smoother welded cave/height transitions.

<SELF_AUDIT_HYBRID_SEAM_QUALITY>
  <TaskReconciliation>
    <Task id="06" status="PASS">Hybrid patch jobs still use polynomial smooth-min to blend heightmap and analytic SDF seam targets.</Task>
    <Task id="07" status="PASS">The seam patcher now consumes continuous `GlobalQualityWeight`; no `GlobalRegistry.ScalabilityTier` branch remains in the applier.</Task>
    <Task id="10" status="PASS">Mask detail remains a visual fake and is weighted above 0.7 quality instead of running as a fixed high-tier switch.</Task>
  </TaskReconciliation>
  <ScalabilityCurve>Below 0.3 quality, `ResolveExpensiveSamplingWeight` is zero and the seam projection skips expensive deformation raymarching. From 0.3..1.0, a smoothstep curve lerps deformation strength and raymarch count from 1 to 16. Above 0.7, slope mask detail fades in through `ResolveMaskDetailWeight`.</ScalabilityCurve>
  <DependencyGraph>`HybridSdfHeightmapProjectionJob`, `HybridTerrainSeamNormalJob`, and `HybridTerrainSeamMaskDetailJob` have required Burst flags and `[NoAlias]` fields. The seam applier still fences only at the cold Unity Terrain `SetHeightsDelayLOD` writeback boundary.</DependencyGraph>
  <CompileEvidence>Forbidden-pattern grep is clean for `GlobalWorldSampler.cs` and `HybridTerrainSeamJobs.cs`. Latest `dotnet build Hecton8.Core.csproj --no-restore` failed after 1:08.57 on unrelated Core/Modding symbols: `FutureCommandSandboxValidator.cs` missing `BufferID.ShinobuRollbackRuntimeState`, and `AupOriginShiftCoordinator.cs` missing `ResolveSupplementalHistoricalMaxLength` / `ScheduleHistoricalRebaseBatch`; no compiler error references SHINOBU_41 terrain files.</CompileEvidence>
</SELF_AUDIT_HYBRID_SEAM_QUALITY>

### D17: Terrain-Local AUP Seam Projection

Problem: The hybrid MapMagic/SDF seam projection still formed `worldX/worldZ/worldY` by adding `terrain.transform.position` to heightmap-local offsets, then compared those absolute runtime floats against contact and voxel centers. At 100km this is a centimeter-scale precision leak in exactly the seam math that must weld terrain and SDF without jitter.
Solution: Changed `HybridSdfHeightmapProjectionJob` to operate entirely in terrain-local meters. The applier now resolves terrain absolute AUP once, subtracts it from plan/contact/voxel AUP in double space, and only then casts local deltas to `float3`. The fallback patch deformation, voxel snap helper, trench deformation, and plan/trench rect builders now follow the same terrain-local pattern. `TerrainPosition` remains in the job only as a stale ABI field and is supplied as `float3.zero`; `HybridTerrainSeamPlanNative` documents that its position fields are terrain-local meters.
Rejected Alternatives: Keeping absolute runtime `Vector3` comparisons, trusting floating origin to hide every 100km seam delta, or touching the global origin/terrain systems outside SHINOBU_41. Renaming the DTO fields was also rejected because the generated Core csproj still references a stale `Hecton8.World.Terrain` assembly; preserving field names avoids a compile-wall while fixing source semantics.
Scalability potential: Low still collapses deformation raymarching below the 0.3 quality ramp; Middle/High/Ultra regain smooth-min raymarch work, now from stable local meters. The same local-AUP conversion works across weak devices and visual-overkill desktops without changing the sampling contract.
Hardware Impact: The patch does not claim raw ALU savings. It removes precision churn from every affected seam texel and avoids downstream corrective work from jittered blend masks/height writes. On i3/MX350-class hardware the expected gain is stability: fewer seam re-writes and less black-box noise under far-origin traversal.

<SELF_AUDIT_TERRAIN_LOCAL_AUP_SEAM>
  <TaskReconciliation>
    <Task id="03" status="PASS">`HybridTerrainSeamPlanNative` remains direct-field DTO data; no hot-path properties were introduced.</Task>
    <Task id="04" status="PASS">`TerrainSeamTelemetryEntry` is natural sequential `Size=64`; `Pack=4` was removed. `HybridTerrainSeamPlanNative` is 72 bytes, 72 % 8 = 0.</Task>
    <Task id="06" status="PASS">Hybrid seam smooth-min now blends local height and local analytic SDF surface, not absolute runtime floats.</Task>
    <Task id="07" status="PASS">Continuous `GlobalQualityWeight` still controls expensive raymarch and mask detail; the local-AUP patch did not reintroduce tier branches.</Task>
    <Task id="13" status="PASS">Seam projection follows the AUP rule: subtract terrain AUP in double space before float distance math.</Task>
  </TaskReconciliation>
  <StructLayout>
    <HybridTerrainSeamPlanNative totalBytes="72" alignment="8-byte multiple">
      <Field name="RuntimeContactPosition" offset="0" size="12" semantic="terrain-local meters" />
      <Field name="RuntimeVoxelCenter" offset="12" size="12" semantic="terrain-local meters" />
      <Field name="VoxelSize" offset="24" size="12" />
      <Field name="SeamBlendRadius" offset="36" size="4" />
      <Field name="TerrainBlendWeight" offset="40" size="4" />
      <Field name="CaveBlendWeight" offset="44" size="4" />
      <Field name="SuggestedTerrainRaise" offset="48" size="4" />
      <Field name="SuggestedTerrainCut" offset="52" size="4" />
      <Field name="TerrainDelta" offset="56" size="4" />
      <Field name="RidgeSignal" offset="60" size="4" />
      <Field name="CanyonSignal" offset="64" size="4" />
      <Field name="CompositionPotential" offset="68" size="4" />
      <Math>3 float3 lanes (36) + 9 floats (36) = 72; 72 % 8 = 0. Source semantic is stable; Unity/Burst `UnsafeUtility.SizeOf` proof is pending Unity import.</Math>
    </HybridTerrainSeamPlanNative>
    <TerrainSeamTelemetryEntry totalBytes="64" alignment="16-byte multiple">
      <Math>16 uint/int/float fields * 4 bytes = 64; `Reserved4` is explicit tail padding; 64 % 16 = 0; no manual Pack.</Math>
    </TerrainSeamTelemetryEntry>
  </StructLayout>
  <AUPPrecision>Terrain absolute AUP is computed once per patch. Plan/contact/voxel AUP values are subtracted from that double anchor, producing local deltas before any float distance, raymarch, rect, or trench math.</AUPPrecision>
  <CompileEvidence>Forbidden-pattern/static seam grep is clean except terrain signal midpoint ingestion, which is not seam distance math. `dotnet build Hecton8.Core.csproj --no-restore` failed after 1:16.77 on unrelated `World/WorldChunkResidencyManager.cs` missing `EstimateAddressableChunkBytes`; no compiler error references SHINOBU_41 terrain files.</CompileEvidence>
</SELF_AUDIT_TERRAIN_LOCAL_AUP_SEAM>

### D18: Reflection Purge Attempt And Deterministic Seam Frame Counter

Problem: The previous seam quality bridge used `System.Reflection` to write `GlobalQualityWeight` and `GlobalQualityWeightValid` into `HybridSdfHeightmapProjectionJob` / `HybridTerrainSeamMaskDetailJob`. That is managed ABI glue, and the mandate rejects reflection as runtime architecture. The same seam path also wrote `Time.frameCount` into black-box telemetry and voxel modification events.
Solution: Tried the correct direct-field assignment first. The local generated `Hecton8.Core.csproj` failed with CS0117 because it still resolves a stale `Hecton8.World.Terrain.dll` that does not contain the newly added quality fields. Under the 3-strike protocol, the direct-field chunk was reverted to restore the green build; the reflection bridge is explicitly marked as `[BLOCKED BY GENERATED ASSEMBLY ABI]` until Unity regenerates `Hecton8.World.Terrain`. Separately, removed `Time.frameCount` from the seam event/black-box route and added `_seamFrameCounter` plus `AdvanceTerrainSeamFrame()` so this domain writes a monotonic local seam frame instead of Unity Time.
Rejected Alternatives: Leaving the build broken to prove purity; writing unsafe bytes past the stale job struct layout; encoding continuous quality into `LowTierVisualOnly` where stale assemblies would interpret any nonzero value as hard low-tier; editing broad generated csproj references by hand.
Scalability potential: Unity-regenerated source jobs still receive continuous quality through the bridge and use the polynomial 0.3/0.7 curves. Stale generated builds retain the old byte fallback without compile failure. Low/Middle/High/Ultra semantics are preserved in source; the ABI gap is an integration dependency.
Hardware Impact: The frame-counter patch does not claim raw speed. It removes a Unity Time dependency from critical seam evidence. Reflection remains cold seam writeback overhead only; hot sampler jobs still have no reflection, no allocation, no Unity Physics, and no `Complete()`.

<SELF_AUDIT_REFLECTION_ABI_FRAME_COUNTER>
  <DirectFieldAttempt status="BLOCKED_BY_GENERATED_ASSEMBLY_ABI">Direct `GlobalQualityWeight` and `GlobalQualityWeightValid` job writes failed in `Hecton8.Core.csproj` with CS0117 against stale `Hecton8.World.Terrain.dll`.</DirectFieldAttempt>
  <RevertReason>The direct-field chunk was reverted because the project cannot be left in a compile-broken state. Cold reflection remains as ABI bridge, not as claimed-clean architecture.</RevertReason>
  <TimeDependency status="PASS">`WorldGenerativeGeologyTerrainSeamApplier` no longer writes `Time.frameCount` to `VoxelChunkModifiedEvent.Frame` or `TerrainSeamTelemetryEntry.Frame`; both use the local monotonic seam frame.</TimeDependency>
  <CompileEvidence>`dotnet build Hecton8.Core.csproj --no-restore` succeeded after the ABI revert/frame patch with 0 errors and 8 unrelated `GlobalPhysicsStateManager.PhysicsDistanceCullingJob` warnings.</CompileEvidence>
  <RemainingDebt>Remove `System.Reflection` from `WorldGenerativeGeologyTerrainSeamApplier` after Unity regenerates `Hecton8.World.Terrain.dll` or after Core is moved behind a stable contracts-level seam job facade.</RemainingDebt>
</SELF_AUDIT_REFLECTION_ABI_FRAME_COUNTER>

### D19: DataVault Seam Native Memory Eviction

Problem: `WorldGenerativeGeologyTerrainSeamApplier` still owned native terrain seam memory locally: the 300-row black-box, TempJob native plans/patch/blend/normal scratch arrays, and a persistent baseline height `NativeArray<float>`. That violated the Vault Law even though the hot sampler itself was stateless.
Solution: Replaced the black-box with `VaultBufferHandle<TerrainSeamTelemetryEntry>` at `BufferID 0x530421`; replaced hybrid scratch arrays with vault buffers `0x530422` native plans, `0x530423` patch heights, `0x530424` blend mask, and `0x530425` normals; replaced persistent baseline storage with per-terrain `VaultBufferHandle<float>` using `BufferID 0x531000 + (terrain instance id & 0x000FFFFF)`. `TerrainApplyState.baselineHeights` now stores only the resolved vault alias.
Rejected Alternatives: Editing the global `BufferID` enum for domain-local lanes, keeping TempJob scratch allocations, keeping the private persistent baseline array, releasing all `SystemID.TerrainSeams` buffers on state dispose, or moving telemetry/scratch into managed lists/files during gameplay.
Scalability potential: Low/Middle/High/Ultra all use the same fixed vault-owned lanes. Weak devices avoid repeated TempJob allocator pressure and one persistent baseline allocation per terrain; high/ultra retain deterministic postmortem evidence and reusable scratch while spending terrain budget on smoother seam math.
Hardware Impact: Expected direct delta is small and patch-size dependent. The concrete gain is allocator/lifetime risk removal: 19,200 bytes black-box + reusable scratch + per-terrain baselines are vault-owned and 64-byte aligned rather than local native allocations on i3/MX350/Quest-class hardware.

<SELF_AUDIT_DATAVAULT_SEAM_BLACKBOX>
  <VaultStatus>`WorldGenerativeGeologyTerrainSeamApplier` no longer allocates private native seam arrays. Black-box: `0x530421`, length `300`. Native plans: `0x530422`. Patch heights: `0x530423`. Blend mask: `0x530424`. Normals: `0x530425`. Baseline heights: per-terrain `0x531000 + (instance id & 0x000FFFFF)`. Owner: `SystemID.TerrainSeams` through `GlobalDataVault`.</VaultStatus>
  <StructLayout>`TerrainSeamTelemetryEntry` remains 64 bytes: 16 unsigned/int/float lanes * 4 bytes = 64, explicit `Reserved4` tail padding, natural sequential layout, no `Pack`.</StructLayout>
  <DependencyGraph>Telemetry record/dump paths resolve the vault buffer synchronously from existing domain state; sampler/seam Burst jobs still return `JobHandle` to callers and do not add hot `Complete()` calls.</DependencyGraph>
  <CompileGuard>No asmdef reference was added or changed. The domain still routes runtime sampler data through contracts/DataVault-style handles; the seam applier remains in existing Core only because it is the Unity Terrain bridge.</CompileGuard>
  <ForbiddenPatternAudit>Latest SHINOBU grep found no `new NativeArray`, private seam allocation/register/unregister, `Physics`, `Raycast`, `MeshCollider`, `Terrain.GetHeights`, `Time.frameCount`, `Pack=1`, `Pack=4`, `GlobalRegistry.ScalabilityTier`, or `ScalabilityTierProfileByte` in the sampler/seam source set.</ForbiddenPatternAudit>
  <CompileEvidence>`dotnet build Hecton8.Core.csproj --no-restore /clp:ErrorsOnly` is currently blocked outside SHINOBU_41 by unrelated `SaveBinaryPayloadCodec.cs` missing `IndustrialLoreBitMask`, `VolcanicUpdraftDirector.cs` missing `fixedDeltaTime`/`_jobPending`, and Visor features missing `HectonDrsRenderFeatureGate`. No compiler error references SHINOBU_41 terrain files.</CompileEvidence>
  <RemainingDebt>Cold `System.Reflection` in the seam quality bridge remains `[BLOCKED BY GENERATED ASSEMBLY ABI]` until Unity regenerates `Hecton8.World.Terrain.dll` with the new quality fields or a contracts-level seam job facade replaces the stale metadata path.</RemainingDebt>
</SELF_AUDIT_DATAVAULT_SEAM_BLACKBOX>

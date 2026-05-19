# LOG_SHINOBU_74

## 2026-05-18 17:47:18 +04:00

What was wrong:
- Active `BiolumPulseSyncRuntime` already had packed `uint` glow state, Burst oscillator, AUP math, Dear Lie globals, editor facade, CSV ingest, and black-box telemetry.
- The scalability path still used a binary `_dearLieOnlyActive` / `UseDearLieOnly` branch from `SystemHealthIndex01 > 0.85`.
- That binary branch violated SHINOBU_74: `GlobalQualityWeight` must continuously collapse 50,000 per-plant waves into 4 global pulses.
- Full `dotnet build` without `--no-dependencies` timed out at 180s because it pulled large Unity dependency graphs. This was not accepted as compile evidence.

What was done:
- Extracted `<AGENT_PROMPT id="SHINOBU_74">` from `Docs/Tasks/CURRENT_BATCH.md`; task count verified as 20.
- Read project domain boundary and relevant mandates before coding.
- Added runtime quality state: `_globalQualityWeight`, `_individualGlowWeight01`, `_dearLieBlend01`, `_scheduledGpuColorCount`.
- Added `RefreshGlobalQualityWeight()` reading `HomeostasisBrain.GlobalQualityWeight`.
- Added continuous math:
  - `ResolveIndividualGlowWeight(globalQualityWeight, systemHealthIndex01)`.
  - `ResolveScheduledGlowCount(individualGlowWeight01)`.
  - `SmoothStepRange01()`.
- Removed the binary Dear Lie branch and job field.
- Updated `BiolumVisualSyncJob` to receive `GlobalQualityWeight`, `IndividualGlowWeight01`, `DearLieBlend01`, and `ActiveIndividualCount`.
- At `GlobalQualityWeight` 0.1, scheduled count collapses to `SyncGroupCount` = 4.
- At `GlobalQualityWeight` 1.0, scheduled count reaches `MaxGlowInstances` = 50,000 when system stress permits.
- Spatial pulse strength now scales by `IndividualGlowWeight01`; Dear Lie group intensity scales by `DearLieBlend01`.
- GPU packed-color upload now copies only `_scheduledGpuColorCount`, not the full 50,000 when quality is reduced.
- Shader clock `w` now publishes `_globalQualityWeight`; master phase `w` publishes `_dearLieBlend01`.
- Telemetry `ActiveGlowingInstances` now records scheduled active glow count.
- Burst compile attributes were hardened to `CompileSynchronously = true`, `FloatMode.Fast`, `FloatPrecision.Standard`.
- Status file updated: `Docs/Tasks/Status_SHINOBU_74.md`.
- Decision journal updated: `Docs/AgentLogs/Rationale_SHINOBU_74.md`.

Cinematic cheats used:
- No Unity Lights.
- No `Material.SetColor`.
- No per-renderer material mutation.
- Bioluminescence remains emission/Bloom/SSGI fake.
- Low quality path uses 4 global colors instead of 50,000 individual spatial wave solves.
- Damage and O2 warning remain packed-color visual signals, not spawned light/particle dependencies.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE. Unity Profiler, GPU timing, and Frame Debugger were not run in this turn.
- Static iteration delta at `GlobalQualityWeight` 0.1: 49,996 per-plant job iterations avoided per scheduled sync.
- Static upload delta at `GlobalQualityWeight` 0.1: packed-color upload reduced from 50,000 `uint` entries to no bulk per-plant upload; 4 global states remain.
- Static CPU estimate for material/light eradication: >5000us avoided versus thousands of realtime point lights.
- Static CPU estimate for no material churn: 35us avoided per pulse frame.
- Static CPU estimate for reduced renderer submission/object traversal: 120us avoided.
- These are budget estimates, not measured profiler facts.

Verification:
- Static scan: no `_dearLieOnlyActive`, `UseDearLieOnly`, `Material.SetColor`, `renderer.material`, `.material.color`, `AddComponent<Light>`, `new Light`, `Pack = 1`, or `{ get; set; }` hits in assigned biolum runtime/editor files.
- Runtime compile: PASS.
  - `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:quiet -m:1 /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly`
- Editor compile: PASS.
  - `dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies -v:quiet -m:1 /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly`
- Unity Play Mode / Profiler / Frame Debugger: NOT RUN. No Unity Editor/MCP session evidence was available.

## 2026-05-18 18:02:00 +04:00

What was wrong:
- First pass removed the visible binary Dear Lie switch, but polish audit found remaining forensic gaps.
- `Biolum_Profiles.bin` hydration used native-endian `MemoryMarshal.Read<float>`.
- `BiolumVisualSyncJob` NativeArray fields did not explicitly prove non-aliasing to Burst.
- The schedule batch count was a raw literal `64`, not a named cache contract.
- The previous H-PHI report over-simplified private arrays. Persistent NativeArray state is Vault-owned, but cold managed Unity bridge buffers exist.

What was done:
- Replaced native-endian profile reads with `ReadFloatLittleEndian(ReadOnlySpan<byte>, int)`, assembling `uint` bytes explicitly and converting with `math.asfloat`.
- Added `[NoAlias]` / `[ReadOnly, NoAlias]` on the Burst job NativeArray fields.
- Added `BiolumJobInnerLoopBatchCount = 64` and scheduled with that constant.
- Re-ran isolated runtime and editor compiles after the corrections.
- Re-ran static scans for material/light/property/binary-switch and hot-path allocation patterns.
- Updated `Status_SHINOBU_74.md` and `Rationale_SHINOBU_74.md` with the correction and the H-PHI caveat.
- Generated `Docs/AgentLogs/SELF_AUDIT_SHINOBU_74.xml` with 20-task reconciliation, struct offsets, H-PHI caveat, dependency graph, and Dear Lie complexity proof.

Cinematic Cheats used:
- The core fake remains unchanged: 4 global Dear Lie pulses at low quality, shader emission/bloom/SSGI instead of Unity Lights, and packed uint colors instead of material mutation.
- The endian/no-alias fixes do not add physical simulation; they harden the data path.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE. Unity Profiler, GPU timing, Burst Inspector, and Frame Debugger were not run in this turn.
- Static low-quality iteration delta remains 49,996 per-plant job iterations avoided per scheduled sync at `GlobalQualityWeight` 0.1.
- `[NoAlias]` creates vectorization eligibility but measured NEON/AVX gain is pending Burst Inspector.
- Explicit endian decode is correctness, not frame-time savings.

Verification:
- Runtime compile: PASS.
  - `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:quiet -m:1 /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly`
- Editor compile: PASS.
  - `dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies -v:quiet -m:1 /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly`
- Static scan: PASS for forbidden material/light/binary-switch/property tokens in assigned files.
- Static hot-path scan: PASS for `foreach`, LINQ, local `new NativeArray`, persistent allocator creation, `FindObject`, and string interpolation in the assigned runtime file.
- Unity Play Mode / Profiler / Frame Debugger / Burst Inspector: NOT RUN. No Unity Editor/MCP session evidence was available.

## 2026-05-18 22:11:38 +04:00

What was wrong:
- Binary ledger still had an integration smell: `Data/Visuals/Biolum_Profiles.bin` had a reader, but no scene/prefab/bootstrap proof that `BiolumPulseSyncRuntime` exists in a clean play session.
- A reader without a boot path can rot into dead binary payload integration even if the C# parser is correct.
- Current compile verification after host wiring is blocked by Core, not by SHINOBU code.

What was done:
- Added a scene-local `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` fallback host in `BiolumPulseSyncRuntime`.
- SUPERSEDED by 2026-05-18 22:31:54 pass: this pass originally added `SubsystemRegistration` reset plus a singleton guard; the singleton guard was later removed and replaced with an atomic process ownership claim.
- The fallback creates exactly one cold `H8_BiolumPulseSyncRuntime` service object only when no runtime instance exists.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`: `Data/Visuals/Biolum_Profiles.bin` is now `ACTIVE_RUNTIME_WIRED`, with Unity profiler/Frame Debugger proof still pending.
- Updated `Docs/Tasks/Status_SHINOBU_74.md`, `Docs/AgentLogs/Rationale_SHINOBU_74.md`, and `Docs/AgentLogs/SELF_AUDIT_SHINOBU_74.xml`.

Cinematic Cheats used:
- Still no Unity Lights.
- Still no per-plant GameObjects; the new GameObject is a single cold scene service host, not flora representation.
- Glow remains packed `uint` emission and 4 global Dear Lie pulses at low `GlobalQualityWeight`.
- `Biolum_Profiles.bin` now feeds the same packed profile path instead of any material/light mutation path.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE. Unity Profiler, GPU timing, Burst Inspector, and Frame Debugger were not run.
- Runtime-host wiring is integration correctness, not frame-time optimization.
- Low-quality static delta remains 49,996 per-plant job iterations avoided at `GlobalQualityWeight` 0.1.
- No new hot-path allocation was introduced by the host edit; the host object is cold startup only.

Verification:
- SUPERSEDED by 2026-05-18 22:31:54 pass: static host proof now uses `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`, `SubsystemRegistration` reset, and atomic claim/release; current runtime has no singleton-guarded `Awake`.
- Ledger proof: PASS. `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` marks `Data/Visuals/Biolum_Profiles.bin` as `ACTIVE_RUNTIME_WIRED`.
- Static forbidden-token scan: PASS for SHINOBU domain material/light/binary-switch/property/hot-path allocation patterns. The only `new GameObject` is the single cold service host.
- Post-host runtime compile: BLOCKED BY CORE DEPENDENCY. `Assembly-CSharp.csproj` cannot resolve `Temp\bin\Debug\Hecton8.Core.dll`.
- Post-host editor compile: BLOCKED BY CORE DEPENDENCY. `Assembly-CSharp-Editor.csproj` cannot resolve `Temp\bin\Debug\Hecton8.Core.dll`.
- Dependency compile check: BLOCKED OUTSIDE DOMAIN. `Hecton8.Core.csproj` fails at `Assets/_Project/Scripts/Core/GlobalSignals.cs(1119,26)` with CS0266 `void*` to `T*`.
- Unity Play Mode / Profiler / Frame Debugger / Burst Inspector: NOT RUN.

## 2026-05-18 22:31:54 +04:00

What was wrong:
- The runtime-host wiring pass used `s_runtimeInstance` and an `Awake()` duplicate guard.
- That was standard Unity singleton-style ownership and violated `AGENTS.md` lifecycle policy.
- A cold fallback host is still useful, but it cannot become a global instance pointer or self-register in `Awake`.

What was done:
- Removed `s_runtimeInstance`.
- Removed `Awake()` from `BiolumPulseSyncRuntime`.
- Added `s_runtimeClaimed` as an atomic process ownership latch reset by `SubsystemRegistration`.
- Added `TryClaimRuntimeOwner()` using `Interlocked.CompareExchange`.
- Added `ReleaseRuntimeOwnerClaim()` and called it from `OnDisable()` and `Dispose()`.
- Kept actual update and late-frame registration through `GlobalRegistry`; the static claim does not expose or store a runtime object.
- Updated status, rationale, self-audit, and binary ledger to record the singleton purge.

Cinematic Cheats used:
- No change to the visual fake: still packed `uint` emission, Dear Lie 4-group shader pulses at low quality, and no Unity Lights.
- The lifecycle correction avoids Unity scene search and does not alter shader/material batching.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- Singleton purge is architectural hygiene, not a performance claim.
- No hot-path allocations were added.

Verification:
- Static scan: PASS for no `s_runtimeInstance`, no `Awake()`, no `FindObject`, no `GameObject.Find`, no Unity Light/material mutation, no binary Dear Lie switch, no hot-path LINQ/foreach/native allocation patterns.
- `git diff --check`: PASS except existing CRLF normalization warning on the runtime file.
- Dotnet build: NOT RERUN. Current build wall is already localized to `Hecton8.Core` at `GlobalSignals.cs(1119,26)`; rerunning without a Core fix would produce the same dependency failure.
- Build process guard: `dotnet.exe` and `csc.exe` were observed already running for `Hecton8.Core.csproj`; no additional build was launched after the singleton purge.
- Unity Play Mode / Profiler / Frame Debugger / Burst Inspector: NOT RUN.

## 2026-05-18 23:36:05 +04:00

What was wrong:
- Runtime uploaded `_BiolumGpuColorBuffer`, but `Hecton_IndirectVegetation.shader` did not consume the packed `uint` buffer. That made the 50,000-instance packed color path incomplete.
- The Burst publish path writes four valid Dear Lie global states, while `_GlobalBiolumParams.x` could publish `_activeStateCount` up to 16. Shaders could select zero-filled state slots.
- Partial mid-tier uploads risked stale buffer reads if the shader sampled indices outside `_scheduledGpuColorCount`.

What was done:
- Added `_publishedGlobalStateCount` and publish that count to `_GlobalBiolumParams.x`.
- Published `_scheduledGpuColorCount` in `_GlobalBiolumParams.w` and `_individualGlowWeight01` in `_GlobalBiolumClock.w`.
- Updated `Hecton_IndirectVegetation.shader` to declare `_BiolumGpuColorBuffer`, decode RGB10_A2 `uint` colors, and blend them by `sourceInstanceIndex` only inside the uploaded scheduled page.
- Updated status, rationale, self-audit, and binary ledger notes.

Cinematic Cheats used:
- Low quality remains a four-state global emission fake, not 50,000 individual light computations.
- High/ultra consumes packed emission colors in the shader, preserving batching and avoiding Unity Lights/material instance churn.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- Static low-quality iteration delta remains up to 49,996 per-plant job iterations avoided at `GlobalQualityWeight` 0.1.
- Shader bridge is a correctness/batching fix; measured GPU and CPU impact still require Unity import, Frame Debugger, and Profiler captures.

Verification:
- Static shader scan: PASS. `_BiolumGpuColorBuffer`, `DecodeBiolumRgb10A2`, and `ResolveSyncedBiolumColor` exist in `Hecton_IndirectVegetation.shader`.
- Static runtime scan: PASS. `_publishedGlobalStateCount`, scheduled-count publishing, and packed-buffer gating are present.
- Dotnet build: NOT RERUN. The known Core dependency wall remains outside SHINOBU, and the latest CPU guard read 100%, above the user's 50% build limit.
- Unity shader import / Play Mode / Profiler / Frame Debugger / Burst Inspector: NOT RUN.

## 2026-05-18 23:49:19 +04:00

What was wrong:
- The shader bridge exposed a cold-start hazard: a `GraphicsBuffer` can exist before the first packed color upload.
- `_scheduledGpuColorCount` was a desired count, not proof of how many `uint` colors were actually uploaded.
- The shader could read stale or undefined packed colors if schedule count exceeded actual uploaded count.

What was done:
- Added `_publishedGpuColorCount` to `BiolumPulseSyncRuntime`.
- Reset `_publishedGpuColorCount` to zero whenever the GPU page is invalidated.
- Set `_publishedGpuColorCount` to the exact `UnlockBufferAfterWrite(count)` count after upload.
- Published actual packed-page count in `_GlobalBiolumParams.w`.
- Published individual shader sampling weight only when an uploaded page larger than the four Dear Lie groups exists.
- Renamed the shader guard to `packedBufferCount` to reflect actual published range, not desired schedule.

Cinematic Cheats used:
- Cold start and low quality remain global four-pulse emission fakes.
- Individual packed color sampling is delayed until real GPU data exists, preserving the illusion without Unity Lights or material churn.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- This is a correctness and artifact-prevention fix. It does not claim new measured runtime savings.
- Static low-quality iteration delta remains up to 49,996 per-plant iterations avoided at `GlobalQualityWeight` 0.1.

Verification:
- Static runtime scan: PASS. `_publishedGpuColorCount` exists, resets on invalidation, and is set after upload.
- Static shader scan: PASS. Shader reads only when `sourceInstanceIndex < packedBufferCount`.
- Dotnet build: NOT RUN under current user build throttle.
- Unity shader import / Play Mode / Profiler / Frame Debugger / Burst Inspector: NOT RUN.

## 2026-05-18 23:49:19 +04:00 - Dear Lie Selector Recheck

What was wrong:
- The shader global fallback selected Dear Lie states from position noise.
- Task 08 requires four sync groups selected by species hash modulo four. Position selection was a visual fake, but not the requested species/template grouping contract.

What was done:
- Added `biolumSyncGroup` to the indirect vegetation vertex-to-fragment payload.
- Derived the group from finite-guarded `TemplateIndex % 4`, with a stable finite-guarded type/variation fallback when no template index is authored.
- Changed `ResolveIndirectVegetationGlobalBiolum` to select `_GlobalBiolumStates[group % activeCount]`.
- Left high-tier shimmer position/time based; only base Dear Lie color selection changed.

Cinematic Cheats used:
- Still four global emission states for low tier.
- The species/template group makes the fake stable and controllable without a new material property, Unity Light, or cross-domain buffer dependency.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- This pass is contract correctness; it does not claim new CPU savings.

Verification:
- Static shader scan: PASS. `biolumSyncGroup`, `templateSyncSeed`, and grouped `ResolveIndirectVegetationGlobalBiolum` are present.
- Unity shader import / Play Mode / Profiler / Frame Debugger: NOT RUN.

## 2026-05-18 23:49:19 +04:00 - Deterministic RNG Recheck

What was wrong:
- Mock predator firing used a custom deterministic hash as the random source.
- The current mandate requires `Unity.Mathematics.Random` seeded from sector/frame state and forbids UnityEngine randomness.

What was done:
- Added `CreateDeterministicRandom(sectorHash, frameCounter, salt)` using `default` plus `InitState(seed)`, avoiding a gameplay-path `new` constructor.
- Rewired `AdvanceMockPredatorSignal` to use `Unity.Mathematics.Random` for roll, angles, radius offsets, and pulse radius.
- Seed source is biome hash when available, otherwise profile source hash, combined with `_frameCounter`.

Cinematic Cheats used:
- The predator remains a local mock signal, not a fauna dependency or physical AI simulation.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- This pass is deterministic correctness, not a performance claim.

Verification:
- Static scan: PASS. `UnityEngine.Random` does not appear in the assigned runtime.
- Runtime replay/lockstep proof: NOT RUN.

## 2026-05-18 23:49:19 +04:00 - Quality Curve Recheck

What was wrong:
- Scheduled packed-color count used a continuous weight but mapped it with raw multiplication.
- The mandate explicitly requires `math.lerp`, `math.step`, and polynomial curves for quality shedding.

What was done:
- Rewrote `ResolveScheduledGlowCount` to use `math.step(0.0001f, weight)`, `SmoothStep01(weight)`, and `math.lerp(4, 50000, activeWeight)`.

Cinematic Cheats used:
- Near-zero quality collapses exactly to four Dear Lie global pulses.
- Mid/high quality ramps into individual packed-buffer work without a binary tier switch.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- Expected benefit is reduced upload/job thrash near thermal collapse; profiler proof pending.

Verification:
- Static source check: PASS. `ResolveScheduledGlowCount` contains `math.step`, `SmoothStep01`, and `math.lerp`.
- Runtime profiler proof: NOT RUN.

## 2026-05-18 23:49:19 +04:00 - HZB Boundary Recheck

What was wrong:
- The polish mandate demanded HZB awareness, but SHINOBU_74 does not own draw-list culling or matrix dispatch.

What was done:
- Verified static ownership: `HectonIndirectVegetationRenderer.cs` owns vegetation culling/BRG dispatch.
- Verified static shader evidence: `FloraCulling.compute` declares `_HectonDepthPyramid`, `_HectonOcclusionEnabled`, depth bias, and visible instance append buffers.
- Recorded the boundary in self-audit instead of duplicating culling in the glow runtime.

Cinematic Cheats used:
- SHINOBU remains a color/emission fake. Occlusion remains in the renderer/culling owner.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- No new culling code was added in SHINOBU.

Verification:
- Static source scan: PASS for existing culling ownership.
- Runtime HZB/Frame Debugger proof: NOT RUN.

## 2026-05-19 00:15:09 +04:00 - Shader Interpolator And Blackbox Recheck

What was wrong:
- The first Dear Lie selector correction added a standalone `TEXCOORD22` scalar for one 0..3 sync group id. That was functional but wasteful on mobile.
- Blackbox `ActiveGlowingInstances` still reported `_scheduledGpuColorCount`, which can be larger than the packed page actually published to the shader.

What was done:
- Packed spatial pulse offset and four-state sync group into one `half2 biolumPulseData : TEXCOORD21`.
- Removed the standalone `biolumSyncGroup : TEXCOORD22` varying.
- Updated spore sparkle, local pulse phase, and global Dear Lie selection to read `biolumPulseData.x/y`.
- Changed telemetry to report `_publishedGpuColorCount` only when a valid uploaded GPU page exists; otherwise it records the four Dear Lie groups.
- Updated Status, Rationale, and SELF_AUDIT evidence.

Cinematic Cheats used:
- No new light simulation. The same fake remains: packed emission colors plus four global Dear Lie pulses, with shader/bloom/SSGI carrying perceived glow.

Exact Microseconds saved:
- CPU: 0us claimed. This is shader interpolator pressure and blackbox correctness.
- GPU: unmeasured; expected lower mobile varying pressure by removing the extra TEXCOORD lane. Shader import/profiler/Frame Debugger still pending.

Verification:
- Static scan: PASS. No `TEXCOORD22`, no stale `input.biolumSyncGroup`, and `biolumPulseData` drives spore/pulse/global selection.
- Forbidden-token scan: PASS. No Unity Lights/material mutation, no binary Dear Lie switch, no hot-path LINQ/foreach/native allocation pattern, no `UnityEngine.Random`, and no `new Unity.Mathematics.Random` in assigned runtime/editor files.
- XML self-audit parse: PASS.
- `git diff --check`: PASS with only existing CRLF normalization warnings on runtime/shader files.
- Process guard: PASS. CPU guard read 100%, so no `dotnet build` was launched.

## 2026-05-19 00:24:27 +04:00 - Deterministic Burst Mode Recheck

What was wrong:
- `BiolumVisualSyncJob` used `FloatMode.Fast`.
- The job mutates `GlowStateDTO.Phase` and packed GPU color DTOs. That state feeds telemetry and replay investigation, so Fast math is a rollback risk.

What was done:
- Changed `BiolumVisualSyncJob` to `FloatMode.Deterministic` while keeping `CompileSynchronously = true` and `FloatPrecision.Standard`.

Cinematic Cheats used:
- No new simulation. The low path remains four global Dear Lie pulses; high path remains packed uint emission data, not Unity Lights.

Exact Microseconds saved:
- 0us claimed. Determinism is chosen over raw Fast-math ALU freedom. Burst Inspector timing still pending.

Verification:
- Static scan: PASS. `BiolumVisualSyncJob` uses `FloatMode.Deterministic`; no `_frameCounter++`, no Unity frame-time reads, no Unity/random constructors, and no forbidden Light/material mutation patterns.
- XML self-audit parse: PASS.
- `git diff --check`: PASS with only existing CRLF normalization warnings on runtime/shader files.
- Build not launched: CPU guard read 100%.

## 2026-05-19 00:22:08 +04:00 - Deterministic Frame Clock Recheck

What was wrong:
- `_frameCounter` advanced inside `RecordTelemetry()`.
- Fault paths can call `RecordTelemetry()` outside the normal Tick tail, so RNG seed, shader frame clock, and mock predator `FrameStamp` could be shifted by blackbox writes.

What was done:
- Added `AdvanceSimulationFrameCounter()` and call it once per dispatcher `Tick`.
- Changed blackbox `Frame` to record the current frame without incrementing.
- Changed mock predator `FrameStamp` to use the current frame instead of predicting the old telemetry-incremented next frame.

Cinematic Cheats used:
- None new. This protects the existing packed-emission Dear Lie path from rollback/forensics drift.

Exact Microseconds saved:
- 0us claimed. This is deterministic state hygiene, not optimization.

Verification:
- Static scan: PASS. `_frameCounter` has no `++` operator and advances only through `AdvanceSimulationFrameCounter()` in `Tick`; `RecordTelemetry()` records the current value only.
- Time/RNG scan: PASS. No Unity frame time, no `UnityEngine.Random`, no `System.Random`, and no `new Unity.Mathematics.Random` in the assigned runtime.
- Forbidden-token scan: PASS for Unity Lights/material mutation, binary Dear Lie switch, hot-path LINQ/foreach/native allocation pattern, scene search, and static runtime instance.
- XML self-audit parse: PASS.
- `git diff --check`: PASS with only existing CRLF normalization warnings on runtime/shader files.
- Process guard: PASS. CPU guard read 100%, so no `dotnet build` was launched.

## 2026-05-19 00:27:58 +04:00 - Narrow Build Attempt

What was wrong:
- C# changed after frame-clock and Burst-mode corrections. A compile check was justified once CPU was below 50% and no `dotnet`/`csc` process was active.

What was done:
- Ran one narrow build: `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:quiet -m:1 /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly`.
- Result: `exit 1` with empty stdout/stderr. No compiler line was available to repair.
- Waited for orphaned `dotnet/csc` processes to finish.

Cinematic Cheats used:
- None. Verification pass only.

Exact Microseconds saved:
- 0us. Verification pass only.

Verification:
- Follow-up diagnostic build was not launched: CPU guard rose to 99-100% and `dotnet/csc` were active after the failed attempt.
- After processes exited, CPU guard still read 99%, so the build-throttle rule still blocked a diagnostic rerun.

## 2026-05-19 01:05:30 +04:00 - Transcendental Oscillator Recheck

What was wrong:
- `BiolumVisualSyncJob` still used `math.sin` inside the active 50,000-instance path for base pulse, damage flicker chaos, and O2 heartbeat.
- The visual contract needs believable glow modulation, not trigonometric truth per plant.

What was done:
- Replaced per-plant sine pulse with a cubic-smoothed triangle pulse.
- Replaced damage flicker sine chaos with deterministic uint hash noise using instance index, damage frame stamp, and fixed time bucket.
- Replaced O2 heartbeat sine with the same smoothed triangle phase.

Cinematic Cheats used:
- Mathematical waveform fake: triangle + smoothstep polynomial replaces sine in the CPU oscillator.
- Damage flicker uses deterministic hash noise instead of simulating spark particles or lights.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- Expected gain: removes transcendental ALU from the active individual glow path. Burst Inspector/profiler pending.

Verification:
- Static scan: PASS. Remaining `math.sin/math.cos` hits are only in rare mock predator origin generation outside `BiolumVisualSyncJob`; the 50k Burst path uses `ResolveSmoothedTrianglePulse01` and `Hash01`.
- Forbidden-token scan: PASS for Unity Lights/material mutation, binary Dear Lie switch, hot-path LINQ/foreach/native allocation pattern, Unity frame time, and random constructors.
- XML self-audit parse: PASS.
- `git diff --check`: PASS with only existing CRLF normalization warnings on runtime/shader files.
- Build not launched: CPU guard read 100%.

## 2026-05-19 01:08:00 +04:00 - Sqrt-Free Wavefront Recheck

What was wrong:
- Spatial pulse propagation and damage flicker still used `math.length()` inside the active individual glow job.
- Exact Euclidean sqrt is unnecessary for a visual-only emissive ripple.

What was done:
- Replaced spatial pulse distance with `distanceSq` vs squared wave radius shell math.
- Replaced damage falloff with `lengthsq / radiusSq` shaped by `SmoothStep01`.
- Added finite guards for localized AUP deltas before distance math.
- Added denominator clamps through `math.max(..., 0.0001f)`.

Cinematic Cheats used:
- Sqrt-free squared-distance shell fake for light wavefronts.
- Sqrt-free squared falloff for damage flicker.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- Expected gain: removes sqrt ALU on active pulse/damage frames in the individual 50k path.

Verification:
- Static scan: PASS. No `math.length()` remains in SHINOBU runtime; remaining `math.sin/math.cos` hits are only rare mock predator origin generation outside `BiolumVisualSyncJob`.
- Forbidden-token scan: PASS for Unity Lights/material mutation, binary Dear Lie switch, hot-path LINQ/foreach/native allocation pattern, Unity frame time, random constructors, and `FloatMode.Fast`.
- XML self-audit parse: PASS.
- `git diff --check`: PASS with only existing CRLF normalization warnings on runtime/shader files.
- Build not launched: CPU guard read 99%.

## 2026-05-19 01:17:10 +04:00 - Quality Cadence Recheck

What was wrong:
- `GlobalQualityWeight` reduced active instance count, but normal job cadence remained `0` seconds, so low quality could still schedule every frame.
- The mandate requires update frequency to shed toward 5Hz under low quality.

What was done:
- Added `LowQualityUpdateIntervalSeconds = 1f / 5f`.
- Added `ResolveUpdateCadenceSeconds()` using `SmoothStepRange01`, `math.lerp`, and a continuous overload scalar.
- Replaced fixed normal/overload cadence selection in `Tick`.
- Published the same cadence through `_GlobalBiolumClock.y`.

Cinematic Cheats used:
- Temporal fake: at low quality, the four global Dear Lie pulses carry continuity while CPU oscillator updates drop toward 5Hz.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- Expected gain: up to 55 fewer oscillator job schedules per second at low quality.

Verification:
- Static scan was performed after this entry and is recorded in the later verification entries.
- Build was deferred until the CPU/process guard allowed one narrow runtime compile.

## 2026-05-19 01:21:48 +04:00 - Narrow Runtime Build Pass

What was wrong:
- The latest runtime code path had static proof only after the quality-cadence correction, and the prior quiet build failure had no diagnostic compiler output.

What was done:
- Rechecked the user's build guard.
- Ran one narrow runtime build only: `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:minimal -m:1 /p:UseSharedCompilation=false /p:BuildInParallel=false /clp:ErrorsOnly`.
- Result: PASS. 0 warnings. 0 errors. Time elapsed: 00:00:16.04.
- Updated `Status_SHINOBU_74.md`, `Rationale_SHINOBU_74.md`, `SELF_AUDIT_SHINOBU_74.xml`, and the binary payload ledger to remove the stale current-Core-blocked statement.

Cinematic Cheats used:
- None new in this verification step. Current runtime still uses packed uint emission, four global Dear Lie pulses, triangle-wave oscillator fake, and squared-distance wavefront fake.

Exact Microseconds saved:
- 0us claimed from the build pass itself.
- Runtime savings remain unmeasured until Unity Profiler/Burst Inspector/Frame Debugger capture.

Verification:
- Narrow runtime C# build: PASS.
- Unity import, shader import, Play Mode, Profiler, Burst Inspector, and Frame Debugger: still pending.

## 2026-05-19 01:23:48 +04:00 - Post-Build Static Audit

What was wrong:
- The documentation had been corrected after the narrow runtime build, but needed a final stale-marker and XML/static-scan pass.

What was done:
- Parsed `SELF_AUDIT_SHINOBU_74.xml` as XML.
- Re-ran forbidden-token scans for Unity Lights, material mutation, Unity time, random constructors, `FloatMode.Fast`, `math.length`, hot-path LINQ/foreach, and local NativeArray allocation patterns in the assigned runtime/editor files.
- Re-ran shader bridge scan for the removed `TEXCOORD22`/standalone sync-group varying.
- Re-ran stale compile-blocker marker search across SHINOBU status, rationale, self-audit, log, and ledger.
- Ran `git diff --check` on the assigned code and documentation files.

Cinematic Cheats used:
- None new. This pass verified the existing packed-color, global-pulse, triangle-wave, and squared-distance fakes.

Exact Microseconds saved:
- 0us claimed. Verification only.

Verification:
- XML parse: PASS.
- Forbidden-token runtime/editor scan: PASS.
- Shader stale-varying scan: PASS.
- Stale compile-blocker marker scan: PASS.
- `git diff --check`: PASS with only Git CRLF normalization warnings on the runtime and shader files.
- No additional build launched.

## 2026-05-19 01:36:59 +04:00 - H-PHI Array Purge And Assembly Split

What was wrong:
- Runtime still owned `_managedStates Vector4[16]` and `_csvWorkerScratch byte[16384]`.
- The shader still declared `_GlobalBiolumStates[16]` for a fallback that should be exactly four Dear Lie groups.
- SHINOBU runtime/editor files still sat in broad predefined/global assemblies instead of a domain runtime/editor asmdef split.

What was done:
- Removed `_managedStates`, `_GlobalBiolumStatesId`, and all `Shader.SetGlobalVectorArray` usage.
- Published four global fallback pulses as `Matrix4x4 _dearLieGroupMatrix` to shader `float4x4 _GlobalBiolumDearLieGroups`.
- Removed CSV worker thread and private byte scratch array. File-change CSV reload now locks `BiolumCsvScratch`, reads into the vault NativeArray via `Span<byte>` over the native pointer, and parses in place.
- Added `Hecton8.VFX.Bioluminescence.Runtime.asmdef` plus `.meta`.
- Moved the editor window into `Assets/_Project/Scripts/VFX/Bioluminescence/Editor` with `Hecton8.VFX.Bioluminescence.Editor.asmdef` plus `.meta`.

Cinematic Cheats used:
- Four-group Dear Lie matrix CBuffer replaces a 16-slot global state array and keeps low quality at O(4).
- CSV hot reload remains a cold designer-control path, not a gameplay simulation path.

Exact Microseconds saved:
- Exact measured savings: NOT AVAILABLE.
- Removed 16KB private managed CSV staging and one managed vector-array bridge from runtime ownership.
- Compile-wall impact is structural; Unity import/build proof pending.

Verification:
- Static scan: PASS. No `_managedStates`, `_GlobalBiolumStates`, private `byte[]`, CSV worker thread, `SetGlobalVectorArray`, Unity Lights, material mutation, hot-path LINQ/foreach, local NativeArray allocation, `FloatMode.Fast`, or `math.length` remain in SHINOBU runtime/shader checks.
- Build not launched after this patch; new asmdefs require Unity import/project regeneration and the user build guard still applies.

## 2026-05-19 01:48:41 +04:00 - Orphan Meta Purge

What was wrong:
- After the editor facade moved into `Assets/_Project/Scripts/VFX/Bioluminescence/Editor`, the old tracked `Assets/_Project/Scripts/Editor/BioluminescenceTunerWindow.cs.meta` remained as a dead Unity GUID.

What was done:
- Deleted the orphan `.meta`.
- Kept the live editor facade and its domain-local asmdef/meta files intact.

Cinematic Cheats used:
- None. This is import hygiene, not runtime simulation.

Exact Microseconds saved:
- 0us runtime.
- Editor import noise reduced; no frame-time claim.

## 2026-05-19 01:56:36 +04:00 - Global Biolum Shader Consumer Purge

What was wrong:
- Runtime publishes `_GlobalBiolumDearLieGroups` only, but active coral, kelp, sargassum, procedural-bio, GPUI, and leviathan shaders still sampled retired `_GlobalBiolumStates[16]`.
- That left a stale global-pulse contract after the H-PHI array purge.

What was done:
- Replaced `_GlobalBiolumStates[16]` with `float4x4 _GlobalBiolumDearLieGroups` in active biolum shader consumers.
- Clamped every affected global biolum consumer to four Dear Lie rows.
- Re-ran static scan across `Assets/_Project/Art/Shaders` and `Assets/_Project/Scripts/VFX/Bioluminescence`; no `_GlobalBiolumStates` or `Shader.SetGlobalVectorArray` hits remain.

Cinematic Cheats used:
- Preserved the four-group Dear Lie matrix fake. No Unity Lights, no material mutation, no legacy vector-array publisher.

Exact Microseconds saved:
- 0us measured. This was a correctness/contract purge; profiler, shader import, and Frame Debugger evidence remain pending.

# Rationale_SHINOBU_44

Status: PENDING VERIFICATION / FULL BUILD BLOCKED OUTSIDE DOMAIN  
Domain: CONTINUOUS_SCALABILITY_DICTATOR / CORE & MEMORY INFRASTRUCTURE  

## Decision 00 - Domain Boundary

Problem: The task asks for a global scalar used by all domains while the agent is blind to rendering, AI, and terrain implementations. Direct references would create compile walls.  
Solution: Expose a small continuous scalability contract and runtime owner. Other domains consume `GlobalQualityWeight`, `FractionalTimeSlice`, and scalar helpers instead of direct concrete dependencies.  
Rejected Alternatives: Direct edits into render/AI/terrain systems. That would be cross-domain sabotage and fragile under 20+ concurrent agents.  
Scalability potential: Low devices consume cheap scalar clamps; Middle keeps full gameplay truth with moderate presentation; High/Ultra can spend recovered budget on visual-overkill shader paths and denser update cadences.  
Hardware Impact: Estimated gain on i3/MX350 is hitch avoidance rather than raw average FPS; expected microfreeze reduction comes from smooth DRS/time-slice attack before thermal downclock.

## Decision 01 - Control Theory Shape

Problem: Direct frame-time-to-quality mapping oscillates and causes resolution bounce under thermal drift.  
Solution: Use EWMA pressure, PID-like correction, fast attack, slow release, and hysteresis timers. Weight can fall quickly but recovers at a bounded per-second rate.  
Rejected Alternatives: Boolean quality states or immediate restoration after one good frame. Both cause visible popping and repeated microstutters.  
Scalability potential: Low/SURVIVAL collapses expensive math continuously; Middle breathes around target; High/Ultra can hold near 1.0 without binary tier locks.  
Hardware Impact: On i3/MX350, reducing oscillation avoids repeated shader/DRS churn and should save multi-millisecond spikes during thermal pressure. PENDING MEASUREMENT.

## Decision 02 - DTO Layout

Problem: Cross-domain quality state must be read cheaply from NativeArray/Burst jobs on ARM64 and x86.  
Solution: `ScalabilityStateDTO` is 16 bytes: `float GlobalQualityWeight`, `float FractionalTimeSlice`, `float VramPressure`, `float ThermalIndex`. Telemetry entries use aligned 32-byte layouts.  
Rejected Alternatives: Properties, `bool` flags, `Pack=1`, or managed classes. They risk CS1612, boxing, unaligned reads, or GC.  
Scalability potential: Same struct supports weak, middle, high, and ultra devices without alternate schemas.  
Hardware Impact: One 16-byte cache-friendly read path for all domains; no branchy tier object graph on low-end silicon.

## Decision 03 - DRS And Shader Scalar Output

Problem: Render degradation must happen without Unity quality-level reloads or shader keyword churn.  
Solution: Compute `TargetRenderScale01 = lerp(0.5, 1.0, GlobalQualityWeight)` and publish it through `IDynamicResolutionRuntime`; publish `_GlobalQualityWeight` and `_H8GlobalQualityWeight` only when the value changes beyond epsilon.  
Rejected Alternatives: `QualitySettings.SetQualityLevel()`, URP asset reloads, and per-material CPU mutation. These stall or allocate and are wrong for VR microfreeze mitigation.  
Scalability potential: Low devices smoothly lose internal pixel cost; Middle rides the curve; High/Ultra keep 1.0 and can spend frame time on richer shader paths.  
Hardware Impact: Estimated i3/MX350 gain is fill-rate rescue during heat spikes; scalar publication is gated and expected under 20us on changed frames.

## Decision 04 - Blind Terrain Sampler Proof

Problem: The dictator cannot depend on Agent 41 terrain code but must prove that external math can consume the scalar.  
Solution: Add `MockTerrainSamplerStatus` plus a Burst job writing `TrilinearSampleProbability01 = GlobalQualityWeight` and `SkippedTrilinearPercent01 = 1 - GlobalQualityWeight`.  
Rejected Alternatives: Editing `GlobalWorldSampler` or inventing a direct terrain interface. That crosses domain ownership and creates integration debt.  
Scalability potential: Low = mostly nearest/cheap samples; Middle = mixed interpolation; High/Ultra = full trilinear/extra detail.  
Hardware Impact: At weight 0.1 the proof skips 90 percent trilinear work, which is the intended cheap-device behavior without binary switches.

## Decision 05 - Bounded GC Freeze

Problem: A GC spike during combat can create a visible hitch, but unbounded GC disable leaks risk into the whole session.  
Solution: Disable GC only on detected pressure spikes and cap the pulse at five seconds; safe-base recovery can re-enable earlier.  
Rejected Alternatives: Permanent `GCMode.Disabled` or ignoring GC spikes. Permanent suppression is operationally unsafe; ignoring spikes leaves the original microfreeze.  
Scalability potential: Low devices get a short emergency shield; Middle/High/Ultra usually never enter the pulse.  
Hardware Impact: Estimated low-end gain is hitch avoidance, not average FPS. Pulse branch is under 5us and editor builds bypass runtime GC suppression.

## Decision 06 - Zero Init Bypass

Problem: Boot-time allocation of frame histories and blackbox rings can waste milliseconds on implicit zeroing.  
Solution: Allocate the affected vault buffers with `NativeArrayOptions.UninitializedMemory` and immediately clear only created arrays through `UnsafeUtility.MemClear`.  
Rejected Alternatives: Blind `ClearMemory` allocation or trusting uninitialized contents. The former hides cold-start cost; the latter risks false mock-load flags and corrupt telemetry.  
Scalability potential: Low devices avoid boot hitches; Middle/High/Ultra preserve deterministic clean state without OS zeroing dependency.  
Hardware Impact: Cold-start savings only. No frame-time claim beyond avoiding allocation-phase stalls on i3/MX350-class hardware.

## Decision 07 - Human Tuning Surface

Problem: Continuous curves need human tuning during play mode, not hardcoded recompiles.  
Solution: Rename the editor facade to "Continuous Scalability Tuner", expose target frame time, thermal danger threshold, hysteresis frames, forced weight, mock pressure, and a live weight/frame graph.  
Rejected Alternatives: Runtime HUD or serialized-only constants. Runtime HUD spends player-frame budget; constants slow iteration.  
Scalability potential: Designers can verify weak, middle, high, and ultra behavior by forcing any float on the same curve.  
Hardware Impact: Editor-only allocation and drawing; runtime cost remains zero when the editor window is closed.

## Decision 08 - Dedicated Telemetry Ring And PID Polish

Problem: The previous pass reused the generic homeostasis blackbox for scalability proof and called the controller "PID-like" without a separate integral/derivative pressure lane. That was not titanium.  
Solution: Add `ScalabilityTelemetryEntry` as a 32-byte explicit-layout DTO and allocate 300 entries from `GlobalDataVault` using `BufferID.ShinobuScalabilityOscilloscope`. Add proportional frame error, bounded integral buildup, positive derivative attack, and the existing 0.01/sec slow release. Dump `Dump_SCALABILITY_DICTATOR.bin` when frame time exceeds 20ms while weight is already at minimum survival.  
Rejected Alternatives: More boolean tiers, more generic blackbox overloading, or a private `NativeArray`. They either violate the continuous law or H-PHI vault sovereignty.  
Scalability potential: Low/Mobile gets immediate quality collapse and forensic proof when collapse is insufficient; Middle breathes around target; High/Ultra recover slowly and avoid resolution bounce.  
Hardware Impact: Added work is one 32-byte vault write per frame plus scalar PID math, estimated under 10us on i3/MX350. It buys post-mortem visibility and stabilizes thermal drift.

## Decision 09 - Compile Wall Boundary

Problem: Fresh `dotnet build` attempts now fail in `Assets/_Project/Scripts/PlayerBuilder.cs` because Construction/Habitat DTO namespaces are missing. This is outside SHINOBU_44 and appeared in the concurrent dirty worktree.  
Solution: Do not patch PlayerBuilder from the scalability domain. Record the dependency wall, run scoped hygiene scans, and preserve SHINOBU_44 isolation.  
Rejected Alternatives: Adding fake construction DTOs or asmdef references from the performance dictator. That would create exactly the compile-wall rot the mandate is attacking.  
Scalability potential: No runtime scalability effect; this protects architectural ownership.  
Hardware Impact: No frame-time impact. Developer iteration remains blocked by an external compile wall until the owning agent fixes `PlayerBuilder.cs`.

## Decision 10 - Ref Telemetry Hot Path

Problem: The dedicated 300-frame scalability telemetry ring existed, but the write path still resolved a `NativeArray<ScalabilityTelemetryEntry>` view each frame before writing. That is avoidable handle churn in the exact forensics path that must survive thermal pressure.  
Solution: Split telemetry setup into `EnsureScalabilityTelemetryHandle` and write live samples through `VaultBufferHandle.GetElementAsRef(vault, index)` field assignments. `NativeArray` views remain only for cold clear, dump, and editor oscilloscope copy. Reset cursor and PID memory on reset/vault rebind to avoid stale control-state bleed.  
Rejected Alternatives: Keeping full-struct `NativeArray` assignment in the frame loop, or caching a private persistent `NativeArray`. The first wastes hot-path work; the second violates H-PHI vault sovereignty and generation safety.  
Scalability potential: Low/Mobile keeps the blackbox write as a single direct vault element update; Middle/High/Ultra pay the same tiny constant cost while receiving deterministic forensic data.  
Hardware Impact: Static estimate saves a small but real per-frame handle/view cost, approximately 1-3us on i3/MX350-class silicon. No profiler claim; no build was launched in this loop by explicit user instruction.

## Decision 11 - Narrow Vault Resolution And New-Keyword Hygiene

Problem: The frame-state writer still called the five-buffer dictator resolver, which refreshed mock-heavy, mock-terrain, and CSV scratch handles even when only health/state DTOs were needed. The hot signal/blackbox paths also used struct object initializers with `new`, which is non-allocating for structs but still violates the project review heuristic for gameplay code.  
Solution: Add narrow vault helpers per responsibility. `WriteDictatorState` uses only `EnsureScalabilityStateHandles`; mock load, mock terrain, CSV scratch, telemetry, and snapshots each resolve their own handles. Replace gameplay-path struct initializers with `default` plus direct field stores in the SHINOBU mock job, `SystemHealthSignal`, `FrameTimeSignal`, `KillSwitchSignal`, `SystemHealthIndexSignal`, and `HomeostasisBlackBoxEntry`.  
Rejected Alternatives: Keep the broad resolver because the extra buffers are small, or argue that struct `new` does not allocate. Both are technically convenient and operationally sloppy; the dictator exists to remove small frame-loop debts before thermal pressure amplifies them.  
Scalability potential: Low/Mobile loses fewer vault-generation checks in the frame loop; Middle/High/Ultra keep identical output with clearer ownership boundaries for future consumers.  
Hardware Impact: Static estimate removes three unnecessary handle refreshes from the per-frame dictator state path and strips hot initializer noise. Expected gain is small, approximately 2-6us on i3/MX350-class silicon, pending profiler proof. No build was launched by explicit user instruction.

## Decision 12 - Continuous Math LOD Shader Scalar

Problem: `_MATH_LOD_LOW` was still published as a binary shader float. Even if the registry bit is only a compatibility lease, a 0/1 scalar creates the same visual pop the continuous dictator is supposed to prevent.  
Solution: Keep the transient registry lease for legacy consumers, but change the shader scalar to `ResolveMathLodLowWeight()`: smooth polynomial pressure from `GlobalQualityWeight`, smooth polynomial pressure from `SystemHealthIndex01`, and a `math.step` survival floor below about 0.1 quality. Refresh it after `UpdateGlobalQualityState` so it tracks the current frame's solved weight.  
Rejected Alternatives: Delete `_MATH_LOD_LOW` outright or keep it binary. Deletion risks shader compatibility; keeping it binary violates the no-dichotomy mandate.  
Scalability potential: Low devices drift into cheaper shader math instead of popping; Middle devices can partially degrade; High/Ultra stay near zero low-weight while still retaining emergency collapse below 0.1.  
Hardware Impact: One gated shader float write and scalar polynomial math, estimated under 2us when changed. It buys visual stability under thermal drift rather than measurable average FPS.

## Decision 13 - Override Continuity And Partial Mock Updates

Problem: Disabling forced `GlobalQualityWeight` reset the seed flag, allowing the controller to jump immediately to the newly desired weight and bypass slow release. CSV mock-load keys were also partial but shared one boolean flag, so a later `mock_vram_pressure=0` line could accidentally disable an earlier `mock_frame_spike_ms=20` line.  
Solution: Only reset `_globalQualityWeightSeeded` when enabling a forced override. Disabling preserves the current scalar and lets normal PID/slow-release math recover it. Synthetic mock load now clears only on a full disabled UI update or when no synthetic pressure remains; partial CSV updates preserve the active flag while any pressure source is still positive.  
Rejected Alternatives: Treat CSV/test controls as outside runtime rigor. These controls are used to reproduce thermal collapse; if they introduce jumps, the oscilloscope evidence becomes contaminated.  
Scalability potential: Low/Mobile test transitions now mimic real thermal recovery instead of snapping. Middle/High/Ultra forced tests can still clamp any float for verification.  
Hardware Impact: Runtime cost is two scalar branches in editor/test facade calls only. The gain is correctness of hysteresis evidence, not steady-state frame time.

## Decision 14 - Live Telemetry Count

Problem: The dedicated 300-frame ring is correct for dumps, but the editor oscilloscope copied all 300 entries even immediately after boot. Cleared zero entries looked like valid zero-quality data and contaminated the human tuning facade.  
Solution: Track `_scalabilityTelemetrySampleCount` separately from the ring cursor. Reset it on boot, shutdown reset, vault rebind, and buffer recreation; increment it on live telemetry writes up to capacity. The editor graph copies only live samples while crash dumps still serialize the fixed 300-entry ring.  
Rejected Alternatives: Infer validity from timestamp zero, or accept the editor graph pollution. Timestamp zero can be legitimate during early startup; polluted graphs undermine the exact hysteresis tuning surface this task required.  
Scalability potential: All tiers keep identical runtime behavior; designers get accurate weak/middle/high/ultra transition evidence from the first frames onward.  
Hardware Impact: One bounded integer increment per telemetry write, under 1us. This improves diagnostics, not steady-state frame rate.

## Decision 15 - Positive Frame-Time Sanitization

Problem: `UpdateGlobalQualityState` accepted finite `0ms` frame time as valid. Cleared DTOs or very early tuner calls could therefore look like infinite headroom and contaminate PID recovery, telemetry, and DRS evidence.  
Solution: Treat frame time as valid only when finite and positive. Invalid or zero values fall back to the resolved target frame time in the controller, state DTO, and DRS handoff.  
Rejected Alternatives: Keep `0ms` because real stopwatch samples should be positive. The problem is not the stopwatch path; it is cold cleared memory and editor/test calls reaching the same control math.  
Scalability potential: Low/Mobile no longer gets false recovery from an uninitialized sample; Middle/High/Ultra tuning evidence stays numerically sane during startup.  
Hardware Impact: One scalar comparison in the state path, under 1us. The gain is NaN/zero resilience, not average FPS.

## Decision 16 - Stochastic Boundary Semantics

Problem: `ShouldExecuteStochasticUpdate` used `sample <= weight`. At `GlobalQualityWeight == 0`, a stable hash with sample zero still passed, which violates absolute survival mode.  
Solution: Explicitly reject work for `weight <= 0`, accept all work for `weight >= 1`, and use strict `sample < weight` between those endpoints.  
Rejected Alternatives: Accept the tiny probability leak as statistically irrelevant. Absolute survival mode is a contract, not a probability suggestion.  
Scalability potential: Low/Mobile can mathematically collapse stochastic work to zero; Middle retains proportional thinning; High/Ultra execute all work at weight 1.  
Hardware Impact: Two endpoint branches and the same deterministic hash math. The gain is correctness of load shedding at the extremes.

## Decision 17 - Continuous Culling Scalar

Problem: `CullingMultiplier` still flipped between `1f` and `_lowCullingMultiplier` based on a pressure bit. That is a visible distance-pop risk and a binary remnant in the global scalability surface.  
Solution: Drive the scalar with `math.lerp(1f, _lowCullingMultiplier, ResolveMathLodLowWeight())`. The legacy `CullingDistanceSqueeze` bit remains only as compatibility/telemetry, while the shader/global multiplier follows the continuous pressure curve.  
Rejected Alternatives: Keep the bit flip because culling pressure is only one scalar. Distance culling is exactly where thermal spikes become visible pops, so it must breathe with the same float contract.  
Scalability potential: Low devices smoothly contract culling distance; Middle partially contracts; High/Ultra remain at full multiplier unless pressure accumulates.  
Hardware Impact: One lerp in the pressure policy path, under 1us. It trades no meaningful CPU time for less visible LOD/culling pop.

## Decision 18 - Public Scalar And Telemetry Clamp

Problem: The stochastic helper internally saturated `GlobalQualityWeight`, but the public `StochasticDecimationThreshold` exposed the backing field directly. The telemetry writer also accepted finite zero/negative frame values when called outside the normal `WriteDictatorState` sanitation path.  
Solution: Return `math.saturate(_globalQualityWeight)` from the public threshold and sanitize telemetry frame samples to the resolved target frame time unless they are finite and positive. Store a saturated quality scalar in the 300-frame ring.  
Rejected Alternatives: Rely on every caller to clamp the public scalar or assume telemetry is only called after sanitized state write. Public contracts must be defensive; blackbox data must not preserve cold/reset garbage as authoritative evidence.  
Scalability potential: Low/Mobile optional work cannot be accidentally over-scheduled by an out-of-range scalar; Middle/High/Ultra retain identical continuous probability semantics. Telemetry remains readable across weak, middle, high, and ultra test passes.  
Hardware Impact: One saturate on public scalar access and two scalar comparisons in telemetry, under 1us. The gain is contract integrity and forensic hygiene, not average FPS.

## Decision 19 - Vault-Backed Tuner State

Problem: The editor/CSV tuner facade clamped runtime scalars, but the explicit Task 18 contract says tuning values must read and write GlobalDataVault unmanaged memory during Play Mode. Static-only tuning state is invisible to vault diagnostics and weaker than the batch contract.  
Solution: Add `ScalabilityTuningDTO` as a 16-byte unmanaged record on `BufferID.ShinobuScalabilityTunerState`. `ApplyHardwareDictatorTuner` now writes target frame ms, emergency threshold, and hysteresis frames into the vault, then mirrors them into hot scalar fields. The editor facade reads the same DTO through `TryGetHardwareDictatorTuning`; handle recreation seeds the DTO from the scalar mirrors so vault rebinds cannot expose zeroed tuning.  
Rejected Alternatives: Keep the static fields because they are faster. Hot runtime still reads scalar mirrors; the vault DTO is the authoritative human-control surface and costs only one cold/editor ref write on changes.  
Scalability potential: Low/Mobile, Middle, High, and Ultra tuning passes now share one unmanaged state record, so designers can force curves without recompilation and without introducing parallel managed truth.  
Hardware Impact: Runtime steady-state hot path does not allocate and does not scan editor data. Tuner writes cost one 16-byte vault store on change; memory budget increases by 16 bytes plus existing handle metadata.

## Decision 20 - Mock Heavy Load Enters EWMA Once

Problem: `MockHeavyLoadSignal` affected the later raw SHI polynomial, but Task 05 explicitly required fake latency injection into the EWMA monitor. That meant the curve could degrade while the frame-history proof and oscilloscope still displayed the unmocked Stopwatch path.  
Solution: Apply the mock frame spike immediately after `Stopwatch.GetTimestamp()` sampling in `SampleFrameMetrics`, before FPS EWMA and frame-history writes. Remove the second frame-spike add from `ComputeDictatorRawShi`, leaving VRAM mock pressure there.  
Rejected Alternatives: Keep the mock only in raw SHI because it still moved `GlobalQualityWeight`. That fails the forensic requirement: EWMA, telemetry, and graph data must show the same synthetic load that caused the quality collapse.  
Scalability potential: Low/Mobile test passes now reproduce thermal collapse through the real frame-time control path; Middle/High/Ultra forced tests can verify slow release and visual-overkill recovery without renderer dependencies.  
Hardware Impact: One mock-signal read already backed by a vault handle and one scalar add when armed. No steady-state FPS claim; mock path is dev/test proof, not production cost center.

## Decision 21 - Canonical Mock Spike And Diagnostic Fallback

Problem: The EWMA injection path existed, but the emergency mock profile seeded `FrameSpikeMs = 0`. A developer could arm "Mock Heavy Load" and get no frame-time pressure unless they also edited the slider/CSV value. The oscilloscope also trusted the smoothed lane if the raw lane was invalid, without a final positive finite fallback.  
Solution: Add `DefaultMockFrameSpikeMs = 20f`. Emergency mock profiles store that canonical 20ms payload with flags disabled; arming the mock with no explicit pressure promotes the same 20ms spike. Non-finite tuner values clamp to zero before reaching vault memory. Emergency mock terrain/status now uses saturated weight, and editor oscilloscope copies fall back to the current target frame time when both raw and smoothed samples are invalid.  
Rejected Alternatives: Treat the mock as "developer must know to drag the slider." The XML demanded a 20ms fake-latency proof in a vacuum; a no-op armed mock is a false test surface.  
Scalability potential: Low/Mobile stress tests can reproduce a 20ms thermal frame through the real EWMA/PID path. Middle/High/Ultra tests can release the signal and verify slow recovery without renderer or AI dependencies.  
Hardware Impact: No steady-state runtime cost when the mock flag is disabled. When armed, cost remains one vault-backed signal read and scalar add. Oscilloscope fallback adds scalar checks in editor copy only.

## Decision 22 - Partial Mock Override Isolation

Problem: Seeding the dormant emergency mock with 20ms created a new ambiguity. A first-time CSV `mock_vram_pressure=0.5` call could arm both VRAM pressure and the dormant 20ms frame spike, even though the author only supplied the VRAM lane. That contaminates pressure-isolation tests.  
Solution: Snapshot whether `MockHeavyLoadSignal` was already armed. If it was inactive and a partial override supplies only VRAM, clear the dormant frame spike before applying the VRAM value. If it was inactive and a partial override supplies only frame spike, clear stale VRAM. Active mocks still preserve the other lane across partial CSV updates.  
Rejected Alternatives: Treat every enabled mock as always including 20ms. The XML requires a 20ms fake-latency proof, but the CSV bridge also needs lane-specific tests for VRAM and frame pressure.  
Scalability potential: Low/Mobile can test thermal frame pressure and memory pressure independently; Middle/High/Ultra can reproduce mixed pressure only when both lanes are explicitly active or already armed.  
Hardware Impact: Editor/CSV facade adds two scalar branches on mock changes only. No hot steady-state runtime allocation or DTO size change.

<SELF_AUDIT>
1. Did I output a boolean like `IsLowQuality`? No public contract was added; output is `GlobalQualityWeight`, `FractionalTimeSlice`, render scale, shader floats, and stochastic threshold. Legacy masks remain compatibility/fault containment, not the new scalability contract.
2. Is `ScalabilityStateDTO` ARM64 aligned? Yes: byte 0 `GlobalQualityWeight`, byte 4 `FractionalTimeSlice`, byte 8 `VramPressure`, byte 12 `ThermalIndex`, total 16 bytes, no `Pack=1`.
3. Did I avoid `{ get; set; }` on array structs? Yes. DTOs are raw fields and vault mutation uses refs/NativeArray values.
4. Does hysteresis prevent rapid oscillation? Yes. Weight attacks downward immediately and recovers at 0.01 per second after EWMA/hysteresis pressure settles.
5. Did I provide the tuner facade? Yes. `Hecton8/Core/Continuous Scalability Tuner` exposes live override and oscilloscope.
</SELF_AUDIT>

<SELF_AUDIT_POLISH>
<Task id="01" status="PASS">Docs/Archive, AgentLogs, binary ledger, and absent StreamingAssets were re-read. No authoritative hardware h8bin layout found; emergency mock profiles remain the fallback.</Task>
<Task id="02" status="PASS">No `QualitySettings.SetQualityLevel()` in touched dictator paths. Runtime output is scalar DRS/shader/vault data.</Task>
<Task id="03" status="PASS">Hot DTOs are public fields; no `{ get; set; }` in touched SHINOBU_44 runtime/editor files.</Task>
<Task id="04" status="PASS">Primary DTO: `ScalabilityStateDTO` offsets 0/4/8/12, total 16. Telemetry DTO: 0 ulong timestamp, 8 raw frame, 12 smoothed frame, 16 weight, 20 VRAM, 24 flags, 28 pad, total 32.</Task>
<Task id="05" status="PASS">`MockTerrainSamplerStatusJob` maps weight 0.1 to 90 percent skipped trilinear probability without terrain dependency.</Task>
<Task id="06" status="PASS">Stopwatch frame measurement and Burst EWMA function pointer retained; Burst attributes now compile synchronously.</Task>
<Task id="07" status="PASS">VRAM pressure and thermal index feed state, PID pressure, blackbox, and telemetry.</Task>
<Task id="08" status="PASS">GlobalQualityWeight is continuous and polynomial/PID pressure-based; no new low/high public mode.</Task>
<Task id="09" status="PASS">DRS target scale is `lerp(0.5, 1.0, weight)` through `IDynamicResolutionRuntime`.</Task>
<Task id="10" status="PASS">FractionalTimeSlice is `lerp(0.1, 1.0, weight)`.</Task>
<Task id="11" status="PASS">Deterministic stochastic helper uses stable hash threshold, no UnityEngine.Random.</Task>
<Task id="12" status="PASS">Fast attack, bounded integral, derivative attack, and 0.01/sec release resist bounce.</Task>
<Task id="13" status="PASS">Boot hardware hash/memory/VRAM clamp maximum weight to 0.6 on weak devices, while output remains float.</Task>
<Task id="14" status="PASS">GC freeze pulse remains bounded at five seconds.</Task>
<Task id="15" status="PASS">Dear Lie shader route publishes `_GlobalQualityWeight` and `_H8GlobalQualityWeight` instead of CPU material churn.</Task>
<Task id="16" status="PASS">Vault buffers use `UninitializedMemory` plus explicit `MemClear` on creation.</Task>
<Task id="17" status="PASS">Dedicated 300-frame telemetry ring exists and dump trigger covers 20ms frame time at zero quality.</Task>
<Task id="18" status="PASS">Editor facade is `Continuous Scalability Tuner`.</Task>
<Task id="19" status="PASS">CSV watch path is `scalability_curves.csv`; parser supports forced quality key.</Task>
<Task id="20" status="PASS">Editor oscilloscope now prefers the dedicated telemetry ring.</Task>
<Compile status="BLOCKED_OUTSIDE_DOMAIN">Core and Editor builds currently fail only in `PlayerBuilder.cs` missing Construction/Habitat DTOs. No SHINOBU_44 file is listed in compiler errors.</Compile>
</SELF_AUDIT_POLISH>

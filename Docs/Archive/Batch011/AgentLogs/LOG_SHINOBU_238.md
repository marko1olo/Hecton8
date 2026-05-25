# SHINOBU_238 Log

Top = old. Bottom = new.

## 2026-05-21 SHINOBU_238 Biolum Matrix Sync Pass

What was wrong:
- `BiolumPulseSyncRuntime.ScheduleStateJob` referenced `syncPulses`, `syncPulseAges`, `lockedPulses`, and `lockedPulseAges` without resolving/declaring them in that scope. This breaks the oscillator route before GPU upload.
- `GenerateMockLightingState` locked `BiolumSyncPulses` and `BiolumSyncPulseAges` but did not unlock them in `finally`.
- Black-box dump paths still used `Dump_BIOLUM_DIRECTOR.*`, not `Dump_SHINOBU_238.bin`.
- Fragment biolum waves used absolute world coordinates; local AUP coordinate use was not explicit.
- Editor tuner did not directly visualize the black-box telemetry ring.
- Editor-time byte-layout enforcement was missing; runtime validation alone was too late.

What was done:
- Repaired sync pulse DataVault locking/resolution for `AdvanceBiolumPhasesJob`.
- Added missing sync pulse unlocks in mock generation.
- Renamed mock init job to `GenerateMockLightingStateJob`; current Burst directive is `FloatMode.Fast` per SHINOBU presentation-domain mandate.
- Added AUP-depth darkness scalar and combined it with ambient/mock eclipse darkness before amplitude multiplication.
- Changed dump target to `Docs/AgentLogs/Dump_SHINOBU_238.bin` with a matching `.h8dump` mirror.
- Added `BiolumPulseLayoutGuard` editor assertion for DTO sizes and 16-byte matrix row offsets.
- Exposed `TryReadEditorTelemetryEntry` and added a preallocated 16-bar telemetry graph to `Abyssal Glow Tuner`.
- Updated `Hecton_IndirectVegetation.shader` to evaluate global biolum fragment waves from localized AUP coordinates.

Cinematic Cheats used:
- Four-row "Dear Lie" matrix: one global oscillator drives apparent individual plant autonomy.
- Spatial wave is a pure sine/interference phase offset, not simulated fluid/light transport.
- Darkness/depth activation is one scalar multiplier, not a full lighting query.
- Predator warning is fixed-slot pulse injection, not plant listeners.

Exact Microseconds saved:
- Per-flora material mutation route remains absent in assigned paths. Avoided cost estimate: 0.8-2.5 us per 1000 skipped renderer touches plus avoided material clone/GC risk.
- Per-plant Update route remains absent in assigned paths. Avoided cost estimate: 200-600 us/frame for 10,000 cosmetic callbacks on weak desktop CPU.
- Matrix upload route cost estimate: one 64B `Shader.SetGlobalMatrix`, approx 1 us CPU-side API cost excluding render-thread internals.
- Oscillator route cost estimate: 4 matrix rows plus 16 pulse slots, approx 1-5 us/frame on i3/MX350.
- Telemetry write cost estimate: one 32B ring write/frame, approx 0.2-0.5 us.

Verification:
- Static forbidden-pattern scan over assigned biolum/vegetation paths: PASS.
- `git diff --check`: PASS; only existing LF-to-CRLF warnings.
- Compile: NOT RUN. CPU guard measured 100 percent twice; protocol forbids dotnet/compile above 50 percent CPU.
- Runtime profiler/GC/frame debugger: NOT RUN.

<SELF_AUDIT agent="SHINOBU_238">
  <DTO name="BiolumPulseStateDTO" sizeBytes="64">
    <field name="Group1_Params" offset="0" type="float4" />
    <field name="Group2_Params" offset="16" type="float4" />
    <field name="Group3_Params" offset="32" type="float4" />
    <field name="Group4_Params" offset="48" type="float4" />
  </DTO>
  <DTO name="SyncPulseDTO" sizeBytes="32" />
  <DTO name="BiolumPulseTelemetryEntry" sizeBytes="32" ringEntries="300" />
  <VaultBuffers>
    <buffer name="BiolumPulseStateBufferId" id="70311" owner="SystemID.Vfx" />
    <buffer name="BiolumProfileFloats" owner="SystemID.Vfx" />
    <buffer name="BiolumBlackBox" owner="SystemID.Vfx" />
    <buffer name="BiolumSyncPulses" owner="SystemID.Vfx" />
    <buffer name="BiolumSyncPulseAges" owner="SystemID.Vfx" />
    <buffer name="BiolumMockWeatherSignal" owner="SystemID.Vfx" />
    <buffer name="BiolumMockPredatorSignal" owner="SystemID.Vfx" />
  </VaultBuffers>
  <ShaderGlobals>
    <matrix name="_GlobalBiolumDearLieGroups" upload="once per visual sync" />
    <vector name="_GlobalBiolumParams" contains="stateCount, GlobalQualityWeight, strobe, reserved" />
    <vector name="_GlobalBiolumClock" contains="time, cadence, frameCounter, reserved" />
    <vector name="_GlobalBiolumAupOffset" contains="localized AUP offset/profile hash payload" />
  </ShaderGlobals>
  <ForbiddenPatternScan result="PASS" scope="assigned biolum/vegetation paths" patterns="Material.SetFloat, sharedMaterial.SetFloat, GetComponent&lt;Renderer&gt;().material, per-plant Update" />
  <PhaseBound result="PASS" method="RepeatRadians modulo TwoPi" />
  <AUP result="PASS" gpuReceives="localized float coordinates, not double AUP" />
  <Scalability result="PASS" scalar="HomeostasisBrain.GlobalQualityWeight via _GlobalBiolumParams.y" binarySwitches="none in biolum shader path" />
  <GC result="STATIC_PASS" hotPathManagedAllocations="none introduced" profilerProof="not run due CPU guard" />
  <Compile result="BLOCKED_BY_POLICY" cpuPercent="100" dotnetOrCscRunning="false" />
</SELF_AUDIT>

## 2026-05-21 SHINOBU_238 Addendum - Legacy Bridge Accessor Purity

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile was not launched under the current guard protocol.

What was wrong:
- `HectonBiolumManager` still hid state mutation behind `GetCameraAup()` and read-like `Resolve*` helper names.
- `GetCameraPosition()` was mutation-free but still read live `Transform.position` from an accessor instead of returning an owner-phase snapshot.
- The touched fallback bridge still polled `GlobalRegistry.DataVault`, `TickDispatcher`, `Fluid`, and `Player` outside cold lifecycle wiring.
- A self-read found `SampleCameraCacheClockSeconds()` declared `static` while reading instance field `_cachedTickDispatcher`, which would be a compile failure.

What was done:
- Camera reference/position/AUP refresh moved to `RefreshCameraSnapshotForOwnerPhase(...)`; `GetCameraPosition()` and `GetCameraAup()` are cached snapshot reads.
- `DataVault`, `TickDispatcher`, `Fluid`, and `Player` are cached during lifecycle and rebound through `IGlobalRegistryHotSwapListener`.
- Mutating helper names were changed away from read-accessor forms; controller survival binding is now `TryBindSurvivalSystemFromPlayerContext()`.
- `SampleCameraCacheClockSeconds()` is now an instance method.

Cinematic Cheats used:
- None added. This pass removes architectural debt in the legacy bridge; the visual fake remains the four-row matrix and shader-local wave route.

Exact Microseconds saved:
- Expected low single-digit or sub-microsecond savings from fewer registry property reads and hidden camera/AUP refresh opportunities.
- No profiler number claimed; Unity import/profiler proof is still pending.

Verification:
- Old helper-name scan returned no hits in the touched manager/controller files.
- `GlobalRegistry.DataVault/TickDispatcher/Fluid/Player` hits in `HectonBiolumManager` are limited to cold cache fill.
- `GlobalRegistry.CelestialRuntimeSnapshot` remains as a documented snapshot bridge; no false zero-registry claim is made.
- Brace count for `HectonBiolumManager.cs`: `189/189`.
- Guarded targeted compile was skipped before build execution when CPU rechecked at `71`, `54`, and `85` percent, all with no `dotnet/csc` processes.

## 2026-05-21 Addendum - Legacy Zone Registry Growth Removal

What was wrong:
- `HectonBiolumManager` used three pre-sized `List<HectonBiolumZone>` registries for cave/ocean/floor zones. Pre-sized is not fixed-capacity; scene content above 32 entries can allocate and grow backing arrays in the runtime fallback bridge.

What was done:
- Replaced the three lists with fixed `HectonBiolumZone[32]` arrays and explicit counters.
- Added bounded duplicate insertion and compaction removal helpers.
- Added `_zoneRegistryOverflowCount` and telemetry flag bit `16` so saturated content is visible in the 300-frame ring.

Cinematic Cheats used:
- None new. This preserves the legacy fallback bridge as a bounded scalar/color support path while the visible flora/coral pulse remains the shader-matrix Dear Lie.

Exact Microseconds saved:
- Hot path: small direct-array/counter read improvement, not claimed as measured.
- Heap: avoids possible `List<T>` capacity-growth allocation when scene content crosses the old initial capacity.

Verification:
- Scoped static scan: no `List<`, `new List<`, `System.Collections.Generic`, LINQ, `Pack=1`, DTO auto-properties, or `UnityEngine.Random` remain in the SHINOBU_238 touched runtime lanes.
- Shader scan: assigned shader set still has no retired scalar phase globals and indirect vegetation no longer feeds `stableAupSeed` into the biolum phase path.
- Compile/import/profiler: still not launched. Latest guard: `CPU_LOAD=100`; active `dotnet` PIDs `11856`, `19480`, `20304`, `26312`, `28396`, `29124`, `30516`.

## 2026-05-21 Addendum - Legacy Touch Ripple Continuous Quality Budget

What was wrong:
- `HectonBiolumManager.PublishTouchRippleBuffer()` used a binary `ScalabilityTier` / `IsHighQualityTier` gate to publish either zero touch ripples or the full staged set.

What was done:
- Replaced the binary tier check with `HomeostasisBrain.GlobalQualityWeight`.
- Upload capacity now scales continuously from 0 to 16 via `smoothstep(0.12, 0.72, qualityWeight)` and `math.lerp`.
- Shader touch-ripple params now carry `writeCount`, `uploadBlend`, and `qualityWeight`.

Cinematic Cheats used:
- Touch response stays a bounded shader buffer and nearest-ripple visual cue; no per-object physics or per-flora truth was added.

Exact Microseconds saved:
- Not measured. Static expectation: low-quality/thermal frames upload fewer ripple rows and let shader consumers skip more touch-ripple work while high quality keeps full visual response.

Verification:
- Scoped scan: no `ScalabilityTier`, `IsHighQualityTier`, `HectonQualityTier`, or `GlobalRegistry.ScalabilityTier` remains in `HectonBiolumManager`.
- Compile/import/profiler: still blocked by guard. Refreshed sample: `CPU_LOAD=43`, but active `dotnet` PIDs remain `11856`, `19480`, `20304`, `26312`, `28396`, `29124`, `30516`.

## 2026-05-21 Addendum - Legacy Master Phase Writer Suppression

What was wrong: `HectonBiolumManager` suppressed `_BiolumIntensity` while PulseSync owned `_GlobalBiolumDearLieGroups`, but still published `_BiolumMasterPhase`. That was a residual competing bridge writer for biolum support phase.

What was done: `HectonBiolumManager.PublishGlobalBiolumPhase()` now samples `_GlobalBiolumParams.x` once and skips both `PublishBiolumMasterPhase()` and `_BiolumIntensity` writes while PulseSync is active. `ResetFloraShaderGlobals()` no longer clears `_BiolumMasterPhase` through the legacy manager under active PulseSync ownership.

Cinematic cheat used: individual flora/coral glow remains the shader Dear Lie: one 64-byte matrix plus local-coordinate wave math, not per-object material mutation or per-plant CPU oscillators.

Microseconds saved: expected direct CPU delta is sub-microsecond per visual frame; the concrete win is removing one legacy vector publication and preventing last-writer-wins support-scalar drift. Evidence class remains STATIC_SOURCE; Unity import, Frame Debugger, profiler, and GCMonitor proof are still pending behind the CPU guard.

## 2026-05-21 Addendum - Subagent Findings Reconciliation

What was wrong: `Hecton_IndirectVegetation` still used `stableAupSeed` for vertex biolum pulse phase and authored pulse offset. `BiolumPulseSyncRuntime.Dispose()` nulled `_dataVault` even if the dump worker failed to stop. Vault generation refresh could reacquire `70312` without restarting the dump writer. Two file-path helpers used `Resolve*` names while touching files or mutating cached path state.

What was done: indirect vegetation vertex biolum now uses `animatedPositionWS - renderOriginWS` and a local deterministic seed. Vault generation refresh restarts the dump worker after handle reacquisition. Failed dispose stop preserves `_dataVault` and keeps `_disposed=false` for retry. Path helpers were renamed to cold/build/find names.

Cinematic cheats used: the route remains one matrix plus local shader wave math; no per-plant CPU phase, material clone, physics fluid/light propagation, or object traversal was added.

Microseconds saved: steady-frame CPU delta is 0 us for the shader coordinate fix and dump lifecycle fix. The measurable value is precision safety and forensic availability. Evidence class remains STATIC_SOURCE; compile/import/profiler/Frame Debugger remain pending because the latest guard sampled CPU above 50 percent.

## 2026-05-21 SHINOBU_238 Addendum - Shader Matrix Row ABI Correction

Status: PENDING VERIFICATION. Static source only; no Unity import, shader import, Frame Debugger, profiler, or Play Mode proof.

What was wrong:
- Six assigned non-indirect shaders read `_GlobalBiolumDearLieGroups[row].rgb` as color and `.w` as intensity.
- The route ABI is one `Matrix4x4`: `.x phase`, `.y frequency`, `.z amplitude`, `.w spatialOffset`.
- Resulting risk: phase/frequency/amplitude data could be rendered as RGB and spatial offset could inflate brightness.

What was done:
- Added deterministic group tint helpers to:
  - `Assets/_Project/Art/Shaders/Hecton_CoralMaster.shader`
  - `Assets/_Project/Art/Shaders/Hecton_CoralMaster_GPUI.shader`
  - `Assets/_Project/Art/Shaders/Hecton_KelpMaster.shader`
  - `Assets/_Project/Art/Shaders/Hecton_KelpMaster_GPUI.shader`
  - `Assets/_Project/Art/Shaders/Hecton_SargassumMaster.shader`
  - `Assets/_Project/Art/Shaders/Hecton_ProceduralBio.shader`
- Replaced color reads with branchless group tint selection.
- Replaced intensity reads from `.w` with `.z`.
- Left `Hecton_IndirectVegetation.shader` `.w` usage intact because it is legitimate spatial wave offset.

Cinematic Cheat used:
- Color identity remains a shader-side deterministic fake keyed by matrix row index. No per-instance color buffer, no material mutations, no second constant buffer.

Exact microseconds saved:
- CPU delta: 0 us; this is a correctness/ABI fix, not a CPU optimization.
- Avoided alternative: second `Matrix4x4` or per-material color payload would add at least one extra global upload or `O(N)` material traffic. Rejected.
- GPU delta: effectively neutral; a small `step/lerp` tint helper replaces invalid payload reads and keeps the same single matrix fetch route.

Regression model:
- CPU: no new C# work.
- GC: no runtime allocation path.
- Memory: no new buffers or shader globals.
- Cadence: no change to oscillator cadence.
- Correctness: color/intensity now match the documented row ABI.
- Failure mode pending: shader compiler compatibility requires Unity import proof.

Verification:
- Static scan: assigned non-indirect shaders no longer contain `state.rgb`, `secondaryState.rgb`, `state.w`, or `secondaryState.w` in global-biolum color/intensity code.
- Static scan: `Hecton_IndirectVegetation.shader` still reports `.w` only as spatial offset.
- Diff check: PASS with existing LF-to-CRLF warnings only.
- Compile/import/profiler/frame debugger: PENDING.

## 2026-05-21 SHINOBU_238 Addendum - Emergency Mock Glow Coverage

Status: PENDING VERIFICATION. Static source only; compile/import/profiler proof blocked by CPU guard.

What was wrong:
- `GenerateEmergencyMockGlows()` allocated/requested 50,000 mock glow and AUP rows but seeded only four rows.
- The buffer route uses uninitialized memory, so the untouched tail could contain undefined fallback data.
- Black-box telemetry reported `SyncGroupCount` as `ActiveGlowingInstances`, mixing four shader rows with instance count.

What was done:
- `GenerateEmergencyMockGlows()` now seeds up to `MaxGlowInstances` rows with deterministic species, phase, color, frequency, and localized AUP coordinates.
- Added `_activeGlowingInstanceCount`.
- `ReleaseVaultHandlesOnly()` resets the active count with handle invalidation.
- `BiolumPulseTelemetryEntry.ActiveGlowingInstances` now writes `_activeGlowingInstanceCount` clamped to the fixed capacity.

Cinematic Cheat used:
- The system still avoids per-instance CPU animation. The 50,000-row seed is fallback/mock data coverage; live animation remains a four-row matrix plus shader-side row selection and spatial waves.

Exact microseconds saved:
- Hot path saved: no added hot CPU work; 0 us/frame versus any per-instance animation path.
- Cold path cost accepted: bounded 50,000-row fill during bootstrap/editor seed. This is a deterministic safety fill, not a recurring frame cost.
- Avoided alternative: `O(N)` per-frame instance update or material writes remains rejected; the update route stays `O(1)` matrix plus bounded sync pulse scan.

Regression model:
- CPU: cold bootstrap fill cost increases; per-frame cost unchanged except one integer clamp in telemetry.
- GC: no managed collections or strings added.
- Memory: no new buffers; existing Vault capacity is now initialized.
- Cadence: oscillator cadence unchanged.
- Correctness: telemetry count now distinguishes active instance coverage from shader matrix row count.
- Failure mode pending: Unity compile/import needed to prove no C# syntax or AOT issue.

Verification:
- Static scan: no hardcoded `ActiveGlowingInstances = SyncGroupCount` remains.
- Static scan: emergency mock fill uses `MaxGlowInstances`.
- Diff check: PASS with existing LF-to-CRLF warnings only.
- CPU guard: latest `CpuPercent=97 Dotnet=0 Csc=0`; compile not launched.

## 2026-05-21 SHINOBU_238 Addendum - First 20 Minutes Route Binding

Status: PENDING VERIFICATION. Static documentation only.

First 20 Minutes moment:
- World load
- Swim
- Hazard readability

Route impact:
- Biolum matrix sync makes abyss/coral/flora readable on the selected route without per-object animation or material mutation.
- Darkness/depth-driven glow response supports route risk perception without becoming gameplay truth.

Proof required:
- Unity import and Console.
- Selected-route Play Mode run.
- Frame Debugger proof of the matrix route.
- GCMonitor/profiler capture.
- Low/middle/high/ultra `GlobalQualityWeight` visual capture.

Parked work rejected:
- fauna/Leviathan shader rewrite
- second matrix/color buffer
- per-instance CPU glow animation
- new cross-domain authority surface

Exact microseconds saved:
- Documentation binding adds 0 us runtime cost.
- It prevents route-irrelevant visual expansion from consuming frame budget before the Copper Wire route is proven.

## 2026-05-21 SHINOBU_238 Support Vector Ownership Proof

What was wrong:
- `_BiolumIntensity` is still a live shader support vector, and `HectonBiolumManager` contains fallback writes to it.
- The PulseSync helper name still said `Legacy`, which obscured that the vector is now derived from the matrix route.

What was done:
- Verified `HectonBiolumManager` suppresses `_BiolumIntensity` update/reset writes when `_GlobalBiolumParams.x > 0.5`, which is the active PulseSync group-count signal.
- Renamed `ResolveLegacyBiolumIntensity` to `ResolveMatrixDerivedBiolumIntensity`.
- Updated the SHINOBU_238 route card and status/rationale logs with the support-vector ownership fence.

Cinematic Cheats used:
- `_BiolumIntensity` stays a support scalar derived from four matrix rows, not a per-object or world-zone pulse simulation while PulseSync is active.

Exact Microseconds saved:
- Avoided competing manager write on matrix-active frames: one `Shader.SetGlobalVector` call when the fallback manager would otherwise republish.
- Estimated direct saving: below 1 us on affected frames. Primary gain is deterministic ownership and removal of last-writer-wins visual flicker risk.

Verification:
- Static source review confirmed the guard path: `HectonBiolumManager.IsGlobalPulseSyncOwningLegacyIntensity()` reads `_GlobalBiolumParams.x` and suppresses fallback `_BiolumIntensity` writes/resets while PulseSync publishes active rows.
- Compile/import still not launched under the CPU guard.

## 2026-05-21 SHINOBU_238 Mock Job Mutability Reconciliation

What was wrong:
- Static alias review initially classified `GenerateMockLightingStateJob` weather and predator rows as sampled-only inputs.
- Source review showed that the job intentionally mutates `WeatherSignal[0]` and `PredatorSignal[0]` during cold/editor mock seeding.

What was done:
- Kept `WeatherSignal` and `PredatorSignal` write-capable with `[NoAlias]`.
- Recorded the distinction between mock seed mutability and oscillator sampled inputs in status/rationale.

Cinematic Cheats used:
- No simulation change. This preserves the one-row mock darkness/predator bridge used to avoid direct Celestial/Apex dependencies.

Exact Microseconds saved:
- Hot frame cost unchanged; the mock job is cold/editor seed work.
- No direct microsecond change. This avoids a compile-breaking `[ReadOnly]` annotation while retaining `[NoAlias]` for Burst alias proof.

Verification:
- Static alias scan confirms oscillator sampled inputs carry `[ReadOnly, NoAlias]`; mock weather/predator rows carry `[NoAlias]` because they are mutated by design.

## 2026-05-21 SHINOBU_238 Static Verification Addendum

What was wrong:
- Runtime proof is still unavailable because the CPU guard remains above the allowed compile threshold.

What was done:
- Re-ran source scans over assigned runtime/editor/shader paths for material mutation, stale legacy pulse globals, read-like editor facades, absolute AUP shader selectors, Unity time/random usage, LINQ/ToArray, and per-renderer material paths.
- Re-ran task extraction from `CURRENT_BATCH.md`: 20 task-heading lines, prompt hash `9d5db96674f0d27a`.
- Re-ran `git diff --check` over the touched SHINOBU files.

Cinematic Cheats used:
- No additional simulation was added. The route remains one four-row Dear Lie matrix plus local AUP shader waves.

Exact Microseconds saved:
- Static verification adds 0 runtime microseconds.
- Preserved avoided cost remains the same: no per-flora callback traversal and no per-material pulse mutation.

Verification:
- Forbidden source/shader pattern scans returned no live matches in assigned paths.
- `git diff --check` returned only existing LF-to-CRLF warnings.
- CPU guard remained above threshold; latest measured value during this addendum was 100.0 percent, with `dotnet=0` and `csc=0`. Compile/import not launched.

## 2026-05-21 SHINOBU_238 Route Capacity Correction

What was wrong:
- The route card described "8 group rows" even though `_GlobalBiolumDearLieGroups` is one `Matrix4x4` with exactly four `float4` rows.

What was done:
- Corrected the route card to state: 4 shader matrix rows, 16 cold profile slots, 16 sync pulse rows, 300 telemetry rows.

Cinematic Cheats used:
- The Dear Lie remains one four-row matrix. Extra authored profiles are cold tuning rows mapped into the four visible shader groups, not extra GPU rows.

Exact Microseconds saved:
- Documentation-only change. It prevents a future two-matrix expansion that would add another global upload and extra shader selection cost.

Verification:
- Source constants confirm `SyncGroupCount = 4`, `MaxGlobalBiolumStates = 16`, `ProfileFloatCount = 128`.

## 2026-05-21 SHINOBU_238 Legacy Float Route Retirement

What was wrong:
- `HectonBiolumController` still wrote `_HectonLegacyBiolumIntensity` and `_BiolumPulseTime` through `Shader.SetGlobalFloat`.
- Atlas/sonar pulse handlers used `Time.time` to publish biolum pulse timing even though SHINOBU_238 owns deterministic visual phase via the matrix route.
- Live source scan found zero first-party shader readers for those two legacy properties; only stale guide text and archives referenced them.

What was done:
- Removed the legacy shader property IDs, `ApplyShader`, all `Shader.SetGlobalFloat` calls, and biolum `Time.time` writes from `HectonBiolumController`.
- Kept local proxy-light response intact; that controller still owns authored local `Light` reactions and registration, not shader pulse authority.
- Updated `LORE_SYSTEMS_GUIDE.md` so future work does not resurrect `_BiolumPulseTime` as a biolum shader route.
- Updated the SHINOBU_238 route card to mark the legacy controller shader-float lane retired.

Cinematic Cheats used:
- One matrix route remains the only shader pulse authority. Apparent individual glow stays a GPU local-coordinate wave, not per-object C# pulse timing.

Exact Microseconds saved:
- Event frames avoid up to two dead `Shader.SetGlobalFloat` calls from Atlas/sonar bursts.
- Slow ticks avoid one dead scalar global write.
- Estimated direct saving: 0.2-2 us on affected frames; primary benefit is removal of a dead second authority and non-deterministic `Time.time` visual pulse write.

Verification:
- `rg` found no live `_BiolumPulseTime` or `_HectonLegacyBiolumIntensity` references in Assets code/guide after the patch; SHINOBU_238 audit docs intentionally mention the retired names.
- `rg` found no `Shader.SetGlobalFloat` or `Time.time` usage in `HectonBiolumController`.
- Unity compile/import still not launched because CPU guard remains above the protocol threshold.

## 2026-05-21 SHINOBU_238 Cold Mock Lock Trim

What was wrong:
- Manual C# diff review found the cold `GenerateMockLightingState()` path locking sync pulse and sync pulse age buffers despite not consuming those buffers.

What was done:
- Removed those two locks and the unused `Resolve()`/created checks from `GenerateMockLightingState()`.
- Left sync pulse locking in the actual owners: initialization, injection, expiration, and the scheduled oscillator.
- Added `BiolumPulseLayoutGuard.cs.meta` with a stable GUID for Unity import hygiene.

Cinematic Cheats used:
- No simulation change. This is lock-surface hygiene for the existing one-row mock seed.

Exact Microseconds saved:
- Cold boot/editor seed avoids two DataVault lock attempts.
- Hot frame cost unchanged; the purpose is narrower ownership and lower contention risk.

Verification:
- Static readback confirms `GenerateMockLightingState()` now locks only pulse state, profile floats, weather, and predator signal rows.

## 2026-05-21 SHINOBU_238 Vault Rebind Fence Hardening

What was wrong:
- `ReloadProfilesFromDiskEditor()` reused teardown force-completion for a manual editor action.
- Vault generation mismatch and DataVault service replacement could invalidate cached handles while the oscillator job still held locks and NativeArray views.

What was done:
- Added `TryFinalizeScheduledJobForEditorReload()`; editor reload now finalizes only completed jobs and otherwise returns without blocking.
- Added `FenceScheduledJobBeforeVaultHandleInvalidation()` and wired it into `BindDataVault()`, `EnsureVaultBuffers()`, and `TryRefreshExistingVaultHandlesNoAllocate()`.

Cinematic Cheats used:
- None. This is memory lifetime hardening for the existing matrix oscillator route.

Exact Microseconds saved:
- Hot frame cost unchanged.
- Rare editor reload avoids a possible forced completion hitch; Vault hotswap/compaction paths avoid stale-handle recovery cost and lock leaks.

Verification:
- Static scan confirms only teardown/vault-invalidation helpers call force completion; editor profile reload no longer does.

## 2026-05-21 SHINOBU_238 Post-Compaction Static Reconciliation

What was wrong:
- The live chat context had been compacted, so disk memory had to be treated as the only authority.
- One transient prompt-count command searched for XML `<task id>` tags. The actual SHINOBU_238 assignment uses literal `Task 01` through `Task 20` lines.
- Runtime/Unity proof is still blocked by the workstation CPU guard, not by a source decision.

What was done:
- Re-read `Docs/Tasks/Status_SHINOBU_238.md`, `Docs/AgentLogs/Rationale_SHINOBU_238.md`, and the SHINOBU_238 prompt block from `Docs/Tasks/CURRENT_BATCH.md`.
- Verified the prompt block hash remains `9d5db96674f0d27a` and the source contains `Task 01` through `Task 20`.
- Re-ran assigned static scans for per-material mutation, read-like editor facades, absolute AUP shader selector math, Unity random/time hot usage, LINQ/ToArray, and forbidden renderer material paths.
- Re-ran CPU/process guard: CPU 100 percent, `dotnet` 0, `csc` 0. Compile/import not launched.

Cinematic Cheats used:
- No new simulation was added. The route remains one four-row Dear Lie matrix plus local coordinate shader waves.
- No new CPU darkness or spatial wave job was split out; darkness remains a scalar inside the existing 4-row oscillator/mock kernels.

Exact Microseconds saved:
- Static reconciliation adds 0 runtime microseconds.
- Avoided compile launch under saturated CPU: protects local iteration rather than frame time.
- Preserved frame-side savings remain: one matrix upload and bounded 16-slot pulse scan instead of per-flora callbacks or per-renderer material writes.

<SELF_AUDIT_DELTA agent="SHINOBU_238" date="2026-05-21" status="STATIC_SOURCE_PASS_RUNTIME_PENDING_COMPILE_BLOCKED_BY_CPU_GUARD">
  <PROMPT_PROOF bytes="18120" taskCount="20" hash="9d5db96674f0d27a">Source headings are `Task 01` through `Task 20`; there are no XML `<task id>` children in this prompt.</PROMPT_PROOF>
  <STATIC_SCANS result="PASS">Assigned runtime/editor/shader paths have no `Material.SetFloat`, `sharedMaterial.SetFloat`, renderer `.material`, `TryReadEditor*`, `_GlobalBiolumAupOffset.x/z` selector math, `WorldPos.x` pulse math, LINQ/ToArray, Unity random, or `Time.deltaTime` hot usage.</STATIC_SCANS>
  <STRUCT_LAYOUT_CORRECTION>
    <dto name="BiolumPulseTelemetryEntry" sizeBytes="32">
      <field name="Frame" offset="0" size="4" />
      <field name="ActiveGlowingInstances" offset="4" size="4" />
      <field name="WavePulsesActive" offset="8" size="2" />
      <field name="QualityTier" offset="10" size="1" />
      <field name="Flags" offset="11" size="1" />
      <field name="OscillatorComputeTimeMs" offset="12" size="4" />
      <field name="GlobalDarknessScalar" offset="16" size="4" />
      <field name="Group0Phase" offset="20" size="4" />
      <field name="FrequencyMultiplier" offset="24" size="4" />
      <field name="PrimaryAmplitudeHdr" offset="28" size="4" />
    </dto>
  </STRUCT_LAYOUT_CORRECTION>
  <COMPILE_GUARD cpuPercent="100" dotnet="0" csc="0" result="BLOCKED_BY_POLICY">No dotnet rebuild, Unity import, shader import, profiler, or frame debugger proof was launched.</COMPILE_GUARD>
</SELF_AUDIT_DELTA>

## 2026-05-21 SHINOBU_238 Shader Consumer AUP Sweep

What was wrong:
- `_GlobalBiolumDearLieGroups` was also consumed by coral, kelp, sargassum, and procedural-bio shaders.
- Those shader consumers selected matrix rows and filament waves with `positionWS` plus `_GlobalBiolumAupOffset.x/z`, which is an absolute-float continuity trick and violates the local AUP wave requirement.

What was done:
- `Hecton_CoralMaster` and `Hecton_CoralMaster_GPUI`: global biolum selector/filament now use finite `localAupCoord`.
- `Hecton_KelpMaster` and `Hecton_KelpMaster_GPUI`: global biolum selector/filament now use finite `localAupCoord`.
- `Hecton_SargassumMaster`: global biolum selector/filament now use finite `localAupCoord`.
- `Hecton_ProceduralBio`: global biolum selector/filament now use finite `localAupCoord`.
- Indirect vegetation remains on the stricter per-instance route `positionWS - originWS`.

Cinematic Cheats used:
- Still one four-row global matrix. Per-organism variation is shader-local phase math, not CPU state or per-renderer material mutation.

Exact Microseconds saved:
- CPU delta: 0 us direct change; this is precision/visual-correctness hardening.
- Preserved avoided cost: 200-600 us/frame versus 10,000 cosmetic callbacks; 0.8-2.5 us per 1000 avoided renderer material touches.

Verification:
- Static shader scan confirms assigned flora/coral/procedural-bio consumers no longer add `_GlobalBiolumAupOffset.x/z` into global biolum selector math.
- `git diff --check` over the shader set passes with LF-to-CRLF warnings only.
- Unity shader import/variant warmup/Frame Debugger proof remains pending behind the no-build CPU guard.

Compile-wall caveat:
- `Hecton8.Core.csproj` currently includes `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs` despite the `Hecton8.VFX.Bioluminescence.Runtime.asmdef` boundary. This pass did not edit generated project metadata.

## 2026-05-21 SHINOBU_238 Ultra Polish Continuation

What was wrong:
- Editor facades used `TryReadEditor*` naming while locking Vault buffers and copying snapshots. That violates the global read-accessor purity doctrine.
- The tuner copied telemetry one entry at a time, causing repeated black-box ring lock attempts during an editor refresh.
- Cold mock seeding called `GenerateMockLightingStateJob.Execute()` directly, which made the Burst-labeled mock route look like an ordinary method call.
- Architecture proof was incomplete: no active SHINOBU_238 route card and no binary-ledger row for the biolum profile payload.

What was done:
- Renamed snapshot facades to `CopyEditorSpeciesTuning`, `CopyEditorMockWeather`, `CopyEditorPulseState`, `CopyEditorPulseControls`, and `CopyEditorTelemetryEntries`.
- Changed the telemetry tuner to use a preallocated 16-entry scratch array and one `Span<T>` copy per refresh.
- Replaced cold mock `job.Execute()` with `job.Run()`; the scheduled hot oscillator remains dispatcher-owned.
- Added `Docs/ARCHITECTURE/SHINOBU_238_BIOLUMINESCENT_MATERIAL_SYNC_ROUTE_CARD.md`.
- Added a SHINOBU_238 boundary row to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

Cinematic Cheats used:
- No flora truth simulation. One matrix row set plus local AUP shader phase offsets fakes individual organism glow.
- Depth/eclipse activation is one scalar multiplier, not a light-grid query.
- Predator and acoustic events remain bounded fixed-slot pulses.

Exact Microseconds saved:
- Per-entry telemetry graph lock attempt count drops from up to 16 to 1 per editor refresh. Shipping runtime unaffected.
- Direct per-renderer material path remains avoided: estimated 0.8-2.5 us per 1000 renderer touches plus material-clone GC risk.
- Per-plant callback path remains avoided: estimated 200-600 us/frame for 10,000 cosmetic callbacks.
- Hot oscillator remains one scheduled 4-row job plus 16 pulse slots, estimated 1-5 us/frame on i3/MX350.

<SELF_AUDIT agent="SHINOBU_238" date="2026-05-21" status="STATIC_SOURCE_PASS_RUNTIME_PENDING">
  <TASK_RECONCILIATION>
    <task id="01" name="MATERIAL_INSTANCE_ERADICATION_PASS" result="PASS">Assigned biolum/vegetation scan has no `Material.SetFloat`, `sharedMaterial.SetFloat`, renderer `.material`, or pulse keyword mutation.</task>
    <task id="02" name="MONOBEHAVIOUR_UPDATE_PURGE" result="PASS">Assigned paths have no per-flora `Update`, `FixedUpdate`, or `LateUpdate` pulse animator.</task>
    <task id="03" name="CS1612_METADATA_STATE_ANNIHILATION" result="PASS">Hot DTO pulse rows use raw fields and explicit layout; no DTO properties were added.</task>
    <task id="04" name="ARM64_PULSE_LAYOUT_ASSERTION" result="PASS">Editor layout guard asserts 16/32/64-byte DTO contracts and matrix row offsets.</task>
    <task id="05" name="EMERGENCY_MOCK_ECLIPSE_GENERATOR" result="PASS">Cold mock seed exists as `FloatMode.Fast` Burst-labeled `GenerateMockLightingStateJob` invoked through `Run()`.</task>
    <task id="06" name="BURST_GLOBAL_OSCILLATOR_KERNEL" result="PASS">`AdvanceBiolumPhasesJob` uses mandated `FloatMode.Fast` Burst directives and writes four phase rows.</task>
    <task id="07" name="THE_DEAR_LIE_SHADER_EVALUATION" result="PASS">`_GlobalBiolumDearLieGroups` matrix drives shader illusion; no per-material pulse route.</task>
    <task id="08" name="SPATIAL_WAVE_PROPAGATION_MATH" result="PASS">Shader wave math consumes local AUP coordinates via `positionWS - originWS`.</task>
    <task id="09" name="ECLIPSE_AND_DEPTH_ACTIVATION_LINK" result="PASS">A separate darkness job was rejected as a tiny job; ambient and depth darkness are inlined into mock and oscillator kernels.</task>
    <task id="10" name="PREDATOR_PROXIMITY_OVERRIDE_ROUTING" result="PASS">Predator strength stays in a decoupled mock/Vault bridge and bounded pulse slot route.</task>
    <task id="11" name="ASYNCHRONOUS_GPU_VARIABLE_UPLOAD" result="PASS">VISUAL_SYNC publishes one matrix and clears it on disable.</task>
    <task id="12" name="CONTINUOUS_SCALABILITY_SHADER_MATH" result="PASS">`GlobalQualityWeight` feeds cadence, amplitude, and shader ALU blend without binary quality switches.</task>
    <task id="13" name="AUP_PRECISION_EPICENTER_MATH" result="PASS">Sync pulse localization subtracts AUP before float downcast.</task>
    <task id="14" name="ROLLBACK_NETCODE_STATE_FENCE" result="PASS">Biolum phase state is presentation-only and absent from rollback/Merkle route scans.</task>
    <task id="15" name="ZERO_INIT_OVERHEAD_BYPASS" result="PASS">Pulse state/profile/mock buffers use Vault allocation and active-row overwrite; no hot clear path added.</task>
    <task id="16" name="TELEMETRY_BIOLUM_RECORDER" result="PASS">300-entry black-box ring and `Dump_SHINOBU_238.bin` route exist; runtime dump trigger still needs Play Mode proof.</task>
    <task id="17" name="BIOLUM_TUNER_EDITOR_WINDOW" result="PASS">UI Toolkit tuner shows pulse and telemetry graph through preallocated elements and `CopyEditor*` facades.</task>
    <task id="18" name="CSV_PULSE_PROFILES_INGESTOR" result="PASS">Cold CSV route uses byte scratch/manual parsing; no string split/float.Parse path added.</task>
    <task id="19" name="LIVE_PULSE_DEBUG_GIZMO" result="PASS">Editor live pulse boxes visualize four phase rows; no scene probes or per-object traversal.</task>
    <task id="20" name="SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION" result="PASS">Static scans, route card, binary ledger, and this log self-audit exist; runtime proof remains pending.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <dto name="BiolumPulseStateDTO" sizeBytes="64" alignment="16-byte rows, one 64-byte cache line">
      <field name="Group1_Params" offset="0" size="16" type="float4" />
      <field name="Group2_Params" offset="16" size="16" type="float4" />
      <field name="Group3_Params" offset="32" size="16" type="float4" />
      <field name="Group4_Params" offset="48" size="16" type="float4" />
      <padding bytes="0">64 bytes exactly; no tail padding required.</padding>
    </dto>
    <dto name="SyncPulseDTO" sizeBytes="32">
      <field name="OriginAUP" offset="0" size="24" type="double3" />
      <field name="WaveSpeed" offset="24" size="4" type="float" />
      <field name="ColorOverride" offset="28" size="4" type="uint" />
    </dto>
    <dto name="BiolumPulseTelemetryEntry" sizeBytes="32">
      <field name="FrameIndex" offset="0" size="4" />
      <field name="ActiveGlowCount" offset="4" size="4" />
      <field name="ActiveSyncPulseCount" offset="8" size="2" />
      <field name="QualityTier" offset="10" size="1" />
      <field name="Flags" offset="11" size="1" />
      <field name="OscillatorMicroseconds" offset="12" size="4" />
      <field name="DarknessScalar" offset="16" size="4" />
      <field name="Group0Phase" offset="20" size="4" />
      <field name="FrequencyMultiplier" offset="24" size="4" />
      <field name="PrimaryHdrAmplitude" offset="28" size="4" />
    </dto>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight=0.3`, cadence stretches continuously toward the low-Hz survival path, amplitudes collapse through scalar multiplication, and shader work weights toward vertex/local wave evaluation instead of extra fragment interference. Between 0.4 and 0.7, the same matrix route keeps spatial waves and depth response with reduced per-pixel overkill. Near 1.0, the shader spends saved CPU on stronger localized interference and filament detail. DTO layout, save identity, rollback authority, and route ownership do not change with quality.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privateNativeArrays="0">
    <buffer id="70311" name="BiolumPulseStateDTO" capacity="1" />
    <buffer name="BiolumProfileFloats" capacity="128 floats" />
    <buffer name="BiolumBlackBox" capacity="300 telemetry rows" />
    <buffer name="BiolumGlowStates" capacity="50000 rows" />
    <buffer name="BiolumGlowAupOrigins" capacity="50000 double3 rows" />
    <buffer name="BiolumSyncPulses" capacity="16 rows" />
    <buffer name="BiolumSyncPulseAges" capacity="16 floats" />
    <buffer name="BiolumMockWeatherSignal" capacity="1" />
    <buffer name="BiolumMockPredatorSignal" capacity="1" />
    <buffer name="BiolumMockDamageSignal" capacity="1" />
    <buffer name="BiolumSpeciesTuning" capacity="150 rows" />
    <buffer name="BiolumCsvScratch" capacity="16384 bytes" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <consumes>Cached DataVault handles plus `AupShiftSignal`, `FrameTimeSignal`, `AcousticPingSignal` snapshots and legacy signal bridge inputs outside Burst jobs.</consumes>
    <job name="AdvanceBiolumPhasesJob" outputHandle="_stateJobHandle" dependency="scheduled by owner phase; teardown-only completion">All non-overlapping NativeArray fields carry `[NoAlias]`; read inputs carry `[ReadOnly]`.</job>
    <job name="GenerateMockLightingStateJob" outputHandle="cold synchronous Run" dependency="none hot; cold seed only">`PulseState`, `WeatherSignal`, and `PredatorSignal` are mutable mock-seed lanes with `[NoAlias]`; `ProfileFloats` is `[ReadOnly, NoAlias]`.</job>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD result="PASS_STATIC">Runtime asmdef references Core.Contracts, Core, Core.Memory, Burst, Collections, Jobs, Mathematics, and Profiler.Core only. No direct AI/World/Environment/Celestial/sibling runtime assembly edge was added.</COMPILE_GUARD>
  <DEAR_LIE result="PASS">Before: `O(N)` CPU material or MonoBehaviour traversal for N glowing flora instances. After: `O(1)` four-row matrix update plus bounded `O(16)` sync-pulse scan; per-vertex/per-fragment spatial waves are GPU ALU over localized AUP coordinates.</DEAR_LIE>
  <VERIFICATION>
    <staticPatternScan result="PASS" />
    <diffCheck result="PASS" caveat="LF-to-CRLF warnings only" />
    <compile result="BLOCKED_BY_POLICY" cpuPercent="100" dotnetOrCscRunning="0" />
    <runtimeProfiler result="PENDING" />
    <frameDebugger result="PENDING" />
  </VERIFICATION>
</SELF_AUDIT>

## 2026-05-21 SHINOBU_238 Latest Addendum - ABI, Mock Coverage, Route Binding

Status: PENDING VERIFICATION. Static source/docs only; latest CPU guard observed `CpuPercent=97 Dotnet=0 Csc=0`, so compile/import was not launched.

What was wrong:
- Assigned non-indirect shaders treated matrix rows as RGB color/intensity payloads.
- Emergency mock glow generation seeded four rows while the fixed Vault lane reserves 50,000 uninitialized rows.
- Route card lacked explicit First 20 Minutes moment binding required by the product contract.

What was done:
- Added deterministic group tint helpers to coral, kelp, sargassum, and procedural-bio shaders; color no longer comes from matrix `.rgb`.
- Rewired shader intensity to matrix `.z`; kept `.w` as spatial wave offset only.
- Seeded up to `MaxGlowInstances` mock glow/AUP rows and changed telemetry to `_activeGlowingInstanceCount`.
- Added First 20 Minutes route binding: World load, Swim, Hazard readability.

Cinematic Cheats used:
- One `Matrix4x4` remains the only hot GPU pulse payload.
- Per-organism glow identity is a shader-side deterministic tint and local coordinate wave, not per-instance CPU state.

Exact Microseconds saved:
- CPU route remains `O(1)` matrix upload plus bounded 16-pulse scan, preserving the estimated 200-600 us/frame avoided versus 10,000 cosmetic callbacks.
- Shader ABI fix adds no CPU work.
- Mock glow coverage adds cold bootstrap memory writes only; hot frame delta remains 0 us except one integer clamp in telemetry.

Verification:
- `rg` scan: no assigned shader color/intensity use of `state.rgb`, `secondaryState.rgb`, `state.w`, or `secondaryState.w`; indirect vegetation retains `.w` only as spatial offset.
- `rg` scan: no `Material.SetFloat`, `Shader.SetGlobalFloat`, `_BiolumPulseTime`, or `_HectonLegacyBiolumIntensity` in assigned live route.
- `git diff --check`: PASS with LF-to-CRLF warnings only.
- Unity import/profiler/Frame Debugger: PENDING.

## 2026-05-21 SHINOBU_238 Sidecar Review Fixes - Local Vertex Basis, CSV Gate, Scalar Retirement

Status: STATIC VERIFICATION IN PROGRESS. Compile/import still gated by CPU policy; no build launched in this loop.

What was wrong:
- Coral, kelp, sargassum, and procedural-bio shaders still forwarded `positionWS` as `biolumLocalAupCoord`; that is floating-origin world space, not a local vertex/AUP basis.
- `BiolumPulseSyncRuntime.Tick()` could reach CSV file polling and `FileStream` work through `ApplyCsvOverridesIfReady()`.
- Shared render bridge and dispatcher still published dead `_GlobalBiolumPhase` scalar state even though no live shader consumed it.
- Route card overclaimed formal phase placement instead of naming the actual dispatcher interfaces in the current code.

What was done:
- Changed assigned object shaders to write object-relative local deltas into `biolumLocalAupCoord`; sargassum subtracts its drift-aware biolum origin.
- Wrapped CSV watcher state, setup/teardown, apply, file read, and path resolution in `UNITY_EDITOR`, removing the player hot-path and player compilation surface for CSV file I/O.
- Removed `_GlobalBiolumPhase` property IDs, publications, and dead shader declarations from the live `Assets/_Project` route.
- Corrected the route card to `IUpdatable.Tick` plus `ILateFrameTickable.LateFrameTick` behavior.

Cinematic Cheats used:
- Spatial wave diversity stays shader-side: one matrix row supplies phase/frequency/amplitude/offset, while local vertex coordinates fake organism-specific wave travel.
- Designer CSV hot reload remains an editor bridge; player runtime consumes baked/default unmanaged profile state.

Exact Microseconds saved:
- Object-relative shader basis: 0 CPU us; one vertex subtraction replaces unstable world-space phase input.
- CSV gate: removes unbounded filesystem stall/allocation risk from player tick and prevents the watcher/FileStream bridge from compiling into the player runtime class; exact saving is platform/filesystem dependent.
- `_GlobalBiolumPhase` retirement: removes one unused scalar global publication from the fallback/master-phase path; profiler proof pending.

Verification to rerun:
- `_GlobalBiolumPhase` live `Assets/_Project` scan; docs references are expected audit/status notes only.
- Assigned shader raw `positionWS` local-coordinate scan.
- Matrix row ABI scan for invalid `.rgb`/`.w` use.
- CSV call-site context scan for `UNITY_EDITOR`.
- `git diff --check` and CPU/dotnet/csc guard.

Verification result:
- `rg _GlobalBiolumPhase Assets/_Project`: no matches.
- Assigned shader raw local-coordinate scan: no matches.
- Matrix row ABI scan: only `Hecton_IndirectVegetation.shader` `.w` spatial-offset reads remain.
- CSV context scan: watcher state and CSV file read/path methods are under `UNITY_EDITOR`; unrelated binary profile load and crash dump `FileStream` routes remain cold non-CSV paths.
- Route card and binary ledger now describe CSV hot reload/scratch as editor-only for SHINOBU_238.
- Continuous row-count hardening: removed `ResolveStateCount(HectonQualityTier)` so the single-matrix ABI always remains four rows; quality degradation stays in continuous cadence/amplitude/shader weights.
- `git diff --check`: PASS with LF-to-CRLF warnings only.
- Build guard: `CpuPercent=100 Dotnet=0 Csc=0`; compile/import not launched by policy.

## 2026-05-21 SHINOBU_238 Addendum - Shader Variant Surface And Cold Route Audit

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; latest CPU guard remained above the allowed build threshold.

What was wrong:
- Shader edits can silently create first-use warmup debt if they add keywords, `multi_compile`, or `shader_feature` branches.
- CSV hot reload must remain an editor tooling bridge, not a player-runtime file polling lane.
- Previous proof needed a fresh post-row-count scan instead of relying on memory.

What was done:
- Re-ran assigned shader diff scan for added `#pragma`, `multi_compile`, and `shader_feature` lines.
- Re-ran forbidden route scans for `Shader.SetGlobalFloat`, `Material.SetFloat`, `_GlobalBiolumPhase`, `_BiolumPulseTime`, `_HectonLegacyBiolumIntensity`, raw world-position AUP assignment, and invalid matrix row color/intensity reads.
- Re-read CSV/FileStream contexts in `BiolumPulseSyncRuntime`.
- Re-ran `git diff --check` and CPU/dotnet/csc guard.

Cinematic Cheats used:
- No shader keyword route was added. Low through Ultra stay on one compiled shader path; `GlobalQualityWeight` changes cadence, amplitude, and ALU blend continuously.

Exact Microseconds saved:
- Variant audit adds 0 runtime cost.
- Avoided alternative: shader keyword quality branching can cause first-use compile stalls and warmup list growth; rejected.
- Player CSV route remains 0 us/frame because CSV watcher and override file read compile only in editor.

Verification:
- Shader diff scan: no added `#pragma`, `multi_compile`, or `shader_feature` lines in assigned shader diffs.
- Forbidden route scans: no live SHINOBU pulse/material matches except indirect vegetation `.w` as spatial offset. Broad rendering bridge scan still reports non-biolum `Shader.SetGlobalFloat` globals for AUP jitter, feature mask, supersaturation, narcosis, and death fade.
- CSV context scan: override watcher/apply/path/read are under `UNITY_EDITOR`; remaining `FileStream` routes are cold binary profile load and black-box dump.
- `git diff --check`: PASS with LF-to-CRLF warnings only.
- CPU guard: `CpuPercent=100 Dotnet=0 Csc=0`; compile/import not launched.

## 2026-05-21 SHINOBU_238 Addendum - Sidecar P1 Fault Dump I/O Fix

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile still blocked by CPU guard.

What was wrong:
- Sidecar audit found synchronous `DumpBlackBox()` file I/O reachable from `Tick()`/`LateFrameTick()` fault paths.
- The previous wording "fault-only" did not remove the managed path construction, `Directory.CreateDirectory`, or `FileStream` work from the gameplay call stack.

What was done:
- Added a cold owner-enable dump worker: the first cut used one `byte[]` snapshot buffer, one `AutoResetEvent`, one background `Thread`, and precomputed dump paths; the later Vault-scratch addendum below replaces that managed buffer with Vault buffer `70312`.
- Changed `DumpBlackBox()` to copy the 16-byte header plus 300 telemetry rows into the preallocated 9,616-byte buffer and signal the worker.
- Moved `Directory.CreateDirectory` and `FileStream` writes into `WriteBlackBoxDumpBytes()` on the worker route.
- Teardown now signals and joins the worker outside the normal frame tick.

Cinematic Cheats used:
- None. This is forensic plumbing: the visual route still stays one matrix plus shader-local waves.

Exact Microseconds saved:
- Steady frame: unchanged.
- Fault frame: removes synchronous directory creation and two file writes from the main thread. Remaining fault-frame work is one bounded 9,616-byte memory copy plus event signal.

Verification:
- Static call-site scan: `DumpBlackBox()` no longer calls `Path.GetFullPath`, `Path.Combine`, `Directory.CreateDirectory`, or `new FileStream`.
- Static I/O scan: `WriteBlackBoxDumpBytes()` is called only by `WriteQueuedBlackBoxDump()` on `BlackBoxDumpWorkerLoop`.
- Broad scalar scan caveat: `HectonShaderGlobalDataVaultBridge` still contains non-biolum `Shader.SetGlobalFloat` calls; SHINOBU pulse identifiers and material pulse routes remain retired.
- `git diff --check` for `BiolumPulseSyncRuntime.cs`: PASS with LF-to-CRLF warning only.
- CPU guard: `CpuPercent=100 Dotnet=0 Csc=0`; compile/import not launched.

## 2026-05-21 SHINOBU_238 Addendum - Sidecar P2/P3 And Editor Surface Fixes

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile still blocked by CPU guard.

What was wrong:
- Dump snapshot copied `blackBox.Length` rows into a buffer sized for exactly 300 telemetry entries.
- Dump worker shutdown could leave a stale `_blackBoxDumpThread` reference after join timeout.
- `Tick()` still wrote shader globals through `UploadShaderScalars()`, contradicting the late-frame VISUAL_SYNC route.
- Public editor tuning facades compiled into the player runtime class even though only the editor tuner uses them.

What was done:
- Clamped dump copy to `BlackBoxFrameCount`, copied the newest fixed forensic window, and wrote the clamped `EntryCount` to the header.
- Added dead-worker checks in queue/ensure paths and timeout failure recording during stop.
- Removed shader scalar upload from `Tick()` and made `LateFrameTick()` publish matrix/scalar globals from either finalized job output or cached state.
- Wrapped `CopyEditor*`, `TryWriteEditor*`, and `TryTriggerEditorGlobalPulse()` in `UNITY_EDITOR`; corrected editor telemetry cursor wrapping for oversized source rings.

Cinematic Cheats used:
- No simulation added. The visual route remains one global matrix plus shader-local waves; all fixes are route discipline and forensic safety.

Exact Microseconds saved:
- Steady frame: no expected change in shader global count.
- Fault frame: bounded to one 9,616-byte copy; no arbitrary buffer-length copy or main-thread file write.
- Player compile surface: strips editor DataVault facades from shipping runtime; frame-time saving is 0 us, compile/link surface is lower.

Verification:
- Static shader upload scan: `Tick()` no longer calls `UploadShaderScalars()`; shader global writes are in `UploadShaderGlobals()`/`ClearShaderGlobals()`.
- Static dump bounds scan: no `EntryCount = blackBox.Length`; no `for (i < blackBox.Length)` dump loop.
- Static editor facade scan: SHINOBU editor facade declarations are inside `UNITY_EDITOR`, with editor-window call sites only.
- CPU guard: compile not launched; latest guard had `CpuPercent=99 Dotnet=0 Csc=0`. An earlier same-turn guard saw live `dotnet` processes; CPU remains the active blocker either way.

## 2026-05-21 SHINOBU_238 Addendum - Burst Directive Compliance

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile still blocked by CPU guard.

What was wrong:
- `GenerateMockLightingStateJob` and `AdvanceBiolumPhasesJob` still used `FloatMode.Deterministic`.
- SHINOBU_238 is a VFX presentation route, not rollback, kinematics, or authoritative gameplay state, so the exception does not apply.

What was done:
- Switched both Burst jobs to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- Left deterministic hashing/RNG seeds intact for mock/fallback visual inputs; that does not require deterministic Burst float mode.

Cinematic Cheats used:
- No new simulation. The Dear Lie remains four matrix rows plus shader-local waves.

Exact Microseconds saved:
- Expected saving is small because the oscillator is four rows plus bounded pulse scan; Burst Inspector/profiler proof is still pending.
- Structural gain: Burst is now allowed to use fast math on ARM64/x86 for visual-only phase math.

Verification:
- Static scan: both `BurstCompile` attributes in `BiolumPulseSyncRuntime.cs` now contain `FloatMode.Fast` and `FloatPrecision.Standard`.
- Compile/import/profiler: not launched; latest CPU guard remained above 50 percent.

## 2026-05-21 SHINOBU_238 Addendum - Matrix Row Shader Authority And Vault Dump Scratch

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile is still gated by CPU and process guard.

What was wrong:
- Assigned shader consumers still had residual scalar phase/amplitude paths: `_BiolumMasterPhase`, `_BiolumIntensity.x`, `_GlobalBiolumClock`, `_GlobalBiolumAupOffset`, or `safeClock`.
- Indirect vegetation still used global clock in overkill/interference even after the Burst oscillator owned the matrix phase.
- Fault dump staging used a private managed `byte[]`, which violates the H-PHI/Vault ownership rule for persistent diagnostic storage.

What was done:
- Rewired assigned flora/coral/sargassum/procedural-bio/indirect shader waves to consume `_GlobalBiolumDearLieGroups[row].x/y/z/w` as phase/frequency/amplitude/spatialOffset.
- Removed assigned shader declarations/uses of `_BiolumMasterPhase`, `_BiolumIntensity.x`, `_GlobalBiolumAupOffset`, `_GlobalBiolumClock`, and `safeClock`; group color is deterministic shader tint, not matrix payload.
- Added Vault buffer `70312` `BiolumBlackBoxDumpScratchBufferId`, exact 9,616-byte capacity, for black-box dump staging.
- Replaced `_blackBoxDumpBytes` and `new byte[BlackBoxDumpByteCount]` with native scratch writes and background `ReadOnlySpan<byte>` file writes from the Vault buffer.
- Updated the SHINOBU route card and binary payload ledger with the new dump scratch boundary.

Cinematic Cheats used:
- Flora autonomy is still faked in shader from one global matrix and local vertex coordinates. No per-plant CPU oscillator, no material mutation, no extra shader keyword branch.

Exact Microseconds saved:
- Steady frame: no additional CPU work; still one matrix route plus bounded support globals.
- Fault path: managed heap allocation for the 9,616-byte dump buffer is removed from owner enable; fault copy remains fixed 9,616 bytes.
- Shader route: CPU saves are authority/maintenance savings, not measurable frame work; scalar phase paths are no longer required by assigned shaders.

Verification:
- Assigned shader scan: no `_BiolumMasterPhase`, `_BiolumIntensity.x`, `_GlobalBiolumAupOffset`, `_GlobalBiolumClock`, or `safeClock`; remaining `state.w` hits are spatial-offset use.
- Shader variant scan: no added `#pragma`, `multi_compile`, or `shader_feature`.
- Runtime scratch scan: no `_blackBoxDumpBytes` or `new byte[BlackBoxDumpByteCount]`; `70312` is requested through DataVault and used by dump copy/write paths.
- External-domain caveat: Leviathan/fauna shaders still consume `_GlobalBiolumClock`/`_GlobalBiolumAupOffset`; not edited under SHINOBU_238.

## 2026-05-21 SHINOBU_238 Addendum - Build Guard Reconciliation

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile is blocked by the active CPU/process guard.

What was wrong:
- Status still contained an older guard result saying no `dotnet` process was active.
- A coarse prompt counter returned 22 `Task NN` matches because it also counted non-task references inside the SHINOBU_238 XML block.

What was done:
- Re-read the SHINOBU_238 XML block from `Docs/Tasks/CURRENT_BATCH.md`.
- Counted only real task-heading lines: `Task 01` through `Task 20`; prompt hash remains `9d5db96674f0d27a`.
- Re-ran CPU/dotnet/csc guard and updated status/rationale to the current blocker.

Cinematic Cheats used:
- None. This is proof hygiene only; the visual route remains one matrix plus shader-local waves.

Exact Microseconds saved:
- Runtime 0 us.
- Avoided build contention: compile/import was not launched while CPU was 100 percent and `dotnet` PID `29148` was active.

Verification:
- Prompt extraction: 20 real task-heading lines, hash `9d5db96674f0d27a`.
- Build guard: CPU `100`, active `dotnet` PID `29148`, no `csc` process.
- Compile/import/profiler: not launched by explicit policy.

## 2026-05-21 SHINOBU_238 Addendum - Editor Pulse AUP Precision

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile is still blocked by guard.

What was wrong:
- `TryTriggerEditorGlobalPulse()` hashed and scaled editor pulse positions by direct absolute `originAUP` float casts.
- The method is editor-only, but it still writes Vault-backed pulse/profile state and must obey the local-AUP precision rule.

What was done:
- The editor pulse facade now fails closed when no active `BiolumPulseSyncRuntime` exists.
- It subtracts the runtime presentation origin with `AupPrecisionMath.LocalDeltaDouble(originAUP, aupReference)` and downcasts only the localized delta.

Cinematic Cheats used:
- No object simulation added. The editor trigger still mutates one matrix row; individual plant response remains shader-local.

Exact Microseconds saved:
- Player runtime 0 us.
- Editor-only button path adds one bounded local-delta calculation and removes precision-risk from absolute float coordinates.

Verification:
- Static scan: no direct `(float)originAUP` or `originAUP.x/y/z` math remains in `TryTriggerEditorGlobalPulse()`.
- Compile/import/profiler: not launched; CPU/process guard still blocks it.

## 2026-05-21 SHINOBU_238 Addendum - Build Guard Refresh

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile remains blocked by CPU guard.

What was wrong:
- The prior guard addendum recorded active `dotnet` PID `29148` as the current blocker.
- A later guard sample changed the process state: CPU stayed saturated, but `dotnet`/`csc` produced no current process output.

What was done:
- Updated status and rationale to distinguish historical process blocker from the latest guard state.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime 0 us.
- Avoided compile contention while CPU remained at `100` percent.

Verification:
- Latest CPU guard: `100`.
- Latest process guard: no `dotnet`/`csc` process output.
- Compile/import/profiler: not launched by explicit policy.

## 2026-05-21 SHINOBU_238 Addendum - Dump Worker Rebind Fence

Status: STATIC SOURCE ONLY. Runtime/import/profiler proof remains pending; compile is still blocked by CPU guard.

What was wrong:
- DataVault rebind and teardown could release Vault handles after signaling the dump writer even if the writer failed to join.
- That is unsafe after moving dump scratch into Vault buffer `70312`, because the writer may still hold a resolved native view.

What was done:
- `StopBlackBoxDumpWorker()` now returns `bool`.
- `BindDataVault()` and Vault generation-mismatch refresh paths abort handle invalidation if the writer cannot stop.
- `OnDisable()` and `Dispose()` release cached Vault handles only after confirmed writer shutdown.

Cinematic Cheats used:
- None. This is memory-safety fencing for the forensic route.

Exact Microseconds saved:
- Steady frame 0 us.
- Rare teardown/hotswap path may retain handles on timeout instead of invalidating memory under a live writer.

Verification:
- Static call-site scan: every `StopBlackBoxDumpWorker()` call now observes the boolean or aborts before handle invalidation, including generation-mismatch refresh.
- Compile/import/profiler: not launched; CPU guard remained above threshold.

<SELF_AUDIT agent_id="SHINOBU_238" domain="BIOLUMINESCENT_MATERIAL_SYNC_ARCHITECT" evidence="STATIC_SOURCE_ONLY" runtime_proof="PENDING">
  <TASK_RECONCILIATION count="20" prompt_hash="9d5db96674f0d27a">
    <TASK id="01" status="PASS_STATIC">Material instance biolum route removed/avoided in assigned lanes; no assigned per-flora `Material.SetFloat` route remains.</TASK>
    <TASK id="02" status="PASS_STATIC">No individual plant/coral/glow-rock cosmetic `Update` oscillator found in assigned route; centralized dispatcher owner remains.</TASK>
    <TASK id="03" status="PASS_STATIC">Hot DTOs use raw public fields; `BiolumPulseStateDTO` has four raw `float4` rows and no C# properties.</TASK>
    <TASK id="04" status="PASS_STATIC">Editor layout guard validates 64-byte pulse matrix and 16-byte row offsets.</TASK>
    <TASK id="05" status="PASS_STATIC">`GenerateMockLightingStateJob` seeds deterministic darkness/weather/predator mock state without Celestial dependency.</TASK>
    <TASK id="06" status="PASS_STATIC">`AdvanceBiolumPhasesJob` advances four phase rows in Burst with mandated `FloatMode.Fast` for this presentation-only route.</TASK>
    <TASK id="07" status="PASS_STATIC">GPU receives `_GlobalBiolumDearLieGroups` matrix; assigned shaders select rows and fake per-plant autonomy.</TASK>
    <TASK id="08" status="PASS_STATIC">Assigned shaders compute spatial waves from local/object-relative coordinates and matrix `.w` spatial offset.</TASK>
    <TASK id="09" status="PASS_STATIC">Darkness scalar combines ambient/mock eclipse and AUP depth activation inside bounded jobs.</TASK>
    <TASK id="10" status="PASS_STATIC">Predator/damage/acoustic pulse routes remain Vault/Signal decoupled and affect frequency/amplitude without plant listeners.</TASK>
    <TASK id="11" status="PASS_STATIC">Late-frame visual sync owns `Shader.SetGlobalMatrix`; simulation `Tick` does not publish shader globals.</TASK>
    <TASK id="12" status="PASS_STATIC">`GlobalQualityWeight` drives cadence/amplitude/shader ALU blend continuously; no binary quality row-count switch remains.</TASK>
    <TASK id="13" status="PASS_STATIC">Runtime sync pulses and editor pulse trigger subtract AUP reference before float downcast.</TASK>
    <TASK id="14" status="PASS_STATIC">Pulse matrix remains presentation-only and excluded from rollback/save identity in route docs and source scans.</TASK>
    <TASK id="15" status="PASS_STATIC">Pulse state and large mock rows request Vault memory with `UninitializedMemory` where overwritten by deterministic seed/fill.</TASK>
    <TASK id="16" status="PASS_STATIC">300-frame telemetry ring is Vault-owned; fault snapshot uses Vault-owned 9,616-byte scratch and background file writer.</TASK>
    <TASK id="17" status="PASS_STATIC">Abyssal Glow Tuner uses editor-only `CopyEditor*`/`TryWriteEditor*` facades and a caller-owned telemetry span.</TASK>
    <TASK id="18" status="PASS_STATIC">CSV tuning bridge is editor-only/human-readable; player hot path does not poll CSV files.</TASK>
    <TASK id="19" status="PASS_STATIC">Editor pulse debug boxes read matrix rows and display `sin(phase) * amplitude` without scene traversal.</TASK>
    <TASK id="20" status="PASS_STATIC">Self-audit, route card, binary ledger, and static scans exist; Unity import/profiler proof remains blocked by CPU guard.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT primary="BiolumPulseStateDTO" size_bytes="64" alignment="16-byte rows / 64-byte payload">
    <FIELD name="Group1_Params" offset="0" size="16" semantic="phase,frequency,amplitude,spatialOffset"/>
    <FIELD name="Group2_Params" offset="16" size="16" semantic="phase,frequency,amplitude,spatialOffset"/>
    <FIELD name="Group3_Params" offset="32" size="16" semantic="phase,frequency,amplitude,spatialOffset"/>
    <FIELD name="Group4_Params" offset="48" size="16" semantic="phase,frequency,amplitude,spatialOffset"/>
    <MATH>16 * 4 = 64 bytes; offsets 0/16/32/48 are 16-byte aligned; total payload is one L1 cache line and one Matrix4x4 constant-buffer upload.</MATH>
    <RELATED name="BiolumPulseTelemetryEntry" size_bytes="32">300 rows = 9,600 bytes plus 16-byte dump header = 9,616-byte fixed dump scratch.</RELATED>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    Low quality collapses C# work by increasing oscillator cadence interval toward the 5Hz range and reducing amplitude/detail through continuous scalar math; shader consumers blend toward vertex/local cheap waves. Middle quality keeps local spatial propagation and bounded secondary waves. High/Ultra keep the same 64-byte matrix but spend more shader ALU on interference/filament detail through `GlobalQualityWeight`; no DTO layout, authority route, save identity, or matrix row count changes.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_arrays="0 for persistent diagnostic/native state">
    <BUFFER id="70300" name="BiolumGlowStates" type="GlowStateDTO" capacity="50000"/>
    <BUFFER id="70301" name="BiolumGlowAupOrigins" type="double3" capacity="50000"/>
    <BUFFER id="70302" name="BiolumProfileFloats" type="float" capacity="128"/>
    <BUFFER id="70303" name="BiolumBlackBox" type="BiolumPulseTelemetryEntry" capacity="300"/>
    <BUFFER id="70304" name="BiolumSyncPulses" type="SyncPulseDTO" capacity="16"/>
    <BUFFER id="70305" name="BiolumSyncPulseAges" type="float" capacity="16"/>
    <BUFFER id="70306" name="BiolumMockWeatherSignal" type="MockWeatherSignal" capacity="1"/>
    <BUFFER id="70307" name="BiolumMockPredatorSignal" type="MockPredatorProximitySignal" capacity="1"/>
    <BUFFER id="70308" name="BiolumMockDamageSignal" type="MockCombatDamageSignal" capacity="1"/>
    <BUFFER id="70309" name="BiolumSpeciesTuning" type="BiolumSpeciesTuningDTO" capacity="150"/>
    <BUFFER id="70310" name="BiolumCsvScratch" type="byte" capacity="16384" boundary="editor tooling"/>
    <BUFFER id="70311" name="BiolumPulseState" type="BiolumPulseStateDTO" capacity="1"/>
    <BUFFER id="70312" name="BiolumBlackBoxDumpScratch" type="byte" capacity="9616"/>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <JOB name="GenerateMockLightingStateJob" consumes="profile floats" mutates="pulse state, mock weather, mock predator" aliasing="[NoAlias] on all native lanes; profile floats [ReadOnly, NoAlias]"/>
    <JOB name="AdvanceBiolumPhasesJob" consumes="profile floats, weather, predator, sync pulses, pulse ages" mutates="pulse state" aliasing="[NoAlias] on pulse state and [ReadOnly, NoAlias] on sampled lanes"/>
    <HANDLE input="dispatcher/current frame dependency via scheduled owner phase" output="_stateJobHandle registered with H8Memory/SystemID.Vfx"/>
    <FENCE>Hot path does not force-complete jobs; completion is attempted in late-frame finalization, teardown/editor reload are explicit non-hot fences.</FENCE>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Runtime asmdef references `Hecton8.Core.Contracts`, `Hecton8.Core`, `Hecton8.Core.Memory`, and Unity packages; no AI/World/Celestial/sibling VFX runtime reference is added. Latest build guard: CPU 100 percent with active dotnet PIDs 11856, 19480, 20304, 26312, 28396, 29124, and 30516; compile/import not launched by policy.
  </COMPILE_GUARD>
  <DEAR_LIE>
    Before: O(N) CPU material/object updates for N visible glowing flora. After: O(1) four-row Burst matrix update plus bounded 16-pulse scan; apparent individual glow is shader-local wave math from local coordinates and matrix rows.
  </DEAR_LIE>
</SELF_AUDIT>

# SHINOBU_333 LOG

## 2026-05-22 Ballast Buoyancy Solver

What was wrong:
- `SubmarineAutoLevelBallastController.ApplyMassDistribution()` used ballast fill to compute total mass and wrote `_hull.mass = totalMass` when `SubmarineFluidDynamics` was absent. That is a dynamic Rigidbody.mass ballast hack.
- `Submarine6DIntegratorJob` still added scalar `math.lerp(-BallastLiftN, BallastLiftN, state.BallastRatio01)` into buoyancy, making ballast a lift knob instead of water/air/pressure math.
- Ballast state existed as normalized fill floats, with no explicit tank volume, compressed-air pressure, ambient pressure failure, or 300-frame ballast black box.

What was done:
- Added `Assets/_Project/Scripts/Physics/Vehicles/SubmarineBallastBuoyancyContracts.cs`.
- Added explicit `[StructLayout(LayoutKind.Explicit, Size = 32)] BallastTankDTO` with mandated offsets: volume 0, water 4, air ATM 8, flags 12, pump rate 16, private padding 20/24/28.
- Added `BallastTankCommandDTO`, `SubmarineBallastFluidSampleDTO`, `SubmarineBallastForcePacketDTO`, `SubmarineBallastTelemetryEntry`, `SubmarineBallastTuningDTO`, and `SubmarineBallastProfileDTO`.
- Added `GenerateMockFluidDisplacementJob`, `EvaluateBallastTanksJob`, and `CalculateBuoyancyForceJob`, all Burst deterministic.
- Added cold `ReadOnlySpan<byte>` CSV parser with FNV-1a vehicle hashes and manual float parser, no `float.Parse`.
- Added BufferID allocations `71771..71778` for SHINOBU_333 tanks, commands, fluid samples, force packets, telemetry, profiles, tuning, and CSV scratch. The earlier `71820..71827` draft was rejected after ledger re-check because SHINOBU_264 async buoyancy owns `71820..71831`.
- Patched `SubmarineAutoLevelBallastController` to seed Vault tank DTOs, prepare per-tank commands, write AUP/depth fluid samples, schedule the ballast solver, mirror legacy fill floats from tank DTOs, and write 300-frame black-box telemetry.
- Removed the direct `_hull.mass = totalMass` fallback. Ballast weight now affects center of mass and external vertical force, not Rigidbody mass.
- Patched `SubmarineDynamicsContracts.cs` so `BallastLiftN` no longer contributes scalar vertical lift in `Submarine6DIntegratorJob`.
- Added editor tools: `OOP_Buoyancy_Scanner`, `SubmarineBallastTunerWindow`, and selected-submarine gizmo drawing tank fill plus buoyant/gravity force vectors.
- Wrote scanner reports:
  - `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_333.json`
  - merged `shinobu333SubmarineBallastScanner` into `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`.

Cinematic cheats used:
- Air expulsion is a sparse `MovementAcousticSignal` emitted from pressure/liters state on deterministic cadence. No air particles, no CPU bubble simulation, no audio mixing in the solver.
- Emergency wave test uses a triangle-wave 10m mock swell and density variation. No sinusoidal/particle ocean dependency required for ballast stress testing.
- Visual debug is editor-only gizmo geometry; no runtime spawned debug meshes.

Exact microseconds saved:
- Dynamic `Rigidbody.mass` write removal: estimated 10-40 us/frame risk reduction on i3/MX350-class hardware by avoiding PhysX mass resync and solver churn. Exact profiler proof blocked by unrelated compile wall.
- Scalar `BallastLiftN` removal: 0-1 us/frame direct ALU saved; real gain is correctness, not speed.
- Native tank DTO traversal instead of managed component buoyancy: estimated 5-15 us/frame for four tanks; exact profiler proof pending Unity import/profiler.
- Pressure failure check: cost <1 us/frame for four tanks.
- Dear-lie hiss signal: <2 us only when emitting, 0 us when inactive; replaces unbounded particle/audio CPU work.
- Mock sampler disabled in production: 0 us/frame.

Verification:
- Targeted scan: 59 source files, 0 dynamic Rigidbody.mass hacks, 0 Physics.OverlapSphere water query hacks, 0 direct AddForceAtPosition sites.
- `rg "\.mass\s*="` over Gameplay/Physics/Vehicles shows only `SubmarineCoreDirector.ApplyProfileMassToHull`, a cold profile mass assignment, not dynamic ballast.
- Gated build attempt 1 failed because ignored Unity-generated `Hecton8.Core.csproj` did not include the new runtime file. Local generated-project compile item was added for verification.
- Gated build attempt 2 reached unrelated VRSomatic/Gyro/Metabolism/Fauna errors; no SHINOBU_333 file appeared in the compiler errors.

Open integration dependency:
- The repo does not currently produce a green `Hecton8.Core.csproj` build due unrelated sibling-domain missing symbols. SHINOBU_333 work is statically clean under that build attempt but cannot claim full-project compile success until those files are repaired.

## 2026-05-22 Verification Addendum

What was wrong:
- The third compile attempt was still required by protocol after CPU/process gating cleared.

What was done:
- Re-sampled CPU and process state before launch: no `dotnet`/`csc` processes, CPU near 21.9%.
- Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`.
- Build terminated after about 60 seconds with `MSB6006: "csc.exe" exited with code -1` and no C# source diagnostics.
- Post-check found no `dotnet`/`csc` process, but total CPU sampled at 99.6%; no fourth compile was launched.

Cinematic cheats used:
- None in this verification pass.

Exact microseconds saved:
- Runtime: 0 us. This was compile verification only.
- Engineering-time containment: stopped at three-strike boundary instead of editing VRSomatic/Gyro/Metabolism/Fauna sibling domains or hammering the compiler under saturated CPU.

Verification:
- Attempt 1: generated project missing SHINOBU_333 compile item.
- Attempt 2: unrelated dependency errors, no SHINOBU_333 file in diagnostics.
- Attempt 3: compiler process exit -1 with no source diagnostics.
- Final evidence class remains STATIC_SOURCE plus targeted scans; full green build is blocked outside SHINOBU_333 authority.

## 2026-05-22 Polish Addendum

What was wrong:
- The first BufferID allocation collided with SHINOBU_264 async buoyancy lanes.
- The route card had static proof but no explicit review disposition.
- The CSV parser existed without a cold source file and without explicit report proof that Data Monolith readiness is still blocked.
- Mutating Vault ensure calls used read-looking SHINOBU_333 names, making hot/cold ownership harder to audit.

What was done:
- Kept SHINOBU_333 Vault ownership at `71771..71778` and recorded rejected `71820..71827`.
- Added `Docs/Reports/SHINOBU_333_SELF_AUDIT.xml` with the 20-task reconciliation, struct layout, Vault status, dependency graph, and yellow items.
- Added `Data/Physics/vehicle_ballast_profiles.csv`.
- Wired cold CSV ingestion: `FileStream` -> Vault scratch `71778` -> `ReadOnlySpan<byte>` parser -> profile rows `71776`.
- Renamed SHINOBU_333 mutating setup path to `Ensure*Cold`; fixed/post-fixed ballast schedule and completion remain on pure `TryReadBallast*` accessors.
- Marked the route card and JSON reports as `YELLOW_STATIC_SOURCE_ONLY`, with no green compile or Data Monolith claim.

Cinematic cheats used:
- No new CPU simulation. Pneumatic air remains a sparse acoustic signal; profile CSV only changes cold tuning limits.

Exact microseconds saved:
- Hot accessor split: steady-state ALU save is 0 us, but removes surprise Vault allocation/growth spikes from ballast fixed/post-fixed work.
- CSV bridge: 0 us/frame after cold load; cold read bounded to 32768 bytes.
- BufferID collision repair: 0 us/frame, removes memory alias/corruption risk.

Verification:
- `Docs/Reports/SHINOBU_333_SELF_AUDIT.xml` now exists.
- `Data/Physics/vehicle_ballast_profiles.csv` now exists.
- `Docs/ARCHITECTURE/SHINOBU_333_SUBMARINE_BALLAST_BUOYANCY_ROUTE_CARD.md` states `YELLOW / STATIC_SOURCE_ONLY`.
- Full compile not rerun in this addendum per rebuild protection; previous compile wall remains external.

<SELF_AUDIT ref="Docs/Reports/SHINOBU_333_SELF_AUDIT.xml" disposition="YELLOW_STATIC_SOURCE_ONLY">
  <summary>20 tasks reconciled as PASS under static-source evidence; promotion requires Unity import, green compile, Play Mode, GC/profiler, and Data Monolith proof.</summary>
  <layout>BallastTankDTO=32 bytes: float@0, float@4, float@8, uint@12, float@16, uint pads@20/24/28.</layout>
  <vault>71771 tanks, 71772 commands, 71773 fluid samples, 71774 force packets, 71775 telemetry ring, 71776 profiles, 71777 tuning, 71778 csv scratch.</vault>
  <known_yellow>Data Monolith static payload absent; legacy Rigidbody centerOfMass/angularDamping/inertiaTensor bridge awaits a dedicated vehicle mass-properties packet route.</known_yellow>
</SELF_AUDIT>

## 2026-05-23 Polish Addendum

What was wrong:
- Subagent audit found missing `.meta` files for three SHINOBU_333 assets.
- Ballast sample count was derived directly from `GlobalQualityWeight`, which could flip near thresholds under thermal oscillation.

What was done:
- Added stable `.meta` files for `SubmarineBallastBuoyancyContracts.cs`, `OOP_Buoyancy_Scanner.cs`, and `vehicle_ballast_profiles.csv`.
- Clarified that `Data/Physics/vehicle_ballast_profiles.csv` is external cold source data; its sidecar is repository identity hygiene, not a Unity import claim.
- Added `SubmarineBallastFluidSampleDTO.ActiveSampleBudget` at offset 148, reusing prior padding inside the fixed 160-byte DTO envelope.
- Added owner-phase sample-budget smoothing and 2.5s hysteresis before the Burst force job consumes the integer sample count.
- Updated route card, status, rationale, and self-audit to reflect the hysteresis and metadata proof.

Cinematic cheats used:
- No new CPU simulation. Quality still changes analytical sample budget only; pneumatic feedback remains sparse acoustic signaling.

Exact microseconds saved:
- Runtime steady-state addition: owner phase pays a few scalar ops per fixed tick.
- Low-quality ballast force path still avoids up to three analytical submerged-ratio samples per submarine.
- Metadata fix: 0 runtime us; prevents nondeterministic Unity import GUID churn.

Verification:
- GUID scan shows each `f333...001/002/003` appears only in its intended `.meta` file.
- `SubmarineBallastFluidSampleDTO` remains 160 bytes by explicit layout; offset 148 now holds the stable sample budget and offset 152 keeps the 8-byte pad.
- No full build launched in this addendum; rebuild gate remains external/blocked until CPU and sibling-domain compile state permit a meaningful attempt.

## 2026-05-23 Assembly Boundary Addendum

What was wrong:
- The compile-wall mandate required explicit proof that SHINOBU_333 did not create direct sibling runtime assembly coupling.

What was done:
- Scanned `Assets/**/*.asmdef`.
- Confirmed `Assets/_Project/Scripts/Physics/Vehicles` has only `Hecton8.Physics.Vehicles.Editor.asmdef`; no `Hecton8.Physics.Vehicles.Runtime.asmdef` exists.
- Recorded that SHINOBU_333 did not add or modify runtime asmdef references.
- Cleaned indentation in the cold `EnsureVaultBufferCold<T>` helper signature to remove a false review hazard.

Cinematic cheats used:
- None. This is compile-wall proof and source hygiene only.

Exact microseconds saved:
- Runtime: 0 us.
- Developer hardware: avoids a new assembly-boundary migration and the associated root assembly recompile churn.

Verification:
- `rg --files -g "*.asmdef" Assets | rg "Physics|Vehicles"` finds the editor-only vehicles asmdef and no vehicles runtime asmdef.
- `Hecton8.Physics.Vehicles.Editor.asmdef` references `Hecton8.Core` and is `includePlatforms: Editor`.
- No build launched in this addendum.

## 2026-05-23 Timing Proof Addendum

What was wrong:
- `ComputeMicros` represented owner schedule-to-completion elapsed time, not profiler-proven Burst job wall-time.

What was done:
- Added `ForceFlagTimingProxy` to SHINOBU_333 force/telemetry flags.
- Owner completion now ORs `ForceFlagTimingProxy` into the patched force packet and telemetry row when setting `ComputeMicros`.
- Reports and self-audit now state that exact Burst wall-time requires profiler/Burst instrumentation.

Cinematic cheats used:
- None. This is black-box evidence hygiene.

Exact microseconds saved:
- Runtime hot Burst cost: 0 us.
- Owner completion adds one flag OR and one telemetry flag assignment.

Verification:
- `SubmarineBallastConstants.ForceFlagTimingProxy = 1u << 3`.
- `CompleteBallastSolverJob` patches the packet flag before telemetry and dump checks.
- `PatchBallastTelemetryComputeMicros` copies packet flags into the ring row.

## 2026-05-23 Hot Snapshot Addendum

What was wrong:
- The fixed/post-fixed PID suppression bridge still called `SubmarineDynamicsRuntime.TryGetActiveGyroRouteForEntity`.
- Ballast sample prep read `HomeostasisBrain.GlobalQualityWeight` live.
- AUP conversion read `GlobalSignals.CurrentRuntimeOriginAup()` from a read helper.

What was done:
- Added owner-phase snapshots for `GlobalQualityWeight` and runtime-origin AUP.
- Replaced the direct SHINOBU_332 runtime call with a cached read-only Vault handle to `BufferID.Shinobu332GyroCounters`.
- Hot PID paths now call `RefreshShinobu332GyroRouteStateFromCachedVault`, which only resolves an already-acquired generation handle and compares `LastTargetEntityHash` to target/fallback hashes.

Cinematic cheats used:
- None. This is route purity and hot-path isolation.

Exact microseconds saved:
- Estimated <1 us/submarine steady-state on i3/MX350-class hardware.
- Main gain is risk removal: no branchy sibling runtime lookup and no live global-origin/quality read in fixed ballast math.

Verification:
- `rg` finds no `SubmarineDynamicsRuntime.` usage in `SubmarineAutoLevelBallastController.cs`.
- The only remaining `HomeostasisBrain.GlobalQualityWeight` read is in `RefreshGlobalQualityWeightSnapshotCold`.
- The only remaining `GlobalSignals.CurrentRuntimeOriginAup()` read is in `RefreshRuntimeOriginAupSnapshotCold`.
- Full build not launched: latest CPU samples were 64.2% and 75.5%, above the 50% rebuild gate.

## 2026-05-23 Scanner Scope Addendum

What was wrong:
- `OOP_Buoyancy_Scanner` treated `Physics.OverlapSphereNonAlloc` as a violation but did not record that scope in the generated report.

What was done:
- Updated the editor-only report builder so rerunning the scanner preserves current SHINOBU_333 proof fields.
- Added `overlapScannerScope` to the sidecar and shared physics reports.

Cinematic cheats used:
- None. This is proof reproducibility.

Exact microseconds saved:
- Runtime: 0 us.
- Prevented regression class: CPU broadphase water-volume queries in Vehicles/Physics, allocating or non-allocating.

Verification:
- `overlapScannerScope` now states that both `Physics.OverlapSphere` and `Physics.OverlapSphereNonAlloc` are counted.
- Static follow-up found the generated report builder was missing `independentHotAudit`; the builder now emits that field as well.

## 2026-05-23 Independent Hot Audit Addendum

What was wrong:
- A second read-only pass was required after replacing the direct SHINOBU_332 runtime call with cached Vault counter reads.

What was done:
- Spawned a read-only auditor for the hot snapshot patch.
- Closed the auditor after receiving findings.

Cinematic cheats used:
- None. This is source verification only.

Exact microseconds saved:
- Runtime: unchanged from the hot snapshot patch, estimated <1 us/submarine.
- Risk removed: no fixed/post-fixed direct sibling runtime/global quality/AUP/global registry/scene-search route found by the auditor.

Verification:
- Auditor reported no issue.
- Auditor confirmed SHINOBU_332 route suppression is read-only/non-owning through cached `BufferID.Shinobu332GyroCounters`.
- Auditor found no obvious compile hazard or hidden allocation in the added fields/methods.
- Residual: build/profiler/GCMonitor proof still not run because rebuild gate remains closed; CPU samples reached 64.2%, 75.5%, and 100.0%.

## 2026-05-23 Cached Handle Read Addendum

What was wrong:
- `TryReadVaultBuffer` was used by fixed/post-fixed ballast reads, but a cache miss could call `TryGetGenerationHandle`.
- That path did not allocate, but it still hid Vault metadata refresh behind a read-looking helper.

What was done:
- Removed the `TryGetGenerationHandle` fallback from `TryReadVaultBuffer`.
- Hot ballast paths now use only already cached generation handles and fail closed if the handle is missing, stale, or blocked by a compaction fence.
- Updated the route card, binary ledger, self-audit XML, scanner report builder, and both physics report JSON files to record the cached-handle-only contract.

Cinematic cheats used:
- None. This is authority-route hygiene.

Exact microseconds saved:
- Valid-handle steady state: approximately 0 us, because the normal read path is unchanged.
- Miss/stale-handle path: removes a Vault metadata lookup from fixed/post-fixed cadence on low-end CPUs; exact profiler proof remains absent.

Verification:
- `TryReadVaultBuffer` now calls only `TryResolveVehiclesPhysicsVaultBuffer`.
- `TryGetGenerationHandle` remains only in `EnsureVaultBufferCold` and `TryResolveExistingVaultBuffer`.
- Reports still parse; full compile/profiler/GCMonitor proof pending the rebuild gate.

## 2026-05-23 DTO Padding Visibility Addendum

What was wrong:
- Support DTO padding fields in the SHINOBU_333 ballast contract were public even though padding is not semantic state.

What was done:
- Changed support DTO padding fields in `SubmarineBallastFluidSampleDTO`, `SubmarineBallastForcePacketDTO`, `SubmarineBallastTuningDTO`, and `SubmarineBallastProfileDTO` to `private`.
- Preserved every `[FieldOffset]` and total size.

Cinematic cheats used:
- None. This is ABI hygiene.

Exact microseconds saved:
- Runtime: 0 us.
- Risk removed: accidental external use of meaningless padding fields.

Verification:
- Targeted scan found no public `_pad*` fields in SHINOBU_333 runtime/controller DTOs after the patch.

## 2026-05-23 Shared Report Merge Repair Addendum

What was wrong:
- A read-only subagent found `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` had been clobbered by another agent's report and no longer contained `shinobu333SubmarineBallastScanner`.
- `OOP_Buoyancy_Scanner.BuildReport()` lagged behind the SHINOBU_333 sidecar on the GUID duplicate-scan proof and the specific compile-wall wording.

What was done:
- Re-added `shinobu333SubmarineBallastScanner` to the shared physics report without deleting the current SHINOBU_346/340 data after a second concurrent overwrite.
- Updated the scanner report builder so rerunning it preserves `GUID scan found no duplicates` and the exact blocked-external compile proof from the sidecar.
- Added a shared-report race policy: the SHINOBU_333 sidecar remains authoritative if another agent rewrites the shared report wholesale.

Cinematic cheats used:
- None. This is report reproducibility and concurrent-agent proof hygiene.

Exact microseconds saved:
- Runtime: 0 us.
- Review/integration risk removed: shared report no longer loses SHINOBU_333 evidence when other agents write their own physics entries.

Verification:
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` parses as JSON.
- `rg` confirms `shinobu333SubmarineBallastScanner`, `GUID scan found no duplicates`, and the exact Hecton8.Core compile-wall proof are present in the shared report, sidecar, and scanner generator after the second merge.

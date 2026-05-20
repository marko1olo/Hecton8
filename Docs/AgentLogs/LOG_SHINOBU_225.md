# LOG_SHINOBU_225

## 2026-05-20T11:10:00Z - LASER_CUTTER_DOD_REWRITE

What was wrong:
- `LaserCutter` no longer had a direct `Physics.Raycast`; archaeology proved the live route already defers through `EquipmentInteractionHandler.TryRaycastPrimary` and `RaycastCommand.ScheduleBatch`.
- The real hot-path violation was CPU `ParticleSystem` ownership: `LaserCutter.UpdateSparks` moved spark transforms, rewrote emission, and called `Play/Stop`.
- Cutter-adjacent reactions in `SealedDoor` and `SargassumCutResponder` still held `ParticleSystem` fields and CPU emission calls.
- No SHINOBU-owned cutter DTO/job/telemetry/editor proof existed for the batch mandate.

What was done:
- Added `LaserCutRequestDTO` explicit 64-byte layout with validator: offsets 0/24/36/40/44/48 for origin, direction, power, range, tool hash, parent entity.
- Added DOD buffers, owner-local `BufferID` block 71320-71335, deterministic request sequencing, 300-entry black-box ring, and `Dump_SHINOBU_225.bin` failure path.
- Added Burst jobs: mock cutter request generation, cooldown gate, AUP-safe RaycastCommand build, and hit evaluation into deformation/decal/drain/VFX/telemetry DTOs.
- Added staged `PowerDrainSignal`, `DebrisSpawnSignal`, and `VfxSparkRequestSignal` publication. No direct battery mutation.
- Replaced `LaserCutter` spark `ParticleSystem` logic with GPU signal staging and DataVault request staging.
- Removed cutter-adjacent `ParticleSystem` code from `SealedDoor` and `SargassumCutResponder`; both now publish typed debris signals.
- Added `LaserCutterSpecsCsvParser` with `ReadOnlySpan<byte>` parsing.
- Added UI Toolkit tuner, editor gizmo, static inquisition script, sidecar report, self-audit XML, status, and rationale files.
- Added non-destructive `shinobu_225_laser_cutter_dod` appendix to `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json`.

Cinematic Cheats used:
- "Dear Lie" hull denting: `LaserCutDeformationStateDTO` writes AUP center, normal, radius, depth, heat, and progress; shader/decal systems fake deformation instead of mutating geometry.
- Spark/melt presentation: GPU debris/spark requests scale continuously by `GlobalQualityWeight`; no prefab/object spawning.
- SDF evaluation is bounded and deterministic; it outputs DTOs, not mesh truth.

Exact microseconds saved:
- Direct measured profiler proof: PENDING. Build/profiler could not be run because CPU guard returned 100%.
- Static estimate for removed `LaserCutter` `ParticleSystem` transform/emission/Play/Stop path: 20-150 us per active impact frame on i3/MX350-class hardware.
- Static estimate for avoided duplicate live raycast scheduler: 40-120 us per cutter frame by preserving the existing deferred interaction backend instead of adding a second query path.
- Static estimate for avoided mesh/decal geometry mutation: 300-3000 us per heavy cut event.
- Static estimate for avoided prefab/object spark bursts: 80-300 us per burst plus allocator/batcher risk.

Verification:
- `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_225.json`: focused cutter scan reports 0 sync raycast, 0 `Instantiate`, 0 `ParticleSystem`, 0 mesh mutation text.
- `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json`: contains SHINOBU_225 appendix with the same pass counts.
- `Docs/Reports/SHINOBU_225_SELF_AUDIT.xml`: written.
- Compile: BLOCKED by project guard. `Win32_Processor.LoadPercentage` returned 100; no `dotnet`, `csc`, or `VBCSCompiler` process was active. Build was not launched.

## 2026-05-20T11:26:49Z - ULTRA POLISH RECONCILIATION

What was wrong:
- `LaserCutRequestDTO` used offsets 52/56/60 for Frame/Flags/RequestSequence. The XML required those bytes to be explicit padding.
- `TryScheduleRaycastBatch` could still run cold bootstrap logic during the simulation route, and live staging could acquire Vault handles if bootstrap failed.
- `SealedDoor` called into `LaserCutterDodRuntime` for generic door sparks, creating avoidable Gameplay -> Tools coupling.
- Tuner/gizmo proof was incomplete: no HitAUP XYZ/battery watts readout and no green hit sphere/yellow normal vector.

What was done:
- Restored `LaserCutRequestDTO` to exact 64-byte ABI and created `LaserCutRequestMetaDTO` as a separate 64-byte metadata lane.
- Added owner-local `RequestMetaBuffer=71336` and updated mock, cooldown, raycast-build, evaluation, telemetry, validator, runtime, and gizmo paths to use request+meta.
- Hot live routes now use already-acquired handles only: `QueueLiveRequest`, post-raycast evaluation, and impact VFX staging resolve with `allowAcquire:false`.
- Registered scheduled raycast handle with `H8Memory.RegisterActiveJob(SystemID.GameplayTools, ...)`.
- Replaced `SealedDoor` Tools-runtime spark call with local `DebrisSpawnSignal` publishing and continuous quality-weight quantity scaling.
- Added telemetry `BatteryWatts`; UI Toolkit tuner now displays frame, sparks, power, distance, heat, battery watts, and HitAUP XYZ.
- Editor gizmo now draws red beam, cyan origin, green hit sphere, and yellow normal vector.
- Updated sidecar construction report, shared construction appendix, status, rationale, binary ledger, and self-audit XML.

Cinematic Cheats used:
- Cutter damage still exports compact deformation/glow/VFX DTOs. GPU shader/decal/indirect paths own visible denting, scorch, molten glow, and sparks.
- Door sparks are a signal-level visual fake, not physical debris simulation or prefab objects.

Exact microseconds saved:
- Measured profiler proof: PENDING. CPU guard stayed at 100%, so no build/profiler run was launched.
- Request ABI/meta split: estimated 2-8 us under dense request pressure by preserving one predictable 64-byte request line and removing semantic padding reads.
- Hot no-acquire/no-GlobalRegistry route: estimated 5-40 us worst-case hitch avoidance per cutter scheduling frame on i3/MX350-class hardware; exact proof pending.
- Door decoupling: no direct measured frame gain; removes cross-domain metadata path and keeps door VFX on one signal route.

Verification:
- Focused scan after polish: sync raycast 0, `Instantiate` 0, `ParticleSystem` 0, mesh mutation 0, `RaycastCommand.ScheduleBatch` 1, `NoAlias` 17.
- `git diff --check` returned only repository LF/CRLF normalization warnings.
- Compile/build: BLOCKED by CPU guard at 2026-05-20T11:33:10Z; latest `Win32_Processor.LoadPercentage=68`, no `dotnet`/`csc`/`VBCSCompiler` process. A brief 46% sample occurred, but CPU returned above threshold before a valid project build command was selected.

## 2026-05-20T11:41:47Z - STRICT SCALABILITY/TUNING POLISH

What was still wrong:
- Task 11 demanded a 0-to-500 GPU spark continuum; the previous constant range was 8-to-128.
- The editor tuning DTO controlled UI fields, but `EvaluateCutterRaycastHitsJob` still used hardcoded dent radius, glow lifetime, and battery watt values.
- Telemetry had battery watts but no deterministic Burst-work proxy in the 128-byte black-box row.

What was done:
- Changed `LowSparkCount=0` and `UltraSparkCount=500`.
- Routed tuning fields into `EvaluateCutterRaycastHitsJob`: dent radius min/max, glow lifetime, battery watts at power one, spark intensity scale, low spark count, and ultra spark count.
- Replaced linear visual density with `math.smoothstep(GlobalQualityWeight)` for spark/decal/deformation presentation scaling.
- Replaced the telemetry tail reserve at byte 124 with `BurstWorkEstimateMicros`; UI Toolkit tuner displays it as `Burst us`.
- Hardened cold Vault reacquire so stale or undersized generation handles are released before replacement acquisition.
- Split GPU spark signal publication so post-evaluation signals forward job-computed `SparkCount` directly and do not recalculate quantity or restage the VFX row.
- Updated direct live spark staging to read tuning `LowSparkCount`, `UltraSparkCount`, and `SparkIntensityScale` through no-acquire Vault resolve.

Cinematic Cheats used:
- Sparks remain pure signal/GPU requests; no prefab spawn, `ParticleSystem`, or CPU debris simulation.
- Wall damage remains shader/decal scalar payload; no runtime mesh mutation.

Exact Microseconds saved:
- Still PENDING PROFILER. Static estimate unchanged for CPU mesh/prefab/raycast removal; visual work now scales down to zero spark requests at quality 0 and up to 500 GPU-only requests at quality 1.
- Redundant post-evaluation VFX row restage removed; exact gain PENDING PROFILER.

Verification:
- XML/JSON parse passed for `SHINOBU_225_SELF_AUDIT.xml`, sidecar construction JSON, and shared construction JSON.
- Focused scan after Loop 7: sync raycast 0, `Instantiate` 0, `ParticleSystem` 0, mesh mutation 0, `RaycastCommand.ScheduleBatch` 1, `NoAlias` 17, `BurstWorkEstimateMicros` 3, `math.smoothstep` 2.
- `git diff --check` returned only repository LF/CRLF normalization warnings.
- Compile/build: BLOCKED by CPU guard at 2026-05-20T11:45:25Z; `Win32_Processor.LoadPercentage=100`, no `dotnet`/`csc`/`VBCSCompiler` process. No build/rebuild launched.

## 2026-05-20T11:52:19Z - GUARDED COMPILE ATTEMPT

What was wrong:
- Build proof was still absent. CPU gate opened at 46% and no `dotnet`, `csc`, or `VBCSCompiler` process was active.

What was done:
- Ran only `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`.
- Build failed with 77 errors before SHINOBU_225 files were implicated. Primary external blockers: missing `Hecton8.Equipment`, missing `Hecton8.Logistics.Grid`, missing `SoundEmissionSignal`, missing `H8BinaryWorldPager`, missing `SocketDefinitionDTO`, missing `IDockingAutopilotService`, and unrelated bridge/interface gaps in Core/Power/Construction/World/Audio.
- Compiler output did not name `LaserCutterDod*`, `LaserCutterPhysicsTunerWindow`, `Cutter_Raycast_Inquisition`, `LaserCutter.cs`, `SealedDoor.cs`, or `SargassumCutResponder.cs` as error locations.
- Post-attempt `dotnet` compiler host processes remained active, so no second build attempt was legal.

Cinematic Cheats used:
- No change in this pass.

Exact Microseconds saved:
- No runtime measurement. Compile wall is external to SHINOBU_225.

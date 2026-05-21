# SHINOBU_229 Auxiliary Equipment Router Log

## 2026-05-20

What was wrong:
- `DeployableFlare` owned Light, ParticleSystem, Rigidbody, dispatcher ticking, spatial registration, and retinal light publishing.
- `GravTrap` owned ITickable/ISlowTickable loops, Collider[] broadphase, Light/ParticleSystem state, and `PhysicsForceRouter` pulls.
- `GravityTetherTool` owned a 32-collider broadphase and per-hit PhysX velocity changes.
- Auxiliary deployment state was not centralized and could not be blind-snapshotted as a single unmanaged routing surface.

What was done:
- Added `Assets/_Project/Scripts/Equipment/Auxiliary/` router contracts, jobs, runtime, CSV parser, debug gizmo, and editor tools.
- Added `DeployedAuxiliaryDTO[1024]`, `AuxiliaryStateDTO[1024]`, auxiliary-only `ActiveEquipmentDTO[1024]`, route counters, VFX matrices, profile scratch, and 300-frame telemetry buffers to the Vault ID map.
- Added `GenerateMockAuxiliaryDeploymentsJob`, `UpdateDeployedAuxiliaryJob`, `StageAuxiliaryVFXJob`, and a post-fence telemetry recorder. Later pass renamed telemetry to `RecordAuxiliaryTelemetryPass` because it is direct wall-time recording, not a scheduled Burst job.
- Added `AuxiliaryFlareLightSignal`, `AuxiliarySonarRequestSignal`, and `AuxiliaryTetherConnectionSignal` lanes through `SignalBus<T>`/`NativeQueue<T>.ParallelWriter`.
- Converted `DeployableFlare`, `GravTrap`, and `GravityTetherTool` into compatibility facades that only route deploy/cancel requests.
- Added cold bootstrap creation of `AuxiliaryEquipmentRouterRuntime` through `GameBootstrapper`; no first-use GameObject allocation occurs in tool activation.
- Added active-bound guards so `UninitializedMemory` capacity above `ShinobuAuxiliaryActiveCount` is never read as live deployment state.
- Wrote architecture note: `Docs/ARCHITECTURE/AUXILIARY_EQUIPMENT_ROUTER_SHINOBU_229.md`.
- Wrote self-audit: `Docs/Reports/SHINOBU_229_SELF_AUDIT.xml`.
- Appended scanner report to `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json`.

Cinematic cheats used:
- Flare brightness is a deterministic scalar/noise route, not a Unity Light.
- Sensor ping is expanding radius math, not a SphereCollider pulse.
- Gravity tether is an AUP constraint packet, not a SpringJoint or local PhysX force loop.
- VFX are staged matrices after AUP subtraction, not per-object ParticleSystems.

Exact microseconds saved:
- Legacy facade purge: estimated 400-1600 us/frame at 50 active auxiliaries.
- 500-record Burst route versus component ownership: estimated 2500-7000 us/frame.
- Idle uninitialized-bound guard: estimated 40-120 us/frame by avoiding 64 KB garbage deployment reads.
- ARM64 aligned DTO layout: estimated 50-300 us/frame under 500-record stress by avoiding mixed-layout copies.
- Removed GravityTetherTool broadphase/force loop: estimated 300-1200 us/frame while primary is held.
- Removed collider pulse sensor model: estimated 200-900 us per ping wave.
- Removed Unity Light mutation/shadow ownership from flare path: estimated 15-80 us/frame per active flare depending shadow path.
- Boot-only router GameObject/AddComponent allocation: 0 hot-path us; correctness fix avoids failed route and first-use allocation.

Verification:
- Static scan of `DeployableFlare.cs`, `GravTrap.cs`, and `GravityTetherTool.cs` found 0 hits for ITickable/IUpdatable/ISlowTickable/Update/Light/ParticleSystem/Rigidbody/OverlapSphere/PhysicsForceRouter/new GameObject/AddComponent/SpringJoint/SphereCollider/UnityEvent.
- XML self-audit parsed successfully.
- Shared equipment optimization report JSON parsed successfully.
- Compile was not launched. CPU guard samples were 100%, 93.4%, 100%, then 82.5%; no `csc.exe` or `dotnet.exe` process was present, but project protocol forbids build while CPU exceeds 50%.

Blocked:
- `Assets/_Project/Scripts/TetherManager.cs:710` still has a cold `new GameObject("TetherInstance")` pool path. That file is tether/cable physics domain, not auxiliary router. Scanner records it as `PARTIAL_BLOCKED_BY_TETHER_MANAGER_OWNER`.

<SELF_AUDIT agent="SHINOBU_229" status="PENDING_COMPILE_VERIFICATION">
  <layout>
    <DeployedAuxiliaryDTO size="64" aupOffset="0" prefabHashOffset="24" lifetimeOffset="28" paddingBytes="32" />
    <AuxiliaryStateDTO size="16" />
    <ActiveEquipmentDTOMirror size="32" buffer="ShinobuAuxiliaryActiveEquipmentState" />
    <AuxiliaryFlareLightSignal size="64" aupOffset="0" />
    <AuxiliarySonarRequestSignal size="64" aupOffset="0" />
    <AuxiliaryTetherConnectionSignal size="64" projectileAupOffset="0" anchorAupOffset="24" />
  </layout>
  <hotPathGC targetBytes="0" evidence="No GameObject, Light, ParticleSystem, Rigidbody, Collider broadphase, UnityEvent, or managed per-object tick remains in the auxiliary facades." />
  <routing evidence="UpdateDeployedAuxiliaryJob uses SignalBus NativeQueue ParallelWriter lanes for flare, sonar, and tether payloads." />
  <aup evidence="Signals carry double3 AUP; VFX staging downcasts only after camera-AUP subtraction." />
  <scalability evidence="GlobalQualityWeight continuously maps cadence from 15Hz to 60Hz." />
</SELF_AUDIT>

## 2026-05-20 - Radar Pulse Purge Pass

What was wrong:
- `ScannerTool` still held OOP radar pulse state: `PulseActive`, `PulseOriginAup`, `PulseStartTime`, pulse shader/mesh fields, and a nested `ScannerPulseDrawer`.
- `ScannerPulseDrawer` was a MonoBehaviour/ITickable/IUpdatable with a runtime Material, `Matrix4x4[]`, and `Graphics.DrawMeshInstanced` submission outside the auxiliary NativeArray lifecycle.
- Local `dotnet build` project files were stale: ignored generated `Hecton8.Core.csproj` had not imported the new auxiliary router folder, so facades could not resolve `Hecton8.Equipment.Auxiliary`.

What was done:
- Deleted `ScannerPulseDrawer` completely.
- Removed scanner pulse state/properties/shader fields and the cold `AddComponent<ScannerPulseDrawer>` path.
- Routed primary scanner pulse through `AuxiliaryEquipmentRouterRuntime.TryDeploySensorPing(scanPosition, pulseDuration, effectiveScanRadius)`.
- Changed sensor ping scalar semantics to store authored max radius in `AuxiliaryStateDTO.Scalar0`; `UpdateDeployedAuxiliaryJob` now lerps expansion rate from cheap lifetime-rate toward authored/global rate using `GlobalQualityWeight`.
- Added stable Unity `.meta` files for the new `Equipment/Auxiliary` folders and scripts; generated `.csproj` files were not edited as source.
- Updated architecture doc, status, rationale, optimization report, and self-audit XML.

Cinematic Cheats used:
- Radar pulse is now an expanding radius signal; scanner no longer draws its own ring mesh or mutates a material.
- Sonar/VFX owners can render the optical lie from `AuxiliarySonarRequestSignal` rather than asking gameplay scanner code to own presentation.

Exact Microseconds saved:
- Deleted scanner pulse drawer tick/render path: estimated 80-250 us during active pulse frames.
- Removed local scanner pulse state update: estimated 20-80 us/frame during pulse windows.
- Avoided scanner material allocation path: one cold allocation removed per scanner drawer creation.
- Prevented stale project import failure after Unity refresh by adding metas: no runtime gain; saves repeated integration churn.

Verification:
- Static `rg` scan found 0 hits in `ScannerTool.cs` for `ScannerPulseDrawer`, `PulseActive`, `PulseOrigin`, `PulseStartTime`, `ScannerPulseShader`, and scanner-local `Graphics.DrawMeshInstanced`.
- Static auxiliary scan found no runtime `new GameObject`, `AddComponent<Light`, `SpringJoint`, `SphereCollider`, `ParticleSystem`, or `OverlapSphere` hits outside editor scanner literal strings.
- `git diff --check` passed for touched scanner/router files with CRLF warnings only.
- CPU guard was clear at 6.2% and no `dotnet/csc/MSBuild` process was running. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was attempted once and failed with 77 errors. SHINOBU-relevant first errors were stale generated project visibility for `Hecton8.Equipment.Auxiliary`; the remaining errors are unrelated sibling-agent missing types including `Hecton8.Logistics.Grid`, docking/autopilot, audio signal, and world health bridge symbols. `dotnet build-server shutdown` was run afterward; no dotnet/csc/MSBuild process remains.

Blocked:
- Clean compile proof still requires Unity project regeneration/import plus sibling-agent dependency fixes. No second build loop launched.
- `Assets/_Project/Scripts/TetherManager.cs:710` still has the cold `new GameObject("TetherInstance")` pool path owned by tether/cable physics.

## 2026-05-20 - Authority Purity Pass

What was wrong:
- `TryReadTelemetry`, `TryReadDeployments`, `TryReadTuning`, and `TryWriteTuning` used `TryResolveViews`, which can acquire/grow Vault buffers. Read-looking APIs were not pure enough for the authority checklist.
- `Tick`, deploy, cancel, and telemetry finalization also shared the same acquisition resolver. That allowed cold buffer acquisition to leak into runtime work if boot was incomplete.
- `EnsureSignalLanes` had a routine `GlobalDataVault.TryGetLatestCreated` fallback in the domain runtime.
- `ResolveQualityWeight` treated `0.0` as "no override", which made explicit minimum-survival quality impossible.
- New SignalBus/Vault routes had an architecture note but no formal route card.

What was done:
- Added `TryResolveExistingViews` and `TryResolveExisting<T>`; read/tick/deploy/cancel/finalize paths now fail closed unless boot already acquired handles.
- Kept `TryResolveViews` as the cold acquisition path for initialization and mock generation.
- Added `InitializeService(IDataVault)` and called it from `GameBootstrapper` for new and already-active router instances.
- Removed auxiliary runtime use of `GlobalDataVault.TryGetLatestCreated`.
- Added `AuxiliaryTuningFlags.OverrideGlobalQualityWeight`; default tuning follows live global weight, editor override can now force `0.0`.
- Added `Docs/ARCHITECTURE/SHINOBU_229_AUXILIARY_EQUIPMENT_ROUTE_CARD.md` with `YELLOW` disposition pending Unity import/profiler proof.

Cinematic Cheats used:
- No new simulation. This pass removed authority leaks around the existing Dear-Lie routes: flare scalar light signal, sonar radius signal, and tether AUP constraint packet.

Exact Microseconds saved:
- Hidden handle acquisition removed from read/hot paths: estimated 5-40 us/frame under normal conditions, with larger spike avoidance if a stale handle would have reacquired during gameplay.
- Quality override fix is correctness, not direct frame-time saving; it restores 0.0 survival-tier load shedding.

Verification:
- Static scan found no `TryGetLatestCreated` in auxiliary runtime targets.
- Static scan found no scanner pulse state or runtime Unity light/physics/particle OOP patterns in SHINOBU_229 target runtime files; only editor scanner literal strings remain.
- `git diff --check` passed for touched source/docs with line-ending warnings only.
- No `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler` process remains. No rebuild was launched in this pass.

Blocked:
- Route card remains `YELLOW`, not `GREEN`, until Unity import, Console, Play Mode, profiler/GC, and signal stress artifacts exist.

## 2026-05-20 - Safety Metadata And Boundary Pass

What was wrong:
- `GenerateMockAuxiliaryDeploymentsJob.ActiveCount` was annotated `[ReadOnly]` even though index 0 writes the initialized deployment bound.
- `_activeEquipmentHandle` was reset to default during shutdown instead of releasing the Vault generation handle.
- Broad `radar|sonar|ping|sensor|scanner` search returned many downstream consumers in Visor/SpectrumSystem, Audio, AI sensory, and cockpit UI. Those are not auxiliary deployment lifecycle owners.

What was done:
- Removed `[ReadOnly]` from the mock job `ActiveCount` field.
- Routed `_activeEquipmentHandle` through the same `ReleaseHandle` path as the rest of the auxiliary Vault handles.
- Re-extracted the SHINOBU_229 XML prompt with an attribute-tolerant CLI regex and confirmed 20 task lines.
- Recorded the radar boundary: scanner active ping lifetime is owned by the auxiliary router; downstream sonar/radar render, audio, and AI consumers remain outside this agent's write boundary.

Cinematic Cheats used:
- No new visual fake was added. This pass preserves the existing fake routes: flare scalar light signal, sonar radius signal, and gravity tether AUP packet.

Exact Microseconds saved:
- Per-frame saving: 0 us direct. The fix prevents job safety rejection in mock stress and prevents Vault refcount leakage across scene reloads.
- Avoided compile-wall risk from cross-domain radar edits: integration-time saving only, not runtime frame time.

Verification:
- Static scan shows writable `GenerateMockAuxiliaryDeploymentsJob.ActiveCount` and read-only `ActiveCount` only on consumer jobs.
- Static scan shows `_activeEquipmentHandle` released through `ReleaseHandle`.
- Forbidden auxiliary runtime scan found no `TryGetLatestCreated`, `ScannerPulseDrawer`, scanner pulse state, Unity Light/Joint/ParticleSystem/SphereCollider/OverlapSphere, or scanner-local `Graphics.DrawMeshInstanced` outside editor scanner literal strings.
- No rebuild was launched in this pass.

Blocked:
- Runtime proof remains blocked by Unity import/project regeneration and unrelated sibling dependency failures from the prior guarded build attempt.

## 2026-05-20 - Read-Only Deployment Snapshot Seal

What was wrong:
- `TryReadDeployments` returned a mutable `NativeArray<DeployedAuxiliaryDTO>` alias to Vault truth. It did not allocate, but it exposed write authority through a read-looking API.

What was done:
- Changed `TryReadDeployments` to return `NativeArray<DeployedAuxiliaryDTO>.ReadOnly` through `AsReadOnly()`.
- Kept the X-Ray histogram and deployment gizmo on read-only indexing; no diagnostic copy buffer was added.

Cinematic Cheats used:
- None added. This is authority sealing around the existing Dear-Lie signal routes.

Exact Microseconds saved:
- 0 us direct. The value is corruption prevention: external diagnostics can no longer mutate deployment state outside router locks.

Verification:
- Static usage scan shows only editor/gizmo consumers of `TryReadDeployments`.
- Read accessor now exposes a read-only NativeArray alias.
- No rebuild was launched in this pass.

## 2026-05-20 - Producer-Side Signal NaN Vaccination

What was wrong:
- Auxiliary jobs publish through `SignalBus<T>.ParallelWriter`, so they do not pass through managed `SignalBus.TryPush` finite guards.
- Tuning fields could carry NaN from external/editor writes, and gravity tether AUP anchors were not explicitly finite-checked before storage.

What was done:
- Added `SanitizeNonNegative` and `SanitizePositive` helpers.
- Sanitized flare intensity/range/signal scale, ping max radius/rate, VFX scale, base lifetimes, and default scalar resolution before enqueue or matrix write.
- Rejected non-finite gravity tether projectile/anchor AUP on deploy and fault-dropped non-finite tether anchors inside the Burst route.

Cinematic Cheats used:
- No new fake. This protects the existing fake packets from becoming invalid: scalar light, expanding sonar radius, and AUP tether constraint.

Exact Microseconds saved:
- 0 us direct; adds constant ALU. The saving is failure avoidance: prevents NaN propagation into downstream physics/sonar/light lanes.

Verification:
- Static source inspection confirms producer-side finite guards before route enqueue.
- No rebuild was launched in this pass.

## 2026-05-20 - ActiveCount Vault Lock Fence Closure

What was wrong:
- Subagent static audit found `ShinobuAuxiliaryActiveCount` was used by scheduled jobs but not locked with the rest of the runtime buffers.

What was done:
- Added `ShinobuAuxiliaryActiveCount` to `TryLockRuntimeBuffers` and `UnlockRuntimeBuffers`.
- Left confirmed-compatible SignalBus, registry, dispatcher, and handle-release paths unchanged.

Cinematic Cheats used:
- None added. This is memory ownership hardening around the existing scalar light, sonar radius, and AUP tether fake routes.

Exact Microseconds saved:
- 0 us direct. The fix prevents Vault relocation/stale-handle corruption of the initialized-bound buffer during job execution.

Verification:
- Static source inspection confirms `ActiveCount` is locked and unlocked with deployments, states, route counters, VFX matrices, and active equipment.
- No rebuild was launched in this pass.

## 2026-05-20 - Subagent Defect Burn-Down

What was wrong:
- Gravity tethers shared one runtime anchor, so concurrent tethers routed to the latest anchor.
- Flare/trap facades could leave stale local state or routed records after expiry/disable.
- `GravTrap` deployed projectile and anchor at the same position, yielding zero rest length.
- CSV parser had no caller; profiles did not affect tuning.
- VFX staging wrote CPU matrices only; no GPU buffer handoff existed.
- Telemetry was named as a job and claimed timing it did not measure.

What was done:
- Added `AuxiliaryTetherAnchorDTO[1024]` and `ShinobuAuxiliaryTetherAnchors`.
- Routed per-slot tether anchors through mock/deploy/update/cancel paths and lock/release fences.
- Added pure router readback for nearest remaining lifetime; facades cancel on disable.
- `GravTrap` now routes a shell sample at `pullRadius` to the center anchor.
- Cold boot loads `auxiliary_equipment_profiles.csv` into Vault scratch, parses profiles, applies tuning, and falls back to deterministic profiles if missing.
- Added persistent VFX `GraphicsBuffer` upload after the pending job fence.
- Renamed telemetry recorder to `RecordAuxiliaryTelemetryPass`; `CpuMicroseconds` is documented as schedule-to-finalize wall time pending profiler proof.

Cinematic Cheats used:
- Gravity trap remains a mathematical tether packet, not a Unity force field.
- VFX remains matrix-buffer procedural staging, not ParticleSystem hierarchy.

Exact Microseconds saved:
- Per-deployment anchor fix: 0 us direct; prevents corrupted downstream solver work.
- Facade cancel/readback: 0 us direct; prevents stale routed records.
- CSV integration: hot path 0 us; removes designer recompile loop.
- VFX GPU handoff: avoids GameObject/ParticleSystem hierarchy cost; exact profiler proof pending.

Verification:
- Static scan found no `_lastTetherAnchorAup`, no `RecordAuxiliaryTelemetryJob`, and no scanner pulse runtime state.
- Static scan found exactly three Burst `IJobParallelFor` batch jobs in auxiliary jobs.
- `git diff --check` passed for touched source with line-ending warnings only.
- No rebuild was launched in this pass.

## 2026-05-20 - Lock And Producer Route Polish

What was wrong:
- Producer jobs still used the legacy SignalBus writer property.
- Runtime paths resolved Vault views before locking buffers, so a relocation-safe proof was incomplete.
- Deployment diagnostics could read the deployment array while the lifecycle job was active.
- Tuning writes resolved all views for one scalar DTO.

What was done:
- Switched producers to `SignalBus<T>.OpenParallelWriter()`.
- Locked runtime buffers before re-resolving job-visible views in Tick, deploy, cancel, and mock paths.
- Made `TryReadDeployments` fail closed while `_jobActive`.
- Locked only `ShinobuAuxiliaryTuning` for editor tuning writes.
- Added finite guards for authored lifetime, radius, accumulated cadence debt, and tether rest length.

Cinematic Cheats used:
- Existing fakes preserved: scalar light signal, expanding sonar radius signal, and AUP tether packet.

Exact Microseconds saved:
- 0 us direct. This pass removes stale alias/race risk rather than measurable frame work.

Verification:
- Static inspection confirmed `OpenParallelWriter()`, lock-before-resolve paths, and tuning-only lock scope.
- No rebuild was launched in this pass.

## 2026-05-20 - Subagent Route Closure

What was wrong:
- Route counters represented enqueue attempts but were documented too strongly.
- SignalBus overflow/backpressure fields were not in the auxiliary telemetry ring.
- `ScannerTool` still published scanner projection presentation state after routing the same ping through the auxiliary router.
- Data Monolith readiness could be misread from the CSV bridge.

What was done:
- Added `DroppedSignals`, `CorruptedSignals`, and `PeakQueuedSignals` to `AuxiliaryTelemetryEntry`.
- Fed telemetry from SignalBus last-flush lane pressure.
- Removed `ScannerTool` direct `HectonScannerProjectionState.Publish`.
- Deleted the now-unused `HectonScannerProjectionState.cs` and `.meta` static shadow-state route.
- Made `HectonScannerProjectionFeature` consume `SignalBus<AuxiliarySonarRequestSignal>.GetSignals()`.
- Moved scanner projection coordinate conversion to AUP-local: double subtract `HectonFloatingOrigin.CurrentTotalOffsetDouble` before float upload, shader uses `worldPos - localOrigin`.
- Documented CSV as cold tuning bridge/static fallback only; `static_data.h8bin` is absent.

Cinematic Cheats used:
- Scanner pulse remains a data signal consumed by presentation; no collider or scanner-owned pulse renderer was restored.

Exact Microseconds saved:
- 0 us direct. The value is authority correctness and blackbox fidelity.

Verification:
- Static source scan shows no `HectonScannerProjectionState.Publish` caller in `ScannerTool`.
- Static source scan shows the projection feature consumes `AuxiliarySonarRequestSignal` snapshots.
- Atomic deletion check: both `HectonScannerProjectionState.cs` and `HectonScannerProjectionState.cs.meta` are removed from disk.
- Generated project staleness check: `Hecton8.Core.csproj` still references deleted `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs`; no build was launched against this stale project file.
- No rebuild was launched in this pass.

## 2026-05-21 - Subagent DOD Hardening

What was wrong:
- Auxiliary runtime/jobs imported sibling `Hecton8.Tools` for `ActiveEquipmentDTO`.
- Mock generation could be called from `Tick` and force-completed the scheduled mock job.
- `GravTrap.IsActive` and scanner quality `Resolve*` methods hid state mutation behind read-looking names.
- X-Ray UI labeled schedule-to-finalize wall time as CPU microseconds.

What was done:
- Added local `AuxiliaryActiveEquipmentDTO` and switched auxiliary Vault mirror/job fields to it.
- Removed `Hecton8.Tools` and `Hecton8.World` imports from auxiliary runtime/jobs.
- Changed mock generation to schedule `GenerateMockAuxiliaryDeploymentsJob` plus dependent `StageAuxiliaryVFXJob`; finalization stays on the existing LateFrame fence.
- Left `DispatcherJobFence.TryComplete` only for teardown.
- Made `GravTrap.IsActive` pure and renamed mutating scanner quality methods to `Refresh*`.
- Changed X-Ray label to `Wallus`.
- Documented scanner scientific/lore managed-string and `ScannerToolActiveSignal` bridge residuals as outside SHINOBU_229 auxiliary pulse-lifecycle ownership.

Cinematic Cheats used:
- No new visual fake. Existing fakes remain: scalar flare light packets, expanding sonar radius packets, and per-slot AUP tether packets.

Exact Microseconds saved:
- Mock path: prevents a same-frame completion wall under seed/mock stress; exact stall avoided depends on 500-record mock scheduling.
- Compile wall: prevents future sibling Tools/World dependency churn in auxiliary domain; runtime gain 0 us.
- Read purity/label fixes: evidence hygiene, 0 us direct.

Verification:
- Static scan shows no `Hecton8.Tools`, `Hecton8.World`, `ActiveEquipmentDTO`, or `ActiveEquipmentStateFlags` in `Assets/_Project/Scripts/Equipment/Auxiliary`.
- Static scan shows no `TryComplete` in auxiliary runtime except teardown.
- Static scan shows no `ResolveFocusedScanResampleInterval`, `ResolveScannerQualityTier`, `ResolveActiveState`, or `CPUus` residues in touched files.
- No rebuild was launched in this pass.

## 2026-05-21 - Final Static Route Gate

What was wrong:
- The previous hardening pass needed a fresh objective gate after local DTO isolation, async mock scheduling, and scanner read-purity renames.
- Generated project metadata is stale: `Hecton8.Core.csproj` still references deleted `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs`.

What was done:
- Re-extracted the exact `SHINOBU_229` XML block from `Docs/Tasks/CURRENT_BATCH.md`.
- Reran forbidden runtime scans for scanner pulse shadow state, Unity Light/Joint/ParticleSystem/OverlapSphere, `UnityEngine.Random`, and `Time.deltaTime`.
- Reran auxiliary sibling import scans for `Hecton8.Tools`, `Hecton8.World`, `ActiveEquipmentDTO`, and `ActiveEquipmentStateFlags`.
- Reran job completion scan; `DispatcherJobFence.TryComplete` exists only in teardown.
- Parsed `Docs/Reports/SHINOBU_229_SELF_AUDIT.xml` and `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json`.
- Checked `.cs.meta`/`.shader.meta` orphan status under `Assets/_Project`.

Cinematic Cheats used:
- No new fake added in this gate. Existing route fakes remain: scalar flare light packets, expanding sonar radius packets, per-slot AUP tether packets, and post-fence VFX matrix buffer.

Exact Microseconds saved:
- 0 us runtime. This gate prevents a predictable stale-build loop and preserves source-level confidence until Unity regenerates project files.

Verification:
- Runtime forbidden-pattern scan found only editor-only scanner-auditor literal strings.
- XML and JSON reports parse successfully.
- No orphan `.cs.meta` or `.shader.meta` files were found under `Assets/_Project`.
- `dotnet`, `csc`, and `MSBuild` were not running during the process check.
- No rebuild was launched against the known stale generated `.csproj`.

## 2026-05-21 - VFX Upload Bandwidth Gate

What was wrong:
- The post-fence VFX handoff used one persistent `GraphicsBuffer` and uploaded the staged matrix payload every frame, even when active auxiliaries, camera AUP, and quality were unchanged.
- The first dirty-gate draft reset `_lastVfxUploadCount` before comparison, which made the gate miss every time.

What was done:
- Replaced the single VFX upload buffer with A/B structured `GraphicsBuffer` pages plus `_vfxGpuReadBuffer`.
- Added dirty-gate inputs: active count, deployment snapshot hash, camera AUP, and quality weight.
- Fixed the self-invalidation bug by preserving the previous upload count before comparing the snapshot.
- Updated route card, architecture note, binary payload ledger, XML audit, JSON report, status, and rationale.

Cinematic Cheats used:
- No CPU particle/light simulation was added. The router still stages one compact matrix DTO stream and lets downstream presentation spend GPU-side visual work.

Exact Microseconds saved:
- Static frames now skip one `64B * activeCount` CPU-to-GPU upload. Exact microseconds are pending Unity profiler/Frame Debugger proof after Unity regenerates the stale generated project files.

Verification:
- Static source scan shows only `_vfxGpuBufferA`, `_vfxGpuBufferB`, and `_vfxGpuReadBuffer`; no old single `_vfxGpuBuffer` owner remains.
- Dirty-gate comparison now uses the previous upload count instead of a reset value.
- No rebuild was launched against the known stale generated `.csproj`.

## 2026-05-21 - Signal Capacity And Scanner Audio Gate

What was wrong:
- Auxiliary SignalBus lanes were prewarmed below the producer ceiling: 256 flare, 256 sonar, and 128 tether versus 1024 possible active deployments.
- Active scanner pulse audio still had a direct audio-service invocation path.
- The last subagent gate needed objective source/report validation after the patch, not prose.

What was done:
- Changed auxiliary flare, sonar, and tether lane configuration to use `AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries` for both expected queue capacity and max-frame signals.
- Replaced scanner active pulse `IAudioService.PlayAtPoint` routing with `SignalBus<AcousticPingSignal>.TryPush`, carrying AUP, radius, intensity, source hash, active-sonar channel, and active-sonar flag.
- Kept `ScanEvents.RaiseScanTriggered` documented as scanner-log/progression legacy routing, outside SHINOBU_229 auxiliary light/physics/VFX ownership.
- Reran source scans, XML/JSON parse, orphan meta scan, process check, and generated project staleness check.

Cinematic Cheats used:
- Scanner pulse remains a signal packet. No AudioSource, Light, ParticleSystem, collider pulse, or scanner-owned renderer was restored.

Exact Microseconds saved:
- Prevents hot NativeQueue growth under 1024 same-frame auxiliary routes; exact stall avoided requires Unity profiler proof.
- Removes one direct scanner-to-audio service invocation from pulse activation; exact us pending profiler.
- VFX dirty gate from the prior pass still skips one `64B * activeCount` matrix upload on unchanged frames.

Verification:
- `ScannerTool.cs` scan found no `_cachedAudioService`, `IAudioService`, `GlobalRegistry.Audio`, or `PlayAtPoint(pingClip)` residue.
- Scanner pulse now calls `PublishScannerAcousticPing`; that helper publishes `SignalBus<AcousticPingSignal>.TryPush`.
- `EnsureSignalLanes` configures all three auxiliary SignalBus lanes from `maxAuxiliarySignalsPerFrame = AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries`.
- XML and JSON reports parse after the capacity/audio updates.
- No orphan `.cs.meta` or `.shader.meta` files were found under `Assets/_Project`.
- `Hecton8.Core.csproj` still references deleted `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs`; no rebuild was launched against stale generated metadata.

## 2026-05-21 - Facade Shadow-State And Audio Asset Gate Purge

What was wrong:
- `ScannerTool` still serialized `AudioClip pingClip` and `cooldownClip`, and active acoustic SignalBus publication depended on `pingClip != null`.
- `DeployableFlare` mirrored router lifetime in `_fuelTimer`.
- `GravTrap` mirrored router active state in `_activationIssued`.

What was done:
- Removed scanner audio clip fields from `ScannerTool`.
- Active scanner pulse now publishes `AcousticPingSignal` from scan route data without Unity-object asset gating.
- Removed `_fuelTimer`; `DeployableFlare.RemainingFuel` reads central router lifetime only.
- Removed `_activationIssued`; `GravTrap.IsActive` reads central router state only.

Cinematic Cheats used:
- Scanner acoustic feedback remains a scalar unmanaged signal. No AudioSource, clip playback, collider pulse, or scanner-owned renderer was restored.

Exact Microseconds saved:
- Removes one Unity-object branch from scanner pulse activation and two facade-local lifecycle mirrors. Exact time is below static-proof resolution and remains profiler-pending.

Verification:
- Targeted scan found no `AudioClip`, `pingClip`, `cooldownClip`, `IAudioService`, `PlayAtPoint`, `GlobalRegistry.Audio`, `_fuelTimer`, or `_activationIssued` in the touched SHINOBU-owned facade/audio route files.
- No rebuild was launched against stale generated project metadata.

## 2026-05-21 - Generated Project Shield

What was wrong:
- Generated `Hecton8.Core.csproj` still lists deleted `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs`.
- Generated project metadata can omit the new SHINOBU_229 auxiliary runtime/editor sources until Unity regenerates project files.

What was done:
- Added `Directory.Build.targets` compile-item pruning for the deleted scanner projection state file under `Hecton8.Core`.
- Added conditional compile includes for SHINOBU_229 runtime auxiliary source files under `Hecton8.Core`.
- Added conditional compile include for `AuxiliaryEquipmentEditorTools.cs` under `Hecton8.Editor`.
- Did not edit generated `.csproj` files and did not launch rebuild.

Cinematic Cheats used:
- None. This is compile-wall hygiene only.

Exact Microseconds saved:
- Runtime 0 us. Engineering cost avoided: one predictable stale-file compile failure before source diagnostics.

Verification:
- `Directory.Build.targets` parses as XML.
- Static scan shows the deleted-file prune and all SHINOBU_229 conditional includes are present.
- `git diff --check -- Directory.Build.targets` reports no whitespace errors, only CRLF normalization warning.
- No `dotnet`, `csc`, or `MSBuild` process was running during guard check.

## 2026-05-21 - Compile-Hazard Audit And Flare Readback Fix

What was wrong:
- The generated-project shield and scanner audio/facade purge needed an independent API/static compile-hazard pass.
- Local review found `DeployableFlare.ResolveState()` could report `Burning` after the router record disappeared.
- CPU load was 100%, so project rules forbade a guarded build attempt.

What was done:
- Integrated subagent static audit: no API/namespace hazards found for `AcousticPingSignal`, `SignalBus<T>.OpenParallelWriter()`, `GraphicsBufferUploadUtility`, auxiliary DTO fields, or scanner projection SignalBus snapshot consumption.
- Fixed `DeployableFlare.ResolveState()` to return `Extinguished` when a previously burning facade no longer has a central router record.
- Kept build blocked under CPU guard; no `dotnet build` or rebuild was launched.

Cinematic Cheats used:
- None. This is correctness and compile-risk burn-down.

Exact Microseconds saved:
- Runtime 0 us. Prevents stale compatibility state without restoring local timers or managed update loops.

Verification:
- Subagent returned zero findings across the assigned compile/API hazard surface.
- CPU guard sampled at 100%; build launch was correctly rejected by protocol.

## 2026-05-21 - Telemetry Vault Fence Closure

What was wrong:
- `ShinobuAuxiliaryTelemetryRing` and `ShinobuAuxiliaryTelemetryCursor` are Vault-backed proof buffers written during auxiliary finalization but were not included in the runtime Vault lock fence.

What was done:
- Added telemetry ring and cursor locks to `TryLockRuntimeBuffers`.
- Added matching unlock calls to `UnlockRuntimeBuffers`.

Cinematic Cheats used:
- None. This is Vault safety for the blackbox proof artifact.

Exact Microseconds saved:
- Runtime 0 us. Prevents relocation/stale-alias corruption around telemetry and fault-dump recording without adding private NativeArrays.

Verification:
- Patch is scoped to `AuxiliaryEquipmentRouterRuntime.cs`; compile/profiler proof remains pending under build guard.
- Source scan confirms telemetry ring/cursor lock and unlock calls.
- `SHINOBU_229_SELF_AUDIT.xml` parses as XML.
- `EQUIPMENT_OPTIMIZATION_REPORT.json` parses as JSON.
- Targeted forbidden-residue scan found no `TryGetLatestCreated`, direct native collection allocation, hot `.Complete()`, `Time.deltaTime`, `UnityEngine.Random`, audio clip gate, direct audio service call, `_fuelTimer`, or `_activationIssued` in SHINOBU-owned touched route files.
- CPU guard sampled at 100%; no guarded rebuild was launched.

## 2026-05-21 - Flare Facade State Purge

What was wrong:
- `DeployableFlare` still retained `_state`, a local compatibility enum mirror, after the lifetime mirror had been removed.

What was done:
- Removed `_state`.
- `State`, `RemainingFuel`, and `IsBurning` now resolve from router/Vault readback only.
- `Deploy`, `ForceExtinguish`, `ResetFlare`, and `OnDisable` now only route intent into `AuxiliaryEquipmentRouterRuntime`.

Cinematic Cheats used:
- None. This is authority cleanup.

Exact Microseconds saved:
- Runtime gain below profiler resolution. Removes the last flare-local lifecycle fact and avoids future stale UI/logic reads.

Verification:
- Pre-removal scan found no external consumers of `FlareState`, `DeployableFlare.State`, `IsBurning`, or `RemainingFuel` outside the facade.
- Post-removal scan found no `_state`, `_fuelTimer`, `_activationIssued`, `AudioClip`, direct audio service call, or clip gate residue in flare/trap/scanner route files.
- XML and JSON audit artifacts parse after the facade state purge.

## 2026-05-21 - Scanner Projection Wall-Clock Purge

What was wrong:
- `HectonScannerProjectionFeature` still derived scanner projection age from `Time.time`, while pulse lifetime is owned by the auxiliary NativeArray and published through `AuxiliarySonarRequestSignal`.

What was done:
- Removed projection `StartTime`, `Duration`, and `_now` fields.
- Projection age is now `CurrentRadius / MaxRadius` from the latest `SignalBus<AuxiliarySonarRequestSignal>` snapshot.
- No gameplay truth, DTO layout, or SignalBus payload layout changed.

Cinematic Cheats used:
- The radar projection remains a Dear Lie fullscreen depth/dither shader effect. The CPU feeds only localized AUP, axes, radius, and signal-derived age; no collider pulse or scanner-local state machine exists.

Exact Microseconds saved:
- Direct steady-state CPU gain is 0 us. The change removes a wall-clock dependency and prevents a second presentation lifetime fact.

Verification:
- `rg` found no `Time.`, `StartTime`, `Duration`, `float now`, or `_now` in `HectonScannerProjectionFeature.cs`.
- Route residue scan found no `ScannerPulseDrawer`, `PulseActive`, `PulseOrigin`, `PulseStartTime`, `HectonScannerProjectionState`, `AudioClip`, `IAudioService`, `PlayAtPoint`, `pingClip`, or `cooldownClip` in owned route files.
- Braces check for `HectonScannerProjectionFeature.cs` reports `32/32`.
- `git diff --check -- Assets/_Project/Scripts/Visor/HectonScannerProjectionFeature.cs` reports no whitespace errors, only CRLF normalization warning.
- CPU guard sampled at 100%; no guarded rebuild was launched.

## 2026-05-21 - Projection Route Documentation Sync

What was wrong:
- route docs did not yet explicitly record the scanner projection age source after the wall-clock purge.

What was done:
- updated the route card, SHINOBU architecture note, and binary payload ledger to state that `HectonScannerProjectionFeature` derives age from `AuxiliarySonarRequestSignal.CurrentRadius / MaxRadius`.
- documented that projection no longer uses `Time.time`, `StartTime`, `Duration`, or a managed projection-state mirror.

Cinematic Cheats used:
- documentation now matches the shader fake: one SignalBus scalar drives projection phase; no CPU pulse drawer or clocked MonoBehaviour route.

Exact Microseconds saved:
- Runtime gain is 0 us. Estimated 1000 us integrator lookup saved by eliminating stale route ambiguity.

Verification:
- JSON and XML audit artifacts parsed cleanly after the report update.
- Markdown route docs were patched only in SHINOBU_229-owned sections/addendum.
- `git diff --check` reports only CRLF normalization warnings for touched SHINOBU_229 files.
- Projection time residue scan and owned route residue scan return clean.
- Process scan listed no `dotnet`, `csc`, or `MSBuild`; CPU guard sampled `100`, so no rebuild was launched.

## 2026-05-21 - Active Pulse Debug Allocation Purge

What was wrong:
- `ScannerTool.LogScanPulse` still built an interpolated `Debug.Log` string in editor/development builds from the active scan pulse path.

What was done:
- Removed the dynamic debug log body; the method remains as a no-op conditional hook to avoid call-site churn.
- Updated JSON/XML self-audit to record zero active-pulse interpolated log hits.

Cinematic Cheats used:
- None. This is profiler hygiene for the sensor ping route.

Exact Microseconds saved:
- Player release: 0 us because the call was already stripped. Editor/development: avoids one dynamic string formatting/allocation per scan pulse.

Verification:
- `rg` found no `$"`, `Debug.Log(`, `string.Format`, LINQ, or `foreach` in SHINOBU_229 owned route files after the patch.
- `Directory.Build.targets` parses as XML.
- Auxiliary runtime import scan found no direct sibling `Hecton8.Tools`, `Hecton8.World`, audio, physics, lighting, sonar, AI, vehicle, construction, logistics, geology, or rendering dependencies.
- Process scan listed no `dotnet`, `csc`, or `MSBuild`; CPU guard sampled `100`, so no rebuild was launched.

## 2026-05-21 - SignalBus Lane Cap Contract Clarification

What was wrong:
- The auxiliary SignalBus lane configuration was correct but review-hostile: positional arguments hid the difference between prewarm capacity, high-tier max-frame flush, and low-tier flush caps.

What was done:
- Replaced positional `SignalBus<Auxiliary*>.Configure(...)` calls with named `expectedCapacity`, `maxFrameSignals`, and `lowTierFrameSignals` arguments.
- Declared explicit low-tier constants: 64 flare, 32 sonar, 16 tether.
- Updated the route card, SHINOBU architecture note, binary payload ledger, JSON report, and XML self-audit to state that low-tier caps shed only optional effect bandwidth, not Vault truth capacity.

Cinematic Cheats used:
- Continuous effect shedding remains the cheat: at low `GlobalQualityWeight`, the system preserves one authoritative NativeArray lifecycle while allowing SignalBus visual/effect flush density to fall continuously instead of simulating/dispatching every possible visible packet.

Exact Microseconds saved:
- No new runtime saving; this patch is contract proof. It preserves the existing low-tier bounded flush budget and avoids a review/integration loop over a misread capacity contract.

Verification:
- `EQUIPMENT_OPTIMIZATION_REPORT.json` parses via `ConvertFrom-Json`.
- `SHINOBU_229_SELF_AUDIT.xml` parses via `[xml]`.
- `Directory.Build.targets` parses as XML.
- Targeted `rg -g` scans found no `$"`, `Debug.Log(`, `string.Format`, LINQ, `foreach`, scanner pulse state, projection wall-clock, audio clip gate, direct audio service, local flare state, or local grav-trap activation residues in SHINOBU_229 owned route files.
- Auxiliary runtime sibling-import scan found no direct `Hecton8.Tools`, `Hecton8.World`, audio, physics, lighting, sonar, AI, vehicle, construction, logistics, geology, or rendering namespace imports.
- `git diff --check` for touched files reports only CRLF normalization warnings.
- CPU guard sampled `100` and process scan listed no `dotnet`, `csc`, or `MSBuild`; no rebuild was launched.

## 2026-05-21 - GPU Upload Discipline Static Proof

What was wrong:
- The VFX handoff report said `GraphicsBuffer` and dirty-gate, but it did not explicitly prove the AGENTS bandwidth rule: `LockBufferForWrite` plus guarded memcpy and no auxiliary `SetData` path.

What was done:
- Ran `Tools/PolishMandateStaticAudit.py` on `Assets/_Project/Scripts/Equipment/Auxiliary` with hard gates for `Pack=1` and missing Burst flags.
- Added SHINOBU-owned audit artifacts:
  - `Docs/AgentLogs/PolishMandateStaticAudit_SHINOBU_229_Auxiliary.json`
  - `Docs/AgentLogs/PolishMandateStaticAudit_SHINOBU_229_Auxiliary.md`
- Updated JSON/XML self-audit, route card, architecture note, and binary payload ledger with the concrete GPU upload path: `CreateStructuredLockBuffer` -> `UploadNativeArray` -> `LockBufferForWrite` -> `UnsafeMemoryCopyGuard.TryMemCpy` -> `UnlockBufferAfterWrite`.

Cinematic Cheats used:
- The router still sends matrices only. Downstream presentation spends the saved CPU; the auxiliary owner does not instantiate particles, lights, joints, or draw calls.

Exact Microseconds saved:
- No new runtime saving in this documentation/static-proof pass. It prevents a future bandwidth regression by making the no-`SetData` contract explicit.

Verification:
- Auxiliary polish audit: `csFiles=6`, `packOne=0`, missing Burst flags `0/0/0`, `jobHandleComplete=0`, `linqSurface=0`, `structAutoProperties=0`, `privateNativeCollectionField=0`, `noAlias=13`, `status=PASS_WITH_WARNINGS`.
- Source scan confirms `AuxiliaryEquipmentRouterRuntime` creates VFX buffers via `GraphicsBufferUploadUtility.CreateStructuredLockBuffer<AuxiliaryVfxMatrixDTO>` and uploads via `GraphicsBufferUploadUtility.UploadNativeArray`.
- Core helper source confirms `UploadNativeArray` calls `LockBufferForWrite`, `UnsafeMemoryCopyGuard.TryMemCpy`, and `UnlockBufferAfterWrite`.
- Auxiliary route source contains no `GraphicsBuffer.SetData` or `GraphicsBufferUploadUtility.UploadArraySetData` call.
- Runtime profiler/Frame Debugger proof remains pending.

## 2026-05-21 - Scanner Status Producer And First 20 Route Backfill

What was wrong:
- Subagent audit found `ScannerToolActiveSignal` was still produced from `ScannerTool.LateFrameTick` through `GlobalSignals.Publish(new ScannerToolActiveSignal)`.
- SHINOBU route docs did not carry the required First 20 Minutes moment / route impact / proof required / parked work rejected block.
- `ScannerTool` still has broad scanner knowledge/UI/lore/fauna/resource namespace coupling; that is real residual debt, but it is not exclusively the auxiliary effect route.

What was done:
- Replaced the scanner-status producer with direct `SignalBus<ScannerToolActiveSignal>.Push(in signal)` from registered `LateFrameTick`.
- Removed duplicate-publish guard fields so persistent scanner-active consumers receive one typed-lane status packet per registered LateFrame without depending on the GlobalSignals latest cache.
- Updated route card, SHINOBU architecture note, binary payload ledger, JSON report, XML self-audit, status, and rationale with the First 20 route-impact block and the new producer route.
- Recorded broad scanner coupling as owner-split debt instead of deleting active scanner dependencies blindly.

Cinematic Cheats used:
- No physics or visual simulation was added. Scanner status is a 32-byte data signal; flare/ping/tether effects remain Dear-Lie packets consumed by downstream presentation/physics owners.

Exact Microseconds saved:
- Expected runtime gain is below profiler resolution: one `GlobalSignals` wrapper/latest-cache sequence write is removed per scanner-status publish.
- Engineering gain is nonzero: removes one live hot bridge violation and one route-contract documentation gap before the next guarded compile/profiler pass.

Verification:
- `rg` found no `GlobalSignals.Publish(new ScannerToolActiveSignal)` under `Assets/_Project/Scripts`.
- `ScannerTool` contains `SignalBus<ScannerToolActiveSignal>.Push(in signal)` at the status producer.
- Targeted `rg` found no `_lastPublishedTuning*` duplicate status fields in `ScannerTool`.
- Route card, architecture note, ledger, JSON, and XML carry First 20 Minutes route-impact fields.
- `EQUIPMENT_OPTIMIZATION_REPORT.json`, `SHINOBU_229_SELF_AUDIT.xml`, and `Directory.Build.targets` parse cleanly.
- Prompt re-extraction from `Docs/Tasks/CURRENT_BATCH.md` still reports `Task` count 20 for `SHINOBU_229`.
- Downstream `GlobalSignals.TryGetLatestScannerToolActiveSignal` fallback readers remain legacy consumer debt for their owners; they were not rewritten in this auxiliary producer pass.
- Runtime compile/profiler proof remains pending; CPU guard sampled `100`, so no rebuild was launched in this log entry.

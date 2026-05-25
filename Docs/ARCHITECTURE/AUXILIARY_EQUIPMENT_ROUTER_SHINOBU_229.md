# Auxiliary Equipment Router - SHINOBU_229



Status: Compile verification pending. `Directory.Build.targets` now shields SHINOBU_229's stale generated-project entries, but guarded compile/profiler proof is still not rerun and unrelated sibling-agent missing types may remain.



Route card: `Docs/ARCHITECTURE/SHINOBU_229_AUXILIARY_EQUIPMENT_ROUTE_CARD.md`, review disposition `YELLOW` until Unity import/profiler proof exists.



## First 20 Minutes Route Impact



First 20 Minutes moment: Tool / Hazard / Proof.



Route impact: flare, scanner pulse, and gravity tether facts are deterministic Vault/SignalBus data, not scene-object local state.

This supports first-route scanner and hazard/tool proof. End-to-end playable proof still requires Unity, Profiler, and Frame Debugger artifacts.



Proof required:

- Clean Unity import/Console for SHINOBU_229 files.
- Play Mode smoke: flare, sensor ping, gravity tether.
- 0 B/frame Profiler or GCMonitor capture.
- SignalBus pressure telemetry under mock deployments.
- VFX double-buffer upload proof.



Parked work rejected: scanner lore/UI managed-string rewrite, downstream audio/AI/radar consumer migrations, tether-manager cold pool removal, Data Monolith h8bin migration, and presentation owner visuals.



## Authority



`AuxiliaryEquipmentRouterRuntime` owns deployed flare, sensor ping, and gravity tether lifecycle records. It does not own lighting, sonar synthesis, or tether physics solvers.



The runtime is created once from `GameBootstrapper` during equipment interaction dependency registration.

- Tool facades do not allocate on first use.
- Flare/trap facades keep no local lifetime, enum-state, or active-state mirrors.
- Compatibility reads resolve from router/Vault state.



## Vault Buffers



- `ShinobuAuxiliaryDeployments`: `DeployedAuxiliaryDTO[1024]`, 64 bytes per record, AUP at offset 0.



- `ShinobuAuxiliaryStates`: `AuxiliaryStateDTO[1024]`, 16 bytes per record.



- `ShinobuAuxiliaryTetherAnchors`: `AuxiliaryTetherAnchorDTO[1024]`, 32 bytes per record, per-deployment anchor AUP.



- `ShinobuAuxiliaryActiveCount`: `int[1]`, initialized deployment bound.



- `ShinobuAuxiliaryActiveEquipmentState`: `AuxiliaryActiveEquipmentDTO[1024]`, auxiliary-only 32-byte mirror; not the modular equipment engine buffer and not dependent on `Hecton8.Tools`.



- `ShinobuAuxiliaryRouteCounters`: per-slot signal counters.



- `ShinobuAuxiliaryVfxMatrices`: staged presentation matrices after AUP subtraction.



- `ShinobuAuxiliaryTelemetryRing`: 300-frame black box.



- Scheduled lifecycle/VFX work locks `Deployments`, `States`, `TetherAnchors`, `ActiveCount`, `RouteCounters`, `VfxMatrices`, `TelemetryRing`, `TelemetryCursor`, and `ActiveEquipmentState` as runtime fence, then re-resolves the Vault views under that lock before scheduling/finalization.
- Tuning is copied into job value fields before scheduling.
- Editor tuning writes use a separate `ShinobuAuxiliaryTuning` lock.
- Telemetry writes after pending fence while proof buffers remain locked.



- `StageAuxiliaryVFXJob` writes `ShinobuAuxiliaryVfxMatrices`.
- After the pending fence, the router uploads that contiguous matrix payload into double-buffered persistent structured `GraphicsBuffer` pages, exposed through `TryReadVfxGraphicsBuffer` for downstream presentation owners.
- Buffers are created with `GraphicsBuffer.UsageFlags.LockBufferForWrite` through `GraphicsBufferUploadUtility.CreateStructuredLockBuffer`, and upload uses `GraphicsBufferUploadUtility.UploadNativeArray`, which maps `LockBufferForWrite`, copies through `UnsafeMemoryCopyGuard.TryMemCpy`, then unlocks.
- The auxiliary VFX path does not call `GraphicsBuffer.SetData`.
- The upload is dirty-gated by active count, deployment snapshot hash, camera AUP, and quality weight; unchanged frames keep the last read buffer and skip CPU-to-GPU bandwidth.



`GenerateMockDeployments()` schedules `GenerateMockAuxiliaryDeploymentsJob` and the dependent `StageAuxiliaryVFXJob` behind the router pending fence. It no longer force-completes from `Tick`; the only forced completion left is teardown.



- `Assets/_SourceData/Equipment/Auxiliary/auxiliary_equipment_profiles.csv` is an editor/source-data input only.
- Editor path hydrates `ShinobuAuxiliaryCsvScratch`, parses `ReadOnlySpan<byte>`, writes `ShinobuAuxiliaryProfiles`, and applies `AuxiliaryTuningDTO`; players use unmanaged fallback profiles until baked binary route exists.
- This CSV is not Data Monolith readiness proof.
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists in the current X_012 scan; route-specific boot proof remains pending, so h8bin migration remains pending under the Data Monolith owner.



## Signal Lanes



- `AuxiliaryFlareLightSignal`: AUP, intensity, range, deterministic source hash.



- `AuxiliarySonarRequestSignal`: AUP, current radius, expansion rate, max radius.



- `AuxiliaryTetherConnectionSignal`: projectile AUP, anchor AUP, rest length.



- Burst producers open typed lanes through `SignalBus<T>.OpenParallelWriter()` and sanitize lifetime, radius, cadence, scalar, and tether rest-length inputs before enqueue.
- Auxiliary flare, sonar, and tether lanes prewarm `1024` queue slots and cap maximum-quality flushes at `1024`, matching the maximum one-signal-per-active-slot producer ceiling.
- Minimum-quality SignalBus flush caps intentionally shed visual/effect bandwidth at `64` flare, `32` sonar, and `16` tether signals per frame; deployment truth remains in Vault.
- Route counters are attempted enqueue counts; `AuxiliaryTelemetryEntry` also records the typed SignalBus lanes' last-flush dropped/corrupted/peak-queued counters so attempts are not reported as guaranteed delivery.


## Legacy Facades



`DeployableFlare`, `GravTrap`, `GravityTetherTool`, and scanner pulse are compatibility shells.

They no longer own Light, ParticleSystem, Rigidbody, Collider buffers, Unity joints, per-object pulse drawers, or local radar pulse lifetime.



- `ScannerTool` still owns its scientific scan and lore query responsibilities, but its radar pulse visual request is now only a `TryDeploySensorPing` call into the auxiliary router.
- The authored scan radius is stored in `AuxiliaryStateDTO.Scalar0` and emitted as `AuxiliarySonarRequestSignal.MaxRadius`.
- Active sonar audio no longer calls `IAudioService.PlayAtPoint` or uses `AudioClip` fields; it emits `AcousticPingSignal` with sonar flags and AUP payload.



- `HectonScannerProjectionFeature` consumes the `AuxiliarySonarRequestSignal` frame snapshot for its screen-space projection.
- It subtracts `HectonFloatingOrigin.CurrentTotalOffsetDouble` from the signal AUP in double precision before local float shader upload, derives presentation age from `CurrentRadius / MaxRadius`, and the shader uses `worldPos - localOrigin`.
- `ScannerTool` no longer publishes `HectonScannerProjectionState` directly, and the unused static shadow-state file was deleted with its `.meta`.
- The projection route no longer uses `Time.time`, `StartTime`, or `Duration`.



`GravTrap` no longer emits zero-length constraints. It routes `pullRadius` shell sample to trap center anchor.

`GravityTetherTool` uses a forward range endpoint when no explicit chest target exists.



Diagnostic deployment reads fail closed while the lifecycle job is active.

The router does not hand out read-only Vault aliases while a scheduled writer owns the buffer.



## Blocked Residue



`TetherManager.cs` contains one cold `new GameObject("TetherInstance")` in the tether/cable physics domain. This router does not edit it. The scanner reports it as cross-domain residue.



## Verification Notes



- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was attempted once after CPU guard cleared.
- It failed before clean verification because ignored generated `Hecton8.Core.csproj` had not imported new auxiliary files and the wider repo still contains unresolved sibling-agent symbols.
- Stable Unity `.meta` files were added for the new folders/scripts; generated `.csproj` was not edited as source.
- `Directory.Build.targets` prunes the deleted scanner projection compile item and conditionally includes SHINOBU_229 runtime/editor sources, clearing this generated metadata gap for the next guarded compile.



Telemetry caveat: `AuxiliaryTelemetryEntry.CpuMicroseconds` currently records schedule-to-finalize wall time around the pending job chain. Exact Burst-kernel execution timing remains pending Unity profiler/marker proof after the compile wall clears.



- Subagent residual audit: scanner scientific/lore discovery still performs managed string construction and broad scanner knowledge/UI coupling remains in `ScannerTool`.
- `ScannerToolActiveSignal` now pushes directly to `SignalBus<ScannerToolActiveSignal>` each registered `LateFrameTick`; downstream `GlobalSignals.TryGetLatestScannerToolActiveSignal` readers remain legacy bridge debt outside SHINOBU_229 ownership.
- The owned radar pulse lifetime and projection route are SignalBus-only through `AuxiliarySonarRequestSignal`.

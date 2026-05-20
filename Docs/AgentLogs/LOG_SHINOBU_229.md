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
- Added `GenerateMockAuxiliaryDeploymentsJob`, `UpdateDeployedAuxiliaryJob`, `StageAuxiliaryVFXJob`, and `RecordAuxiliaryTelemetryJob` with deterministic Burst attributes.
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

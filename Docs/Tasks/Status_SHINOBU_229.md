# SHINOBU_229 Status

Agent: SHINOBU_229
Domain: AUXILIARY_EQUIPMENT_ROUTER
Task count: 20
Status: PENDING GUARDED COMPILE / PROFILER PROOF - CPU GUARD CURRENTLY BLOCKS REBUILD

## Mandates Selected Before Coding

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - hot path must stay 0 B GC; no GameObject/Light/Joints/managed events for auxiliary lifecycle.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` - NativeArray ownership, job fences, no mid-frame Complete, UninitializedMemory when fully overwritten.
- `DATA_Runtime_Struct_Layout_ARM64.txt` - explicit unmanaged DTO layout, 8-byte multiple, padding audit.
- `MATH_AUP_Determinism_Sync.txt` - AUP remains spatial authority; no early float truncation in signal payloads.
- `ARCH_Signal_Lane_Segregation.txt` - first-party gameplay broadcasts use typed unmanaged SignalBus lanes.
- `ARCH_Execution_Phases.txt` - lifecycle in SIMULATION, routing/telemetry in POST_SIMULATION, VFX staging for VISUAL_SYNC.
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt` - tool modules route math requests; Unity joints and component-owned physics are forbidden.
- `PHYS_Tether_Cable_Acceleration_Constraints.txt` - gravity/tether gameplay must be constraint packet routing, not Unity Joint ownership.

## State Machine Checklist

- [x] Task 01 MONOBEHAVIOUR_AUXILIARY_INQUISITION | Implemented; compile pending because CPU guard blocked build.
- [x] Task 02 UNITY_LIGHT_AND_JOINT_PURGE | Implemented for auxiliary hot facades; `TetherManager` cold pool is cross-domain residue.
- [x] Task 03 CS1612_HOT_PATH_PROPERTY_ANNIHILATION | Implemented in auxiliary DTO/job layer with raw fields and pointer refs.
- [x] Task 04 ARM64_AUX_LAYOUT_ASSERTION | Implemented via explicit layout DTOs and editor ABI validator.
- [x] Task 05 EMERGENCY_MOCK_AUX_DEPLOYMENT | Implemented as Burst `GenerateMockAuxiliaryDeploymentsJob`.
- [x] Task 06 BURST_AUXILIARY_LIFECYCLE_KERNEL | Implemented as Burst `UpdateDeployedAuxiliaryJob`.
- [x] Task 07 FLARE_LIGHTING_ROUTING | Implemented as `AuxiliaryFlareLightSignal` SignalBus lane.
- [x] Task 08 SENSOR_PING_RAYMARCH_DISPATCH | Implemented as `AuxiliarySonarRequestSignal` SignalBus lane; `ScannerTool` pulse routes through the auxiliary router.
- [x] Task 09 THE_DEAR_LIE_GRAVITY_TETHER | Implemented as `AuxiliaryTetherConnectionSignal` SignalBus lane.
- [x] Task 10 CONTINUOUS_SCALABILITY_TICK_MODULATION | Implemented with continuous 15Hz-60Hz cadence curve.
- [x] Task 11 AUP_PRECISION_SIGNAL_LOCALIZATION | Implemented; signal payloads carry `double3` AUP.
- [x] Task 12 ASYNCHRONOUS_VFX_STAGING | Implemented as `StageAuxiliaryVFXJob`.
- [x] Task 13 ROLLBACK_NETCODE_STATE_FENCE | Implemented with deterministic Burst and AUP/frame hash seeding.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS | Implemented for large auxiliary arrays with `UninitializedMemory`.
- [ ] Task 15 TELEMETRY_AUXILIARY_RECORDER | 300-frame telemetry ring and raw dump path implemented; exact Burst-kernel timing proof remains pending Unity profiler/marker artifact. Current `CpuMicroseconds` is schedule-to-finalize wall time.
- [x] Task 16 AUXILIARY_ROUTER_XRAY_WINDOW | Implemented as UI Toolkit editor window.
- [x] Task 17 CSV_AUXILIARY_PROFILES_INGESTOR | Implemented as `ReadOnlySpan<byte>` cold parser.
- [x] Task 18 LIVE_DEPLOYMENT_DEBUG_GIZMO | Implemented as editor AUP deployment gizmo.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | Implemented; report marks one cross-domain `TetherManager` cold pool finding.
- [ ] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | Static audit written and safety/refcount/lock-fence passes applied; stale generated-project entry is shielded by `Directory.Build.targets`, but guarded compile/profiler proof has not rerun because CPU guard remains at 100% and unrelated sibling-agent missing types may remain.

## Loop Log

### Loop 0 - Initialization

- [x] Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` by `SHINOBU_229` XML tag using CLI regex | DOD practice: batch prompt protocol, exact task count 20 | Rejected: relying on chat prompt summary or neighboring XML blocks | Estimate: 2000 us.
- [x] Domain checked against `Docs/Actual Domains of Project.txt` | DOD practice: domain-boundary read before edits | Rejected: broad cross-domain edits without boundary proof | Estimate: 3000 us.
- [x] Mandates selected and read before code | DOD practice: mandate registry first | Rejected: writing DTOs before zero-GC/ARM64/AUP/signal phase constraints were loaded | Estimate: 19000 us.

### Loop 1 - Tasks 01-05

- [x] Legacy flare/trap/tether entrypoints scanned | DOD practice: targeted OOP inquisition with `rg` and file reads | Rejected: blind delete across scanner/tether-manager domains | Estimate: 12000 us.
- [x] `DeployableFlare`, `GravTrap`, `GravityTetherTool` reduced to router facades | DOD practice: no Light/ParticleSystem/Rigidbody/Collider broadphase in auxiliary hot path | Rejected: keeping ITickable/ISlowTickable compatibility | Estimate: saved 400-1600 us/frame at 50 active auxiliaries.
- [x] DTO ABI created for deployment/state/signals | DOD practice: explicit offsets, raw fields, no properties in job structs | Rejected: auto-layout payloads | Estimate: saved 50-300 us/frame under 500 records.
- [x] Mock deployment job added | DOD practice: deterministic Burst load generator, 500 records | Rejected: waiting on player equipment path | Estimate: 10000 us test setup saved per run.
- [ ] Compile verification | Blocked: CPU sample 100%; build launch forbidden by project protocol.

### Loop 2 - Tasks 06-10

- [x] Burst lifecycle kernel implemented | DOD practice: `IJobParallelFor`, deterministic float mode, NativeArray refs | Rejected: managed foreach or per-prefab Update | Estimate: saved 2500-7000 us/frame under 500 auxiliaries.
- [x] Flare lighting route implemented | DOD practice: scalar flicker plus AUP SignalBus payload | Rejected: Unity Light mutation | Estimate: saved 15-80 us/frame per active flare depending shadows.
- [x] Sensor ping route implemented | DOD practice: expanding radius signal, no SphereCollider | Rejected: collider pulse prefab | Estimate: saved 200-900 us per ping wave.
- [x] Gravity tether route implemented | DOD practice: AUP projectile/anchor signal, no Joint/force ownership | Rejected: PhysicsForceRouter loop in tool | Estimate: saved 300-1200 us/frame during loot pull.
- [x] Continuous quality cadence implemented | DOD practice: `GlobalQualityWeight` curve 15Hz-60Hz | Rejected: low/high binary switches | Estimate: sheds up to 75% auxiliary ALU at quality 0.

### Loop 3 - Tasks 11-15

- [x] AUP signal localization implemented | DOD practice: double3 in all route signals, local float only in VFX staging | Rejected: Vector3 payload truth | Estimate: jitter avoided at 100km scale.
- [x] Asynchronous VFX staging implemented | DOD practice: matrices in NativeArray after camera-AUP subtraction | Rejected: ParticleSystem ownership | Estimate: saved hierarchy cost for thousands of emitters.
- [x] Deterministic rollback fence implemented | DOD practice: deterministic Burst and AUP/frame hash | Rejected: random flicker seeded by managed time | Estimate: desync false-positive avoidance, not frame-time quantified.
- [x] Zero-init bypass implemented | DOD practice: `UninitializedMemory` for large arrays, deterministic overwrite on spawn/mock/update | Rejected: blanket MemClear | Estimate: avoids 64-160 KB cold clear per router boot.
- [x] Telemetry ring/dump implemented | DOD practice: 300-frame NativeArray ring and raw dump file | Rejected: managed list diagnostics | Estimate: 0 B hot-path telemetry target.

### Loop 4 - Tasks 16-19

- [x] X-Ray editor window implemented | DOD practice: UI Toolkit, direct Vault telemetry/tuning mutation | Rejected: play-mode Mono HUD | Estimate: editor-only.
- [x] CSV parser implemented | DOD practice: `ReadOnlySpan<byte>`, FNV, manual float parse | Rejected: `string.Split`/`float.Parse` | Estimate: cold boot parser avoids profile string arrays.
- [x] Debug gizmo implemented | DOD practice: AUP subtract then editor draw | Rejected: scene GameObject debug markers | Estimate: editor-only.
- [x] OOP scanner/report implemented | DOD practice: static target scan and shared report append | Rejected: overwriting other agents' report entries | Estimate: editor-only; one cross-domain finding recorded.

### Loop 5 - Self Audit

- [x] Dedicated auxiliary `AuxiliaryActiveEquipmentDTO` buffer added | DOD practice: no ownership collision with modular equipment state or sibling `Hecton8.Tools` imports | Rejected: writing 1024 auxiliaries into 5-slot player tool buffer | Estimate: prevents lock conflict/stale ownership.
- [x] Facade cancel route added | DOD practice: deploy/cancel writes are locked and compact active count | Rejected: leaving NativeArray records alive after `ForceExtinguish`/`Deactivate` | Estimate: prevents stale signal routing.
- [x] Active-bound guard added for uninitialized arrays | DOD practice: no read above `ShinobuAuxiliaryActiveCount` | Rejected: cold clearing all 1024 records | Estimate: 40-120 us/frame saved at idle.
- [x] Bound/live-count semantics fixed | DOD practice: telemetry no longer shrinks initialized bound through holes | Rejected: unsafe swap-pop in `IJobParallelFor` | Estimate: 20-90 us/frame saved versus full serial compaction.
- [x] Bootstrap hook added | DOD practice: cold router creation during equipment dependency registration | Rejected: first-use GameObject allocation inside deploy facade | Estimate: prevents runtime route failure without hot-path allocation.
- [x] Architecture doc and XML self-audit written | DOD practice: on-disk memory survives compaction | Rejected: chat-only proof | Estimate: 3000 us future integration lookup saved.
- [ ] Compile/profiler proof | CPU guard later cleared at 6.2%; `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was attempted once and failed before clean verification because generated `Hecton8.Core.csproj` had not imported new auxiliary files and the wider repo still misses sibling-agent types (`Hecton8.Logistics.Grid`, docking/autopilot, audio signal, world health bridges). `dotnet build-server shutdown` was run afterward; no dotnet/csc/MSBuild process remains.

### Loop 6 - Radar Pulse Purge

- [x] `ScannerTool` OOP pulse state removed | DOD practice: sensor ping visual authority moved to `AuxiliaryEquipmentRouterRuntime`/SignalBus | Rejected: keeping `PulseActive`, `PulseOriginAup`, and `PulseStartTime` in the scanner tool | Estimate: saved 20-80 us/frame during scan pulse windows and removed one managed state owner.
- [x] `ScannerPulseDrawer` deleted | DOD practice: no MonoBehaviour/ITickable pulse drawer, no per-pulse `Material`, no `Matrix4x4[]`, no `Graphics.DrawMeshInstanced` from scanner | Rejected: hiding drawer behind cold AddComponent | Estimate: saved 80-250 us per active pulse frame plus cold material allocation.
- [x] Scanner ping radius routed as data | DOD practice: `TryDeploySensorPing(scanPosition, pulseDuration, effectiveScanRadius)` stores max radius in `AuxiliaryStateDTO.Scalar0` and expands continuously in Burst | Rejected: fixed global ping radius or collider pulse | Estimate: preserves authored scan radius without a local pulse simulation.
- [x] Unity `.meta` files added for new auxiliary folders/scripts | DOD practice: stable asset GUIDs so Unity import/project regeneration can see the router namespace | Rejected: editing ignored generated `.csproj` as source | Estimate: avoids repeated namespace compile failures after AssetDatabase refresh.
- [x] Static radar residue scan rerun | DOD practice: `rg` found 0 hits for `ScannerPulseDrawer`, `PulseActive`, `PulseOrigin`, `PulseStartTime`, `ScannerPulseShader`, and scanner-local `Graphics.DrawMeshInstanced` | Rejected: relying on visual inspection | Estimate: editor-only proof.

### Loop 7 - Authority Purity And Route Card

- [x] Read-looking APIs made pure | DOD practice: `TryReadTelemetry`, `TryReadDeployments`, `TryReadTuning`, and `TryWriteTuning` now resolve only existing Vault handles | Rejected: hidden `GetGenerationHandle` allocation inside read APIs | Estimate: prevents cold allocation/growth from editor/status polling.
- [x] Hot paths fail closed on missing Vault readiness | DOD practice: `Tick`, deploy, cancel, and telemetry finalization use `TryResolveExistingViews`; handle acquisition remains in bootstrap/explicit init/mock path | Rejected: per-frame `GlobalRegistry.DataVault` retry or `TryGetLatestCreated` fallback | Estimate: avoids hidden registry/vault work under load.
- [x] Continuous quality override fixed | DOD practice: `AuxiliaryTuningFlags.OverrideGlobalQualityWeight` allows authored 0.0 override while default follows live `HomeostasisBrain.GlobalQualityWeight` | Rejected: `overrideWeight > 0` test that made 0.0 impossible | Estimate: preserves minimum-survival quality scaling.
- [x] Route card added | DOD practice: `Docs/ARCHITECTURE/SHINOBU_229_AUXILIARY_EQUIPMENT_ROUTE_CARD.md` records owner, instruments, phases, cadence, capacity, failure, telemetry, shutdown, and proof | Rejected: architecture note without review disposition | Estimate: integration blocker visibility.
- [x] Static authority scan rerun | DOD practice: no `TryGetLatestCreated`, scanner pulse state, Unity physics/light patterns, or hot OOP remnants in runtime targets; editor scanner literal strings are ignored | Rejected: compile-only proof claim | Estimate: static gate only.

### Loop 8 - Safety Metadata And Radar Boundary Audit

- [x] Prompt block re-extracted with attribute-tolerant CLI regex | DOD practice: exact `SHINOBU_229` XML block only, 20 task lines | Rejected: stale regex that assumed `id` was the only `AGENT_PROMPT` attribute | Estimate: 2000 us.
- [x] Mock job safety metadata corrected | DOD practice: `GenerateMockAuxiliaryDeploymentsJob.ActiveCount` is writable because index 0 writes the initialized bound | Rejected: `[ReadOnly]` safety lie on a written NativeArray | Estimate: prevents job safety rejection; runtime us unchanged.
- [x] Auxiliary `ActiveEquipmentDTO` mirror handle release fixed | DOD practice: all acquired Vault generation handles release through `ReleaseHandle` on disable | Rejected: dropping `_activeEquipmentHandle` to default without refcount release | Estimate: prevents persistent handle leak across scene reloads.
- [x] Broad radar/sonar search audited for domain ownership | DOD practice: `ScannerTool` active pulse lifecycle is routed; `Visor/SpectrumSystem`, Audio sonar, cockpit radar, and AI sensory hits are downstream/cross-domain consumers | Rejected: editing UI/audio/AI radar systems from auxiliary router without route ownership | Estimate: avoids compile-wall and authority drift.
- [x] Static safety scan rerun | DOD practice: only editor scanner literal strings and cold facade read-only properties remain; no runtime auxiliary Light/Joint/ParticleSystem/SphereCollider/OverlapSphere/ScannerPulseDrawer residue | Rejected: relying on previous scan after patch | Estimate: static gate only.

### Loop 9 - Read-Only Deployment Snapshot Seal

- [x] Mutable deployment read accessor sealed | DOD practice: `TryReadDeployments` now returns `NativeArray<DeployedAuxiliaryDTO>.ReadOnly` instead of mutable Vault storage | Rejected: editor/gizmo consumers receiving writable deployment truth | Estimate: 0 us direct; removes external mutation route.
- [x] Existing editor consumers kept read-only | DOD practice: X-Ray histogram and gizmo index into read-only alias only | Rejected: adding a copy buffer or managed list for diagnostics | Estimate: avoids diagnostic allocation and preserves Vault ownership.

### Loop 10 - Producer-Side Signal NaN Vaccination

- [x] Tuning scalar sanitizers added | DOD practice: route jobs sanitize non-finite/negative tuning before `ParallelWriter.Enqueue` because job producers bypass managed `SignalBus.TryPush` guards | Rejected: relying on downstream signal flush to repair NaN payloads | Estimate: prevents queue poisoning; ALU cost is constant.
- [x] Gravity tether anchor finite guard added | DOD practice: deploy rejects non-finite projectile/anchor AUP and job drops non-finite anchor route with fault telemetry | Rejected: allowing NaN rest length into tether signal lane | Estimate: prevents catastrophic physics consumer contamination.
- [x] Sanitizer fallbacks hardened | DOD practice: `Sanitize01`, `SanitizeNonNegative`, and `SanitizePositive` sanitize fallback constants/inputs before signal payload construction | Rejected: trusting fallback arguments from future tuning bridges | Estimate: prevents rare non-finite fallback propagation; constant ALU.

### Loop 11 - Vault Lock Fence Closure

- [x] `ActiveCount` added to runtime Vault lock fence | DOD practice: every NativeArray used by scheduled lifecycle/VFX/telemetry jobs is locked before scheduling and unlocked after finalization | Rejected: treating the scalar initialized-bound buffer as relocation-safe because it has length 1 | Estimate: prevents stale pointer/relocation hazard; direct frame-time gain 0 us.
- [x] Subagent SignalBus/Vault contract audit integrated | DOD practice: accepted concrete defect and left confirmed-compatible SignalBus, registry, dispatcher, and handle-release routes untouched | Rejected: broad dependency rewrite after static API compatibility was confirmed | Estimate: avoids compile-wall churn; static integration risk reduced.

### Loop 12 - Subagent Defect Burn-Down

- [x] Per-deployment tether anchors added | DOD practice: `AuxiliaryTetherAnchorDTO[1024]` in Vault stores one anchor per tether slot; `DeployedAuxiliaryDTO` 64-byte rollback ABI remains unchanged | Rejected: one runtime `_lastTetherAnchorAup` shared by all active tethers | Estimate: prevents concurrent tether corruption; direct frame-time gain 0 us.
- [x] Facade cancel/readback hardened | DOD practice: flare/trap facades cancel routed records on disable and derive compatibility state from pure router readback | Rejected: local facade state as gameplay truth | Estimate: prevents stale records; no hot allocation.
- [x] Gravity trap radius routed as data | DOD practice: trap deploys a shell sample at `pullRadius` to center anchor, avoiding zero rest length | Rejected: projectile and anchor at identical AUP | Estimate: restores pull semantics without Unity physics.
- [x] CSV profiles integrated into cold boot | DOD practice: StreamingAssets CSV loads into Vault scratch via span and applies parsed profiles to tuning; deterministic fallback profiles seed missing-file CI | Rejected: dead parser with unused Profile/CsvScratch buffers | Estimate: cold boot only; avoids designer recompile loop.
- [x] VFX staging GPU upload route added | DOD practice: Burst writes Vault `AuxiliaryVfxMatrixDTO[]`; post-fence upload copies to a persistent `GraphicsBuffer` exposed through `TryReadVfxGraphicsBuffer` | Rejected: claiming a GraphicsBuffer route while only writing CPU matrices | Estimate: keeps hierarchy-free presentation handoff.
- [x] Telemetry overclaim removed | DOD practice: renamed telemetry recorder to direct post-fence pass and documented wall-time metric honestly | Rejected: pretending direct `.Execute()` is exact Burst execution timing | Estimate: avoids false audit proof; runtime cost unchanged.

### Loop 13 - Lock And Signal Producer Polish

- [x] Signal producer writer route tightened | DOD practice: lifecycle job now opens typed lanes through `SignalBus<T>.OpenParallelWriter()` | Rejected: legacy `SignalBus<T>.ParallelWriter` property even though it forwards today | Estimate: 0 us direct; removes migration/review debt.
- [x] Vault lock-before-resolve discipline added | DOD practice: Tick, deploy, cancel, and mock paths lock runtime buffers before re-resolving job-visible NativeArrays | Rejected: using pre-lock aliases for scheduled jobs | Estimate: 0 us direct; prevents stale alias/relocation hazard.
- [x] Deployment diagnostic read race sealed | DOD practice: `TryReadDeployments` fails closed while `_jobActive` and returns only read-only aliases | Rejected: editor/gizmo read while lifecycle job mutates the same Vault buffer | Estimate: prevents debug-time data race; no hot allocation.
- [x] Tuning write fenced separately | DOD practice: editor tuning mutation locks only `ShinobuAuxiliaryTuning`, resolves the tuning handle, writes one DTO, and unlocks | Rejected: resolving all auxiliary views for one tuning write | Estimate: saves unnecessary handle checks and narrows lock scope.
- [x] Additional finite vaccination added | DOD practice: authored lifetimes/radii, accumulated cadence debt, and tether rest length are sanitized before integration/route emission | Rejected: trusting facade/editor inputs | Estimate: prevents downstream NaN/Infinity packet propagation.

### Loop 14 - Route Delivery And Scanner Projection Closure

- [x] SignalBus delivery accounting clarified | DOD practice: route counters are attempted enqueue counts, while SignalBus last-flush dropped/corrupted/peak queued values are stored in `AuxiliaryTelemetryEntry` | Rejected: reporting enqueue attempts as guaranteed delivery | Estimate: 0 us direct; prevents false telemetry.
- [x] Scanner projection duplicate publish removed | DOD practice: `ScannerTool` emits only `AuxiliarySonarRequestSignal`; `HectonScannerProjectionFeature` consumes the SignalBus snapshot and the unused `HectonScannerProjectionState.cs/.meta` shadow route was deleted | Rejected: second scanner-local `HectonScannerProjectionState.Publish` route | Estimate: prevents duplicate authority and avoids one managed presentation state sync.
- [x] Facade authority hints reduced | DOD practice: `DeployableFlare` marks Burning only after router success; `GravTrap` `_activationIssued` is invalidated by pure router readback | Rejected: local facade booleans as gameplay truth | Estimate: 0 us direct; prevents stale facade truth.
- [x] Data Monolith caveat recorded | DOD practice: CSV is documented as XML-mandated cold bridge/static fallback, not runtime h8bin readiness | Rejected: claiming Data Monolith compliance without `static_data.h8bin` | Estimate: evidence hygiene only.
- [x] Scanner projection AUP downcast fixed | DOD practice: `AuxiliarySonarRequestSignal` AUP is localized by subtracting `HectonFloatingOrigin.CurrentTotalOffsetDouble` in double precision before float shader upload | Rejected: absolute float AUP subtraction in shader | Estimate: 0 us direct; prevents 100km-scale projection jitter.
- [x] Generated project staleness re-confirmed | DOD practice: `rg` over `*.csproj` shows `Hecton8.Core.csproj` still references deleted `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs` and still does not enumerate new auxiliary sources | Rejected: editing generated project files as source | Estimate: prevents a knowingly noisy build loop.

### Loop 15 - Subagent DOD Hardening

- [x] Auxiliary sibling DTO dependency removed | DOD practice: local `AuxiliaryActiveEquipmentDTO` mirrors auxiliary state without importing `Hecton8.Tools` or reusing modular equipment DTOs | Rejected: sibling `ActiveEquipmentDTO` dependency in the auxiliary hot assembly surface | Estimate: protects compile wall; runtime gain 0 us.
- [x] Router AUP conversion decoupled from `Hecton8.World` | DOD practice: runtime-position deploy calls add local meters to `HectonFloatingOrigin.CurrentTotalOffsetDouble` directly | Rejected: `AbsoluteUniversePosition` helper import from world domain for a simple local offset | Estimate: protects compile wall; runtime gain below noise.
- [x] Mock generation no longer force-completes from `Tick` | DOD practice: mock job schedules with dependent VFX staging and finalizes through the existing LateFrame fence | Rejected: `DispatcherJobFence.TryComplete` in gameplay tick path | Estimate: prevents a same-frame completion wall under seed/mock paths.
- [x] Read-looking mutation reduced | DOD practice: `GravTrap.IsActive` no longer mutates `_activationIssued`; scanner quality methods with cache mutation were renamed from `Resolve*` to `Refresh*` | Rejected: accessor names that hide state writes | Estimate: correctness/authority hygiene, 0 us direct.
- [x] Telemetry label de-overclaimed | DOD practice: editor X-Ray now labels the existing metric as `Wallus`, matching schedule-to-finalize wall-time documentation | Rejected: UI label implying exact CPU/Burst kernel time | Estimate: evidence hygiene only.
- [x] Scanner residuals bounded | DOD practice: subagent findings for scanner scientific/lore managed strings and legacy `ScannerToolActiveSignal` bridge are documented as outside SHINOBU_229 auxiliary pulse lifecycle ownership | Rejected: broad rewrite of localization/lore scanner route from the auxiliary router pass | Estimate: avoids compile-wall churn; active radar pulse remains SignalBus-owned.

### Loop 16 - Final Static Route Gate

- [x] Prompt block re-extracted after hardening | DOD practice: exact `SHINOBU_229` XML block read from `Docs/Tasks/CURRENT_BATCH.md` with 20 task lines | Rejected: relying on compacted chat state | Estimate: 2000 us.
- [x] Runtime forbidden-pattern scan rerun | DOD practice: touched runtime/facade/projection files show no `ScannerPulseDrawer`, scanner pulse shadow state, Unity Light/Joint/ParticleSystem/OverlapSphere, `UnityEngine.Random`, or `Time.deltaTime` residues | Rejected: claiming OOP purge from prior scans only | Estimate: static gate only.
- [x] Signal/Vault route scan rerun | DOD practice: auxiliary runtime uses `SignalBus<T>.OpenParallelWriter()`, existing Vault handles, runtime-buffer locks, and teardown-only force completion | Rejected: hidden hot `GlobalRegistry` polling, `TryGetLatestCreated`, or same-frame job readback loops | Estimate: static gate only.
- [x] ABI/report parse gate rerun | DOD practice: `SHINOBU_229_SELF_AUDIT.xml` parses as XML and `EQUIPMENT_OPTIMIZATION_REPORT.json` parses as JSON; no orphan `.cs.meta`/`.shader.meta` remains under `Assets/_Project` | Rejected: text-only report without machine parse | Estimate: static gate only.
- [ ] Unity compile/profiler gate | Blocked: `Hecton8.Core.csproj` still references deleted `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs` and omits new auxiliary files until Unity regenerates generated project metadata. Build was intentionally not relaunched against known stale metadata.

### Loop 17 - VFX Upload Bandwidth Gate

- [x] VFX GPU handoff double-buffered | DOD practice: replace single persistent VFX `GraphicsBuffer` with A/B pages plus immutable read-buffer pointer | Rejected: writing into the same buffer that downstream VISUAL_SYNC might still read | Estimate: avoids driver/GPU synchronization bubbles; exact us pending Frame Debugger/profiler.
- [x] VFX upload dirty-gated | DOD practice: skip `UploadNativeArray` when active count, deployment snapshot hash, camera AUP, and quality weight are unchanged | Rejected: unconditional post-fence upload of the staged matrix array every frame | Estimate: saves up to one `AuxiliaryVfxMatrixDTO[active]` CPU-to-GPU copy on static frames.
- [x] Dirty-gate self-invalidation fixed | DOD practice: preserve previous upload count before comparing the snapshot | Rejected: resetting `_lastVfxUploadCount` before comparison, which would make the gate always miss | Estimate: restores intended zero-upload static frames.

### Loop 18 - Subagent Capacity And Scanner Audio Burn-Down

- [x] SignalBus prewarm ceiling fixed | DOD practice: auxiliary flare, sonar, and tether lanes now configure expected capacity as 1024, matching one signal per active deployment | Rejected: 256/256/128 prewarm that could grow NativeQueue storage under same-frame route pressure | Estimate: avoids hot native queue growth under 1024-slot stress.
- [x] Scanner active audio route moved to SignalBus | DOD practice: active scanner pulse now publishes `AcousticPingSignal` with AUP payload and active-sonar flags | Rejected: direct `IAudioService.PlayAtPoint` call from scanner pulse path | Estimate: removes direct audio service invocation from radar pulse activation; exact us pending profiler.
- [x] Scanner legacy event boundary documented | DOD practice: `ScanEvents.RaiseScanTriggered` remains scanner-log/progression legacy routing, not auxiliary light/physics/VFX effect authority | Rejected: editing scan-log/progression ownership from the auxiliary router pass | Estimate: avoids compile-wall churn outside assigned route.

### Loop 19 - Post-Subagent Static Gate

- [x] Scanner active audio scan rerun | DOD practice: targeted `rg` found no `_cachedAudioService`, `IAudioService`, `GlobalRegistry.Audio`, or `PlayAtPoint(pingClip)` in `ScannerTool` | Rejected: assuming the patch removed direct audio without source proof | Estimate: static gate only.
- [x] Auxiliary lane capacity scan rerun | DOD practice: `EnsureSignalLanes` configures flare/sonar/tether lanes with `AuxiliaryEquipmentConstants.MaxDeployedAuxiliaries` for both expected and max-frame counts | Rejected: leaving prewarm smaller than producer ceiling | Estimate: prevents hot NativeQueue growth under 1024 route pressure.
- [x] VFX upload gate scan rerun | DOD practice: A/B `GraphicsBuffer` pages plus `_vfxGpuReadBuffer` remain the only VFX GPU owners; dirty-gate hash route remains present | Rejected: single buffer or unconditional upload regression | Estimate: static frames skip one staged matrix upload.
- [x] Audit artifact parse rerun | DOD practice: `SHINOBU_229_SELF_AUDIT.xml` parses as XML and `EQUIPMENT_OPTIMIZATION_REPORT.json` parses as JSON after capacity/audio updates | Rejected: chat-only report claims | Estimate: static gate only.
- [ ] Unity compile/profiler gate | Blocked: `Hecton8.Core.csproj` still references deleted `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs`; no rebuild launched against known stale generated metadata.

### Loop 20 - Facade Shadow-State And Audio Asset Gate Purge

- [x] Scanner AudioClip gate removed | DOD practice: active acoustic pulse publishes `AcousticPingSignal` unconditionally from scanner route data; no `AudioClip`, `pingClip`, or `cooldownClip` fields remain in `ScannerTool` | Rejected: Unity-object asset presence deciding whether an effect signal exists | Estimate: removes one object-reference branch from scan activation; exact us pending profiler.
- [x] Flare local lifetime mirror removed | DOD practice: `DeployableFlare.RemainingFuel` reads central router lifetime only; local `_fuelTimer` shadow fact is gone | Rejected: facade-owned countdown cache | Estimate: prevents stale compatibility lifetime; no hot allocation.
- [x] GravTrap local activation mirror removed | DOD practice: `GravTrap.IsActive` reads central router state only; local `_activationIssued` shadow fact is gone | Rejected: facade boolean as gameplay truth | Estimate: prevents stale compatibility active flag; no hot allocation.
- [ ] Unity compile/profiler gate | Still blocked by stale generated project metadata and missing runtime artifacts; no rebuild launched.

### Loop 21 - Generated Project Shield

- [x] Deleted scanner projection state pruned from generated Core item list | DOD practice: `Directory.Build.targets` removes `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs` from `Hecton8.Core` compile items without editing generated `.csproj` | Rejected: hand-editing `Hecton8.Core.csproj` | Estimate: removes a known stale-file compile wall before source compilation.
- [x] New auxiliary runtime sources conditionally included | DOD practice: `Directory.Build.targets` conditionally includes SHINOBU_229 runtime auxiliary source files when Unity-generated project metadata has not regenerated | Rejected: moving files into unrelated folders or committing generated project edits | Estimate: makes future guarded compile reach real source errors instead of missing namespace staleness.
- [x] New auxiliary editor facade conditionally included | DOD practice: `Directory.Build.targets` conditionally includes `AuxiliaryEquipmentEditorTools.cs` in `Hecton8.Editor` project metadata | Rejected: excluding the required X-Ray/static scanner from generated editor project proof | Estimate: editor compile visibility only.
- [x] Build metadata static gate | DOD practice: `Directory.Build.targets` parses as XML and diff check has no whitespace errors | Rejected: launching rebuild before metadata guard validation | Estimate: static gate only.
- [ ] Unity compile/profiler gate | Rebuild still not launched; sibling-agent dependency errors and runtime profiler artifacts remain unverified.

### Loop 22 - Compile-Hazard Subagent And Flare Readback Regression

- [x] Subagent compile/API audit integrated | DOD practice: external static check found no API/namespace hazards in `AcousticPingSignal`, `OpenParallelWriter`, VFX upload utility, DTO fields, or scanner projection SignalBus consumer | Rejected: trusting local patch memory only | Estimate: static gate only.
- [x] Flare expired-state regression fixed | DOD practice: `DeployableFlare.ResolveState()` now returns `Extinguished` when the router record is gone after a Burning state | Rejected: leaving compatibility state stuck Burning after central lifecycle expiry | Estimate: prevents stale facade state; no hot allocation.
- [x] CPU build guard observed | DOD practice: CPU load sampled at 100%, so guarded `dotnet build` was not launched | Rejected: violating compile discipline to test during system load | Estimate: avoids IO/CPU contention.

### Loop 23 - Telemetry Vault Fence Closure

- [x] Telemetry ring/cursor added to runtime lock fence | DOD practice: every Vault buffer written during post-fence auxiliary finalization is locked with the simulation buffers until telemetry recording and fault dump checks finish | Rejected: treating post-fence telemetry writes as relocation-safe without a Vault lock | Estimate: 0 us direct; prevents stale handle/relocation corruption during diagnostics.
- [x] Static lock-fence proof rerun | DOD practice: source scan confirms `TryLockBuffer` and `TryUnlockBuffer` calls for `TelemetryRing` and `TelemetryCursor`; XML/JSON reports parse; `git diff --check` reports no whitespace errors beyond CRLF normalization warnings | Rejected: chat-only lock-fence claim | Estimate: static gate only.
- [ ] Unity compile/profiler gate | CPU guard sampled at 100% again; no guarded rebuild has been launched after this lock-fence patch.

### Loop 24 - Flare Facade State Purge

- [x] `DeployableFlare._state` removed | DOD practice: flare compatibility state is now entirely derived from `AuxiliaryEquipmentRouterRuntime.TryReadNearestRemainingLifetime`; facade methods only route deploy/cancel intent | Rejected: keeping an inactive/extinguished enum mirror as harmless local state | Estimate: 0 us direct; removes final local flare lifecycle shadow fact.
- [x] External-use scan rerun before removal | DOD practice: `rg` showed no external `FlareState`, `DeployableFlare.State`, `IsBurning`, or `RemainingFuel` consumers outside the facade | Rejected: deleting a public compatibility surface blindly | Estimate: static gate only.
- [x] Post-removal facade residue scan rerun | DOD practice: touched flare/trap/scanner route files show no `_state`, `_fuelTimer`, `_activationIssued`, `AudioClip`, direct audio service, or clip gate residue | Rejected: relying on patch review only | Estimate: static gate only.

### Loop 25 - Scanner Projection Wall-Clock Purge

- [x] Prompt block re-extracted before pass | DOD practice: exact `SHINOBU_229` XML block from `CURRENT_BATCH.md`, `Task NN:` count = 20 | Rejected: trusting compacted chat state or broad regex count that matched narrative task words | Estimate: 2000 us.
- [x] Radar projection wall-clock dependency removed | DOD practice: `HectonScannerProjectionFeature` now derives projection age from `AuxiliarySonarRequestSignal.CurrentRadius / MaxRadius` instead of `Time.time` | Rejected: presentation-side wall-clock age for a SignalBus-owned radar pulse | Estimate: 0 us direct; removes a non-authoritative time source from scanner pulse presentation.
- [x] Static projection residue gate rerun | DOD practice: `rg` found no `Time.`, `StartTime`, `Duration`, `_now`, scanner pulse drawer/state, audio clip gate, direct audio service, or deleted projection state residue in owned scanner route files | Rejected: visual inspection only | Estimate: static gate only.
- [ ] Unity compile/profiler gate | CPU guard sampled at 100%; guarded rebuild remains forbidden.

### Loop 26 - Projection Route Documentation Sync

- [x] Machine report parse rerun | DOD practice: `EQUIPMENT_OPTIMIZATION_REPORT.json` parses via `ConvertFrom-Json`, and `SHINOBU_229_SELF_AUDIT.xml` parses via `[xml]` after projection-age edits | Rejected: chat-only audit claim | Estimate: static gate only.
- [x] Route documentation synchronized | DOD practice: route card, SHINOBU architecture note, and binary payload ledger now state that scanner projection age is derived from `AuxiliarySonarRequestSignal.CurrentRadius / MaxRadius`, with no `Time.time`, `StartTime`, `Duration`, or projection-state mirror | Rejected: leaving docs to describe only AUP localization while omitting the wall-clock purge | Estimate: 1000 us future integrator lookup saved.
- [x] Post-doc static gates rerun | DOD practice: `git diff --check` reports only CRLF warnings; projection time residue scan and owned route residue scan return clean | Rejected: assuming docs/source remained parse-clean after patch | Estimate: static gate only.
- [ ] Unity compile/profiler gate | CPU guard sampled `100`; no `dotnet`/`csc`/`MSBuild` processes were listed, but rebuild remains forbidden while CPU is above 50%.

### Loop 27 - Active Pulse Debug Allocation Purge

- [x] Dev-only scanner pulse allocation removed | DOD practice: `ScannerTool.LogScanPulse` no longer builds an interpolated `Debug.Log` string on the active pulse path | Rejected: keeping allocation under `UNITY_EDITOR || DEVELOPMENT_BUILD` because zero-GC proof should not depend on build flavor | Estimate: prevents dev/editor allocation per scan pulse.
- [x] Managed-string static gate rerun | DOD practice: `rg` found no `$"`, `Debug.Log(`, `string.Format`, LINQ, or `foreach` in SHINOBU_229 owned route files after the patch | Rejected: relying on compile stripping or conditional attributes | Estimate: static gate only.
- [x] Build metadata parse and dependency scan rerun | DOD practice: `Directory.Build.targets` parses as XML; auxiliary runtime has no sibling `Hecton8.Tools`, `Hecton8.World`, audio, physics, lighting, sonar, AI, vehicle, construction, logistics, geology, or rendering imports | Rejected: compile attempt before metadata/process/CPU proof | Estimate: static gate only.
- [ ] Unity compile/profiler gate | CPU guard sampled `100`; no `dotnet`/`csc`/`MSBuild` processes were listed, but rebuild remains forbidden while CPU is above 50%.

### Loop 28 - SignalBus Lane Cap Contract Clarification

- [x] SignalBus Configure contract made explicit | DOD practice: auxiliary flare/sonar/tether lanes now call `Configure` with named `expectedCapacity`, `maxFrameSignals`, and `lowTierFrameSignals` arguments | Rejected: positional arguments that require rereading `GlobalSignals.cs` to prove prewarm versus flush caps | Estimate: static review risk reduction; runtime behavior unchanged.
- [x] Low-tier effect shedding documented | DOD practice: route card, architecture note, binary payload ledger, JSON report, and XML self-audit state prewarm 1024/high-tier max 1024/low-tier caps 64 flare, 32 sonar, 16 tether | Rejected: implying low-tier caps shrink deployment truth or Vault capacity | Estimate: prevents false capacity-review loop.
- [x] Static signal-cap gate rerun | DOD practice: JSON, XML, and `Directory.Build.targets` parse; `rg` confirms named `SignalBus<Auxiliary*>.Configure` arguments and low-tier constants; targeted residue and sibling-import scans return clean | Rejected: accepting earlier noisy whole-repo `rg` output as target proof | Estimate: static gate only.
- [ ] Unity compile/profiler gate | CPU guard sampled `100` with no `dotnet`/`csc`/`MSBuild` processes. Rebuild remains forbidden while CPU is above 50%; exact Burst timing proof remains pending.

### Loop 29 - GPU Upload Discipline Static Proof

- [x] Auxiliary polish static audit generated | DOD practice: `python Tools/PolishMandateStaticAudit.py --source-root Assets/_Project/Scripts/Equipment/Auxiliary --fail-on-pack-one --fail-on-missing-burst-flags` wrote SHINOBU-owned JSON/MD artifacts | Rejected: whole-project audit noise as proof for this route | Estimate: static gate only.
- [x] Pack/Burst/NoAlias static gate recorded | DOD practice: owned auxiliary slice reports `packOne=0`, missing Burst flags `0/0/0`, `jobHandleComplete=0`, `linqSurface=0`, `structAutoProperties=0`, `privateNativeCollectionField=0`, `noAlias=13` | Rejected: manual grep only for Burst flag proof | Estimate: static gate only.
- [x] VFX upload route verified against bandwidth discipline | DOD practice: auxiliary VFX buffers are created with `CreateStructuredLockBuffer`; upload uses `UploadNativeArray`, which maps `LockBufferForWrite` and copies through `UnsafeMemoryCopyGuard.TryMemCpy`; unchanged frames skip upload through dirty gate | Rejected: undocumented `GraphicsBuffer` handoff that could be mistaken for `SetData` | Estimate: avoids false bandwidth-review failure; runtime behavior unchanged.
- [ ] Unity compile/profiler gate | CPU/process guard not cleared in this loop. Runtime GC/frame/Frame Debugger proof remains pending.

### Loop 30 - Scanner Status Producer And First 20 Route Impact

- [x] Subagent scanner-status defect integrated | DOD practice: accepted Goodall finding that `ScannerToolActiveSignal` was still produced through `GlobalSignals.Publish` from `LateFrameTick` | Rejected: treating a live hot bridge as harmless documentation residue | Estimate: static gate only.
- [x] Scanner active status producer migrated | DOD practice: `ScannerTool` now pushes `ScannerToolActiveSignal` directly through `SignalBus<ScannerToolActiveSignal>.Push` each registered `LateFrameTick` and no `GlobalSignals.Publish(new ScannerToolActiveSignal)` producer remains under `Assets/_Project/Scripts` | Rejected: preserving latest-signal bridge ownership in the producer | Estimate: avoids one GlobalSignals wrapper/latest write per scanner-status publish; exact us pending profiler.
- [x] Scanner broad coupling bounded | DOD practice: direct scanner `using Hecton8.AI/Building/Construction/Caves/Tools/World/Narrative` remains because `ScannerTool` still owns scanner knowledge/UI/lore/fauna/resource surfaces outside the auxiliary effect route | Rejected: blind namespace deletion without clean compile and owner split | Estimate: avoids compile-wall churn; residual owner split required.
- [x] First 20 route-impact backfill written | DOD practice: route card, architecture note, binary payload ledger, JSON report, and XML self-audit now include First 20 Minutes moment, route impact, proof required, and parked work rejected | Rejected: SHINOBU docs that only describe architecture without first-route product proof | Estimate: 1000-3000 us integrator lookup saved.
- [x] Static parser/residue gate rerun | DOD practice: JSON/XML/Directory.Build.targets parse clean; targeted `rg` reports no scanner-active GlobalSignals producer and no removed duplicate status fields in `ScannerTool`; prompt re-extraction still reports 20 tasks | Rejected: chat-only subagent integration claim | Estimate: static gate only.
- [ ] Unity compile/profiler gate | Not launched in this loop. CPU guard sampled `100` and runtime artifacts still gate source proof.

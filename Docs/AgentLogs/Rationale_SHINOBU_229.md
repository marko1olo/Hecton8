# SHINOBU_229 Rationale

Status: PENDING GUARDED COMPILE / PROFILER PROOF - CPU GUARD CURRENTLY BLOCKS REBUILD

## Decision 0 - Work Boundary

Problem: Auxiliary equipment routing touches tools, physics, lighting, sonar, VFX, telemetry, and editor diagnostics. Direct concrete dependencies would collide with parallel agents and violate Global Authority boundaries.
Solution: Own only the data-oriented router surface: aligned DTOs, deterministic lifecycle jobs, typed unmanaged payloads, telemetry, static scanner, and editor x-ray. Use hash-based signal payloads and no direct class references to downstream lighting/physics/sonar owners.
Rejected Alternatives: Direct calls into lighting, physics, sonar, or player equipment systems; those would introduce dependency walls on agents 143/144/151 and create new global authority without owner review.
Scalability potential: Low uses bounded arrays, cadence throttling, and visual fake signals; Middle keeps normal cadence; High increases signal density and telemetry; Ultra spends saved CPU on downstream VISUAL_SYNC overkill without bloating simulation truth.
Hardware Impact: Low-end i3/MX350 avoids GameObject/Light/Joint churn and per-frame managed dispatch. Estimated saved work versus 500 component updates: 3000-9000 us CPU and 0 B GC target, pending Unity profiler proof.

## Decision 1 - Mandate Set

Problem: The task demands NativeArray lifecycle, SignalBus broadcasts, AUP precision, ARM64 layout, and tether routing in one pass.
Solution: Read and apply these mandates before coding: Zero-GC, Native Memory/Jobs, ARM64 Layout, AUP Determinism, Signal Lane Segregation, Execution Phases, Tool Equipment Routing, Tether Constraints.
Rejected Alternatives: Reading generic project rules only; this misses exact field layout, phase, and physics ownership laws.
Scalability potential: Mandates enforce continuous `GlobalQualityWeight` cadence instead of low/ultra switches, with separate Low/Middle/High/Ultra behavior in rationale and code.
Hardware Impact: Mandate-driven linear NativeArray and typed signal routing target L1-friendly sequential reads and no managed allocation spikes on MX350/i3-class silicon.

## Decision 2 - Auxiliary Facade Purge

Problem: `DeployableFlare`, `GravTrap`, and `GravityTetherTool` owned Light, ParticleSystem, Rigidbody, Collider buffers, and PhysX force loops, so auxiliary lifetime remained scattered across MonoBehaviours.
Solution: Reduce these scripts to compatibility facades that only route deploy/cancel intents into `AuxiliaryEquipmentRouterRuntime`; all countdown, signal emission, VFX staging, and telemetry occur in NativeArray-backed Burst jobs.
Rejected Alternatives: Keeping ITickable/ISlowTickable facades or `Physics.OverlapSphereNonAlloc` with cached Collider arrays; still fragments update ownership and blocks one-buffer telemetry.
Scalability potential: Low routes sparse packets into 15 Hz cadence; Middle holds authored response; High raises cadence and VFX matrices; Ultra lets downstream light/sonar/tether solvers spend saved C# time on visual density.
Hardware Impact: Removes three managed update surfaces and 32-collider per-tool broadphase buffers from hot use. Estimated i3/MX350 saving for 50 active auxiliaries: 400-1600 us/frame versus component ticks and broadphase loops, pending profiler.

## Decision 3 - Dedicated ActiveEquipmentDTO Mirror

Problem: `BufferID.ShinobuActiveEquipmentState` is owned by `ModularEquipmentEngine` and is sized for tracked player tools, not 1024 deployed auxiliary items.
Solution: Add `BufferID.ShinobuAuxiliaryActiveEquipmentState` and mirror auxiliary truth into a separate `AuxiliaryActiveEquipmentDTO` NativeArray using the same 32-byte ABI.
Rejected Alternatives: Reusing the modular equipment buffer; that would either fail capacity checks or overwrite another agent's tool state.
Scalability potential: Low/Middle/High/Ultra all read the same contiguous DTO shape, with capacity pressure isolated to auxiliary routing.
Hardware Impact: Avoids lock contention and false sharing with modular equipment. Estimated gain: prevents multi-system stall and preserves sequential writes for 1024 auxiliary mirrors.

## Decision 4 - Explicit DTO And Signal Layout

Problem: Auxiliary payloads need AUP precision and ARM64-safe blind snapshots.
Solution: Use `[StructLayout(LayoutKind.Explicit)]`: `DeployedAuxiliaryDTO` is 64 bytes with `double3` at offset 0, prefab hash at 24, lifetime at 28; state is 16 bytes; route signals are 64 bytes and carry `double3` AUP payloads.
Rejected Alternatives: Auto-layout structs, C# properties, or Vector3 world positions; all create ABI drift, CS1612 copies, or AUP jitter at map edge.
Scalability potential: Low can memcpy fewer records; Ultra can blast-copy 1024 records and route dense VFX without schema conversion.
Hardware Impact: 64-byte deployment records and 16-byte state records stay cache-line predictable on ARM64 and x86. Estimated avoided penalty: 50-300 us/frame under 500-item stress compared with misaligned mixed fields.

## Decision 5 - SignalBus Dear-Lie Routing

Problem: Flares, pings, and gravity tethers need effects without owning light objects, sphere colliders, or Unity joints.
Solution: `UpdateDeployedAuxiliaryJob` emits `AuxiliaryFlareLightSignal`, `AuxiliarySonarRequestSignal`, and `AuxiliaryTetherConnectionSignal` through `NativeQueue<T>.ParallelWriter`; downstream systems own visual and physics reality.
Rejected Alternatives: Direct writes into lighting, sonar, or tether solver buffers; those would create concrete dependencies on agents 143/144/151.
Scalability potential: Low emits at 15 Hz with lower route density; Middle/High/Ultra increase cadence continuously through `GlobalQualityWeight` and leave visual overkill downstream.
Hardware Impact: Replaces per-object component work with queue writes and deterministic scalar math. Estimated i3/MX350 saving under 500 mock deployments: 2500-7000 us/frame, pending profiler.

## Decision 6 - Verification Boundary

Problem: `TetherManager.cs` still contains one cold `new GameObject("TetherInstance")` in a pool prewarm path owned by tether/cable physics, while this agent owns auxiliary routing and tool facades.
Solution: Do not edit `TetherManager`; document it as cross-domain residue. The auxiliary scanner reports remaining cross-domain findings instead of hiding ownership conflict.
Rejected Alternatives: Mutating tether-manager pooling from this agent; that would violate the domain boundary and risk breaking SHINOBU_132/143 work.
Scalability potential: Auxiliary routing still scales Low/Middle/High/Ultra independently; tether-manager cold pooling requires owner migration to reach total project zero.
Hardware Impact: No hot-path auxiliary cost remains from that cold allocation. Runtime gain for router path remains intact; full tether-manager removal belongs to tether physics owner.

## Decision 7 - Uninitialized Memory Active Bound

Problem: Large auxiliary arrays use `NativeArrayOptions.UninitializedMemory`; scheduling jobs across full capacity before slots are written would read random bytes as fake deployments.
Solution: Treat `ShinobuAuxiliaryActiveCount` as the initialized bound. Deploy scans only initialized slots plus the next append slot; update, VFX, telemetry, and cancel jobs return before reading deployment records above that bound.
Rejected Alternatives: Clearing all 1024 records on boot or using `ClearMemory`; that violates zero-init bypass and burns memory bandwidth.
Scalability potential: Low boots with zero active records and no array read; Middle/High/Ultra pay only for initialized active bounds while still preserving 1024 capacity.
Hardware Impact: Avoids reading 64 KB of garbage deployment data per frame when no auxiliaries exist. Estimated i3/MX350 saving at idle: 40-120 us/frame plus fault avoidance.

## Decision 8 - Bound Is Not Live Count

Problem: A parallel lifecycle job can create holes when individual deployments expire. If telemetry overwrites the initialized bound with live count, a live tail record after a hole can be dropped from future updates.
Solution: `ShinobuAuxiliaryActiveCount` now acts as initialized bound. Telemetry records live active count in the ring but does not mutate the bound. Main thread compacts only trailing dead slots after the job fence completes.
Rejected Alternatives: Swap-and-pop inside `IJobParallelFor`; unsafe without atomics and introduces alias contention.
Scalability potential: Low avoids any scan above bound; Middle/High/Ultra tolerate holes until tail compaction and reuse holes on the next deploy.
Hardware Impact: Preserves correctness without a serial compaction pass over all 1024 slots. Estimated saved cost versus full compaction: 20-90 us/frame at 500 slots.

## Decision 9 - Bootstrap Hook

Problem: Static flare/tether facade calls fail silently if no `AuxiliaryEquipmentRouterRuntime` exists in the scene.
Solution: Add one cold bootstrap hook in `GameBootstrapper` during equipment interaction dependency registration. This creates the router runtime once and lets it register with dispatcher/Vault.
Rejected Alternatives: Creating a GameObject inside `TryDeploy*` on first tool use; that would move allocation into gameplay activation and violate hot-path zero-GC.
Scalability potential: Low/Middle/High/Ultra all share one router instance and one set of preallocated buffers.
Hardware Impact: One cold GameObject/AddComponent allocation at bootstrap; estimated hot-path saving is correctness rather than frame time, because deploy calls no longer fail or allocate at use time.

## Decision 10 - Scanner Pulse Routed Through Auxiliary Data

Problem: `ScannerTool` still owned a local pulse state machine (`PulseActive`, `PulseOriginAup`, `PulseStartTime`) and spawned `ScannerPulseDrawer`, which registered as a MonoBehaviour/ITickable and rendered a material/matrix pulse outside the auxiliary router.
Solution: Delete `ScannerPulseDrawer`, remove scanner pulse state/properties/shader fields, and route primary scan pulses through `AuxiliaryEquipmentRouterRuntime.TryDeploySensorPing(scanPosition, pulseDuration, effectiveScanRadius)`. The auxiliary Burst job stores scanner-authored max radius in `AuxiliaryStateDTO.Scalar0` and emits `AuxiliarySonarRequestSignal`.
Rejected Alternatives: Keeping the drawer as a "cold" AddComponent or only disabling it at runtime; that leaves a second owner of radar pulse lifetime and violates one fact -> one owner.
Scalability potential: Low collapses ping expansion toward lifetime-rate math at lower cadence; Middle keeps authored scanner radius; High/Ultra can let sonar/VFX consumers spend signal density on visual overkill without scanner-local render loops.
Hardware Impact: Low-end i3/MX350 avoids one per-pulse ITickable path, material mutation, matrix array writes, and draw submission from the scanner. Estimated saved work: 80-250 us during active pulse frames plus one cold material allocation.

## Decision 11 - Unity Meta Instead Of Generated Csproj Edits

Problem: `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed because ignored Unity-generated `Hecton8.Core.csproj` had not imported newly created auxiliary files, so facades saw `using Hecton8.Equipment.Auxiliary` before the namespace existed in that stale project model.
Solution: Add stable `.meta` files for `Equipment`, `Equipment/Auxiliary`, `Equipment/Auxiliary/Editor`, and each new C# file so Unity AssetDatabase/project regeneration can import the router namespace. Leave generated `.csproj` untouched as source.
Rejected Alternatives: Committing edits to ignored generated project files or moving the router into unrelated existing folders just to satisfy a stale local project file.
Scalability potential: No runtime scalability change; it preserves compile-wall hygiene by keeping the auxiliary domain in its folder under the root core asmdef instead of inventing a circular asmdef.
Hardware Impact: No runtime cost. Build verification remains blocked by sibling-agent missing types after this correction; no second heavy build loop was launched.

## Decision 12 - Accessor Purity And Route Card

Problem: `TryRead*` APIs and hot runtime paths still shared `TryResolveViews`, which can call `GetGenerationHandle` and grow/acquire Vault buffers. The global authority checklist rejects hidden allocation/growth inside read-looking APIs and rejects new global routes without a route card.
Solution: Split resolver paths. `TryResolveViews` remains the cold acquisition path for bootstrap/explicit initialization/mock generation. `TryResolveExistingViews` is used by reads, tuning writes, Tick, deploy/cancel, and telemetry finalization. Removed routine `GlobalDataVault.TryGetLatestCreated` fallback from the auxiliary router and added `SHINOBU_229_AUXILIARY_EQUIPMENT_ROUTE_CARD.md` with `YELLOW` disposition.
Rejected Alternatives: Per-frame `GlobalRegistry.DataVault` retry, `TryGetLatestCreated` fallback, or accepting hidden allocation in editor polling because it is "diagnostic." Those violate the authority boundary and hide failure.
Scalability potential: Low/Middle/High/Ultra all now use the same pre-acquired Vault handles; quality scaling remains continuous, and a new `AuxiliaryTuningFlags.OverrideGlobalQualityWeight` permits an explicit 0.0 survival override while default routing follows live global quality.
Hardware Impact: Removes hidden handle acquisition from hot/read paths and preserves fail-closed behavior under DataVault relocation or missing boot. Estimated i3/MX350 saving is small per frame (5-40 us) but prevents worst-case allocation/growth spikes and compile-wall review rejection.

## Decision 13 - Job Safety Metadata And Radar Boundary

Problem: Static review found `GenerateMockAuxiliaryDeploymentsJob.ActiveCount` was marked `[ReadOnly]` while index 0 writes the initialized bound. The same pass found the auxiliary `ActiveEquipmentDTO` mirror handle was reset without `ReleaseBuffer`. A broad radar/sonar scan also produced thousands of downstream hits outside auxiliary ownership.
Solution: Remove `[ReadOnly]` from the mock job `ActiveCount` field, release `_activeEquipmentHandle` through `ReleaseHandle`, and document broad radar hits as downstream/cross-domain unless they own deployment lifetime. `ScannerTool` active ping lifetime remains the only radar-like path in SHINOBU_229 ownership, and it already routes through `TryDeploySensorPing`.
Rejected Alternatives: Ignoring the metadata mismatch because it is mock-only; dropping the handle without refcount release; editing `SpectrumSystem`, audio sonar, cockpit radar, or AI sensory systems from this domain. Those alternatives either break Unity job safety, leak Vault ownership, or violate compile-wall boundaries.
Scalability potential: Low/Middle/High/Ultra behavior does not change. The fix preserves the same continuous cadence and signal lanes while keeping boot/mock stress paths valid for every tier.
Hardware Impact: No direct per-frame gain. It prevents job safety rejection in mock stress runs and prevents persistent Vault handle leaks across scene reloads, which protects long endurance sessions on low-end memory budgets.

## Decision 14 - Read-Only Deployment Snapshot Seal

Problem: `TryReadDeployments` was pure in the narrow allocation sense but returned a mutable `NativeArray<DeployedAuxiliaryDTO>` alias to Vault-backed truth. Editor and gizmo consumers could accidentally mutate deployment state outside router locks and outside the lifecycle job.
Solution: Change `TryReadDeployments` to return `NativeArray<DeployedAuxiliaryDTO>.ReadOnly` via `AsReadOnly()`. Existing editor consumers only index the alias for histogram/gizmo rendering, so no managed copy or new buffer is required.
Rejected Alternatives: Keeping the mutable alias because current consumers are editor-only, or copying deployments into a managed diagnostic array. The first violates global read-accessor purity; the second creates avoidable allocation and stale snapshot ambiguity.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The same Vault buffer remains authoritative; external diagnostics cannot create tier-dependent shadow state.
Hardware Impact: No direct frame-time gain. It removes a mutation hazard that could corrupt long-running mock/profiler sessions and force expensive forensic rebuilds.

## Decision 15 - Producer-Side Signal NaN Vaccination

Problem: `UpdateDeployedAuxiliaryJob` writes directly through `SignalBus<T>.ParallelWriter`. This bypasses the managed `SignalBus.TryPush` finite sanitizer. If editor tuning writes NaN or a gravity tether anchor is non-finite, the router can poison downstream light, sonar, or tether lanes.
Solution: Add explicit scalar sanitizers in `AuxiliaryEquipmentMath`, sanitize fallback constants/inputs, sanitize flare intensity/range, signal scale, ping radius/rate, VFX scale, and default scalar resolution before enqueue, reject non-finite gravity tether AUP inputs, and drop non-finite tether anchor packets with `NonFiniteRecovered` telemetry.
Rejected Alternatives: Trusting editor sliders and downstream consumers, or switching job producers to managed `TryPush`. The first breaks NaN fatalism; the second cannot run from Burst jobs and would reintroduce managed hot dispatch.
Scalability potential: All quality tiers use the same finite payload contract. Low-tier cadence still sheds ALU; High/Ultra still receive richer signals without changing gameplay truth.
Hardware Impact: Adds a few branchless finite selects per routed signal. Cost is below measurement noise versus the avoided worst case: NaN propagation into physics/sonar/light consumers and blackbox-corrupting state.

## Decision 16 - ActiveCount Vault Lock Fence

Problem: `ShinobuAuxiliaryActiveCount` is a Vault-backed NativeArray used by scheduled jobs, but the runtime lock fence covered deployments, states, counters, VFX matrices, and active equipment only. A Vault relocation or release attempt could treat the initialized-bound buffer differently from the arrays that depend on it.
Solution: Add `ShinobuAuxiliaryActiveCount` to `TryLockRuntimeBuffers` and `UnlockRuntimeBuffers`, matching the job graph that reads/writes the bound. The buffer remains a one-int owner-local fact, but it is fenced with the rest of the scheduled runtime set.
Rejected Alternatives: Assuming a scalar NativeArray is safe without a lock, or widening the lock to tuning/telemetry/profiles. Tuning is copied into job values before scheduling, telemetry is written after the pending fence, and profiles/CSV scratch are cold boot data.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The fix preserves stable buffer ownership under every cadence tier without changing DTO shape or quality math.
Hardware Impact: Direct frame-time saving is 0 us. It removes a stale-handle/relocation hazard that could corrupt the job graph during long endurance sessions or scene reloads.

## Decision 17 - Per-Deployment Tether Anchors

Problem: Gravity tether routing used one `_lastTetherAnchorAup` runtime field. Multiple active tether deployments would all route against the latest anchor, corrupting concurrent constraints.
Solution: Add `AuxiliaryTetherAnchorDTO[1024]` as a 32-byte explicit-layout Vault buffer. Each gravity tether slot stores its own anchor AUP and flags. The lifecycle job reads the per-slot anchor and clears it with the deployment on expiry/cancel.
Rejected Alternatives: Expanding `DeployedAuxiliaryDTO` beyond 64 bytes or keeping a global anchor field. Expanding the primary DTO would disrupt rollback/memcpy ABI; a global field violates one fact -> one owner.
Scalability potential: Low/Middle/High/Ultra use the same anchor buffer and cadence curve. More concurrent tethers do not change route semantics or require Unity joints.
Hardware Impact: Adds one 32-byte sequential read per active tether route. Prevents physics corruption; direct frame-time saving is 0 us.

## Decision 18 - Facade Readback And Cancel Discipline

Problem: `DeployableFlare` and `GravTrap` compatibility state could remain active after router expiry or disable/despawn, leaving stale central records or stale facade booleans.
Solution: Facades cancel routed records on disable and derive public compatibility state from a pure router readback scan that fails closed during active jobs. `GravTrap` now deploys a shell sample at `pullRadius` to the center anchor so rest length is non-zero.
Rejected Alternatives: Reintroducing Update loops to synchronize facade state, or treating local booleans as gameplay truth. Both rebuild the OOP lifecycle that the router removed.
Scalability potential: Low/Middle/High/Ultra lifecycle truth remains in Vault. Facade reads are compatibility-only and do not affect cadence, DTO layout, or signal ownership.
Hardware Impact: No hot allocation. Avoids stale deployments and zero-length tether packets that would waste downstream solver work.

## Decision 19 - CSV Profiles Actually Drive Tuning

Problem: The CSV parser existed but had no caller; `Profiles` and `CsvScratch` were allocated then ignored, so Task 17 was only a parser artifact.
Solution: Cold boot now reads `Assets/StreamingAssets/auxiliary_equipment_profiles.csv` into Vault scratch memory, parses via `ReadOnlySpan<byte>`, writes `AuxiliaryProfileDTO[]`, and applies the parsed values to `AuxiliaryTuningDTO`. Missing-file CI gets deterministic fallback unmanaged profiles.
Rejected Alternatives: `File.ReadAllBytes`, `string.Split`, `float.Parse`, or leaving profiles unused. Managed string/array parsing undermines the tuning bridge and dead parser claims are false proof.
Scalability potential: Designers can tune Low/Middle/High/Ultra behavior through lifetime/radius/intensity/rate values without recompiling. GlobalQualityWeight still controls continuous cadence and presentation density.
Hardware Impact: Cold boot only. Hot path remains 0 B GC target; parser writes contiguous unmanaged buffers.

## Decision 20 - VFX GPU Route And Telemetry Honesty

Problem: Task 12 was overclaimed: the job wrote `AuxiliaryVfxMatrixDTO[]` but no GPU buffer route existed. Task 15 was also overclaimed: direct `.Execute()` telemetry was not an exact Burst timing measurement.
Solution: Add a persistent cold `GraphicsBuffer` for `AuxiliaryVfxMatrixDTO` and upload the Vault matrix array after the pending job fence. Expose `TryReadVfxGraphicsBuffer` for presentation owners. Rename telemetry to `RecordAuxiliaryTelemetryPass` and document `CpuMicroseconds` as schedule-to-finalize wall time pending Unity profiler proof.
Rejected Alternatives: Instantiating ParticleSystems or claiming exact Burst time without profiler evidence. The first violates the Dear Lie; the second violates reporting discipline.
Scalability potential: Low uploads fewer active matrices due cadence/count; High/Ultra can consume the same buffer for procedural/indirect VFX density without changing simulation truth.
Hardware Impact: Upload is one contiguous post-fence copy; no hierarchy traversal. Exact frame cost requires Unity profiler proof after the compile wall clears.

## Decision 21 - Lock-Before-Resolve And Producer Writer Discipline

Problem: Static polish found three residual risks: producer jobs used the legacy `SignalBus<T>.ParallelWriter` property, diagnostics could read deployment Vault aliases while a lifecycle job was writing them, and runtime write paths resolved Vault views before acquiring the relocation lock.
Solution: Switch auxiliary producer jobs to `SignalBus<T>.OpenParallelWriter()`, make `TryReadDeployments` fail closed while `_jobActive`, lock runtime buffers before re-resolving job-visible views in Tick/deploy/cancel/mock paths, and lock only `ShinobuAuxiliaryTuning` for editor tuning writes. Added finite guards for authored lifetime, radius, accumulated cadence debt, and tether rest length.
Rejected Alternatives: Keeping the legacy writer facade because it currently forwards to the same queue; returning read-only deployment aliases during active jobs; relying on pre-lock resolved NativeArray views. Those options preserve avoidable review risk around SignalBus migration and Vault relocation safety.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The cadence curve, DTO layout, and authority route remain stable while lock/re-resolve discipline protects every tier.
Hardware Impact: Direct frame-time gain is near 0 us. The value is failure avoidance: no diagnostic read/write race, no stale NativeArray alias after relocation, and no non-finite lifetime/radius packet entering downstream solvers.

## Decision 22 - Signal Delivery Accounting And Scanner Projection Route

Problem: Subagent audit found two remaining authority/accounting risks. Burst route counters were incremented immediately after queue enqueue attempts and could be misread as guaranteed delivery. `ScannerTool` still published scanner projection presentation state after routing the same ping through the auxiliary router, leaving two routes for one sensor-ping visual fact.
Solution: Keep Burst-side `AuxiliaryRouteCounterDTO` as attempted enqueue counters and add SignalBus last-flush pressure fields (`DroppedSignals`, `CorruptedSignals`, `PeakQueuedSignals`) to the 64-byte telemetry ring. Remove the `ScannerTool` direct `HectonScannerProjectionState.Publish` call, delete the now-unused `HectonScannerProjectionState.cs/.meta` static shadow-state route, and make `HectonScannerProjectionFeature` consume `SignalBus<AuxiliarySonarRequestSignal>.GetSignals()` for presentation state.
Rejected Alternatives: Treating `NativeQueue.Enqueue` attempts as delivery proof, or retaining `HectonScannerProjectionState.Publish` as a harmless visual shortcut/fallback. The first creates false blackbox telemetry; the second violates one fact -> one owner -> one route for scanner pulse presentation.
Scalability potential: Low records minimal lane pressure while lower cadence emits fewer packets; Middle/High/Ultra preserve the same signal route and can consume denser snapshots for richer presentation without adding scanner-local lifecycle state.
Hardware Impact: Direct frame-time gain is 0 us. The gain is correctness: overflow/backpressure is now visible, and scanner projection no longer duplicates route work or creates stale managed presentation state.

## Decision 23 - Scanner Projection AUP Local Downcast

Problem: After moving scanner projection to `AuxiliarySonarRequestSignal`, the presentation feature still risked absolute float subtraction by uploading the double AUP as a float origin and letting the shader subtract it from `worldPos + _TotalUniverseOffset`.
Solution: In `HectonScannerProjectionFeature`, subtract `HectonFloatingOrigin.CurrentTotalOffsetDouble` from the signal `double3` AUP before float downcast. The scanner projection shader now computes `delta = worldPos - localOrigin`, so presentation math is local-space after the double subtraction.
Rejected Alternatives: Keeping absolute float origin because the route is visual-only. That still violates the 100km jitter rule and can make sonar projection swim at large offsets.
Scalability potential: Low/Middle/High/Ultra route semantics are unchanged. The same signal snapshot feeds the shader; only coordinate conversion is safer.
Hardware Impact: One double3 subtraction per presented scanner projection. Direct CPU cost is below noise; visual stability improves at large AUP offsets.

## Decision 24 - CSV Bridge Is Not Data Monolith Readiness

Problem: The XML task mandates a cold `auxiliary_equipment_profiles.csv` parser, but global documentation now treats `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` as the runtime readiness boundary. This workspace does not contain that h8bin payload.
Solution: Keep the CSV path as a task-specific cold tuning bridge and deterministic static fallback, document the missing Data Monolith payload in route docs, report JSON, and self-audit, and avoid claiming h8bin runtime readiness.
Rejected Alternatives: Moving auxiliary tuning into a non-existent h8bin pipeline from this domain, or pretending the source CSV proves Data Monolith readiness. Both would violate owner boundaries and evidence discipline.
Scalability potential: Designers can still tune Low/Middle/High/Ultra values through CSV during cold boot; final h8bin migration can preserve the same DTO shape and quality semantics under the Data Monolith owner.
Hardware Impact: Hot path impact is 0 us. Cold CSV I/O remains outside Tick and uses Vault scratch plus span parsing; runtime monolith migration remains pending owner work.

## Decision 25 - Generated Project Compile Wall Reconfirmed

Problem: After deleting `HectonScannerProjectionState.cs`, static project scan shows `Hecton8.Core.csproj` still contains `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs` and still does not enumerate the new auxiliary router sources. A `dotnet build` against this stale generated project would fail before proving the current Unity source state.
Solution: Do not edit generated `.csproj` files as source and do not spend a build attempt on known stale metadata. Keep stable Unity `.meta` files for the deleted/new assets and require Unity project regeneration/import before the next guarded compile proof.
Rejected Alternatives: Hand-editing `Hecton8.Core.csproj`, or running another `dotnet build` immediately to produce a predictable missing-file/generated-project failure. Both waste the compile budget and can hide real code issues behind generated metadata noise.
Scalability potential: No runtime scalability change. This protects iteration velocity and keeps the auxiliary domain in source files plus Unity asset metadata.
Hardware Impact: Runtime cost 0 us. Engineering impact: avoids a known compile-wall loop until Unity regenerates source inclusion.

## Decision 26 - Subagent DOD Hardening

Problem: Subagent audit found three SHINOBU_229-owned defects: auxiliary jobs imported sibling `Hecton8.Tools` DTOs, mock generation force-completed from a Tick-callable path, and read-looking accessors hid mutation in `GravTrap`/scanner quality tier refresh.
Solution: Added local 32-byte `AuxiliaryActiveEquipmentDTO`, removed `Hecton8.Tools` and `Hecton8.World` imports from auxiliary runtime/jobs, made mock generation schedule `GenerateMockAuxiliaryDeploymentsJob -> StageAuxiliaryVFXJob` behind the existing pending fence, left forced completion only for teardown, made `GravTrap.IsActive` pure, and renamed mutating scanner quality methods from `Resolve*` to `Refresh*`.
Rejected Alternatives: Reusing sibling `ActiveEquipmentDTO`, keeping `TryComplete` because mock data is "debug only", or leaving mutating methods under read-looking names. Those alternatives hide cross-domain compile dependencies and violate the global accessor purity doctrine.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Mock stress data still fills the same 500 deterministic deployments; production cadence and SignalBus route density still scale continuously with GlobalQualityWeight.
Hardware Impact: Direct runtime gain is near 0 us. The value is avoiding same-frame completion stalls in seed/mock paths and preventing sibling assembly churn in future asmdef splits.

## Decision 27 - Scanner Scientific/Lore Residual Boundary

Problem: Subagent audit found managed string construction in `ScannerTool` discovery/lore paths and a legacy `GlobalSignals.Publish(new ScannerToolActiveSignal)` bridge. Those are real scanner-system residues, but they do not own the auxiliary radar pulse lifetime that SHINOBU_229 was assigned to route.
Solution: Keep SHINOBU_229 edits scoped to the owned radar pulse lifecycle: `ScannerTool` primary pulse emits `AuxiliarySonarRequestSignal`, `HectonScannerProjectionFeature` consumes that SignalBus snapshot, and the deleted `HectonScannerProjectionState` shadow route remains gone. Document the scientific/lore string route and ScannerToolActiveSignal bridge as out-of-domain residuals for the scanner knowledge/UI owner.
Rejected Alternatives: Rewriting scanner localization/lore discovery from this auxiliary pass. That would touch unrelated localization, item, construction, lore, PDA, and data archaeology contracts and risk a compile wall unrelated to flares/gravity tethers/radar pulse routing.
Scalability potential: The owned pulse visual scales through auxiliary cadence and sonar signal density. Scanner knowledge UI needs its own later zero-GC pass without changing the auxiliary DTO or signal route.
Hardware Impact: No direct runtime gain in this pass. Avoids high-risk churn while preserving the already-routed radar pulse savings: no scanner-local pulse drawer, material mutation, or draw submission.

## Decision 28 - Final Static Route Gate Without Stale Build Loop

Problem: The route needed another post-hardening gate, but `Hecton8.Core.csproj` still references deleted `Assets\_Project\Scripts\Gameplay\HectonScannerProjectionState.cs` and omits new auxiliary sources until Unity regenerates generated metadata. Launching another `dotnet build` would re-measure the generated-project failure instead of source correctness.
Solution: Run targeted static gates instead: prompt re-extract, forbidden runtime pattern scan, sibling import scan, teardown-only completion scan, XML/JSON parse, orphan meta scan, process check for dotnet/csc/MSBuild, and generated csproj stale-reference confirmation. Record compile/profiler proof as blocked until Unity import/project regeneration.
Rejected Alternatives: Hand-editing generated `.csproj` files or burning another build attempt against a known stale source list. Both hide real source defects behind generated metadata noise and violate command discipline.
Scalability potential: No runtime behavior changed. The gate preserves the same Low/Middle/High/Ultra continuous route while protecting iteration time for parallel agents.
Hardware Impact: Runtime gain 0 us. Engineering impact: prevents a predictable compile-wall loop and keeps verification evidence tied to files that actually own the auxiliary route.

## Decision 29 - Double-Buffered VFX Upload And Dirty Gate

Problem: The VFX staging route wrote `AuxiliaryVfxMatrixDTO[]` in Vault but the GPU handoff used one persistent `GraphicsBuffer` and uploaded every post-fence frame. That can overwrite a buffer a downstream VISUAL_SYNC consumer still reads and burns CPU-to-GPU bandwidth when the deployment snapshot is unchanged.
Solution: Replace the single buffer with A/B structured `GraphicsBuffer` pages plus an immutable read-buffer pointer. Post-fence upload hashes active deployment slots and compares active count, snapshot hash, camera AUP, and quality weight. Unchanged frames skip `UploadNativeArray`; changed frames write the inactive page and flip it into the read pointer.
Rejected Alternatives: Keep one buffer because the upload happens after the job fence, or upload every frame because matrices are contiguous. The first ignores CPU/GPU frame overlap; the second wastes bandwidth on static flare/tether/radar frames and violates the visual-sync double-buffer mandate used elsewhere in HECTON-8.
Scalability potential: Low/thermal frames shed both cadence and upload bandwidth when auxiliary state is stable. Middle keeps normal cadence with unchanged-frame skips. High/Ultra can feed dense presentation buffers without adding simulation truth or forcing consumers to read a buffer being written.
Hardware Impact: On i3/MX350-class devices this avoids repeated `64B * activeCount` matrix payload uploads on static frames and reduces driver synchronization risk. Exact microseconds require Unity profiler/Frame Debugger proof after the generated-project compile wall clears.

## Decision 30 - Signal Queue Prewarm And Scanner Audio Signal Route

Problem: Subagent audit found that auxiliary SignalBus lanes prewarmed only 256 flare, 256 sonar, and 128 tether entries while the Burst lifecycle job can enqueue one route per active deployment slot. At the 1024 deployment ceiling, the NativeQueue could grow in the hot route. The same audit found the active scanner pulse still called `IAudioService.PlayAtPoint` directly.
Solution: Configure all three auxiliary lanes with expected capacity 1024 and max frame signals 1024, matching the one-signal-per-active-slot producer ceiling. Replace scanner pulse direct audio playback with `SignalBus<AcousticPingSignal>.TryPush`, carrying AUP, radius, intensity, source hash, active-sonar channel, and active-sonar flag. Keep `ScanEvents.RaiseScanTriggered` documented as scanner-log/progression legacy routing, outside auxiliary light/physics/VFX effect ownership.
Rejected Alternatives: Relying on `maxFrameSignals` to imply prewarm capacity; SignalBus code proves prewarm uses `expectedCapacity` only. Keeping direct audio because the service was cached; it still couples scanner pulse activation to the audio domain. Rewriting all ScanEvents consumers from this pass; that would cross into scan log/progression ownership and broaden the compile wall.
Scalability potential: Low/Middle/High/Ultra use the same 1024-slot lane ceiling while `GlobalQualityWeight` still controls cadence and low-tier frame limits. Scanner active audio becomes an acoustic signal that audio/AI/VFX consumers can interpret continuously without changing scan truth.
Hardware Impact: Prevents hot native queue growth under 1024 same-frame route pressure. Removes one direct audio service invocation from the scanner pulse activation path. Exact microseconds require Unity profiler proof; expected gain is allocation/stall avoidance rather than steady-state ALU.

## Decision 31 - Post-Subagent Static Gate Discipline

Problem: After the capacity/audio patches, the workspace needed objective proof without burning another build attempt into known stale Unity-generated project metadata.
Solution: Run targeted static gates for scanner direct-audio residue, auxiliary SignalBus capacity, VFX double-buffer/dirty-gate ownership, XML/JSON parse, orphan meta files, process state, and generated `.csproj` staleness. Keep rebuild blocked until Unity regenerates project files and removes the stale deleted-script entry.
Rejected Alternatives: Launching `dotnet build` immediately, hand-editing generated `.csproj`, or treating the previous rationale text as proof. Those would waste the compile budget and mix source correctness with stale generated metadata failure.
Scalability potential: No gameplay route changes. Low/Middle/High/Ultra behavior remains the 1024-slot SignalBus route plus continuous cadence; VFX bandwidth still sheds on unchanged frames.
Hardware Impact: Runtime gain 0 us. Engineering impact is avoiding a known compile-wall loop and preserving clean evidence for the next Unity import/profiler pass.

## Decision 32 - Facade Shadow-State And Audio Asset Gate Purge

Problem: The scanner active acoustic route no longer called `IAudioService`, but the signal still depended on a serialized `AudioClip pingClip` reference. `DeployableFlare` also mirrored remaining lifetime in `_fuelTimer`, and `GravTrap` mirrored central active state in `_activationIssued`.
Solution: Remove `pingClip` and `cooldownClip` from `ScannerTool`, publish `AcousticPingSignal` directly from scan route data, remove `_fuelTimer` from `DeployableFlare`, and remove `_activationIssued` from `GravTrap`. Compatibility properties now read the router/Vault state instead of local lifecycle mirrors.
Rejected Alternatives: Keeping `AudioClip` as an enable flag, keeping local facade booleans as "harmless UI state", or adding a new managed settings object. Those options preserve Unity-object effect gating or duplicate lifecycle truth outside the auxiliary NativeArray.
Scalability potential: Low/Middle/High/Ultra behavior remains the same SignalBus route and continuous cadence. Audio/presentation consumers can shed work by intensity/quality without changing whether the scanner route publishes the effect fact.
Hardware Impact: Removes one Unity-object branch from scan activation and removes two stale local lifecycle mirrors. Direct frame-time gain is below profiler resolution; correctness gain is no duplicated effect/lifetime authority.

## Decision 33 - Generated Project Shield Without Editing Generated Csproj

Problem: `Hecton8.Core.csproj` still directly lists the deleted `HectonScannerProjectionState.cs` file and can omit new SHINOBU_229 source files until Unity regenerates project metadata. That blocks guarded compile from reaching real source diagnostics.
Solution: Add a minimal `Directory.Build.targets` shield: remove the deleted scanner projection state from `Hecton8.Core` compile items and conditionally include the SHINOBU_229 runtime/editor source files when generated metadata has not caught up. This follows the existing project pattern for generated project gaps and leaves `.csproj` files untouched.
Rejected Alternatives: Editing `Hecton8.Core.csproj`, relaunching `dotnet build` against known stale generated metadata, or relocating source files to satisfy stale metadata. Those alternatives create churn or measure the wrong failure.
Scalability potential: No runtime behavior changes. The shield only preserves compile-wall discipline so Low/Middle/High/Ultra route code can be verified once sibling dependencies are ready.
Hardware Impact: Runtime gain 0 us. Engineering impact is removing one predictable stale-file compile failure and one missing-namespace generated-project failure from the next guarded compile attempt.

## Decision 34 - Compile-Hazard Audit And Flare Readback Regression

Problem: After the scanner audio/facade purge and generated-project shield, the touched files needed a second static API pass. Local review also found `DeployableFlare.ResolveState()` could stay `Burning` after the central router record disappeared.
Solution: Integrated the subagent static audit: `AcousticPingSignal`, `SignalBus<T>.OpenParallelWriter()`, `GraphicsBufferUploadUtility`, DTO field names, and scanner projection SignalBus consumption all line up. Fixed `DeployableFlare.ResolveState()` so a missing router record transitions a previously burning facade to `Extinguished`.
Rejected Alternatives: Waiting for compile to catch a local logic regression, or keeping `_fuelTimer`/local active mirrors to paper over missing central records. Those alternatives reintroduce shadow lifecycle state or leave compatibility UI stale.
Scalability potential: No route scaling changes. The facade now reflects central lifecycle truth across all quality tiers; the SignalBus/Vault route remains the only hot fact path.
Hardware Impact: Runtime gain is 0 us. Correctness gain is removal of stale facade state without adding allocations or managed ticking.

## Decision 35 - Telemetry Vault Fence Closure

Problem: `AuxiliaryTelemetryEntry[300]` and `TelemetryCursor[1]` are Vault-backed proof artifacts written during post-fence finalization, but the runtime lock fence covered only deployment/state/counter/VFX/active-equipment buffers.
Solution: Add `ShinobuAuxiliaryTelemetryRing` and `ShinobuAuxiliaryTelemetryCursor` to `TryLockRuntimeBuffers` and `UnlockRuntimeBuffers`, so the owner holds the same Vault relocation lock while recording telemetry and evaluating dump triggers.
Rejected Alternatives: Leaving telemetry out because writes happen after `_pendingHandle` finalization, or copying telemetry into a private NativeArray first. The first still permits an owner-lock gap around Vault-backed proof writes; the second violates the Vault law and adds another persistent owner.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Telemetry remains optional proof data and does not affect gameplay truth, DTO layout, save identity, or quality cadence.
Hardware Impact: Direct frame-time gain is 0 us. The impact is memory safety: no stale telemetry alias during diagnostics or endurance runs, and no extra persistent allocation.

## Decision 36 - Flare Facade State Purge

Problem: After `_fuelTimer` removal, `DeployableFlare` still retained `_state` as a local inactive/burning/extinguished mirror even though the router is the only valid lifecycle owner.
Solution: Remove `_state`. `State`, `RemainingFuel`, and `IsBurning` now derive from `AuxiliaryEquipmentRouterRuntime.TryReadNearestRemainingLifetime`; `Deploy`, `ForceExtinguish`, `ResetFlare`, and `OnDisable` only publish deploy/cancel intent.
Rejected Alternatives: Keeping `_state` as a compatibility hint or adding a new facade status DTO. Both preserve a second lifecycle fact outside the auxiliary NativeArray.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The facade no longer changes truth per tier; all tiers read the same router/Vault state.
Hardware Impact: Runtime gain is below profiler resolution. Correctness gain is complete removal of local flare lifecycle state without adding allocations, polling loops, or managed events.

## Decision 37 - Scanner Projection Signal-Derived Age

Problem: `HectonScannerProjectionFeature` still used `Time.time` to derive presentation age for a scanner projection, even though the radar pulse lifetime is now owned by `AuxiliaryEquipmentRouterRuntime` and published as `AuxiliarySonarRequestSignal`.
Solution: Remove the wall-clock fields from `ProjectionRuntimeState`. The render feature now computes `Age01` directly from `CurrentRadius / MaxRadius` in the latest `AuxiliarySonarRequestSignal` snapshot, after double-precision AUP localization.
Rejected Alternatives: Keeping `Time.time` because the feature is visual-only, or adding a second managed projection state. The first preserves a wall-clock dependency in a signal-owned route; the second recreates the deleted `HectonScannerProjectionState` shadow fact.
Scalability potential: Low uses the same sparse SignalBus cadence and the shader receives coarse but consistent age. Middle/High/Ultra receive denser signal snapshots and smoother projection age without changing gameplay truth ownership or adding a second route.
Hardware Impact: Direct frame-time gain is 0 us. Correctness gain is route hygiene: radar presentation now derives its phase from the same NativeArray/SignalBus payload as scanner pulse existence, and static scan shows no `Time.` residue in the projection feature.

## Decision 38 - Projection Route Documentation Sync

Problem: The source code and machine audit already described signal-derived scanner projection age, but the route card and architecture note still only documented AUP localization. That leaves an integration ambiguity: a reviewer could think wall-clock presentation phase remains allowed.
Solution: Update `SHINOBU_229_AUXILIARY_EQUIPMENT_ROUTE_CARD.md`, `AUXILIARY_EQUIPMENT_ROUTER_SHINOBU_229.md`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` to state that projection age is derived from `AuxiliarySonarRequestSignal.CurrentRadius / MaxRadius` and that `Time.time`, `StartTime`, `Duration`, and the managed projection-state mirror are absent from the owned route.
Rejected Alternatives: Treating source scans as sufficient and leaving the ledger stale. That would weaken the one fact -> one route proof and force the integrator to rediscover the change from C#.
Scalability potential: Low/Middle/High/Ultra behavior does not change. The documentation now matches the continuous signal cadence: sparse low-tier signals produce coarser age, while high-tier cadence gives smoother projection without a second clock.
Hardware Impact: Runtime gain is 0 us. Engineering impact is preventing a stale-document review loop and keeping the binary payload ledger aligned with the SignalBus route.

## Decision 39 - Active Pulse Debug Allocation Purge

Problem: `ScannerTool.LogScanPulse` built an interpolated `Debug.Log` string in editor/development builds from the active scan pulse path. The call is stripped in non-development builds, but it still weakens zero-GC proof and creates editor profiling noise exactly where the sensor ping route is being validated.
Solution: Leave the conditional method present for call-site stability, but make it intentionally blank. The active pulse path now routes sensor ping and acoustic payloads without constructing a dynamic debug string.
Rejected Alternatives: Keeping the log because it is editor/development only, or replacing it with formatted diagnostic text. Both keep managed string work in the active pulse path and contaminate profiler captures.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Cleaner editor/development profiling makes the continuous cadence and SignalBus route easier to measure without diagnostic allocation noise.
Hardware Impact: Runtime player gain is 0 us in stripped builds. In editor/development, it prevents one dynamic string allocation and formatting cost per logged scan pulse.

## Decision 40 - SignalBus Lane Cap Contract Clarification

Problem: The auxiliary SignalBus lanes already used 1024 for expected capacity and max-frame signals, but the positional `Configure(maxAuxiliarySignalsPerFrame, maxAuxiliarySignalsPerFrame, 64/32/16, laneHash)` calls made the prewarm/max/low-tier roles easy to misread during review. That ambiguity can produce a false claim that low-tier caps reduce Vault truth capacity or that prewarm is only 64/32/16.
Solution: Replace positional calls with named arguments and explicit low-tier constants: `expectedCapacity: 1024`, `maxFrameSignals: 1024`, `lowTierFrameSignals: 64/32/16`. Update route card, architecture note, binary payload ledger, JSON report, and XML self-audit to state that low-tier caps shed optional visual/effect flush bandwidth only; deployment truth remains in Vault and SignalBus pressure is recorded in telemetry.
Rejected Alternatives: Leaving the code as-is because it was technically correct, or raising low-tier caps to 1024. The first preserves review ambiguity; the second removes a useful thermal-shedding lane and contradicts continuous `GlobalQualityWeight` behavior.
Scalability potential: Low keeps 1024 deployment truth and prewarmed lane storage but drains fewer optional visual/effect packets per frame. Middle increases delivered effect density through the existing quality curve. High/Ultra can flush up to 1024 lane packets without queue growth. No tier changes DTO layout, save identity, or authority route.
Hardware Impact: Runtime behavior is unchanged from the previous code path. Engineering gain is proof clarity; low-end i3/MX350 keeps bounded SignalBus flush work while avoiding hot NativeQueue growth under 1024 producer pressure. Exact microseconds remain profiler-pending.

## Decision 41 - GPU Upload Discipline Static Proof

Problem: The VFX handoff had a correct double-buffer/dirty-gate route, but the audit reports did not prove the exact AGENTS bandwidth mandate: `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`/guarded memcpy. A reviewer could misclassify the route as `SetData` or unbounded CPU-to-GPU upload.
Solution: Ran `PolishMandateStaticAudit.py` on the owned auxiliary slice and recorded its JSON/MD artifacts. Verified the auxiliary VFX route creates A/B buffers with `GraphicsBufferUploadUtility.CreateStructuredLockBuffer` and uploads through `GraphicsBufferUploadUtility.UploadNativeArray`, whose implementation calls `LockBufferForWrite`, `UnsafeMemoryCopyGuard.TryMemCpy`, and `UnlockBufferAfterWrite`. Updated JSON/XML reports, route card, architecture note, and binary ledger with the exact upload path and `SetData=0` claim for the auxiliary route.
Rejected Alternatives: Relying on broad text that said "GraphicsBuffer" or editing the Core upload helper. The helper already satisfies the bandwidth mandate; changing Core would be cross-domain churn.
Scalability potential: Low skips unchanged uploads entirely and writes only changed active matrices. Middle keeps normal presentation density with dirty gating. High/Ultra can feed the same double-buffered payload at higher auxiliary density without changing simulation truth or GPU read/write ownership.
Hardware Impact: Runtime behavior is unchanged by this pass. Static proof now ties the route to lock-write memcpy discipline, protecting MX350-class PCIe/driver bandwidth from accidental future `SetData` regression. Exact frame-time delta remains profiler-pending.

## Decision 42 - Scanner Active Status SignalBus Producer

Problem: Subagent audit proved `ScannerToolActiveSignal` was still produced by `ScannerTool.LateFrameTick` through `GlobalSignals.Publish(new ScannerToolActiveSignal)`. Even though that wrapper mirrored into `SignalBus`, it left a live hot producer on the legacy bridge and contradicted the first-party SignalBus doctrine.
Solution: Change `ScannerTool` to build the 32-byte `ScannerToolActiveSignal` as a local unmanaged value and push it directly through `SignalBus<ScannerToolActiveSignal>.Push(in signal)` every registered `LateFrameTick`. Removed the duplicate-detection fields so persistent scanner-active consumers receive a continuous typed-lane status stream instead of depending on a GlobalSignals latest cache.
Rejected Alternatives: Keeping the wrapper because it internally calls SignalBus, or switching only when values change. The wrapper preserves legacy producer ownership; change-only publishing would break consumers that need persistent active state without GlobalSignals latest fallback.
Scalability potential: Low/Middle/High/Ultra all get one bounded 32-byte scanner-status packet per registered late frame. This does not change scanner truth, DTO layout, save identity, or auxiliary deployment ownership; it only removes the legacy producer route.
Hardware Impact: Avoids one `GlobalSignals` wrapper call and latest-cache sequence write per scanner-status publish. Expected gain on i3/MX350 is below profiler resolution per frame, but it removes a hot-route governance violation and keeps queue pressure visible in `SignalBus<ScannerToolActiveSignal>` telemetry.

## Decision 43 - First 20 Route Impact Backfill And Scanner Coupling Boundary

Problem: The route card and reports described architecture but did not include the First 20 Minutes moment/route-impact/proof/parked-work block required by the vertical-slice contract. The same subagent audit flagged broad `ScannerTool` sibling namespace coupling, but that file still owns scanner knowledge/UI/lore/fauna/resource responsibilities outside SHINOBU_229's auxiliary effect route.
Solution: Backfilled First 20 Minutes route-impact fields into the route card, architecture note, binary payload ledger, JSON report, and XML self-audit. Recorded the exact moment as Tool / Hazard / Proof, with required Unity import, Play Mode smoke, Profiler/GC, SignalBus pressure, and Frame Debugger proof. Bounded scanner coupling as residual owner-split debt: do not delete active scanner knowledge/UI dependencies without compile proof and owner handoff.
Rejected Alternatives: Leaving the product-route proof implicit, or blindly deleting `using Hecton8.AI/Building/Construction/Caves/Tools/World/Narrative` from `ScannerTool`. The first fails the route contract; the second risks a compile wall in a large scanner file that is not solely auxiliary ownership.
Scalability potential: Low route remains cheap data packets for darkness/pulse/tether feedback; Middle increases cadence smoothly; High/Ultra can spend saved CPU/GPU bandwidth on richer downstream presentation while the same first-route facts stay Vault/SignalBus owned.
Hardware Impact: Runtime behavior unchanged by the documentation backfill. Engineering gain: reviewers get one route-impact block instead of rediscovering proof gaps; deleting broad scanner coupling is deferred to a scanner owner split where compile/profiler risk can be contained.

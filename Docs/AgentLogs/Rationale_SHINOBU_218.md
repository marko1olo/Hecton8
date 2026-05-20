# SHINOBU_218 Rationale

Status: ULTRA POLISH STATIC PASS / HABITAT DEFORMATION GENERATION HANDLE ROUTE PATCHED / CONTINUOUS HEALTH-PRESSURE QUALITY PATCHED / HULL JOB DETERMINISM PATCHED / STRUCTURAL CSV PLAYER ROUTE PATCHED / DAMAGE CONTRACT DEAR LIE PATCHED / LAYOUT REFLECTION PLAYER FENCE PATCHED / COMPILE BLOCKED BY CPU POLICY

## Bootstrap
Problem: Legacy flat structural strength cannot represent depth-derived load propagation or deterministic collapse cascades.
Solution: Build a stateless Burst kernel path over AUP and CSR buffers, with explicit 32-byte DTO layout and telemetry ring buffer.
Rejected Alternatives: Unity joints, Rigidbody mass, scene hierarchy scanning, scalar MonoBehaviour summation.
Scalability potential: Low uses sparse cadence and cheap scalar math; Middle uses regular CSR stress passes; High increases telemetry/visual buckling responsiveness; Ultra evaluates every frame and feeds richer wall deformation.
Hardware Impact: Expected MX350/i3 gain comes from removing PhysX/static integrity dependence and keeping solver data linear; measured proof absent.

## Mandate Selection
Problem: Structural integrity touches pressure math, graph propagation, native memory, signal routing, editor debug, and collapse telemetry; missing one boundary risks a compile wall or fake global authority.
Solution: Read `OPT_Zero_GC_Policy_AllocFree_Mandate`, `DATA_Runtime_Struct_Layout_ARM64`, `MATH_AUP_Determinism_Sync`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `PHYS_Physics_Integrity_Determinism_ForceMode`, `ARCH_Signal_Lane_Segregation`, `DBG_Telemetry_Crash_Reporting_PostMortem`, plus graph/execution/fake-first mandates.
Rejected Alternatives: Treating base integrity as local MonoBehaviour scalar fields, relying on PhysX joints, or pushing single-use string events.
Scalability potential: Low cadence throttles CSR solves; Middle keeps stable graph truth; High/Ultra spend recovered CPU on buckling visual intensity and richer telemetry, not more fragile physics.
Hardware Impact: Linear CSR traversal and 32-byte DTOs are cache-resident for thousands of nodes; measured proof absent until Unity profiler/GCMonitor.

## Phase 1 Archaeology
Problem: Construction still exposes scalar integrity APIs consumed by save, repair drones, HUD, and compatibility code; deleting those blindly would break unrelated systems and cross an asmdef boundary.
Solution: Treat `StructuralIntegrityCalculatorRuntime` as the new authority and document legacy scalar sites as compatibility surfaces. Static scanner reports no `BaseStrength.cs` or `IntegrityCounter.cs` and no Unity joint authority sites.
Rejected Alternatives: Directly deleting `BaseModule.CurrentIntegrity`, `ModuleIntegrityComponent`, or `HabitatGraphManager` scalar methods; Assembly-CSharp cannot safely hard-reference non-autoreferenced `Hecton8.Habitat.Deformation` without owner-level assembly work.
Scalability potential: Low/Middle/High/Ultra all read one Vault-backed CSR state path; legacy UI/save surfaces remain passive consumers until a stable construction graph export lands.
Hardware Impact: No hot-path managed hierarchy scan added; scanner is editor/CLI only.

## Solver Identity And Mock Entry
Problem: Existing structural solver carried stale SHINOBU_115 forensic identity while the assignment requires SHINOBU_218 telemetry dumps.
Solution: Changed structural agent/base hashes to SHINOBU_218, changed primary fault dump to `Docs/AgentLogs/Dump_SHINOBU_218.bin`, and added `GenerateMockStructuralStress()` as the mandated public mock stress entrypoint.
Rejected Alternatives: Leaving the SHINOBU_115 dump path or creating a second duplicate solver.
Scalability potential: Same 32-byte DTO/CSR path scales from sparse cadence on weak devices to per-frame high-buckling output on high tier.
Hardware Impact: Identity change has no runtime cost; mock entry is cold only.

## Burst CSR Pressure Path
Problem: Flat module integrity cannot model depth pressure, unsupported spans, or cascade loss of support.
Solution: Use deterministic Burst jobs in sequence: depth pressure, SDF anchor, CSR stress, collapse/leak signal emission, edge severing, telemetry. Depth uses double AUP subtraction before float conversion. CSR uses offsets/destinations and skips severed edges.
Rejected Alternatives: PhysX joints, Rigidbody mass, recursive cascade calls, or Transform hierarchy scans.
Scalability potential: Low uses 1/30 cadence and cheap fallback anchors; Middle/High use regular CSR evaluation; Ultra buys stronger buckling visuals and more responsive SDF sampling through `GlobalQualityWeight`.
Hardware Impact: Source estimate is `nodeCount*0.018us + edgeCount*0.006us + 7us` amortized by cadence; profiler proof pending.

## Signals And Black Box
Problem: Structural faults need downstream UI/audio/fluid response and crash forensics without managed hot-path logging.
Solution: Push unmanaged `BaseIntegrityEventPayload`, `FluidIncursionSignal`, and `BaseModuleCompromisedSignal` through `SignalBus<T>`; write 300 `StructuralTelemetryEntry` frames and dump `Dump_SHINOBU_218.bin` on non-finite or mass-collapse flags.
Rejected Alternatives: Direct `AudioSource`, direct water simulation, or `Debug.Log` as forensic truth.
Scalability potential: Low sheds solve cadence but keeps breach signal authority; High/Ultra preserve richer visual deformation through BucklingScalar.
Hardware Impact: Ring write is O(1); dump I/O only occurs on fault.

## Human Control And Static Validator
Problem: Designers need controlled tuning and proof that PhysX is not the base-integrity authority.
Solution: Keep the UI Toolkit tuner and SceneView heatmap wired to runtime telemetry/state; seed `Docs/Data/hull_materials.csv`; add `Tools/Structural_Integrity_Scanner.ps1` writing `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json`.
Rejected Alternatives: manual report text, prefab-based debug geometry, or hot-path material ScriptableObjects.
Scalability potential: Low keeps sparse authoritative solves and simple visuals; Middle/High/Ultra expose the same continuous quality weight through tuning and shader state.
Hardware Impact: Editor and CLI tooling cost 0 us in player hot path.

## Compile Gate
Problem: Compile verification is mandatory but project law forbids dotnet when CPU is above 50% or another compiler is running.
Solution: Latest samples showed CPU 100/100/96.1/94/100/99.2/100/100/100% with no active compiler process. Did not launch build. `git diff --check` passed on touched files with CRLF warnings only.
Rejected Alternatives: Running dotnet against explicit CPU gate or fabricating compile success.
Scalability potential: No runtime behavior claim depends on an unrun build.
Hardware Impact: Avoided adding build load to an already saturated machine.

## Ultra Polish Continuous SDF Tap
Problem: SDF anchor quality used `math.step(0.3f, quality)`, which made the extra cross-tap path a hard threshold even though the project bans binary quality gates.
Solution: Replaced the threshold with `math.smoothstep(0.25f, 0.75f, quality)` multiplied by the existing cubic quality curve. Severe throttling collapses to nearest-neighbor SDF or deterministic fallback; middle devices blend in extra taps gradually; high/ultra reaches full cross-tap support without a pop.
Rejected Alternatives: Keeping the step gate, adding an `IsLowEndHardware` branch, or forcing cross taps on every device.
Scalability potential: Low = 0 extra SDF ALU; Middle = partial cross-tap blend; High = near-full support smoothing; Ultra = full visual anchor refinement feeding buckling.
Hardware Impact: On i3/MX350 and Quest-class thermals this avoids the cross-tap reads at survival quality while removing the 0.3-quality discontinuity; measured proof pending profiler.

## Assembly Route Guard
Problem: Structural integrity must not create a compile wall by directly depending on sibling gameplay/runtime assemblies.
Solution: Verified `Hecton8.Habitat.Deformation.asmdef` references Core, Core.Contracts, Core.Memory, Bootstrap.Contracts, its own Contracts, and Unity packages only. No Construction, Vehicles, Fluid, UI, or sibling runtime assembly reference was added. Runtime uses SignalBus payloads and Vault buffers for cross-domain state.
Rejected Alternatives: Directly referencing `BaseModule`, Construction graph internals, or Fluid runtime components from the solver assembly.
Scalability potential: Low/Middle/High/Ultra all keep the same one-owner Vault route; other domains consume signals without forcing a recompilation of this solver.
Hardware Impact: Compile-wall protection is iteration-time impact, not frame-time impact; hot path still remains linear Burst jobs.

## Historical Architecture Doc Boundary
Problem: `Docs/ARCHITECTURE/SHINOBU_115_STRUCTURAL_INTEGRITY_CALCULATOR.md` describes the same runtime and still named the old SDF `math.step` gate plus stale CSV absence, which would mislead later agents after the SHINOBU_218 polish pass.
Solution: Marked the SHINOBU_115 document as historical where it diverges from current source, pointed it to the SHINOBU_218 architecture/status/log, and corrected the SDF gate and CSV status lines.
Rejected Alternatives: Leaving stale architecture text or deleting the historical document.
Scalability potential: Documentation now routes future structural edits to the current continuous-quality solver instead of reviving the thresholded quality path.
Hardware Impact: Documentation-only; prevents future compile/runtime churn from stale instructions.

## GPU Upload Dirty Gate
Problem: Visual sync copied the full structural state buffer after every completed solver pass, even when the Vault state hash and active node count had not changed. That wastes PCIe/unified-memory bandwidth and violates the bandwidth discipline rule against uploading unchanged data.
Solution: Included `BucklingScalar` in `StructuralTelemetryJob.StateHash`, cached the last uploaded hash/count in the runtime, and skipped `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy` when the structural state is unchanged. Shader params are still refreshed so local visual quality and frame index can move without re-copying the buffer. Fallback uploads without telemetry now clear the cache-valid flag; only telemetry-backed hashes can authorize a later skip.
Rejected Alternatives: Unconditional full-buffer upload, per-renderer `MaterialPropertyBlock` updates, or renderer hierarchy traversal.
Scalability potential: Low cadence already reduces solve frequency; this gate removes redundant uploads at every tier when pressure/collapse/buckling state is stable, while Ultra still uploads immediately when deformation state changes.
Hardware Impact: At 4096 nodes, unchanged-pass copy avoided is `4096 * 32 = 131,072 bytes` plus one `LockBufferForWrite`/unlock pair. Measured PCIe/UMA proof pending profiler.

## Owner Job Fence Registration
Problem: The structural runtime stored `_scheduledHandle` locally and completed it via `DispatcherJobFence`, but did not register the active solver chain with Core memory tracking. That reduced owner-level teardown and forensic visibility.
Solution: After scheduling telemetry, the final structural handle is registered with `H8Memory.RegisterActiveJob(SystemID.HullIntegrity, handle)`. Cold clear/mock/material-apply jobs also register their handles before their intentional cold `.Complete()`.
Rejected Alternatives: Private-only fence tracking or adding a new dispatcher/global route.
Scalability potential: Low/Middle/High/Ultra scheduling stays identical; owner memory tracking now sees the same final dependency chain.
Hardware Impact: Metadata registration only; no new solver pass or buffer allocation. Cold sync jobs remain cold-only and are skipped while the solver fence is alive.

## Global Authority Route Card
Problem: The solver changes Vault, SignalBus, shader upload, and black-box routes; global authority law rejects undocumented route changes even when the code path is narrow.
Solution: Added `Docs/ARCHITECTURE/SHINOBU_218_DEPTH_BASED_INTEGRITY_ROUTE_CARD.md` with owner, instruments, phases, cadence, capacities, failure mode, telemetry, shutdown, stale-handle behavior, rejected alternatives, and proof requirements. Review status is YELLOW because Unity import/profiler/GC/player proof is still absent.
Rejected Alternatives: Chat-only route explanation or claiming GREEN from static source.
Scalability potential: The card fixes the accepted route across Low/Middle/High/Ultra and blocks future agents from reintroducing MPB traversal, PhysX joints, or registry polling.
Hardware Impact: Documentation-only; prevents future runtime/compile churn by making the authority route explicit.

## Telemetry NaN Vaccination
Problem: `StructuralTelemetryJob` detected non-finite stress/pressure/buckling, but the same raw values still entered max calculations and state hashing. A NaN could poison telemetry fields or produce platform-variant hash bits before the dump.
Solution: The telemetry fold now creates sanitized local `stress`, `pressure`, and `buckling` values before critical counters, max pressure/stress, weakest buckle, and state hash. Non-finite input still sets `TelemetryFlagNonFinite`.
Rejected Alternatives: Trusting upstream jobs to always sanitize or hashing raw NaN payload bits.
Scalability potential: All quality tiers get deterministic black-box output; Ultra visual data can still fault and dump without corrupting the forensic row.
Hardware Impact: Three finite selects per active node in telemetry only; prevents one NaN from corrupting the 300-frame forensic ring.

## Buffer ID Collision Patch
Problem: Static audit found active structural `BufferID` values `70110-70116` overlapped existing raw Environment/Celestial constants in `HectonSeismicTideDirector`. DataVault is keyed by buffer ID, so the overlap could alias incompatible DTO types across domains.
Solution: Moved active structural IDs in `H8Memory.cs` to `70488-70497`, a free range between scalability and flora allocations, and updated current SHINOBU_218 route docs. Environment source was not edited.
Rejected Alternatives: Editing the seismic/celestial owner file, keeping duplicate IDs and relying on type validation, or adding local private NativeArrays to bypass Vault.
Scalability potential: Low/Middle/High/Ultra now use one non-colliding Vault route, preserving data sovereignty under any cadence.
Hardware Impact: Runtime ALU cost is 0 us; prevents catastrophic cross-domain memory alias and stale-handle faults.

## Cold CSV Player No-Op
Problem: `ColdTick()` polled CSV file metadata and path strings in player steady state, contradicting the intended zero-GC/zero-I/O hot route even though the parser itself used spans and Vault scratch.
Solution: Player runtime no longer registers `ColdTick`, and the method compiles to no-op outside `UNITY_EDITOR`. Boot CSV load remains cold; editor hot reload remains available for designers.
Rejected Alternatives: Per-cold-tick `File.Exists`/`GetLastWriteTimeUtc`, managed asset polling, or deleting CSV tuning entirely.
Scalability potential: All hardware tiers keep deterministic material data after boot; editor iteration keeps human tuning without player frame debt.
Hardware Impact: Removes steady-state file-system checks and path normalization from player cold phase; measured GC proof pending.

## Zero-Node GPU Upload Patch
Problem: GPU upload forced at least one structural row even when active node count was zero, causing shader params to advertise one stale/default row.
Solution: Zero active nodes now publish shader count `0`, skip `LockBufferForWrite` and `MemCpy`, and update the dirty-upload cache with a zero-count state.
Rejected Alternatives: Copying one dummy row, leaving stale GPU data visible, or branching in shaders to guess invalid node state.
Scalability potential: Low-tier empty/streaming bases avoid a needless copy; Ultra still uploads immediately once real nodes exist.
Hardware Impact: Saves one buffer lock/copy in empty structural sectors and removes visual correctness drift.

## Signal Lane Capacity Patch
Problem: Structural runtime explicitly configured only `BaseIntegrityEventPayload`; `FluidIncursionSignal` could fall back to default typed SignalBus capacity even though it is a structural breach route.
Solution: Structural boot now configures `FluidIncursionSignal` and `BaseModuleCompromisedSignal` capacities/lane hashes before ensuring their buses. BaseModule uses the generated FNV32 hash matching Core's signal identity.
Rejected Alternatives: Relying on default SignalBus capacity or direct GlobalSignals queue writes.
Scalability potential: Low-tier survival frames shed to 16 fluid events; high-tier can accept denser breach bursts without changing route.
Hardware Impact: Cold configuration only; hot signal enqueue path remains unmanaged queue push.

## Vault Generation Descriptor Migration
Problem: The structural runtime still persisted legacy `VaultBufferHandle<T>` fields. Current Core binary ledger marks that pointer-bearing handle as obsolete for new manager code because stale cached pointer metadata can survive DataVault generation churn.
Solution: Replaced structural handle fields with 16-byte `VaultGenerationHandle<T>` descriptors, changed boot acquisition to `GetGenerationHandle`, resolved method-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`, validated capacities after acquisition, and released descriptors through `IDataVault.ReleaseBuffer` on failed boot or shutdown.
Rejected Alternatives: Keeping `VaultBufferHandle<T>` and relying on the legacy bridge, caching `NativeArray<T>` views across frames, or adding private native arrays outside Vault.
Scalability potential: Low/Middle/High/Ultra solver cadence is unchanged; all tiers now survive DataVault relocation/generation invalidation through descriptor re-resolution instead of stale pointer reuse.
Hardware Impact: 0 us solver ALU change. Removes stale-pointer fault risk and shrinks persisted handle metadata from legacy 24-byte pointer-bearing records to 16-byte descriptors per structural lane.

## Habitat Deformation Runtime Descriptor Migration
Problem: A broad Habitat/Deformation scan found `HullIntegrityRuntime.cs` still persisted legacy `VaultBufferHandle<T>` fields, `.Resolve(_dataVault)` calls, and pointer-based MemClear scheduling. Even though it is a neighboring hull dent/deformation lane, the residual pointer route kept the domain-level H-Phi claim weak.
Solution: Migrated `HullIntegrityRuntime.cs` to `VaultGenerationHandle<T>` descriptors, `IDataVault.TryResolveHandle` phase-local `NativeArray<T>` views, capacity validation, failed-boot/shutdown `ReleaseBuffer`, and `H8Memory.RegisterActiveJob` for scheduled/cold clear handles. Scoped scan now finds no legacy Vault handle/pointer patterns in `Assets/_Project/Scripts/Habitat/Deformation`.
Rejected Alternatives: Leaving the residual lane as documentation debt, caching `NativeArray<T>` views across frames, or introducing private native collections outside Vault.
Scalability potential: Low/Middle/High/Ultra hull dent, breach jet, and structural visual lanes now survive DataVault generation churn through descriptor re-resolution; quality/cadence math is unchanged.
Hardware Impact: 0 us solver ALU change. Removes 20 legacy 24-byte pointer-bearing manager fields in favor of 20 16-byte descriptors, saving 160 bytes of persistent handle metadata and removing stale pointer reuse risk.

## Continuous Health Pressure Quality Ramp
Problem: `HullIntegrityRuntime` still compressed homeostasis pressure into discrete warning/critical quality ceilings. That preserved a binary visual/perf step during thermal or health pressure even while structural cadence was continuous.
Solution: Consume `SystemHealthIndexSignal.Pressure01` as the primary scalar. Warning and critical states are only fallback floors for missing pressure. The runtime now derives warning and critical ramps with `math.smoothstep`, then blends the quality ceiling with `math.lerp` before applying the existing dent capacity hysteresis.
Rejected Alternatives: hard `HealthStateWarning`/`HealthStateCritical` caps, a binary low-end hardware branch, or hiding state changes behind longer hysteresis.
Scalability potential: Low pressure keeps full configured quality; rising pressure continuously sheds tracked dents and shader dent rows; severe pressure converges toward the survival ceiling without a visual pop.
Hardware Impact: Two smoothsteps and two lerps per quality-drain pass. This replaces abrupt workload cliffs with predictable degradation; measured profiler proof pending.

## Breach Jet Camera Registry Cache
Problem: `RefreshBreachJetCameraCold()` fell back to `GlobalRegistry.Player` when the override/current camera was absent. That is cold-side, but it still weakens the rule that registry reads must be cached at boot/hot-swap boundaries.
Solution: `HullIntegrityRuntime` now implements `IGlobalRegistryHotSwapListener` and `IGlobalRegistryHotSwapRefListener`, caches `IPlayerRuntimeContext` at boot, updates it on `GlobalRegistryServiceSlot.Player` replacement, and reads the cached context during breach-jet refresh.
Rejected Alternatives: polling `GlobalRegistry.Player` during every refresh, scene camera searches, or adding a new direct player dependency.
Scalability potential: All tiers use the same cached camera route; low tiers shed dent/breach visuals through continuous quality while high/ultra still get breach jet alignment without service polling.
Hardware Impact: Saves one service-locator read on uncached breach-jet refreshes and removes a hot-swap stale-context risk. Solver ALU unchanged.

## Editor-Only CSV Cold Lane
Problem: The documentation claimed player runtime no longer registered `ColdTick`, but source still registered the cold lane and compiled CSV file polling methods into player builds. Even if the cold path was not structural solver math, it left avoidable dispatcher and file-I/O surface in the player assembly.
Solution: `TryRegisterTickables()` now registers `IColdTickable` only under `UNITY_EDITOR`, and CSV hot-reload/file parser methods are compiled only for editor. Fault dump file I/O remains in player because black-box dump is a crash-forensic route, not tuning hot reload.
Rejected Alternatives: keeping development/player file metadata polling, deleting designer CSV hot reload, or moving tuning into ScriptableObjects.
Scalability potential: Low/Middle/High/Ultra player builds keep boot-seeded material/tuning data without steady cold polling; editor keeps human tuning control.
Hardware Impact: Removes one player cold dispatcher callback and strips CSV file polling/parsing code from player builds. Runtime solver ALU unchanged.

## Hull Burst Determinism Pass
Problem: `HullIntegrityTypes.cs` jobs still used `FloatMode.Fast` while mutating SIP, breach flags, deformation rows, pressure buckling dents, breach jet args, and telemetry. That is rollback-adjacent state and can drift between x86 and ARM64.
Solution: Converted every Burst job in `HullIntegrityTypes.cs` to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`, matching `StructuralIntegrityCalculatorTypes.cs`.
Rejected Alternatives: fast-math for state mutation, selective deterministic mode with a missed job risk, or leaving visual/deformation state divergent across platforms.
Scalability potential: All tiers now use the same deterministic hull state mutation path; visual load is still shed through continuous quality/capacity curves.
Hardware Impact: Potential ALU throughput loss is accepted for authoritative consistency. No measured profiler proof yet; static Burst directive scan is clean.

## Structural CSV Editor Fence
Problem: `StructuralIntegrityCalculatorRuntime` no longer registered player `ColdTick`, but boot still attempted `Docs/Data/hull_materials.csv` file reads and the CSV parser compiled into player builds. That left an avoidable player file-open route for material tuning.
Solution: Kept `WriteDefaultMaterials()` in player boot, wrapped the boot CSV read, CSV hot reload, span parser helpers, and cold material-apply job in `UNITY_EDITOR`. Crash dump path and default material table remain available in player.
Rejected Alternatives: player boot `File.Exists`/`File.GetLastWriteTimeUtc`/`FileStream.Open`, deleting designer CSV tuning, or moving material truth to a private native collection.
Scalability potential: Low/Middle/High/Ultra player builds use deterministic defaults or future baked payloads without file-system dependency; editor retains hot tuning.
Hardware Impact: Removes player boot file metadata/open route for structural material CSV. Solver ALU unchanged.

## Damage Contract Dear Lie Pass
Problem: `HabitatDamageMeshStateResolver.ResolveStateIndex(float)` used hard staged pressure thresholds and could select Stressed/Ruptured mesh hashes before collapse. That contradicts the SHINOBU_218 Dear Lie requirement: pressure should feed shader buckling until real collapse, not swap meshes early.
Solution: Pressure-to-state now returns only Intact or Collapsed. Added `ResolveVisualBuckling01(float)` using `math.smoothstep(0.33333334,0.95,p)` for continuous visual buckling. Explicit byte-state mesh resolution remains for editor/baked tools, but pressure truth no longer selects staged meshes before collapse.
Rejected Alternatives: `math.step` staged mesh thresholds, CPU mesh swaps for pre-collapse deformation, or removing the baked mapping DTO.
Scalability potential: Low tier keeps cheap pristine/collapsed selection; Middle/High/Ultra spend visual fidelity through shader buckling scalar instead of asset-state churn.
Hardware Impact: Avoids pre-collapse mesh state churn. Runtime consumers can drive one scalar into GPU displacement; no new buffer allocation or DTO layout change.

Follow-up source proof: A direct file read after later scans found the old staged `math.step` code still present on disk. The source is now actually patched and a rerun scoped `math.step` scan over Habitat/Deformation Runtime/Contracts returns no hits.

## Layout Reflection Player Fence
Problem: `HullIntegrityRuntime.ValidateLayouts()` still used reflection-backed field offset checks during normal boot. The pass is cold, but it is managed metadata traversal in player assembly and weaker than the structural runtime's editor-only offset proof.
Solution: Split validation into size checks for every build and offset checks under `UNITY_EDITOR` only. Player boot still rejects DTO size drift through `UnsafeUtility.SizeOf<T>()`; editor boot retains exact offset proof for authoring and import validation.
Rejected Alternatives: keeping runtime reflection, stripping all layout validation, or moving the proof into private test-only code that the editor does not execute.
Scalability potential: Low/Middle/High/Ultra runtime no longer pays managed metadata traversal; editor still blocks offset drift before content reaches any device tier.
Hardware Impact: Removes one boot-time reflection pass over hull/deformation DTOs from player builds. Solver ALU and Vault routes unchanged.

## Hull Cold Unregister Fence
Problem: `HullIntegrityRuntime` cold registration was editor-only, but the unregister branch still compiled into player and depended on `_registeredCold == 0` to avoid the call.
Solution: Wrapped cold unregister in the same `UNITY_EDITOR` fence as cold registration. Follow-up: both structural and hull runtime classes now implement `IColdTickable` and compile `ColdTick()` only under `UNITY_EDITOR`.
Rejected Alternatives: relying on player branch elimination, leaving dead cold-lane callsites in the assembly, or allowing player types to satisfy an unused cold dispatcher interface.
Scalability potential: All device tiers avoid player cold-dispatcher lifecycle code; editor keeps hot CSV/tuning reload lifecycle.
Hardware Impact: Removes player cold interface surface, methods, and hull teardown branch/callsite. Runtime solver ALU unchanged.

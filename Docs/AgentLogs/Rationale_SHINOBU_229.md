# SHINOBU_229 Rationale

Status: COMPILE WALL - BLOCKED BY GENERATED PROJECT STALENESS AND SIBLING DEPENDENCIES

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
Solution: Add `BufferID.ShinobuAuxiliaryActiveEquipmentState` and mirror auxiliary truth into a separate `ActiveEquipmentDTO` NativeArray using the same 32-byte ABI.
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

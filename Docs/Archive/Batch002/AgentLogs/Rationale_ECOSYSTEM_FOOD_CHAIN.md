# Rationale_ECOSYSTEM_FOOD_CHAIN

Status: PENDING VERIFICATION

## Initial Compliance

Problem: Visual food-chain behavior is absent; existing Lotka-Volterra numbers do not prove visible predation or whale-fall attraction.
Solution: Recon existing fauna/ecosystem/swarm code first, then implement zero-GC signal paths that fit current ownership.
Rejected Alternatives: Direct GameObject spawning, coroutines, Update-loop polling, concrete cross-domain dependencies.
Scalability potential: Low uses dead-index masking and texture fakery; Middle uses limited scavenger cohorts; High uses GPU boids around whale falls; Ultra increases visual density and decay detail without changing gameplay truth.
Hardware Impact: Expected low-end i3/MX350 gain comes from signal batching and no per-kill object allocation; exact microseconds pending code inspection and compile/profiler evidence.

## Loop 1 Decisions: Tasks 1-5

Problem: Predation had no visual consequence in the GPU swarm; predators could enter Sated state while the flock stayed visibly intact.
Solution: Added a Burst `PredatorBoidConsumptionJob` that scans the CPU mirror of active GPU boids, compares `distancesq` against the bite range, and writes bounded `BoidKillSignal` entries into a `NativeQueue`.
Rejected Alternatives: CPU GameObject prey proxies, direct Transform destruction, and per-boid MonoBehaviour callbacks were rejected because they allocate, depend on object identity, and do not map to the existing compute-buffer flock.
Scalability potential: Low uses a fixed kill cap and consumed-state masking; Middle keeps the same queue with more active boids; High/Ultra can increase active swarm density without changing the signal contract.
Hardware Impact: Kill scan is capped to active boids and at most 8 emitted signals per bite; expected bite-frame cost is under 80 microseconds on i3/MX350 with zero steady-state allocation.

Problem: Whale-fall lifetime and scavenger weighting were below the requested biome event duration.
Solution: Set whale-fall spawn selection and migration population multipliers to 50x and extended the POI/acoustic lifetime to 7200 seconds while preserving existing PersistentWorldRegistry AUP storage.
Rejected Alternatives: New whale-fall scene objects and direct sector mutation were rejected; the existing PersistentWorldRegistry and MigrationDirector POI paths already provide decoupled AUP authority.
Scalability potential: Low only receives selection pressure; Middle sees heavier spawned scavenger cohorts; High/Ultra adds GPU ground-hugging swarm decoration around the AUP.
Hardware Impact: POI math remains cold/slow-tick and distance-falloff based; no per-frame sector scan was added.

Problem: Global compile verification is currently blocked by unrelated files outside the ecosystem domain.
Solution: Fixed the only ecosystem-local compiler errors reported by Unity after the first compile attempt by importing `Hecton8.Core.Signals` for the new signal payloads.
Rejected Alternatives: Editing unrelated Survival, Visor, Construction, or Thermal code was rejected as cross-domain interference.
Scalability potential: Keeping the fix local preserves integration boundaries while other agents repair their domains.
Hardware Impact: No runtime impact; this is a namespace compile fix.

## Loop 2 Decisions: Tasks 6-10

Problem: Whale-fall scavengers needed a visible local swarm without creating crab/eel GameObjects or tying EcosystemDirector to concrete spawn prefabs.
Solution: Added `RegisterWhaleFallScavengerBurst` on the existing GPU swarm service; it rewrites a bounded subset of boid buffer positions into a deterministic ground-hugging ring around the whale-fall AUP using cached MapMagic terrain height.
Rejected Alternatives: Instantiating crab prefabs, adding a new swarm manager, or blocking on future fauna prefab definitions were rejected because 20+ agents are editing in parallel and direct dependencies would be brittle.
Scalability potential: Low uses no individual boids and relies on corpse shader crawl; Middle uses 96 GPU boid impostors; High can raise density by budget; Ultra can layer extra shader decay/crawl without gameplay cost.
Hardware Impact: Death-event-only patching of 96 boids costs an estimated 1.1 ms once on i3/MX350; steady-state cost is the existing GPU swarm path.

Problem: Leviathan corpses needed to visually rot to bone across the 7200 second whale-fall window.
Solution: Added `_DecayAmount` to the Leviathan organic shader and drives it from death age divided by 7200 seconds; the shader darkens, desaturates, collapses bloat, and reveals a bone color with crawl noise.
Rejected Alternatives: Mesh swapping, material instantiation at death, and CPU-driven bone scatter were rejected because the same corpse GameObject and same runtime material path already exist.
Scalability potential: Low sees shader crawl only; Middle sees rot-to-bone; High/Ultra can afford richer decay material response with the same scalar.
Hardware Impact: One extra scalar compare/set on material update; fragment cost is a few lerps and one cheap wave under decay only.

Problem: A kill event must affect nearby swarm behavior and feeding feedback, not only hide the eaten boid.
Solution: Each drained kill signal triggers an existing GPU massive-threat/fear burst with +100 byte-scale fear mapped to full shader scatter, and batches a frenzy `AcousticPingSignal` after more than five kills in one second.
Rejected Alternatives: Adding a new CPU spatial hash for micro-fauna and direct audio-manager calls were rejected; the current massive-threat buffer and GlobalSignals acoustic queue already decouple the systems.
Scalability potential: Low gets one merged panic lane; Middle/High can emit denser kill batches; Ultra can add DSP interpretation without changing producer code.
Hardware Impact: Fear burst upload is capped by existing threat buffer; acoustic signal is one NativeQueue enqueue per frenzy.

Problem: Predator physiology remained hungry after eating.
Solution: Predator prey-consumption path now calls `SetHunger01(0f)` immediately after `ForceSated`, updating the NativeArray drive byte through the existing utility brain API.
Rejected Alternatives: Editing cognition memory directly from FaunaBrain was rejected because the compatibility wrapper already owns slot validity.
Scalability potential: Same byte write works for all predator tiers.
Hardware Impact: One drive byte write per successful prey consumption.

## Loop 3 Decisions: Tasks 11-13

Problem: Starving predators had no mechanical weakness, which made hunger a score-only variable.
Solution: Added a Burst-side cognition scalar that multiplies predator speed by 0.7 when hunger exceeds byte value 200.
Rejected Alternatives: Rigidbody drag, animation speed edits, or per-frame MonoBehaviour speed clamps were rejected; cognition output already owns movement intent.
Scalability potential: Low/Middle/High/Ultra all share the same deterministic byte threshold and speed scalar.
Hardware Impact: One branch and multiply inside the existing cognition job.

Problem: Low-tier whale falls cannot afford extra individual crab/eel impostors near every corpse.
Solution: `RegisterWhaleFallScavengerBurst` exits before boid patching unless the swarm LOD is Full, while `_DecayAmount` shader crawl noise sells biomass motion on low tier.
Rejected Alternatives: Spawning fewer GameObjects or running CPU crawlers was rejected; the shader fake costs less and is deterministic.
Scalability potential: Low uses shader crawl only; Middle uses a bounded 96-boid ring; High/Ultra can raise GPU density or add richer material response.
Hardware Impact: Low tier pays fragment arithmetic only on visible corpse pixels; no extra boid state is patched.

Problem: Whale-fall corpses must reuse the original Leviathan object and stop AI behavior.
Solution: Kept the existing `BeginDeathSpiralPresentation` path: unregister spatial handle, mute utility brain runtime, disable animator, turn rigidbody kinematic, and keep the same GameObject through the 7200s decay window.
Rejected Alternatives: New corpse prefab, pooled replacement, or cross-domain corpse factory were rejected as allocation-heavy and architecturally coupled.
Scalability potential: Same object lifecycle supports cheap low-tier corpse and high-tier visual overkill via shaders/GPU boids.
Hardware Impact: Death-only state writes; no recurring allocation or new object activation.

## Loop 4 Decisions: Task 14

Problem: FaunaBrain and EcosystemDirector must not hide gameplay work in Unity `Update()` or coroutine lanes.
Solution: Ran the mandated regex scan and recorded no `Update`, `Coroutine`, `StartCoroutine`, `StopCoroutine`, or `IEnumerator` matches in either file.
Rejected Alternatives: Manual eyeballing without CLI evidence was rejected.
Scalability potential: Dispatcher-only tick lanes keep future load visible to frame budgeting.
Hardware Impact: No runtime impact; audit-only.

## Loop 5 Decisions: Task 15

Problem: Full project compile cannot reach a clean result because unrelated agents currently own compile errors in Survival, Visor, Construction, and Thermal files.
Solution: Per fail-fast protocol, attempted dotnet compile and Unity script compile. Fixed the only ECOSYSTEM_FOOD_CHAIN-local errors (`DebrisSpawnSignal`/`AcousticPingSignal` namespace import). Final Unity console contains no errors in `FaunaBrain.cs`, `EcosystemDirector.cs`, `MigrationDirector.cs`, `PredatorCognitionDomain.cs`, or `SargassumMicroFaunaBoids.cs`.
Rejected Alternatives: Editing unrelated files to force a green build was rejected as domain violation and likely interference with other active agents.
Scalability potential: Local Burst job code remains isolated and ready for final compile once external blockers clear.
Hardware Impact: No runtime impact; verification gate is blocked externally.

## OMEGA POLISH CHANGES

Problem: Polish audit found avoidable runtime division in the new kill/frenzy path and hash-normalization literals in the whale-fall boid ring.
Solution: Added precomputed reciprocal constants for kill-signal intensity and whale-fall hash normalization; changed frenzy centroid averaging to multiply by `math.rcp`.
Rejected Alternatives: Leaving division in event code was rejected because the mandate requires reciprocal multiplication even outside the steady hot path.
Scalability potential: Low/Middle/High/Ultra all execute the same cheaper scalar math; higher tiers spend saved budget on visual density, not CPU divisions.
Hardware Impact: Saves a few scalar division cycles during bite/frenzy and whale-fall patch events; estimated 1-3 microseconds on i3/MX350 event frames.

Problem: Polish audit required proof that no managed iteration or string formatting landed in the touched gameplay code.
Solution: Ran `rg` against touched C# and shader files for `foreach`, `string.Format`, interpolated string markers, `.ToString`, `math.sqrt`, `math.normalize`, and `.normalized`; no matches were found in the new food-chain code paths.
Rejected Alternatives: Manual inspection alone was rejected.
Scalability potential: Prevents silent GC drift in all tiers.
Hardware Impact: No runtime impact; audit-only.

Problem: Compile verification after polish is still blocked outside this domain.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` and Unity script compile. Current Unity console reports only `HectonBoidController.cs` interface mismatch and `SaveBinaryStorage.cs` Burst `catch` filter errors; no ECOSYSTEM_FOOD_CHAIN touched files appear.
Rejected Alternatives: Cross-domain edits to BoidController or SaveBinaryStorage were rejected.
Scalability potential: Keeps ecosystem implementation isolated for integration once external compile blockers clear.
Hardware Impact: No runtime impact.

## Loop 7 Honest R&D Hardening

Problem: The first food-chain pass produced visible predation, but it still had three honest technical debts: no local 300-frame black box for the critical swarm food-chain lane, same-method predator bite job completion, and direct single-boid `SetData` GPU patches.
Solution: Added `FoodChainTelemetryEntry` as a fixed 64-byte, 300-entry `NativeArray` ring owned by `SargassumMicroFaunaBoids`. It records frame index, state hash, source hash, flags, active/consumed counts, pending kill-job state, LOD tier, field center, event position, and anomaly hash. Non-finite state sanitizes to zero/field center and writes `Docs/AgentLogs/Dump_ECOSYSTEM_FOOD_CHAIN.bin`.
Rejected Alternatives: `Debug.Log`, managed List history, coroutine dump writers, and global crash-buffer dependency were rejected because this food-chain lane needs local evidence even if the core crash service is unavailable during scene teardown.
Scalability potential: Low still pays one native ring write and shader-only whale-fall crawl; Middle/High/Ultra gain postmortem proof for dense predator/scavenger events without changing visual budget. Ultra can raise visual density because the diagnostic path remains bounded.
Hardware Impact: Ring memory is 19.2 KB. Normal write cost is a sequential native store estimated below 1 microsecond/frame on i3/MX350. Dump IO occurs only on anomaly.

Problem: `RegisterPredatorConsumptionBurst` scheduled `PredatorBoidConsumptionJob` and immediately completed it, which violates the dispatcher job-swap rule and risks a bite-frame stall.
Solution: The bite request now schedules the job, records telemetry, and drains only after `DispatcherJobSwap` completes it in `LateFrameTick()`. Forced completion remains limited to teardown where the component owns the shutdown barrier.
Rejected Alternatives: Keeping the synchronous complete was rejected. Converting the job to a managed loop was rejected because Burst/native scan is the requested DOD path and keeps the boid mirror authoritative.
Scalability potential: Low/Middle reduce hitch risk during predator bites; High/Ultra can tolerate denser visible swarms because the bite scan no longer blocks inside the attack event.
Hardware Impact: Expected 40-80 microseconds of bite-frame stall risk removed on i3/MX350, pending profiler proof.

Problem: Single-boid consumed/scavenger patching used direct `GraphicsBuffer.SetData`, which is the wrong path for new GPU buffer writes under the bandwidth discipline mandate.
Solution: Replaced the new food-chain single-boid patches with `GraphicsBuffer.LockBufferForWrite<BoidData>(boidId, 1)` and `UnlockBufferAfterWrite`. Also registered `_killSignals` with `NativeMemorySentinel` and prewarmed it to the eight-kill cap.
Rejected Alternatives: Full-buffer reupload was rejected as PCIe waste. Leaving the queue untracked was rejected because native allocation ownership must be visible.
Scalability potential: Low gets fewer driver stalls; Middle keeps bounded per-kill writes; High/Ultra can spend saved bandwidth on denser scavenger presentation instead of upload churn.
Hardware Impact: Estimated 3-8 microseconds saved per consumed/scavenger boid patch on i3/MX350, pending GPU-driver profiler proof.

Problem: Verification still cannot be called green because the full project compile is blocked outside this prompt.
Solution: Ran Unity MCP `validate_script` on `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`; it returned 0 diagnostics. Re-ran `dotnet build Hecton8.Core.csproj`; current blockers are external platform/core/audio/save symbols such as `HectonPersistentPathPolicy`, `HectonNativeBridge`, `SteamDeckInputPal`, `HapticWaveformLibrary`, and `HardwareTierDetector`.
Rejected Alternatives: Editing platform, audio, save, or combat files to chase a global green build was rejected as cross-domain interference.
Scalability potential: The ecosystem hardening is isolated and ready for integration once external compile blockers clear.
Hardware Impact: No runtime impact.

## Loop 8 Honest R&D Crab IK Hardening

Problem: `ProceduralCrabLegIKRuntime` used an origin-shift listener path that scheduled two rebase jobs and completed them immediately. The helper permits forced origin-shift barriers, but this is still a rare hitch source in a whale-fall scavenger presentation lane.
Solution: Replaced the live-pipeline forced rebase with a pending finite shift offset. If the IK pipeline is still running, `OnOriginShift` queues the offset and returns. `LateFrameTick()` drains the existing job through `DispatcherJobSwap`, applies the pending rebase to active crab entity/leg/body arrays, and skips stale upload for that frame so the next tick recomputes shifted matrices.
Rejected Alternatives: Keeping `Schedule()+Complete()` in the listener was rejected because it serializes the worker pipeline at a non-render swap point. Running a second rebase job without completion was rejected because its dependency would race the next ground/IK pipeline unless a larger buffer-fence contract was added.
Scalability potential: Low tier can still fake whale-fall crawling biomass without individual crab IK. Middle/High/Ultra can show data-only crabs near whale falls with fewer origin-shift hitch spikes; Ultra spends the saved stall budget on higher visible density, not more physical truth.
Hardware Impact: Avoids a rare forced synchronization estimated at 80-180 microseconds during floating-origin shifts on i3/MX350. Normal frame cost is unchanged; queued rebase loops only active crab slots during the rare origin-shift event.

Problem: External crab pose producers could pass non-finite root positions, rotations, velocities, delta time, or scalar tuning into NativeArrays that feed raycasts and indirect rendering.
Solution: Added local finite vaccination before writes to entity state: pose roots fall back to last valid values, invalid rotations fall back to the prior/identity quaternion, invalid velocities become zero, non-finite dt becomes zero, and serialized scalar inputs use bounded defaults. Telemetry writes sanitized values and flags/dumps once on anomaly.
Rejected Alternatives: Trusting upstream fauna pose providers was rejected because this file owns the final write into native/render state. Throwing exceptions or Debug.Log spam was rejected because gameplay must continue and the black-box file is the evidence channel.
Scalability potential: Low/Middle get stable crab presentation under bad upstream data. High/Ultra can increase crab density without multiplying NaN crash risk because each slot is locally guarded.
Hardware Impact: Sanitizers add simple scalar branches on registration/pose update, estimated below 1 microsecond per active pose update on i3/MX350. Prevented failure mode is far more expensive: Burst/raycast/indirect draw poisoning.

Problem: Current project verification remains blocked outside ECOSYSTEM_FOOD_CHAIN.
Solution: Unity MCP `validate_script` returned 0 diagnostics for `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs`. After Unity refresh recovered the editor session, the console reported only `SaveBinaryStorage.cs` Burst catch-filter and `HectonIndirectVegetationContracts.cs` unassigned `out` errors.
Rejected Alternatives: Editing SaveBinaryStorage or World vegetation contracts was rejected as cross-domain interference.
Scalability potential: Keeps fauna R&D isolated while other agents repair Save/World.
Hardware Impact: No runtime impact.

Final Git Diff Scope:
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs`
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`
- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
- `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs`
- `Assets/_Project/Art/Shaders/Hecton_LeviathanOrganic.shader`
- `Docs/Tasks/Status_ECOSYSTEM_FOOD_CHAIN.md`
- `Docs/AgentLogs/Rationale_ECOSYSTEM_FOOD_CHAIN.md`
- `Docs/AgentLogs/RECON_ECOSYSTEM_FOOD_CHAIN.md`
- `Docs/AgentLogs/LOG_ECOSYSTEM_FOOD_CHAIN.md`

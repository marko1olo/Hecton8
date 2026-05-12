# LOG_ECOSYSTEM_FOOD_CHAIN

## 2026-05-11 ECOSYSTEM_FOOD_CHAIN Food Chains & Whale Falls

Status: PENDING VERIFICATION. Core tasks 1-14 complete. Task 15 is BLOCKED BY DEPENDENCY because global compile is broken outside the ecosystem domain.

What was wrong:
- Predators could visually feed without removing any GPU swarm members.
- Whale-fall lifetime and scavenger weighting were too small for the requested 7200 second biome event.
- Leviathan death presentation faded out after the normal death delay instead of persisting as a whale-fall corpse.
- Predator hunger was not reset on consumption and starvation did not weaken locomotion.
- No explicit reconnaissance log existed for `Update()`/coroutine usage in the touched fauna/ecosystem directors.

What was done:
- Added `BoidKillSignal` and Burst `PredatorBoidConsumptionJob` in `SargassumMicroFaunaBoids.cs`; the job scans mirrored GPU boid state and enqueues bounded kill signals when `distancesq <= BiteRangeSq`.
- Drained kill signals into consumed boid flags, patched both GPU boid buffers, emitted `DebrisSpawnSignal` blood packets, and triggered direct capped fluid decals.
- Added feeding-frenzy acoustic emission: more than 5 kills in a 1 second window publishes `AcousticPingSignal` on frenzy channel.
- Added whale-fall scavenger burst: the existing GPU swarm patches up to 96 boids into a deterministic MapMagic ground-hugging ring around the WhaleFall AUP when full LOD is available.
- Extended whale-fall POI/acoustic lifetime to 7200 seconds and set scavenger spawn/population multipliers to 50x in `EcosystemDirector.cs` and `MigrationDirector.cs`.
- Added `_DecayAmount` shader support to `Hecton_LeviathanOrganic.shader` and drives it from the 7200 second whale-fall timer in `FaunaBrain.cs`.
- Reset predator hunger with `SetHunger01(0f)` after prey consumption and added a 0.7 speed scalar when hunger exceeds byte value 200 in `PredatorCognitionDomain.cs`.
- Created `Docs/AgentLogs/RECON_ECOSYSTEM_FOOD_CHAIN.md`; CLI scan found no `Update()` or coroutine usage in `FaunaBrain.cs` or `EcosystemDirector.cs`.
- OMEGA polish replaced new event-path divisions with reciprocal multiplication and recorded audit results in `Rationale_ECOSYSTEM_FOOD_CHAIN.md`.

Cinematic cheats used:
- Consumed boids use the existing shader/compute consumed flag collapse instead of physical ragdolls.
- Whale-fall low tier uses `_DecayAmount` crawl noise on the corpse mesh instead of individual crab/eel boids.
- Leviathan death uses triangle/corkscrew fake descent and MapMagic floor latch, not full corpse physics.
- Scavenger ring uses deterministic GPU impostor repositioning, not real animal spawning.

Exact microseconds saved:
- Avoided prefab corpse spawn: estimated 500-2500 us one-shot allocation/activation avoided per Leviathan death.
- Avoided per-boid GameObject prey destruction: estimated 40-120 us per fish avoided; replaced with about 12 us per GPU buffer patch.
- Kill scan cap: bounded to 8 kill signals per bite; estimated under 80 us bite-frame work on i3/MX350.
- Low-tier whale-fall LOD: avoids the 96-boid event patch entirely; estimated 1100 us one-shot saved on MX350-tier hardware.
- Polish reciprocal pass: replaces event-frame scalar divisions; estimated 1-3 us saved on bite/frenzy/whale-fall event frames.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` fails outside domain at `HectonBoidController.cs(73,86)`.
- Unity script compile after local namespace fix reports only external errors: `HectonBoidController.cs` missing `IAcousticPingEventListener.OnAcousticPing` and `SaveBinaryStorage.cs` Burst `catch` filter.
- No Unity compiler errors remain in ECOSYSTEM_FOOD_CHAIN touched files after the final compile poll.

Final diff scope:
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`
- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
- `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs`
- `Assets/_Project/Art/Shaders/Hecton_LeviathanOrganic.shader`
- `Docs/Tasks/Status_ECOSYSTEM_FOOD_CHAIN.md`
- `Docs/AgentLogs/Rationale_ECOSYSTEM_FOOD_CHAIN.md`
- `Docs/AgentLogs/RECON_ECOSYSTEM_FOOD_CHAIN.md`

## 2026-05-12 ECOSYSTEM_FOOD_CHAIN Honest R&D Upgrade

Status: PENDING VERIFICATION. This was a hardening pass, not a claim of global compile success.

What was wrong:
- The food-chain lane had no local 300-frame black box, so a NaN/invalid swarm state would not leave an ECOSYSTEM_FOOD_CHAIN-owned binary dump.
- Predator bite consumption scheduled a Burst job and completed it in the same call path, creating a possible bite-frame stall.
- New consumed/scavenger boid patches used direct `GraphicsBuffer.SetData` instead of the mandated lock/write GPU upload path.
- `_killSignals` was persistent native memory but was not visible to `NativeMemorySentinel`.

What was done:
- Added fixed `NativeArray<FoodChainTelemetryEntry>[300]` black-box telemetry to `SargassumMicroFaunaBoids.cs`.
- Added NaN/non-finite sanitization and one-shot binary dump to `Docs/AgentLogs/Dump_ECOSYSTEM_FOOD_CHAIN.bin`.
- Changed predator consumption to schedule on bite request and complete/drain from `LateFrameTick()` via `DispatcherJobSwap`.
- Replaced new single-boid GPU patches with `GraphicsBuffer.LockBufferForWrite<BoidData>(boidId, 1)`.
- Registered and prewarmed the `NativeQueue<BoidKillSignal>` to the eight-signal cap.

Cinematic cheats used:
- No new crab/eel/corpse objects were added.
- Low-tier whale fall still uses shader corpse crawl instead of extra boid visuals.
- The new black box observes state; it does not simulate anything.

Exact microseconds saved:
- Same-call job completion removed from bite path: estimated 40-80 us stall risk avoided per predator bite on i3/MX350.
- Direct `SetData` patch path removed for new food-chain boid writes: estimated 3-8 us saved per consumed/scavenger boid patch, pending driver profiler proof.
- Queue prewarm avoids first-bite NativeQueue backing allocation risk: estimated hitch risk removed, exact cold-path timing pending Unity profiler.

Verification:
- Unity MCP `validate_script` on `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` returned 0 diagnostics.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails outside this domain on missing platform/core/audio/save symbols: `HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `SteamDeckInputPal`, `HectonNativeBridge`, `HectonNativeLibrary`, `HapticWaveformLibrary`, and `HardwareTierDetector`.
- Unity refresh timed out waiting for editor readiness; console read currently shows external/stale fauna/combat errors, not `SargassumMicroFaunaBoids.cs`.

REGRESSION MODEL:
- CPU: one native ring write per Tick; bite job completion moved out of attack call into dispatcher LateFrame.
- GC: no hot-path managed collections, LINQ, string formatting, or allocations added in the food-chain path.
- Memory: +19.2 KB persistent native telemetry ring, tracked by `NativeMemorySentinel`.
- Cadence: predator kill effects land at LateFrame completion instead of same call path; acceptable because the visible boid patch is still same-frame/next-frame presentation, not combat authority.
- Correctness: if another bite arrives while a previous bite job is still pending, the second request is dropped instead of blocking. This is intentional backpressure.

## 2026-05-12 Honest R&D Continuation - Crab IK Hardening

Status: PENDING VERIFICATION. This pass hardened the in-domain fauna presentation lane and did not claim global compile success.

What was wrong:
- The editor previously surfaced stale `ProceduralCrabLegIKRuntime.cs` compile errors; current disk validation was clean, but code audit exposed a real R&D debt in the same fauna lane.
- `OnOriginShift` scheduled rebase jobs and completed them immediately. That is a rare but real hitch source for whale-fall scavenger presentation.
- Crab pose input writes trusted upstream root position, rotation, velocity, dt, and scalar tuning before those values entered NativeArrays used by raycasts and indirect rendering.

What was done:
- Replaced live origin-shift forced completion with a queued finite shift offset. Pending rebases are applied after `DispatcherJobSwap` drains in `LateFrameTick()`.
- Skipped stale crab indirect upload on frames where a pending origin shift was applied after a completed IK job; next tick recomputes shifted matrices.
- Added finite guards for crab registration, pose updates, avoidance strength, dt, serialized scalar inputs, quaternion normalization, and telemetry writes.
- Added one-shot telemetry dump gating for invalid origin-shift or telemetry anomaly paths.

Cinematic cheats used:
- Kept whale-fall scavengers as data-only indirect crabs; no new crab GameObjects.
- Accepted a one-frame missing crab indirect draw during rare live-pipeline origin shifts instead of forcing a stall or simulating extra truth.
- Low-tier remains corpse shader crawl/noise fake; individual crab IK stays a higher-tier visual purchase.

Exact microseconds saved:
- Origin-shift forced sync avoided: estimated 80-180 us on i3/MX350 during rare floating-origin events.
- Native input vaccination cost: estimated <1 us per active pose update.
- GPU/render steady-state cost: no new draw calls, no new `SetData` path, no hot-path managed allocation.

Mandates followed:
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `ANIM_IK_FABRIK_GroundSnapping_Procedural.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

Verification:
- Unity MCP `validate_script` on `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs`: 0 diagnostics.
- Unity refresh recovered after editor disconnect/retry.
- Unity console after refresh: external blockers only:
  - `SaveBinaryStorage.cs(7667,41)` Burst `catch` + filter unsupported.
  - `HectonIndirectVegetationContracts.cs(145,17)` unassigned `out sporeEvent`.
- Static grep found no `SetData`, coroutine, `foreach`, `math.sqrt`, `Vector3.Distance`, or `.magnitude` matches in the crab IK file after the patch. Remaining job completion calls are dispatcher late-frame completion and non-blocking finalize-if-completed.

REGRESSION MODEL:
- CPU: Better during origin-shift events; normal Tick cadence unchanged.
- GC: No hot-path managed allocations added.
- Memory: No new native collections; two scalar fields added to the MonoBehaviour.
- Cadence: Live IK job still schedules in Tick and drains in LateFrame; queued origin shift prevents mid-pipeline mutation.
- Correctness: Invalid pose data now falls back before native/render writes. Risk: one frame of missing indirect crab draw after a live origin shift is intentional to avoid uploading stale pre-shift matrices.

# Rationale - RADIATION_HAZARD_SYS

Agent: THERMAL_ENGINEER
Domain: Combat & Survival Physiology / Radiation Scrubber
Prompt: RADIATION_HAZARD_SYS

## Decision 0 - Runtime Shape

Problem: Radiation hazards currently exist as gameplay-trigger components and broad hazard-zone math. The prompt requires a dedicated Jacobi diffusion grid, EventBus dose signaling, AUP anchoring, and telemetry.

Solution: Add a dedicated radiation grid runtime registered through GlobalRegistry, using fixed NativeArray buffers, Burst IJobParallelFor Jacobi diffusion, and EventBus dose output. Existing radiation source components will register mathematical sources into the grid instead of relying on trigger stay damage.

Rejected Alternatives: Unity trigger volumes and per-frame MonoBehaviour radius checks were rejected because they scale with collider traffic and do not provide deterministic fixed-grid state or postmortem telemetry. Direct Player.TakeDamage routing was rejected because the prompt mandates a radiation dose signal path.

Scalability potential: Low disables Jacobi and samples inverse-square against registered sources. Middle runs 32^3 FrostTick diffusion. High keeps full grid with stronger visual/audio response. Ultra uses saved cycles for denser source accumulation, shader mutation, and visor static without changing gameplay math.

Hardware Impact: On i3/MX350 class hardware, Low mode avoids 32,768-cell diffusion work and should remain effectively below 0.05 ms per FrostTick outside source registration. Full Jacobi is isolated to FrostTick and Native/Burst memory, avoiding managed GC.

## Decision 1 - Memory / Black Box

Problem: The system needs crash evidence for radiation state without creating runtime allocations or depending on chat reports.

Solution: Store the last 300 frames of high-level radiation state in a fixed NativeArray circular buffer and dump it to Docs/AgentLogs/Dump_RADIATION_HAZARD_SYS.bin on NaN detection or disposal after corruption.

Rejected Alternatives: Debug.Log spam, managed List telemetry, and Unity profiler-only evidence were rejected because they allocate, drop data, or disappear outside the editor.

Scalability potential: Telemetry is constant-size regardless of grid count; Low/Ultra modes write the same compact record.

Hardware Impact: One telemetry write per FrostTick/LateFrame update is negligible on low-end silicon and bounded at 300 entries.

## Decision 2 - Trigger Purge

Problem: RadiationHazard and EnvironmentalHazard exposed radiation through Unity collider/trigger semantics. That couples radiation to physics broadphase and cannot scale across wreck reactors.

Solution: RadiationHazard now registers a mathematical source into RadiationHazardGrid. EnvironmentalHazard keeps non-radiation behavior, but radiation exits before trigger/radius damage and registers a grid source.

Rejected Alternatives: Preserving trigger enter/exit as a notifier was rejected because the prompt requires mathematical sampling, not collider residence state. Physics.OverlapSphereNonAlloc was rejected for radiation because it remains per-zone sampling.

Scalability potential: Low/MX350 registers sources and samples inverse-square. Middle/High/Ultra feed the same source list into the Jacobi grid, so content authoring does not fork by tier.

Hardware Impact: On i3/MX350, removing radiation trigger residence avoids broadphase callbacks for every reactor zone. Expected saving depends on scene collider count, but hot-path per-zone trigger cost is eliminated.

## Decision 3 - Persistence Encoding

Problem: A 32^3 float grid is 131,072 bytes raw and would be wasteful in save payloads, especially when most cells are zero.

Solution: Persist only non-zero quantized cells as sparse RLE packets: ushort start index, byte value, ushort run length. Runtime grid remains float NativeArray; save payload uses byte quantization.

Rejected Alternatives: Saving raw floats was rejected for disk bloat. Saving managed dictionaries was rejected for GC and binary codec churn.

Scalability potential: Low tier often saves no grid packets. High/Ultra can afford richer source diffusion while persistence stays sparse and bounded.

Hardware Impact: Sparse RLE encode is cold save-time work. MX350 runtime does not pay this cost per frame.

## Decision 4 - Compile Wall

Problem: `dotnet build Hecton8.Core.csproj` and `dotnet build Assembly-CSharp.csproj` fail before radiation verification on missing Cartography namespace/types, missing VRAMMonitor references, and HectonNarrativeDirector interface implementation errors.

Solution: Record compile as blocked by dependency for loop 1 while continuing local scans and implementation. No radiation file appeared in the compiler error set.

Rejected Alternatives: Editing Cartography/VRAM/Narrative domains was rejected as out-of-domain sabotage for this prompt.

Scalability potential: No runtime impact; this is integration state tracking.

Hardware Impact: 0 us runtime.

## Decision 5 - Diffusion Consequences Path

Problem: Radiation needs to feel physical without putting an expensive simulation on the frame loop or creating cross-domain direct calls into audio, VFX, or damage systems.

Solution: Run the 32^3 Jacobi grid only on FrostTick, write cumulative dose into PlayerRuntimeContext, publish RadiationDoseSignal, and drive Geiger/static/hand mutation through data signals and global shader scalars. The hand mutation uses a scalar/tint signal so the shader can apply noise masking without CPU renderer edits.

Rejected Alternatives: Direct Player.TakeDamage, AudioSource clips, and renderer material instance edits were rejected because they create hidden coupling, allocations, or frame-time spikes. A fully physical particle/ray radiation simulation was rejected under the Cinematic Cheat Protocol.

Scalability potential: Low/MX350 uses inverse-square sampling and still emits dose/audio/VFX signals. Middle runs the grid. High/Ultra spend the same gameplay dose budget but can make the shader/static/audio response heavier without changing dose math.

Hardware Impact: MX350 avoids the 32,768-cell Jacobi pass entirely. Non-low tiers pay one Burst job per 5 seconds plus a few scalar writes; estimated hot-frame cost remains 0 us because the work is not on render Update.

## Decision 6 - Consequences / Persistence Binding

Problem: Dose must affect survivability, anti-rad items, visor static, AUP shifts, and save/load without turning RadiationHazardGrid into a god-object that owns unrelated systems.

Solution: Use existing survival fatigue math for max HP, expose the state through `PlayerRuntimeContext` and `SurvivalStatusMasks`, consume iodine via `ItemAcquiredSignal`, push static through visor shader globals, keep world positions as AUPs across origin shifts, and serialize only sparse quantized RLE bytes into the binary save payload.

Rejected Alternatives: Direct inventory references, direct visor renderer references, and raw float-grid save blocks were rejected. Recomputing runtime float origins on AUP shift was rejected because AUP storage already represents the logical world; rebasing would corrupt absolute samples.

Scalability potential: Low/Middle/High/Ultra share the same survival and save semantics. Low emits cheap scalar static and empty/sparse RLE; Ultra can turn the same shader scalar into heavier visor noise and hand mutation.

Hardware Impact: Low-end silicon pays no render-frame logic; iodine and save encode are event/cold paths. RLE turns a 131,072-byte raw grid into sparse 5-byte packets, with zero cost during ordinary frame updates.

## Decision 7 - Low-Tier Lie / Compile Wall

Problem: MX350 hardware cannot justify a 32^3 diffusion pass for a hazard that can be faked near reactor sources. At the same time, project-wide compile verification is blocked before the radiation job can be validated by the normal build.

Solution: Low/MX350/Unknown tiers skip Jacobi entirely and sample registered source AUPs with inverse-square distance. The Jacobi job remains Burst-only for non-low tiers. Task 17 is recorded as blocked after repeated builds fail in Core.Memory, Cartography, Physics.Determinism, and PDA/UI dependencies outside the radiation domain.

Rejected Alternatives: A half-resolution Jacobi grid was rejected because the prompt explicitly asks for inverse-square on low tier. Fixing unrelated missing assemblies/types was rejected as domain sabotage under the current assignment.

Scalability potential: Low uses the cheap scalar lie. Middle runs the exact 32^3 grid. High/Ultra can keep the same dose grid while spending extra visual budget on shader mutation/static intensity.

Hardware Impact: MX350 path removes the 32,768-cell FrostTick job, replacing it with at most 64 distance-squared source samples. Estimated low-tier FrostTick cost remains below 0.05 ms in ordinary source counts.

## OMEGA POLISH CHANGES

Problem: OMEGA audit found bootstrap bloat: the grid could scene-search with `FindObjectOfType`, spawn a hidden runtime object, and call `DontDestroyOnLoad`. That violated the explicit runtime ownership model and made source registration depend on an implicit manager.

Solution: Removed the scene-search/autospawn path and the remaining private runtime cache. Radiation source registration now pushes `RadiationSourceSignal` through `SignalBus<RadiationSourceSignal>`. External radiation dose publishes `RadiationDoseSignal` directly. The grid drains the source signal snapshot on Frost/LateFrame and updates its NativeArray source list only when an explicit grid owner is present. Geiger jitter threshold was reduced from `math.lerp` to `0.35f + intensity * 0.60f`. RLE was rechecked: float cells quantize to byte values before 5-byte sparse packets are written.

Rejected Alternatives: A hidden singleton/runtime creator was rejected because it hides execution order and violates the registry/event corridor. A half-resolution Jacobi grid for MX350 was rejected because the prompt mandates the inverse-square lie. Direct renderer/material mutation for hand damage and full-screen static was rejected because shader globals are cheaper and keep VFX ownership out of physiology.

Scalability potential: Low/MX350 disables Jacobi and samples inverse-square against registered sources. Middle runs the 32^3 FrostTick grid. High keeps the same deterministic dose grid and spends more response budget on Geiger cadence, visor static, and shader mutation. Ultra can overdrive visual noise and hand shader effects without changing gameplay math.

Hardware Impact: Low-end silicon saves the 32,768-cell diffusion pass and replaces it with at most 64 distance-squared source samples per FrostTick. Non-low tiers keep Jacobi off the render frame. Source registration is now an EventBus drain, not a scene search or singleton lookup. Exact saved estimate: 30-80 us cold startup scene-search avoidance in populated scenes, 0 us render-frame cost, and roughly 35-70 us per MX350 FrostTick by skipping Jacobi under ordinary source counts.

Dear Lie Audit: The radiation field is honest only on non-low tiers. Low uses inverse-square; hand mutation is a shader scalar/tint/noise mask; visor static is a scalar global; Geiger is an LCG cadence signal, not spawned audio clips. No particle radiation, no proton simulation, no collider-zone residence.

Zero-GC Audit: Radiation-owned hot paths use `for` loops, fixed NativeArrays, and value-type signals. No `foreach`, `.ToString()`, `string.Format`, string interpolation, `math.sqrt`, `Mathf.Sqrt`, `Vector3.Distance`, or `math.length` remain in `RadiationHazardGrid.cs`. The only radiation-owned `new byte[]` is the cold save DTO/RLE buffer path. Existing SaveData dictionaries/lists and survival CSV parser allocations are pre-existing/shared cold paths, not radiation hot-path code.

Cross-Domain Justification: `GlobalSignals.cs` was touched to add `RadiationDoseSignal` and `RadiationSourceSignal` so damage/source registration uses the EventBus corridor. `PlayerRuntimeContext`, `HectonSurvivalSystem`, `HectonPlayerHealth`, and `SurvivalStatusMasks` were touched because radiation is a survival physiology state with max-HP penalty. `SaveData` and `SaveBinaryPayloadCodec` were touched because the prompt explicitly required binary RLE persistence.

Final Git Diff: Radiation-relevant changes are `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs`, `RadiationHazard.cs`, `HectonHazardSource.cs`, `EnvironmentalHazard.cs`, `HectonPlayerHealth.cs`, `HectonSurvivalSystem.cs`, `Core/PlayerRuntimeContext.cs`, `Gameplay/SurvivalStatusMasks.cs`, `Core/GlobalSignals.cs`, `SaveData.cs`, and `SaveBinaryPayloadCodec.cs`. The working tree contains concurrent shared-file edits from other agents, so broad `git diff` is not a clean ownership report.

Build Status: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` still fails before a radiation-specific Burst verdict. Current blockers are missing `Hecton8.Core.Memory`/`IDataVault`/`SystemID`, missing `Hecton8.Cartography` types, missing `Hecton8.Physics.Determinism`, and PDA/UI map dependencies. No compiler error reported a radiation file in the final build attempt.

## Continuation Audit - 2026-05-13

Problem: Radiation sources registered through `HectonHazardSource` were still entering `HectonHazardManager` before also registering into `RadiationHazardGrid`. Survival could therefore read local reactor radiation from the old manager while the grid also applied local dose.

Solution: Branch `HectonHazardSource.InternalUpdateRegistry()` before legacy registration. Radiation unregisters from `HectonHazardManager`, publishes the grid source signal, and returns. Non-radiation hazards unregister from the grid and keep the old hazard manager path.

Rejected Alternatives: Leaving both paths active was rejected because it double-counts radiation. Removing `ResolveHazardIntensity(HazardType.Radiation)` from survival was rejected because legacy/out-of-domain hazard zones may still rely on the manager until they are migrated.

Scalability potential: Low/MX350 still uses inverse-square source sampling from the grid. Middle/High/Ultra keep grid diffusion without duplicate physiology penalties.

Hardware Impact: Removes one duplicated legacy radiation contribution. Expected CPU gain is small per source, but correctness gain is direct: local reactor dose is applied once.

Problem: The grid drained `SignalBus` snapshots in both FrostTick and LateFrame. The frame guard was set even when the first call saw an empty snapshot, which could starve a later same-frame valid snapshot depending on dispatcher flush order.

Solution: Snapshot length is checked before setting the frame guard. Non-empty snapshots are still processed once per frame; empty snapshots do not poison the frame.

Rejected Alternatives: Destructive `TryDequeueItemAcquired`/`TryDequeueRadiationDose` was rejected because it would starve other consumers. Removing LateFrame drains was rejected because it would increase event latency.

Scalability potential: Low through Ultra share the same signal path. The work is a length branch and only loops when snapshots exist.

Hardware Impact: Adds negligible branch cost; prevents missed iodine/source/external dose events without allocations.

Problem: The radiation penalty mask could become sticky after iodine or long decay because grid code only ORed `SurvivalStatusMasks.RadiationPenalty`.

Solution: Dose application now clears the bit when the penalty is below threshold. Player health exposure is still set exactly through `SetRadiationExposure`.

Rejected Alternatives: Waiting for `HectonSurvivalSystem.RefreshSurvivalStatusMask()` was rejected because the grid can update physiology state between survival publishes.

Scalability potential: Status mask remains exact on cheap and high-end devices; visual overkill can read the same clean state.

Hardware Impact: One bitwise clear branch on FrostTick/iodine event; no render-frame cost.

Problem: Survival `OnRadiationChanged` reported atmosphere plus legacy manager radiation, but grid-owned reactor intensity no longer appears in the manager after the double-count fix.

Solution: Publish the max of legacy radiation and finite `PlayerRuntimeContext.RadiationIntensity01`. This exposes grid intensity to subscribers without feeding it back into survival damage math.

Rejected Alternatives: Summing grid and legacy values was rejected because legacy values may already represent non-migrated radiation. Reapplying grid intensity in `HandleRadiation` was rejected because the grid already owns local dose accumulation.

Scalability potential: Low-tier inverse-square and high-tier Jacobi both publish the same scalar to survival events.

Hardware Impact: One finite check and `math.max` during survival event publication; 0 us render-frame impact.

Continuation Verification: User explicitly forbade `dotnet build` during this continuation. Verification used static scans only: no targeted radiation `RadiationManager.Instance`, `Player.TakeDamage`, radiation `OnTriggerStay`, destructive item/radiation dequeue use, scene-search/autospawn residue, or `CompleteDiffusionJobForTeardown` remains in radiation-owned paths. The only `.Complete()` calls left in `RadiationHazardGrid` are finished-job swap and cold save/load readback.

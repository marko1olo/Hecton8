# Status - RADIATION_HAZARD_SYS

Agent: THERMAL_ENGINEER
Domain: Combat & Survival Physiology / Radiation Scrubber
Prompt: RADIATION_HAZARD_SYS
Task Count: 18
Batch: Docs/Tasks/CURRENT_BATCH.md

## Mandates Read Before Coding

- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt

## Loop 0 - Extraction / Recon

- [x] Extracted XML prompt from CURRENT_BATCH.md using CLI regex.
  - DOD practice: strict prompt-isolation extraction by id.
  - Rejected alternative: IDE tab memory or broad MCP read.
  - Estimated cost: 42 us one-time CLI parse; 0 us runtime.
- [x] Confirmed authoritative domain from Docs/Actual Domains of Project.txt.
  - DOD practice: domain boundary validation before edits.
  - Rejected alternative: guessing from class names.
  - Estimated cost: 18 us one-time CLI read; 0 us runtime.

## Core Task Checklist

- [x] 01. Scan Assets/_Project/Scripts/World/Hazards. Delete RadiationManager.Instance.
  - DOD practice: CLI scan of declared path plus full scripts tree for RadiationManager.Instance.
  - Rejected alternative: blind delete by assumed folder name; declared folder does not exist.
  - Estimated cost: 0 us runtime; 1.3 s one-time rg scan.
- [x] 02. Radiation damage must not call Player.TakeDamage; push RadiationDoseSignal to EventBus.
  - DOD practice: direct integrity radiation path now accumulates dose and publishes RadiationDoseSignal.
  - Rejected alternative: Player.TakeDamage/HectonPlayerHealth.TakeDamage routing; it hides cumulative poison state.
  - Estimated cost: 4-8 us per emitted signal on main thread queue path.
- [BLOCKED BY DEPENDENCY] 03. Hecton8.Thermodynamics.asmdef must link to Contracts, not Core monolith.
  - DOD practice: asmdef inventory scan across Assets/_Project.
  - Rejected alternative: creating a new thermodynamics asmdef without ownership; no Hecton8.Thermodynamics.asmdef exists to patch.
  - Estimated cost: 0 us runtime.
- [x] 04. Delete all OnTriggerStay radiation components. Radiation purely mathematical sampling.
  - DOD practice: radiation trigger source was replaced by math source registration; EnvironmentalHazard radiation exits before trigger/radius damage.
  - Rejected alternative: keeping trigger enter/exit notifiers; still collider-coupled and not a grid sample.
  - Estimated cost: removes collider stay/path cost; grid source registration is cold or low-rate.
- [x] 05. NativeArray<float> 32x32x32 mapped to Wreckage AUP.
  - DOD practice: fixed 32^3 NativeArray read/write/source buffers, first wreck/source AUP as grid origin.
  - Rejected alternative: managed 3D float array; it violates zero-GC and Burst job requirements.
  - Estimated cost: 384 KB float grids plus bounded source/telemetry native buffers; 0 GC in runtime sampling.
- [x] 06. Burst Jacobi diffusion on FrostTick 0.2Hz. Next=(Self+Neighbors)*0.16f.
  - DOD practice: `RadiationJacobiDiffusionJob` is `[BurstCompile]`, scheduled from FrostTick, and uses the mandated 7-sample `0.16f` stencil.
  - Rejected alternative: per-source `Update()` falloff clouds; that would burn render-frame CPU and produce no stable grid state.
  - Estimated cost: 32,768 cells per FrostTick on non-low tiers; 0 us per render frame.
- [x] 07. Sample grid at player AUP, add to PlayerRuntimeContext.RadiationDose.
  - DOD practice: player runtime position converts to AUP, samples nearest grid cell, and writes `PlayerRuntimeContext.RadiationDose`.
  - Rejected alternative: storing trigger residency seconds in the player; it does not reflect world-space diffusion.
  - Estimated cost: 1 AUP conversion plus one NativeArray read per FrostTick.
- [x] 08. Decay dose every FrostTick: RadiationDose *= 0.999f.
  - DOD practice: accumulated dose is updated as `(dose + add) * 0.999f` once per FrostTick.
  - Rejected alternative: frame-time exponential decay; unnecessary precision and worse determinism for a slow poison scalar.
  - Estimated cost: 1 multiply per FrostTick.
- [x] 09. Geiger counter audio via AcousticPingSignal(Geiger) with LCG randomized clicks.
  - DOD practice: intensity advances a scalar click phase and publishes `AcousticPingSignal` with LCG jitter through GlobalSignals.
  - Rejected alternative: AudioSource/clip spawning; allocates and crosses DSP ownership.
  - Estimated cost: 1 LCG step only when a click candidate is due; otherwise scalar accumulation only.
- [x] 10. Visual mutation hands: pass RadiationDose to first-person hand shader globals.
  - DOD practice: dose and mutation scalars publish as global shader properties, including sickly green tint data.
  - Rejected alternative: material instance mutation on hand renderers; that risks allocations and breaks batching.
  - Estimated cost: six shader-global writes per FrostTick.
- [x] 11. Max HP penalty from RadiationDose, expose via SurvivalStatusMasks.
  - DOD practice: dose resolves through existing radiation fatigue math, applies exact max-health scale, and sets `SurvivalStatusMasks.RadiationPenalty`.
  - Rejected alternative: separate radiation HP field; it would fork survival math and desync HUD masks.
  - Estimated cost: one saturate/fatigue-scale path per FrostTick.
- [x] 12. Anti-rad consumable: consume ItemAcquiredSignal(Iodine), subtract 50 dose.
  - DOD practice: Frost/LateFrame queue drain checks FNV-1A iodine hashes and subtracts `50.0f * quantity` from accumulated dose.
  - Rejected alternative: inventory direct reference or polling item stacks; it violates EventBus decoupling.
  - Estimated cost: queue drain only when item signals exist; one hash compare pair per signal.
- [x] 13. Screen static VFX if grid cell >0.5: push scalar to HectonVisorUberPost.
  - DOD practice: intensity above `0.5f` pushes `_HectonVisualStaticGlitch` / seed globals consumed by the visor post shader path.
  - Rejected alternative: spawning a full-screen UI noise overlay; it adds Canvas work and new draw ownership.
  - Estimated cost: two shader-global writes per FrostTick when visuals update.
- [x] 14. AUP shift sync: grid tied to world/wrecks; shift logical AUP origin natively.
  - DOD practice: grid origin and sources are stored as AbsoluteUniversePosition data, so floating-origin shifts do not move logical world coordinates; listener completes active jobs and records shift sequence telemetry.
  - Rejected alternative: rebasing float grid origins by runtime offsets; that would double-apply shifts against AUP-backed samples.
  - Estimated cost: 0 us for ordinary shifts unless a job must be completed before the shift record.
- [x] 15. RLE save delta: non-zero grid cells, sbyte quantization, SaveBinaryStorage payload.
  - DOD practice: non-zero grid cells quantize float intensity to byte-range packets and `SaveBinaryPayloadCodec` writes the RLE slice into the binary save payload.
  - Rejected alternative: writing the raw 32^3 float grid; it costs 131,072 bytes before compression and violates the save delta intent.
  - Estimated cost: cold save-time scan of 32,768 cells; 0 us runtime frame cost.
- [x] 16. Math LOD: Low/MX350 disables Jacobi, inverse-square player/reactor fallback.
  - DOD practice: Low/MX350/Unknown branch skips `ScheduleDiffusionJobIfIdle()` and samples active sources with inverse-square AUP distance.
  - Rejected alternative: running reduced-grid Jacobi on MX350; it still spends job overhead where the prompt mandates the cheap lie.
  - Estimated cost: up to 64 source samples per FrostTick; no 32,768-cell diffusion job.
- [BLOCKED BY DEPENDENCY] 17. Verify Burst compilation.
  - DOD practice: repeated `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` attempts plus static scan of `[BurstCompile] IJobParallelFor`.
  - Rejected alternative: editing Core.Memory, Cartography, Physics.Determinism, or PDA/UI domains to force the build forward.
  - Estimated cost: 0 us runtime; build does not reach a radiation-specific compiler verdict.
- [x] 18. Telemetry: write AccumulatedRads to Blackbox.
  - DOD practice: fixed 300-entry NativeArray ring records player runtime position, intensity, max-HP penalty, source/grid versions, flags, and `AccumulatedRads`; NaN path dumps to `Docs/AgentLogs/Dump_RADIATION_HAZARD_SYS.bin`.
  - Rejected alternative: Debug.Log or managed List history; both violate the black-box evidence requirement.
  - Estimated cost: one struct write per FrostTick/shift record.

## Loop Log

- Loop 1 complete: tasks 01-05 implemented; compile blocked by pre-existing Cartography/VRAM/Narrative dependency errors outside radiation files.
- Loop 2 complete: tasks 06-10 implemented; scan verified Burst job, dose decay, Geiger signal, and shader globals. Compile still blocked by pre-existing/out-of-domain Cartography, Determinism/InputSignal, and UI LabelSwapScheduler dependencies before radiation-specific verification.
- Loop 3 complete: tasks 11-15 implemented; scan verified fatigue/mask, iodine subtract, visor static globals, AUP listener, and quantized RLE binary payload. Compile remains blocked by the same out-of-domain project dependency errors.
- Loop 4 complete: tasks 16 and 18 implemented; task 17 blocked by dependency after repeated builds. Static scan verified low-tier inverse-square path, Burst job annotation, and black-box `AccumulatedRads` dump path.
- Loop 5 complete: recursive prompt re-read and OMEGA polish ran. Removed scene-search/autospawn bootstrap and the remaining private runtime cache from the grid; source registration now routes through `RadiationSourceSignal` on `SignalBus`, while external radiation publishes dose directly. RLE verified byte quantized before compression. Final build remains blocked by out-of-domain Core.Memory, Cartography, Physics.Determinism, and PDA/UI dependency errors before radiation-specific Burst verification.

## Final Status

PENDING VERIFICATION - core tasks complete or dependency-blocked; project compile is red outside the radiation domain.

## Continuation Audit - 2026-05-13

- [x] Re-extracted `RADIATION_HAZARD_SYS` from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex with full tag attributes.
  - DOD practice: prompt re-read after continuation work, not chat-memory reconstruction.
  - Rejected alternative: relying on the previous compressed summary.
  - Estimated cost: 0 us runtime; one cold CLI parse.
- [x] Prevented radiation source double-counting through `HectonHazardManager`.
  - DOD practice: `HectonHazardSource` now routes `HazardType.Radiation` to `RadiationHazardGrid` and unregisters the legacy hazard-manager entry.
  - Rejected alternative: summing legacy manager radiation and grid radiation; it double-applies reactor exposure.
  - Estimated cost: 0 us render-frame; removes one legacy hazard lookup contribution per radiation source.
- [x] Hardened Frost/LateFrame signal drains.
  - DOD practice: item/source/dose drains now check snapshot length before setting same-frame guards, so an empty pre-flush call cannot starve a later valid snapshot.
  - Rejected alternative: destructive `TryDequeue*` reads or unconditional frame guards.
  - Estimated cost: one span length branch per drain; 0 GC.
- [x] Fixed radiation penalty mask stickiness.
  - DOD practice: grid dose application now clears `SurvivalStatusMasks.RadiationPenalty` when penalty returns to zero.
  - Rejected alternative: OR-only status writes; iodine/decay would leave stale HUD/physiology state.
  - Estimated cost: one bitwise clear branch on FrostTick/iodine event.
- [x] Published grid intensity through the survival radiation event without adding duplicate dose.
  - DOD practice: `OnRadiationChanged` now reports max of legacy atmosphere/manager radiation and finite grid intensity.
  - Rejected alternative: refeeding grid intensity into survival damage math; it would double-count local reactor dose.
  - Estimated cost: one finite check plus `math.max` when survival events publish.
- [x] Verification constrained to static scans by user order.
  - DOD practice: scanned for `RadiationManager.Instance`, `Player.TakeDamage`, radiation `OnTriggerStay`, destructive radiation/item dequeue use, scene search/autospawn residue, and hot-path math violations.
  - Rejected alternative: launching `dotnet build`; user explicitly forbade it in this continuation.
  - Estimated cost: 0 us runtime; no build executed.

Continuation Status: PENDING VERIFICATION - static scans passed for the targeted radiation checks; compile/Burst verification intentionally not rerun.

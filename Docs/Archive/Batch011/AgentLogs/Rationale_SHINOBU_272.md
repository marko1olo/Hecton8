# Rationale_SHINOBU_272

Agent: SHINOBU_272
Role: PHYSIOLOGICAL_GAS_TOXICITY_SOLVER
Status: PENDING VERIFICATION

## Decision 000 - Bootstrap Discipline

Problem: The task requires a physiology authority, but the actual project may already contain health, oxygen, or status systems. Creating a parallel solver without archaeology would create conflicting gameplay truth.
Solution: Read AGENTS.md, the SHINOBU_272 XML block, domain boundaries, and eight relevant mandates before code. Create status/rationale files as disk memory before runtime edits.
Rejected Alternatives: Directly generating new physiology classes from prompt text was rejected because HECTON-8 requires one fact -> one owner -> one route -> one proof artifact.
Scalability potential: Low uses existing authority paths and one unmanaged DTO path; Middle adds deterministic slow cadence; High/Ultra can spend saved CPU on visual symptoms and telemetry density without bloating gameplay truth.
Hardware Impact: Estimated 0 us runtime impact from bootstrap documentation; prevents duplicate systems that would waste CPU and memory on i3/MX350.

## Decision 001 - Gas Truth Route

Problem: Existing survival oxygen is a resource bar and pressure damage is direct integrity mutation; neither can represent Dalton partial pressures without becoming a second physiology authority.
Solution: Add explicit unmanaged GasPhysiologyStateDTO and BreathingGasFractionsDTO rows under Shinobu Physiology Vault ownership. Physiology jobs compute gas truth and publish PhysiologyStateSignal/CombatDamageSignal only.
Rejected Alternatives: Directly editing HectonPlayerHealth or HectonSurvivalSystem gas internals was rejected because health/survival are consumers or legacy presentation paths, not the new gas-tension authority.
Scalability potential: Low evaluates fewer Haldane compartments and slower cadence; Middle keeps full Dalton rows with partial tissue LOD; High/Ultra uses the same DTO to drive stronger visuals and full 16-compartment tension without changing gameplay ownership.
Hardware Impact: Estimated low-tier cost is 3-8 us per active player row at 0.2 s cadence; no managed allocations and no scene searches in jobs. i3/MX350 avoids duplicate oxygen systems and keeps damage routing event-based.

## Decision 002 - Mock Breathing Gas

Problem: The solver needs deterministic gas fractions before authored suit gas assets exist.
Solution: Generate mock breathing gas in Burst: normal air near surface, continuous air-to-heliox blend by depth, then Dalton partial pressures from ambient ATM.
Rejected Alternatives: Binary air/heliox quality switch was rejected because GlobalQualityWeight and gas selection must stay continuous and predictable. Random gas profiles were rejected because rollback and black-box telemetry require reproducible rows.
Scalability potential: Low gets cheap air/heliox lerp; Middle can override with CSV gas fractions; High/Ultra can spend saved N2 cycles on richer hypoxia/CNS presentation while using the same DTO.
Hardware Impact: Approx 1 us/entity, 32 B read/write, no GC. On i3/MX350 the mock generator is cheaper than object-based gas profile lookup.

## Decision 003 - Dalton And Tissue Math

Problem: Existing decompression math used ambient pressure as the tissue equilibrium target, which overstates nitrogen on heliox and cannot model gas mix changes.
Solution: Add CalculatePartialPressuresJob and make the Haldane integrator use nitrogen partial pressure as the tissue equilibrium. AUP depth remains resolved in double before float conversion in the existing environment seed.
Rejected Alternatives: Keeping ambient-pressure tissue saturation was rejected because it cannot distinguish air from heliox. Simulating oxygen tissue compartments was rejected for this pass because health truth only needs PPO2 availability and N2 tissue tension; adding 16 O2 compartments would spend CPU without gameplay proof.
Scalability potential: Low evaluates 4 active compartments plus the slowest compartment; Middle increases active compartments; High/Ultra reaches 16 compartments and can drive detailed dive-computer visuals from the same ring.
Hardware Impact: Estimated 2-6 us/entity per physiology tick depending quality. i3/MX350 keeps cadence at 0.2 s and low compartment count; high-end reaches 0.016 s cadence.

## Decision 004 - Toxicity Presentation And Damage

Problem: Hyperoxia, CO2 retention, hypoxia, and narcosis must affect the player without concrete cross-domain calls.
Solution: CalculateCnsToxicityJob writes gas flags/scalars, publishes PhysiologyStateSignal, and stages toxic CombatDamageSignal. Visual sync bridges hypoxia to `_HypoxiaSignal` and packs gas toxicity in `_HectonGasToxicityParams`.
Rejected Alternatives: Calling HectonPlayerHealth.TakeDamage from physiology was rejected. Mutating input state for narcosis was rejected; the input owner must consume PhysiologyStateSignal.Narcosis01.
Scalability potential: Low uses scalar tunnel/stamina fake; Middle uses narcosis + CNS scalar; High/Ultra can layer stronger post effects and audio from the same gas vector.
Hardware Impact: Toxicity math is approx 1 us/entity; damage signal is emitted only on severe toxic frames. No GC and no per-frame scene searches.

## Decision 005 - Legacy Bridge Without Ownership Theft

Problem: Some existing readers still use GlobalSignals.TryGetLatestPhysiologyStateSignal, while the Burst jobs publish through SignalBus parallel writers.
Solution: After job completion, publish one sanitized latest gas PhysiologyStateSignal from the main thread. HectonPlayerHealth reads both SignalBus snapshots and GlobalSignals latest, but only caches stress/toxicity scalars.
Rejected Alternatives: Moving HectonPlayerHealth into Physiology assembly or making health query DataVault was rejected because it creates a hot concrete dependency and violates owner boundaries.
Scalability potential: Low reads one latest signal; Middle/High/Ultra can consume snapshot lanes for richer UI/input without changing gas authority.
Hardware Impact: One main-thread signal publish per completed physiology tick. Estimated <1 us, no GC. i3/MX350 avoids direct Vault reads in health.

## Decision 006 - Black Box Scope

Problem: Fatal gas physiology must leave forensic state without allocating per-frame logs.
Solution: Reuse the 300-entry PhysiologyTelemetryEntry ring, hash PPO2/PPN2/PPCO2 into StateHash, preserve depth/highest tissue/supersaturation/execution microseconds, and dump to Docs/AgentLogs/Dump_SHINOBU_272.bin on fatal gas/NaN.
Rejected Alternatives: JSON/text telemetry was rejected because it allocates and is too slow during failure. A second gas-only ring was rejected because the existing physiology ring already owns the black-box route.
Scalability potential: Low records fixed high-level state only; Middle/High/Ultra can add visual readback from the same ring without increasing gameplay truth.
Hardware Impact: Fixed 64 B ring write per tick; dump only on fatal. No recurring IO on i3/MX350.

## Decision 007 - Tooling And CSV Boundaries

Problem: Gas profiles need designer override and live inspection, but runtime gas rows must stay unmanaged and allocation-free.
Solution: Use a cold CSV reader for `physiological_gas_profiles.csv` with FO2/FN2/FCO2 key support, then pass a sanitized BreathingGasFractionsDTO into the Burst generator. Editor windows and dev overlays read gas DTOs; they do not own runtime truth.
Rejected Alternatives: ScriptableObject lookup during simulation was rejected because it introduces managed references in the hot gas path. Creating a new debug overlay was rejected; the existing DCS dev overlay was extended.
Scalability potential: Low devices use mock gas or one CSV override; Middle supports tuned gas blends; High/Ultra can visualize more gas telemetry without changing DTO layout.
Hardware Impact: CSV has 0 hot-frame cost after parsing. Editor/dev visualization is excluded from player builds or development-only.

## Decision 008 - Continuous Quality Scaling

Problem: HECTON-8 rejects low/ultra binary toggles, but gas physiology can get expensive if every tissue compartment updates every frame.
Solution: Cadence uses `lerp(0.016, 0.2, 1 - GlobalQualityWeight)` and tissue compartment count is continuously derived from GlobalQualityWeight.
Rejected Alternatives: Hard disabling gas physiology on low-end was rejected because gameplay truth must remain authoritative. Full 16-compartment every frame on low-end was rejected because it wastes frame budget.
Scalability potential: Low = 0.2 s cadence, 4 active compartments plus slow sentinel; Middle = blended cadence/compartments; High = near-frame cadence; Ultra = full visual overkill from same state.
Hardware Impact: i3/MX350 avoids same-frame full physiology cost; high-end buys smoother toxicity visuals and narcosis feedback.

## Decision 009 - Verification Gate

Problem: The project forbids dotnet build when CPU is above 50% or csc.exe is running. Latest CPU preflight reported 100% and csc.exe count was 1.
Solution: Do not launch build. Run static diffs, direct-dependency scans, prompt re-read, and layout review; mark compile verification as blocked by machine load, not code wall.
Rejected Alternatives: Running dotnet build anyway was rejected because it violates the explicit project rule and risks starving other agents.
Scalability potential: Verification remains repeatable when CPU drops; no runtime design compromise was made.
Hardware Impact: Saved a high-contention build launch on an already saturated workstation.

## Decision 010 - Subagent Runtime Fence Repairs

Problem: Audit found managed CSV polling inside `Tick`, Vault writes before lock acquisition, unlocked tuning row mutation, and public live ref access to Vault vitals.
Solution: Removed CSV polling from the simulation tick and limited file ingestion to cold Vault initialization. Moved job buffer locking before environment seeding and NativeArray resolution. Added gas tuning and physiology tuning lanes to the lock set. Removed `GetVitalsRef`; editor/test injectors and readers now fail closed while a physiology job is scheduled.
Rejected Alternatives: Keeping hot-reload polling in `Tick` was rejected because managed file IO/date checks are not gameplay-frame work. Returning live mutable refs was rejected because it exposes Vault rows outside the owner phase and races jobs.
Scalability potential: Low devices avoid periodic IO spikes and lock contention during thermal throttling; Middle/High/Ultra keep identical gameplay truth and spend cycles only on the scheduled Burst chain.
Hardware Impact: Removes one managed IO poll per second from runtime frames and prevents race-induced stalls on i3/MX350. Expected saved spike is unbounded IO latency; steady simulation remains microsecond-scale.

## Decision 011 - Gas Tuning And Scanner Closure

Problem: Task 16 needed direct control over CNS/hypoxia gas thresholds, and Task 19 required a real `Physiology_OOP_Scanner` proof artifact.
Solution: Added 64-byte `GasPhysiologyTuningDTO` in Vault buffer 70215, editor sliders for CNS rate/narcosis/hypoxia/anoxia/CO2, CSV key ingestion for gas thresholds, and `Physiology_OOP_Scanner` with static mirror output in `Docs/Reports`.
Rejected Alternatives: ScriptableObject tuning was rejected because runtime jobs need unmanaged rows. A checklist-only scanner was rejected because it is not a proof artifact.
Scalability potential: Low keeps cheap scalar thresholds and slow cadence; Middle/High/Ultra can tune richer visual stress curves from the same DTO without layout or authority changes.
Hardware Impact: Gas tuning row is 64 bytes, read once per scheduled tick and copied into jobs by value. Static scanner has 0 player-build runtime impact.

## Decision 012 - Editor/Test Injector Fence Closure

Problem: A post-compaction code read found that pressure/gas/tuning readers rejected access during an active physiology job, but four mock editor/test injectors still resolved Vault lanes without checking `_jobScheduled`. These paths are not normal gameplay hot loops, yet they can race scheduled jobs during tooling or automated smoke tests.
Solution: Added fail-closed `_jobScheduled` guards to `InjectMockCombatDamage`, `InjectMockPredatorAggro`, `InjectMockToxemia`, and `InjectMockMedicalItem`, matching the existing pressure, hyperbaric, mock dive, mock breathing gas, tuning, telemetry, and gas-state read fences.
Rejected Alternatives: Locking inside each injector was rejected because those methods are cold tooling entry points and should not contend with the dispatcher-owned simulation chain. Leaving the race for editor-only use was rejected because QA smoke tools can run during play mode.
Scalability potential: Low devices avoid incidental editor/test writes competing with Burst jobs under thermal pressure; Middle/High/Ultra keep the same truth route and do not change DTO layout, cadence, or signal authority.
Hardware Impact: Runtime cost is one branch per cold injector call, 0 us on normal scheduled physiology ticks. Prevents rare race stalls or stale writes that are more expensive than the guard on i3/MX350-class hardware.

<SELF_AUDIT>
Agent: SHINOBU_272
DTO Layouts:
- GasPhysiologyStateDTO: 32 bytes; offsets O2=0, N2=4, CO2=8, CNS=12, Narcosis=16, StaminaDrain=20, Flags=24, pad=28.
- BreathingGasFractionsDTO: 32 bytes; offsets O2=0, N2=4, CO2=8, inert reserve=12, GasHash=16, Flags=20.
- PhysiologyTelemetryEntry: 64 bytes; fixed 300-entry ring retained.
Vault Buffer IDs:
- ShinobuBreathingGasFractions = 70214.
- ShinobuGasPhysiologyStates = 70239.
- Existing ShinobuPhysiologyTelemetryRing = 70226.
GC Audit:
- Hot jobs allocate 0 managed bytes; no `new[]`, List, Dictionary, StringBuilder, or direct health calls in SHINOBU physiology runtime/jobs scan.
- CSV parser is cold IO via existing scratch NativeArray; editor/dev UI allocations are outside runtime player path.
Alias/Fence Audit:
- Jobs use separate NativeArray lanes for vitals, scalars, gas states, breathing fractions, environment, telemetry, and tissue rows.
- Vault buffers are locked before scheduling and unlocked after completion.
- Damage route is unmanaged CombatDamageSignal; health bridge reads signals only.
Verification:
- `git diff --check` returned only line-ending warnings.
- Compile not run: CPU=100%, csc.exe=0, build prohibited by project rule.
</SELF_AUDIT>

## Decision 013 - Rendering Boundary And Safety Proof Closure

Problem: A subagent audit found Physiology writing `HectonShaderGlobalDataVaultBridge.PublishPhysiology*` directly, broad `NativeDisableContainerSafetyRestriction` on queue writers, and implicit padding holes inside `PhysiologyStateSignal`.
Solution: Physiology now publishes only unmanaged `PhysiologyStateSignal` and `HypoxiaSignal`. `GlobalShaderDispatcher` owns the shader projection for decompression/gas slots and writes `_HectonDcsPhysiologyParams`, `_HectonGasToxicityParams`, and `_HypoxiaSignal`. Queue writer fields no longer use broad container-safety disable. The tissue slice writer uses `NativeDisableParallelForRestriction` with a local three-part proof: one entity owns one fixed 16-compartment slice, the full slice is bounds-checked before unsafe ref mutation, and the runtime locks the Vault lane until the dispatcher completion fence. `PhysiologyStateSignal` now explicitly occupies former holes at offsets 18/19 with gas CNS/CO2 severity bytes and offset 54 with padding; layout validation checks the 64-byte contract and gas offsets.
Rejected Alternatives: Keeping Physiology-to-rendering shader calls was rejected because visual projection is rendering authority. Keeping broad `NativeDisableContainerSafetyRestriction` was rejected because it suppresses safety without proving a partition. Adding a new gas visual DTO in Rendering was rejected because it would create a sibling assembly dependency on Physiology-owned payloads.
Scalability potential: Low devices read one compact signal snapshot and update scalar shader slots without Vault gas DTO coupling; Middle/High/Ultra reuse the same signal bytes for richer shader curves without changing gameplay truth, BufferIDs, or rollback layout.
Hardware Impact: Removes a concrete cross-domain call path and preserves compile-wall isolation. Runtime projection is one signal snapshot scan in rendering VisualSync, estimated <2 us for the configured 64-signal lane on i3/MX350 and amortized away from physiology Burst ticks.

<SELF_AUDIT rev="4">
Agent: SHINOBU_272
Additional Loop 10 Findings:
- PASS: Hot `Tick()` no longer invokes allocation-capable `EnsureVaultState()`; it returns unless `_defaultsInitialized` and all generation handles are ready.
- PASS: Focused scan for `HectonShaderGlobalDataVaultBridge.PublishPhysiology*` under SHINOBU Physiology runtime/jobs/data returned 0 hits. Rendering owns shader slot projection from physiology signals.
- PASS: Focused scan for `NativeDisableContainerSafetyRestriction` in `ShinobuPhysiologyJobs.cs` and `ShinobuRespawnJobs.cs` returned 0 hits.
- PASS: `TissueCompartments` slice mutation uses `NativeDisableParallelForRestriction` with explicit ownership/bounds/fence safety proof.
- PASS: `PhysiologyStateSignal` remains 64 bytes and now has explicit gas visual bytes at offsets 18/19 plus explicit padding at offset 54; `ValidateTelemetryAndSignalLayouts()` checks these offsets.
- PASS: Shared `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` again contains `shinobu272PhysiologyOopScanner`, and both shared/sidecar scanner reports parse as JSON.
- PASS_STATIC_BUILD_BLOCKED: Build not launched because CPU preflight returned `CPU=100`, `csc.exe=0`, `dotnet=0`; project rule forbids build above 50% CPU.
Struct Layout Delta:
- `PhysiologyStateSignal` unchanged at 64 B. Offsets: PlayerStress01 f32@0, O2DrainMultiplier f32@4, Recovery01 f32@8, Frame u32@12, Cause u8@16, Flags u8@17, GasCnsSeverity u8@18, GasCarbonDioxideSeverity u8@19, Supersaturation01 f32@20, Narcosis01 f32@24, AmbientPressureAtm f32@28, NitrogenLoadAtm f32@32, AscentRate f32@36, TissueMask u32@40, SourceHash u32@44, EntityIndex i32@48, ActiveCompartments u8@52, FatalSeverity u8@53, pad u16@54, StatusFlags u32@56, pad u32@60.
Dear Lie Route:
- Before: Physiology VisualSync called the shader bridge directly. After: Physiology emits signal truth; Rendering VisualSync converts signal bytes into shader globals. Complexity remains O(signal lane) with a 64-row cap, and the CPU still avoids post-process volume/component mutation.
</SELF_AUDIT>

<SELF_AUDIT rev="2">
Agent: SHINOBU_272
Task Reconciliation:
- 01 PASS: active oxygen/health surfaces scanned; gas truth owned by Physiology Vault rows.
- 02 PASS: gas damage routes through unmanaged CombatDamageSignal, not direct health mutation.
- 03 PASS: hot DTOs use raw fields; no get/set properties in Physiology DTO scan.
- 04 PASS: GasPhysiologyStateDTO 32 B and GasPhysiologyTuningDTO 64 B offsets validated by editor guard.
- 05 PASS: deterministic mock air-to-heliox breathing gas generator exists.
- 06 PASS: Dalton job writes PPO2/PPN2/PPCO2 from fractions * ambient ATM.
- 07 PASS: nitrogen tissue tensions integrate toward PPN2 with Haldane exponential; oxygen availability feeds blood oxygen scalar.
- 08 PASS: CNS toxicity integrates above PPO2 threshold with exponential extreme branch and tunable recovery.
- 09 PASS: narcosis scalar derives from PPN2 and publishes via PhysiologyStateSignal.
- 10 PASS: hypoxia tunnel is a shader scalar, not a CPU post-process mutation.
- 11 PASS: severe gas stress emits CombatDamageSignal with toxic damage type through SignalBus writer.
- 12 PASS: cadence = lerp(0.016, 0.2, 1 - GlobalQualityWeight); compartment count is quality-derived.
- 13 PASS: depth route subtracts sea-level AUP from player AUP in double before float pressure math.
- 14 PASS: DTOs are blittable and jobs use deterministic Burst float mode.
- 15 PASS: 300-entry telemetry ring hashes gas pressures and dumps to Dump_SHINOBU_272.bin on fatal/NaN.
- 16 PASS: UI Toolkit tuner writes Vault gas tuning sliders without C# recompile.
- 17 PASS: CSV ingestion is cold only and parses gas fractions plus gas threshold keys via ReadOnlySpan<byte>.
- 18 PASS: dev-only dive overlay displays gas state and tissue bars.
- 19 PASS: Physiology_OOP_Scanner exists; static mirror report summary is "OOP Physiology Triggers Purged" with 0 findings.
- 20 PASS_STATIC_BUILD_BLOCKED: lock order, aliasing, DTO layout, and reports audited; compile not run under CPU gate.
Struct Layout Verification:
- GasPhysiologyStateDTO = 32 B: 0 O2 f32, 4 N2 f32, 8 CO2 f32, 12 CNS f32, 16 Narcosis f32, 20 StaminaDrain f32, 24 Flags u32, 28 pad u32.
- BreathingGasFractionsDTO = 32 B: 0 O2 f32, 4 N2 f32, 8 CO2 f32, 12 inert f32, 16 GasHash u32, 20 Flags u32, 24 pad u32, 28 pad u32.
- GasPhysiologyTuningDTO = 64 B: sixteen 4-byte lanes from HypoxiaPPO2 offset 0 through Version offset 60; exactly one L1 cache line.
Scalability Curve:
- Quality below 0.3 lengthens physiology cadence toward 0.2 s and evaluates only the low active tissue set plus sentinel slow tissue; Dalton and toxicity scalars remain authoritative. Higher quality smoothly restores cadence toward 0.016 s and full 16 tissue compartments. Visual intensity is shader-driven from the same scalars.
H-PHI Vault Status:
- No private persistent NativeArray ownership was added. Buffers: 70214 breathing gas fractions, 70215 gas tuning, 70239 gas physiology states, existing 70226 telemetry ring, existing Physiology tuning/scalars/vitals lanes.
Pointer Aliasing And Dependency Graph:
- Job chain: MockEnvironmentDrop -> GenerateMockBreathingGas -> CalculatePartialPressures -> PhysiologySignalIngest -> IntegrateBloodGasTensions -> CalculateCnsToxicity -> OxygenConsumption. The returned JobHandle is registered with H8Memory and finalized through DispatcherJobFence.
- Non-overlapping NativeArray fields carry NoAlias; NativeQueue writers carry WriteOnly + NoAlias. Tissue slice mutation uses the narrower NativeDisableParallelForRestriction with an explicit slice/bounds/fence proof before unsafe ref writes.
Compile Guard:
- Physiology does not reference HectonPlayerHealth. Health consumes named PhysiologyStateSignal constants. Runtime no longer initializes the CombatDamage SignalBus lane; Core bootstrap owns that lane.
Dear Lie Confirmation:
- Hypoxia and gas toxicity use scalar shader globals/CBuffer slot 11, so suffocation visuals are O(1) scalar publish instead of CPU-side post-process object mutation. Before: per-frame managed trigger/post volume path risk O(objects/components). After: O(1) slot write plus GPU shader curve.
Verification:
- `git diff --check` focused pass reports line-ending warnings only.
- `Physiology_OOP_Scanner.StaticMirror` findings = 0.
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` parses as valid JSON after SHINOBU_272 section upsert.
- Compile not run: CPU=100%, csc.exe=1.
</SELF_AUDIT>

<SELF_AUDIT rev="3">
Agent: SHINOBU_272
Additional Loop 9 Findings:
- PASS: Mock combat, predator, toxemia, and medical item injectors now fail closed while `_jobScheduled` is true.
- PASS: Focused scan for Physiology DTO `{ get; set; }` and `{ get; private set; }` returned no hits.
- PASS: Focused runtime scan for `UnityEngine.Random`, direct `TakeDamage`, hard depth damage, and `HectonPlayerHealth` references under Physiology returned no hits.
- PASS: Shared and sidecar physics report JSON still parse with `ConvertFrom-Json`.
- PASS_STATIC_BUILD_BLOCKED: Build not launched because the latest CPU preflight returned `CPU=100`, `csc.exe=0`, `dotnet=1`; project rule forbids build above 50% CPU or while another dotnet process is active.
Fence Detail:
- Cold injector writes are guarded before DataVault resolve. Scheduled jobs still own locks from `TryLockJobBuffers` through `FinishFrameJobCompletion`, and `_jobScheduled` remains true until publish/dump/visual-sync and `UnlockJobBuffers` finish.
</SELF_AUDIT>

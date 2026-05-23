# SHINOBU_324 Rationale

Status: POLISH LOOP 21 HOT PATH VAULT INIT FENCE / CORE BUILD BLOCKED EXTERNAL
Evidence: STATIC_SOURCE plus guarded narrow Core build after Loop 9. Unity import/profiler/player-build evidence is absent; latest Core build fails in external PlayerKinematics/VRSomatic/CombatDamage/KCC contract coverage, not SHINOBU_324 files.

## Decision 00: Initial Route

Problem: Radiation mutation must affect stamina and hand deformation without OOP material swaps, per-arm particle prefabs, or hot-path registry polling.
Solution: Use data-only mutation DTOs, deterministic jobs for dose-to-severity mapping, and shader-side presentation through scalar globals/bridge slot 22. Gameplay truth remains physiology data; body horror is a Dear Lie.
Rejected Alternatives: `SkinnedMeshRenderer.materials` edits allocate material arrays and break batching; particle prefabs attached to bones create GameObjects and managed lifecycle churn; direct concrete references to unfinished systems create compile-wall risk under parallel agents.
Scalability potential: Low uses bounded scalar updates; Middle adds pulse cadence; High admits richer procedural shader response; Ultra spends saved CPU/GPU budget on stronger vertex displacement without changing truth ownership.
Hardware Impact: i3/MX350 estimate is avoiding material clone stalls and prefab churn; static estimate 1.2 ms-class spike avoidance, not profiler-measured.

## Decision 01: Radiation Source Contract

Problem: Prompt asks for attenuation from radiation sources, but the source storage inside `RadiationHazardGrid` is private and owned by a prior agent.
Solution: Read the public Core.Contracts `RadiationStateDTO` Vault buffer (`Shinobu274RadiationStates`) and derive attenuated dose from cumulative dose, current exposure rate, and shielding factor. Add SHINOBU_324 mock-dose buffer for emergency testing only.
Rejected Alternatives: Reflecting private source buffers, scene-searching radiation emitters, or inventing a duplicate source owner.
Scalability potential: Low reads one player row; Middle/High/Ultra can extend the same DTO contract to more rows without touching renderer state.
Hardware Impact: i3/MX350 avoids source object traversal and managed lookup; estimated 30-80 us saved per slow tick versus scene-side source enumeration.

## Decision 02: DTO And ARM64 Layout

Problem: Mutation state must be Burst/NativeArray-safe and stable under ARM64 alignment.
Solution: Use explicit-layout unmanaged DTOs: `MutationStateDTO` 32 bytes, tuning 64 bytes, profile 32 bytes, telemetry 64 bytes, guarded by reflection/UnsafeUtility layout validation.
Rejected Alternatives: managed profile classes, auto-layout structs, bool fields, and reference fields.
Scalability potential: Low keeps single-row buffers cache-local; Ultra can batch more entities without changing ABI.
Hardware Impact: i3/MX350 benefits from contiguous rows and no managed dereferences; estimated 10-35 us saved over managed object state at small counts.

## Decision 03: Stamina Corruption Boundary

Problem: The task asks to reduce max stamina, but `MetabolicStateDTO` has no max-stamina field and is not SHINOBU_324-owned.
Solution: Store `MaxStaminaPenalty` in SHINOBU_324 `MutationStateDTO`; bridge to metabolism only through existing toxicity scalar and fatigue/toxic flags with a guard bit.
Rejected Alternatives: Adding a max-stamina field to `MetabolicStateDTO`, writing an unowned DTO layout, or using an OOP player-stats component.
Scalability potential: Low consumes the penalty scalar directly; Middle/High/Ultra can bind more stamina UI/animation consumers without changing metabolism ABI.
Hardware Impact: i3/MX350 avoids cross-domain DTO churn and compile walls; estimated 15-25 us saved versus component lookup and property mutation.

## Decision 04: Shader Mutation Presentation

Problem: The visual body-horror effect must not swap hands, meshes, materials, or textures on CPU.
Solution: Publish `Vector4(severity, staminaPenalty, healingSuppression, quality)` through `HectonShaderGlobalDataVaultBridge` slot 22 and shader globals `_HectonRadiationMutationParams`/`_HectonHandRadiationMutation01`; `UberNoir` performs vertex displacement.
Rejected Alternatives: CPU arm mesh deformation, material replacement, blendshape object swaps, or shader keyword explosion.
Scalability potential: Low uses scalar displacement; Middle adds pulse; High and Ultra can spend shader cycles on stronger procedural deformation and blister blending.
Hardware Impact: i3/MX350 avoids CPU skin/mesh mutation and material clone stalls; estimated 60-120 us CPU saved per visible update, GPU cost remains continuous and quality-scaled.

## Decision 05: Toxic Blood Signal Lane

Problem: Radiation mutation needs toxic blood feedback without dynamic particle objects.
Solution: Emit `DebrisSpawnSignal` through `SignalBus<T>` with finite AUP-derived position from `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`.
Rejected Alternatives: `ParticleSystem` prefab spawning, `new GameObject`, transform polling, or renderer-attached emitters.
Scalability potential: Low throttles cadence; Middle/High increase signal count/intensity; Ultra can make downstream VFX overkill while SHINOBU_324 remains scalar.
Hardware Impact: i3/MX350 avoids prefab allocation and scene hierarchy updates; estimated 0.3-0.7 ms spike avoidance on mutation pulses.

## Decision 06: Black Box Telemetry

Problem: NaN or overbudget mutation math must leave a forensic artifact instead of a chat explanation.
Solution: Keep a fixed 300-row telemetry ring in Vault and dump raw bytes with a small header to `Docs/AgentLogs/Dump_SHINOBU_324.bin` on NaN/overbudget.
Rejected Alternatives: managed per-frame log strings, `BinaryWriter`, unlimited history, or no dump.
Scalability potential: Low keeps the same 300-row cap; Ultra can add more visual inputs while telemetry ABI stays fixed.
Hardware Impact: i3/MX350 hot path has fixed row writes only; dump is exceptional cold path.

## Decision 07: Editor Tooling And Metrics

Problem: Tuning/scanner/debug support is required but must not pollute runtime GC behavior.
Solution: Put tuner, gizmo, layout validator, and OOP scanner under `Assets/_Project/Scripts/Physiology/Editor`; persist scanner evidence to `Docs/Reports/RENDERING_OPTIMIZATION_REPORT_SHINOBU_324.json`.
Rejected Alternatives: runtime GUI, debug GameObjects, and chat-only validation.
Scalability potential: Low devices ship without editor tooling; high-end development can inspect severity live without touching runtime ABI.
Hardware Impact: No player runtime impact; editor-only cost is irrelevant to target frame budget.

## Decision 08: Compile Gate

Problem: Project law forbids launching `dotnet build` while CPU is above 50% or another `dotnet`/`csc` is active.
Solution: Ran the gate checks, observed CPU above 50% and active compiler processes (`dotnet` earlier, latest `VBCSCompiler`), and stopped at static validation.
Rejected Alternatives: Launching a competing build to create a fake green claim or ignoring active compiler load.
Scalability potential: Keeps parallel agent integration stable under heavy batch execution.
Hardware Impact: Prevents starving other compiler/import workers; compile proof remains pending, not fabricated.

## Decision 09: Contract Radiation DTO

Problem: SHINOBU_324 initially read `RadiationHazardGrid.RadiationStateDTO`, which created concrete gameplay type coupling even though the DataVault buffer is the real route.
Solution: Added `Hecton8.Core.Contracts.Physiology.RadiationStateDTO` and migrated SHINOBU_274 allocation plus SHINOBU_324 read handles to that shared ABI. BufferID `72740`, size 32, offsets, and owner remain unchanged.
Rejected Alternatives: Mirroring the struct under a different type was rejected because DataVault type hashes include the runtime type handle; unsafe reinterpretation would fail collections checks or hide ABI drift. Keeping the concrete nested type was rejected by compile-wall doctrine.
Scalability potential: Low/Middle/High/Ultra all consume the same contract DTO; more consumers can bind without referencing a gameplay MonoBehaviour class.
Hardware Impact: No runtime cost change; compile-wall risk reduced by moving identity to contracts and preserving one type hash.

## Decision 10: Phase Discipline And Tiny Job Removal

Problem: One-row `.Run()` wrappers for SHINOBU_324 violated the current doctrine rejecting tiny same-frame job wrappers without profiler proof, and stamina penalty was written in SlowTick instead of the requested pre-KCC phase.
Solution: Extracted deterministic row math into `RadiationMutationKernel`, kept Burst jobs as batch proof kernels, executed the current one-row path directly, moved metabolism bridge to dispatcher `PreSimulation`, and moved shader/VFX publication to dispatcher `VisualSync` with `LateFrame` fallback only.
Rejected Alternatives: Hidden `.Complete()`, same-frame `.Schedule()`/readback, keeping stamina bridge in SlowTick, or faking a dispatcher-owned dependency graph when no async work is emitted.
Scalability potential: Low uses direct scalar row math; Middle/High/Ultra can switch to the existing Burst batch job if entity count expands enough to amortize scheduling.
Hardware Impact: i3/MX350 avoids job wrapper overhead for a single player row; estimated 5-20 us saved per mutation slow tick until profiler evidence replaces the estimate.

## Decision 11: Immutable Source Snapshot And CSV Scratch

Problem: SHINOBU_324 was conservatively locking the SHINOBU_274 radiation state buffer even though it only consumes that fact, and the cold CSV bridge still used `File.ReadAllBytes` before copying into Vault scratch.
Solution: Source radiation now binds the descriptor and reads through `TryReadHandle` as an immutable snapshot. CSV ingestion opens a `FileStream` with `FileShare.ReadWrite` and reads directly into the Vault scratch span before the zero-GC parser slices it.
Rejected Alternatives: Cross-owner write lock on buffer `72740`, direct private radiation source reads, byte-array staging, `string.Split`, and `float.Parse`.
Scalability potential: Low keeps the single-row source read cheap; Middle/High/Ultra can batch more mutation rows without changing authority. CSV remains cold and bounded by the 8192-byte scratch lane.
Hardware Impact: i3/MX350 avoids an unnecessary lock route and one cold managed byte-array allocation; runtime hot path impact is authority cleanliness and fewer synchronization hazards.

## Decision 12: Generated Project Compile Wall

Problem: The first guarded `Hecton8.Core.csproj` build could not see the new standalone `Core/Contracts/Physiology/RadiationStateContract.cs` because Unity-generated project files are stale, producing `RadiationStateDTO` errors along with unrelated external errors.
Solution: Moved the radiation contract ABI into already compiled `Core/Contracts/HectonDataSovereigntyContract.cs` and deleted the standalone contract file to avoid duplicate type definitions after Unity import. The second guarded build removed all SHINOBU_324 errors; remaining 6 errors are external: Predator AUP, VRSomatic DTOs, and HandIK config flags.
Rejected Alternatives: Editing generated `.csproj`, reverting to `RadiationHazardGrid.RadiationStateDTO`, or unsafe reinterpretation between distinct DataVault type handles.
Scalability potential: Contract source remains a stable ABI consumed by SHINOBU_274 and SHINOBU_324 without sibling runtime coupling.
Hardware Impact: No runtime cost. Compile-wall damage is confined to existing external red symbols; SHINOBU_324 no longer contributes compile errors in the guarded Core build.

## Decision 13: Quality-Gated Shader Mutation Dear Lie

Problem: The hand mutation visual path used shader-side vertex displacement, but its first pass still admitted two procedural noise evaluations whenever mutation was visible, which weakened the low-quality collapse requirement.
Solution: Reworked the `UberNoir` hand mutation path so low quality uses a triangle/hash scar approximation and `ValueNoise2` blister/pore detail appears only behind smooth `GlobalQualityWeight` gates. Added a surface helper that uses the same scalar for blister tint, subsurface mask, roughness loss, and tiny emission without material swaps or shader variants.
Rejected Alternatives: CPU arm mesh mutation, blendshape state, material replacement, texture decal sampling, and shader keyword tiers. Those routes add CPU ownership, batching breaks, asset streaming risk, or visible quality cliffs.
Scalability potential: Low uses one cheap deterministic scar scalar; Middle blends into one/two noise taps; High and Ultra spend extra ALU on richer blister tint/SSS while keeping the same shader ABI and Vault truth.
Hardware Impact: i3/MX350 avoids CPU deformation and removes high-cost shader noise below quality 0.30; estimated GPU ALU shed is two `ValueNoise2` calls per mutated vertex plus two per mutated fragment on the low-quality path, pending Frame Debugger/profiler proof.

## Decision 14: Dispatcher Slot 22 VisualSync Sync

Problem: `HectonShaderGlobalDataVaultBridge.PublishRadiationMutation` suppresses direct `Shader.SetGlobal*` when `GlobalShaderDispatcher` owns VisualSync. Slot 22 was valid in the bridge, but the dispatcher did not yet read that slot, so mutation scalars could stay in Vault and fail to reach UberNoir in the normal dispatcher path.
Solution: Added a minimal dispatcher read of `RadiationMutationSlot`, threaded the vector through `ExecuteGlobalDispatch`, and published `_HectonRadiationMutationParams` plus `_HectonHandRadiationMutation01` through the existing command buffer. The bridge fallback path remains unchanged for inactive dispatcher cases.
Rejected Alternatives: Forcing direct `Shader.SetGlobal*` from the physiology runtime, polling shader state from the shader, or adding a second renderer-local owner. Those routes violate phase ownership or create duplicate presentation truth.
Scalability potential: Low/Middle/High/Ultra all use the same slot 22 scalar ABI; quality still scales shader richness, not DTO layout or save identity.
Hardware Impact: CommandBuffer adds two existing-style global writes in VisualSync only. Cost is negligible against avoiding a broken visual route; exact cost awaits Unity profiler.

## Decision 15: Shader Scalar Pre-Sanitize

Problem: HLSL `max(_HectonHandRadiationMutation01, _HectonRadiationMutationParams.x)` could evaluate after one operand became NaN, allowing a corrupted global to reach mutation displacement before the final feature clamp.
Solution: Sanitize legacy and bridge scalars independently with `H8UberNoirFeatureScalar`, then compare the finite saturated results. This keeps vertex and surface mutation paths finite even if one global is poisoned.
Rejected Alternatives: Trusting C# bridge sanitation only, or clamping after `max`. GPU global state can be stale or externally overwritten, so the shader boundary must defend itself.
Scalability potential: Same ALU count class on all devices; quality scaling remains unchanged.
Hardware Impact: Adds two cheap finite/saturate checks in the mutation path only when the hand radiation feature is sampled; prevents catastrophic NaN fan-out through vertex positions/normals.

## Decision 16: Shared Metabolism Mutation Guard

Problem: SHINOBU_324 was using a private `1UL << 45` mutation guard while metabolism/KCC use `ShinobuMetabolismVaultContract.MetabolismStateMutationGuardMask` for the same `MetabolicStateDTO` Vault fact. Separate guard bits can allow two owner-phase writers to believe they have exclusive access to the same buffer.
Solution: Remove the private guard and acquire/release the shared contract guard from the radiation mutation PreSimulation bridge.
Rejected Alternatives: Keeping a SHINOBU-local guard was rejected because it creates shadow authority around one Vault fact. Adding another buffer-level lock only is insufficient because the project uses mutation guards to serialize cross-domain access around shared buffers.
Scalability potential: Low/Middle/High/Ultra all keep one guard identity for metabolism writes; visual quality still scales through shader scalars, not guard routes or DTO layout.
Hardware Impact: No microsecond saving claim. This is a correctness and contention containment fix that prevents rare parallel write races under dispatcher pressure.

## Decision 17: Roslyn Mutation OOP Scanner

Problem: The first OOP mutation scanner was a targeted token scanner. That catches obvious material/particle strings but does not satisfy the prompt's AST parsing requirement and can leave stale shared-report evidence if rerun.
Solution: Upgrade the editor-only scanner to parse C# through `CSharpSyntaxTree`, detect mutation-authority material assignments, `Instantiate`, `GetComponent<SkinnedMeshRenderer>`, `ParticleSystem`, and forbidden mutation type constructions from syntax nodes, and reserve token fallback for shader/HLSL bridge files only. Shared-report upsert now replaces the existing scanner object instead of returning early.
Rejected Alternatives: Keeping grep-only C# evidence was rejected because comments/strings and future syntax changes can produce false positives/negatives. Runtime scanner code was rejected because Roslyn and report writing are editor-only proof tooling, not player simulation.
Scalability potential: No runtime tier cost. Developer proof quality improves on all tiers because future OOP regressions are caught before shipping without changing Vault/shader truth.
Hardware Impact: No player-frame impact; editor-only scan cost is cold tooling. It protects low-end devices indirectly by preventing material clone and particle-object routes from reentering runtime.

## Decision 18: Raw Pointer Burst Proof Jobs

Problem: The SHINOBU_324 batch proof jobs still carried `NativeArray<T>` fields, while the prompt specifically requires raw pointer access inside custom Burst jobs for the mutation state arrays.
Solution: Convert all five radiation mutation proof jobs to `unsafe struct` pointer kernels with explicit count fields and `[NativeDisableUnsafePtrRestriction, NoAlias]` on each non-overlapping lane. Runtime still uses the direct deterministic row kernel for the one-player path to avoid tiny same-frame job overhead.
Rejected Alternatives: Leaving `NativeArray` fields was rejected because it weakens pointer-aliasing proof and does not match the assignment. Scheduling those jobs for one row was rejected because it reintroduces job wrapper overhead without profiler evidence.
Scalability potential: Low keeps the direct scalar row path; Middle/High/Ultra can schedule the pointer batch jobs when entity count justifies amortized job work, without changing DTO layout or Vault ownership.
Hardware Impact: No new measured runtime saving until batch scheduling is enabled. The architecture now gives Burst stronger aliasing information for future SIMD/NEON/AVX batching.

## Decision 19: Scanner Evidence Scope Repair

Problem: The Roslyn scanner source scans `Physiology` and `Player` roots, but the hand-written sidecar evidence still listed older individual SHINOBU files. That mismatch weakens audit value even when `findingCount=0`.
Solution: Align the sidecar and shared JSON report sections with the scanner root set and scanner name: `RadiationMutationOopScanner_ROSLYN_AST`, `Assets/_Project/Scripts/Physiology`, `Assets/_Project/Scripts/Player`, the Core contract DTO file, radiation grid bridge, shader bridge, and UberNoir.
Rejected Alternatives: Leaving stale scope text was rejected because proof artifacts must match executable editor tooling. Broadening runtime scan code was rejected because this is documentation evidence repair, not a player-runtime change.
Scalability potential: No runtime tier cost. Better scan scope makes future OOP regressions in player/physiology mutation paths visible before they can reintroduce CPU deformation, material clones, or particle objects.
Hardware Impact: No frame-time claim. The value is preventing audit drift that would otherwise hide low-end hardware regressions.

## Decision 20: Toxic Blood Compute-Shard Flag

Problem: SHINOBU_324 emitted `DebrisSpawnSignal` for toxic blood with `Flags = 0`, while the GPU debris renderer accepts compute-shard work only when `DebrisSpawnSignal.FlagComputeShard` is present. That made the VFX route data-valid but visually inert.
Solution: Set `Flags = DebrisSpawnSignal.FlagComputeShard` on the toxic blood signal while preserving the `AbsoluteUniversePosition` payload, species hash, quality-scaled intensity, and bounded quantity.
Rejected Alternatives: Adding a custom `VfxSpawnSignal` type was rejected because the project already has a first-party GPU debris lane with renderer consumption and size validation. CPU `ParticleSystem` fallback was rejected as OOP visual mutation.
Scalability potential: Low emits one organic shard at sparse cadence; higher quality smoothly increases quantity to four while the renderer remains GPU/compute owned.
Hardware Impact: No new CPU allocation. This restores the intended GPU path and avoids a future pressure to add managed particle fallback.

## Decision 21: CSV Polling Editor Fence

Problem: The CSV parser was zero-GC and bounded, but `SlowTick()` still probed the filesystem every slow tick in player runtime. Task 17 requires cold boot ingestion; regular player file probes are unnecessary and can create I/O jitter.
Solution: Gate the repeated `TryLoadCsvProfilesCold(vault)` call in `SlowTick()` behind `#if UNITY_EDITOR`. `EnsureVaultState()` still performs the cold boot ingestion once after Vault buffers exist, and designers keep editor play-mode hot reload.
Rejected Alternatives: Removing hot reload entirely was rejected because editor tuning needs it. Keeping player runtime polling was rejected because it violates the cold-ingest intent and wastes I/O budget.
Scalability potential: Low-end player builds avoid filesystem probes; editor/high-end development keeps live balancing without a C# recompile.
Hardware Impact: Removes a recurring `File.Exists`/`GetLastWriteTimeUtc` probe from player slow ticks; exact gain is platform storage dependent, not profiler-measured.

## Decision 22: Resolved Vault Length Guard

Problem: `RunEvaluation()` assumed resolved Vault handles had nonzero lengths and later used `_telemetryCursor % telemetry.Length`. A corrupted or empty handle could turn an otherwise recoverable missing-buffer state into a divide-by-zero fault.
Solution: Add an early guard for `mutationStates.Length`, `tuningRows.Length`, `telemetry.Length`, and `mockDose.Length` before source snapshot binding, locks, or modulo arithmetic.
Rejected Alternatives: Trusting `EnsureGenerationHandle` counts was rejected because runtime proof must survive stale handles, bad editor state, and partial boot failures. Catching exceptions was rejected because this path must be deterministic and allocation-free.
Scalability potential: Same behavior across quality tiers; low-end devices avoid exception-driven failure and all tiers keep telemetry modulo safe.
Hardware Impact: Four integer comparisons in SlowTick; negligible cost, prevents fatal fault.

## Decision 23: Stable Architecture Evidence Sync

Problem: Stable architecture docs still described the older toxic blood and CSV routes after runtime hardening. That creates future audit drift even when code and status are correct.
Solution: Update the SHINOBU_324 architecture page and the SHINOBU_324 ledger row to name `DebrisSpawnSignal.FlagComputeShard`, editor-only post-boot CSV polling, and the `RunEvaluation()` resolved-length guard.
Rejected Alternatives: Leaving evidence only in status/log was rejected because AGENTS.md treats stable docs and ledger as long-lived route memory. Editing unrelated ledger rows was rejected under multi-agent boundary discipline.
Scalability potential: No runtime tier cost. Future agents see the correct low-end route: no player file probes, GPU debris only, and safe telemetry modulo.
Hardware Impact: Documentation-only, but prevents stale instructions from reintroducing runtime I/O or inert VFX routes.

## Decision 24: High-Quality 3D Shader Noise

Problem: The rich shader path used 2D `ValueNoise2` for mutation blistering while the assignment calls for volumetric/procedural hand deformation. The low-tier fallback was correct, but high quality lacked a true 3D sample volume.
Solution: Add `H8UberNoirValueNoise3(float3)` and route rich vertex blisters plus surface pore/blister detail through 3D noise. The calls remain behind the continuous quality/high-cost gate; low quality still uses the cheap triangle/hash scar approximation.
Rejected Alternatives: Full CPU mesh deformation, blendshape assets, material swaps, shader keywords, or always-on high-tap noise were rejected for CPU ownership, asset churn, or low-tier ALU cost. A full simplex implementation was rejected for this pass because existing UberNoir noise style is value/hash based and the requirement is procedural 3D deformation without new variants.
Scalability potential: Low keeps one cheap scar scalar; Middle/High/Ultra smoothly admit 3D blister/pore volume detail through `GlobalQualityWeight`.
Hardware Impact: Low-tier sheds all `ValueNoise3` calls below the gate. Higher tiers spend extra GPU ALU only on already-rendered mutated hands/surfaces.

## Decision 25: Latest Self-Audit Delta

Problem: The full XML audit in the log predated later hardening loops, so a reader could miss the compute-shard VFX flag, editor-only CSV polling fence, Vault length guard, and 3D shader noise upgrade.
Solution: Append a concise `<SELF_AUDIT_UPDATE>` to `LOG_SHINOBU_324.md` covering task continuity, DTO byte layout, scalability, Vault IDs, NoAlias pointer jobs, compile guard, and Dear Lie route.
Rejected Alternatives: Relying on chat output was rejected because AGENTS.md says CTO reads files, not chat. Rewriting the historical full audit was rejected because append-only log order must preserve old evidence.
Scalability potential: No runtime tier cost; audit now documents low/mid/high/ultra behavior accurately.
Hardware Impact: Documentation-only, prevents stale audit from masking low-end route regressions.

## Decision 26: SlowTick Vault Init Fence And Prompt Extractor Repair

Problem: `SlowTick()` still called `EnsureVaultState()`. Most frames returned through existing handles, but the call path could cold-acquire or create Vault buffers after a stale handle or late DataVault bind, which violates the hot-path rule that allocation/ownership resolution must stay in cold setup or explicit swap windows. A fresh CLI prompt proof also exposed a stale extractor pattern that searched `<task id=` even though this prompt uses `Task 01:` lines.
Solution: Add `HasRuntimeVaultState()` and route `SlowTick()` through `_defaultsInitialized`, `!IsCompactionFenceActive`, and generation-checked `HandlesReady()` only. Keep `EnsureVaultState()` in `OnEnable`, `Start`, and DataVault hot-swap handling. Update status/report proof to count tasks with `^Task\s+\d{2}:`.
Rejected Alternatives: Keeping the hot `EnsureVaultState()` call was rejected because it hides cold buffer acquisition behind the mutation cadence. Removing recovery entirely was rejected because cold boot and DataVault replacement still need to reacquire owned descriptors. Keeping the `<task id=` extractor was rejected because it reports `0` for this batch format.
Scalability potential: Low devices avoid surprise Vault allocation or CSV cold-load work during SlowTick; middle/high/ultra keep the same scalar mutation math and shader richness without changing DTO layout or authority route.
Hardware Impact: No measured profiler delta. Static impact is removing a cold-allocation branch from player SlowTick and preserving compile/runtime proof discipline.

## Mandates Used

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

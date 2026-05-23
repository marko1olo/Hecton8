# SHINOBU_343 Rationale

Status: STATIC_SOURCE_R15_PENDING_FRESH_GUARDED_BUILD

## Decision 00 - Mandate Selection

Problem: Hatch lock work touches pressure safety, unmanaged runtime DTOs, AUP audio coordinates, signal lanes, DataVault ownership, and Black Box telemetry.
Solution: Read the eight mandates most directly governing zero-GC hot paths, ARM64 struct layout, fluid pressure containment, AUP determinism, global registry dependency injection, signal segregation, native memory/job ownership, and crash telemetry.
Rejected Alternatives: Reading only the batch prompt was rejected because it would miss current GlobalRegistry, SignalBus, DataVault, and telemetry constraints.
Scalability potential: Low/Middle/High/Ultra all share one gameplay truth route; quality changes cadence and presentation, not authority.
Hardware Impact: Avoids managed door polling and scene searches on i3/MX350; expected win is dominated by removing per-door Update dispatch and object graph checks.

## Decision 01 - Existing Authority Route

Problem: The assignment names hatch locks, but the codebase already owns containment through `BulkheadContainmentRuntime`; adding a parallel door manager would create two facts for the same physical barrier.
Solution: Extend the existing construction containment owner with a partial hatch FSM path and vault-backed DTOs. This keeps one owner phase and lets Agent 220 KCC barriers consume the same bulkhead state route.
Rejected Alternatives: A new `MonoBehaviour` door component, scene-object registry, or `HectonHabitatRuntime` facade was rejected because it would create hot polling, hidden script order, and unmanaged/managed split authority.
Scalability potential: Low uses sparse cadence and flat bitmasks; Middle adds continuous pressure lock visuals; High/Ultra spend saved cycles on richer shader and acoustic state-edge presentation without moving gameplay truth.
Hardware Impact: On i3/MX350 this removes per-door virtual dispatch and object traversal. Expected hot-path cost is a flat Burst pass below 0.1 ms for typical base hatch counts.

## Decision 02 - No Blind Door Deletion

Problem: The request says to destroy scripted doors with `Update()`, but scans in the assigned Habitat/Interaction/Construction domains found no matching legacy door Update loops.
Solution: Record the absence as evidence and implement a validator/report so future regressions are visible. Leave unrelated `Gameplay/SealedDoor.cs` intact because it is a non-pressure wreck interaction and not the hatch safety authority.
Rejected Alternatives: Deleting unrelated gameplay interaction code was rejected as cross-domain sabotage and would not satisfy the pressure-lock route.
Scalability potential: The hatch FSM remains data-local across Low/Middle/High/Ultra; unrelated gameplay doors can be reported without becoming a dependency.
Hardware Impact: Avoids accidental gameplay regressions. No measurable low-end CPU gain from deleting a non-Update unrelated script.

## Decision 03 - Existing Acoustic Lane

Problem: Pressure lock transitions need spatial groan/spark audio, but inventing a door-specific managed signal would violate signal-lane segregation.
Solution: Use `MovementAcousticSignal`, the existing unmanaged hot audio lane, and emit only on FSM state edges from the Burst path.
Rejected Alternatives: `HectonEventBus`, UnityEvents, or a new `DoorSlammedSignal` were rejected because they add managed isolation overhead or duplicate first-party hot broadcast lanes.
Scalability potential: Low emits fewer state-edge pulses by cadence; Middle/High/Ultra can scale volume and optional edge effects through continuous quality weight.
Hardware Impact: On i3/MX350 emission stays near zero when states are stable; only transition frames pay queue write cost.

## Decision 04 - Pressure Extraction From Current Fluid DTO

Problem: The assigned pressure field name `PressureDifferentialATM` exists only in the requested hatch DTO. Current Agent 330 `FluidCompartmentDTO` exposes `CurrentWaterVolume`, `MaxWaterVolume`, `WaterLevelHeight01`, and flags, but no explicit gas/ATM pressure scalar.
Solution: Extract a deterministic ATM proxy from compartment fill: `1.0 + max(volumeFill, waterlineFill) + breachBonus`. The mock path writes high/low fill pairs to prove lock transitions without waiting for a real crash.
Rejected Alternatives: Adding an ATM field to Agent 330 DTO was rejected as cross-domain contract mutation. Querying scene room scripts was rejected as OOP regression and hot-path allocation risk.
Scalability potential: Low keeps the same pressure truth but evaluates at 0.2 s cadence; Middle tightens cadence; High/Ultra can spend saved cycles on shader/acoustic overkill while the pressure proxy remains deterministic.
Hardware Impact: On i3/MX350 this is one division guarded by `math.max(0.0001f, MaxWaterVolume)` and a few scalar ops per hatch, cheaper than fluid resimulation or collider checks.

## Decision 05 - Catastrophic Manual Override

Problem: A manual hatch override under high pressure must not create a benign unlock; it must force base-flood consequences through existing containment data.
Solution: When `ManualOverride` and pressure exceeds the catastrophic threshold, `UpdateHatchFsmJob` sets `CatastrophicFlood`, `BulkheadStateFlags.CatastrophicDamage`, and `Destroyed`, clears the associated lock, and leaves fluid flow open for the existing bulkhead route.
Rejected Alternatives: Blocking manual override silently was rejected because it erases systemic consequence. Spawning flood objects was rejected because containment already owns fluid flow through data.
Scalability potential: Low/Middle/High/Ultra share the same catastrophic truth; visual severity can scale via `_GlobalHatchLockStates` and audio volume without changing authority.
Hardware Impact: The catastrophe path is one branch on rare state transition. It costs effectively 0 us during stable play and avoids expensive physics door destruction.

## Decision 06 - Uninitialized Vault Ownership

Problem: The hatch FSM needs state, tuning, profiles, telemetry, shader upload, and mock fluid scratch without paying clear-memory tax.
Solution: Allocate SHINOBU_343-owned buffers `72024..72029` and `72031` with `NativeArrayOptions.UninitializedMemory` and overwrite active rows deterministically in sync/mock/evaluation jobs. BufferID `72030` remains reserved but is not requested after R14 because shader upload uses prewarmed double `GraphicsBuffer` resources directly.
Rejected Alternatives: `MemClear`, per-frame `NativeArray` creation, and ownership of Agent 330/218 buffers were rejected. External fluid and structural buffers are read by generation handle only.
Scalability potential: Low avoids cold clear overhead on weak devices; Ultra uses the same memory but enables richer shader presentation.
Hardware Impact: Saves cold memory bandwidth on low-end silicon and prevents GC pressure from temporary managed collections.

## Decision 07 - Quality Cadence And Black Box

Problem: Evaluating every hatch every frame wastes ALU on weak devices, but missing NaN/flood states must be diagnosable.
Solution: Use the mandated continuous cadence `lerp(0.016, 0.2, 1-q)` and record each slow tick into a 300-entry `HatchTelemetryEntry` ring. Non-finite pressure, catastrophic flood, or >0.2 ms schedule telemetry requests `Docs/AgentLogs/Dump_SHINOBU_343.bin`.
Rejected Alternatives: Binary hardware tiers and `Debug.Log` telemetry were rejected; both violate project doctrine and hide crash evidence.
Scalability potential: Low 5 Hz survival math, Middle moderate cadence, High near-frame cadence, Ultra visual overkill through shader/audio while preserving one truth.
Hardware Impact: On i3/MX350 expected steady cost stays below 0.1 ms for typical hatch counts; telemetry is a fixed 64-byte stride scan on slow ticks only.

## Decision 08 - Editor Facades Stay Cold

Problem: Designers need live pressure/jam tuning and state visibility, but player runtime cannot pay UI Toolkit, string, or editor drawing costs.
Solution: Place `ContainmentLockFsmTunerWindow`, bar chart, CSV load button, and `OOP_Door_Scanner` under `#if UNITY_EDITOR`; runtime exposes narrow static methods that write tuning DTOs and read telemetry.
Rejected Alternatives: Runtime Canvas prompts, IMGUI play-mode overlays, and per-door label gizmos were rejected because they allocate and obscure the single FSM truth route.
Scalability potential: Low devices pay 0 player runtime cost; High/Ultra editor workflows can inspect richer telemetry without changing player code.
Hardware Impact: On i3/MX350 player build cost is 0 us; editor-only polling is irrelevant to shipping frame time.

## Decision 09 - Static Proof Artifacts

Problem: A chat claim that OOP doors are gone is not an enforceable artifact.
Solution: Add `OOP_Door_Scanner` and write `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_343.json`, markdown evidence, and a non-destructive aggregate entry. Static mirror scan found 89 files and 0 suspicious door Update state machines.
Rejected Alternatives: Deleting unrelated non-Update gameplay code or overwriting the aggregate report were rejected; the report preserves sibling agents' sections.
Scalability potential: Scanner remains cold/editor only across all quality levels.
Hardware Impact: 0 us player cost. The proof exists for integration review without runtime instrumentation overhead.

## Decision 10 - Compile Gate Honesty

Problem: Project rules forbid launching `dotnet build` when CPU load is above 50 percent, and local samples remained above the threshold.
Solution: Run non-build static checks only: `git diff --check`, JSON parse validation, XML parse validation, and targeted source scans. Record build status as not run guarded, not pass.
Rejected Alternatives: Forcing a rebuild at 57-100 percent CPU or claiming a green compile without compiler output was rejected.
Scalability potential: This decision protects low-end developer hardware and keeps integration signal honest.
Hardware Impact: Avoided loading an already saturated CPU with csc/dotnet work; no compile wall was manufactured by this agent.

## Decision 11 - Ultra Polish Compile Route, Timing Proxy, And Structural Scan Budget

Problem: The hatch FSM must read Agent 218 structural integrity without adding a sibling runtime assembly reference, and Task 15 demanded execution timing without permitting hidden `.Complete()` readbacks.
Solution: Added only `Hecton8.Habitat.Deformation.Contracts` to `Hecton8.Core.asmdef`, leaving the structural Runtime assembly unreferenced. Marked hatch telemetry with `HatchTelemetryFlags.ScheduleTimeOnly`, because `LastScheduleMicroseconds` is intentionally a non-blocking schedule overhead proxy. Fenced layout offset reflection behind `UNITY_EDITOR`; player/runtime validation keeps `UnsafeUtility.SizeOf<T>()` only. The R6 quality-scaled structural lookup experiment was superseded by Decision 13 because it could change gameplay truth.
Rejected Alternatives: Directly referencing `Hecton8.Habitat.Deformation` runtime was rejected as compile-wall coupling. Measuring exact Burst wall time by completing the handle was rejected because it would block the dispatcher and violate dependency chaining. Runtime reflection layout checks were rejected as avoidable costs.
Scalability potential: Low/Middle/High/Ultra continue to scale hatch cadence and presentation only. Structural jam truth is fixed by Decision 13.
Hardware Impact: Contract-only structural access and editor-only offset reflection avoid compile-wall and player-runtime metadata cost. Structural lookup performance/truth tradeoff is owned by Decision 13.

## Decision 12 - Final Static Gate Without Rebuild

Problem: After the R6 hardening pass, proof artifacts had to reflect the latest source state, but the build gate still forbids spawning another compiler while the machine is already loaded.
Solution: Re-ran JSON/XML parse checks, target `git diff --check`, hot-path source scans, contract reference scan, and Burst attribute scan. Recorded the final CPU sample and active `dotnet` process instead of manufacturing a compile claim.
Rejected Alternatives: Launching `dotnet build` with CPU at 65 percent and active `dotnet` PID 25560 was rejected by the explicit local hardware rule. Ignoring the final static scan was rejected because the status/rationale files are the durable memory for this agent.
Scalability potential: No gameplay truth changed. The gate protects iteration speed while preserving the same Low/Middle/High/Ultra runtime route.
Hardware Impact: Avoided adding another compiler process on already loaded developer hardware. Runtime impact remains 0 us.

## Decision 13 - Truth-Invariant Structural Jam And Fluid-Missing Fail-Closed

Problem: The R6 structural fallback budget used `q*q`, which could make a jam appear on high-quality hardware and disappear on low-quality hardware. That violates the doctrine that `GlobalQualityWeight` may scale cadence and presentation but must not change gameplay truth. A second gap existed when no real/mock Fluid compartment buffer was available: the pipeline skipped pressure/FSM mutation and could leave stale hatch masks.
Solution: Structural jam now performs deterministic structural-state scanning on the slow hatch cadence and early-exits only after finding a health row below the jam threshold. `GlobalQualityWeight` remains in the cadence curve and presentation/acoustic scalar only. Added `MarkHatchFluidUnavailableJob`, a deterministic Burst fail-closed path that marks intact hatches as `MissingCompartment | PressureLocked | Closed` and sets `AssociatedLock` when Fluid data is absent; already destroyed or catastrophic flood hatches preserve the open flood route. Moved `SignalBus<MovementAcousticSignal>.EnsureInitialized()` from the slow tick path to cold initialization. Telemetry now records the current non-blocking schedule microsecond sample, still flagged `ScheduleTimeOnly`.
Rejected Alternatives: Keeping q-scaled structural read coverage was rejected because it changes containment truth. Leaving missing Fluid data as telemetry-only was rejected because it can fail open. Forcing exact Burst timing with `.Complete()` was rejected because it would break dispatcher chaining.
Scalability potential: Low/Middle/High/Ultra now differ by hatch evaluation cadence and shader/acoustic richness only; a given input state produces the same lock/jam/flood truth once evaluated.
Hardware Impact: Low-end hardware pays full structural certainty only on the slower cadence. Jammed hatches skip structural scan after latching, and active scans early-exit on the first below-threshold structural row. Missing Fluid data now costs one flat fail-closed Burst pass for intact hatches instead of risking stale KCC locks.

## Decision 14 - AST Door Scanner Upgrade

Problem: Task 19 explicitly requested an AST-style architectural validator, but the R8 `OOP_Door_Scanner` source was lexical token matching only. That could miss syntax-shaped door state machines and weakens the proof artifact even though it is editor-only.
Solution: Upgraded the scanner to a Roslyn `CSharpSyntaxTree` primary pass. It walks `MethodDeclarationSyntax` nodes for managed `Update`, `LateUpdate`, and `FixedUpdate` methods in door/hatch/bulkhead contexts, and `SwitchStatementSyntax` nodes for `DoorState` pressure/water coupling. Lexical fallback remains only for parse exceptions and is counted in generated reports.
Rejected Alternatives: Keeping token-only search was rejected because it did not satisfy the XML. Running a runtime validator was rejected because the scanner is proof tooling, not gameplay authority.
Scalability potential: Low/Middle/High/Ultra player runtime remains unchanged at 0 us. Editor/CI can run a stronger scanner without creating a runtime dependency.
Hardware Impact: i3/MX350 player builds pay 0 us and 0 B/frame; editor scanner cost is cold proof work only.

## Decision 15 - CSV Refresh Polling Fence

Problem: `RefreshHatchLockVaultState` is called from owner refresh and visual-sync guard paths. In R9, an absent `hatch_hardware_profiles.csv` could make the route retry `File.Exists`/cold file logic every refresh until a file appeared. That violates the cold-authoring boundary even if the parser itself is allocation-conscious.
Solution: Added `_hatchProfileCsvLoadAttempted`. The default CSV path now gets one boot-time attempt after Vault handles exist; explicit editor/file reload remains available through the tuner path. Failed manual byte parsing does not latch the attempted flag. Reset clears the attempted flag only during owner reset, not every visual sync.
Rejected Alternatives: Polling file timestamps from visual sync was rejected as IO jitter risk. Loading profiles through ScriptableObjects was rejected as managed authoring dependency and DataMonolith drift.
Scalability potential: Low/Middle/High/Ultra runtime does not poll the filesystem. Designers still get explicit reload control without a C# recompile.
Hardware Impact: On i3/MX350 this removes repeated `File.Exists`/`FileInfo` checks from steady owner refresh when the CSV is absent; steady visual-sync cost returns to 0 us for profile IO.

## Decision 16 - Explicit CSV Bootstrap Separation

Problem: The R10 one-shot guard stopped repeated absent-file polling, but explicit editor byte/file reload still called the same `EnsureHatchLockVaultState` route as owner bootstrap. That route could consume the default CSV probe before the designer-supplied payload was applied, mixing bootstrap fallback behavior with manual authoring action.
Solution: Split `EnsureHatchLockVaultState` and `RefreshHatchLockVaultState` with an `allowDefaultProfileLoad` flag. Owner simulation ensure uses `true` and retains the one-shot default profile load. Explicit byte/file reload and visual sync use `false`, so they resolve Vault handles without invoking default file discovery.
Rejected Alternatives: Keeping a single implicit ensure path was rejected because the method name hid cold IO side effects. Removing default CSV loading entirely was rejected because Task 17 requires cold boot hydration when the DataMonolith/profile file exists.
Scalability potential: Low/Middle/High/Ultra runtime behavior remains identical. Profile authoring remains cold and explicit; gameplay truth and shader presentation still scale by continuous cadence/presentation, not by profile-load route.
Hardware Impact: On i3/MX350 this prevents accidental `File.Exists`/`FileInfo` work from visual-sync and manual reload paths. Steady visual-sync profile IO remains 0 us; explicit reload pays file IO only when the designer presses reload.

## Decision 17 - Roslyn Scanner And Assembly Route Audit

Problem: The AST door scanner imports `Microsoft.CodeAnalysis*`, and the hatch runtime aliases `FluidCompartmentDTO`, `IntegrityStateDTO`, and `AbsoluteUniversePosition` namespaces that can look like cross-domain coupling without checking the actual assembly layout.
Solution: Verified `Hecton8.Core.csproj` already references the Roslyn precompiled DLLs under `Assets/Plugins/Roslyn`, and the DLLs exist. Verified `Assets/_Project/Scripts/Construction` has no local asmdef, Agent 330 Fluid contracts and AUP root World files compile in the root Core surface, and SHINOBU_343's structural route adds only `Hecton8.Habitat.Deformation.Contracts` to `Hecton8.Core.asmdef`.
Rejected Alternatives: Adding a new editor asmdef or Roslyn package reference was rejected because existing project references already cover the scanner. Referencing `Hecton8.Habitat.Deformation` runtime directly was rejected as a compile-wall edge.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged; scanner proof stays editor/cold, and structural truth remains Vault snapshot input.
Hardware Impact: Player hot path remains 0 us for scanner dependencies. Compile-wall risk is constrained by avoiding a new runtime assembly reference.

## Decision 18 - Pressure Proxy NaN Pre-Guard and Compile Wall Attribution

Problem: `PressureProxyATM` returned safe fallback on non-finite Fluid values, but it performed denominator/division/fill math before checking non-finite inputs. That created avoidable transient NaN arithmetic even though the output path was masked. After the patch, the guarded build reached external missing-symbol errors outside SHINOBU_343.
Solution: Move the finite checks for `CurrentWaterVolume`, `MaxWaterVolume`, and `WaterLevelHeight01` before `math.max`, division, and `math.saturate`. When CPU dropped to 38 and no compiler process was active, run one targeted `dotnet build Hecton8.Core.csproj --no-restore`; record the exact external diagnostics and no SHINOBU_343 diagnostics.
Rejected Alternatives: Leaving the NaN guard after arithmetic was rejected because prevention is cheaper than post-fact masking. Fixing `GlobalSignals`, `SolarPanel`, or `HectonNarrativeDirector` was rejected as cross-domain compile-medic work outside this hatch-lock prompt.
Scalability potential: Gameplay truth does not change across Low/Middle/High/Ultra; the proxy now fails closed earlier and cadence remains quality-driven.
Hardware Impact: One finite-check block moves before three scalar operations. Runtime cost is effectively unchanged, but it reduces NaN propagation risk on i3/MX350 and ARM64 by avoiding polluted intermediates.

## Decision 19 - Subagent R14 Hot-Path Purge and Stale Compile Wall Downgrade

Problem: Read-only audit found that the hatch schedule path could still call the generic Vault ensure route, which can allocate handles or invoke default CSV probing if bootstrap/rebind state is incomplete. Visual sync could refresh hatch Vault state and mutate tuning, graphics buffers could be allocated from visual sync if missing, the acoustic writer had an unnecessary safety suppression, mock pressure lookup fell through to sparse O(hatches*compartments) scans despite paired mock rows, the active Vault list still included an unused shader-upload buffer, and reports overstated the stale R13 external compile wall.
Solution: Schedule now requires `_vaultInitialized` and resolves already-created handles only. `RefreshVaultState` has a `refreshHatchLocks` flag so visual sync does not mutate hatch tuning. `VisualSyncHatchLocks` requires prewarmed hatch graphics buffers and cannot allocate them; bootstrap prewarms them when hatch shader upload is enabled, and prewarm failure disables only hatch shader upload instead of blocking containment simulation. Default CSV loading is confined to owner bootstrap, while later refresh/schedule/visual routes pass no default file discovery. `_hatchShaderUploadHandle` allocation was removed and BufferID 72030 is treated as retired/reserved, not requested. `NativeDisableContainerSafetyRestriction` was removed from the acoustic writer. `EvaluateHatchPressureJob` now tries paired mock-room indexes before the legacy sparse hash scan. `HatchTelemetryEntry` tail padding is two `uint` fields instead of one trailing `ulong`.
Rejected Alternatives: Keeping generic ensure in schedule was rejected because cold allocation/IO side effects must never be reachable from the simulation scheduler. Keeping visual-sync refresh was rejected because read/presentation phases must not mutate control DTOs. Always reallocating graphics buffers from visual sync was rejected as driver/GC jitter risk. Creating a hash map for Fluid compartments was rejected for this patch because it would require a new ownership route; the paired fast path removes the mock stress-test worst case without new memory. Claiming R13 compile wall as current was rejected because neighboring agents added the previously missing symbols after that attempt and no fresh guarded build has been run.
Scalability potential: Low/Middle/High/Ultra keep identical lock truth; quality still scales cadence and shader/acoustic scalar richness only. The mock fast path lowers stress-test ALU cost on weak devices, while high-tier presentation still uses the GPU bitmask buffer.
Hardware Impact: Removes schedule-path handle allocation/CSV risk, visual-sync tuning mutation, and visual-sync hatch buffer allocation. A driver/headless graphics failure now disables presentation only, preserving base containment truth. Mock stress tests avoid the avoidable O(256*512) sparse scan when paired rows are present, returning to two direct indexed probes per hatch on i3/MX350-class hardware.

## Decision 20 - R14 Static Validation Without Build

Problem: After R14 source and report edits, the project needs a fresh compile, but local CPU sampled 96 percent. The explicit project rule forbids launching `dotnet build` above 50 percent CPU even when no compiler process is active.
Solution: Run non-build gates only: JSON parse for aggregate and SHINOBU reports, XML parse for self-audit, scoped `git diff --check`, runtime hot-path grep, Burst attribute scan, and route grep for the removed schedule/visual-sync hazards.
Rejected Alternatives: Launching `dotnet build Hecton8.Core.csproj --no-restore` at 96 percent CPU was rejected. Claiming R13's external wall as current was rejected because external symbols were added after that attempt and no raw build log was persisted.
Scalability potential: No gameplay truth changed. The gate protects developer hardware while preserving the Low/Middle/High/Ultra runtime route.
Hardware Impact: Avoided creating compiler pressure on a saturated machine. Runtime code remains pending fresh compiler verification.

## Decision 21 - Profile Envelope and Local Sourcegraph Proof R15

Problem: Task 17 says hatch hardware profiles dictate pressure limits, but the R14 parser only wrote `HatchHardwareProfileDTO` rows and did not feed those rows into the active tuning DTO. The profile buffer is allocated with `UninitializedMemory`, so scanning all 32 rows without a parsed-row fence could apply garbage. A separate proof gap existed because the ignored/generated `Hecton8.Core.csproj` did not include the new SHINOBU_343 files, making any later targeted build unable to compile this patch surface.
Solution: Added `_hatchProfileRowCount`, set it only after successful byte/file CSV parse, and folded only `[0, parsedCount)` valid rows into `HatchTuningDTO`. Because no existing hatch/bulkhead row owns a per-hatch hardware type hash, the runtime applies a conservative envelope: minimum safe pressure, maximum structural jam threshold, and minimum catastrophic pressure across loaded profiles while preserving `catastrophic >= safe`. Editor readback now reports the effective Vault tuning row, not only serialized defaults. The local generated csproj sourcegraph now includes `HatchLockContracts.cs`, `HatchLockJobs.cs`, `BulkheadContainmentRuntime_HatchLocks.cs`, and `Editor/HatchLockFsmEditor.cs` so the next guarded `dotnet build` can actually compile SHINOBU_343 files.
Rejected Alternatives: Adding a `ProfileHash` to `HatchStateDTO` was rejected because the XML mandates its 32-byte field layout and offsets. Mutating `BulkheadStateDTO` was rejected as cross-domain API churn. Applying per-profile branching in the Burst FSM without a hatch-owned type route was rejected as fake precision. Scanning the whole profile buffer was rejected because uninitialized rows are not valid input. Leaving the generated csproj stale was rejected because it creates false compile evidence.
Scalability potential: Low/Middle/High/Ultra keep identical safety thresholds once profiles are loaded; quality still scales cadence and presentation only. High/Ultra can use profile pulse scalars later in shader-owned presentation, but gameplay truth remains the folded safety envelope until a legitimate per-hatch type route exists.
Hardware Impact: Cold boot/editor reload pays <=32 scalar profile rows once. Slow hatch schedule now reads an already-folded tuning row; no per-hatch profile scan enters the Burst hot path. The csproj change is ignored/generated local sourcegraph only and has 0 us runtime cost.

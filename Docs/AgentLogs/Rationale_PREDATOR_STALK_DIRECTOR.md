# Rationale_PREDATOR_STALK_DIRECTOR

## Decision 1: Stale Missing-Prompt Status Replaced

Problem: `Status_PREDATOR_STALK_DIRECTOR.md` and `Rationale_PREDATOR_STALK_DIRECTOR.md` still recorded the earlier missing XML state, but `Docs/Tasks/CURRENT_BATCH.md` now contains `<AGENT_PROMPT id="PREDATOR_STALK_DIRECTOR" ...>` at line 2077.

Solution: Treat the filesystem as primary memory and replace the stale blocker with the live 18-task checklist. DOD pattern used: batch prompt extraction gate plus anti-amnesia status repair.

Rejected Alternatives: Continuing with the stale blocker was rejected because it would cause future compacted context to stop incorrectly. Borrowing from archived `ALPHA_LEVIATHAN_COGNITION` logs was rejected because AGENTS forbids previous-batch logs unless explicitly ordered.

Scalability potential: No runtime impact. Low/Middle/High/Ultra behavior becomes trackable because the live task list is now persisted.

Hardware Impact: 0 us runtime impact on i3/MX350; this is documentation state repair.

## Decision 2: DataVault-Owned Alpha Leviathan State

Problem: The prompt requires `AgressionLevel`, `CurrentPhase`, and `TargetAnchorAUP` to leave local AI owner state and live in `GlobalDataVault`, but AI/Cognition had no dedicated buffer IDs or bridge.

Solution: Added `SystemID.AICognition` and five `BufferID` slots for state, sensory stimulus, steering output, telemetry ring, and telemetry cursor; added `AlphaLeviathanCognitionVault.TryResolve(IDataVault, ...)` as the cold-path bridge. DOD pattern used: DataVault sovereignty and GlobalRegistry injection discipline.

Rejected Alternatives: Local `NativeArray<T>` ownership was rejected because the mandate forbids persistent NativeArrays outside GlobalDataVault. `AIManager.Instance` was rejected because singletons are forbidden and no such dependency exists in AI/Cognition. Direct Fauna edits were rejected because the authoritative domain is AI/Cognition.

Scalability potential: Low uses compact fixed arrays and caller-side low-frequency scheduling. Middle runs the same kernel every frame. High enables SDF contouring. Ultra can spend the same buffer layout on richer cave-wall contour steering without changing cross-domain contracts.

Hardware Impact: MX350 avoids managed state churn and singleton polling; exact profiler proof absent. Static estimate: one vault view resolution on cold path, zero hot-path managed allocation.

## Decision 3: Tangent Orbit Instead Of NavMesh

Problem: A stalking predator cannot use NavMesh/AStar under the task contract, and straight-line pursuit is predictable.

Solution: Added `LeviathanStalkJob`, a Burst `IJobParallelFor` kernel. It computes AUP distance in `double3`, derives a tangent from `cross(Up, normalize(anchor - leviathan))`, applies ring-distance radial correction, and writes a steering vector rather than moving transforms. DOD pattern used: raw vector math, AUP distance authority, output-only steering contract.

Rejected Alternatives: Unity `NavMeshAgent` was rejected by explicit task rule. A* was rejected by explicit task rule and project-wide third-party ban. Transform-space `RotateAround` was rejected because it breaks AUP/floating-origin stability and creates presentation coupling.

Scalability potential: Low disables contouring and uses caller-side 5Hz interpolation. Middle uses radial orbit. High uses SDF gradient tangent contour. Ultra can increase contour weight and sensory fidelity while keeping the same job row.

Hardware Impact: MX350 cost is fixed scalar/vector ALU per active slot and one telemetry write. Static estimate remains below 0.1 ms for 64 slots, but profiler proof is absent.

## Decision 4: Sensory Inputs Are Pre-Digested Rows

Problem: The job must react to player noise, headlights, sonar ping, system stress, and SDF gradient without pulling concrete Audio, Light, Submarine, or Fauna classes into AI/Cognition.

Solution: Added `AlphaLeviathanSensoryStimulus`, a blittable DataVault row containing player AUP, ping AUP, forward/light dot, noise threshold, fog distance, SDF gradient, stress, sonar age, runtime flags, and shift frame ID. DOD pattern used: decoupled data row rather than direct dependency.

Rejected Alternatives: Reading `SubmarineLightsChangedSignal` or `SonarPing` directly inside the Burst job was rejected because jobs cannot consume managed event buses and signal ownership is cross-domain. Polling `GlobalRegistry` inside the job was rejected by mandate. Adding concrete submarine/audio references was rejected as domain coupling.

Scalability potential: Low can omit expensive SDF and chemical/acoustic richness while still filling the same row. High/Ultra can inject richer SDF gradients, acoustic confidence, or lure intensity without changing the job API.

Hardware Impact: MX350 pays one contiguous NativeArray read per slot, no managed calls. Exact microseconds saved over direct component polling are unmeasured; static impact is removal of all per-frame object lookup risk.

## Decision 5: Black Box Ring Over Debug Logs

Problem: The prompt requires last-300-frame AI state; AGENTS forbids "I don't know why it crashed" and forbids hot-path string logs.

Solution: `LeviathanStalkJob` writes `AlphaLeviathanTelemetryEntry` with frame, slot, stalk phase, flags, distance, ring distance, AUP-derived positions, direction, state hash, and `LeviathanAgressivity01`. The telemetry ring is sized as 300 frames times 64 slots. `AlphaLeviathanCognitionVault.TryDumpBlackBox(...)` writes a cold-path binary dump to `Docs/AgentLogs/Dump_PREDATOR_STALK_DIRECTOR.bin`, and `TryDumpBlackBoxOnFault(...)` scans for `AlphaLeviathanTelemetryFlags.Fault` so an owner can dump immediately after NaN/fault detection without file I/O inside Burst. DOD pattern used: fixed-size NativeArray circular buffer plus cold crash/fault-path dump.

Rejected Alternatives: `Debug.Log` was rejected because it allocates strings and is unusable in Burst. Managed queues were rejected because the black box must be fixed-size native telemetry. Per-frame file writes were rejected because Steam Deck/MicroSD I/O stalls are worse than a cold dump.

Scalability potential: Low records compact hash and flags. Middle/High record same row. Ultra can add external dump handling via owner system without touching the job.

Hardware Impact: One 64-byte telemetry write per slot per scheduled tick plus a byte fault flag already inside the row. On MX350 this is predictable memory bandwidth, not GC; exact measurement absent. Cold fault scans are not hot-path work.

## Decision 6: Compile Gate Still Open

Problem: `dotnet build` with no target fails because the Unity root has many project files. Broad `Hecton8.Core.csproj` and `Assembly-CSharp.csproj` builds exceeded 120 seconds before producing actionable compile errors. The generated Unity project files do not yet include the new AI/Cognition source files, while the stale `Library/ScriptAssemblies/Hecton8.AI.Cognition.dll` predates this pass.

Solution: Continue with targeted compile discovery: generated project inspection, Unity script assembly presence checks, and a pending targeted Unity/C# compile. DOD pattern used: fail-fast compile wall handling without reverting working source prematurely.

Rejected Alternatives: Declaring `dotnet build` green from static scans was rejected. Editing generated `.csproj` files was rejected because Unity overwrites them. Reverting the kernel before a real compiler error was rejected because no code error has been isolated yet.

Scalability potential: No runtime impact.

Hardware Impact: 0 us runtime impact; this is validation infrastructure state.

## Decision 7: Omega Branch Removal

Problem: The Omega mandate forbids `if` branches inside the Burst job and requires AUP shift handling so the beast does not interpolate across an origin snap. The follow-up audit also found short-circuit bool operators that were not `if` tokens but still create conditional gating risk.

Solution: Reworked the AUP target selection and distance fallback inside `LeviathanStalkJob` to use `math.select` and bit-mask selection instead of ternary branching. Removed `&&` and `||` from the Burst file and used non-short-circuit bool operators where both sides are scalar-safe. Added telemetry position sanitization and retained `ObservedShiftFrameId` versus `LastShiftFrameId` steering reset with `ShiftFenceReset` telemetry. DOD pattern used: branchless Burst selection and AUP snap-fence reset.

Rejected Alternatives: Keeping the ternary `usePing ? ping : player` was rejected because it is still a conditional target selection in the job. Keeping short-circuit bool chains was rejected because the branchless audit must be stricter than token-only `if` removal. Interpolating through an AUP shift was rejected by the floating-origin mandate. Adding a managed `AupShiftSignal` subscription inside AI/Cognition was rejected because there is no runtime owner in this domain and the job must consume DataVault rows only.

Scalability potential: Low keeps branchless cheap steering. Middle/High keep deterministic shift reset. Ultra can attach richer shift telemetry externally while the kernel remains stable.

Hardware Impact: MX350 avoids one conditional AUP-target branch per slot and removes short-circuit gating in the Burst source. Exact microseconds saved are unmeasured.

## Decision 8: Final Validation Blocked Outside Domain

Problem: The prompt requires `dotnet build` exit 0, but the project root contains many generated `.csproj` files so bare `dotnet build` exits MSB1011. `dotnet build Hecton8.AI.Cognition.csproj --no-restore` fails because Unity has not generated a dedicated AI/Cognition project file. The latest Unity batch startup crashes before C# compile because `Assets/_Project/Scripts/Physics/Tethers/Contracts/Hecton8.Physics.Tethers.Contracts.asmdef` is missing.

Solution: Verified the owned code through the Unity Bee/Csc response file `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.rsp`; it exits 0 over the AI/Cognition source set. Marked task 18 as `[BLOCKED BY DEPENDENCY]` with exact unrelated failure owners. DOD pattern used: fail-fast three-strike compile-wall protocol.

Rejected Alternatives: Editing `Physics.Tethers.Contracts`, `Audio.Virtualization`, editor tooling, or a missing Tethers asmdef was rejected as outside AI/COGNITION domain and would be architectural sabotage without assignment. Declaring whole-project build green was rejected because Unity crashes before the compile graph reaches owned AI code.

Scalability potential: No runtime impact.

Hardware Impact: 0 us runtime impact; validation blocker is assembly graph/editor tooling, not AI steering cost.

## Decision 9: Multiplatform Layout Lockdown

Problem: ARM64/Quest/Android builds are sensitive to implicit struct packing. The previous AI payloads needed an explicit pass proving no default CLR packing survived in the domain.

Solution: Locked every AI/Cognition payload to `StructLayout(..., Pack = 1)`: `AlphaLeviathanTelemetryEntry` 64 bytes, `AlphaLeviathanAup` explicit 48 bytes, `AlphaLeviathanCognitionState` 144 bytes, `AlphaLeviathanSensoryStimulus` 176 bytes, and `AlphaLeviathanSteeringOutput` 88 bytes. DOD pattern used: fixed blittable stride and explicit AUP offsets.

Rejected Alternatives: Default sequential packing was rejected because it can shift stride across runtimes. Platform-specific `#if` layouts were rejected because they multiply failure modes and break deterministic telemetry parsing.

Scalability potential: Low/Middle/High/Ultra tiers share the same binary layout, so Math LOD changes do not require platform-specific marshaling.

Hardware Impact: 0 us runtime gain. The value is stability: deterministic stride across ARM64 and x64.

## Decision 10: VFX Intent Without Rendering Coupling

Problem: High-end mode cannot be a mobile steering output with one generic intensity. It needs enough AI-authored intent for salt crystal, silt, hull dent, SSS, and particle escalation while AI/Cognition remains renderer-free.

Solution: Extended `AlphaLeviathanSteeringOutput` from 64 to 88 bytes with `VisorSaltCrystalGrowth01`, `HullDentImpulse01`, `SubsurfaceScatterPulse01`, `ParticleOverkillBudget01`, and `PredatorSilhouetteNoise01`. Existing `WakeSiltIntensity01`, `SdfContourWeight01`, and `VisualOverkill01` remain. DOD pattern used: scalar intent channel, no material/shader dependency in AI.

Rejected Alternatives: Adding renderer calls, material property writes, or shader keywords inside AI was rejected as cross-domain coupling and hot-path allocation risk. Leaving only the mobile-safe biolum value was rejected because Ultra tier needs explicit overkill budget for downstream systems.

Scalability potential: Low uses cheap cadence, reduced particle budget, and triangle-wave silhouette noise. Middle keeps wake/biolum without SDF contour. High uses SDF contour plus stronger silt/SSS. Ultra can spend `ParticleOverkillBudget01`, salt/dent, and silhouette-noise channels in VFX without changing AI.

Hardware Impact: Adds five float stores per active slot. On 64 slots this is 1280 bytes of extra contiguous output per scheduled tick; exact microseconds are unmeasured.

## Decision 11: I/O And Shader Boundary

Problem: Steam Deck/MicroSD stutter and Metal shader compliance were called out, but AI/Cognition must not cross into renderer or asset streaming ownership.

Solution: Static scan found no shader, compute, HLSL, or CG include files in AI/Cognition. Static scan found file I/O only in `TryDumpBlackBox`/`TryDumpBlackBoxOnFault`, which are cold crash/fault-path binary dumps; `LeviathanStalkJob` has no file access, `Debug.Log`, string formatting, or managed lookup calls.

Rejected Alternatives: Writing per-frame text diagnostics was rejected because it creates unbounded I/O pressure. Editing rendering shaders was rejected because there are no AI/Cognition shader assets and rendering/VFX is a separate domain.

Scalability potential: Low/Steam Deck keeps the hot path memory-only. High/Ultra receive richer scalar intent through DataVault without pulling the render path into AI.

Hardware Impact: 0 us hot-path I/O; cold dump cost is paid only on crash/NaN handling.

## Decision 12: In-Place Vault State Instead Of Fake Double Buffer

Problem: `AlphaLeviathanCognitionVault` resolves one DataVault-owned state buffer, but `LeviathanStalkJob` exposed separate `InputStates` and `OutputStates`. A caller would either pass the same buffer twice, risking Unity job safety alias rejection, or invent a second state buffer outside the prompt contract.

Solution: Collapsed the job contract to one `States` NativeArray view. Each parallel index reads its row, computes steering, then writes the same row back. Added `AlphaLeviathanCognitionVault.CreateStalkJob(...)` so the cold owner path gets canonical wiring from DataVault views. DOD pattern used: stateless job over DataVault rows, no false private double buffer.

Rejected Alternatives: Adding a second Alpha Leviathan state buffer was rejected because the task only requires one DataVault truth state and extra state creates synchronization drift. Leaving manual input/output wiring was rejected because it lets integration code reintroduce alias bugs.

Scalability potential: Low/Middle/High/Ultra tiers all update a single state row per slot. Future double-buffering must be an explicit DataVault contract, not caller folklore.

Hardware Impact: Removes one job NativeArray field and one duplicate state-buffer binding. Exact microseconds are unmeasured.

## Decision 13: Faults And Lures Must Be Actionable

Problem: Default inactive rows can have zeroed AUPs. The previous fault flag would mark invalid same-position distance even for inactive slots, causing false black-box dumps. Also, a sonar-only lure idled if the player anchor flag was absent.

Solution: Introduced `hasTrackingAnchor = HasPlayerAnchor | sonarActive`. Idle phase now requires no active tracking anchor, so sonar-only pings can drive circling. Fault telemetry now requires invalid distance plus active plus tracking anchor. The job also writes dense slot IDs from the job index so black-box rows are identifiable even before caller-side state seeding.

Rejected Alternatives: Dumping on every invalid idle slot was rejected because it hides real NaNs behind default-state noise. Requiring player anchor for a sonar lure was rejected because the task explicitly says a `SonarPing` should move the Leviathan to `PingAUP`.

Scalability potential: Low tier gets deterministic ping lures without extra sensing. High/Ultra can layer SDF contouring around ping anchors without changing the state contract.

Hardware Impact: Adds one boolean OR, one gated flag select, and one ushort slot write per active row. Exact microseconds are unmeasured.

## Decision 14: Dot-Product Vision And Triangle Noise Dear Lie

Problem: `PlayerForward` existed in the sensory row but was unused. That left a cheap dot-product perception cue on the table and failed the "Dear Lie" requirement for low-end mathematical fakes.

Solution: The job normalizes `PlayerForward`, computes a branchless gaze dot against the predator direction, emits `PlayerGazeBreak`, and generates `PredatorSilhouetteNoise01` with a triangle wave derived from frame/index/aggression. Low tier receives the full fake flicker; high tier receives a quieter value for render/VFX layering.

Rejected Alternatives: Ray/visibility simulation was rejected because the prompt already provides pre-digested sensory data and dot products are enough for stalking presentation. Random noise was rejected because deterministic triangle noise is cheaper, replayable, and does not allocate state.

Scalability potential: Low uses the triangle fake to sell a silhouette without SDF or particle density. High/Ultra use it as a subtle modulation under heavier SSS/silt/particle work.

Hardware Impact: Cheap ALU only: one normalize, one dot, one frac/abs triangle wave per row. Profiler proof is absent.

## Decision 15: Action Gate Before Predator Intent

Problem: A default or inactive row could still carry stale aggression and valid previous state. Without a single authority gate, that row could select Charge after Idle selection, request high-tier SDF/VFX intent, or emit acoustic/gaze/light flags even when no active tracking anchor existed.

Solution: Added an `eligibleToAct = active & hasTrackingAnchor` gate inside `LeviathanStalkJob` and applied it to Charge, Retreat, SDF contouring, aggression gain, acoustic lure, gaze break, light retreat, and fault telemetry. Sensory comparison scalars are clamped with `math.select` before use. Idle rows preserve `TargetAnchorAup` unless eligible; `PreviousSteeringDirection` and `Forward` refresh only when eligible or a shift fence reset is required. Steering output and telemetry use the same gate to zero desired direction, target offset, exported ring/distance, output aggression, bioluminescence, wake, salt, SSS, particle, and silhouette intent for dormant rows. DOD pattern used: branchless authority gating with DataVault-owned state preservation.

Rejected Alternatives: Letting phase priority rely on later `math.select` order was rejected because Charge could override Idle. Clearing inactive rows every frame was rejected because the job does not own lifecycle seeding and would destroy useful post-shift state. Adding managed owner-side fixes was rejected because the bug is in the Burst decision kernel.

Scalability potential: Low tier avoids fake predator intent on dormant rows and keeps the cheap radial lie only for active anchors. Middle keeps stable stalking state. High/Ultra only spend SDF/VFX overkill on rows that are actually tracking a player or sonar ping; dormant slots emit zero presentation budget.

Hardware Impact: Adds a few scalar boolean gates and three finite/saturate guards per row. Measured savings: 0 us. Profiler proof absent; this is a correctness and stability fix that can avoid false downstream renderer/VFX work but is not a measured performance claim.

## Decision 16: Handle-First Vault Integration

Problem: `AlphaLeviathanCognitionVault.TryResolve(...)` returned raw `NativeArray` views. Those views are DataVault-owned, but long-lived caller caching can become stale after vault generation changes.

Solution: Added `AlphaLeviathanVaultHandles`, `TryResolveHandles(...)`, and `TryResolveViews(...)`. Owners can cache `VaultBufferHandle<T>` values, then resolve transient views immediately before scheduling `LeviathanStalkJob` or dumping the black box. DOD pattern used: generation-checked DataVault handles with stale-alias fail-fast.

Rejected Alternatives: Removing the existing `TryResolve(...)` compatibility path was rejected because public API removal would break integrators during the batch. Allocating private persistent arrays was rejected by DataVault sovereignty. Resolving GlobalRegistry inside the job was rejected by Burst and DI rules.

Scalability potential: Low/Middle/High/Ultra tiers share the same handles; high-tier SDF and visual-overkill channels do not require new native owners or cross-domain arrays.

Hardware Impact: 0 us hot path. Handle resolution is cold/schedule-path metadata validation. Profiler proof absent.

## Decision 17: Layout Sweep Includes Job And Vault Carriers

Problem: The ARM64/Quest audit previously focused on payload structs, but the domain also exposes vault carrier structs and the Burst job struct.

Solution: Added explicit `StructLayout(LayoutKind.Sequential, Pack = 1)` to `AlphaLeviathanVaultBuffers` and `LeviathanStalkJob`, and `StructLayout(LayoutKind.Sequential, Pack = 1, Size = 120)` to `AlphaLeviathanVaultHandles`. The existing payload sizes remain fixed: telemetry 64, AUP 48, cognition state 144, sensory row 176, steering output 88. DOD pattern used: no unannotated public struct in AI/Cognition.

Rejected Alternatives: Treating scheduler/view structs as exempt was rejected because the user explicitly requested a full struct pass. Platform-specific layout branches were rejected because they invite x64/ARM64 divergence.

Scalability potential: All hardware tiers use one deterministic contract surface. Quest/Android layout stays explicit while PC/RTX keeps the same VFX intent payload.

Hardware Impact: 0 us runtime. This is ABI stability work, not a performance claim.

## Decision 18: Dormant Rows Must Not Request Cadence Or LOD Work

Problem: The action gate zeroed most dormant presentation intent, but `RecommendedCadenceSeconds` and `LowTierRadialFallback` could still tell downstream owners that an inactive slot had work to schedule. That is a false signal under the DataVault contract because dormant rows have no tracking authority.

Solution: Gate `LowTierRadialFallback` with `lowTier & eligibleToAct` and zero `RecommendedCadenceSeconds` when `eligibleToAct` is false. DOD pattern used: branchless authority gating with `math.select`, no `if` inside the Burst job.

Rejected Alternatives: Leaving the cadence live was rejected because render/VFX owners may reasonably trust the steering row as an intent contract. Moving the fix downstream was rejected because every consumer would need duplicate Active/anchor checks. Clearing the entire state row was rejected because lifecycle seeding belongs to the owner, not the Burst solver.

Scalability potential: Low tier only schedules the 5Hz Dear Lie for a real stalking row. Middle/High/Ultra only spend SDF contour, VFX, and steering update budget when the predator has a valid player or ping anchor.

Hardware Impact: Measured savings: 0 us. Static effect is false-work containment on MX350 and Steam Deck; no profiler proof was captured.

## Decision 19: Handle-First Scheduling And Fault Dump API

Problem: The previous handle pass let owners resolve handles and views, but canonical scheduling still required raw `AlphaLeviathanVaultBuffers`. That left room for integrators to cache raw `NativeArray` views for too long or manually assemble a job with stale aliases.

Solution: Added `TryCreateStalkJob(IDataVault, ref AlphaLeviathanVaultHandles, uint, out LeviathanStalkJob)` and a handle-based `TryDumpBlackBoxOnFault(...)` overload. Owners can cache handles, resolve current views at schedule or fault-dump time, and let the vault refresh handle generations.

Rejected Alternatives: Removing the raw-buffer API was rejected because public API removal during batch integration can break other agents. Requiring every caller to repeat the view-resolution pattern was rejected because it spreads stale-alias risk. Allocating private arrays was rejected by DataVault sovereignty.

Scalability potential: All tiers keep one buffer contract. Low/Middle/High/Ultra feature differences stay in the DataVault rows, not in caller-owned native containers.

Hardware Impact: 0 us hot path. The new work is cold schedule-path and cold fault-path metadata resolution; no measured runtime savings are claimed.

## Decision 20: Stalk Phase Constants Reference The Legacy Wire Contract

Problem: `AlphaLeviathanStalkPhase` repeated byte literals that already existed in `AlphaLeviathanPhase`. The values matched today, but duplicated literals are interface drift waiting to happen because Fauna still consumes the legacy phase contract.

Solution: Replaced the stalk phase literal values with references to `AlphaLeviathanPhase.Hidden`, `Circling`, `FalseCharge`, and `VeerOff`. The public byte values remain unchanged: Idle/Hidden = 0, Circle/Circling = 1, Charge/FalseCharge = 2, Retreat/VeerOff = 3.

Rejected Alternatives: Editing Fauna phase consumers was rejected as outside the authoritative AI/COGNITION domain. Changing numeric values was rejected because telemetry and legacy Fauna contracts already depend on them. Adding a third mapping table was rejected because it would create another duplicated source of truth.

Scalability potential: Low/Middle/High/Ultra all emit the same byte contract; tier differences stay in steering/VFX scalar fields, not phase identity.

Hardware Impact: 0 us runtime. This is compile-time constant deduplication and interface-risk reduction only.

## Decision 21: Cursor Heartbeat Belongs Outside The Parallel Stalk Job

Problem: The Alpha Leviathan telemetry cursor buffer existed but had no public owner-side write path. Writing it inside `LeviathanStalkJob` would either require a branch for index zero or cause all parallel workers to write the same cursor cell, both of which violate the current Burst job hygiene.

Solution: Added `TryRecordTelemetryHeartbeat(...)` overloads on `AlphaLeviathanCognitionVault`. The owner calls this after the job completes; it writes one int cursor value into the DataVault-owned `AlphaLeviathanTelemetryCursor` buffer. DOD pattern used: post-job heartbeat cursor, no parallel cursor race, DataVault-owned storage.

Rejected Alternatives: Writing the cursor from every job row was rejected because it creates a parallel write race. Adding a second Burst cursor job was rejected because it adds scheduling overhead for one int. Dropping the cursor buffer was rejected because the black-box dump format already records it.

Scalability potential: Low/Middle/High/Ultra all use the same 300-frame cursor. Tier behavior stays in steering rows and VFX scalars, not in dump mechanics.

Hardware Impact: Measured savings: 0 us. Static cost is one cold post-job integer write; no profiler data was captured.

## Decision 22: Fault Dump Checks Should Scan The Current Frame First

Problem: The broad fault-dump helper scans all 19,200 telemetry entries. That is acceptable for crash triage but wasteful for a normal post-job fault check, and it can keep reacting to an old historical fault.

Solution: Added `TryDumpBlackBoxOnFrameFault(...)` overloads. They compute `frame % 300`, scan only the 64 slots for that frame, require `entry.Frame == frame`, and dump the full black box only when the current frame contains a fault. DOD pattern used: bounded current-frame fault gate with full historical dump only after a live fault.

Rejected Alternatives: Replacing the broad scan was rejected because crash recovery may still need a full-ring sweep. Clearing old fault flags was rejected because black-box history must remain intact. Per-frame file writes were rejected as Steam Deck/MicroSD pressure.

Scalability potential: Low tier uses a 64-row cold scan after completion instead of a full-ring sweep. High/Ultra retain the same full dump payload when a current fault is detected.

Hardware Impact: Measured savings: 0 us. Static cold scan bound drops from 19,200 rows to 64 rows for normal post-job fault checks; runtime profiler proof absent.

## Decision 23: Readonly View Carrier Fix

Problem: The first heartbeat implementation took `in AlphaLeviathanVaultBuffers` and attempted to write `TelemetryCursor[0]`, producing CS8332 because the compiler treats the view carrier as readonly.

Solution: Changed only `TryRecordTelemetryHeartbeat` to accept `AlphaLeviathanVaultBuffers` by value. `NativeArray<int>` remains a small view struct pointing at DataVault-owned memory, so ownership does not move and no allocation is introduced.

Rejected Alternatives: Unsafe pointer writes were rejected because the standard `NativeArray` setter is sufficient. Passing the whole carrier by `ref` was rejected because the method does not mutate the carrier fields. Moving the write into the parallel job was rejected for race/branch reasons.

Scalability potential: Identical across Low/Middle/High/Ultra; this is API correctness, not tier behavior.

Hardware Impact: 0 us runtime claim. This is a compiler-correctness fix with no measured performance delta.

## Decision 24: AUP Shift Frame Must Survive The Dump

Problem: The job reset steering on `ObservedShiftFrameId` changes and set the `ShiftFenceReset` flag, but the blackbox payload did not preserve the actual shift frame ID. A dump could show that a reset occurred without proving which origin-shift fence caused it.

Solution: Wrote `stimulus.ObservedShiftFrameId` into the existing `AlphaLeviathanTelemetryEntry.Reserved1` field. The binary dump already writes this field, so the AUP fence ID now reaches crash artifacts without changing the 64-byte telemetry stride or public method signatures. DOD pattern used: reuse reserved telemetry capacity, no hot-path allocation, no ABI expansion.

Rejected Alternatives: Expanding `AlphaLeviathanTelemetryEntry` was rejected because ARM64/Quest stride stability matters during batch integration. Adding a separate shift telemetry stream was rejected because it creates another DataVault buffer and integration dependency. Renaming `Reserved1` was rejected because downstream dump readers may already parse the public field name/offset.

Scalability potential: Low/Middle/High/Ultra all keep the same telemetry stride. Low tier gains cheap hash/fence evidence; High/Ultra keep enough dump context to diagnose SDF contour steering resets after origin rebases.

Hardware Impact: Measured savings: 0 us. Static cost is one uint store replacing a zero literal in the existing telemetry write; no profiler proof captured.

## Decision 25: Document The Wire Contract Without Renaming It

Problem: `AlphaLeviathanCognitionContracts.cs` exposed phase bytes, telemetry flags, and a 64-byte telemetry row with almost no public XML documentation. After assigning `Reserved1` to the observed AUP shift frame, leaving the field undocumented would make dump readers depend on tribal knowledge.

Solution: Added XML summaries to the public phase constants, telemetry flags, and telemetry entry fields. `Reserved1` is documented as the observed AUP shift frame ID while keeping its name and offset unchanged. DOD pattern used: public contract hygiene with zero ABI churn.

Rejected Alternatives: Renaming `Reserved1` was rejected because downstream dump readers may already parse the public field. Expanding the struct was rejected because Quest/Android layout stability and existing binary dump readers depend on the 64-byte stride. Adding an external schema document only was rejected because the C# contract itself must carry enough intent for integrators.

Scalability potential: Low/Middle/High/Ultra share the same documented telemetry row. The field meaning is stable across tier behavior and helps diagnose both low-tier Dear Lie resets and high-tier SDF contour resets after AUP shifts.

Hardware Impact: 0 us runtime. XML docs compile away; verification is compile/static only.

## Decision 26: Owners Need A Canonical Schedule Count

Problem: `TryCreateStalkJob(...)` could return a valid job from resolved DataVault views, but the caller still had to choose an `IJobParallelFor` schedule length. If any DataVault view was shorter than the requested slot count, an owner-side schedule mistake could drive `LeviathanStalkJob.Execute` past one of its NativeArray views.

Solution: Added `GetScheduleLength(in AlphaLeviathanVaultBuffers)` to compute the minimum safe row count across state, sensory, steering output, telemetry slots, and `MaxLeviathanSlots`. Added `TryGetScheduleLength(...)` plus guarded `TryCreateStalkJob(...)` overloads for both raw transient views and generation-checked handles. The existing handle-first job factory overload remains source-compatible and delegates to the guarded overload. `TryRecordTelemetryHeartbeat(...)` now refuses to write when the resolved view set has no schedulable rows. DOD pattern used: cold fail-fast integration guard around DataVault views, Burst job unchanged.

Rejected Alternatives: Adding bounds checks inside `LeviathanStalkJob.Execute` was rejected because the Omega mandate keeps the Burst job branchless and the schedule count is an owner responsibility. Changing the existing `TryCreateStalkJob` signature was rejected because batch-time public API churn can break integrators. Trusting caller folklore was rejected because the vault bridge exists to prevent repeated manual wiring mistakes.

Scalability potential: Low/Middle/High/Ultra all schedule the exact safe row count for the resolved vault state. Low tier avoids accidental out-of-bounds fault spam on partially provisioned buffers; High/Ultra keep SDF and VFX intent tied to valid rows only.

Hardware Impact: Measured savings: 0 us. Static cost is a few cold-path integer `min` operations before scheduling; hot Burst job cost is unchanged.

## Decision 27: Black-Box Dumps Must Promote Atomically

Problem: `TryDumpBlackBox(...)` streamed directly into `Dump_PREDATOR_STALK_DIRECTOR.bin`. On a slow or interrupted Steam Deck/MicroSD fault path, that can leave a half-written file that looks like the authoritative crash artifact.

Solution: The dump writer now writes to `Dump_PREDATOR_STALK_DIRECTOR.bin.tmp` with exclusive file sharing, closes the writer, then promotes the completed payload. If an older dump exists, `File.Replace(...)` preserves that final artifact unless the replacement succeeds. If no dump exists, `File.Move(...)` creates the first final artifact. Recoverable file/path failures clean up the temp file and return false.

Rejected Alternatives: Direct final-file streaming was rejected because partial crash artifacts are worse than no new artifact. Deleting the old dump before move was rejected because an interrupted promotion can erase the last useful black box. Hot-path dump writing from the Burst job was rejected because file I/O belongs on the owner cold path.

Scalability potential: Low tier and Steam Deck get bounded cold-path I/O hygiene without touching the stalking kernel. Middle/High/Ultra keep the same full telemetry payload and can still diagnose AUP shift fences, SDF contour decisions, and visual-overkill intent after a fault.

Hardware Impact: Measured savings: 0 us. Hot Burst cost is unchanged. Cold fault path adds temp-file promotion and cleanup logic; the benefit is artifact integrity, not frame-time reduction.

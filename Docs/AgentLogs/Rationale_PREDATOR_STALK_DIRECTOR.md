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

Solution: `LeviathanStalkJob` writes `AlphaLeviathanTelemetryEntry` with frame, slot, stalk phase, flags, distance, ring distance, AUP-derived positions, direction, state hash, and `LeviathanAgressivity01`. DOD pattern used: fixed-size NativeArray circular buffer.

Rejected Alternatives: `Debug.Log` was rejected because it allocates strings and is unusable in Burst. Managed queues were rejected because the black box must be fixed-size native telemetry. Full binary dump code was not added yet because there is no AI/Cognition runtime owner in domain to perform file IO safely.

Scalability potential: Low records compact hash and flags. Middle/High record same row. Ultra can add external dump handling via owner system without touching the job.

Hardware Impact: One 64-byte telemetry write per slot per scheduled tick. On MX350 this is predictable memory bandwidth, not GC; exact measurement absent.

## Decision 6: Compile Gate Still Open

Problem: `dotnet build` with no target fails because the Unity root has many project files. Broad `Hecton8.Core.csproj` and `Assembly-CSharp.csproj` builds exceeded 120 seconds before producing actionable compile errors. The generated Unity project files do not yet include the new AI/Cognition source files, while the stale `Library/ScriptAssemblies/Hecton8.AI.Cognition.dll` predates this pass.

Solution: Continue with targeted compile discovery: generated project inspection, Unity script assembly presence checks, and a pending targeted Unity/C# compile. DOD pattern used: fail-fast compile wall handling without reverting working source prematurely.

Rejected Alternatives: Declaring `dotnet build` green from static scans was rejected. Editing generated `.csproj` files was rejected because Unity overwrites them. Reverting the kernel before a real compiler error was rejected because no code error has been isolated yet.

Scalability potential: No runtime impact.

Hardware Impact: 0 us runtime impact; this is validation infrastructure state.

## Decision 7: Omega Branch Removal

Problem: The Omega mandate forbids `if` branches inside the Burst job and requires AUP shift handling so the beast does not interpolate across an origin snap.

Solution: Reworked the AUP target selection and distance fallback inside `LeviathanStalkJob` to use `math.select` and bit-mask selection instead of ternary branching. Added telemetry position sanitization and retained `ObservedShiftFrameId` versus `LastShiftFrameId` steering reset with `ShiftFenceReset` telemetry. DOD pattern used: branchless Burst selection and AUP snap-fence reset.

Rejected Alternatives: Keeping the ternary `usePing ? ping : player` was rejected because it is still a conditional target selection in the job. Interpolating through an AUP shift was rejected by the floating-origin mandate. Adding a managed `AupShiftSignal` subscription inside AI/Cognition was rejected because there is no runtime owner in this domain and the job must consume DataVault rows only.

Scalability potential: Low keeps branchless cheap steering. Middle/High keep deterministic shift reset. Ultra can attach richer shift telemetry externally while the kernel remains stable.

Hardware Impact: MX350 avoids one conditional AUP-target branch per slot and prevents post-shift steering spikes; exact microseconds saved are unmeasured.

## Decision 8: Final Validation Blocked Outside Domain

Problem: The prompt requires `dotnet build` exit 0, but the project root contains many generated `.csproj` files so bare `dotnet build` exits MSB1011. Unity batch compilation does rebuild `Hecton8.AI.Cognition.dll`, but the whole project fails in unrelated assemblies.

Solution: Verified the owned code through two gates: a targeted Roslyn compile probe over `H8Memory.cs`, `GlobalDataVault.cs`, and AI/Cognition files exits 0; Unity batch log shows `Hecton8.AI.Cognition.dll` Csc, ILPostProcess, and CopyFiles complete with `ExitCode: 0`. Marked task 18 as `[BLOCKED BY DEPENDENCY]` with exact unrelated failure owners. DOD pattern used: fail-fast three-strike compile-wall protocol.

Rejected Alternatives: Editing `Physics.Tethers.Contracts`, `Audio.Virtualization`, or editor tooling was rejected as outside AI/COGNITION domain and would be architectural sabotage without assignment. Declaring whole-project build green was rejected because Unity still reports `Scripts have compiler errors`.

Scalability potential: No runtime impact.

Hardware Impact: 0 us runtime impact; validation blocker is assembly graph/editor tooling, not AI steering cost.

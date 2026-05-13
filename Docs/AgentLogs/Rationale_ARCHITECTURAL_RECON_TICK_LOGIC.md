# Rationale_ARCHITECTURAL_RECON_TICK_LOGIC

## Decision 1 - Audit Boundary

Problem: The prompt requests a timing infrastructure audit, not feature work, while project rules require domain ownership and evidence classification.

Solution: Treat the assignment as Echelon 1 Domain 10 Tick Dispatcher & Time Dilation. Use STATIC_SOURCE and STATIC_DOC evidence only unless Unity/runtime artifacts are produced. No runtime claims beyond static inspection.

Rejected Alternatives: Reading AGENTS.md timing contract as implementation proof was rejected because QA_Evidence_Text_Filter_Audit forbids promoting text search to runtime verification. Editing dispatcher code was rejected because the prompt explicitly forbids new features.

Scalability potential: Low uses findings to remove unnecessary per-frame CPU burn on weak devices; Middle uses cadence separation to keep CPU budget predictable; High uses saved CPU for richer AI/visual simulation; Ultra can spend headroom on visual overkill while preserving deterministic cadence gates.

Hardware Impact: Static audit changes 0 microseconds directly. Expected downstream value on i3/MX350 is identifying per-frame systems that can be moved to Slow/Cold/Frost cadence without GC or job stalls.

## Decision 2 - Mandate Set

Problem: The codebase has dozens of mandates; reading all would consume audit time and raise stale-context risk.

Solution: Pin the six mandates directly relevant to tick infrastructure: Zero GC, GlobalRegistry/SystemDispatcher, Native Memory/Jobs, Debug Telemetry/Black Box, QA Evidence, and Domain/Pentarchy audit.

Rejected Alternatives: AI/Physics/Voxel-specific mandates were deferred until domain adoption inspection requires them. Graphics mandates are irrelevant unless the audit finds render tick coupling.

Scalability potential: Mandate filtering keeps the audit tied to cadence, GC, job admission, and evidence law, which are the systems that determine whether low hardware survives and high hardware scales.

Hardware Impact: Process impact only; no runtime microseconds claimed.

## Decision 3 - Verification Language

Problem: The assignment requires `STATUS: AUDIT VERIFIED`, but local evidence law forbids claiming runtime readiness from text scans.

Solution: Mark the audit artifact as `AUDIT VERIFIED` for the static-source reconnaissance only, and explicitly state that Play Mode, profiler, GCMonitor, player build, and runtime integration remain unverified.

Rejected Alternatives: Claiming zero-GC runtime behavior from source patterns was rejected. Running a broad compile was rejected because the task changed no C# and current shared workspace compile state may be affected by other agents.

Scalability potential: Honest evidence boundaries prevent false confidence that would let over-budget systems survive into MX350 profiling.

Hardware Impact: 0 microseconds saved directly. The audit isolates candidate CPU savings but does not implement them.

## Decision 4 - Bucketing Verdict

Problem: The codebase contains cadence lanes, foveated simulation, modulo/ring buffer math, and budgeted queue drains. Treating all of these as one "bucket system" would hide important differences.

Solution: Classify them separately: Slow/Cold/Frost are accumulator timers; FoveatedSimulationManager is true per-target simulation time-slicing; queue drains are budgeted work slicing; modulo hits are mostly diagnostic/ring/double-buffer patterns.

Rejected Alternatives: Calling the whole system "true simulation bucketing" was rejected because no universal entity bucket dispatcher exists. Calling it "only simple timers" was rejected because foveated per-target cadence is real.

Scalability potential: Low tier can rely on foveated and budgeted drains to skip invisible work. Middle can keep 10 Hz maintenance. High/Ultra can spend saved cycles on richer AI/presentation while keeping the same admission gates.

Hardware Impact: Static audit only. Candidate downstream gain on i3/MX350 is avoiding per-frame entity work in fauna/world/voxel systems, but measurement is pending.

## Decision 5 - Job Admission Classification

Problem: The prompt asks whether Agent 54/45 implemented token bucket or priority queue for Burst jobs.

Solution: Confirmed source-present `BurstTokenBucketJobAdmissionService` with six fixed lanes, native token arrays, EWMA cost table, AUP barrier, VFX kill switch, and blackbox ring. Classified it as token bucket, not priority queue.

Rejected Alternatives: Treating unrelated audio/content priority queues as job admission was rejected. Treating `ScheduleAdmitted` wrappers as universal adoption was rejected because 266 `.Schedule(` calls remain.

Scalability potential: Low tier denies/degrades non-critical jobs earlier. Middle/High/Ultra can admit more VFX/world jobs by budget without changing scheduling API.

Hardware Impact: Direct audit impact 0 microseconds. Downstream stall avoidance could be multi-ms in overloaded frames, but profiler proof is absent.

## Decision 6 - Second-Pass Hardening

Problem: The first report correctly identified architecture shape, but several findings were phrased qualitatively. "Broad adoption is partial" is too soft for implementation planning.

Solution: Re-ran focused no-Editor scans and added counts for actual Unity message declarations, dispatcher registration pressure, interface implementer-pattern hits, and schedule/admission wrapper hits. Kept them labeled as text hits, not profiler samples.

Rejected Alternatives: Editing runtime systems was rejected because the active assignment is an audit. Claiming measured frame savings was rejected because no profiler pass was run. Treating Editor/dev smoke tester schedules as runtime debt was rejected in the second pass by excluding Editor and calling out no-Editor counts.

Scalability potential: Low tier gets the clearest fix queue: reduce `IUpdatable` ownership, adopt true foveation where visuals tolerate it, and route heavy jobs through token buckets. Middle/High/Ultra can scale admission budgets and foveated targets without reintroducing direct Unity loops.

Hardware Impact: Static audit impact remains 0 microseconds. The highest downstream low-end target is naked job scheduling and broad per-frame dispatcher registration, especially Voxel/World/Fauna IK/UI visualizers.

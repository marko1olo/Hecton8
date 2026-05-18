# Rationale - ARCHITECTURAL_INQUISITOR_SENTINEL

Date: 2026-05-17
Status: COMPLETE STATIC FORENSIC AUDIT

## Decision 1 - Missing XML Prompt

Problem: User supplied `Prompt ID: ARCHITECTURAL_INQUISITOR_SENTINEL`, but `Docs/Tasks/CURRENT_BATCH.md` has no matching `<AGENT_PROMPT>` block.

Solution: Treat the missing XML tag as a prompt hygiene defect and continue only because the user gave an explicit one-off validator directive. Status records `0` XML tasks and `1` override task.

Rejected Alternatives: Do not impersonate a neighboring prompt; this would contaminate the architectural audit. Do not invent a fake 18-task XML block; that would violate evidence discipline.

Scalability potential: Low/Middle/High/Ultra all benefit from validator work that blocks false authority before runtime code consumes it. No runtime visual change.

Hardware Impact: 0us runtime gain on i3/MX350. Indirect gain is avoiding bad merges that would create profiler and compile churn.

## Decision 2 - Audit Scope

Problem: Request is "forensic audit of all agent logs and code for architectural treason" while the worktree is already dirty with concurrent edits.

Solution: Keep validator edits confined to `Docs/Tasks` and `Docs/AgentLogs`; run static scans and classify evidence. Do not repair runtime code during the audit unless a fatal compile/logging artifact must be written.

Rejected Alternatives: Directly mutate runtime source during the first scan; unsafe under 20+ agent concurrency. Revert dirty files; forbidden because existing edits may belong to other agents.

Scalability potential: Low tier gets less risk from unbounded systems. High/Ultra keeps cycles for visual overkill only after architecture proof.

Hardware Impact: 0us direct runtime gain. Expected prevention value: avoids unprofiled hot-path work on i3/MX350.

## Decision 3 - Mandate Set

Problem: Audit needs a compact mandate baseline without loading the whole registry.

Solution: Read 8 mandates: evidence text filter, registry/DI, signal segregation, execution phases, zero-GC, native memory/jobs, crash telemetry, and AUP determinism.

Rejected Alternatives: Bulk-read 78 mandates; wastes context and increases noise. Read only one evidence mandate; insufficient for code architecture audit.

Scalability potential: Covers Low/Middle/High/Ultra by enforcing typed lanes, bounded phases, DataVault memory, and 300-frame black-box proofs.

Hardware Impact: 0us runtime gain. Audit focuses on defects that commonly cost >0.1ms or trigger GC on i3/MX350.

## Decision 4 - Live Batch Mutability

Problem: `Docs/Tasks/CURRENT_BATCH.md` changed during the validator run and still never contained `<AGENT_PROMPT id="ARCHITECTURAL_INQUISITOR_SENTINEL">`. Final snapshot exposes SHINOBU prompts, not the requested validator prompt.

Solution: Classify the missing tag and live mutation as P0 prompt authority failure. Proceed only with the explicit user override, and make the report a static forensic artifact rather than pretending XML authority exists.

Rejected Alternatives: Do not adopt `SHINOBU_01` or any neighboring prompt. Do not infer task count from unrelated XML. Do not edit the batch file, because that would destroy audit evidence.

Scalability potential: Low/Middle/High/Ultra all depend on stable task routing. A mutable or wrong batch can send low-end devices into unnecessary systems and high-end devices into incompatible overkill paths.

Hardware Impact: 0us direct runtime gain on i3/MX350. Prevents bad code from entering frame-critical lanes under false prompt authority.

## Decision 5 - Active Batch Mocking Is A Hard Violation

Problem: The live batch instructs agents to define mock buses, mock signals, local partial signal structs, mock terrain, and generated fake schemas when dependencies are absent. The project already has real `MemoryAddressShiftSignal` in `GlobalSignals.cs` and dispatcher consumers/publishers.

Solution: Mark this as P0 active-batch architectural treason. Required pattern is GlobalRegistry contracts or EventBus/SignalBus lanes with real shared signal definitions, not local partial stand-ins that can compile in isolation and collide at integration.

Rejected Alternatives: Accept mocks as "unit test scaffolding"; this repo's rule set rejects invented direct dependencies and isolated fake architecture. Use generated code per-agent; that only hides integration breaks until merge.

Scalability potential: Low lane needs deterministic cheap signals with no duplicate structs. Ultra lane needs the same contract so extra visual systems can subscribe without semantic drift.

Hardware Impact: 0us direct runtime gain in this audit. Avoids integration churn and potential hot-path adapter layers that would cost >0.1ms when patched late.

## Decision 6 - DataVault Sovereignty Gate

Problem: The DataVault sovereignty audit failed closed: baseline missing, 1085 forbidden direct `NativeArray<T>` constructors, 2643 forbidden field-like declarations, and hundreds of affected files.

Solution: Treat current native memory ownership as a P0 architecture gate failure. Do not auto-refactor it in the validator pass because this spans many domains and concurrent agents.

Rejected Alternatives: Hand-wave direct native containers as "probably okay"; this violates the DataVault mandate. Bulk-rewrite ownership during audit; high merge risk and no per-domain proof.

Scalability potential: Low lane requires centralized ownership to avoid allocator churn. Ultra lane requires the same ownership model so extra memory can buy visuals instead of fragmentation and lifetime bugs.

Hardware Impact: 0us direct runtime gain from reporting. Remediation target is large but unmeasured; exact gain cannot be claimed without profiler.

## Decision 7 - Compile Evidence Boundary

Problem: Prior logs contained strong build language, but product readiness requires more than Core C# compilation.

Solution: Ran a fresh Core restore/build gate and recorded only what it proves: Core compiled, 0 warnings, 0 errors, 00:01:26.04. Unity import/playmode/player/profile gates were not run and are not claimed.

Rejected Alternatives: Claim "build green" globally. Use old logs as current proof. Ignore missing restore assets after the first no-restore failure.

Scalability potential: Low/Middle/High/Ultra all need honest gates so content/runtime claims do not outrun proof.

Hardware Impact: 0us runtime gain. Prevents false readiness reports from masking real device budget failures.

## Decision 8 - Follow-Up Compile Failure From Parallel Runtime Changes

Problem: After the initial validator report, `CURRENT_BATCH.md` expanded to 30 SHINOBU prompts and runtime source gained prompt-directed mock artifacts. A fresh Core build now fails with 24 errors.

Solution: Preserve the failure as integration evidence and do not patch other agents' runtime code from the validator lane. Root causes were narrowed to cross-domain signal namespace/project inclusion mismatch, readonly telemetry mutation in `GlobalWorldSampler`, and invalid fixed-buffer pointer use in `SaveDeltaCompression`.

Rejected Alternatives: Blindly add `SignalWardenRuntime.cs` to the generated csproj by hand; Unity project files are generated and this would hide the real asmdef/source-placement issue. Revert other agents' files; forbidden. Continue claiming Core green from the earlier build; false after new disk changes.

Scalability potential: Low/Middle/High/Ultra are all blocked by compile failure. Mock/fallback artifacts also undermine deterministic low-tier lanes and high-tier overkill subscribers.

Hardware Impact: 0us runtime gain from reporting. Build is broken, so runtime frame-time claims are invalid until owners repair compile and profiler gates rerun.

## Decision 9 - Targeted Compile-Wall Repair Was Allowed

Problem: The polish mandate required continued work after the follow-up Core build failed, but the repository is under heavy parallel edits and the validator has no XML runtime ownership block.

Solution: Apply only narrow compiler-error repairs with local evidence: make `GlobalWorldSampler.WriteTelemetryWarning` mutable instead of `in`, replace self-comparison finite check with `math.isnan`, remove illegal fixed-buffer pinning in `SaveDeltaCompression`, restore the missing audio virtualization contract file and GUID, disambiguate `MockSDFSampler` in `SpatialAudioManager`, import `Hecton8.World` for `DispatcherJobSwap`, and update `ShinobuLootMagnetSpatialQueryJob` to `NativeParallelMultiHashMap`.

Rejected Alternatives: Do not hand-edit generated `Hecton8.Core.csproj`; duplicate compile includes already produced CS2002. Do not revert parallel agent files. Do not broaden the pass into a full Save/Audio/World refactor without XML authority.

Scalability potential: Low/Middle/High/Ultra all benefit from restoring the compile gate because no runtime budget can be measured while Core fails to build. These are build integrity fixes, not visual upgrades.

Hardware Impact: 0us measured runtime gain on i3/MX350. Compile recovered to 0 warnings/0 errors, elapsed 00:00:07.18 in the final Core attempt.

## Decision 10 - Parallel Agent Repair Was Preserved

Problem: `H8BinaryWorldPager.cs` initially failed on missing `EnsureDirectoryPage`, but before a local patch was made the file changed and now contains directory-page logic, directory header constants, and offset rebasing.

Solution: Treat the pager change as parallel work, re-read the file, and rerun the build instead of overwriting it. The next Core build passed, proving the current disk state rather than the stale failure.

Rejected Alternatives: Add a second `EnsureDirectoryPage` implementation; that would risk duplicate methods or semantic drift. Revert the pager; forbidden and unnecessary.

Scalability potential: Low tier benefits from preserving staged directory and WAL work that avoids main-thread random I/O. High/Ultra remain blocked on runtime profiling, not compile.

Hardware Impact: 0us directly measured by this validator. Pager blackbox layout was reviewed only as source evidence; no I/O profiling was run.

## Decision 11 - Architecture Gates Still Fail Despite Core Green

Problem: The final Core CLI build is green, but the mandate asked for ARM64, H-Phi, diff hygiene, blackbox, and 20-task proof. Current disk evidence does not satisfy those gates.

Solution: Rerun static gates and record failures: `Pack = 1` scan found 745 matches under `Assets/_Project/Scripts`; DataVault sovereignty audit failed closed with 1114 direct constructors and 2947 forbidden declarations; `git diff --check` still fails on current batch trailing whitespace and line-ending churn.

Rejected Alternatives: Do not report "titanium" or "complete" from Core build alone. Do not mass-edit 745 layout declarations from a validator lane because many may be binary file formats or interop records requiring owner review.

Scalability potential: Low/Middle/High/Ultra remain at risk until memory ownership and runtime struct layout are partitioned by domain and audited with binary compatibility proof.

Hardware Impact: 0us measured runtime gain. The likely impact is real on ARM64 and low-end devices, but exact microseconds cannot be claimed without profiler/device proof.

## Decision 12 - 20-Task Matrix Cannot Be Invented

Problem: The polish mandate says the original XML assignment had exactly 20 tasks, but `Docs/Tasks/CURRENT_BATCH.md` still has no `<AGENT_PROMPT id="ARCHITECTURAL_INQUISITOR_SENTINEL">` block.

Solution: Mark Tasks 01-20 as not authoritative in the forensic SELF_AUDIT rather than inventing a task list from neighboring SHINOBU prompts. The explicit user override remains a single validator/polish task.

Rejected Alternatives: Borrow `SHINOBU_02` or another visible 20-task block. That would be prompt contamination and would make the report false.

Scalability potential: Stable task authority is a prerequisite for scalable parallel development; wrong-domain work causes compile walls and runtime contract fragmentation.

Hardware Impact: 0us runtime gain. Prevents false completion reports that would waste developer iteration time.

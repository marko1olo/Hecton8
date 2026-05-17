# Mandate Remote Audit - 2026-05-15

Agent: GIT_CONFLICT_RESOLUTION
Scope: remote-incoming mandate updates from the other checkout, focused on `.agents-skills`.
Status: ACCEPTED WITH TEXT-HYGIENE PATCH / RUNTIME PENDING VERIFICATION

## Evidence Boundary

- Current Git sync before audit: `origin/main...HEAD = 0 0`.
- Full two-day incoming report: `Docs/AgentLogs/RemoteIncoming_Day2_GIT_CONFLICT_RESOLUTION_20260515.md`.
- Primary mandate range: `1875424c7..926ed7a55`.
- Commits in primary range:
  - `953354e7b` - `Add auxiliary batch data and tooling outputs`
  - `d2c221e51` - `Update auxiliary batch artifact revisions`
  - `926ed7a55` - `Record auxiliary batch verification logs`
- Follow-up mandate-cleanup checkpoint seen in incoming audit: `d3fb78a93`.

## What Changed

- `.agents-skills` primary range: 56 files changed, 3848 insertions, 3338 deletions.
- New mandate files:
  - `.agents-skills/ARCH_Execution_Phases.txt`
  - `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
  - `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- README inventory claim: 78 `.txt` mandates.
- Filesystem count: 78 `.txt` mandates.

## Correct Parts

1. `ARCH_Execution_Phases.txt` is directionally correct.
   It formalizes the existing dispatcher split: pre-simulation signal/input drain, simulation mutation, post-simulation swap/telemetry, and late-frame visual sync. Current code does not expose a literal phase enum everywhere, but `SystemDispatcher` already has `FlushPreSimulation`, post-fixed lanes, late-frame lanes, and post-simulation snapshot clearing. Treat this as authority language over existing dispatcher concepts, not proof that every system is compliant.

2. `ARCH_Signal_Lane_Segregation.txt` is correct as a new-gameplay rule.
   It does not break AGENTS.md because it explicitly allows legacy `EventBus` / `GlobalSignals` to remain as queue infrastructure. Current code has `ISignal`, `SignalBus<T>`, `SignalBusRegistry`, `ReadOnlySpan<T> GetFrameSnapshot()`, and many current consumers. The mandate correctly rejects dumping new gameplay traffic into one monolithic lane.

3. `MATH_AUP_Determinism_Sync.txt` is aligned with current AUP doctrine.
   The 300-frame Sync-Fence, millimeter quantization, drift probes, stale shift-id rejection, and dump-on-fault behavior match existing Black Box/AUP direction. Current source and archived task evidence show `AupShiftSignal`, 300-frame telemetry, and multiple dump paths, but full runtime compliance remains unproven.

4. README authority model is correct.
   It keeps `AGENTS.md` above `.agents-skills`, treats dated reports as evidence only, and explicitly marks runtime proof as `PENDING VERIFICATION`.

5. Evidence language is materially improved.
   The updates correctly reject false `VERIFIED`, `0 GC`, platform, profiler, and microsecond claims unless an artifact path/tool/timestamp/evidence class is named.

## Defects Found

1. Advisory-language cleanup was incomplete.
   README says new mandate text rejects `consider`, `maybe`, `should`, and `recommended` unless quoted as a banned pattern. The incoming text still left several unquoted advisory words in mandate files. That is not an architectural rollback blocker, but it is a registry-quality defect.

2. Runtime compliance is not proven.
   The mandate author status/logs explicitly say runtime is pending. Current code still contains legacy raw Unity-loop methods in dispatcher-owned or exceptional paths, direct `GlobalSignals` infrastructure, and local/native allocation surfaces under migration. These are acceptable only because the new text is a governing target, not a claim that all code already complies.

3. DataVault sovereignty is stricter than current source reality.
   `GlobalDataVault` exists and many systems use `IDataVault`, but source still has fallback/local `NativeArray` and `NativeQueue` paths. The mandate is correct as target architecture; it must not be reported as complete implementation.

## Patch Applied By This Audit

To make the registry obey its own command-language rule, this audit changed only text wording:

- `.agents-skills/AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`: `Recommended values` -> `Default values`.
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: replaced `Consider class...` and `Should Fix`.
- `.agents-skills/PHYS_Fluid_Incursion_Interior.txt`: `Recommended tau` -> `Default tau`.
- `.agents-skills/PHYS_Kinematic_Interaction_Hands.txt`: `Recommended` -> `Tuning range`.
- `.agents-skills/STRM_ModuleDTO_LZ4_Dictionary.txt`: `Recommended pipeline` -> `Required pipeline`.

Post-patch scan result: the only remaining advisory-word hit is the README line quoting the banned words.

## Verdict

The mandate updates are mostly correct and should stay. They are stricter governance, not runtime proof. No rollback is justified.

Required interpretation for future agents:

- Use these mandates as authority.
- Do not claim project-wide compliance without Unity/import/build/profiler/GCMonitor/player evidence.
- Treat phase, SignalBus, DataVault, and AUP Sync-Fence compliance as migration requirements unless a specific system has fresh proof.
- Continue using `AGENTS.md` as the top authority when wording conflicts.

Date: 2026-05-19
Agent: SHINOBU_79
Status: IMPLEMENTED; STATIC VERIFIED; UNITY COMPILE NOT LAUNCHED

Decision 00 - QA watchdog boundary
Problem: The task requires a headless 10km endurance bot, but other agents own input, AUP rebase, SystemHealthIndex, and crash dump systems.
Solution: Use a local QA watchdog runtime with unmanaged DTOs and mock-compatible interfaces/structs. Integrate through cold activation and stable DTO surfaces only; do not create hard dependencies on unfinished concrete systems.
Rejected Alternatives: Player-prefab MonoBehaviour autoplay was rejected because it couples QA to hierarchy and XR/Input Manager. Direct calls into Agent 30/33 systems were rejected because those contracts are not proven current.
Scalability potential: Low uses sparse sampling and compact CSV rows. Middle increases probe cadence. High records richer slope metrics. Ultra spends saved overhead on denser telemetry and editor visual forensics.
Hardware Impact: MX350/i3 expected gain is avoiding manual scene replay and avoiding GC-induced false leak positives; hot-path budget target under 100 microseconds with CSV cadence throttled.

Decision 01 - File output strategy
Problem: CSV telemetry can become the leak if it formats strings or opens files per frame.
Solution: Keep a persistent FileStream and preallocated byte staging buffer. Write ASCII numerics manually. Headers are cold-path only.
Rejected Alternatives: File.AppendAllText, string.Format, interpolation, StreamWriter, and per-row string construction were rejected as managed allocation hazards.
Scalability potential: Low writes less often. Middle writes standard cadence. High/Ultra can increase telemetry frequency while preserving the same staging path.
Hardware Impact: MX350/i3 avoids GC spikes from managed logging; expected save is variable but any per-frame string allocation is eliminated from the watchdog-owned path.

Decision 02 - DTO layout
Problem: WatchdogStateDTO and TelemetrySnapshotDTO must survive ARM64 alignment and NativeArray/Burst use.
Solution: Sequential unmanaged structs with explicit fields matching the prompt byte layouts; no properties; no Pack=1; unsafe layout asserts in editor/test entry points.
Rejected Alternatives: Auto-properties, classes, bool fields, and packed structs were rejected because they create copies, managed metadata risk, or misaligned ARM64 reads.
Scalability potential: Same layout across tiers. Ultra can add separate extended telemetry DTOs rather than bloating truth structs.
Hardware Impact: Stable 40B/16B records reduce cache waste and prevent ARM64 unaligned penalties.

Decision 03 - Archive binary fallback
Problem: Task 01 required locating qa_waypoints.h8bin/endurance_profiles.bin layouts in Archive Batch005-007, but targeted scans found no matching binary names or rationale lines.
Solution: Treat the missing archive payload as an expected cold-path failure and keep GenerateEmergencyMockRoute() as the boot fallback: four 32-byte waypoint records with 16-byte aligned AUP payloads covering a 10km path.
Rejected Alternatives: Blocking on absent OSHINO data or parsing unrelated archive rationale was rejected because the CI endurance run must be self-contained.
Scalability potential: Low uses the four-point route and 4Hz CSV; Middle can consume qa_bot_waypoints.csv; High increases route complexity; Ultra can replace the same DTO buffer with real h8bin data without changing the runtime contract.
Hardware Impact: MX350/i3 avoids asset-database lookups and managed parsing in hot runtime; expected boot-path save is dominated by avoiding failed broad archive reads, not per-frame cost.

Decision 04 - SHINOBU_79 over duplicate runtime
Problem: A nearly complete SHINOBU_38 headless watchdog existed in the QA domain, but cloning it would create two systems sharing the same flag and input buffer.
Solution: Surgical reuse: re-identify paths/results/dumps to SHINOBU_79 and keep class names stable to minimize Unity meta and asmdef churn.
Rejected Alternatives: New MonoBehaviour bot or renaming every type was rejected because it increases prefab/meta risk and compile surface without functional gain.
Scalability potential: Low/Middle/High/Ultra all use the same VaultBufferHandle IDs; only CSV cadence, route data, and quality modulation alter load.
Hardware Impact: MX350/i3 gains from avoiding duplicate watchdogs and duplicate FileStreams; expected save is one avoided background writer and one avoided input publisher during QA runs.

Decision 05 - Input bridge without XR/Input Manager
Problem: Writing only BufferID.ShinobuInputCurrentDto satisfies the byte contract, but current InputDispatcher consumes gameplay automation from PhysicsDeterminismSignals.TryConsumeLatestInputOverride().
Solution: Keep the unmanaged DataVault write, then mirror the same bytes into PhysicsDeterminismSignals.PublishInputOverride using the Unity frame counter so KCC consumes it without touching XR or Input Manager.
Rejected Alternatives: Direct XR device emulation, Player prefab scripts, or adding a QA dependency to Input assembly were rejected as hierarchy-coupled or assembly-hostile.
Scalability potential: Low is one override per frame; Middle/High can add action masks; Ultra can layer richer combat pulses while preserving the same DTO.
Hardware Impact: MX350/i3 cost is a small struct copy and static signal write; it prevents manual regression passes and avoids managed input synthesis.

Decision 06 - GlobalQualityWeight stress curve
Problem: The watchdog previously published SHI stress only; it did not mathematically force GlobalQualityWeight through the required 0.1-to-1.0 thermal recovery transition.
Solution: Apply HomeostasisBrain.SetForcedGlobalQualityWeightForTuner() on a 600s loop: 300s clamp at 0.1, 60s smoothstep recovery to 1.0, remainder at full quality; release override on finish/destroy.
Rejected Alternatives: Binary low/high tier flags and one-shot quality writes were rejected because the project forbids dichotomy and needs transition stress.
Scalability potential: Low=0.1 survival pressure, Middle=continuous recovery slope, High=full quality baseline, Ultra=repeatable overkill phase with the same loop.
Hardware Impact: MX350/i3 exposes quality-collapse regressions early; expected frame cost is one float comparison and rare static setter call due QualityEpsilon gating.

Decision 07 - Legacy autorun collision removal
Problem: Legacy QAEnduranceWatchdogBot also responded to H8_QA_ENDURANCE_10KM and -h8QaEndurance*, causing two bots to fight over automation when SHINOBU_79 launches.
Solution: Restrict legacy autorun to explicit H8_QA_LEGACY_ENDURANCE, -h8QaLegacyEndurance, or Temp/H8_QA_LEGACY_ENDURANCE.flag.
Rejected Alternatives: Leaving both active or deleting the legacy file was rejected. Leaving both creates nondeterminism; deletion risks unrelated QA callers.
Scalability potential: Low through Ultra all run one authority for endurance; legacy remains opt-in for forensic comparison.
Hardware Impact: MX350/i3 avoids duplicate writer/input work and removes a potential false memory leak source.

Decision 08 - Build guard result
Problem: The project rules forbid launching dotnet/Unity builds while CPU exceeds 50 percent or csc.exe is active.
Solution: The latest guard sample is CPU=100.00 percent with csc.exe count 1, so compilation remains blocked by policy. A separate target scan found no Hecton8.QA.Headless.csproj or sln entry for these QA Headless files. Static scans covered forbidden logging APIs, properties, Pack=1, AutoPlayer, asmdef references, generated target presence, and active code paths.
Rejected Alternatives: Forcing a broad dotnet build was rejected because it would not verify the ungenerated QA Headless assembly and the user explicitly ordered not to launch dotnet build until necessary.
Scalability potential: Low/Middle/High/Ultra unaffected; this is a workstation contention guard.
Hardware Impact: MX350/i3 avoids compounding compiler workload or running irrelevant builds; no runtime microsecond gain claimed.

Decision 09 - Wall-clock stress timing
Problem: The first pass drove GlobalQualityWeight and SHI stress from fast-forwarded simulation seconds or frame count, and the second pass still used unscaled tick delta. Manual batch stepping can execute hundreds of ticks per editor update, so delta-based wall time is not wall time.
Solution: Track _qualityWallSeconds from Stopwatch.GetTimestamp() deltas, clamped to 1s per sample for pause resilience. GlobalQualityWeight now clamps at 0.1 for 300 real elapsed seconds, smoothstep-recovers for 60 real elapsed seconds, then dwells at 1.0. SHI sabotage now publishes 0.95 for 10 real elapsed seconds per 60s cycle.
Rejected Alternatives: Frame-count modulo, simulation-duration timing, and manual-step delta timing were rejected because fast-forward/manual batch stepping invalidates the thermal soak requirement.
Scalability potential: Low spends a real five minutes in survival quality. Middle validates recovery. High validates normal quality after recovery. Ultra repeats the same cycle to expose long-run state leaks.
Hardware Impact: MX350/i3 gets real sustained load-shedding exposure; per-frame cost is one phase calculation and one epsilon-gated quality setter.

Decision 10 - Explicit catastrophic AUP delta audit
Problem: The first pass checked millimeter float-downcast jitter but did not explicitly implement the prompt's >500m inter-frame local delta failure gate.
Solution: Store the last audited double3 AUP, subtract before casting to local float3, and flag VaultFlagFatal when length(localDelta) > 500m without the current MockRebaseSignal.
Rejected Alternatives: Relying only on target-relative jitter or absolute double comparisons was rejected; the watchdog must catch frame-to-frame coordinate explosions.
Scalability potential: Same audit across all tiers; Ultra can add richer blackbox fields later without changing the fatal gate.
Hardware Impact: MX350/i3 cost is one local vector subtraction and length check per watchdog frame, estimated 1-3 us.

Decision 11 - Input frame handshake containment
Problem: InputDispatcher consumes automation overrides by comparing PhysicsDeterminismSignals frame against its own Time.frameCount. Publishing SHINOBU_79's fast-forward simulation frame would age out the override and break the Dear Lie input path.
Solution: Use Time.frameCount only for the input override handshake. Movement integration, quality soak, SHI pulse duration, memory windows, and AUP audit do not use Unity Time.frameCount for state evolution.
Rejected Alternatives: Publishing the simulation _frame to PhysicsDeterminismSignals was rejected because the existing consumer uses engine frame age. XR/Input Manager injection was rejected again because it violates the prompt.
Scalability potential: Low through Ultra keep one compatible input lane; richer action masks can be added without changing frame authority.
Hardware Impact: MX350/i3 cost is unchanged; this prevents silent no-op automation that would make the endurance bot look alive in logs while the KCC ignores input.

Decision 12 - Quality-weighted SDF normal collapse
Problem: The avoidance path still paid for rich SDF normals whenever a wall was detected, even while GlobalQualityWeight was clamped to 0.1 for the thermal stress soak.
Solution: Feed the forced GlobalQualityWeight into BotNavigationJob. Below 0.3, math.step collapses the rich normal path to a cheap analytic normal; above the threshold, smoothstep-style polynomial blending lerps into the six-sample SDF gradient.
Rejected Alternatives: Always sampling the rich normal was rejected because it hides low-tier ALU shedding failures. A binary hardware flag was rejected because HECTON-8 requires continuous GlobalQualityWeight math.
Scalability potential: Low uses one SDF distance plus analytic normal. Middle blends toward gradient. High uses the gradient. Ultra can spend the reclaimed CPU on denser telemetry or richer downstream visual validation without changing the input contract.
Hardware Impact: MX350/i3 avoidance frames avoid six extra SDF samples below 0.3 quality. Static estimate is 4-12 us saved on CPU-bound avoidance frames, unmeasured because compile/playmode was blocked.

Decision 13 - SHI recovery flag instead of sticky emergency
Problem: The first sabotage path could prove entry into LowTierEmergency but not clean recovery; a sticky low-tier bit would make the forensic CSV ambiguous.
Solution: Track whether SHINOBU_79 owns the current stress pulse. When the pulse ends and SHI drops below 0.9, clear VaultFlagLowTierEmergency and set VaultFlagStressRecoveryObserved plus TelemetryFlagStressRecovered.
Rejected Alternatives: Leaving the low-tier flag sticky was rejected because it cannot distinguish a valid recovery from a permanent load-shedding failure. Clearing all vault flags was rejected because it would erase unrelated memory/fatal evidence.
Scalability potential: Low validates survival-mode entry. Middle validates recovery slope. High and Ultra validate that forced stress does not permanently pin quality after release.
Hardware Impact: MX350/i3 cost is one boolean and bitmask update in POST_SIMULATION; no measurable runtime claim beyond forensic correctness.

Decision 14 - Actual KCC AUP audit bridge
Problem: AUP jitter should compare the intended path against the engine-produced KCC AUP when that signal exists. Auditing only the next waypoint is too coarse and can miss KCC output drift.
Solution: Read PhysicsDeterminismSignals.TryGetLatestKccVelocity opportunistically, reject stale samples older than two engine frames, subtract vault.CurrentAUP from BodyAup before casting to float3, then reconstruct and flag >1mm error.
Rejected Alternatives: A target-waypoint-relative audit was rejected because the waypoint is not the bot's current intended position. Direct Gameplay/KCC assembly references were rejected because QA Headless must stay behind Core signal lanes.
Scalability potential: Low and Middle get optional real KCC output verification when available. High and Ultra can use the same signal for richer postmortem without adding a sibling assembly reference.
Hardware Impact: MX350/i3 adds a small optional signal read and local double3 subtraction, estimated 1-3 us on frames where KCC publishes.

Decision 15 - CLI tail parsing
Problem: The command-line parser ignored inline values when the inline argument was the final argv element, which can silently discard CI tuning such as -h8qaSpeed=8.
Solution: Iterate all argv entries, check inline key=value first, then check separated key value only when i + 1 exists. Apply the same pattern to -h8qaTier.
Rejected Alternatives: Separated-only CLI was rejected because CI scripts commonly use inline values. Throwing on malformed args was rejected because QA activation must fail soft to safe defaults.
Scalability potential: Low through Ultra can be selected from CI without editor state. The tuning surface remains cold-path only.
Hardware Impact: No hot-path cost; cold startup avoids false runs using default speed/cadence.

Decision 16 - Shared input DTO contract alignment
Problem: SHINOBU_79 originally defined a local Shinobu38InputStateDTO with the same 24-byte shape as InputDispatcher's InputStateDTO, then requested BufferID.ShinobuInputCurrentDto with that local type. Same-size shadow DTOs are still a DataVault contract hazard because handle typing, ownership, and future ABI changes can diverge silently.
Solution: Delete the local shadow DTO and request VaultBufferHandle<Hecton8.Core.InputStateDTO> for BufferID.ShinobuInputCurrentDto, matching InputDispatcher.EnsureDeterministicInputNativeBuffers exactly. BotNavigationJob writes the canonical struct; PhysicsDeterminismSignals still receives the mirrored PlayerInputState override.
Rejected Alternatives: Keeping the same-size local DTO was rejected because identical layout is not a durable contract. Referencing Hecton8.Input.Determinism.InputStateDTO was rejected because QA Headless does not reference that sibling asmdef and Core already owns the active buffer contract.
Scalability potential: Low through Ultra use the same input ABI; future input fields change in one authority file rather than a QA duplicate.
Hardware Impact: No hot-path cost difference. It prevents a runtime handle mismatch and avoids undefined automation behavior during long endurance runs.

Decision 17 - Actual AUP position error, not reconstruction-only
Problem: The optional KCC AUP audit measured float reconstruction error but did not flag a real position mismatch between the intended watchdog AUP and the engine-produced BodyAup if the mismatch was representable in float.
Solution: Subtract intended vault.CurrentAUP from actual BodyAup first, cast the local delta to float3, and record the worst of local position delta and reconstruction error. Any worst error above 1mm increments ErrorCount and sets VaultFlagActualAupJitter.
Rejected Alternatives: Reconstruction-only auditing was rejected because it proves float precision but not the prompt's intended-path-versus-actual-position requirement. Absolute double comparisons were rejected because AUP rules require subtracting before float math.
Scalability potential: Low and Middle catch coarse KCC drift. High and Ultra still get float precision reconstruction telemetry for overkill forensic detail.
Hardware Impact: Adds one local vector length on frames with KCC output; estimated 1 us on low-end CPUs, unmeasured because build/playmode remains blocked.

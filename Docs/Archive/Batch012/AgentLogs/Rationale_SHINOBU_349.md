# Rationale_SHINOBU_349

Status: STATIC VERIFIED / PRIVATE NATIVE MIRRORS EVICTED / COMPILE BLOCKED BY RESTORE + CPU POLICY

## Decision 0: Authority Route

Problem: Narrative POI triggers need to replace collider callbacks without creating a new hot global route.
Solution: Use unmanaged DTO buffers for POI state, deterministic Burst jobs for spatial and prerequisite checks, and typed `SignalBus<T>`/NativeQueue progression payloads for decoupled broadcast.
Rejected Alternatives: Unity trigger colliders and `QuestManager.Instance.HasCompleted(string)` are managed, broadphase-heavy, and not Burst-compatible.
Scalability potential: Low = cadence throttled and same-cell POI checks only; Middle = stable local-cell checks at normal cadence; High = denser telemetry and debug visuals; Ultra = editor analytics and richer presentation consumers outside gameplay truth.
Hardware Impact: Estimated MX350/i3 gain is removal of PhysX trigger broadphase cost and managed string quest checks; measured proof absent.

## Decision 1: DTO Layout

Problem: POI data must be safe for ARM64 and cache-local for Burst traversal.
Solution: Define `NarrativePoiDTO` as explicit 64-byte layout with `double3` at offset 0, scalar fields at mandated offsets, and explicit padding fields.
Rejected Alternatives: auto-layout structs, properties, or class wrappers can create defensive copies, alignment drift, and managed heap traffic.
Scalability potential: Low/Middle/High/Ultra all share identical gameplay truth; only cadence and diagnostics scale with `GlobalQualityWeight`.
Hardware Impact: Estimated benefit is one cache-line DTO reads and no unaligned 8-byte loads on ARM64; measured proof absent.

## Decision 2: Existing Owner Integration

Problem: `HectonNarrativeDirector` already owns `ISpatialTriggerSystem`; a new POI manager would split save authority and GlobalRegistry ownership.
Solution: Convert `HectonNarrativeDirector` to `partial`, add `HectonNarrativeDirector_PoiTriggers.cs`, and route AUP POI evaluation plus managed presentation metadata through Vault-backed `NarrativePoiDTO` and `NarrativePoiPresentationDTO` arrays.
Rejected Alternatives: A standalone `HectonPoiManager` would compete with `GlobalRegistry.SpatialTriggerSystem` and duplicate narrative save state.
Scalability potential: Low = same-cell only checks at slow cadence; Middle = stable 100m hash buckets; High = editor telemetry graph and gizmos; Ultra = dense POI datasets via CSV/mock jobs without changing truth layout.
Hardware Impact: Estimated i3/MX350 gain is replacing old O(N) float3 scan, private native mirror reads, and managed post-trigger checks with same-bucket Burst evaluation plus a Vault presentation lane; measured proof pending compile/runtime.

## Decision 3: Signal Lane Bridge

Problem: Existing `ProgressionEventSignal` producers push through `SignalBus<T>`, but legacy consumers still call `GlobalSignals.TryDequeueProgressionEvent`.
Solution: Patch `TryDequeueProgressionEvent` to consume `SignalBus<ProgressionEventSignal>` first, then fall back to the legacy direct queue.
Rejected Alternatives: Creating a new story-zone signal would fragment progression routing and strand `MetaCampaignService` consumers.
Scalability potential: Low/Middle/High/Ultra share the same unmanaged signal lane; only queue capacity and telemetry pressure scale.
Hardware Impact: Estimated gain is no managed event fanout in the trigger solver and no extra bridge queue; measured proof pending compile/runtime.

## Decision 4: Prerequisite And State Bitmasks

Problem: Existing authoring POIs expose discovery hashes and bit indices but no Burst-safe prerequisite object graph.
Solution: Store prerequisite truth as `ulong PrerequisiteBitmask` in `NarrativePoiDTO`; read completed quest truth from `QuestDagGlobalStateMasks[0]`; map authored first-hour quest hashes through generated `H8QuestMasks`; flip Vault-owned `NarrativePoiStateMasks` word bits for one-shot POI state.
Rejected Alternatives: Querying `QuestManager`, `IQuestSystem.IsActive(string)`, or discovery `HashSet<string>` inside spatial evaluation is managed and non-Burst.
Scalability potential: Low = zero-prereq authored POIs still work; Middle/High = CSV can author bitmask prerequisites; Ultra = 10k mock POIs stress the same primitive.
Hardware Impact: Estimated per-candidate prerequisite cost is one `AND` and compare; measured proof pending compile/runtime. Authored-mask resolution is cold rebuild work, not Burst tick work.

## Decision 5: Black Box And Fault Route

Problem: NaN AUPs or slow POI evaluation cannot be diagnosed from chat or logs after a crash.
Solution: Add a Vault-owned 300-entry `AupNarrativeTriggerTelemetryEntry` ring and raw span/memory-map dump to `Docs/AgentLogs/Dump_SHINOBU_349.bin` when invalid AUP data or >0.1ms execution is detected.
Rejected Alternatives: Managed `BinaryWriter` per tick or postmortem verbal reports add hot-path cost and lose pre-fault history.
Scalability potential: Low = sparse telemetry only; Middle = stable ring inspection; High/Ultra = editor graph/gizmo reads the same ring without changing gameplay truth.
Hardware Impact: Estimated hot-path telemetry write is one fixed DTO per solver tick; disk dump is cold fault-only. Runtime measurement pending compile/runtime.

## Decision 6: Scanner Report Ownership

Problem: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` is shared by concurrent agents; a blunt writer would erase unrelated proof sections and convert one non-story audio reverb trigger into a false narrative-trigger failure.
Solution: Add `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_349.json` as the authoritative sidecar and merge a top-level `SHINOBU_349_AUP_Narrative_Poi_Trigger_Scanner` section into the shared report through `JObject` upsert. Classify hits as `story` or `non-story`; current story hit count is 0. Scanner detection now uses Roslyn syntax nodes with token fallback only for parser failure.
Rejected Alternatives: Overwriting the shared JSON, string-splicing shared report text, token-only scanner proof, or reporting only a chat claim. These destroy evidence quality under 20+ agent concurrency.
Scalability potential: Low = sidecar proof survives report races; Middle = shared report remains machine-readable; High/Ultra = scanner can be expanded without touching runtime truth.
Hardware Impact: Editor-only cold scan; no runtime cost on i3/MX350.

## Decision 7: Compile Gate

Problem: Final build verification is mandated, but local policy forbids launching `dotnet build` when CPU is above 50% or csc/dotnet is active.
Solution: Do not launch build under load. Record static proofs: DTO explicit layout, deterministic Burst attributes, same-cell hash path, SignalBus bridge, JSON parse, and `git diff --check`.
Rejected Alternatives: Starting a build at 100% CPU would violate the batch policy and could collide with other agents' compile windows.
Scalability potential: Low/Middle/High/Ultra truth route is code-stable; runtime performance still requires profiler proof after a clean compile window.
Hardware Impact: No additional load injected during active CPU saturation; compile proof remains blocked, not green.

## Decision 8: Route Card And Ledger Repair

Problem: Source defines SHINOBU_349 Vault lanes as `74000..74008`, but the first log entry carried a stale `70166..70173` range and the binary payload ledger had no route entry.
Solution: Add `SHINOBU_349_AUP_NARRATIVE_POI_TRIGGER_ROUTE_CARD.md`, add `SHINOBU_349_SELF_AUDIT.xml`, correct the log range, and append the `74000..74008` route to `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
Rejected Alternatives: Leaving BufferID discovery to source grep only. That breaks route-card auditability and increases integration error risk under concurrent agents.
Scalability potential: Low = designers and integrators can find the fixed-capacity lanes without source spelunking; Middle/High/Ultra = same route supports larger authored datasets through capacity tuning without changing gameplay truth.
Hardware Impact: Documentation-only change; no runtime cost. Prevents wrong-lane Vault collisions that would be catastrophic on every hardware tier.

## Decision 9: Per-POI State Word Array And Authored Prerequisites

Problem: A single `ulong` state mask aliases POI identity above 64 rows, and scene-authored POIs cannot all carry `PrerequisiteBitmask = 0` if first-hour quest gating is required.
Solution: `NarrativePoiStateMasks` is sized as `(PoiCapacity + 63) / 64` and uses `poiIndex >> 6` plus `1UL << (poiIndex & 63)` for one-shot state. `NarrativePoiDTO.StateFlags` now carries `DispatchPending` and `Dispatched`, so managed completion scans explicit DTO flags instead of diffing one aliased mask. Rebuild/sync uses discovered POI hashes for AUP solver exhaustion, not the legacy 64-bit save mask. First-hour authored POIs resolve prerequisite masks from generated `H8QuestMasks` by quest hash or completion trigger hash.
Rejected Alternatives: Keeping a global 64-bit trigger identity word, adding a managed dictionary to the Burst path, or polling `QuestManager` per POI. The first aliases 10k rows; the latter two add managed/hot dependencies.
Scalability potential: Low = one 157-word state array for 10k rows and same local-bucket math; Middle = identical state route at faster cadence; High/Ultra = larger POI capacities can scale by increasing Vault capacity and word count without changing DTO ABI or signal route.
Hardware Impact: Per-trigger state set is one indexed `ulong` OR. On i3/MX350 this avoids false replay storms and keeps cache-local state; measured runtime proof still pending because compile did not reach C#.

## Decision 10: Compile Attempt Boundary

Problem: CPU gate briefly allowed a narrow build, but `Assembly-CSharp.csproj --no-restore` had no generated NuGet assets file.
Solution: Stop after `NETSDK1004` and resample CPU before attempting restore/build. CPU returned to 98.46%, so no restore or second build was launched.
Rejected Alternatives: Running restore/build under 98.46% CPU would violate the local compile discipline and risk colliding with other agents.
Scalability potential: No runtime effect. Verification remains static until a legal restore/build window exists.
Hardware Impact: No additional load injected after CPU saturation returned.

## Decision 11: Private Native Mirror Eviction

Problem: The AUP Burst solver used Vault rows, but `HectonNarrativeDirector` still carried legacy private POI `NativeArray` mirrors, an old O(N) `NarrativePoiSpatialCheckJob`, a private blackbox `NativeArray`, and a cold `NativeHashMap` node cache.
Solution: Delete the legacy spatial job and private native mirrors. Add Vault lane `74008` for `NarrativePoiPresentationDTO[10000]` so managed biome, soundscape, lore, and HUD presentation can consume metadata without local native ownership.
Rejected Alternatives: Keeping private native mirrors as "presentation only" still violates Vault-law and creates a second state route. Rehydrating managed presentation from scene components at dispatch would reintroduce scene search and object coupling.
Scalability potential: Low = same 0.25s cadence and local bucket math with no private native residency; Middle = stable same-cell evaluations with presentation metadata already cache-line aligned; High/Ultra = 10k authored/mock rows can carry quest/biome/lore/HUD metadata without changing DTO ABI or hot signal route.
Hardware Impact: On i3/MX350, this removes the old O(N) private spatial job surface and duplicate native cache maintenance. Static proof only; no profiler measurement because compile/runtime remains blocked by restore asset and CPU policy.

## Decision 12: Full State Telemetry Hash

Problem: After moving POI identity to a 157-word Vault state mask, telemetry still hashed only `NarrativePoiStateMasks[0]`, hiding state changes for POIs above index 63.
Solution: `EvaluatePoiTriggersJob` now computes `StateHash` as FNV over every `NarrativePoiStateMasks` word plus the player cell hash. `GlobalProgressionMask` remains word0 only as a compact compatibility probe, not the forensic identity proof.
Rejected Alternatives: Expanding telemetry to store all 157 `ulong` words would bloat the 64-byte blackbox ABI. Keeping the first-word hash would make 10k-row failures ambiguous.
Scalability potential: Low = one 157-word hash only on solver cadence, not per frame; Middle/High = same proof without DTO growth; Ultra = larger capacities can increase word count while preserving the telemetry ABI and route.
Hardware Impact: Adds a bounded sequential read of 157 `ulong` words per solver telemetry write. On i3/MX350 this is below the cost of a single PhysX trigger broadphase path and buys deterministic forensic coverage for all 10k POIs. Measured runtime proof remains pending.

## Decision 13: Bucket Overflow Telemetry Surface

Problem: Bucket overflow was stored in `NarrativePoiBucketRangeDTO.Flags`, but the evaluator did not translate that range flag into `AupNarrativeTriggerTelemetryEntry.Flags`, so the dump gate could miss an overloaded local cell.
Solution: Add explicit `NarrativePoiBucketRangeFlags` with `Occupied` and `Overflow`; `BuildNarrativePoiBucketsJob` writes those flags and `EvaluatePoiTriggersJob` maps `Overflow` to `TelemetryFlags.BucketOverflow`.
Rejected Alternatives: Reusing `NarrativePoiStateFlags.BucketOverflow` for bucket metadata blurred POI state and hash-bucket state. Relying only on `Counters.FaultCount` loses the exact sampled-player-cell fault in the 300-frame ring.
Scalability potential: Low = overloaded cells fail visible without evaluating extra rows; Middle/High/Ultra = designers can tune bucket count/stride from telemetry without changing DTO ABI.
Hardware Impact: One bit test after bucket lookup. Static cost is below noise; runtime profiler proof remains pending.

## Decision 14: Proof Artifact Synchronization

Problem: Source and route code carried the bucket-overflow telemetry bridge, but the self-audit and ledger did not name the exact `NarrativePoiBucketRangeFlags.Overflow -> AupNarrativeTriggerTelemetryFlags.BucketOverflow` mapping.
Solution: Update self-audit, route card, ledger, status, and log after the final static gate so the forensic files match source truth.
Rejected Alternatives: Leaving the proof implicit in source grep only would make CTO/integrator review depend on code archaeology and could be misread as a missing blackbox fault lane.
Scalability potential: Low/Middle/High/Ultra unchanged; proof sync helps tune bucket count and stride from telemetry without route ambiguity.
Hardware Impact: Documentation-only; no runtime cost. Latest static gate remains compile-blocked by CPU policy and active compiler processes, not by a source finding.

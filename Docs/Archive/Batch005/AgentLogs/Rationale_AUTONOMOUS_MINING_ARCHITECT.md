# Rationale_AUTONOMOUS_MINING_ARCHITECT

Status: PENDING VERIFICATION

## Decision 0 - Domain Gate

Problem: Deployable SDF Drills cross gameplay tools, voxel carving, logistics power, inventory, audio threat, and persistence. Direct ownership of those systems would violate domain boundaries and create dependencies on agents working in parallel.
Solution: Scope implementation to mining-owned runtime contracts and adapters. Use GlobalRegistry/service interfaces or typed signal packets for cross-domain traffic. Keep SDF mutation as a `VoxelCarveEvent`, acoustic threat as `AcousticPingSignal`, and power as a scalar query/consumer interface where existing contracts permit it.
Rejected Alternatives: Direct calls into concrete voxel, fauna, logistics, or audio managers; Unity trigger callbacks; AudioSource event strings; per-frame SDF edits.
Scalability potential: Low uses deterministic background resource accrual and skips visible carve rebuilds. Middle emits sparse 60 s carve events. High/Ultra can spend saved cycles on stronger crater/debris/acoustic presentation through owning systems.
Hardware Impact: i3/MX350 avoids per-frame mining simulation, managed allocations, trigger callbacks, and repeated mesh churn; expected savings are in milliseconds during multi-drill scenes compared with direct Unity physics/presentation loops.

## Decision 1 - Assembly Boundary

Problem: `HectonVoxelEngine.TryGetNearestActiveVolume` and `DeltaProcessor` are internal to the root assembly. Using them from a new gameplay asmdef would require changing voxel ownership or leaking internals.
Solution: Keep `Hecton8.Gameplay.Mining` isolated and use public voxel contracts: serialized `VoxelDeltaProcessor`/`HectonVoxelVolume` when authored, plus `HectonVoxelVolume.TryRaymarchAnyPublishedSdf` to resolve a public active volume before calling `TryQueueCarveEvent`.
Rejected Alternatives: Moving the drill into `Hecton8.Core` to access internals; making voxel internals public; direct SDF cell edits.
Scalability potential: Low can skip the visible SDF carve and still run extraction. Middle/High/Ultra can send sparse carve packets to the voxel owner for presentation without mining owning rebuild cost.
Hardware Impact: i3/MX350 avoids per-frame voxel lookups and mesh rebuild coupling; public raymarch is only used on the 60 s carve cadence.

## Decision 2 - Deploy Snap Path

Problem: A deployable drill must anchor to uneven seabed without `OnTriggerStay` or synchronous placement rays during gameplay ticks.
Solution: Use a persistent one-command `RaycastCommand` batch, complete it on `ColdTick`, reject steep normals, then align with `quaternion.LookRotationSafe(normal, up)`.
Rejected Alternatives: `Physics.Raycast` in deploy code; trigger proxy volumes; Unity joints that fight floating-origin rebases.
Scalability potential: Low/Middle use one probe. High/Ultra can author multiple probe feet later without changing the runtime contract.
Hardware Impact: i3/MX350 avoids immediate main-thread physics stalls during placement and prevents continuous trigger costs.

## Decision 3 - Power Coupling

Problem: The current `IPowerGridService.TryQueueWirelessToolDrain` lane is capped at 4096 watt-seconds, but the drill mandate requires a 50kW dormant threshold. Requesting 50000 watt-seconds directly would make every drill dormant under the existing queue cap.
Solution: Enforce the 50kW threshold through aggregate generation/battery telemetry, then queue the largest supported wireless drain packet so the power owner still sees demand without concrete mutation.
Rejected Alternatives: Editing `PowerGridManager` queue limits from gameplay mining; adding a direct `PowerNode` dependency; ignoring the 50kW dormant rule.
Scalability potential: Low/Middle get one scalar gate per second. High/Ultra can later expose a local-grid reservation contract without changing mining inventory/extraction logic.
Hardware Impact: i3/MX350 pays only a few scalar reads and one bounded queue write per active drill per second.

## Decision 4 - Macro Extraction

Problem: A deployed drill must keep producing while unloaded without keeping GameObjects, triggers, or SDF visuals resident.
Solution: Store a blittable `DeployableSdfDrillMacroRecord` and apply a capped Burst LCG extraction delta on restore. Inventory capacity caps hydration so offline time cannot create unbounded items.
Rejected Alternatives: Running hidden drill MonoBehaviours in unloaded chunks; serialized managed inventory lists; uncapped offline catch-up loops.
Scalability potential: Low caps offline cycles at 64 and skips SDF visual. Middle/High/Ultra increase catch-up caps while still respecting inventory.
Hardware Impact: i3/MX350 converts hours of offline time into one bounded 4-slot Burst job at hydration instead of continuous simulation.

## Decision 5 - Threat and Damage Decoupling

Problem: The drill must attract fauna and break under Leviathan attacks, but direct fauna/audio/VFX dependencies would conflict with parallel agents.
Solution: Emit `AcousticPingSignal` for thumper threat, `CombatDamageSignal` for damage feedback, and `DebrisSpawnSignal` when broken. Drill owns only health/broken state.
Rejected Alternatives: Referencing fauna brains, spawning debris prefabs directly, playing AudioSources from the drill.
Scalability potential: Low gets the same gameplay threat with cheaper presentation. High/Ultra can let audio/VFX systems turn the same signals into visual overkill.
Hardware Impact: i3/MX350 avoids per-drill audio emitters and VFX prefab churn; one typed packet per second is bounded.

## Decision 6 - Low-Tier Visual Skip

Problem: Carving visible SDF craters from every remote drill is presentation cost, not resource simulation. Low-tier devices need resource output without mesh churn.
Solution: Math LOD reads `GlobalRegistry.ScalabilityTier`: Low/MX350/Unknown skip SDF carve packets but still run extraction, inventory, macro catch-up, power, and threat.
Rejected Alternatives: Running a single middle path on all devices; stopping production when SDF visuals are skipped.
Scalability potential: Low: no visible carve. Middle: sparse carve. High: larger catch-up cap. Ultra: highest macro cap and same signal contract for overkill presentation.
Hardware Impact: i3/MX350 saves voxel raymarch and downstream rebuild pressure while retaining gameplay output.

## Decision 7 - Blackbox and UI

Problem: Drill faults must be explainable after NaN/crash, and fill feedback must not allocate strings.
Solution: Keep a 300-entry fixed `NativeArray<DeployableSdfDrillTelemetryEntry>` ring with AUP, active drill count, ores extracted, fill, health, flags, and job cycles. Dump to `Docs/AgentLogs/Dump_AUTONOMOUS_MINING_ARCHITECT.bin` on broken/NaN. Use a fixed TMP char buffer for fill percentage.
Rejected Alternatives: `Debug.Log` telemetry spam; managed circular lists; interpolated UI strings.
Scalability potential: Low gets the same blackbox with 19.2 KB fixed memory. High/Ultra can consume telemetry for richer diagnostics without changing drill runtime.
Hardware Impact: i3/MX350 avoids GC from diagnostics/UI while preserving failure evidence.

## Decision 8 - Burst Verification Boundary

Problem: The extraction job must be Burst-compiled, but the current project does not reach a clean global compile because unrelated `SaveManager.cs` duplicate methods and a `Hecton8.Core` Burst hash error stop verification.
Solution: Mark `DeployableSdfDrillExtractionJob` with `[BurstCompile]`, keep it unmanaged NativeArray-only, validate the scripts, and mark the final Burst compile proof blocked by dependency instead of fabricating success.
Rejected Alternatives: Removing Burst from the job; claiming compile success from script validation alone; editing unrelated SaveManager/Core code outside assigned domain.
Scalability potential: Once dependencies clear, Low runs the same tiny job with lower cycle caps; High/Ultra increase caps without changing the algorithm.
Hardware Impact: i3/MX350 benefits from a bounded four-slot job; exact Burst speedup remains unverified until the project compiles.

## OMEGA POLISH CHANGES

Problem: The post-core polish mandate required proof that the drill path did not hide bloated math, string allocation, Unity frame callbacks, or fake Burst status.
Solution: Replaced the Burst extraction job's `inventoryFull` boolean with the existing `ushort` status bitmask and changed cycle, health, integrity, and fill calculations to `math.rcp` multiplication. Kept the SDF carve as a sparse 60 s presentation packet and kept low-tier devices on extraction-only output. Ran `rg` audits for forbidden allocation/math/frame-loop patterns and executed `dotnet build Hecton8.Core.csproj`.
Rejected Alternatives: Lookup tables for three TMP digits; replacing the cold public voxel fallback with root assembly internals; claiming clean compile while root project references missing cross-domain contracts.
Scalability potential: Low skips visible SDF carving and caps macro catch-up at 64 cycles. Middle emits sparse craters and thumper packets. High increases macro cap and downstream presentation headroom. Ultra keeps the same mining contract while voxel/VFX/audio owners can spend the saved cycles on overkill debris, crater detail, and acoustic spectacle.
Hardware Impact: i3/MX350 avoids per-frame drill polling, per-frame SDF edits, managed allocation, and trigger proxies. The remaining cold `GetComponent<VoxelDeltaProcessor>` is only a fallback on the 60 s carve path when an authored reference is missing; primary path is serialized bridge injection.

Exact cinematic cheats: SDF drilling is a sparse `VoxelCarveEvent` rather than physical continuous boring; low-tier devices skip the visible crater while inventory still accrues; threat is one `AcousticPingSignal` packet instead of an AudioSource emitter; offline macro production is one bounded Burst delta instead of hidden GameObjects.
Final diff summary: created `Assets/_Project/Scripts/Gameplay/Mining/Contracts/DeployableSdfDrillContracts.cs`, `Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs`, and two mining asmdefs; updated status/rationale/log evidence files. `git diff --check` reports only existing LF-to-CRLF warnings on markdown logs.
Verification state: `validate_script` passed for both mining C# scripts before Unity MCP became unstable. `dotnet build Hecton8.Core.csproj` restores, then fails with 154 unrelated root assembly errors such as missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `IGroundRadarService`, acoustic contracts, and fauna/resource contracts. Status remains `PENDING VERIFICATION`.

## Decision 9 - Recheck Upgrade Pass

Problem: The first implementation still had three technical debts: tick-reachable dependency reads, immediate scalability tier flipping, and item acquisition signals collapsing mixed-slot extraction into one last-slot hash.
Solution: Cached power, MapMagic, voxel, and math LOD dependencies on lifecycle/manual refresh and through `IGlobalRegistryHotSwapListener`/`IGlobalRegistryHotSwapRefListener`; added `IScalabilityChangedEventListener` with a minimum 3 second LOD hysteresis; expanded `DeployableSdfDrillExtractionResult` with four slot delta lanes and per-slot hashes; changed item publish to emit one zero-GC signal per mutated slot.
Rejected Alternatives: Polling `GlobalRegistry` in `ColdTick`; calling `GetComponent` on the sparse carve path; immediate LOD downgrade/upgrade on scalability event; publishing one aggregate item signal under the last ore hash.
Scalability potential: Low/MX350 keeps the cheapest math and no visible SDF carve while still producing inventory. Middle keeps sparse visible craters. High/Ultra get larger macro catch-up caps and clean per-slot signals for richer downstream inventory/VFX/audio presentation without mining owning those domains.
Hardware Impact: i3/MX350 avoids service lookup, component lookup, and tier branch churn in the 1 Hz drill lane. Correct per-slot signals prevent downstream inventory correction work and remove an overproduction/reporting fault under mixed ore output.

Verification state: `DeployableSdfDrillContracts.cs` and `DeployableSdfDrillRuntime.cs` both pass Unity `validate_script` with 0 errors and 0 warnings after this pass. Static forbidden-pattern scan found no `foreach`, `string.Format`, interpolated strings, `.ToString()`, managed collection allocation, `FindObject`, Unity frame callbacks, `math.sqrt`, `math.normalize`, or `GetComponent<` in mining scripts. Unity console now reports unrelated `Hecton_CausticsGenerator.compute` shader errors at lines 56-57; no mining C# errors are present in the latest console read.

## Decision 10 - Lifecycle and Ore Identity Hardening

Problem: A restored or re-enabled drill could retain `_snappedToTerrain=true` while a fresh async snap job was pending, allowing one cold tick of power/extraction on stale terrain authority. The extraction LCG also mixed cycle data into ore hashes, which made a fixed SOA ore lane report changing ore identity instead of deterministic slot selection. Full inventory could preserve stale elapsed backlog and pay no-op catch-up work later.
Solution: `ScheduleTerrainSnap()` now clears the backing snapped boolean and flag before scheduling or returning. `ResolveOreHash()` now returns the configured lane ore hash or the default ore hash only; the LCG continues to mix drill seed, sector, biome, and cycle count for slot selection and seed progression. Full-inventory commits advance `_lastMacroUpdateUnscaledTime` to the commit time so blocked production does not accumulate fake debt.
Rejected Alternatives: Trusting the saved snapped flag during hydration; forcing a synchronous terrain ray during restore; mutating ore identity per cycle; keeping blocked macro backlog for later no-op jobs.
Scalability potential: Low/MX350 avoids stale active ticks and no-op catch-up work. Middle keeps sparse correct visual craters. High/Ultra can use stable ore-lane identity for richer downstream debris/material presentation without mining owning presentation systems.
Hardware Impact: i3/MX350 avoids one accidental powered drill tick per restore/re-enable, removes useless full-inventory catch-up passes, and preserves zero-GC deterministic four-slot extraction.

Verification state: Attribute-aware CLI prompt extraction succeeded with `PROMPT_BYTES=3435` and `PROMPT_HASH=79F98468F82EBD62573D3F6B5AD038C520D78F2435CC0DB14BF2771D0754C9B1`. `DeployableSdfDrillContracts.cs` and `DeployableSdfDrillRuntime.cs` both pass Unity `validate_script` with 0 errors and 0 warnings after this pass. Static forbidden-pattern scan is clean for mining scripts. `git diff --check` is clean for mining C#. Unity console filter for `DeployableSdfDrill` returns 0 entries; global compile remains blocked outside this domain by `H8MacroDatabaseService.cs` unsafe-await errors.

## Decision 11 - Boundary Input and Authoring Sanitation

Problem: External inputs could still corrupt mining state before the blackbox caught them. Non-finite deploy positions, hit points, origin-shift runtime positions, or macro record locals could reach AUP conversion, RaycastCommand payloads, combat/debris signals, or voxel carve payloads. Mined item signals also used only the typed `SignalBus` alias, while some project producers use `GlobalSignals.Publish` to feed both the legacy NativeQueue and typed lane.
Solution: Added finite guards for deploy, damage, origin shift, terrain snap, anchor capture, voxel carve emission, and macro restore. Bad macro records now fail closed before touching the transform, clear stale inventory, mark the drill broken, and dump the fixed blackbox. Added `OnValidate` sanitation for serialized tuning and blocked runtime inventory reconfiguration while an extraction job is pending. Mined item acquisition now calls `GlobalSignals.Publish(in signal)` to match broader project producer convention.
Rejected Alternatives: Letting `AbsoluteUniversePosition.FromRuntimePosition` absorb NaNs; trusting macro DB records blindly; scheduling RaycastCommand with NaN origins; pushing only the typed item alias; letting inspector edits mutate SOA capacity while a job can read those lanes.
Scalability potential: Low/MX350 fails bad deployed drills without doing power, carve, or catch-up work. Middle keeps deterministic sparse output. High/Ultra still receive full item/acoustic/debris lanes and can spend presentation budget safely because payloads are finite.
Hardware Impact: i3/MX350 avoids accidental invalid physics jobs, invalid signal payload fanout, and stale inventory correction work. The authoring sanitation is cold/editor-time; hot path remains fixed arrays, bounded loops, no string formatting, and no managed collection allocation.

Verification state: `DeployableSdfDrillContracts.cs` and `DeployableSdfDrillRuntime.cs` both pass Unity `validate_script` with 0 errors and 0 warnings after this pass. Static forbidden-pattern scan is clean for mining scripts. `git diff --check` is clean for mining C#. Unity script compile was requested; editor returned idle once, but final `read_console` retries failed with Unity MCP ping timeouts. Latest readable global console before the console-ping instability showed unrelated `GlobalDataVault.cs` missing `Hecton8.Core.Signals`/Burst symbols and no mining-specific diagnostics.

## Decision 12 - Blackbox Fault Ordering

Problem: The blackbox ring was fixed-size, but dumps wrote raw array index order. After wraparound, postmortem readers would see a valid 300-entry file with temporal discontinuity and no guaranteed terminal sample for invalid-runtime faults.
Solution: Dump from `_blackBoxCursor` through the ring so the file is oldest-to-newest. Invalid runtime faults now zero health, clear active/snapped/dormant flags, write one final telemetry entry, and then dump.
Rejected Alternatives: Raw array-index dumps; relying on the previous cold tick to explain a NaN/fault; expanding logs with managed per-frame text.
Scalability potential: Low/MX350 keeps the same 19.2 KB fixed diagnostic cost. Middle/High/Ultra can consume chronologically ordered fault traces for richer QA tooling without changing runtime simulation or adding hot-path allocation.
Hardware Impact: i3/MX350 pays no extra steady-state cost. Fault-only dump ordering is O(300) writes and removes ambiguous postmortem analysis that would otherwise waste debugging time or lead to heavier runtime logging.

Verification state: `DeployableSdfDrillContracts.cs` and `DeployableSdfDrillRuntime.cs` both pass Unity `validate_script` with 0 errors and 0 warnings after this pass. Static forbidden-pattern scan is clean for mining scripts. `git diff --check` is clean for mining C#. Unity console filter for `DeployableSdfDrill` reports 0 entries; global compile remains blocked outside this domain by `Assets\_Project\Scripts\Core\Memory\GlobalDataVault.cs(940,21): error CS0103: The name 'ElapsedMillisecondsSince' does not exist in the current context`.

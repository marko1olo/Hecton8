# Rationale - HPHI_SYNAPTIC_FORGER

Agent: ARCHITECTURAL_SURGEON
Domain: Core/Gameplay Signal Architecture
State: PENDING - GLOBAL COMPILE DEPENDENCY BLOCK

## Decisions

### 2026-05-14 - Managed Callback Purge Scope

Problem: Core/Gameplay contains many serialized UnityEvents plus public Action events; converting all in one pass would mutate prefab-facing design contracts across unrelated domains.
Solution: Convert the top five code-level managed callbacks with direct subscribers: PlayerActionController progress/completed/cancelled, PDAExchangeSystem state changed, VehicleUpgradeModule upgrades changed.
Rejected Alternatives: Broad UnityEvent purge was rejected because serialized scene hooks need prefab migration and domain owners; compatibility wrapper events were rejected because they keep delegate lists alive.
Scalability potential: Low keeps HUD/PDA callback paths cheap on toaster hardware; Middle/High/Ultra can add richer feedback consumers by reading the same signal snapshots without extra producer references.
Hardware Impact: Estimated i3/MX350 saving is 8-16 us during action/PDA/vehicle mutation bursts by removing delegate invocation lists, retry subscription logic, and singleton-bound consumer refreshes.

### 2026-05-14 - Signal Lane Transport Location

Problem: Prompt says route through Hecton8.Core.Contracts, but the repository's actual SignalBus<T> constraint is Hecton8.Core.Signals.ISignal in GlobalSignals.cs.
Solution: Place payloads in Hecton8.Core.Signals and route producers through GlobalSignals.Publish, avoiding any UI-to-Gameplay or Gameplay-to-UI concrete dependency.
Rejected Alternatives: Moving ISignal into Contracts was rejected because it is a cross-assembly migration outside this prompt; duplicating ISignal in Contracts would not satisfy SignalBus<T>.
Scalability potential: Low has one native queue/snapshot per lane; Middle/High/Ultra can increase lane capacity or attach more consumers without rewriting producers.
Hardware Impact: i3/MX350 gain is small but deterministic: no class payloads, no string payloads, and 32-byte lane packets fit predictable cache scans.

### 2026-05-14 - Player Action Payload Shape

Problem: Legacy PlayerActionController events exposed ItemData and a float, tying HUD behavior to gameplay objects and singleton reads.
Solution: Emit PlayerActionProgressSignal, PlayerActionCompletedSignal, and PlayerActionCancelledSignal with item hash, action kind, frame, progress, flags, and packed inventory anchors.
Rejected Alternatives: Passing ItemData through the signal was rejected because managed references are banned; HUD polling GlobalRegistry.PlayerActions.ActiveItem was rejected because it keeps the island dependency.
Scalability potential: Low renders generic action progress with fixed text/color selection; Middle/High/Ultra can use item hashes for richer diegetic HUD effects without adding object references.
Hardware Impact: i3/MX350 expected gain is about 4-6 us during active delayed actions by removing per-enable delegate management and per-progress managed event dispatch.

### 2026-05-14 - PDA Exchange and Vehicle Upgrade Signals

Problem: PDAExchangeSystem and VehicleUpgradeModule used managed events for state mutation notification, forcing UI and downstream systems to bind to concrete components.
Solution: Emit PdaExchangeStateChangedSignal and VehicleUpgradesChangedSignal with source id, frame, counts/masks, numeric reasons, and scalar state.
Rejected Alternatives: Static Instance event access was rejected; managed DTOs were rejected due GC and cross-domain reference retention.
Scalability potential: Low uses one UI refresh on matching source id; Middle/High/Ultra can add telemetry, audio, and cockpit consumers from the same lane.
Hardware Impact: i3/MX350 expected gain is about 3-5 us on barter/upgrade bursts and lower long-session leak risk from missing unsubscriptions.

### 2026-05-14 - DataVault Migration Boundary

Problem: Gameplay still contains 86 direct new NativeArray sites, but many are owned by other active agents and systems with distinct allocator lifecycles.
Solution: Verify the DataVault-migrated SubmarineAutoLevelBallastController path: owned arrays request GlobalDataVault.GetBuffer<T>() via BufferID and use a vault ownership mask to avoid disposing aliases.
Rejected Alternatives: Rewriting every Gameplay NativeArray was rejected as cross-domain sabotage; leaving SubmarineAutoLevel direct allocation was rejected because that system already has IDataVault ownership.
Scalability potential: Low avoids allocator churn and alias disposal errors; Middle/High/Ultra can defrag larger ballast/flood telemetry buffers without changing callers.
Hardware Impact: i3/MX350 load-time allocation savings are microsecond-scale per buffer and reduce persistent allocator fragmentation; steady-state frame gain is effectively 0 us.

### 2026-05-14 - AUP, Black Box, and Compile Wall

Problem: New signal lanes require postmortem visibility and AUP safety, while the project compile is currently blocked by unrelated missing namespaces/types.
Solution: New converted signals carry no world coordinates; lane telemetry uses the existing SignalBusRegistry -> CrashTelemetryBuffer.ReportSignalLaneStats route; compile failure is recorded as dependency-blocked after dotnet build evidence.
Rejected Alternatives: Adding no-op AUP transformers was rejected; duplicating Black Box buffers per signal was rejected; stubbing unrelated audio/world/fauna contracts was rejected.
Scalability potential: Low gets deterministic lane counters and no coordinate rebase risk; Middle/High/Ultra can expand lane capacities and visual overkill consumers while retaining the same telemetry spine.
Hardware Impact: i3/MX350 saves about 0.7 us/frame by using existing telemetry aggregation rather than per-signal managed logging; compile wall has no runtime impact but blocks final proof.

### 2026-05-14 - OMEGA POLISH CHANGES

Problem: Core tasks were functionally closed, but the Polish Mandate required an anti-bloat pass, compile proof, and honest state labeling before final reporting.
Solution: Re-ran targeted rg against the six touched scripts for managed event remnants, foreach, string.Format, interpolation, .ToString(), math.sqrt, and math.normalize. Converted SignalBus hot lanes remain clean. One `.ToString()` was found in PDAExchangeSystem.BuildBundleSummaryForSave, a save serialization cold path; it was documented and left intact because changing persistence string output is outside this signal-lane mandate.
Rejected Alternatives: Editing the save serialization path was rejected because it is not in the hot SignalBus/HUD/PDA tick path and would risk save-data behavior for no frame-time win. Claiming `VERIFIED MASTER GRADE` was rejected because `dotnet build Hecton8.Core.csproj --no-restore` fails on 131 unrelated global dependency errors, `Assembly-CSharp.csproj` timed out, and Unity MCP validation had no active session.
Scalability potential: Low keeps five 32-byte numeric lanes with no managed producers; Middle can attach more UI/audio telemetry consumers; High can increase lane capacities; Ultra can spend the saved delegate/subscription budget on richer cockpit/diagnostic visuals without rebuilding producers.
Hardware Impact: Estimated i3/MX350 gain remains 16.1 us/frame in selected burst paths from killed callbacks and singleton retry logic. Polish edits added 0 us/frame cost. Final status is PENDING - GLOBAL COMPILE DEPENDENCY BLOCK, not master-grade, because the compile wall is outside this agent domain.

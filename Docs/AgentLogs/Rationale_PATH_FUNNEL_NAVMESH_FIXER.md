# PATH_FUNNEL_NAVMESH_FIXER Rationale

## Decision 001 - Scope And Existing Contracts

Problem: The assigned pathfinding folder did not exist, but Core/World already expose WFC state signals, DataVault buffer IDs, AUP structs, and dispatcher lanes.
Solution: Add a narrow `Assets/_Project/Scripts/AI/Pathfinding/` module instead of editing legacy vendor A* or World/Power owners. Use `SignalBus<WfcOutpostStateChangedSignal>` and `GlobalRegistry.DataVault` as cross-domain interfaces.
Rejected Alternatives: Using `Assets/AstarPathfindingProject` was rejected because AGENTS forbids the legacy A* package. Editing World voxel navgraph internals was rejected because the prompt domain is AI/PATHING and World owns SDF grid generation.
Scalability potential: Low uses short local smoothing windows; Middle/High/Ultra can widen look-ahead while preserving the same data layout.
Hardware Impact: Avoiding managed A* modifiers and `Vector3` path lists prevents hot-path GC and cuts per-corner smoothing to scalar cross products. Estimated low-end gain: 20-80 microseconds per 32-portal path versus angle/managed-list smoothing, pending profiler proof.

## Decision 002 - Mandates Applied

Problem: Funnel smoothing touches AI navigation, dynamic WFC obstruction, native jobs, AUP, signals, and blackbox telemetry.
Solution: Loaded mandates for AI funnel pathing, dynamic navgrid/SDF, zero-GC, native jobs, execution phases, GlobalRegistry DI, telemetry, and AUP determinism.
Rejected Alternatives: Reading unrelated batch prompts or dated reports was rejected; the XML prompt and stable mandates are the authority.
Scalability potential: The same kernel supports Low/Middle/High/Ultra by changing only look-ahead and clearance radius.
Hardware Impact: Cross-product funnel math avoids `acos`, normalized angles, and managed path modifiers. Estimated ALU saving: 30-50 scalar ops per portal, pending Burst inspection.

## Decision 003 - Burst XZ Funnel Kernel

Problem: The path needed smoothing without angle math, managed path lists, or legacy A* package ownership.
Solution: Implemented `FunnelSmoothingJob` as a Burst `IJob` over `NativeArray<NavPortal>` and used the XML-required XZ cross product `(ab.x * ac.z) - (ab.z * ac.x)` for funnel tightening.
Rejected Alternatives: `Vector3.Angle`, `acos`, normalized dot/cross comparisons, Bezier smoothing, and A* project modifiers were rejected because they add ALU, branch noise, and package dependency risk. The AI mandate's 3D funnel warning was noted, but the batch prompt explicitly required XZ cross math for this WFC outpost lane.
Scalability potential: Low = 2-portal look-ahead; Middle = 8 portals; High = 16 portals; Ultra = same stable 16-portal kernel with higher call frequency or larger caller-owned buffers.
Hardware Impact: On i3/MX350-class silicon, removing angle math saves an estimated 30-50 scalar ALU ops per portal and 20-80 microseconds per 32-portal smoothing request compared with managed angle smoothing.

## Decision 004 - WFC Door Invalidation Through Vault And Signals

Problem: Door closures must invalidate only paths that pass through the changed WFC cell without coupling AI to Power or World implementation classes.
Solution: Cached `GlobalRegistry.DataVault`, read `BufferID.WfcOutpostGrid`, consumed `SignalBus<WfcOutpostStateChangedSignal>`, and tracked each active path with an exact 500-bit cell mask.
Rejected Alternatives: Direct WFC registry references, broad sector invalidation, and path owner polling were rejected. Broad invalidation is cheaper to write but too destructive for AI behavior and wastes replans.
Scalability potential: Low = 128 tracked paths and 64 invalidations; Middle/High = raise serialized capacities; Ultra = same bitmask path can be duplicated per crowd lane without changing signal contracts.
Hardware Impact: Bit testing one `ulong` per candidate path saves an estimated 20-80 microseconds per door event versus scanning corridor cell arrays on i3/MX350-class hardware.

## Decision 005 - AUP And Native SOA Layout

Problem: Path smoothing must not accumulate world-space float drift and must not allocate managed waypoint lists.
Solution: Kept math in sector-local `float3`, wrote `NativeArray<float3> Waypoints`, and optionally converted final waypoints to `AbsoluteUniversePositionBlit`.
Rejected Alternatives: World-space `Vector3`, managed `List<Vector3>`, and automatic `NativeList` growth were rejected because they hide allocation and rebase costs.
Scalability potential: Low = small caller-owned waypoint buffers; Middle = larger buffers for dense interiors; High/Ultra = same ABI with extra AUP output for deterministic remote consumers.
Hardware Impact: Avoiding managed lists and world-float rebasing saves an estimated 10-30 microseconds per long path and prevents GC spikes on low-end hardware.

## Decision 006 - SDF And Radius Corner Guard

Problem: String pulling can cut corners through narrow doors or SDF-eroded obstacles if portal clearance is not represented.
Solution: Added `NavPortal.ClearanceMeters` as a pre-eroded SDF clearance lane and clamped portals when clearance or portal width is below `AgentRadiusMeters`.
Rejected Alternatives: Raw SDF texture sampling inside AI was rejected because World owns SDF generation and metadata. Post-smooth collision repair was rejected because it hides the error until after the path is already published.
Scalability potential: Low = radius and clearance clamp only; Middle = caller supplies better per-portal clearance; High = denser portal generation; Ultra = visual overkill comes from more candidate paths, not heavier per-portal math.
Hardware Impact: Early clamp costs a few scalar ops and avoids 10-40 microseconds of late corner repair or replan churn on i3/MX350-class hardware.

## Decision 007 - Schedule Window And Homeostasis

Problem: Funnel jobs must not serialize simulation by forcing completion in the same frame, and stressed hardware must reduce path smoothing cost.
Solution: Added `PathFunnelSchedule.SchedulePreSimulation` and `TryReadPostSimulation`; readback refuses to call `Complete()` until the handle is already done. `Stressed` forces one-portal look-ahead.
Rejected Alternatives: Synchronous job completion and fixed 16-portal smoothing were rejected because frame-time spikes matter more than perfect smoothing under stress.
Scalability potential: Low = look-ahead 2; stressed = 1; Middle = 8; High/Ultra = 16 with non-blocking readback.
Hardware Impact: Avoiding forced completion prevents estimated 50-300 microsecond sync spikes. Stressed one-portal mode saves 20-60 microseconds per long path.

## Decision 008 - Black Box Telemetry

Problem: Door-driven invalidation failures need a deterministic last-frames record without managed log spam.
Solution: Added a 300-frame `NativeArray<PathFunnelTelemetryEntry>` ring with `PathInvalidationCount`, last sector/path/cell, active count, invalidated count, and binary dump path `Docs/AgentLogs/Dump_PATH_FUNNEL_NAVMESH_FIXER.bin`.
Rejected Alternatives: `Debug.Log` per invalidation and unbounded event history were rejected for hot-path GC and noise.
Scalability potential: Low = same 300-frame ring; Middle/High/Ultra = larger runtime capacities can be serialized without changing telemetry entry format.
Hardware Impact: One struct write per late frame is estimated below 1 microsecond; dump allocation only happens on explicit crash/NaN request, outside the hot path.

## Decision 009 - Build Wall

Problem: Validation could not reach a clean project build because upstream project files failed before pathfinding diagnostics mattered.
Solution: Restored project assets, ran `Assembly-CSharp.csproj`, then isolated `Hecton8.Core.csproj` with errors-only logging. At that pass, `Hecton8.Core.csproj` exited 1 with 49 non-pathing missing contract symbol errors and 0 pathfinding matches. `Assembly-CSharp.csproj` exited nonzero with 216 missing RealtimeCSG source-file errors and 0 pathfinding matches.
Rejected Alternatives: Editing RealtimeCSG package project files, ecology/physics/survival contract owners, URP package outputs, XR, Submarine, VFX, Audio, Fauna, Voxel, Bootstrap, GlobalSignals, Core diagnostics, World systems, Core assembly references, World flora/fauna, VFX dependencies, or killing other agents' processes was rejected as cross-domain or unsafe parallel-workspace behavior outside AI/PATHING. Reporting full green build was rejected because Core and Assembly-CSharp still fail upstream with zero owned pathfinding diagnostics.
Scalability potential: The new `Hecton8.AI.Pathfinding.asmdef` keeps pathfinding isolated and prevents owned AI/PATHING code from bloating Core.
Hardware Impact: No runtime hardware impact. Build blockage is integration debt, not a frame-time choice.

## Decision 010 - Multiplatform Data Sovereignty Pass

Problem: The path invalidation runtime originally owned private persistent `NativeArray` fields and used an ad hoc memory owner value. That violates GlobalDataVault sovereignty, weakens H-Phi connectivity, and creates avoidable ABI/lifetime risk on ARM64/Quest/Android. The blackbox dump also used a managed `byte[]` copy path.
Solution: Moved active paths, WFC cell masks, invalidation ring, telemetry ring, and runtime counters into GlobalDataVault handles under `SystemID.AIPathfinding` and dedicated `BufferID.PathFunnel*` entries. Added `PathFunnelRuntimeState` as the single vault-resident mutable counter block. Converted all pathing binary structs to explicit `StructLayout(..., Pack = 1)` with fixed field offsets and kept 64-bit fields at aligned offsets where the layout contains them. Dump export now streams a `ReadOnlySpan<byte>` over the native telemetry pointer through `FileStream`; no managed `byte[]` is created. AUP grid conversion uses a fixed inverse cell-size multiply instead of runtime division.
Rejected Alternatives: Keeping private `H8Memory.Allocate` arrays was rejected because systems must be stateless. A broad managed event/delegate bridge was rejected because the existing typed `SignalBus<WfcOutpostStateChangedSignal>` already carries the door-change lane as `ReadOnlySpan<T>`. A managed JSON/text telemetry dump was rejected because it bloats crash I/O and adds allocation pressure. Polling WFC classes directly was rejected because AI/PATHING must stay decoupled from World/Power implementation owners.
Scalability potential: Low/Toaster = vault capacities 128 paths/64 invalidations, look-ahead 2, stressed look-ahead 1, exact bitmask invalidation. Middle = same ABI with caller-raised capacities and look-ahead 8. High = look-ahead 16 with denser path requests. Ultra = same cheap simulation truth frees budget for visual overkill in owning VFX/Presentation systems; this prompt marked VFX N/A, so no visor salt/silt/hull shader code was touched.
Hardware Impact: On i3/MX350, removing private persistent arrays eliminates a native lifetime failure mode and keeps normal frame I/O at zero disk reads/writes; blackbox disk write occurs only on explicit dump/crash. Door invalidation remains an estimated 20-80 microseconds faster per event than scanning corridor cell arrays. The telemetry write remains estimated below 1 microsecond per late frame. Steam Deck/MicroSD pressure is not increased during gameplay because no pathing data is read from disk in hot cadence.

## Decision 011 - Survival Telemetry And Cursor Re-Audit

Problem: A re-registered or unregistered invalidated path could leave `InvalidatedPathCount` overstated, and repeated door-close processing could enqueue duplicate invalidation events for a path already marked invalid. Ring cursors also used modulo, and `AgentRadiusMeters` accepted non-finite input before clamp.
Solution: Added finite sanitization for `AgentRadiusMeters`, decremented invalidated active count when an invalidated path is replaced or removed, made WFC invalidation transition-only, and replaced modulo cursor advancement with branch-based `AdvanceRingCursor`.
Rejected Alternatives: Keeping cumulative active invalidation count was rejected because telemetry must describe current high-level state, not stale history. Keeping duplicate invalidation events was rejected because it pollutes the 300-frame blackbox. Keeping modulo was rejected because branch wrap is cheaper and clearer for small fixed rings.
Scalability potential: Low/Toaster = no duplicate ring churn and one branch cursor wrap. Middle = exact current invalidated count for AI recovery logic. High/Ultra = same telemetry truth supports richer presentation-layer diagnostics without touching the hot path.
Hardware Impact: The cursor change is nominal sub-microsecond and not profiler-measured. The real gain is correctness: false invalidated counts no longer trigger unnecessary recovery work, and repeated door-close signals no longer waste ring capacity.

## Decision 012 - Blackbox Dump Exception Containment

Problem: The blackbox dump path used filesystem APIs that can throw. A crash-diagnosis mechanism that throws during dump request violates the survival rule because it can convert a reportable failure into another unknown crash.
Solution: Converted the dump path to `TryDumpBlackBox`, returned false for invalid telemetry/directory state, caught filesystem exceptions only on the explicit dump path, recorded failure with `PathFunnelTelemetryFlags.BlackBoxDumpFailed` in the vault-resident runtime state, and patched the just-written telemetry entry so the failure is visible in the current 300-frame ring instead of waiting one frame.
Rejected Alternatives: Letting the exception escape was rejected because the blackbox must not be the source of a second crash. Managed text logging was rejected because the prompt requires binary blackbox evidence and hot-path log spam is forbidden. Moving dump I/O into FastTick was rejected because Steam Deck/MicroSD pressure must stay out of gameplay cadence.
Scalability potential: Low/Toaster = no hot-path disk I/O; dump only on request. Middle = failure bit gives deterministic telemetry for recovery tooling. High/Ultra = richer crash tools can consume the same flag without changing the pathing runtime.
Hardware Impact: Normal frame cost remains unchanged. Dump failure handling has no measured microsecond claim; it is an exception-survival guard on a non-hot crash path.

## Decision 013 - ABI Tail Padding Elimination

Problem: `PathFunnelResult`, `PathFunnelActivePath`, and `PathFunnelInvalidation` had explicit `Pack = 1` sizes, but their final unused bytes were not named fields. The CLR layout size was fixed, yet unnamed tails are weak evidence for Quest/ARM64 binary review and future unsafe copies.
Solution: Added explicit `Reserved*` tail fields at the final byte ranges of those structs so every byte in the owned fixed-size pathing payloads is intentionally covered.
Rejected Alternatives: Leaving implicit tail bytes was rejected because the platform audit asked for no padding ambiguity. Reordering fields was rejected because the current offsets already keep 64-bit values aligned and preserve the existing ABI.
Scalability potential: Low/Toaster = same compact payloads with no extra allocation. Middle = deterministic dump/telemetry decoding. High/Ultra = tooling can extend reserved bytes deliberately later without guessing stale padding.
Hardware Impact: No measured microsecond saving is claimed. This is ABI safety and postmortem determinism work, not a frame-time optimization.

## Decision 014 - Extreme-Value And Ring Capacity Survival

Problem: AUP conversion sanitized NaN/Infinity but still allowed finite coordinates large enough to overflow deterministic double-to-long grid casts. The invalidation ring also allowed a serialized capacity of 1, which makes a read/write cursor ring permanently ambiguous because empty and full both map to cursor 0.
Solution: Added explicit AUP grid range checks before casting and fallback to zero AUP with `AupFallback` when grid coordinates exceed safe `long` bounds. Clamped invalidation ring capacity to at least 2 and capped path/invalidation capacities at 4096 to prevent inspector-driven native memory spikes.
Rejected Alternatives: Trusting sector origins was rejected because AUP is a critical deterministic contract. Keeping a one-slot ring was rejected because it silently drops every queued invalidation under the existing empty-ring encoding. Unbounded serialized capacities were rejected because low-end hardware should not be able to allocate pathological vault buffers from one inspector value.
Scalability potential: Low/Toaster = fixed small vault buffers and deterministic fallback on impossible AUP coordinates. Middle = same ring semantics with larger caller capacity. High/Ultra = capacity can scale to 4096 without changing payload ABI, while presentation-domain overkill remains outside this prompt.
Hardware Impact: Range checks add only a few scalar comparisons during AUP conversion; no measured microsecond saving is claimed. The memory cap prevents worst-case native allocation pressure on i3/MX350 and Steam Deck-class devices.

## Decision 015 - Direct Signal Assembly Reference

Problem: `PathFunnelNavmeshRuntime` imports `Hecton8.Core.Contracts.Signals`, but the new pathfinding asmdef did not directly reference `Hecton8.Core.Contracts`. Relying on `Hecton8.Core` to expose that dependency is fragile and can fail Unity asmdef compilation.
Solution: Added `Hecton8.Core.Contracts` as a direct reference in `Hecton8.AI.Pathfinding.asmdef`.
Rejected Alternatives: Moving signal use behind managed reflection or a duplicate local signal was rejected because typed lanes must stay authoritative. Depending on transitive visibility was rejected because Unity assembly definition references should be explicit.
Scalability potential: Low/Toaster = no runtime cost; compile graph is explicit. Middle/High/Ultra = future pathing signal lanes can use contracts without assembly churn.
Hardware Impact: No runtime hardware impact and no microsecond claim. This is compile-boundary correctness.

## Decision 016 - Focused Bee Response Probe

Problem: Root `dotnet build` is blocked outside AI/PATHING, so the owned asmdef needed a narrower compile-surface check.
Solution: Located Unity Bee response file `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Pathfinding.rsp`; confirmed it lists `FunnelSmoothingJob.cs`, `PathFunnelContracts.cs`, `PathFunnelNavmeshRuntime.cs`, and `PathFunnelSchedule.cs` plus direct Core/Core.Contracts/Core.Memory references. Ran Unity Roslyn csc against that response file with temp output.
Rejected Alternatives: Editing generated `.csproj` files or generated Bee response files was rejected. Claiming a green focused compile was rejected because `Hecton8.Core.ref.dll` is missing upstream.
Scalability potential: Low/Toaster = no runtime cost; this only improves evidence quality. Middle/High/Ultra = same focused probe can validate pathing once Core ref generation recovers.
Hardware Impact: No runtime hardware impact. The probe exits 1 on missing upstream metadata only; no microsecond claim.

## Decision 017 - WFC Contract Constant Aliasing

Problem: `PathFunnelConstants` duplicated WFC cell count, mask word count, and door-open bit values that already exist in Core contracts. Duplicate constants are an interface drift risk on the same typed signal/data lane.
Solution: `PathFunnelConstants.WfcOutpostCellCount` now aliases `WfcOutpostPersistenceConstants.CellCount`, the mask word count derives from that contract value, and `WfcDoorOpenFlag` aliases `WfcOutpostCellStateFlags.DoorOpen`.
Rejected Alternatives: Keeping local magic numbers was rejected because WFC persistence and path invalidation must agree on cell count and door state semantics. Creating a new pathfinding signal was rejected because `WfcOutpostStateChangedSignal` already exists.
Scalability potential: Low/Toaster = no runtime cost and no silent mismatch when WFC grid size changes. Middle/High/Ultra = the same typed lane remains authoritative for larger pathing budgets.
Hardware Impact: No measured microsecond change. This is interface sovereignty, not a frame-time optimization.

## Decision 018 - Effective LOD Truth In Result Payload

Problem: `FunnelSmoothingJob` already executed one-portal smoothing when `Stressed != 0`, but `PathFunnelResult.MathLod` still reported the caller-requested tier. That makes blackbox/result consumers believe High or Ultra smoothing ran when the homeostasis path actually used the stressed kernel.
Solution: Added `ResolveEffectiveMathLod`, stored the effective byte in `PathFunnelResult.MathLod`, and passed the same byte into `ResolveLookAhead`. Stressed execution and result telemetry now share one source of truth.
Rejected Alternatives: Leaving requested LOD in the result was rejected because it is a telemetry lie under frame pressure. Adding another result field was rejected because the 32-byte result ABI is already compact and the effective tier is the value consumers need for postmortem analysis.
Scalability potential: Low/Toaster = stressed mode is explicitly visible to recovery logic and blackbox decoding. Middle = exact result payload helps tune frame-pressure thresholds. High/Ultra = overkill smoothing remains reported only when it actually ran.
Hardware Impact: No measured microsecond saving is claimed. This is observability and blackbox correctness; it prevents wasted investigation time when stressed frames degrade path quality intentionally.

## Decision 019 - Current Compile Wall Refresh

Problem: The previous compile snapshot became stale under concurrent workspace changes. At that refresh, `Hecton8.Core.csproj` no longer passed; it failed on missing `HectonEcologyContract`, `ScalabilityContract`, `HectonPhysicsContract`, and `HectonSurvivalContract` references outside AI/PATHING.
Solution: Reran Core and Assembly-CSharp validation, rewrote the build logs, and rescanned both logs for owned pathfinding symbols. Both logs contain 0 pathfinding matches.
Rejected Alternatives: Editing contract owners or RealtimeCSG was rejected because the XML domain is `Assets/_Project/Scripts/AI/Pathfinding/`. Claiming the previous Core pass was still current was rejected as stale evidence.
Scalability potential: Low/Middle/High/Ultra pathfinding behavior is unchanged; this is validation evidence only.
Hardware Impact: No runtime hardware impact and no microsecond claim. The compile wall is integration debt outside this agent's domain.

## Decision 020 - Burst-Safe AUP Contract Constant

Problem: The AUP converter should obey the shared Core contract sector size, but a Burst job should not touch `HectonPhysicsContract` static ref properties or static constructor paths for the inverse sector size.
Solution: Kept `HectonPhysicsContract.AupSectorSizeMetersDouble` as the authoritative sector-size constant and derived `inverseCellSize` as `const double 1.0d / HectonPhysicsContract.AupSectorSizeMetersDouble` inside the Burst job.
Rejected Alternatives: Calling `HectonPhysicsContract.OneOverAupSectorSizeMeters` from the Burst job was rejected because it is a static ref property backed by static readonly data. Reverting to an unlabelled local 5000.0 constant was rejected because contract drift is the original interface risk.
Scalability potential: Low/Middle/High/Ultra all share identical AUP sector math with no runtime property call.
Hardware Impact: No measured microsecond saving is claimed. The value is compile/Burst safety and interface correctness.

## Decision 021 - Source Truth Reconciliation

Problem: The status/rationale files recorded the Burst-safe AUP inverse before `FunnelSmoothingJob.cs` actually matched that decision. `RegisterActivePath` and `UnregisterActivePath` also resolved the invalidation ring even though they only mutate active-path records and cell masks.
Solution: Re-read the source as the authority, changed the Burst job to the compile-time inverse expression, normalized invalid `MathLod` bytes to Low with `PathFunnelResultFlags.InvalidMathLod`, and split active-path mutation resolution away from invalidation-ring resolution.
Rejected Alternatives: Leaving docs ahead of source was rejected because the anti-amnesia protocol requires disk truth. Resolving the invalidation buffer for register/unregister was rejected because it widens the mutation view without need. Passing unknown Math LOD bytes through to result telemetry was rejected because blackbox consumers need executed-tier truth.
Scalability potential: Low/Toaster = malformed LOD input falls back to cheap two-portal smoothing with an explicit flag. Middle = active-path registration touches fewer vault buffers. High/Ultra = overkill smoothing remains available only for known High/Ultra requests, preserving telemetry truth.
Hardware Impact: No measured microsecond saving is claimed for this pass. The expected runtime effect is reduced vault handle traffic during register/unregister and deterministic fallback for invalid LOD input; compile validation remains blocked upstream with zero owned pathfinding diagnostics.

## Decision 022 - Blackbox Dump Catch Narrowing

Problem: `TryDumpBlackBox` contained dump-path failures, but its broad `catch (Exception)` could also hide unexpected runtime bugs in the crash evidence path.
Solution: Replaced the broad catch with specific filesystem/path exception catches: `IOException`, `UnauthorizedAccessException`, `NotSupportedException`, and `ArgumentException`.
Rejected Alternatives: Letting dump I/O exceptions escape was rejected because the blackbox must not create a second crash. Catching all exceptions was rejected because unknown runtime faults should not be silently downgraded to a dump failure bit.
Scalability potential: Low/Toaster = same no-hot-path disk I/O behavior. Middle/High/Ultra = crash tooling still gets a deterministic dump-failure flag for expected file/path failures, while unexpected faults remain visible to integration diagnostics.
Hardware Impact: No measured microsecond saving is claimed. This is survival semantics on a non-hot explicit dump path.

## Decision 023 - WFC Cell Contract Boundary

Problem: The Burst door-block check treated any portal cell index inside the current `WfcGridBitmasks` buffer length as valid. If an oversized or stale grid buffer existed, out-of-contract portal cells could participate in path blocking despite the WFC contract defining the authoritative cell count.
Solution: `IsDoorCellBlocked` now rejects cell indices greater than or equal to `PathFunnelConstants.WfcOutpostCellCount`, emits `PathFunnelResultFlags.InvalidWfcCell`, and only then checks the buffer length. Blocked-path telemetry now reports `ProcessedPortalCount = i + 1` so the count reflects the examined portal, not its zero-based index.
Rejected Alternatives: Trusting the buffer length was rejected because GlobalDataVault capacity is not the semantic contract. Blocking on invalid cells was rejected because malformed portal data should be flagged and ignored, not converted into a false closed door.
Scalability potential: Low/Toaster = no extra allocation and only a branch before door flag lookup. Middle/High/Ultra = blackbox consumers can distinguish malformed portal data from real WFC closures without changing the ABI.
Hardware Impact: No measured microsecond saving is claimed. This is correctness and diagnostics hardening; the extra branch is non-material compared with path repair churn from false invalidation.

## Decision 024 - Active Path Cell Count Truth

Problem: `RegisterActivePath` ignored invalid corridor cells when setting the exact WFC bitmask, but still stored the clamped input count in `PathFunnelActivePath.CellCount`. That made active-path metadata claim cells that were not actually represented in the mask.
Solution: `SetPathCell` now returns whether it packed a cell, and registration stores a `validCellCount` based only on successful bit writes.
Rejected Alternatives: Counting raw caller input was rejected because the bitmask is the authoritative invalidation truth. Throwing on invalid cells was rejected because path registration must stay fail-soft and zero-GC under malformed corridor data.
Scalability potential: Low/Toaster = same loop, one boolean return, no allocation. Middle/High/Ultra = active-path metadata now matches the exact mask used by invalidation consumers.
Hardware Impact: No measured microsecond saving is claimed. This prevents misleading telemetry and avoids downstream recovery logic overestimating corridor coverage.

## Decision 025 - Unique WFC Cell Count Truth

Problem: The previous cell-count fix counted every successful `SetPathCell` call, but duplicate corridor entries can target a bit that is already set. That still lets caller duplication inflate `PathFunnelActivePath.CellCount` above the unique WFC mask truth used by invalidation.
Solution: `SetPathCell` now reads the target mask word, returns false when the bit is already set, and only increments `validCellCount` on a new bit write. Refreshed build evidence after the change: `Hecton8.Core.csproj` exits 0, while Assembly-CSharp remains blocked by 216 `RealtimeCSG.csproj` CS2001 missing source errors with 0 owned pathfinding matches.
Rejected Alternatives: Counting duplicate input cells was rejected because the bitmask is the only authoritative invalidation surface. Adding a separate deduplication container was rejected because it would add memory traffic and violate the zero-allocation path registration goal. Editing RealtimeCSG or generated build inputs was rejected because those files are outside AI/PATHING.
Scalability potential: Low/Toaster = one hot word load and bit test prevents duplicate metadata without allocation. Middle = active path metadata stays aligned with exact invalidation masks. High/Ultra = richer path telemetry can trust cell counts when rendering/debugging high-density route diagnostics, while this prompt still owns no VFX.
Hardware Impact: No measured microsecond saving is claimed. The expected gain is correctness under noisy WFC corridor input and avoiding downstream recovery work based on inflated cell counts; Core build validation is green, Assembly validation remains blocked outside this domain.

## Decision 026 - Signal/Vault Phase Truth

Problem: `ProcessWfcStateSignal` resolved `CurrentFlags` from `BufferID.WfcOutpostGrid` when the vault cell existed. Save persistence writes that vault from the same typed signal lane, so phase ordering can leave the vault one frame behind the signal. A door close could be published with `CurrentFlags` closed while the vault still reads open, causing path invalidation to be skipped.
Solution: Treat `WfcOutpostStateChangedSignal.CurrentFlags` as the authoritative transition value for close detection, mask it with `WfcOutpostPersistenceConstants.MutableFlagMask`, keep reading the vault cell as an audited snapshot, and set `PathFunnelTelemetryFlags.WfcVaultSignalMismatch` when the vault snapshot disagrees with the signal. No new signal lane was created.
Rejected Alternatives: Trusting the vault over the signal was rejected because it can miss a live door-close event under phase lag. Polling WFC implementation classes was rejected as cross-domain coupling. Creating a duplicate pathfinding-specific door signal was rejected because `WfcOutpostStateChangedSignal` already exists as the typed lane. Running another dotnet build was rejected for this pass because the user explicitly ordered no repeated rebuilds.
Scalability potential: Low/Toaster = one masked byte compare keeps the fast exact-bit invalidation path and avoids expensive re-path recovery from missed closes. Middle = blackbox can distinguish normal close events from signal/vault phase disagreement. High/Ultra = presentation/debug consumers can react to trustworthy invalidation state without increasing gameplay broadcast cost; VFX remains out of scope for this XML.
Hardware Impact: No measured microsecond saving is claimed. The hot-path change is one byte mask and optional mismatch bit set per WFC state signal; the correctness gain is preventing stale-vault close misses that would force later AI recovery work.

## Decision 027 - WFC Mutable Mask Isolation

Problem: WFC state payloads are byte fields with a defined mutable flag mask. Runtime invalidation already masked `CurrentFlags`, but `PreviousFlags` was still tested raw, and the Burst door check read raw vault bytes. Future or reserved bits should not be able to influence door-open truth.
Solution: Mask `signal.PreviousFlags` with `PathFunnelConstants.WfcMutableFlagMask` before testing the close transition, pass the masked previous flags into invalidation payloads, and mask Burst-side `WfcGridBitmasks[cellIndex]` before checking `WfcDoorOpenFlag`.
Rejected Alternatives: Trusting producers to always mask forever was rejected because pathfinding is a critical AI/survival consumer and must defend its own contract boundary. Adding a new local mask constant was rejected because the Core persistence contract already owns `MutableFlagMask`. Running a rebuild was rejected because the current user instruction says not to rebuild every pass.
Scalability potential: Low/Toaster = one byte mask in runtime and one byte mask in the Burst door check keeps exact path invalidation predictable. Middle/High/Ultra = WFC can add future mutable bits without destabilizing pathing door truth or blackbox decoding.
Hardware Impact: No measured microsecond saving is claimed. The extra byte masks are negligible; the value is preventing reserved-bit false positives or false negatives in door invalidation.

## Decision 028 - Blackbox Transient Flag Truth

Problem: `PathFunnelTelemetryFlags.WfcVaultSignalMismatch` was recorded by setting `runtimeState.TelemetryFlags`. Without a frame-scope clear, one signal/vault disagreement would make every later blackbox frame look like an active phase mismatch.
Solution: Added `PathFunnelTelemetryFlags.TransientFrameMask` and clear transient bits immediately after `WriteTelemetry` captures the current frame. Dump failure handling preserves the just-written frame flags and patches the current slot with `BlackBoxDumpFailed` if the explicit dump fails, while keeping `BlackBoxDumpFailed` persistent in runtime state.
Rejected Alternatives: Leaving mismatch sticky was rejected because blackbox evidence must show when a fault occurred, not smear it across later frames. Clearing all telemetry flags was rejected because dump failure is a durable status until the next dump request. Running another rebuild was rejected because the current user instruction says not to rebuild every pass.
Scalability potential: Low/Toaster = one ushort mask per late-frame telemetry write keeps crash evidence precise without allocation. Middle/High/Ultra = tools can distinguish a one-frame signal/vault phase race from ongoing runtime failure without adding signal traffic or path scans.
Hardware Impact: No measured microsecond saving is claimed. The extra ushort mask is negligible; the value is postmortem correctness and avoiding false investigation trails.

## Decision 029 - Portal Input Clamp Truth

Problem: `FunnelSmoothingJob` clamped `PortalCount` to `Portals.Length`, then used that clamped count as the truth for partial-lookahead status. If the caller requested more portals than the native buffer held, the job could report `Complete` after smoothing only the available prefix. A negative portal count also collapsed to a direct start-goal path with only a flag.
Solution: Added `PathFunnelResultFlags.PortalInputClamped`, made negative `PortalCount` return `InvalidInput`, made positive counts with missing/empty portal buffers return `InvalidInput`, and compared partial-lookahead status against the requested portal count rather than the clamped buffer length. Truncated buffers now stop at the last available portal midpoint and report partial/truncated flags.
Rejected Alternatives: Trusting the caller was rejected because corridor input is a safety boundary; a fake complete path can cut corners through closed geometry. Allocating a repair buffer was rejected because the Burst job must stay zero-allocation and caller-owned. Running another rebuild was rejected because the current user instruction says not to rebuild every pass.
Scalability potential: Low/Toaster = malformed path requests fail fast with no recovery allocation or physics probe. Middle/High/Ultra = blackbox and path consumers can distinguish real partial look-ahead from complete corridor smoothing before spending cycles on presentation/debug overlays.
Hardware Impact: No measured microsecond saving is claimed. The extra branches run once per job entry and prevent later expensive re-path/debug work caused by false complete results.

## Decision 030 - AUP Sidecar Clamp Truth

Problem: `ConvertWaypointsToAup` converted `min(WaypointCount, Waypoints.Length, WaypointAups.Length)` entries without flagging when the optional AUP sidecar buffer was smaller than the produced waypoint count. Consumers could receive `WaypointCount = N` but only the first M AUP entries valid, with no telemetry bit explaining the partial sidecar.
Solution: Added `PathFunnelResultFlags.AupOutputClamped` and set it when `WaypointAups.Length` is smaller than the safe waypoint count. The job still writes only the valid prefix and does not allocate or repair caller buffers.
Rejected Alternatives: Forcing AUP output to be required was rejected because the prompt defines `NativeArray<float3> Waypoints` as the primary SOA output and AUP is a sidecar. Allocating a larger sidecar was rejected because the Burst job must use caller-owned NativeArrays only. Running another rebuild was rejected because the current user instruction says not to rebuild every pass.
Scalability potential: Low/Toaster = no allocation and one length compare prevents hidden deterministic-coordinate loss. Middle/High/Ultra = debug and presentation consumers can detect incomplete AUP sidecars before spending cycles on route overlays or postmortem decoding.
Hardware Impact: No measured microsecond saving is claimed. The added compare is negligible; the value is preventing silent mismatch between visual waypoint count and deterministic AUP output.

## Decision 031 - Agent Radius Clamp Telemetry

Problem: `TightenPortalForRadius` made non-finite `AgentRadiusMeters` safe by selecting zero, but the result payload did not expose that malformed radius input changed corner-shrink behavior. Negative radius values were also clamped to zero without telemetry.
Solution: Added `PathFunnelResultFlags.AgentRadiusClamped`. Non-finite radius sets both `NonFiniteInput` and `AgentRadiusClamped`; negative finite radius sets `AgentRadiusClamped` and clamps to zero.
Rejected Alternatives: Silent clamping was rejected because NaN vaccination must be visible in blackbox/result payloads. Throwing or failing the whole path was rejected because malformed caller tuning should not break path output when a deterministic safe fallback exists. Running another rebuild was rejected because the current user instruction says not to rebuild every pass.
Scalability potential: Low/Toaster = no allocation and one branch prevents bad radius data from poisoning the path. Middle/High/Ultra = tuning/debug consumers can detect radius sanitation instead of misreading corner quality as a funnel math problem.
Hardware Impact: No measured microsecond saving is claimed. The added branch is negligible; the value is deterministic safety and postmortem clarity.

## Decision 032 - Telemetry Ring Contract Clamp

Problem: The blackbox telemetry buffer is requested at 300 entries, but vault handles can resolve a larger buffer if the shared `BufferID` was ever grown. `WriteTelemetry` and `TryDumpBlackBox` used the resolved length directly, which could widen the ring and dump beyond the mandated last 300 frames.
Solution: Added `ResolveTelemetryRingLength` and used it for telemetry cursor clamping, telemetry cursor advancement, runtime-state initialization in the telemetry path, and dump byte count. The buffer still requires at least 300 entries, but the contract window remains exactly `PathFunnelConstants.TelemetryFrames`.
Rejected Alternatives: Trusting vault capacity was rejected because blackbox evidence must be a fixed-size contract. Shrinking or reallocating the vault buffer was rejected because pathfinding does not own arena compaction and must not add memory churn. Running another rebuild was rejected because the current user instruction says not to rebuild every pass.
Scalability potential: Low/Toaster = fixed 300-entry dump prevents unnecessary MicroSD write pressure. Middle/High/Ultra = postmortem decoders can rely on a stable binary length independent of vault growth.
Hardware Impact: No measured microsecond saving is claimed. The extra `min` is negligible; dump I/O is bounded to the intended 300-frame payload.

## Decision 033 - Editor-Time Registry Guard

Problem: `PathFunnelNavmeshRuntime.OnEnable` registered fast tick, late-frame tick, hot-swap listener, and vault handles without checking `Application.isPlaying`. A scene object enabled during editor/import time could mutate runtime registries outside the dispatcher lifecycle.
Solution: Added an early `Application.isPlaying` guard to `OnEnable`; runtime registration and vault handle resolution now happen only in play mode.
Rejected Alternatives: Relying on the absence of `[ExecuteAlways]` was rejected because Unity lifecycle calls can still surprise tooling during editor scene handling. Moving registration to `Awake` was rejected because cross-system registry wiring should stay in enable/disable lifecycle and remain reversible. Running another rebuild was rejected because the current user instruction says not to rebuild every pass.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; editor/import stability improves by avoiding accidental registry mutation.
Hardware Impact: No runtime microsecond saving is claimed. The guard executes once per enable and prevents cold-path lifecycle corruption.

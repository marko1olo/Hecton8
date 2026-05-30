# AUTONOMOUS_REVIEW Rationale

Problem: The user requested autonomous improvement/review after a broad multi-agent audit. The tree is already heavily dirty, so uncontrolled changes would increase integration risk.
Solution: Work lane-by-lane. Start with core authority and hot-path candidates, inspect current source and diffs, then patch only direct defects with narrow ownership.
Rejected Alternatives: A broad cleanup wave would collide with concurrent agent work and obscure blame; vendor refactoring would violate third-party quarantine without an explicit task.
Scalability potential: Low tier benefits from removing hot lookup/alloc candidates; middle/high/ultra can spend saved budget on visual fidelity after core routes are stable.
Hardware Impact: No runtime gain is claimed until profiler/device proof exists. Static target is preventing MX350/i3 hot-path overhead and global-route drift.

Problem: Static scans can overstate both defects and fixes.
Solution: Treat grep/diff findings as candidate evidence only. Use direct source inspection before any edit and mark unverified claims as pending verification.
Rejected Alternatives: Reporting source text hits as runtime proof would violate the evidence mandate.
Scalability potential: Keeps cheap static triage separate from expensive runtime/device validation.
Hardware Impact: None directly; prevents false confidence before hardware validation.

Problem: `HardwareThermalService.Tick` writes one black-box entry every frame, but the current source acquired and released a `GlobalDataVault` writer lock for that owner-only ring write. That path mutates lock metadata and block flags per frame.
Solution: Keep cold `OpenOrAcquire...`/write-lock setup unchanged, but switch the hot `WriteBlackBox` path to `TryResolveHandle` for a current-phase owner-write view, then write the single telemetry entry directly.
Rejected Alternatives: Holding a write lock across frames would block relocation/consumers; rewriting `GlobalDataVault` would be broad core risk; disabling black-box writes would violate the telemetry mandate.
Scalability potential: Low/MX350 avoids repeated writer-fence metadata churn; middle/high/ultra keep the 300-frame telemetry ring while spending saved CPU budget on visual systems.
Hardware Impact: Estimated static saving is one DataVault writer acquire plus one release per frame for `HardwareThermalService`; exact microseconds remain PENDING PROFILER VERIFICATION.

Problem: `AcousticZoneController.Tick` calls `RefreshSoundscapeTierContext(false)` every frame. When `_cachedSoundscapeReadModel` is empty, the fallback `ResolveSoundscapeReadModel(false)` periodically queried `GlobalRegistry.SoundscapeTierReadModel` from the hot route.
Solution: Keep `GlobalRegistry` lookup only for forced cold resolution. Runtime service changes are already handled through `IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced`, which updates the cached soundscape read model.
Rejected Alternatives: Removing soundscape refresh from Tick would risk stale tier response; adding another polling lane would preserve the same architectural defect.
Scalability potential: Low tier avoids hidden registry polling during acoustic Tick; higher tiers keep the same audio presentation behavior through cached read models and hot-swap updates.
Hardware Impact: Estimated static saving is the elimination of periodic registry lookup attempts from `AcousticZoneController.Tick` when soundscape cache is empty. Exact microseconds remain PENDING PROFILER VERIFICATION.

Problem: `ObjectPoolManager` now caches `IPoolable[]` during pool instantiation. Interface references do not reliably use Unity's destroyed-object null semantics, so a destroyed component can survive as a non-null interface and receive `OnSpawn`/`OnDespawn`.
Solution: Add `IsValidPoolable` with a UnityEngine.Object null check and use it for cached callback dispatch and typed cached component lookup.
Rejected Alternatives: Re-scanning components on every spawn/despawn would undo the cache optimization; ignoring destroyed-object semantics would leave a rare crash path.
Scalability potential: Low tier keeps cached component dispatch; middle/high/ultra get the same pool behavior without repeated component probing.
Hardware Impact: Adds a tiny branch/type check per cached `IPoolable` callback, avoids dynamic component scans and destroyed-component callback faults. Exact cost/saving remains PENDING PROFILER VERIFICATION.

Problem: The user requested continued autonomous work. The next risky zone is visual/platform runtime code, where small hot-path leaks can cost more than broad static architecture claims.
Solution: Continue with a bounded inspection of `HectonUnderwaterVisuals`, `VRAMPressureMonitor`, and `RandomEventSystem`; patch only directly evidenced defects.
Rejected Alternatives: Touching vendor code or running another broad audit would create more churn than value at this stage.
Scalability potential: Low/MX350 gets cheaper visual/platform cadence; higher tiers keep visual overkill capacity after hot-path cleanup.
Hardware Impact: Pending. This pass is source-level until profiler/device proof exists.

Problem: `VRAMPressureMonitor` owns emergency global quality overrides (`QualitySettings.globalTextureMipmapLimit`, `QualitySettings.lodBias`, and `BrgLodDistanceScalar`), but disable/destroy cleanup restored only the BRG scalar. A disabled owner could leave downgraded global texture mips or LOD bias behind.
Solution: Add `RestoreGlobalQualityOverrides` and call it from `OnDisable` and `OnDestroy`. The helper restores active mip limit, LOD bias, active flag, and BRG scalar without changing sampling cadence.
Rejected Alternatives: Using the normal pressure response path during teardown would publish extra runtime signals from lifecycle cleanup; ignoring it would leak global presentation state across scene/system transitions.
Scalability potential: Low tier can shed quality under pressure, then return to authored baseline when the owner is gone; middle/high/ultra avoid being stuck in low-tier visual state after monitor teardown.
Hardware Impact: No per-frame runtime gain claimed. It prevents persistent visual-quality degradation and stale pressure state after lifecycle transitions.

Problem: `Fabricator.CompleteCraft` commits a successful `BaseLogisticsNetwork` reservation and clears `_networkReservation`, but left `_networkCostCount` populated until the next reservation/refund cycle. That leaves stale cost-ledger state inside a completed craft owner.
Solution: Clear `_networkCostCount` immediately after `CommitReserved` succeeds, matching the existing reservation-owner cleanup pattern and `RefundIngredients` count reset.
Rejected Alternatives: Clearing the backing arrays adds unnecessary work and is not done by the existing refund path; changing delivery/refund semantics on output failure is gameplay-authority risk outside this narrow ledger defect.
Scalability potential: Low tier avoids stale diagnostic/state branching around completed network crafts; middle/high/ultra keep the same logistics behavior with cleaner owner state for later instrumentation.
Hardware Impact: Runtime cost is one integer store on successful network-backed craft completion. It prevents stale state from surviving into diagnostics or later owner logic; exact microseconds are not claimed.

Problem: `Fabricator.CompleteCraft` commits local and network input reservations before the physical output stack or inventory fallback is proven. If output delivery fails after input commit, the old route could return with inputs consumed and no owned output.
Solution: Add a bounded owner-local pending output state in `Fabricator`. Failed post-commit output now stores remaining result quantity in the fabricator owner, blocks new craft starts, and retries delivery on `SlowTick` without repeated fault spam.
Rejected Alternatives: Rolling back committed network inputs is unsafe because the original storage owner has already consumed the reservation; adding a full output reservation API is the correct long-term fix but too broad for this autonomous pass; pretending the item was acquired would corrupt player inventory truth.
Scalability potential: Low devices avoid extra allocation and keep retry cadence at slow tick; middle/high/ultra can later replace the local buffer with proper output reservations or dedicated visible output bins without changing input ownership.
Hardware Impact: Adds four owner fields and one slow-tick branch. No frame-time saving claimed; the gain is correctness: no silent post-commit craft output loss in the checked route.

Problem: `PowerGrid.ResolveBatteryDispatch` stages battery charge/discharge from global raw distribution, then commits staged energy after final graph evaluation. In multi-island topology this can charge/discharge batteries from global surplus/deficit instead of proven island-local route contribution.
Solution: Record as critical residual risk requiring per-island dispatch or final solved contribution data before commit. No code patch was applied in this pass.
Rejected Alternatives: A blind guard that disables battery dispatch whenever `IslandCount > 1` would stop energy teleportation but also remove valid island-local batteries; moving commit order alone would not prove delivery.
Scalability potential: Low/mid/high/ultra all need the same truth route. Fidelity tiers must not change stored battery energy authority.
Hardware Impact: No runtime change. The required future fix is correctness-first, not optimization.

Problem: `ContextualPhysicalIkRig.CaptureScheduledState` updated spine targets, appendage targets, and predictive repair latch before resolving the entity throttle state. Throttled entities with `UpdateThisFrame == 0` therefore still paid part of the expensive target-capture cost.
Solution: Resolve throttle after the cheap root/viewer pose read, then run spine/appendage/predictive target capture only when `updateThisFrame != 0`. Cheap timers and presentation decay remain active.
Rejected Alternatives: Removing LateFrame publication or rewriting to a pure animation-stream architecture is broader than this pass and would collide with existing playable injection; disabling IK for throttled entities would cause visible pops instead of time-sliced reuse of previous targets.
Scalability potential: Low tier skips target capture on throttled frames; middle/high/ultra keep higher update cadence through existing quality-weight and distance tiers.
Hardware Impact: Static saving is avoided Transform/voxel target capture work on skipped IK frames. Exact microseconds remain pending profiler/device proof.

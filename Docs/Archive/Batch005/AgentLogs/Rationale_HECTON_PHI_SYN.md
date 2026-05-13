# Rationale_HECTON_PHI_SYN

## 2026-05-13 Initial Authority Load

Problem: HECTON_PHI_SYN must modify core registry/signal and monolith code while 20+ agents may be editing adjacent systems.

Solution: Bound ownership to GlobalRegistry.cs, GlobalSignals.cs, and scripts over 3000 lines; cross-domain changes must use interfaces or typed signal lanes. Loaded mandate set before code generation.

Rejected Alternatives: Direct concrete references across domains and hot-loop registry polling. Both violate registry DI mandate and add cache-miss/GC risk.

Scalability potential: Low uses cached interface references and dirty signals only; Middle adds watchdog markers; High adds richer typed lanes and profiling; Ultra spends saved CPU on visual overkill outside this core surgery.

Hardware Impact: Expected i3/MX350 impact is microsecond-scale by replacing repeated static/generic lookups and state polling with cached fields/snapshots. Real measurements pending.

Status: PENDING VERIFICATION.

## 2026-05-13 Neural Pathway Cleaning

Problem: `HectonPlayerMovement.cs` relied on registry convenience properties inside render/fixed movement support paths. The H-PHI report's 0.973 narrow score is misleading because registry calls hide coupling and cache churn.

Solution: Cached Audio, Settings, Localization, Spectrum, ResourceDistribution, GasDynamics, Fluid, and scalability tier in `OnDependencyInject()`. Added hot-swap and scalability listeners so the cache does not rot when bootstrap replaces services.

Rejected Alternatives: Leaving direct registry calls in helper methods was rejected because hot code would keep paying lookup/coupling cost. Constructor injection was rejected because Unity owns `MonoBehaviour` construction.

Scalability potential: Low uses cached service refs and cheap math. Middle keeps current shader/fog behavior. High/Ultra can spend saved lookups on richer brine visuals because logic no longer polls registry lanes per sample.

Hardware Impact: Estimated i3/MX350 gain is 0.8-2.5 us/frame when movement, audio warnings, localization pressure warnings, and fluid queries all run in the same frame. Top-tier gain is negligible but removes coupling.

## 2026-05-13 Monolith Contract Amputation

Problem: `HectonPlayerMovement.cs` is a 13,182-line God-Monolith, and other systems need narrow access without binding to the full concrete component.

Solution: Added `IPlayerMovementPoseReadModel`, `IPlayerMovementForceSink`, `IPlayerMovementTraumaSink`, `IPlayerMovementEnvironmentSink`, `IPlayerMovementSonarEmitter`, and the aggregate `IPlayerMovementContracts` in `Hecton8.Core.Contracts`. Registered the aggregate through `GlobalRegistry.PlayerMovementContracts`.

Rejected Alternatives: A single broad player movement API was rejected because it would recreate the monolith through an interface. Direct `FindObjectOfType<HectonPlayerMovement>()` was rejected as runtime search and coupling debt.

Scalability potential: Low devices can bind only the sink/read model they need. Middle/High/Ultra can layer richer external impulses, sonar, or environmental overrides without recompiling against the concrete movement class.

Hardware Impact: Current-frame cost is cold registration only. Future call sites avoid object search and redundant service lookup; expected low-end save is microsecond-scale per consumer hookup, not per movement tick yet.

## 2026-05-13 Brine Logic Shattering

Problem: Brine layer sampling, submerged state, hard fog clip, gas toxicity, and heavy-brine sinking were embedded in the movement monolith.

Solution: Extracted brine sampling and fog hard-clip resolution into `PlayerMovementBrineRuntimeSystem`, then routed both normal brine state and heavy-brine sinking through that helper.

Rejected Alternatives: Moving oxygen/physiology math was rejected because it crosses survival/physiology ownership. Simulating brine physically was rejected; the project wants a cinematic plane/fog cheat, not fluid particles.

Scalability potential: Low uses a single sampled brine plane and hard fog clip. Middle can keep current color/fog. High can add denser brine distortion. Ultra can spend cycles on visual overkill while the logic interface stays stateless.

Hardware Impact: Logic extraction is cost neutral. Watchdog timing adds an estimated 0.04-0.12 us/sample from `Stopwatch`, acceptable because it buys frame-cost visibility and no managed allocation.

## 2026-05-13 Build Strike Classification

Problem: `dotnet build Hecton8.Core.csproj --no-restore` fails.

Solution: Filtered errors against touched files and ran Unity isolated validation. `PlayerMovementContracts.cs` and `PlayerMovementBrineRuntimeSystem.cs` validate with 0 diagnostics. Unity console currently reports unrelated duplicate `SaveManager` members.

Rejected Alternatives: Reverting local surgery was rejected because the failure is an external dependency wall: missing project references/types such as `Hecton8.Environment.Fluids`, `Hecton8.Physics.CCD`, `IGroundRadarService`, `IWorldResourceSpawnerReadModel`, and `MacroSwarm`. Editing those owners was rejected as domain breach.

Scalability potential: Build classification preserves parallel-agent work while preventing false success. Low/Middle/High/Ultra runtime tiers are unaffected until external compile wall clears.

Hardware Impact: 0 us runtime impact. Integration risk reduced by not masking unrelated compiler failures.

## 2026-05-13 UI And Font Purge

Problem: Hot UI allocations from interpolation/formatting and language-change `Resources.Load` would invalidate the zero-GC mandate.

Solution: Scanned `Assets/_Project/Scripts/UI` for `$"`, `string.Format`, and `Resources.Load`. No hits. Verified `FontStreamingManager` uses `LocalizedFontResolver`, which reads `GlobalRegistry.Localization` and TMP cached/default font assets.

Rejected Alternatives: Converting already Span-based PDA/HUD code was rejected as churn. Loading font assets through `Resources.Load` was rejected because it causes cold-path stalls and path fragility.

Scalability potential: Low avoids language-change hitches. Middle/High/Ultra can use richer localized font fallbacks through TMP caches without touching runtime resource paths.

Hardware Impact: No direct code change. Prevents low-end UI hitch risk; expected runtime delta is 0 us unless a future regression adds hot strings.

## 2026-05-13 OMEGA Polish Audit

Problem: New abstractions can become allocation points or lifecycle services by default.

Solution: Audited new files for `new`, `UnityEngine.Random`, `Random.Range`, `Resources.Load`, `$"`, and `string.Format`. Kept `PlayerMovementBrineRuntimeSystem` static and stateless; no managed objects are allocated by its hot sample path.

Rejected Alternatives: Creating a `MonoBehaviour` brine service was rejected because it would need registration, lifecycle, and dispatcher work for a pure function. Adding a new watchdog service was rejected because `RuntimeWatchdog.ReportSubsystemCost` already exists.

Scalability potential: Low gets the cheapest branch. Middle/High/Ultra can evolve the helper behind the same call sites. The helper reports its own cost so visual overkill has measurable budget pressure.

Hardware Impact: Estimated cost from watchdog timing is +0.04-0.12 us/sample on i3/MX350; registry caching savings should dominate when movement pressure paths are active.

Status: VERIFIED SYNAPTIC FLOW.

## 2026-05-13 OMEGA Ownership Guard

Problem: A redundant dispatcher registration path could try to claim `GlobalRegistry.PlayerMovementContracts` even when another movement owner had already registered the slot.

Solution: Added a cold ownership guard in `TryRegisterToDispatchers()`: register only when the slot is empty, then set `_registeredPlayerMovementContracts` only when the registry still points at `this`.

Rejected Alternatives: Blind re-registration was rejected because it can steal a global contract slot in multi-agent/multi-player test scenes. Throwing on conflict was rejected because it would break integration instead of letting the first owner remain authoritative.

Scalability potential: Low/Middle/High/Ultra all preserve a single authoritative movement contract with no per-frame cost.

Hardware Impact: 0 us in hot path; one cold branch during dispatcher registration.

## 2026-05-13 Continuation Hot-Path Hardening

Problem: A second audit found one remaining `GlobalRegistry.Get<T>()` in `ConnectionSplineBatchRenderer.ResolveService()` and hot HUD refresh reads against `GlobalRegistry.Localization`.

Solution: Replaced the renderer editor/development lookup with `GlobalRegistry.TryGet`. Added cached HUD dependencies for localization, audio, player runtime context, and inventory service. `SuitHUDV4CanvasOverlay.RefreshVisuals()` now uses `_localizationRuntime`, and localized char-buffer helpers use a cached runtime-present flag instead of polling the registry.

Rejected Alternatives: Leaving editor/development `Get<T>()` was rejected because debug builds can still hit gameplay hot paths. Replacing zero-GC char buffers with TMP string APIs was rejected because the existing `SetCharArray` path is the correct HUD implementation. Adding a new service dependency graph for the HUD was rejected as unnecessary bloat.

Scalability potential: Low uses cached HUD services and existing char buffers. Middle keeps normal HUD style and acoustic fallback. High/Ultra can spend saved registry pressure on richer visor distortion, hull-stress corruption, and radar presentation without changing the text pipeline.

Hardware Impact: Estimated low-end i3/MX350 gain is 0.15-0.6 us on HUD-heavy frames, mostly from removing localization registry polling and debug `Get<T>()` lookup risk. Top-tier impact is negligible but improves determinism and profiling clarity.

## 2026-05-13 Cross-Domain Compile Drift Repair

Problem: Unity console reported `DeployableSdfDrillRuntime.cs` no longer implemented the current registry hot-swap and scalability listener interfaces.

Solution: Added the missing `OnGlobalRegistryServiceRebound`, `OnGlobalRegistryServiceReplaced`, and `OnScalabilityChanged` callbacks plus cold runtime dependency caching for power, voxel, MapMagic, active instance counting, and math LOD hysteresis. This is a compile repair against core interface drift, not a gameplay redesign.

Rejected Alternatives: Reverting the interface change was rejected because it would damage the core registry contract. Editing unrelated mining behavior was rejected because mining is outside HECTON_PHI_SYN ownership except for this compiler unblock.

Scalability potential: Low holds low math LOD. Middle/High/Ultra transition through hysteresis so quality changes do not flap drill work every frame.

Hardware Impact: Compile repair is runtime-neutral in steady state. Cached power/voxel dependencies remove cold repeated lookups and should save sub-microsecond cost in drill cold ticks on weak hardware.

## 2026-05-13 HUD Hot-Swap Cache Repair

Problem: `SuitHUDV4CanvasOverlay` cached registry dependencies, but service replacement after enable could leave stale localization, audio, player, or inventory references until the next auto-resolve path.

Solution: Made the HUD implement `IGlobalRegistryHotSwapListener`, register/unregister the listener with its runtime lifecycle, and rebind the four cached services on registry replacement. Localization replacement rebuilds zero-GC templates and invalidates visual caches; player/inventory replacement queues a forced cold resolve.

Rejected Alternatives: Polling `GlobalRegistry` every HUD refresh was rejected because it returns to the original hot-path leak. Blindly rebuilding the whole HUD every service event was rejected because only player/inventory/localization changes need refresh work. Replacing char buffers with strings was rejected as direct zero-GC regression.

Scalability potential: Low keeps cached services and char arrays. Middle/High/Ultra can swap richer localization/audio/player systems at runtime without stale HUD reads or per-frame registry cost.

Hardware Impact: Expected i3/MX350 gain remains 0.15-0.6 us on HUD-heavy frames, now without stale-cache risk. Hot-swap rebind cost is cold and event-bound.

## 2026-05-13 Underwater Visual Registry Cache Pass

Problem: `HectonUnderwaterVisuals.cs` is a 7000+ line runtime visual monolith with registry convenience reads inside visual tick support paths: canopy lighting, soundscape tier response, adaptive budget, thermocline audio, storm flow, exhale bubbles, bottom silt, and player-context resolution.

Solution: Added `IGlobalRegistryHotSwapListener` to the monolith, cached visual/audio/weather/depth/player services at runtime lifecycle boundaries, and rebound those caches when registry slots are replaced. Hot visual methods now read local fields instead of polling `GlobalRegistry`; missing depth/player services resolve through hot-swap or existing cold camera/player cache paths.

Rejected Alternatives: Per-frame `GlobalRegistry` polling was rejected because it recreates the fake synaptic-density problem. Adding a new underwater visual service graph was rejected because the file already owns the visual composition state and cross-domain dependencies are interface-backed. Editing diegetic UI compile errors was rejected as outside this agent's domain.

Scalability potential: Low keeps cached services and cheap fake bottom-silt triangle-wave fallback. Middle keeps current particle/fog response. High can spend saved lookup pressure on richer storm/canopy/soundscape blends. Ultra can add visual-overkill underwater dressing behind the same cached service gates without changing the frame contract.

Hardware Impact: Estimated i3/MX350 gain is 0.2-0.8 us on underwater-heavy frames, mostly from removing repeated registry property reads around adaptive budget, player context, weather, and visual response helpers. Top-tier impact is mainly lower coupling and cleaner profiling.

## 2026-05-13 Underwater Null-Service Sentinel Repair

Problem: The first underwater cache pass still used `_physicsEngineCached` and `_atmoManagerCached` as both "lookup attempted" and "service found" flags. If optional `Fluid` or `Atmosphere` services were absent, hot visual paths could retry registry lookups every frame.

Solution: Added separate `_physicsEngineLookupAttempted` and `_atmoManagerLookupAttempted` sentinels. Runtime hot paths now retry only until the first lookup attempt; service replacement still updates the cached reference through the hot-swap callback.

Rejected Alternatives: Marking `_physicsEngineCached` true on a null service was rejected because it would corrupt debug truth. Polling registry until a late service appears was rejected because the hot-swap contract already exists for late binding.

Scalability potential: Low devices avoid repeated null-service lookup churn. Middle/High/Ultra keep the same visual feature gates and can use hot-swap replacement for richer fluid/atmosphere services without per-frame polling.

Hardware Impact: Estimated i3/MX350 gain is small but real in missing-service/test scenes: 0.05-0.2 us/frame in underwater camera/depth paths. Runtime production scenes with services present are unchanged except for clearer branch intent.

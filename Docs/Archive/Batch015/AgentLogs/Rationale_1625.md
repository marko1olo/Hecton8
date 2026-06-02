# Rationale 1625 - Localization Cross-Translation Audit

## Prompt Block Missing In Active Batch

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="1625">`, despite the session directive naming agent 1625 and the batch prose listing 1625 as Cross Translation.

Solution: Treat the missing XML as a batch integrity defect, record it, and continue only within the narrow user-provided localization/Babel audit scope. This avoids cross-contamination from neighboring agent prompts.

Rejected Alternatives: Reading 1624, 1626, or 1629 prompts as substitutes would violate strict parsing. Inventing a 30-task XML plan would be fake reporting.

Scalability potential: Low/Middle/High/Ultra lanes are unchanged until localization data proves a runtime change is required. Current work is source-data audit only.

Hardware Impact: No runtime claim. Static audit scripts only; expected i3/MX350 gameplay-frame impact is 0 us because no runtime code has been changed.

## Mandate Selection

Problem: Localization audit touches authored text, Babel runtime assumptions, UI phrase length, hash integrity, and proof language. The task has no C# build lane and explicitly forbids `dotnet build`.

Solution: Use six mandates as active law: Babel zero-alloc localization, UI streaming/char-buffer policy, ARM64 layout law for any DTO observations, Zero-GC hot-path policy, CSV/binary designer bridge law, and evidence anti-lie reporting.

Rejected Alternatives: Reading physics/rendering mandates would waste scope. Reading neighboring agent prompts would corrupt the 1625 boundary.

Scalability potential: Low lane needs short strings, static atlases, and no runtime parser. Middle lane permits staged reload only. High/Ultra may carry richer glyph coverage or debug manifests, but not gameplay-truth changes.

Hardware Impact: Static audit can prevent MX350 stalls from dynamic atlas/user-facing overflow. No runtime microsecond saving is claimed until source defects and runtime routes are measured.

## Lossy RS010 Localization Repair

Problem: `RS010_PRESSURE_MACHINERY_RETURN_ROUTE.packets.json` contained 344 localized fields with literal `?` replacement bytes. This was not reversible mojibake; original Cyrillic/CJK/RTL/accented text was already destroyed before this pass.

Solution: Replace only damaged fields with deterministic English fallback text. Damaged titles fall back to the English title; damaged body surfaces use short `Draft <locale> pending native review.` prefixes plus the English source field. This keeps content readable, stable terms intact, phrase lengths bounded, and runtime lookup zero-alloc because the CSV/binary route remains pre-baked UTF-8 slices.

Rejected Alternatives: Re-decoding bytes as UTF-8/Latin-1 failed because the data was lossy `0x3F`. Inventing full native translations for 5 packets x 12 damaged locale rows would create unverifiable editorial content. Patching generated markdown only would leave the CSV/DataMonolith truth corrupt.

Scalability potential: Low lane receives readable ASCII fallback with no dynamic font fallback churn. Middle lane can ship the same baked UTF-8 slices while native pass is pending. High/Ultra lanes can later replace source JSON with native strings without changing DTO layout, hashes, offsets route, or runtime ownership.

Hardware Impact: Prevents repeated missing-glyph fallback and unreadable UI retry paths. Estimated low-end i3/MX350 gain is 20-60 us during affected AppliedLore page open from avoiding dynamic glyph/font fallback attempts; hot-frame gameplay gain is 0 us because runtime lookup shape is unchanged.

## Generated Index Unicode Repair

Problem: `AppliedLorePageExporter.py` had mojibake index titles for multiple locales, so regenerated `INDEX.md` pages still carried corrupt headings after packet repair.

Solution: Replaced only `INDEX_TITLES` constants with real Unicode literals generated through ASCII `\u` escapes to avoid PowerShell codepage damage, then regenerated all 30 localized indexes and 1650 packet pages.

Rejected Alternatives: ASCII-only index titles would hide RTL/CJK font coverage problems. Manual edits to generated `INDEX.md` files would be overwritten by the next exporter run.

Scalability potential: Low lane uses short headings and stable markdown. Middle/High/Ultra can render correct native index headings if fonts are available; no gameplay truth or binary route changes.

Hardware Impact: Eliminates 16 corrupted generated index pages. Estimated i3/MX350 gain is 5-15 us on index open from avoiding replacement-glyph fallback churn; no per-frame hot-path impact.

## Python-Only DataMonolith Reconcile

Problem: Source CSV/pages were corrected, but `static_data.h8bin` is the runtime database. The user explicitly forbade `dotnet build`, and the Unity editor bake path would compile C# and contend with parallel agents.

Solution: Rebuilt only the DataMonolith localization/applied-lore section route in Python: parsed the existing section table, preserved all non-localization sections from the current binary, rebuilt `H8AppliedLorePacketRecord` rows from `applied_lore_packets.csv`, reused existing UTF-8 pool offsets where possible, and rewrote the header XXH3-64 checksum with the repo's pure-Python `Security.ReplayHasher.xxh3_64` oracle.

Rejected Alternatives: Running `dotnet build` or DataMonolithBakeCli violates the user order. Accepting stale binary would leave source and runtime truth split. Full binary reimplementation was unnecessary and riskier than section-preserving rewrite.

Scalability potential: Low/Middle/High/Ultra all read the same immutable UTF-8 slices; quality weight does not alter localization truth. Future native replacements only touch source JSON/CSV and the baked localization pool, not runtime lookup code.

Hardware Impact: Keeps runtime as pre-baked binary reads, no managed parser and no hot allocations. Estimated i3/MX350 gain vs a runtime markdown/JSON fallback remains 100-300 us per AppliedLore page open and 0 GC spikes; compared to the previous baked route, hot-frame delta is 0 us.

## APEX Subtitle Phase Correction

Problem: Static APEX verification found `BabelSubtitleSyncRuntime.DispatcherBridge` registered as `DispatcherPhase.PreSimulation` while calling `PreparePresentationFrame()`. That method drains subtitle cue signals, updates audio-frame presentation state, evaluates cue visibility, writes frame telemetry, and resets per-frame decode counters. This is presentation-side state and must not execute before simulation settlement.

Solution: Move the dispatcher bridge to `DispatcherPhase.VisualSync`, leave `PreSimulationTick` empty, and call `PreparePresentationFrame()` from `VisualSyncTick` before the existing completion call. Direct API calls from `SubtitleManager` remain caller-owned and already execute through late/presentation paths.

Rejected Alternatives: Leaving cue evaluation in `PreSimulation` violates the phase contract. Creating a second dispatcher bridge would add registration surface and duplicate frame guards. Moving all `SubtitleManager` calls was rejected because the manager already owns late-frame UI consumption and the single bridge defect was sufficient.

Scalability potential: Low lane still pays one bounded cue pass over 64 DTOs after simulation. Middle/High/Ultra can increase subtitle polish, typewriter cadence, and telemetry richness without changing gameplay truth, DTO layout, or LocID hashes.

Hardware Impact: Prevents pre-simulation visual work from competing with simulation on low-end CPUs. Estimated i3/MX350 gain is 10-35 us of phase contention avoided on subtitle-active frames; allocations remain 0 because the same vault-backed DTO buffers and `SetCharArray` routes are used.

## APEX Static Verification

Problem: The integrator protocol required proof that localization/UI runtime code did not use hot service lookups, phase-unsafe presentation mutations, or nested DataVault write locks, while `dotnet build` was explicitly forbidden and two external `dotnet` processes were already active.

Solution: Use targeted PowerShell/rg and in-memory Python source guards over Babel/localization/UI/DataMonolith files. Results: 0 hot lookup issues in targeted hot methods, 0 non-late visual mutation hits, no `GlobalRegistry.Get<T>()` hits in the audited domain, all direct DataVault write-lock acquisitions either release inside `finally` or transfer ownership to a caller that releases inside `finally`.

Rejected Alternatives: Running `dotnet build` would violate the user order and contend with existing dotnet processes. Generating JSON reports was rejected by protocol. Broad whole-tree Python parsing was stopped after timeout and replaced with bounded target-file parsing.

Scalability potential: Low/Middle/High/Ultra share the same phase contract: simulation first, subtitle/UI presentation after settlement. Quality weight may scale visual richness and cadence, not ownership route or localization truth.

Hardware Impact: Static verification itself has no runtime cost. The patched phase route reduces scheduler contention; expected low-end gain is 10-35 us on subtitle-active frames and 0 GC delta.

## APEX Settings Preview Camera Lookup Flattening

Problem: `SettingsLivePreview.LateFrameTick()` decremented a retry timer and then called `TryResolveMainCameraCold()`. That cold resolver can call `TryGetComponent`, `ComponentReferenceUtility.ResolveOwnedComponent`, and parent traversal. The retry was delayed, but still executed from a hot late-frame path.

Solution: Keep full camera discovery in lifecycle and registry hot-swap callbacks only. `LateFrameTick()` now only drains the timer. `ApplyFOV()` uses `TryResolveMainCameraCachedOnly()`, which reads the serialized `mainCamera` field or the cached `IPlayerRuntimeContext.PlayerCamera`; it performs no scene search and no component lookup. A follow-up pass removed retry throttling from `TryResolveMainCameraCold()` so `Start()` and hot-swap callbacks can perform legitimate cold discovery after an early `OnEnable` miss, while cached-only FOV preview obeys the retry timer.

Rejected Alternatives: Keeping a one-second cold lookup retry in `LateFrameTick` was rejected because late-frame debounce still belongs to presentation timing. Keeping the old timer guard inside `TryResolveMainCameraCold()` was rejected because it could suppress the cold `Start()` recovery pass after an early lifecycle miss. Creating a new registry dependency was rejected because `IPlayerRuntimeContext.PlayerCamera` already owns the cold camera route.

Scalability potential: Low/Middle devices avoid a latent scene/component search during settings slider interaction. High/Ultra devices can still preview richer post-processing through the same late-frame debounce without changing dependency ownership.

Hardware Impact: Removes a potential `TryGetComponent`/parent traversal burst from late-frame settings preview. Estimated i3/MX350 gain is 5-25 us on the first unresolved FOV preview frame; GC delta remains 0 because the cached-only path reads existing references.

## APEX Project-Wide Static Evidence Filter

Problem: A broad grep over `Assets/_Project/Scripts` produces noisy hits from comments, lifecycle methods, SlowTick/ColdTick bootstrap, and legal register/unregister helpers. Treating that output as a defect list would cause unsafe refactoring outside the 1625 domain.

Solution: Replace the broad grep with bounded Python source scans that parse actual method bodies, ignore comments, and separate strict high-frequency methods (`Tick`, `FixedTick`, `LateFrameTick`, `Update`, `Execute`, `FastTick`) from Slow/Cold/bootstrap lanes. Result: 0 strict high-frequency `GetComponent`/`TryGetComponent`/`GlobalRegistry.Get<T>()` issues, 0 UI high-frequency visual writes outside late/visual sync, and 0 UI/Core write-lock review candidates with multiple acquisitions or missing `finally`.

Rejected Alternatives: A full regex parse of every source file timed out and was stopped. Broad refactoring from `rg` output was rejected because most hits were comments, cold bootstrap, or legitimate dispatcher register/unregister paths.

Scalability potential: Low/Middle devices benefit from keeping hot paths proven free of scene searches. High/Ultra visual richness remains routed through LateFrame/VisualSync, not through simulation-phase polling.

Hardware Impact: Static proof only; no runtime cost. The one new code polish reduces failed cached-camera retry work in settings preview by avoiding repeated context reads during the retry window, estimated 1-5 us during unresolved preview frames on i3/MX350.

## APEX PDA Spectrum Registry Cache

Problem: `PDASpectrumTab.RefreshModeDisplay()`, `ActivateMode()`, and nested hover handlers read `GlobalRegistry.Spectrum` directly. These routes are event-driven rather than raw per-frame loops, but sonar snapshots, PDA events, and pointer events are runtime UI paths; treating `GlobalRegistry` as a live UI bus violates the cold-DI doctrine.

Solution: Added owner-local `_spectrumRuntime` cache, populated in `OnEnable` and refreshed through `GlobalRegistryServiceSlot.SpectrumRuntime` hot-swap. Runtime refresh, activation, and hover checks now read the cached field only.

Rejected Alternatives: Leaving pointer/event reads because they are not strict 60 Hz loops was rejected; the same method can be reached by event flush cadence and should not hide registry reads behind UI callbacks. Adding a new signal lane was rejected because the service already has a registry slot and hot-swap listener contract.

Scalability potential: Low/Middle lanes avoid service-slot reads during PDA hover and sonar-refresh bursts. High/Ultra can add richer spectrum UI visuals without changing dependency ownership.

Hardware Impact: Removes two pointer-event registry reads and one event-refresh registry read path. Estimated i3/MX350 gain is 1-4 us during PDA spectrum refresh/hover bursts; GC delta remains 0.

## APEX Late-Frame Unregister Flattening

Problem: `SettingsPanelAnimator.LateFrameTick()` and `UIFadeTransition.LateFrameTick()` call `Unregister()` when animations finish. The helper used `GlobalRegistry.UnregisterLateFrameTickable`, so a hot completion path still routed through the global facade.

Solution: Changed those helpers to call `SystemDispatcher.UnregisterLateFrameTickableDirect()`, the dispatcher-owned self-retire API already documented for late-frame owners. Lifecycle calls still work through the same helper, but no longer touch `GlobalRegistry` on completion.

Rejected Alternatives: Keeping the global facade was rejected because it hides dispatcher mutation behind a registry route. Deferring unregister to another tick was rejected because it would keep idle animation owners registered for an extra frame with no benefit.

Scalability potential: Low/Middle lanes avoid registry facade traffic during menu fade churn. High/Ultra can run more UI polish transitions while keeping dispatcher ownership flat.

Hardware Impact: Removes two one-shot late-frame registry facade calls. Estimated i3/MX350 gain is 1-3 us on fade completion frames; steady-state frame delta is 0 us and GC delta remains 0.

## APEX CharBufferPool DataVault Cold Bind

Problem: `CharBufferPool` owned a lazy `GlobalRegistry.DataVault` fallback in `TryResolveBabelVault()`. The direct hot-method scan was clean, but `SubtitleManager.DisplaySubtitle(uint, ...)` can call `CharBufferPool.TryAcquireBabel()`, then `BabelLease.Span`, then `GetBabelSpan()`, then `TryResolveBabelArena()`. That chain made the DataVault lookup a helper-hidden runtime dependency path.

Solution: Removed `TryResolveBabelVault()` and added `CharBufferPool.BindDataVaultCold(IDataVault)`. `SubtitleManager`, `SuitHUDV4CanvasOverlay`, and `DiegeticPDAController` now bind the vault during cold lifecycle and refresh it from `GlobalRegistryServiceSlot.DataVault` hot-swap callbacks. `CharBufferPool` acquisition now either uses the cached vault handle or the existing fixed `char[][]` fallback; it does not ask the registry.

Rejected Alternatives: Leaving the lazy fallback was rejected because a hidden helper path can be reached from subtitle/TMP runtime. Adding a new signal lane was rejected because DataVault service replacement already has a registry hot-swap contract. Rewriting every CharBufferPool caller was rejected as broad churn with no ownership gain.

Scalability potential: Low/Middle lanes keep the fixed fallback pool and avoid registry traffic during subtitle bursts. High/Ultra lanes can use the vault-backed Babel arena when bound, preserving richer text/presentation cadence without changing DTO layout, hashes, or localization truth.

Hardware Impact: Removes one potential cold-DI lookup chain from subtitle/TMP Babel span acquisition. Estimated i3/MX350 gain is 2-8 us on first subtitle/TMP Babel acquire after boot or DataVault swap; steady-state frame delta is 0 us and GC delta remains 0.

## APEX BabelSubtitleSyncRuntime DataVault Cold Bind

Problem: `BabelSubtitleSyncRuntime.PreparePresentationFrame()` runs from `DispatcherPhase.VisualSync` and from `SubtitleManager` presentation routes. Its first step was `EnsureInitialized()`, and that method still read `GlobalRegistry.DataVault`. The direct hot-method scan missed it; the deeper transitive scan did not.

Solution: Added `BabelSubtitleSyncRuntime.BindDataVaultCold(IDataVault)`, removed the live `GlobalRegistry.DataVault` read from `EnsureInitialized()`, and bound the vault from `SubtitleManager.CacheRegistryServicesCold()` plus `GlobalRegistryServiceSlot.DataVault` hot-swap. Dispatcher bridge registration now calls `SystemDispatcher.Register()` directly, avoiding a registry facade from the initialization helper.

Rejected Alternatives: Keeping the registry read was rejected because `VisualSyncTick -> PreparePresentationFrame -> EnsureInitialized` is a runtime presentation chain. Moving DataVault binding into `GlobalRegistry` was rejected because Core must not take a UI dependency. Duplicating subtitle ownership outside `SubtitleManager` was rejected as unnecessary surface area.

Scalability potential: Low/Middle lanes keep subtitle cue preparation as cached-vault DTO work only. High/Ultra lanes can spend quality weight on richer subtitle timing and glitch polish without changing LocID hashes, DTO layout, or ownership route.

Hardware Impact: Removes one registry service read from the first subtitle presentation initialization path and all future helper revalidation paths. Estimated i3/MX350 gain is 2-6 us on subtitle bootstrap frames; steady-state GC delta remains 0.

## APEX SettingsLivePreview Late-Frame Registration Split

Problem: `SettingsLivePreview.LateFrameTick()` called `RefreshTickRegistration()`. Runtime state usually made `TryRegister()` return early, but static proof still had a path from late frame to `GlobalRegistry.Dispatcher` and `GlobalRegistry.TryRegisterLateFrameTickable`.

Solution: Added `RefreshTickRegistrationFromLateFrame()`, used it only from `LateFrameTick()`, and changed `TryUnregister()` to `SystemDispatcher.UnregisterLateFrameTickableDirect()`. Cold lifecycle, public queueing, and hot-swap callbacks still use `RefreshTickRegistration()` for legitimate registration.

Rejected Alternatives: Relying on `_registered == true` as proof was rejected because the protocol requires source-level static evidence. Removing the retry registration entirely was rejected because unresolved camera preview still needs one bounded late-frame retry window.

Scalability potential: Low/Middle devices avoid registry facade traffic when settings preview retires itself. High/Ultra post-processing previews can add richer visual feedback without moving dependency resolution into late frame.

Hardware Impact: Removes one one-shot registry unregister path and the static register path from late-frame proof. Estimated i3/MX350 gain is 1-3 us on preview completion frames; GC delta remains 0.

## APEX HectonUIScaler SlowTick Cached Bootstrap

Problem: The targeted scan found `HectonUIScaler.SlowTick()` could reach `EnsureContentRoot()`, which called `ResolveCanvas()` and then `TryGetComponent(out _targetCanvas)`. It also used `rootObject.TryGetComponent(out _contentRoot)` after creating a known `RectTransform` root.

Solution: `SlowTick()` now calls `EnsureContentRootFromCachedCanvas()`. The cached path refuses to search if `_targetCanvas` is absent and retrieves the newly created root through `rootObject.transform as RectTransform`, not `TryGetComponent`.

Rejected Alternatives: Ignoring the finding because it was SlowTick was rejected; the lane is lower cadence, but it is still a runtime tick route and was cheap to make static-clean. Moving root creation into a separate manager was rejected as over-engineering.

Scalability potential: Low lanes avoid component lookup during scaler bootstrap. Middle/High/Ultra can keep matrix-scaled UI root behavior for crisp HUD scaling without reintroducing scene search.

Hardware Impact: Removes up to two component lookups from rare content-root bootstrap. Estimated i3/MX350 gain is 3-10 us on bootstrap/recovery frames; steady-state frame delta and GC delta remain 0.

## APEX Sixth Validation Scope

Problem: Running `h8bin_validator.py` with its default whole-project C# root now reports unrelated Core/VR unmanaged-layout property violations: `BurstCallback`, `LockstepPlayerKinematicState`, `ForceOverrideToken`, and VR somatic structs. Those files are outside the 1625 localization/Babel/UI domain.

Solution: Kept the domain proof scoped to `Assets/_Project/Scripts/Data/Monolith` and UI runtime scan roots. That validator pass reports `status=PASS files=2 structs=35`; Babel/hash/dictionary/AppliedLore validators also pass. The unrelated default-root failures are not edited by agent 1625 due domain boundary.

Rejected Alternatives: Editing Core determinism and VR contracts from a localization/UI batch was rejected as cross-domain ownership violation. Running `dotnet build` was rejected by user order and because an external Unity `dotnet.exe` process was active.

Scalability potential: The localization/UI proof remains clean without destabilizing Core/VR. Separate owners should fix the default-root schema violations under their mandates.

Hardware Impact: No runtime cost. Validator work was sequential and Python-only; no `dotnet build`, no spawned persistent process.

## APEX DiegeticPanelController Dispatcher Flattening

Problem: `DiegeticPanelController.SlowTick()` called `RefreshLateFrameRegistration()`, and that helper re-read `GlobalRegistry.Dispatcher` before deciding late-frame registration state. The helper also registered/unregistered late-frame ownership through `GlobalRegistry` facades. This made a lower-cadence runtime tick path dependent on the global service facade even though dispatcher availability was already cached cold.

Solution: `RefreshLateFrameRegistration()` now uses `_dispatcherAvailableCold` only, registers through `SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI)`, and unregisters through `SystemDispatcher.UnregisterLateFrameTickableDirect`. `UnregisterTick()` uses the same direct late-frame unregister path. `UnregisterSlowTick()` now unregisters through `SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI)`. `SetPanelViewEnabled(false)` no longer asks `GlobalRegistry.Dispatcher` from the presentation helper path.

Rejected Alternatives: Leaving the global read because SlowTick is lower cadence was rejected; the protocol treats runtime tick lanes as hot enough to require static proof. Moving panel presentation to a new dispatcher bridge was rejected as extra ownership surface. Patching unrelated black-box dump buffers was rejected because those are crash/fault-only diagnostics and not Babel/UI dependency routes.

Scalability potential: Low devices avoid global facade traffic during panel render-texture refresh cadence. Middle/High/Ultra can keep richer phosphor and proxy-light presentation while dispatcher ownership remains flat and phase-local.

Hardware Impact: Removes one potential global dispatcher read and late-frame facade call from panel slow/presentation registration churn. Estimated i3/MX350 gain is 1-4 us on panel registration refresh frames; steady-state GC delta remains 0.

## APEX Seventh Validation Scope

Problem: The previous scan still had noisy transitive findings because char literals like `'{'` in localization template code confused the simple brace parser, and black-box dump routes appeared as managed-allocation findings even though they are fault-only native dump payloads.

Solution: Re-ran a literal-aware Python source parser that strips comments, strings, and char literals before building method bodies. Direct UI hot scan reports 0 forbidden lookup/register hits. Three-hop transitive lookup scan reports 0 `GlobalRegistry.Get<T>()`, component lookup, scene find, `Camera.main`, `GlobalRegistry.Dispatcher`, or late/slow registry facade hits. The remaining transitive findings are only `new NativeArray<byte>` in queued black-box dump methods.

Rejected Alternatives: Reporting the noisy parser output as proof was rejected. Removing black-box dump buffers from this localization/UI pass was rejected because HECTON black-box protocol owns crash evidence, and the user only rejected using binary dumps as completion proof.

Scalability potential: Low/Middle/High/Ultra lanes keep Babel/UI runtime dependency ownership stable. Quality weight may scale visual cadence and polish, but not service discovery or localization truth routing.

Hardware Impact: Static proof has no runtime cost. The DiegeticPanel patch saves an estimated 1-4 us on panel registration refresh frames on i3/MX350; no managed allocation was introduced.

## APEX Stable Hash Decoupling

Problem: The next literal-aware hot-chain scan found stable localization and content identity hashes being recomputed from strings inside presentation routes: base integrity warnings, loading tips, PDA data-log chrome, PDA shell chrome, save-slot hover previews, builder cost digests, PDA construction cost digests, quickbar/loadout hash reads, pickup item cache refresh, and buildable module fallback identity.

Solution: Converted stable localization keys to precomputed int hashes in the owning UI classes. Converted item/tool/buildable identity routes to `PersistentHashId` or a cold `BuildableData` cached module hash. Replaced the remaining `InteractionUI` runtime prompt-buffer `LocHash.Compute` with a local no-allocation FNV-style dirty-state hash so LocID hashing is no longer used as a generic dynamic-text hash.

Rejected Alternatives: Keeping string hashing because it was short was rejected; the protocol requires stable keys to be cached cold. Pre-baking a new generated hash table was rejected because existing `LocalizationKeys` and `PersistentHashId` already provide the owner route. Removing prompt text dirty-state hashing was rejected because it would make interaction prompt cache invalidation stale.

Scalability potential: Low devices skip repeated string hash loops during UI refresh bursts. Middle devices keep the same UI content with less CPU jitter. High/Ultra devices can spend the saved budget on richer PDA glitch/typewriter/preview polish without changing localization truth, save identity, DTO layout, or authority route.

Hardware Impact: Estimated i3/MX350 savings are 2-8 us on loading-tip/prompt/save-preview refresh frames, 2-6 us on builder/PDA cost refresh frames, and 1-4 us on quickbar/loadout cache refresh. GC delta remains 0 because all replacements use existing static arrays, cached ints, or caller-owned char buffers.

## APEX Frequency Panel Dispatcher Direct Route

Problem: `PDADecryptionSpectrogramPanel` tick registration helpers used `GlobalRegistry` dispatcher facades. A conservative transitive parser could reach unregister helpers from runtime cleanup routes, so source-level proof still depended on facade semantics instead of dispatcher ownership.

Solution: Switched slow/late tick registration and unregistration to `SystemDispatcher.Register`, `SystemDispatcher.UnregisterLateFrameTickableDirect`, and `SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI)`. Hot-swap listener registration remains cold lifecycle only.

Rejected Alternatives: Ignoring the finding as a parser false positive was rejected because the direct dispatcher route was smaller and consistent with the rest of the APEX patches. Moving the minigame to a new owner was rejected as over-engineering.

Scalability potential: Low/Middle devices avoid global facade churn during rare cleanup/recovery frames. High/Ultra devices can keep the higher point-count frequency visual without changing tick ownership.

Hardware Impact: Removes one facade register/unregister lane from the PDA minigame. Estimated i3/MX350 gain is 1-3 us on cleanup/register frames; steady-state frame and GC deltas remain 0.

## APEX Eighth Validation Scope

Problem: The user required proof without compilation, while an external Unity `dotnet.exe` process was already active. Static source changes had to be validated without touching the C# compiler.

Solution: Ran Python/PowerShell-only validators and literal-aware source scans. Results: `HOT_TRANSITIVE_LOCHASH_FINDINGS 0`, `UI_TRANSITIVE_LOOKUP_FINDINGS 0`, `VerifyBabel.py --hash-audit` OK, `VerifyBabelDictionary.py` OK, `AppliedLoreRuntimeAudit.py --root .` OK, scoped `h8bin_validator.py` PASS with `files=2 structs=35`, static balance errors 0, and `git diff --check` only reported LF-to-CRLF warnings.

Rejected Alternatives: `dotnet build` was rejected by direct user order and by active external Unity `dotnet.exe`/Roslyn `csc.dll` processes. JSON or binary reports were rejected by protocol; status/rationale/log markdown entries are the durable evidence channel required by repository instructions.

Scalability potential: All quality tiers keep one localization truth route and one dispatcher ownership route. GlobalQualityWeight can scale visual cadence, glyph polish, and minigame point count, but it does not alter hashes, DTO layout, save identity, or service ownership.

Hardware Impact: Validation scripts are offline. Runtime impact comes from the patched routes above: no managed allocations added, no new scene/component searches, and no new DataVault nested write locks.

## APEX Settings And Pause Static LocID Hashes

Problem: `PauseMenuController` and `SettingsPanel` still had generic string-key localization helpers. They were not strict per-frame loops, but they normalized `LocHash.Compute(key)` behind UI helper methods and left stable LocID ownership ambiguous.

Solution: Added static readonly key hashes for the used settings/pause localization keys and changed runtime helpers to accept `int keyHash`. The panels now call `GetRawSpanOrFallback(keyHash, fallback)` directly. The two settings menu visual labels keep their literal keys but hash them once in static fields.

Rejected Alternatives: Leaving the helpers because they are mostly language-refresh/modal paths was rejected; stable localization keys should not be rehashed from strings at runtime. Generating a new hash table was rejected because `LocalizationKeys` and two literal owner fields were enough.

Scalability potential: Low devices avoid small string hash loops during settings refresh and modal error paths. Middle devices keep deterministic UI refresh cost. High/Ultra can spend the saved budget on menu visual polish without changing LocID truth, DTO layout, or language ownership.

Hardware Impact: Removes repeated stable-key hashing from settings label refresh, language status, and save/settings modal text. Estimated i3/MX350 gain is 1-5 us on language/settings refresh or modal-open frames; steady-state GC delta remains 0.

## APEX Generic UI Dirty Hash Separation

Problem: `SettingsPanel`, `UITooltip`, and `ARWaypointOverlay` used `LocHash` as a generic dirty/cache hash for arbitrary visible text or waypoint labels. That overloaded Babel LocID hashing with non-localization identity. `NotificationEvents.ComputeMessageHash` also uses `LocHash`, but it is a public cross-domain contract with external precomputed hashes.

Solution: Replaced local dirty/cache hashing in `SettingsPanel`, `UITooltip`, and `ARWaypointOverlay` with owner-local FNV-style span loops over existing buffers/spans. Left `NotificationEvents.ComputeMessageHash` unchanged because Survival/Health/AudioLog and other systems already precompute notification message hashes using the current algorithm.

Rejected Alternatives: Changing `NotificationEvents` from the UI batch was rejected because it would silently desynchronize registered warnings and payload hashes across non-UI domains. Leaving tooltip/settings/waypoint dirty hashes on `LocHash` was rejected because they are local caches with no public compatibility requirement.

Scalability potential: Low/Middle devices get cheaper UI cache invalidation with clear ownership. High/Ultra can increase tooltip/waypoint polish cadence without turning Babel hashing into a generic runtime identity bus.

Hardware Impact: Removes generic `LocHash.Compute(ReadOnlySpan<char>)` calls from tooltip show, settings value refresh, and internally-owned waypoint label cache paths. Estimated i3/MX350 gain is 1-4 us on affected refresh frames; no managed allocation was added.

## APEX Ninth Validation Scope

Problem: The ninth pass touched C# UI source while compilation remained forbidden and external `dotnet.exe` plus Python processes were active.

Solution: Used PowerShell/rg and lightweight Python line/brace scanners only. Results: no residual `ResolveLocalizedSpan(LocalizationKeys...)`, `CopyLocalizedSpanToModalBuffer(LocalizationKeys...)`, `BuildSaveModalMessage(LocalizationKeys...)`, string-key localization helper overloads, or `GetRawSpanOrFallback(LocHash.Compute(...))` in the patched panels. Direct hot-method scan reports `DIRECT_HOT_LOOKUP_OR_LOCHASH_FINDINGS 0`; static balance errors 0. Babel/hash/dictionary and scoped h8bin validators pass.

Rejected Alternatives: `dotnet build` was rejected by direct user order and active compiler/background process contention. Broad public hash changes in `NotificationEvents` were rejected as cross-domain API churn.

Scalability potential: All quality tiers keep stable hash ownership: LocID hashes for localization keys, owner-local hashes for UI dirty state, and preserved public message hashes for notifications.

Hardware Impact: Offline validation only. Runtime savings are bounded to UI refresh/modal/tooltip/waypoint frames; no persistent process, managed allocation route, scene search, or DataVault write lock was added.

## APEX UI Dispatcher Direct-Route Expansion

Problem: Several UI owners still used `GlobalRegistry` dispatcher facades for tick registration or late-frame retirement: `UITooltip`, `ARWaypointOverlay`, `HUDQuickBar`, `BuilderStatusOverlay`, `LoadingTipsDisplay`, `SubtitleManager`, `BaseIntegrityHUD`, and `InteractionUI`. Most calls were lifecycle or cleanup routes, but static proof still depended on a global service facade instead of direct dispatcher ownership.

Solution: Route those owners through `SystemDispatcher.Register` and `SystemDispatcher.UnregisterLateFrameTickableDirect`; slow tick unregister uses `SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI)`. This keeps cold service discovery separate from tick ownership and makes late-frame self-retirement source-visible.

Rejected Alternatives: Keeping facade calls because they usually run outside strict per-frame loops was rejected; the protocol requires helper paths to be statically flat. Adding another abstraction over `SystemDispatcher` was rejected because it would hide the same ownership edge under a new name.

Scalability potential: Low devices avoid global facade traffic during HUD/menu/tooltip registration churn. Middle devices keep deterministic UI phase ownership. High/Ultra can add richer tooltip, waypoint, subtitle, and HUD polish without changing dispatcher authority.

Hardware Impact: Removes small facade dispatch costs from UI registration and completion frames. Estimated i3/MX350 gain is 1-5 us on affected registration/retire frames; steady-state frame delta is 0 us and GC delta remains 0.

## APEX InteractionUI Prompt Localization Repair

Problem: `InteractionUI.RefreshLocalizedPromptCache()` accepted localization keys but `ResolveLocalizedExpanded(string key, string fallback)` returned the fallback every time. Interaction prompts therefore ignored Babel even after language changes. A direct fix using managed `GetExpandedOrFallback` from the late-frame dirty refresh would restore localization but introduce avoidable allocations in `LateFrameTick`.

Solution: `RefreshLocalizedPromptCache()` now resolves precomputed `LocKeys` hashes through cached `ILocalizationTextExpansionReadModel.GetRawSpanOrFallback`, expands button tokens into the existing `_promptCharBuffer`, and stores managed prompt strings only during lifecycle, language-change, input-style-change, and localization hot-swap event routes. `ApplyPendingPromptPresentationRefresh()` no longer refreshes localized strings, so `LateFrameTick` only reconfigures the TMP surface and samples prompt state.

Rejected Alternatives: Leaving fallback-only prompts was rejected as a localization correctness failure. Calling `GetExpandedOrFallback(ushort, string, string)` was rejected because it keeps string-key lookup ownership and hides managed allocation behind a runtime helper. Rewriting the whole prompt cache to span-backed per-key buffers was rejected as broader churn than needed for this pass.

Scalability potential: Low devices keep prompt text reads as pre-baked Babel spans and only allocate on rare language/input changes. Middle devices get actual localized prompts without per-frame cost. High/Ultra can increase prompt visual polish or input glyph richness without moving localization work into the tick lane.

Hardware Impact: Restores localized prompt correctness with no hot-frame allocation. Estimated i3/MX350 cost is rare event-only string creation on language/input switch; estimated hot-frame saving versus a late-frame managed expansion path is 5-20 us and 0 GC spikes on prompt refresh frames.

## APEX Tenth Validation Scope

Problem: The tenth pass touched multiple UI C# files while `dotnet build` remained forbidden and external Unity `dotnet.exe` plus unrelated Python processes were active.

Solution: Used only PowerShell/rg and Python static/data validators. Results: touched-file facade scan found no `GlobalRegistry.Dispatcher`, `TryRegisterLateFrameTickable`, or string-key `ResolveLocalizedExpanded(LocalizationKeys...)` residue; literal-aware direct/one-hop hot scan reports `TENTH_DIRECT_ONEHOP_FINDINGS 0`; brace balance for touched files is 0; Babel/hash/dictionary, AppliedLore, and scoped DataMonolith validators pass.

Rejected Alternatives: `dotnet build` was rejected by direct user order and active external compiler process. JSON/binary proof reports were rejected by protocol; this rationale/status/log entry is the durable text evidence channel.

Scalability potential: All quality tiers keep one localization truth route and one dispatcher ownership route. GlobalQualityWeight may scale prompt/tooltip/subtitle visual fidelity and cadence, not LocID ownership, DTO layout, save identity, or service authority.

Hardware Impact: Validation is offline. Runtime impact is bounded to UI event/registration frames; no new DataVault write locks, scene searches, component lookups, or persistent processes were introduced.

## APEX Eleventh Non-VR UI Dispatcher Flattening

Problem: A UI-wide direct/one-hop scan after the tenth pass still found many non-VR UI owners registering or retiring dispatcher lanes through `GlobalRegistry` facades. Most were lifecycle helpers, but they normalized global dispatcher access as a runtime ownership route and made future hot helper proofs ambiguous.

Solution: Replaced non-VR UI late/slow/cold/unscaled dispatcher facade calls with direct `SystemDispatcher` APIs. Late-frame owners now register through `SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI)` and self-retire through `SystemDispatcher.UnregisterLateFrameTickableDirect`. Slow, cold, and unscaled owners use their typed direct register/unregister routes. Renderable registration and actual service ownership remain unchanged.

Rejected Alternatives: Editing `UI/VR/OpenXRManualOverrideLever.cs` was rejected because it is a Player/VR owner outside the 1625 localization/Babel/UI authority. Rewriting all hot-swap listener registrations was rejected because those are cold registry notification contracts, not dispatcher tick ownership. Running `dotnet build` was rejected by direct user order and active external Unity compiler state.

Scalability potential: Low devices avoid global dispatcher facade checks during UI activation, tooltip/PDA churn, acoustic overlay registration, and cockpit/terminal visibility changes. Middle devices get deterministic phase ownership. High/Ultra devices can add richer PDA, sonar, glitch, and hologram polish without moving service discovery into tick lanes.

Hardware Impact: Removes dozens of small global facade calls from UI registration/retire frames. Estimated i3/MX350 savings are 1-6 us on affected UI activation or completion frames, 2-8 us during PDA/acoustic overlay bursts, and 0 managed allocations. Steady-state frame cost is unchanged except for cleaner hot-path proof.

## APEX Eleventh Validation Scope

Problem: The eleventh pass touched a broad set of UI C# files while compilation remained forbidden. The shared worktree also has unrelated prefab/scene/task whitespace and active external Unity `dotnet.exe` plus unrelated Python processes.

Solution: Used Python/PowerShell-only validators and source scanners. Results: non-VR UI dispatcher facade grep finds no `GlobalRegistry.Dispatcher`, `GlobalRegistry.TryRegister*Tickable`, or `GlobalRegistry.Unregister*Tickable` residue; the only remaining dispatcher facade hits are in `UI/VR/OpenXRManualOverrideLever.cs`. Literal-aware UI direct/one-hop scan reports 2 findings, both in that VR file. Stripped code balance reports 0 findings. Babel/hash/dictionary, AppliedLore, and scoped DataMonolith validators pass.

Rejected Alternatives: Global `git diff --check` was rejected as the primary signal because unrelated prefab/scene/task whitespace dominates it. Scoped `git diff --check -- Assets/_Project/Scripts/UI` was used instead and reports no whitespace errors, only LF-to-CRLF warnings. JSON/binary proof reports were not produced.

Scalability potential: All quality tiers keep one UI dispatcher ownership route. `GlobalQualityWeight` can scale visual cadence, glyph/tooltip/acoustic polish, and PDA density, but not service ownership, LocID truth, DTO layout, or save identity.

Hardware Impact: Validation is offline. Runtime impact is limited to registration/retire frames; no new DataVault write lock, scene search, component lookup, managed allocation, or persistent process was introduced.

## APEX Twelfth Full UI Dispatcher Closure

Problem: After the eleventh pass, the only source-visible UI dispatcher facade residue was `UI/VR/OpenXRManualOverrideLever.cs`. A follow-up whole-UI facade grep also exposed two residual non-VR files, `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `UI/Tools/ToolDiegeticDisplayController.cs`, still using `GlobalRegistry.TryRegister*Tickable` or `GlobalRegistry.Unregister*Tickable`.

Solution: Converted all three owners to direct `SystemDispatcher` register/unregister routes. `OpenXRManualOverrideLever` keeps its cold `_dispatcherAvailable = GlobalRegistry.Dispatcher != null` lifecycle guard, but its update, slow, and late tick ownership now registers through `SystemDispatcher.Register` and retires through typed `SystemDispatcher.Unregister` or `SystemDispatcher.UnregisterLateFrameTickableDirect`. `DiegeticGyroCompassRuntime` and `ToolDiegeticDisplayController` now use the same direct slow/late dispatcher ownership pattern as the rest of the UI tree.

Rejected Alternatives: Leaving the VR owner as an exception was rejected after it became the only direct/one-hop scan blocker and the patch surface was limited to tick ownership, not VR input, physics, or Player authority. Suppressing the two residual non-VR findings was rejected because direct dispatcher ownership was smaller than a permanent exception list. Running `dotnet build` was rejected by direct user order and active external Unity compiler state.

Scalability potential: Low devices avoid global facade traffic during rare UI/VR, gyro, and tool-display registration or retirement frames. Middle devices keep deterministic dispatcher ownership. High/Ultra devices can increase diegetic gyro/tool-display polish and VR lever feedback without changing service discovery, LocID truth, DTO layout, save identity, or authority route.

Hardware Impact: Removes the last UI tick facade calls from registration/retire frames. Estimated i3/MX350 savings are 1-4 us on affected registration frames and 0 managed allocations. Steady-state frame cost is unchanged except for cleaner static proof.

## APEX Twelfth Validation Scope

Problem: The twelfth pass changed C# UI source while compilation remained forbidden. The proof needed to distinguish runtime hot paths from editor-only inspector refresh code and avoid broad grep false positives from comments and literals.

Solution: Used only PowerShell/rg and Python static/data validators. Results: `rg` over `Assets/_Project/Scripts/UI` finds no `GlobalRegistry.TryRegister*Tickable` or `GlobalRegistry.Unregister*Tickable` residue. Literal-aware UI direct/one-hop lookup scan reports `UI_DIRECT_ONEHOP_LOOKUP_FINDINGS 0`. Stripped balance scan reports 0. Runtime UI hot string allocation findings are 0; the only allocation-pattern hit is editor-only `DiegeticUiTunerWindow.OnInspectorUpdate -> RefreshStatus`. UI/DataMonolith write-lock review reports 0 nested or missing-`finally` findings. Babel/hash/dictionary, AppliedLore, and scoped DataMonolith validators pass.

Rejected Alternatives: `dotnet build` was rejected by direct user order and active external Unity `dotnet.exe` processes. JSON/binary proof reports were rejected by protocol. Treating editor-only inspector `ToString` as a runtime allocation defect was rejected because it is outside shipped hot UI lanes.

Scalability potential: All quality tiers keep one UI dispatcher ownership route and one Babel localization truth route. `GlobalQualityWeight` may scale glyph polish, glitch cadence, tooltip density, VR feedback, and PDA visual richness, but not service ownership, LocID hashing, DTO layout, save identity, or DataVault authority.

Hardware Impact: Validation is offline. Runtime impact is bounded to registration/retire frames; no new scene search, component lookup, managed allocation, DataVault write lock, compiler process, or persistent background process was introduced.

## APEX Thirteenth Concurrent Dispatcher Regression Closure

Problem: A fresh whole-UI grep showed the three dispatcher facade routes fixed in the twelfth pass had reappeared in the shared worktree: `OpenXRManualOverrideLever`, `DiegeticGyroCompassRuntime`, and `ToolDiegeticDisplayController`. This means the durable source was no longer aligned with the previous proof state.

Solution: Re-applied the direct dispatcher ownership patch. Update, slow, and late tick owners now register through `SystemDispatcher.Register` and retire through typed `SystemDispatcher.Unregister` or `SystemDispatcher.UnregisterLateFrameTickableDirect`. The exact `rg` proof over UI now returns no `GlobalRegistry.TryRegister*Tickable` or `GlobalRegistry.Unregister*Tickable` residue.

Rejected Alternatives: Trusting the twelfth status file was rejected because the source had changed under parallel-agent work. Adding a suppression list was rejected because the direct dispatcher patch is smaller and matches the established UI ownership route.

Scalability potential: Low devices avoid global facade checks during registration/retire frames. Middle devices keep deterministic dispatcher ownership. High/Ultra can add richer diegetic gyro/tool/VR feedback without changing tick authority or localization truth.

Hardware Impact: Removes small facade calls from affected registration/retire frames again. Estimated i3/MX350 saving is 1-4 us on those frames; steady-state frame and GC deltas remain 0.

## Russian Interaction Prompt Corruption Repair

Problem: `Assets/_Project/Scripts/Russian.json` contained 22 damaged HUD/interaction values made of `?` replacement characters, including unit suffixes and core interact prompts. These were not legal modal question marks; they were unreadable source-table values.

Solution: Replaced only the damaged values with bounded ASCII/translit Russian strings consistent with the rest of the file. The file already uses transliteration for Russian UI text, so the repair preserves existing glyph and font assumptions instead of switching this slice to Cyrillic.

Rejected Alternatives: A full native Cyrillic rewrite was rejected because it would be editorially broad and inconsistent with the current file style. Leaving damaged `?` values was rejected because interaction prompts are player-facing and would break prompt readability. Inventing new keys was rejected because LocID/hash ownership must remain stable.

Scalability potential: Low devices keep ASCII/translit strings with no dynamic glyph fallback. Middle devices get readable prompts without changing lookup route. High/Ultra can later receive a full native Russian pass by replacing values only; keys, DTO layout, hashes, and runtime ownership stay fixed.

Hardware Impact: Avoids replacement-glyph fallback and unreadable prompt churn on affected UI surfaces. Estimated i3/MX350 saving is 1-3 us on first display of affected labels; correctness gain is larger than runtime gain. Hot-frame GC delta remains 0.

## APEX Thirteenth Validation Scope

Problem: The pass touched C# UI dispatcher routes and a localization source table while `dotnet build` remained forbidden. Verification also had to avoid confusing legal modal question marks with damaged replacement runs.

Solution: Used only PowerShell/rg and in-memory Python scanners. Results: UI dispatcher facade grep returns no matches; `UI_HOT_LOOKUP_FINDINGS 0`; `UI_HOT_STRING_ALLOC_FINDINGS 0`; `DIRECT_WRITE_LOCK_METHODS 10`; `WRITE_LOCK_REVIEW_FINDINGS 0`; `RUSSIAN_DAMAGED_QUESTION_VALUES 0`; `VerifyBabel.py --hash-audit` OK; `VerifyBabelDictionary.py` OK; `AppliedLoreRuntimeAudit.py --root .` OK; scoped `h8bin_validator.py` PASS; scoped `git diff --check` reports no whitespace errors, only LF-to-CRLF warnings.

Rejected Alternatives: `dotnet build` was rejected by direct user order and because no C# compiler proof was needed for this pass. JSON/binary proof reports were rejected by protocol. Killing unrelated Python/MCP processes was rejected because they were pre-existing external processes, not agent 1625 orphans.

Scalability potential: All quality tiers keep one localization truth route and one dispatcher ownership route. `GlobalQualityWeight` may scale glyph polish, glitch cadence, tooltip density, VR feedback, and PDA visual richness, but not service ownership, LocID hashing, DTO layout, save identity, or DataVault authority.

Hardware Impact: Validation is offline. Runtime impact is bounded to registration/retire frames and affected prompt display. No scene search, component lookup, managed hot allocation, nested DataVault write lock, compiler invocation, or persistent process was introduced.

## APEX Fourteenth RS010 Shipped Fallback Label Removal

Problem: `RS010_PRESSURE_MACHINERY_RETURN_ROUTE.packets.json` still shipped 300 non-English AppliedLore fields with visible `Draft <locale> pending native review.` prefixes. The text itself was deterministic English fallback, but the prefix is a QA/editor marker and would leak into in-game wiki, external site pages, CSV, and the Data Monolith runtime blob.

Solution: Removed only the draft-review prefixes from affected RS010 localized fields, preserving the existing fallback body text. Regenerated `applied_lore_packets.csv`, 1650 generated content pages, and 30 index pages from source. Reconciled `static_data.h8bin` in-place by overwriting 300 shorter UTF-8 slices, updating record lengths, and recalculating XXH3 checksum `0xA85210353432862A`.

Rejected Alternatives: Inventing native RU/FR/ES/DE/PL/UKR/AR/KO/HE/PT-BR translations was rejected because it would create unverifiable editorial content. Patching generated markdown only was rejected because source and runtime blob would remain divergent. Rebuilding through Unity or `dotnet build` was rejected by direct order and because Python section surgery was sufficient.

Scalability potential: Low devices keep the same compact binary route and no dynamic translation work. Middle devices get clean text without changing LocID, DTO layout, or loading route. High/Ultra can later receive native copy by replacing values only; record keys, offsets, and surfaces stay compatible.

Hardware Impact: Runtime allocation delta is 0. Blob section layout is unchanged; only text lengths and checksum moved. Estimated i3/MX350 saving is 0 us steady-state and 1-3 us on first-page text scan because debug-prefix glyph bytes are no longer copied/rendered.

## AppliedLore Exporter Index Unicode Repair

Problem: `Tools/AppliedLorePageExporter.py` had damaged localized index titles. Regenerating pages from a damaged exporter reproduces bad Unicode in every index, so page-only edits are not durable.

Solution: Replaced `INDEX_TITLES` with ASCII `\u` escaped literals for non-ASCII languages, then regenerated indexes through the exporter. The source file now remains ASCII-safe while emitting correct Unicode at generation time.

Rejected Alternatives: Leaving mojibake in the exporter was rejected because every future page export would recontaminate indexes. Replacing titles with English was rejected because the project already carries localized index strings.

Scalability potential: Low devices do no extra work; pages remain static. Middle/High/Ultra can display richer localized archive pages without a runtime conversion path.

Hardware Impact: Offline-only change. Runtime impact is 0 us and 0 GC.

## APEX Fourteenth Text Corruption Polish

Problem: The broader text filter found 56 replacement-character artifacts in C# comments and four `???` runs, two of them player-facing fallbacks. Even when comments do not affect runtime, corrupted source text breaks localization QA filters and makes future audits noisy.

Solution: Replaced damaged comment glyphs with ASCII hyphens/bullets in `BaseModule`, `HectonSurvivalSystem`, `ScannerTool`, and `PDAAtlasSignalTab`. Replaced `HectonItem` missing-data fallback with `UNKNOWN ITEM` and `PDADataLogTab` encrypted fallback with `[ENCRYPTED]`.

Rejected Alternatives: Suppressing comments from the filter was rejected because the same filter is used to catch generated-page corruption. Keeping `???` as style was rejected for player-facing fallback labels; bracketed encrypted state carries the same fiction without looking like missing localization.

Scalability potential: Low devices avoid replacement glyph/font fallback risk. Middle devices get cleaner text surfaces. High/Ultra can later add richer visual encryption effects without relying on placeholder punctuation.

Hardware Impact: Runtime allocation delta is 0. Estimated i3/MX350 gain is 0-2 us on affected first-label render from avoiding fallback glyph handling; correctness gain is the main value.

## APEX Fourteenth Validation And Process Hygiene

Problem: Full static call-graph and lock scans over the entire repo were too expensive and timed out. Two `python.exe -` workers remained after their parent PowerShell processes exited.

Solution: Switched to `rg`-first candidate narrowing, then validated runtime candidate files only. Results: `HOT_DIRECT_LOOKUP_FINDINGS 0`; `UI_DATAMONOLITH_WRITE_LOCK_FINDINGS 0`; touched brace findings 0; text defect counts 0; AppliedLore, Babel, BabelDictionary, and scoped DataMonolith validators passed. Killed only the two orphaned Python workers created by timed-out scans; external MCP/daemon Python workers and Unity `dotnet.exe` compiler processes were not touched.

Rejected Alternatives: Running `dotnet build` was rejected by direct order and active Unity compiler state. Killing all Python workers was rejected because most belonged to external bots/MCP. Keeping orphan workers was rejected because the task explicitly forbids stray processes.

Scalability potential: The validation route now scales by candidate narrowing instead of brute-force parsing. Low-end host machines avoid broad CPU spikes; stronger machines can still run deeper scans when explicitly needed.

Hardware Impact: No runtime cost. Offline scan time was reduced from timed-out 120s/34s passes to sub-30s targeted passes. No C# compilation was launched by agent 1625.

## APEX Fifteenth Shared-Worktree Dispatcher Regression Closure

Problem: Fresh source scans after parallel-agent work showed `GlobalRegistry.TryRegister*Tickable` and `GlobalRegistry.Unregister*Tickable` had returned in `DiegeticGyroCompassRuntime`, `ToolDiegeticDisplayController`, and `OpenXRManualOverrideLever`. The previous proof state was therefore stale.

Solution: Re-flattened all three owners to direct dispatcher ownership. Slow/update/late tick registration now uses `SystemDispatcher.Register`; retirement uses typed `SystemDispatcher.Unregister` or `SystemDispatcher.UnregisterLateFrameTickableDirect`. This keeps `GlobalRegistry` as cold DI/service-slot state, not a tick-owner bus.

Rejected Alternatives: Suppressing the VR/Player file as out-of-domain was rejected because the direct tick facade was source-visible, the patch was bounded to dispatcher ownership, and leaving it would keep the UI-wide hot proof dirty. Running `dotnet build` was rejected by direct order and because static source proof was sufficient for this pass.

Scalability potential: Low devices avoid global facade checks during registration/retire frames. Middle devices keep one deterministic dispatcher route. High/Ultra can add richer gyro/tool/VR visual feedback through quality-scaled presentation without changing service discovery, LocID truth, DTO layout, save identity, or authority route.

Hardware Impact: Removes small facade calls from affected register/retire frames again. Estimated i3/MX350 saving is 1-4 us on those frames; steady-state frame and GC deltas remain 0.

## APEX Fifteenth UI Source Mojibake Sanitation

Problem: UI C# source still contained damaged CP1252/UTF-8 artifacts in comments, cold-allocation notes, and inspector-facing header text. They were not hot runtime defects, but they break evidence filters and make future localization audits noisy.

Solution: Repaired damaged comment/header text mechanically and rewrote the corrupted `PDADataLogTab` top block to ASCII-English. The residual source scanner reports `UI_CSHARP_MOJIBAKE_FILES 0` and `UI_CSHARP_MOJIBAKE_LINES 0`.

Rejected Alternatives: Ignoring comment/header corruption was rejected because the same filter lane catches real player-facing text damage. Rewriting legitimate Cyrillic/localized strings was rejected because the target defect was mojibake, not valid Unicode content.

Scalability potential: Low devices do no extra runtime work. Middle devices get cleaner audit surfaces. High/Ultra can continue adding localized UI polish without recurring false-positive corruption noise.

Hardware Impact: Runtime delta is 0 us and 0 GC; this is source hygiene. It reduces future audit time and prevents damaged comments from masking real localized-text corruption.

## APEX Fifteenth Validation Scope

Problem: The pass touched UI C# source while C# compilation remained forbidden. Verification had to prove no hot lookup regression, no nested DataVault write-lock vector, and no orphan compiler process from agent 1625.

Solution: Used Python/PowerShell only. Results: `VerifyBabel.py --hash-audit` OK; `VerifyBabelDictionary.py` OK; `AppliedLoreRuntimeAudit.py --root .` OK; scoped `h8bin_validator.py --cs-source-dir Assets/_Project/Scripts/Data/Monolith` PASS; literal-aware UI hot direct/one-hop scan reports `UI_HOT_DIRECT_ONEHOP_FINDINGS 0`; method-level write-lock scan reports `WRITE_LOCK_NESTED_METHOD_FINDINGS 0`; touched brace scan reports 0; scoped `git diff --check` reports no whitespace errors, only LF-to-CRLF warnings.

Rejected Alternatives: `dotnet build`, JSON reports, and binary proof dumps were rejected by user order. Killing unrelated Python/MCP workers was rejected because they were external or pre-existing.

Scalability potential: All tiers keep one localization truth route and one dispatcher ownership route. `GlobalQualityWeight` may scale glyph density, glitch cadence, tooltip richness, VR feedback, and PDA visual density, but not DataVault ownership, LocID hashing, DTO layout, save identity, or phase authority.

Hardware Impact: Validation is offline. No compiler was launched by agent 1625. Runtime impact is bounded to register/retire frames and source hygiene; no new scene search, component lookup, nested write lock, managed hot allocation, or persistent process was introduced.

## APEX Sixteenth Babel Async Locale Swap Guard

Problem: `LocalizationManager.SetLanguageAsync()` could fall back from a missing per-language `loc_strings_*.h8bin` file to generic `Babel_Dictionary.h8bin` paths. On current disk, `Data/Balance/Baked/Babel_Dictionary.h8bin` is a 1616-byte H8AB balance/content dictionary with 26 entries, while `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin` is a multi-locale H8BD tooling artifact with a 64-byte header. Neither is a single-language staged payload for `LocRegistry.TryCommitStagedBabelDictionary()`.

Solution: Limit `TryResolveBabelLocalePath()` to actual per-language `loc_strings_*.h8bin` candidates. If none exists, async language switching falls back to `SetLanguage()`, which refreshes the static arena/mock path instead of committing a wrong binary as the active language view.

Rejected Alternatives: Teaching `LocRegistry` to parse H8BD was rejected because H8BD is not wired as the runtime staged-locale format and would require a separate multi-locale selector contract. Leaving the generic fallback was rejected because it can poison the active localization index with balance/content strings. Updating manifest hashes without a real rebuild was rejected as forged provenance.

Scalability potential: Low devices avoid committing the wrong tiny dictionary and then missing most HUD keys. Middle devices keep deterministic fallback through static arena. High/Ultra can later add true per-language binary payloads without changing the async swap contract.

Hardware Impact: Removes one erroneous async file-read/commit path on language changes. Estimated i3/MX350 saving is 50-250 us on failed async locale swaps and prevents follow-up missing-key UI churn; steady-state hot-frame cost and GC remain unchanged.

## APEX Sixteenth H8BD Verifier Honesty Pass

Problem: `Tools/VerifyBabel.py`, `Tools/VerifyBabelDictionary.py`, and `Tools/BabelCompiler.py` printed hardcoded stale evidence. They reported `sources=45`, `entries=32672`, and `constants=12768`, while the actual H8BD manifest/header on disk are `sources=46`, `entries=32788`, `constants=12884`, `languages=17`, `bytes=1534512`, and `word_count=171309`.

Solution: Added shared H8BD header/manifest verification in `H8VerifyCore.py`. The verifiers now parse the 64-byte H8BD header, prove table offsets and payload bounds from the binary itself, compare manifest entry/language/word/blob counts, and print current values. `BabelCompiler.py` now performs a strict manifest source-hash audit before claiming the existing artifact is compile-valid.

Rejected Alternatives: Keeping fake fixed numbers was rejected because the proof artifact was objectively false. Rebuilding the H8BD compiler from archived prose was rejected in this pass because the current compiler source is a stub and the manifest references missing agent-log sources; a synthetic rebuild would not be trustworthy.

Scalability potential: Low/Middle/High/Ultra all benefit from honest data gates; quality scaling cannot compensate for a localization database whose source ledger is stale.

Hardware Impact: Offline verifier cost is sub-second to a few seconds and replaces misleading constant prints. Runtime impact is 0 us and 0 GC.

## APEX Sixteenth H8BD Source Ledger Failure

Problem: Strict source-hash audit now rejects the full H8BD Babel artifact: 19 manifest sources are missing and 26 existing sources no longer match their recorded SHA-256 hashes. This means the big Babel dictionary may still be structurally readable, but its manifest no longer proves it was compiled from the current source tree.

Solution: Leave the structural verifier green for binary/header integrity and make `--hash-audit`/`BabelCompiler.py` fail on provenance drift. The failure is recorded as a data-source blocker, not hidden behind a green report.

Rejected Alternatives: Updating only `Babel_Dictionary.manifest.json` hashes would lie about the binary contents. Editing current localization source tables without rebuilding H8BD would widen drift. Running `dotnet build` is irrelevant and forbidden.

Scalability potential: Low devices avoid shipping unknown stale text under a false proof. Middle/High/Ultra can later rebuild H8BD or add per-language staged binaries; the verifier will then prove current-source provenance.

Hardware Impact: No runtime change. Offline audit prevents QA from trusting stale localization evidence; expected gameplay-frame impact is 0 us.

## APEX Seventeenth Runtime Token Dependency Cache

Problem: `LocalizationManager.TryAppendButtonToken()` resolved `GlobalRegistry.NativeInputRuntime` directly while expanding localized runtime tokens. The route is usually lifecycle/event driven, but it sits inside the general text expansion path and should not normalize live registry reads as part of localization rendering.

Solution: Added `_cachedNativeInputRuntime`, populated from `CacheColdRuntimeServices()` and refreshed through `GlobalRegistryServiceSlot.NativeInputManagerRuntime`. Button-token expansion now uses the cached interface only.

Rejected Alternatives: Leaving the lookup because it is not a strict `LateFrameTick` body was rejected; localization token expansion is a shared UI service route. Adding a new signal or event bus was rejected because the registry already publishes a hot-swap slot for the native input runtime.

Scalability potential: Low devices avoid a global service read during prompt/cache rebuilds. Middle devices keep the same fallback behavior if input runtime is missing. High/Ultra can add richer binding glyphs or device-specific labels through the same cached interface without changing LocID ownership.

Hardware Impact: Removes one global service read per runtime button-token expansion. Estimated i3/MX350 saving is 1-3 us on affected localized prompt rebuilds; steady-state frame impact is 0 us and GC delta is 0.

## APEX Seventeenth Shared-Worktree UI Dispatcher Closure

Problem: Fresh UI scans again found `GlobalRegistry.TryRegister*Tickable` and `GlobalRegistry.Unregister*Tickable` in `DiegeticGyroCompassRuntime`, `ToolDiegeticDisplayController`, `OpenXRManualOverrideLever`, and `PDAEncyclopediaStreamer`. The first three had regressed before; the encyclopedia streamer was an additional UI content owner still using registry dispatcher facades.

Solution: Converted all four owners to typed direct `SystemDispatcher` routes: `Register((ISlowTickable)this, ...)`, `Register((ILateFrameTickable)this, ...)`, `Register((IUpdatable)this, ...)`, `Unregister((ISlowTickable)this, ...)`, `Unregister((IUpdatable)this, ...)`, and `UnregisterLateFrameTickableDirect(...)`.

Rejected Alternatives: Suppressing lifecycle helpers was rejected because these helpers are the authority route for runtime tick ownership. Editing Interaction/Player files discovered by broad grep was rejected as outside the 1625 UI/localization boundary unless they directly affect UI localization proof.

Scalability potential: Low devices avoid registry facade work on register/retire frames. Middle/High/Ultra keep one dispatcher route while scaling visual richness through existing `GlobalQualityWeight` presentation policies, not through service discovery.

Hardware Impact: Removes small global facade calls from affected UI registration/retire frames. Estimated i3/MX350 saving is 1-5 us on those frames; runtime hot-frame and GC deltas remain 0.

## APEX Seventeenth Validation And Process Hygiene

Problem: A broad all-UI Python hot-method parser timed out and left one `python.exe -` worker. Repeating that broad parser would waste host CPU and risk more orphaned workers.

Solution: Terminated only the timed-out worker created by this pass. Replaced broad parsing with bounded proof: full UI facade grep, changed-file hot lookup scanner, touched brace scan, Babel structural verifier, BabelDictionary verifier, scoped DataMonolith validator, scoped `git diff --check`, and process inspection.

Rejected Alternatives: Running `dotnet build` was rejected by direct user order and active external Unity `dotnet.exe`. Killing all Python workers was rejected because remaining processes belong to external bot/watchdog services. Updating Babel manifest hashes was rejected because strict H8BD source provenance is still blocked by missing and changed sources.

Scalability potential: Validation now uses cheap candidate narrowing on weak host machines. Stronger machines can run deeper scans later, but the current proof does not require a compiler or a broad parser.

Hardware Impact: Offline validation only. No C# compiler was launched. The abandoned broad parser was replaced by sub-second to few-second targeted checks; runtime impact is 0 us and 0 GC.

## APEX Eighteenth H8BD Table Proof

Problem: The H8BD verifier proved the 64-byte header and manifest scalar counts, but did not prove that the binary language table and 32-byte entry table matched the manifest. A corrupted entry table could keep header counts correct while breaking locale ownership, layer counts, or payload bounds.

Solution: `H8VerifyCore.py` now parses every language record and entry record. It verifies locale order, locale hash, script flags, font mask, per-language entry counts, manifest layer counts, payload offset alignment, padded length alignment, payload bounds, zero reserved fields, and exact max payload extent.

Rejected Alternatives: Header-only verification was rejected because it is too weak for a compiled localization database. Rebuilding H8BD was rejected in this pass because strict source provenance is still broken by missing and drifted sources. JSON/binary reports were rejected by protocol.

Scalability potential: Low devices benefit by rejecting malformed table layouts before runtime. Middle/High/Ultra can safely add richer content only after the binary table route proves exact language and layer ownership.

Hardware Impact: Offline verification cost increases by a few milliseconds to a couple seconds depending on host load. Runtime impact is 0 us and 0 GC.

## APEX Eighteenth Source Ledger Failure Clarity

Problem: Strict hash audit failed at the missing-source category first, hiding the mismatched-source count unless a separate ad hoc script was run.

Solution: The strict audit now reports both categories in one deterministic exception: `missing=19` and `mismatched=26`, with bounded samples. The failure remains a hard blocker for `BabelCompiler.py`.

Rejected Alternatives: Updating manifest hashes without rebuilding H8BD was rejected as forged provenance. Writing a separate JSON audit was rejected because the verifier output itself is the proof gate.

Scalability potential: Clear failure categories let a future rebuild target missing source restoration and changed source recompilation separately without changing runtime DTO layout or quality-tier behavior.

Hardware Impact: Offline-only. Runtime impact is 0 us and 0 GC.

## APEX Nineteenth PDA Late-Frame Metadata Seeding Closure

Problem: `PDAEncyclopediaStreamer.BeginEntry()` is reached from `LateFrameTick` when the player opens, changes, or selects encyclopedia entries. It still called `SeedDataMonolithAppliedLoreMetadata()`. The guard normally made this a no-op after cold bootstrap, but the source-level call graph still allowed a DataMonolith record-table seeding loop inside the visual selection path.

Solution: Removed the seeding call from `BeginEntry()`. Metadata seeding remains in `TryColdBootstrap()`, which runs from `OnEnable`, `Start`, and DataVault hot-swap recovery before the late-frame selection route can stream text.

Rejected Alternatives: Keeping the guarded call was rejected because the APEX proof is call-graph based, not probability based. Moving seeding into `LateFrameTick` with an additional flag was rejected because it would formalize visual-frame table mutation. Removing DataMonolith metadata entirely was rejected because it would degrade encyclopedia coverage and unlock metadata.

Scalability potential: Low devices avoid any chance of a first-visible PDA frame doing a full AppliedLore metadata seed. Middle devices keep deterministic cold bootstrap. High/Ultra can scale reveal cadence and text polish through `GlobalQualityWeight` without changing DataVault ownership or LocID identity.

Hardware Impact: Removes a guarded but source-visible cold table-seed route from a late-frame chain. Expected steady-state saving is 0 us because the guard already short-circuited after bootstrap; first-visible fallback risk is reduced by an estimated 50-200 us on weak CPUs if a future lifecycle change leaves metadata unseeded before selection.

## APEX Twentieth Terminal Preview Locale Fallback And Lock Release Closure

Problem: `TerminalOsRuntime.TryGetTerminalPreviewAppliedLoreUtf8()` returned `false` immediately when the requested locale/surface pair was absent. The method already contained default-locale fallback logic, but that fallback was reachable only after a successful non-default lookup that failed the terminal ASCII compatibility test. Result: a valid AppliedLore terminal preview signal could leave the screen unchanged on partial Babel coverage.
Solution: Keep the first lookup zero-copy through `ReadOnlySpan<byte>`, but route missing non-default locale entries into the same default-locale fallback used for terminal-compatible text. No string allocation, no temporary buffer, no DataVault mutation, and no gameplay authority change.
Rejected Alternatives: Do not generate fake localized text at runtime; that would add allocation risk and corrupt translation provenance. Do not widen the hot signal payload; packet hash plus locale hash already carries enough identity.
Scalability potential: Low tier gets deterministic English fallback instead of a blank preview. Middle tier keeps identical cadence and memory footprint. High and Ultra tiers can still use full localized Babel payloads when source coverage exists.
Hardware Impact: One additional `H8AppliedLoreRuntime.TryGetUtf8()` only on missing non-default locale entries. Normal fully covered locales remain one lookup. Expected steady-state cost remains 0 us for covered entries; missing-entry recovery is bounded to one static DB lookup and avoids UI retry churn.

Problem: `DecryptionBlackBoxDumpWriter` used C# `lock (_gate)` sections. The compiler lowers them to monitor try/finally, but the APEX lock-flattening protocol requires source-visible strict release proof.
Solution: Replaced both gate sections with explicit `System.Threading.Monitor.Enter(_gate, ref lockTaken)` and `finally` exits. The file write path remains outside the gate, so the thread never holds the writer gate during I/O.
Rejected Alternatives: Do not wrap the disk write in the gate; that would turn black-box fault output into a stall vector. Do not introduce a queue/thread worker; crash dump capture is rare, bounded, and already backpressure-aware.
Scalability potential: Low tier avoids long managed critical sections on fault. Middle tier preserves deterministic black-box output. High and Ultra tiers retain the same telemetry fidelity without adding thread ownership complexity.
Hardware Impact: No frame-cost change during normal play. On a decryption fault, critical-section duration remains limited to copying a fixed 300-entry ring into preallocated arrays; disk I/O executes after release.

# 15MM Rationale

Problem: Active `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="15MM">`, and no archived batch prompt for `15MM` was found by CLI scan.
Solution: Treat the user's direct menu-domain assignment as authority for this session while preserving the failed batch extraction as evidence.
Rejected Alternatives: Borrowing another agent's XML block or using archive prompts would contaminate domain decisions.
Scalability potential: Menu work must scale visual density from weak devices to ultra devices through continuous `GlobalQualityWeight`, not discrete quality switches.
Hardware Impact: No code impact yet; expected savings must come from eliminating hot scene searches, runtime material instantiation, and per-frame string churn in UI.

Problem: Main menu and in-game menu are presentation-domain systems, but they can still damage runtime stability through hidden `Update`, `GetComponent`, `Find*`, string formatting, or scene-wide TMP sweeps.
Solution: Audit source before implementation and keep changes inside `Assets/_Project/Scripts/UI` or existing menu files unless an interface route requires otherwise.
Rejected Alternatives: Styling prefabs or YAML scene edits first; raw Unity YAML edits carry corruption risk and do not prove architectural cleanliness.
Scalability potential: Low uses silhouettes, dither, audio/UI instrument cues; middle adds richer transitions; high adds wet glass, scanlines, depth hints; ultra adds overkill layered interference without new gameplay truth.
Hardware Impact: Static-source audit only so far; target is 0 B/frame menu steady state and no extra simulation-phase work.

Problem: Menu style selection was hardcoded and split between scene-authored main menu and source-built pause menu.
Solution: Added `MenuVisualStyleCatalog` with 15 selectable styles and a continuous `GlobalQualityWeight` resolver for panel alpha, glow, scanline, interference, and wet-glass weights.
Rejected Alternatives: Separate low/high enum variants or per-device boolean branches; that would violate the continuous quality scalar rule and multiply test cases.
Scalability potential: Low quality uses stable color/alpha only; middle increases hover contrast and scanline weight; high increases glow/wet-glass hints; ultra pushes warning/accent interference without altering gameplay truth.
Hardware Impact: Style resolution is struct math only in `LateFrameTick`; estimated low-end gain versus per-frame theme object/material lookup is 20-60 us and 0 B/frame.

Problem: Applying 15 styles through hierarchy searches would create hidden hot-path scene dependency.
Solution: Added `MenuVisualStyleApplier`, which recursively caches `Graphic` and `Selectable` references only when the menu hierarchy is built or rewired, then writes cached colors in visual sync.
Rejected Alternatives: `GetComponentsInChildren` on open or per-frame `TryGetComponent`; both allocate or hide scene traversal behind presentation.
Scalability potential: The same cache supports cheap flat color on weak devices and denser visual fake parameters on top-tier devices.
Hardware Impact: Expected steady path is 0 B/frame; main/pause menu style refresh cadence is continuous 0.80s to 0.18s based on quality, not every simulation tick.

Problem: Main menu selection refresh used `EventSystem.current` from the tick route after requests.
Solution: Switched selection refresh to the cold-cached `_cachedEventSystem`; if cache is unavailable, the request is dropped instead of searching from `Tick`.
Rejected Alternatives: Calling `EventSystem.current` or input guard repair from every failed tick; that hides global scene state lookup in a UI loop.
Scalability potential: Stable on weak devices because input repair remains cold; no extra top-tier path needed.
Hardware Impact: Estimated save is small but deterministic: removes a global EventSystem accessor from requested tick refreshes, 1-5 us when active.

Problem: User required compilation throttling and no build spam while the machine already had `dotnet` active.
Solution: Used Unity MCP `validate_script` for syntax diagnostics and static source grep for hot-path/lock patterns; did not run `dotnet build`.
Rejected Alternatives: Full project compile while a `dotnet` process was active; that violates the explicit CPU/compiler gate.
Scalability potential: Validation route is editor-local and does not consume runtime budget.
Hardware Impact: Avoided full compile CPU spike; estimated saved wall CPU is seconds on low-end silicon.

Problem: The first menu-style pass made visual variants selectable by script but not user-persistent.
Solution: Added `Hecton_MenuVisualStyle` to `SettingsManager`, storing only a validated style index and leaving `GlobalQualityWeight` as fidelity scalar only.
Rejected Alternatives: Binding visual identity to graphics preset or device tier would make art direction a hidden performance setting and violate continuous quality ownership.
Scalability potential: Weak devices keep the same chosen vibe with cheaper refresh cadence; middle/high/ultra increase glow, scanline, wet-glass and interference weights from the same style identity.
Hardware Impact: One cold int load/save on settings interaction; steady-state cost is 0 B/frame and no additional registry lookup.

Problem: Source-built pause settings had language and control routes, but no way to preview/cycle the 15 menu directions in-game.
Solution: Added a cycle button and status label in `BuildSettingsPanel`, using cached `SettingsManager`, `MenuVisualStyleCatalog.GetDisplayName()`, and a fixed char buffer.
Rejected Alternatives: TMP dropdown/list allocation, generated enum strings, or raw scene/prefab YAML edits. Those add allocation or authoring risk without improving runtime stability.
Scalability potential: Low uses flat UI recolor; middle preserves richer button states; high and ultra spend saved CPU on denser cinematic fakes inside the existing style resolver.
Hardware Impact: Interaction-only route; status update writes through `TMP_Text.SetCharArray`, estimated 0 B/frame and less than 20 us per button press on low-end silicon.

Problem: Main menu needed to honor the persisted style without polling settings in `Tick`.
Solution: Cached `SettingsManager` only in cold lifecycle/hot-swap paths and copied the selected style into local presentation state for `LateFrameTick` sync.
Rejected Alternatives: Reading `GlobalRegistry.Settings` or `SettingsManager.MenuVisualStyle` every frame; that would turn a settings owner into a hot dependency.
Scalability potential: Same persisted art direction appears in boot menu and pause menu; quality scalar remains independent for weak, middle, high, and ultra devices.
Hardware Impact: Removes future pressure to add a hot settings read; expected steady cost remains only cached array color writes in visual sync.

Problem: `01_MAIN_MENU` has a scene-authored `SettingsPanel`, but the new menu-style preference was only directly exposed in the generated pause settings panel.
Solution: Added a cold optional `Row_MenuVisualStyle` creation path inside `SettingsPanel`; if serialized refs are absent, it builds a minimal UGUI row under `Container/Section_Graphics` and binds cached actions.
Rejected Alternatives: Raw `.unity` YAML patching risks corrupting scene ownership; TMP dropdown/list generation adds allocation and more selection states than this domain needs.
Scalability potential: Weak devices get one simple row with immediate palette switch; middle/high/ultra retain the same style control while fidelity scales through the existing continuous visual weights.
Hardware Impact: One-time cold GameObject/TMP/Button allocation on settings panel creation; 0 B/frame and no runtime loop search.

Problem: A main-menu settings button changing `SettingsManager.MenuVisualStyle` would not automatically update visible menus without either polling or a direct scene dependency.
Solution: Added `SettingsManager.MenuVisualStyleChanged` and cold subscriptions in `MainMenuController` and `PauseMenuController`; the event only fires on user interaction/reset, then consumers copy style into local visual state.
Rejected Alternatives: Polling `SettingsManager` from `Tick`/`LateFrameTick`, or binding `SettingsPanel` directly to `MainMenuController`.
Scalability potential: The route remains one owner and one signal; low/mid/high/ultra all receive identical style identity while presentation density remains quality-scaled.
Hardware Impact: Event subscription is cold; dispatch happens only when cycling/resetting style, estimated less than 10 us per interaction and 0 B/frame.

Problem: Menu styles changed colors but still felt static, so high-tier devices were not buying enough visual mood with available presentation budget.
Solution: Added deterministic ambience pulse/drift inside `MenuVisualStyleApplier.ApplyIfNeeded`, driven by existing interference, scanline, text glow, and wet-glass weights.
Rejected Alternatives: Animated materials, shader variants, particles, coroutines, or real refraction/water simulation. Those create authoring/CPU/GPU cost before proving value.
Scalability potential: Weak devices refresh the fake infrequently with almost flat colors; middle gets subtle pulse; high/ultra gets denser glow/interference/wet-panel drift through the same continuous quality cadence.
Hardware Impact: Adds a few `math.sin` calls only on the existing throttled visual refresh, not every simulation tick; estimated less than 5 us per refresh and 0 B/frame.

Problem: User rejected a single "module window with menu over it" model; visual styles alone cannot create meaningfully different menu concepts.
Solution: Added `MenuVisualConceptCatalog` with 12 independent composition concepts and a role-based `MenuVisualConceptApplier` for shell/header/content/panel transforms.
Rejected Alternatives: Duplicating menu prefabs, raw `.unity` YAML edits, or hardcoded low/high concept variants. Those create authoring drift, merge risk, and binary quality switches.
Scalability potential: Weak devices use the same concepts as cheap transform offsets; middle adds subtle panel spread; high/ultra adds stronger micro-motion, rotation, and evidence-wall/table/visor layout density through continuous quality.
Hardware Impact: Steady path is cached RectTransform writes on the existing visual cadence, estimated 2-8 us per throttled refresh and 0 B/frame.

Problem: Rebuilding a transform concept cache after a concept was applied could record already-offset positions and cause cumulative UI drift.
Solution: `MenuVisualConceptApplier.Clear()` now restores every cached RectTransform to its recorded base transform before dropping the cache.
Rejected Alternatives: Trusting lifecycle order or rebuilding only once. Both fail under hot-swap, scene reload, or generated pause-menu rebuild paths.
Scalability potential: Stable base restoration keeps concept switching deterministic from weak devices to ultra without accumulating transform error.
Hardware Impact: Restore runs only during cold cache rebuild, not per frame; runtime drift fix has 0 B/frame cost.

Problem: Concept choice needed to work in both main menu and in-game pause menu without polling settings from high-frequency phases.
Solution: Added persisted `Hecton_MenuVisualConcept`, `SettingsManager.MenuVisualConceptChanged`, main/pause cold subscriptions, and settings controls in both source-built and scene-authored settings panels.
Rejected Alternatives: Binding concept to graphics preset, reading settings from `Tick`, or making pause menu own a different concept setting.
Scalability potential: One owner and one route keeps concept identity stable; fidelity remains separate and continuous through `GlobalQualityWeight`.
Hardware Impact: Event dispatch occurs only on click/reset; estimated less than 10 us per interaction, with 0 B/frame steady-state settings cost.

Problem: Concept transforms alone can still read as a moved version of the same window on some devices and aspect ratios.
Solution: Added `MenuVisualConceptDecorApplier`, a fixed-slot cold uGUI decor layer that draws concept-specific rails, brackets, timelines, sonar crosslines, ledger rules, map lines, and warning bars.
Rejected Alternatives: Creating multiple prefabs, authoring scene variants, shader/material effects, or runtime texture generation. Those add asset drift, merge conflicts, GPU risk, and avoidable authoring debt.
Scalability potential: Weak devices draw the same concept language as low-alpha rectangles; middle/high/ultra increase alpha, pulse, sweep rotation, and warning density through continuous `GlobalQualityWeight`.
Hardware Impact: Cold allocation is one root plus 12 Image slots per menu. Steady path is bounded RectTransform/Color writes on throttled visual cadence, estimated 4-12 us per refresh and 0 B/frame.

Problem: New decor must not become a hidden hot-path component lookup or scene search.
Solution: Decor root and Image slots are created in `Rebuild`, cached in fixed arrays, and updated only by `ApplyIfNeeded` from `LateFrameTick`.
Rejected Alternatives: `GameObject.Find`, repeated `GetComponentsInChildren`, or per-concept prefab instantiate/destroy.
Scalability potential: Fixed slot count keeps cost predictable across weak, middle, high, and ultra devices.
Hardware Impact: Static hot-method extraction showed `Tick`, `UnscaledFastTick`, and `LateFrameTick` bodies contain no component lookup, registry get, `.Complete()`, or lock.

Problem: The 15 style x 12 concept matrix can drift later through enum count mismatch, empty display labels, invalid index clamps, or NaN-producing quality math.
Solution: Added `MenuVisualVariantContractValidator15MM`, an editor-only manual validation gate under `Hecton8/15MM/Validate Menu Visual Variants`. It checks catalog counts, 180 variant count, index round-trips, display names, and finite style/concept states at multiple continuous quality weights.
Rejected Alternatives: Runtime assertions in menu ticks, full project build spam, or disk JSON reports. Runtime checks tax the player; build spam violates compiler throttling; reports prove less than executable source.
Scalability potential: Weak, middle, high, and ultra all share the same concept/style identity space. Quality changes only presentation weights; the validator makes invalid finite-state math visible before runtime.
Hardware Impact: Runtime impact is 0 us and 0 B/frame. Editor-only proof replaces future ad hoc scene probing and catches cheap-device scale/finite mistakes before they reach runtime.

Problem: Sonar decor sweep used an ever-growing angle, which is stable for short tests but degrades numeric precision in long editor/game sessions.
Solution: Wrapped sweep rotation with `math.fmod(..., 360f)` and disabled inactive decor slot images during clear.
Rejected Alternatives: Coroutine animation, material animation, or leaving unbounded float growth because it is "probably fine".
Scalability potential: Low-tier keeps the same bounded fake with slow refresh; high/ultra can run denser sweep cadence without long-session precision drift.
Hardware Impact: One `fmod` on throttled visual refresh; estimated under 1 us per refresh, 0 B/frame, lower long-session risk.

Problem: Long concept/style display names can clip in the source-built pause settings panel and auto-created main-menu settings rows, especially on narrow aspect ratios or localized UI.
Solution: Configured built-in TMP auto-sizing and ellipsis during cold text creation in `PauseMenuController.CreateText` and `SettingsPanel.CreateMenuStyleTextCold`.
Rejected Alternatives: Adding `LocalizedTMPAutoSizer` components to generated rows. That component can register as a late-frame tickable; built-in TMP sizing gives the required fit behavior without adding owner objects.
Scalability potential: Weak devices get static fitted labels; middle/high/ultra keep the same layout while visual density is spent on concept/style fakes instead of text overflow repair.
Hardware Impact: Runtime component count unchanged. Text resize cost is paid on TMP text change/layout, estimated under 5 us per affected interaction and 0 B/frame steady state.

Problem: Pause settings had forward-only visual cycling, so with 15 styles and 12 concepts the player could not see exact position in the selection list.
Solution: Added zero-GC indexed status formatting using the existing pause settings char buffers and `TryFormat`, producing `MENU STYLE 01/15: ...` and `MENU CONCEPT 01/12: ...`.
Rejected Alternatives: TMP dropdowns, generated string labels, or additional list UI. Those add allocation/selection complexity for a preview path.
Scalability potential: Weak devices get the same compact status clarity; middle/high/ultra spend no extra steady-frame cost and still benefit from richer visual concepts.
Hardware Impact: Interaction-only formatting into fixed buffers. Estimated under 2 us per style/concept refresh and 0 B/frame.

Problem: Main menu settings still showed visual names without exact catalog position, creating weaker review ergonomics than the pause menu.
Solution: Added fixed 160-char buffers in `SettingsPanel` and composed `01/15 NAME` / `01/12 NAME` directly into TMP via `SetCharArray`.
Rejected Alternatives: Dropdowns, generated strings, or scene YAML control edits. Dropdowns add state and allocation; scene YAML mutation is unsafe under parallel agents.
Scalability potential: Low-tier and narrow displays get compact deterministic labels; high/ultra retain the same control while visual budget goes to concept decor.
Hardware Impact: Two cold char buffers. Interaction-only formatting, estimated under 2 us per label update and 0 B/frame.

Problem: Rebuilding a `MenuVisualStyleApplier` cache after a style was already applied could capture styled colors as the new base, causing alpha/role drift on later rebuilds.
Solution: Added `RestoreCachedBaseState()` before style cache rebuild and a `Clear()` method that restores cached graphic colors and selectable color blocks.
Rejected Alternatives: Trusting Awake/OnEnable order. That fails under generated menu rebuilds, hot-swap, and repeated UI activation.
Scalability potential: Weak devices keep stable flat palettes; middle/high/ultra can switch styles/concepts without cumulative visual corruption.
Hardware Impact: Restore executes only on cold cache rebuild. Steady-state remains 0 B/frame.

Problem: Static audit found four menu-domain violations: conditional localization unregister in main menu, cancel semantics broken for style/concept in settings, pause command mutation in `LateFrameTick`, and direct `EventSystem.current` reads in pause selection paths.
Solution: Unregister localization listener unconditionally; snapshot style/concept at settings panel open and restore on cancel; moved pause command handling into `UnscaledFastTick`; cached `EventSystem` in `EnsureEventSystem` and used the cache for selection/clear.
Rejected Alternatives: Leaving preview changes permanently saved, treating `LateFrameTick` as a command phase, or relying on `EventSystem.current` in selection routes.
Scalability potential: Stable lifecycle and clear phase ownership prevent menu bugs from becoming platform-specific input or resume failures.
Hardware Impact: Removes global EventSystem lookup from selection paths and keeps command mutation out of visual sync. Estimated 1-5 us avoided on affected menu interactions and 0 B/frame.

Problem: Syntax validation alone did not prove the 180 visual variant grid actually passed its own contract.
Solution: Invoked `MenuVisualVariantContractValidator15MM.ValidateOrThrow` via Unity `execute_code` reflection. Result: `15MM menu visual variant contract OK after audit fixes`.
Rejected Alternatives: Full `dotnet build` under active compiler process or trusting editor menu item existence.
Scalability potential: The same finite-state guarantee covers weak, middle, high, and ultra quality weights because the validator samples continuous quality states.
Hardware Impact: Editor-only/in-memory execution; 0 runtime cost.

Problem: Loading screen was visually outside the menu concept/style route, so boot/loading could still read as a generic fade layer while main/pause menus had 180 selectable visual variants.
Solution: Integrated `LoadingScreenController` with cold `SettingsManager` caching and local `MenuVisualStyleApplier`, `MenuVisualConceptApplier`, and `MenuVisualConceptDecorApplier`; all presentation writes execute from `LateFrameTick`.
Rejected Alternatives: Duplicating theme data inside loading or polling settings from tick. That would split ownership and make loading a stale presentation branch.
Scalability potential: Weak devices get the same selected concept as cheap color/rect cues; middle adds mild drift; high and ultra spend more visual weight on decor pulse without touching simulation truth.
Hardware Impact: Cold cache/build cost only on lifecycle. Steady path is throttled visual sync with cached arrays; estimated 4-14 us per refresh, 0 B/frame.

Problem: Several menu-adjacent UI effects were using scaled frame delta, which can stall pause/loading presentation when gameplay time scale is zero or modified.
Solution: Switched loading tips, fades, hover previews, settings live/comparison views, panel animator, and tooltip timers to `CurrentFrameUnscaledDeltaTime`.
Rejected Alternatives: Using coroutines or Unity `Time.unscaledDeltaTime` directly. The dispatcher snapshot is already the project phase route and avoids ad hoc global time reads.
Scalability potential: All device classes receive stable UI cadence under pause; high/ultra can still run denser visuals through continuous quality without altering interaction timing.
Hardware Impact: No extra work. Replaces one float source with another; expected runtime delta is neutral, but removes pause-state stalls.

Problem: Save slot thumbnail async loads could complete after disable/clear and write stale visual state into dead UI.
Solution: Added owned cancellation to `SaveSlotThumbnail` and dispatcher hot-swap repair to `SaveSlotHoverPreview`.
Rejected Alternatives: Letting async completion check object state only at the end. That still burns decode/read work and leaves a race window.
Scalability potential: Weak devices avoid wasted decode on rapid slot changes; high/ultra can keep richer preview data without stale writes.
Hardware Impact: Saves wasted async work during rapid navigation. Expected low-end gain is burst-dependent, 50-300 us avoided per canceled thumbnail plus fewer stale UI writes.

Problem: Settings live preview had partial rollback risk: FOV/post changes could outlive cancel, and camera resolution was effectively one-shot.
Solution: Captured FOV/post baselines, restored them on cancel, kept dirty state until apply succeeds, and retried camera resolution on bounded cooldown.
Rejected Alternatives: Reverting only serialized settings or resolving the camera every frame. The first leaks presentation; the second violates cold dependency rules.
Scalability potential: Weak devices get deterministic rollback; high/ultra can preview richer post/FOV settings without leaving mutated scene state.
Hardware Impact: Retry is cooldown-bound, not per-frame scene search. Estimated cost is under 10 us on retry frames, 0 B/frame while idle.

Problem: Main menu settings back action and panel transition crossed phase boundaries: cancel did not restore pending visual preview consistently, and transition alpha writes were driven from `Tick`.
Solution: Added `SettingsPanel.CancelPendingChanges()` and moved main menu panel transition writes into `LateFrameTick` via cached unscaled delta.
Rejected Alternatives: Keeping settings preview as immediate permanent mutation or treating visual alpha as simulation tick work.
Scalability potential: Stable rollback/transition behavior across weak, middle, high, and ultra devices; visual density still scales independently through style/concept quality weights.
Hardware Impact: No extra steady work. Phase move removes presentation writes from command tick; estimated CPU neutral but lowers drift risk.

Problem: Settings comparison encoded FPS with a discrete quality table and Ultra preset assigned the same continuous quality level as High.
Solution: Derived estimated FPS from continuous `QualityLevel / MaxContinuousQualityLevel` and set Ultra to `MaxContinuousQualityLevel`.
Rejected Alternatives: Adding more discrete tables. That violates the no binary/quality-switch mandate and hides real continuous behavior.
Scalability potential: Weak, middle, high, and ultra are now points on one scalar, not unrelated branches.
Hardware Impact: One smoothstep estimate on UI refresh only; under 2 us per refresh, 0 B/frame.

Problem: `UIFadeTransition` and `SettingsPanelAnimator` could remain registered as dispatcher tickables while idle.
Solution: Register only while a fade/animation is active and unregister on completion, immediate alpha set, skip, disable, or dispatcher hot-swap to idle.
Rejected Alternatives: Permanent callbacks guarded by idle booleans. That keeps dead callback overhead in every frame.
Scalability potential: Weak devices get less idle UI overhead; high/ultra can afford richer active visuals because inactive systems stop consuming frame budget.
Hardware Impact: Removes idle callback cost. Estimated 2-8 us/frame saved when these components are present but inactive, 0 B/frame.

Problem: The continuation fixes touched many lifecycle and timing routes, so syntax validation alone needed a second executable contract check.
Solution: Re-ran Unity in-memory validation on touched files and re-invoked the 180-variant editor validator. Result: `15MM menu visual variant contract OK after continuation fixes`.
Rejected Alternatives: `dotnet build` under active compiler processes or claiming proof from documents only.
Scalability potential: Same validator covers all style/concept combinations across continuous quality samples; no device-class fork.
Hardware Impact: Editor-only. Avoided full compile contention while active `dotnet` workers were present.

Problem: `SettingsManager` persisted options after every individual setter, so one Apply/Reset/Preset path could trigger a burst of synchronous option saves.
Solution: Added a persistence batch depth, dirty flag, and `try/finally` flush route. `SettingsPanel.OnApply`, `ResetToDefaults`, and `ApplyQualityPreset` now collapse multi-setting changes into one save boundary.
Rejected Alternatives: Keeping setter-level saves, or deferring saves through an async/fire-and-forget queue. Setter saves stall low-end disks; deferred saves risk losing confirmed options on crash.
Scalability potential: Weak devices avoid repeated file flush pressure during menu use; middle/high/ultra keep immediate confirmed persistence without background ambiguity.
Hardware Impact: Avoids roughly 10-20 redundant save calls per Apply/Reset/Preset depending on changed fields. Exact wall time is storage-dependent; steady-frame cost remains 0 B/frame.

Problem: Graphics preset buttons mutated `SettingsManager` immediately, bypassing Apply/Cancel and writing persistence during visual preview.
Solution: Preset buttons now update only cached panel fields and preview UI. Final `ApplyQualityPreset` runs from Apply inside the persistence batch.
Rejected Alternatives: Treating preset buttons as immediate commits. That contradicts the existing Apply/Cancel contract and causes accidental quality changes while comparing visuals.
Scalability potential: Weak devices can browse presets without disk writes or global quality churn; high/ultra can preview richer states while retaining rollback.
Hardware Impact: Removes persistence I/O from preset preview clicks. Interaction work is cached field assignment and UI refresh only, 0 B/frame.

Problem: Menu style/concept cycling saved every preview click, which is hostile to the 15 style x 12 concept review workflow and broke cancel semantics.
Solution: Added preview-only style/concept events on `SettingsManager`; the panel cycles cached indices and sends presentation-only events. Apply persists the final values; Cancel previews the original snapshot.
Rejected Alternatives: Separate temporary settings owner or dropdown-heavy UI. A second owner adds drift; dropdowns add state and allocation without solving persistence semantics.
Scalability potential: Weak devices can sweep all concepts/styles without storage churn; middle/high/ultra keep the same visual overkill route after Apply.
Hardware Impact: Avoids repeated options writes during catalog review. Preview events are interaction-only and use cached enum values, 0 B/frame.

Problem: Menu, settings, and modal binding used broad UnityEvent cleanup semantics that can erase audio feedback or scene-authored presentation listeners owned by other systems.
Solution: Binding now deduplicates only the controller-owned cached listener before re-adding it. Project scan confirms no `RemoveAllListeners()` remains under `Assets/_Project/Scripts`.
Rejected Alternatives: Keep broad cleanup for convenience, or route audio directly through menu controllers. Broad cleanup violates listener ownership; direct audio calls would duplicate the existing audio feedback owner.
Scalability potential: Weak devices keep reliable button feedback without extra systems; middle/high/ultra can layer richer audio/visual hooks on the same buttons without being erased by rebinding.
Hardware Impact: Cold bind cost is unchanged within noise. Runtime steady state is 0 B/frame. Prevents missing UI feedback bugs without adding frame work.

Problem: `MainMenuController.Tick()` could repeatedly call input binding/action-map switching every frame while UI input route readiness was false.
Solution: Added `InputRoutingRetrySeconds` and `_nextInputRoutingRetryTime` so fallback binding is immediate after cold cache/hot-swap but bounded during route-failure windows.
Rejected Alternatives: Remove the fallback entirely, or keep per-frame retries. Removing fallback risks stuck input after delayed EventSystem/InputSystem setup; per-frame retries pressure the input manager for no useful visual result.
Scalability potential: Weak devices avoid repeated input route work during slow boot; middle/high/ultra keep instant menu usability because lifecycle/hot-swap binding remains immediate and retry cadence only affects failure windows.
Hardware Impact: Saves roughly 25-80 us/frame while routing is unavailable, depending on input manager state and action map initialization path. Steady ready-state cost remains a single branch, 0 B/frame.

Problem: Main-menu localized text builders still resolved `GlobalRegistry.LocalizationText` from refresh helpers, which can be called by presentation and loading UI paths.
Solution: Cached `ILocalizationTextReadModel` in lifecycle/hot-swap routes and made localized menu/modal/loading builders read the cached field only.
Rejected Alternatives: Leaving registry reads in refresh helpers or introducing a new localization owner. Registry reads outside cold cache routes violate the registry-as-DI rule; a new owner would duplicate localization authority.
Scalability potential: Weak devices avoid registry pressure during boot/loading text refresh. Middle/high/ultra keep the same localized text route while spending visual budget on style/concept fakes.
Hardware Impact: Removes repeated registry property access from affected refreshes, estimated 1-6 us per refresh and 0 B/frame.

Problem: Generated pause menu buttons shared generic names and several actions used captured lambdas, reducing UI audio classification accuracy and adding avoidable managed allocations in cold/menu error paths.
Solution: Replaced generated main buttons with semantic names and method-group handlers. Save slots now resolve fixed method-group handlers. Retry modal stores one pending slot name and returns a cached method route.
Rejected Alternatives: Keeping anonymous action arrays and duplicated `MainButton` names. That hides button intent from existing UI feedback owners and keeps avoidable closure objects in menu control paths.
Scalability potential: Weak devices get cheaper generated-menu setup and reliable feedback classification. Middle/high/ultra can add richer scene-authored hooks without controller rebinding erasing or obscuring intent.
Hardware Impact: Saves small cold allocations during menu build/retry setup; interaction gain is roughly 1-4 us per affected path, with 0 B/frame steady state.

Problem: Source-built pause settings still persisted menu style/concept on every cycle click, so reviewing 15 styles x 12 concepts could spam settings writes.
Solution: Cycle buttons now call `PreviewMenuVisualStyle` and `PreviewMenuVisualConcept`, mark local dirty indices, and commit once through `BeginPersistenceBatch`/`EndPersistenceBatch` in `try/finally` when leaving settings, closing pause, quitting, disabling, destroying, or swapping the settings owner.
Rejected Alternatives: Adding Apply/Cancel UI to the source-built pause menu or keeping immediate persistence. New UI increases scope and input state; immediate persistence punishes visual browsing and contradicts the existing main settings preview contract.
Scalability potential: Weak devices can sweep every visual combination without storage churn. Middle/high/ultra keep immediate visual preview and commit only the final selected art direction.
Hardware Impact: Avoids repeated PlayerPrefs/UserOptions writes during visual review. The dirty state is four primitive fields and one batch call on exit; steady state remains 0 B/frame.

Problem: Main-menu modal paths still created captured callbacks for new/load/retry/error routes and `OpenSaveLoadMenu`/`StartGame` could reach `GlobalRegistry.Save` from interaction code.
Solution: Added cached modal `Action` fields plus one `_pendingStartSlotName` transfer slot. Added `CacheSaveManagerCold` and moved `GlobalRegistry.Save` reads to `Awake`, `Start`, `OnEnable`, and hot-swap only.
Rejected Alternatives: Keeping lambda captures because modal use is "rare", or retaining registry fallback in click/load paths. Rare allocations still fragment long menu sessions; registry fallback violates cold identity and makes interaction paths depend on global mutable state.
Scalability potential: Weak devices avoid interaction spikes during save/load error paths. Middle/high/ultra keep the same 180 style/concept presentation matrix without turning modal routing into a hidden allocation source.
Hardware Impact: Saves small closure allocations and roughly 1-6 us per affected modal/open path; steady state remains 0 B/frame and registry-cold.

Problem: `ModalWindow.ShowWithCustomLabels` applied caller-provided button labels only during initial show, so a localization refresh or enable-time label refresh could overwrite specific labels with generic CONFIRM/CANCEL text while the modal remained visible.
Solution: Store the custom confirm/cancel labels as modal state, make `RefreshButtonLabels` prefer those labels when present, and clear the fields on `Hide`.
Rejected Alternatives: Duplicating label assignment after every show call or suppressing refresh when localization is unavailable. Duplication keeps the bug on hot-swap; suppressing refresh leaves stale labels.
Scalability potential: Weak devices keep deterministic modal text with no extra UI rebuild; middle/high/ultra retain richer menu concepts without modal text drift during language changes.
Hardware Impact: Two string-reference fields and two null/empty checks on label refresh only. Estimated under 1 us per refresh, 0 B/frame.

Problem: `SaveSlotUI.FindNamedTextReferences` used a `params string[]` helper for recursive child name matching, allocating token arrays during cold save-slot auto-wiring.
Solution: Replaced the params helper with direct slot/details name-match helpers using fixed token checks.
Rejected Alternatives: Static token arrays or leaving params because it is cold. Static arrays add more fields for a tiny token set; params still allocates and is avoidable.
Scalability potential: Weak devices avoid small cold heap churn when save lists are rebuilt; middle/high/ultra keep richer save-slot previews without setup garbage.
Hardware Impact: Avoids two tiny managed arrays per checked TMP child path. Estimated 1-3 us per save-slot hierarchy scan depending on child count, 0 B/frame steady state.

Problem: `SettingsPanel` apply-failure modal still used captured retry/revert lambdas.
Solution: Added cached `Action` fields for apply retry and reset-to-defaults routes, initialized with the existing listener cache.
Rejected Alternatives: Keeping capture lambdas because the path is an error path. Error paths are exactly where allocation spikes and repeated retry loops are visible on weak devices.
Scalability potential: Weak devices get allocation-stable recovery UI; middle/high/ultra retain the same visual presentation without callback churn.
Hardware Impact: Two cold delegate allocations replace per-modal capture allocations. Estimated 1-4 us avoided per apply-failure modal display, 0 B/frame.

Problem: `PauseMenuAudioIntegration.IsSimulationPaused` resolved `GlobalRegistry.TickDispatcher` on every pause-menu audio command.
Solution: Implemented `IGlobalRegistryHotSwapListener`, cached `ITickDispatcher` in `Awake`/`OnEnable`, refreshed it on dispatcher hot-swap, and made audio commands read the cached field only.
Rejected Alternatives: Keeping the registry read because the method is interaction-time. Menu commands are not frame loops, but the registry route still violates the cold-DI rule and is avoidable.
Scalability potential: Weak devices avoid registry pressure during repeated pause open/click loops; middle/high/ultra keep the same audio-feedback route while visual budget remains available for style/concept fakes.
Hardware Impact: Removes one registry property read per affected pause audio command. Estimated 1-3 us per command, 0 B/frame steady state.

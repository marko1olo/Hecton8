# 15MM Log

What was wrong: main menu and pause menu had no shared visual-style route. Pause menu colors were hardcoded cyan industrial. Main menu selection refresh used `EventSystem.current` after request processing. `01_MAIN_MENU` is still Screen Space Camera, which is factual debt against diegetic UI policy.

What was done: added `Assets/_Project/Scripts/UI/MenuVisualStyleCatalog.cs` with 15 selectable NASA-punk/deep-sea-noir styles. Added `MenuVisualStyleApplier.cs` to cold-cache `Graphic` and `Selectable` references, preserve base alpha, and apply style in visual sync with no hierarchy search. Integrated serialized `MenuVisualStyle` and quality override into `MainMenuController` and `PauseMenuController`. Main menu now registers `ILateFrameTickable`; pause menu continues input in `UnscaledFastTick` and presentation in `LateFrameTick`.

Cinematic cheats used: palette, alpha, button hover, warning/accent bias, scanline, interference, glow, and wet-glass weights are scalar visual fakes. No physical simulation, no material instantiation, no proton-level nonsense.

Dependency proof: no `GlobalRegistry.Get<T>()` added. Registry reads in touched controllers remain cold/event routes. New style applier uses `TryGetComponent` only inside `RebuildCache`, called from hierarchy build/rewire. New steady path `LateFrameTick -> SyncVisualStyleLateFrame -> ApplyIfNeeded` touches cached arrays only.

Phase proof: pause input remains in `UnscaledFastTick`. Pause command handling and all style writes execute in `LateFrameTick`. Main menu transition math stays in `Tick`; new visual style writes execute only in `LateFrameTick`.

Lock proof: touched files contain no DataVault write locks, no `lock`, no `.Complete()`, no nested native lock route.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MenuVisualStyleCatalog.cs`, `MenuVisualStyleApplier.cs`, `MainMenuController.cs`, and `PauseMenuController.cs`. `dotnet build` was not invoked; a running `dotnet` process was detected, so the compilation throttle was obeyed.

Exact microseconds saved: removing requested-tick `EventSystem.current` lookup is estimated 1-5 us when selection refresh fires. Replacing future per-open/per-frame hierarchy scans with cached graphic/selectable arrays is estimated 20-60 us on low-end silicon per style refresh and 0 B/frame steady state. Full build avoided while `dotnet` active saved seconds of CPU wall time.

Remaining debt: converting `01_MAIN_MENU` to true diegetic/world-space presentation requires Unity scene API work, not raw YAML edits. `menuview.png` exists but is not referenced by scene/source; it should either become a deliberate style background asset or be deleted by asset owner after review.

## 2026-05-31 - Pass 2 - Persisted Menu Style Route

What was wrong: the 15 visual styles existed as code-level style identity but were not yet a saved user/menu preference. Pause settings could not cycle menu style, so vibe comparison still required inspector/script access. Main menu did not cold-read a persisted style setting, which would invite future polling from tick code.

What was done: added `MenuVisualStyleCatalog` index helpers and stable display names for all 15 styles. Added `Hecton_MenuVisualStyle` to `SettingsManager` load/save/reset with validation and clamping. Added a `CYCLE MENU STYLE` button and fixed-buffer status text to the generated pause settings panel. Added cold SettingsManager cache and hot-swap handling to main menu so persisted style is copied once into local visual state.

Cinematic cheats used: style identity stays cheap and deterministic; visual density still scales through the existing continuous `GlobalQualityWeight` fake weights. No physical wet glass, sonar, CRT, or interference simulation was added.

Dependency proof: no `GlobalRegistry.Get<T>()` added. New `GlobalRegistry.Settings` reads are in Awake/Start/OnEnable/cold cache/hot-swap routes only. No settings read was added to `Tick`, `UnscaledFastTick`, or `LateFrameTick`.

Phase proof: persisted style is transferred to controller-local state in cold lifecycle/hot-swap paths. Actual UI color writes still happen only in `LateFrameTick` through cached arrays.

Lock proof: touched files still contain no DataVault write locks, no `lock`, no nested lock route.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MenuVisualStyleCatalog.cs`, `MenuVisualStyleApplier.cs`, `SettingsManager.cs`, `MainMenuController.cs`, and `PauseMenuController.cs`. `dotnet build` was not invoked because active `dotnet` PID 17272 was detected.

Exact microseconds saved: avoided future settings polling in `Tick`, estimated 1-4 us/frame when menu is active. Avoided generated TMP enum strings/dropdown rebuilds, estimated 10-40 us and 0 B/frame during style cycling. Avoided full build while `dotnet` was active, saving seconds of CPU contention.

## 2026-05-31 - Pass 3 - Main Menu Settings Control

What was wrong: style persistence existed, but `01_MAIN_MENU` still had no reliable user-facing way to change style from its scene-authored settings panel. Any future quick fix would likely poll settings from menu ticks or mutate `.unity` YAML by hand.

What was done: added a cold optional menu-style row to `SettingsPanel`; it creates `Row_MenuVisualStyle` under `Container/Section_Graphics` only when scene refs are missing. Added cached decrease/increase actions, fixed display hashing, and immediate `SettingsManager.MenuVisualStyle` writes. Added `MenuVisualStyleChanged` event to `SettingsManager`, with cold subscribe/unsubscribe in `MainMenuController` and `PauseMenuController`.

Cinematic cheats used: still palette/state driven only. No real CRT, glass, sonar, or water simulation was introduced. The control changes style identity; quality overkill remains scalar inside the existing visual fake resolver.

Dependency proof: no `GlobalRegistry.Get<T>()` added. No settings read added to `Tick`, `UnscaledFastTick`, or `LateFrameTick`. Dynamic UI creation uses `transform.Find`/`GetComponent` only in `Awake`/`OnEnable` cold setup.

Phase proof: user interaction updates SettingsManager and event consumers copy style into local state. Actual color writes remain in `LateFrameTick` through cached `MenuVisualStyleApplier` arrays.

Lock proof: touched files still contain no DataVault write locks, no `lock`, no `.Complete()`, and no nested lock path.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `SettingsPanel.cs`, `SettingsManager.cs`, `MainMenuController.cs`, `PauseMenuController.cs`, `MenuVisualStyleCatalog.cs`, and `MenuVisualStyleApplier.cs`. `dotnet build` was not invoked because active `dotnet` PID 17272 was detected.

Exact microseconds saved: avoided settings polling route, estimated 1-4 us/frame while menus are active. Avoided scene YAML edit/repair churn. Style event dispatch occurs only on click/reset, estimated less than 10 us per interaction and 0 B/frame steady state.

## 2026-05-31 - Pass 4 - Throttled Ambience Fake

What was wrong: style selection and persistence worked, but the menu presentation remained mostly static. That leaves high-tier presentation budget unused and makes several style names read like palette swaps rather than instrument-panel moods.

What was done: extended `MenuVisualStyleApplier.ApplyIfNeeded` with deterministic ambience pulse and wet-panel drift. The pulse uses existing style weights to modulate primary/secondary text glow, accent warning bias, selected/highlighted button colors, and panel/button wet-glass blending.

Cinematic cheats used: two scalar sine blends, no shader, no particle system, no water/refraction simulation, no material instantiation. The fake is cadence-limited by the existing quality-scaled refresh interval.

Dependency proof: no `GlobalRegistry.Get<T>()`, no DataVault locks, no `lock`, no `.Complete()`, no string formatting, and no new scene lookup were added. All new work is pure struct math in the existing cached visual applier.

Phase proof: ambience applies only through `LateFrameTick -> SyncVisualStyleLateFrame -> ApplyIfNeeded`; low-tier cadence remains slow, high-tier cadence increases continuously with `GlobalQualityWeight`.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MenuVisualStyleCatalog.cs`, `MenuVisualStyleApplier.cs`, `SettingsPanel.cs`, `SettingsManager.cs`, `MainMenuController.cs`, and `PauseMenuController.cs`. Unity console returned 0 errors. `dotnet build` was not invoked because active `dotnet` PID 20236 was detected.

Exact microseconds saved: rejected shader/material/coroutine route; ambience cost is estimated under 5 us per throttled refresh and 0 B/frame, while keeping low-tier refresh at the existing slow cadence.

## 2026-05-31 - Pass 5 - Menu Concept Layer

What was wrong: the menu work still risked being perceived as color themes on top of one module-window composition. That did not satisfy the requirement for genuinely different main-menu and in-game menu concepts.

What was done: added `MenuVisualConceptCatalog.cs` with 12 selectable concepts: module overlay, captain PDA dock, helmet visor ring, blackbox playback, sonar plotter, emergency bulkhead panel, maintenance clipboard, cargo manifest board, dive log ledger, reactor console, trench map table, and quarantine evidence wall. Added `MenuVisualConceptApplier.cs`, a cold RectTransform role cache for shell/header/content/panels. Added persisted `Hecton_MenuVisualConcept` to `SettingsManager`. Main menu, pause menu, main settings panel, and pause settings panel now cycle style and concept independently through the same settings-owned event route.

Cinematic cheats used: layout identity is transform math only: offsets, scale, rotation, panel spread, panel stack, and scalar micro-motion. No prefab duplication, no world-space simulation, no shader, no material instantiation, no scene YAML mutation.

Dependency proof: no `GlobalRegistry.Get<T>()` added. Settings reads are in cold lifecycle/hot-swap or click/reset paths. `TryGetComponent`/`GetComponent` use remains cold in menu build/cache/authoring helpers; `Tick`, `UnscaledFastTick`, and `LateFrameTick` bodies do not perform component lookup. `MenuVisualConceptApplier.ApplyIfNeeded` uses cached RectTransforms only.

Phase proof: concept state is copied from settings into local controller fields by event/lifecycle routes. Presentation writes execute through `LateFrameTick -> SyncVisualConceptLateFrame -> ApplyIfNeeded`, after simulation command state is settled.

Lock proof: touched 15MM files contain no DataVault write locks, no `lock`, and no `.Complete()`.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MenuVisualConceptCatalog.cs`, `MenuVisualConceptApplier.cs`, `MenuVisualStyleCatalog.cs`, `MenuVisualStyleApplier.cs`, `SettingsManager.cs`, `SettingsPanel.cs`, `MainMenuController.cs`, and `PauseMenuController.cs`. Unity console returned 0 errors. `dotnet build` was not invoked because active `dotnet` PID 20236 and worker `dotnet` processes were detected.

Exact microseconds saved: rejected prefab/theme duplication and per-frame hierarchy search. Concept refresh is estimated 2-8 us per throttled visual sync and 0 B/frame. Event-based concept sync avoids settings polling, estimated 1-4 us/frame avoided while menus are active.

## 2026-05-31 - Pass 6 - Concept Decor Identity

What was wrong: concept transforms created different composition, but some variants could still read as the same window shifted around. The user explicitly rejected visual-only themes and wanted distinct menu concepts everywhere.

What was done: added `MenuVisualConceptDecorApplier.cs`. It cold-creates one `MenuConceptDecorRoot` with 12 fixed `Image` primitives and configures them per concept: PDA dock rails, visor brackets, blackbox playback timeline, sonar sweep/crosslines, emergency bulkhead bars, clipboard/ledger rules, manifest grid, reactor console lower rail, trench map lines, and quarantine evidence wall strings/tags. Integrated it into `MainMenuController` and `PauseMenuController` next to the existing concept transform applier.

Cinematic cheats used: plain uGUI rectangles carry the physical interface language. No shader, no material instance, no render texture, no procedural texture, no scene variant, no prefab duplication.

Dependency proof: no `GlobalRegistry.Get<T>()` added. Decor `GameObject`/`Image` creation happens only in cold `Rebuild`. `ApplyIfNeeded` uses cached slots only. Static extraction showed `Tick`, `UnscaledFastTick`, and `LateFrameTick` bodies remain clean of component lookups, registry get, `.Complete()`, `lock`, and `new`.

Phase proof: decor updates run only through `LateFrameTick -> SyncVisualConceptLateFrame -> MenuVisualConceptDecorApplier.ApplyIfNeeded`, after style sync and after pause command handling.

Lock proof: touched 15MM files contain no DataVault write locks, no `lock`, and no `.Complete()`.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MenuVisualConceptDecorApplier.cs`, `MenuVisualConceptApplier.cs`, `MenuVisualConceptCatalog.cs`, `MainMenuController.cs`, and `PauseMenuController.cs`. Unity console returned 0 errors. `dotnet build` was not invoked because active `dotnet` PID 20236 and worker processes were detected.

Exact microseconds saved: rejected prefab scene variants and shader/material path. Fixed decor refresh is estimated 4-12 us per throttled visual sync and 0 B/frame; cold creation cost is paid once per menu hierarchy build.

## 2026-05-31 - Pass 7 - Variant Contract Gate

What was wrong: the project now has 180 visual menu combinations, but there was no code-level gate proving enum counts, index round-trips, display labels, and continuous quality math stay valid after future edits.

What was done: added `Assets/_Project/Scripts/UI/Editor/MenuVisualVariantContractValidator15MM.cs`, a manual editor validator at `Hecton8/15MM/Validate Menu Visual Variants`. It checks 15 styles, 12 concepts, 180 total combinations, valid index clamp boundaries, non-empty display names, and finite style/concept states at multiple quality weights. Also bounded sonar sweep rotation with `math.fmod` and disabled inactive decor slots in `MenuVisualConceptDecorApplier`.

Cinematic cheats used: no new runtime system. The proof gate is editor-only; runtime decor remains fixed uGUI rectangle fakes.

Dependency proof: no `GlobalRegistry.Get<T>()`, no DataVault access, no lock, no `.Complete()`, no scene search in hot methods. The validator lives in an Editor asmdef and is not player runtime.

Phase proof: no runtime phase route was added. Decor still updates only from `LateFrameTick`; validator runs only when manually invoked in editor.

Lock proof: touched files contain no DataVault write locks and no nested lock route.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MenuVisualVariantContractValidator15MM.cs` and `MenuVisualConceptDecorApplier.cs`. `dotnet build` was not invoked because active `dotnet` PIDs 19372 and 20236 were detected.

Exact microseconds saved: runtime validator cost is 0 us/frame. Bounded sweep adds under 1 us per throttled decor refresh and prevents long-session precision drift. Avoided full build while compiler workers were active, saving seconds of CPU contention.

## 2026-05-31 - Pass 8 - Long Label Fit

What was wrong: several valid menu names are long enough to clip in generated settings UI on narrow layouts. The weakest examples are `EMERGENCY BULKHEAD PANEL` and `QUARANTINE EVIDENCE WALL`.

What was done: configured TMP built-in auto-sizing and ellipsis in `PauseMenuController.CreateText` and `SettingsPanel.CreateMenuStyleTextCold`. Also corrected the serialized tooltip to describe style and concept rows, not style only.

Cinematic cheats used: none. This is layout stability, not atmosphere.

Dependency proof: no registry route, no scene search, no DataVault lock, no `.Complete()`, no new component type. Built-in TMP properties are set only during cold text creation.

Phase proof: no runtime phase route was added. Existing visual sync still owns style/concept presentation writes.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `SettingsPanel.cs` and `PauseMenuController.cs`. Unity console returned 0 errors. `dotnet build` was not invoked because active `dotnet` PID 20236 was detected.

Exact microseconds saved: rejected `LocalizedTMPAutoSizer` component route to avoid permanent late-frame tickables. Runtime steady-state cost remains 0 B/frame; text fit cost is interaction/layout-bound and estimated under 5 us per affected label update.

## 2026-05-31 - Pass 9 - Pause Variant Indexing

What was wrong: pause settings could cycle style/concept forward, but the UI did not expose list position. With 15 styles and 12 concepts this makes visual review slower and less deterministic.

What was done: changed pause style/concept status to `MENU STYLE 01/15: ...` and `MENU CONCEPT 01/12: ...`. The formatting uses existing fixed char buffers, `CopySpanToBuffer`, and `TryFormat`; no managed strings or dropdowns.

Cinematic cheats used: none. This is selection usability and review speed.

Dependency proof: no registry route, no scene lookup, no DataVault lock, no `.Complete()`, no extra UI component. Status writes remain bounded char-buffer TMP writes.

Phase proof: status refresh happens on settings panel build, interaction, hot-swap, or localization callback. Style/concept presentation still applies in `LateFrameTick`.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `PauseMenuController.cs`. Unity console returned 0 errors. `dotnet build` was not invoked because active `dotnet` PIDs 17572 and 20236 were detected.

Exact microseconds saved: avoided dropdown/list allocation path. Indexed status costs under 2 us per refresh and 0 B/frame.

## 2026-05-31 - Pass 10 - Main Settings Variant Indexing

What was wrong: main menu settings could show style/concept names, but not catalog position. That made visual review weaker than pause settings and slower across 180 combinations.

What was done: added fixed display buffers in `SettingsPanel` and changed main settings labels to `01/15 NAME` and `01/12 NAME` through `SetCharArray`.

Cinematic cheats used: none. This is selection ergonomics.

Dependency proof: no registry route, no scene lookup, no dropdown/list UI, no string formatting.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `SettingsPanel.cs`. `dotnet build` was not invoked because active `dotnet` PID 20236 was detected.

Exact microseconds saved: avoided dropdown/list path; indexed labels cost under 2 us per interaction and 0 B/frame.

## 2026-05-31 - Pass 11 - Audit Fixes And Phase Cleanup

What was wrong: independent static audit found four valid issues: stale localization listener risk in main menu, style/concept cancel semantics in settings, pause command mutation in `LateFrameTick`, and `EventSystem.current` lookups in pause selection paths.

What was done: main menu now unregisters localization listener unconditionally. `SettingsPanel` snapshots style/concept on open and restores them on cancel. `PauseMenuController` processes queued pause/cancel commands in `UnscaledFastTick`, leaving `LateFrameTick` as visual sync only. Pause selection and clear now use `_cachedEventSystem`; `EventSystem.current` remains only in cold `EnsureEventSystem`.

Cinematic cheats used: none. This is architecture and lifecycle hardening.

Dependency proof: no `GlobalRegistry.Get<T>()`, no DataVault lock, no `.Complete()`, no string interpolation. `EventSystem.current` is not used by pause selection methods anymore.

Phase proof: `LateFrameTick` in pause now only calls `SyncVisualStyleLateFrame` and `SyncVisualConceptLateFrame`. Pause truth/input/cursor/time-dilation mutations execute from `UnscaledFastTick`.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MainMenuController.cs`, `PauseMenuController.cs`, and `SettingsPanel.cs`. Unity console returned 0 errors.

Exact microseconds saved: removed global EventSystem accessor from pause selection routes, estimated 1-5 us on affected interactions. More important: eliminated command mutation from visual sync.

## 2026-05-31 - Pass 12 - Variant Contract Execution

What was wrong: editor validator existed and compiled, but had not been executed after audit fixes.

What was done: invoked `MenuVisualVariantContractValidator15MM.ValidateOrThrow` through Unity `execute_code` reflection. Result: `15MM menu visual variant contract OK after audit fixes`.

Cinematic cheats used: none. This is proof execution.

Dependency proof: in-memory editor execution only. No runtime dependency added.

Validation proof: `validate_script` passed on modified menu files; Unity console returned 0 errors; the 180-variant validator returned OK. `dotnet build` was not invoked because active `dotnet` PID 20236 was detected.

Exact microseconds saved: avoided full project build under active compiler process, saving seconds of CPU contention. Runtime cost remains 0 B/frame.

## 2026-05-31 - Final Validation Caveat

What was wrong: final Unity console check returned 3 errors, but they are in bootstrap/global files outside the 15MM menu domain: `GlobalRegistry.cs`, `GameBootstrapper.cs`, and `BootstrapStatus.cs`.

What was done: did not modify those files because they are outside assigned domain and likely owned by another active agent. Menu-domain script validation remains clean.

Validation proof: `validate_script` passed on `MainMenuController.cs`, `PauseMenuController.cs`, `SettingsPanel.cs`, and `MenuVisualStyleApplier.cs` after the audit fixes. `git diff --check` returned only LF-to-CRLF warnings on existing files. `dotnet build` was not invoked because active `dotnet` PID 20236 was detected.

Exact microseconds saved: no runtime change. This is a boundary note to avoid false reporting.

## 2026-05-31 - Pass 13 - Continuation Hardening

What was wrong: loading screen was still outside the visual style/concept contract; multiple menu-adjacent presentation systems used scaled time or idle dispatcher registration; save thumbnail/live settings preview had lifecycle rollback holes.

What was done: integrated `LoadingScreenController` with the same 15 style x 12 concept route and decor applier. Converted loading tips, fade transition, hover preview, settings live preview/comparison, settings animator, and tooltip timers to unscaled dispatcher time. Added cancellation to save thumbnail async loading, dispatcher hot-swap repair to hover preview, baseline rollback/retry logic to live settings preview, `CancelPendingChanges()` on settings panel, and moved main menu panel transition writes from `Tick` to `LateFrameTick`. Replaced hardcoded comparison FPS table with continuous quality estimate, fixed Ultra preset to `MaxContinuousQualityLevel`, and made fade/animator tick registration active-only.

Cinematic cheats used: loading/menu concepts remain cheap uGUI geometry, scalar drift, pulse, scanline, and bounded sonar sweep. No physics, material animation, particles, shader variants, or scene prefab duplication were added.

Dependency proof: static scan of touched files showed no `GlobalRegistry.Get<T>()`, no direct `GetComponent()` in high-frequency loops, no `.Complete()`, and no `lock`. `TryGetComponent` remains only in cold build/resolve paths. Settings and dispatcher dependencies are cached in lifecycle/hot-swap routes.

Phase proof: style/concept/decor application and main menu panel alpha writes execute from `LateFrameTick`. Pause/menu commands execute from command/input phases, not visual sync. State transfer into visual sync is primitive enum/float/bool fields and cached references, no heap handoff.

Lock proof: no DataVault write lock was added or touched by 15MM. No nested lock vector exists in the edited menu files.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on continuation files including loading, settings, save preview, tooltip, fade, animator, main menu, pause menu, and settings manager. Unity in-memory reflection executed the editor variant validator and returned `15MM menu visual variant contract OK after continuation fixes`. Latest console query after continuation returned 0 errors.

Compilation throttle proof: `dotnet build` was not invoked. Active `dotnet`/compiler workers were detected, so validation stayed in Unity script validation and in-memory editor execution.

Exact microseconds saved: active-only fade/animator callbacks save roughly 2-8 us/frame while idle. Canceled thumbnail loads can avoid 50-300 us of wasted read/decode work during rapid slot changes. Avoided scaled-time stall fixes are CPU-neutral but prevent pause/loading visual dead zones. Continuous comparison estimate costs under 2 us per UI refresh. Loading style/concept refresh stays throttled and cached at roughly 4-14 us per refresh, 0 B/frame.

## 2026-05-31 - Pass 14 - Settings Persistence And Preview Semantics

What was wrong: settings Apply/Reset/Preset paths saved options after many individual setters. Graphics preset buttons also committed immediately, and menu style/concept cycling wrote persistence on every preview click.

What was done: added a dirty persistence batch route to `SettingsManager` with `BeginPersistenceBatch`/`EndPersistenceBatch` and `try/finally` flush boundaries. `SettingsPanel.OnApply` now applies cached settings inside one batch. Preset buttons update cached preview state only. Style/concept cycle buttons send preview-only events and persist only when Apply is pressed; Cancel restores the original visual snapshot without writing.

Cinematic cheats used: none. This is persistence and interaction contract hardening.

Dependency proof: no `GlobalRegistry.Get<T>()`, no DataVault lock, no `.Complete()`, and no hot-loop `GetComponent()` were added. Static scan shows only cold `TryGetComponent` routes in settings camera/UI resolve helpers.

Phase proof: preview events are interaction-triggered presentation state changes. Final settings mutation happens inside Apply. Style/concept visual consumers still sync from `LateFrameTick`; no simulation truth route was added.

Lock proof: no DataVault write lock was added or touched. Persistence batching uses a local depth counter and dirty flag, not nested locks.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `SettingsPanel.cs` after the preview-only changes; `SettingsManager.cs` returned zero diagnostics after the batch and preview route changes. Follow-up console read was attempted but Unity MCP readiness timed out after 60 seconds, so no forced build or repeated compiler pressure was applied.

Compilation throttle proof: `dotnet build` was not invoked. Active `dotnet` PID 20236 was detected.

Exact microseconds saved: avoids roughly 10-20 redundant option save calls per Apply/Reset/Preset when many fields change. Preview clicks now avoid disk writes entirely. Runtime steady state remains 0 B/frame.

## 2026-05-31 - Pass 15 - Listener Ownership And Input Retry Throttle

What was wrong: local menu binding could erase other systems' UnityEvent listeners through broad cleanup. Main menu input route fallback could also keep calling UI input bind/action-map switch every frame while route readiness stayed false.

What was done: menu/settings/modal bindings now remove only their own cached listener before re-adding it. Main menu input route fallback now uses `InputRoutingRetrySeconds` and resets the retry window only on cold EventSystem/input-manager cache changes.

Cinematic cheats used: none. This is ownership and hot-path pressure cleanup.

Dependency proof: no `GlobalRegistry.Get<T>()`, no DataVault lock, no `.Complete()`, and no hot-loop `GetComponent()` were added. `TryGetComponent` remains only in cold resolve/cache paths.

Phase proof: input fallback remains in `Tick`, but the expensive action-map switch path is bounded. Presentation style/concept/decor still syncs from `LateFrameTick`.

Lock proof: no DataVault write lock was added or touched.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MainMenuController.cs` and `SettingsPanel.cs`. Static scan shows no `RemoveAllListeners()` under `Assets/_Project/Scripts`. `ModalWindow.cs` has one `EnsureRuntimeBindings` definition and one `Hide` definition by source scan; Unity `validate_script` reports a false duplicate from call sites in that file, with no `ModalWindow` console errors. Unity in-memory reflection returned `15MM menu visual variant contract OK after input retry pass`.

Compilation throttle proof: `dotnet build` was not invoked. Active `dotnet` PID 20236 was detected.

Exact microseconds saved: input route failure windows avoid roughly 25-80 us/frame of repeated action-map switch pressure. Listener change is cold-path correctness with 0 B/frame steady-state cost.

## 2026-05-31 - Pass 16 - Pause Menu Finalization And Preview Batching

What was wrong: main-menu localization was still resolved from refresh helpers, generated pause buttons had generic identities, save retry modals used captured lambdas, and source-built pause style/concept cycling still persisted on every catalog click.

What was done: cached `ILocalizationTextReadModel` cold in `MainMenuController`. Replaced generated pause main buttons with semantic names and method-group handlers. Replaced save-slot and retry capture routes with fixed method groups plus one pending retry slot field. Converted source-built pause style/concept cycling to preview-only events and committed the final dirty selection once through `SettingsManager` persistence batching on settings exit/close/quit/disable/destroy/settings-owner swap.

Cinematic cheats used: none. This pass is dependency, allocation, and persistence hardening for the existing visual style/concept system.

Dependency proof: static extraction of `Tick`, `UnscaledFastTick`, `LateFrameTick`, `Execute`, and `FixedUpdate` bodies in touched menu files found no direct `GlobalRegistry.Get`, `GetComponent`, `TryGetComponent`, `EventSystem.current`, `.Complete()`, or `lock`. Broader scans show `TryGetComponent`/`EventSystem.current` only in cold build/cache methods.

Phase proof: menu style/concept/decor presentation remains in `LateFrameTick`. Pause style/concept selection is an interaction event; final persistence happens on section/menu lifecycle boundaries, not every visual sync.

Lock proof: no DataVault or other write lock was added or touched. Persistence batching uses a local depth counter and dirty flag with `try/finally`, not nested locks.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `PauseMenuController.cs` and `MainMenuController.cs`. Unity in-memory reflection returned `15MM menu visual variant contract OK final`. Final Unity console read could not run because the MCP session was unavailable; no forced rebuild or retry storm was performed.

Compilation throttle proof: `dotnet build` was not invoked. Active `dotnet` PIDs 20236 and 21132 were detected.

Exact microseconds saved: localization cache removes roughly 1-6 us per affected text refresh. Input retry remains bounded at 25-80 us/frame saved during route-failure windows. Pause generated-button/retry cleanup removes small cold allocations and roughly 1-4 us per affected interaction. Preview batching avoids repeated settings writes while browsing up to 180 visual combinations; steady state remains 0 B/frame.

## 2026-05-31 - Pass 17 - Main Modal Route And Save Cache Hardening

What was wrong: main-menu modal confirm/cancel paths still used captured lambdas for load/retry/error flows, and save validation could resolve `GlobalRegistry.Save` from interaction code.

What was done: added cached modal `Action` fields, one pending start-slot transfer field, and `CacheSaveManagerCold`. `GlobalRegistry.Save` is now copied only from `Awake`, `Start`, `OnEnable`, and hot-swap. Modal load/retry/return/quit routes reuse method groups.

Cinematic cheats used: none. This pass removes managed callback churn and hot dependency fallback from menu control flow.

Dependency proof: extracted `Tick`, `UnscaledFastTick`, `LateFrameTick`, `Execute`, and `FixedUpdate` bodies in touched menu files contain no direct `GlobalRegistry.Get`, `GlobalRegistry.Save`, `GlobalRegistry.LocalizationText`, `GetComponent`, `TryGetComponent`, `EventSystem.current`, `.Complete()`, or `lock`.

Phase proof: presentation still synchronizes in `LateFrameTick`. Modal actions are interaction commands; no deferred simulation truth or visual mutation was moved into the wrong phase.

Lock proof: no DataVault lock was added or touched. Existing settings persistence batching remains `try/finally` bounded and local to SettingsManager.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `MainMenuController.cs` and `PauseMenuController.cs`. Unity in-memory reflection invoked `Hecton8.UI.Editor.MenuVisualVariantContractValidator15MM.ValidateOrThrow` from `Hecton8.UI.Editor` and returned `15MM menu visual variant contract OK after main modal/save-manager cold-cache pass`.

Console proof: no `15MM` or `MenuVisual` errors are present. Current Unity console has unrelated existing/global errors: `GameBootstrapper` CoreServices bootstrap failure, `BIOS ERROR 0xBOOT_TIMEOUT`, missing script references, and `GlobalRegistry SystemDispatcher is not registered`.

Compilation throttle proof: `dotnet build` was not invoked. Active `dotnet` PID 20236 was detected.

Exact microseconds saved: removes closure allocations and roughly 1-6 us per affected modal/open path; keeps save routing registry-cold during interaction; steady state remains 0 B/frame.

## 2026-05-31 - Pass 18 - Modal Label Refresh And Cold Allocation Cleanup

What was wrong: custom modal button labels were applied only once, so localization refresh could revert visible modals to generic CONFIRM/CANCEL labels. Save-slot text auto-wiring used `params string[]` token arrays. Settings apply-failure modal still created captured retry/revert callbacks.

What was done: `ModalWindow` now stores custom confirm/cancel labels until `Hide` and `RefreshButtonLabels` preserves them across localization refresh. `SaveSlotUI` now uses direct fixed token helpers for slot/details text discovery. `SettingsPanel` now uses cached `Action` fields for apply retry and reset defaults modal routes.

Cinematic cheats used: none. This pass is localization correctness and allocation cleanup inside the menu domain.

Dependency proof: no registry lookup, component lookup, DataVault lock, `.Complete()`, or new signal route was added to any hot method.

Phase proof: modal label refresh remains presentation-only. Settings retry/reset remain interaction commands. No simulation-phase mutation was introduced.

Lock proof: no DataVault write lock was added or touched. No nested lock path exists in this pass.

Validation proof: `git diff --check` returned only existing LF/CRLF warnings, no whitespace errors. Static scans found no `() =>`, `new Action`, `params string[]`, or `RemoveAllListeners()` in the touched menu files. Hot-method extraction returned `HOT_METHOD_SCAN_OK`.

Unity validation state: Unity editor first reported compiling; after a bounded wait the Unity MCP state query returned `Unity session not ready` while active `dotnet` PID 1840 was still present. `validate_script`, in-memory variant execution, and `dotnet build` were not launched for this pass.

Compilation throttle proof: `dotnet build` was not invoked while `dotnet` was active.

Exact microseconds saved: modal label fix is correctness with under 1 us per refresh. Save-slot params removal avoids tiny token-array allocations and roughly 1-3 us per cold hierarchy scan. Settings cached actions avoid roughly 1-4 us and closure allocation per apply-failure modal display. Steady state remains 0 B/frame.

## 2026-05-31 - Pass 19 - Pause Audio Dispatcher Cold Cache

What was wrong: `PauseMenuAudioIntegration` still resolved `GlobalRegistry.TickDispatcher` inside `IsSimulationPaused`, which runs from pause-menu open/close and button audio commands.

What was done: `PauseMenuAudioIntegration` now implements `IGlobalRegistryHotSwapListener`, caches `ITickDispatcher` in `Awake` and `OnEnable`, refreshes it on `GlobalRegistryServiceSlot.Dispatcher` hot-swap, unregisters on disable/destroy, and uses the cached field in audio command paths.

Cinematic cheats used: none. This pass is cold-DI cleanup for menu audio feedback.

Dependency proof: `GlobalRegistry.TickDispatcher` remains only in lifecycle cache routes. Static scan found no `GlobalRegistry.Get<T>()`, `GetComponent()`, `EventSystem.current`, `.Complete()`, `lock`, `new Action`, or lambda in the touched file.

Phase proof: no simulation or presentation phase was moved. Pause audio commands remain interaction commands; dispatcher paused-state is read from a cached owner interface.

Lock proof: no DataVault write lock was added or touched. No lock route exists in this pass.

Validation proof: Unity MCP `validate_script` returned zero diagnostics on `Assets/_Project/Scripts/UI/PauseMenuAudioIntegration.cs`. Hot-method extraction returned `HOT_METHOD_SCAN_OK`. `git diff --check` returned only the existing LF/CRLF warning.

Console proof: Unity console still reports unrelated/global existing errors: `FatalMemoryLeakException` in `NativeMemorySentinel`, `GameBootstrapper` CoreServices bootstrap failure, and `BIOS ERROR 0xBOOT_TIMEOUT`.

Compilation throttle proof: `dotnet build` was not invoked. Active `dotnet` PID 1840 was detected.

Exact microseconds saved: removes one registry property read from each affected pause audio command, estimated 1-3 us per command. Steady state remains 0 B/frame.

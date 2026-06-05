# 2702 Underwater Registry Publication Route Audit

Date: 2026-06-04
Worker: Batch27 / 2702
Mode: report-only static/source/log audit
Evidence label: STATIC VERIFIED for source and serialized scene inspection only. Runtime remains PENDING VERIFICATION.

## Authority Read

- `AGENTS.md`
- `quality.md`
- `water.md`
- `systems.md`
- `bootstrap.md`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `Docs/Reports/Batch26/BATCH26_SYNTHESIS_FOR_UNITY_OWNER.md`

No Unity launch, Play Mode, dotnet build, process kill, asset import, or project edit outside this report was performed.

## Finding

`HectonUnderwaterVisuals` still publishes its registry service from its own `OnEnable()` path:

- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:75` has `[ExecuteAlways]`.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:77` has `[DefaultExecutionOrder(-4000)]`.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:1025-1085` runs runtime setup in `OnEnable()`.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:1041` calls `GlobalRegistry.RegisterUnderwaterVisualsRuntime(this)`.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:1594-1625` retries runtime dependencies in `Start()`, but does not retry service publication.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:1640-1641` and `:1738-1739` unregister only when the current slot equals this owner.

This is phase-fragile. `DefaultExecutionOrder(-4000)` cannot prove registry publication order against bootstrap ready-lock, async scene activation, domain reload, manual/editor component toggles, additive scene operations, or late `AddComponent`/enable paths.

`GlobalRegistry` already contains the correct ready-lock mechanism and a narrow scene runtime publication gate:

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:2497-2500` locks the registry to `Ready`.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:2507-2533` opens/closes `_sceneRuntimePublicationGateDepth`.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:3519-3522` routes `RegisterUnderwaterVisualsRuntime()` through `RegisterServiceAllowSameInstance`.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7042-7053` issues a hot-swap override token only when the scene publication gate is open and the slot is eligible.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7065-7096` excludes core/bootstrap slots; `UnderwaterVisualsRuntime` is not excluded, so it is eligible as a scene runtime hot-swap slot.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7101-7111` throws `CriticalBootException` when service publication happens after `Ready` without a valid token.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7246-7283` applies the token before ready-lock guard and queues rebound only after accepted replacement.

`GameBootstrapper` already owns the right phase boundary:

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2177-2180` runs scene activation before `GlobalRegistry.LockReady()`.
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2810-2835` delegates non-bootstrap active scene activation through `ExecuteSceneActivationAsync`.
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2965-3042` wraps bootstrap gameplay scene handoff in `BeginSceneRuntimePublicationGate()` / `EndSceneRuntimePublicationGate()`.

Serialized scene state is statically sane but not runtime proof:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4604-4613` has one active GameObject named `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4614-4625` has one enabled `HectonUnderwaterVisuals` MonoBehaviour on that object.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4626-4654` has required main camera, player camera, sun, atmosphere, ocean underwater material, sky material, biome palette, and biome matrix references assigned.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4637-4640` still has motes/snow/bubbles/beam refs unassigned. That is a visual proof blocker, not the registry publication blocker.

Current local `Editor.log` search found only a script import line for `HectonUnderwaterVisuals`, not a fresh ready-lock rejection in the sampled tail. The prior ready-lock rejection remains dirty-log evidence from Batch26, not a clean current-session repro.

## Phase Map

1. `GlobalRegistry.ResetStaticState()` runs at subsystem registration and clears `_sceneRuntimePublicationGateDepth`.
2. `GameBootstrapper` begins registration before bootstrap/service setup.
3. Core, environment, player, and UI bootstrap services register while registry phase is not `Ready`.
4. Scene activation loads or validates the gameplay scene.
5. Scene runtime services must publish under one of two legal windows:
   - before `GlobalRegistry.LockReady()` during bootstrap scene activation;
   - after `Ready` only while `BeginSceneRuntimePublicationGate()` is open.
6. `GlobalRegistry.LockReady()` closes ordinary service publication.
7. `HectonUnderwaterVisuals` may register cold/slow/late-frame tick callbacks after dispatcher availability, but service publication must already be owner-approved.
8. `OnDisable()` / `OnDestroy()` may unregister the matching registry slot; a later re-enable after `Ready` is illegal unless it is inside the scene runtime publication gate.

## Owner Decision

Owner-correct publication route: `GameBootstrapper` / scene activation owns `UnderwaterVisualsRuntime` service publication as a scene runtime owner.

`HectonUnderwaterVisuals` owns underwater presentation state and visual sync. It should not decide when it becomes a global registry service. Its `OnEnable()` may initialize local presentation state and set local active references, but global service publication must be called from the bootstrap/scene activation publication pass or rejected with a clear owner-phase diagnostic.

No new global route is needed. `GlobalRegistry` already has the required scene publication gate and ready-lock policy. The fix is ownership and phase wiring, not a broader registry API and not a ready-lock bypass.

## Candidate Fix Plan

1. Add a bootstrap-owned scene runtime publication pass in `GameBootstrapper` or the existing scene activation service path. Placement: after the gameplay scene is active and before `GlobalRegistry.LockReady()`, and inside `BeginSceneRuntimePublicationGate()` for post-ready scene handoffs.
2. That pass must cold-scan the active scene only during scene activation, resolve exactly one enabled `HectonUnderwaterVisuals` owner, and reject zero or duplicate active owners with a boot/scene activation fault. Do not run this scan from `Tick`, `Update`, `SlowTick`, or visual sync.
3. Move `GlobalRegistry.RegisterUnderwaterVisualsRuntime(this)` out of `HectonUnderwaterVisuals.OnEnable()` or guard it so it cannot publish from an arbitrary post-ready enable. Local visual initialization may remain in `OnEnable()`.
4. Keep `OnDisable()` / `OnDestroy()` unregister guards as-is for matching-owner cleanup. If a registered owner is disabled after ready-lock outside a scene transition, treat it as an owner lifecycle fault and require a scene activation gate for re-publication.
5. Keep `Start()` dependency retries limited to local dependencies and dispatcher callback registration. Do not add a `Start()` or per-frame registry publication retry.
6. Add proof-only diagnostics to the accepted runtime manifest: owner GameObject name, instance id, registry slot state, registry phase, scene publication gate state at publication time, and whether registration happened pre-ready or through the scene gate.

## Rejected Shortcuts

- Do not relax `GuardServicePublication()` or bypass ready-lock.
- Do not add a blind retry in `Start()`, `SlowTick()`, `Update()`, or editor update.
- Do not turn `GlobalRegistry` into hot polling or a late self-repair bus.
- Do not depend on `[DefaultExecutionOrder(-4000)]` as proof of owner publication order.
- Do not accept manual AddComponent/toggle logs as clean gameplay proof.
- Do not clean this by deleting the static scene owner; the static scene currently has one active owner.
- Do not treat missing motes/snow/bubbles/beam refs as the registry root cause. They are separate underwater visual proof blockers.
- Do not make quality tiers change the registry owner route.

## Route Card Outline

No changed global authority route is required if the implementation uses the existing `UnderwaterVisualsRuntime` registry slot and existing scene runtime publication gate.

If implementation adds a new registry API or bootstrap-owned publication helper, the route card must be:

```text
Route ID: SCENE_UNDERWATER_VISUALS_RUNTIME_PUBLICATION
Owner: GameBootstrapper / scene activation
Owner domain: Bootstrap / scene runtime publication
Owning file/system: Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs or scene activation service
Problem: HectonUnderwaterVisuals self-registers from OnEnable and can hit ready-lock after RegistryPhase.Ready.
Why owner-local data is insufficient: consumers need one stable cold identity for underwater presentation runtime.
Why direct caller/owner interface is insufficient: multiple consumers already cache or receive the registry service; the slot exists.
Instrument: GlobalRegistry cold service/interface
Producer/consumer phase: producer scene activation cold setup; consumers cache after bootstrap/hot-swap notification.
Cadence/capacity: one publication per gameplay scene activation; zero per-frame work.
Expected max events/reads per frame: 0 hot reads; one rebound notification on scene owner replacement.
GlobalQualityWeight behavior: no authority change; only manifest/reporting may record quality weight.
Accessor purity: no Get/TryGet/Resolve/Read API publishes, syncs scene state, allocates, completes jobs, mutates global state, or searches hot paths.
Payload/data shape: HectonUnderwaterVisuals service identity only.
Managed fields present: service identity is Unity object; cold registry only.
UnityEngine.Object fields present: yes, cold service identity only.
Overflow/failure: zero active owners or duplicates fail scene activation; late post-ready publication outside gate is rejected.
Telemetry fields: scene name, owner name, owner instance id, registry phase, gate-open flag, duplicate count, publication result.
Black-box fields: not gameplay critical; include in bootstrap/scene activation state record if available.
Profiler marker: scene activation publication marker if runtime source changes.
GC proof required: yes for changed scene activation scan/path.
Shutdown/disposal: matching `OnDisable`/`OnDestroy` unregister; scene unload clears/rebinds through scene transition.
Scene unload behavior: old owner unregisters; next scene owner publishes only during scene activation gate.
Rejected alternatives: OnEnable self-registration, Start retry, ready-lock bypass, hot registry polling.
Why this does not increase global monolith risk: reuses existing slot and narrows publication to bootstrap-owned scene activation.
Proof required before GREEN: clean Unity log, single active owner, registry slot bound to the owner, no ready-lock rejection, no per-frame registry reads, manifest fields for underwater proof routes.
Review disposition: YELLOW until source diff, compile/import proof, clean Play Mode/log proof, and manifest proof exist.
Status: PROPOSED
```

## Proof Requirements

Minimum fresh proof before accepting the route:

- Clean same-session Unity log newer than final screenshot/capture, with no `Ready-locked registry rejected registration: HectonUnderwaterVisuals`, no `CriticalBootException`, no compile/import/domain reload noise during the proof window, and no MCP transport fault storm.
- Static or runtime proof of exactly one active enabled `HectonUnderwaterVisuals` owner in `02_HECTON_WORLD`.
- Runtime proof that `GlobalRegistry.UnderwaterVisuals` is bound to `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` or the current scene activation owner.
- Runtime publication proof naming whether publication happened pre-ready or through the scene runtime publication gate.
- No late self-publication attempt from `OnEnable()` after `RegistryPhase.Ready` outside the gate.
- Manifest fields for `underwater_0_5m` and `underwater_20_50m_route`: scene, loaded scenes, camera transform/FOV, player or cockpit depth, water depth band, `_debugIsUnderwater`, `_debugDepth`, registered underwater owner name/id, material GUIDs/key values, continuous `GlobalQualityWeight`, render scale, log path, and clean-window summary.
- Underwater visual refs for motes/snow/bubbles/beam must be either assigned or explicitly documented as disabled/fallback in the visual manifest before visual acceptance.

## Low / Middle / High / Ultra Consequences

- Low: one cold publication during scene activation, no hot registry retry, no extra per-frame cost, underwater visuals may reduce optional particles/caustics by continuous `GlobalQualityWeight` only after owner route is stable.
- Middle: same owner route, normal visual sync cadence, no authority or DTO change.
- High: same owner route, richer underwater presentation can be bought in `VISUAL_SYNC`; registry publication remains one cold event.
- Ultra: same owner route, visual overkill is presentation-only; no additional registry polling, no changed gameplay truth, no changed save identity.

## Key Blockers

1. Source still self-publishes `UnderwaterVisualsRuntime` from `HectonUnderwaterVisuals.OnEnable()`.
2. Accepted proof cannot use any log containing a post-ready `HectonUnderwaterVisuals` registration rejection.
3. Runtime owner binding is not proven by the serialized scene. It needs clean same-session runtime proof.
4. Visual underwater proof remains blocked by missing or undocumented underwater detail refs and missing manifest fields for 0-5 m and 20-50 m routes.

## Final Status

STATIC VERIFIED: source and serialized scene route inspected.

PENDING VERIFICATION: runtime publication order, clean log, Play Mode behavior, manifest fields, underwater captures, profiler/GC state, and final visual acceptance.

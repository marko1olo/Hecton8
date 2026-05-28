# HECTON-8 Mod API Specification

Date: 2026-05-19
Status: ENVELOPE-ONLY MOD API SPEC / STATIC DOC UPDATE / PENDING RUNTIME VERIFICATION

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Evidence class: STATIC_SOURCE / STATIC_DOC
Owner domain: Modding API static contract
Companion schema: `Docs/Modding/Signal_Schema.json`

## 2026-05-19 Current Runtime Authority

This specification contains older source-audit material for the managed mod API. The active runtime boundary is now narrower:

- `HectonAPI.Commands.RequestFuture(in FutureCommandEnvelope envelope)` is the only current public command ingress for UGC; it requires active `ModExecutionScope` and a matching `ModderSignature`.
- `HectonAPI.Commands.Request`, `RequestAup`, and `RequestRenderInstance` require active `ModExecutionScope`, then return `false` while envelope-only mode is enforced.
- `FutureCommandSandboxValidator` is internal engine/control-plane code with no public static control-plane method surface. `MockModQueue` also has no public queue handle or public instance control methods. Neither is a public SDK or runtime mod surface.
- `HectonModHooks` and `IModCommandKernel` are internal first-party infrastructure, not modder extension points; their direct static/hook routes are not public SDK members.
- Managed mod entry points, managed factories, projected managed events, direct resource proxy registration, filesystem content discovery, localization file injection, and direct asset loading are quarantined in envelope-only mode.
- Canonical mod IDs are part of the package contract: loader and SDK builder require lowercase letters/digits separated by single `.`, `_`, or `-`, reject whitespace and reserved filesystem device segments, validate dependency IDs with the same rule, and restrict `EntryAssembly` to a package-local `.dll` file name only.
- Scope owner proof is part of the public facade contract: `ModExecutionScope` rejects blank/anonymous owners and requires a non-zero owner hash before `HasActiveMod` can pass.
- SaveState store owner proof is part of the loader/save contract: public mod save payloads require active `ModExecutionScope`; internal engine-owned payloads use explicit `hecton.internal.` keys instead of deriving an owner from arbitrary payload keys.
- Engine-owned assembly identities are not mod identities. Loader and SDK builder reject managed DLLs named or metadata-identified as `Hecton8.*`, `Unity*`, `Assembly-CSharp`, `System`, `mscorlib`, or `netstandard`; the loader scans accepted top-level package DLLs up to the `32` DLL cap, disables over-cap packages, and the SDK builder caps selected DLLs at `32`, rejects duplicate output names, and deletes stale output DLLs through bounded cleanup. Friend assemblies such as `Hecton8.Plugins` remain first-party only.
- Runtime UGC packages must not rely on `.dll`, `.bundle`, `lang_*.json`, raw PNG, prefab, material, mesh, texture, audio clip, `GameObject`, `Transform`, `NativeArray`, `NativeQueue`, `GlobalDataVault`, or first-party `SignalBus<T>` access.
- Assets are referenced by approved hashes and CRC-checked envelope opcodes, not by loose files or Unity object handles.
- Human-friendly modder work starts from the Unity Editor `Hecton/Modding/SDK Hub` and `Hecton/Modding/External Starter Kit Workbench`. The Hub prioritizes `Create External Starter Kit`, opens the Workbench, links the SDK authoring plan, external starter kit file contract, product blueprint, API spec, runtime playbook, sample mod, local Mods folder, and static validator, runs that validator asynchronously, shows failed validator runs as Editor error UI, and gates the internal legacy package builder behind an explicit warning.
- Public external modders do not need the full HECTON-8 Unity project for manifest, graph, table, locale, validation, and review-handoff authoring. The versioned `ModdingSDK/ExternalStarterKit/` folder is the current public starter template; the SDK Hub can refresh missing files non-destructively and the Workbench gives Unity users one integrated screen for starter creation/refresh, required-file health, Capability Matrix, graph contract preview for budget/runtime/node/opcode errors, authoring data preview for settings rows and locale strings, graph/settings/locale snippet generation through `Tools/create_graph_node_snippet.ps1`, `Tools/create_settings_row_snippet.ps1`, and `Tools/create_locale_entry_snippet.ps1`, bounded settings/locale snippet application through `Tools/apply_settings_row_snippet.ps1` and `Tools/apply_locale_entry_snippet.ps1` with duplicate rejection and rollback, reviewed submission zip generation through `Tools/build_submission_package.ps1`, current submission package path/freshness and open route for `Generated/<mod-id>_submission.zip`, root `h8mod.ps1` launcher access, identity edits, async starter tool execution, failed-tool error UI, direct structure validation, validation/review, opcode discovery, key file access, core contract links, review manifest freshness, and `Reports/review_manifest.json` summary. The Workbench required-file health list matches the starter validator schema paths, including `Docs/capabilities.md`, `assets.schema.json`, `settings_table.schema.json`, and `locale.schema.json`. The starter kit contains root `h8mod.ps1`, `Docs/capabilities.md`, `mod.h8manifest.json`, `mod.json`, graph/table/content/locale folders, copied opcode/tuning references, `Schemas/*.schema.json`, `.vscode/settings.json`, `Tools/prepare_mod.ps1` for one-command no-Unity identity setup when `-Id` is supplied plus repeat validation/review-manifest rebuilds without identity arguments, `Tools/set_mod_identity.ps1` for safe no-Unity identity edits across both manifests, `Tools/list_allowed_opcodes.ps1` for no-Unity graph opcode discovery, `Tools/create_graph_node_snippet.ps1` for safe no-Unity graph node snippet generation, `Tools/create_settings_row_snippet.ps1` and `Tools/create_locale_entry_snippet.ps1` for safe no-Unity settings/locale snippet generation, `Tools/apply_settings_row_snippet.ps1` and `Tools/apply_locale_entry_snippet.ps1` for bounded settings/locale insertion with duplicate rejection and rollback, `Tools/build_submission_package.ps1` for reviewed no-Unity submission zip generation, `Tools/validate_structure.ps1` for no-Unity local structure validation, and `Tools/build_review_manifest.ps1` for deterministic `Reports/review_manifest.json` identity/file/hash reports. The copied reference CSVs are statically compared against the authoritative files under `Docs/Modding/`. The root launcher exposes menu/setup/validate/review/prepare/submission/opcode/snippet/apply-snippet/capabilities actions and delegates to the existing `Tools/*.ps1` scripts or prints `Docs/capabilities.md`; it is not a second package contract. The opcode list helper prints allowed aliases/hex tokens from `Reference/allowed_opcodes.csv` and supports JSON output for Workbench/CLI reuse. The graph snippet helper writes `Generated/graph_node_snippet.json` only after node id and opcode validation; it never rewrites `Graphs/main.h8graph.json`, so authors explicitly copy the generated object into `Nodes[]` and run validation. The settings/locale snippet helpers write `Generated/settings_row_snippet.json` and `Generated/locale_entry_snippet.json` only after id/key/kind/value validation; the apply helpers insert those snippets into `Tables/settings.h8table.json` and `Locales/en.h8loc.json` with duplicate rejection, post-write validation, and rollback while keeping manual copy as a fallback. The local validator requires `h8mod.ps1` and `Docs/capabilities.md`, rejects stale capability guide text, enforces canonical mod/dependency IDs, matching authoring/runtime manifest ID, display name/name, author, and semantic version fields, settings row schema/ID/kind/default type constraints, locale schema/code/key/value constraints, JSON Schema file presence/parseability, exact `.vscode/settings.json` schema URL/fileMatch pairs, graph node ID uniqueness, required graph opcodes, graph opcode allowlist membership against `Reference/allowed_opcodes.csv`, graph budget parity with `mod.h8manifest.json`, and editor schema mapping before later SDK tooling touches the package; the review manifest records package identity, hashes authoring/tool/schema files, records total bytes plus explicit file/byte limits, rejects oversized source files, and excludes `Generated/` plus `Reports/` outputs. The Workbench freshness check uses the same exclusion rule for `Generated/` and `Reports/` so generated reports do not make themselves stale. The submission package tool writes to a temp zip first and restores the previous submission zip if final replacement fails. The public tools chain scripts in-process and compose child paths through portable `Join-Path` segments so authors can use Windows PowerShell or `pwsh` on macOS/Linux. Unity is optional for advanced asset preview; it is not required for the starter-kit authoring loop.

Any section below that describes `IHectonMod` callbacks, `SubscribeProjected`, `SubscribeNative`, cold content overlays, resource proxy resolution, or legacy command lanes is legacy/source-audit context until the runtime owner explicitly re-enables it and the verification playbook is rewritten around the envelope-only boundary.

## Source Reality

The historical public mod event surface was not direct access to first-party simulation lanes. In the current envelope-only authority, this managed event surface is quarantined for runtime UGC.

Legacy source-backed mod surfaces:

- `HectonAPI.Events.SubscribeProjected(Action<ModEventDto>)` for selected `SignalBus<T>` projections.
- `HectonAPI.Events.SubscribeNative(HectonNativeEventHandler)` for immutable byte copies from approved NativeQueue event lanes.
- `HectonAPI.Events.Subscribe<TPayload>(HectonUnmanagedEventHandler<TPayload>)` for unmanaged mod-facing payloads.
- `HectonAPI.Commands.RequestFuture`, `Request`, `RequestAup`, and `RequestRenderInstance` for engine-validated writes.
- `HectonAPI.SaveState` for mod-owned save payloads.

Managed event access is a single-route facade: external mod code must route through `HectonAPI.Events`. `HectonEventBus` is internal first-party infrastructure with no public static bus member surface. `HectonGameEvents` legacy payload classes and members are internal-only first-party infrastructure, not SDK event DTOs; they must not expose `ItemData`, `BuildableData`, `HectonSurvivalSystem`, or survival records as mod handles. Public event subscription/publish methods require an active `ModExecutionScope` before envelope-only quarantine checks; unmanaged, native, and projected bridge routes plus private channel implementations reject anonymous subscribers before token creation; `Publish<TPayload>` rejects engine-owned command, result, projection, and lifecycle payload types when managed events are reopened; explicit `subscriberId` values must match the active mod id, `Unsubscribe` rejects tokens owned by a different active mod, and direct `HectonEventSubscription.Dispose()` validates active mod ownership for mod-owned tokens.

Forbidden for mods:

- Direct `SignalBus<T>.GetFrameSnapshot()` access.
- Direct `NativeQueue`, `NativeArray`, `DataVault`, `GlobalDataVault`, or DataVault handle access.
- Direct `GameObject`, `Transform`, prefab, audio clip, texture, material, mesh, or ScriptableObject references.
- String event names and JSON event payloads in hot paths.

## Allowed SignalBus Projections

Every currently mod-exposed `SignalBus<T>` lane is listed below. No other first-party `SignalBus<T>` lane is public unless this document and `Signal_Schema.json` are expanded.

| First-party lane | Mod event kind | DTO | Access | Cap |
|---|---|---|---|---:|
| `SignalBus<CombatDamageSignal>` | `CombatDamage` | `ModEventDto` | read-only projection | 10 low / 50 high per frame |
| `SignalBus<WeatherChangedSignal>` | `WeatherChanged` | `ModEventDto` | read-only projection | 10 low / 50 high per frame |

`InteractionEvents` and `CraftingEvents` are also exposed, but they are not `SignalBus<T>` projections. They are copied into `SubscribeNative` as immutable bytes for the callback duration. Native byte payloads are decoded by schema only: `InteractionEventPayload` is an explicit 32-byte source layout, and `CraftingEventPayload` is an explicit 64-byte source layout. Mods must not infer Unity object references, first-party queue handles, or lifetime beyond the callback span.

Full source audit: [Signal_Audit_Matrix.md](Signal_Audit_Matrix.md) records 173 current `ISignal` structs in `Core/Signals/GlobalSignalPayloads*.cs`. Only 2 are projected for mods. The remaining 171 are denied by default.

Current static closure: `Signal_Schema.json` schema revision `99` records the `173 / 2 / 171` signal split, the public `HectonAPI` surface counts, the internal-only sandbox validator/constants/event hook/managed command kernel control plane, internal-only sandbox validator plus `MockModQueue` static methods, queue handle, and instance control methods, public `FutureCommandEnvelope.SizeBytes` as the only sandbox size constant, internal-only FutureCommand output SignalBus DTOs, internal-only loader diagnostics descriptors and their members, canonical mod/dependency id validation, EntryAssembly filename-only validation, pre-read `mod.json` byte cap enforcement, bounded manifest discovery before candidate allocation, bounded top-level DLL/bundle/localization discovery, fail-closed raw texture file read handling after the raw PNG byte gate, exact-name-only legacy AssetBundle lookup with `GetAllAssetNames` suffix fallback forbidden, Unity Editor SDK Hub authoring entry point with External Starter Kit priority, External Starter Kit Workbench launch, async static validator execution, failed static validator error UI, and an explicit internal legacy builder warning gate, external starter kit workbench proof for generator reuse, required-file health, Capability Matrix, required-file list parity with the starter validator schema paths (`Docs/capabilities.md`, `assets.schema.json`, `settings_table.schema.json`, `locale.schema.json`), graph contract preview against `Graphs/main.h8graph.json`, `Reference/allowed_opcodes.csv`, and `mod.h8manifest.json` budget, authoring data preview for `Tables/settings.h8table.json` and `Locales/en.h8loc.json`, graph/settings/locale snippet generation through `Tools/create_graph_node_snippet.ps1`, `Tools/create_settings_row_snippet.ps1`, and `Tools/create_locale_entry_snippet.ps1`, bounded graph/settings/locale snippet application through `Tools/apply_graph_node_snippet.ps1`, `Tools/apply_settings_row_snippet.ps1`, and `Tools/apply_locale_entry_snippet.ps1` with duplicate rejection, graph budget repair, validation, and rollback, reviewed submission zip generation and submission package status/freshness/open route through `Tools/build_submission_package.ps1`, root launcher health/file access, direct structure validator launch, core docs links, async starter tool execution, failed starter tool error UI, review freshness against starter source files, identity tool, prepare tool, opcode list, review summary, and envelope-only warning, external starter kit generator, root `h8mod.ps1` launcher generation/proof including `submission`, `node-snippet`, `apply-node-snippet`, `setting-snippet`, `locale-snippet`, `apply-setting-snippet`, `apply-locale-snippet`, and `capabilities`, versioned starter template, `Docs/capabilities.md` capability guide generation/validation, copied reference CSV source parity, file contract, one-command no-Unity prepare helper with identity setup proof and existing-manifest rerun proof, no-Unity identity helper with canonical ID validation, semantic version validation, identity text parity proof, and invalid-version rejection, no-Unity allowed graph opcode list helper with text/JSON proof, no-Unity graph/settings/locale snippet helpers with CLI/JSON proof, Generated-only output, bounded graph apply that rejects duplicate node ids before `-Replace`, repairs the minimum envelope budget, and restores graph/manifest files after failed validation, and bounded settings/locale apply helpers that reject duplicates before `-Replace` and restore previous files after failed validation, in-process script chaining plus portable `Join-Path` child path composition for Windows PowerShell/pwsh portability, local no-Unity structure validator with root launcher required-file proof, capability guide text checks, canonical ID, dependency ID, authoring/runtime manifest ID/display name/author/version parity checks, settings row schema/ID/kind/default type checks, locale schema/code/key/value checks, graph opcode allowlist checks, duplicate/missing graph node checks, 256-node graph cap, graph budget parity checks, invalid graph opcode rejection, JSON Schema parse checks, exact editor schema URL/fileMatch mapping checks, and no-Unity review manifest builder with package identity summary, identity parity proof, file hash proof, explicit source count/byte limits, oversized-file rejection, and `Generated/`/`Reports/` output exclusion, no-Unity submission package helper proof with previous-zip preservation and `Reports/review_manifest.json` included in `Generated/<mod-id>_submission.zip`, bounded internal legacy Mod Builder bundle asset collection with a 512-asset cap, builder DLL selection capped at the loader's 32-DLL package cap, duplicate selected DLL filename rejection, shallow OnGUI validation with deep asset/DLL identity scans deferred to Build Internal Legacy Package, bounded stale-DLL cleanup, active scope owner proof, explicit SaveState store owner proof for scoped mod payloads and `hecton.internal.` engine payloads, reserved subtitle cue aliases (`TriggerSubtitleCue` and `SubtitleCue`) outside the runtime allowlist/editor opcode tuner/kernel inspector injector, reserved managed assembly identity blocking across accepted top-level package DLLs with over-cap package disable plus SDK stale-DLL cleanup, internal-only mod registry invalidation/settings UI DTOs, private listener adapters for public engine MonoBehaviours that consume internal registry invalidation, internal-only `ModWorldPersistenceManager` and `GlobalRegistry.ModWorldPersistence` routes, internal-only `HectonEventBus` with no public static bus member surface and no anonymous channel fallback, internal-only `HectonGameEvents` legacy managed payload classes and members, fixed explicit layout for public mod event/spatial payloads, schema-checked native byte payload layouts for `SubscribeNative`, engine-owned payload rejection in `Events.Publish<TPayload>`, active-scope ownership before envelope-only quarantine for public event facades, active-scope ownership for `RequestFuture` and legacy quarantined command facades, active-scope guards for resource, telemetry, localization, and save-state facades, resource registry owner-id match enforcement, smooth continuous projected event cap proof (`round(lerp(10, 50, smoothstep(saturate(GlobalQualityWeight01))))`), guarded public property routes, and owner-checked direct subscription `Dispose` with `runtimeProof` set to `PENDING_VERIFICATION`. `Validate_Mod_API_Static.ps1` fails if that static snapshot block drifts behind the source inventory again. This is still static source/doc evidence only, not Unity runtime verification.

## ModEventDto Contract

`ModEventDto` is a 64-byte fixed payload. It contains hashes, frame id, relative position, direction, two scalar values, kind, flags, quality tier, and sequence. It does not contain Unity object references or native container handles.

`CombatDamage` projection:

- `SubjectHash` = target hash.
- `ContextHash` = damage type.
- `SourceHash` = source hash.
- `RelativePosition` = damage point relative to current player runtime position.
- `Scalar0` = magnitude.
- `Scalar1` = integrity delta.

`WeatherChanged` projection:

- `SubjectHash` = current weather hash.
- `ContextHash` = previous weather hash.
- `Scalar0` = clamped strength.
- `Scalar1` = non-negative flow-field scale.
- `QualityTier` = mod-facing legacy projection derived from `GlobalQualityWeight`; it is not engine authority.

Budget-capped samples set `ModEventDto.LowTierSampleFlag`. The flag means the bridge emitted a reduced cosmetic sample under continuous budget pressure; it must not change gameplay truth.

Projected event enqueue and dispatch budgets use a continuous cap curve: `round(lerp(10,50,smoothstep(saturate(GlobalQualityWeight01))))`, clamped to `10..50`. This may scale callback cadence/fidelity only; it must not change event DTO layout, save identity, or gameplay authority.

## Native Byte Events

`SubscribeNative` exposes immutable payload bytes for:

- `HectonNativeEventKind.Interaction`
- `HectonNativeEventKind.Crafting`

The span is valid only during the callback. Mods must copy only small data they own and must not store the span.

Full subscription audit: [Event_Subscription_Audit_Matrix.md](Event_Subscription_Audit_Matrix.md) records public event methods, native event kinds, projected event kinds, bridge lanes, callback watchdog limits, dispatch recursion cap, and subscription lifetime rules.

Event lifetime rules:

- Every subscription returns `HectonEventSubscription`.
- Tokens must be disposed from `IHectonMod.OnUnload`.
- Direct `HectonEventSubscription.Dispose()` validates active mod ownership for mod-owned tokens.
- `HectonAPI.Events.Unsubscribe` is only an owner-checked `Dispose` convenience wrapper.
- `DisableManagedMod` isolates native, unmanaged, and projected subscribers by subscriber id.
- Dispatch recursion is capped at `5` and callback stalls are watched at `2.0 ms`.

## Command Writes

Mods request writes. They do not mutate simulation truth. In the active envelope-only boundary, requests are fixed 64-byte `FutureCommandEnvelope` packets; legacy command APIs remain listed for source-audit continuity only.

Current allowed command API:

- `RequestFuture(in FutureCommandEnvelope envelope)`

`RequestFuture` is a mod-owned write request, not an anonymous packet pipe. The active execution scope must exist, and `FutureCommandEnvelope.ModderSignature` must equal `ModExecutionScope.CurrentModHash`; package loader and editor bulk routes use internal validator paths instead.

The sandbox validator, bulk stream ingress, external queue drain, tuning, telemetry snapshot, opcode gate, approved-asset registration, CSV reload routes, direct command dispatcher helpers, event publication hooks, and legacy managed command kernels are first-party/editor tooling only. Mods do not call `FutureCommandSandboxValidator`, `MockModQueue`, `FutureCommandSandboxConstants`, `ModCommandDispatcher`, `HectonModHooks`, or `IModCommandKernel` directly, and they never receive a `MockModQueue` `NativeQueue` handle. The public binary size fact is `FutureCommandEnvelope.SizeBytes`; sandbox budgets and fault hashes are not SDK constants.

Legacy command APIs, currently quarantined:

- `Request(in ModCommand command)`
- `RequestAup(in ModAupCommand command)`
- `RequestRenderInstance(in ModRenderInstanceCommand command)`

These legacy facades still require active `ModExecutionScope` before returning the quarantine `false` result. Anonymous calls must fail fast instead of silently probing command availability.

## SDK Authoring Contract

The SDK must hide the binary envelope from casual creators without weakening the runtime boundary.

Required SDK surfaces:

- Unity Editor SDK Hub plus External Starter Kit Workbench as the current project-integrated entry points for local authoring support;
- project/workspace creator;
- manifest editor that emits `RequiredAPIVersion` and `ModPriority`;
- capability selection mapped to opcode families;
- command graph or preset authoring that proves max envelopes per frame;
- graph compiler must reject reserved future kernels and aliases unless their hash appears in `allowed_opcodes.csv` and the static validator proves the runtime allowlist matches; `TriggerSubtitleCue` and `SubtitleCue` are reserved subtitle aliases and are not runtime-allowed command opcodes or editor-injectable inspector opcodes.
- CRC asset importer and approved asset manifest writer;
- local 300-frame sandbox simulator with quality, thermal, rollback, quota, and rejection modeling;
- envelope inspector for advanced authors;
- package validator and CLI packer;
- readable rejection reports.

Forbidden SDK promises:

- "drop a DLL into Mods and run it";
- "name a DLL like an engine assembly to access internal APIs";
- "patch any game method";
- "subscribe to any engine event";
- "load any bundle directly";
- "get a GameObject";
- "write player oxygen/inventory/save truth directly";
- "ignore low-end thermal throttling".

The SDK can offer friendly APIs in editor scripts, CLI tools, or generated packers. Those APIs must compile to package metadata, `.h8bin` tables, approved asset manifests, and `FutureCommandEnvelope` streams. The current source-backed Unity Editor entry points are `Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs` and `Assets/_Project/Scripts/Editor/ModdingSDK/ExternalStarterKitWorkbenchWindow.cs`; the Hub prioritizes External Starter Kit creation/opening, links the core docs, opens the local Mods folder, launches the static validator asynchronously, shows failed static validation as Editor error UI, and gates `ModBuilderWindow` as internal legacy tooling, while the Workbench reuses the Hub starter generator, shows required-file health, shows a graph contract preview for invalid opcode/duplicate ID/budget mistakes, shows authoring data preview for settings/locale mistakes, generates graph/settings/locale snippets through no-Unity helpers, requires/opens the root `h8mod.ps1` launcher, review manifest freshness, opens the core contracts, and runs the existing starter-kit identity, prepare, direct structure-validator, opcode-list, snippet, and review-summary routes asynchronously from one screen with nonzero tool exits shown as Editor error UI. The runtime still validates every envelope and may reject packets under quality, thermal, rollback, or quota pressure.

Current accepted opcodes:

- `SpawnDebris`
- `ApplyHeat`
- `RaycastQuery`
- `SpawnEffect`
- `MoveEntity`
- `VoxelModify`
- `FlowQuery`
- `AcousticPing`

Full command audit: [Command_Audit_Matrix.md](Command_Audit_Matrix.md) records opcode values, valid targets, AUP requirement, rejection reasons, command caps, result payloads, and the non-opcode render instance lane.

Limits from source:

- Future envelope size: 64 bytes (`FutureCommandEnvelope.SizeBytes`).
- Command queue capacity: 4096.
- Late-frame drain: 256.
- Per-mod per-tick commands: 128.
- Raycasts: 128.
- Render instances: 1024.
- Mod heap quota: 16 MB total, 1 MB per frame.
- Voxel modify radius: 8 meters.

Rejected commands publish unmanaged result payloads such as `ModInteractionRejectedPayload`, `ModRaycastResultPayload`, `ModCriticalMemoryEvictionPayload`, or `ModAupResponse`.

## Security Audit

Blocked direct signal families:

| Family | Examples | Reason | Wrapper requirement |
|---|---|---|---|
| AUP/origin shift | `AupShiftSignal`, `RebaseSignal`, `MemoryAddressShiftSignal` | Can desynchronize coordinate authority or stale native handles. | Read-only rebased DTOs only. |
| DataVault/streaming/save | `SectorHydratedSignal`, `StorageDebtSignal`, `SaveRequestSignal`, WFC signals | Can expose native handles, file offsets, save lifecycle, or sector identity. | Redacted status DTOs with no handles or offsets. |
| Player/survival/input | `InputStateSignal`, `PlayerStateSignal`, `PhysiologyStateSignal`, `HypoxiaSignal` | Enables input spoofing, inventory duplication, or survival corruption. | Read-only redacted DTO plus engine-owned command kernels. |
| High-volume simulation | `WakeGeneratedSignal`, `FluidImpulseSignal`, `RigidbodySleepSignal` | Callback storm risk. | Sampled projection with tier cap. |
| Presentation internals | `CameraFrustumSignal`, `SubmarineLightsChangedSignal`, `CullingOverloadSignal` | No stable gameplay contract. | Visual-only sampled DTO if approved. |

## Cheat Mod Spec: Infinite O2

Current runtime status: true Infinite O2 is not available through the public mod API. That is correct. Direct survival mutation would bypass player physiology ownership and save truth.

Full sample artifact: [Sample_InfiniteO2_Mod.md](Sample_InfiniteO2_Mod.md) records the manifest, managed entry pattern, forbidden accesses, and required future survival command kernel.

Safe design:

1. Register a boolean setting named `infinite_o2`.
2. Persist only that setting through `HectonAPI.SaveState`.
3. Listen to read-only public projections for context.
4. When an approved survival command kernel exists, submit a bounded TTL command. The player survival owner applies or rejects it.
5. Handle rejection payloads and disable UI claims when rejected.

Specification sketch:

```csharp
using Hecton8.Modding;

public sealed class InfiniteO2Mod : IHectonMod
{
    private bool _enabled;
    private HectonEventSubscription _projectionSub;
    private HectonEventSubscription _rejectSub;

    public void OnLoad()
    {
        _enabled = HectonAPI.SaveState.GetModString("com.example.infinite_o2.enabled", "0") == "1";
        HectonAPI.UI.RegisterSetting("com.example.infinite_o2", "infinite_o2", _enabled, OnToggle);
        _projectionSub = HectonAPI.Events.SubscribeProjected(OnProjectedEvent, "com.example.infinite_o2");
        _rejectSub = HectonAPI.Events.Subscribe<ModInteractionRejectedPayload>(OnRejected, "com.example.infinite_o2");
    }

    public void OnInitialize()
    {
    }

    public void OnUnload()
    {
        _projectionSub?.Dispose();
        _rejectSub?.Dispose();
    }

    private void OnToggle(bool enabled)
    {
        _enabled = enabled;
        HectonAPI.SaveState.SetModString("com.example.infinite_o2.enabled", enabled ? "1" : "0");
    }

    private void OnProjectedEvent(ModEventDto dto)
    {
        if (!_enabled)
            return;

        // Current API has no SurvivalOverride opcode. This request is a required future kernel,
        // not a direct write to player physiology or DataVault.
        // HectonAPI.Commands.Request(in survivalOverrideCommand);
    }

    private void OnRejected(in ModInteractionRejectedPayload payload)
    {
        // Disable visible cheat status if the engine rejects the request.
    }
}
```

Required future kernel before this cheat can affect gameplay:

- `ModCommandOpcode.SurvivalOverride`
- target system `PlayerSurvival`
- max TTL 3 seconds
- clamps oxygen floor in engine code
- not serialized into first-party save truth
- telemetry on every accepted/rejected request
- revocation on mod unload or quarantine

Reservation status: this is not a public opcode. The reservation and payload boundary are tracked in
[Future_Command_Kernel_Reservations.md](Future_Command_Kernel_Reservations.md). Do not add this enum
or target without the PlayerSurvival owner, rejection telemetry, save-exclusion proof, and the full
mod static/runtime verification chain.

## Why Unmanaged Structs, Not JSON

Unmanaged structs are mandatory because the mod bridge crosses native queues, Burst-facing projections, and save-adjacent command results. Fixed payloads provide:

- predictable byte layout
- no string event names
- no JSON parsing
- no per-event heap allocations
- direct NativeQueue and Burst compatibility
- numeric event hashes for telemetry and schema versioning
- bounded payload sizes for continuous quality-budget throttling

JSON remains acceptable only for cold mod configuration or mod-owned save text under `HectonAPI.SaveState`. It is not an event transport.

## Public Facade Matrix

The public facade is the implementation boundary. Anything internal or first-party-only is not a mod right.

Full facade audit: [API_Surface_Audit_Matrix.md](API_Surface_Audit_Matrix.md) records the current `HectonAPI.cs` public nested surfaces, public methods, public properties, and internal forbidden methods. Loader diagnostics remain internal: `ModRuntimeInfo` and its package-path/status members are engine UI descriptors, not SDK DTOs.
Resource/content audit: [Resource_Content_Audit_Matrix.md](Resource_Content_Audit_Matrix.md) records hash-only resource resolution, cold content registration, registry capacities, raw texture caps, and forbidden Unity object returns.

| Surface | Public methods | Classification | Hard rule |
|---|---|---|---|
| `HectonAPI.Events` | `Subscribe<TPayload>`, `SubscribeNative`, `SubscribeProjected`, `OnPlayerSpawned`, `OnBiomeChanged`, `Unsubscribe`, `Publish<TPayload>` | unmanaged event/read-only projection/mod-owned payload | Active mod scope required before envelope-only quarantine; `Publish<TPayload>` rejects engine-owned payload types when managed events are reopened; no direct first-party `SignalBus<T>`, `HectonEventBus`, `HectonGameEvents`, or managed `HectonEvent` subscription for mods. `HectonEventBus` has no public static bus methods. |
| `HectonAPI.Input` | `GetButtonMask`, `HasButtonMask` | read-only frame mask | Active mod scope required; no Input System objects or action references. |
| `HectonAPI.Commands` | `RequestFuture`, `Request`, `RequestAup`, `RequestRenderInstance` | engine-validated write request | Active mod scope required for every command facade; `RequestFuture` also requires `ModderSignature` to match the active mod hash. |
| `HectonAPI.Resources` | `Proxy`, `TryResolvePrefab`, `TryResolveAudioClip`, `TryResolveTexture` | hash-only resource resolution | Active mod scope required for property and method routes; direct proxy methods use the same guard. No Unity asset reference leaves the engine. |
| `HectonAPI.Telemetry` | `Publish` | mod marker write | Active mod execution scope required through the shared facade guard; hash plus scalar only. |
| `HectonAPI.Items` | none public | internal forbidden ScriptableObject item accessors | No `ItemData` handle crosses the mod facade. |
| `HectonAPI.Crafting` | none public | internal forbidden owner override | Recycle yield overrides need content manifest ownership and unload revocation before public exposure. |
| `HectonAPI.Recycling` | none public | internal forbidden gameplay mutation | Direct recycling requests must use an engine-owned command route. |
| `HectonAPI.Construction` | none public | internal forbidden ScriptableObject buildable accessors | No `BuildableData` handle crosses the mod facade. |
| `HectonAPI.Ecosystem` | none public | internal forbidden owner overlay | Mutation overlays need mod ownership, unload revocation, and runtime proof before public exposure. |
| `HectonAPI.Localization` | `InjectBabelEnvelope` | rejected binary Babel envelope seam | Active mod execution scope required before rejection. Runtime dictionary/string localization injection is disabled. |
| `HectonAPI.UI` | `ShowInfo`, `ShowWarning`, `ShowCritical`, `RegisterSetting` | presentation/settings | Active mod scope required; setting `modId` must match the active scope. UI must reflect engine acceptance, not assumed command success. |
| `HectonAPI.World` | `IsGameReady`, `TryGetPlayerEntityHash` | read-only hash state | Active mod scope required for readiness and player hash lookup; `GameObject`, `Transform`, spawn, and despawn methods are internal and throw. |
| `HectonAPI.SaveState` | `SetModString`, `GetModString` | mod-owned cold save text | Active mod execution scope required. Store rejects scope-less mod payload access; engine-owned internal payloads use explicit `hecton.internal.` keys. JSON allowed here only, never as event transport. |

`HectonAPI.Assets.LoadPrefab`, `LoadAudioClip`, `LoadTexture`, `HectonAPI.Items.RegisterCustomItem`, `TryFindItem`, `HectonAPI.Crafting.RegisterRecipe`, `RegisterRecycleYield`, `HectonAPI.Recycling.ProcessRecycle`, `HectonAPI.Construction.RegisterBuildable`, `TryFindBuildable`, `HectonAPI.Ecosystem.RegisterBiomeMutation`, `HectonAPI.Mods.GetLoadedMods`, `ModRuntimeInfo`, `ModLoadStatus`, `ModRegistryEventType`, `ModRegistryEventPayload`, `IModRegistryEventListener`, `ModSettingKind`, `ModSettingView`, FutureCommand output signal DTOs, `FutureCommandSandboxValidator`, `ModCommandDispatcher`, `HectonModHooks`, and `IModCommandKernel` are internal. Public engine MonoBehaviours that need registry invalidation use private adapter classes and do not expose `IModRegistryEventListener` in public base lists. The object/authority paths throw `IllegalContractException`; loader diagnostics, registry invalidation, command dispatch helpers, and menu setting snapshots are engine UI/tooling only. Modders must resolve hashes and submit `FutureCommandEnvelope` packets instead of writing SignalBus lanes.

## Resource And Content Boundary

Mods may register cold content overlays and resolve resource hashes. They may not receive live Unity asset references.

Current source-backed resource/content limits:

| Contract | Value | Rule |
|---|---:|---|
| Public resource methods | `3` | `TryResolvePrefab`, `TryResolveAudioClip`, `TryResolveTexture`; hash ids only. |
| Resource kinds | `3` | `Prefab`, `AudioClip`, `Texture`. |
| Resource registry capacity | `256` | Engine owner resolves hash ids internally. |
| Resource owner proof | active scope id match | Registry rejects forged `modId` values before hashing. |
| Public content methods | `6` | Settings/localization/UI/plain DTO methods only. |
| Internal asset/object/authority/diagnostic methods | `18` | Public facade blocks direct Unity object loads, ScriptableObject handles, unowned gameplay overlays, and all-package loader diagnostics. |
| Raw PNG cap | `8388608` bytes / `2048` px | Cold fallback only; file size is checked before `File.ReadAllBytes` and read exceptions fail closed. |
| AssetBundle lookup fallback | exact asset name only | `AssetBundle.GetAllAssetNames()` suffix scan is forbidden; SDK/workbench must provide exact asset names or hash manifests. |

## Loader And Save Boundary

Loader/save contracts are part of the public mod API even though they are cold-path managed boundaries.

Full loader/save audit: [Loader_Save_Audit_Matrix.md](Loader_Save_Audit_Matrix.md) records manifest fields, manifest byte cap, bounded manifest discovery cap, bounded top-level package file discovery, `CurrentAPIVersion`, `IHectonMod` callbacks, runtime info fields, and mod save payload limits.

| Contract | Current source-backed value | Rule |
|---|---:|---|
| Manifest file | `mod.json` | Package discovery starts from this file. |
| Current API version | `2` | Mods requiring a newer version are disabled. |
| Manifest fields | `9` | `Id`, `Name`, `Version`, `Author`, `Dependencies`, `EntryAssembly`, `EntryType`, `RequiredAPIVersion`, `ModPriority`. |
| Manifest byte cap | `32768` bytes | Loader checks file size and rejects missing, empty, or oversized `mod.json` before JSON read/parse. |
| Manifest discovery cap | `64` manifests | Loader enumerates `mod.json` lazily and stops before candidate allocation can scale beyond the cap. |
| Canonical mod IDs | enforced | `Id` and dependencies use lowercase letters/digits separated by single `.`, `_`, or `-`; no path-ish separator-only ids, whitespace, or reserved filesystem device segments. |
| `IHectonMod` callbacks | `3` | `OnLoad`, `OnInitialize`, and `OnUnload`; dispose every subscription from `OnUnload`. |
| `ModMetadata` fields | `8` | Runtime diagnostics and dependency ordering use this descriptor. |
| `ModRuntimeInfo` fields | `7` | Internal engine UI descriptor; all-package loader diagnostics are not public mod API. |
| Reserved managed assembly identities | blocked | `Hecton8.*`, `Unity*`, `Assembly-CSharp`, `System`, `mscorlib`, and `netstandard` are engine/runtime assembly names, not mod package names. |
| Package DLL identity scan | max `32` top-level `.dll` files | Loader identity-validates every accepted top-level package DLL; packages above the cap are disabled before load. SDK builder caps selected DLLs at `32`, rejects duplicate output names, and deletes stale output DLLs through bounded cleanup before writing the new manifest. |
| Legacy bundle discovery cap | `4` top-level `.bundle` files | Legacy non-envelope fallback is bounded and only accepts the single-bundle case. |
| Legacy localization discovery cap | `16` top-level `lang_*.json` files | Legacy non-envelope fallback is bounded; envelope-only runtime does not ingest localization files. |
| EntryAssembly file name only | enforced | Explicit managed entry assembly names are package-local `.dll` file names only, not absolute or relative paths. |
| Scope owner proof | enforced | Active `ModExecutionScope` cannot be anonymous/blank and must carry a non-zero owner hash before public facade guards pass. |
| Event subscriber owner proof | enforced | Unmanaged, native, and projected event bridge routes plus private channel implementations reject anonymous subscribers before creating `HectonEventSubscription` tokens. |
| SaveState public methods | `2` | `SetModString` and `GetModString`; active `ModExecutionScope` required. |
| SaveState store owner proof | enforced | Public mod payloads require active scope; engine payloads use explicit `SetEngineString` / `GetEngineString` with `hecton.internal.` keys. |
| Save storage prefix | `m8v1:` | Mod-owned keys are hashed/namespaced before persistence. |
| Max MMF mod payload | `16352` bytes | Protected block `16384` minus `32` byte mod payload header. |

`HectonAPI.SaveState` is not a general persistence escape hatch. Mods may store their own text payloads; they may not write first-party save owners, inventory truth, player physiology, world sectors, or DataVault-backed state. The backing store must not create a mod owner from an arbitrary key when no active mod scope exists.

SDK builder parity: `Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs` is internal legacy tooling. It serializes the full loader-required manifest shape, including `RequiredAPIVersion` and `ModPriority`, rejects non-canonical mod/dependency ids, writes the canonical trimmed mod id to the output path and manifest, caps selected managed DLLs at the loader's 32-DLL package cap, rejects duplicate selected DLL file names, rejects selected managed DLLs with reserved engine/runtime assembly identities during Build Internal Legacy Package deep validation, enumerates selected bundle build assets with a 512 asset cap, defers deep asset and DLL metadata scans out of `OnGUI`, and removes stale top-level DLLs through bounded cleanup. `ModLoader` enumerates discovered manifests lazily with a 64-manifest cap, rejects missing, empty, or `>32768` byte manifests before `File.ReadAllText`, caps top-level DLL discovery at 32 with fail-closed package disable, and caps legacy bundle/localization discovery at 4/16. `Validate_Mod_API_Static.ps1` extracts source structs and fails if the SDK hub, builder manifest, builder asset cap, builder DLL cap, shallow UI validation, bounded stale-DLL cleanup, discovery caps, byte cap, id validation, or reserved-identity gate drifts from `ModLoader`. This is static source proof only; Unity package smoke remains required before claiming runtime verification.

## Payload Layouts

Implementation-facing field contracts:

Full payload audit: [Payload_Layout_Audit_Matrix.md](Payload_Layout_Audit_Matrix.md) records fixed sizes, `FutureCommandEnvelope` offsets, `ModEventDto` offsets, event hash constants, and legacy command/result payload fields.

| Payload | Layout | Size | Use |
|---|---|---:|---|
| `ModEventDto` | explicit | 64 bytes | Projected `CombatDamage` and `WeatherChanged` event metadata. |
| `FutureCommandEnvelope` | explicit | 64 bytes | Active UGC runtime packet; opcode hash, mod signature, AUP, payload lanes, integrity hash, and padding. |
| `ModCommand` | explicit | 64 bytes | Dormant legacy command packet; `Payload0` overlays `ModHash` low 32 bits and `RequestId` high 32 bits. |
| `ModPlayerSpawnedEvent` | explicit | 24 bytes | Read-only player spawn event payload; fixed offsets keep managed callback layout stable. |
| `ModBiomeChangedEvent` | explicit | 24 bytes | Read-only biome transition event payload; explicit padding keeps size 8-byte aligned. |
| `ModAupCommand` | explicit | 120 bytes | Position-changing command wrapper; dispatcher rebases AUP at drain time. |
| `ModAupResponse` | explicit | 64 bytes | Async response for flow, voxel, and acoustic AUP requests. |
| `ModRenderInstanceCommand` | explicit | 80 bytes | One mod instancing matrix request. |
| `ModRaycastResultPayload` | explicit | 48 bytes | Next-frame result for proxied mod raycast requests. |
| `ModInteractionRejectedPayload` | explicit | 16 bytes | Security gate rejection reason; legacy opcode fields overlay `OpcodeHash`. |
| `ModCriticalMemoryEvictionPayload` | explicit | 24 bytes | Heap quota eviction warning before quarantine. |

`ModEventDto` byte offsets are fixed in source: `EventHash` 0, `SubjectHash` 4, `ContextHash` 8, `SourceHash` 12, `Frame` 16, `RelativePosition` 20, `Direction` 32, `Scalar0` 44, `Scalar1` 48, `Kind` 52, `Flags` 54, `QualityTier` 56, `Reserved0` 57, `Sequence` 58, `Reserved1` 60. The `QualityTier` byte is an API compatibility field. New engine code must use continuous `GlobalQualityWeight` and may only write this byte as a deterministic projection for old mod readers.

## Signal Extension Gate

Adding another mod-visible signal is not a documentation-only change. Required gate:

1. Add one `Signal_Schema.json` entry with source payload, size/capacity, event hash, field projection, and security notes.
2. Add a projection job or copy bridge that clamps non-finite floats and never exposes Unity objects or native handles.
3. State the continuous `GlobalQualityWeight` cap curve, projection rule for any legacy tier byte, and overflow telemetry.
4. Add a 300-frame blackbox path for cull/overflow or explicitly attach to an existing one.
5. Run Unity callback smoke tests and record GCMonitor/profiler evidence before changing status from `PENDING RUNTIME VERIFICATION`.

Adding another command opcode requires an engine-owned `IModCommandKernel`, target validation, rejection reason, unmanaged response path when applicable, and quota accounting.

## Change Control

Required checklist: [Change_Control_Checklist.md](Change_Control_Checklist.md).

Every source or contract edit must update the matching audit matrix, this spec, the schema, the runtime playbook, and the static validator when needed. Schema-only and Markdown-only expansions are invalid.

## Acceptance Tests

Static checks already required for this package:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1
Get-Content -Raw Docs/Modding/Signal_Schema.json | ConvertFrom-Json
rg --pcre2 -n "[^\x00-\x7F]" Docs/Modding Docs/Tasks/Status_MODDING_API_SCHEMA_BUILDER.md Docs/AgentLogs/Rationale_MODDING_API_SCHEMA_BUILDER.md Docs/AgentLogs/LOG_MODDING_API_SCHEMA_BUILDER.md
git diff --check -- Docs/Modding/Signal_Schema.json Docs/Modding/Mod_API_Specification.md Docs/Modding/Signal_Audit_Matrix.md Docs/Modding/Command_Audit_Matrix.md Docs/Modding/API_Surface_Audit_Matrix.md Docs/Modding/Payload_Layout_Audit_Matrix.md Docs/Modding/Loader_Save_Audit_Matrix.md Docs/Modding/Event_Subscription_Audit_Matrix.md Docs/Modding/Change_Control_Checklist.md Docs/Modding/Runtime_Verification_Playbook.md Docs/Modding/Validate_Mod_API_Static.ps1 Docs/Tasks/Status_MODDING_API_SCHEMA_BUILDER.md Docs/AgentLogs/Rationale_MODDING_API_SCHEMA_BUILDER.md Docs/AgentLogs/LOG_MODDING_API_SCHEMA_BUILDER.md
```

`Validate_Mod_API_Static.ps1` is the static drift gate. It fails when the source `ISignal` count, schema inventory, projection bridge lanes, command opcodes, facade shape, event subscription contracts, internal-only `HectonGameEvents` payload rule, payload byte layout, loader/save contracts, audit matrices, or runtime verification gate drift apart.

Runtime checks required before a future `VERIFIED` status:

1. Load a dummy mod implementing `IHectonVersionedMod` with `RequiredAPIVersion = 2`.
2. Subscribe to `SubscribeProjected`, `SubscribeNative`, `OnPlayerSpawned`, and `OnBiomeChanged`; dispose all tokens in `OnUnload`.
3. Force one combat damage event and one weather event; verify only `CDMG` and `WEAT` reach `ModEventDto`.
4. Submit valid and invalid `RequestAup` raycasts; verify `ModRaycastResultPayload` and `ModInteractionRejectedPayload`.
5. Spam more than 128 commands from one mod in one tick; verify command flood rejection, no crash, and no unbounded callback fanout.
6. Allocate past the mod heap quota in a controlled test mod; verify `ModCriticalMemoryEvictionPayload` then quarantine.
7. Confirm GCMonitor hot-path output is 0 B/frame for projection dispatch under the cap.

## Verification Boundary

This pass did not run Unity, Play Mode, GCMonitor, profiler, player build, or mod callback smoke tests. Current status is source/doc defined, runtime pending.

The required runtime proof path is [Runtime_Verification_Playbook.md](Runtime_Verification_Playbook.md). Do not mark the mod API `VERIFIED` until that playbook passes with Unity Console, GCMonitor, and profiler evidence.

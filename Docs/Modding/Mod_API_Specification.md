# HECTON-8 Mod API Specification

Date: 2026-05-19
Status: ENVELOPE-ONLY MOD API SPEC / STATIC DOC UPDATE / PENDING RUNTIME VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence class: STATIC_SOURCE / STATIC_DOC  
Owner prompt: MODDING_API_SCHEMA_BUILDER  
Companion schema: `Docs/Modding/Signal_Schema.json`

## 2026-05-19 Current Runtime Authority

This specification contains older source-audit material for the managed mod API. The active runtime boundary is now narrower:

- `HectonAPI.Commands.RequestFuture(in FutureCommandEnvelope envelope)` is the only current command ingress for UGC.
- `HectonAPI.Commands.Request`, `RequestAup`, and `RequestRenderInstance` return `false` while envelope-only mode is enforced.
- Managed mod entry points, managed factories, projected managed events, direct resource proxy registration, filesystem content discovery, localization file injection, and direct asset loading are quarantined in envelope-only mode.
- Runtime UGC packages must not rely on `.dll`, `.bundle`, `lang_*.json`, raw PNG, prefab, material, mesh, texture, audio clip, `GameObject`, `Transform`, `NativeArray`, `NativeQueue`, `GlobalDataVault`, or first-party `SignalBus<T>` access.
- Assets are referenced by approved hashes and CRC-checked envelope opcodes, not by loose files or Unity object handles.
- Human-friendly modder work happens in the SDK authoring layer described in [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md).

Any section below that describes `IHectonMod` callbacks, `SubscribeProjected`, `SubscribeNative`, cold content overlays, resource proxy resolution, or legacy command lanes is legacy/source-audit context until the runtime owner explicitly re-enables it and the verification playbook is rewritten around the envelope-only boundary.

## Source Reality

The historical public mod event surface was not direct access to first-party simulation lanes. In the current envelope-only authority, this managed event surface is quarantined for runtime UGC.

Legacy source-backed mod surfaces:

- `HectonAPI.Events.SubscribeProjected(Action<ModEventDto>)` for selected `SignalBus<T>` projections.
- `HectonAPI.Events.SubscribeNative(HectonNativeEventHandler)` for immutable byte copies from approved NativeQueue event lanes.
- `HectonAPI.Events.Subscribe<TPayload>(HectonUnmanagedEventHandler<TPayload>)` for unmanaged mod-facing payloads.
- `HectonAPI.Commands.RequestFuture`, `Request`, `RequestAup`, and `RequestRenderInstance` for engine-validated writes.
- `HectonAPI.SaveState` for mod-owned save payloads.

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

`InteractionEvents` and `CraftingEvents` are also exposed, but they are not `SignalBus<T>` projections. They are copied into `SubscribeNative` as immutable bytes for the callback duration.

Full source audit: [Signal_Audit_Matrix.md](Signal_Audit_Matrix.md) records 160 current `ISignal` structs in `GlobalSignals.cs`. Only 2 are projected for mods. The remaining 158 are denied by default.

R21 static closure: `Signal_Schema.json` schema revision `14` records the `160 / 2 / 158` signal split in both the source inventory and `staticValidation.lastStaticValidationSnapshot`, with `runtimeProof` set to `PENDING_VERIFICATION`. `Validate_Mod_API_Static.ps1` fails if that static snapshot block drifts behind the source inventory again. This is still static source/doc evidence only, not Unity runtime verification.

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
- `QualityTier` = source quality tier.

Low-tier samples set `ModEventDto.LowTierSampleFlag`.

## Native Byte Events

`SubscribeNative` exposes immutable payload bytes for:

- `HectonNativeEventKind.Interaction`
- `HectonNativeEventKind.Crafting`

The span is valid only during the callback. Mods must copy only small data they own and must not store the span.

Full subscription audit: [Event_Subscription_Audit_Matrix.md](Event_Subscription_Audit_Matrix.md) records public event methods, native event kinds, projected event kinds, bridge lanes, callback watchdog limits, dispatch recursion cap, and subscription lifetime rules.

Event lifetime rules:

- Every subscription returns `HectonEventSubscription`.
- Tokens must be disposed from `IHectonMod.OnUnload`.
- `HectonAPI.Events.Unsubscribe` is only a `Dispose` convenience wrapper.
- `DisableManagedMod` isolates native, unmanaged, and projected subscribers by subscriber id.
- Dispatch recursion is capped at `5` and callback stalls are watched at `2.0 ms`.

## Command Writes

Mods request writes. They do not mutate simulation truth. In the active envelope-only boundary, requests are fixed 64-byte `FutureCommandEnvelope` packets; legacy command APIs remain listed for source-audit continuity only.

Current allowed command API:

- `RequestFuture(in FutureCommandEnvelope envelope)`

Legacy command APIs, currently quarantined:

- `Request(in ModCommand command)`
- `RequestAup(in ModAupCommand command)`
- `RequestRenderInstance(in ModRenderInstanceCommand command)`

## SDK Authoring Contract

The SDK must hide the binary envelope from casual creators without weakening the runtime boundary.

Required SDK surfaces:

- project/workspace creator;
- manifest editor that emits `RequiredAPIVersion` and `ModPriority`;
- capability selection mapped to opcode families;
- command graph or preset authoring that proves max envelopes per frame;
- CRC asset importer and approved asset manifest writer;
- local 300-frame sandbox simulator with quality, thermal, rollback, quota, and rejection modeling;
- envelope inspector for advanced authors;
- package validator and CLI packer;
- readable rejection reports.

Forbidden SDK promises:

- "drop a DLL into Mods and run it";
- "patch any game method";
- "subscribe to any engine event";
- "load any bundle directly";
- "get a GameObject";
- "write player oxygen/inventory/save truth directly";
- "ignore low-end thermal throttling".

The SDK can offer friendly APIs in editor scripts, CLI tools, or generated packers. Those APIs must compile to package metadata, `.h8bin` tables, approved asset manifests, and `FutureCommandEnvelope` streams. The runtime still validates every envelope and may reject packets under quality, thermal, rollback, or quota pressure.

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
- bounded payload sizes for low-tier throttling

JSON remains acceptable only for cold mod configuration or mod-owned save text under `HectonAPI.SaveState`. It is not an event transport.

## Public Facade Matrix

The public facade is the implementation boundary. Anything internal or first-party-only is not a mod right.

Full facade audit: [API_Surface_Audit_Matrix.md](API_Surface_Audit_Matrix.md) records the current `HectonAPI.cs` public nested surfaces, public methods, public properties, and internal forbidden methods.
Resource/content audit: [Resource_Content_Audit_Matrix.md](Resource_Content_Audit_Matrix.md) records hash-only resource resolution, cold content registration, registry capacities, raw texture caps, and forbidden Unity object returns.

| Surface | Public methods | Classification | Hard rule |
|---|---|---|---|
| `HectonAPI.Events` | `Subscribe<TPayload>`, `SubscribeNative`, `SubscribeProjected`, `OnPlayerSpawned`, `OnBiomeChanged`, `Unsubscribe`, `Publish<TPayload>` | unmanaged event/read-only projection/mod-owned payload | No direct first-party `SignalBus<T>` or managed `HectonEvent` subscription for mods. |
| `HectonAPI.Input` | `GetButtonMask`, `HasButtonMask` | read-only frame mask | No Input System objects or action references. |
| `HectonAPI.Commands` | `RequestFuture`, `Request`, `RequestAup`, `RequestRenderInstance` | engine-validated write request | Mods request; first-party kernels execute or reject. |
| `HectonAPI.Resources` | `Proxy`, `TryResolvePrefab`, `TryResolveAudioClip`, `TryResolveTexture` | hash-only resource resolution | No Unity asset reference leaves the engine. |
| `HectonAPI.Telemetry` | `Publish` | mod marker write | Active mod execution scope required; hash plus scalar only. |
| `HectonAPI.Items` | `RegisterCustomItem`, `TryFindItem` | cold catalog overlay | Runtime overlay only; no authored asset mutation. |
| `HectonAPI.Crafting` | `RegisterRecipe`, `RegisterRecycleYield` | cold recipe overlay | Managed lists are cold registration data, not event payloads. |
| `HectonAPI.Recycling` | `ProcessRecycle` | owner-arbitrated gameplay request | Official `ScrapManager` owns inventory mutation. |
| `HectonAPI.Construction` | `RegisterBuildable`, `TryFindBuildable` | cold buildable overlay | Catalog injection is not scene spawning. |
| `HectonAPI.Ecosystem` | `RegisterBiomeMutation` | deterministic overlay | Mods provide bias data, not fauna handles. |
| `HectonAPI.Localization` | `InjectTable` | cold localization overlay | Dictionary/string use is cold only. |
| `HectonAPI.UI` | `ShowInfo`, `ShowWarning`, `ShowCritical`, `RegisterSetting` | presentation/settings | UI must reflect engine acceptance, not assumed command success. |
| `HectonAPI.World` | `IsGameReady`, `TryGetPlayerEntityHash` | read-only hash state | `GameObject`, `Transform`, spawn, and despawn methods are internal and throw. |
| `HectonAPI.SaveState` | `SetModString`, `GetModString` | mod-owned cold save text | JSON allowed here only, never as event transport. |
| `HectonAPI.Mods` | `GetLoadedMods` | diagnostics copy | Caller provides destination list. |

`HectonAPI.Assets.LoadPrefab`, `LoadAudioClip`, and `LoadTexture` are internal and throw `IllegalContractException`. Modders must resolve hashes and submit commands.

## Resource And Content Boundary

Mods may register cold content overlays and resolve resource hashes. They may not receive live Unity asset references.

Current source-backed resource/content limits:

| Contract | Value | Rule |
|---|---:|---|
| Public resource methods | `3` | `TryResolvePrefab`, `TryResolveAudioClip`, `TryResolveTexture`; hash ids only. |
| Resource kinds | `3` | `Prefab`, `AudioClip`, `Texture`. |
| Resource registry capacity | `256` | Engine owner resolves hash ids internally. |
| Public content methods | `14` | Cold catalog/settings/localization/UI methods only. |
| Internal asset loaders | `3` | Public facade throws for direct Unity object loads. |
| Raw PNG cap | `8388608` bytes / `2048` px | Cold fallback only; not hot-path event transport. |

## Loader And Save Boundary

Loader/save contracts are part of the public mod API even though they are cold-path managed boundaries.

Full loader/save audit: [Loader_Save_Audit_Matrix.md](Loader_Save_Audit_Matrix.md) records manifest fields, `CurrentAPIVersion`, `IHectonMod` callbacks, runtime info fields, and mod save payload limits.

| Contract | Current source-backed value | Rule |
|---|---:|---|
| Manifest file | `mod.json` | Package discovery starts from this file. |
| Current API version | `2` | Mods requiring a newer version are disabled. |
| Manifest fields | `9` | `Id`, `Name`, `Version`, `Author`, `Dependencies`, `EntryAssembly`, `EntryType`, `RequiredAPIVersion`, `ModPriority`. |
| `IHectonMod` callbacks | `3` | `OnLoad`, `OnInitialize`, and `OnUnload`; dispose every subscription from `OnUnload`. |
| `ModMetadata` fields | `8` | Runtime diagnostics and dependency ordering use this descriptor. |
| `ModRuntimeInfo` fields | `7` | UI copies this descriptor; loader internals stay private. |
| SaveState public methods | `2` | `SetModString` and `GetModString`; active `ModExecutionScope` required. |
| Save storage prefix | `m8v1:` | Mod-owned keys are hashed/namespaced before persistence. |
| Max MMF mod payload | `16352` bytes | Protected block `16384` minus `32` byte mod payload header. |

`HectonAPI.SaveState` is not a general persistence escape hatch. Mods may store their own text payloads; they may not write first-party save owners, inventory truth, player physiology, world sectors, or DataVault-backed state.

SDK builder drift: `Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs` currently serializes only `Id`, `Name`, `Version`, `Author`, `Dependencies`, `EntryAssembly`, and `EntryType` into `mod.json`. Runtime loader source requires positive `RequiredAPIVersion` and consumes `ModPriority`; therefore a builder-created mod package is not proven loadable without manual manifest repair until the builder emits the full manifest or a Unity runtime smoke fixture proves a compatible fallback.

## Payload Layouts

Implementation-facing field contracts:

Full payload audit: [Payload_Layout_Audit_Matrix.md](Payload_Layout_Audit_Matrix.md) records fixed sizes, `FutureCommandEnvelope` offsets, `ModEventDto` offsets, event hash constants, and legacy command/result payload fields.

| Payload | Layout | Size | Use |
|---|---|---:|---|
| `ModEventDto` | explicit | 64 bytes | Projected `CombatDamage` and `WeatherChanged` event metadata. |
| `FutureCommandEnvelope` | explicit | 64 bytes | Active UGC runtime packet; opcode hash, mod signature, AUP, payload lanes, integrity hash, and padding. |
| `ModCommand` | explicit | 64 bytes | Dormant legacy command packet; `Payload0` overlays `ModHash` low 32 bits and `RequestId` high 32 bits. |
| `ModAupCommand` | sequential | source-defined | Position-changing command wrapper; dispatcher rebases AUP at drain time. |
| `ModAupResponse` | sequential | 64 bytes | Async response for flow, voxel, and acoustic AUP requests. |
| `ModRenderInstanceCommand` | sequential | source-defined | One mod instancing matrix request. |
| `ModRaycastResultPayload` | sequential | source-defined | Next-frame result for proxied mod raycast requests. |
| `ModInteractionRejectedPayload` | sequential | source-defined | Security gate rejection reason. |
| `ModCriticalMemoryEvictionPayload` | sequential | source-defined | Heap quota eviction warning before quarantine. |

`ModEventDto` byte offsets are fixed in source: `EventHash` 0, `SubjectHash` 4, `ContextHash` 8, `SourceHash` 12, `Frame` 16, `RelativePosition` 20, `Direction` 32, `Scalar0` 44, `Scalar1` 48, `Kind` 52, `Flags` 54, `QualityTier` 56, `Reserved0` 57, `Sequence` 58, `Reserved1` 60.

## Signal Extension Gate

Adding another mod-visible signal is not a documentation-only change. Required gate:

1. Add one `Signal_Schema.json` entry with source payload, size/capacity, event hash, field projection, and security notes.
2. Add a projection job or copy bridge that clamps non-finite floats and never exposes Unity objects or native handles.
3. State low-tier/high-tier caps and overflow telemetry.
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

`Validate_Mod_API_Static.ps1` is the static drift gate. It fails when the source `ISignal` count, schema inventory, projection bridge lanes, command opcodes, facade shape, event subscription contracts, payload byte layout, loader/save contracts, audit matrices, or runtime verification gate drift apart.

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

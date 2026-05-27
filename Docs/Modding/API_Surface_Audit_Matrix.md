# HECTON-8 Mod API Surface Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC SOURCE AUDIT / PENDING RUNTIME VERIFICATION

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Owner domain: Modding API static contract
Source file: `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
Companion schema: `Docs/Modding/Signal_Schema.json`

## 2026-05-19 Envelope-Only Override

The public facade inventory below records source shape, not current runtime permission. While envelope-only mode is active:

- `Commands.RequestFuture` is the active UGC ingress;
- FutureCommand output `SignalBus<T>` DTOs are internal first-party lanes, not SDK payload types;
- mod registry invalidation and mod menu setting snapshots are internal engine UI infrastructure, not SDK payload types;
- `Commands.Request`, `RequestAup`, and `RequestRenderInstance` are legacy/quarantined; they require active `ModExecutionScope`, then return `false`;
- `Events`, `Resources`, `Localization`, content overlay, and managed callback surfaces must not be treated as public runtime mod rights;
- SDK tools may expose friendly authoring APIs, but runtime packets still cross only as 64-byte `FutureCommandEnvelope` records.

Use [Mod_API_Sandbox_Quarantine.md](Mod_API_Sandbox_Quarantine.md) for runtime authority and [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md) for modder-facing tool design.

## Extraction Evidence

Source-backed facade facts:

- 15 public nested API surfaces under `HectonAPI`.
- 26 public static facade methods.
- 2 public static facade properties.
- 18 internal static methods exist in public/internal nested classes but are not public mod rights.

## Public Surfaces

| Surface | Public methods/properties | Classification | Rule |
|---|---|---|---|
| `Events` | `Subscribe`, `SubscribeNative`, `SubscribeProjected`, `OnPlayerSpawned`, `OnBiomeChanged`, `Unsubscribe`, `Publish` | unmanaged event/read-only projection/mod-owned payload | Active mod scope required before envelope-only quarantine; `Publish<TPayload>` rejects engine-owned payload types when managed events are reopened; no direct first-party `SignalBus<T>`, `HectonEventBus`, or managed `HectonEvent` subscription for mods. |
| `Input` | `GetButtonMask`, `HasButtonMask` | read-only frame mask | Active mod scope required; no Input System objects or action references. |
| `Commands` | `RequestFuture`, `Request`, `RequestAup`, `RequestRenderInstance` | engine-validated write request | Active mod scope required for every command facade; `RequestFuture` also requires matching `ModderSignature`; first-party kernels execute or reject. |
| `Resources` | `Proxy`, `TryResolvePrefab`, `TryResolveAudioClip`, `TryResolveTexture` | hash-only resource resolution | Active mod scope required for property and method routes; direct proxy methods use the same guard. No Unity asset reference leaves the engine. |
| `Telemetry` | `Publish` | mod marker write | Active mod execution scope required through the shared facade guard; hash plus scalar only. |
| `Items` | none public | internal forbidden ScriptableObject item accessors | No `ItemData` handle crosses the mod facade. |
| `Crafting` | none public | internal forbidden owner override | Recycle yield overrides require content manifest ownership and unload revocation. |
| `Recycling` | none public | internal forbidden gameplay mutation | Direct inventory mutation must use engine-owned command routes. |
| `Construction` | none public | internal forbidden ScriptableObject buildable accessors | No `BuildableData` handle crosses the mod facade. |
| `Ecosystem` | none public | internal forbidden owner overlay | Biome mutation overlays require mod ownership, unload revocation, and runtime proof. |
| `Assets` | none public | internal forbidden Unity object accessors | Direct Unity asset references are intentionally blocked. |
| `Localization` | `InjectBabelEnvelope` | rejected binary Babel envelope seam | Active mod execution scope required before rejection. Runtime dictionary/string localization injection is disabled. |
| `UI` | `ShowInfo`, `ShowWarning`, `ShowCritical`, `RegisterSetting` | presentation/settings | Active mod scope required; setting `modId` must match the active scope. |
| `World` | `IsGameReady`, `TryGetPlayerEntityHash` | read-only hash state | Active mod scope required for readiness and player hash lookup; no `GameObject`, `Transform`, spawn, or despawn access. |
| `SaveState` | `SetModString`, `GetModString` | mod-owned cold save text | Active mod execution scope required. The underlying store rejects scope-less mod payload access; engine-owned payloads use explicit `hecton.internal.` keys. JSON/text is allowed here only, never as hot-path event transport. |

## Public Method Inventory

```text
GetButtonMask
GetModString
HasButtonMask
InjectBabelEnvelope
OnBiomeChanged
OnPlayerSpawned
Publish
Publish
RegisterSetting
RegisterSetting
RequestFuture
Request
RequestAup
RequestRenderInstance
SetModString
ShowCritical
ShowInfo
ShowWarning
Subscribe
SubscribeNative
SubscribeProjected
TryGetPlayerEntityHash
TryResolveAudioClip
TryResolvePrefab
TryResolveTexture
Unsubscribe
```

## Public Property Inventory

```text
IsGameReady
Proxy
```

## Internal Forbidden Methods

These methods exist in public nested classes but are internal and throw or route first-party-only paths. They must not be documented as public mod API.

```text
DespawnPersistentInstance
GetLoadedMods
LoadAudioClip
LoadPrefab
LoadTexture
ProcessRecycle
Publish
RegisterBiomeMutation
RegisterBuildable
RegisterCustomItem
RegisterRecipe
RegisterRecycleYield
SpawnPersistentPrefab
Subscribe
TryFindBuildable
TryFindItem
TryGetPlayerObject
TryGetPlayerTransform
```

## Security Rules

- Public method presence is not enough to grant hot-path use. Classification still applies.
- `Commands.RequestFuture` rejects anonymous or forged signatures at the public facade before the internal sandbox validator sees the envelope.
- `Commands.Request`, `RequestAup`, and `RequestRenderInstance` reject anonymous calls before returning their legacy quarantine `false` result.
- `Events.Subscribe<TPayload>` is public only for unmanaged payloads and validates active mod ownership before envelope-only quarantine.
- Unmanaged, native, and projected event bridge routes reject anonymous subscribers before creating subscription tokens.
- `Events.Publish<TPayload>` is for mod-owned unmanaged coordination; it validates active mod ownership before envelope-only quarantine, rejects engine-owned payload types before `HectonEventBus.Publish` when managed events are reopened, and first-party gameplay authority still belongs to engine owners.
- `HectonEventBus` is internal first-party infrastructure with no public static bus member surface. Public mod event access must route through `HectonAPI.Events`.
- `HectonGameEvents` legacy managed payload classes and members are internal-only first-party infrastructure. Public mods must not receive `ItemData`, `BuildableData`, `HectonSurvivalSystem`, or survival record handles through managed event payloads.
- `HectonModHooks` is internal first-party event publication infrastructure, and its publication methods are internal-only. Public mods must not publish player/biome lifecycle events.
- `FutureCommandSandboxValidator` is internal first-party infrastructure and has no public static control-plane method surface. `MockModQueue` exposes no public queue handle or public instance control methods. Public mod command ingress must route through `HectonAPI.Commands.RequestFuture`.
- `ModCommandDispatcher` is internal first-party command infrastructure and has no public static member surface. Public mods must not call direct dispatcher helpers, legacy queue ingress, or float-packing helpers.
- `FutureCommandSandboxConstants` is internal first-party control-plane data. Public mods may use `FutureCommandEnvelope.SizeBytes` for packet layout only.
- `ModRuntimeInfo` is internal engine UI diagnostics and all members are internal-only. Public mods must not receive package paths, bundle paths, load status, or loader failure text as SDK DTO fields.
- FutureCommand output signal DTOs are internal first-party lanes. Public mods submit 64-byte envelopes, never direct `SignalBus<T>` writes.
- `ModRegistryEventType`, `ModRegistryEventPayload`, `IModRegistryEventListener`, `ModSettingKind`, and `ModSettingView` are internal engine UI/invalidation infrastructure. Public mods register settings through `HectonAPI.UI.RegisterSetting`, not by consuming menu DTOs or registry events.
- Public engine MonoBehaviours that consume internal mod registry invalidation use private adapter classes. They must not expose `IModRegistryEventListener` in public base lists.
- `IModCommandKernel` is internal legacy engine infrastructure. Public mods must not implement managed command kernels.
- `ModWorldPersistenceManager` and `GlobalRegistry.ModWorldPersistence` are internal engine save/spawn infrastructure. Persistent spawn/despawn remains command-kernel work, not a public service route.
- Public event subscription methods require active `ModExecutionScope`; explicit `subscriberId` must equal the active mod id.
- `Events.Unsubscribe` rejects tokens owned by a different active mod before delegating to `Dispose`.
- Direct `HectonEventSubscription.Dispose()` validates active mod ownership for mod-owned tokens before channel unsubscribe.
- `HectonAPI.Mods.GetLoadedMods` is internal diagnostics only. Public mods must not receive loader path/status lists for all packages.
- `Resources` returns hashes only.
- Public property routes are not anonymous diagnostics. `Resources.Proxy` and `World.IsGameReady` route through guarded accessors and require an active `ModExecutionScope`.
- Resource hash registration rejects forged owner ids; the registered `modId` must match the active `ModExecutionScope`.
- `World` returns hash state only; object/transform methods are internal forbidden methods.
- `SaveState` permits mod-owned text only in cold save/config paths.
- `ModSaveStateStore` must not synthesize public mod ownership from arbitrary keys; scope-less engine payloads use the explicit internal engine route only.
- UI and localization methods are managed/cold/presentation paths, not signal transport.

## Consistency Gate

If `HectonAPI.cs` adds, removes, renames, or changes visibility for a public nested surface, public method, public property, or internal forbidden Unity-object method, update this audit, `Signal_Schema.json`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Validate_Mod_API_Static.ps1` in the same change.

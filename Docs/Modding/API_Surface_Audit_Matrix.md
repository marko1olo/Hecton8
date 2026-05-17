# HECTON-8 Mod API Surface Audit Matrix

Date: 2026-05-17
Status: STATIC SOURCE AUDIT / PENDING RUNTIME VERIFICATION  
Owner prompt: MODDING_API_SCHEMA_BUILDER  
Source file: `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`  
Companion schema: `Docs/Modding/Signal_Schema.json`

## Extraction Evidence

Source-backed facade facts:

- 16 public nested API surfaces under `HectonAPI`.
- 34 public static facade methods.
- 2 public static facade properties.
- 9 internal static methods exist in public nested classes but are not public mod rights.

## Public Surfaces

| Surface | Public methods/properties | Classification | Rule |
|---|---|---|---|
| `Events` | `Subscribe`, `SubscribeNative`, `SubscribeProjected`, `OnPlayerSpawned`, `OnBiomeChanged`, `Unsubscribe`, `Publish` | unmanaged event/read-only projection/mod-owned payload | No direct first-party `SignalBus<T>` or managed `HectonEvent` subscription for mods. |
| `Input` | `GetButtonMask`, `HasButtonMask` | read-only frame mask | No Input System objects or action references. |
| `Commands` | `Request`, `RequestAup`, `RequestRenderInstance` | engine-validated write request | Mods request; first-party kernels execute or reject. |
| `Resources` | `Proxy`, `TryResolvePrefab`, `TryResolveAudioClip`, `TryResolveTexture` | hash-only resource resolution | No Unity asset reference leaves the engine. |
| `Telemetry` | `Publish` | mod marker write | Active mod execution scope required; hash plus scalar only. |
| `Items` | `RegisterCustomItem`, `TryFindItem` | cold catalog overlay | Runtime overlay only; no authored asset mutation. |
| `Crafting` | `RegisterRecipe`, `RegisterRecycleYield` | cold recipe overlay | Managed list use is cold registration data, not event payload transport. |
| `Recycling` | `ProcessRecycle` | owner-arbitrated gameplay request | Official recycling owner mutates inventory. |
| `Construction` | `RegisterBuildable`, `TryFindBuildable` | cold buildable overlay | Catalog injection is not scene spawning. |
| `Ecosystem` | `RegisterBiomeMutation` | deterministic overlay | Mods provide biome mutation data, not live fauna handles. |
| `Assets` | none public | internal forbidden Unity object accessors | Direct Unity asset references are intentionally blocked. |
| `Localization` | `InjectTable` | cold localization overlay | Dictionary/string use is cold only. |
| `UI` | `ShowInfo`, `ShowWarning`, `ShowCritical`, `RegisterSetting` | presentation/settings | UI must not imply unaccepted gameplay authority. |
| `World` | `IsGameReady`, `TryGetPlayerEntityHash` | read-only hash state | No `GameObject`, `Transform`, spawn, or despawn access. |
| `SaveState` | `SetModString`, `GetModString` | mod-owned cold save text | JSON/text is allowed here only, never as hot-path event transport. |
| `Mods` | `GetLoadedMods` | diagnostics copy | Caller owns destination list. |

## Public Method Inventory

```text
GetButtonMask
GetLoadedMods
GetModString
HasButtonMask
InjectTable
OnBiomeChanged
OnPlayerSpawned
ProcessRecycle
Publish
Publish
RegisterBiomeMutation
RegisterBuildable
RegisterCustomItem
RegisterRecipe
RegisterRecycleYield
RegisterSetting
RegisterSetting
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
TryFindBuildable
TryFindItem
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
LoadAudioClip
LoadPrefab
LoadTexture
Publish
SpawnPersistentPrefab
Subscribe
TryGetPlayerObject
TryGetPlayerTransform
```

## Security Rules

- Public method presence is not enough to grant hot-path use. Classification still applies.
- `Events.Subscribe<TPayload>` is public only for unmanaged payloads.
- `Events.Publish<TPayload>` is for mod-owned unmanaged coordination; first-party gameplay authority still belongs to engine owners.
- `Resources` returns hashes only.
- `World` returns hash state only; object/transform methods are internal forbidden methods.
- `SaveState` permits mod-owned text only in cold save/config paths.
- UI and localization methods are managed/cold/presentation paths, not signal transport.

## Consistency Gate

If `HectonAPI.cs` adds, removes, renames, or changes visibility for a public nested surface, public method, public property, or internal forbidden Unity-object method, update this audit, `Signal_Schema.json`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Validate_Mod_API_Static.ps1` in the same change.

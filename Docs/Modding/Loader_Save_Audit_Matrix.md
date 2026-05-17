# Loader And Save Audit Matrix

Date: 2026-05-17
Status: STATIC_SOURCE_AUDIT / RUNTIME_PENDING
Owner prompt: MODDING_API_SCHEMA_BUILDER

## Source Files

- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/ModdingAPI/IHectonMod.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModMetadata.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeInfo.cs`
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs`
- `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs`
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`

## Loader Contract

| Contract | Source value | Mod-facing rule |
|---|---:|---|
| Manifest file name | `mod.json` | Loader discovers packages by manifest only. |
| Current API version | `2` | Mods requiring an API newer than `2` are disabled. |
| Manifest field count | `9` | Manifest schema drift must update this audit and `Signal_Schema.json`. |
| `ModMetadata` field count | `8` | Runtime diagnostics use this stable descriptor. |
| `ModRuntimeInfo` field count | `7` | UI/diagnostics copy this shape; no loader internals are exposed. |
| `IHectonMod` lifecycle methods | `3` | `OnLoad`, `OnInitialize`, `OnUnload` are mandatory callbacks. |
| `IHectonVersionedMod` required properties | `1` | `RequiredAPIVersion` is the source-backed version gate. |

## Manifest Fields

| Field | Type | Rule |
|---|---|---|
| `Id` | `string` | Required stable mod id; hashed for runtime lookup and command ownership. |
| `Name` | `string` | Optional display label; defaults to `Id`. |
| `Version` | `string` | Optional package version; defaults to `0.0.0`. |
| `Author` | `string` | Optional diagnostics string. |
| `Dependencies` | `string[]` | Stable ids that must load before this mod. |
| `EntryAssembly` | `string` | Optional explicit managed assembly file. |
| `EntryType` | `string` | Optional managed entry type. |
| `RequiredAPIVersion` | `int` | Must be positive and no higher than `CurrentAPIVersion`. |
| `ModPriority` | `int` | Arbitration priority for conflicting mod world requests. |

## Metadata Fields

| Field | Type | Rule |
|---|---|---|
| `Id` | `string` | Stable public id. |
| `Name` | `string` | Display label. |
| `Version` | `string` | Display/package version. |
| `Author` | `string` | Display/diagnostic author. |
| `Dependencies` | `string[]` | Loader ordering input. |
| `RequiredAPIVersion` | `int` | Resolved loader API requirement. |
| `StableIdHash` | `uint` | Runtime O(1) lookup key. |
| `ModPriority` | `int` | Command arbitration input. |

## Runtime Info Fields

| Field | Type | Rule |
|---|---|---|
| `Metadata` | `ModMetadata` | Public descriptor copy. |
| `Status` | `ModLoadStatus` | Active or disabled state. |
| `DirectoryPath` | `string` | Package root path for diagnostics. |
| `StatusMessage` | `string` | Loader status or disable reason. |
| `AssetBundlePath` | `string` | Primary mod bundle path if discovered. |
| `HasManagedEntry` | `bool` | True when a managed entry was discovered. |
| `HasLocalizationFiles` | `bool` | True when localization overlays were discovered. |

## Lifecycle Boundaries

| Callback | Source phase | Allowed | Forbidden |
|---|---|---|---|
| `OnLoad` | After managed instance creation and command registration. | Subscribe to public mod events, register cold content, register settings. | Resolve live Unity objects, mutate player/simulation truth, store native handles. |
| `OnInitialize` | After bootstrap reports game ready. | Submit validated requests and resolve supported hash-only runtime state. | Cache `GameObject`, `Transform`, `NativeArray`, or `SignalBus<T>` handles. |
| `OnUnload` | Shutdown, domain reset, or quarantine. | Dispose subscriptions and clear mod-owned state. | Spawn/despawn Unity instances or write first-party save truth directly. |

Source-backed callback safety:

- Loader wraps callbacks in `ModExecutionScope`.
- Managed allocation deltas are reported to `ModCommandDispatcher`.
- Callback exceptions disable or unload the offending mod path instead of keeping it active.
- `DisableManagedMod` disables `HectonEventBus` subscribers and quarantines command dispatch for that mod id.

## SaveState Boundary

| Contract | Source value | Mod-facing rule |
|---|---:|---|
| Public methods | `SetModString`, `GetModString` | SaveState is text-only and mod-owned. |
| Active scope required | `ModExecutionScope.HasActiveMod` | Calls outside mod callbacks throw `IllegalContractException`. |
| Storage prefix | `m8v1:` | Persisted keys are hashed/namespaced; raw mod keys are not first-party save owners. |
| Protected payload block | `16384` bytes | Source: `SaveBinaryPayloadCodec.ProtectedLz4BlockSizeBytes`. |
| Mod payload header | `32` bytes | Source: `SaveBinaryStorage.ModPayloadHeaderSizeBytes`. |
| Max mod payload | `16352` bytes | Source: block minus header. Larger payloads are rejected for MMF commit. |

JSON is allowed here only as cold mod-owned text. It is still forbidden as signal or command transport.

## Static Drift Gate

`Docs/Modding/Validate_Mod_API_Static.ps1` must fail if:

- `ModLoader.CurrentAPIVersion` drifts from the schema.
- `mod.json` manifest field count changes.
- `ModMetadata`, `ModRuntimeInfo`, `IHectonMod`, or `IHectonVersionedMod` shapes change.
- SaveState public method count changes.
- `m8v1:` storage prefix or mod payload byte caps change.
- This audit is not linked by `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Signal_Schema.json`.

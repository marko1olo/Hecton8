# Loader And Save Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC_SOURCE_AUDIT / RUNTIME_PENDING

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Owner domain: Modding API static contract

## 2026-05-19 Envelope-Only Override

This audit keeps the historical loader/save shape visible, but the active UGC runtime mode disables managed entry execution:

- `EntryAssembly` or `EntryType` marks a package as managed-entry and is rejected/quarantined before gameplay execution;
- boot-registered managed factories are rejected while envelope-only mode is active;
- `IHectonMod.OnLoad`, `OnInitialize`, and `OnUnload` are legacy source-audit callbacks, not a current runtime promise for public UGC;
- SDK packages should emit validated manifest/package metadata, binary tables, approved asset manifests, and `FutureCommandEnvelope` streams instead of a gameplay `.dll`;
- mod-owned persistence remains a future/package metadata concern until an envelope-safe save path is explicitly reopened and verified.

SDK/package authoring details are in [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md).

## Source Files

- `Assets/_Project/Scripts/ModdingAPI/ModLoader.cs`
- `Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs`
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
| Manifest byte cap | `32768` bytes | Loader inspects file size and rejects missing, empty, or oversized `mod.json` before `File.ReadAllText`. |
| Manifest discovery cap | `64` manifests | Loader enumerates `mod.json` lazily and stops before allocating candidate lists beyond the cap. |
| Canonical mod IDs | lowercase token segments | `Id` and dependency IDs must be lowercase letters/digits separated by single `.`, `_`, or `-`; no whitespace, leading/trailing/repeated separators, or reserved filesystem device segments. |
| `ModMetadata` field count | `8` | Runtime diagnostics use this stable descriptor. |
| `ModRuntimeInfo` field count | `7` | Internal engine UI/diagnostics shape; not a public mod facade payload or public SDK type. ModRuntimeInfo members internal-only because the descriptor contains package paths and loader status. |
| `IHectonMod` lifecycle methods | `3` | `OnLoad`, `OnInitialize`, `OnUnload` are mandatory callbacks. |
| `IHectonVersionedMod` required properties | `1` | `RequiredAPIVersion` is the source-backed version gate. |
| Reserved managed assembly identities | blocked | Mod packages cannot use `Hecton8.*`, `Unity*`, `Assembly-CSharp`, `System`, `mscorlib`, or `netstandard` by file name or assembly metadata identity. |
| Package DLL identity scan | max `32` top-level `.dll` files | Loader scans every accepted top-level DLL in the manifest directory for reserved file/metadata identities. Packages with more than `32` top-level DLLs are disabled before load. |
| Legacy bundle discovery cap | `4` top-level `.bundle` files | Legacy non-envelope bundle fallback uses bounded top-level enumeration and only accepts the single-bundle case. |
| Legacy localization discovery cap | `16` top-level `lang_*.json` files | Legacy non-envelope localization fallback uses bounded top-level enumeration; envelope-only runtime does not ingest localization files. |
| EntryAssembly file name only | package-local `.dll` name | `EntryAssembly` must not be absolute, rooted, path-like, whitespace-padded, or non-DLL. |
| Scope owner proof | active mod id + non-zero hash | `ModExecutionScope` cannot open an anonymous/blank active owner; public facade guards depend on this owner proof. |
| SaveState store owner proof | scoped mod owner or explicit engine owner | `ModSaveStateStore` rejects scope-less public mod payload access; internal engine payloads must use `SetEngineString` / `GetEngineString` with `hecton.internal.` keys. |

## Manifest Fields

| Field | Type | Rule |
|---|---|---|
| `Id` | `string` | Required canonical stable mod id; hashed for runtime lookup and command ownership. |
| `Name` | `string` | Optional display label; defaults to `Id`. |
| `Version` | `string` | Optional package version; defaults to `0.0.0`. |
| `Author` | `string` | Optional diagnostics string. |
| `Dependencies` | `string[]` | Canonical stable ids that must load before this mod. Invalid dependency ids disable the package before load-order resolution. |
| `EntryAssembly` | `string` | Optional explicit managed assembly file name only; never a path. |
| `EntryType` | `string` | Optional managed entry type. |
| `RequiredAPIVersion` | `int` | Must be positive and no higher than `CurrentAPIVersion`. |
| `ModPriority` | `int` | Arbitration priority for conflicting mod world requests. |

SDK builder parity: `ModBuilderWindow.ModManifestData` emits the same `9` manifest fields as `ModLoader.ModManifest`, including positive `RequiredAPIVersion` and `ModPriority`. `ModBuilderWindow` validates the required API against current loader API version `2`, validates canonical mod/dependency ids, caps selected managed DLLs at the loader's `32` top-level DLL cap, rejects duplicate selected DLL file names, keeps `OnGUI` validation shallow, and uses the canonical trimmed mod id for output path and manifest identity. Treat this as static source proof only; SDK-built packages remain `PENDING VERIFICATION` until Unity runtime smoke evidence proves load behavior without manual edits.

Reserved managed assembly identities are blocked in both cold package paths. `ModLoader` disables packages whose `EntryAssembly`, resolved DLL file name, or any accepted top-level package DLL metadata identity claims an engine-owned name; packages above the 32-DLL top-level cap are disabled instead of partially trusted. `ModBuilderWindow` rejects selected DLLs with the same reserved identities during Build Mod deep validation and deletes stale top-level DLLs that are not part of the current build through bounded cleanup. This keeps `InternalsVisibleTo` friend assemblies such as `Hecton8.Plugins` first-party only and prevents a future managed-mode reopening from turning assembly-name spoofing or stale support DLLs into an internal API route.

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
| `Metadata` | `ModMetadata` | Descriptor copy for engine UI. |
| `Status` | `ModLoadStatus` | Internal active or disabled state. |
| `DirectoryPath` | `string` | Package root path for internal engine diagnostics only. |
| `StatusMessage` | `string` | Loader status or disable reason. |
| `AssetBundlePath` | `string` | Primary mod bundle path for internal engine diagnostics only. |
| `HasManagedEntry` | `bool` | True when a managed entry was discovered. |
| `HasLocalizationFiles` | `bool` | True when localization overlays were discovered. |

Runtime info visibility: `ModRuntimeInfo` and all of its members are internal-only engine UI diagnostics. Package paths, bundle paths, load status, and failure text must not become public SDK fields.

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
| Internal engine route | `SetEngineString`, `GetEngineString` | Engine-owned mod-world payloads use explicit `hecton.internal.` keys and do not synthesize owner hashes from arbitrary payload keys. |
| Storage prefix | `m8v1:` | Persisted keys are hashed/namespaced; raw mod keys are not first-party save owners. |
| Protected payload block | `16384` bytes | Source: `SaveBinaryPayloadCodec.ProtectedLz4BlockSizeBytes`. |
| Mod payload header | `32` bytes | Source: `SaveBinaryStorage.ModPayloadHeaderSizeBytes`. |
| Max mod payload | `16352` bytes | Source: block minus header. Larger payloads are rejected for MMF commit. |

JSON is allowed here only as cold mod-owned text. It is still forbidden as signal or command transport.

## Static Drift Gate

`Docs/Modding/Validate_Mod_API_Static.ps1` must fail if:

- `ModLoader.CurrentAPIVersion` drifts from the schema.
- `mod.json` manifest field count changes.
- `mod.json` byte cap or pre-read file-size validation is removed.
- `mod.json` bounded discovery cap or lazy enumeration before candidate allocation is removed.
- `ModBuilderWindow.ModManifestData` drifts from `ModLoader.ModManifest`.
- Canonical mod id, dependency id, or EntryAssembly filename-only validation is removed from loader or SDK builder.
- Reserved managed assembly identity validation, builder DLL input cap parity, duplicate DLL filename rejection, or top-level package DLL scanning is removed from `ModLoader` or `ModBuilderWindow`.
- Bounded top-level DLL, bundle, or localization discovery is removed from `ModLoader`.
- `ModMetadata`, `ModRuntimeInfo`, `IHectonMod`, or `IHectonVersionedMod` shapes change.
- SaveState public method count changes.
- SaveState store owner proof is removed or engine-owned payloads stop using the explicit internal route.
- `m8v1:` storage prefix or mod payload byte caps change.
- This audit is not linked by `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Signal_Schema.json`.

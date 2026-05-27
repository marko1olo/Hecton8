# HECTON-8 Modding API Contract Index

Date: 2026-05-19
Status: ENVELOPE-ONLY MODDING AUTHORITY / SDK PLAN ADDED / RUNTIME_PENDING

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Owner domain: Modding API static contract

## 2026-05-19 Envelope-Only Authority

Current UGC runtime authority is envelope-only:

- modders do not run Harmony patches, BepInEx patches, arbitrary managed callbacks, or gameplay `.dll` code in the frame;
- modder-facing SDK tools may be rich, friendly, and managed, but they are authoring/offline surfaces;
- the game runtime accepts fixed 64-byte `FutureCommandEnvelope` packets through `HectonAPI.Commands.RequestFuture`; the sandbox validator and its tuning/capacity constants are engine-internal control-plane code;
- `RequestFuture` requires an active mod execution scope and a matching `ModderSignature`;
- event publication hooks and managed command kernels are first-party/internal infrastructure, not SDK extension points;
- legacy `IHectonMod`, `HectonAPI.Events`, resource proxy, localization injection, asset bundle discovery, `Request`, `RequestAup`, and `RequestRenderInstance` sections are historical/source-audit references unless they explicitly agree with `Mod_API_Sandbox_Quarantine.md`;
- filesystem content ingress is not a runtime mod right in envelope-only mode; assets must be CRC-approved and referenced by envelope asset opcodes.

The human-facing modding answer is documented in [SDK_Authoring_Interface_Plan.md](SDK_Authoring_Interface_Plan.md). The short version: yes, modders need interfaces, but those interfaces are SDK/workbench/CLI/graph/manifest tools, not runtime C# interfaces inside the game.

## Current Contract Snapshot

- Schema revision: `60`
- Source `ISignal` structs: `173`
- Mod-projected `SignalBus<T>` lanes: `2`
- Denied-by-default `ISignal` structs: `171`
- Accepted command opcodes: `8`
- Future envelope runtime allowlist: `8` hashes; `TriggerSubtitleCue` and `SubtitleCue` are reserved subtitle aliases, not runtime-allowed opcodes.
- Public `HectonAPI` surfaces: `15`
- Public event methods: `7`
- Native event kinds: `2`
- SDK builder manifest parity: `ModBuilderWindow.ModManifestData` emits the same `9` manifest fields required by `ModLoader`, including `RequiredAPIVersion` and `ModPriority`; `Validate_Mod_API_Static.ps1` fails if this source parity drifts.
- Manifest byte cap: loader rejects missing, empty, or `>32768` byte `mod.json` files before `File.ReadAllText`.
- Manifest discovery cap: loader enumerates `mod.json` lazily and caps discovery at `64` manifests before candidate allocation.
- Canonical mod IDs: loader and SDK builder require lowercase letters/digits separated by single `.`, `_`, or `-`; IDs and dependency IDs cannot use leading/trailing/repeated separators, whitespace, or reserved filesystem device segments.
- Scope owner proof: `ModExecutionScope` cannot synthesize an anonymous active owner; active scope requires a non-empty mod id and non-zero owner hash.
- SaveState owner proof: public mod save payloads require active `ModExecutionScope`; engine-owned mod-world payloads use an explicit `hecton.internal.` store route, not key-hash owner synthesis.
- Reserved managed assembly identities blocked: loader and SDK builder reject `Hecton8.*`, `Unity*`, `Assembly-CSharp`, `System`, `mscorlib`, and `netstandard` names by file name or assembly metadata identity. The loader scans every accepted top-level package DLL up to the `32` DLL cap and disables over-cap packages; the SDK builder deletes stale output DLLs not selected for the current package build.
- Top-level package file caps: managed DLL discovery is capped at `32`, legacy AssetBundle discovery at `4`, and legacy localization discovery at `16`.
- Sandbox control plane: `FutureCommandSandboxValidator`, `MockModQueue` static methods, `MockModQueue` queue handles, and `MockModQueue` instance control methods are internal-only; runtime mods only submit `FutureCommandEnvelope` through `HectonAPI.Commands.RequestFuture`.
- Direct dispatcher/hooks: `ModCommandDispatcher` static helpers and `HectonModHooks` publication methods are internal-only; public mods route through `HectonAPI.Commands` and `HectonAPI.Events`.
- Loader diagnostics: `ModRuntimeInfo` and its package-path members are internal-only engine UI diagnostics, not SDK DTOs.
- Native `SubscribeNative` byte payload layouts: `InteractionEventPayload = 32` bytes and `CraftingEventPayload = 64` bytes; both are explicit-layout source contracts checked by `Validate_Mod_API_Static.ps1`.
- Legacy managed game events: `HectonGameEvents` payload classes and members are internal-only first-party infrastructure, not SDK event DTOs.
- Event bus boundary: `HectonEventBus` is internal first-party infrastructure and has no public static bus member surface; public mod event access is only through `HectonAPI.Events`.
- Event subscription ownership: unmanaged, native, and projected event bridge routes plus private channel implementations reject anonymous subscribers before creating subscription tokens.
- Projected event cap: per-frame projection and dispatch budget is `round(lerp(10,50,smoothstep(saturate(GlobalQualityWeight01))))`, clamped to `10..50`; this is cadence/fidelity only and never gameplay truth.
- Resource ownership: resource hash registration rejects forged `modId` values; the owner must match the active `ModExecutionScope`.
- Runtime proof: `PENDING`

## Primary Files

- `Signal_Schema.json` - machine-readable source-backed contract.
- `Mod_API_Specification.md` - human-facing API specification.
- `Validate_Mod_API_Static.ps1` - required static drift gate.
- `Runtime_Verification_Playbook.md` - required Unity runtime proof path before `VERIFIED`.
- `Change_Control_Checklist.md` - required edit checklist for any mod API contract change.
- `Sample_InfiniteO2_Mod.md` - safe sample mod spec with no current survival mutation authority.
- `Future_Command_Kernel_Reservations.md` - non-public reservations for future engine-owned command kernels; no enum/runtime expansion by itself.
- `Mod_API_Sandbox_Quarantine.md` - current envelope-only runtime quarantine and validator boundary.
- `allowed_opcodes.csv` - editor-reload allowlist source for currently runtime-accepted `FutureCommandEnvelope` opcode hashes; reserved kernels are rejected even if their hash constants exist.
- `kernel_tuning_profiles.csv` - editor-reload priority/budget/range/duration source for reserved command-kernel previews; profile rows do not make an opcode public.
- `SDK_Authoring_Interface_Plan.md` - planned human SDK/workbench/CLI/graph workflow for modders.
- `SDK_Product_Blueprint.md` - product-level SDK screens, CLI, package format, graph compiler rules, Workshop/moderation model, and MVP backlog.

## Audit Matrices

- `Signal_Audit_Matrix.md` - full `ISignal` inventory and deny-by-default list.
- `Command_Audit_Matrix.md` - command opcodes, targets, caps, rejection reasons, and result payloads.
- `API_Surface_Audit_Matrix.md` - public facade and internal forbidden method inventory.
- `Payload_Layout_Audit_Matrix.md` - fixed unmanaged payload sizes and offsets.
- `Loader_Save_Audit_Matrix.md` - loader manifest, lifecycle, runtime info, and mod save-state boundary.
- `Event_Subscription_Audit_Matrix.md` - event methods, native/projected kinds, bridge lanes, and subscription lifetime.
- `Resource_Content_Audit_Matrix.md` - hash-only resources, cold content overlays, registry capacities, and raw asset caps.

## Required Gate

Run this after every related source or document edit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1
```

Do not mark this API runtime verified until `Runtime_Verification_Playbook.md` passes in Unity with Console, GCMonitor, and profiler evidence.

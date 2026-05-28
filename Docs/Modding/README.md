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

- Schema revision: `99`
- Source `ISignal` structs: `173`
- Mod-projected `SignalBus<T>` lanes: `2`
- Denied-by-default `ISignal` structs: `171`
- Accepted command opcodes: `8`
- Future envelope runtime allowlist: `8` hashes; `TriggerSubtitleCue` and `SubtitleCue` are reserved subtitle aliases, not runtime-allowed opcodes, and editor runtime opcode tools must not expose them as injectable opcodes.
- Public `HectonAPI` surfaces: `15`
- Public event methods: `7`
- Native event kinds: `2`
- SDK builder manifest parity: `ModBuilderWindow.ModManifestData` emits the same `9` manifest fields required by `ModLoader`, including `RequiredAPIVersion` and `ModPriority`; `Validate_Mod_API_Static.ps1` fails if this source parity drifts.
- SDK entry point: Unity menu `Hecton/Modding/SDK Hub` prioritizes `Create External Starter Kit`, opens `External Starter Kit Workbench`, links the core modding docs, opens the local `Mods` folder, runs `Validate_Mod_API_Static.ps1` asynchronously so Unity Editor repaint is not blocked by stdout/stderr reads, shows failed validator runs as Editor error UI, and gates the internal legacy Mod Builder behind an explicit warning.
- External starter workbench: Unity menu `Hecton/Modding/External Starter Kit Workbench` is the current project-integrated authoring screen for starter-kit users. It reuses the SDK Hub starter generator to create/refresh missing files, shows required starter-file health before validation using the same required-file list as `Tools/validate_structure.ps1` and the current schema filenames (`assets.schema.json`, `settings_table.schema.json`, `locale.schema.json`, `Docs/capabilities.md`), shows a Capability Matrix for supported authoring surfaces and blocked runtime rights, shows a Graph Contract Preview for `Graphs/main.h8graph.json` budget/runtime/node/opcode errors against `Reference/allowed_opcodes.csv`, shows an Authoring Data Preview for settings row count/IDs/kinds and locale code/key/value issues, generates validated graph/settings/locale snippets through `Tools/create_graph_node_snippet.ps1`, `Tools/create_settings_row_snippet.ps1`, and `Tools/create_locale_entry_snippet.ps1`, applies graph/settings/locale snippets through `Tools/apply_graph_node_snippet.ps1`, `Tools/apply_settings_row_snippet.ps1`, and `Tools/apply_locale_entry_snippet.ps1` with duplicate rejection, post-write validation, budget repair for first graph nodes, and rollback, builds the reviewed submission zip through `Tools/build_submission_package.ps1`, shows current submission package path/freshness, opens the current `Generated/<mod-id>_submission.zip` or Generated folder, requires the root `h8mod.ps1` launcher, opens that launcher from the same screen, edits package identity through `Tools/set_mod_identity.ps1`, runs starter tools asynchronously so Unity Editor repaint is not blocked by stdout/stderr reads, shows failed starter tool runs as Editor error UI, runs `Tools/prepare_mod.ps1`, runs `Tools/validate_structure.ps1` directly for fast structure checks, lists graph opcodes through `Tools/list_allowed_opcodes.ps1`, opens the key authoring files and core docs, shows `Reports/review_manifest.json` identity/file/byte summary, and warns when the review manifest is stale relative to starter source files while preserving the envelope-only runtime boundary.
- External starter kit: `ModdingSDK/ExternalStarterKit/` is a versioned starter template and `Hecton/Modding/SDK Hub -> Create External Starter Kit` can refresh missing files non-destructively. It contains root `h8mod.ps1`, `Docs/capabilities.md`, `mod.h8manifest.json`, `mod.json`, graph/table/locale/content/report folders, copied opcode/tuning references that are statically compared against `Docs/Modding/*.csv`, and a README stating that no Unity project is required for manifest/graph/table authoring while runtime remains envelope-only.
- External local validation: starter kits include root `h8mod.ps1` plus `Tools/validate_structure.ps1`; the validator requires the launcher and `Docs/capabilities.md`, rejects stale capability guide text, and checks required files, JSON parseability, canonical mod/dependency IDs, authoring/runtime manifest ID parity, envelope-only graph/manifest flags, empty `EntryAssembly`/`EntryType`, graph node ID uniqueness, graph opcode allowlist membership against `Reference/allowed_opcodes.csv`, and graph budget parity with `mod.h8manifest.json`.
- External review manifest: starter kits include `Tools/build_review_manifest.ps1`, a no-Unity review handoff tool that runs the structure validator first and writes `Reports/review_manifest.json` with package identity, sorted file paths, byte counts, SHA-256 hashes, total bytes, and explicit count/byte limits while excluding `Generated/` and `Reports/` outputs.
- External identity helper: starter kits include `Tools/set_mod_identity.ps1`, a no-Unity helper that writes the same canonical mod id, display name, author, and semantic version into both manifests and then runs structure validation.
- External manifest identity parity: local validation requires matching authoring/runtime id, display name/name, author, and semantic version, so one package cannot carry split public identity.
- External root launcher: starter kits include `h8mod.ps1`, a no-Unity launcher for menu, setup, validate, review, prepare, submission package build, opcode discovery, graph/settings/locale snippet generation, bounded graph/settings/locale snippet application, and `Docs/capabilities.md` display through `-Action capabilities`. It delegates to the existing `Tools/*.ps1` scripts and does not create a runtime install contract.
- External prepare helper: starter kits include `Tools/prepare_mod.ps1`, a one-command no-Unity helper that writes identity when `-Id` is supplied, then validates structure and builds `Reports/review_manifest.json`; without identity arguments it validates existing manifests and rebuilds the review report for the normal edit-review loop.
- External opcode helper: starter kits include `Tools/list_allowed_opcodes.ps1`, a no-Unity helper that prints allowed graph opcode aliases/hex tokens from `Reference/allowed_opcodes.csv` and can emit JSON for Workbench/CLI reuse.
- External graph apply helper: starter kits include `Tools/create_graph_node_snippet.ps1` and `Tools/apply_graph_node_snippet.ps1`. The snippet helper writes `Generated/graph_node_snippet.json` after validating node id and opcode against `Reference/allowed_opcodes.csv`; the apply helper inserts it into `Graphs/main.h8graph.json`, raises graph/manifest `MaxEnvelopesPerFrame` to one for the first node when needed, rejects duplicate node ids unless `-Replace` is explicit, validates after write, and restores previous graph/manifest files on failure.
- External settings/locale apply helpers: starter kits include `Tools/apply_settings_row_snippet.ps1` and `Tools/apply_locale_entry_snippet.ps1`, no-Unity helpers that insert Generated settings/locale snippets into `Tables/settings.h8table.json` and `Locales/en.h8loc.json`, reject duplicate IDs/keys unless `-Replace` is explicit, validate after write, and restore the previous file on failure.
- External shell portability: public starter tools compose child paths through normalized `Join-Path` segments and do not rely on Windows backslash child paths, so the copied kit can use Windows PowerShell or `pwsh` on macOS/Linux.
- External editor help: starter kits include `Schemas/*.schema.json` plus `.vscode/settings.json`; the local validator checks exact schema URL and fileMatch pairs, and now rejects invalid settings row IDs/kinds/default types plus invalid locale codes/keys/values, so schema-aware editors and CLI validation catch table/locale mistakes before submission packaging.
- SDK builder authoring caps: internal legacy Mod Builder enumerates bundle build assets from the selected folder with bounded filesystem enumeration and a `512` bundle-eligible asset cap; selected managed assemblies are capped at the loader's `32` top-level DLL limit and duplicate DLL file names are rejected before copy.
- SDK builder UI validation: internal legacy Mod Builder uses shallow `OnGUI` validation for responsive editor repaint and performs deep bundle asset discovery plus DLL metadata identity reads only when `Build Internal Legacy Package` is invoked.
- Manifest byte cap: loader rejects missing, empty, or `>32768` byte `mod.json` files before `File.ReadAllText`.
- Manifest discovery cap: loader enumerates `mod.json` lazily and caps discovery at `64` manifests before candidate allocation.
- Canonical mod IDs: loader and SDK builder require lowercase letters/digits separated by single `.`, `_`, or `-`; IDs and dependency IDs cannot use leading/trailing/repeated separators, whitespace, or reserved filesystem device segments.
- Scope owner proof: `ModExecutionScope` cannot synthesize an anonymous active owner; active scope requires a non-empty mod id and non-zero owner hash.
- SaveState owner proof: public mod save payloads require active `ModExecutionScope`; engine-owned mod-world payloads use an explicit `hecton.internal.` store route, not key-hash owner synthesis.
- Reserved managed assembly identities blocked: loader and SDK builder reject `Hecton8.*`, `Unity*`, `Assembly-CSharp`, `System`, `mscorlib`, and `netstandard` names by file name or assembly metadata identity. The loader scans every accepted top-level package DLL up to the `32` DLL cap and disables over-cap packages; the SDK builder caps selected DLLs at `32`, rejects duplicate output names, and deletes stale output DLLs through bounded cleanup.
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

- `Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs` - Unity Editor SDK hub; menu path `Hecton/Modding/SDK Hub`.
- `Assets/_Project/Scripts/Editor/ModdingSDK/ExternalStarterKitWorkbenchWindow.cs` - Unity Editor starter-kit workbench; menu path `Hecton/Modding/External Starter Kit Workbench`.
- `Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs` - internal legacy Unity Editor package builder; menu path `Hecton/Modding/Internal/Legacy Mod Builder`.
- `Signal_Schema.json` - machine-readable source-backed contract.
- `Mod_API_Specification.md` - human-facing API specification.
- `Validate_Mod_API_Static.ps1` - required static drift gate.
- `Runtime_Verification_Playbook.md` - required Unity runtime proof path before `VERIFIED`.
- `External_Starter_Kit_File_Contract.md` - public modder file layout, Unity/no-Unity answer, and starter kit rejection rules.
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

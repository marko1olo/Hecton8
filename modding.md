# HECTON-8 Modding, SDK, And Public API Bible

Date: 2026-06-02
Status: ROOT MODDING AUTHORITY / ENVELOPE-ONLY / STATIC BIBLE / RUNTIME PROOF REQUIRED

## Prime Law

Modding in HECTON-8 is not a permission to patch the game, run arbitrary gameplay code, hold Unity object handles, or mutate first-party truth. The public modding surface is an envelope-only authoring and submission system: external creators use the SDK, Workbench, CLI tools, manifests, graphs, settings tables, locale files, content manifests, review manifests, and submission packages; the game runtime accepts only validated, fixed-size command envelopes and approved content references after the engine owner decides they are legal.

Any agent asked to add mod support, SDK support, public API methods, external starter kit tooling, Workshop packaging, mod command opcodes, mod events, mod save payloads, mod assets, or mod UI must read this file first, then read `Docs/Modding/README.md`, `Docs/Modding/Mod_API_Specification.md`, `Docs/Modding/Mod_API_Sandbox_Quarantine.md`, `Docs/Modding/External_Starter_Kit_File_Contract.md`, and `Docs/Modding/Change_Control_Checklist.md`. If those files disagree, the narrower, safer, source-backed envelope-only contract wins until runtime verification proves a wider route.

## Truth Ownership

The engine owns simulation truth. Mods request outcomes. They do not own player oxygen, inventory, save identity, world transforms, AUP origin shifts, physics bodies, scene objects, native buffers, AI decisions, streaming residency, UI state, or first-party event lanes.

Allowed mod-side facts are authoring facts: package id, display name, author, semantic version, dependencies, declared capabilities, graph nodes, settings rows, locale entries, approved content asset ids, review manifest hashes, and generated submission package proof. Runtime execution facts are engine-owned: accepted envelope hash, rejected envelope reason, owner signature, quota state, projected event sample, package load state, and mod-owned save payload.

No root bible, SDK screen, sample mod, or public text may imply that a mod can bypass the owner domain. A survival mod cannot directly set oxygen. A construction mod cannot directly instantiate a prefab. A rendering mod cannot directly inject materials. A world mod cannot directly mutate sectors. Every request must route through an engine-owned opcode, approved hash, validated payload, and owner-specific rejection path.

## Public Runtime Boundary

The active runtime boundary is envelope-only. The current public command ingress is `HectonAPI.Commands.RequestFuture(in FutureCommandEnvelope envelope)`, and that route requires an active `ModExecutionScope` plus a matching `ModderSignature`. The envelope size is fixed at 64 bytes. A mod package may describe intent, but runtime authority begins only after the sandbox validator accepts that envelope and the relevant engine owner applies it.

Forbidden runtime promises:

- no Harmony patches, BepInEx patches, method detours, or runtime C# gameplay callbacks;
- no arbitrary `.dll` execution as the public gameplay mod route;
- no direct `GameObject`, `Transform`, prefab, material, mesh, texture, audio clip, ScriptableObject, `NativeArray`, `NativeQueue`, `GlobalDataVault`, DataVault handle, first-party `SignalBus<T>`, or hot engine service handle exposure;
- no string event names, JSON hot-path payloads, or reflection-driven gameplay mutation;
- no loose runtime asset loading from random files, bundles, PNGs, materials, meshes, or localization JSON;
- no public direct calls into `FutureCommandSandboxValidator`, `MockModQueue`, `ModCommandDispatcher`, `HectonModHooks`, `IModCommandKernel`, internal registry invalidation, internal mod-world persistence, or engine diagnostics DTOs.

Legacy managed API material in older documents is source-audit context only unless it explicitly agrees with the envelope-only quarantine and passes the runtime verification playbook. A public C# type existing in source is not a public modding right.

## SDK Authoring Contract

The SDK must feel usable to creators without weakening runtime authority. The authoring layer may be friendly, graphical, and managed. The runtime layer must remain validated data.

Required public authoring surfaces:

- Unity Editor `Hecton/Modding/SDK Hub` for starter kit creation, core contract links, local Mods folder access, static validator launch, and legacy tool warning;
- Unity Editor `Hecton/Modding/External Starter Kit Workbench` for required-file health, capability matrix, graph preview, settings preview, locale preview, content asset preview, manifest budget editing, dependency editing, snippet creation, snippet application, review manifest state, local install, local diagnosis, and submission package access;
- no-Unity starter kit route through root `h8mod.ps1` and copied `Tools/*.ps1`;
- VS Code task surface that routes through `h8mod.ps1` rather than inventing a second package format;
- strict JSON/text readers with byte caps, UTF-8 validation, exact-case path checks, duplicate rejection, rollback on failed validation, and deterministic review manifest output;
- readable rejection reports that tell the creator which package fact failed, not vague "mod invalid" messages.

The SDK may generate graphs, manifests, settings rows, locale entries, content asset entries, CRC records, review reports, and submission zip files. It must not promise that those artifacts grant runtime permission. Capabilities are review metadata. Budgets are declared ceilings. Graph nodes are authoring intent. The engine may reject every envelope if authority, quota, rollback state, quality pressure, thermal state, asset approval, or owner-domain validation fails.

## Command Envelope And Opcode Rules

Every public write request must compile to an allowlisted `FutureCommandEnvelope` opcode with finite, bounded parameters. The opcode allowlist is data, not folklore: `Docs/Modding/allowed_opcodes.csv` and the static validator decide what is public. Reserved kernel names, editor preview names, or constants in source do not become public runtime rights by existing.

Command rules:

- each envelope carries the mod owner signature and must match the active execution scope;
- each opcode must name its engine owner domain before it is added;
- every opcode requires target validation, AUP/spatial validation if spatial, quota validation, rollback validation, rejection payload design, and proof that rejected commands do not mutate truth;
- no opcode may expose Unity object references, native handles, first-party service pointers, or scene search as parameters;
- binary payload layout must stay fixed and explicitly sized;
- command cadence may scale with `GlobalQualityWeight`, but command authority, owner identity, save identity, DTO layout, and accepted gameplay truth must not change with quality.

If an agent cannot identify the owner system that applies an opcode, the opcode is not ready. If an agent cannot identify the rejection payload and black-box fields for a failed opcode, the opcode is not ready. If an agent adds an opcode to UI tooling but not to the source-backed validator and audit matrix, the SDK is lying.

## Events And Read-Only Projections

Mods may receive only approved, sampled, immutable projections. They do not subscribe to arbitrary first-party buses. `HectonEventBus` is mod/API/cold managed isolation, not the first-party hot gameplay bus. First-party gameplay broadcasts use typed `SignalBus<T>` lanes, and those lanes are denied to mods unless the schema and audit matrices explicitly project them.

Projection rules:

- projected DTOs must be fixed-layout or explicitly sized;
- native byte payload spans are callback-duration only and must not be stored;
- projected events are read-only context, not authority;
- event budgets are continuous with `GlobalQualityWeight`, such as low-cadence compact sampling and higher-cadence high-tier sampling;
- event quality flags may describe reduced sampling but must not change gameplay truth;
- anonymous subscribers, owner-mismatched tokens, and cross-mod disposal must fail closed.

An event projection is accepted only when the source lane, projected fields, field redactions, sample cap, owner id, callback lifetime, stall watchdog, recursion cap, rejection behavior, and static schema entry are documented.

## Content, Assets, And Localization

Mod content enters through approved package records and hashes, not loose runtime file privileges. A content asset may be authored in the starter kit and included in a reviewed submission package, but runtime systems must resolve it by approved id/hash/CRC/byte proof through engine-owned loaders. A mod must never receive a direct `Texture`, `Material`, `Mesh`, `AudioClip`, `Prefab`, or `GameObject` handle.

Content rules:

- content asset paths must stay under the approved starter-kit content path and pass exact-case, no-ADS, no-rooted-path, no-dot-segment checks;
- content byte totals must respect manifest budgets and must not be silently raised by runtime;
- CRC, byte length, and hash proof are part of the authoring handoff;
- localization entries are authored data, not a hot runtime localization injection right;
- settings rows are user-facing configuration facts, not direct gameplay authority;
- review manifests must exclude generated output folders exactly as specified and must reject case-fold duplicate paths.

Generated content can look rich, but it must still enter as data. A beautiful texture or model does not grant a mod permission to instantiate it directly in the scene.

## Save, Persistence, And Identity

Mod-owned save payloads must be scoped by active owner. Public mod save data cannot write first-party save truth, cannot forge `hecton.internal.` keys, cannot derive owner identity from arbitrary payload keys, and cannot create scene-object identity by name. Engine-owned mod-world payloads use explicit internal routes.

Persistence rules:

- package ids and dependency ids must use the canonical id rule;
- manifest identity must match between authoring and runtime manifests;
- mod save keys must be owner scoped and size capped;
- persistent world mutations require an engine-owned command route and approved asset hash;
- save/load proof must show accepted payload, rejected payload, owner mismatch, dependency mismatch, and missing package behavior.

Any "spawn persistent thing from mod" feature is invalid until there is a proved command route from envelope to engine owner to stable save identity to load restore, with no direct prefab handle exposed to the mod.

## Security And Abuse Resistance

The modding system must assume hostile packages, malformed JSON, overlarge files, duplicate ids, dependency cycles, reserved names, spoofed engine identities, stale review manifests, invalid UTF-8, path tricks, case variants, over-budget graphs, unknown opcodes, forged signatures, and asset hash mismatches.

Mandatory rejection classes:

- reserved managed assembly identities such as engine, Unity, system, and standard library names;
- package files above byte caps;
- more manifests, DLLs, bundles, localization files, graph nodes, or content bytes than the current caps allow;
- duplicate package ids, duplicate dependency ids, self dependencies, missing dependencies, and dependency cycles;
- exact-case path failures and case-fold duplicate entries;
- stale review manifest proof;
- graph opcodes absent from the allowlist;
- non-empty managed entry fields in envelope-only public packages;
- runtime packets without active owner scope or matching signature.

Security failures must be understandable to modders and useful to engineers. Error reports should name the rejected file, field, id, opcode, hash, budget, or owner condition.

## Runtime Performance Boundary

Mod support must not become a managed hot-path tax. Runtime mod processing must use bounded queues, fixed payloads, capped projections, owner-scoped commands, deterministic rejection, and low-allocation diagnostics. Authoring richness belongs in the SDK and offline package tools. Runtime richness belongs in engine-owned systems applying accepted data.

Forbidden runtime costs:

- per-frame filesystem scans for mod content;
- per-frame JSON parsing for mod state;
- managed callback storms;
- reflection over package objects in gameplay loops;
- dynamic scene searches for mod targets;
- per-mod material, mesh, or prefab cloning in hot paths;
- logging strings every frame for mod diagnostics;
- hidden `.Complete()` calls introduced only to service mod packets.

`GlobalQualityWeight` may scale projection cadence, optional diagnostics, preview density, UI detail, and command budget ceilings where documented. It must not scale simulation truth, package identity, save identity, DTO layout, or authority route.

## Platform And Distribution Rules

Modding must not assume Windows-only tooling unless the current route explicitly says so. Public starter tools should support Windows PowerShell and `pwsh` on macOS/Linux when practical, use portable path composition, and fail closed when template files are missing. Steam Deck/Linux, macOS, console, and XR claims require `platform.md` proof. Public Workshop or distribution claims require `release.md` proof.

Console or restricted-platform builds may disable public mod loading, but they must not silently change first-party gameplay truth. If platform policy removes a mod surface, the UI and public text must state that route accurately and must not imply unsupported runtime code execution.

## Required Proof Artifacts

No modding change is accepted by prose. Required proof depends on the edit:

- Static contract proof: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1`.
- Source route proof: changed source files, schema revision, audit matrix updates, and source-backed snapshot agreement.
- SDK proof: starter kit creation/refresh, no-Unity validation, review manifest generation, submission package generation, local install or diagnosis when relevant.
- Runtime proof: `Docs/Modding/Runtime_Verification_Playbook.md` completed in Unity with Console, GCMonitor, profiler, accepted command, rejected command, owner mismatch, quota pressure, and package load evidence.
- Save proof: owner-scoped save payload roundtrip and rejection of owner mismatch or internal key spoof.
- Asset proof: CRC/hash/byte proof for approved content and rejection of missing, oversized, stale, or case-mismatched assets.
- Platform proof: device or player-build proof when claiming distribution support outside editor-only authoring.

If only static docs were edited, the report must say STATIC ONLY / RUNTIME NOT VERIFIED. If runtime play mode was not run, no agent may claim that mod loading, event callbacks, command dispatch, save roundtrip, or package discovery is runtime verified.

## Rejection Gates

Reject any modding or SDK work that:

- exposes Unity object handles or native containers to public mods;
- turns `HectonEventBus` into first-party hot gameplay infrastructure;
- allows runtime `.dll` callbacks, Harmony, BepInEx, or method patching as the public route;
- adds opcodes without owner, rejection payload, validator, audit matrix, quota, and runtime playbook updates;
- accepts loose files as runtime truth without reviewed package proof;
- parses JSON or scans package folders in hot paths;
- lets capabilities become runtime permissions;
- changes gameplay truth based on `GlobalQualityWeight`;
- publishes public claims that modding is runtime verified without runtime proof artifacts;
- hides security failures behind vague errors.

## Acceptance Sentence

A modding feature is acceptable only when it preserves envelope-only runtime authority, names the engine owner for every accepted command, rejects unsafe or unauthenticated packages deterministically, keeps authoring richness outside hot gameplay paths, provides static validator proof, and separates static documentation claims from Unity runtime verification.

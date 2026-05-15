# LOG - MODDING_API_SCHEMA_BUILDER

## 2026-05-14 Session Start

What was wrong -> No `Docs/Modding` schema/spec existed for the mod-facing signal surface, and the scoped agent status/rationale files were absent.

What was done -> Extracted the `MODDING_API_SCHEMA_BUILDER` prompt from `Docs/Tasks/CURRENT_BATCH.md`, read relevant mandates, source-scanned `GlobalSignals.cs`, `GLOBAL_SIGNAL_CORRIDOR.md`, `SYSTEM_INTERCONNECT_MATRIX.md`, and current `Assets/_Project/Scripts/ModdingAPI` files.

Cinematic Cheats used -> None. This is a docs/schema task. The architectural cheat is read-only projection instead of simulating or exposing mutable game truth to mods.

Exact Microseconds saved -> No profiler-backed microsecond claim. Static estimate only: avoiding JSON event feeds and direct all-lane mod callbacks prevents allocation and unbounded fanout risk.

## 2026-05-14 Final Report

What was wrong -> The mod API had source code surfaces (`HectonAPI`, `HectonEventBus`, `ModEventProjectionBridge`, `ModCommandDispatcher`) but no stable docs/schema defining which first-party `SignalBus<T>` lanes are public to mods, which lanes are blocked, or how a dangerous cheat-style request must route through engine ownership.

What was done -> Created `Docs/Modding/Signal_Schema.json` and `Docs/Modding/Mod_API_Specification.md`. The schema defines only two current mod-projected `SignalBus<T>` lanes: `SignalBus<CombatDamageSignal>` and `SignalBus<WeatherChangedSignal>`. It also documents native byte-copy Interaction/Crafting events, unmanaged mod result payloads, command quotas, DataVault/AUP/survival blocked lanes, read-only wrapper rules, and the Infinite O2 command-kernel specification. Updated `Docs/Tasks/Status_MODDING_API_SCHEMA_BUILDER.md` and `Docs/AgentLogs/Rationale_MODDING_API_SCHEMA_BUILDER.md`.

Cinematic Cheats used -> Projection over direct truth exposure. Mods get a 64-byte DTO or copied bytes, not mutable gameplay state, Unity object references, native handles, or DataVault pointers.

Exact Microseconds saved -> No profiler-backed timing claim. Static estimate only: read-only DTO projection avoids JSON parse/string allocation and prevents all-lane callback storms. Runtime timing remains PENDING VERIFICATION.

Verification -> `ConvertFrom-Json` parsed `Signal_Schema.json` and reported schema id `hecton8.modding.signal_schema.v1` with 2 allowed lanes. ASCII scan found no non-ASCII in touched files. `git diff --check` on touched files exited 0. Compile proof is BLOCKED_BY_ENVIRONMENT because `rg --files -g '*.csproj'` and `rg --files -g '*.sln'` found no project/solution targets in this workspace; no C# source was changed.

Final status -> MOD API DEFINED / PENDING RUNTIME VERIFICATION.

## 2026-05-14 Continuation Hardening Report

What was wrong -> The first pass met the five batch tasks, but the durable docs still left implementation ambiguity around the broader `HectonAPI` facade, exact unmanaged payload layout, lifecycle/version rules, and acceptance gates for adding future mod-visible signals or command opcodes.

What was done -> Re-extracted the prompt from `Docs/Tasks/CURRENT_BATCH.md` lines 240-252. Re-read source slices from `HectonAPI.cs`, `ModEventContracts.cs`, `ModSpatialContracts.cs`, `ModCommandDispatcher.cs`, `IHectonMod.cs`, `ModLoader.cs`, and `ModEventProjectionBridge.cs`. Expanded `Signal_Schema.json` to schema revision 2 with lifecycle, API version 2, 16 public facade surface records, 8 payload layout records, and acceptance gates. Expanded `Mod_API_Specification.md` with a public facade matrix, payload layout table, signal extension gate, and exact static/runtime acceptance tests.

Cinematic Cheats used -> Same architectural cheat: bounded projection and command arbitration instead of exposing mutable simulation truth. No physical simulation was added.

Exact Microseconds saved -> No profiler-backed timing claim. Static risk reduction only: the hardened facade matrix prevents managed/UI/cold-path APIs from being mistaken for hot-path mod transport, which avoids slower implementation shapes before they enter code.

Verification -> `ConvertFrom-Json` parsed schema revision 2 and reported 16 public surfaces, 8 payload layouts, 2 allowed projected lanes, current API version 2, and three acceptance gate groups. Markdown anchors for the new sections were found by `rg`. Full touched-doc ASCII scan returned `ASCII_SCAN_NO_MATCHES`. `git diff --check` returned `DIFF_CHECK_OK`. `rg --files -g '*.csproj' -g '*.sln'` returned `NO_CSPROJ_OR_SLN_FOUND`, so compile proof remains blocked by workspace layout and no C# source was changed.

Final status -> MOD API DEFINED / HARDENED / PENDING RUNTIME VERIFICATION.

## 2026-05-15 Source-Wide Signal Audit Report

What was wrong -> The schema documented the allowed projected lanes, but the broader 129-struct `ISignal` source inventory was not recorded in a durable audit. That left room for future agents to treat unlisted public C# signals as unaudited instead of denied.

What was done -> Extracted the current `ISignal` inventory from `Assets/_Project/Scripts/Core/GlobalSignals.cs`. Created `Docs/Modding/Signal_Audit_Matrix.md` with the two allowed projected signals and all 127 denied-by-default signals. Added `sourceSignalInventory` to `Signal_Schema.json` and linked the audit from `Mod_API_Specification.md`.

Cinematic Cheats used -> Deny-by-default projection boundary. Mods receive capped read-only projections or command responses, not full first-party signal truth.

Exact Microseconds saved -> No profiler-backed timing claim. Static prevention only: blocking accidental exposure of 127 internal signal lanes avoids a likely callback fanout and managed allocation path before it can enter implementation.

Verification -> Prompt re-extracted from `CURRENT_BATCH.md` lines 240-252. Source inventory command found 129 unique `ISignal` structs. Projection bridge readback still shows only `SignalBus<CombatDamageSignal>` and `SignalBus<WeatherChangedSignal>` consumed for `ModEventDto`. Inventory/schema consistency check reported source `129`, schema `129`, projected `2`, denied `127`, match `True`. ASCII scan returned `ASCII_SCAN_NO_MATCHES`; `git diff --check` returned `DIFF_CHECK_OK`; build target scan returned `NO_CSPROJ_OR_SLN_FOUND`.

Final status -> MOD API DEFINED / HARDENED / SOURCE-WIDE SIGNAL AUDITED / PENDING RUNTIME VERIFICATION.

## 2026-05-15 Static Validator Report

What was wrong -> The schema/spec/audit package was correct by manual commands, but it had no single repeatable static gate. Future source edits could drift the schema, projection bridge, and audit without a hard failure.

What was done -> Added `Docs/Modding/Validate_Mod_API_Static.ps1`. The first run exposed a PowerShell quoting bug in the audit-table assertion; fixed it and reran. Promoted `Signal_Schema.json` to revision 3 and added `staticValidation` with the validator path and last-known pass values. Updated the spec and audit matrix to name the validator command.

Cinematic Cheats used -> Offline static drift gate instead of runtime probing for a docs-only contract. Runtime verification is still required before `VERIFIED`.

Exact Microseconds saved -> No profiler-backed timing claim. Static prevention only: the validator blocks accidental expansion of mod callback lanes before it reaches gameplay code.

Verification -> Validator output: `PASS`, schema revision `3`, source signals `129`, allowed projected signals `2`, denied-by-default signals `127`, bridge signals `CombatDamageSignal,WeatherChangedSignal`.

Final status -> MOD API DEFINED / HARDENED / STATIC VALIDATOR PASSING / PENDING RUNTIME VERIFICATION.

## 2026-05-15 Runtime Playbook Report

What was wrong -> The remaining runtime verification boundary was accurate but not executable enough. Static checks passed, but the exact Unity proof sequence for projected events, command quotas, memory eviction, teardown, and GC/profiler evidence was not isolated as a durable playbook.

What was done -> Added `Docs/Modding/Runtime_Verification_Playbook.md`. Linked it from `Signal_Schema.json` and `Mod_API_Specification.md`. Promoted schema to revision 4. Updated `Validate_Mod_API_Static.ps1` to require the runtime playbook and key pass criteria before the static package can pass.

Cinematic Cheats used -> Runtime proof is deferred to an explicit playbook instead of pretending static source review proves frame behavior. No physical simulation or gameplay code was added.

Exact Microseconds saved -> No profiler-backed timing claim. The playbook requires future evidence that hot-path projection dispatch is 0 B/frame and no mod API system exceeds 0.1 ms without tier gate and justification.

Verification -> Validator output: `PASS`, schema revision `4`, source signals `129`, allowed projected signals `2`, denied-by-default signals `127`, bridge signals `CombatDamageSignal,WeatherChangedSignal`, runtime playbook `Docs/Modding/Runtime_Verification_Playbook.md`.

Final status -> MOD API DEFINED / HARDENED / STATIC VALIDATOR PASSING / RUNTIME PLAYBOOK DEFINED / PENDING RUNTIME VERIFICATION.

## 2026-05-15 Command Authority Audit Report

What was wrong -> Command write authority had only a short accepted-opcode list. It lacked a durable source-backed matrix for valid targets, AUP requirements, rejection reasons, result payloads, render instance lane, and hard caps.

What was done -> Added `Docs/Modding/Command_Audit_Matrix.md`. Promoted `Signal_Schema.json` to revision 5 with `commandApi.auditPath` and `commandAudit`. Linked the command audit from `Mod_API_Specification.md` and `Runtime_Verification_Playbook.md`. Extended `Validate_Mod_API_Static.ps1` to parse command enums from `ModCommandDispatcher.cs` and validate schema/audit/spec/playbook parity.

Cinematic Cheats used -> Command arbitration remains the fake boundary: mods submit small validated packets, not direct simulation mutations.

Exact Microseconds saved -> No profiler-backed timing claim. Static prevention only: the audit and validator block unauthorized command/target drift before it can create runtime fanout, invalid AUP writes, or expensive rejection storms.

Verification -> Validator output: `PASS`, schema revision `5`, source signals `129`, allowed projected signals `2`, denied-by-default signals `127`, accepted command opcodes `8`, command reject reasons `19`, bridge signals `CombatDamageSignal,WeatherChangedSignal`, command audit path `Docs/Modding/Command_Audit_Matrix.md`.

Final status -> MOD API DEFINED / HARDENED / STATIC VALIDATOR PASSING / COMMAND AUDITED / RUNTIME PLAYBOOK DEFINED / PENDING RUNTIME VERIFICATION.

## 2026-05-15 API Surface Audit Report

What was wrong -> `HectonAPI.cs` facade visibility was not validator-enforced. Signals and commands were audited, but public nested surfaces, public facade methods/properties, and internal forbidden methods could drift without a failing static gate.

What was done -> Added `Docs/Modding/API_Surface_Audit_Matrix.md`. Promoted `Signal_Schema.json` to revision 6 with `apiSurfaceAudit`. Linked the API surface audit from `Mod_API_Specification.md` and `Runtime_Verification_Playbook.md`. Extended `Validate_Mod_API_Static.ps1` to parse `HectonAPI.cs` and validate 16 public surfaces, 34 public methods, 2 public properties, and 9 internal forbidden methods against the audit/schema.

Cinematic Cheats used -> Facade remains hash/unmanaged/cold-path oriented. No Unity object references or direct runtime owners were promoted to public API.

Exact Microseconds saved -> No profiler-backed timing claim. Static prevention only: facade drift is caught before it can add managed hot-path callbacks, Unity object exposure, or direct owner mutation.

Verification -> Validator output: `PASS`, schema revision `6`, source signals `129`, allowed projected signals `2`, denied-by-default signals `127`, accepted command opcodes `8`, command reject reasons `19`, public API surfaces `16`, public API methods `34`, public API properties `2`, internal forbidden API methods `9`.

Final status -> MOD API DEFINED / HARDENED / STATIC VALIDATOR PASSING / API SURFACE AUDITED / COMMAND AUDITED / RUNTIME PLAYBOOK DEFINED / PENDING RUNTIME VERIFICATION.

## 2026-05-15 Payload Layout Audit Report

What was wrong -> Payload layouts were documented, but the static validator did not prove the source byte contracts. For unmanaged modding, `ModEventDto` offsets and fixed-size packets are ABI boundaries.

What was done -> Added `Docs/Modding/Payload_Layout_Audit_Matrix.md`. Promoted `Signal_Schema.json` to revision 7 with `payloadLayoutAudit`. Linked the payload audit from `Mod_API_Specification.md` and `Runtime_Verification_Playbook.md`. Extended `Validate_Mod_API_Static.ps1` to parse `ModEventDto` explicit size and offsets, `ModCommand` size, and `ModAupResponse` size from source.

Cinematic Cheats used -> Fixed 64-byte DTO/command packets remain the cheap transport instead of JSON/dynamic payload parsing.

Exact Microseconds saved -> No profiler-backed timing claim. Static prevention only: the validator blocks payload ABI drift before it can force parsing, boxing, or managed fallback paths.

Verification -> Validator output: `PASS`, schema revision `7`, `ModEventDto` size `64`, `ModEventDto` field offsets `15`, `ModCommand` size `64`, `ModAupResponse` size `64`.

Final status -> MOD API DEFINED / HARDENED / STATIC VALIDATOR PASSING / PAYLOAD AUDITED / API SURFACE AUDITED / COMMAND AUDITED / RUNTIME PLAYBOOK DEFINED / PENDING RUNTIME VERIFICATION.

## 2026-05-15 Loader And Save Boundary Audit Report

What was wrong -> Loader lifecycle, manifest fields, runtime info shape, and mod-owned SaveState limits were public contract surfaces, but they were not locked by the static validator. A future loader/save edit could drift without failing the schema gate.

What was done -> Added `Docs/Modding/Loader_Save_Audit_Matrix.md`. Promoted `Signal_Schema.json` to revision 8 with `loaderSaveAudit`. Linked the loader/save audit from `Mod_API_Specification.md` and `Runtime_Verification_Playbook.md`. Extended `Validate_Mod_API_Static.ps1` to parse `ModLoader.cs`, `IHectonMod.cs`, `ModMetadata.cs`, `ModRuntimeInfo.cs`, `ModRuntimeState.cs`, `SaveBinaryStorage.cs`, and `SaveBinaryPayloadCodec.cs`.

Cinematic Cheats used -> Loader and SaveState remain cold, scoped, and text-only. Mods persist their own namespaced payloads; they do not write first-party save truth or hold Unity/native handles.

Exact Microseconds saved -> No profiler-backed timing claim. Static prevention only: the validator blocks managed loader/save contract drift before it can create package callback fanout or unbounded save payload pressure.

Verification -> Validator output: `PASS`, schema revision `8`, current API version `2`, manifest fields `9`, `ModMetadata` fields `8`, `ModRuntimeInfo` fields `7`, lifecycle methods `3`, SaveState public methods `2`, mod payload max bytes `16352`.

Final status -> MOD API DEFINED / HARDENED / STATIC VALIDATOR PASSING / PAYLOAD AUDITED / LOADER/SAVE AUDITED / API SURFACE AUDITED / COMMAND AUDITED / RUNTIME PLAYBOOK DEFINED / PENDING RUNTIME VERIFICATION.

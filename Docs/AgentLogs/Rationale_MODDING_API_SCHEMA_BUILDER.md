# Rationale - MODDING_API_SCHEMA_BUILDER

Prompt: `MODDING_API_SCHEMA_BUILDER`
Role: TECH_RESEARCHER
Evidence class: STATIC_SOURCE / STATIC_DOC

## Decision 1 - Mod Signal Surface Must Be Projection, Not Direct SignalBus

Problem: First-party `SignalBus<T>` lanes are native snapshot infrastructure. Direct mod access to `NativeQueue<T>`, `NativeArray<T>.ReadOnly`, or first-party lane handles would let managed mod code pin itself to internal cadence, DataVault ownership, and payload layout.

Solution: Expose only read-only projected DTOs through `HectonAPI.Events.SubscribeProjected(Action<ModEventDto>)`, plus immutable native byte payload copies through `SubscribeNative`, and unmanaged command-result payloads through `HectonEventBus.Subscribe<TPayload>`.

Rejected Alternatives: Direct `SignalBus<T>.GetFrameSnapshot()` for mods was rejected because it exposes first-party timing and native ownership. JSON event feeds were rejected because they allocate strings and parse state in the callback path. Unity object references were rejected because existing `HectonAPI` throws `IllegalContractException` for GameObject, Transform, prefab, audio clip, and texture references.

Scalability potential: Low uses the existing projection cap of 10 events; Middle/High/Ultra can raise visual/diagnostic richness inside the bridge without exposing more mutable state. Ultra should add visual-overkill consumers in `VISUAL_SYNC`, not wider mod write privileges.

Hardware Impact: On i3/MX350, bounded DTO projection prevents unbounded mod callback storms and keeps first-party signal snapshots read-only. Estimated savings versus JSON callback fanout is unmeasured; static risk reduction is string allocation avoidance and bounded dispatch.

## Decision 2 - Current Allowed SignalBus List Is Narrow

Problem: `GlobalSignals.cs` defines many first-party signals, but source-backed mod projection currently samples only `CombatDamageSignal` and `WeatherChangedSignal` into `ModEventDto`. Treating every first-party signal as public would expose DataVault, save, AUP, input, persistence, and streaming internals.

Solution: `Signal_Schema.json` names only `SignalBus<CombatDamageSignal>` and `SignalBus<WeatherChangedSignal>` as current mod-projected `SignalBus<T>` lanes. Interaction and Crafting are documented separately as native event byte-copy lanes because they are not `SignalBus<T>` projections.

Rejected Alternatives: Exposing `PlayerStateSignal`, `PhysiologyStateSignal`, `SaveRequestSignal`, `AupShiftSignal`, or WFC/DataVault lanes as direct mod surfaces was rejected. They are either player-authority, persistence-authority, origin-shift authority, or native buffer ownership surfaces.

Scalability potential: Low: sample only core public projections. Middle: keep projected DTOs at default cap. High: allow additional read-only projections after Integrator approval. Ultra: add richer non-authoritative cinematic signals only if bridge cost is measured and bounded.

Hardware Impact: The documented cap avoids extra work on low-end silicon. A direct-all-lanes model could multiply callback volume during streaming or save activity and degrade frame stability.

## Decision 3 - Infinite O2 Cheat Sample Requires Engine-Owned Command Kernel

Problem: The requested cheat sample is intentionally dangerous. True infinite O2 mutates survival physiology and can corrupt save/state if a mod writes directly into player systems or DataVault-backed buffers.

Solution: The spec marks current true infinite O2 as not directly available. The allowed pattern is settings + read-only projected events + an engine-owned future `SurvivalOverride` command with TTL, clamp, save exclusion, telemetry, and revocation. Until that kernel exists, a mod can only persist its toggle and show UI, not change O2 truth.

Rejected Alternatives: Direct player component lookup, reflection, `GlobalRegistry` polling inside mod callbacks, and DataVault handle access were rejected. They bypass ownership and cannot be made deterministic under origin shifts/save loads.

Scalability potential: Low uses coarse TTL refresh and no extra presentation. Middle/High add UI feedback only. Ultra can add non-authoritative visor/VFX confirmation, never more authority.

Hardware Impact: Command batching through `ModCommandDispatcher` keeps writes in late-frame bounded queues. Direct per-frame managed polling would risk allocation and callback stalls on i3/MX350.

## Decision 4 - Unmanaged Structs Over JSON

Problem: Mod events cross managed, native, Burst, and persistence-adjacent boundaries. JSON payloads require strings, parsing, schema drift handling, allocations, and culture-sensitive numeric conversion.

Solution: The spec requires blittable/unmanaged DTOs with explicit or documented layout, numeric event hashes, bounded payload sizes, and version fields where needed.

Rejected Alternatives: JSON events, string event names, dictionaries, and dynamic object payloads were rejected. They conflict with zero-GC hot paths and Burst/native layout rules.

Scalability potential: Low consumes minimal 64-byte DTOs. Middle/High can add additional event kinds behind new fixed DTO versions. Ultra can add richer presentation-only event streams after profiler proof.

Hardware Impact: Fixed-size DTOs preserve cache behavior and avoid per-event parse costs. Exact microseconds saved are not claimed without profiler evidence.

## Decision 5 - Compile Proof Boundary

Problem: Batch protocol asks for compile verification, but this task changed only docs/json and the current workspace scan found no `.csproj` or `.sln` build target.

Solution: Validate the JSON schema with `ConvertFrom-Json`, run ASCII and whitespace checks on touched files, and record compile as `BLOCKED_BY_ENVIRONMENT` instead of inventing a green build.

Rejected Alternatives: Running `dotnet build` without a project would only prove there is no default project. Claiming compile success from old docs or agent logs was rejected by the evidence mandate.

Scalability potential: No runtime code changed. The mod API spec itself preserves low-tier caps and gives high/ultra tiers only read-only visual/diagnostic expansion after profiling.

Hardware Impact: No direct hardware impact from docs. The specified bridge keeps low-end callback pressure bounded and reserves high-end expansion for presentation-only DTOs.

## Decision 6 - Public Facade Matrix Added After Hardening Pass

Problem: The first schema version correctly named the projected lanes, but it did not map the entire public `HectonAPI` facade. That left a documentation gap where implementers could confuse internal methods and first-party source classes with supported mod rights.

Solution: Added `publicApiSurface` to `Signal_Schema.json` and `Public Facade Matrix` to `Mod_API_Specification.md`. Each surface is classified as read-only, cold registration, presentation, diagnostic, mod-owned save text, or engine-validated write request. Internal Unity-object methods are explicitly marked forbidden or throwing.

Rejected Alternatives: Leaving the API surface implicit was rejected because it encourages direct class spelunking. Exposing `HectonAPI.Assets.LoadPrefab`, `World.TryGetPlayerObject`, `World.TryGetPlayerTransform`, or persistent prefab spawn/despawn was rejected because source throws `IllegalContractException` and the architecture forbids Unity object references for mods.

Scalability potential: Low: mod callbacks stay hash/DTO only. Middle: cold registration APIs can add content without widening hot-path event transport. High/Ultra: richer visual overkill remains presentation-only through validated command/resource hashes, not live Unity references.

Hardware Impact: On i3/MX350, the facade split prevents hot-path use of managed strings, dictionaries, lists, and Unity objects. Exact microseconds saved are not claimed; the static gain is preventing a slower API shape from being treated as approved.

## Decision 7 - Payload Layout Table Added

Problem: Mod API implementation depends on exact unmanaged payload shape. A prose-only DTO description is insufficient when command packets and event payloads cross native queues, Burst-facing jobs, and managed mod callbacks.

Solution: Added `payloadLayouts` to the JSON schema and a payload table to the Markdown spec. `ModEventDto` records explicit byte offsets. `ModCommand` records its fixed 64-byte payload and packed `Payload0` semantics. AUP, raycast, rejection, render, and memory eviction payloads are listed with source layout classification.

Rejected Alternatives: Documenting only event names was rejected because it does not protect AOT/Burst/native compatibility. JSON schema-only generic properties were rejected because mod authors need concrete C# field contracts and implementers need source-backed acceptance gates.

Scalability potential: Low consumes only small fixed payloads and command caps. Middle/High can add new fixed payload versions without changing existing offsets. Ultra can add presentation-only payloads after profiler proof, still as unmanaged structs.

Hardware Impact: Fixed payloads preserve cache predictability and avoid parser allocation. No measured microsecond delta exists in this docs-only pass.

## Decision 8 - Full Signal Audit Matrix Added

Problem: The allowed-lane documentation proved what mods can subscribe to, but it did not give a source-wide denial artifact for the other first-party `ISignal` structs. With 129 current signal structs in `GlobalSignals.cs`, omission could be misread later as "not audited" instead of "forbidden by default."

Solution: Added `Docs/Modding/Signal_Audit_Matrix.md`. It records the extraction command, total count 129, the two projected signals, all 127 denied-by-default signal names, high-risk groups, and a consistency gate requiring schema/spec/audit updates if the count changes.

Rejected Alternatives: Listing only high-risk examples was rejected because it still leaves lower-risk presentation or telemetry signals ambiguous. Exposing all public structs was rejected because public C# visibility is not equivalent to mod API visibility.

Scalability potential: Low keeps the projection surface at two capped lanes. Middle/High/Ultra can add new projections only by explicit schema/audit expansion, with visual/diagnostic richness kept read-only and presentation-bound.

Hardware Impact: The audit itself has no runtime cost. It prevents accidental mod callback expansion across 127 internal lanes, which would be a likely callback storm and allocation risk on low-end hardware.

## Decision 9 - Static Drift Validator Added

Problem: The mod API docs were source-backed, but enforcement still depended on humans rerunning several commands and comparing counts manually. With parallel agents editing `GlobalSignals.cs` and mod bridge code, that is not durable.

Solution: Added `Docs/Modding/Validate_Mod_API_Static.ps1`. It parses `Signal_Schema.json`, extracts current `ISignal` structs, extracts `SignalBus<T>` usages from `ModEventProjectionBridge.cs`, checks schema/bridge parity, checks audit matrix coverage for every current signal, and checks the spec still contains the runtime verification gate. The schema was promoted to revision 3 and now records the validator path plus last-known pass values.

Rejected Alternatives: Keeping validation as prose was rejected because it cannot fail a future edit. Adding Unity runtime tests was not done in this pass because no Unity/MCP runtime is available and no C# source was changed.

Scalability potential: Low: static drift fails before runtime exposure grows. Middle/High/Ultra: new projected lanes can be added only with matching schema/audit/spec updates and then runtime profiling.

Hardware Impact: No runtime impact. The validator is offline. It prevents accidental callback fanout across internal signal lanes before it can cost frame time on MX350-class hardware.

## Decision 10 - Runtime Verification Playbook Added

Problem: The status correctly remained `PENDING RUNTIME VERIFICATION`, but the exact Unity proof sequence was scattered across prose. A later agent could under-test the bridge and incorrectly mark the API verified after only static checks.

Solution: Added `Docs/Modding/Runtime_Verification_Playbook.md`. It defines the required test mod, lifecycle checks, projected event checks, native byte event checks, command result checks, command flood and memory eviction gates, teardown checks, GC/profiler evidence format, failure report format, and final pass criteria. The schema is revision 4 and the static validator now requires the playbook and key pass criteria.

Rejected Alternatives: Marking runtime verification complete from static docs was rejected. Creating runtime C# smoke code in this pass was rejected because the current assignment is a schema/spec research role, no Unity runtime is available here, and no compile target exists in the workspace.

Scalability potential: Low tier must prove capped 10-event projection and 0 B hot-path dispatch. High/Ultra can expand visual/diagnostic richness only after the playbook passes and profiler evidence exists.

Hardware Impact: No direct runtime impact from the playbook. It forces future runtime proof of 0 B/frame hot-path projection dispatch and prevents unmeasured callback fanout on MX350-class hardware.

## Decision 11 - Command Audit Matrix Added

Problem: The signal side was fully audited, but command write authority was still summarized as an opcode list. That left drift risk around target validation, AUP requirements, rejection reasons, render instance lane caps, and result payloads.

Solution: Added `Docs/Modding/Command_Audit_Matrix.md` and promoted the schema to revision 5. The audit records 8 accepted non-none opcodes, valid target systems, AUP requirement for every current opcode, non-opcode render instance lane, hard limits, 19 rejection reasons including `None`, and command security rules. The static validator now parses `ModCommandOpcode`, `ModCommandTargetSystem`, and `ModCommandRejectReason` from source and checks the schema/audit/spec/playbook against them.

Rejected Alternatives: Trusting the existing short `acceptedOpcodes` array was rejected because command write surfaces need stronger traceability than read-only signals. Creating gameplay kernels for missing/future commands was rejected; this role defines the contract and must not expand authority without implementation ownership.

Scalability potential: Low tier keeps all command writes behind AUP rebase and quotas. Middle/High/Ultra can add richer effects through engine-owned kernels only after the command audit, schema, validator, and runtime playbook are updated.

Hardware Impact: No runtime impact from docs. The audit prevents command fanout and invalid target routing from entering runtime unnoticed, protecting MX350-class frame stability.

## Decision 12 - HectonAPI Surface Audit Matrix Added

Problem: Signals and command authority were audited, but `HectonAPI.cs` itself remained a drift risk. A new public facade method or a visibility change could bypass the schema/spec review if the validator did not inspect the facade source.

Solution: Added `Docs/Modding/API_Surface_Audit_Matrix.md` and promoted the schema to revision 6. The audit records 16 public nested surfaces, 34 public static methods, 2 public static properties, and 9 internal forbidden methods. The static validator now parses `HectonAPI.cs` for these counts and checks audit coverage, spec link, and runtime playbook link.

Rejected Alternatives: Treating the facade matrix inside `Mod_API_Specification.md` as enough was rejected because it could not fail source drift. Expanding public access to internal methods was rejected because those methods expose or imply Unity object, prefab, audio clip, texture, persistent instance, or managed event paths forbidden to mods.

Scalability potential: Low tier keeps public API surfaces hash/unmanaged/cold only. Middle/High/Ultra can add richer mod-facing capabilities only when the facade audit, schema, command/signal audits, validator, and runtime playbook all update together.

Hardware Impact: No runtime impact from docs. The audit prevents managed or Unity-object facade drift from becoming a hot-path allocation or object-reference leak on low-end hardware.

## Decision 13 - Payload Layout Audit Matrix Added

Problem: The schema contained payload layout data, but validator enforcement did not prove the actual unmanaged byte contracts. `ModEventDto` offsets and fixed 64-byte packets are mod ABI, not documentation decoration.

Solution: Added `Docs/Modding/Payload_Layout_Audit_Matrix.md` and promoted schema to revision 7. The validator now parses `ModEventContracts.cs`, `ModCommandDispatcher.cs`, and `ModSpatialContracts.cs` to confirm `ModEventDto` explicit size 64, 15 field offsets, `ModCommand` sequential size 64, and `ModAupResponse` sequential size 64. Spec and runtime playbook now link the payload audit.

Rejected Alternatives: Manual offset review was rejected because one field shift can break Burst/native/AOT compatibility. Runtime-only detection was rejected because bad layout should fail before Unity execution.

Scalability potential: Low tier keeps payloads compact and fixed. Middle/High/Ultra can add richer DTOs only by adding new fixed-version payloads, not by mutating existing offsets silently.

Hardware Impact: No runtime impact from docs. The audit protects cache predictability and prevents hidden boxing/parsing fallbacks caused by unstable payload ABI.

## Decision 14 - Loader And Save Boundary Audit Added

Problem: The schema already described lifecycle phases, but the validator did not enforce `ModLoader.CurrentAPIVersion`, manifest shape, `IHectonMod` callbacks, runtime info fields, or `HectonAPI.SaveState` payload limits. Loader/save drift can widen mod authority without touching signal or command code.

Solution: Added `Docs/Modding/Loader_Save_Audit_Matrix.md` and promoted schema to revision 8. The validator now parses `ModLoader.cs`, `IHectonMod.cs`, `ModMetadata.cs`, `ModRuntimeInfo.cs`, `ModRuntimeState.cs`, `SaveBinaryStorage.cs`, and `SaveBinaryPayloadCodec.cs` to confirm API version 2, `mod.json`, 9 manifest fields, 8 metadata fields, 7 runtime info fields, 3 lifecycle methods, 2 SaveState public methods, `m8v1:` prefix, and 16352-byte max mod payload.

Rejected Alternatives: Treating loader and save as out-of-scope was rejected because mod package loading and mod-owned persistence are public API contracts. Runtime-only detection was rejected because manifest/save drift should fail before Unity execution.

Scalability potential: Low tier keeps mod package execution small, scoped, and unloadable. Middle/High/Ultra can add richer content packages only by changing the audited manifest/save contract and then proving runtime callback and save behavior through the playbook.

Hardware Impact: No runtime impact from docs. The audit prevents silent expansion of managed loader or save payload paths that could increase memory pressure or package callback fanout on i3/MX350-class hardware.

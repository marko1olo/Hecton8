# Rationale_MODDING_SDK_AUDIT

Date: 2026-05-26
Evidence class: STATIC_SOURCE / STATIC_DOC

## Decision 1 - SDK builder manifest parity

Problem: `ModBuilderWindow.ModManifestData` emitted a 7-field `mod.json`, while `ModLoader.ModManifest` requires `RequiredAPIVersion > 0` and consumes `ModPriority`. Packages produced by the SDK builder were structurally invalid for the loader unless manually repaired.

Solution: Make the SDK builder emit the full 9-field manifest and validate `RequiredAPIVersion` against the current loader API value. Add a static validator check that extracts builder manifest fields and compares them to the loader manifest fields.

Rejected Alternatives: A loader fallback that accepts missing `RequiredAPIVersion` was rejected because it weakens the fail-closed package contract. A doc-only warning was rejected because it preserves a broken SDK output path.

Scalability potential: Low tier sees no runtime cost because this is editor/package-time validation. Middle tier and high tier avoid wasted boot retries and support ambiguity. Ultra tier can layer richer SDK validation on the same manifest contract without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Practical gain is cold-path: invalid packages fail before runtime loading instead of spending loader/debug time.

## Decision 2 - Static layout gate must accept source constants

Problem: `Validate_Mod_API_Static.ps1` failed before SDK manifest checks because `ModAupResponse` uses `Size = ModSpatialContractLayout.AupResponseStrideBytes`, while the validator only accepted numeric literal sizes. The payload contract was still explicit and 64 bytes, but the gate could no longer prove it.

Solution: Teach the validator to resolve either a numeric literal or a same-source `const int` token for `ModAupResponse` layout size. The source layout remains unchanged.

Rejected Alternatives: Replacing the source constant with a literal was rejected because it would duplicate layout facts and make future payload audit harder. Ignoring the validator failure was rejected because static gates are the only current proof path for this API surface.

Scalability potential: Low/Middle/High/Ultra tiers all keep the same 64-byte unmanaged response contract. The change only restores static proof reliability.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. It removes a false build-gate failure with no runtime code cost.

## Decision 3 - Signal inventory source moved out of GlobalSignals shell

Problem: `Validate_Mod_API_Static.ps1` still read `Assets/_Project/Scripts/Core/GlobalSignals.cs`, but that file is now a compatibility shell. The real signal payloads live in `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads*.cs`. Static source count became 0 against schema 162, and the audit missed new signal payloads.

Solution: Move the mod API static inventory to `GlobalSignalPayloads*.cs`, use a regex that accepts `readonly` and `partial` modifiers and rejects `ISignalSnapshotTransformer`, then update schema/audit/playbook counts to 173 total, 2 projected, 171 denied.

Rejected Alternatives: Reading the entire `Core` tree was rejected because it would pull in mock/test/domain contract signals not covered by the existing mod projection audit. Keeping the old `GlobalSignals.cs` path was rejected because it proves an empty shell.

Scalability potential: Low tier keeps the same deny-by-default mod boundary. Middle/High/Ultra can add signals only through explicit schema and projection work; new public C# structs do not become mod API by accident.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Static gate reliability improves; no runtime code path changed.

## Decision 4 - Compile gate deferred under CPU protocol

Problem: The editor C# change should normally receive compile proof, but the project rule forbids launching dotnet build when CPU is above 50 percent or `csc.exe` is active. Sampled CPU was 77.7 percent. No `dotnet` or `csc` process was found, but CPU exceeded the hard threshold.

Solution: Run the mod API static validator and diff hygiene checks now; record Unity/dotnet compile as deferred by protocol instead of violating the build rule.

Rejected Alternatives: Starting a build under 77.7 percent CPU was rejected because it violates the explicit multi-agent resource rule. Marking compile as passed without running it was rejected because that is fake evidence.

Scalability potential: Keeps other agents' compile/editor work from being starved. Low/Middle/High/Ultra runtime behavior is unchanged.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Avoided adding build contention during a high CPU window.

## Decision 5 - FutureCommand constants are not public opcode authority

Problem: `FutureCommandOpcodes` contained `SurvivalOverride`, `HapticPulse`, and `SubtitleCue` constants, `allowed_opcodes.csv` listed them, and `GenerateEmergencyOpcodeMap()` inserted them into opcode records. The reservation doc says those kernels are not public API until an owning runtime system and proof exist. Source was granting authority the docs explicitly denied.

Solution: Keep the hash constants and tuning CSV rows as dormant reservation data, but remove the reserved kernels from runtime opcode-record insertion and from `allowed_opcodes.csv`. Add `IsRuntimeAllowedFutureCommandOpcode()` to make editor CSV ingest fail closed, and make the static validator compare `allowed_opcodes.csv` to `GenerateEmergencyMockOpcodes()` rather than to every constant.

Rejected Alternatives: Deleting the constants was rejected because SDK/workbench reservations still need stable names and hashes. Leaving the default runtime map active was rejected because it lets unowned gameplay/presentation kernels appear public without owner proof.

Scalability potential: Low tier avoids spending any runtime work on unowned kernels. Middle/High/Ultra can later enable kernels explicitly with owner telemetry, quality-scaled budgets, and runtime proof without changing packet layout.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame in normal play because the change removes authority, not a hot loop. Worst-case spam now fails before reserved kernel routing, avoiding useless queue and signal work.

## Decision 6 - Public HectonAPI must not expose ScriptableObject handles

Problem: Public `HectonAPI` methods accepted or returned `ItemData`, `RecipeData`, and `BuildableData`, all of which are `ScriptableObject` types. That contradicted `directUnityObjectReferencesForMods=false` and the resource/content audit rule that no ScriptableObject handle crosses the public facade.

Solution: Convert the five offending methods to internal forbidden guards that throw `IllegalContractException`: `RegisterCustomItem`, `TryFindItem`, `RegisterRecipe`, `RegisterBuildable`, and `TryFindBuildable`. Update schema/docs to public API methods `30`, internal forbidden methods `14`, public content methods `9`, and add a validator check that fails if those Unity-object signatures become public again.

Rejected Alternatives: Keeping the methods public with stronger comments was rejected because signatures are the contract. Returning copied DTOs was rejected for this pass because no source-backed DTO contract exists yet; inventing one would create a larger cross-domain API without proof.

Scalability potential: Low tier avoids asset-handle lifetime bugs and object graph retention from mods. Middle/High/Ultra can still support rich content later through hash/CRC/binary manifests and quality-scaled owner ingestion, not public Unity object references.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame in current envelope-only mode. Risk reduction is memory/lifetime safety: mods cannot retain authored Unity assets through this facade.

## Decision 7 - Direct owner mutation facades require quarantine

Problem: `HectonAPI.Crafting.RegisterRecycleYield`, `HectonAPI.Recycling.ProcessRecycle`, and `HectonAPI.Ecosystem.RegisterBiomeMutation` exposed direct owner mutation/overlay routes. They bypassed the one-owner command doctrine: no mod owner record, no unload revocation proof, no runtime telemetry proof, and no first-party command kernel boundary.

Solution: Convert the three methods to internal forbidden guards that throw `IllegalContractException`. Keep future exposure behind content manifests or `FutureCommandEnvelope` owner routes only after owner telemetry and revocation proof exist.

Rejected Alternatives: Keeping the methods public as convenience wrappers was rejected because direct inventory mutation and ecosystem overlays are gameplay authority, not presentation. Adding comments was rejected because the public signature itself is the API promise.

Scalability potential: Low tier avoids extra registry mutation and impossible-to-revoke overlays. Middle/High/Ultra can reintroduce richer recycle/ecosystem features through quality-scaled owner kernels with explicit budgets, telemetry, and unload rollback.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame in envelope-only mode. Risk reduction is authority correctness and avoiding orphaned mod state.

## Decision 8 - Non-command facade calls need active mod scope

Problem: `Input.GetButtonMask`, UI notification/settings calls, and `World.TryGetPlayerEntityHash` could be invoked without an active mod execution scope. That created anonymous reads/presentation writes/settings registration that could not be attributed or revoked by mod id.

Solution: Add `ThrowIfNoActiveMod` and `ThrowIfScopeMismatch` helpers. Guard Input, UI notification, UI setting registration, and World player-hash lookup; setting `modId` must match the active mod scope.

Rejected Alternatives: Letting null scope fall back to `"anonymous"` was rejected because anonymous mod state breaks isolation and disable paths. Moving these checks into every callee registry was rejected because the public facade is the contract boundary.

Scalability potential: Low tier avoids ownerless settings and UI spam. Middle/High/Ultra can add richer presentation/settings surfaces while preserving a single mod owner route and deterministic unload.

Hardware Impact: Estimated runtime cost on i3/MX350 is 0 to low microseconds per managed mod API call. These are not Burst/hot simulation paths.

## Decision 9 - Event route must be single-owner and facade-only

Problem: `HectonEventBus` was public, and public `HectonAPI.Events` methods passed nullable `subscriberId` through to bus code that can fall back to `ModExecutionScope.CurrentModId` or `"anonymous"`. Envelope-only mode blocked this today, but reopening managed callbacks would allow anonymous subscribers or a direct `HectonEventBus` bypass outside the facade.

Solution: Make `HectonEventBus` internal first-party infrastructure. Public event access now routes only through `HectonAPI.Events`; subscriptions require active `ModExecutionScope`, explicit `subscriberId` must match the active mod id, publish requires active scope, and unsubscribe rejects a token owned by another active mod. The static validator now fails if EventBus becomes public again or the event scope guards are removed.

Rejected Alternatives: Relying on envelope-only quarantine was rejected because it leaves a dormant managed-mode trap. Adding only documentation was rejected because direct public class access is a second route. Making `HectonEventSubscription.Dispose` scope-aware was rejected for this pass because `OnUnload` already runs under `ModExecutionScope`, and changing `IDisposable.Dispose` semantics can break owner-side cleanup patterns; the facade `Unsubscribe` is now scoped.

Scalability potential: Low tier keeps event bridges disabled or cheap without anonymous subscriber leaks. Middle/High/Ultra can reopen managed read-only projections with richer DTOs while retaining one owner, one route, one revoke path.

Hardware Impact: Estimated runtime cost on i3/MX350 is 0 in envelope-only mode. If managed events are reopened, subscription-time branch/string-compare cost is cold and not frame-critical.

## Decision 10 - Loader diagnostics are engine UI only

Problem: `HectonAPI.Mods.GetLoadedMods(List<ModRuntimeInfo>)` exposed all-package loader diagnostics through the public facade. `ModRuntimeInfo` includes `DirectoryPath`, `AssetBundlePath`, status, and failure reason, which are useful for engine UI but not a runtime mod right.

Solution: Make `HectonAPI.Mods` and `GetLoadedMods` internal and add validator checks that reject a public diagnostics facade. Existing engine UI already uses `ModLoader.CollectRuntimeInfo` directly.

Rejected Alternatives: Redacting fields inside `ModRuntimeInfo` was rejected for this pass because the engine UI needs the full diagnostic descriptor. Keeping the route public with documentation was rejected because the public method is the API promise.

Scalability potential: Low tier avoids extra managed list copies for mod callers. Middle/High/Ultra can expose curated community diagnostics later through redacted DTOs without leaking loader paths or all-package state.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. The fix removes an unsafe cold diagnostics route, not a frame path.

## Decision 11 - FutureCommand sandbox control plane must be internal

Problem: `FutureCommandSandboxValidator` was public and carried lifecycle, raw stream ingress, external queue drain, approved-asset registration, opcode gates, tuning, thermal pressure, telemetry snapshot, CSV reload, self-audit, and blackbox dump methods. That made engine/package-loader/editor controls callable as if they were SDK runtime API.

Solution: Keep the public packet contract (`FutureCommandEnvelope`, opcode constants, and signal DTOs required across assemblies), but make the validator and control-plane structs internal: opcode records, tuning, modder leases/counters, approved asset records, ring state, telemetry entries, mock queue, and malicious injection job. Add static validator checks for these visibility rules.

Rejected Alternatives: Public methods with comments were rejected because control-plane calls can change budgets, allowlists, approved assets, or fault dumps. Moving public methods behind runtime `ModExecutionScope` checks was rejected because package-loader/editor ingress is not a mod-owned facade and must remain first-party.

Scalability potential: Low tier keeps the cheapest public path: one owned envelope request. Middle/High/Ultra can use internal bulk streams, scheduled validation, richer editor telemetry, and visual-overkill tooling without giving runtime mods direct tuning or queue authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Risk reduction is authority and memory safety: mods cannot mutate sandbox budgets or enqueue through native/raw bypasses.

## Decision 12 - Legacy event hooks and managed kernels are not extension points

Problem: `HectonModHooks` was public and could publish player/biome lifecycle payloads into the mod event bus if managed events are reopened. `IModCommandKernel` was public while registration was internal/quarantined, which falsely implied managed command kernel plugins are supported.

Solution: Make `HectonModHooks` and `IModCommandKernel` internal and add validator checks. Public mods stay on `HectonAPI.Events` for reads and `HectonAPI.Commands.RequestFuture` for writes.

Rejected Alternatives: Leaving the types public because they are dormant was rejected; dormant public symbols become accidental SDK contracts. Throwing inside public hooks was rejected because first-party owners still need an internal publication route.

Scalability potential: Low tier keeps event publication single-owner and cheap. Middle/High/Ultra can reopen managed projections or command kernels only through explicit owner routes and runtime proof.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. The fix prevents future route ambiguity rather than reducing current frame time.

## Decision 13 - RequestFuture must bind envelope ownership to active scope

Problem: `HectonAPI.Commands.RequestFuture` accepted a `FutureCommandEnvelope` without checking active `ModExecutionScope` or matching `ModderSignature`. A managed caller could submit anonymous packets or forge another mod hash before the sandbox validator saw the envelope.

Solution: Add `ThrowIfSignatureMismatch("Commands.RequestFuture", envelope.ModderSignature)`. Public `RequestFuture` now requires active mod scope and exact signature/hash match; package-loader/editor bulk ingress uses internal validator routes.

Rejected Alternatives: Letting the sandbox validator reject unknown signatures later was rejected because the public facade must enforce ownership before queue insertion. Overwriting `ModderSignature` in the facade was rejected because envelope bytes and integrity hash are part of the packet contract.

Scalability potential: Low tier pays one managed branch and integer compare per public managed request. Middle/High/Ultra keep deterministic ownership while internal bulk/package routes can still batch envelopes without public bypass.

Hardware Impact: Estimated runtime cost on i3/MX350 is low microseconds per managed `RequestFuture` call and 0 us/frame in envelope-only package runtime where managed mod entry is disabled. It prevents forged queue ownership.

## Decision 14 - Remaining public cold facades still need one owner

Problem: `HectonAPI.Resources`, `Telemetry.Publish`, `Localization.InjectBabelEnvelope`, and `SaveState` were not all using the same facade-level active `ModExecutionScope` guard. Some paths depended on lower-level registry checks or hand-written local checks. That leaves future managed-mode reopening vulnerable to anonymous resource/hash/save/telemetry calls.

Solution: Add shared `ThrowIfNoActiveMod` checks to resource resolution, telemetry, localization injection, and save-state methods. Add a direct `ModResourceProxy` guard before envelope-only fallback so callers cannot bypass the facade by caching `HectonAPI.Resources.Proxy`.

Rejected Alternatives: Relying on `ModResourceRegistry.TryRegister` was rejected because the public proxy is also a route. Returning `false` for anonymous resource calls in envelope-only mode was rejected because failed ownership must be explicit, not silently unattributed.

Scalability potential: Low tier keeps resource and save paths cold and owner-attributed. Middle/High/Ultra can add richer hash-backed content and telemetry without changing the ownership rule.

Hardware Impact: Estimated runtime cost on i3/MX350 is low microseconds per cold managed SDK call and 0 us/frame in normal envelope-only gameplay.

## Decision 15 - Loader diagnostics descriptors are not SDK data

Problem: `HectonAPI.Mods.GetLoadedMods` was internal, but `ModRuntimeInfo` and `ModLoadStatus` remained public. `ModRuntimeInfo` contains `DirectoryPath`, `AssetBundlePath`, status, and failure text. Even without a public getter, the public type shape implied a stable SDK descriptor and preserved path-bearing fields as public contract.

Solution: Make `ModRuntimeInfo` and `ModLoadStatus` internal engine UI diagnostics. Change `ModMenuModEntryView.Bind(ModRuntimeInfo)` to internal so the public UI component does not expose an internal descriptor through a public method. Add static validator checks.

Rejected Alternatives: Keeping the type public because the facade was closed was rejected; public DTOs become accidental SDK commitments. Redacting fields was rejected for this pass because engine UI still needs full diagnostics and no public redacted DTO route is required.

Scalability potential: Low tier avoids unnecessary public diagnostic copy paths. Middle/High/Ultra can later expose curated loader status through redacted hash-only DTOs if there is a real mod-facing need.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. The fix removes an unsafe cold contract.

## Decision 16 - FutureCommand output lanes must not be public mod payloads

Problem: `FutureCommandSandboxValidator` and its control-plane structs were internal, but FutureCommand output `ISignal` DTOs remained public. Since `SignalBus<T>` is first-party infrastructure, public DTOs falsely imply that managed mods may write output lanes directly instead of submitting validated `FutureCommandEnvelope` packets.

Solution: Make `ModSpawnRequestSignal`, `ModAssetReferenceSignal`, `MockAcousticSignal`, `MockDamageSignal`, `ModFutureDevNullSignal`, `SurvivalOverrideSignal`, `ModHapticPulseSignal`, and `ModSubtitleCueSignal` internal. Keep `FutureCommandEnvelope` and opcode constants public as the only supported packet contract. Add validator checks that fail if output lane DTOs become public again.

Rejected Alternatives: Keeping DTOs public as harmless data was rejected because payload visibility shapes SDK behavior. Hiding only reserved lanes was rejected because active output lanes are also first-party authority surfaces.

Scalability potential: Low tier keeps one bounded envelope ingress. Middle/High/Ultra can run visual-overkill output lanes internally without exposing direct SignalBus authority to mods.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Risk reduction is route correctness: no public direct lane payloads for command output.

## Decision 17 - Registry invalidation and menu setting snapshots are not SDK contracts

Problem: `ModRegistryEventType`, `ModRegistryEventPayload`, `IModRegistryEventListener`, `ModSettingKind`, and `ModSettingView` were public. They are engine UI/invalidation and menu snapshot data, not mod runtime contracts. Public visibility implied that mods could consume registry invalidation or menu DTOs directly instead of using `HectonAPI.UI.RegisterSetting`.

Solution: Make the registry event and setting snapshot types internal. Change menu setting view `Bind(ModSettingView)` methods to internal and convert `Fabricator`/`ModMenuUIController` listener methods to explicit internal-interface implementations. Add validator checks and schema revision 30 evidence.

Rejected Alternatives: Keeping the types public with "internal use" comments was rejected because public signatures define the SDK. Replacing the route with a new public redacted DTO was rejected because no mod-facing consumer need exists; current requirement is first-party menu redraw only.

Scalability potential: Low tier keeps menu invalidation cold and first-party only. Middle/High/Ultra can add richer mod settings UI later through facade-owned descriptors and quality-scaled presentation, without exposing engine invalidation lanes.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. The change removes a false contract and direct UI lane route; it does not add a frame-path branch or allocation.

## Decision 18 - Public property routes still need active mod scope

Problem: `HectonAPI.Resources.Proxy` and `HectonAPI.World.IsGameReady` were public property routes without an active `ModExecutionScope` guard. Methods around them were guarded, but these read-looking properties still allowed anonymous access to a resource proxy handle and bootstrap readiness state.

Solution: Keep the public property signatures stable, but route them through `GetProxy()` and `GetIsGameReady()` guarded accessors. Add static validator checks and schema revision 31 so the property route cannot silently revert to direct expression-bodied access.

Rejected Alternatives: Relying on downstream `ModResourceProxy` method guards was rejected because the public facade property itself is a route. Leaving `IsGameReady` anonymous was rejected because public mod facade calls must be owner-attributed even when read-only.

Scalability potential: Low tier keeps cold property calls cheap and owner-attributed. Middle/High/Ultra can add richer readiness/resource diagnostics later without creating a second anonymous route.

Hardware Impact: Estimated runtime cost on i3/MX350 is low microseconds per managed property call. Normal gameplay frame cost is 0 us/frame measured/claimed because this is a cold SDK facade route, not a simulation loop.

## Decision 19 - Subscription token Dispose must not bypass owner scope

Problem: `HectonAPI.Events.Unsubscribe(subscription)` validated active mod ownership before delegating to `Dispose`, but direct public `HectonEventSubscription.Dispose()` called the channel `Unsubscribe` without proving active `ModExecutionScope`. The docs and samples tell mods to dispose tokens directly in `IHectonMod.OnUnload`, so `Dispose` itself is a public lifetime route, not a private implementation detail.

Solution: Add an internal owner-scope requirement bit to `HectonEventSubscription`. Tokens created while a mod scope is active now require active scope and ordinal subscriber-id match before direct `Dispose` can unsubscribe. Tokens created by first-party/internal code outside mod scope remain disposable without that guard. Extend the static validator and schema revision 32 so every token constructor call must pass `ModExecutionScope.HasActiveMod`.

Rejected Alternatives: Forcing every token, including internal anonymous first-party tokens, to require a mod scope was rejected because it would break non-mod cleanup routes. Relying only on `HectonAPI.Events.Unsubscribe` was rejected because direct `IDisposable.Dispose` is already documented and used by sample code. Making `Dispose` silently no-op on wrong scope was rejected because failed ownership must be explicit.

Scalability potential: Low tier keeps subscription teardown as a cold managed call with one branch and one ordinal compare only for mod-owned tokens. Middle/High/Ultra can reopen richer event projections without creating a second unauthorized lifetime route; the same owner proof scales to additional native/projected lanes.

Hardware Impact: Estimated runtime cost on i3/MX350 is low microseconds per mod-owned subscription disposal and 0 us/frame in normal gameplay. No hot simulation loop or Burst job path changed.

## Decision 20 - Mod world persistence service is not SDK API

Problem: `ModWorldPersistenceManager` was a public concrete `MonoBehaviour`, and `GlobalRegistry.ModWorldPersistence`, `RegisterModWorldPersistenceRuntime`, and `UnregisterModWorldPersistenceRuntime` exposed that concrete engine save/spawn service publicly. The `HectonAPI.World.SpawnPersistentPrefab` and `DespawnPersistentInstance` facade methods were already internal forbidden methods, but the concrete service and registry route still created an accidental SDK/control-plane path.

Solution: Make `ModWorldPersistenceManager` internal and change the `GlobalRegistry` mod-world-persistence property/register/unregister route to internal. Keep bootstrap, loader, and save-owner access inside the same runtime assembly. Add static validator checks and schema revision 33 so the concrete service and registry route cannot become public again.

Rejected Alternatives: Leaving the class public because Unity can create the component was rejected; runtime bootstrap can still add an internal component from the same assembly. Keeping the registry route public while relying on internal facade methods was rejected because a public concrete service route bypasses the facade boundary. Moving persistent spawns into the public SDK was rejected because command-kernel ownership, AUP validation, unload revocation, and runtime proof are not present.

Scalability potential: Low tier keeps persistent spawn/save as engine-owned cold infrastructure and avoids mod-driven service mutation. Middle/High/Ultra can later expose persistent spawn through a validated `FutureCommandEnvelope` opcode with quality-scaled spawn budgets and visual overkill asset handling, without changing the service owner.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This removes a public authority leak; no hot path or Burst job cost changed.

## Decision 21 - Public mod payload layouts must be fixed, not implied

Problem: The public mod event/spatial payload contract was drifting. `ModPlayerSpawnedEvent` and `ModBiomeChangedEvent` were public unmanaged callback payloads with default sequential layout and no fixed size; `ModBiomeChangedEvent` would be a 20-byte DTO without explicit padding. Meanwhile schema/spec/docs still described spatial/result payloads as `Sequential` or `source-defined` even though source had explicit offsets and fixed sizes.

Solution: Make `ModPlayerSpawnedEvent` and `ModBiomeChangedEvent` explicit 24-byte structs with field offsets and padding. Update schema revision 34, payload/spec/runtime docs, and `Validate_Mod_API_Static.ps1` so public event/spatial payload sizes and schema `payloadLayouts` must match source explicit layouts.

Rejected Alternatives: Leaving default sequential layout was rejected because public SDK payloads crossing managed callbacks need platform-stable byte contracts. Updating Markdown without validator checks was rejected because the same drift would recur silently. Changing gameplay/event ownership was rejected because this pass is only layout contract hardening.

Scalability potential: Low tier keeps callback payloads compact and cache-aligned. Middle tier keeps deterministic byte-copy/event smoke tests. High tier can add richer telemetry through separate explicit payloads instead of bloating these DTOs. Ultra tier can project visual-overkill event consumers without changing gameplay truth layout or mod authority routes.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. The improvement is ABI/layout safety; no hot loop, allocation, SignalBus route, or native container ownership changed.

## Decision 22 - Publish must not impersonate engine-owned payload lanes

Problem: `HectonAPI.Events.Publish<TPayload>` required an active mod scope, but it accepted any unmanaged payload type. If managed event bridges are reopened, a mod could publish engine-owned DTOs such as `ModEventDto`, lifecycle payloads, command envelopes, command DTOs, and engine result/rejection payloads into the managed event bus. That contradicts the documented rule that public publish is only for mod-owned coordination payloads.

Solution: Add `ThrowIfEngineOwnedPublishPayload<TPayload>` before `HectonEventBus.Publish`. The guard rejects 11 engine-owned payload types: `ModEventDto`, `ModPlayerSpawnedEvent`, `ModBiomeChangedEvent`, `ModRaycastResultPayload`, `ModInteractionRejectedPayload`, `ModCriticalMemoryEvictionPayload`, `ModAupResponse`, `FutureCommandEnvelope`, `ModCommand`, `ModAupCommand`, and `ModRenderInstanceCommand`. Update schema revision 35, docs, playbook, and static validator so this route cannot silently reopen.

Rejected Alternatives: Making `HectonEventBus.Publish` inspect caller ownership was rejected because the public facade is the mod contract boundary and internal first-party publishers still need engine-owned routes. Removing public `Publish<TPayload>` entirely was rejected for this pass because docs retain it as a dormant managed-mode mod-owned coordination surface. Relying on envelope-only mode was rejected because it leaves a dormant route leak for the next managed-event enablement pass.

Scalability potential: Low tier keeps envelope-only runtime at 0 us/frame. Middle tier can reopen limited managed coordination events without engine DTO spoofing. High tier can add richer read-only projections as explicit engine-owned DTOs while keeping mod-owned publish separate. Ultra tier can run visual-overkill event telemetry internally without exposing command/result/projection DTO publication to mods.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Added cost is 0 in envelope-only mode because `ThrowIfEnvelopeOnly()` rejects first. If managed events are reopened, legal mod-owned publish pays type comparisons on a cold managed API path; illegal publish throws before bus dispatch and avoids callback fanout.

## Decision 23 - Legacy command quarantine must still be owner-scoped

Problem: `HectonAPI.Commands.Request`, `RequestAup`, and `RequestRenderInstance` were public legacy command facades that returned `false` while envelope-only mode is enforced, but they did not require an active `ModExecutionScope`. That made them anonymous probe routes in the public write surface and contradicted the public facade rule that calls must be owner-attributed even when the route is quarantined.

Solution: Add `ThrowIfNoActiveMod` to all three legacy command facades before the quarantine `false` return. Keep the public obsolete signatures for source-audit compatibility and do not alter `RequestFuture`. Update schema revision 36, docs, runtime playbook, and static validator so the guard is part of the contract.

Rejected Alternatives: Removing the legacy public methods was rejected because they are retained as source-audit compatibility surfaces and the public API count would churn. Leaving silent anonymous `false` returns was rejected because failed ownership must be explicit; otherwise external code can probe command availability without a mod owner. Routing legacy calls into `RequestFuture` was rejected because the packet layouts and authority rules are different.

Scalability potential: Low tier keeps envelope-only runtime at 0 us/frame. Middle tier gets deterministic fail-fast behavior if legacy managed calls are exercised by test fixtures. High and Ultra tiers can re-enable richer command tooling only through owned envelope paths or first-party internal bulk ingress, without preserving anonymous legacy command probes.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Added cost is low microseconds only when a legacy managed command facade is called; normal envelope-only runtime and Burst/native command validation are unchanged.

## Decision 24 - Public event quarantine must not leak before owner proof

Problem: Public `HectonAPI.Events` subscribe/publish facades checked envelope-only quarantine before proving active `ModExecutionScope`. Anonymous callers could therefore probe the public event surface and receive quarantine status without owner attribution, which contradicts the fail-fast public facade rule.

Solution: Reorder public `HectonAPI.Events` subscribe helpers and `Publish<TPayload>` so they require active mod scope, and subscriber owner match where applicable, before `ThrowIfEnvelopeOnly`. Add schema revision 37, static validator gates, and docs/playbook evidence for `publicEventFacadesRequireScopeBeforeEnvelopeOnly`.

Rejected Alternatives: Leaving the order unchanged was rejected because "blocked later" still exposes an anonymous probe route. Moving the check into `HectonEventBus` was rejected because the public facade is the SDK boundary and internal first-party bus routes have different ownership. Removing the public event methods was rejected because they remain source-audit compatibility surfaces for a future managed projection reopening.

Scalability potential: Low tier keeps envelope-only runtime at 0 us/frame and reports illegal access deterministically. Middle tier can reopen managed projection tests with one owner route. High and Ultra tiers can add richer read-only event projections and visual-overkill telemetry internally without anonymous public event probes or DTO spoofing.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Added cost is low microseconds only on cold illegal/managed SDK event calls; legal envelope-only gameplay remains unchanged.

## Decision 25 - Sandbox constants are control-plane data, not SDK API

Problem: `FutureCommandSandboxConstants` was public while it contained pending/staging capacities, tracked modder limits, approved asset capacity, telemetry capacity, command budget floors, thermal/fault hashes, kernel profile caps, and fallback flags. Even with `FutureCommandSandboxValidator` internal, those constants made sandbox tuning and control-plane details look like stable public SDK contract.

Solution: Make `FutureCommandSandboxConstants` internal and add `FutureCommandEnvelope.SizeBytes` as the only public size fact mods need for the 64-byte packet contract. Update schema revision 38, docs, playbook, and the static validator so the control constants cannot become public again without a failing gate.

Rejected Alternatives: Leaving the class public was rejected because public constants are still API promises and encourage mods to couple to internal budgets. Moving every constant to a new public "limits" class was rejected because runtime budget is quality, thermal, rollback, and owner-policy dependent. Duplicating the envelope size in docs only was rejected because source-level authors still need a stable compile-time size fact.

Scalability potential: Low tier keeps strict internal command budgets that can shrink under thermal pressure without mod ABI churn. Middle tier can expose richer SDK simulation reports without leaking runtime control constants. High and Ultra tiers can raise visual-overkill/internal command capacity through engine-owned tuning while the public envelope contract stays fixed at 64 bytes.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is compile-time/API surface hardening only; no queue capacity, native buffer, Burst job, or per-frame command budget changed.

## Decision 26 - Internal registry listener cannot appear in public component base lists

Problem: `IModRegistryEventListener` was correctly internalized as engine UI/invalidation infrastructure, but `Fabricator` and `ModMenuUIController` were still public MonoBehaviours declaring it in their base lists. That risks C# inconsistent-accessibility compile failure and leaks the existence of the internal listener route through public component signatures.

Solution: Keep `IModRegistryEventListener`, `ModRegistryEventPayload`, and `ModRegistryEventType` internal. Remove the listener interface from public component base lists and register private `ModRegistryEventAdapter` instances that forward payloads to owner-private handlers. Add schema revision 39 and static validator gates for the adapter route.

Rejected Alternatives: Making `IModRegistryEventListener` public again was rejected because it reopens the exact SDK leak that schema 30 closed. Making `Fabricator` or `ModMenuUIController` internal was rejected because it is a wider Unity component visibility change with prefab/editor risk. Leaving explicit implementations on public classes was rejected because compile accessibility and public signature hygiene were both wrong.

Scalability potential: Low tier keeps the existing coalesced registry invalidation lane and avoids extra frame work. Middle tier can keep UI refresh and recipe cache invalidation stable through private adapters. High and Ultra tiers can add richer mod menu presentation without exposing the internal invalidation DTOs or creating a new mod-facing event route.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Added cost is one cold managed adapter allocation per component instance; no Tick, LateFrame, NativeQueue capacity, or SignalBus route changed.

## Decision 27 - External DLLs must not claim engine assembly identities

Problem: `AssemblyInfo.cs` grants internals to first-party friend assemblies such as `Hecton8.Plugins`, while the mod loader and SDK builder accepted arbitrary managed DLL names and metadata identities. In current envelope-only mode managed entries are disabled, but a future managed-mod reopening could let a Mods-root DLL claim an engine-owned simple assembly name and create an internal API spoof path.

Solution: Add cold managed assembly identity validation to `ModLoader` and `ModBuilderWindow`. The loader disables packages whose manifest `EntryAssembly`, resolved DLL filename, or `AssemblyName.GetAssemblyName()` metadata identity is reserved. The SDK builder rejects those DLLs before copying. The managed factory registration path also rejects reserved assembly factories loaded from the Mods root and fails closed when a reserved factory has an unreadable path. Schema revision 40 and the static validator now prove the guard in source, schema, audit docs, and change-control checklist.

Rejected Alternatives: Retrofitting strong-name signing was rejected for this pass because it changes assembly/package infrastructure and does not match the current Unity asmdef setup. Relying on envelope-only mode was rejected because it leaves a dormant security trap for managed-mode reactivation. Loading the DLL to inspect types was rejected because metadata identity can be read cold without executing mod code.

Scalability potential: Low tier keeps package discovery as a cold fail-fast scan and avoids runtime reflection/probing. Middle tier gets deterministic SDK packaging errors before users ship invalid packages. High and Ultra tiers can reopen managed-mod experiments only behind explicit quarantine and still keep engine friend identities first-party owned.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Added cost is cold editor/package-discovery metadata inspection only; no Tick, FixedTick, LateFrame, NativeQueue, SignalBus, DataVault, Burst job, or gameplay route changed.

## Decision 28 - Native byte payloads need source-backed layout proof

Problem: `HectonAPI.Events.SubscribeNative` exposes callback-scoped `ReadOnlySpan<byte>` for native Interaction and Crafting lanes, but the schema/docs only recorded the event kind names. They did not prove the source payload struct layout, size, or field offsets. That makes future managed-event reopening unsafe because mods would decode bytes against an undocumented ABI.

Solution: Keep the runtime bridge unchanged and add static contract proof. Schema revision 41 records `InteractionEventPayload` as an explicit 32-byte layout and `CraftingEventPayload` as an explicit 64-byte layout, including source files and offsets. `Validate_Mod_API_Static.ps1` now reads `InteractionEvents.cs` and `CraftingEvents.cs`, checks struct layout attributes, sizes, offsets, schema entries, audit docs, and last validation snapshot.

Rejected Alternatives: Removing `SubscribeNative` was rejected because that is a larger source-compatibility break and current envelope-only quarantine already blocks runtime managed callbacks. Exposing typed Unity/gameplay objects was rejected because it violates the no Unity/native handle rule. Relying on "callback-scoped span" documentation alone was rejected because lifetime proof is not byte layout proof.

Scalability potential: Low tier pays 0 us/frame because this is static validation only. Middle tier gets deterministic byte decode rules for test fixtures. High tier can reopen richer read-only native projections only after adding explicit payload contracts. Ultra tier can add visual-overkill event telemetry internally without changing gameplay truth ownership, DTO layout, or public authority route.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. No Tick, FixedTick, LateFrame, NativeQueue capacity, SignalBus route, DataVault handle, Burst job, or callback dispatch path changed.

## Decision 29 - Package DLL identity scan must cover stale and support DLLs

Problem: The loader validated `EntryAssembly` and the resolved primary DLL path, but it did not scan every top-level DLL beside `mod.json`. In envelope-only mode a package with a loose `Hecton8.*.dll` and no `EntryAssembly` would still be disabled as filesystem/content ingress, but the reserved-identity rule in the docs was false for that package shape. The SDK builder also deleted only the previous primary `EntryAssembly`, so support DLLs could remain in `Mods/[ModId]` after a later manifest-only rebuild.

Solution: Add a cold `ResolveManagedAssemblyIdentityScanPaths` route in `ModLoader` that scans every top-level package DLL for reserved file names and `AssemblyName` metadata identity. Any top-level DLL now marks the package as a managed-entry candidate even when envelope-only mode keeps `EntryAssemblyPath` empty. Add `ModBuilderWindow.RemoveStaleAssemblies` so each SDK build deletes top-level DLLs not selected for the current package. Schema revision 42 and the static validator now prove the loader scan, builder cleanup, docs, and runtime playbook.

Rejected Alternatives: Relying on envelope-only quarantine was rejected because the contract promised reserved identity validation, not just later disablement. Scanning arbitrary recursive package content was rejected because the existing runtime ingress convention and SDK builder output are top-level; nested content is not a managed entry route in current source. Keeping the previous primary-only cleanup was rejected because stale support DLLs are exactly the package-shape drift that hides in repeated SDK builds.

Scalability potential: Low tier gets deterministic cold package rejection with 0 us/frame. Middle tier can use SDK validation to catch stale packages before install. High tier can reopen managed-mod experiments only after this identity gate remains in place. Ultra tier can add richer package analysis without changing runtime truth ownership, envelope ABI, or hot SignalBus/DataVault routes.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Added cost is cold `Directory.GetFiles` plus `AssemblyName.GetAssemblyName` during package discovery or editor packaging only; no Tick, FixedTick, LateFrame, NativeQueue, SignalBus, DataVault, Burst job, command queue, or rendering path changed.

## Decision 30 - Package identity must be canonical before path or hash use

Problem: `ModBuilderWindow.TryValidateModId` allowed strings made only from separators such as `.`, `..`, or `---`, and it validated a trimmed value while `BuildModPackage` still used the raw `_modId` for `Path.Combine(modsRoot, _modId)` and the manifest `Id`. The loader accepted any non-whitespace manifest `Id` and did not validate dependency ids. `EntryAssembly` was also a raw manifest string passed into package-local `Path.Combine`, so a manual manifest could express a relative or absolute path before the package contract rejected it.

Solution: Add canonical mod identifier validation in both loader and SDK builder. Valid ids are lowercase letters/digits separated by single `.`, `_`, or `-`; they cannot have leading/trailing/repeated separators, whitespace, or reserved filesystem device segments. Loader validates manifest `Id` before hashing/path use, validates dependency ids before load-order resolution, and restricts `EntryAssembly` to a package-local `.dll` file name. Invalid `EntryAssembly` is cleared before path combine or metadata scan. Builder validates dependency ids and writes the canonical trimmed mod id to the output path and manifest. Schema revision 43 and the static validator now prove these routes.

Rejected Alternatives: Sanitizing invalid ids after hashing was rejected because one fact needs one owner and one canonical route. Allowing separator-only ids was rejected because they are path-like and not stable public identities. Letting `EntryAssembly` contain relative paths was rejected because the package contract is a file name, not arbitrary filesystem authority. Validating only in SDK builder was rejected because manual manifests and Workshop ingestion still hit the runtime loader.

Scalability potential: Low tier avoids filesystem ambiguity and package diagnosis churn with 0 us/frame. Middle tier gets deterministic SDK/package validation errors. High tier can add richer package registries without migrating dirty ids. Ultra tier can support larger mod catalogs and Workshop mirroring with stable canonical ids that do not change hash, save namespace, or command ownership routes.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Added cost is cold string validation during package discovery/editor build only; no Tick, FixedTick, LateFrame, NativeQueue, SignalBus, DataVault, Burst job, command queue, save commit, or rendering path changed.

## Decision 31 - Active mod scope must not synthesize anonymous ownership

Problem: Public facade guards now depend on `ModExecutionScope.HasActiveMod`, but `ModExecutionScope.Enter(null)` or blank owner input previously created an active `"anonymous"` scope. That made owner proof decorative: a caller could satisfy "active mod" without a canonical mod id or non-zero owner hash.

Solution: Make active scope creation fail closed. `ModExecutionScope` now rejects blank owner ids, resolves or requires a non-zero owner hash, reports `HasActiveMod` only when depth, owner id, and owner hash are all valid, and returns an empty current id outside scope instead of synthesizing `"anonymous"`. Schema revision 44 and the static validator prove the source guard, schema flag, loader audit, spec, runtime playbook, and change-control checklist.

Rejected Alternatives: Keeping `"anonymous"` as an active fallback was rejected because it violates one owner/one route and breaks unload/revoke attribution. Moving the guard only into public `HectonAPI` methods was rejected because the shared scope primitive would remain unsafe for direct same-assembly callers and future managed bridge reopenings. Reusing the internal event-bus no-scope `"anonymous"` label was rejected for active scopes because internal first-party fallback labels are not mod owner identities.

Scalability potential: Low tier keeps envelope-only runtime at 0 us/frame and avoids anonymous managed probes. Middle tier can run managed harnesses with deterministic owner failures. High tier can reopen selected managed projections with hard owner proof. Ultra tier can add visual-overkill diagnostics or mod workbench replay without weakening mod id/hash authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Added cost is only a few managed branches when opening a mod execution scope; no Tick, FixedTick, LateFrame, NativeQueue, SignalBus hot path, DataVault route, Burst job, command queue, save commit, or rendering path changed.

## Decision 32 - Subtitle cue alias must not bypass reserved kernel status

Problem: `SubtitleCue` was documented as a reserved future localization kernel, but `TriggerSubtitleCue` remained in the runtime default `FutureCommandEnvelope` allowlist, `allowed_opcodes.csv`, and the editor runtime opcode tuner. That created a bypass: the public alias could route to `ModSubtitleCueSignal` while the reserved kernel still lacked localization owner proof, token proof, quota telemetry, unload behavior, and runtime verification.

Solution: Keep the `TriggerSubtitleCue` hash constant as a stable reserved source fact, but remove it from `GenerateEmergencyMockOpcodes`, `IsRuntimeAllowedFutureCommandOpcode`, `allowed_opcodes.csv`, and `ModApiSandboxTunerWindow`. Schema revision 45 records `futureSubtitleCueAliasesReserved=true`, `runtimeForbiddenFutureCommandOpcodes`, and future allowed opcode count 8. The static validator now fails if the alias re-enters the CSV, default runtime map, or editor tuner.

Rejected Alternatives: Deleting the alias constant was rejected because stable hash names are useful reservation metadata and internal routing probes still need to identify the alias. Leaving the alias runtime-allowed was rejected because it grants localization/subtitle authority without a route owner or proof artifact. Moving it only out of docs was rejected because the defect was executable allowlist state.

Scalability potential: Low tier avoids spending any runtime work on unowned subtitle cue envelopes. Middle tier can model subtitle requests in SDK/workbench validation without granting runtime authority. High tier can add richer localized subtitle presentation after zero-GC char path and quota proof. Ultra tier can support visual-overkill subtitle presentation and diagnostics later without changing the 64-byte envelope ABI or public authority route.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Worst-case illegal subtitle spam now fails at opcode allowlist validation before signal routing; added cost is static/editor validation only.

## Decision 33 - Legacy managed game event payloads must stay internal-only

Problem: `HectonGameEvents.cs` payload classes were internal, but many constructors and properties were still `public`. Several of those members carried Unity/authored/runtime handles such as `ItemData`, `BuildableData`, `HectonSurvivalSystem`, and `SurvivalDeathRecord`. Today that is not external API because the containing classes are internal, but it is stale contract debt: one visibility change could accidentally expose first-party object handles as SDK event DTOs. The static validator also had a blind spot: subscription token constructor coverage concatenated `$modEventProjectionBridgeSource`, an undefined variable, instead of the already-read projection bridge source.

Solution: Make every `HectonGameEvents` constructor and member internal while preserving first-party same-assembly access. Add schema revision 46 with `gameEventPayloadMembersInternalOnly=true`. Extend the static validator to read `HectonGameEvents.cs`, fail on any line-level `public` member in that file, require matching schema/docs/playbook evidence, and fix the subscription-token constructor scan to use `$projectionSource`.

Rejected Alternatives: Leaving public members because the classes are internal was rejected because it leaves a fragile dormant SDK leak. Deleting the legacy payload classes was rejected because that is a wider first-party event cleanup outside this pass. Creating public replacement DTOs was rejected because no runtime managed-event reopening, owner proof, or zero-GC payload contract exists for those domains.

Scalability potential: Low tier keeps envelope-only runtime with no extra frame work and no Unity object retention through mod callbacks. Middle tier can keep managed harnesses honest by proving payloads are first-party only. High and Ultra tiers can reopen richer managed projections later only with explicit fixed DTOs, quality-scaled budgets, and runtime proof, not accidental legacy object handles.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is static validation only. Runtime code path, SignalBus routing, DataVault ownership, Burst jobs, and command queue budgets are unchanged.

## Decision 34 - Runtime loader diagnostics members are engine-internal only

Problem: `ModRuntimeInfo` and `ModLoadStatus` were internal after the earlier diagnostics quarantine, but every `ModRuntimeInfo` field remained `public`. The descriptor includes `DirectoryPath`, `AssetBundlePath`, load status, and status text. Current external exposure is blocked by the internal type, but public fields inside a path-bearing descriptor are stale contract debt and a future publicening trap.

Solution: Make all `ModRuntimeInfo` fields internal. Keep `ModMetadata` public because it is the declared package metadata contract, but keep runtime/package-path diagnostics as engine UI only. Update schema revision 47 with `modRuntimeInfoMembersInternalOnly=true`, add a validator path that counts declared fields while rejecting line-level `public` members in `ModRuntimeInfo.cs`, and update loader/save/spec/runtime docs.

Rejected Alternatives: Leaving public fields because the struct is internal was rejected because this repeats the exact dormant-SDK-leak pattern. Redacting the fields was rejected because engine UI diagnostics still need full paths and failure reasons. Creating a public redacted DTO was rejected because no mod-facing all-package diagnostics route is authorized.

Scalability potential: Low tier keeps loader diagnostics cold and avoids leaking path strings to runtime mods. Middle tier keeps engine UI diagnostics intact. High and Ultra tiers can later expose curated marketplace/community diagnostics through a separate hash-only DTO without changing internal loader state or save identity.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is static validation only. Runtime loader behavior, allocation profile, save payload layout, and event/command routing are unchanged.

## Decision 35 - HectonEventBus must not carry public static bus methods

Problem: `HectonEventBus` was internal first-party infrastructure, but its unmanaged subscribe, native subscribe, projected subscribe, and unmanaged publish methods remained `public static`. Current external exposure was blocked by the internal containing class, but the member visibility preserved a dormant second event route if the class is ever publicened or moved into a wider assembly surface.

Solution: Make the direct bus static methods internal and keep the public surface exclusively on `HectonAPI.Events`. Add schema revision 48 with `hectonEventBusPublicStaticMembersForbidden=true`. Extend the static validator so any line-level `public static` member in `HectonEventBus.cs` fails the gate, and update event/API/runtime docs.

Rejected Alternatives: Leaving the methods public because the class is internal was rejected because this repeats the dormant SDK leak pattern already found in legacy event payloads and loader diagnostics. Removing `HectonAPI.Events` methods was rejected because those are the documented public facade. Adding comments was rejected because visibility is the contract.

Scalability potential: Low tier keeps the same envelope-only runtime and avoids an accidental managed event bypass. Middle tier can run managed harnesses through one facade. High and Ultra tiers can reopen richer projected/native event tooling later while preserving one owner route and a single schema gate.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is static validation only. Event dispatch logic, callback watchdog, NativeQueue bridge, SignalBus projection, subscription lists, and payload layouts are unchanged.

## Decision 36 - FutureCommand sandbox control-plane methods are not SDK methods

Problem: `FutureCommandSandboxValidator` was internal, but its lifecycle, request, raw stream, external queue, tuning, CSV reload, telemetry, CRC, self-audit, kernel telemetry, and blackbox methods remained `public static`. `MockModQueue.Wrap` also remained public static. Current external exposure was blocked by internal containing types, but those member visibilities contradicted the documented rule that the validator is engine/control-plane infrastructure and preserved a dormant direct sandbox route.

Solution: Make the validator and `MockModQueue` static control-plane methods internal while preserving the actual public packet contract: `FutureCommandEnvelope`, `FutureCommandEnvelope.SizeBytes`, and `FutureCommandOpcodes`. Add schema revision 49 with `futureCommandSandboxPublicStaticMembersForbidden=true`. Extend the static validator to inspect only the validator class body plus `MockModQueue.Wrap`, so public opcode constants stay intentional while control-plane methods cannot drift public again.

Rejected Alternatives: Leaving public static methods because the class is internal was rejected because it repeats the same dormant SDK leak pattern found in event bus and loader diagnostics. Making validator methods public facade APIs was rejected because they include raw stream ingress, external queue drain, tuning, CSV reload, telemetry copy, and blackbox dump routes that are not mod-owned. Removing public opcode constants was rejected because those are stable hash names for the packet contract and SDK/workbench authoring.

Scalability potential: Low tier keeps the cheapest runtime model: one owned 64-byte envelope ingress through `HectonAPI.Commands.RequestFuture`. Middle tier can keep editor/tuning windows same-assembly without SDK leakage. High and Ultra tiers can expand internal batch ingress, telemetry, and visual-overkill workbench tooling without changing public runtime authority or ABI.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is static validation only. Command queue behavior, validation jobs, opcode map, tuning buffers, telemetry rings, CSV ingest, and blackbox dump logic are unchanged.

## Decision 37 - Direct dispatcher and hook methods are not SDK routes

Problem: `HectonModHooks` and `ModCommandDispatcher` were internal, but their direct publication, legacy request, and float-packing helper methods remained `public static`. Current external exposure was blocked by internal containing types, but the member visibility preserved the same dormant SDK leak pattern already found in the event bus, loader diagnostics, and sandbox validator.

Solution: Make `HectonModHooks.PublishPlayerSpawned`, `PublishBiomeChanged`, `ModCommandDispatcher.Request`, `RequestAup`, `RequestRenderInstance`, `PackSequentialFloat2`, and `PackSequentialFloat3` internal. Keep public UGC routes on `HectonAPI.Events` and `HectonAPI.Commands`, then add schema revision 50 with validator gates for `hectonModHooksPublicStaticMembersForbidden` and `modCommandDispatcherPublicStaticMembersForbidden`.

Rejected Alternatives: Leaving public static members because their containing types are internal was rejected because future assembly or visibility drift would reopen direct command/event routes. Removing `HectonAPI.Commands` or `HectonAPI.Events` facades was rejected because those are the documented public facade contracts. Making the dispatcher helpers public SDK utilities was rejected because they sit in first-party queue/kernel infrastructure, not mod-owned ABI.

Scalability potential: Low tier keeps the cheapest envelope-only runtime with no extra frame work. Middle tier keeps managed harnesses on one facade route. High tier can expand internal command/event diagnostics without SDK leakage. Ultra tier can add visual-overkill workbench tooling while preserving the same public authority and packet ABI.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is static validation only. Command dispatch behavior, event publication behavior, queue capacities, packet layout, SignalBus routing, and Burst/job paths are unchanged.

## Decision 38 - Projected event bridge must not synthesize anonymous subscribers

Problem: Public `HectonAPI.Events` facade methods already required active `ModExecutionScope`, but internal unmanaged/native/projected bridge routes still resolved blank subscriber ids from `ModExecutionScope.CurrentModId` and `ModEventProjectionBridge.Subscribe` could fall back to `"anonymous"`. That contradicted the schema rule that anonymous active owners are rejected and preserved a dormant anonymous callback token route if an internal caller bypassed the facade.

Solution: Add `RequireModSubscriberScope` to `HectonEventBus` unmanaged, native, and projected subscription routes. Add explicit active-scope and subscriber-id match checks to `ModEventProjectionBridge.SubscribeProjected`, and make the private projected subscribe path throw if no concrete subscriber id exists. Add schema revision 51 with `projectedEventBridgeRejectsAnonymousSubscribers=true` and validator checks that reject the old anonymous fallback.

Rejected Alternatives: Relying only on `HectonAPI.Events.RequireSubscriberScope` was rejected because the internal bridge remains callable inside the same assembly. Keeping anonymous internal subscribers was rejected because the projected bridge is mod-facing and creates mod-owned `HectonEventSubscription` tokens. Removing projected events entirely was rejected because the current source-audit contract keeps them as quarantined future read-only projections.

Scalability potential: Low tier keeps envelope-only runtime at 0 us/frame. Middle tier gets deterministic managed harness behavior if projected events are reopened. High tier can add richer projected DTOs without anonymous owner leaks. Ultra tier can add visual-overkill diagnostics on the same bridge while preserving owner attribution and cull telemetry.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is a few branches/string comparison on cold subscription calls only. LateFrame dispatch, SignalBus snapshot reads, callback watchdog, NativeQueue bridge, packet layout, and Burst jobs are unchanged.

## Decision 39 - Mock queue handles are not SDK objects

Problem: `MockModQueue` was already an internal sandbox control-plane struct and its static `Wrap` method was internal, but the struct still exposed `public NativeQueue<FutureCommandEnvelope> Queue`, `public GetIsCreated`, `public Attach`, and `public Dispose`. Current external exposure was blocked by the internal type, but the member surface contradicted the SDK boundary: external mods must submit through `HectonAPI.Commands.RequestFuture`, not receive or manipulate a `NativeQueue`-backed ingress helper.

Solution: Move the queue handle to a private `_queue` field, make `GetIsCreated` and `Attach` internal, and implement disposal through explicit `IDisposable.Dispose`. Add schema revision 52 with `mockModQueueMembersInternalOnly=true`, a static validator body scan for `MockModQueue`, and docs/runtime playbook evidence. The public packet contract remains `FutureCommandEnvelope` and `FutureCommandEnvelope.SizeBytes`; no queue handle is public SDK state.

Rejected Alternatives: Leaving the public field because the containing struct is internal was rejected because this repeats the dormant-SDK-leak pattern found in the validator, event bus, loader diagnostics, and event hooks. Removing `MockModQueue` entirely was rejected because it is first-party/package-loader ingress plumbing and outside this narrow closure. Exposing a public read-only queue property was rejected because a `NativeQueue` handle is not a mod API DTO and would violate the single facade route.

Scalability potential: Low tier keeps one cheap envelope ingress route with no extra frame work. Middle tier can keep package-loader/editor queue plumbing same-assembly. High tier can add richer batch validation without exposing native handles. Ultra tier can add visual-overkill workbench diagnostics while preserving the same packet ABI, ownership route, and queue authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is static validation only. Runtime command queue behavior, opcode validation, dispatcher drain, SignalBus output lanes, telemetry rings, NativeQueue allocation, and Burst/job paths are unchanged.

## Decision 40 - Resource hashes must be owned by the active mod scope

Problem: `HectonAPI.Resources` passed `ModExecutionScope.CurrentModId` into `ModResourceRegistry.TryRegister`, but the internal registry helper accepted any non-empty `modId` as long as some mod scope was active. Same-assembly callers could therefore register a prefab/audio/texture hash under a forged owner id while another mod scope was active. The public facade was correct; the shared registry primitive was not strict enough.

Solution: Add an ordinal owner check in `ModResourceRegistry.TryRegister` before hash computation. The supplied `modId` must equal `ModExecutionScope.CurrentModId`; otherwise the call throws `IllegalContractException`. Add schema revision 53 with `resourceRegistryRejectsForgedOwner=true`, static validator coverage, resource audit docs, and runtime playbook evidence.

Rejected Alternatives: Relying on `ModResourceProxy` to always pass `CurrentModId` was rejected because the registry helper remains callable inside the same assembly. Hashing caller-supplied owner strings was rejected because it violates one owner/one route and can create resource ids attributed to the wrong mod. Removing the resource registry was rejected because the current source-audit contract still keeps hash-only resource resolution as a quarantined legacy surface.

Scalability potential: Low tier keeps envelope-only runtime with no extra frame work. Middle tier gets deterministic SDK/harness failures for forged resource ownership. High tier can reopen richer hash-backed resource workflows without owner ambiguity. Ultra tier can add visual-overkill workbench asset diagnostics while preserving resource id attribution and unload/revoke policy.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is one ordinal string comparison on cold resource registration only. AssetBundle loading, raw PNG fallback, command packets, SignalBus lanes, NativeQueue drains, and Burst/job paths are unchanged.

## Decision 41 - Projected event cap proof must match the continuous source curve

Problem: `ModEventProjectionBridge.ResolveProjectionCap` used a smoothstep curve over finite-saturated `GlobalQualityWeight01`, but `Runtime_Verification_Playbook.md` still documented a linear `round(lerp(10, 50, GlobalQualityWeight))` cap and the static validator did not prove the cap curve. QA or SDK harnesses following the playbook would model the wrong callback pressure and could miss binary-quality regressions.

Solution: Route the source cap through the existing `Smooth01` helper, record schema revision 54 with `projectedEventCapUsesSmoothContinuousCurve=true`, add `projectionCapCurve` and `projectionCapFormula`, and extend the validator to prove source low/high caps, finite saturation, smoothstep use, schema snapshot, event audit text, and runtime playbook text.

Rejected Alternatives: Leaving the inline polynomial was rejected because it duplicates the curve expression and makes static proof brittle. Updating only the playbook was rejected because schema/validator drift would recur. Changing cap numbers was rejected because this pass is a contract proof repair, not a runtime budget rebalance.

Scalability potential: Low tier keeps the 10-event projected callback cap and reduced cosmetic cadence. Middle tier ramps smoothly without a binary switch. High tier approaches the 50-event cap for richer callback presentation. Ultra tier can add visual-overkill projected diagnostics later without changing DTO layout, save identity, or gameplay authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Source arithmetic is behavior-equivalent and allocation-free; added cost is static validation only. SignalBus snapshots, NativeQueue capacity, callback watchdog, event DTO layout, command queue, DataVault, save, and Burst/job ownership are unchanged.

## Decision 42 - Spec closure revision parity must be machine-checked

Problem: `Mod_API_Specification.md` claimed the current static closure was guarded against drift, but the paragraph still referenced `Signal_Schema.json` schema revision 53 after the schema had advanced to 54. The existing validator only checked the README schema line, so the spec's authoritative closure paragraph could silently lie.

Solution: Advance the schema to revision 55, add `modApiSpecCurrentClosureRevisionMatchesSchema=true` to the last static validation snapshot, update the spec closure to revision 55, and add a validator assertion that constructs the expected spec closure prefix from `Signal_Schema.json.schemaRevision`. The playbook now also lists `ProjectedEventCapUsesSmoothContinuousCurve = True` because the validator result already emits it.

Rejected Alternatives: A one-line spec revision edit was rejected because it would repeat the same drift on the next schema bump. Moving the entire closure paragraph to generated output was rejected for this pass because it would be a documentation system refactor outside the narrow mod/API closure. Running dotnet was rejected because no C# runtime/editor source changed.

Scalability potential: Low tier gets no runtime cost and a stricter SDK proof chain. Middle tier avoids QA harnesses following stale modding contracts. High tier can extend mod-facing projected diagnostics with the same schema gate. Ultra tier can add visual-overkill SDK/workbench reporting without turning docs into unmanaged runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is static validation only. Runtime command queues, SignalBus snapshots, NativeQueue bridges, resource hashes, save identity, DTO layout, and Burst/job paths are unchanged.

## Decision 43 - Private event channels must reject anonymous subscriber ids

Problem: `HectonEventBus` facade and projected bridge guards rejected anonymous subscribers, but the private channel implementations still contained `"anonymous"` fallback logic before `HectonEventSubscription` token creation. A future same-class route or managed-mode reopen could create ownerless callback tokens, breaking unload attribution and contradicting the schema.

Solution: Add a single `RequireConcreteSubscriberId` guard and call it from the public managed subscription resolver and all private channel `Subscribe` methods before token creation. Advance `Signal_Schema.json` to revision 56 with `eventChannelsRejectAnonymousSubscribers=true`, update docs/spec/playbook wording, and extend the static validator to reject the old fallback pattern.

Rejected Alternatives: Relying on `HectonAPI.Events.RequireSubscriberScope` was rejected because private channel code is still the token factory. Keeping first-party anonymous subscribers was rejected because mod-owned callback tokens need a concrete owner. A documentation-only fix was rejected because the source still created ownerless tokens.

Scalability potential: Low tier remains envelope-only with 0 us/frame cost. Middle tier gets deterministic managed harness failures instead of anonymous tokens. High tier can reopen richer read-only event projections without owner ambiguity. Ultra tier can add heavy diagnostics and callback visualization on the same owner route without changing DTO layout, save identity, or SignalBus authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is one cold-path branch/string check on subscription attempts. LateFrame dispatch, NativeQueue bridge, SignalBus snapshots, packet layout, DataVault ownership, Burst jobs, and frame cadence are unchanged.

## Decision 44 - SaveState store owner proof must exist below the public facade

Problem: `HectonAPI.SaveState.SetModString` and `GetModString` required active `ModExecutionScope`, but the backing `ModSaveStateStore` still accepted scope-less calls and derived the persistence owner from the arbitrary payload key. `ModWorldPersistenceManager` used that hidden fallback for the internal mod-world payload. This violated one fact -> one owner -> one route because the store primitive could invent owners outside a mod scope.

Solution: Require active mod scope in `ModSaveStateStore.SetModString` and `GetModString`. Add explicit engine-owned `SetEngineString` / `GetEngineString` methods restricted to `hecton.internal.` keys and a reserved `hecton.internal.engine_save_owner` id. Move `ModWorldPersistenceManager` to that engine route. Preserve legacy read compatibility by allowing `GetEngineString` to read the old key-hash owner only after the explicit engine owner lookup misses.

Rejected Alternatives: Keeping the key-hash fallback was rejected because it lets any same-assembly caller mint save owners from payload keys. Entering a fake mod scope for engine payloads was rejected because engine save infrastructure is not a mod and should not pollute mod identity. A documentation-only warning was rejected because the store still accepted the bad route.

Scalability potential: Low tier uses the same cold save data with no frame work and deterministic owner proof. Middle tier can keep internal mod-world persistence without synthetic mod ids. High tier can add richer engine save payloads under the same `hecton.internal.` route. Ultra tier can add overkill diagnostics or migration tooling without changing mod save identity or public DTO shape.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is cold-path branch/string prefix validation during save/load only. No SignalBus lane, NativeQueue, Burst job, DataVault hot path, packet layout, or frame cadence changed.

## Decision 45 - Mod manifests need a pre-read byte cap

Problem: `ModLoader.TryReadManifest` called `File.ReadAllText(manifestPath)` before any file-size validation. `mod.json` is cold package ingress, but it is still untrusted filesystem input. A malicious oversized manifest could allocate a large managed string before canonical id, dependency, EntryAssembly, API version, or reserved assembly validation ran.

Solution: Add `MaxManifestBytes = 32768` and `TryValidateManifestFileSize`. The loader now rejects missing, empty, or oversized `mod.json` before JSON read/parse. Schema revision 58 records `manifestMaxBytes` and `manifestByteCapEnforcedBeforeRead`; the static validator proves source order, schema snapshot, loader/save audit, spec, and runtime playbook evidence.

Rejected Alternatives: Validating after JSON parse was rejected because it preserves the allocation spike. Relying on `ModBuilderWindow` was rejected because runtime package discovery reads arbitrary directories, not only packages made by the current SDK UI. Raising the cap to a large arbitrary value was rejected because the active manifest has 9 small fields and richer authored data belongs in separate bounded binary/manifest artifacts.

Scalability potential: Low tier avoids cold-load memory spikes from hostile manifests. Middle tier gets deterministic package rejection before any asset or DLL path scan. High tier and Ultra tier can still use richer SDK/workbench metadata by placing it in separate bounded artifacts with explicit validators, not by growing the runtime `mod.json` read.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is one cold `FileInfo` allocation and two length branches per discovered manifest. Removed risk is a large managed string allocation during mod discovery.

## Decision 46 - Static validator must distinguish SDK forbidden facades from root cold infrastructure

Problem: Concurrent source drift changed `MockModQueue` to `internal ref struct`, renamed `MockAcousticSignal` to `SandboxMockAcousticSignal`, and added root `HectonAPI` cold registry cache hooks. The source still preserved the SDK boundary, but `Validate_Mod_API_Static.ps1` treated the old `partial struct : IDisposable` shape and every root `internal static` method as part of the internal-forbidden public facade count.

Solution: Keep the source changes intact and tighten the validator around the actual contract. `MockModQueue` passes if the queue handle and control methods remain non-public, whether disposal is explicit `IDisposable.Dispose` or internal pattern `Dispose`. FutureCommand output signal checks now track `SandboxMockAcousticSignal`. Root cold registry cache hooks are excluded from the internal-forbidden facade method count because they are first-party infrastructure on the containing `HectonAPI` class, not public nested facade methods.

Rejected Alternatives: Reverting the concurrent source change was rejected because it would interfere with another agent and was not required for the SDK boundary. Inflating `API_Surface_Audit_Matrix.md` with `BindRegistryServicesCold`, `ResetRegistryCacheCold`, and `OnGlobalRegistryServiceReplaced` was rejected because those are not mod-facing forbidden facade methods. Dropping `MockModQueue` proof was rejected because the native queue handle must remain non-public.

Scalability potential: Low tier keeps `HectonAPI.Input` away from hot `GlobalRegistry` polling while preserving the mod facade inventory. Middle tier can hot-swap the input service through cold cache hooks. High and Ultra tiers can add richer registry hot-swap infrastructure without turning root first-party methods into SDK promises.

Hardware Impact: Estimated runtime gain on i3/MX350 from this validator-only patch is 0 us/frame measured/claimed. It preserves proof accuracy for another source change that reduces facade service lookup risk; no new runtime work is introduced by the validator update.

## Decision 47 - Manifest discovery must be bounded before candidate allocation

Problem: `ModLoader.DiscoverAndLoadMods` used recursive `Directory.GetFiles(modsRoot, ManifestFileName, SearchOption.AllDirectories)`. The loader now capped manifest bytes before JSON read, but a hostile or broken Mods tree could still allocate an unbounded `string[]` of manifest paths and then size the candidate list from that array before per-manifest validation.

Solution: Add `MaxDiscoveredManifestCount = 64`, collect manifests with lazy `Directory.EnumerateFiles`, stop when the cap is reached, and allocate `List<ModCandidate>` from the bounded path count. Schema revision 59 and `Validate_Mod_API_Static.ps1` now prove the lazy enumeration route, the cap value, the removal of recursive `Directory.GetFiles` for discovery, and the ordering before candidate allocation.

Rejected Alternatives: A post-allocation cap was rejected because it still allocates the full path array. SDK-only package count limits were rejected because runtime discovery reads arbitrary directories, not only SDK-built packages. Unlimited discovery was rejected even though it is cold-path because untrusted filesystem ingress must be bounded before managed allocations scale with attacker-controlled file count.

Scalability potential: Low tier avoids cold-load memory spikes and long package scans from polluted Mods folders. Middle tier keeps predictable diagnostics with a fixed package ceiling. High tier can layer curated workbench/package catalogs over the same loader contract. Ultra tier can add richer SDK discovery UI and package previews without making runtime recursive crawl unbounded.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Cold-path impact is bounded allocation: worst-case manifest path collection is capped at 64 strings plus one bounded list, instead of a full recursive path array sized by filesystem contents.

## Decision 48 - Top-level package file discovery must be bounded and fail closed

Problem: After recursive manifest discovery was capped, `ModLoader` still used `Directory.GetFiles` for top-level managed DLL identity scan, legacy AssetBundle fallback, and legacy localization fallback. Those arrays were cold-path but still untrusted filesystem ingress. Worse, managed DLL identity discovery could fail or exceed a reasonable package envelope without disabling the package, leaving an ambiguous partial-trust state.

Solution: Add `CollectTopLevelFiles` with lazy top-level `Directory.EnumerateFiles`, deterministic sort after bounded collection, and explicit caps: `32` managed assemblies, `4` bundles, `16` localization files. Managed assembly cap overflow or discovery failure now sets the manifest contract error and disables the package before load. Schema revision 60 and `Validate_Mod_API_Static.ps1` prove the source caps, removal of old `Directory.GetFiles` calls, docs, runtime playbook, and last validation snapshot.

Rejected Alternatives: Capping after `Directory.GetFiles` was rejected because it still allocates the attacker-sized path array. Silently skipping managed assembly identity scan on discovery failure was rejected because reserved/friend-assembly spoofing must fail closed. Removing bundle/localization fallback entirely in this pass was rejected because envelope-only quarantine already disables runtime ingestion, and the narrow defect was unbounded cold discovery in existing source.

Scalability potential: Low tier avoids cold-load allocation spikes from polluted package folders. Middle tier gets predictable package rejection and bounded diagnostics. High tier can use curated SDK catalogs and richer package previews over the same caps. Ultra tier can add visual-overkill workbench analysis without making runtime package discovery unbounded or partially trusted.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added work is cold bounded list allocation and sort for at most 32 DLL paths, 4 bundle paths, or 16 localization paths. Removed risk is unbounded top-level path-array allocation and ambiguous package trust after failed DLL identity discovery.

## Decision 49 - Raw texture reads must fail closed after the byte gate

Problem: `ModAssetManager.LoadRawTexture` validated raw PNG file size before `File.ReadAllBytes`, but the read block caught only `IOException`. A file can become inaccessible, invalid, or otherwise fail after the size check. In the dormant legacy content path this would still be a filesystem exception escape instead of a mod-content rejection.

Solution: Catch `System.UnauthorizedAccessException`, `IOException`, and `System.Exception` around `File.ReadAllBytes`, log the rejected raw texture path, and return null. Schema revision 61 records `rawTextureByteCapEnforcedBeforeRead=true` and `rawTextureReadFailsClosed=true`; the static validator proves source ordering, catch coverage, schema snapshot, resource/content audit, spec, and runtime playbook evidence.

Rejected Alternatives: Relying on the earlier `FileInfo` check was rejected because filesystem state can change between inspect and read. Catching only `IOException` was rejected because unauthorized access and other invalid read exceptions are common filesystem failure classes. Removing raw PNG fallback entirely was rejected because envelope-only quarantine already disables it at runtime, and the narrow defect was fail-closed behavior if legacy mode is reopened.

Scalability potential: Low tier avoids crash-prone legacy raw file ingress and keeps envelope-only runtime at 0 us/frame. Middle tier gets deterministic null-return behavior for broken packages. High tier can reopen curated legacy content only with bounded file reads. Ultra tier can add richer workbench diagnostics for rejected texture files without changing runtime asset authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Added cost is only exception-path catch handling around a cold file read. Removed risk is an unhandled raw texture filesystem exception after the byte cap.

## Decision 50 - Legacy AssetBundle lookup must be exact-name-only

Problem: `ModAssetManager.LoadAsset<TAsset>` used exact `AssetBundle.LoadAsset<TAsset>(assetName)` first, but on miss it called `AssetBundle.GetAllAssetNames()` and suffix-matched paths. That creates a dormant allocation of the full bundle asset-name array and lets ambiguous suffixes pick a different asset than the SDK/workbench intended.

Solution: Remove the suffix fallback and `EndsWithAssetPath`. Legacy AssetBundle lookup now uses the exact asset name only. Schema revision 62 records `assetBundleSuffixFallbackDisabled=true` and `assetBundleGetAllAssetNamesForbidden=true`; the validator proves source, schema, resource audit, spec, and playbook.

Rejected Alternatives: Capping the loop after `GetAllAssetNames()` was rejected because the allocation already happened before the cap. Keeping suffix lookup for convenience was rejected because SDK tooling must write exact asset names or hash manifests instead of making runtime guess.

Scalability potential: Low tier avoids cold allocation spikes from bad bundle queries. Middle tier gets deterministic authoring errors instead of suffix surprises. High tier can add richer workbench asset previews and exact-name manifests. Ultra tier can add visual-overkill package diagnostics without changing runtime lookup authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Removed risk is cold `string[]` allocation on legacy AssetBundle lookup miss and ambiguous asset resolution.

## Decision 51 - Modders need one current SDK entry point, not scattered docs

Problem: The implemented Unity Editor surface had only `Hecton/Modding/Mod Builder`. The docs described a future Workbench/CLI/graph product, but a developer opening the project had no single current place for builder, contracts, sample, runtime playbook, local Mods folder, and static validation. The builder also used `AssetDatabase.FindAssets` for bundle collection, allocating a full GUID array from the selected folder.

Solution: Add `ModdingSdkHubWindow` at `Hecton/Modding/SDK Hub`. It opens the Mod Builder, core docs, sample, runtime playbook, local `Mods/` folder, and runs `Validate_Mod_API_Static.ps1`. Bound `ModBuilderWindow` bundle asset collection with `MaxBundleBuildAssetCount=512`, `Directory.EnumerateFiles`, deterministic sort, and no `AssetDatabase.FindAssets` GUID array. Schema revision 63 records the hub and builder cap; the validator proves source, schema, README, spec, authoring plan, and playbook.

Rejected Alternatives: Adding only more documentation was rejected because the user asked how mod developers will actually work. Leaving `FindAssets` was rejected because a broad selected folder can allocate and process an uncontrolled GUID array before the builder knows package scale. Adding runtime managed DLL execution was rejected because current authority is envelope-only and managed entries are legacy/internal.

Scalability potential: Low tier runtime is unchanged: 0 us/frame and envelope-only. Middle tier creators get deterministic local validation and a bounded package asset list. High tier can build richer Workbench screens on the same hub. Ultra tier can add overkill package inspection, previews, and graph simulation while preserving the runtime envelope boundary.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Editor/package-time risk reduction: selected asset folders are capped at 512 bundle-eligible files instead of using an unbounded GUID array. No gameplay tick, SignalBus, NativeQueue, DataVault, save, or Burst path changed.

## Decision 52 - SDK builder validation must stay bounded and loader-parity exact

Problem: `ModBuilderWindow` could select more managed DLLs than the loader's 32-DLL package cap, allowed duplicate selected DLL file names to collide in output, performed deep asset/DLL validation from UI repaint paths, and cleaned stale output DLLs through an unbounded top-level file array.

Solution: Add `MaxManagedAssemblyInputCount=32`, reject duplicate selected DLL output names, split shallow `OnGUI` validation from build-time deep file validation, make configured empty asset folders fail at build time, and bound stale DLL cleanup with `MaxStaleAssemblyCleanupScanCount=128`. Schema revision 64 records the proof.

Rejected Alternatives: Letting the loader reject bad SDK packages was rejected because the SDK must not generate ambiguous packages. Keeping deep scans in repaint was rejected because selected folders and DLL metadata are filesystem-bound authoring inputs, not cheap UI state. Capping stale cleanup after `Directory.GetFiles` was rejected because it still allocates the full path array first.

Scalability potential: Low tier runtime remains 0 us/frame and envelope-only. Middle tier creators get predictable SDK package failures before load. High tier can add richer workbench diagnostics on the same caps. Ultra tier can add heavy package previews and graph simulation without widening runtime package authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Editor/package-time risk reduction: managed assembly inputs cap at 32, stale DLL cleanup scans at most 128 top-level DLLs, and UI repaint no longer performs deep asset/DLL identity scans.

## Decision 53 - Reserved subtitle aliases must not be editor-injectable

Problem: `allowed_opcodes.csv` and `GenerateEmergencyMockOpcodes()` already rejected `TriggerSubtitleCue` and `SubtitleCue`, but `ModKernelInspectorWindow` still exposed `FutureCommandOpcodes.SubtitleCue` through an Inject Subtitle button. That editor route contradicted the reserved opcode contract and could make an unowned localization/subtitle kernel look supported.

Solution: Remove the Inject Subtitle button, make the inspector injector return for unknown opcode hashes instead of constructing the old subtitle payload, and extend the static validator to read both `ModApiSandboxTunerWindow` and `ModKernelInspectorWindow`. Schema revision 65 records `editorRuntimeOpcodeTunersRejectReservedSubtitleAliases=true`.

Rejected Alternatives: Leaving the button as "diagnostic only" was rejected because executable editor tooling is an authority signal for mod developers. Only checking `ModApiSandboxTunerWindow` was rejected because the actual leak was in the kernel inspector. Removing subtitle telemetry counters was rejected because reading internal telemetry is not the same as exposing an injector.

Scalability potential: Low tier runtime stays at 0 us/frame with no subtitle route. Middle tier avoids false SDK promises. High tier can reopen subtitles later only through localization-owner proof, quotas, unload behavior, and playbook evidence. Ultra tier can add visual-overkill subtitle authoring previews as offline tooling without granting runtime opcode authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. Removed risk is authority drift in editor tooling; no gameplay tick, packet layout, SignalBus, NativeQueue, save, DataVault, or Burst/job path changed.

## Decision 54 - Public modders need a concrete external starter kit

Problem: `Hecton/Modding/SDK Hub` made the internal Unity project easier to navigate, but a random external mod author still had no concrete folder contract. Existing docs described a future Workbench/CLI while the implemented builder still emitted legacy `mod.json`/DLL/bundle shaped packages that current envelope-only runtime disables. That made the practical question "Unity or what program, and what files do I need?" under-answered.

Solution: Add a non-destructive `Create External Starter Kit` action to `ModdingSdkHubWindow`. It creates `ModdingSDK/ExternalStarterKit/` with `README.md`, `mod.h8manifest.json`, `mod.json`, `Content/assets.h8manifest.json`, `Graphs/main.h8graph.json`, `Tables/settings.h8table.json`, `Locales/en.h8loc.json`, `Generated/`, `Reports/`, and `Reference/` copies of `allowed_opcodes.csv` plus `kernel_tuning_profiles.csv`. Add `Docs/Modding/External_Starter_Kit_File_Contract.md` and schema revision 66 proof so the validator checks that the starter kit documents no-full-Unity-project authoring and the envelope-only runtime boundary.

Rejected Alternatives: Telling public modders to use the full HECTON-8 Unity project was rejected because it is not a sane public workflow and exposes source-project assumptions. Making managed DLL/BepInEx/Harmony the normal answer was rejected because current runtime authority is envelope-only. Shipping only a Markdown file was rejected because the SDK Hub needed an executable creation point. Overwriting existing starter-kit files was rejected because public authors must not lose edits when refreshing references.

Scalability potential: Low tier authoring works with text files and emits no runtime packets until validation. Middle tier authors can use the Unity SDK Hub for starter-kit creation and static validation. High tier can layer Workbench graph/table/asset UI over the same file contract. Ultra tier can add package diff, simulation, preview, and visual-overkill diagnostics without widening runtime authority beyond validated envelopes.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is editor/offline tooling only. Runtime gameplay tick, packet layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 55 - Starter kit must validate without Unity or source-project access

Problem: The external starter kit gave public authors a folder layout, but still did not provide a local check that works without the HECTON-8 Unity project. A random internet author could edit JSON, leave a managed entry enabled, change graph runtime away from envelope-only, delete reference CSVs, or miss a required file and only discover the problem later inside internal tooling.

Solution: Generate `Tools/validate_structure.ps1` and `Tools/README.md` as part of `Create External Starter Kit`. The validator is self-contained PowerShell: it checks required directories/files, JSON parseability, `mod.h8manifest.json` `Compatibility.Runtime = envelope-only`, graph runtime `envelope-only`, API version floor, empty `EntryAssembly`, empty `EntryType`, asset/settings/locale shape, and presence of opcode/tuning reference CSVs. Schema revision 67 and static validation prove these checks exist in the generator and docs.

Rejected Alternatives: Requiring Unity for first-pass starter validation was rejected because it contradicts the public authoring answer. Depending only on the project static validator was rejected because it needs source-project access and validates the SDK, not a copied starter folder. Allowing non-empty managed entry fields in starter templates was rejected because it implies a runtime right that envelope-only mode disables.

Scalability potential: Low tier authors can validate with a text editor and PowerShell before any heavy tools. Middle tier can still use the Unity SDK Hub. High tier can have Workbench call the same structure checks before graph compile. Ultra tier can add package simulation and visual diagnostics after the same cheap fail-fast structure gate.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline validation only. No gameplay tick, packet layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, or Burst/job path changed.

## Decision 56 - External starter validation must match loader identity rules

Problem: The schema 67 starter kit validator checked structure and envelope-only safety, but did not enforce the same package identity rules as `ModLoader` and `ModBuilderWindow`. A public author could ship mismatched `mod.h8manifest.json`/`mod.json` IDs, use uppercase/path-ish/reserved filesystem device names, or add invalid dependency IDs and only hit failure later in project tooling.

Solution: Extend generated `Tools/validate_structure.ps1` with `Validate-ModId` and reserved segment checks matching the loader/builder contract: lowercase letters/digits separated by single `.`, `_`, or `-`, no whitespace, no leading/trailing/repeated separators, and no `con`, `prn`, `aux`, `nul`, `com1..com9`, or `lpt1..lpt9` segments. The validator now requires authoring/runtime ID parity and validates non-empty runtime dependency IDs. Schema revision 68 and the static validator prove the source, docs, schema, and playbook evidence.

Rejected Alternatives: Leaving ID validation only in the loader was rejected because random external authors need fail-fast no-Unity feedback. Lowercasing IDs automatically was rejected because package identity must be explicit and stable. Allowing mismatched authoring/runtime IDs was rejected because it creates one package with two identities and breaks deterministic ownership, save keys, reports, and dependency resolution.

Scalability potential: Low tier authors catch identity mistakes in a text-folder workflow before any Unity or Workbench cost. Middle tier can use the SDK Hub and local validator with the same contract. High tier can have Workbench call the same identity gate before package compilation. Ultra tier can add richer diagnostics and package diffing without changing runtime authority or identity ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is editor/offline validation only. Runtime gameplay tick, packet layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 57 - Public starter kit must exist as a versioned folder

Problem: The SDK Hub could generate `ModdingSDK/ExternalStarterKit/`, but the repository did not contain that folder as an actual public artifact. That still forces an external author without Unity to depend on someone inside the source project running an editor action before the starter kit can be copied or zipped.

Solution: Add `ModdingSDK/ExternalStarterKit/` as a versioned template with manifests, graph/table/content/locale drafts, reports/generated/reference/tool folders, copied opcode/tuning CSVs, and `Tools/validate_structure.ps1`. Extend `Validate_Mod_API_Static.ps1` to run the template's own local validator and require `ExternalStarterKitTemplateVersioned=True` plus `ExternalStarterKitTemplatePassesLocalValidator=True` in schema revision 69.

Rejected Alternatives: Keeping only the Unity generator was rejected because it contradicts the no-Unity authoring path for random public authors. Duplicating a separate docs-only template was rejected because one file contract must have one route. Shipping a ZIP binary was rejected because a versioned text folder is auditable and diffable.

Scalability potential: Low tier authors can copy the folder and run one PowerShell script with no Unity install. Middle tier can still refresh it through the SDK Hub. High tier Workbench can consume the same layout. Ultra tier can add simulation/package diff layers over the same template without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is repository/offline authoring material only. Runtime gameplay tick, FutureCommandEnvelope layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 58 - Starter reference CSVs must prove source parity

Problem: The versioned starter kit copies `allowed_opcodes.csv` and `kernel_tuning_profiles.csv`, but copied reference files can silently drift from the authoritative docs. A public author using stale opcode/tuning references would get false authoring guidance even though the runtime validator source is correct.

Solution: Extend `Validate_Mod_API_Static.ps1` to normalize line endings and compare `ModdingSDK/ExternalStarterKit/Reference/allowed_opcodes.csv` plus `kernel_tuning_profiles.csv` against `Docs/Modding/allowed_opcodes.csv` and `Docs/Modding/kernel_tuning_profiles.csv`. Schema revision 70 records `ExternalStarterKitTemplateReferenceCsvsMatchSource=True`.

Rejected Alternatives: Trusting the copied files was rejected because duplicated facts need a proof gate. Removing copied references was rejected because external authors need an offline starter folder. Making the local no-Unity validator depend on project docs was rejected because the copied starter kit must validate outside the source project.

Scalability potential: Low tier authors get correct offline reference data. Middle tier SDK Hub refresh keeps missing files available. High tier Workbench can surface the same authoritative references. Ultra tier can add richer opcode previews while this parity gate prevents stale public data.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is static/offline validation only. Runtime allowlist loading, command envelope layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 59 - Starter review handoff needs deterministic file hashes

Problem: The external starter kit could validate structure, IDs, envelope-only flags, and copied CSV parity, but a public author still had no no-Unity handoff artifact that answers "what exact files am I submitting?" A reviewer or future Workbench would need ad hoc folder inspection, and `Reports/` existed without a concrete report producer.

Solution: Add `Tools/build_review_manifest.ps1` to the versioned starter kit and SDK Hub generator. The script runs `Tools/validate_structure.ps1` first, then writes `Reports/review_manifest.json` with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root mod id, sorted relative file paths, byte counts, and lowercase SHA-256 hashes. It excludes `Generated/` and `Reports/` so reports/package outputs cannot hash themselves. Schema revision 71 and `Validate_Mod_API_Static.ps1` now prove the builder exists, passes on the versioned template, hashes required authoring/tool files, and excludes output folders.

Rejected Alternatives: A ZIP packer was rejected for this pass because runtime package ingestion is still envelope-only and not verified; shipping a binary artifact would obscure the text contract. A timestamped report was rejected because it would make the proof nondeterministic. Hashing `Reports/` was rejected because a report should not include itself or previous reports as source truth.

Scalability potential: Low tier authors can validate and produce a review manifest with PowerShell and a text editor. Middle tier SDK Hub users get the same tool generated non-destructively. High tier Workbench can ingest the JSON manifest as a stable review input. Ultra tier can add diff, simulation, visual preview, and overkill diagnostics while preserving the same file/hash route.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring/report generation only. Runtime gameplay tick, FutureCommandEnvelope layout, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 60 - Starter kit needs editor-readable JSON Schemas

Problem: The starter kit had JSON examples and a PowerShell validator, but schema-aware editors had no local schema mapping. A random public author could typo field names, forget envelope-only constants, or misunderstand required manifest fields while editing, then only discover the problem after running a script. That is avoidable friction in the no-Unity path.

Solution: Add `Schemas/*.schema.json` for authoring manifest, runtime manifest, graph, assets, settings table, and locale drafts, plus `.vscode/settings.json` mapping those schemas to starter files. Extend `Tools/validate_structure.ps1` to require the schema directory, parse every schema, require `$schema`, `title`, object type, and require the editor `json.schemas` mapping. Schema revision 72 and static validation prove generator output, versioned template presence, schema parseability, and editor mapping.

Rejected Alternatives: Relying only on Markdown was rejected because docs do not give inline field validation. Depending on an online schema URL was rejected because the starter kit must work offline after copying. Making schemas authoritative runtime validation was rejected because runtime authority still belongs to the envelope validator and loader, not editor hints.

Scalability potential: Low tier authors get autocomplete/error hints in a cheap text editor with no Unity install. Middle tier authors get the same hints plus local PowerShell validation. High tier Workbench can reuse the schema files as UI field contracts. Ultra tier can layer rich graph/table editors and visual diagnostics on the same schema route without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline editor assistance and validation metadata only. Runtime gameplay tick, FutureCommandEnvelope layout, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 61 - Starter identity edits need one offline route

Problem: The external starter kit had two identity-bearing manifests (`mod.h8manifest.json` and `mod.json`). A public modder copying the folder still had to manually edit id/name/author/version in both places, then discover mismatches only after running validation. That is a predictable failure mode for random external authors and it weakens one-owner identity discipline.

Solution: Add `Tools/set_mod_identity.ps1` to the versioned starter kit and SDK Hub generator. The tool validates the canonical mod id with the same lowercase separator/reserved-device rules as the starter validator, writes matching identity fields to both manifests, then runs `Tools/validate_structure.ps1`. Schema revision 73 and `Validate_Mod_API_Static.ps1` now prove the generated tool exists, the template contains it, a temp-copy positive probe updates both manifests, and malformed ids are rejected.

Rejected Alternatives: Leaving identity edits to documentation was rejected because two manually edited files create deterministic drift. Auto-normalizing uppercase or path-like ids was rejected because package identity must remain explicit, stable, and reviewable. Making the Unity SDK Hub the only identity editor was rejected because the public starter path must work without the full Unity project.

Scalability potential: Low tier authors copy the starter folder, run one PowerShell command, and validate with no Unity install. Middle tier authors can still refresh the same tool from SDK Hub. High tier Workbench can call this same identity route before graph/table UI opens. Ultra tier can add package diff and identity migration previews over the same route without changing runtime authority. Toaster path is text files plus PowerShell; high-end path can spend extra cycles on visual diagnostics after the same identity gate.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring only. It removes review/build churn and prevents identity mismatch packages before runtime loader, FutureCommandEnvelope, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job paths are touched.

## Decision 62 - Starter bootstrap must be one command and shell-portable

Problem: The starter kit had separate identity, validation, and review-manifest tools, but the happy path still required a public author to run commands in the right order. The tools also spawned nested `powershell`, which is Windows-specific and brittle for authors running PowerShell 7 as `pwsh` on macOS/Linux.

Solution: Add `Tools/prepare_mod.ps1` as the one-command no-Unity bootstrap. It calls `set_mod_identity.ps1`, `validate_structure.ps1`, and `build_review_manifest.ps1` in order through in-process script calls, not nested Windows PowerShell. Update the SDK Hub generator, versioned starter kit, docs, schema revision 74, and static validator. The validator now proves the prepare tool exists, updates identity on a temp copy, builds `Reports/review_manifest.json`, includes `Tools/prepare_mod.ps1` in the review hash list, excludes output folders, and verifies public tools do not contain nested `& powershell` child calls.

Rejected Alternatives: Leaving three separate commands as the main workflow was rejected because random external authors will run them out of order. Keeping nested `powershell -File` calls was rejected because it couples copied kits to Windows PowerShell instead of the current host, blocking the intended `pwsh` path. Building a real `.h8mod` packer here was rejected because runtime package ingestion proof is still pending and would imply shipping authority that the current envelope-only playbook has not verified.

Scalability potential: Low tier authors get one command and text files with no Unity install. Middle tier authors can still use the Unity SDK Hub to refresh missing files. High tier Workbench can call the same prepare route before richer UI opens. Ultra tier can add package diff, simulation, and visual diagnostics after the same deterministic review manifest is produced. Weak-device path stays command-line and offline; high-end path spends extra cycles only after the same proof artifact exists.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring and report generation only. Runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 63 - Starter portability claims need path proof, not prose

Problem: Public starter docs advised `pwsh` on macOS/Linux, but the copied-kit tools still used Windows backslash child paths for internal tool lookup. The local validator also accepted any `.vscode/settings.json` with a `json.schemas` property, so a copied kit could lose exact schema mappings and still pass.

Solution: Add `Join-StarterPath` to all public starter scripts and to SDK Hub script generation. Child tool paths and review output paths now normalize `\` to `/` and then compose platform-native path segments through `Join-Path`. Tighten `validate_structure.ps1` to require exact schema URL/fileMatch pairs for all six starter file families. Schema revision 75 and static validation now prove both properties.

Rejected Alternatives: Keeping the `pwsh` advice as documentation only was rejected because it made a portability promise without source proof. Removing macOS/Linux guidance was rejected because the copied starter kit should remain no-Unity and text-editor friendly. Checking only `json.schemas` existence was rejected because it does not protect actual editor usability.

Scalability potential: Low tier authors keep the cheapest path: copied text folder plus one PowerShell command. Middle tier uses the same local validator before SDK Hub/Workbench. High tier can add richer schema-aware editors over the same `.vscode` and JSON Schema contract. Ultra tier can add simulation/package diff tooling after the same deterministic review manifest; runtime authority remains envelope-only.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring tooling only. Runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 64 - Review manifests need bounded source hashing

Problem: `Tools/build_review_manifest.ps1` validated structure before hashing, but then walked every non-output source file without an explicit file count or byte ceiling. A copied starter kit could accidentally contain a large binary dump or bulk folder and turn a cheap no-Unity review step into an unbounded local hashing job.

Solution: Add explicit cold authoring limits to the versioned starter script and SDK Hub generator: max `256` hashed source files, max `4194304` bytes per source file, and max `33554432` total source bytes. The manifest now records `TotalBytes` and `Limits`, and oversized source files fail before hashing. Static validation runs a temp-copy oversized-file probe and records schema revision 76 proof.

Rejected Alternatives: Leaving the report unbounded was rejected because public starter kits are for random external authors, not trusted internal folders. Building a `.h8mod` packer here was rejected because runtime package ingestion remains envelope-only and not verified. Hashing outputs was still rejected because reports and generated binaries must not become their own source proof.

Scalability potential: Low tier authors keep a bounded text-folder workflow that cannot become a surprise bulk-ingest tool. Middle tier SDK Hub refreshes the same bounded script. High tier Workbench can ingest the same review manifest with predictable report size. Ultra tier can add package diff, simulation, and visual diagnostics after the same limits; runtime authority remains envelope-only.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring/report generation only. It prevents cold tool stalls and accidental large-file hashing before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job paths are touched.

## Decision 65 - Starter graph opcode validation must fail before Workbench/runtime

Problem: `Graphs/main.h8graph.json` had a JSON schema `Opcode` string but the copied-kit validator did not prove the string belonged to `Reference/allowed_opcodes.csv`. It also did not reject duplicate/missing node IDs, missing opcodes, or graph `MaxEnvelopesPerFrame` above `mod.h8manifest.json` `Budgets.MaxEnvelopesPerFrame`. A public author could submit a review manifest that looked structurally valid while the graph contained an opcode the runtime allowlist would never accept.

Solution: Extend `Tools/validate_structure.ps1` and the SDK Hub generated validator to read `Reference/allowed_opcodes.csv`, accept hex tokens plus first-word comment aliases such as `SpawnItem`, reject invalid CSV tokens, reject null/duplicate/missing graph nodes, reject unsupported node opcodes, and enforce graph budget parity. Tighten `Schemas/h8graph.schema.json` so non-empty node objects require `Id` and `Opcode`. Extend schema revision 77, static validation, runtime playbook, and public docs with temp-copy invalid opcode rejection proof.

Rejected Alternatives: Hardcoding opcode names inside the JSON schema was rejected because the CSV is the copied offline authority and must stay source-compared to `Docs/Modding/allowed_opcodes.csv`. Deferring errors to a future Workbench graph compiler was rejected because the current public path is a no-Unity text folder. Allowing arbitrary aliases was rejected because comments in `allowed_opcodes.csv` are documentation, not unbounded public authority.

Scalability potential: Low tier authors get immediate text-folder validation with no Unity install. Middle tier SDK Hub refresh produces the same local gate. High tier Workbench can call the same validator before visual graph compile. Ultra tier can add package diff, simulation, and graph visual diagnostics after the same allowlist/budget proof; runtime authority remains envelope-only.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring validation only. It prevents invalid graph submissions before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job paths are touched.

## Decision 66 - Starter graph opcodes need a discoverable no-Unity list

Problem: After schema 77, copied starter kits could fail invalid graph opcodes, but random public authors still had to open `Reference/allowed_opcodes.csv` and infer that the first comment token was an accepted alias. That is a poor SDK contract: validation existed, discovery did not.

Solution: Add `Tools/list_allowed_opcodes.ps1` as the single offline discovery route. It reads the copied `Reference/allowed_opcodes.csv`, validates token shape, rejects duplicate hex tokens and aliases, prints alias/hash pairs for humans, and emits `hecton8.allowed_graph_opcodes.v1` JSON for future Workbench/CLI UI. The SDK Hub generator emits the same file, the local validator requires it, review manifests hash it, and schema revision 78/static validation prove text and JSON output.

Rejected Alternatives: Hardcoding opcode aliases into Markdown was rejected because duplicated facts drift. Hardcoding aliases in JSON Schema was rejected because schema regex cannot remain the opcode authority. Requiring Unity/Workbench for opcode discovery was rejected because the current public path explicitly supports no-Unity authoring.

Scalability potential: Low tier authors get a cheap text command before editing graph nodes. Middle tier can use the same helper in copied starter folders. High tier Workbench can consume the JSON output without inventing a second opcode route. Ultra tier can add graph previews and simulation after the same allowlist proof. Runtime authority remains envelope-only.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring only. It prevents review churn and invalid graph submissions before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job paths are touched.

## Decision 67 - Starter manifests need one public identity and semantic versions

Problem: The public starter kit had two identity-bearing manifests. `Id` parity was enforced, but `DisplayName`/`Name`, `Author`, and `Version` could drift. `Version` only required non-empty text, so `bad version` could enter authoring/runtime manifests and reach review tooling as if it were a real package version.

Solution: Enforce semantic version syntax in the identity helper, structure validator, JSON Schemas, SDK Hub generated scripts, schema revision 79, and static validation. Require `mod.h8manifest.json` `DisplayName`, `Author`, and `Version` to match `mod.json` `Name`, `Author`, and `Version`. Keep the starter defaults generic and run identity mutation probes only on temp copies.

Rejected Alternatives: Leaving version shape to documentation was rejected because random external authors need fail-fast local validation. Auto-normalizing invalid versions was rejected because package identity/version must be explicit and reviewable. Checking only `Id` parity was rejected because a review artifact with mismatched names/authors/versions is not a single package identity.

Scalability potential: Low tier authors get a copied folder with one command and deterministic local errors before review. Middle tier SDK Hub refresh emits the same checks. High tier Workbench can call the same validator and show field-level errors. Ultra tier can add package diff, migration preview, and visual diagnostics over the same identity contract. Runtime authority remains envelope-only.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring and review-proof hygiene. It prevents invalid package metadata before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job paths are touched.

## Decision 68 - Review manifest must carry validated package identity

Problem: `Reports/review_manifest.json` proved file hashes and root id, but not the public package identity a reviewer or future Workbench/CLI screen needs: display name, author, semantic version, required API version, and mod priority. Those facts were already validated across `mod.h8manifest.json` and `mod.json`, but the report did not carry them.

Solution: Add an `Identity` object to the no-Unity review manifest after `Tools/validate_structure.ps1` passes. The object records `Id`, `DisplayName`, `Author`, `Version`, `RequiredAPIVersion`, and `ModPriority`. Extend SDK Hub generated scripts, starter docs, schema revision 80, static validation, and runtime playbook proof to assert the identity summary exists and matches the validated runtime/authoring manifests.

Rejected Alternatives: Requiring reviewers to inspect both manifests was rejected because review handoff should be one deterministic report. Adding a package format or runtime loader change was rejected because runtime package ingestion remains envelope-only and separately pending verification. Duplicating unvalidated identity values was rejected; the report is written only after the structure validator succeeds.

Scalability potential: Low tier authors still run one PowerShell command and get a readable report. Middle tier SDK Hub emits the same report. High tier Workbench can display identity without rescanning files. Ultra tier package diff and migration preview can key off the same report object. Runtime authority remains envelope-only.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline report metadata only. It prevents review and tooling churn before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job paths are touched.

## Decision 69 - Public SDK entry must not lead with legacy runtime packaging

Problem: The SDK Hub's first authoring action opened `ModBuilderWindow`, and the builder had a direct top-level `Hecton/Modding/Mod Builder` menu. That teaches random public authors to start from a Unity-only DLL/AssetBundle-oriented legacy builder even though current public UGC runtime is envelope-only and the no-Unity External Starter Kit is the intended public path.

Solution: Make External Starter Kit the first SDK Hub action. Move builder access to an `Internal Legacy` section, require a confirmation dialog, and move the direct menu to `Hecton/Modding/Internal/Legacy Mod Builder`. Update schema revision 81, static validator, runtime playbook, and docs so this UX boundary is source-checked.

Rejected Alternatives: Removing `ModBuilderWindow` was rejected because its manifest parity and bounded DLL/bundle checks are still useful internal loader proof while runtime package smoke is pending. Keeping the old first button was rejected because warning prose is not enough if the first click sends authors into the wrong tool. Reopening managed runtime modding was rejected because envelope-only quarantine remains the active runtime contract.

Scalability potential: Low tier authors see the no-Unity starter path first and can work from text files plus PowerShell/pwsh. Middle tier Unity users still get the Hub and static validator without entering legacy packaging. High tier Workbench can replace the same public starter route without changing runtime authority. Ultra tier can add package diff/simulation over the starter/review manifest while keeping legacy packaging isolated.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is editor UX and static contract hygiene only. Runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, and Burst/job paths are unchanged.

## Decision 70 - Prepare must support the normal edit-review loop

Problem: `Tools/prepare_mod.ps1` was framed as the one-command public starter path, but it hard-failed without `-Id`. After a modder already set identity, the common loop is edit JSON/graph/table/locale, validate, and rebuild `Reports/review_manifest.json`. Requiring identity arguments every time is noisy and invites accidental identity churn.

Solution: Make prepare two-mode. With `-Id`, it calls `Tools/set_mod_identity.ps1` and then builds the review manifest. Without identity arguments, it skips identity mutation, validates existing manifests through `Tools/build_review_manifest.ps1`, parses the generated review manifest, and reports the package id from that proof artifact. Static validation now runs both modes on a temp copy and schema revision 82 records `externalStarterKitPrepareToolSupportsExistingManifest`.

Rejected Alternatives: Keeping separate `validate_structure.ps1` plus `build_review_manifest.ps1` as the edit loop was rejected because the public SDK already promises a one-command path. Silently accepting `DisplayName`, `Author`, or `Version` without `-Id` was rejected because partial identity edits would be ambiguous. Reopening Unity/legacy builder as the normal loop was rejected because current public runtime remains envelope-only.

Scalability potential: Low tier authors get one cheap PowerShell/pwsh command after every edit. Middle tier SDK Hub refresh emits the same behavior. High tier Workbench/CLI can call the same prepare route for deterministic review reports. Ultra tier can add simulation, package diff, and visual diagnostics after the same report without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring only. It reduces authoring churn before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job paths are touched.

## Decision 71 - Unity users need one starter-kit cockpit, not scattered buttons

Problem: The public no-Unity starter kit had the right files and tools, and the SDK Hub pointed authors to it, but a Unity-side author still had to jump between Hub buttons, file explorer, PowerShell scripts, raw JSON files, and docs to answer basic questions: what is my mod identity, how do I validate, what opcodes are legal, and what did the review report contain. That is a real usability defect for random external authors and it makes the SDK feel less integrated than the actual contracts are.

Solution: Add `ExternalStarterKitWorkbenchWindow` as an Editor-only facade over the existing External Starter Kit. It reuses `ModdingSdkHubWindow.CreateExternalStarterKit()` for create/refresh, calls `Tools/set_mod_identity.ps1`, `Tools/prepare_mod.ps1`, and `Tools/list_allowed_opcodes.ps1`, opens the authoring/runtime manifests, graph, settings, locale, and review report, and displays `Reports/review_manifest.json` identity/file/byte summary. Schema revision 83 and `Validate_Mod_API_Static.ps1` now prove the Hub opens the Workbench and the Workbench preserves the same generator/tool/report/envelope-only route.

Rejected Alternatives: A second Unity generator was rejected because it would create drift against the no-Unity starter contract. Opening the legacy `ModBuilderWindow` as the "full interface" was rejected because managed DLL and loose AssetBundle runtime ingress are disabled. A docs-only explanation was rejected because the user-facing flaw was workflow fragmentation, not missing prose.

Scalability potential: Low tier authors can still use copied files and PowerShell/pwsh with no Unity project. Middle tier authors get a single Unity screen for starter creation, identity, validation, opcode discovery, and review summary. High tier can add graph/table/asset panels over the same files. Ultra tier can add simulation, package diff, preview, and visual-overkill diagnostics after the same review manifest without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is editor/offline tooling only. It removes authoring friction before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling are touched.

## Decision 72 - Workbench needs visible starter health before tool failure

Problem: The first Workbench gave Unity-side authors a single cockpit, but it still made them discover a broken copied starter folder only after launching a script. Random external modders need immediate health state, direct structure validation, and local contract links from the same screen.

Solution: Add a required-file health panel to `ExternalStarterKitWorkbenchWindow`, backed by the same starter file list used by the public contract. The Workbench now counts present/missing starter files, bytes, and newest write time, warns on missing files, runs `Tools/validate_structure.ps1` directly for fast checks, keeps `Tools/prepare_mod.ps1` for review handoff, and opens the file contract, API spec, authoring plan, and runtime playbook. Schema revision 84 and static validation now prove these routes.

Rejected Alternatives: Reimplementing validation in C# was rejected because `Tools/validate_structure.ps1` is the copied no-Unity authority and a second validator would drift. Adding only more README text was rejected because the defect is in the interactive authoring loop. Enabling legacy builder or managed runtime mod ingress was rejected because current public runtime remains envelope-only.

Scalability potential: Low tier authors still use copied files plus PowerShell/pwsh without Unity. Middle tier Unity users see starter health and run direct validation before review. High tier can layer graph/table/asset panels over the same health/report route. Ultra tier can add simulation, package diff, preview, and visual diagnostics after the same review manifest without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline tooling only. It prevents broken starter folders from reaching runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 73 - Workbench tool execution must not freeze the Unity Editor

Problem: `ExternalStarterKitWorkbenchWindow` launched PowerShell tools synchronously from the EditorWindow path and read stdout/stderr with blocking `ReadToEnd` plus `WaitForExit`. For random public authors this means the project-integrated SDK screen can freeze during validation, and large stderr/stdout output can deadlock the process pipe.

Solution: Replace blocking tool execution with an async Editor-only runner. The Workbench now starts the process, reads stdout/stderr through `BeginOutputReadLine`/`BeginErrorReadLine`, disables tool/action buttons while a tool is active, records process completion through the `Exited` callback, and finalizes tool summary/reload from `EditorApplication.update` on the Unity main thread. Schema revision 85 and static validation prove the async route and reject `ReadToEnd`/`WaitForExit` regression.

Rejected Alternatives: Keeping blocking waits was rejected because public SDK UX must remain responsive. Reimplementing validation in C# was rejected because the no-Unity PowerShell tools are the copied starter authority. Moving tool execution into runtime code was rejected because this is authoring-only and the public runtime remains envelope-only.

Scalability potential: Low tier authors still run scripts directly outside Unity. Middle tier Unity users get a responsive Workbench while validation/review tools run. High tier can add graph/table preview panels without blocking editor repaint. Ultra tier can add simulation/package diff/preview over the same async tool lane without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline tooling only. It removes an editor responsiveness/deadlock risk before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 74 - Review reports must expose freshness, not just existence

Problem: The Workbench showed `Reports/review_manifest.json` identity/file summary, but did not tell a modder whether that report was older than edited starter sources. A random public author could change graph/table/locale files and submit a stale review report while the Workbench still looked healthy.

Solution: Add a bounded review freshness check to `ExternalStarterKitWorkbenchWindow`. It compares the report write time against the newest starter source file, excludes `Generated/` and `Reports/` so outputs do not stale themselves, caps the scan at `512` source files, and warns when the report is stale or the scan is capped. Schema revision 86 and static validation now prove this route.

Rejected Alternatives: Hashing every source file on every Workbench reload was rejected because the review builder already owns hashing and Workbench reload must stay cheap. Trusting report existence was rejected because existence is not freshness. Including `Generated/` and `Reports/` was rejected because output files would create false stale states.

Scalability potential: Low tier authors still rely on the CLI prepare loop. Middle tier Unity users see stale report warnings before handoff. High tier can add package diff and graph/table previews over the same source/report freshness rule. Ultra tier can add simulation and visual diagnostics after the same review manifest without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline tooling only. It prevents stale review handoff before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 75 - Copied starter kits need one root launcher

Problem: The starter kit had correct local tools, and the Workbench could call them, but a random external author outside Unity still had to copy several raw `Tools/*.ps1` commands from README. That is a real SDK usability defect: the file contract was valid, but the first no-Unity interaction was fragmented.

Solution: Add root `h8mod.ps1` as a thin launcher over the existing tools. It exposes menu/setup/validate/review/prepare/opcode actions, delegates to `Tools/prepare_mod.ps1`, `Tools/validate_structure.ps1`, `Tools/build_review_manifest.ps1`, and `Tools/list_allowed_opcodes.ps1`, and keeps the existing scripts as the contract owners. SDK Hub generation, Workbench health/file access, local validation, schema revision 87, docs, and static validation now require/prove that launcher.

Rejected Alternatives: Reimplementing validation inside `h8mod.ps1` was rejected because it would create drift against the no-Unity validator. Adding a compiled CLI was rejected for this pass because it would introduce new packaging/runtime dependencies while the PowerShell/pwsh starter contract already exists. Leaving README command lists as the main path was rejected because it fails the random-modder usability requirement.

Scalability potential: Low tier authors get one command/menu in a copied folder with no Unity install. Middle tier authors can use the same starter through the Unity Workbench. High tier can later wrap the same action names in a richer CLI or GUI. Ultra tier can add simulation, package diff, preview, and visual diagnostics after the same review manifest without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline authoring only. It prevents user-error package churn before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 76 - SDK Hub validator must not block the Unity Editor

Problem: `ModdingSdkHubWindow.RunStaticValidator` launched `Validate_Mod_API_Static.ps1` with blocking `StandardOutput.ReadToEnd`, `StandardError.ReadToEnd`, and `WaitForExit` directly from the EditorWindow path. That left the public SDK Hub vulnerable to Unity Editor freezes and pipe deadlock on large validator output, while the Workbench had already moved starter tools to async execution.

Solution: Convert the SDK Hub validator button to the same editor-safe process pattern: async stdout/stderr reads, disabled validator button while a process is active, process completion through `EditorApplication.update`, and process kill/dispose on window disable. Schema revision 88 and `Validate_Mod_API_Static.ps1` now fail if `ReadToEnd`/`WaitForExit` returns to the Hub.

Rejected Alternatives: Leaving the Hub synchronous because the Workbench has async tools was rejected; the Hub is still the first public integrated SDK entry. Removing the Hub validator button was rejected because the static validator is a useful source/doc proof route. Reimplementing the validator in C# was rejected because the PowerShell validator is the current single source-backed contract gate.

Scalability potential: Low tier Unity Editor users avoid UI stalls while static validation runs. Middle tier authors keep the same Hub workflow. High tier can run heavier static checks without freezing the entry screen. Ultra tier can add richer diagnostics over the same async process lane without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline tooling only. It prevents authoring UI stalls before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 77 - Workbench health must match the no-Unity validator

Problem: `ExternalStarterKitWorkbenchWindow.RequiredStarterFiles` drifted from `Tools/validate_structure.ps1`: it still checked stale `Schemas/h8table.schema.json` and `Schemas/h8loc.schema.json`, while the actual starter contract requires `Schemas/settings_table.schema.json`, `Schemas/locale.schema.json`, `Schemas/assets.schema.json`, folder READMEs, and `Tools/README.md`. A valid copied starter kit could therefore look broken in the integrated Workbench, which is a direct usability failure for random external modders.

Solution: Align the Workbench health list with the validator's required-file list and add a schema/static proof flag, `ExternalStarterKitWorkbenchRequiredFileListMatchesValidator`, so stale schema names or omitted required files fail the modding static validator. Schema revision 89, runtime playbook, README, API spec, authoring plan, product blueprint, and starter file contract now record the parity.

Rejected Alternatives: Renaming starter schema files back to old names was rejected because `settings_table.schema.json` and `locale.schema.json` are already the current editor schema mappings validated by `.vscode/settings.json`. Leaving the direct `Validate Structure Only` button as the only truth was rejected because health UI must not lie before the user clicks validation. Reimplementing the PowerShell validator in C# was rejected because it would create a second file contract.

Scalability potential: Low tier authors still use the copied folder and root `h8mod.ps1` without Unity. Middle tier Unity authors get accurate required-file health from the Workbench. High tier can add graph/table/asset panels over the same file contract. Ultra tier can add package diff, preview, and simulation without changing the required-file owner.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline SDK tooling only. It prevents false broken-starter states before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 78 - Failed SDK tools must be Editor error UI

Problem: The SDK Hub and External Starter Kit Workbench already ran validators/tools asynchronously, but completed nonzero process exits were rendered through normal info help boxes. For random external modders this hides the severity of broken structure validation, failed prepare, or failed static validation behind a visually neutral message.

Solution: Track process-result severity separately from summary text. `ModdingSdkHubWindow` stores `_lastValidatorFailed`; `ExternalStarterKitWorkbenchWindow` stores `_toolSummaryIsError`. Missing scripts, launch failures, process-start failures, and nonzero exit codes render as `MessageType.Error`; running/already-running and successful summaries stay informational. Schema revision 90 and the static validator now fail if those error-state routes disappear.

Rejected Alternatives: Parsing stderr text in docs was rejected because UI severity must not depend on human interpretation. Reimplementing starter validation in C# was rejected because the PowerShell/pwsh starter tools are the copied no-Unity authority. Adding modal dialogs was rejected because tool output must remain reviewable in the Workbench/Hub surface without blocking the authoring loop.

Scalability potential: Low tier authors using only `h8mod.ps1` keep the same CLI contract. Middle tier Unity authors get immediate severity in the integrated Workbench/Hub. High tier can add richer tool-result panels over the same boolean error state. Ultra tier can add diagnostics, package diff, and simulation after the same process-result contract without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline SDK UX only. It prevents failed authoring packages from advancing toward runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 79 - Workbench must show graph contract state before validation

Problem: The External Starter Kit Workbench could open `Graphs/main.h8graph.json` and list allowed opcodes, but it did not show whether the current graph already violated the copied starter contract. A random internet author had to edit raw JSON and run validation to discover duplicate node IDs, missing fields, invalid opcode aliases/hex tokens, wrong `Runtime`, or `MaxEnvelopesPerFrame` budget drift.

Solution: Add an Editor-only Graph Contract Preview to `ExternalStarterKitWorkbenchWindow`. It parses `Graphs/main.h8graph.json`, loads `Reference/allowed_opcodes.csv`, validates CSV hex/alias shape, compares graph budget to `mod.h8manifest.json`, caps the preview at `256` nodes, `1 MB` graph/CSV files, and `512` opcode rows, and surfaces runtime flag, node count, duplicate IDs, invalid opcodes, missing fields, and budget errors before tool execution. Schema revision 91 and the static validator prove the panel remains present.

Rejected Alternatives: Reimplementing the full PowerShell validator in C# was rejected because `Tools/validate_structure.ps1` remains the no-Unity authority. A docs-only note was rejected because the usability defect is interactive visibility, not missing prose. Adding a runtime graph compiler was rejected because public runtime ingress remains envelope-only and this pass is editor/offline authoring.

Scalability potential: Low tier authors still use `h8mod.ps1` and schema-aware text editors without Unity. Middle tier Unity authors get immediate graph-contract feedback in the integrated Workbench. High tier can add structured graph editing over the same file contract. Ultra tier can add simulation, diff, and visual diagnostics after the same validation route without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline SDK UX only. It prevents invalid graph authoring states from advancing toward runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 80 - Graph node creation must be a generated snippet, not blind JSON surgery

Problem: The Workbench graph preview made contract defects visible, but creation of a new graph node was still a raw JSON editing task. A random external modder could mistype an opcode alias, invent an invalid node id, or ask tooling to mutate `Graphs/main.h8graph.json` directly and risk losing unknown future fields or author ordering.

Solution: Add `Tools/create_graph_node_snippet.ps1` as an offline snippet generator. It validates node IDs, resolves opcode aliases or hex values from `Reference/allowed_opcodes.csv`, writes a minimal object to `Generated/graph_node_snippet.json`, supports machine-readable JSON output, and leaves the graph file untouched. The Unity Workbench exposes the same route through Node Id/Opcode fields and generate/open buttons. The root `h8mod.ps1` launcher exposes `node-snippet`, and the SDK Hub generator writes the same tool into refreshed starter kits. Schema revision 92 and static validation prove the tool, Workbench route, root launcher route, docs, and runtime playbook flags.

Rejected Alternatives: Directly appending nodes into `Graphs/main.h8graph.json` was rejected because it can destroy unknown fields, comments are already unavailable in JSON, and author ordering/conflict resolution belongs to a future structured graph editor. A Unity-only graph editor was rejected because the external starter kit explicitly supports authors with no Unity project. A runtime graph compiler was rejected because public runtime ingress remains envelope-only and this pass is authoring-only.

Scalability potential: Low tier authors use `h8mod.ps1 -Action node-snippet` and copy one generated object in a text editor. Middle tier authors use the Workbench panel with immediate graph preview and structure validation. High tier can add structured insert/merge after the same snippet contract. Ultra tier can add visual graph simulation, diff, and package diagnostics after the same review manifest without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline SDK UX only. It prevents malformed graph nodes before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 81 - Submission handoff must be a review package, not runtime install

Problem: The starter kit could validate and build `Reports/review_manifest.json`, but a public author still had no single handoff artifact. A direct install/copy-to-`Mods/` route would be false because current runtime UGC ingress remains envelope-only and loose starter sources are not a verified loader format.

Solution: Add `Tools/build_submission_package.ps1` as an offline submission packer. It runs prepare, validates review manifest schema/runtime, packages reviewed starter sources plus `Reports/review_manifest.json` into `Generated/<mod-id>_submission.zip`, rejects unsafe paths, refuses review-listed `Generated/`/`Reports/` source inputs, and is exposed through `h8mod.ps1 -Action submission` plus the Workbench button. Schema revision 93 and the static validator now prove generator/template/Workbench/root-launcher parity.

Rejected Alternatives: Installing directly into `Mods/` was rejected because it would imply runtime support the loader policy does not grant. Blindly zipping the folder was rejected because it can include stale reports, generated outputs, or unsafe paths. A Unity-only export was rejected because the starter kit must work for authors without the full project.

Scalability potential: Low tier authors get one PowerShell/pwsh command that produces a bounded review artifact. Middle tier authors use the Workbench button over the same tool. High tier can layer package diff and moderation checks over the same zip and manifest. Ultra tier can add simulation/preview diagnostics after the same review manifest without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline SDK tooling only. It reduces failed handoffs before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 82 - Submission package status must be visible and replacement must preserve the previous zip

Problem: Schema 93 produced a review/submission zip, but a Unity Workbench author still had no integrated status for the current `Generated/<mod-id>_submission.zip`: no path, freshness, byte size, or direct open/reveal route. The packer also removed the previous zip before installing the replacement, so a final move failure could erase the last good handoff artifact.

Solution: Add an Editor-only submission package status panel to `ExternalStarterKitWorkbenchWindow` that finds the newest `Generated/*_submission.zip`, compares its write time to `Reports/review_manifest.json`, shows path/bytes/write time/freshness, opens the zip, and reveals `Generated/` when absent. Change checked-in and generated `Tools/build_submission_package.ps1` to write a temp zip first, move the old zip to a `.previous` backup only during replacement, restore that backup on replacement failure, and clean stale temp/backup outputs. Schema revision 94 and the static validator prove the Workbench status route and previous-zip preservation route.

Rejected Alternatives: A runtime `Mods/` install button was rejected because public runtime ingress remains envelope-only and this starter zip is a review handoff artifact. Blindly opening `Generated/` without freshness was rejected because it leaves stale package risk invisible. Removing the old zip before temp creation or before final replacement proof was rejected because it destroys the last known-good review artifact on failure.

Scalability potential: Low tier authors use `h8mod.ps1 -Action submission` and keep the previous zip if replacement fails. Middle tier authors use the Workbench package status/open route. High tier can add package diff and moderation checks over the same review manifest and zip. Ultra tier can add simulation, preview, and visual diagnostics after the same handoff artifact without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline SDK tooling only. It prevents failed handoff artifacts before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 83 - Settings and locale authoring must fail before handoff

Problem: The external starter kit exposed settings and locale JSON files, but the Workbench did not show their current contract state and the no-Unity validator only proved shallow existence. A random author could submit invalid setting IDs, wrong setting kinds/default types, bad locale codes, bad string keys, or empty localized values and only discover the damage later in review/runtime integration.

Solution: Make settings and locale data first-class authoring contracts. The checked-in and generated validators now enforce settings schema, row array shape, row cap, canonical row IDs, duplicates, kind enum, required/default type rules, locale schema, locale code, canonical string keys, non-empty string values, and string cap. The Workbench now has a bounded Authoring Data Preview over `Tables/settings.h8table.json` and `Locales/en.h8loc.json`. JSON Schemas and static validation were updated to schema revision 95 with negative probes for bad settings and locale files.

Rejected Alternatives: A runtime loader fallback was rejected because malformed authoring data must not reach runtime. A full Unity table/locale editor was rejected for this pass because it would create a larger UI surface before the file contract was strict. Duplicating every PowerShell validator rule in C# was rejected because the Workbench panel is a preview; `Tools/validate_structure.ps1` remains the no-Unity authority.

Scalability potential: Low tier authors use `h8mod.ps1 -Action validate` and schema-aware editors without Unity. Middle tier Unity authors see settings/locale status directly in the Workbench before package handoff. High tier can add structured table/locale editing over the same strict file contract. Ultra tier can add visual preview, localization diffing, and package simulation after the same review manifest without changing runtime authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline SDK tooling only. It prevents invalid authoring data before runtime loader, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, Burst/job paths, or device quality scaling.

## Decision 84 - Public mod powers must be explicit before they become broader

Problem: The starter kit had safe envelope-only boundaries and increasingly strict graph/settings/locale/review tooling, but a public author still had to infer what could be modded from scattered files. That creates two bad outcomes: authors assume forbidden routes like Harmony/BepInEx/loose AssetBundles, or they under-use the allowed graph/settings/locale/content/review surfaces.

Solution: Add a first-class capability contract that is visible in both no-Unity and Unity authoring loops. `Docs/capabilities.md` now lists supported surfaces, forbidden runtime rights, no-Unity flow, Unity Workbench flow, and expansion route. `h8mod.ps1 -Action capabilities` prints it. `ExternalStarterKitWorkbenchWindow` now shows a Capability Matrix with supported authoring surfaces, declared manifest capability count, allowed opcode counts, budgets, file state, and forbidden runtime rights. `Tools/validate_structure.ps1`, the SDK Hub generator, schema revision 96, static validator, runtime playbook, and public docs now prove this route and fail on drift.

Rejected Alternatives: Enabling arbitrary managed DLLs, Harmony, BepInEx, or loose asset runtime loading was rejected because the current owner model has no runtime authority, sandbox proof, save boundary, hot lane capacity, or security telemetry for those rights. A docs-only guide outside the starter kit was rejected because random authors using a copied folder may never open project docs. A Unity-only capability UI was rejected because public authors can build mods without the HECTON-8 Unity project.

Scalability potential: Low tier authors use `h8mod.ps1 -Action capabilities`, JSON files, schemas, and validation without Unity. Middle tier authors use the Workbench Capability Matrix plus graph/data previews. High tier can add structured graph/table/content editors over the same file contract. Ultra tier can add preview simulation, package diff, visual diagnostics, and capability-specific validators after the same engine-owned route without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline SDK UX and validation only. It prevents false runtime promises before FutureCommandEnvelope validation, SignalBus, GlobalRegistry, GlobalDataVault, save, physics, rendering, Burst/job, telemetry, or quality-scaling routes are changed.

## Decision 85 - Settings and locale creation must be snippet-generated before structured editors

Problem: Schema 96 made public mod powers explicit and schema 95 validated settings/locale data, but a random external author still had to hand-author new settings rows and locale entries as raw JSON. That is a usability and safety defect: the validator catches bad objects after the fact, while the starter kit should also generate the correct object shape before copy/paste.

Solution: Add no-Unity snippet helpers for settings rows and locale entries, expose them through root `h8mod.ps1`, surface them in `ExternalStarterKitWorkbenchWindow`, and make `ModdingSdkHubWindow` generate the same scripts/routes/docs into refreshed starter kits. The helpers write only under `Generated/`, validate canonical IDs/keys and typed defaults/values, support machine-readable JSON, and explicitly do not mutate `Tables/settings.h8table.json` or `Locales/en.h8loc.json`. Schema revision 97 and static validation now fail if this route drifts.

Rejected Alternatives: Directly editing `Tables/settings.h8table.json` or `Locales/en.h8loc.json` from a helper was rejected because it can destroy unknown future fields, reorder author data, or hide merge conflicts. A Unity-only editor was rejected because public starter-kit authoring must work without the HECTON-8 Unity project. Managed DLL/Harmony/BepInEx expansion was rejected because current runtime authority remains envelope-only and lacks sandbox, save-boundary, hot-lane, telemetry, and device-budget proof.

Scalability potential: Low tier authors use `h8mod.ps1 -Action setting-snippet` and `h8mod.ps1 -Action locale-snippet` in a copied folder with no Unity install. Middle tier authors use the Workbench panel and the same scripts. High tier can add structured table/locale editors over this Generated-only object contract. Ultra tier can add simulation, diff, localization preview, and package diagnostics after the same review manifest without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is Editor/offline SDK UX only. It prevents malformed settings/locale objects before runtime loader, FutureCommandEnvelope validation, HectonEventBus mod isolation, SignalBus, GlobalRegistry, GlobalDataVault, save, physics, rendering, Burst/job, telemetry, or continuous `GlobalQualityWeight` routes are touched.

## Decision 86 - Settings and locale snippets need a bounded apply path

Problem: Schema 97 generated valid settings and locale snippets, but the primary workflow still told random external authors to copy JSON by hand into `Tables/settings.h8table.json` and `Locales/en.h8loc.json`. That is exactly where non-technical modders break brackets, duplicate IDs, destroy unknown future fields, or miss validation before review handoff.

Solution: Add bounded offline apply helpers for settings and locale snippets. The helpers accept only safe starter-relative Generated snippets and exact target files, reject duplicate IDs/keys unless `-Replace` is explicit, write through temp files, validate the whole starter kit after replacement, and restore the previous file on failure. Expose the route through `h8mod.ps1`, External Starter Kit Workbench buttons, SDK Hub generator output, local validator requirements, schema revision 98, static validator probes, and public docs.

Rejected Alternatives: Blind append was rejected because it can corrupt unknown table/locale structure and silently create duplicates. Always replacing was rejected because overwrite must be an explicit author decision. A Unity-only editor was rejected because the starter kit must remain usable by authors without the HECTON-8 Unity project. Runtime code/mod DLL expansion was rejected because this is authoring data, and runtime UGC authority remains envelope-only.

Scalability potential: Low tier authors can create and apply settings/locale entries from PowerShell or pwsh without Unity. Middle tier authors can use Workbench buttons over the same tools. High tier can layer a structured table/locale editor on this contract. Ultra tier can add preview, localization diff, simulation, package diagnostics, and visual-overkill authoring views without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame measured/claimed. This is offline SDK UX and validation only. It prevents malformed authoring data before runtime loader, FutureCommandEnvelope validation, HectonEventBus mod isolation, SignalBus, GlobalRegistry, GlobalDataVault, save, physics, rendering, Burst/job, telemetry, or continuous GlobalQualityWeight routes are touched.

## Decision 87 - Validator invocation from apply tools must not treat null LASTEXITCODE as failure

Problem: The first apply implementation invoked `validate_structure.ps1` in-process and interpreted a null `$LASTEXITCODE` as nonzero. The validator printed PASS but the apply helper still failed. That would make the new UX path unusable despite a valid table/locale write.

Solution: Add `-ThrowInsteadOfExit` to `validate_structure.ps1` and generated validator output. Apply helpers call the validator with that switch and suppress PASS text so JSON output remains machine-readable. If validation throws, the apply helper restores the backup; if validation returns, the apply operation succeeds.

Rejected Alternatives: Nested `powershell` validation was rejected because the starter tools already enforce in-process script chaining for portability and static proof. Ignoring validation after write was rejected because the helper must prove the whole starter kit still passes before reporting success.

Scalability potential: Low tier CLI authors get deterministic success/failure. Middle tier Workbench receives clean process output. High/Ultra tier automation can parse JSON apply output without host text contamination.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is no-Unity offline tooling; the gain is failed-handoff prevention, not frame time.

## Decision 88 - Graph node snippets need the same bounded apply path as settings and locale

Problem: Graph node snippet generation produced a valid `Generated/graph_node_snippet.json`, but authors still had to manually insert the object into `Graphs/main.h8graph.json`. That preserved the highest-risk step: malformed JSON, duplicate node IDs, bad opcode tokens, missing graph budget, or silent destruction of future graph fields.

Solution: Add `Tools/apply_graph_node_snippet.ps1`, expose it through `h8mod.ps1 -Action apply-node-snippet`, the Workbench Apply Node Snippet button, and the SDK Hub generator. The helper accepts only starter-relative Generated snippets and the exact graph/manifest files, validates node IDs/opcodes/parameters, rejects duplicates unless `-Replace` is explicit, writes through temp files, validates the full starter kit, and restores previous files on failure.

Rejected Alternatives: Manual copy/paste was rejected because it leaves the most fragile authoring step in place. Blind append was rejected because it can corrupt unknown graph shape and duplicate authority. A runtime graph compiler/editor was rejected because current public runtime ingress remains envelope-only and this pass is offline SDK authoring.

Scalability potential: Low tier authors can create and apply graph nodes from PowerShell or pwsh without Unity. Middle tier authors use the Workbench over the same tool. High tier can layer a structured graph editor on the same file contract. Ultra tier can add graph simulation, visual diff, and package diagnostics without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is offline authoring and validation; it prevents broken graph data before runtime loader, FutureCommandEnvelope validation, HectonEventBus isolation, SignalBus, GlobalDataVault, save, physics, rendering, Burst/job, telemetry, or GlobalQualityWeight routes are touched.

## Decision 89 - Graph apply must repair the minimum envelope budget atomically

Problem: The starter graph begins empty with `MaxEnvelopesPerFrame = 0` and authoring manifest budget `0`. Applying the first node without budget repair would produce an immediately invalid graph, forcing random authors into a second manual budget edit. Repairing only one file would create graph/manifest budget drift.

Solution: When a valid graph node is applied and the graph budget is below `1`, raise graph `MaxEnvelopesPerFrame` to `1`; if manifest `Budgets.MaxEnvelopesPerFrame` is below that graph budget, raise it to match. Write graph and manifest through temp files, keep per-file backups, validate the whole starter kit after both replacements, and restore only files with real backups on failure.

Rejected Alternatives: Leaving budgets unchanged was rejected because a generated/apply workflow must not produce an invalid starter. Raising only the graph budget was rejected because manifest parity is a validator contract. Raising budgets at runtime was rejected because package authoring must be explicit and reviewable before runtime.

Scalability potential: Low tier authors get a one-command valid graph. Middle tier Workbench authors get the same result without hidden state. High/Ultra tiers can later expose budget sliders or graph capacity planning over the same manifest/graph contract.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. Runtime cost does not change; the value is preventing invalid command graph packages from reaching loader and review.

## Decision 90 - Graph node creation needs a real authoring interface, not free-text JSON friction

Problem: Schema 99 made graph-node apply safe, but graph-node creation still forced modders through a weak surface: free-text opcode, no first-class parameter editing, no disabled-node creation, and no visible replace-on-apply intent. A second problem appeared during verification: non-empty JSON passed through `powershell.exe -File` can lose quotes, turning a correct command into `{Quantity:3,Item:demo}` and failing before the author reaches validation.

Solution: Add a Workbench Graph Opcode Picker sourced from `Reference/allowed_opcodes.csv`, a Parameters JSON text area, Create Disabled Node, and Replace Existing On Apply controls. Root `h8mod.ps1` now calls `create_graph_node_snippet.ps1` explicitly instead of using array splatting. The snippet helper keeps strict JSON as the primary contract, enforces top-level object shape, canonical keys, and a 64-entry cap, and accepts a bounded flat CLI fallback for quote-stripped shell calls. Schema revision 100 and the static validator prove Workbench controls, root launcher pass-through, disabled nodes, relaxed CLI parameters, and graph apply preservation.

Rejected Alternatives: Telling modders to escape JSON correctly was rejected because random external authors will use mixed shells and copy commands from docs. A full runtime graph editor was rejected because public runtime UGC ingress remains envelope-only. Arbitrary managed DLL/Harmony/BepInEx expansion was rejected because it lacks owner authority, save-boundary, hot-lane budget, sandbox, and telemetry proof.

Scalability potential: Low tier authors can create/apply graph nodes from PowerShell or pwsh without Unity and without hand-splicing JSON. Middle tier authors use the Workbench picker and parameter controls. High tier can add a structured graph editor over the same snippet/apply contract. Ultra tier can add graph simulation, diff, package diagnostics, and visual authoring without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is Editor/offline SDK tooling only. It prevents malformed graph packages before runtime loader, FutureCommandEnvelope validation, HectonEventBus isolation, SignalBus, GlobalDataVault, save, physics, rendering, Burst/job, telemetry, or continuous GlobalQualityWeight routes are touched.

## Decision 91 - Content asset manifests need generated/apply proof before runtime rights

Problem: `Content/assets.h8manifest.json` existed as a draft contract, but a random external author still had to hand-author asset entries and manually keep file path, kind, CRC32, byte size, and `Budgets.MaxAssetBytes` coherent. That is unsafe for public modding because a single bad byte count or path can bypass review intent, break package validation, or imply loose runtime loading that the current envelope-only boundary does not grant.

Solution: Make content asset authoring follow the same Generated snippet plus bounded apply pattern as graph/settings/locale. `create_asset_entry_snippet.ps1` validates canonical ids, kind enum, `Content/Assets/` path containment, extension, CRC32, and byte length. `apply_asset_entry_snippet.ps1` verifies the referenced file, rejects duplicates unless `-Replace` is explicit, repairs `MaxAssetBytes`, writes through temp files, validates the whole starter kit, and restores content/authoring manifests on failure. Workbench and `h8mod.ps1` expose the same route; schema revision 101 and the static validator prove the files, root launcher actions, Workbench controls, local validator contracts, review manifest, and submission zip.

Rejected Alternatives: Loose runtime asset loading was rejected because there is no owner-approved bake/import route, residency budget proof, save boundary, or hot-lane telemetry. Manual JSON editing was rejected because external authors will get CRCs and budgets wrong. A Unity-only asset editor was rejected because the starter kit must work without the full HECTON-8 Unity project.

Scalability potential: Low tier authors use PowerShell/pwsh and small files under strict 4 MiB per-source review limits. Middle tier authors use the Workbench content asset panel. High tier can add preview/import diagnostics over the same manifest route. Ultra tier can add compression scoring, visual preview, package diffing, and bake simulation without granting loose runtime file authority.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is Editor/offline SDK tooling only. It prevents malformed content packages before runtime loader, approved asset ingestion, FutureCommandEnvelope validation, SignalBus, GlobalDataVault, save, rendering, Burst/job, telemetry, or continuous GlobalQualityWeight routes are touched.

## Decision 92 - Manifest capability and budget edits need a bounded authoring contract

Problem: `mod.h8manifest.json` had `Capabilities` and `Budgets`, but external authors had no safe interface for changing them. Random modders would either hand-edit JSON, invent runtime-like rights, or lower budgets below graph/content requirements and produce packages that pass intent text but fail real review. This was a UX and contract problem, not a runtime permission system.

Solution: Add `Tools/configure_manifest_contract.ps1` and expose it through `h8mod.ps1 -Action manifest-contract`, the External Starter Kit Workbench Manifest Contract panel, SDK Hub generated kits, local validation, schema revision 102, static validation, runtime playbook evidence, and public docs. The helper uses a public allowlist, caps capability count and budget values, refuses unknown capability IDs, refuses lowering envelope/asset budgets below current graph/content requirements, validates after write, and restores the previous manifest on failure. Capabilities are explicitly review metadata, not runtime rights.

Rejected Alternatives: Arbitrary capabilities were rejected because they look like authority without sandbox/loader proof. Runtime DLL/Harmony/BepInEx expansion was rejected because current public ingress remains envelope-only and lacks hot-lane, save, telemetry, trust, and device-budget proof. Manual manifest editing was rejected because it creates drift between schema, validator, Workbench, docs, and package review.

Scalability potential: Low tier authors use one PowerShell command without Unity. Middle tier authors use the Workbench dropdown and budget fields. High tier can add dependency/version visualization over the same manifest contract. Ultra tier can add package simulation, conflict analysis, and visual review dashboards without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is offline SDK UX only. It prevents invalid package metadata before runtime loader, FutureCommandEnvelope validation, HectonEventBus isolation, SignalBus, GlobalDataVault, save, physics, rendering, Burst/job, telemetry, or continuous GlobalQualityWeight routes are touched.

## Decision 93 - Submission package temp files must not live beside final Generated artifacts

Problem: `build_submission_package.ps1` wrote `Generated/<id>_submission.zip.tmp` and then renamed/moved files in the same generated folder. In this sandbox and likely on some locked/synced folders, cleanup failed with `Access to the path ... .tmp is denied`, leaving a stale temp zip and making one-command public submission fail after review and prepare had already passed.

Solution: Create the temporary zip and backup copy in the system temp directory with unique `hecton8-*` names, copy the finished zip to `Generated/<id>_submission.zip`, keep a previous-output backup until the copy succeeds, restore it on failure, and best-effort cleanup only temp artifacts. Mirror the same implementation in `ModdingSdkHubWindow` generated starter kits and update static proof markers to require this safer copy/restore route.

Rejected Alternatives: Keeping same-folder `.tmp` files was rejected because the failure was reproduced. Blind overwrite was rejected because a failed replacement can destroy the previous review handoff artifact. Deleting `Generated/` outputs wholesale was rejected because other agents or authors may have snippets there.

Scalability potential: Low tier authors get a stable one-command zip handoff in copied starter folders. Middle tier authors use the Workbench submission button without stale temp artifacts. High/Ultra tiers can add signed packages and diffed package history over the same final zip path without changing the runtime install boundary.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is no-Unity packaging only. It removes a file-system failure mode before review handoff; runtime systems and frame budget are untouched.

## Decision 94 - External modders need a VS Code task surface over the same bounded launcher

Problem: The External Starter Kit had JSON schemas and a root launcher, but a random public author still had to copy long PowerShell commands from docs for setup, validation, review, submission, snippet creation/apply, opcode discovery, capabilities, and manifest budget/capability edits. That is a usability defect and a contract risk: people will call inner `Tools/*.ps1` scripts directly, skip validation, or assume Unity is mandatory.

Solution: Add `.vscode/tasks.json` as a first-class no-Unity task surface and keep `.vscode/settings.json` as the single executable selector through `hecton8.powerShellExecutable`. Every VS Code task routes through root `h8mod.ps1` with `-Action`; no task calls inner tools directly and no task grants runtime rights. The SDK Hub generator emits the same task file from the checked-in template, the Workbench opens both VS Code settings/tasks files, the local validator checks task version, labels, inputs, and launcher-only routing, schema revision 103 records the contract, static validation proves it, and docs describe the workflow.

Rejected Alternatives: Direct VS Code tasks against `Tools/*.ps1` were rejected because they bypass the public package entry point and would create drift. Runtime DLL/Harmony/BepInEx expansion was rejected because this pass is public authoring UX, not a sandbox/authority expansion. A Unity-only interface was rejected because external authors must be able to build mods from a copied starter folder with VS Code and PowerShell/pwsh only.

Scalability potential: Low tier authors use VS Code `Tasks: Run Task` and `h8mod.ps1` without Unity. Middle tier authors use the Unity Workbench over the same package contract. High tier can add structured editors and package diff views over the task-backed files. Ultra tier can add preview simulation, conflict diagnostics, and visual review dashboards without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is offline SDK/editor tooling only. Runtime loader, FutureCommandEnvelope validation, HectonEventBus isolation, SignalBus, GlobalRegistry, GlobalDataVault, save, rendering, Burst/job, telemetry, and continuous GlobalQualityWeight routes are untouched.

## Decision 95 - VS Code tasks must expose disabled and replace authoring routes

Problem: Schema 103 gave random external authors a VS Code task surface, but it still lacked the Workbench/CLI parity for disabled graph node creation and explicit replacement of graph/settings/locale/content asset entries. That forced VS Code users back to manual command editing for common safe-edit paths and made overwrite intent less visible.

Solution: Extend `.vscode/tasks.json` with `HECTON-8: create disabled graph node snippet` plus explicit replace tasks for graph nodes, settings rows, locale entries, and content asset entries. Keep every task routed through root `h8mod.ps1`; add local validator checks for `-NodeDisabled` and `-Replace`; update schema revision 104, static validator assertions, runtime playbook, and public docs. A temp-copy probe verified disabled graph snippets, graph replace, settings replace, locale replace, content asset replace, and final validation without touching runtime authority.

Rejected Alternatives: Hidden overwrite-by-default was rejected because replacement must be an explicit author action. Direct VS Code tasks against inner `Tools/*.ps1` were rejected because they bypass the public launcher contract. Runtime DLL/Harmony/BepInEx expansion was rejected again because this work is authoring UX, while runtime public ingress remains envelope-only.

Scalability potential: Low tier authors use VS Code task labels instead of command escaping. Middle tier authors use the Unity Workbench controls over the same launcher. High tier can add structured diff/preview UI over these explicit replace routes. Ultra tier can add package simulation and conflict diagnostics without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is offline SDK/editor tooling only. Runtime loader, FutureCommandEnvelope validation, HectonEventBus isolation, SignalBus, GlobalRegistry, GlobalDataVault, save, physics, rendering, Burst/job, telemetry, and continuous GlobalQualityWeight routes are untouched.

## Decision 96 - SDK Hub starter generation must use reviewed starter templates

Problem: The versioned ExternalStarterKit had current no-Unity docs/tasks/tools, but ModdingSdkHubWindow still regenerated many files from hardcoded C# strings. Missing-file refresh from the Hub could recreate stale README/schema/tool/manifest content and diverge from the validated starter template random modders actually receive.

Solution: Add BuildStarterKitTemplateFile(relativePath, fallbackFactory) and route docs, manifests, content/graph/table/locale files, schemas, tools, and VS Code files through the checked-in ModdingSDK/ExternalStarterKit files first. Keep C# fallback factories only for missing-template recovery. Add schema 105/static validator proof for every generator path.

Rejected Alternatives: Re-syncing every hardcoded C# string was rejected because the next SDK doc/tool pass would drift again. Removing fallbacks entirely was rejected because the Hub still needs a recovery path if a file is missing locally. Expanding runtime mod rights was rejected because this is authoring UX and runtime ingress remains envelope-only.

Scalability potential: Low tier authors get a consistent copied starter with no Unity required. Middle tier authors get the same files from Workbench refresh. High tier can add structured editors over one stable template source. Ultra tier can add simulation/package diagnostics without changing runtime truth ownership.

Hardware Impact: Estimated runtime gain on i3/MX350 is 0 us/frame. This is editor/offline SDK generation only; runtime loader, FutureCommandEnvelope, HectonEventBus, SignalBus, GlobalRegistry, GlobalDataVault, save, rendering, Burst/jobs, telemetry, and GlobalQualityWeight are untouched.

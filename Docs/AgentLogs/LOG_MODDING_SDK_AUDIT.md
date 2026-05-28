# LOG_MODDING_SDK_AUDIT

Top = old, bottom = new.

## 2026-05-26 MODDING_SDK_AUDIT

What was wrong:
- `ModBuilderWindow.ModManifestData` emitted only 7 manifest fields. `ModLoader.ModManifest` requires 9 fields and disables packages with `RequiredAPIVersion <= 0`. SDK-built packages were structurally invalid unless manually repaired.
- `Validate_Mod_API_Static.ps1` failed on valid source because `ModAupResponse` layout size was expressed as `ModSpatialContractLayout.AupResponseStrideBytes`, while the validator accepted only numeric literal sizes.
- The signal inventory gate read `Assets/_Project/Scripts/Core/GlobalSignals.cs`, now a compatibility shell. Real payloads live in `Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads*.cs`; schema/audit counts were stale.

What was done:
- Updated `Assets/_Project/Scripts/Editor/ModdingSDK/ModBuilderWindow.cs` to emit `RequiredAPIVersion` and `ModPriority`, validate API version against loader API `2`, and warn that managed DLL entries are legacy/internal under envelope-only runtime.
- Extended `Docs/Modding/Validate_Mod_API_Static.ps1` to prove SDK builder manifest parity against `ModLoader.ModManifest`, resolve constant-based `ModAupResponse` layout size, and read `GlobalSignalPayloads*.cs` with a stricter `ISignal` regex.
- Updated `Signal_Schema.json`, `Signal_Audit_Matrix.md`, `README.md`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Loader_Save_Audit_Matrix.md` to schema revision `17`, `173` source signals, `2` projected signals, and `171` denied-by-default signals.

Cinematic cheats used:
- None. This pass touched editor packaging contracts and static verification only. No physical simulation, water, light, deformation, or presentation fake was added.

Exact microseconds saved:
- Runtime frame: 0 us/frame. No runtime code path changed.
- Cold package/load path: not measured; no microsecond saving claimed. The real gain is fail-closed SDK output and restored static proof.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1`
- PASS: scoped `git diff --check` for touched source/docs.
- DEFERRED: Unity/dotnet compile was not launched because CPU sampled at 77.7 percent; project rule forbids dotnet build above 50 percent CPU. No `dotnet`/`csc` process was active at sample time.

## MODDING_SDK_AUDIT - 2026-05-26 - pass 2

What was wrong:
- `SurvivalOverride`, `HapticPulse`, and `SubtitleCue` were documented as reserved/not-public kernels, but `allowed_opcodes.csv` listed them and `GenerateEmergencyOpcodeMap()` inserted them into runtime opcode records.
- The static validator treated every `FutureCommandOpcodes` constant as public API. That made future hash reservations indistinguishable from active runtime ingress.
- Public `HectonAPI` exposed `ItemData`, `RecipeData`, and `BuildableData` in public signatures. Those are `ScriptableObject` handles, contradicting `directUnityObjectReferencesForMods=false`.

What was done:
- Removed reserved kernel activation from `FutureCommandSandboxValidator.GenerateEmergencyOpcodeMap()`.
- Added `IsRuntimeAllowedFutureCommandOpcode()` and made editor CSV ingest reject hashes outside the explicit runtime allowlist.
- Removed reserved kernel hashes from `Docs/Modding/allowed_opcodes.csv`.
- Updated `Validate_Mod_API_Static.ps1` so `allowed_opcodes.csv` must match `GenerateEmergencyMockOpcodes()` and must not contain reserved hashes.
- Converted `HectonAPI.Items.RegisterCustomItem`, `TryFindItem`, `HectonAPI.Crafting.RegisterRecipe`, `HectonAPI.Construction.RegisterBuildable`, and `TryFindBuildable` to internal forbidden guards that throw `IllegalContractException`.
- Updated schema/docs to revision `19`: public API methods `30`, internal forbidden methods `14`, public content methods `9`, future allowed opcodes `9`, kernel tuning profiles `3`.

Cinematic cheats used:
- None. This was API authority cleanup and static validation. No simulation or presentation fake was added.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Reserved kernel spam now fails before reserved kernel routing; no profiler measurement was run, so no numeric savings are claimed.
- Public Unity-object retention risk removed from the mod facade; memory lifetime gain is qualitative until runtime/player proof exists.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1`
- PASS: scoped `git diff --check` for touched source/docs.
- PASS: trailing whitespace scan for touched source/docs.
- DEFERRED: Unity/dotnet compile was not launched because CPU sampled at 97.7 percent on the final check; project rule forbids dotnet build above 50 percent CPU. No `dotnet`/`csc` process was active at sample time.

## MODDING_SDK_AUDIT - 2026-05-26 - pass 3

What was wrong:
- `HectonAPI.Crafting.RegisterRecycleYield`, `HectonAPI.Recycling.ProcessRecycle`, and `HectonAPI.Ecosystem.RegisterBiomeMutation` were public direct owner mutation/overlay routes without mod ownership, unload revocation, or runtime proof.
- `Input.GetButtonMask`, UI notification/settings, and `World.TryGetPlayerEntityHash` could be called without active `ModExecutionScope`, creating anonymous reads/writes/settings.
- `HectonEventBus` was public, and public `HectonAPI.Events` methods could pass nullable or mismatched `subscriberId` values into bus subscriptions if managed callbacks are reopened.

What was done:
- Converted `RegisterRecycleYield`, `ProcessRecycle`, and `RegisterBiomeMutation` to internal forbidden guards that throw `IllegalContractException`.
- Added active-scope helpers in `HectonAPI` and guarded Input, UI, World, and public event subscribe/publish/unsubscribe paths.
- Made `HectonEventBus` internal first-party infrastructure. Public event access is now only `HectonAPI.Events`.
- Extended `Validate_Mod_API_Static.ps1` to fail if `HectonEventBus` becomes public or if event/Input/UI/World active-scope guards are removed.
- Updated schema/docs to revision `22`, including event route ownership, subscriber scope rules, public API methods `27`, internal forbidden methods `17`, public content methods `6`.

Cinematic cheats used:
- None. This pass changed API authority and managed-boundary validation only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed in current envelope-only mode.
- Future managed event reopen path: one cold subscription-time active-scope branch/string compare; no profiler saving claimed.
- Risk removed: anonymous event subscribers and ownerless facade calls no longer survive as a latent route.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `22`.
- PASS: scoped `git diff --check` for touched source/docs.
- PASS: trailing whitespace scan for touched source/docs.
- DEFERRED: Unity/dotnet compile was not launched because CPU sampled at 100 percent; project rule forbids dotnet build above 50 percent CPU. No `dotnet`/`csc` process was active at sample time.

## MODDING_SDK_AUDIT - 2026-05-26 - pass 4

What was wrong:
- `HectonAPI.Mods.GetLoadedMods` was public and returned all-package `ModRuntimeInfo`, including loader path/status diagnostics.
- `FutureCommandSandboxValidator` was public and exposed engine control-plane methods: raw stream ingress, external queue drain, tuning, thermal pressure, approved asset registration, opcode gates, telemetry snapshot, CSV reload, self-audit, and blackbox dump.
- `HectonModHooks` and `IModCommandKernel` were public dormant symbols, implying direct lifecycle event publication and managed command kernel extension points.
- `HectonAPI.Commands.RequestFuture` accepted envelopes without active mod scope or `ModderSignature` ownership check.

What was done:
- Made `HectonAPI.Mods.GetLoadedMods` internal diagnostics only.
- Made `FutureCommandSandboxValidator` internal and sealed related control-plane structs: tuning, opcode records, modder counters/leases, approved asset records, ring/telemetry records, mock queue, and malicious injection job.
- Made `HectonModHooks` and `IModCommandKernel` internal first-party infrastructure.
- Added `RequestFuture` active-scope and signature match guard.
- Extended `Validate_Mod_API_Static.ps1` to fail on public diagnostics/control-plane/hook/kernel regressions and missing `RequestFuture` ownership guard.
- Updated modding docs/schema to revision `26`.

Cinematic cheats used:
- None. This pass was API authority/control-plane cleanup. No simulation, water, lighting, deformation, or visual fake changed.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Public managed `RequestFuture` adds one scope check and one uint compare; estimated low microseconds per managed call, not a Burst/hot simulation path.
- Removed risk: mods cannot mutate sandbox budgets, enqueue through raw/native bypasses, forge envelope ownership, or publish first-party lifecycle events through public symbols.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `26`.
- PASS: scoped `git diff --check` for touched source/docs.
- PASS: trailing whitespace scan for touched source/docs.
- DEFERRED: Unity/dotnet compile was not launched because CPU sampled at 96.9 percent; project rule forbids dotnet build above 50 percent CPU. No `dotnet`/`csc` process was active at sample time.

## MODDING_SDK_AUDIT - 2026-05-26 - pass 5

What was wrong:
- `HectonAPI.Resources`, `Telemetry`, `Localization`, and `SaveState` did not all prove active `ModExecutionScope` at the public facade boundary. Direct `HectonAPI.Resources.Proxy` calls could rely on lower-level behavior instead of owning the check at the route edge.
- `ModRuntimeInfo` and `ModLoadStatus` remained public after the loader diagnostics facade was made internal. The descriptor contains package root and AssetBundle paths, so the type itself was still a false SDK contract.
- FutureCommand output SignalBus DTOs remained public while the validator/control-plane was internal. That implied direct lane payload access instead of the single `FutureCommandEnvelope` ingress.

What was done:
- Added `ThrowIfNoActiveMod` guards to public resource resolution, telemetry publish, localization injection, and save-state methods.
- Added direct active-scope checks inside `ModResourceProxy` before envelope-only fallback.
- Made `ModRuntimeInfo` and `ModLoadStatus` internal, and changed `ModMenuModEntryView.Bind(ModRuntimeInfo)` to internal.
- Made FutureCommand output signal DTOs internal: spawn request, asset reference, acoustic, damage, dev-null, survival override, haptic pulse, and subtitle cue.
- Extended `Validate_Mod_API_Static.ps1` to fail on missing active-scope guards, public loader diagnostics DTOs, public FutureCommand output DTOs, and direct proxy guard order drift.
- Updated modding docs/schema to revision `29`.

Cinematic cheats used:
- None. This pass was SDK authority/surface cleanup. No physical simulation or visual fake changed.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed in current envelope-only mode.
- Added cost: low microseconds per cold managed SDK call for active-scope branch checks.
- Removed risk: no anonymous resource/save/telemetry/localization route, no public package-path DTO contract, no public FutureCommand output lane DTOs.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `29`.
- PASS: scoped `git diff --check` for touched source/docs.
- PASS: trailing whitespace scan for touched source/docs.
- PASS: static public-leak scan for `ModRuntimeInfo`, `ModLoadStatus`, and FutureCommand output DTO declarations. The remaining `public ModLoadStatus Status` text is inside the internal `ModRuntimeInfo` struct.
- DEFERRED: Unity/dotnet compile was not launched because CPU sampled at 83.0 percent; project rule forbids dotnet build above 50 percent CPU. No `dotnet`/`csc` process was active at sample time.

## MODDING_SDK_AUDIT - 2026-05-26 - pass 6

What was wrong:
- `ModRegistryEventType`, `ModRegistryEventPayload`, and `IModRegistryEventListener` were public even though they are engine registry invalidation infrastructure.
- `ModSettingKind` and `ModSettingView` were public even though they are menu snapshot DTOs built from facade-registered settings.
- `ModMenuSettingToggleView.Bind(ModSettingView)`, `ModMenuSettingSliderView.Bind(ModSettingView)`, `ModMenuUIController.OnModRegistryEvent`, and `Fabricator.OnModRegistryEvent` exposed those internal DTOs through public members.

What was done:
- Made registry event and menu setting snapshot types internal.
- Converted `ModMenuUIController` and `Fabricator` listener methods to explicit `IModRegistryEventListener` implementations.
- Changed setting view bind methods to internal.
- Extended `Validate_Mod_API_Static.ps1` to fail if these DTO/listener routes become public again.
- Updated modding docs/schema to revision `30`.

Cinematic cheats used:
- None. This pass changed SDK surface authority and first-party UI route isolation only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: 0 us/frame; visibility and explicit interface changes do not add frame-path work.
- Removed risk: mods cannot treat engine registry invalidation or menu snapshot DTOs as a supported SDK route.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `30`.
- PASS: scoped `git diff --check` for touched source/docs.
- PASS: touched-file trailing whitespace scan. A wider directory scan hit pre-existing whitespace in `Assets/_Project/Scripts/ModdingAPI/Editor.meta`; that file was not touched.
- PASS: public-leak scan for `ModRegistryEvent*`, `IModRegistryEventListener`, `ModSettingKind`, `ModSettingView`, public `Bind(ModSettingView)`, and public `OnModRegistryEvent`.
- DEFERRED: Unity/dotnet compile was not launched because CPU sampled at 81 percent; project rule forbids dotnet build above 50 percent CPU. No `dotnet`/`csc` process was active at sample time.

## MODDING_SDK_AUDIT - 2026-05-26 - pass 7

What was wrong:
- `HectonAPI.Resources.Proxy` was a public property that returned the proxy object without first proving an active mod execution scope.
- `HectonAPI.World.IsGameReady` was a public property that read bootstrap readiness without active mod attribution.
- Static gates covered method guards and direct proxy method guards, but did not prove public property routes were guarded.

What was done:
- Routed `Resources.Proxy` through `GetProxy()` and `World.IsGameReady` through `GetIsGameReady()`.
- Added `ThrowIfNoActiveMod("Resources.Proxy")` and `ThrowIfNoActiveMod("World.IsGameReady")`.
- Extended `Validate_Mod_API_Static.ps1` to fail if either property bypasses the guarded accessor.
- Updated modding docs/schema to revision `31`.

Cinematic cheats used:
- None. This pass changed cold SDK facade ownership only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: low microseconds per managed property call on the cold mod facade.
- Removed risk: no anonymous resource proxy handle acquisition and no anonymous world readiness read through the public SDK facade.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `31`.
- PASS: scoped `git diff --check` for touched source/docs.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: Unity/dotnet compile was not launched because CPU sampled at 59 percent; project rule forbids dotnet build above 50 percent CPU. No `dotnet`/`csc` process was active at sample time.

## MODDING_SDK_AUDIT - 2026-05-26 - pass 8

What was wrong:
- `HectonAPI.Events.Unsubscribe(subscription)` validated active mod ownership, but direct public `HectonEventSubscription.Dispose()` called channel `Unsubscribe` without owner proof.
- Docs and samples already use direct token disposal from `IHectonMod.OnUnload`, so `Dispose` was a public lifetime route and could not rely on facade-only validation.
- Static gates proved that `Dispose` existed, but did not prove owner-scope validation or constructor call parity.

What was done:
- Added an internal owner-scope requirement bit to `HectonEventSubscription`.
- Direct `Dispose` now validates active mod ownership and ordinal subscriber-id match before channel unsubscribe for mod-owned tokens.
- Internal/first-party tokens created outside active mod scope remain disposable without a mod owner requirement.
- Updated all 4 token creation sites to pass `ModExecutionScope.HasActiveMod`.
- Extended `Validate_Mod_API_Static.ps1` to prove constructor shape, stored owner-scope bit, direct `Dispose` guard, active scope check, and constructor-call parity.
- Updated modding docs/schema to revision `32`.

Cinematic cheats used:
- None. This pass changed cold SDK lifetime ownership only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: low microseconds per mod-owned subscription disposal; one branch and one ordinal string compare on a cold teardown path.
- Removed risk: no direct public subscription token can unsubscribe another mod's handler outside the owning active execution scope.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `32`.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- PASS: static scan found constructor owner-scope parameter, `ThrowIfOwnerScopeMismatch()` in direct `Dispose`, and all 4 constructor call sites passing `ModExecutionScope.HasActiveMod`.
- DEFERRED: Unity/dotnet compile was not launched because final pre-compile CPU sampling included 85.13 and 53.18 percent; project rule forbids compile launch above 50 percent CPU. No `Unity`, `dotnet`, or `csc` process was active.

## MODDING_SDK_AUDIT - 2026-05-26 - pass 9

What was wrong:
- `HectonAPI.World.SpawnPersistentPrefab` and `DespawnPersistentInstance` were internal forbidden methods, but the concrete backing service `ModWorldPersistenceManager` was public.
- `GlobalRegistry.ModWorldPersistence`, `RegisterModWorldPersistenceRuntime`, and `UnregisterModWorldPersistenceRuntime` exposed that concrete engine save/spawn service publicly.
- This created an accidental SDK/control-plane route around the facade quarantine.

What was done:
- Made `ModWorldPersistenceManager` internal.
- Made `GlobalRegistry.ModWorldPersistence`, `RegisterModWorldPersistenceRuntime`, and `UnregisterModWorldPersistenceRuntime` internal.
- Preserved same-assembly bootstrap, loader, and save-owner access.
- Extended `Validate_Mod_API_Static.ps1` to fail if the concrete service or registry route becomes public again.
- Updated modding docs/schema to revision `33`.

Cinematic cheats used:
- None. This pass changed cold engine service visibility and SDK route ownership only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: 0 us/frame; visibility changes do not add runtime work.
- Removed risk: runtime mods cannot treat engine persistent spawn/save service or its GlobalRegistry route as a supported SDK surface.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `33`.
- PASS: static leak scan found no public `ModWorldPersistenceManager`, `GlobalRegistry.ModWorldPersistence`, `RegisterModWorldPersistenceRuntime`, or `UnregisterModWorldPersistenceRuntime` route.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- BLOCKED: Unity batchmode compile was attempted after CPU/dotnet/csc gate allowed it, but failed in pre-existing core contract dependencies: `Assets/_Project/Scripts/Core/Contracts/PhysicsImpactContracts.cs` and `Assets/_Project/Scripts/Core/Contracts/Physics/HabitatFluidIncursionContracts.cs` cannot resolve `Hecton8.Core.Memory`, `BinaryBlittableSafe`, `AbsoluteUniversePosition`, and `AbsoluteUniversePositionBlit`. Log: `Logs/MODDING_SDK_AUDIT_UnityCompile.log`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 10

What was wrong:
- Public mod event payloads `ModPlayerSpawnedEvent` and `ModBiomeChangedEvent` used default sequential layout with no fixed size. `ModBiomeChangedEvent` had no explicit padding and would not satisfy the 8-byte-aligned public DTO rule.
- Payload docs/schema still claimed several mod spatial/result payloads were `Sequential` or `source-defined`, while source already used explicit fixed-size layouts.
- The static validator only checked `ModEventDto`, `ModCommand`, and `ModAupResponse`; it did not prove the rest of the public mod-facing payload layout contract.

What was done:
- Made `ModPlayerSpawnedEvent` explicit 24 bytes with offsets `PlayerId@0`, `AbsoluteUniversePosition@8`, `BiomeId@20`.
- Made `ModBiomeChangedEvent` explicit 24 bytes with offsets `PreviousBiomeId@0`, `CurrentBiomeId@4`, `AbsoluteUniversePosition@8`, `_pad0@20`.
- Updated `Signal_Schema.json` to schema revision `34`.
- Updated `Payload_Layout_Audit_Matrix.md`, `Mod_API_Specification.md`, `Event_Subscription_Audit_Matrix.md`, `Runtime_Verification_Playbook.md`, and `README.md`.
- Extended `Validate_Mod_API_Static.ps1` to prove explicit source sizes, event field offsets, schema `payloadLayouts`, schema snapshot size fields, and payload audit entries.

Cinematic cheats used:
- None. This pass changed ABI/layout contract proof only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: 0 us/frame; attributes and padding do not add frame work.
- Removed risk: mod callback/result payloads no longer depend on implicit compiler/platform sequential layout or stale documentation.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `34`; event/spatial payload sizes `24, 24, 120, 64, 80, 48, 16, 24`.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- PASS: stale layout text scan found no `Sequential`, `source-defined`, or `sizeBytes: null` claims in the mod payload docs/schema set.
- DEFERRED: Unity/dotnet compile was not launched after schema `34` because CPU samples included 81.18 percent and 89.18 percent. No active `Unity`, `dotnet`, or `csc` process was found.
- PREVIOUS BLOCKER STILL EXISTS: Last Unity compile attempt failed in unrelated core contract dependencies: `Assets/_Project/Scripts/Core/Contracts/PhysicsImpactContracts.cs` and `Assets/_Project/Scripts/Core/Contracts/Physics/HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 11

What was wrong:
- `HectonAPI.Events.Publish<TPayload>` had an active mod scope check, but no payload ownership check.
- The docs said `Publish<TPayload>` is for mod-owned unmanaged coordination only, while source allowed engine-owned DTO types to be published if managed events are reopened.
- A mod could impersonate engine-owned lifecycle/projection/result/command payload lanes such as `ModEventDto`, `ModPlayerSpawnedEvent`, `ModBiomeChangedEvent`, `ModAupResponse`, `ModInteractionRejectedPayload`, or `FutureCommandEnvelope`.

What was done:
- Added `ThrowIfEngineOwnedPublishPayload<TPayload>` in `HectonAPI.cs`.
- `Events.Publish<TPayload>` now rejects 11 engine-owned payload types before `HectonEventBus.Publish`.
- Updated `Signal_Schema.json` to schema revision `35` with `publishEngineOwnedPayloadsForbidden=true` and forbidden payload count `11`.
- Extended `Validate_Mod_API_Static.ps1` to prove the source helper, the publish call, all forbidden payload type checks, schema entries, static snapshot entries, and event audit wording.
- Updated `README.md`, `Mod_API_Specification.md`, `API_Surface_Audit_Matrix.md`, `Event_Subscription_Audit_Matrix.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This pass changed cold managed SDK boundary ownership only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: 0 us/frame in current envelope-only mode because `ThrowIfEnvelopeOnly()` rejects before the payload guard.
- Future managed-event mode: legal mod-owned publish pays low microseconds for type comparisons on a cold managed API path; illegal publish exits before bus dispatch and callback fanout.
- Removed risk: engine command/result/projection/lifecycle DTOs cannot be published by mods through the public event facade.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `35`, `PublishRejectsEngineOwnedPayloads=True`, `EngineOwnedPublishForbiddenPayloadCount=11`.
- PASS: `Signal_Schema.json` parsed with `schemaRevision=35`, `publishEngineOwnedPayloadsForbidden=True`, and `engineOwnedPublishForbiddenPayloadCount=11`.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- PASS: stale schema-34 closure scan found no old revision claims in modding docs/schema.
- DEFERRED: Unity/dotnet compile was not launched because CPU samples were 20.26, 22.98, and 91.33 percent. No active `Unity`, `dotnet`, or `csc` process was found.
- PREVIOUS BLOCKER STILL EXISTS: Last Unity compile attempt failed in unrelated core contract dependencies: `Assets/_Project/Scripts/Core/Contracts/PhysicsImpactContracts.cs` and `Assets/_Project/Scripts/Core/Contracts/Physics/HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 12

What was wrong:
- `HectonAPI.Commands.Request`, `RequestAup`, and `RequestRenderInstance` were public legacy command facades.
- They returned quarantine `false` in envelope-only mode but did not require active `ModExecutionScope`.
- That left anonymous public write-surface probes in the SDK boundary, even though `RequestFuture` and other public facades were already owner-scoped.

What was done:
- Added `ThrowIfNoActiveMod("Commands.Request")`, `ThrowIfNoActiveMod("Commands.RequestAup")`, and `ThrowIfNoActiveMod("Commands.RequestRenderInstance")`.
- Kept the obsolete signatures and quarantine `false` result for active-owner legacy calls.
- Updated `Signal_Schema.json` to schema revision `36` with `legacyCommandFacadesRequireActiveScope=true`.
- Extended `Validate_Mod_API_Static.ps1` to require the three guards and schema snapshot flag.
- Updated `README.md`, `Mod_API_Specification.md`, `API_Surface_Audit_Matrix.md`, `Command_Audit_Matrix.md`, `Mod_API_Sandbox_Quarantine.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This pass changed cold command facade ownership only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: low microseconds only when a legacy managed command facade is called.
- Removed risk: external code cannot anonymously probe legacy command availability through public SDK methods.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `36`, `LegacyCommandFacadesRequireActiveScope=True`.
- PASS: `Signal_Schema.json` parsed with `schemaRevision=36`, `commandApi.legacyCommandFacadesRequireActiveScope=True`, and snapshot flag `True`.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- PASS: stale schema-35/R36 closure scan found no old revision claims in modding docs.
- BLOCKED: Unity batchmode compile was launched after CPU/process gate allowed it. Compile still fails in unrelated Core contract dependencies: `PhysicsImpactContracts.cs` unresolved `Hecton8.Core.Memory`, `BinaryBlittableSafe`, `AbsoluteUniversePosition`; `HabitatFluidIncursionContracts.cs` unresolved `AbsoluteUniversePositionBlit`. No modding-file errors were reported before this wall. Log: `Logs/MODDING_SDK_AUDIT_UnityCompile.log`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 13

What was wrong:
- Public `HectonAPI.Events` subscribe/publish facades checked envelope-only quarantine before active owner proof.
- Anonymous callers could probe event surface availability and receive quarantine status without proving `ModExecutionScope`.
- This violated the same active-scope-first rule already applied to command facades and left a dormant managed-event reopening trap.

What was done:
- Reordered public `Events.Subscribe<TPayload>`, `SubscribeNative`, `SubscribeProjected`, `OnPlayerSpawned`, and `OnBiomeChanged` so `RequireSubscriberScope(...)` runs before `ThrowIfEnvelopeOnly()`.
- Reordered public `Events.Publish<TPayload>` so `ThrowIfNoActiveMod("Events.Publish")` runs before `ThrowIfEnvelopeOnly()`.
- Kept internal first-party typed event routes unchanged; they are not public SDK facades.
- Updated `Signal_Schema.json` to schema revision `37` with `publicEventFacadesRequireScopeBeforeEnvelopeOnly=true`.
- Extended `Validate_Mod_API_Static.ps1` to prove ordering for public event subscribe/publish facades and schema snapshot drift.
- Updated `README.md`, `Mod_API_Specification.md`, `API_Surface_Audit_Matrix.md`, `Event_Subscription_Audit_Matrix.md`, `Runtime_Verification_Playbook.md`, and `Mod_API_Sandbox_Quarantine.md`.

Cinematic cheats used:
- None. This pass changed cold SDK boundary ownership/order only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: low microseconds only when cold managed SDK event facades are called.
- Removed risk: anonymous code cannot learn event quarantine state before active mod ownership is proven.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `37`, `PublicEventFacadesRequireScopeBeforeEnvelopeOnly=True`.
- PASS: `Signal_Schema.json` parsed with `schemaRevision=37`, event audit flag `True`, and snapshot flag `True`.
- PASS: public event facade ordering source scan showed `RequireSubscriberScope` or `ThrowIfNoActiveMod` before `ThrowIfEnvelopeOnly` for every public subscribe/publish route.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- PASS: stale schema-36/R37/event wording scan found no old revision claims in modding docs.
- DEFERRED: Unity/dotnet compile was not launched because CPU gate failed: 63, 64, 99, and 88 percent samples. No active `Unity`, `dotnet`, or `csc` process was found. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 14

What was wrong:
- `FutureCommandSandboxConstants` was public while it contained sandbox pending/staging capacities, tracked modder limits, asset caps, telemetry capacity, command budget floors, fault hashes, kernel profile caps, and fallback flags.
- `FutureCommandSandboxValidator` was already internal, but the public constants still implied stable SDK authority over internal runtime budgets and tuning.
- Modders only need one public binary fact here: the 64-byte envelope size.

What was done:
- Made `FutureCommandSandboxConstants` internal.
- Added public `FutureCommandEnvelope.SizeBytes` so SDK/source authors still have the fixed 64-byte packet size without seeing sandbox control-plane constants.
- Updated `Signal_Schema.json` to schema revision `38` with `futureCommandSandboxConstantsPublic=false` and `futureCommandEnvelopeExposesSizeBytes=true`.
- Extended `Validate_Mod_API_Static.ps1` to reject public `FutureCommandSandboxConstants` and require `FutureCommandEnvelope.SizeBytes`.
- Updated `README.md`, `Mod_API_Specification.md`, `API_Surface_Audit_Matrix.md`, `Command_Audit_Matrix.md`, `Runtime_Verification_Playbook.md`, and `Mod_API_Sandbox_Quarantine.md`.

Cinematic cheats used:
- None. This pass changed cold SDK/source contract exposure only.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: 0 us/frame; constants and visibility do not change runtime execution.
- Removed risk: public mods cannot couple to internal quality, thermal, budget, capacity, fault-hash, or kernel-tuning constants.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `38`, `FutureCommandSandboxConstantsPublic=False`, `FutureCommandEnvelopeExposesSizeBytes=True`.
- PASS: `Signal_Schema.json` parsed with schema and snapshot flags for internal constants and public envelope size.
- PASS: stale schema-37/R38/public-constants scan found no old revision or public constants claims in modding docs/source.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: Unity/dotnet compile was not launched because CPU gate failed at 99 percent. No active `Unity`, `dotnet`, or `csc` process was found. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 15

What was wrong:
- `IModRegistryEventListener` was internal, but public `Fabricator` and `ModMenuUIController` still declared it in their base lists.
- That risks C# inconsistent-accessibility compile failure and exposes an engine-only invalidation route through public component signatures.

What was done:
- Removed `IModRegistryEventListener` from public `Fabricator` and `ModMenuUIController` base lists.
- Added private `ModRegistryEventAdapter` bridges that register with `ModRegistryEvents` and forward to owner-private handlers.
- Updated `Signal_Schema.json` to schema revision `39` with `modRegistryListenersUsePrivateAdapters=true`.
- Extended `Validate_Mod_API_Static.ps1` to reject public listener base-list exposure, reject `Register(this)`/`Unregister(this)`, and require private adapters.
- Updated `README.md`, `Mod_API_Specification.md`, `API_Surface_Audit_Matrix.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This pass preserved the existing coalesced internal NativeQueue invalidation lane instead of creating public event traffic or UI polling.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: one cold adapter allocation per component instance.
- Removed risk: public components no longer leak internal registry listener types or depend on public/internal accessibility mismatch.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `39`, `ModRegistryListenersUsePrivateAdapters=True`.
- PASS: `Signal_Schema.json` parsed with `schemaRevision=39` and adapter snapshot flag `True`.
- PASS: public-base leak scan found no public `Fabricator` or `ModMenuUIController` base-list exposure of `IModRegistryEventListener`, and no `ModRegistryEvents.Register(this)` / `Unregister(this)` route.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- PASS: stale schema-38/R39 listener scan found no old revision or public-listener claims in modding docs/source.
- DEFERRED: Unity/dotnet compile was not launched because CPU gate failed at 100 percent. No active `Unity`, `dotnet`, or `csc` process was found. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 16

What was wrong:
- `AssemblyInfo.cs` grants internals to first-party friend assemblies, including `Hecton8.Plugins`.
- The loader and SDK builder accepted managed DLLs with arbitrary file names and metadata identities.
- Current public runtime UGC is envelope-only, but a future managed-mod reopening could let a Mods-root DLL claim a reserved engine assembly identity and spoof a friend/internal route.

What was done:
- Added reserved managed assembly identity guards to `ModLoader`.
- Loader now disables packages whose manifest entry name, resolved DLL filename, or `AssemblyName.GetAssemblyName()` metadata identity is reserved.
- Loader managed factory registration rejects reserved assembly factories loaded from the Mods root, with fail-closed path handling for reserved factories.
- Added the same reserved identity validation to `ModBuilderWindow` before DLL copy/package generation.
- Updated `Signal_Schema.json` to schema revision `40` with `managedAssemblyIdentityReservedNamesBlocked=true`.
- Extended `Validate_Mod_API_Static.ps1` to prove loader, SDK builder, schema, audit matrix, and change-control checklist coverage.
- Updated `README.md`, `Mod_API_Specification.md`, `Loader_Save_Audit_Matrix.md`, `Runtime_Verification_Playbook.md`, `Change_Control_Checklist.md`, `Mod_API_Sandbox_Quarantine.md`, and `SDK_Authoring_Interface_Plan.md`.

Cinematic cheats used:
- None. This is cold package/SDK validation, not simulation or presentation.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: cold package discovery/editor validation only, from file-name checks and assembly metadata read.
- Removed risk: external packages cannot claim `Hecton8.*`, `Unity*`, `Assembly-CSharp`, `System`, `mscorlib`, or `netstandard` identities to imply first-party/internal authority.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `40`, `ManagedAssemblyIdentityReservedNamesBlocked=True`.
- PASS: `Signal_Schema.json` parsed with `schemaRevision=40`, loader flag `True`, and last static validation snapshot flag `True`.
- PASS: stale schema-39/R40 scan found no old revision or stale closure text in modding docs/source.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: Unity/dotnet compile was not launched because CPU samples were 100, 99.06, and 59.44 percent, and two active `dotnet.exe` processes were present. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 17

What was wrong:
- `SubscribeNative` exposes callback-scoped byte payloads for Interaction and Crafting lanes.
- Schema/docs recorded the native event kinds but did not prove source payload layouts, sizes, or field offsets.
- That left a future managed-event reopening with an undocumented byte ABI despite the public SDK callback route.

What was done:
- Updated `Signal_Schema.json` to schema revision `41`.
- Added schema payload layouts for `InteractionEventPayload` (`32` bytes) and `CraftingEventPayload` (`64` bytes), including source files and field offsets.
- Extended `eventSubscriptionAudit`, `payloadLayoutAudit`, and `lastStaticValidationSnapshot` with `nativeBytePayloadLayoutsChecked=true`.
- Extended `Validate_Mod_API_Static.ps1` to read `InteractionEvents.cs` and `CraftingEvents.cs`, prove explicit layouts, sizes, offsets, schema entries, audit docs, and native byte event metadata.
- Updated `README.md`, `Payload_Layout_Audit_Matrix.md`, `Event_Subscription_Audit_Matrix.md`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Change_Control_Checklist.md`.

Cinematic cheats used:
- None. This pass added static ABI proof only; it did not add simulation, presentation, polling, queue traffic, or event dispatch.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: 0 us/frame; static validator/doc/schema work only.
- Removed risk: mods cannot decode `SubscribeNative` bytes against undocumented or drifting source layouts if managed callbacks are reopened.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `41`, `NativeBytePayloadLayoutsChecked=True`, `NativeInteractionEventPayloadSizeBytes=32`, `NativeCraftingEventPayloadSizeBytes=64`.
- PASS: `Signal_Schema.json` parsed with `schemaRevision=41`, native byte layout snapshot flag `True`, and payload sizes `32/64`.
- PASS: stale schema-40/R41 scan found no old revision or stale closure text in modding docs/source.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: Unity/dotnet compile was not launched because CPU samples were 82.56, 60.19, and 33.08 percent. No active `Unity`, `dotnet`, or `csc` process was found. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 18

What was wrong:
- Loader reserved-identity validation covered manifest `EntryAssembly` and the resolved primary DLL path, but not every top-level DLL beside `mod.json`.
- In envelope-only mode that package still could not execute, but the documented reserved-DLL identity proof was false for loose/stale support DLLs.
- `ModBuilderWindow` removed only a stale previous primary `EntryAssembly`; support DLLs could remain in `Mods/[ModId]` after a later manifest-only rebuild.

What was done:
- Added `ResolveManagedAssemblyIdentityScanPaths` to `ModLoader`.
- Loader now scans every top-level package DLL for reserved file name and `AssemblyName` metadata identity before candidate activation.
- Any top-level DLL now marks the package as a managed-entry candidate while keeping runtime `EntryAssemblyPath` empty in envelope-only mode.
- Added `RemoveStaleAssemblies` to `ModBuilderWindow` so each build deletes top-level output DLLs not selected in the current DLL list.
- Updated `Signal_Schema.json` to schema revision `42` with `managedAssemblyIdentityScansAllPackageDlls=true`.
- Extended `Validate_Mod_API_Static.ps1` to prove loader scan, builder cleanup, schema snapshot, audit docs, spec, and runtime playbook coverage.
- Updated `README.md`, `Mod_API_Specification.md`, `Loader_Save_Audit_Matrix.md`, `Mod_API_Sandbox_Quarantine.md`, `Runtime_Verification_Playbook.md`, `SDK_Authoring_Interface_Plan.md`, `SDK_Product_Blueprint.md`, and `Change_Control_Checklist.md`.

Cinematic cheats used:
- None. This is cold package/SDK validation, not physical simulation or presentation. The chosen cheat is architectural: reject stale/unsafe DLL package shapes before runtime instead of adding any frame-time policing path.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: cold package discovery/editor validation only: top-level `Directory.GetFiles` plus assembly metadata identity read.
- Removed risk: stale/support DLLs cannot carry reserved `Hecton8.*`, `Unity*`, `Assembly-CSharp`, `System`, `mscorlib`, or `netstandard` identities through the package boundary without the validator seeing them.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `42`, `ManagedAssemblyIdentityReservedNamesBlocked=True`, `ManagedAssemblyIdentityScansAllPackageDlls=True`.
- PASS: `Signal_Schema.json` parsed with `schemaRevision=42`, loader scan flag `True`, and last static validation snapshot flag `True`.
- PASS: stale schema-41 scan found no old revision or stale closure text in modding docs/source.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- PASS: stale removed-code scan found no `ReadExistingManifest`, `previousManifest`, or obsolete contiguous identity-check route.
- DEFERRED: Unity/dotnet compile was not launched because CPU samples were 100, 100, and 62 percent, with active `csc.exe` and `dotnet.exe` processes; a later retry sampled 100, 100, and 100 percent CPU with 9 active `dotnet.exe` processes. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 19

What was wrong:
- Builder mod-id validation accepted separator-only ids like `.`, `..`, and `---`.
- Builder validated `modId.Trim()` but still used raw `_modId` for `Mods/[ModId]` and manifest `Id`.
- Loader accepted any non-whitespace manifest `Id`, did not validate dependency ids, and let `EntryAssembly` remain path-like before package-local path resolution.

What was done:
- Added canonical mod id validation to `ModLoader` and `ModBuilderWindow`.
- Valid ids now use lowercase letters/digits separated by single `.`, `_`, or `-`; no whitespace, leading/trailing/repeated separators, separator-only ids, or reserved filesystem device segments.
- Loader validates manifest `Id` before hash/path use.
- Loader validates dependency ids before load-order resolution.
- Loader restricts `EntryAssembly` to a package-local `.dll` file name and clears invalid values before any path combine or metadata scan.
- Builder validates dependency ids and writes canonical trimmed `modId` to output path, bundle name, and manifest `Id`.
- Updated `Signal_Schema.json` to schema revision `43` with `modIdentifierCanonicalForm=true`, `dependencyIdentifiersValidated=true`, and `entryAssemblyPathRestrictedToFileName=true`.
- Extended `Validate_Mod_API_Static.ps1` and updated `README.md`, `Mod_API_Specification.md`, `Loader_Save_Audit_Matrix.md`, `Mod_API_Sandbox_Quarantine.md`, `Runtime_Verification_Playbook.md`, `SDK_Authoring_Interface_Plan.md`, `SDK_Product_Blueprint.md`, and `Change_Control_Checklist.md`.

Cinematic cheats used:
- None. This is cold package identity validation. The design rejects ambiguous package identities before runtime instead of spending frame-time on defensive routing.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: cold string validation during package discovery/editor build only.
- Removed risk: package ids cannot alias filesystem tokens or unstable owner hashes; `EntryAssembly` cannot express an external path through the manifest.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `43`, `ModIdentifierCanonicalForm=True`, `DependencyIdentifiersValidated=True`, `EntryAssemblyPathRestrictedToFileName=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `43` and all package identity flags true in loader-save audit and last static validation snapshot.
- PASS: stale schema-42/path-unsafe scan found no old revision, raw builder mod-id path, raw manifest id assignment, or contiguous unsafe EntryAssembly validation route.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: Unity/dotnet compile was not launched because CPU samples were 57, 90, and 40 percent. No active `dotnet.exe`, `csc.exe`, or Unity process was found. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 20

What was wrong:
- Public facade ownership checks depend on `ModExecutionScope.HasActiveMod`.
- `ModExecutionScope.Enter(null)` or blank owner input previously opened an active `"anonymous"` scope.
- `CurrentModId` also synthesized `"anonymous"` outside a real owner, so active-scope proof could be satisfied without canonical mod id or non-zero owner hash.

What was done:
- `ModExecutionScope` now rejects blank owner ids before opening scope.
- Scope creation resolves or requires a non-zero owner hash.
- `HasActiveMod` now requires positive scope depth, non-empty current mod id, and non-zero hash.
- `CurrentModId` returns empty outside a real scope instead of synthesizing `"anonymous"`.
- Updated `Signal_Schema.json` to schema revision `44` with `modExecutionScopeRejectsAnonymousOwner=true`.
- Extended `Validate_Mod_API_Static.ps1` to prove source guards, schema snapshot, loader audit, spec, runtime playbook, and change-control checklist coverage.
- Updated `README.md`, `Mod_API_Specification.md`, `Loader_Save_Audit_Matrix.md`, `Mod_API_Sandbox_Quarantine.md`, `Runtime_Verification_Playbook.md`, and `Change_Control_Checklist.md`.

Cinematic cheats used:
- None. This is ownership-contract hardening, not simulation or presentation. The architectural cheat is fail-fast scope creation: reject ownerless managed execution before any facade, event, command, save, or telemetry path can spend frame-time work.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: low microseconds only when a managed mod execution scope is opened; envelope-only runtime hot paths are unchanged.
- Removed risk: public facade guards cannot be bypassed by a synthetic `"anonymous"` owner during future managed bridge or harness execution.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `44`, `ModExecutionScopeRejectsAnonymousOwner=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `44`, loader-save audit scope flag `True`, and last static validation snapshot scope flag `True`.
- PASS: stale schema-43 scan found no stale current-revision text in touched modding docs/schema; remaining `43` occurrences are event hash literals `0x43444D47`.
- PASS: removed-code scan found no `ModExecutionScope` anonymous active owner fallback or `_scopeDepth > 0`-only `HasActiveMod` route.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: Unity/dotnet compile was not launched because CPU samples were 100, 100, and 100 percent with multiple active `dotnet.exe` processes. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 21

What was wrong:
- `SubtitleCue` was treated as a reserved future localization kernel, but `TriggerSubtitleCue` still existed in the runtime default future-envelope allowlist.
- The same alias also remained in `allowed_opcodes.csv` and the editor sandbox tuner opcode list.
- Result: a subtitle/localization alias could route toward `ModSubtitleCueSignal` without localization owner proof, zero-GC subtitle path proof, quota telemetry, rejection behavior, unload behavior, or Unity runtime playbook evidence.

What was done:
- Removed `TriggerSubtitleCue` from `GenerateEmergencyMockOpcodes()` and `IsRuntimeAllowedFutureCommandOpcode()`.
- Removed `0xBCEE082A # TriggerSubtitleCue` from `Docs/Modding/allowed_opcodes.csv`.
- Removed `FutureCommandOpcodes.TriggerSubtitleCue` and `TRIGGER_SUBTITLE_CUE_OP` from `ModApiSandboxTunerWindow`.
- Kept the public hash constant as reserved metadata; no packet layout or source hash changed.
- Updated `Signal_Schema.json` to schema revision `45` with `futureCommandAllowedOpcodeCount=8`, `futureSubtitleCueAliasesReserved=true`, and `runtimeForbiddenFutureCommandOpcodes`.
- Extended `Validate_Mod_API_Static.ps1` to reject alias re-entry into default runtime map, CSV, or editor runtime tuner.
- Updated `Command_Audit_Matrix.md`, `Future_Command_Kernel_Reservations.md`, `Runtime_Verification_Playbook.md`, `README.md`, `Mod_API_Specification.md`, `Mod_API_Sandbox_Quarantine.md`, and `Change_Control_Checklist.md`.

Cinematic cheats used:
- None. This is authority pruning, not simulation. The practical cheat is fail-fast opcode rejection: do not simulate or present unowned subtitle behavior until the localization owner supplies proof.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static/editor validation only.
- Removed risk: illegal subtitle cue envelopes fail before runtime signal routing and cannot consume localization/presentation budget through an alias.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `45`, `FutureCommandAllowedOpcodeCount=8`, `FutureSubtitleCueAliasesReserved=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `45`, subtitle alias flag `True`, and forbidden future opcode list containing `TriggerSubtitleCue`.
- PASS: stale schema/count/alias scan found no schema-44 current text, `FutureCommandAllowedOpcodeCount = 9`, `TRIGGER_SUBTITLE_CUE_OP`, or allowed-opcode `0xBCEE082A # TriggerSubtitleCue` text.
- PASS: forbidden runtime exposure scan found no `FutureCommandOpcodes.TriggerSubtitleCue` in the editor runtime tuner or `allowed_opcodes.csv`; remaining source references are the hash constant and reserved/internal routing checks.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: Unity/dotnet compile was not launched because CPU samples were 100, 100, and 100 percent, with eight active `dotnet.exe` processes and one `VBCSCompiler.exe` process. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 22

What was wrong:
- `HectonGameEvents.cs` legacy managed event payload classes were internal, but many constructors and properties were still `public`.
- Some public members carried first-party handles: `ItemData`, `BuildableData`, `HectonSurvivalSystem`, `SurvivalDeathRecord`.
- Current external exposure was blocked by internal containing classes, but the member visibility was stale contract debt and a future publicening trap.
- `Validate_Mod_API_Static.ps1` also had a blind spot: subscription constructor coverage used undefined `$modEventProjectionBridgeSource` instead of the already-read projection bridge source, so it did not prove `ModEventProjectionBridge` token constructors.

What was done:
- Changed all `HectonGameEvents` constructors and properties from public to internal.
- Preserved same-assembly first-party access; no runtime route or payload layout was changed.
- Updated `Signal_Schema.json` to schema revision `46` with `gameEventPayloadMembersInternalOnly=true`.
- Extended `Validate_Mod_API_Static.ps1` to read `HectonGameEvents.cs`, reject any line-level `public` member in that file, and require schema/docs/playbook evidence.
- Fixed the subscription constructor scan to use `$projectionSource`, so `ModEventProjectionBridge` token constructors are covered.
- Updated `Event_Subscription_Audit_Matrix.md`, `API_Surface_Audit_Matrix.md`, `README.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is API-boundary hardening. The practical cheat is keeping legacy managed event payloads first-party only instead of paying runtime complexity to redact object handles after exposure.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static validation only.
- Removed risk: future managed-event reopening cannot accidentally expose Unity object, authored asset, survival-system, or survival-record handles through legacy payload members.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `46`, `GameEventPayloadMembersInternalOnly=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `46`, event audit flag `True`, and last static validation snapshot flag `True`.
- PASS: `rg -n "^\s*public\s+" Assets/_Project/Scripts/ModdingAPI/HectonGameEvents.cs` returned no matches.
- PASS: stale validator-variable scan found no `$modEventProjectionBridgeSource` reference.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: Unity/dotnet compile was not launched because CPU samples were 74.3, 90.26, and 74.27 percent, with active `dotnet.exe` and `VBCSCompiler.exe` processes. Last known Unity compile wall remains the unrelated Core contract dependency failure in `PhysicsImpactContracts.cs` and `HabitatFluidIncursionContracts.cs`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 23

What was wrong:
- `ModRuntimeInfo` and `ModLoadStatus` were internal engine diagnostics, but `ModRuntimeInfo` still had public fields.
- Those fields include `DirectoryPath`, `AssetBundlePath`, load status, and loader status text.
- Current external exposure was blocked by the internal type, but public fields were dormant SDK leak debt if the descriptor is ever made public again.

What was done:
- Changed every `ModRuntimeInfo` field from public to internal.
- Kept `ModMetadata` public because it is package-declared metadata, not runtime path/status diagnostics.
- Updated `Signal_Schema.json` to schema revision `47` with `modRuntimeInfoMembersInternalOnly=true`.
- Extended `Validate_Mod_API_Static.ps1` so `ModRuntimeInfo` field count still audits declared fields but any public member in `ModRuntimeInfo.cs` fails the gate.
- Updated `Loader_Save_Audit_Matrix.md`, `API_Surface_Audit_Matrix.md`, `README.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is cold diagnostics boundary hardening. The practical cheat is keeping full path diagnostics internal instead of creating a redaction layer before a real public diagnostics requirement exists.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static validation only.
- Removed risk: package root paths, AssetBundle paths, load status, and loader failure text cannot become accidental SDK fields through the existing runtime descriptor.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `47`, `ModRuntimeInfoMembersInternalOnly=True`, `GameEventPayloadMembersInternalOnly=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `47`, loader-save audit flag `True`, and last static validation snapshot flag `True`.
- PASS: `rg -n "^\s*public\s+" Assets/_Project/Scripts/ModdingAPI/ModRuntimeInfo.cs` returned no matches.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- BLOCKED: `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` failed before modding compile proof on external package references in `CandiceSQLiteProvider.cs`: missing `Mono.Data` and `SqliteDataReader`.
- DEFERRED: Unity batchmode was not launched because `Temp/UnityLockfile` exists. The lockfile was not deleted.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 24

What was wrong:
- `HectonEventBus` was internal, but four direct bus methods were still `public static`: unmanaged subscribe, native subscribe, projected subscribe, and unmanaged publish.
- Current external exposure was blocked by the internal class, but the public members created dormant SDK leak debt and a future second event route.
- Docs claimed `HectonAPI.Events` is the only public route, but the static gate only proved the class-level `internal` keyword.

What was done:
- Changed the direct `HectonEventBus` bus methods from public to internal.
- Kept all public mod-facing event methods on `HectonAPI.Events`; public event method count remains `7`.
- Updated `Signal_Schema.json` to schema revision `48` with `hectonEventBusPublicStaticMembersForbidden=true`.
- Extended `Validate_Mod_API_Static.ps1` to fail on any line-level `public static` member in `HectonEventBus.cs`.
- Updated `README.md`, `Event_Subscription_Audit_Matrix.md`, `API_Surface_Audit_Matrix.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is API-boundary hardening. The practical cheat is keeping one public facade instead of supporting a duplicate event route with extra ownership checks.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static validation only.
- Removed risk: future managed-event reopening cannot accidentally expose direct bus subscribe/publish methods outside the facade.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `48`, `HectonEventBusPublicStaticMembersForbidden=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `48`, event audit flag `True`, and last static validation snapshot flag `True`.
- PASS: `rg -n "^\s*public\s+static\s+" Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs` returned no matches.
- PASS: stale schema-47 scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- BLOCKED: `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` failed before modding compile proof on external package references in `CandiceSQLiteProvider.cs`: missing `Mono.Data` and `SqliteDataReader`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 25

What was wrong:
- `FutureCommandSandboxValidator` was internal, but its static control-plane methods were still public.
- Public methods covered lifecycle, public request, raw byte stream ingress, external queue drain, validation scheduling/finalization, tuning, opcode toggles, approved asset registration, CSV reload, telemetry snapshots, CRC, self-audit, integrity hash, kernel telemetry, and blackbox dumping.
- `MockModQueue.Wrap` was also public static.
- This contradicted the existing contract that mods submit packets through `HectonAPI.Commands.RequestFuture` and do not call the validator directly.

What was done:
- Changed validator static control-plane methods from public to internal.
- Changed `MockModQueue.Wrap` from public static to internal static.
- Preserved public `FutureCommandOpcodes` and `FutureCommandEnvelope.SizeBytes` as the public packet/hash facts.
- Updated `Signal_Schema.json` to schema revision `49` with `futureCommandSandboxPublicStaticMembersForbidden=true`.
- Extended `Validate_Mod_API_Static.ps1` to inspect the validator class body and `MockModQueue.Wrap` without rejecting intended public opcode constants.
- Updated `README.md`, `API_Surface_Audit_Matrix.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is control-plane boundary hardening. The practical cheat is keeping rich editor/tuning/telemetry controls same-assembly only instead of creating public redaction wrappers without runtime proof.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static validation only.
- Removed risk: future SDK/public assembly changes cannot accidentally expose raw stream ingress, tuning, CSV reload, telemetry copy, or blackbox dump methods as mod APIs.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `49`, `FutureCommandSandboxPublicStaticMembersForbidden=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `49`, command API flag `True`, and last static validation snapshot flag `True`.
- PASS: targeted source scan found `ValidatorPublicStaticMatches=0` and `MockWrapPublic=False`; the only remaining file-level public static line is `public static class FutureCommandOpcodes`.
- PASS: stale schema-48 scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet/Unity compile was not launched because CPU sampled 68 percent and an active `dotnet.exe` process was present.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 26

What was wrong:
- `HectonModHooks` was internal, but `PublishPlayerSpawned` and `PublishBiomeChanged` were still `public static`.
- `ModCommandDispatcher` was internal, but legacy queue ingress and float-packing helpers were still `public static`.
- This repeated the dormant SDK leak pattern: current access was blocked by internal containing types, but member visibility preserved direct command/event routes if assembly or class visibility drifts later.

What was done:
- Changed `HectonModHooks.PublishPlayerSpawned` and `PublishBiomeChanged` to internal.
- Changed `ModCommandDispatcher.Request`, `RequestAup`, `RequestRenderInstance`, `PackSequentialFloat2`, and `PackSequentialFloat3` to internal.
- Kept public mod-facing access on `HectonAPI.Events` and `HectonAPI.Commands`.
- Updated `Signal_Schema.json` to schema revision `50` with `hectonModHooksPublicStaticMembersForbidden=true` and `modCommandDispatcherPublicStaticMembersForbidden=true`.
- Extended `Validate_Mod_API_Static.ps1` with gates for direct hook publication methods and dispatcher public static members.
- Updated `README.md`, `Mod_API_Specification.md`, `API_Surface_Audit_Matrix.md`, `Event_Subscription_Audit_Matrix.md`, `Command_Audit_Matrix.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is cold API-boundary hardening. The practical cheat is refusing a duplicate public dispatcher/helper layer and keeping one public facade route.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static validation only.
- Removed risk: direct first-party command/event infrastructure cannot become accidental SDK API through public static members inside internal types.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `50`, `HectonModHooksPublicStaticMembersForbidden=True`, `ModCommandDispatcherPublicStaticMembersForbidden=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `50`, event/command flags `True`, and matching last static validation snapshot flags.
- PASS: targeted source scan found zero public hook publication methods and zero public static members inside `ModCommandDispatcher`.
- PASS: stale schema-49 scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- BLOCKED: `dotnet build Assembly-CSharp.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal` failed outside this domain on `CandiceSQLiteProvider.cs`: missing `Mono.Data` and `SqliteDataReader`.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 27

What was wrong:
- Public event facade methods required active `ModExecutionScope`, but internal unmanaged/native/projected event bridge routes could still resolve blank subscriber ids from `ModExecutionScope.CurrentModId`.
- `ModEventProjectionBridge.Subscribe` still had an explicit `"anonymous"` fallback before creating a `HectonEventSubscription`.
- That contradicted the current owner-proof contract and preserved an anonymous projected callback route if same-assembly code bypassed the public facade.

What was done:
- Added active-scope and subscriber-id match enforcement to `HectonEventBus` unmanaged, native, and projected subscription routes.
- Added the same ownership check to `ModEventProjectionBridge.SubscribeProjected`.
- Removed the projected bridge `"anonymous"` fallback; token creation now requires a concrete mod subscriber id.
- Updated `Signal_Schema.json` to schema revision `51` with `projectedEventBridgeRejectsAnonymousSubscribers=true`.
- Extended `Validate_Mod_API_Static.ps1` to reject missing bridge ownership guards or the old anonymous fallback.
- Updated `README.md`, `Mod_API_Specification.md`, `Event_Subscription_Audit_Matrix.md`, `API_Surface_Audit_Matrix.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is cold API-boundary hardening. The practical cheat is failing anonymous subscriptions before token creation instead of adding runtime cull/redaction complexity.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: low microseconds only on cold subscription calls.
- Removed risk: projected managed callbacks cannot be created under an anonymous subscriber id if managed event projections are reopened.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `51`, `ProjectedEventBridgeRejectsAnonymousSubscribers=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `51`, event audit flag `True`, and matching last static validation snapshot flag.
- PASS: targeted bridge scan found `BusRequireScopeRoutes=3`, `ProjectionAnonymousFallback=False`, and bridge active-scope/concrete-id guards present.
- PASS: stale schema-50 scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet/Unity compile was not launched after schema 51 because CPU sampled 100 percent and active `dotnet.exe` / `VBCSCompiler.exe` processes were present.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 28

What was wrong:
- `MockModQueue` was internal, but it still exposed a public `NativeQueue<FutureCommandEnvelope>` field and public instance control methods.
- That did not make the queue external today, but it preserved a dormant native-handle SDK leak if type visibility or assembly boundaries drift later.
- The previous validator gate only covered sandbox public static methods, so this instance-level leak could return unnoticed.

What was done:
- Changed `MockModQueue.Queue` to private `_queue`.
- Changed `GetIsCreated` and `Attach` to internal.
- Changed `Dispose` to explicit `IDisposable.Dispose`, removing the public instance dispose method from the struct surface.
- Updated `Signal_Schema.json` to schema revision `52` with `mockModQueueMembersInternalOnly=true`.
- Extended `Validate_Mod_API_Static.ps1` with a body scan that rejects public queue handles or public instance control methods in `MockModQueue`.
- Updated `README.md`, `Mod_API_Specification.md`, `API_Surface_Audit_Matrix.md`, `Command_Audit_Matrix.md`, `Mod_API_Sandbox_Quarantine.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is cold control-plane boundary hardening. The practical cheat is keeping batch queue plumbing first-party only instead of building public redaction wrappers for native queue handles.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static validation only.
- Removed risk: public SDK cannot accidentally gain a `NativeQueue` ingress helper; runtime mods keep the single owned `HectonAPI.Commands.RequestFuture` route.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `52`, `MockModQueueMembersInternalOnly=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `52`, command API flag `True`, and last static validation snapshot flag `True`.
- PASS: targeted `MockModQueue` body scan found `PublicMembers=0`, `PrivateQueue=True`, `InternalAttach=True`, `ExplicitDispose=True`.
- PASS: stale schema-49/50/51 scan found no stale current-revision text in modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet/Unity compile was not launched because CPU sampled 85 percent and active `dotnet.exe` / `VBCSCompiler.exe` processes were present.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 29

What was wrong:
- `HectonAPI.Resources` passed the active mod id, but `ModResourceRegistry.TryRegister` accepted any non-empty `modId` while any mod scope was active.
- A same-assembly caller could register a hash under a forged resource owner before hash creation.
- The validator did not prove owner-id equality inside the registry primitive.

What was done:
- Added an ordinal `modId == ModExecutionScope.CurrentModId` guard in `ModResourceRegistry.TryRegister`.
- Updated `Signal_Schema.json` to schema revision `53` with `resourceRegistryRejectsForgedOwner=true`.
- Extended `Validate_Mod_API_Static.ps1` to prove the equality guard, schema flags, resource audit wording, and runtime playbook output.
- Updated `README.md`, `Resource_Content_Audit_Matrix.md`, `API_Surface_Audit_Matrix.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is cold owner-proof hardening. The practical cheat is rejecting forged resource ids before asset resolution instead of adding runtime revoke/rewrite machinery later.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: one ordinal string comparison on cold resource registration.
- Removed risk: hash-only resources cannot be attributed to a different mod owner through the internal registry helper.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `53`, `ResourceRegistryRejectsForgedOwner=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `53`, resource audit flag `True`, and last static validation snapshot flag `True`.
- PASS: targeted source scan found the `modId` / `ModExecutionScope.CurrentModId` ordinal equality guard and rejection message.
- PASS: stale schema-50/51/52 scan found no stale current-revision text in modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet/Unity compile was not launched because CPU sampled 99 percent. No active dotnet/csc/Unity process was present, but CPU gate failed.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 30

What was wrong:
- The projected event bridge source used a smooth continuous cap curve, but the runtime playbook still documented a linear `GlobalQualityWeight` cap.
- `Signal_Schema.json` had low/high cap numbers but no curve formula or snapshot flag proving continuous scaling.
- `Validate_Mod_API_Static.ps1` could pass while the QA playbook and runtime source disagreed.

What was done:
- Changed `ModEventProjectionBridge.ResolveProjectionCap` to call the existing `Smooth01` helper instead of duplicating the polynomial inline.
- Updated `Signal_Schema.json` to schema revision `54` with `projectedEventCapUsesSmoothContinuousCurve=true`, `projectionCapCurve`, and `projectionCapFormula`.
- Extended `Validate_Mod_API_Static.ps1` to prove low/high cap constants, finite saturation, `Smooth01` use, schema fields, snapshot flag, event audit text, and runtime playbook formula.
- Updated `README.md`, `Mod_API_Specification.md`, `Event_Subscription_Audit_Matrix.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- Continuous cadence scaling. Low devices keep a reduced projected callback budget; higher devices spend the saved budget on richer mod-facing presentation/diagnostics without changing gameplay truth.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static validation only.
- Source behavior: equivalent cap arithmetic, no new allocation, no new native buffer, no new public route.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `54`, `ProjectedEventCapUsesSmoothContinuousCurve=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `54`, projection curve text, formula text, and last static validation snapshot flag `True`.
- PASS: targeted source/doc scan found `Smooth01(qualityWeight01)` cap use and the smoothstep formula in schema/playbook/audit/spec.
- PASS: stale schema-53/linear-cap scan found no stale current-revision or old linear cap text in modding docs/source.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet/Unity compile was not launched because CPU sampled 82 percent. No active dotnet/csc/Unity process was present, but CPU gate failed.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 31

What was wrong:
- `Mod_API_Specification.md` still said the current static closure was schema revision `53`, while `Signal_Schema.json` had advanced beyond it.
- `Validate_Mod_API_Static.ps1` checked the README schema line but did not prove the spec closure paragraph matched the schema revision.
- The runtime playbook omitted the `ProjectedEventCapUsesSmoothContinuousCurve = True` result even though the validator emitted it.

What was done:
- Updated `Signal_Schema.json` to schema revision `55` with `modApiSpecCurrentClosureRevisionMatchesSchema=true`.
- Updated `Mod_API_Specification.md` current closure to revision `55` and included the smooth continuous projected event cap proof in that closure.
- Extended `Validate_Mod_API_Static.ps1` to fail when the spec closure revision does not match `Signal_Schema.json.schemaRevision`.
- Updated `README.md` and `Runtime_Verification_Playbook.md` for schema 55 output.

Cinematic cheats used:
- None. This is proof-chain hygiene. The practical cheat is making the static gate catch stale contract text instead of paying runtime complexity for ambiguous SDK behavior.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: static validation only.
- Removed risk: the public mod API spec can no longer silently claim an old schema closure while the README/schema advance.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `55`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`, `ProjectedEventCapUsesSmoothContinuousCurve=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `55`, spec closure parity flag `True`, and projected cap curve flag `True`.
- PASS: stale schema closure scan found no stale current-closure or README schema revision `50-54` text in touched modding contract files.
- PASS: scoped `git diff --check` for touched schema 55 files. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- NOT RUN: dotnet/Unity compile was not launched because this pass did not change C# runtime/editor source. CPU/process gate was sampled at 21 percent CPU with zero active dotnet/csc/VBCSCompiler/Unity processes.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 32

What was wrong:
- `HectonEventBus` private channel implementations still synthesized `"anonymous"` subscriber ids.
- The public facade and projected bridge had owner checks, but the actual token factory could still create ownerless `HectonEventSubscription` tokens if a same-class route passed a blank subscriber id.
- `Validate_Mod_API_Static.ps1` proved the outer guards but did not prove private channel bodies rejected anonymous fallback logic.

What was done:
- Added `RequireConcreteSubscriberId` in `HectonEventBus`.
- Routed managed, unmanaged, and native channel subscriptions through that guard before token creation.
- Updated `Signal_Schema.json` to schema revision `56` with `eventChannelsRejectAnonymousSubscribers=true`.
- Extended `Validate_Mod_API_Static.ps1` to prove the source guard, schema flags, event audit wording, spec closure, and runtime playbook output.
- Updated `README.md`, `Event_Subscription_Audit_Matrix.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.

Cinematic cheats used:
- None. This is cold route hardening. The practical cheat is failing ownerless subscriptions immediately instead of adding runtime revoke scans for anonymous callback tokens.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: one cold branch/string check on subscription creation.
- Removed risk: anonymous event tokens cannot survive into unload/callback ownership paths.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `56`, `EventChannelsRejectAnonymousSubscribers=True`, `ProjectedEventBridgeRejectsAnonymousSubscribers=True`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `56`, event channel anonymous rejection flag `True`, and last static validation snapshot flag `True`.
- PASS: source anonymous-literal scan found no `"anonymous"` fallback in `HectonEventBus.cs` or `ModEventProjectionBridge.cs`.
- PASS: stale schema-55 scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- BLOCKED: `dotnet build Assembly-CSharp.csproj -v:minimal` was attempted after CPU/process gate allowed it and failed on external Candice SQLite dependency errors: `CandiceSQLiteProvider.cs(1,12): CS0234 Mono.Data missing` and `CandiceSQLiteProvider.cs(489,60): CS0246 SqliteDataReader missing`.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 33

What was wrong:
- `ModSaveStateStore` still had a lower-level scope-less fallback that derived persistence ownership from arbitrary save keys.
- Public `HectonAPI.SaveState` facade calls were scoped, but same-assembly store callers could bypass that proof.
- `ModWorldPersistenceManager` relied on the fallback for the internal `hecton.internal.mod_world_spawns` payload.

What was done:
- Removed the `ResolvePersistenceOwnerHash` key-hash fallback from public mod store calls.
- Added active-scope enforcement to `SetModString` / `GetModString`.
- Added explicit engine-owned `SetEngineString` / `GetEngineString` restricted to `hecton.internal.` keys and reserved owner id `hecton.internal.engine_save_owner`.
- Moved `ModWorldPersistenceManager` to the explicit engine route while preserving legacy read compatibility for old key-hash payloads.
- Updated `Signal_Schema.json` to schema revision `57`.
- Updated `README.md`, `Loader_Save_Audit_Matrix.md`, `API_Surface_Audit_Matrix.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.
- Extended `Validate_Mod_API_Static.ps1` to prove source guards, schema flags, audit docs, spec text, and playbook output.

Cinematic cheats used:
- None. This is save ownership hardening. The practical cheat is an explicit cold engine route instead of adding runtime owner-reconciliation scans or save migration work on every frame.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: cold save/load branch and string-prefix checks only.
- Removed risk: arbitrary keys cannot mint mod save owners from same-assembly code.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `57`, `SaveStateStoreRequiresScopedOrEngineOwner=True`, `SaveStatePublicMethods=2`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `57`, `loaderSaveAudit.saveStateStoreRequiresScopedOrEngineOwner=True`, `engineSaveStateKeyPrefix=hecton.internal.`, `engineSaveStateOwnerId=hecton.internal.engine_save_owner`, and last static validation snapshot flag `True`.
- PASS: legacy fallback scan found no `ResolvePersistenceOwnerHash`, no `SetModString(SaveKey`, and no `GetModString(SaveKey` in touched SaveState source.
- PASS: stale schema-56 scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet compile was not launched because CPU sampled 100 percent and an active `dotnet.exe` process was present. Last attempted dotnet compile still fails outside this domain on external Candice SQLite dependency errors from pass 32.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 34

What was wrong:
- `ModLoader.TryReadManifest` read `mod.json` with `File.ReadAllText` before any byte cap.
- A hostile oversized manifest could allocate a large managed string during cold package discovery before canonical id, dependency, EntryAssembly, API version, or reserved assembly checks.
- The static proof chain did not record a manifest byte cap.
- Concurrent source drift changed `MockModQueue` shape and renamed the acoustic sandbox output signal; the validator needed to follow the real no-public-route contract without reverting other-agent changes.

What was done:
- Added `MaxManifestBytes = 32768` and `TryValidateManifestFileSize` in `ModLoader`.
- The loader now rejects missing, empty, or `>32768` byte `mod.json` before `File.ReadAllText`.
- Updated `Signal_Schema.json` to schema revision `58`.
- Updated `README.md`, `Loader_Save_Audit_Matrix.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.
- Extended `Validate_Mod_API_Static.ps1` to prove manifest byte cap, pre-read ordering, schema snapshot, audit docs, spec text, and playbook output.
- Adjusted the validator to accept current `internal ref struct MockModQueue` if all queue/control members remain non-public, to track `SandboxMockAcousticSignal`, and to exclude root cold registry cache hooks from the internal-forbidden facade method count.
- Updated `Command_Audit_Matrix.md` for `SandboxMockAcousticSignal`.

Cinematic cheats used:
- No physical simulation cheat. The practical SDK cheat is bounding the small runtime manifest and pushing richer authoring data into separate bounded artifacts instead of letting JSON grow unbounded.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: one cold `FileInfo` allocation and length checks per discovered manifest.
- Removed risk: large managed string allocation during mod discovery from oversized `mod.json`.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `58`, `ManifestMaxBytes=32768`, `ManifestByteCapEnforcedBeforeRead=True`, `SaveStateStoreRequiresScopedOrEngineOwner=True`, `MockModQueueMembersInternalOnly=True`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `58`, manifest byte cap `32768`, and last static validation snapshot `manifestByteCapEnforcedBeforeRead=True`.
- PASS: source ordering scan found `TryValidateManifestFileSize(manifestPath)` before `File.ReadAllText(manifestPath)`.
- PASS: stale schema-57 scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet compile was not launched because CPU sampled 69 percent and active `dotnet.exe` process `61052` was present.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 35

What was wrong:
- `ModLoader.DiscoverAndLoadMods` recursively called `Directory.GetFiles(..., SearchOption.AllDirectories)` and allocated the full manifest path array before any package count cap.
- The schema 58 byte cap protected each `mod.json` read, but package discovery count was still controlled by filesystem contents.
- Candidate list capacity was derived from the unbounded path array length.

What was done:
- Added `MaxDiscoveredManifestCount = 64` and `MaxDiscoveredManifestCountLabel = "64"` in `ModLoader`.
- Replaced recursive `Directory.GetFiles` discovery with `CollectManifestPaths`, lazy `Directory.EnumerateFiles`, capped collection, and explicit warning on cap hit or discovery exceptions.
- Allocated `List<ModCandidate>` from the bounded collected count instead of an unbounded path array.
- Updated `Signal_Schema.json` to schema revision `59`.
- Updated `README.md`, `Loader_Save_Audit_Matrix.md`, `Mod_API_Specification.md`, and `Runtime_Verification_Playbook.md`.
- Extended `Validate_Mod_API_Static.ps1` to prove discovery cap, lazy enumeration, removal of recursive manifest `Directory.GetFiles`, ordering before candidate allocation, schema snapshot, audit docs, spec text, and playbook output.

Cinematic cheats used:
- No physical simulation cheat. The practical SDK cheat is a fixed runtime discovery ceiling while richer package browsing belongs in SDK/workbench tooling, not in the game boot crawl.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: cold directory enumeration and up to 64 collected manifest path strings.
- Removed risk: full recursive path-array allocation and candidate-list sizing from arbitrary Mods directory contents.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `59`, `ManifestMaxBytes=32768`, `ManifestByteCapEnforcedBeforeRead=True`, `ManifestDiscoveryMaxCount=64`, `ManifestDiscoveryUsesBoundedEnumeration=True`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `59`, manifest discovery cap `64`, and last static validation snapshot `manifestDiscoveryUsesBoundedEnumeration=True`.
- PASS: source scan found lazy `Directory.EnumerateFiles`, no recursive manifest `Directory.GetFiles`, and `CollectManifestPaths` before candidate allocation.
- PASS: stale schema-58 scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet compile was not launched because CPU sampled 100 percent and active `dotnet.exe` process `61052` was present. Last attempted dotnet compile still fails outside this domain on external Candice SQLite dependency errors from pass 32.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 36

What was wrong:
- `ModLoader` still used top-level `Directory.GetFiles` arrays for managed DLL identity scan, legacy `.bundle` fallback, and `lang_*.json` fallback.
- A package folder could force cold managed path-array allocation even after recursive manifest discovery was capped.
- Managed DLL identity discovery needed a fail-closed package outcome when the top-level DLL cap is exceeded or discovery fails.

What was done:
- Added top-level package file caps in `ModLoader`: `32` managed assemblies, `4` bundles, `16` localization files.
- Replaced the remaining top-level package `Directory.GetFiles` calls with `CollectTopLevelFiles`, lazy `Directory.EnumerateFiles`, bounded lists, deterministic sort, and cap/failure warnings.
- Made managed assembly discovery over-cap or discovery failure set the manifest contract error, disabling the package before load.
- Updated `Signal_Schema.json` to schema revision `60`.
- Updated `README.md`, `Loader_Save_Audit_Matrix.md`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `Mod_API_Sandbox_Quarantine.md`.
- Extended `Validate_Mod_API_Static.ps1` to prove bounded top-level package file discovery, caps `32/4/16`, fail-closed DLL over-cap behavior, schema snapshot, audit docs, spec text, and playbook output.

Cinematic cheats used:
- No physical simulation cheat. The practical SDK cheat is a strict runtime package envelope: rich browsing and package analysis belong in SDK/workbench tools, while game boot discovery stays bounded and predictable.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: cold bounded list allocation and sort for up to 32 DLL paths, 4 bundle paths, or 16 localization paths.
- Removed risk: unbounded top-level package path-array allocation and partial trust after failed managed DLL identity discovery.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `60`, `ManagedAssemblyIdentityScanUsesBoundedEnumeration=True`, `MaxTopLevelManagedAssemblyCount=32`, `ExcessTopLevelManagedAssembliesDisablePackage=True`, `MaxTopLevelBundleCount=4`, `MaxLocalizationFileCount=16`, `TopLevelContentDiscoveryUsesBoundedEnumeration=True`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `60`, loader/save audit caps `32/4/16`, and last static validation snapshot top-level discovery flags.
- PASS: source scan found old top-level DLL/bundle/localization `Directory.GetFiles` calls removed, bounded top-level `Directory.EnumerateFiles` present, and DLL cap/failure package disable reasons present.
- PASS: stale schema-59/current-text scan found no stale current revision or old unbounded top-level DLL wording in touched modding docs/schema/validator.
- PASS: scoped `git diff --check` for touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: dotnet compile was not launched because active `dotnet.exe` process `52044` was present, despite CPU sampling 33.95 percent. Last attempted dotnet compile still fails outside this domain on external Candice SQLite dependency errors from pass 32.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 37

What was wrong:
- `ModAssetManager.LoadRawTexture` enforced file size before `File.ReadAllBytes`, but the read catch covered only `IOException`.
- `UnauthorizedAccessException` or other invalid filesystem/read failures after the byte gate could escape from the dormant legacy raw PNG path.
- Resource/content schema and audits recorded raw texture caps, but not fail-closed read behavior.

What was done:
- Added `System.UnauthorizedAccessException`, `IOException`, and `System.Exception` catch blocks around `File.ReadAllBytes`.
- Kept the pre-read raw PNG byte cap and dimension cap unchanged: `8388608` bytes and `2048` px.
- Updated `Signal_Schema.json` to schema revision `61`.
- Updated `README.md`, `Mod_API_Specification.md`, `Resource_Content_Audit_Matrix.md`, and `Runtime_Verification_Playbook.md`.
- Extended `Validate_Mod_API_Static.ps1` to prove raw texture byte cap before read, fail-closed catch coverage, schema snapshot, audit docs, spec closure revision, and playbook output.

Cinematic cheats used:
- No physical simulation cheat. The practical content-ingress cheat is fail-closed legacy file reads while real runtime UGC remains on CRC-approved `FutureCommandEnvelope` asset references.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Added cost: exception-path catch handling only around cold raw texture file reads.
- Removed risk: unhandled raw PNG filesystem exception after the byte cap.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `61`, `RawTextureMaxBytes=8388608`, `RawTextureMaxDimension=2048`, `RawTextureByteCapEnforcedBeforeRead=True`, `RawTextureReadFailsClosed=True`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `61`, raw texture pre-read byte gate `True`, fail-closed read handling `True`, and matching last static validation snapshot flags.
- PASS: source scan found `TryValidateRawTextureFile(filePath)` before `File.ReadAllBytes(filePath)`, plus `System.UnauthorizedAccessException`, `IOException`, and `System.Exception` catch blocks.
- PASS: stale schema-60 scan found no stale current revision or false raw texture closure flags in touched modding docs/schema/validator.
- PASS: scoped unstaged and cached `git diff --check` for touched source/docs. No whitespace errors.
- PASS: touched-file trailing whitespace scan.
- PASS: `dotnet build Assembly-CSharp.csproj -v:minimal` -> Build succeeded, 45 warnings, 0 errors. Warnings are existing/non-domain: MSB3246 reference metadata warnings, MoreMountains demo type conflict, and Candice unused-field warnings.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 38

What was wrong:
- `ModAssetManager.LoadAsset<TAsset>` exact AssetBundle lookup fell back to `AssetBundle.GetAllAssetNames()` and suffix matching.
- The fallback allocated the bundle asset-name array on lookup miss and could resolve the wrong asset when suffixes collided.
- Schema 61 documented raw texture read safety but did not prove AssetBundle exact-name lookup.

What was done:
- Removed `AssetBundle.GetAllAssetNames()` fallback from `ModAssetManager`.
- Removed `EndsWithAssetPath`.
- Kept exact `bundle.LoadAsset<TAsset>(assetName)` lookup.
- Updated `Signal_Schema.json` to schema revision `62`.
- Updated `README.md`, `Mod_API_Specification.md`, `Resource_Content_Audit_Matrix.md`, and `Runtime_Verification_Playbook.md`.
- Extended `Validate_Mod_API_Static.ps1` to prove `AssetBundleSuffixFallbackDisabled=True` and `AssetBundleGetAllAssetNamesForbidden=True`.

Cinematic cheats used:
- No physical simulation cheat. The SDK/content cheat is exact-name or hash-manifest asset addressing instead of runtime suffix guessing.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Removed risk: cold `string[]` allocation from `GetAllAssetNames()` on legacy AssetBundle lookup miss.
- Removed correctness risk: ambiguous suffix match.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `62`, `AssetBundleSuffixFallbackDisabled=True`, `AssetBundleGetAllAssetNamesForbidden=True`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `62`, `resourceContentAudit.assetBundleSuffixFallbackDisabled=True`, `resourceContentAudit.assetBundleGetAllAssetNamesForbidden=True`, and matching last static validation snapshot flags.
- PASS: source scan found no `GetAllAssetNames`, no `EndsWithAssetPath`, and exact `bundle.LoadAsset<TAsset>(assetName)` still present.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 39

What was wrong:
- Current implemented SDK UX was scattered: only `Hecton/Modding/Mod Builder` existed as a tool, while core docs, sample, playbook, and validator were separate files.
- A mod developer did not have one obvious Unity Editor entry point for authoring, validation, and support docs.
- `ModBuilderWindow.CollectBundleAssetPaths` used `AssetDatabase.FindAssets`, allocating a full GUID array for the selected folder before any package asset cap.

What was done:
- Added `Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs` and `.meta`.
- Added menu `Hecton/Modding/SDK Hub`.
- Hub opens Mod Builder, local `Mods/` folder, README, API spec, authoring plan, product blueprint, sample mod, runtime playbook, and runs `Validate_Mod_API_Static.ps1`.
- Reworked Mod Builder bundle asset collection to use bounded filesystem enumeration, deterministic sort, and `MaxBundleBuildAssetCount=512`.
- Removed non-ASCII punctuation from touched editor C# comments.
- Updated `Signal_Schema.json` to schema revision `63`.
- Updated `README.md`, `Mod_API_Specification.md`, `Runtime_Verification_Playbook.md`, and `SDK_Authoring_Interface_Plan.md`.
- Extended `Validate_Mod_API_Static.ps1` to prove SDK hub presence/actions, docs links, envelope-only warning, builder asset cap, and bounded builder asset discovery.

Cinematic cheats used:
- No physical simulation cheat. Product cheat: make the current SDK path a single Editor hub while preserving envelope-only runtime; heavy Workbench/CLI stays planned, not faked as runtime permission.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Editor/package-time risk reduction: selected bundle folders cap at 512 bundle-eligible files and avoid `AssetDatabase.FindAssets` GUID-array discovery.
- No gameplay tick, SignalBus, NativeQueue, DataVault, save, or Burst path changed.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `63`, `ModdingSdkHubPresent=True`, `ModdingSdkHubOpensBuilder=True`, `ModdingSdkHubLinksCoreDocs=True`, `ModdingSdkHubRunsStaticValidator=True`, `ModdingSdkHubShowsEnvelopeOnlyBoundary=True`, `MaxBundleBuildAssetCount=512`, `BundleBuildAssetDiscoveryUsesBoundedEnumeration=True`, `AssetBundleSuffixFallbackDisabled=True`, `AssetBundleGetAllAssetNamesForbidden=True`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `63`, `sdkAuthoringAudit.hubMenuPath=Hecton/Modding/SDK Hub`, `maxBundleBuildAssetCount=512`, and last static validation snapshot `bundleBuildAssetDiscoveryUsesBoundedEnumeration=True`.
- PASS: source scan found `ModdingSdkHubWindow` menu, `RunStaticValidator`, docs links, `ModBuilderWindow.ShowWindow()`, `MaxBundleBuildAssetCount=512`, bounded `Directory.EnumerateFiles`, and no `AssetDatabase.FindAssets` bundle collection.
- PASS: stale schema-62/current-text scan found no stale current revision or false SDK/AssetBundle closure flags in touched modding docs/schema/validator.
- PASS: scoped `git diff --check` for tracked touched source/docs. Git line-ending warnings only; no whitespace errors.
- PASS: touched-file trailing whitespace scan after schema 63, including new SDK hub `.cs` and `.meta`.
- PASS: touched editor C# non-ASCII scan reported `NonAsciiByteCount=0`.
- DEFERRED: dotnet compile was not launched because CPU sampled 73.99 percent, 58.28 percent, then 27.66 percent while active `dotnet.exe` PID 21868 was present.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 40

What was wrong:
- `ModBuilderWindow` had no explicit SDK-side cap matching the loader's 32 top-level managed DLL package cap.
- Duplicate selected DLL file names could collide in the output package.
- UI validation could perform deep asset and DLL metadata scans from repaint paths.
- Stale output DLL cleanup used an unbounded top-level file array.

What was done:
- Added `MaxManagedAssemblyInputCount=32` and UI/build-time rejection above that cap.
- Added duplicate selected DLL filename rejection before package output.
- Split shallow UI validation from Build Mod deep asset/DLL validation.
- Made configured empty asset folders fail explicitly during bundle build.
- Added `MaxStaleAssemblyCleanupScanCount=128` and bounded stale DLL cleanup enumeration.
- Updated `Signal_Schema.json` to schema revision `64`.
- Updated README, Mod API spec, runtime playbook, loader/save matrix, SDK authoring plan, and static validator proof.

Cinematic cheats used:
- No physical simulation cheat. Product cheat: keep the current SDK builder bounded and deterministic instead of pretending runtime managed DLL execution is supported.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Editor/package-time risk reduction: selected DLLs cap at 32, stale output DLL cleanup caps at 128, and UI repaint avoids deep filesystem/metadata scans.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `64`, `MaxManagedAssemblyInputCount=32`, `MaxStaleAssemblyCleanupScanCount=128`, `BuilderManagedAssemblyInputCapMatchesLoader=True`, `BuilderSkipsExpensiveValidationDuringOnGUI=True`, `StaleDllCleanupUsesBoundedEnumeration=True`, `BuilderRejectsDuplicateManagedAssemblyFileNames=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `64` and matching SDK authoring audit flags.
- PASS: stale schema-63 scan found no stale current revision text in touched modding docs/schema.
- PASS: scoped `git diff --check`, touched-file trailing whitespace scan, and editor C# non-ASCII scan.
- DEFERRED: dotnet compile was not launched because CPU sampled 72.78 percent and active `dotnet.exe` PID 14740 was present.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-27 - pass 41

What was wrong:
- `ModKernelInspectorWindow` still exposed `FutureCommandOpcodes.SubtitleCue` through an Inject Subtitle button.
- That contradicted the reserved subtitle alias contract: `TriggerSubtitleCue` and `SubtitleCue` must not appear in runtime allowlists or editor runtime opcode tools until localization-owner proof exists.
- The validator only checked `ModApiSandboxTunerWindow`, so the inspector leak was not covered by static proof.

What was done:
- Removed the Inject Subtitle button from `ModKernelInspectorWindow`.
- Changed unknown inspector opcode injection to return without payload generation.
- Extended `Validate_Mod_API_Static.ps1` to load `ModKernelInspectorWindow.cs` and reject both reserved subtitle aliases in editor runtime opcode tools.
- Updated `Signal_Schema.json` to schema revision `65` with `EditorRuntimeOpcodeTunersRejectReservedSubtitleAliases=True`.
- Updated README, Mod API spec, runtime playbook, and command audit matrix.

Cinematic cheats used:
- No physical simulation cheat. Authority cheat: keep subtitle support as offline/reserved authoring data until a real localization owner provides proof, instead of letting an editor injector imply runtime support.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Removed risk: editor tooling no longer exposes an unowned subtitle opcode injection path.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `65`, `FutureSubtitleCueAliasesReserved=True`, `EditorRuntimeOpcodeTunersRejectReservedSubtitleAliases=True`, `ModApiSpecCurrentClosureRevisionMatchesSchema=True`.
- PASS: source scan found no `FutureCommandOpcodes.TriggerSubtitleCue` or `FutureCommandOpcodes.SubtitleCue` in `ModKernelInspectorWindow.cs` or `ModApiSandboxTunerWindow.cs`.
- PASS: `Signal_Schema.json` parsed with schema revision `65`, command API subtitle editor-tool rejection flag `True`, and matching last static validation snapshot flag.
- PASS: stale schema-64 scan found no stale current revision text in touched modding docs/schema/validator.
- PASS: scoped `git diff --check`, touched-file trailing whitespace scan, and editor C# non-ASCII scan.
- DEFERRED: dotnet compile was not launched because CPU sampled 63.33 percent and active `dotnet.exe` PID 60456 was present.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-28 - pass 42

What was wrong:
- The SDK Hub was useful inside the Unity project, but did not answer the public-modder workflow concretely.
- A random external author still had no generated file layout, no explicit "Unity is not required for normal authoring" answer, and no current starter contract separate from future Workbench/CLI plans.
- The current Mod Builder can produce legacy-shaped packages while runtime envelope-only mode disables managed DLL and loose content ingestion, so documentation needed to stop implying that those are active public runtime rights.

What was done:
- Extended `ModdingSdkHubWindow` with `Create External Starter Kit` and `Open External Starter Kit`.
- The generator writes missing files only under `ModdingSDK/ExternalStarterKit/`.
- Generated starter kit includes `README.md`, `mod.h8manifest.json`, `mod.json`, `Content/assets.h8manifest.json`, `Graphs/main.h8graph.json`, `Tables/settings.h8table.json`, `Locales/en.h8loc.json`, `Generated/README.md`, `Reports/README.md`, `Reference/README.md`, `Reference/allowed_opcodes.csv`, and `Reference/kernel_tuning_profiles.csv`.
- Added `Docs/Modding/External_Starter_Kit_File_Contract.md`.
- Updated README, Mod API spec, SDK authoring plan, SDK product blueprint, runtime playbook, schema, and static validator.
- Advanced `Signal_Schema.json` to schema revision `66`.
- Extended `Validate_Mod_API_Static.ps1` to prove starter kit generator presence, required manifest outputs, folder README outputs, copied opcode references, no-full-Unity-project guidance, and envelope-only boundary guidance.

Cinematic cheats used:
- No physical simulation cheat. Product cheat: provide a concrete generated external file skeleton now, while keeping Workbench/CLI as future tooling over the same contract instead of pretending managed runtime code is supported.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Editor/offline risk reduction: public author onboarding no longer depends on unbounded source-project discovery or legacy DLL/bundle assumptions.
- No gameplay tick, SignalBus, NativeQueue, DataVault, save, physics, rendering, packet layout, or Burst/job path changed.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `66`, `ExternalStarterKitGeneratorPresent=True`, `ExternalStarterKitWritesAuthoringManifest=True`, `ExternalStarterKitWritesRuntimeManifest=True`, `ExternalStarterKitWritesFolderReadmes=True`, `ExternalStarterKitCopiesOpcodeReferences=True`, `ExternalStarterKitDocumentsNoUnityProjectRequirement=True`, `ExternalStarterKitDocumentsEnvelopeOnlyBoundary=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `66`, `sdkAuthoringAudit.externalStarterKitOutputPath=ModdingSDK/ExternalStarterKit`, and matching last static validation snapshot flags.
- PASS: stale schema-65 scan found no stale current-revision text in touched modding docs/schema/validator.
- PASS: scoped `git diff --check`; Git line-ending warnings only, no whitespace errors.
- PASS: touched-file trailing whitespace scan and editor C# ASCII scan.
- PASS: CPU/process gate before compile: average CPU `29.63`, no `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process present.
- PASS: `dotnet build Assembly-CSharp.csproj -v:minimal` -> Build succeeded, 45 warnings, 0 errors. Warnings are existing/non-domain: MSB3246 reference metadata warnings, MoreMountains demo type conflict, and Candice unused-field warnings.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-28 - pass 43

What was wrong:
- The external starter kit had a concrete layout, but a copied kit still had no local validation script that works without Unity and without the source project.
- A public author could accidentally enable managed entry fields, change runtime away from envelope-only, delete reference CSVs, or break JSON and only discover it later inside internal tooling.

What was done:
- Extended `CreateExternalStarterKit` to write `Tools/README.md` and `Tools/validate_structure.ps1`.
- The generated validator checks required directories/files, JSON parseability, authoring manifest `Compatibility.Runtime = envelope-only`, graph runtime `envelope-only`, API version floor, empty `EntryAssembly`, empty `EntryType`, asset/settings/locale shape, and `Reference/allowed_opcodes.csv` plus `Reference/kernel_tuning_profiles.csv`.
- Updated `External_Starter_Kit_File_Contract.md`, README, Mod API spec, SDK authoring plan, SDK product blueprint, runtime playbook, schema, and static validator.
- Advanced `Signal_Schema.json` to schema revision `67`.
- Extended `Validate_Mod_API_Static.ps1` to prove local structure validator generation and required-file/envelope-only/managed-entry-disabled checks.

Cinematic cheats used:
- No physical simulation cheat. Product cheat: add a cheap fail-fast local validator before Workbench/CLI exists, instead of requiring Unity or pretending the starter folder is runtime-verified.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Offline authoring risk reduction: malformed starter folders fail before pack/sim/runtime ingestion.
- No gameplay tick, SignalBus, NativeQueue, DataVault, save, physics, rendering, packet layout, or Burst/job path changed.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `67`, `ExternalStarterKitWritesLocalStructureValidator=True`, `ExternalStarterKitValidatorChecksRequiredFiles=True`, `ExternalStarterKitValidatorChecksEnvelopeOnly=True`, `ExternalStarterKitValidatorChecksManagedEntryDisabled=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `67` and matching SDK authoring audit local validator flags.
- PASS: stale schema-66 scan found no stale current-revision text in touched modding docs/schema/validator.
- PASS: scoped `git diff --check`; Git line-ending warnings only, no whitespace errors.
- PASS: touched-file trailing whitespace scan and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` was not launched after this C# edit because build-process gate found active `VBCSCompiler` PID `55184` on two samples. CPU averages were `23.97` and `44.04`.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-28 - pass 44

What was wrong:
- The schema 67 local starter validator did not enforce the same package identity contract as `ModLoader` and `ModBuilderWindow`.
- External authors could use non-canonical package IDs, reserved filesystem device segments, mismatched `mod.h8manifest.json`/`mod.json` IDs, or invalid dependency IDs and only fail later.

What was done:
- Extended generated `Tools/validate_structure.ps1` with `Validate-ModId`.
- Added reserved segment rejection for `con`, `prn`, `aux`, `nul`, `com1..com9`, and `lpt1..lpt9`.
- Added authoring/runtime manifest ID parity and runtime dependency ID validation.
- Updated README, Mod API spec, SDK authoring plan, SDK product blueprint, runtime playbook, external starter kit contract, schema, and static validator.
- Advanced `Signal_Schema.json` to schema revision `68`.
- Extended `Validate_Mod_API_Static.ps1` to prove canonical ID, manifest ID parity, and dependency ID validator coverage.

Cinematic cheats used:
- No physical simulation cheat. Product cheat: fail identity errors in the text-folder authoring gate before package bake instead of adding runtime repair, guessing, or source-project dependency.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Offline authoring risk reduction: identity errors fail before pack/sim/runtime ingestion.
- No gameplay tick, `FutureCommandEnvelope` layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, or Burst/job path changed.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `68`, `ExternalStarterKitValidatorChecksCanonicalIds=True`, `ExternalStarterKitValidatorChecksManifestIdParity=True`, `ExternalStarterKitValidatorChecksDependencyIds=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `68` and matching SDK authoring audit identity validator flags.
- PASS: stale schema-67 fixed-string scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check`; Git line-ending warnings only, no whitespace errors.
- PASS: touched-file trailing whitespace scan and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` was not launched after this C# edit because build-process gate first found average CPU `58.5` with active `dotnet.exe` PID `47780`, then re-sample found active `csc.exe` PID `43064` and `dotnet.exe` PID `47780`.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-28 - pass 45

What was wrong:
- The SDK Hub could generate `ModdingSDK/ExternalStarterKit/`, but the folder itself was not present as a versioned public artifact.
- A random external author without Unity still depended on an internal editor action before the starter kit could be copied, zipped, or validated.

What was done:
- Added `ModdingSDK/ExternalStarterKit/` as a versioned folder.
- Included authoring/runtime manifests, graph/table/content/locale drafts, `Generated/`, `Reports/`, `Reference/`, `Tools/`, copied opcode/tuning CSVs, and `Tools/validate_structure.ps1`.
- Updated `Validate_Mod_API_Static.ps1` to require all template files and execute the template's own validator.
- Advanced `Signal_Schema.json` to schema revision `69`.
- Updated README, API spec, SDK authoring plan, SDK product blueprint, runtime playbook, and external starter kit contract to state the versioned template path.

Cinematic cheats used:
- No physical simulation cheat. Product cheat: ship a diffable text starter folder instead of a binary package or Unity-only generation step.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Offline authoring risk reduction: external authors can validate the starter folder without Unity or source-project access.
- No gameplay tick, `FutureCommandEnvelope` layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, or Burst/job path changed.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File ModdingSDK/ExternalStarterKit/Tools/validate_structure.ps1 -Root ModdingSDK/ExternalStarterKit` -> `PASS HECTON-8 external starter structure`.
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `69`, `ExternalStarterKitTemplateVersioned=True`, `ExternalStarterKitTemplatePassesLocalValidator=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `69` and matching starter template flags.
- PASS: stale schema-68 fixed-string scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check`; Git line-ending warnings only, no whitespace errors.
- PASS: touched-file trailing whitespace scan including starter template files and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` was not launched because build-process gate found average CPU `96.5` and active `dotnet.exe` PID `47780`.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-28 - pass 46

What was wrong:
- The versioned starter kit copied `allowed_opcodes.csv` and `kernel_tuning_profiles.csv`, but copied files can drift from authoritative docs.
- A public author could receive stale opcode/tuning guidance while the runtime/source validator used newer data.

What was done:
- Extended `Validate_Mod_API_Static.ps1` to compare starter reference CSVs against `Docs/Modding/allowed_opcodes.csv` and `Docs/Modding/kernel_tuning_profiles.csv`.
- Normalized line endings for the comparison.
- Advanced `Signal_Schema.json` to schema revision `70`.
- Updated README, API spec, SDK authoring plan, SDK product blueprint, runtime playbook, and external starter kit contract to state source parity.

Cinematic cheats used:
- No physical simulation cheat. Product cheat: keep external references as simple copied text files, but enforce source parity in static validation instead of adding runtime lookup or source-project dependency to the portable starter kit.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Offline authoring risk reduction: copied opcode/tuning references cannot silently drift from authoritative docs.
- No runtime allowlist loading, command envelope layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, or Burst/job path changed.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `70`, `ExternalStarterKitTemplateReferenceCsvsMatchSource=True`.
- PASS: `Signal_Schema.json` parsed with schema revision `70` and matching starter reference parity flag.
- PASS: stale schema-69 fixed-string scan found no stale current-revision text in touched modding docs/schema.
- PASS: scoped `git diff --check`; Git line-ending warnings only, no whitespace errors.
- PASS: touched-file trailing whitespace scan including starter template files and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` was not launched because final build-process gate found average CPU `98.5` and active `dotnet.exe` PID `14348`.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-28 - pass 47

What was wrong:
- The external starter kit gave public modders a no-Unity folder contract and structure validator, but no deterministic review/submission artifact.
- `Reports/` existed as a folder role but had no concrete report producer, so future review depended on manual folder inspection or untracked Workbench assumptions.

What was done:
- Added `ModdingSDK/ExternalStarterKit/Tools/build_review_manifest.ps1`.
- Updated `ModdingSdkHubWindow` so `Create External Starter Kit` writes the same review manifest builder non-destructively for generated starter kits.
- Updated `Tools/validate_structure.ps1` required-file checks so a copied starter folder cannot silently lose the review manifest builder.
- Generated `ModdingSDK/ExternalStarterKit/Reports/review_manifest.json` as proof: schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, `14` hashed files.
- Extended `Validate_Mod_API_Static.ps1` to run the review manifest builder and prove `ExternalStarterKitWritesReviewManifestBuilder`, `ExternalStarterKitReviewManifestPasses`, `ExternalStarterKitReviewManifestHashesFiles`, and `ExternalStarterKitReviewManifestExcludesReports`.
- Updated `Signal_Schema.json` to schema revision `71` and synchronized README/spec/authoring plan/product blueprint/runtime playbook/external starter contract.

Cinematic cheats used:
- No runtime simulation added. Product cheat: deterministic offline hashing with a sorted file list and SHA-256 instead of heavy package simulation or Unity import.
- `Generated/` and `Reports/` are excluded so build artifacts do not become source truth.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Offline authoring risk reduction: no measured microsecond claim; review handoff no longer requires manual file enumeration.
- No gameplay tick, `FutureCommandEnvelope` layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, or Burst/job path changed.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File ModdingSDK/ExternalStarterKit/Tools/validate_structure.ps1 -Root ModdingSDK/ExternalStarterKit`.
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File ModdingSDK/ExternalStarterKit/Tools/build_review_manifest.ps1 -Root ModdingSDK/ExternalStarterKit`.
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `71`, all review manifest flags true.
- PASS: `Signal_Schema.json` parsed with `schemaRevision=71`.
- PASS: stale schema-70 current-text scan, scoped `git diff --check`, trailing whitespace scan, and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` was not launched because average CPU was `55.37` and active `dotnet.exe` PID `14348` existed.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.

## MODDING_SDK_AUDIT - 2026-05-28 - pass 48

What was wrong:
- The external starter kit had JSON examples and a no-Unity validator, but no local JSON Schemas or editor mapping.
- A public author using VS Code or another schema-aware editor had no autocomplete/error hints before running PowerShell validation.

What was done:
- Added `ModdingSDK/ExternalStarterKit/Schemas/*.schema.json` for authoring manifest, runtime manifest, command graph, asset declaration, settings table, and locale files.
- Added `ModdingSDK/ExternalStarterKit/.vscode/settings.json` mapping starter JSON files to local schemas.
- Updated `ModdingSdkHubWindow` so generated starter kits receive the same schema files and editor mapping.
- Updated `Tools/validate_structure.ps1` to require and parse schema files and verify `.vscode/settings.json` has `json.schemas`.
- Updated `Validate_Mod_API_Static.ps1` to prove generator output, versioned template presence, schema parseability, editor mapping, and review-manifest inclusion.
- Advanced `Signal_Schema.json` to schema revision `72` and synchronized README/spec/authoring plan/product blueprint/runtime playbook/external starter contract.

Cinematic cheats used:
- No runtime simulation added. Product cheat: local JSON Schema autocomplete catches authoring mistakes before Unity, Workbench, package bake, or runtime validation.
- Schemas are editor hints and offline validation metadata only; they do not grant runtime authority.

Exact microseconds saved:
- Runtime frame: 0 us/frame measured/claimed.
- Offline authoring risk reduction: no measured microsecond claim; field-name and constant mistakes can be flagged while editing.
- No gameplay tick, `FutureCommandEnvelope` layout, SignalBus, NativeQueue, DataVault, save, physics, rendering, or Burst/job path changed.

Verification:
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File ModdingSDK/ExternalStarterKit/Tools/validate_structure.ps1 -Root ModdingSDK/ExternalStarterKit`.
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File ModdingSDK/ExternalStarterKit/Tools/build_review_manifest.ps1 -Root ModdingSDK/ExternalStarterKit`.
- PASS: schema files under `ModdingSDK/ExternalStarterKit/Schemas/*.schema.json` and `.vscode/settings.json` parse as JSON.
- PASS: `powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `72`, JSON schema/editor mapping flags true.
- PASS: `Reports/review_manifest.json` includes schema files and `.vscode/settings.json`, excludes `Reports/review_manifest.json`, and hashes `21` files.
- PASS: stale schema-71 current-text scan, scoped `git diff --check`, trailing whitespace scan, and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` was not launched because average CPU was `79.29`.
- NOT RUN: Unity batchmode compile was not launched because `Temp/UnityLockfile` exists.
## 2026-05-28 - MODDING_SDK_AUDIT - Pass 49 - Schema 73 Starter Identity Helper

What was wrong:
- External authors had a concrete no-Unity starter kit, schemas, validator, and review manifest, but identity setup still required manual edits in two files: `mod.h8manifest.json` and `mod.json`.
- The validator caught mismatch after the fact, but there was no single public authoring route for setting id/name/author/version safely.

What was done:
- Added `ModdingSDK/ExternalStarterKit/Tools/set_mod_identity.ps1`.
- Added the same generated tool to `Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs` so `Hecton/Modding/SDK Hub -> Create External Starter Kit` refreshes missing identity tooling.
- The tool validates canonical mod IDs, rejects reserved filesystem device segments, writes matching identity values to both manifests, and immediately runs `Tools/validate_structure.ps1`.
- Updated starter README, Tools README, external starter contract, runtime playbook, schema revision 73, and static validator proof.
- Regenerated `ModdingSDK/ExternalStarterKit/Reports/review_manifest.json`; it now hashes 22 files and includes `Tools/set_mod_identity.ps1`.

Cinematic Cheats used:
- None in runtime. This is offline authoring tooling. Runtime remains envelope-only; no managed DLL execution, no loose AssetBundle authority, no loose PNG authority, no localization injection right.

Exact Microseconds saved:
- Runtime: 0 us/frame claimed. No gameplay tick, FutureCommandEnvelope layout, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job path changed.
- Authoring cold path: exact runtime microseconds not applicable. Practical saving is eliminating a two-manifest manual identity drift class before package review/load.

Verification:
- PASS: starter validator -> `PASS HECTON-8 external starter structure`.
- PASS: review manifest builder -> `PASS HECTON-8 review manifest: Reports/review_manifest.json`.
- PASS: review manifest parsed with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, 22 hashed files, identity tool included, self-report excluded.
- PASS: static validator -> schema revision 73 with `ExternalStarterKitWritesIdentityTool=True`, `ExternalStarterKitIdentityToolValidatesCanonicalId=True`, `ExternalStarterKitIdentityToolPasses=True`.
- PASS: temp-copy invalid identity probe using `Bad Id` rejected with exit code 1.
- PASS: stale schema-72 scan, scoped `git diff --check`, trailing whitespace scan, and editor C# ASCII scan.
- DEFERRED: dotnet/Unity compile by resource rule. CPU sample was `100`, with active `csc.exe` PID `4764` and `dotnet.exe` PID `34204`.

## 2026-05-28 - MODDING_SDK_AUDIT - Pass 50 - Schema 74 One-Command Starter Prepare Tool

What was wrong:
- The copied starter kit still required public authors to run identity setup, validation, and review manifest generation as separate commands in the correct order.
- Public tools spawned nested `powershell`, which is a Windows-specific child shell and a bad portability assumption for authors using PowerShell 7 as `pwsh` on macOS/Linux.

What was done:
- Added `ModdingSDK/ExternalStarterKit/Tools/prepare_mod.ps1`.
- Updated `Tools/build_review_manifest.ps1` and `Tools/set_mod_identity.ps1` to chain local scripts in-process.
- Updated `Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs` to generate the prepare tool and the in-process chaining scripts.
- Updated starter README, Tools README, external starter contract, runtime playbook, spec, authoring plan, product blueprint, schema revision 74, and static validator proof.
- Regenerated `ModdingSDK/ExternalStarterKit/Reports/review_manifest.json`; it now hashes 23 files and includes `Tools/prepare_mod.ps1`.

Cinematic Cheats used:
- None in runtime. Authoring cheat: one deterministic command produces the same proof artifact instead of relying on human command ordering.
- Runtime remains envelope-only; no managed DLL execution, loose AssetBundle runtime authority, loose PNG authority, or localization injection right was added.

Exact Microseconds saved:
- Runtime: 0 us/frame claimed. No gameplay tick, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job path changed.
- Authoring cold path: exact runtime microseconds not applicable. The practical saving is fewer invalid review submissions and no Windows-only nested shell dependency in copied kits.

Verification:
- PASS: starter validator -> `PASS HECTON-8 external starter structure`.
- PASS: temp-copy `prepare_mod.ps1` -> `PASS HECTON-8 starter prepared: com.validation.prepared`; generated review manifest included `Tools/prepare_mod.ps1`.
- PASS: review manifest builder -> `PASS HECTON-8 review manifest: Reports/review_manifest.json`.
- PASS: review manifest parsed with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, 23 hashed files, prepare tool included, output folders excluded.
- PASS: static validator -> schema revision 74 with `ExternalStarterKitWritesPrepareTool=True`, `ExternalStarterKitToolsAvoidNestedPowerShell=True`, `ExternalStarterKitPrepareToolPasses=True`.
- PASS: stale schema-73 scan, nested `& powershell` scan, scoped `git diff --check`, trailing whitespace scan, and editor C# ASCII scan.
- DEFERRED: dotnet/Unity compile by resource rule. CPU sample was `100`, with active `csc.exe` PID `14520` and `dotnet.exe` PID `17744`.

## Pass 51 - Schema 75 Starter Tool Path Portability And Exact Editor Mapping

What was wrong:
- Public docs told copied starter-kit authors to use `pwsh` on macOS/Linux, but internal tool lookup used Windows backslash child paths. That is not a proven portable workflow.
- `Tools/validate_structure.ps1` accepted `.vscode/settings.json` when `json.schemas` merely existed; exact schema URL/fileMatch pairs could be broken and still pass local validation.

What was done:
- Added `Join-StarterPath` to all public starter scripts and to SDK Hub script generation.
- Changed prepare/identity/review/validator paths to normalize separators and compose child paths through `Join-Path` segments.
- Tightened the local validator to require exact mappings for authoring manifest, runtime manifest, graph, assets, settings table, and locale files.
- Updated schema revision 75, static validator proof, runtime playbook, README, spec, authoring plan, product blueprint, external starter contract, and starter README/tool README.
- Regenerated `ModdingSDK/ExternalStarterKit/Reports/review_manifest.json`.

Cinematic Cheats used:
- None in runtime. Authoring cheat: text-folder + deterministic script validation stays cheaper than forcing Unity/Workbench just to catch file path and editor mapping defects.
- Runtime remains envelope-only; no managed DLL execution, loose AssetBundle runtime authority, loose PNG authority, localization injection right, or new hot lane was added.

Exact Microseconds saved:
- Runtime: 0 us/frame claimed. No gameplay tick, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job path changed.
- Authoring cold path: exact runtime microseconds not applicable. The practical saving is preventing copied-kit false passes and platform-specific script failure before review.

Verification:
- PASS: starter validator -> `PASS HECTON-8 external starter structure`.
- PASS: review manifest builder -> `PASS HECTON-8 review manifest: Reports/review_manifest.json`.
- PASS: temp-copy broken `.vscode/settings.json` fileMatch probe was rejected with missing schema mapping for `./Schemas/h8mod.authoring.schema.json -> /mod.h8manifest.json`.
- PASS: static validator -> schema revision 75 with `ExternalStarterKitToolsUsePortableJoinPath=True` and `ExternalStarterKitValidatorChecksEditorSchemaMappings=True`.
- PASS: `Signal_Schema.json` parsed with schema revision 75 and matching sdkAuthoringAudit/staticValidation snapshot flags.
- PASS: stale schema-74 scan, public-tool backslash child path scan, scoped `git diff --check`, trailing whitespace scan, and editor C# ASCII scan.
- DEFERRED: dotnet/Unity compile by resource rule. CPU sample was `97.29`, with active `dotnet.exe` PID `17744`; no `Temp/UnityLockfile` existed at sample time.

## 2026-05-28 - MODDING_SDK_AUDIT - Pass 52 - Schema 76 Bounded Review Manifest

What was wrong:
- The external starter review manifest builder validated structure first, but source hashing itself had no explicit count or byte ceiling.
- A random public author could accidentally drop a bulk binary/source folder into a copied kit and get a slow or harmful no-Unity review pass instead of a precise fail-fast error.

What was done:
- Added review source limits to `ModdingSDK/ExternalStarterKit/Tools/build_review_manifest.ps1`: `256` files, `4194304` bytes per file, `33554432` total bytes.
- Added the same limits to `Assets/_Project/Scripts/Editor/ModdingSDK/ModdingSdkHubWindow.cs` so SDK Hub generated kits match the versioned template.
- `Reports/review_manifest.json` now records `TotalBytes` and `Limits`.
- Extended `Docs/Modding/Validate_Mod_API_Static.ps1` with generated-limit checks and a temp-copy oversized-file rejection probe.
- Updated schema revision 76 plus README/spec/authoring plan/product blueprint/runtime playbook/external starter contract/starter README/tool README wording.
- Regenerated `ModdingSDK/ExternalStarterKit/Reports/review_manifest.json`.

Cinematic Cheats used:
- None in runtime. Authoring cheat: fail-fast source byte limits are cheaper and more predictable than letting a review/report tool behave like a general bulk package scanner.
- Runtime remains envelope-only; no managed DLL execution, loose AssetBundle runtime authority, loose PNG authority, localization injection right, or new hot lane was added.

Exact Microseconds saved:
- Runtime: 0 us/frame claimed. No gameplay tick, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job path changed.
- Authoring cold path: exact runtime microseconds not applicable. The practical saving is bounding worst-case local report generation and rejecting accidental bulk files before hashing.

Verification:
- PASS: starter validator -> `PASS HECTON-8 external starter structure`.
- PASS: review manifest builder -> `PASS HECTON-8 review manifest: Reports/review_manifest.json`.
- PASS: review manifest parsed with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, 23 hashed files, `29729` total bytes, limits `256/4194304/33554432`, build tool included, output folders excluded.
- PASS: manual temp-copy oversized file probe rejected `Content/oversized_review_source.bin` at `4194305` bytes with `Review file exceeds max bytes`.
- PASS: static validator -> schema revision 76 with `ExternalStarterKitReviewManifestHasLimits=True` and `ExternalStarterKitReviewManifestRejectsOversizedFile=True`.
- PASS: `Signal_Schema.json` parsed with schema revision 76 and matching sdkAuthoringAudit/staticValidation snapshot flags.
- PASS: stale schema-75 scan, public limit source scan, scoped `git diff --check`, trailing whitespace scan, and editor C# ASCII scan.
- DEFERRED: dotnet/Unity compile by resource rule. CPU sample was `87`; no active dotnet/csc/MSBuild process and no `Temp/UnityLockfile` existed at sample time.

## 2026-05-28 - MODDING_SDK_AUDIT - Pass 53 - Schema 77 Graph Opcode And Budget Validation

What was wrong:
- The external starter graph schema accepted an `Opcode` string, but the local no-Unity validator did not prove it was in `Reference/allowed_opcodes.csv`.
- `Graphs/main.h8graph.json` node IDs could be missing/duplicated and graph `MaxEnvelopesPerFrame` could drift above the authoring manifest budget before later tooling caught it.
- That was bad UX for random public authors: a copied kit could produce a review manifest while carrying an impossible graph.

What was done:
- Added `Read-AllowedGraphOpcodeTokens()` to the versioned starter validator and SDK Hub generated validator.
- The validator now accepts CSV hex tokens and first-word comment aliases, rejects invalid CSV tokens, missing/duplicate node IDs, missing opcodes, unsupported opcodes, and graph budget drift.
- Tightened `Schemas/h8graph.schema.json` so graph node objects require `Id` and `Opcode` and constrain opcode shape.
- Updated starter README/tool README, SDK Hub generated text, schema revision 77, static validator proof, runtime playbook, README, spec, authoring plan, product blueprint, and external starter contract.
- Regenerated `ModdingSDK/ExternalStarterKit/Reports/review_manifest.json`.

Cinematic Cheats used:
- None in runtime. Authoring cheat: local fail-fast CSV validation is cheaper and clearer than forcing Unity/Workbench just to discover unsupported graph opcodes.
- Runtime remains envelope-only; no managed DLL execution, loose AssetBundle runtime authority, loose PNG authority, localization injection right, or new hot lane was added.

Exact Microseconds saved:
- Runtime: 0 us/frame claimed. No gameplay tick, FutureCommandEnvelope validation, SignalBus, NativeQueue, GlobalDataVault, save, physics, rendering, or Burst/job path changed.
- Authoring cold path: exact runtime microseconds not applicable. The practical saving is preventing invalid graph review handoff before package/runtime tooling.

Verification:
- PASS: starter validator -> `PASS HECTON-8 external starter structure`.
- PASS: review manifest builder -> `PASS HECTON-8 review manifest: Reports/review_manifest.json`.
- PASS: review manifest parsed with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, 23 hashed files, `32609` total bytes, limits `256/4194304/33554432`, validator included, graph schema included.
- PASS: manual temp-copy graph probe with `Opcode = SpawnItem` and graph/manifest budget `1` passed local validation.
- PASS: manual temp-copy graph probe with `Opcode = DefinitelyNotAllowed` failed with `node Opcode is not in Reference/allowed_opcodes.csv`.
- PASS: static validator -> schema revision 77 with `ExternalStarterKitValidatorChecksGraphOpcodes=True`, `ExternalStarterKitValidatorChecksGraphBudget=True`, `ExternalStarterKitValidatorRejectsInvalidGraphOpcode=True`.
- PASS: `Signal_Schema.json` parsed with schema revision 77 and matching sdkAuthoringAudit/staticValidation snapshot flags.
- PASS: stale schema-76 scan, scoped `git diff --check`, trailing whitespace scan, and editor C# ASCII scan.
- DEFERRED: dotnet/Unity compile by resource rule. CPU sample was `60`, with active `dotnet.exe` PID `43436`; no `Temp/UnityLockfile` existed at sample time.
## Pass 54 - Schema 78 Starter Opcode Discovery Helper

What was wrong:
- The copied starter kit could reject unsupported graph opcodes but did not give public authors a direct no-Unity command to list valid graph opcode aliases and hex tokens.
- Authors had to infer alias usage from raw CSV comments, which is not an acceptable SDK usability path for random external modders.

What was done:
- Added `Tools/list_allowed_opcodes.ps1` to the versioned external starter kit and SDK Hub generator.
- The helper reads `Reference/allowed_opcodes.csv`, rejects malformed/duplicate rows, prints alias/hash pairs, and supports `-Json` output with schema `hecton8.allowed_graph_opcodes.v1`.
- Made `Tools/validate_structure.ps1` require the helper and made the review manifest include it.
- Updated starter README, Tools README, Reference README, SDK authoring docs, product blueprint, external starter contract, runtime playbook, static validator, and `Signal_Schema.json` schema revision 78.

Cinematic Cheats used:
- None in runtime. This is a cold authoring helper. The performance cheat is avoiding Unity/Workbench startup for a pure allowlist read.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Authoring path: avoids manual CSV inspection and invalid graph review loops; no runtime measurement claimed.

Verification:
- PASS: list helper text output printed 8 opcode aliases/hashes.
- PASS: list helper JSON output reported schema `hecton8.allowed_graph_opcodes.v1`, runtime `envelope-only`, count `8`.
- PASS: local starter validator.
- PASS: review manifest builder with `24` source files and `Tools/list_allowed_opcodes.ps1` included.
- PASS: duplicate-opcode temp probe rejected.
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` schema revision `78`.
- PASS: `git diff --check`, touched-file whitespace scan, editor C# ASCII scan.
- DEFERRED: dotnet/Unity compile by resource gate: CPU `97`, active `dotnet.exe` PID `43436`, no Unity lock.

## Pass 55 - Schema 79 Starter Manifest Identity Version Contract

What was wrong:
- Public starter manifests had one enforced identity route for `Id`, but `DisplayName`/`Name`, `Author`, and `Version` could drift between `mod.h8manifest.json` and `mod.json`.
- Starter `Version` accepted arbitrary non-empty text, which is not a stable package version contract for public review, future Workbench, or package diff tooling.

What was done:
- Enforced semantic package versions and identity text parity in the copied starter local tools and SDK Hub generated toolchain.
- Updated public docs and runtime proof records so external authors see the real no-Unity contract: `Tools/set_mod_identity.ps1`, `Tools/validate_structure.ps1`, semantic versions, and authoring/runtime manifest parity.
- Regenerated `Reports/review_manifest.json` after restoring generic starter template identity defaults.

Cinematic cheats used:
- None. This is cold authoring/tooling and documentation. Runtime simulation, rendering, physics, FutureCommandEnvelope layout, and SignalBus lanes are unchanged.

Exact microseconds saved:
- Runtime: `0 us/frame`.
- Offline: prevents review/build churn before runtime by rejecting bad versions and manifest drift locally.

Verification:
- `ModdingSDK/ExternalStarterKit/Tools/validate_structure.ps1` PASS.
- `ModdingSDK/ExternalStarterKit/Tools/build_review_manifest.ps1` PASS.
- Temp valid identity probe PASS with parity across both manifests.
- Temp invalid version probe rejected `bad version`.
- `Docs/Modding/Validate_Mod_API_Static.ps1` PASS, schema revision `79`, semver/parity/invalid-version flags true.
- `Signal_Schema.json` parsed schema revision `79`.
- `Reports/review_manifest.json`: schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, `24` files, `39823` total bytes.
- Stale schema-78 scan: no current-text hits in `Docs/Modding`.
- `git diff --check`: PASS with line-ending warnings only.
- Trailing whitespace scan: PASS.
- Editor C# ASCII scan: PASS.
- Resource gate before compile: CPU `31`, no active `dotnet/csc/VBCSCompiler/MSBuild`, Unity lockfile absent.
- `dotnet build Assembly-CSharp.csproj -v:minimal`: PASS, `0 Error(s)`, `19 Warning(s)`. Warnings are pre-existing third-party/demo unused-field and type-conflict warnings under `Assets/Feel/MMTools/Demos/` and `Assets/Candice AI for Games/`.
- Unity batchmode compile was not run after the successful C# project build.

## Pass 56 - Schema 80 Review Manifest Identity Summary

What was wrong:
- `Reports/review_manifest.json` proved source hashes but was not self-describing enough for review handoff. It had `RootId`, but not display name, author, semantic version, required API version, or mod priority.
- Future Workbench/CLI/reviewer flow would need to open both manifests again for basic identity, despite the validator already proving parity.

What was done:
- Added an `Identity` object to `Tools/build_review_manifest.ps1` output.
- Updated the SDK Hub generated review-manifest script and starter documentation to describe identity/file/hash reports.
- Bumped `Signal_Schema.json` to schema revision `80` and extended `Validate_Mod_API_Static.ps1` to prove review manifest identity exists and matches the validated manifests.
- Regenerated `ModdingSDK/ExternalStarterKit/Reports/review_manifest.json`.

Cinematic cheats used:
- None. This is cold authoring/report metadata. Runtime simulation, rendering, physics, FutureCommandEnvelope layout, SignalBus lanes, and loader authority are unchanged.

Exact microseconds saved:
- Runtime: `0 us/frame`.
- Offline: removes duplicate manifest inspection from review/Workbench/CLI paths; no runtime timing claimed.

Verification:
- Starter review manifest builder PASS.
- Starter structure validator PASS.
- Static validator PASS, schema revision `80`, review identity flags true.
- Review manifest parsed: `Identity.Id=com.example.starter`, `DisplayName=Starter Mod`, `Author=YourName`, `Version=0.1.0`, `RequiredAPIVersion=2`, `ModPriority=0`, `FileCount=24`, `TotalBytes=40301`.
- `Signal_Schema.json` parsed schema revision `80`.
- Stale schema-79 scan: no current-text hits in `Docs/Modding`.
- `git diff --check`: PASS with line-ending warnings only.
- Trailing whitespace scan: PASS.
- Editor C# ASCII scan: PASS.
- Compile deferred: CPU `96-99`, active `dotnet.exe` PID `33312`; Unity lockfile absent.

## Pass 57 - Schema 81 Public Starter Priority And Legacy Builder Gate

What was wrong:
- The SDK Hub led with `Open Mod Builder`, while the actual public authoring route is the no-Unity External Starter Kit.
- `ModBuilderWindow` still exposed a direct top-level `Hecton/Modding/Mod Builder` menu even though DLL/AssetBundle package building is legacy/internal under envelope-only runtime policy.

What was done:
- Reordered SDK Hub authoring so `Create External Starter Kit` and `Open External Starter Kit` are first.
- Moved builder access under `Internal Legacy`, added an explicit warning, and required `EditorUtility.DisplayDialog` confirmation before opening it.
- Moved builder menu to `Hecton/Modding/Internal/Legacy Mod Builder`.
- Renamed builder title/action/help/log text to internal legacy wording.
- Updated schema revision 81, static validator, runtime playbook, README, API spec, SDK authoring plan, product blueprint, and loader/save audit.

Cinematic cheats used:
- UX route fake instead of runtime system expansion: no managed runtime modding reopened, no AssetBundle/localization/PNG ingress enabled, no new package loader behavior.

Exact microseconds saved:
- Runtime: `0 us/frame`.
- Offline: prevents public authors from entering legacy runtime packaging as their first SDK action; no runtime timing claimed.

Verification:
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` returned `Status=PASS`, `SchemaRevision=81`, `ModdingSdkHubPrioritizesExternalStarterKit=True`, `ModdingSdkHubGatesLegacyBuilder=True`, `ModBuilderMenuIsInternalLegacy=True`.
- PASS: scoped `git diff --check` for touched source/docs; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- PASS: editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` because CPU was `99` at the compile gate.

## Pass 58 - Schema 82 Prepare Existing Manifest Loop

What was wrong:
- `Tools/prepare_mod.ps1` was the advertised one-command public path, but it required identity arguments every run.
- After first setup, random external authors need a plain edit-review command: validate existing manifests and rebuild `Reports/review_manifest.json` without risking identity churn.

What was done:
- Made `prepare_mod.ps1` two-mode. With `-Id`, it runs identity setup and review generation. Without identity arguments, it validates existing manifests through the review builder and reports package id from the generated review manifest.
- Rejected partial identity edits without `-Id` so command intent stays explicit.
- Updated the SDK Hub starter generator, checked-in starter README/tools docs, public modding docs, runtime playbook, `Signal_Schema.json` schema revision 82, and static validator proof.
- Static validator now proves both prepare modes on a temp copy and records `ExternalStarterKitPrepareToolSupportsExistingManifest=True`.

Cinematic cheats used:
- UX/tooling shortcut instead of runtime expansion: no managed DLL execution, Harmony/BepInEx path, loose AssetBundle/PNG/localization ingress, or package loader behavior was enabled.

Exact microseconds saved:
- Runtime: `0 us/frame`.
- Offline: removes repeated manual identity arguments and separate validate/review commands from the normal edit-review loop; no runtime timing claimed.

Verification:
- PASS: real starter `prepare_mod.ps1` without `-Id` validated structure, rebuilt `Reports/review_manifest.json`, and reported `com.example.starter`.
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` returned `Status=PASS`, `SchemaRevision=82`, `ExternalStarterKitPrepareToolSupportsExistingManifest=True`.
- PASS: scoped `git diff --check` for touched source/docs; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- PASS: editor C# ASCII scan.
- PASS: stale schema-81 current-text scan found no current revision drift.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` because the latest resource gate still showed CPU `73` and active `dotnet.exe` PID `34196`; the earlier sample showed CPU `100` with active `csc.exe` PID `59224` and active `dotnet.exe` PID `13688`.

## Pass 59 - Schema 83 External Starter Kit Workbench

What was wrong:
- The public starter kit and no-Unity tools existed, but Unity-side modders had no single cockpit for starter creation/refresh, identity, validation, opcode discovery, file access, and review summary.
- A direct Workbench menu without create/refresh would still leave first-time users dependent on the SDK Hub generator.

What was done:
- Added `ExternalStarterKitWorkbenchWindow` at `Hecton/Modding/External Starter Kit Workbench`.
- Integrated SDK Hub public authoring with an `Open Starter Kit Workbench` action.
- Reused `ModdingSdkHubWindow.CreateExternalStarterKit()` from the Workbench, so starter generation has one source of truth.
- Routed Workbench identity edits through `Tools/set_mod_identity.ps1`, validation/review through `Tools/prepare_mod.ps1`, opcode discovery through `Tools/list_allowed_opcodes.ps1`, and review display through `Reports/review_manifest.json`.
- Updated schema revision 83, static validator, runtime playbook, README, API spec, authoring plan, product blueprint, starter file contract, starter README, and review manifest.

Cinematic cheats used:
- Editor UX/workflow consolidation instead of runtime expansion. No managed DLL execution, Harmony/BepInEx patching, loose AssetBundle/PNG/localization runtime ingress, loader bypass, or hot-path event route was enabled.

Exact microseconds saved:
- Runtime: `0 us/frame`.
- Offline: removes file explorer/script/docs jumping for Unity-side authors; no runtime timing claimed.

Verification:
- PASS: `ModdingSDK/ExternalStarterKit/Tools/prepare_mod.ps1` validated structure, rebuilt `Reports/review_manifest.json`, and reported `com.example.starter`.
- PASS: `Reports/review_manifest.json` parsed with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, `24` files, `41781` total bytes.
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` returned `Status=PASS`, `SchemaRevision=83`, `ModdingSdkHubOpensStarterWorkbench=True`, `ExternalStarterKitWorkbenchPresent=True`, `ExternalStarterKitWorkbenchCanRefreshStarterKit=True`, `ExternalStarterKitWorkbenchUsesIdentityTool=True`, `ExternalStarterKitWorkbenchUsesPrepareTool=True`, `ExternalStarterKitWorkbenchListsOpcodes=True`, `ExternalStarterKitWorkbenchShowsReviewSummary=True`, and `ExternalStarterKitWorkbenchShowsEnvelopeBoundary=True`.
- PASS: `Signal_Schema.json` parsed at schema revision 83 with matching Workbench flags.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan and editor C# ASCII scan.
- PASS: stale schema-82/future-Workbench text scan found no blocked stale text.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` because the latest resource gate showed CPU `100` and active `dotnet.exe` PID `16780`; an earlier sample showed active `dotnet.exe` PID `53704`.

## Pass 60 - Schema 84 Workbench Starter Health

What was wrong:
- The new Workbench could run prepare/review tools, but it did not show whether the copied starter kit was structurally complete before a user ran a script.
- The fastest validation route, `Tools/validate_structure.ps1`, was not exposed directly from the Workbench.
- Core contract discovery still depended on the SDK Hub or docs navigation instead of the active starter-kit screen.

What was done:
- Added a required starter-file health panel to `ExternalStarterKitWorkbenchWindow`.
- Health now counts required files, missing files, total bytes, newest starter write time, and lists `OK`/`MISSING` per required file.
- Added a `Validate Structure Only` action that calls `Tools/validate_structure.ps1`.
- Added Workbench buttons for the external starter file contract, API spec, authoring plan, and runtime playbook.
- Updated schema revision 84, static validator, runtime playbook, README, API spec, SDK authoring plan, product blueprint, starter file contract, starter README, and regenerated `Reports/review_manifest.json`.

Cinematic cheats used:
- Editor-facing health summary and direct script reuse instead of a second validator or runtime feature expansion. No managed DLL execution, Harmony/BepInEx patching, loose AssetBundle/PNG/localization runtime ingress, loader bypass, or hot-path event route was enabled.

Exact microseconds saved:
- Runtime: `0 us/frame`.
- Offline: catches missing starter files and structure errors before review handoff; no runtime timing claimed.

Verification:
- PASS: `ModdingSDK/ExternalStarterKit/Tools/prepare_mod.ps1` validated structure, rebuilt `Reports/review_manifest.json`, and reported `com.example.starter`.
- PASS: `Reports/review_manifest.json` parsed with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, `24` files, `41843` total bytes.
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` returned `Status=PASS`, `SchemaRevision=84`, `ExternalStarterKitWorkbenchShowsStarterHealth=True`, `ExternalStarterKitWorkbenchRunsStructureValidator=True`, and `ExternalStarterKitWorkbenchLinksCoreDocs=True`.
- PASS: `Signal_Schema.json` parsed at schema revision 84.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` because the resource gate showed CPU `100` and active `dotnet.exe` PID `55080`; no `Temp/UnityLockfile` was present.

## Pass 61 - Schema 85 Workbench Async Tool Runner

What was wrong:
- `ExternalStarterKitWorkbenchWindow` launched public starter tools synchronously from the EditorWindow path.
- It read stdout and stderr with blocking `ReadToEnd` and then called `WaitForExit`, so the Unity Editor could freeze during validation and the process could deadlock if output pipes filled.

What was done:
- Replaced blocking process waits with an async Editor-only runner.
- The Workbench now starts PowerShell/pwsh tools, reads stdout/stderr via `BeginOutputReadLine` and `BeginErrorReadLine`, disables Workbench action buttons while a tool is active, and finalizes summary/reload through `EditorApplication.update`.
- Static validator now rejects regressions to `StandardOutput.ReadToEnd` or `WaitForExit`.
- Updated schema revision 85, runtime playbook, README, API spec, SDK authoring plan, product blueprint, starter file contract, starter README, and regenerated `Reports/review_manifest.json`.

Cinematic cheats used:
- Editor responsiveness fix through async process IO instead of a runtime feature. No managed DLL execution, Harmony/BepInEx patching, loose AssetBundle/PNG/localization runtime ingress, loader bypass, or hot-path event route was enabled.

Exact microseconds saved:
- Runtime: `0 us/frame`.
- Editor/offline: removes blocking process waits from the Workbench path; no runtime timing claimed.

Verification:
- PASS: `ModdingSDK/ExternalStarterKit/Tools/prepare_mod.ps1` validated structure, rebuilt `Reports/review_manifest.json`, and reported `com.example.starter`.
- PASS: `Reports/review_manifest.json` parsed with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, `24` files, `41858` total bytes.
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` returned `Status=PASS`, `SchemaRevision=85`, and `ExternalStarterKitWorkbenchRunsToolsAsync=True`.
- PASS: `Signal_Schema.json` parsed at schema revision 85.
- PASS: source scan found `BeginOutputReadLine`, `BeginErrorReadLine`, and `EditorApplication.update` in `ExternalStarterKitWorkbenchWindow.cs`, with no remaining `ReadToEnd` or `WaitForExit`.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` because the resource gate showed CPU `100`, active `dotnet.exe` PID `55080`, and active `csc.exe` PID `8756`; no `Temp/UnityLockfile` was present.

## Pass 62 - Schema 86 Workbench Review Freshness

What was wrong:
- The Workbench showed `Reports/review_manifest.json`, but did not say whether the report was stale after graph/table/locale/source edits.
- A modder could submit an old review report while the Workbench still displayed a valid-looking summary.

What was done:
- Added `Review Freshness` to `ExternalStarterKitWorkbenchWindow`.
- The Workbench compares report write time against the newest starter source file, excludes `Generated/` and `Reports/`, caps the scan at `512` source files, and warns on stale or capped freshness checks.
- Updated schema revision 86, static validator, runtime playbook, README, API spec, SDK authoring plan, product blueprint, starter file contract, starter README, and regenerated `Reports/review_manifest.json`.

Cinematic cheats used:
- Timestamp-based editor freshness check instead of eager per-reload hashing. The review builder remains the hash authority; runtime remains envelope-only.

Exact microseconds saved:
- Runtime: `0 us/frame`.
- Editor/offline: avoids stale review handoff without hashing every starter source file on every repaint; no runtime timing claimed.

Verification:
- PASS: `ModdingSDK/ExternalStarterKit/Tools/prepare_mod.ps1` validated structure, rebuilt `Reports/review_manifest.json`, and reported `com.example.starter`.
- PASS: `Reports/review_manifest.json` parsed with schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, root id `com.example.starter`, `24` files, `41885` total bytes.
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` returned `Status=PASS`, `SchemaRevision=86`, and `ExternalStarterKitWorkbenchShowsReviewFreshness=True`.
- PASS: `Signal_Schema.json` parsed at schema revision 86.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan and editor C# ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` because the resource gate showed CPU `100` and active `dotnet.exe` PID `29008`; no `Temp/UnityLockfile` was present.
## 2026-05-28 - Pass 63 - Schema 87 Root No-Unity Launcher

What was wrong:
- External Starter Kit had valid no-Unity tools, but no single root command for random public authors. The first copied-folder workflow still depended on README command copying.
- Workbench health did not require the public root launcher because the file did not exist.

What was done:
- Added `ModdingSDK/ExternalStarterKit/h8mod.ps1` with `menu`, `setup`, `validate`, `review`, `prepare`, `opcodes`, and `opcodes-json` actions.
- Launcher delegates to existing `Tools/*.ps1` scripts; no second validator, no second package contract, no runtime ingress change.
- SDK Hub generator now writes `h8mod.ps1`; Workbench required-file health includes it and exposes `Open Root Launcher`; local starter validator requires it.
- Updated `Signal_Schema.json` to revision `87`, static validator root-launcher gates, modding docs, starter README/tool README, and review manifest.

Cinematic Cheats used:
- None. This is SDK/editor/offline authoring surface only.

Exact Microseconds saved:
- Runtime frame: `0 us/frame` measured/claimed. No gameplay, render, physics, save, SignalBus, EventBus, NativeQueue, GlobalDataVault, or Burst/job path changed.
- Authoring friction: one root action replaces raw multi-command lookup; not a frame-time metric.

Verification:
- PASS: `h8mod.ps1 -Action validate`.
- PASS: `h8mod.ps1 -Action opcodes`.
- PASS: `h8mod.ps1 -Action opcodes-json`.
- PASS: `h8mod.ps1 -Action prepare`.
- PASS: `h8mod.ps1 -Action review`.
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `87`.
- PASS: review manifest parse -> `25` files, `47352` bytes, `h8mod.ps1` included.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- PASS: editor C# ASCII scan.
- DEFERRED: dotnet/Unity compile by resource gate. Latest sample: CPU `97`, active `dotnet.exe` PID `15108`; earlier sample: CPU `78`, no active build process.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT. No Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 65 - Schema 89 Workbench Required-File List Parity

What was wrong:
- `ExternalStarterKitWorkbenchWindow.RequiredStarterFiles` drifted from `Tools/validate_structure.ps1`.
- The Workbench checked stale `Schemas/h8table.schema.json` and `Schemas/h8loc.schema.json`, but the actual starter uses `Schemas/settings_table.schema.json`, `Schemas/locale.schema.json`, and `Schemas/assets.schema.json`.
- It also omitted validator-required files such as folder READMEs and `Tools/README.md`, so valid starter folders could show false missing-file health.

What was done:
- Aligned the Workbench required-file health list with the starter validator file contract.
- Added `starterWorkbenchRequiredFileListMatchesValidator` and `externalStarterKitWorkbenchRequiredFileListMatchesValidator` to `Signal_Schema.json` schema revision `89`.
- Extended `Validate_Mod_API_Static.ps1` so stale Workbench schema names or missing validator-required health entries fail the static gate.
- Updated README, API spec, runtime playbook, SDK authoring plan, product blueprint, and external starter file contract.

Cinematic Cheats used:
- Editor/offline UX contract fix only. No runtime ingress, no managed DLL enablement, no Harmony/BepInEx path, no loose AssetBundle/PNG/localization route, and no hot signal path changed.

Exact Microseconds saved:
- Runtime frame: `0 us/frame` measured/claimed.
- Editor/offline: prevents false Workbench health errors before validation; no frame-time metric claimed.

Verification:
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `89`, `ExternalStarterKitWorkbenchRequiredFileListMatchesValidator=True`.
- PASS: `Signal_Schema.json` parse -> `schemaRevision=89`, `starterWorkbenchRequiredFileListMatchesValidator=True`, snapshot `externalStarterKitWorkbenchRequiredFileListMatchesValidator=True`.
- PASS: `h8mod.ps1 -Action validate`.
- PASS: `h8mod.ps1 -Action prepare`.
- PASS: `Reports/review_manifest.json` parse -> schema `hecton8.external_review_manifest.v1`, id `com.example.starter`, `25` files, `47352` bytes, root launcher included.
- PASS: stale schema-88 scan.
- PASS: stale `h8table.schema.json`/`h8loc.schema.json` scan found hits only in negative static-validator assertions.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- PASS: touched-file non-ASCII scan.
- DEFERRED: dotnet/Unity compile by resource gate. Samples: CPU `100`, active `csc.exe` PID `12916`, active `dotnet.exe` PID `42716`, no Unity lock.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT. No Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 64 - Schema 88 SDK Hub Async Validator

What was wrong:
- `ModdingSdkHubWindow.RunStaticValidator` used blocking `StandardOutput.ReadToEnd`, `StandardError.ReadToEnd`, and `WaitForExit` from the EditorWindow path.
- That made the public SDK Hub capable of freezing Unity Editor or deadlocking on validator pipe output, while Workbench starter tools already used async process IO.

What was done:
- Replaced the SDK Hub validator launch with async stdout/stderr reads, active-process button disable, completion polling through `EditorApplication.update`, and cleanup on `OnDisable`.
- Added `ModdingSdkHubRunsStaticValidatorAsync` to `Signal_Schema.json` schema revision `88`.
- Extended `Validate_Mod_API_Static.ps1` so it fails if SDK Hub regresses to blocking `ReadToEnd` or `WaitForExit`.
- Updated README, API spec, runtime playbook, SDK authoring plan, and product blueprint.

Cinematic Cheats used:
- Editor/offline responsiveness fix only. No managed DLL execution, Harmony/BepInEx patching, loose AssetBundle/PNG/localization runtime ingress, loader bypass, or hot-path event route was enabled.

Exact Microseconds saved:
- Runtime frame: `0 us/frame` measured/claimed. No gameplay, render, physics, save, SignalBus, HectonEventBus, NativeQueue, GlobalDataVault, or Burst/job path changed.
- Editor/offline: removes blocking process waits from the SDK Hub validator button; no frame-time metric claimed.

Verification:
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `88`, `ModdingSdkHubRunsStaticValidatorAsync=True`.
- PASS: `Signal_Schema.json` parse -> `schemaRevision=88`, `hubRunsStaticValidatorAsync=True`, snapshot `moddingSdkHubRunsStaticValidatorAsync=True`.
- PASS: source scan found no `ReadToEnd` or `WaitForExit()` in `ModdingSdkHubWindow.cs` or `ExternalStarterKitWorkbenchWindow.cs`.
- PASS: `h8mod.ps1 -Action validate`.
- PASS: `h8mod.ps1 -Action prepare`.
- PASS: stale schema-87 scan.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- PASS: editor C# ASCII scan.
- DEFERRED: dotnet/Unity compile by resource gate. Samples: CPU `91`, then CPU `95` after delayed retry; no active build process, no Unity lock.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT. No Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 66 - Schema 90 SDK Tool Failure Error UI

What was wrong:
- SDK Hub and External Starter Kit Workbench had async tool execution, but failed validator/tool exits were still displayed as normal info help boxes.
- A random mod author could miss the severity of a failed structure validator, failed prepare, missing tool, or failed static validator because the UI color/state did not distinguish success from failure.

What was done:
- Added `_lastValidatorFailed` to `ModdingSdkHubWindow` and render failed validator summaries with `MessageType.Error`.
- Added `_toolSummaryIsError` to `ExternalStarterKitWorkbenchWindow` and render failed starter tool summaries with `MessageType.Error`.
- Marked missing scripts, launch failures, process-start failures, and nonzero exit codes as failures; kept running/already-running/successful states informational.
- Bumped `Signal_Schema.json` to schema revision `90`.
- Extended `Validate_Mod_API_Static.ps1` with source/schema/runtime-playbook gates for `ModdingSdkHubShowsValidatorFailuresAsErrors` and `ExternalStarterKitWorkbenchShowsToolFailuresAsErrors`.
- Updated README, API spec, runtime playbook, SDK authoring plan, product blueprint, and external starter file contract.

Cinematic Cheats used:
- Editor/offline UX severity fix only. No runtime ingress, no managed DLL enablement, no Harmony/BepInEx path, no loose AssetBundle/PNG/localization route, no SignalBus hot path, and no gameplay authority route changed.

Exact Microseconds saved:
- Runtime frame: `0 us/frame` measured/claimed.
- Editor/offline: prevents failed tool output from looking successful; no frame-time metric claimed.

Verification:
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `90`, `ModdingSdkHubShowsValidatorFailuresAsErrors=True`, `ExternalStarterKitWorkbenchShowsToolFailuresAsErrors=True`.
- PASS: `Signal_Schema.json` parse -> `schemaRevision=90`, `hubShowsValidatorFailuresAsErrors=True`, `starterWorkbenchShowsToolFailuresAsErrors=True`, snapshot flags true.
- PASS: `h8mod.ps1 -Action validate`.
- PASS: `h8mod.ps1 -Action prepare`.
- PASS: source scan found `_lastValidatorFailed`, `_toolSummaryIsError`, `MessageType.Error`, and `exitCode != 0`.
- PASS: stale `89` scan found only review-manifest SHA/byte substrings and `.meta` GUID, not stale schema text.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- PASS: touched-file non-ASCII scan.
- PASS: `dotnet build Assembly-CSharp.csproj -v:minimal` -> `0 Warning(s)`, `0 Error(s)`.
- NOT RUN: Unity MCP/Editor console verification because Unity MCP tools are not available in this session.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT / DOTNET_COMPILE. No Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 73 - Schema 97 Settings And Locale Snippet Authoring

What was wrong:
- Settings and locale data had validation and Workbench preview, but a public author still had to hand-author new row/entry JSON.
- The starter kit did not provide one low-friction no-Unity route for generating correct settings/locale object shapes.
- A first static run exposed a real contract-marker drift in the Workbench safety text; the UI said `do not mutate`, while the static contract expected `does not mutate` as the safety marker.

What was done:
- Added `ModdingSDK/ExternalStarterKit/Tools/create_settings_row_snippet.ps1`.
- Added `ModdingSDK/ExternalStarterKit/Tools/create_locale_entry_snippet.ps1`.
- Added root launcher actions `setting-snippet` and `locale-snippet`.
- Added Workbench `Authoring Snippets` panel with settings/locale fields, generate buttons, generated-file open buttons, and tool open buttons.
- Updated SDK Hub generator so refreshed starter kits include the new scripts, launcher routes, capability guide text, README/tool README text, and validator required-file/capability checks.
- Updated `Signal_Schema.json` to schema revision `97`.
- Updated `Validate_Mod_API_Static.ps1`, Runtime Playbook, README, API spec, authoring plan, product blueprint, external starter contract, checked-in starter README, starter capability guide, and tools README.
- Regenerated `Reports/review_manifest.json` and `Generated/com.example.starter_submission.zip`.

Cinematic Cheats used:
- Offline Generated-only snippets instead of runtime table mutation, runtime localization repair, managed DLL execution, Harmony/BepInEx patching, loose asset runtime loading, SignalBus hot lanes, or new gameplay authority.

Exact Microseconds saved:
- Runtime: `0 us/frame` measured/claimed.
- Authoring risk removed: malformed settings/locale objects are generated safely and validated before review/submission; no frame-time saving claimed.

Evidence:
- PASS: PowerShell parser scan for changed launcher/tool/static-validator scripts.
- PASS: root `h8mod.ps1 -Action setting-snippet`.
- PASS: root `h8mod.ps1 -Action locale-snippet`.
- PASS: direct JSON settings/locale snippet probes emitted `hecton8.settings_row_snippet.v1` and `hecton8.locale_entry_snippet.v1`.
- PASS: negative probes reject invalid settings default, invalid settings ID, invalid locale key, and empty locale value.
- PASS: starter validate.
- PASS: starter submission package build.
- PASS: static validator schema revision `97`, authoring snippet Workbench/tool/root launcher flags true.
- PASS: review manifest parse: `30` hashed files, `89173` total bytes, settings/locale snippet tools included, no `Generated/` or `Reports/` source entries.
- PASS: zip inspection: `31` entries, required manifests/review manifest/settings snippet tool/locale snippet tool present, no `Generated/*` entry.
- PASS: scoped `git diff --check`, line-ending warnings only.
- PASS: touched-file trailing whitespace scan, editor C# ASCII scan, stale schema-96 text scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal`; build gate saw CPU `99`, then CPU `60` with active `dotnet.exe` PID `35512`; no Unity lockfile.
- NOT RUN: Unity MCP/Editor console verification; Unity MCP tools are unavailable in this session.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT. No DOTNET_COMPILE, Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed for this pass.

## 2026-05-28 - Pass 72 - Schema 96 Capability Matrix And Public Author Route

What was wrong:
- The modding route was safe but not explicit enough for random public authors. The starter kit exposed graph/settings/locale/content/review files, but did not provide one authoritative answer for "what can I mod, what is blocked, and how do I start".
- The Unity Workbench had health, graph, settings/locale, and submission panels, but no capability matrix that connected those files to supported/forbidden rights.
- The root no-Unity launcher had no direct capability discovery action.

What was done:
- Added `ModdingSDK/ExternalStarterKit/Docs/capabilities.md` with supported surfaces, not-public runtime rights, no-Unity workflow, Unity Workbench workflow, and expansion route.
- Added `h8mod.ps1 -Action capabilities` and generator parity in `ModdingSdkHubWindow`.
- Added Capability Matrix to `ExternalStarterKitWorkbenchWindow`: supported surfaces, declared manifest capabilities, allowed opcode counts, budgets, required file state, and forbidden runtime rights.
- Updated checked-in and generated `Tools/validate_structure.ps1` to require capability guide content.
- Updated `Signal_Schema.json` to schema revision `96`.
- Updated `Validate_Mod_API_Static.ps1`, Runtime Playbook, README, API spec, authoring plan, product blueprint, starter file contract, starter README, and tool README.
- Regenerated `Reports/review_manifest.json` and `Generated/com.example.starter_submission.zip`.

Cinematic Cheats used:
- Offline/Editor capability surfacing instead of runtime execution broadening. The engine still owns command execution, hot lanes, asset loading, save authority, and validation.

Exact Microseconds saved:
- Runtime: `0 us/frame` claimed. No runtime loader, FutureCommandEnvelope validation, SignalBus, GlobalRegistry, GlobalDataVault, save, physics, rendering, Burst/job, or quality route was changed.
- Authoring risk removed: public authors now get a single validated capability route before they attempt forbidden runtime paths.

Evidence:
- PASS: starter validate.
- PASS: `h8mod.ps1 -Action capabilities` printed `# HECTON-8 Mod Capability Matrix`.
- PASS: starter submission package build.
- PASS: static validator schema revision `96`, `ExternalStarterKitWorkbenchShowsCapabilityMatrix=True`, `ExternalStarterKitWritesCapabilityGuide=True`, `ExternalStarterKitValidatorChecksCapabilityGuide=True`, `ExternalStarterKitRootLauncherSupportsCapabilities=True`.
- PASS: schema JSON parse: `schemaRevision=96`, `starterWorkbenchShowsCapabilityMatrix=True`, `externalStarterKitWritesCapabilityGuide=True`.
- PASS: review manifest parse: `28` hashed files, `73424` total bytes, `Docs/capabilities.md` included, no `Generated/` or `Reports/` source entries.
- PASS: zip inspection: `29` entries, `Docs/capabilities.md` and `Reports/review_manifest.json` present, no `Generated/*` entry.
- PASS: scoped `git diff --check`, trailing whitespace scan, editor C# ASCII scan, JSON parse scan, stale schema-95 text scan.
- PASS: `dotnet build Assembly-CSharp.csproj -v:minimal`; launched only after gate allowed it with CPU `33`, no active compiler processes, and no Unity lockfile; result `0 Warning(s)`, `0 Error(s)`.
- NOT RUN: Unity MCP/Editor console verification; Unity MCP tools are unavailable in this session.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT / DOTNET_COMPILE. No Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 67 - Schema 91 Workbench Graph Contract Preview

What was wrong:
- The integrated Workbench could open `Graphs/main.h8graph.json` and list allowed opcodes, but did not show whether the current graph was already invalid.
- External authors still had to edit raw JSON and run validation before seeing duplicate IDs, invalid opcode aliases/hex tokens, missing fields, wrong runtime flag, or `MaxEnvelopesPerFrame` budget drift.

What was done:
- Added `Graph Contract Preview` to `ExternalStarterKitWorkbenchWindow`.
- The preview parses `Graphs/main.h8graph.json`, loads and shape-checks `Reference/allowed_opcodes.csv`, compares graph budget to `mod.h8manifest.json`, caps preview work at `256` nodes, `1 MB` graph/CSV files, and `512` opcode rows, and reports runtime flag, node count, invalid opcodes, duplicate node IDs, missing/invalid fields, and budget errors.
- Bumped `Signal_Schema.json` to schema revision `91`.
- Extended `Validate_Mod_API_Static.ps1` with source/schema/runtime-playbook gates for `ExternalStarterKitWorkbenchShowsGraphContractPreview`.
- Updated README, API spec, runtime playbook, SDK authoring plan, product blueprint, and external starter file contract.

Cinematic Cheats used:
- Editor/offline authoring preview only. No runtime graph compiler, managed DLL execution, Harmony/BepInEx patching, loose AssetBundle/PNG/localization ingress, SignalBus hot path, or gameplay authority route was enabled.

Exact Microseconds saved:
- Runtime frame: `0 us/frame` measured/claimed.
- Editor/offline: prevents graph-contract mistakes from advancing to review/runtime handoff; no frame-time metric claimed.

Verification:
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `91`, `ExternalStarterKitWorkbenchShowsGraphContractPreview=True`.
- PASS: `Signal_Schema.json` parse -> `schemaRevision=91`, `starterWorkbenchShowsGraphContractPreview=True`, snapshot `externalStarterKitWorkbenchShowsGraphContractPreview=True`.
- PASS: `h8mod.ps1 -Action validate`.
- PASS: `h8mod.ps1 -Action prepare`.
- PASS: source scan found `Graph Contract Preview`, `LoadGraphContractPreview`, `MaxGraphPreviewNodes`, invalid opcode text, duplicate node ID text, and budget-drift text.
- PASS: stale schema-90 scan found no stale current revision text in touched modding docs/source.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- PASS: touched-file non-ASCII scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` by resource gate. Samples: CPU `71` with active `dotnet.exe` PID `6088`, then CPU `96` with active `csc.exe` PID `38600` and `dotnet.exe` PID `59612`; no Unity lock.
- NOT RUN: Unity MCP/Editor console verification because Unity MCP tools are not available in this session.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT. No DOTNET_COMPILE, Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 69 - Schema 93 Submission Package Handoff

What was wrong:
- External starter kit authors could validate and generate `Reports/review_manifest.json`, but there was no single checked handoff artifact.
- A direct `Mods/` install route would be false under the current envelope-only runtime boundary.
- SDK generator, Workbench, root launcher, local validator, schema, and docs needed one route or they would drift again.

What was done:
- Added `ModdingSDK/ExternalStarterKit/Tools/build_submission_package.ps1`.
- Added `h8mod.ps1 -Action submission` and Workbench `Build Submission Package`.
- Updated `ModdingSdkHubWindow` so newly generated starter kits include the submission packer, launcher action, README text, tools README text, and validator required-file list.
- Updated the checked-in starter validator required-file list.
- Raised `Signal_Schema.json` to schema revision `93`.
- Extended `Validate_Mod_API_Static.ps1` with submission package probes and schema/runtime-playbook gates.
- Updated modding README/spec/playbook/authoring/product/file-contract docs.
- Regenerated `Reports/review_manifest.json`; it now hashes `27` files and includes `Tools/build_submission_package.ps1`.

Cinematic Cheats used:
- No runtime physical simulation or visual effect was touched.
- The product-side shortcut is a bounded review zip instead of pretending a loose folder is runtime-ready.

Exact Microseconds saved:
- Runtime frame: `0 us/frame` measured/claimed.
- Editor/offline: reduces review handoff ambiguity; no frame-time metric claimed.

Verification:
- PASS: `h8mod.ps1 -Action validate`.
- PASS: `h8mod.ps1 -Action submission`.
- PASS: submission zip inspection found `mod.json`, `mod.h8manifest.json`, `Tools/build_submission_package.ps1`, and `Reports/review_manifest.json`; no `Generated/*` entry was packaged.
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `93`, submission Workbench/tool/root launcher flags true.
- PASS: `Reports/review_manifest.json` parse -> schema `hecton8.external_review_manifest.v1`, runtime `envelope-only`, `27` files, `62294` bytes, `Generated/`/`Reports/` excluded.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- PASS: editor C# ASCII scan.
- PASS: stale schema-92 scan found no stale current revision text; remaining `92` hits were SHA/meta/numeric substrings.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` by resource gate. Samples: CPU `100`, then CPU `66`; no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild`; no Unity lock.
- NOT RUN: Unity MCP/Editor console verification because Unity MCP tools are not available in this session.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT. No DOTNET_COMPILE, Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 68 - Schema 92 Graph Node Snippet Helper

What was wrong:
- The Workbench could show graph contract errors, but creating a valid graph node was still raw JSON authoring.
- External authors had no no-Unity helper for canonical node IDs, opcode alias/hex resolution, or safe snippet generation.
- A direct graph mutation tool would be unsafe because it could erase unknown future fields or reorder author-controlled graph data.

What was done:
- Added `ModdingSDK/ExternalStarterKit/Tools/create_graph_node_snippet.ps1`.
- Added root launcher action `h8mod.ps1 -Action node-snippet -NodeId <id> -Opcode <alias-or-hex>`.
- Added Workbench UI fields/buttons for graph node snippet generation and opening the generated snippet/tool.
- Updated SDK Hub starter-kit generation so refreshed kits include the tool, root launcher route, README text, and validator requirement.
- Updated `Tools/validate_structure.ps1`, starter docs, modding docs, runtime playbook, schema, and static validator.
- Bumped `Signal_Schema.json` to schema revision `92`.

Cinematic Cheats used:
- Offline authoring snippet instead of runtime graph mutation, managed DLL execution, Harmony/BepInEx patching, loose AssetBundle/PNG/localization ingress, SignalBus hot path, or gameplay authority changes.

Exact Microseconds saved:
- Runtime frame: `0 us/frame` measured/claimed.
- Editor/offline: prevents invalid node JSON and review churn before loader/runtime handoff; no frame-time metric claimed.

Verification:
- PASS: `Docs/Modding/Validate_Mod_API_Static.ps1` -> schema revision `92`, snippet Workbench/tool/root launcher flags true.
- PASS: static validator negative probes reject invalid graph opcode, invalid snippet opcode, invalid semver, and oversized review file.
- PASS: `h8mod.ps1 -Action validate`.
- PASS: `Reports/review_manifest.json` parse -> schema `hecton8.external_review_manifest.v1`, `26` files, `55170` bytes.
- PASS: stale schema-91 scan found no stale current revision text in touched modding docs/source.
- PASS: scoped `git diff --check`; line-ending warnings only.
- PASS: touched-file trailing whitespace scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal` by resource gate. Sample: CPU `98.64`, active `csc.exe` PID `29640`, active `dotnet.exe` PID `23460`, no Unity lock.
- NOT RUN: Unity MCP/Editor console verification because Unity MCP tools are not available in this session.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT. No DOTNET_COMPILE, Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 70 - Schema 94 Submission Package Status And Atomic Handoff

What was wrong:
- The Workbench could build `Generated/<mod-id>_submission.zip`, but it did not show the current package path, bytes, timestamp, or freshness against `Reports/review_manifest.json`.
- The submission packer could delete the previous zip before a replacement was safely installed.

What was done:
- `ExternalStarterKitWorkbenchWindow` now shows a `Submission Package` status panel, checks newest `Generated/*_submission.zip`, reports stale/missing package states, opens the current zip, and reveals `Generated/`.
- Checked-in and generated `Tools/build_submission_package.ps1` now write temp zip output first and use `.previous` backup/restore during final replacement.
- `Signal_Schema.json` advanced to schema revision `94`.
- `Validate_Mod_API_Static.ps1`, Runtime Playbook, README, API spec, authoring plan, product blueprint, starter file contract, and starter README/tool README now prove and document Workbench package status plus previous-zip preservation.

Cinematic Cheats used:
- None. This pass is SDK authoring/offline packaging only.

Exact Microseconds saved:
- Runtime: `0 us/frame` claimed. No runtime loader, SignalBus, GlobalDataVault, save, physics, rendering, Burst/job, or quality route was changed.
- Authoring risk removed: stale handoff package visibility and previous zip loss during failed replacement.

Evidence:
- PASS: starter validate.
- PASS: starter submission package build.
- PASS: zip inspection: `28` entries, required manifests/tool/review manifest present, no `Generated/*` entry.
- PASS: static validator schema revision `94`, `ExternalStarterKitWorkbenchShowsSubmissionPackageStatus=True`, `ExternalStarterKitSubmissionPackagePreservesPreviousOutputUntilSuccess=True`.
- PASS: review manifest parse: `27` hashed files, `63955` total bytes, no `Generated/` or `Reports/` source entries.
- PASS: scoped `git diff --check`, trailing whitespace scan, editor C# ASCII scan, stale schema-93 text scan.
- DEFERRED: `dotnet build Assembly-CSharp.csproj -v:minimal`; build gate saw CPU `100` with active `csc.exe` PID `41544` and `dotnet.exe` PID `43740`, then CPU `80` with active `dotnet.exe` PID `62104`; no Unity lockfile.
- NOT RUN: Unity MCP/Editor console verification; Unity MCP tools are unavailable in this session.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT. No DOTNET_COMPILE, Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## 2026-05-28 - Pass 71 - Schema 95 Authoring Data Preview And Validation

What was wrong:
- Settings and locale files were part of the public starter kit, but the integrated Workbench did not show their current validity before review/submission handoff.
- `Tools/validate_structure.ps1` proved presence and parseability but did not deeply validate settings row IDs/kinds/defaults or locale code/key/value contracts.
- Generated starter kits could drift from the checked-in validator/schemas if only the template was hardened.

What was done:
- Added bounded `Authoring Data Preview` to `ExternalStarterKitWorkbenchWindow` for `Tables/settings.h8table.json` and `Locales/en.h8loc.json`.
- Hardened checked-in and generated `Tools/validate_structure.ps1` for settings schema, row array cap, canonical IDs, duplicate IDs, supported kinds, default type matching, locale schema/code, canonical string keys, non-empty values, and locale string cap.
- Hardened `settings_table.schema.json` and `locale.schema.json`, plus the SDK Hub schema/validator generator.
- Updated `Signal_Schema.json` to schema revision `95`.
- Updated `Validate_Mod_API_Static.ps1`, Runtime Playbook, README, API spec, authoring plan, product blueprint, starter file contract, starter README, and tool README.
- Regenerated `Reports/review_manifest.json` and `Generated/com.example.starter_submission.zip`.

Cinematic Cheats used:
- Offline/editor validation and preview instead of runtime loader repair, runtime localization fallback, managed DLL execution, SignalBus hot lanes, or gameplay authority changes.

Exact Microseconds saved:
- Runtime: `0 us/frame` claimed. No runtime loader, FutureCommandEnvelope validation, SignalBus, GlobalDataVault, save, physics, rendering, Burst/job, or quality route was changed.
- Authoring risk removed: malformed settings/locale data is rejected before review/submission.

Evidence:
- PASS: starter validate.
- PASS: starter submission package build.
- PASS: static validator schema revision `95`, `ExternalStarterKitWorkbenchShowsAuthoringDataPreview=True`, `ExternalStarterKitValidatorChecksSettingsAndLocaleContracts=True`.
- PASS: negative static probes reject invalid settings row ID/kind and invalid locale code/key/value.
- PASS: JSON parse for `Signal_Schema.json`, starter settings schema, starter locale schema, and review manifest.
- PASS: zip inspection: `28` entries, required manifests/settings schema/locale schema/review manifest present, no `Generated/*` entry.
- PASS: scoped `git diff --check`, trailing whitespace scan, editor C# ASCII scan, stale schema-94 text scan.
- PASS: `dotnet build Assembly-CSharp.csproj -v:minimal`; launched only after gate allowed it with CPU `44.53`, no active compiler processes, and no Unity lockfile; result `0 Warning(s)`, `0 Error(s)`.
- NOT RUN: Unity MCP/Editor console verification; Unity MCP tools are unavailable in this session.

Evidence class:
- STATIC_SOURCE / STATIC_DOC / CLI_SCRIPT / DOTNET_COMPILE. No Unity Console, PlayMode, profiler, player build, or runtime GC proof claimed.

## MODDING_SDK_AUDIT - Pass 74 - Schema 98 Bounded Settings/Locale Apply

What was wrong:
- Settings/locale snippets existed, but random authors still had to hand-splice JSON into table/locale files.
- Initial apply validator call treated null `$LASTEXITCODE` as failure and polluted `-Json` output with validator PASS text.

What was done:
- Added `Tools/apply_settings_row_snippet.ps1` and `Tools/apply_locale_entry_snippet.ps1` with safe path checks, duplicate rejection, explicit `-Replace`, temp-write, post-write validation, and rollback.
- Added `apply-setting-snippet` and `apply-locale-snippet` to root `h8mod.ps1`.
- Added Workbench Apply Setting/Locale Snippet buttons plus target/tool open routes.
- Updated SDK Hub generator to emit the same apply tools, launcher routes, validator requirements, docs, and capability guide text.
- Added `-ThrowInsteadOfExit` validator mode so apply tools can validate in-process without false failure or JSON contamination.
- Updated `Signal_Schema.json` to revision 98 and extended `Validate_Mod_API_Static.ps1` with temp-copy apply probes and duplicate-rejection gates.
- Updated public docs and starter docs for the actual no-Unity and Workbench workflow.

Cinematic Cheats used:
- None. This is SDK/offline authoring UX, not runtime rendering/simulation.

Exact Microseconds saved:
- Runtime frame: 0 us/frame. No gameplay loop changed.
- Low-end i3/MX350 impact: 0 us/frame; avoids failed review packages and authoring support churn before runtime.

Evidence:
- Static validator PASS at schema 98.
- Starter validate PASS.
- Starter submission PASS.
- Review manifest includes 32 source files and both apply tools; Generated/ excluded.
- Submission zip includes 33 entries, both apply tools, and Reports/review_manifest.json; Generated/ excluded.
- Parser, JSON parse, scoped diff check, trailing whitespace, C# ASCII, stale schema-97 scans PASS.
- Dotnet build deferred by CPU gate: samples 100.00 and 63.70 percent.

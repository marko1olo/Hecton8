# Rationale 1309 - MEMORY_SOVEREIGN_UI_PRESENTATION_EXORCIST

Status: APEX STATIC PASS / LISTENER REGISTRY FIX / HOT VALUE-CONSTRUCTOR SYNTAX PURGE / CURRENT PASS BUILD NOT RUN PER USER BUILD-RARE ORDER / PREVIOUS BUILD BLOCKED BY NON-DOMAIN ERRORS

## Decision 000 - Domain Path Normalization

Problem: Prompt names `Assets/Project/Scripts/UI`, but the live repository uses `Assets/_Project/Scripts/UI` and `Assets/_Project/Scripts/Visor`. A literal `Assets/Project` scan returns no source files.

Solution: Use live source authority under `Assets/_Project/Scripts/UI` and visor/presentation-adjacent code under `Assets/_Project/Scripts/Visor`, while recording the mismatch. This follows the domain file's current-source precedence and avoids fake zero-result reports.

Rejected Alternatives: Literal scan of missing `Assets/Project` path would create a false clean result. Broad project-wide edits would violate domain boundary.

Scalability potential: Low tier avoids unnecessary audit span; Middle/High/Ultra maintain exact source ownership and prevent unrelated churn.

Hardware Impact: Static scan only. Runtime gain unknown until offenders are proven and patched. Estimated current hot-path gain: 0 us pending evidence.

## Decision 001 - Phase 0 Evidence First

Problem: The assignment demands Vault migration, but code reality must prove persistent native aliases before mutation. Blind substitution risks inventing dependencies and breaking parallel agents.

Solution: Run AST/static scans first, build machine-readable ledgers, then patch only proven offenders. DOD practice: one owner, one route, one proof artifact.

Rejected Alternatives: Search-and-replace `NativeArray` fields with fake handles would be compile-hostile and architecturally dishonest.

Scalability potential: Low/Middle/High/Ultra all benefit from only removing real alias hazards, not adding ornamental Vault surfaces.

Hardware Impact: No runtime impact yet. Estimated saved time: 0 us until offenders are removed and verified.

## Decision 002 - Event Lane NativeQueue Removal

Problem: `BaseIntegrityEvents`, `NotificationEvents`, `PDAIntrusionEvents`, and `SpectrumEvents` owned persistent `NativeQueue<T>` fields. These lanes are presentation dispatch buffers, not cross-domain native truth, and they were outside `GlobalDataVault` ownership.

Solution: Replace those persistent unmanaged queues with `FixedUiEventQueue<T>` cold fixed-capacity ring lanes. The route remains main-thread presentation dispatch; no gameplay truth or DTO authority moved. DOD practice: one fact owner, no hot `GlobalRegistry` polling, no persistent unmanaged alias outside the Vault.

Rejected Alternatives: Keeping `NativeQueue<T>` plus `NativeMemorySentinel` preserves the forbidden alias. Routing these tiny same-thread events through Vault buffers would create fake dependencies and higher lock cost than the payload deserves. Routing through legacy `GlobalSignals` would be a colder bridge, not a local UI lane.

Scalability potential: Low tier uses the same fixed caps and drops excess events fail-closed. Middle/High/Ultra spend saved allocator/sentinel overhead on existing visual feedback, not extra gameplay truth.

Hardware Impact: Removes persistent native queue allocation and prewarm/dispose paths from four event systems. Hot-path estimate: 1-4 us saved during burst event enqueue/flush spikes on i3/MX350-class CPUs; normal frames near 0 us. No per-frame GC introduced.

## Decision 003 - MockTextSpan Pointer Exorcism

Problem: `MockTextSpan` stored `ushort* Buffer` inside a Vault DTO. That is a stale-pointer hazard across Vault compaction and violates descriptor sovereignty even though jobs also received phase-local pointers.

Solution: Replace the pointer with `BufferId` and `BufferGeneration` fields while keeping the struct size at 32 bytes. Scheduled jobs continue to receive raw pointers only as transient `IJob` parameters resolved inside the owner phase.

Rejected Alternatives: Leaving the pointer unused but present would keep the Roslyn audit red. Passing `VaultGenerationHandle<T>` into Burst jobs was rejected; jobs consume physical views, not handles.

Scalability potential: Low/Middle/High/Ultra share stable descriptor metadata; visual quality remains controlled by existing continuous tuning and `GlobalQualityWeight`.

Hardware Impact: Runtime speed change is approximately 0 us. Risk reduction is stale pointer removal on compaction/live reload; ARM64 DTO size remains 32 bytes.

## Decision 004 - Vault View Handles Instead Of Persisted Views

Problem: `DynamicDecalFrameStats.UploadBuffer` persisted a `NativeArray<TraumaDecalDTO>` view beyond the owner finalize phase, and `PdaH8lrLoreStore` persisted a raw `byte*` base pointer.

Solution: Store `VaultGenerationHandle<TraumaDecalDTO>` plus capacity in decal frame stats and resolve the upload view immediately before GPU upload. Remove the lore-store base pointer field and resolve the vault mirror pointer per read accessor. The lore mirror write uses `TryAcquireWriteLock` with `finally` release.

Rejected Alternatives: Copying decal upload data into a managed array would create GC pressure and duplicate memory bandwidth. Re-enabling memory-mapped persistent pointers would preserve the stale pointer hazard on platforms that support MMF.

Scalability potential: Low tier keeps one upload scratch buffer and capped read spans. Middle/High/Ultra can raise existing visual density through the runtime's continuous quality paths without changing authority ownership.

Hardware Impact: Avoids stale `NativeArray` and pointer lifetime faults. Microsecond estimate: 0-8 us saved on frames that avoided failed/stale upload resolution; main benefit is correctness under compaction.

## Decision 005 - Verification Boundary

Problem: The code needed proof without violating the coordinator build gate. The latest gate reported CPU at 100% and active `csc`/`dotnet` processes.

Solution: Do not launch `dotnet build`. Use the existing Roslyn native-alias audit executable to parse `Assets/_Project/Scripts/UI` and `Assets/_Project/Scripts/Visor`. Result: `forbiddenPersistentCandidates=0`, `parseFailures=0` in both ledgers. Proof report written to `Docs/Reports/VAULT_EXORCISM_REPORT_1309.json`.

Rejected Alternatives: Launching another build would violate the explicit CPU/process rule. Reporting a fake compile pass is rejected.

Scalability potential: Verification path is static and device-independent. Low/Middle/High/Ultra runtime behavior is unaffected by this audit pass.

Hardware Impact: No runtime impact. Verification time only. Estimated saved runtime: 0 us; prevents shipping persistent native alias regressions.

## Decision 006 - Inline Queue Slots, Not Managed Arrays

Problem: The first fixed UI event queue still had a managed `T[]` backing store. It removed `NativeQueue`, but it did not satisfy the APEX no-hidden-managed-buffer standard for hot presentation event lanes.

Solution: Replace the backing array with explicit inline slots `_item0` through `_item23`. Enqueue/dequeue use index switches and `default` clearing. Event payload construction now uses `default` plus field assignment instead of object initializers.

Rejected Alternatives: `NativeQueue<T>` preserved the original forbidden persistent alias. `T[]` preserved managed heap backing. Routing local visual events through GlobalDataVault would create a fake global truth owner for same-thread UI dispatch.

Scalability potential: Low tier drops overflow fail-closed; Middle/High/Ultra keep deterministic event latency and spend cycles on presentation effects.

Hardware Impact: 1-4 us saved during burst dispatch spikes on i3/MX350-class hardware by removing native queue prewarm/sentinel work and managed backing array pressure. Normal frames near 0 us.

## Decision 006B - Inline Listener Registries, Not ListenerSlot Arrays

Problem: After persistent native queues were removed, `BaseIntegrityEvents`, `PDAIntrusionEvents`, `NotificationEvents`, and `SpectrumEvents` still used fixed managed listener arrays. These are not `NativeArray` aliases, but they are avoidable managed heap objects in hot event lane infrastructure.

Solution: Replace the listener array backing stores with inline fixed-slot registry structs. UI registries hold eight direct listener references. `SpectrumEvents` uses a 24-slot generic inline registry to preserve the existing sonar ping capacity. Reentrant deferred register/unregister buffers now use the same inline registry pattern.

Rejected Alternatives: Routing listener registrations through `GlobalDataVault` would be fake ownership; listeners are managed callback endpoints, not cross-domain unmanaged truth. Replacing every unrelated visual cache array in the same pass was rejected because those caches include UI object references and large text/radar buffers that require a separate design, not a blind mechanical rewrite.

Scalability potential: Low tier keeps the same caps and fail-closed overflow behavior. Middle/High/Ultra preserve the same listener capacity and spend no extra memory traffic on array indirection.

Hardware Impact: Removes 20 bare managed `new` hits from the modified-source scan and avoids separate listener-array heap objects. Estimated runtime gain is 0-2 us during listener mutation/dispatch setup; normal frame effect is near 0 us. Main gain is stricter heap surface control.

## Decision 007 - Managed Scan Defects Removed Instead Of Explained Away

Problem: Full-file scan exposed two cold managed containers in files touched by this task: `PDAIntrusionManager._driftScanBuffer` as `List<TextMeshProUGUI>` and `SpectrumSystem.s_glitchControllers` as `List<VisorHUDController>`. Cold comments are not enough because both containers can grow or push List-based API use into runtime paths.

Solution: PDA text drift now traverses the UI hierarchy with the existing `_driftRects` fixed array as a high-end temporary stack and stores targets from the low end. Spectrum no longer copies active controllers; `VisorHUDController.PulseActiveControllers` pulses the owner registry directly. `VisorHUDController` active registry was converted from `new List<VisorHUDController>(2)` to eight explicit static slots.

Rejected Alternatives: Keeping `GetComponentsInChildren(..., List<T>)` risks List capacity growth. Adding a new `Transform[]` stack was rejected after diff scan flagged a new managed array. FindObjects-based resolution was rejected as an allocation-heavy scene search.

Scalability potential: Low tier truncates traversal at fixed capacity without allocation. Middle/High/Ultra keep stable visual pulse behavior; capacity can be expanded by adding explicit slots only if profiler evidence proves more active visors are required.

Hardware Impact: 0-3 us saved on PDA drift rescan and visor mode switch spikes; main gain is removal of hidden managed growth and scene-copy buffers.

## Decision 008 - ARM64 DTO Alignment Repairs

Problem: `ScrambledCharacterDTO` was 4 bytes, and `GlitchBlackBoxDumpHeader` stored a 64-bit timestamp after 4-byte fields. That violated the strict ARM64 layout rule in the APEX review even if x64 tolerated it.

Solution: `ScrambledCharacterDTO` is now 8 bytes with explicit padding at offsets 2 and 4. Dump timestamp is split into `TimestampTicksLow` and `TimestampTicksHigh` at offsets 24 and 28. `MockTextSpan` remains 32 bytes with descriptor fields, not a pointer. Layout validators now cover the repaired offsets.

Rejected Alternatives: Keeping a `ulong TimestampTicks` at offset 24 was rejected because the field ordering rule requires real 8-byte fields first; splitting keeps the dump ABI 32 bytes without misordered 64-bit data. Repacking every unrelated DTO was rejected as scope creep without a proven offender.

Scalability potential: Low/Middle/High/Ultra share the same DTO ABI; visual fidelity scaling does not mutate layout or authority routes.

Hardware Impact: 0 us frame gain; prevents ARM64 alignment penalties and ABI drift on Quest-class devices.

## Decision 009 - AUP Local-First Decal Matrix

Problem: Dynamic decal placement works with absolute positions. Directly casting absolute AUP coordinates to `float3` would destroy precision and violate the AUP doctrine.

Solution: `TryBuildMatrix` computes `double3 local = request.ImpactAup - CameraAup`, checks finite local double values, then casts only the local delta to `float3`. Matrix construction uses the local float vector.

Rejected Alternatives: Direct `float3` cast from `ImpactAup` was rejected. Storing camera-relative truth in DTOs was rejected because quality/camera state must not alter save or authority identity.

Scalability potential: Low tier uses the same local formula with fewer decals; Middle/High/Ultra can increase decal count/refraction intensity without changing coordinate truth ownership.

Hardware Impact: Prevents precision jitter and NaN propagation. Estimated frame gain 0 us; correctness and visual stability gain dominate.

## Decision 010 - APEX Verification Boundary

Problem: A full build and custom net10 AST allocation auditor were blocked by environment rules: CPU stayed above 50%; final gate had 0 compiler processes but CPU was still 77%. PowerShell 5 could not load net10 Roslyn assemblies directly.

Solution: Re-ran the already-built Roslyn native alias and presentation decoupling auditors. Added textual diff/full-file scans for forbidden managed constructs. Wrote all proof to `Docs/Reports/VAULT_EXORCISM_REPORT_1309.json`; build remains explicitly NOT_RUN rather than faked.

Rejected Alternatives: Launching another build would violate the coordinator rule. Reporting a compile pass or AST-GC pass not actually executed was rejected.

Scalability potential: Verification is static and device-independent. Runtime behavior remains bounded by fixed capacities and continuous `GlobalQualityWeight` paths.

Hardware Impact: No runtime impact. Prevents false release confidence under saturated build conditions.

## Decision 011 - Visor Active Registry Compaction

Problem: The managed `List<VisorHUDController>` registry was removed, but the first fixed-slot registry could fail closed too early if Unity destroyed a controller without a matching unregister call. Eight stale pseudo-null slots would block future visor pulse routing.

Solution: Add `CompactActiveControllerSlots()` before active-controller registration. The registry remains eight explicit static fields plus count, removes Unity pseudo-null entries, and keeps overflow fail-closed. The switch expression in `GetActiveControllerSlot` was replaced with a classic `switch` statement for conservative Unity compiler compatibility.

Rejected Alternatives: Restoring `List<VisorHUDController>` would reintroduce managed capacity growth. Expanding to a managed array would create another heap-backed registry. Scene search was rejected because it is allocation-prone and violates cached-owner routing.

Scalability potential: Low tier keeps a hard eight-controller cap and skips stale slots. Middle/High/Ultra keep the same deterministic route; capacity can only increase by adding explicit slots with profiler evidence, not by hidden dynamic growth.

Hardware Impact: Estimated 0-2 us saved on visor mode-switch spikes versus list copy/remove behavior. Main gain is release safety: destroyed Unity references cannot permanently poison the fixed registry.

## Decision 012 - APEX Hot-Link Purge

Problem: The second-pass audit found release-grade weaknesses that the prior report did not close: `PDAIntrusionManager` retried missing owner resolution every `LateFrameTick`, `SpectrumSystem` still used concrete `PlayerRuntimeContextService.TryGetActiveRuntimeContext` reads in hot presentation helpers, and `VisorHUDController` stored a concrete `SubmarineStructuralGrid` pointer for fatigue visuals.

Solution: PDA owner resolution is now bounded by `RuntimeOwnerRetryIntervalSeconds = 0.5f` and fails closed when player movement is absent. PDA and Spectrum runtime-space conversion now uses `AbsoluteUniversePosition.DeltaMetersClamped(positionAup, RuntimeOriginRoute.CurrentRuntimeOriginAup())` in double precision before assigning float fields. Spectrum caches `IPlayerRuntimeContext` through cold registry/hot-swap and reads owner-published snapshots. Visor stores `ISubmarineHullBreachReadModel` and gates destroyed Unity read-models before reading fatigue.

Rejected Alternatives: Leaving `ToRuntimeFloat3` calls as "internally safe" was rejected because the release proof needed the local-delta formula visible at the consuming site. Keeping `PlayerRuntimeContextService` was rejected as a horizontal concrete dependency. Keeping `SubmarineStructuralGrid` was rejected because presentation only needs the hull breach read model.

Scalability potential: Low tier avoids missing-owner hierarchy traversal every frame and keeps fixed presentation caps. Middle/High/Ultra keep the same continuous sonar, visor, and HUD visual math; no binary quality switch or authority route change was added.

Hardware Impact: PDA missing-owner frames avoid repeated hierarchy recursion; estimated 0-4 us saved on i3/MX350-class CPUs during bootstrap/despawn gaps. Spectrum/Visor dependency changes are primarily release-safety and isolation gains; normal-frame speed gain is 0-2 us.

## Decision 013 - Net10 Static Proof Refresh

Problem: The already-built `net8.0` Roslyn audit executables cannot run on this machine because only .NET 10.0.6 is installed. The build gate still forbids launching project compilation while dotnet/compiler processes are active.

Solution: Run the already-built `net10.0` audit executables directly. This refreshes AST proof without invoking `dotnet build` or project compilation. Results stayed stable: UI native alias 0 persistent candidates, Visor native alias 0 persistent candidates, UI presentation decoupling 0 fatal hot-path findings, Visor presentation decoupling 0 fatal hot-path findings.

Rejected Alternatives: Installing .NET 8 is outside task scope. Launching any build while 7 dotnet/compiler processes are active violates the coordinator rule. Reporting stale AST proof after code edits was rejected.

Scalability potential: Static proof only. Low/Middle/High/Ultra runtime behavior is unchanged.

Hardware Impact: No runtime impact. Verification confidence restored after the hot-link purge.

## Decision 014 - Generated Csproj Inclusion Fix

Problem: `dotnet build Hecton8.Core.csproj --no-restore` became legal after the CPU/compiler gate cleared and exposed 24 missing-type errors for `FixedUiEventQueue<>`. The new source file existed on disk but was not included by the generated Unity csproj.

Solution: Move `FixedUiEventQueue<T>` into existing compiled source `BaseIntegrityHUD.cs` and remove the standalone untracked source. Rebuild then advanced past the 1309 UI/Visor patch and stopped only on non-domain errors in Audio/Tether.

Rejected Alternatives: Editing the generated csproj was rejected because Unity can regenerate it and erase the fix. Keeping the uncompiled source would leave the patch unreleasable.

Scalability potential: Low/Middle/High/Ultra behavior unchanged; this is compile integration only.

Hardware Impact: Runtime impact 0 us. Release risk reduced because the fixed queue type is now in the compiled assembly route.

## Decision 015 - APEX Lock and Payload Contract Hardening

Problem: Re-review found three release blockers: `FixedUiEventQueue<T>` only required `struct`, allowing future managed-reference payloads; `PdaH8lrLoreStore.TryOpenVaultMirror` could acquire a write lock and return before `finally` when the resolved mirror was invalid; deleting `FixedUiEventQueue.cs` left an orphan `.meta` file.

Solution: Change `FixedUiEventQueue<T>` and Spectrum queue helpers to `where T : unmanaged`; split vault mirror lock acquisition from validation so every post-acquire branch exits through `finally`; delete `FixedUiEventQueue.cs.meta`; remove `new Span<byte>` / `new ReadOnlySpan<byte>` syntax from vault mirror, glitch dump, and decal CSV paths via `MemoryMarshal.CreateSpan/CreateReadOnlySpan`.

Rejected Alternatives: Keeping `where T : struct` was rejected because it cannot prove zero managed references. Keeping validation inside the acquire condition was rejected because it can leak a lock. Leaving the `.meta` was rejected by atomic file deletion rules.

Scalability potential: Low/Middle/High/Ultra behavior unchanged; this is contract and failure-mode hardening. Fixed queues still drop overflow fail-closed instead of growing.

Hardware Impact: Runtime speed gain 0 us normal frame. Failure impact: prevents vault write-lock leak on corrupt H8LR mirror metadata and prevents future managed payload regression in UI/Spectrum event lanes.

## Decision 016 - APEX Private Padding and ARM64 Field Ordering

Problem: Re-review found ABI hygiene debt that the prior report did not close: some DTO padding fields were public, and `MockTextSpan`, `DecalRequestQueueStateDTO`, `TraumaWoundTelemetryEntry`, and `DecalMaterialProfileDTO` used `ulong` padding after 4-byte payload fields. Sizes were multiples of 8, but the field ordering proof was not clean.

Solution: Convert those pads to private 4-byte `uint` fields, remove all external writes to padding, and update editor-only offset reflection to include non-public fields. This preserves binary size while proving there are no late 8-byte padding fields after 4-byte payload.

Rejected Alternatives: Leaving `ulong _pad*` as "just padding" was rejected because the prompt requires byte-level ordering proof, not convenient ABI excuses. Making every pad public for validator access was rejected; validators now use reflection with `BindingFlags.NonPublic`.

Scalability potential: Low/Middle/High/Ultra runtime visuals are unchanged. The benefit is ABI determinism across Quest ARM64 and desktop validation.

Hardware Impact: 0 us normal-frame speed gain. Prevents ARM64 layout-review failure and future accidental writes to padding fields.

## Decision 017 - APEX Unmanaged Helper and Publish-After-Validate Hardening

Problem: Third self-audit found remaining release-grade proof gaps: modified Vault helper generics still accepted `where T : struct`, Spectrum DTO declarations did not put the 48-byte AUP field first in source order, and the PDA H8LR mirror path assigned Vault mirror state before metadata validation completed.

Solution: Change the patched Glitch, DynamicDecal, and Spectrum Vault helper constraints to `where T : unmanaged`; reorder `AcousticEchoEvent` and `PingReturnSignal` declarations so `WorldAup` is first; replace touched value-constructor syntax in AUP offset helpers with default-initialized structs; publish `_vaultMirrorBacked` only after `ValidateMappedBytes` succeeds and reset decoded H8LR state on validation failure while keeping the write-lock `finally`.

Rejected Alternatives: Keeping `where T : struct` was rejected because it allows future managed-reference payload regression. Calling `Dispose()` inside the H8LR write-lock failure path was rejected because it mixed state teardown with lock ownership. Treating correct `FieldOffset` values as sufficient was rejected because the APEX proof requires visible byte-order discipline.

Scalability potential: Low/Middle/High/Ultra behavior unchanged. The gain is deterministic buffer contracts under compaction, not a visual change.

Hardware Impact: 0 us normal-frame speed gain. Prevents future managed generic misuse and removes a corrupt-mirror state publication window on Quest-class ARM64 targets.

## Decision 018 - Hot Value Constructor Syntax Purge

Problem: The APEX re-scan still found constructor syntax in modified hot presentation files. Most offenders were value-type constructors (`float2`, `float3`, `float4`, `float4x4`, `Vector2`, `Vector4`) rather than managed heap allocations, but keeping them inflated the `new` audit and hid the remaining real cold/fixed-cache surfaces.

Solution: Replace mechanically safe value constructors and DTO object initializers with `default` plus explicit field assignment in `DiegeticGlitchSurgeonRuntime`, `PDAIntrusionManager`, `DeferredDecalPass`, `DynamicDecalVaultRuntime`, `SpectrumSystem`, and `VisorHUDController`. Keep Unity serialized `Vector3` defaults, fixed managed shader/UI caches, and cold `CommandBuffer` setup classified instead of pretending they are hot-path Zero-GC violations.

Rejected Alternatives: Blindly deleting UI component arrays or serialized defaults was rejected because it would be a risky design rewrite without proof of a native alias bug. Replacing cold `CommandBuffer` allocation with a fake no-op was rejected because the HUD scissor guard requires real Unity command buffers.

Scalability potential: Low tier keeps the same fixed caps and deterministic visual cheats. Middle/High/Ultra keep the same continuous presentation fidelity paths; no binary quality switch or gameplay authority route was introduced.

Hardware Impact: Normal-frame gain is approximately 0 us because value-type constructor syntax does not allocate managed heap memory. Verification gain is material: full modified-source `new` scan dropped from 143 to 79; broad targeted constructor/cold-allocation scan dropped to 8 classified residual hits.

## Decision 019 - Domain-Wide Unmanaged Generic Contract

Problem: The patched files no longer had the original native alias defects, but a broader UI/Visor scan still found `where T : struct` helper constraints. That is not a current allocation by itself, but it permits future managed-reference structs at Vault/Native helper boundaries and weakens the Zero-GC contract proof.

Solution: Convert the remaining UI/Visor helper constraints to `where T : unmanaged` across 22 runtime files and the Terminal OS editor layout validator. Then re-run text and Roslyn proof: domain `where T : struct` hits are 0, native allocator constructor hits are 0, UI/Visor forbidden persistent native candidates are 0/0, presentation fatal hot-path findings are 0/0. Broad added-diff constructor scan then exposed 7 value/object initializer lines, which were converted to `default` plus assignment. Two residual added-diff `new` hits remain and are explicitly documented cold `GraphicsBuffer` resource creation in `DiegeticGyroCompassRuntime.cs:1449-1450`.

Rejected Alternatives: Leaving editor validator generic constraints as `struct` was rejected because it is part of DTO layout proof even though it is not runtime. Removing the two `GraphicsBuffer` cold allocations was rejected because they are real Unity GPU resource owners; a fake no-op would break the compass draw route. Running another `dotnet build` was rejected under the user build-rare order and because the last build wall was non-domain Audio/Tether errors.

Scalability potential: Low tier keeps fixed-capacity fail-closed presentation lanes and cold GPU resource ownership. Middle/High/Ultra retain the same continuous quality paths; the contract change only prevents future managed-reference DTO misuse and does not create binary quality switches.

Hardware Impact: 0 us normal-frame speed claimed. The gain is release-proof hardening: future managed-reference payloads are compile-time rejected at local helper boundaries, and the domain diff now has only 2 documented cold GPU allocations after stripping value constructor noise.

## Decision 020 - Domain Span Constructor, AUP, and Dump Route Purge

Problem: Re-scan after the unmanaged generic pass still found `new Span<T>` / `new ReadOnlySpan<T>` syntax in UI/Visor dump, CSV, tuner, and text-buffer paths. The same touched surface also had direct AUP-to-runtime float conversion in Terminal/Tooltip visualization code and SHINOBU-named runtime dump routes. Those are proof defects even when some paths are cold/editor-only.

Solution: Convert array-backed spans to `.AsSpan(...)` and unsafe pointer-backed spans to `MemoryMarshal.CreateSpan/CreateReadOnlySpan(ref UnsafeUtility.AsRef<byte/char>(ptr), length)`. Replace Terminal/Tooltip direct `ToRuntimeFloat3()` usage with `AupPrecisionMath.LocalDeltaFloat3(targetAup.ToAbsoluteDouble3(), RuntimeOriginRoute.CurrentRuntimeOriginAup().ToAbsoluteDouble3(), fallback)`. Rename runtime dump routes touched in this pass to `Dump_1309_*`. Re-run existing net10 Roslyn audits and text gates without launching `dotnet build`.

Rejected Alternatives: Leaving pointer span constructors as "struct constructors" was rejected because it keeps the textual Zero-GC gate noisy. Leaving Terminal/Tooltip direct `ToRuntimeFloat3()` was rejected because the local-double-subtract proof was not visible at the consuming site. Renaming editor-only scanner prose was rejected as documentation churn; runtime route scan excludes Editor and is clean. Running another `dotnet build` was rejected under the user build-rare order and because the last build wall remains non-domain Audio/Tether.

Scalability potential: Low tier keeps the same cold file/dump paths and fixed UI buffers. Middle/High/Ultra keep the same continuous presentation fidelity; this pass changes proof hygiene and failure routing, not quality authority.

Hardware Impact: 0 us normal-frame speed claimed. Failure-path gain is deterministic dump ownership: runtime UI/Visor crash files now route to `Dump_1309_*`. AUP gain is visual correctness under large coordinates; direct absolute-to-float conversion is removed from the patched Terminal/Tooltip scope.

## Decision 021 - Domain Player Context and Runtime AUP Purge

Problem: A follow-up domain scan still found one concrete runtime player-context call in `PDASpectrumTab.IsEmpSensorBlindActive`, and the broader patched UI/Visor surface still needed explicit proof that beacon, relay, PDA map, radar, compass, focus, and stress presentation code did not cast absolute AUP directly to runtime float space. The failure was not a profiler micro-optimization issue; it was an isolation and coordinate-determinism proof gap.

Solution: Replace the remaining `PlayerRuntimeContextService.TryGetActiveRuntimeContext` call with the already cached `IPlayerRuntimeContext` route in `PDASpectrumTab`. Replace direct runtime AUP conversions in `BeaconHUDElement`, `RelayHUDElement`, `PDAMapTab`, and `PlayerStressVFX` with `RuntimeOriginRoute.CurrentRuntimeOriginAup()` plus `AupPrecisionMath.LocalDeltaDouble(targetAup.ToAbsoluteDouble3(), originAup.ToAbsoluteDouble3())`, finite checks, and local-only `Vector3` casts. Remove concrete context fallbacks from `AcousticEcholocationTranslator`, `AcousticRadarSphereRenderer`, `FakeRadarBlipController`, `SonarHoloCompass`, and `HectonVRDiegeticFocusController`. `InternalFloodWaterlineRuntime` failure logging now uses a constant error string instead of concatenating exception text.

Rejected Alternatives: Keeping `PlayerRuntimeContextService` as a "small helper" was rejected because it is a concrete horizontal dependency in UI refresh. Keeping `ToRuntimeFloat3()` as an opaque conversion was rejected because the prompt requires the local double-subtract formula visible at the consuming site. Launching another `dotnet build` was rejected under the user build-rare order and because the last build wall was non-domain Audio/Tether.

Scalability potential: Low tier fails closed to zero/hidden UI when cached player context or finite AUP is missing. Middle tier keeps deterministic UI/radar/visor refresh without scene search. High and Ultra can increase visual density through existing continuous quality routes without changing DTO layout, authority ownership, or coordinate truth.

Hardware Impact: 0 us normal-frame speed claimed for the AUP formula changes. Removing the remaining concrete context lookup is estimated at 0-2 us on PDA/sonar refresh spikes on i3/MX350-class devices; the main gain is proof-grade isolation and avoidance of precision jitter under large coordinates.

## Decision 022 - APEX Camera-Relative AUP and Fluid Render Contract Purge

Problem: The follow-up text gate still found 11 `AbsoluteUniversePosition.ToCameraRelativeFloat3(...)` calls in UI/Visor runtime code and one concrete `Hecton8.Physics.HectonFluidEngine` dependency in `HectonFluidAdvectionRenderFeature`. The AUP helper may be internally correct, but the prompt required visible double-origin subtraction at consuming sites. The fluid feature also violated presentation isolation by naming the physics owner type.

Solution: Replace the remaining UI/Visor camera-relative AUP helpers with explicit `AupPrecisionMath.LocalDeltaFloat3(target.ToAbsoluteDouble3(), origin.ToAbsoluteDouble3(), float3.zero)` calls in subtitle, waypoint, radar, echolocation, sonar, stencil preview, passive radar, compass, and HUD threat projection paths. Replace the `HectonFluidEngine` presentation dependency with `IFluidAdvectionRenderGraphReadModel` and `FluidAdvectionRenderGraphPayload` in the core contract surface, expose it through `GlobalRegistry.FluidAdvectionRenderGraph`, and let the fluid owner implement the interface. Remove the managed `Vector2[16]` radar ghost LUT and switch to a deterministic constant switch. Convert tooltip glyph upload value constructors/object initializer to `default` plus field assignment. Replace a fluid telemetry warning string concatenation with a constant message.

Rejected Alternatives: Keeping `ToCameraRelativeFloat3` was rejected because it hides the formula the release gate asks to see. Keeping `HectonFluidEngine` in the visor feature was rejected because Presentation must consume a route, not a concrete Physics owner. Adding a new standalone contract source file was rejected after the previous generated-csproj failure mode; the interface was placed in already compiled `GlobalRegistryContracts.cs`.

Scalability potential: Low tier still drops/omits optional visuals through existing fail-closed UI routes. Middle tier keeps deterministic local-space projection. High/Ultra keep the same advection RenderGraph visuals without granting presentation direct physics ownership. `GlobalQualityWeight` authority is unchanged.

Hardware Impact: Normal-frame speed claim remains 0 us for value-constructor syntax removals. Practical gain is jitter prevention under large AUP coordinates and removal of one concrete type bridge. Radar thermal ghost lookup removes one managed cold LUT object and one array index from the hot ghost path; expected gain is below 1 us on i3/MX350-class hardware, proof value is larger than frame-time value.

## Decision 023 - Fixed Active Registries and Unity API Residual List

Problem: Re-audit found remaining managed scratch/list discovery in the UI/Visor runtime surface: active overlay/compositor/source registries, scene-root fallback discovery, `GetComponentsInChildren` list overloads, and PDA pointer target discovery. One `List<RaycastResult>` remains because Unity `GraphicRaycaster.Raycast` requires `List<RaycastResult>`.

Solution: Replace active registries with explicit fixed slots, replace hierarchy discovery with direct `Transform` recursion, remove scene-root enumeration fallbacks, and use active owner registries for overlay/compositor/controller lookup. Keep the single `GraphicRaycaster` list as a disabled compatibility fallback guarded by `EnableGraphicRaycasterFallback=false`; the normal PDA route uses fixed pointer target arrays.

Rejected Alternatives: Pretending the Unity `GraphicRaycaster` API can be called without a list was rejected. Reintroducing scene searches or root enumeration arrays was rejected. Changing unrelated external domains to remove compatibility callers was rejected.

Scalability potential: Low tier uses bounded registries and fails closed on overflow. Middle tier keeps deterministic HUD routing without growth. High and Ultra can add visual density through existing quality-weighted presentation paths without dynamic registry growth.

Hardware Impact: 0-6 us saved on cold/refresh spikes on i3/MX350-class hardware by removing list growth/copy and scene traversal fallbacks. Normal-frame gain is 0 us unless a refresh path was previously active.

## Decision 024 - Private 4-Byte Padding and Runtime Fail-Closed

Problem: Broad ARM64 scan still found public padding fields, private `ulong` padding after 4-byte payload fields, external padding writes, and runtime `throw new` fail-fast paths in UI/Visor. These were proof defects under the APEX prompt even when some paths were cold/editor-only.

Solution: Convert padding to private byte/uint/float fields, split late `ulong` padding into explicit 4-byte uint pads, remove all external padding writes, and update reflection-based layout checks to include non-public fields. Convert `DiegeticVisorLensRuntime` Vault descriptor loss to fail-closed default/fallback views and a constant error log. Convert AR stencil validation menu failure to log-and-return.

Rejected Alternatives: Leaving `ulong` pads as convenient filler was rejected because the byte-order rule requires 8-byte fields before 4-byte payload fields. Throwing managed exceptions on descriptor loss was rejected because presentation runtime must fail closed, not tear down the frame with exception allocation.

Scalability potential: Low tier gets deterministic safe defaults under descriptor loss. Middle tier retains normal visuals when Vault is valid. High/Ultra keep the same shader/GPU visual paths; no quality binary switch or authority route changed.

Hardware Impact: 0 us normal-frame speed claimed. Failure path becomes deterministic and avoids managed exception allocation. ARM64 ABI proof is cleaner on Quest-class hardware.

## Decision 025 - NativeSlice Text Gate and Debug Label Concat Purge

Problem: The post-patch APEX scan still found six `new NativeSlice<T>` constructor expressions in `DiegeticGyroCompassRuntime` and two runtime string literal concatenations in `SuitHUDPresentationController`. The NativeSlice expressions were value views over existing Vault-owned `NativeArray` buffers, not allocations, but they kept the strict `new` gate noisy. The string concatenations were debug labels, but they still allocate under `DEVELOPMENT_BUILD`.

Solution: Replace the six NativeSlice constructors with `NativeArray.Slice()` at the call sites. Replace the two debug label concatenations with fixed switch-table labels: `ResolveFallbackOverlayModeLabel` and `ResolveProjectionUnavailableModeLabel`. Re-run focused runtime scans over UI/Visor for NativeSlice constructors, native allocator constructors, span constructors, string concat, Format/ToString/LINQ/interpolation, scene discovery, direct AUP float helpers, concrete physics links, player context service links, and padding defects.

Rejected Alternatives: Keeping `new NativeSlice<T>` as "struct constructor only" was rejected because the user's gate is textual and strict. Replacing debug labels with `string.Format` or interpolation was rejected because that just moves the allocation. Running another `dotnet build` was rejected because the user ordered rare build attempts and the last known compile wall is outside 1309's domain.

Scalability potential: Low/Middle/High/Ultra visuals are unchanged. This removes proof noise and development-label allocations without changing quality authority, DTO layout, or gameplay truth routes.

Hardware Impact: 0 us normal-frame speed claimed. Development/debug allocation from the label concat is removed. NativeSlice `.Slice()` change is proof hygiene only; it does not change owned memory or frame cost.

## Decision 026 - Fixed Managed Slot Cache Re-Audit

Problem: The APEX diff gate still found managed fixed-cache arrays in `LocalizedLayoutMirror` and `PDADataLogTab`, plus runtime-visible component discovery text in `HectonDryVolumeStencilSource`. The old exact prompt extractor also failed because the current `AGENT_PROMPT` tag includes extra attributes.

Solution: Re-extract prompt 1309 with flexible tag matching from `Docs/Tasks/CURRENT_BATCH.md:724-805`; task count is 20 and SHA-256 is `2b8da8f84d91ee1e180e39e4a511d83b42dbb718ed2ec225eb3e56f63276e00b`. Replace `LocalizedLayoutMirror` icon/base-scale arrays and `PDADataLogTab` row array with fixed fields and switch accessors. Replace `GetComponentsInChildren<Renderer>()` in the dry-volume editor rebuild with direct `Transform` traversal and `TryGetComponent`.

Rejected Alternatives: Keeping the arrays as "cold only" was rejected because they are managed allocations in runtime UI sources and remained visible in added diff. Hiding the component-discovery call behind `#if UNITY_EDITOR` was rejected because the user's text gate is strict. Running fresh dotnet/Roslyn audit was rejected because CPU sample was 83.5%, above the 50% project gate.

Scalability potential: Low tier keeps bounded fixed slots and fails closed on capacity. Middle tier avoids cold managed cache allocations during layout/log setup. High and Ultra keep the same visual routes and continuous quality weighting; no gameplay truth, DTO layout, or authority route changed.

Hardware Impact: 0 us normal-frame speed claimed. Cold construction pressure is lower in two UI components, and editor-only dry-volume rebuild no longer allocates a Unity component array before serializing renderer entries. Fresh Roslyn proof remains blocked by CPU gate, not by code state.

## Decision 027 - ShaderTagId Cache and Broad Residual Truth

Problem: The added-diff scan still contained duplicate `ShaderTagId` value constructors in fill-rate and overdraw features, and a full-source domain scan exposed a larger truth: runtime UI/Visor still contains 62 `GraphicsBuffer` constructors, 366 managed array constructor syntax hits, and one `List<RaycastResult>` compatibility buffer. A literal "no `new` anywhere" claim would be false.

Solution: Centralize the four visor shader pass tags in `HectonVisorShaderTagIds` and route `HectonFillrateDepthPrepassFeature`, `HectonHalfResParticlesFeature`, and `HectonOverdrawHeatmapFeature` through that cache. This removes duplicate tag fields and removes per-pass tag construction from `HectonHalfResParticlesFeature`. Record the broad residual scan instead of hiding it.

Rejected Alternatives: Moving `new ShaderTagId(...)` behind a factory was rejected because it hides the same value construction and makes the text gate less honest. Removing or disabling `GraphicsBuffer` owners was rejected because those are Unity GPU resource wrappers for actual presentation visuals; replacing them with no-op fallbacks would be a fake optimization. Running dotnet/Roslyn/build was rejected because CPU was 100.0%, above the project gate.

Scalability potential: Low tier can keep these GPU resource paths disabled or low-capacity through existing quality gates. Middle tier keeps bounded constant/indirect buffers. High and Ultra keep the visual-overkill GPU paths; the shader tag cache reduces duplicate setup without changing quality authority.

Hardware Impact: 0 us normal-frame speed claimed. Cold value construction surface for shader pass tags is reduced from 10 to 4 constructors, and one pass no longer rebuilds tag IDs per pass instance. The remaining `GraphicsBuffer` constructors are resource ownership debt, not hidden managed hot-loop allocations.

## Decision 028 - Runtime Span Constructor Text Gate

Problem: A stricter runtime UI/Visor text scan found explicit `new System.Span<char>` and `new System.ReadOnlySpan<char>` constructor syntax in `RelayHUDElement`, `SettingsPanel`, `UISliderValueDisplay`, and `PDADataLogTab`. These constructors are stack-only value views, not heap allocations, but they keep the APEX "no hidden `new` in runtime text paths" gate noisy. The same scan also exposed 94 remaining `ToCharArray()` cold static caches, so a literal "all text cache construction is gone" claim would be false.

Solution: Replace the remaining explicit span constructors with `.AsSpan(...)` on the existing string/char-array sources. Remove `PDADataLogTab`'s playback timer `ToCharArray()` cache and pass `PlaybackTimerTemplate.AsSpan()` directly to `LocNumericBuffer`. Re-run runtime UI/Visor scans excluding Editor and record residual broad counts instead of hiding them.

Rejected Alternatives: Leaving the constructors as "safe because Span is a ref struct" was rejected because the user requested a strict textual gate. Replacing all 94 cold `ToCharArray()` caches in this pass was rejected because several are static UI label caches shared across broad runtime surfaces and require a separate fixed literal/span design, not a blind churn patch. Running dotnet/build was rejected because the user ordered rare dotnet/build attempts and the previous known build wall was non-domain Audio/Tether.

Scalability potential: Low tier keeps fixed preallocated text buffers and no per-update string construction. Middle tier keeps the same UI cadence. High and Ultra keep the same visual presentation paths; this patch changes proof hygiene, not quality authority or visual density.

Hardware Impact: 0 us normal-frame speed claimed. The practical gain is stricter static evidence: explicit runtime span constructor hits are now 0, while remaining cold caches/resource owners are documented as debt rather than falsely reported as clean.

## Decision 029 - Changed-File Added-Diff Truth Gate

Problem: The repeated APEX review demanded proof over every changed/created file, not just a narrow post-patch scan. A broad full-source grep is noisy because it catches Unity value-type constructors, cold UI construction, GPU resource ownership, and editor-only rebuild paths. A fake "no `new` anywhere" claim would be false.

Solution: Split the verification into two evidence layers. Layer 1 parses only added lines in the current UI/Visor diff and reports exact residual `new` line numbers. Layer 2 runs full runtime scans excluding Editor for the hot managed patterns and architecture hazards. Result: added-diff forbidden managed patterns are zero except 7 classified `new` residuals: two cold compass `GraphicsBuffer` allocations at `DiegeticGyroCompassRuntime.cs:1449-1450`, one editor-only `RendererEntry[]` rebuild at `HectonDryVolumeStencilSource.cs:336`, and four shared `ShaderTagId` value constructors at `HectonFillrateDepthPrepassFeature.cs:17-20`.

Rejected Alternatives: Replacing `GraphicsBuffer` with a fake no-op would break the compass GPU route. Hiding `ShaderTagId` construction behind a factory would preserve the same value construction with worse proof. Removing the editor `RendererEntry[]` would destroy the serialized dry-volume source rebuild path. Running `dotnet build` or Roslyn CLI was rejected because the user explicitly ordered rare dotnet/build attempts and the last compile wall remains outside 1309's domain.

Scalability potential: Low tier can keep GPU resource paths disabled or low-capacity through existing quality gates and fixed caps. Middle tier keeps deterministic UI/visor routing. High and Ultra keep the visual-overkill GPU paths; no binary quality switch, DTO layout mutation, or authority reroute was introduced.

Hardware Impact: 0 us measured and 0 us normal-frame speed claimed. This pass is release-proof hardening. The exact runtime text gate now reports 0 for `ToString`, `string.Format`, LINQ/Enumerable, interpolation, `foreach`, `throw new`, `.Complete`, `TryGetLatestCreated`, native allocator constructors, explicit spans, scene search, direct player-context service, and direct AUP float helpers.


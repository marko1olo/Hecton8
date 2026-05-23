## 2026-05-22 SHINOBU_318 Start
What was wrong: Armor evaluation still depended on generic scalar armor and legacy local-point/headshot fakes; exact AUP-to-target LUT armor was absent.
What was done: Started archaeology and selected `CombatDamageRuntime` partial integration route.
Cinematic Cheats used: Dear Lie volumetric LUT will replace body-part collider truth.
Exact Microseconds saved: PENDING VERIFICATION. No code modification yet.

## 2026-05-22 SHINOBU_318 Implementation
What was wrong: Armor damage had no target-local material LUT evaluator in `CombatDamageRuntime`; existing global signal ingestion converted `ImpactAup` into a generic runtime point and could not resolve chitin/steel strength without collider/body-part logic. `VisualFlareSignal` was unusable for armor feedback because it carries screen UV/intensity only. `FaunaColliderValidator` stripped mesh colliders but did not strip redundant primitive damage hitboxes. Residual legacy `FaunaBrain.TakeDamage` paths remain documented compatibility debt outside the new ballistic armor route.

What was done: Added `Assets/_Project/Scripts/Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs`. Wired `CombatDamageRuntime` to preserve `CombatDamageSignal.ImpactAup`, refresh target AUP/rotation/extents, evaluate an 8x6 fixed-byte `ArmorProfileDTO` LUT in the Burst damage job, and subtract health through float-bit CAS. Routed deflection feedback through existing `DeflectSignal` and material/AUP feedback through existing `ImpactSignal`. Added 300-frame armor telemetry ring and `Docs/AgentLogs/Dump_SHINOBU_318.bin` dump path for NaN/over-budget samples. Added `BallisticArmorXRayWindow`, `ArmorLutDebugGizmo`, `OOP_Hitbox_Scanner`, `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_318.json`, and `SHINOBU_318_SELF_AUDIT.xml`. Extended `FaunaColliderValidator` to strip redundant primitive damage hitboxes in editor validation. Added route card: `Docs/ARCHITECTURE/SHINOBU_318_ARMOR_PENETRATION_LUT_ROUTE_CARD.md`.

Cinematic Cheats used: Physical armor plates are replaced by an 8x6 byte LUT projected from AUP-local hit deltas. Chitin/steel material is encoded in two high bits; thickness uses six low bits. `GlobalQualityWeight` smoothly blends cheap base armor toward detailed LUT armor and controls feedback intensity. Surface normal is a dominant-axis box fake, not a collider normal.

Exact Microseconds saved: Existing-owner integration: estimated 8us saved by avoiding a second manager/registry path. Existing signal lanes: estimated 3us saved by avoiding a new signal flush lane. Removed collider/Transform hot path for armor: estimated 30us saved on i3/MX350 for a 50-target pellet burst; profiler pending. Byte LUT lookup versus body-part raycast target remains under 10us for 50 armored crab shotgun burst; profiler pending. Compile proof: NOT RUN. Guard blocked it because a separate `dotnet` process was active and CPU sampled at 98-100%.

## 2026-05-22 SHINOBU_318 Vault Polish / Static Audit
What was wrong: The first implementation still carried armor-side private persistent native arrays and the local Vault candidate `71620..71630` collided with SHINOBU_158 buoyancy/SIMD lanes. `TryGetArmorTuning` / `TryGetArmorDebugBuffers` could route through an ensure path, which violated pure read accessor doctrine. The QA mock Burst generator had an explicit completion but no Vault lock proof.

What was done: Replaced SHINOBU_318 armor persistent containers with `VaultGenerationHandle<T>` descriptors and transient `ArmorPenetrationVaultViews`. Runtime scheduling resolves buffers from `GlobalDataVault`, locks the eight hot armor lanes during `ProcessDamageQueueJob`, registers the job with `H8Memory.RegisterActiveJob(SystemID.GameplayCombat, handle)`, and unlocks at completion/finalization/shutdown. Read accessors now use `ensure=false` and `TryReadHandle` only. Moved Vault lanes to local numeric `73580..73590` after exact collision scan. Added `Hecton8.AI` using for editor facade files that inspect `FaunaBrain`. Updated `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and `SHINOBU_318_ARMOR_PENETRATION_LUT_ROUTE_CARD.md`.

Cinematic Cheats used: Armor truth is an 8x6 byte LUT over localized AUP delta, not body-part colliders. Surface normals are dominant-axis box normals. Deflect/impact presentation reuses unmanaged signal lanes; no CPU gore/particle object path was added.

Exact Microseconds saved: Vault migration saves no direct ALU but removes stale-pointer/compaction risk. BufferID collision repair saves 0us and prevents cross-domain memory corruption. Pure read accessors avoid hidden editor/player diagnostic memory work, estimated 1-5us when debug panels poll. Core armor raycast removal estimate remains 30us for a 50-target pellet burst on i3/MX350; profiler proof pending.

<SELF_AUDIT agent="SHINOBU_318" date="2026-05-22">
  <TASK_RECONCILIATION>
    <Task id="01" status="PASS">`rg` archaeology over combat/fauna hit routes performed.</Task>
    <Task id="02" status="PASS">Partial integration in `CombatDamageRuntime`; no competing armor manager.</Task>
    <Task id="03" status="PASS">Existing `DeflectSignal` and `ImpactSignal` reused; no `ArmorBouncedSignal`.</Task>
    <Task id="04" status="PASS">Editor validator/scanner strips/reports redundant fauna damage colliders; raw prefab YAML deletion rejected.</Task>
    <Task id="05" status="PASS">Hot damage route uses native queue/Burst job, not `SendMessage` or managed damage objects.</Task>
    <Task id="06" status="PASS">`GenerateMockArmorImpactSignalsJob` exists, deterministic, editor/development only, Vault locked during job.</Task>
    <Task id="07" status="PASS">AUP impact-target subtraction is double precision before local float mapping.</Task>
    <Task id="08" status="PASS">8x6 `ArmorGridLUT` byte lookup replaces body-part raycasts.</Task>
    <Task id="09" status="PASS">Mitigation/deflect uses scalar math and `math.select`; material branch limited to byte identity route.</Task>
    <Task id="10" status="PASS">Deflect/impact VFX are unmanaged signal writes; `VisualFlareSignal` rejected for missing AUP/material fields.</Task>
    <Task id="11" status="PASS">`GlobalQualityWeight` smooth-blends base armor toward LUT armor and feedback intensity.</Task>
    <Task id="12" status="PASS">Health mutation uses float-bit CAS through `Interlocked.CompareExchange`.</Task>
    <Task id="13" status="PASS">Damage job uses `FloatMode.Deterministic`; finite guards applied to AUP/local/health math.</Task>
    <Task id="14" status="PASS">Armor side memory is Vault-owned; overwritten lanes use `UninitializedMemory`.</Task>
    <Task id="15" status="PASS">300-frame telemetry ring and binary dump path implemented.</Task>
    <Task id="16" status="PASS">UI Toolkit `BallisticArmorXRayWindow` implemented.</Task>
    <Task id="17" status="PASS">Cold `ReadOnlySpan<byte>` CSV parser implemented and editor/dev file I/O guarded.</Task>
    <Task id="18" status="PASS">Scene debug gizmo implemented through editor facade.</Task>
    <Task id="19" status="PASS">`OOP_Hitbox_Scanner` writes physics optimization report.</Task>
    <Task id="20" status="PASS_STATIC_ONLY">Static scans, route card, ledger, and rationale updated; Unity compile/profiler proof pending CPU/dotnet guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <ArmorProfileDTO size="64">SpeciesHashID uint @0 size4; BaseHealth float @4 size4; BaseArmor float @8 size4; _pad0 uint @12 size4; ArmorGridLUT fixed byte[48] @16 size48; total 64.</ArmorProfileDTO>
    <ArmorPenetrationTuningDTO size="64">Eight scalar/uint fields in first 32 bytes plus four ulong pads; exact 64-byte row.</ArmorPenetrationTuningDTO>
    <ArmorPenetrationTelemetryEntry size="64">Frame/impact counters and scalar/hash lane through byte 40 plus uint/ulong pads to 64.</ArmorPenetrationTelemetryEntry>
    <ArmorPenetrationDebugHitDTO size="96">double3 AUP at @0, float3 lanes and scalar/hash metadata through @72, explicit padding to 96.</ArmorPenetrationDebugHitDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below quality 0.3 the solver biases toward cheap `BaseArmor * 0.65` and low-intensity feedback. Middle weights blend through `Smooth01`. High/ultra weights use the LUT strength and material-rich impact signal intensity. Quality never changes DTO layout, buffer IDs, health authority, or save identity.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_arrays="0">Armor persistent lanes are `73580..73590`, acquired via `VaultGenerationHandle<T>` from `GlobalDataVault`. Only transient `NativeArray<T>` views are passed to jobs/editor/parser methods.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>NoAlias is present on non-overlapping job arrays. Runtime consumes the current `_damageJobHandle` dependency state, schedules `ProcessDamageQueueJob`, registers it with `H8Memory`, and completion is through `LateFrameTick`, `CanMutateTargets`, or `Shutdown`, not a hidden same-frame `.Complete()`.</POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>No new asmdef or sibling runtime reference was added. Existing combat files reside in the pre-existing `Hecton8.Core` assembly; this pass did not expand its reference list.</COMPILE_GUARD>
  <DEAR_LIE>Before: mesh/body-part raycasts are O(log broadphase + collider traversal) with scene sync cost. After: O(1) byte LUT lookup plus local AUP delta math in Burst.</DEAR_LIE>
  <VERIFICATION>Static scans found no SHINOBU_318 runtime `Physics.Raycast`, `RaycastCommand`, hot LINQ/foreach, private persistent armor `NativeArray`, DTO getter/setter properties, or `Pack=1`. Exact BufferID scan found `73580..73590` only in SHINOBU_318 source/docs.</VERIFICATION>
  <COMPILE_GATE status="BLOCKED">Active `dotnet` processes and CPU load `100%` were observed on 2026-05-22; no dotnet rebuild was launched. `git diff --check` returned LF-to-CRLF warnings only.</COMPILE_GATE>
</SELF_AUDIT>

## 2026-05-22 SHINOBU_318 Mock Lane Isolation / Locked-Vault Polish
What was wrong: The Vault resolver still treated QA mock request/detail/AUP lanes as part of the core `ensure:true` armor route. That meant runtime boot/scheduling could require cold test buffers that are not part of gameplay truth. The first allocation-lock guard was also too blunt: it protected against `EnsureGenerationHandle`, but it would block existing descriptor recovery after DataVault service replacement. Default tuning initialization wrote through a resolved NativeArray without a writer lock.

What was done: `TryResolveArmorPenetrationVaultViews` now has `includeMock`, defaulting false. Runtime, parser, editor reads, and scheduling resolve only lanes `73580..73587`; `GenerateMockArmorImpacts` is the sole `includeMock:true` caller for lanes `73588..73590`. `TryResolveArmorVaultBuffer` now performs existing-descriptor-first recovery under `IDataVault.IsAllocationLocked` and returns false before allocation-capable `EnsureGenerationHandle` can run. Default tuning hydration moved into `TryWriteDefaultArmorTuning`, guarded by `TryAcquireWriteLock`/`ReleaseWriteLock`.

Cinematic Cheats used: The mock generator remains cold QA proof only. The hot route is still the AUP-local 8x6 byte LUT; no PhysX hitbox, raycast, collider tree, or managed VFX object path was introduced.

Exact Microseconds saved: Runtime common path removes three mock buffer descriptor/growth checks from `ensure:true` armor resolution; expected savings are low single-digit microseconds during cold boot/repair only. Main frame win remains the O(1) LUT replacement for body-part raycasts; profiler still gated by CPU/dotnet policy.

Verification: Static scan after patch: `HectonCombatRuntime_ArmorPenetration.cs` braces `136/136`, preprocessor `2/2`. Focused forbidden-token scan shows no armor runtime `Physics.Raycast`, `RaycastCommand`, hot `foreach`, private persistent armor `new NativeArray`, DTO property setter, or `Pack=1`; the only SHINOBU_318 `.Complete()` remains the documented editor/development mock generator.

## 2026-05-22 SHINOBU_318 Subagent Static Audit Integration
What was wrong: Turing found four valid risks. `WriteSignalImpactAup` was reachable from damage enqueue and could call `TryResolveArmorPenetrationVaultViews(ensure:true)`. Armor Vault locks could remain stuck if `ProcessDamageQueueJob.Schedule()` or mock generation threw in editor/safety checks. CSV and tuning writer locks lacked `finally`. The telemetry dump path performed synchronous managed disk I/O in the runtime fault branch. `FaunaColliderValidator` could delete non-root non-trigger primitive colliders that were not explicit damage hitboxes.

What was done: `WriteSignalImpactAup` now uses `ensure:false`; if the AUP lane is unavailable, the solver falls back to `CombatDamageSignalDetail.LocalPoint` rather than allocating in enqueue. `FrameTick` scheduling also uses `ensure:false`, so core armor allocation remains in cold init/register/editor paths. Runtime job lock ownership and mock generation are wrapped in `try/finally`; mock unlock handles partial mock-lock failure without double unlock. Default tuning, live tuning writes, and CSV profile ingestion release DataVault writer locks in `finally`. Telemetry faults now set a pure dump-request flag; only editor/development builds synchronously emit `Dump_SHINOBU_318.bin`, and the interpolated runtime `Debug.LogError` was removed. Primitive collider deletion now requires explicit damage name, damage layer, or damage component markers.

Cinematic Cheats used: No change to gameplay truth. Armor remains the AUP-local 8x6 byte LUT; the new work removes side effects around the fake, not the fake itself.

Exact Microseconds saved: Hot enqueue no longer has an allocation-capable Vault path, avoiding a worst-case frame stall rather than a stable per-hit ALU number. Lock `finally` guards save recovery/debug time by preventing stuck Vault lanes after safety exceptions. Collider stripping is less aggressive, trading some possible broadphase savings for prefab safety.

Verification: Static scan after this pass: armor runtime braces `144/144`, preprocessor `3/3`; `CombatDamageRuntime` braces `199/199`; `FaunaColliderValidator` braces `32/32`. Focused forbidden-token scan still shows only pre-existing `CombatDamageRuntime` persistent NativeArray allocation, scanner string literals in the editor facade, and the editor/development mock `.Complete()`.

Compile gate: XML and JSON proof artifacts parse successfully. `git diff --check` on touched SHINOBU_318 files reports LF-to-CRLF warnings only. No build launched: CPU sampled `82%` with active `csc.exe` PID 13316 and `dotnet.exe` PID 17724, which violates the project build guard.

## 2026-05-22 SHINOBU_318 DataVault Hot-Swap Recovery
What was wrong: Moving damage enqueue and frame scheduling to `ensure:false` removed hot allocation risk, but it also exposed a recovery gap: if `GlobalDataVault` was unavailable at first static initialization or replaced later, armor lanes could stay unopened until another cold mutation path ran.

What was done: Added a cold `ArmorRegistryHotSwapBridge` registered from `EnsureArmorPenetrationNativeState`. On `GlobalRegistryServiceSlot.DataVault` replacement, SHINOBU_318 releases old owner-local generation handles, caches the new Vault, and reopens core lanes outside the damage path. If a damage job is currently using the lanes, the rebind is deferred through `_armorVaultRebindPending` and applied after `FinishArmorPenetrationScheduledCompletion`.

Cinematic Cheats used: None added. This preserves the existing Dear Lie route while removing a lifecycle edge case.

Exact Microseconds saved: Normal-frame cost is 0us; no polling was added. The gain is failure-mode elimination: late Vault replacement no longer requires reintroducing allocation-capable combat scheduling.

Verification: Static scan after the hot-swap bridge: armor runtime braces `155/155`, preprocessor `3/3`; focused forbidden-token scan remains limited to the editor/development mock `.Complete()`, editor scanner string literals, and pre-existing base `CombatDamageRuntime` persistent allocations.

## 2026-05-22 SHINOBU_318 Burst Metadata Bounds Vaccination
What was wrong: The Burst damage job trusted packed `detailIndex`, damage class, and armor class before indexing native arrays. Current ingress writes valid metadata, but corrupted payloads or stale queue rows should not be able to force out-of-range reads.

What was done: Added an unsigned bounds check before `SignalDetails[detailIndex]`. Packed damage class and armor class are clamped to the 8x8 `DamageArmorLut` envelope before indexing.

Cinematic Cheats used: No change. This hardens the math route around the existing 8x6 armor LUT fake.

Exact Microseconds saved: No speed claim. This spends two clamps and a rare branch per signal to avoid undefined native array access. The cost is lower than any PhysX body-part query and stays inside the same Burst job.

Verification: Static scan after the guard: armor runtime braces `155/155`, combat runtime braces `200/200`, validator braces `32/32`; focused forbidden-token scan unchanged.

## 2026-05-22 SHINOBU_318 Guarded Compile Attempt
Command: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:BuildInParallel=false`

What was wrong: Build proof was previously blocked by active compiler processes and high CPU. The guard later cleared: CPU sampled below 50% and no `dotnet/csc/VBCSCompiler` process was active.

What was done: Ran one narrow core build. It failed on external compile wall errors outside the SHINOBU_318 armor files: unresolved VRSomatic/HorizonLock symbols, HandIK unassigned locals, KCC metabolism contract constants, and one external `CombatDamageRuntime_StatusEffects.cs` `math.select` ambiguity.

Cinematic Cheats used: None. Compile validation only.

Exact Microseconds saved: No runtime claim from compile validation.

Result: No compiler diagnostic was emitted for `HectonCombatRuntime_ArmorPenetration.cs`, `CombatDamageRuntime.cs`, `ArmorPenetrationEditorFacade.cs`, or `FaunaColliderValidator.cs`. Per 3-strike protocol this is an external compile wall; no unrelated domain edits were made.

## 2026-05-22 SHINOBU_318 Registered Tool-Hit Collider Metadata Purge
What was wrong: `ToolHitUtility.TryQueueCentralDamage` still used `ResolveLocalizedHit(hitCollider, ...)` and `Transform.InverseTransformPoint(hitPoint)` for registered combat targets. That preserved the old child-collider weakspot path even though the new armor solver is AUP/LUT based.

What was done: Added a public AUP-bearing `CombatDamageRuntime.TryQueueDamage(in CombatDamageRequest, in CombatDamageSignalDetail, double3 impactAup)` ingress. `ToolHitUtility` now resolves finite hit-point AUP for registered targets, sets weakspot metadata to `None`, leaves `LocalPoint` zero as non-authoritative fallback detail, and lets `ProcessDamageQueueJob` localize via `impactAup - targetRootAup`. Removed dead `ResolveLocalizedHit`, `ICombatWeakspot`, and `ICombatLimbHealthSource` surfaces. Extended `OOP_Hitbox_Scanner` scope to include `ToolHitUtility.cs`.

Cinematic Cheats used: The registered tool-hit route now uses the same Dear Lie as ballistic damage: one AUP delta and one 8x6 byte LUT lookup replace collider-local weakspot interpretation.

Exact Microseconds saved: Estimated 2-8us during dense registered tool hit bursts on i3/MX350 by removing two component/interface probes and one transform inverse. No profiler claim; source-only proof because build rerun is currently CPU-guarded.

Verification: Superseded by the following fallback-correction entry. Registered armor route `InverseTransformPoint(hitPoint)=0`; total ToolHitUtility legacy fallback `InverseTransformPoint(hitPoint)=1`.

## 2026-05-22 SHINOBU_318 Tool-Hit AUP Fallback Correction
What was wrong: The registered tool-hit armor route no longer used child-collider metadata, but it could still queue `impactAup = double3.zero` when the player pose bridge was unavailable. Separately, proof artifacts claimed `InverseTransformPoint(hitPoint)=0` across the full ToolHitUtility scan even though the unregistered legacy `IDamageReceiver` fallback still has one transform-local path outside registered armor authority.

What was done: `ToolHitUtility.TryQueueCentralDamage` now fails closed if a finite hit-point AUP cannot be resolved. `TryResolveImpactPointAup` tries the player pose bridge first and then falls back to `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(hitPoint)` for finite runtime-space hits. `OOP_Hitbox_Scanner` now reports registered armor method counts separately from the unregistered fallback.

Cinematic Cheats used: The registered armor path remains AUP delta -> 8x6 LUT. Missing AUP no longer becomes a center-cell visual/armor lie; it is rejected.

Exact Microseconds saved: No speed claim. One finite guard and fallback conversion are added; the registered route still avoids the transform inverse and component weakspot probes.

Verification: Current focused scan excluding scanner self reports `Physics.Raycast=0`, `RaycastCommand=14`, `SendMessage("ApplyDamage")=0`, `IDamageable=0`, `TakeDamage(`=3, `ICombatWeakspot=0`, `ICombatLimbHealthSource=0`, `ResolveLocalizedHit=0`, total `InverseTransformPoint(hitPoint)=1`, registered armor route `InverseTransformPoint(hitPoint)=0`, registered `double3.zero` fallback=0. Braces/preprocessor: tool utility `59/59 1/1`, editor facade `65/65 1/1`. Build rerun skipped by guard: CPU `100%`, compiler process output `0`.

## 2026-05-22 SHINOBU_318 Mock Burst NoAlias Proof Sync
What was wrong: The source patch added `[NoAlias]` to the cold `GenerateMockArmorImpactSignalsJob`, but the durable proof artifacts still only documented production job aliasing. That leaves a false audit gap: Task 06 is an explicit Burst job and must advertise its memory-disjoint contract even though it is editor/development gated.

What was done: Synced the route card, binary ledger, JSON scanner report, and XML self-audit with the exact mock aliasing proof: all six NativeArray fields are now `[NoAlias]` (`InstanceIds`, `TargetRootAups`, `TargetHalfExtents`, `Requests`, `Details`, `ImpactAups`). The runtime route remains unchanged.

Cinematic Cheats used: No new simulation. This preserves the same Dear Lie: synthetic QA impacts enter the same AUP -> 8x6 LUT path instead of creating test-only colliders or raycasts.

Exact Microseconds saved: No gameplay claim. The mock path is not a production frame path. Editor/development stress runs give Burst clearer alias facts for vectorized loads/stores; production savings remain the O(1) LUT route replacing body-part physics.

Verification: Static source check after patch reported armor runtime braces `155/155`, preprocessor `3/3`, mock job `NoAlias=6`, and clean `git diff --check` for `HectonCombatRuntime_ArmorPenetration.cs`. XML parse returned `mockBurstNoAliasNativeArrays=6`; JSON parse returned `mockBurstNativeArrayNoAliasHits=6`. Corrected focused scan excluding the editor scanner self-string reports armor `Physics.Raycast=0` and `RaycastCommand=14` deferred sensory/leg-IK hits. Build rerun remains forbidden: latest guard sampled CPU `100%` with active `csc`, `dotnet`, and `VBCSCompiler`.

PROMPT IDENTIFIED: SHINOBU_318
DOMAIN: Echelon 5 Combat & Survival Physiology / Armor Penetration LUT
TASK COUNT: 20
STATUS: ACTIVE POLISH PASS / STATIC AUDITED / EXTERNAL COMPILE WALL / POST-COMPILE SOURCE PATCH GUARDED BY CPU

## Mandates Read
- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Signal_Lane_Segregation.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt

## Archaeology Facts
- `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs` is the existing damage owner. Competing manager rejected.
- `Assets/_Project/Scripts/Gameplay/Combat/BallisticsRuntime.cs` already emits `CombatDamageSignal` from mathematical AABB hits.
- `Assets/_Project/Scripts/Core/GlobalSignals.cs` owns `CombatDamageSignal`, `DeflectSignal`, `ImpactSignal`, and `VisualFlareSignal`.
- `VisualFlareSignal` lacks AUP/material fields. Existing `DeflectSignal` and `ImpactSignal` are the usable feedback routes without inventing a new lane.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` still exposes managed `TakeDamage` calls and primitive collider validation. Mesh colliders are already rejected at runtime.
- `Assets/_Project/Scripts/Editor/FaunaColliderValidator.cs` now strips MeshCollider and redundant primitive damage hitboxes in editor validation.
- `GlobalDataVault` exposes pointer-free `VaultGenerationHandle<T>` descriptors plus `TryReadHandle`, `TryResolveHandle`, `TryLockBuffer`, and writer-lock APIs. Armor side data now uses Vault handles, not private persistent armor `NativeArray` fields.
- Static BufferID scan proved `71620..71630` is owned by SHINOBU_158 buoyancy/SIMD. Armor lanes moved to free local numeric `73580..73590`.

## Loop 1: Tasks 01-05
- [x] Task 01 MANDATORY_CODEBASE_GREP_SCAN | DOD: exact `rg` scan over Combat/Fauna surfaces found no armor `Physics.Raycast`, residual `FaunaBrain.TakeDamage`, MeshCollider guard, and WeakPoint steering references. Alternative rejected: memory-based architecture. Estimate: 35us saved per chaotic combat frame by avoiding duplicate owner scans.
- [x] Task 02 PARTIAL_CLASS_INTEGRATION_MANDATE | DOD: `CombatDamageRuntime` extended as partial with `HectonCombatRuntime_ArmorPenetration.cs`. Alternative rejected: new `HectonArmorManager`. Estimate: 8us saved by avoiding second dispatcher/lookup route.
- [x] Task 03 SIGNALBUS_MATRIX_VERIFICATION | DOD: consumed `CombatDamageSignal`, reused `DeflectSignal`/`ImpactSignal`, rejected `VisualFlareSignal` for missing AUP/material payload. Alternative rejected: new `ArmorBouncedSignal`. Estimate: 3us saved from no extra lane flush.
- [x] Task 04 MULTI_COLLIDER_INQUISITION | DOD: `FaunaColliderValidator` strips mesh/redundant primitive hitboxes only when explicit damage naming/layer/component markers are present, and scanner reports suspects. Alternative rejected: broad deletion of non-root non-trigger child colliders because movement/proximity/interaction colliders can be legitimate. Estimate: scene-dependent broadphase savings pending Unity prefab run.
- [x] Task 05 MANAGED_DAMAGE_EVENT_PURGE | DOD: new solver consumes `CombatDamageSignal` and mutates flat native health buffers by CAS. Alternative rejected: `SendMessage`/`IDamageable` hot routing. Estimate: 20us saved under swarm damage bursts.

## Loop 2: Tasks 06-10
- [x] Task 06 EMERGENCY_MOCK_COMBAT_SCENARIO | DOD: `GenerateMockArmorImpactSignalsJob` Burst-fills deterministic request/detail/AUP arrays and queues through normal route. Alternative rejected: waiting for AI spawn path. Estimate: test-only, runtime disabled.
- [x] Task 07 BURST_AUP_LOCALIZATION_KERNEL | DOD: `double3` AUP subtraction before float downcast and inverse rotation in `EvaluateArmorPenetrationForSignal`. Alternative rejected: absolute float conversion. Estimate: correctness over speed; prevents edge-map misses.
- [x] Task 08 THE_DEAR_LIE_LUT_MAPPING | DOD: 8x6 fixed-byte LUT in `ArmorProfileDTO` maps material/strength in O(1). Alternative rejected: mesh collider/body-part raycast. Estimate: 50 armored crab shotgun burst under 10us target pending profiler.
- [x] Task 09 BRANCHLESS_MITIGATION_MATH | DOD: strength masks, `math.select`, scalar max, and smooth quality blend. Alternative rejected: managed material decision tree. Estimate: 2us saved per 128 hits.
- [x] Task 10 ASYNCHRONOUS_VFX_ROUTING | DOD: deflect/impact feedback through existing unmanaged lanes. Alternative rejected: managed VFX callback. Estimate: 3us saved from no managed fanout.

## Loop 3: Tasks 11-15
- [x] Task 11 CONTINUOUS_SCALABILITY_LUT_INTERPOLATION | DOD: `GlobalQualityWeight` smooth-blends base armor and LUT armor. Alternative rejected: low/high hardware branch. Estimate: ALU shedding pending profiler.
- [x] Task 12 ATOMIC_HEALTH_MUTATION | DOD: `Interlocked.CompareExchange` float-bit CAS over native health buffer. Alternative rejected: non-atomic health write. Estimate: correctness under pellet fanout.
- [x] Task 13 ROLLBACK_NETCODE_STATE_FENCE | DOD: deterministic Burst job mode retained; finite guards sanitize AUP/local/health. Alternative rejected: platform-fast float math. Estimate: deterministic correctness.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS | DOD: armor side Vault lanes use `UninitializedMemory` where overwritten and `ClearMemory` only for tuning/telemetry; persistent ownership is `GlobalDataVault` generation handles. Alternative rejected: private persistent arrays and blanket `MemClear`. Estimate: low microsecond saved per buffer.
- [x] Task 15 TELEMETRY_COMBAT_RECORDER | DOD: 300-entry ring records NaN/over-budget and sets a dump-request flag; editor/development builds write `Dump_SHINOBU_318.bin`, release runtime avoids synchronous managed file I/O. Alternative rejected: blocking production fault path disk write. Estimate: forensic coverage, overhead <0.05ms target.

## Loop 4: Tasks 16-20
- [x] Task 16 ARMOR_LUT_TUNER_WINDOW | DOD: UI Toolkit `BallisticArmorXRayWindow`. Alternative rejected: runtime UI. Estimate: editor-only.
- [x] Task 17 CSV_ARMOR_PROFILES_INGESTOR | DOD: cold `ReadOnlySpan<byte>` parser for `fauna_armor_luts.csv`. Alternative rejected: managed per-row CSV objects. Estimate: boot-only.
- [x] Task 18 LIVE_HITBOX_DEBUG_GIZMO | DOD: `ArmorLutDebugGizmo` draws LUT cells and latest hits. Alternative rejected: runtime debug GameObjects. Estimate: editor-only.
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | DOD: `OOP_Hitbox_Scanner` writes shared/dedicated physics reports. Alternative rejected: manual grep report. Estimate: editor-only.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: layout offsets recorded, no SHINOBU_318 runtime `Physics.Raycast`/`RaycastCommand`/LINQ/foreach/private persistent armor `NativeArray`/DTO properties/`Pack=1`; BufferID collision repaired to `73580..73590`; runtime resolver no longer allocates QA mock lanes unless explicitly requested by the cold mock generator; route card and binary ledger updated. Alternative rejected: final chat claim. Estimate: no runtime cost. STATUS: compile/profiler proof remains gated by active dotnet/CPU policy until a safe build window.

## Iteration Log
- Iteration 1: archaeology complete; existing `CombatDamageRuntime` selected as owner.
- Iteration 2: runtime partial added; missed scanner self-count and fixed exclusion.
- Iteration 3: validator/editor tooling added; preliminary BufferID candidates rejected after sovereignty scan found occupied lanes.
- Iteration 4: text-level audits found balanced braces in new files and no armor Physics.Raycast/SendMessage route.
- Iteration 5: compile attempt deferred; `dotnet` process active and CPU sampled at 100%, so build launch is forbidden by prompt guard.
- Iteration 6: Vault archaeology removed SHINOBU_318 private persistent armor arrays; runtime now uses `VaultGenerationHandle<T>` plus transient views and explicit job buffer locks.
- Iteration 7: static BufferID scan found `71620..71630` collision with SHINOBU_158; armor lanes moved to `73580..73590` and documented in ledger/route card.
- Iteration 8: verification gate sampled active `dotnet` processes and CPU load `100%`; per project rule no dotnet rebuild was launched. `git diff --check` reported only LF->CRLF warnings.
- Iteration 9: resolver polish split hot core Vault lanes from cold QA mock lanes. `TryResolveArmorPenetrationVaultViews(ensure:true)` now creates only core armor state by default; mock request/detail/AUP buffers require `includeMock:true`. Allocation-locked Vault recovery now tries existing SHINOBU-owned generation descriptors before `EnsureGenerationHandle`, and default tuning writes use a DataVault writer lock. Static scan: braces `136/136`, preprocessor `2/2`; only remaining `.Complete()` is the documented editor/QA mock generator.
- Iteration 10: subagent static audit integrated. Hot `WriteSignalImpactAup` and `FrameTick` scheduling now use `ensure:false`; core Vault allocation remains cold init/register/editor. Job lock ownership and mock generation are protected by `try/finally`. Tuning/CSV writer locks are exception-safe. Runtime fault path now records a dump request and only editor/development writes the synchronous dump. Primitive collider deletion narrowed to explicit damage-hitbox markers. Static scan: armor runtime braces `144/144`, preprocessor `3/3`; `CombatDamageRuntime` braces `199/199`; `FaunaColliderValidator` braces `32/32`.
- Iteration 11: XML and JSON proof artifacts parse successfully. `git diff --check` on touched files reports LF-to-CRLF warnings only. Compile remains forbidden by project guard: CPU sampled `82%` with active `csc.exe` and `dotnet.exe`.
- Iteration 12: cold DataVault recovery added without hot polling. `EnsureArmorPenetrationNativeState` registers an armor hot-swap bridge; DataVault replacement releases old generation handles and reopens SHINOBU-owned lanes from the registry event. If a damage job is in flight, rebind is deferred until the owner completion window. Static scan after patch: armor runtime braces `155/155`, preprocessor `3/3`; focused forbidden-token scan unchanged.
- Iteration 13: Burst solver metadata reads hardened. `ProcessDamageQueueJob` now bounds-checks `detailIndex` before `SignalDetails[detailIndex]` and clamps packed damage/armor class indices before indexing the 8x8 damage armor LUT. Static scan: armor runtime `155/155`, combat runtime `200/200`, validator `32/32`.
- Iteration 14: guarded `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:BuildInParallel=false` ran after CPU/compiler guard cleared. Result: external compile wall, 53 errors outside SHINOBU_318 touched armor files (`VRSomaticProvider*`, `PlayerKinematicsRuntime_HandIK`, `HydrodynamicKccRuntime`, and external `CombatDamageRuntime_StatusEffects.cs`). No diagnostics were emitted for `HectonCombatRuntime_ArmorPenetration.cs`, `CombatDamageRuntime.cs`, `ArmorPenetrationEditorFacade.cs`, or `FaunaColliderValidator.cs`.
- Iteration 15: cross-domain registered tool-hit ingress hardened. `ToolHitUtility.TryQueueCentralDamage` now sends impact AUP into `CombatDamageRuntime.TryQueueDamage(..., double3 impactAup)` and no longer derives weakspot/limb metadata from child colliders or `Transform.InverseTransformPoint(hitPoint)`. Dead collider-local `ResolveLocalizedHit`, `ICombatWeakspot`, and `ICombatLimbHealthSource` surfaces were removed. Scanner scope now includes `ToolHitUtility.cs`. Focused static counts over Combat/Fauna/ToolHit excluding scanner self: registered armor route `InverseTransformPoint(hitPoint)=0`; total legacy unregistered fallback `InverseTransformPoint(hitPoint)=1`; `Physics.Raycast=0`, `SendMessage("ApplyDamage")=0`, `IDamageable=0`, `ICombatWeakspot=0`, `ICombatLimbHealthSource=0`, `ResolveLocalizedHit=0`; `RaycastCommand=14` remains deferred sensory/leg-IK, not armor. Braces/preprocessor before fallback polish: armor runtime `155/155 3/3`, combat runtime `193/193 0/0`, editor facade `57/57 1/1`, tool utility `58/58 1/1`, validator `32/32 0/0`. Build rerun skipped by guard: CPU sampled `93.07%`, no compiler process active.
- Iteration 16: subagent static audit integrated and proof wording corrected. `ToolHitUtility.TryQueueCentralDamage` now fails closed if hit-point AUP is non-finite and no longer initializes armor ingress with `double3.zero`; `TryResolveImpactPointAup` uses player pose first and falls back to `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(hitPoint)` for finite runtime-space hits. `OOP_Hitbox_Scanner` now reports method-scoped registered armor counts separately from the unregistered legacy `IDamageReceiver` fallback. Current focused scan: `Physics.Raycast=0`, `RaycastCommand=14`, `SendMessage("ApplyDamage")=0`, `IDamageable=0`, `TakeDamage(`=3, `ICombatWeakspot=0`, `ICombatLimbHealthSource=0`, `ResolveLocalizedHit=0`, total `InverseTransformPoint(hitPoint)=1`, registered armor route `InverseTransformPoint(hitPoint)=0`, registered `double3.zero` fallback=0. Braces/preprocessor: tool utility `59/59 1/1`, editor facade `65/65 1/1`. Build rerun forbidden by guard: CPU sampled `100%`, no compiler process output.
- Iteration 17: pointer aliasing audit tightened. `GenerateMockArmorImpactSignalsJob` now annotates all six NativeArray fields with `[NoAlias]` in addition to `[ReadOnly]`/`[WriteOnly]`, matching the production `ProcessDamageQueueJob` aliasing discipline. Static check: armor runtime braces `155/155`, preprocessor `3/3`, mock job `NoAlias=6`; `git diff --check` for the armor runtime file returned clean.
- Iteration 18: durable proof artifacts synchronized after the mock NoAlias patch. `LOG_SHINOBU_318.md`, route card, binary ledger, JSON scanner report, and XML self-audit now record mock job `NoAlias=6` and keep the runtime route unchanged. Verification: XML parse OK with `mockBurstNoAliasNativeArrays=6`; JSON parse OK with `mockBurstNativeArrayNoAliasHits=6`; corrected focused scan excluding scanner self reports `Physics.Raycast=0`, `RaycastCommand=14`; braces/preprocessor remain balanced. Build rerun still forbidden: latest guard sampled CPU `100%` with active `csc`, `dotnet`, and `VBCSCompiler`.

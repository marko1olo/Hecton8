PROMPT IDENTIFIED: 1701
DOMAIN: Echelon 5 Combat & Survival Physiology + Presentation HUD
TASK COUNT: 30
STATUS: SOURCE + UNITY MESH IMPORT VERIFIED AFTER LOOP 36 / BUILD NOT LAUNCHED BY ACTIVE UNITY COMPILE AND CPU THROTTLE

## Assignment Source
- Extracted from `Docs/Tasks/CURRENT_BATCH.md` with CLI regex for `<AGENT_PROMPT id="1701">`.
- Target files named by prompt: `Assets/_Project/Scripts/Gameplay/Survival/HectonSurvivalSystem.cs`, `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`.
- Primary blockers: RB-004 runtime chevron mesh generation; missing SHINOBU_323 barotrauma oxygen coupling; missing unmanaged hypoxia agony window.

## Mandate Selection
- [x] `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt` | Survival oxygen/pressure formulas, one-shot fatal route, HUD zero-GC constraint.
- [x] `DATA_Runtime_Struct_Layout_ARM64.txt` | DTO layout, byte offsets, multiple-of-8 proof, runtime bool ban.
- [x] `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` | Hot-path allocation ban, zero-GC HUD numeric formatting.
- [x] `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` | GlobalDataVault ownership, generation handles, lock/fence discipline.
- [x] `ARCH_Execution_Phases.txt` | SIMULATION truth mutation and VISUAL_SYNC presentation split.
- [x] `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` | Cold DI and hot-path registry polling ban.
- [x] `ARCH_Signal_Lane_Segregation.txt` | Unmanaged typed SignalBus lane requirements.
- [x] `UI_Data_Streaming_ZeroGC_Optimization.txt` | TMP `SetCharArray`, TryFormat spans, UI cadence rules.

## Authority Docs Read
- [x] `TASTE.md`, `ui.md`, `survival.md`, `gameplay.md`, `performance.md`, `systems.md`, `data.md`, `telemetry.md`, `physics.md`
- [x] `Docs/ARCHITECTURE/SHINOBU_323_SUIT_INTEGRITY_DEPTH_CRUSH_ROUTE_CARD.md`

## Discovery Notes
- Prompt path `Assets/_Project/Scripts/Gameplay/Survival/` is absent in this checkout.
- Live survival class found at `Assets/_Project/Scripts/HectonSurvivalSystem.cs`.
- Live HUD target found at `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`.
- Existing physiology contracts/stacks found under `Assets/_Project/Scripts/Core/Contracts/Physiology/` and `Assets/_Project/Scripts/Physiology/`.

## Loop 0 - Bootstrap
- [x] Prompt extracted | DOD: exact XML block for id 1701 read from current batch using CLI; task count parsed as 30 | Alternative rejected: relying on user summary or archive prompt | Estimate: 30-80 us local regex scan after file read
- [x] Status and rationale files initialized | DOD: fresh files created because both mandated files were missing | Alternative rejected: reading archived batch state | Estimate: no runtime cost

## Loop 1 - Archaeology and Contract Authority
- [x] Tasks 01-05 | DOD: live survival/HUD files found; SHINOBU_323 route and first-party signals mapped | Alternative rejected: creating duplicate survival owner or new death signal | Estimate: 0 runtime us
- [x] Tasks 06-10 | DOD: hot GlobalRegistry calls avoided; MetabolicStateDTO extended to 48 bytes with explicit offsets and multiple-of-8 validators | Alternative rejected: stealing padding without validator updates | Estimate: DTO copy remains one cache-local state write

## Loop 2 - Survival Simulation Integration
- [x] Tasks 11-15 | DOD: barotrauma drain multiplier reads SuitIntegrityDTO; oxygen zero arms 4 s agony timer; chevron runtime mesh builder removed | Alternative rejected: pressure damage inside HectonSurvivalSystem or runtime Mesh allocation | Estimate: removes cold mesh generation path and avoids extra simulation owner
- [x] Tasks 16-20 | DOD: HUD uses static chevron mesh, smoothstep oxygen deception, and DataVault write lock with try/finally ReleaseWriteLock | Alternative rejected: TMP string formatting and multi-lock transaction | Estimate: one MetabolicStateDTO write lock only, no string allocation added

## Loop 3 - Integration Polish
- [x] Tasks 21-25 | DOD: compaction fence checks before/inside vault write; bounded handle retries for late buffers; no dotnet build launched per APEX throttle | Alternative rejected: per-frame handle polling and rebuild spam | Estimate: retry cadence 30 frames, then cached handle reads

## Loop 4 - Layout and Source Gates
- [x] Tasks 26-29 | DOD: UnsafeUtility.SizeOf validators updated; no runtime chevron mesh tokens remain; no orphan/missing .meta output | Alternative rejected: JSON/binary proof artifacts after superseding directive | Estimate: no runtime I/O
- [x] Task 30 | DOD: source-only verification via rg/git diff --check/meta scans; JSON report intentionally not written by latest user directive | Alternative rejected: bloated metric report file | Estimate: 0 runtime us

## Loop 5 - Apex Polish Gate
- [x] Final source pass | DOD: removed temporary separate suit contract surface, kept SuitIntegrityDTO in existing Core physiology contract, added explicit 8-byte multiple validation, confirmed HUD has no physiology DataVault bridge | Alternative rejected: new helper contract file and HUD-side vault read | Estimate: no new steady-state cost

## Loop 6 - One-Owner Presentation Gate
- [x] Shader ownership correction | DOD: removed survival `_HypoxiaSignal` direct writer; routed hypoxia presentation through existing ShinobuSensoryImpairmentRuntime -> HectonShaderGlobalDataVaultBridge VISUAL_SYNC path | Alternative rejected: second shader global writer from survival LateFrameTick | Estimate: removes one survival late-frame shader write
- [x] DTO stale-flag correction | DOD: MetabolicIntegrationJob now forces `FlagHypoxia` to match `IsInHypoxia` every pass | Alternative rejected: masking stale flags in UI/rendering | Estimate: no allocation, one existing job flag operation

## Loop 7 - Hot Math Polish Gate
- [x] Barotrauma curve scalar polish | DOD: replaced `math.pow(x, 2f)` with direct multiply in oxygen drain multiplier | Alternative rejected: libm-backed exponent call for fixed quadratic curve | Estimate: one multiply instead of exponent helper in slow survival tick
- [x] Vault lock flattening polish | DOD: hoisted oxygen scalar sanitization and survival signal source resolution before `TryAcquireWriteLock`; locked region now only copies/assigns one DTO row | Alternative rejected: finite checks and lazy ID resolution inside the write lock | Estimate: shorter critical section, no extra allocation

## Loop 8 - Parser Throttle Boundary
- [x] Roslyn AST host-binding boundary recorded | DOD: local `csi` was unavailable; PowerShell Roslyn parse with local dependencies failed on `Roslyn.Utilities.StringTable`; CPU stayed at 100 with external Unity/dotnet processes active, so no `dotnet build` or MSBuild route was launched | Alternative rejected: false AST OK or build spam under throttle violation | Estimate: 0 runtime cost

## Loop 9 - ABI Default Polish Gate
- [x] KCC mock metabolism ABI completion | DOD: `HydrodynamicKccRuntime` mock `MetabolicStateDTO` initializer now writes `RealO2=1`, `AgonyTimeRemaining=0`, `IsInHypoxia=0` | Alternative rejected: relying on zero default for newly added oxygen fields | Estimate: no added allocation, three direct field stores in existing mock generation job
- [x] Physiology owner route rechecked | DOD: `ShinobuMetabolismRuntime` and `ShinobuSuitIntegrityRuntime` both own relevant buffers under `SystemID.GameplayPlayer`, matching survival handle filters | Alternative rejected: weakening handle validation to any owner | Estimate: no runtime change

## Loop 10 - Apex Reverification Gate
- [x] Post-KCC source gates | DOD: `git diff --check` clean except CRLF notices; HUD chevron/DataVault scan empty for runtime mesh and physiology bridge tokens; survival scan has no `_HypoxiaSignal`, `math.pow`, LINQ, `new Mesh`, `.Complete()`, or `WaitForCompletion` in modified routes | Alternative rejected: trusting previous pre-KCC gate | Estimate: 0 runtime us
- [x] Hot lookup proof refresh | DOD: `TryGetComponent`/`GlobalRegistry` hits in targets map to bootstrap, cold refresh, save restore, parser, or existing hierarchy repair; `LateFrameTick` steady solve requires ready hierarchy and does not call `AutoResolve`/`TryGetComponent` | Alternative rejected: broad refactor of unrelated HUD cold bootstrap code | Estimate: no new steady-state lookup cost
- [x] Meta hygiene scoped honestly | DOD: orphan `.meta` scan returned no output; missing `.meta` scan found three pre-existing/out-of-domain files (`FaunaRigBuilder1714.cs`, `RockSculptorEngine1713.cs`, `EquipmentMetadata.cs`) not touched by 1701 | Alternative rejected: editing/deleting unrelated fauna/geology/interaction assets from physiology/HUD domain | Estimate: 0 runtime us
- [x] Build throttle rechecked | DOD: CPU sampled at 100 and Unity `VBCSCompiler.dll` dotnet process was active, so `dotnet build` remained blocked by APEX throttle | Alternative rejected: build spam under explicit throttle ban | Estimate: 0 runtime us

## Loop 11 - Dear Lie Curve Polish Gate
- [x] Hot-method contextual scan | DOD: 34 hot method bodies scanned for forbidden `GlobalRegistry.Get`, `GetComponent`, `TryGetComponent`, `.Complete`, `WaitForCompletion`, `new Mesh`, LINQ, `ToString`, `string.Format`, and unbounded collection allocation tokens; zero hits | Alternative rejected: broad file grep as proof because cold bootstrap tokens create false positives | Estimate: 0 runtime us
- [x] Oxygen display curve corrected | DOD: Dear Lie low-band now uses `real * (2 - smoothstep)` below 15%, making displayed oxygen stay above real oxygen throughout the low band while preserving exact 0 and 15% endpoints | Alternative rejected: sqrt/pow/inverse trigonometry curves and HUD-side MetabolicStateDTO vault read | Estimate: one existing `smoothstep`, one subtraction, one multiply in VISUAL_SYNC only
- [x] Loop 11 verification | DOD: `git diff --check` clean except CRLF notices; formula samples prove low-band display >= real at 1/5/7.5/10%; RB-004 token scan still has no runtime mesh builder; touched asset `.meta` coverage is complete; Assets-wide missing meta is a pre-existing out-of-domain test file | Alternative rejected: launching build while CPU sampled 100 and dotnet processes were active | Estimate: 0 runtime us

## Loop 12 - Chevron AUP Hoist Polish Gate
- [x] Camera AUP hoisted out of chevron slot loop | DOD: `BuildThreatChevronMatrices` now resolves camera `AbsoluteUniversePosition` once per VISUAL_SYNC frame and passes it by `in` to each slot matrix builder | Alternative rejected: leaving per-slot AUP reconstruction and floating-origin finite checks | Estimate: saves up to three redundant AUP conversions/checks per active four-chevron frame
- [x] Loop 12 verification | DOD: hot-method contextual scan still covers 34 methods with zero forbidden hits; focused HUD scan shows no runtime mesh builder or `new Mesh`; `git diff --check` has no whitespace errors beyond CRLF warnings; build throttle still blocks compile because CPU sampled 100 with active dotnet | Alternative rejected: claiming compile proof under active compiler load | Estimate: 0 runtime us

## Loop 13 - Safety Semantics Separation Gate
- [x] Haptic/status oxygen source corrected | DOD: `EvaluateCriticalHapticCoupling` and `ResolveStatusKeyHash` now consume `realOxygen`; only gauge/color/fog presentation keeps the nonlinear Dear Lie display scalar | Alternative rejected: allowing visual oxygen inflation to delay safety feedback | Estimate: no new operations, existing scalar argument swap
- [x] Loop 13 verification | DOD: 39 hot method bodies scanned with zero forbidden hits; focused RB-004 scan shows only serialized static mesh and instanced draw path; touched Unity asset `.meta` coverage is complete; orphan `.meta` count is zero; `git diff --check` reports no whitespace errors beyond CRLF warnings | Alternative rejected: launching `dotnet build` while CPU sampled 100 with active dotnet workers | Estimate: 0 runtime us

## Loop 14 - Visual Sync Handle Retry Gate
- [x] Metabolic visual read retry bounded | DOD: `ShinobuSensoryImpairmentRuntime` now retries the metabolism state generation handle at most every 30 frames while missing/stale, preserving pure `TryReadOnlyHandle` for steady-state reads | Alternative rejected: per-frame generation lookup from VISUAL_SYNC or forcing a write/read lock for pure readback | Estimate: removes up to 29 redundant failed metadata lookups per missing-buffer window
- [x] Loop 14 verification | DOD: 42 hot method bodies scanned with zero forbidden hits; `git diff --check` reports no whitespace errors beyond CRLF warnings; build throttle still blocks compile because CPU sampled 100 with active dotnet | Alternative rejected: build spam under APEX throttle | Estimate: 0 runtime us

## Loop 15 - Oxygen API Atomicity Gate
- [x] Surface/refill/drain oxygen staging | DOD: `UpdateOxygen` surface recovery, `ApplyOxygenRefill`, and `DrainOxygen` now stage `MetabolicStateDTO` before mutating local oxygen and return on DataVault lock denial | Alternative rejected: relying on later grace-state fallback or local-only public API mutation | Estimate: one existing DTO write route, no new allocation
- [x] Loop 15 verification | DOD: contextual hot-method scan returned zero forbidden tokens; one write lock surface remains guarded by `finally ReleaseWriteLock`; `git diff --check` reports CRLF warnings only | Alternative rejected: declaring compile proof under active CPU/dotnet throttle | Estimate: 0 runtime us

## Loop 16 - Capacity Multiplier Atomicity Gate
- [x] Runtime capacity multiplier staging | DOD: `SetRuntimeOxygenCapacityMultiplier` now stages the clamped oxygen/hypoxia DTO, rolls back multiplier on lock denial, and uses `math.clamp` instead of `Mathf.Clamp` | Alternative rejected: capacity clamp silently changing effective `RealO2` outside the metabolism route | Estimate: no hot-loop cost, public API path only
- [x] Loop 16 verification | DOD: contextual hot-method scan returned `HOT_METHOD_FORBIDDEN_TOKEN_HITS=0`; touched Unity asset `.meta` coverage is complete; orphan `.meta` count is zero; build remains throttled by CPU=100 and active dotnet | Alternative rejected: build spam or out-of-domain meta churn | Estimate: 0 runtime us

## Loop 17 - Signal/Pocket/Load Oxygen Coherence Gate
- [x] Critical signal and localized oxygen pocket staging | DOD: `ConsumeOxygenCriticalSignals` and `TryApplyLocalizedOxygenPocket` stage metabolic oxygen state before local oxygen mutation or return on lock denial | Alternative rejected: relying on the next grace-state pass to repair signal/pocket drift | Estimate: one existing DTO route, no new allocation
- [x] Load restore metabolic sync | DOD: `LoadFromSaveData` writes normoxic or hypoxia DTO immediately after restoring oxygen state | Alternative rejected: allowing stale DataVault state until the first slow tick after load | Estimate: cold load only
- [x] Loop 17 verification | DOD: oxygen assignment audit shows remaining simulation assignments are post-staging; contextual hot-method scan returns zero forbidden tokens; `git diff --check` reports CRLF warning only | Alternative rejected: build launch under CPU=100/dotnet active throttle | Estimate: 0 runtime us

## Loop 18 - Active Agony Timer Coherence Gate
- [x] Active agony stale-write correction | DOD: removed the `_oxygenGraceActive` pre-write from `UpdateOxygen`; `UpdateOxygenGraceState` now remains the single post-decrement DTO publisher for active agony | Alternative rejected: writing the old timer before decrement and masking it with a second write later | Estimate: removes one redundant DTO write attempt per active agony slow tick
- [x] Loop 18 verification | DOD: exact brace-bounded hot-method scan returns zero forbidden tokens after the correction; lock release remains centralized in `TryWriteMetabolicOxygenStateToVault` | Alternative rejected: broad window grep that misclassified helper/cold code | Estimate: 0 runtime us

## Loop 19 - Offline Chevron Asset Binding Gate
- [x] RB-004 asset-side closure | DOD: added `Assets/_Project/Art/Meshes/M_HUD_ThreatChevron.asset` plus `.meta`, and bound `Suit_HUD_Canvas.prefab` `_threatChevronStaticMesh` to GUID `f842ecd7ee1f487fa7e631164e81fbea` | Alternative rejected: waiting on compiling Unity editor to run runtime/editor mesh generation code | Estimate: removes runtime mesh construction path; steady state remains one instanced draw
- [x] Loop 19 verification | DOD: mesh byte counts match `indexCount` and `m_DataSize`; focused RB-004 scan has no runtime builder/`new Mesh`; touched Unity assets all have `.meta`; orphan `.meta` count is zero; build remains throttled by active Unity VBCSCompiler | Alternative rejected: claiming Unity import/build proof while editor is compiling | Estimate: 0 runtime us

## Loop 20 - Unity Mesh Import Repair Gate
- [x] Baked mesh asset re-authored through Unity | DOD: Unity `AssetDatabase` reports `M_HUD_ThreatChevron.asset` as `UnityEngine.Mesh`, GUID `f842ecd7ee1f487fa7e631164e81fbea`, 8 vertices, 12 indices, one submesh | Alternative rejected: keeping hand-written YAML after Unity reported `assetType=Unknown` | Estimate: runtime still one cached static mesh draw
- [x] Loop 20 verification | DOD: shader contract reads only POSITION/TEXCOORD0; Unity console has no 1701 file errors; console blockers are out-of-domain `Quest/MissionMarkerSystem.cs`; `validate_script` false duplicate reports were checked by `rg`; orphan `.meta` count remains zero | Alternative rejected: editing quest domain under physiology/HUD authority | Estimate: 0 runtime us

## Loop 21 - Visual Sync Metabolic Handle Fallback Gate
- [x] Stale handle fallback tightened | DOD: failed `TryReadOnlyHandle` in `TryReadMetabolicStateBuffer` now clears the cached metabolism handle and resets retry once before a refresh attempt | Alternative rejected: waiting 30 frames after a stale generation read miss | Estimate: failure-path only; steady-state read remains one cached `TryReadOnlyHandle`
- [x] Loop 21 verification | DOD: exact hot-method scan covers 65 bodies with zero forbidden tokens; `MetabolicStateContract` validates clean; `ShinobuSensoryImpairmentRuntime` duplicate validator warning is an `rg`-verified false positive on `MutationGuardBit` call sites | Alternative rejected: adding a read lock to VISUAL_SYNC | Estimate: 0 steady-state runtime us

## Loop 22 - Final Static Hygiene Gate
- [x] Runtime mesh eradication rechecked | DOD: focused scan of `SuitHUDV4CanvasOverlay`, `HectonSurvivalSystem`, and `ShinobuSensoryImpairmentRuntime` returned no `BuildThreatChevronMesh`, append helper, `new Mesh`, or runtime vertex/index setter tokens | Alternative rejected: accepting the prefab binding without proving the old generator stayed removed | Estimate: 0 runtime us
- [x] Hot definition scan rechecked | DOD: declaration-bounded scan of 18 hot method bodies returned `HOT_METHOD_FORBIDDEN_TOKEN_HITS=0`; cold `TryGetComponent` and CSV collection allocation hits remain outside simulation/update definitions | Alternative rejected: broad `rg` proof that misclassifies cold bootstrap/save-load code | Estimate: 0 runtime us
- [x] Asset/meta hygiene rechecked | DOD: touched chevron mesh `.meta` exists; orphan `.meta` scan returned no rows; scoped `git diff --check` reports CRLF warnings only | Alternative rejected: deleting or editing unrelated assets under other agents' ownership | Estimate: 0 runtime us
- [x] Compile throttle honored | DOD: CPU sampled at 100 and active `dotnet` processes were present, so no `dotnet build` was launched | Alternative rejected: build spam under explicit APEX throttle | Estimate: 0 runtime us

## Loop 23 - Agony Timer Fail-Closed Polish Gate
- [x] Agony timer staged before local commit | DOD: `UpdateOxygenGraceState` now computes next timer/blur locally, stages `MetabolicStateDTO`, and commits `_oxygenGraceTimer`, `_oxygenGraceActive`, vignette, and movement multiplier only after the write route does not report lock/fence denial | Alternative rejected: decrementing local agony during DataVault contention and repairing the DTO later | Estimate: freezes one agony tick under contention instead of drifting truth
- [x] Invalid writer view fails closed | DOD: `TryWriteMetabolicOxygenStateToVault` now treats acquired-but-invalid/empty writer views as `lockDenied=true`, causing callers to abort local oxygen/timer mutation | Alternative rejected: returning false without lock denial and allowing local state to diverge from the vault | Estimate: failure path only
- [x] Loop 23 verification | DOD: declaration-bounded hot-method scan across survival/sensory/HUD reports 18 definitions and 0 forbidden tokens; `git diff --check` on `HectonSurvivalSystem.cs` reports CRLF warning only; brace balance remains zero from prior source pass | Alternative rejected: launching build while CPU sampled 100 with active `dotnet` | Estimate: 0 steady-state runtime us

## Loop 24 - Oxygen Gauge Presentation Consistency Gate
- [x] O2 numeric display aligned to Dear Lie scalar | DOD: `SuitHUDV4CanvasOverlay.RefreshVisuals` now leaves `UIStateStore.OxygenCurrent` truthful but passes a display-only `oxygenCurrentDisplay` into the O2 gauge, scaled by `ResolveDearLieOxygenCurrentDisplay` from the same smoothstep-derived presentation oxygen | Alternative rejected: changing the headless UI truth slot or delaying safety haptics/status | Estimate: one multiply/rcp branchless scalar in VISUAL_SYNC
- [x] Loop 24 verification | DOD: targeted hot ranges for HUD `RefreshVisuals`, `UpdateGauge`, Dear Lie helpers, survival agony state, and DataVault writer returned zero case-sensitive forbidden token hits; HUD `SetText`/`.text`/`ToString` scan returned no hits; HUD brace balance is zero; `git diff --check` reports CRLF warning only | Alternative rejected: `dotnet build` under CPU=100 with active `dotnet` workers | Estimate: 0 heap bytes

## Loop 25 - Stale Console And Validator False-Positive Gate
- [x] Scanner marker console errors checked against current source | DOD: Unity console listed missing `HectonScanMarkerSystem` symbols, but current source no longer contains those references and `validate_script` reports zero diagnostics for that file | Alternative rejected: patching non-existent line-518 symbols or reintroducing runtime shader/mesh generation | Estimate: 0 runtime us
- [x] Large-file validator duplicates reconciled with `rg` | DOD: `MetabolicStateContract` validates clean; HUD validates with no errors; `HectonSurvivalSystem`/sensory duplicate warnings are line-0 validator false positives confirmed by one actual definition for each reported method/helper | Alternative rejected: deleting valid call sites or splitting existing methods into duplicate owners | Estimate: 0 runtime us
- [x] Build throttle rechecked | DOD: CPU sampled 88 and active `dotnet` PID 3100 remained, so `dotnet build` was not launched | Alternative rejected: violating explicit compile throttle | Estimate: prevents build worker contention

## Loop 26 - Scanner Marker Authored Mesh Closure
- [x] Scanner marker mesh split from HUD chevron mesh | DOD: created `Assets/_Project/Art/Meshes/M_ScannerMarkerQuad.asset` with 4 vertices, 6 indices, UV0, data size 80, and GUID `17f1ed7ac819d334982c9f9eac9c16a6`; `Tool_Scanner_Held.prefab` now binds scanner marker mesh to that GUID instead of the threat-chevron mesh | Alternative rejected: sharing the chevron mesh with the scanner marker shader despite incompatible UV/shape contract | Estimate: 0 runtime allocation; one authored quad draw source
- [x] Scanner marker source hygiene | DOD: `ScannerTool.cs` and `HectonScanMarkerSystem.cs` validate without errors; focused token scan found no `new Mesh`, runtime mesh setters, shader path loading, or `new Material` in the scanner marker path | Alternative rejected: restoring the old runtime mesh/material generator | Estimate: removes runtime mesh/material construction path
- [x] Asset hygiene | DOD: scanner marker `.asset` and `.meta` both exist, local mesh-folder orphan `.meta` count is zero, prefab mesh/material GUIDs resolve to dedicated scanner quad and existing instanced marker material | Alternative rejected: binding prefab to a deferred/unknown asset without sidecar proof | Estimate: 0 runtime us

## Loop 27 - Cold Load Reset Metabolic Sync Retry Gate
- [x] Save-load metabolic sync flag corrected | DOD: `LoadFromSaveData` now assigns `_metabolicOxygenStateSyncedThisTick` from the actual `TryWriteMetabolicOxygenStateToVault` result for both normoxic and hypoxia restore branches | Alternative rejected: ignoring write failure and leaving a stale prior sync flag across load | Estimate: cold path only
- [x] Respawn reset metabolic sync flag corrected | DOD: `ResetToMax` now records whether the normoxic DTO write succeeded instead of discarding the result | Alternative rejected: assuming a respawn vault write succeeded while compaction or handle retry denied it | Estimate: cold path only
- [x] Loop 27 verification | DOD: edited survival windows have zero case-sensitive forbidden token hits, brace balance is zero, and scoped `git diff --check` reports CRLF warning only; Unity MCP validator was not claimed because the local MCP transport failed during validation | Alternative rejected: false clean validator report | Estimate: 0 runtime heap bytes

## Loop 28 - Scanner Marker Instance Data Gate
- [x] Scanner marker instanced alpha restored | DOD: `HectonScanMarkerSystem` now preallocates a `Vector4[64]` instance-data mirror, creates one cold `MaterialPropertyBlock`, writes `_InstanceData.x/y` per visible marker, and passes the block to `DrawMeshInstanced` | Alternative rejected: shader default alpha with no instance payload, or runtime material mutation per marker | Estimate: 0 heap bytes in steady render; one cold MPB allocation
- [x] Scanner marker hot-source gate | DOD: declaration-bounded scan of `Tick`, `LateFrameTick`, `RenderMarkers`, `BuildMarkerMatrices`, `UpdateMarkerTimers`, and `EnsureRuntimeResources` returned zero hot forbidden hits; focused scan found no runtime mesh/material construction or asset lookup tokens | Alternative rejected: broad substring grep that misclassified `MaterialPropertyBlock` as `Material` | Estimate: no new registry/component lookup and no sync completion
- [x] Loop 28 verification | DOD: scanner brace balance is zero; scoped `git diff --check` reports CRLF warning only; build was not launched because `VBCSCompiler.exe` was active despite CPU averaging 41 | Alternative rejected: violating compile throttle for a scanner presentation patch | Estimate: 0 runtime us

## Loop 29 - Cold MPB Prewarm Gate
- [x] HUD chevron MPB lazy branch removed | DOD: `_threatChevronPropertyBlock` is now a cold readonly field allocation and `EnsureThreatChevronRuntimeResources` no longer contains `??= new MaterialPropertyBlock()` | Alternative rejected: leaving a resource-ensure allocation branch in the RB-004 draw path | Estimate: 0 heap bytes in VISUAL_SYNC; one cold object at component construction
- [x] Scanner marker MPB lazy branch removed | DOD: `_markerPropertyBlock` is now a cold readonly field allocation; `EnsureRuntimeResources` only validates/binds authored mesh/material references | Alternative rejected: lazy MPB creation in a helper included by marker setup/resource validation | Estimate: no object allocation branch in marker render/resource gate
- [x] Loop 29 verification | DOD: declaration-bounded hot scan over HUD/scanner draw/resource methods returned `HOT_SCAN_HITS=0`; brace balances are zero; Assets-wide missing/orphan `.meta` counts are zero; build was not launched because CPU averaged 100 with active `dotnet` workers | Alternative rejected: build spam under APEX throttle | Estimate: 0 runtime us

## Loop 30 - DataVault Handle Race Gate
- [x] Compaction-fence refresh race closed | DOD: `TryWriteMetabolicOxygenStateToVault` now reports `lockDenied=true` if the fence rises during metabolic handle refresh | Alternative rejected: letting callers mutate local oxygen after a fence-raced refresh failure | Estimate: failure path only
- [x] Stale metabolic write handle recovery | DOD: failed write-lock acquisition without an active fence clears the cached metabolic handle and retry frame, forcing the next owner tick to refresh generation metadata | Alternative rejected: keeping a stale ready flag after generation rotation | Estimate: no extra lock, no second vault writer
- [x] Early dispatcher retry bounded | DOD: physiology handle retry gates fall back to `Time.frameCount` when `SystemDispatcher.CurrentFrameId` is still zero | Alternative rejected: unbounded generation lookups during early dispatcher bootstrap | Estimate: no hot allocation; one frame-count read on missing/stale handle paths
- [x] Loop 30 verification | DOD: declaration-bounded scan of 99 touched hot definitions returned zero forbidden tokens; runtime mesh/material blocking scan returned no rows; Assets-wide missing/orphan `.meta` counts are zero; build was not launched because CPU averaged 75.9 with active `dotnet` and `VBCSCompiler` | Alternative rejected: compile throttle violation | Estimate: 0 runtime us

## Loop 31 - Scanner Instance Payload Source Reconciliation
- [x] Scanner marker source corrected to match shader contract | DOD: `HectonScanMarkerSystem.RenderMarkers` now calls `_markerPropertyBlock.SetVectorArray(InstanceDataId, _markerInstanceDataMirror)` and passes the block to `DrawMeshInstanced`; `BuildMarkerMatrices` writes `_InstanceData.x/y` for every visible marker | Alternative rejected: letting `Hecton_ScannerMarkerInstanced.shader` fall back to default alpha because the old draw path passed `null` property data | Estimate: 0 heap bytes steady-state; fixed 64-vector mirror upload only while markers are visible
- [x] Loop 31 verification | DOD: declaration-bounded scanner hot scan returned `HOT_TOKEN_HITS=0`; runtime mesh/material blocking scan with word boundaries returned no rows; brace balance is zero; `git diff --check` reports CRLF warning only; build was not launched because CPU averaged 99.4 | Alternative rejected: violating build throttle under active host load | Estimate: 0 runtime us

## Loop 32 - Metabolic Writer Handle Release Integrity
- [x] Invalid writer-view recovery repaired | DOD: invalid/empty metabolic writer views now clear the cached handle and retry frame, but write-lock release uses a local `writeHandle` captured before acquisition so `finally ReleaseWriteLock` releases the exact acquired lock | Alternative rejected: clearing the cached field before `finally` and accidentally releasing a default handle | Estimate: failure path only; one stack-local handle copy
- [x] Loop 32 verification | DOD: survival writer-window scan shows one `TryAcquireWriteLock(in writeHandle)` and one `ReleaseWriteLock(in writeHandle)`; targeted survival hot scan returned `HOT_TOKEN_HITS=0`; brace balance is zero; scoped `git diff --check` reports CRLF warnings only; mesh asset `.meta` scan reports zero missing/orphan sidecars; build was not launched because CPU averaged 79.4 | Alternative rejected: compile throttle violation | Estimate: 0 steady-state runtime us

## Loop 33 - Critical Oxygen Signal Grace Phase Ordering
- [x] Double grace-state invocation removed | DOD: `ConsumeOxygenCriticalSignals` no longer calls `UpdateOxygenGraceState(0f)` inside `SlowTick`; the owner phase already calls `UpdateOxygenGraceState(dt)` once immediately afterward, preventing local agony timer decrement while the DTO still carries the pre-decrement value | Alternative rejected: keeping a same-tick zero-delta grace call that could desync local timer from `MetabolicStateDTO.AgonyTimeRemaining` | Estimate: removes one same-tick helper call on terminal oxygen signal frames
- [x] Loop 33 verification | DOD: `rg` confirms the private signal consumer is followed by a single owner-phase grace update; remaining zero-delta grace calls are public immediate oxygen mutation entry points; targeted survival hot scan returned `SURVIVAL_HOT_TOKEN_HITS=0`; brace balance is zero; scoped `git diff --check` reports CRLF warnings only; build was not launched because CPU averaged 94.2 | Alternative rejected: compile throttle violation | Estimate: 0 steady-state runtime us

## Loop 34 - Final Scoped Source Audit
- [x] Touched hot methods audited | DOD: declaration-bounded scan over survival, scanner marker, HUD chevron/gauge, and sensory hypoxia bridge hot definitions returned `TOUCHED_HOT_DEFINITIONS=23` and `TOUCHED_HOT_TOKEN_HITS=0` | Alternative rejected: broad grep proof that misclassifies cold bootstrap helpers | Estimate: 0 runtime us
- [x] Runtime mesh/material blocker audit re-run | DOD: focused blocker scan returned no `BuildThreatChevronMesh`, `AppendThreatChevron`, `new Mesh`, real `new Material`, runtime mesh setter, `Shader.Find`, `Resources.Load`, `AssetDatabase`, `WaitForCompletion`, or `.Complete()` rows in touched runtime routes | Alternative rejected: trusting previous pre-loop proof after scanner/survival patches | Estimate: 0 runtime us

## Loop 35 - Active Agony DTO Timer Preservation
- [x] Public zero-oxygen restage corrected | DOD: `ResolveStagedAgonyTimeRemaining` now preserves `_oxygenGraceTimer` when hypoxia is already active, so critical oxygen signals, `DrainOxygen`, and runtime capacity changes cannot reset `MetabolicStateDTO.AgonyTimeRemaining` back to 4.0 seconds mid-agony | Alternative rejected: duplicating per-call timer branches or allowing public APIs to extend the death window | Estimate: one scalar helper call on oxygen mutation paths only
- [x] Loop 35 verification | DOD: all old `nextHypoxiaState != 0 ? OxygenGraceDurationSeconds : 0f` staging sites are gone; targeted survival hot scan returned `HOT_SCAN_HITS=0`; brace balance is zero; `git diff --check` reports CRLF warning only; build was not launched because CPU averaged 77.3 and `VBCSCompiler` was active | Alternative rejected: build throttle violation | Estimate: 0 steady-state heap bytes

## Loop 36 - Terminal Respawn Signal Fail-Closed Gate
- [x] Respawn reset gated by signal enqueue | DOD: `CheckLethalConditions` now calls `PlayerDeathReconciliationBridge.RequestRespawn` into a bool and only runs `ApplyRespawnReconciliationSurvival` after `SignalBus<PlayerRespawnSignal>` accepts the request | Alternative rejected: resetting survival locally after a dropped cross-domain respawn signal | Estimate: death path only, no steady-state cost
- [x] Loop 36 verification | DOD: targeted survival hot scan including `CheckLethalConditions` returned `HOT_SCAN_HITS=0`; brace balance is zero; `git diff --check` reports CRLF warning only; build was not launched because `VBCSCompiler` was active even though CPU averaged 13 | Alternative rejected: compile throttle violation | Estimate: 0 steady-state heap bytes

## Task Checklist
- [x] Task 01 SURVIVAL_SYSTEM_STATIC_AUDIT
- [x] Task 02 HUD_CANVAS_OVERLAY_DECONSTRUCTION
- [x] Task 03 DTO_MEMORY_ALIGNMENT_INSPECTION
- [x] Task 04 SIGNAL_BUS_TOPOLOGY_MAPPING
- [x] Task 05 SHINOBU_323_MATHEMATICAL_MODELING
- [x] Task 06 GLOBAL_REGISTRY_HOT_POLLING_DETECTION
- [x] Task 07 COMPACTION_FENCE_VULNERABILITY_SCAN
- [x] Task 08 TELEMETRY_AND_REPORTING_ARCHITECTURE
- [x] Task 09 DTO_ALIGNMENT_AND_FIELD_INJECTION
- [x] Task 10 DETERMINISTIC_LINEAR_OXYGEN_DECAY
- [x] Task 11 SHINOBU_323_BAROTRAUMA_MULTIPLIER
- [x] Task 12 THE_FOUR_SECOND_AGONY_STATE
- [x] Task 13 TERMINAL_DEATH_SIGNAL_PUBLICATION
- [x] Task 14 AIRLOCK_RECOVERY_AND_STATE_RESET
- [x] Task 15 RB-004_RUNTIME_MESH_ERADICATION
- [x] Task 16 COMMAND_BUFFER_UI_RENDERING
- [x] Task 17 NON-LINEAR_SMOOTHSTEP_DECEPTION
- [x] Task 18 ZERO-GC_STRING_FORMATTING_IMPLEMENTATION
- [x] Task 19 HYPOXIA_VIGNETTE_INTERPOLATION
- [x] Task 20 DATA_VAULT_TRANSACTIONAL_LOCKING
- [x] Task 21 FAIL-CLOSED_VAULT_FALLBACKS
- [x] Task 22 HOT-SWAP_DEPENDENCY_INJECTION
- [x] Task 23 COMPILATION_WALL_AND_ASSEMBLY_HYGIENE
- [x] Task 24 DRY_RUN_VERIFICATION_EXECUTION
- [x] Task 25 BATCHED_COMPILATION_AND_SYNTAX_ASSERTION
- [x] Task 26 EXPLICIT_SIZEOF_VALIDATION_GATE
- [x] Task 27 COMPACTION_FENCE_RACE_CONDITION_AUDIT
- [x] Task 28 ZERO_GC_ALLOCATION_PROFILER_MOCK
- [x] Task 29 SHINOBU_323_LIMIT_TESTING
- [x] Task 30 AUTOMATED_METRIC_VALIDATOR_REPORT

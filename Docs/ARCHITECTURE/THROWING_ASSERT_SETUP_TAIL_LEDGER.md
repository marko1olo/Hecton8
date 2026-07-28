# Throwing-Assert Setup-Tail Ledger

Date: 2026-07-29
Status: CENSUS VERIFIED / REMEDIATION IN PROGRESS
Owner: ASSERT_SWEEP_REMAINDER_AND_LEDGER
Evidence class: STATIC_SOURCE (file:line, this tree) + RUNTIME_LOG (cited log lines only)

Accounting for one defect class so it does not get lost between remediation waves. Every row is a
`file:line` read in this tree. No Unity build, no dotnet build, and no profiler run was performed for this
document — where a claim is runtime-proven it names the log line, and everything else is static review.

## 1. The defect class

`UnityEngine.Assertions.Assert` **throws** in this project. Independently re-verified: `raiseExceptions`
appears nowhere under `Assets` as an assignment. The only three occurrences are prose inside comments that
document this very fact — `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs:3362`,
`Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs:1247`,
`Assets/_Project/Scripts/HectonVoxelEngine.cs:8073`.

Both halves of the damage matter, and every pre-585401145 repair attempt fixed only one:

1. **In-method**: statements below a firing assert are unreachable. Cleanup, state-set, null-out and
   latch-set code placed *after* the assert is dead code.
2. **Caller-tail**: the throw unwinds the caller, deleting the rest of the caller's setup tail — usually
   `OnEnable`/`Awake`, which is where registration and cache warming live.

### 1.1 Amplifier: the dispatcher has no per-tickable try/catch

Verified directly, because it changes the ranking for anything reachable from a tick:

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:5413` — `lane.GetAt(itemIndex).LateFrameTick();`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6332` — `tickable.SlowTick();`

Both are bare calls inside the lane loop. A throw out of any tickable therefore unwinds the whole lane
loop and every later lane in that phase — the blast radius of a tick-reachable assert is not confined to
the component that owns it.

### 1.2 Why this is expensive, not theoretical

`HectonVoxelEngine.EnsureVoxelBakeGhostMaterial` was a bare assert on a **cosmetic** material called from
`OnEnable`. It threw twice per run (`Logs/omega_route22.log:7192`, `:7809`) and killed seven following
statements including `MCTables.Initialize` (marching-cubes tables) and `CacheVoxelDeltaProcessorCold`, so
runtime voxel carving and delta-save replay were silently dead. The material was optional by construction:
its sole consumer at `HectonVoxelVolume.cs:4142-4144` already null-checks and falls back to `voxelMaterial`.
Fixed in commit `585401145`; the method now holds a one-shot `LogWarning` and zero asserts.

### 1.3 The four lost-tail classes that make a system silently inert

Ranking axis for section 4. A lost tail containing any of these produces a system that is *inert*, not
*visibly broken*, which is this codebase's named dominant failure mode:

1. a `GlobalRegistry.TryRegister*` / `SystemDispatcher.Register` call
2. a cache warm
3. a table init
4. a signal subscription

## 2. Census — independently recounted, and the brief's numbers are off in two places

Regex covered the complete `UnityEngine.Assertions.Assert` public surface: `IsTrue`, `IsFalse`,
`IsNotNull`, `IsNull`, `AreEqual`, `AreNotEqual`, `AreApproximatelyEqual`, `AreNotApproximatelyEqual`,
`raiseExceptions`. Also confirmed there is **no** `using static ...Assert` and **no** `using Assert =`
alias anywhere under `Assets/_Project`, so no bare `IsNotNull(...)` call form exists and the qualified
search is complete.

| Population | Count | Note |
|---|---:|---|
| Raw regex hits, first-party non-test `Assets/_Project/Scripts` | 48 | includes comment lines |
| — of which are **comment** lines, not calls | 5 | `HectonVoxelEngine.cs:8072`, `:8073`; `HectonMarineSnowRenderer.cs:3361`, `:3362`; `DataArchaeologyRuntime.cs:1247` |
| **Real call sites at the start of this wave** | **43** | in **22** files |
| Already in the correct shape | 4 | `DataArchaeologyRuntime.cs:1295-1296`, `HectonMarineSnowRenderer.cs:3370-3371` |
| Landed by siblings during this wave | 10 | see 3.2 |
| **Live real call sites at close of audit** | **33** | in **16** files; 43 − 10 = 33 reconciles exactly |
| **Genuinely remaining unfixed** | **29** | in **14** files (33 − the 4 correct-shape) |

The tree moved three times while this audit was in progress, so treat **43 real sites in 22 files** as the
stable baseline to reconcile future waves against, and the live figures as a dated snapshot. Line numbers in
section 4 are as-read and will drift as fixes land — anchor on the enclosing method name, not the line.

### 2.1 Where the brief's 44 / 23 came from

44 in 23 files is the census **before** commit `585401145`. `HectonVoxelEngine.cs` held exactly one site and
now holds zero, so post-fix the number is 43 in 22 files. Consequently the brief's "40 unfixed in 21 files"
should read **39 unfixed in 20 files** — the already-fixed voxel site was counted twice, once as fixed and
once as unfixed. The brief's claim that 4 sites in 2 files already carry the correct shape is exactly right
and corroborates the per-file counts.

### 2.2 The Debug.Assert exclusion set is right; the headline number is wrong

`Debug.Assert` logs `LogType.Assert` and does **not** throw. It is not this defect and must not be touched.
The brief lists the correct lines but says "11 sites". The true count is **14** call sites in 5 files —
`MainMenuController.cs:439-443` is five separate `Debug.Assert` calls, not two.

| File | Lines | Calls |
|---|---|---:|
| `Core/BinaryLayoutManifest.cs` | 960, 969, 979, 989, 997, 1006 | 6 |
| `MainMenuController.cs` | 439, 440, 441, 442, 443 | 5 |
| `Core/HectonArenaAllocator.cs` | 809 | 1 |
| `World/HybridTerrainSeamJobs.cs` | 112 | 1 |
| `World/WorldSpatialHashGrid.cs` | 1588 | 1 |
| **Total** | | **14** |

No change to the exclusion set — only to the count. Nothing here needs editing.

### 2.3 Out of first-party scope

`Assets/MapMagic/Tools/Matrix/MatrixWorld.cs` carries 2 throwing asserts. Third-party vendor tree, correctly
excluded from the 44.

## 3. Status by file

### 3.1 Already correct — do not re-open

| File | Sites | Shape |
|---|---|---|
| `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs` | 1295, 1296 | Reference template, `1263-1324`. Latch at 1265, cleanup via `DisableReconstructionAfterUnrecoverableSetupFailure()` at 1292 and one-shot log at 1293, both **above** the asserts, then return. |
| `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` | 3370, 3371 | Lane exit precedes the asserts; comment at 3361-3362 records why. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | — | Zero sites. Assert replaced by a one-shot guarded `LogWarning` at 8095-8110 in commit `585401145`. |

### 3.2 Landed by siblings during this wave — verified zero real sites remaining

| File | Sites removed | Sibling lane | Analysis retained in section 4 |
|---|---:|---|---|
| `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs` | 1 (was 646) | VFX/Debris | — |
| `Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs` | 1 (was 152) | VFX | — |
| `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` | 1 (was 1907) | World | — |
| `Assets/_Project/Scripts/World/HectonHLODRenderer.cs` | 2 (was 271, 272) | World | — |
| `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs` | 2 (was 895, 902) | VFX/UI | entry 3, kept as the record of what the fix had to preserve |
| `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs` | 2 (was 407, 408) | World | entry 15, same |
| `Assets/_Project/Scripts/Fabricator.cs` | 1 of 2 (was 3261) | Gameplay | entry 2, marked `LANDED` |

**`Fabricator.cs` is only half done.** The sibling removed `ResolveAssemblyFallbackMesh():3261` but the
`Awake()` site survives, now at **`Fabricator.cs:573`** (it shifted by one line), with its seven-statement
tail at 576-582 completely intact. It remains the highest-blast-radius site in the codebase and is still
unowned. Section 4 entry 1 stands.

The last two landed while this ledger was being written. All six still show as modified in the working tree,
uncommitted at the time of writing. Their section 4 entries are kept and marked `LANDED` rather than
deleted, because they document the tail that the fix had to preserve and are the check to review the fix
against. The CarveDebris
fix independently documented the same dispatcher amplifier recorded in 1.1: the missing-`Texture3D` branch
was reached from `SlowTick` at 10 Hz with no latch, so it fired roughly ten times per second of gameplay.

The `GroundPenetratingRadarRuntime` fix is the highest-value one landed so far and is worth reading as a
second template (`1913-1948`): the assert was the first statement of `EnsureRuntimeDrawResourcesCold()`,
called from `OnEnable:176`, and deleted a tail of `EnsureBlackBoxDumpPathCold`,
`GlobalRegistry.RegisterGroundRadarService(this)`, two cache warms, and three registrations —
`TryRegisterLateFrameTickable`, `TryRegisterSlowTickable`, `Renderables.TryRegister`.

## 4. The remaining 34 sites, ranked by blast radius

`Cadence` is how the enclosing method is reached. `Below` counts statements in the same block after the
first firing assert. `Tail` is what the caller loses. `Latched?` records whether a re-entry guard actually
prevents a repeat throw — several sites set their latch *below* the assert, which means the assert fires on
every call forever.

### Tier 1 — lost tail contains a registration, a cache warm or a table init

**1. `Assets/_Project/Scripts/Fabricator.cs:573`** — `Awake()`, setup. Guards serialized
`assemblyFallbackMesh`. **STILL LIVE — the top-priority remaining site.**
Below: **7** statements — `FlushEndAssemblyVisual()` (576), `ToolHapticsRuntime.EnsureRuntimeInstance()`
(577), `CacheThermalHostModule()` (578), `EnsureCraftingScratchCold()` (579),
`RebuildAssemblySourceCacheCold()` (580), `EnsureRecipeCache()` (581), `CacheFabricatorAup()` (582).
That is a table init plus four cache warms. Crafting goes silently inert on every Fabricator in the world.
Latched?: n/a, `Awake` runs once — but it runs once *per instance*, so the loss is total, not transient.
**Verdict: DOWNGRADE.** The field is optional by construction and this file proves it —
`ResolveAssemblyFallbackMesh()` at 3256-3264 already returns `assemblyFallbackMesh` when present and
`null` otherwise. This is the structural twin of the HectonVoxelEngine defect, in the crafting system, and
is the single highest-value remaining site.

**2. `Assets/_Project/Scripts/Fabricator.cs:3261`** — **LANDED by a sibling mid-audit; retained as the
review check.** `ResolveAssemblyFallbackMesh()`, cold resolve.
The assert sits in a branch reached **only** when `assemblyFallbackMesh == null`, because 3258-3259 already
returned for the non-null case. It is therefore an unconditional throw disguised as a null check, and the
`return null;` at 3264 — the documented fallback contract — is unreachable.
Below: 1 (`return null`). **Verdict: DOWNGRADE**, delete the assert; the `return null` *is* the contract.

**3. `Assets/_Project/Scripts/UI/DiegeticTooltipSystem.cs:895`** (and `:902`) — **LANDED by a sibling
mid-audit; retained as the review check.** Analysis as found: —
`EnsureResourceObjects()`, setup, reached from `EnsureResources():884` which is called from `OnEnable:345`
and again at `:434`.
Below, in-method: `_resolvedQuadMesh = glyphQuadMesh` (898), the validity fold (899-901), assert 902, the
cleanup `_resolvedQuadMesh = null` (905-906), `argsDirty = true` (907), and both `GraphicsBuffer`
allocations (910-917, 919+).
Tail: **7** statements — `EnsureBlackBox()` (346), `TryRegisterRuntime()` (347),
`TryRegisterHotSwapListener()` (348), `CacheRegistryServicesCold()` (349), `RefreshScalabilityPolicy()`
(350), `RefreshInputDeterminismService()` (351), `_activeSchemeHash = ResolveCurrentSchemeHash()` (352).
Latched?: **No.** `_resourceObjectsReady` is assigned at 974, ~80 lines below the assert, so the guard at
883 never arms and the assert re-throws on every `OnEnable` and every call from 434.
Also note 902 can never report while 895 fires first — the asserts hide each other.
**Verdict: DOWNGRADE** for 895/902 with cleanup and latch moved above; a missing glyph quad mesh degrades
tooltips, it does not justify deleting the component's registration and cache warm.

**4. `Assets/_Project/Scripts/UI/FakeRadarBlipController.cs:853`** (and `:855`, `:856`) —
`EnsureRuntimeResources()`, setup, from `OnEnable:159` and `Start:169`.
Below: 5 statements (854-858) including `_radarBlipMesh` resolve and the dirty flag.
Tail: `TryRegisterScanEvents()` (160) → `ScanEvents.Register(this)` at 936, a **signal subscription**, and
`TryRegister()` (161) → `SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI)` at 925 plus
`GlobalRegistry.Renderables.TryRegister(this)` at 928. Two of the four inert-making classes in one tail.
Latched?: **No.** Re-throws on every enable, and this is UI that gets re-enabled.
**Verdict: DOWNGRADE** — 857 already resolves `_radarBlipMesh` to `null` when authoring is invalid, i.e. the
degraded path is already designed.

**5. `Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs:866`** (and `:867`) —
`EnsureGraphicsResources()`, setup, from `OnEnable:544`, `Start:555`, and `:2416`.
Below: `_runtimeQuadMesh`/`_runtimeMaterial` assignment (868-869), two `EnsureGraphicsBuffer` calls
(872-873), active-buffer selection (874-875), `MaterialPropertyBlock` cold alloc (877-878),
`ApplyMaterialColdState()` (880).
Tail from `OnEnable`: `SeedInitialState()` (545), `PdaProjectorOnEnable()` (546), `TryRegisterTickLanes()`
(547) → two `SystemDispatcher.Register` calls at 915 and 918. From `Start`: `TryRegisterTickLanes()`.
Latched?: **No.**
**Verdict: DOWNGRADE.** 868-869 already resolve to `null` on invalid authoring, so the null-tolerant path
exists; the wrist HUD carries survival vitals and must not lose its tick lanes over an unassigned mesh.

**6. `Assets/_Project/Scripts/UI/PDADecryptionSpectrogramPanel.cs:471`** (and `:472`, `:475`, `:476`) —
`EnsureGraphicsResources()`, setup, from `OnEnable:134`.
Below: ~12 statements including the `_resolvedMaterial`/`_resolvedMesh` clear-and-return path (477-483), the
resolve (485-486), both `GraphicsBuffer` cold allocs (488-492), `UpdateDrawArgs()` (496) and the
`_graphicsReady` fold (497).
Tail: `ResetRuntimeState(_artifactHash, _blueprintHash)` (135) and `TryRegisterTickHandlers()` (136) → two
`SystemDispatcher.Register` calls at 1075 and 1078.
Latched?: **No.** The early return at 462-469 fires only on *success*; there is no permanent-failure latch,
so a missing material re-throws on every enable.
**Verdict: DOWNGRADE** — 477-483 is already the designed degraded path.

**7. `Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs:448`** (and `:449`, `:452`, `:453`) —
`EnsureResources()`, setup, from `OnEnable:71` and `Start:78`.
Below: 7 statements — asserts 449/452/453, the clear-and-return path (454-459), the material property cache
(461-467), `_resolvedVoxelMesh = voxelMesh` (469), `ApplyMaterialPropertiesIfNeeded()` (471).
Tail: `TryRegisterTickManager()` (72 / 79) → `SystemDispatcher.Register((ILateFrameTickable)this,
PriorityLayer.UI)` at 521.
Latched?: **No.**
**Verdict: DOWNGRADE** — 454-459 already nulls both resolved handles, and `RefreshMatricesForLateFrame()`
at 106-107 returns early on null. The degraded path is designed; the assert only costs the tick lane.

**8. `Assets/_Project/Scripts/HectonFabricatorUI.cs:1235`** (and `:1243`, `:1251`) —
`EnsureHologramResources()`, setup, from `Awake:257`.
Below 1235, in-block: `_resolvedHologramMesh = hologramMesh` (1238) — so the field stays null and the guard
at 1233 never arms. Then the whole material branch 1241-1267+ including `CacheHologramMaterialProperties()`
(1264).
Tail: `EnsureRecipeListPool()` (258) — a pool warm that creates the recipe-list root GameObject at 1437.
Latched?: **No** for 1235.
Per-site verdicts: **1243 DOWNGRADE (redundant)** — 1247-1248 already handles the null with an early
return, so the assert adds nothing but a throw. **1251 DOWNGRADE with cleanup moved** — its cleanup
`_resolvedHologramMaterial = null; return;` sits at 1254-1258, *below* the assert, dead. **1235 DOWNGRADE.**

**9. `Assets/_Project/Scripts/UI/ShaderCompassRibbon.cs:189`** — `EnsureAuthoredMaterial()`, setup, from
`EnsureUiBuilt():167`, itself called from `OnEnable:43` and `Start:51`.
Below, in-method: `_resolvedMaterial = compassMaterial` (190) — the latch target, dead, so the guard at
186-187 never arms and this re-throws on every call.
Below, in `EnsureUiBuilt`: `_ribbonImage.material` assignment (168-169), `CacheNavigationServiceCold()`
(171) — a **cache warm** of `GlobalRegistry.InertialNavigation` at 181 — `_uiBuilt = true` (172) and
`return true` (173). `_uiBuilt` never becomes true, so `LateFrameTick` at 95 permanently takes the
`ApplyRootAlpha(0f)` path.
Tail from `OnEnable`: `CacheNavigationServiceCold()` (44), `TryRegisterHotSwapListener()` (45),
`TryRegister()` (46) → `SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI)` at 198.
**Verdict: DOWNGRADE.** `LateFrameTick` at 101-103 already hides the ribbon when `_resolvedMaterial` is
null — a documented fallback.

**10. `Assets/_Project/Scripts/UI/PDADataLogTab.cs:2416`** — `EnsureHologramMaterial()`, setup, from
`Awake:361`.
Below, in-method: `_resolvedHologramMaterial = hologramMaterial` (2417) — the latch target at 2413-2414,
dead, so this re-throws on every call.
Tail: `RebuildLoreBindingCache()` (362) — a **cache warm** that allocates and fills `_catalogLoreHashes`
and `_catalogLoreRecordIndices` at 1467/1470. Without it the PDA data-log tab cannot map catalog entries to
lore records.
**Verdict: DOWNGRADE.** `RenderSelectedLoreHologram` at 2425-2426 already returns early when
`_resolvedHologramMaterial` is null.

**11. `Assets/_Project/Scripts/HectonScanMarkerSystem.cs:432`** (and `:433`) —
`EnsureRuntimeResources()`, setup, from `Initialize():78`, `Awake:88`, `OnEnable:96`.
This is the textbook "cleanup below the assert" instance: the asserts sit inside
`if (shouldReportInvalidResources)` at 430-434, and the cleanup `_runtimeMarkerMesh = null;`
`_runtimeMarkerMaterial = null;` `return;` is at **436-438**, below them, therefore dead. 433 can never
report because 432 fires first.
Tail: from `Awake` none (it is the last statement); from `OnEnable:96` the tail is
`TryRegisterHotSwapListener()` (97) and everything after it; from `Initialize:78` none.
Note `Initialize()` sets `_markerResourcesConfigured = true` at 77 *before* calling, which makes
`shouldReportInvalidResources` true at 427 — so an `Initialize` call with invalid overrides is guaranteed to
reach the asserts.
**Verdict: DOWNGRADE, and move the null-outs above.** `AreRuntimeResourcesReady()` at 449-451 already
gates on the resolved handles.

**12. `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs:424`** — `EnsureResources()`, setup,
from `OnEnable:75` and `Start:83`.
Below, in-method: 1 — `ApplyMaterialPropertiesIfNeeded()` (425), which itself returns early on a null
`sonarMapMaterial` at 440-441, so the in-method loss is a no-op.
Tail: `TryRegisterTick()` (76 / 84). The entire cost of this site is the tick registration.
Latched?: **No.**
**Verdict: DOWNGRADE** — the consumer's own guard at 440-441 is the fallback.

**13. `Assets/_Project/Scripts/UI/PDAMapTab.cs:815`** (and `:816`, `:819`) —
`TryResolvePointCloudAssets()`, setup **and slow-tick reachable**: called from
`EnsurePointCloudResources():768` and from `FlushSonarComputeKernelRepairSlow():798`.
Latched?: **Partially, and correctly.** `_pointCloudAssetLookupAttempted = true` is set at 813, *above* the
asserts, so this throws once rather than once per slow tick. That placement is already the right shape and
is the reason this site is not Tier 1's worst — worth preserving in the fix.
Below: assert 816, the validity fold (818), assert 819, `_resolvedPointCloudMaterial` assign (821),
`_resolvedHologramMapMaterial = hologramMapMaterial` (822), `return` (824). Note 822 resolves an
**unrelated** material that has nothing to do with the asserted fields and is silently lost forever.
Tail from 768: `QueueSonarComputeKernelRepair()` (772) and `FlushSonarComputeKernelRepairSlow()` (773).
Tail from 798: the slow-tick lane — see 1.1, a throw here unwinds every remaining tickable in the phase.
**Verdict: DOWNGRADE**, keeping the existing pre-assert latch and lifting 821-822 above the asserts.

**14. `Assets/_Project/Scripts/UI/DiegeticPDAController.cs:1252`** — `ResolveTabletScreenMaterial()`,
setup, reached as an argument expression at
`diegeticPanel.OverridePanelPresentation(ResolveTabletScreenMaterial(), tabletScreenRenderer)` inside
`ConfigureDiegeticPdaShell():439`.
Below, in-method: `return tabletScreenUnlitMaterial;` (1255) — the whole point of the method.
Tail in `ConfigureDiegeticPdaShell`: `OverridePanelPresentation` itself never runs, then
`diegeticPanel.ForceRefreshRenderTexture()` (442-443) and `_uiConfigured = true` (445).
Latched?: **No, and worse than most.** `_uiConfigured` is the idempotency flag tested at 423; it is set at
445, below the throw point. So the whole shell configuration — `EnsureTabRoutingCache` (426),
`playerPda.ConfigureUI` (427), `EnsureUiInteractionState` (428), `RebuildPointerTargetCache` (429),
`ApplyPresentationCullState` (432) and the four other `Override*` calls — re-runs on every call and the
assert re-throws forever.
**Verdict: KEEP or DOWNGRADE — needs the owner's judgement.** Unlike every other row, no fallback was found:
`ResolveTabletScreenMaterial` has a single caller which passes the result straight into
`OverridePanelPresentation`, and I did not read `DiegeticPanelController.OverridePanelPresentation` to see
whether it null-guards. Either way `_uiConfigured = true` and the `Override*` sequence must move above the
assert; that part is unconditional.

### Tier 2 — bounded loss, no registration or cache in the tail

**15. `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs:407`** (and `:408`) — **LANDED by a
sibling mid-audit; retained as the review check.** Analysis as found: —
`EnsureResources()`, setup, from `Awake:89` where it is the **last** statement, so the caller tail loss is
zero.
Below: the entire `BatchRendererGroup` construction — `new BatchRendererGroup` (411-415),
`CreateBatchHandleBuffer()` (417), the metadata allocation (418-422), `AddBatch` (425-430+), and the bounds
publish. `EnsureResources` is called from `Awake` only, so the BRG is permanently absent; `OnEnable` still
runs and still registers the tick and the floating-origin listener, producing a renderer that ticks and
draws nothing.
**Verdict: DOWNGRADE.** Its identical twin `HectonHLODRenderer` was fixed by a sibling this wave; the same
patch applies verbatim. In that twin the tick already null-guarded `_mesh`/`_material` before drawing
(`HectonHLODRenderer.cs:119-120`, with `SyncBatchRegistration` tolerating null at 324/333), which is what
made the downgrade safe — confirm the same guard in the landmark renderer's tick before applying.

**16. `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs:415`, `:416`** —
`InjectCoreHack(uint codeHash, float validity01)`, **public API, not setup**, and **zero callers anywhere
under `Assets`** — verified by searching the whole tree. Currently unreachable, hence lowest priority.
These guard *arguments*, not authored assets — a different sub-shape from every other row.
Below: the entire `SignalBus<CoreHackedSignal>.TryPushTracked(...)` push (418-425).
**Verdict: DOWNGRADE both, when it is touched at all.** `:416` is redundant — 423 already applies
`math.saturate(validity01)`, so the range it asserts is enforced anyway. `:415` has an in-file precedent for
the fallback seven lines above: `SetEditorField` at 408 writes
`field.GlitchHash != 0u ? field.GlitchHash : SeedShipAnomalyConstants.GlitchHash`. The same coalesce on
`codeHash` removes the throw without losing the signal push.

### Tier 3 — leave alone

**17. `Assets/_Project/Scripts/Atmosphere/AtmosphereMemorySovereigntyValidator1324.cs:56` — BRIEF WAS
WRONG. Do not change this site.**

- The file is wrapped in `#if UNITY_EDITOR` (line 1) and the method is
  `[InitializeOnLoadMethod] private static void Validate()` (11-12). Editor bootstrap, not runtime setup.
- The assert is `Assert.IsTrue(false, "1324 gas dynamics DTO layout violation")` at 56, guarded by
  `if (failureMask != 0u || auditFailureMask != 0u)` at 55. It is the **last statement in the method**:
  **zero** statements below it.
- There is no caller tail to lose. Unity's `InitializeOnLoad` dispatcher isolates callbacks, and nothing in
  this project's control is sequenced after it.
- What it guards is genuinely unrecoverable: `UnsafeUtility.SizeOf` and field-offset invariants on
  `GasDynamicsSolver.PendingBaseTransitionSignal`, `GasDynamicsTelemetryEntry` and
  `GasDynamicsNativeMemoryAudit` — the ARM64-safe DTO layout contract. A silent pass here corrupts native,
  Burst, GPU and persistence boundaries.

This site matches the search pattern but not the defect. It is a correctly placed hard gate: no in-method
loss, no caller-tail loss, unrecoverable subject. Counting it as remediation debt would be a false positive.

## 5. Remediation summary

Counts exclude the three section 4 entries (2, 3 and 15) that siblings landed mid-audit.

| Tier | Sites | Files |
|---|---:|---:|
| Tier 1 — registration / cache / table in the lost tail | 26 | 12 |
| Tier 2 — bounded loss | 2 | 1 |
| Tier 3 — leave alone (`BRIEF_WAS_WRONG`) | 1 | 1 |
| **Remaining unfixed total** | **29** | **14** |
| Already correct (not counted above) | 4 | 2 |
| Landed by siblings this wave (not counted above) | 10 | 6 full + 1 partial |

Of the 29 remaining: **27 are clear DOWNGRADE** — in every one the guarded thing is optional by
construction, with the fallback already present in the same file and cited above. **1**
(`DiegeticPDAController.cs:1252`) needs an owner decision on `KEEP` versus `DOWNGRADE`, but needs its
`_uiConfigured` latch and `Override*` sequence lifted above the assert either way. **1**
(`AtmosphereMemorySovereigntyValidator1324.cs:56`) should not be touched at all.

Priority order for the next wave, highest blast radius first: **`Fabricator.cs:573`** (still live, seven
statements including the recipe-table init); `FakeRadarBlipController.cs:853/855/856`; `WristHologramHudRuntime.cs:866/867`;
`PDADecryptionSpectrogramPanel.cs:471/472/475/476`; `AcousticRadarSphereRenderer.cs:448/449/452/453`;
`HectonFabricatorUI.cs:1235/1243/1251`; `ShaderCompassRibbon.cs:189`; `PDADataLogTab.cs:2416`;
`HectonScanMarkerSystem.cs:432/433`; `SubmarineSonarHoloMapRenderer.cs:424`; `PDAMapTab.cs:815/816/819`;
`DiegeticPDAController.cs:1252`; then `SeedShipAnomalyRuntime.cs:415/416`, which is currently unreachable.

Recurring sub-defects to check for in every fix, each observed above:

- **Latch below the assert** — `ShaderCompassRibbon:190`, `PDADataLogTab:2417`,
  `HectonFabricatorUI:1238`, `DiegeticTooltipSystem:974`, `DiegeticPDAController:445`. The re-entry guard
  never arms, so the assert fires forever rather than once.
- **Cleanup below the assert** — `HectonScanMarkerSystem:436-438`, `HectonFabricatorUI:1254-1258`. This is
  the exact mistake that wasted two earlier `HectonMarineSnowRenderer` attempts.
- **Asserts hiding each other** — `HectonScanMarkerSystem:432` before `:433`,
  `DiegeticTooltipSystem:895` before `:902`, `AcousticRadarSphereRenderer:448` before `:449/452/453`. The
  later asserts have never been able to report, which is why no log has ever named those fields.
- **Assert inside a branch that already proved the null** — `Fabricator:3261`, and the two sites siblings
  fixed this wave (`CarveDebrisComputeRenderer:646`, `ParasiteSwarmGpuRuntime:152`). These are
  unconditional throws wearing a null check, and the `return` below them is dead.

## 6. Method note

Static source review only. No Unity run, no `dotnet` build, no profiler or device capture was performed for
this ledger, and none is claimed. The only runtime evidence cited is the pre-existing
`Logs/omega_route22.log:7192` / `:7809` pair for the HectonVoxelEngine case. Statement counts, cadence,
caller tails and fallback claims are all `file:line` reads in this tree; the tree moved during the audit as
siblings landed fixes, and section 3.2 records that state as of this document's date.

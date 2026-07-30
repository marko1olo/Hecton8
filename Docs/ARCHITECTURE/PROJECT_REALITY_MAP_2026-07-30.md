# HECTON-8 — PROJECT REALITY MAP

Date: 2026-07-30. Author: Claude (technical lead session).
Purpose: the owner has not launched the game since **May 2026**. Last thing he saw with his own eyes was
the CPU-render height-map / macro geology fields work. This document states where the project actually is,
separating what is PROVEN from what is merely COMMITTED.

## How to read this document

Evidence classes, hardest to softest. Every claim below carries one.

| Class | Meaning |
|---|---|
| `PLAYER` | ran in a built player or Play Mode, seen by a human |
| `MEASURED` | a tool/test/probe produced a number I read myself, this session |
| `EDITOR` | Unity batchmode confirmed it (import, scene state, compile) |
| `STATIC` | I read the source/asset and reasoned. No execution. |
| `CLAIMED` | a doc/handoff/commit says so and I did NOT verify it |

**The single most important line in this document:**
there is **no `PLAYER` evidence anywhere in this project**. No built executable exists
(`fd -e exe` over `Build/`, `Builds/` returns nothing, and neither directory exists). No Play Mode
artifact. So "is the game playable" is not a question this repo can currently answer, and nobody should
claim it is.

---

## 1. Answer to the direct question: what is real?

| Claim you may have heard | Reality | Class |
|---|---|---|
| "agents ran, lots of work happened" | TRUE. 661 commits since 2026-07-27. | MEASURED |
| "3D models / forge pipeline works" | PARTLY. 18 FBX exist on disk from 7 generators. They are NOT in the world. | MEASURED |
| "voxels work" | UNPROVEN. `HectonVoxelVolume` appears **0 times** in `GameBootstrapper`. | STATIC |
| "headless simulation works" | UNPROVEN AS SHIPPED. No gate JSON, no headless log exists on disk today. | MEASURED |
| "tests pass" | WAS TRUE FOR 3 HOURS TODAY, THEN RE-DISABLED. See §4. | MEASURED |
| "the world scene is fixed" | TRUE, by me, today. See §3. | EDITOR |

---

## 2. Where the macro geology work you last saw actually landed

That work is still present and still the most mature system in the repo.

- Owner: `Assets/_Project/Scripts/Editor/HectonMacroGeologyBaseIntegrator.cs`
- It has **real determinism tests**: `Scripts/PureLogic/Tests/WorldMacroGeologyDeterminismTests.cs`,
  `WorldMacroGeologyChunkContinuityTests.cs` — these live in `PureLogic`, which is why they were never
  caught by the test-assembly gate described in §4. `STATIC`
- Since May it received: province Voronoi ranked in squared space (dropped two transcendentals),
  a determinism pin proving the field consumes its seed, and a spawn-depth fix
  (`34e4591ae geology: the spawn-depth pair - an ignored shelf parameter and a water datum of zero`).
  `CLAIMED` (commit messages; I did not re-run them)

So: the thing you personally verified in May is intact and has been extended. It is the strongest part of
the project.

---

## 3. What I fixed today, with proof

These are mine, this session, each with evidence I produced myself.

### 3.1 The whole authored world was switched OFF — FIXED
`02_HECTON_WORLD.unity` had `--- WORLD ---` reparented under `DEPRECATED_STUFF` with `activeSelf=False`,
taking **77 descendants** and 14 `WorldContentSocket`s dark with it.

Cause: `H8_SceneCleaner`, a screenshot convenience tool with no `[MenuItem]`, pointed at the production
scene. It moves every scene root whose name lacks a keep-token into `DEPRECATED_STUFF`, disables it, and
saves. `--- WORLD ---` matched no token. `[MANAGERS]` contains `MANAGER`, which is why every director
survived and code review read clean while the content those directors operate on was inactive one level
above them.

Fix: ran `H8_WorldRootGraveyardRepair` APPLY. Proof `Docs/AgentLogs/worldroot_apply6.log:4917`
`APPLIED AND SAVED - now a scene root with activeSelf=True, 77 descendants restored`.
Scene on disk 6270260 -> 6438976 bytes. Commit `d7e461e67`. `EDITOR`

**This does NOT mean the game boots.** See §5.

### 3.2 Two compile blockers that made every batchmode run fail
- `FabricationBootstrapAuthoring.cs:427` held a live `ThisSymbolDoesNotExist_H8GateProbe` — a synthetic
  negative-control probe left committed in `main`. Every `-executeMethod` run died on CS0103 before
  reaching its entry point. Commit `8c2b99054`. `MEASURED`
- `GlobalDataVault.cs` referenced `ResolveGuard64LockBit` from two call sites; the method was never
  written. Another session left it uncommitted in the tree. Commit `90ff72e76`. `MEASURED`

### 3.3 A real physics precision bug — FIXED AND MEASURED
`HydrodynamicKccMath.QuantizeMillimeter` multiplied by `(double)InvMillimeterScale`. That cast does not
produce 1/1000: the float nearest `0.001` widens to `0.0010000000474974513`, a **+4.75e-8 relative** bias.
Because it is relative, absolute error grows with distance from the AUP origin:

| distance | quantization error |
|---|---|
| 1.5 km | 0.07 mm |
| 21 km | 1.00 mm (crosses the gate) |
| 99 km | 4.70 mm |

Effect: the KCC smoke test seeds its sector at `(99000, -1500, 99000)`, so it measured ~4.7 mm of drift
at **frame 1**, against a 1.0 mm threshold, before any physics ran.
Fix: `InvMillimeterScaleExact = 1.0d / 1000.0d`. Commits `c6064dd8f` + proof `a8e3766a6`.
MEASURED: `ErrorFlags 74 -> 66`, bit 3 cleared, failures `838500 -> 743920` (−94580), Bee rebuilt
`Hecton8.Core.dll` in-run with zero `error CS`. `MEASURED`

### 3.4 Leviathan tentacles ignored the quality system
The Verlet job pinned `qualityNoiseScale`/`qualityPulseScale` to `1f`, while the owner class already sent
`SmoothQuality01(_globalQualityWeight)` to the shader as `RadiusFxFlow.z`. CPU and GPU disagreed about the
same creature. Threaded the real value in with a 0.35 floor. Commit `468b779ac`. `STATIC` + compile gate
`Ошибок: 0`. Player-visible result **not** yet captured.

### 3.5 Stale test assertions that crashed instead of failing
`SaveManagerLoadPriorityConflictEditTests` reads `SaveManager.cs` as **text** and asserts literal call-site
strings, chaining `IndexOf(literal, startIndex)` to prove statement order. Two refactors moved past it in
opposite directions (`PublishSaveStatus*` LOST a params wrapper, `PublishSaveCompleted*` GAINED one), so 22
literals no longer existed. `IndexOf` returned −1, −1 became the next `startIndex`, and the tests threw
`ArgumentOutOfRangeException` — the only real exceptions in a 2256-test run.
Repaired all 22. MEASURED: `63 -> 60` failures, `2193 -> 2196` passed, zero regressions.
Rationale commit `effa674c0` (the code itself was swept into cement commit `568a19cca`). `MEASURED`

---

## 4. The test story, and why it matters most

**For the entire history of this project, 434 test files had never once compiled.** They were gated behind
a `NEVER_COMPILE_TESTS` constraint defined for no platform.

Today that gate was opened by another session (`d5689745e`), and I got the first full run in project
history:

```
TOTAL 2256   PASSED 2193   FAILED 63   SKIPPED 0   compile errors: ZERO
```

That is a genuinely good result — 97.2% pass on code that had never been tested. I then fixed 3 and got
to **2196 / 60**.

**Then it was re-closed.** Commit `e29ab1438 fix(tests): restore NEVER_COMPILE_TESTS on EditModeTests
asmdef` put the constraint back. As of right now `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef:47`
reads `"NEVER_COMPILE_TESTS"` again, so **the EditMode suite is dark again**. `MEASURED`

That is the single highest-value decision on the table: 2256 tests exist, they mostly pass, and they are
currently switched off. Whoever re-closed it may have had a reason (import time, or the 60 failures being
noisy) — but it needs to be a deliberate, stated decision, not a silent revert.

### The 63 failures, classified by me
- **11 are source-text assertions** — tests that grep a `.cs` file for a literal. These pass while the
  feature is broken and fail on harmless refactors. They are worse than no test.
- **52 are behavioural.** Largest clusters: `HazardZoneRuntimeSave` 22, `WorldPickupStateCodec` 10,
  `SaveManagerLoadPriorityConflict` 8, `WalIntegrityChecker` 5.
- 6 of the pickup failures are one shape: the product correctly logs an error and the test never declared
  it (`Use LogAssert.Expect`). Test-side, cheap.
- 4 look like real `WorldStateManager` defects (suppressed-pickup sweep, legacy identity promotion).
`MEASURED` from `Logs/alltests_probe.xml`.

---

## 5. What is NOT wired — the "raw files, not a system" problem

This is the honest core of your question. A lot exists as *files* that the running game never touches.

`GameBootstrapper.cs` is the wiring boundary. It has 43 `AddComponent` calls. Everything it constructs:

```
AccessibilitySettings, AudioListener, BeaconNetworkSystem,
BootstrapPresentationFallbackRuntime, Canvas, CanvasScaler,
ConnectionSplineBatchRenderer, ConstructionManager, CrashTelemetryBuffer,
EcosystemDirector, EquipmentInteractionHandler, GameBootstrapper,
GameTickManager, GlobalPhysicsStateManager, HUDNotification,
HardwareErrorCanvas, Image, InputDispatcher, InputManager,
ModWorldPersistenceManager, ObjectPoolManager, PersistentWorldRegistry,
PhysicsApplySystem, PowerGridManager, PrefabRegistry, RebindingManager,
RectTransform, RenderDispatcher, RuntimePerformanceProfiler, SaveManager,
SettingsManager, SystemDispatcher, TextMeshProUGUI, UserOptionsPersistence,
WorldStateManager
```

Note what is **absent**: no `HectonVoxelVolume`, no terrain streaming owner, no flora runtime. `STATIC`

### Confirmed-dead pipelines

**Flora L-system genomics.** `FloraGenomeVaultRuntime` has **zero** callers outside its own file. Its GUID
`eff6285ef651fa248b0cc51c81e343b6` appears only in its own `.meta` and `_guidmap.json`. It is a
`public sealed class`, not a MonoBehaviour, so no scene can bind it — the binary-scene blind spot does not
apply and the negative is solid. Its only scheduler is `Editor/LSystemGenomeLabWindow.cs:236`, a lab
preview window: a button a human presses. `MEASURED` (three independent reachability routes)

Inside it, `MockTerrainHeight.SampleHeight()` returns `0f` — a flat seabed at Y=0. Honest placeholder
labelling, not a silent constant. **Real constraint for whoever wires this:** the consumers are inside
`[BurstCompile] IJob` structs, so they *cannot* call the managed `TryGetTerrainHeightSamplePayload`
interface. It needs a blittable height array sampled outside the job — not a call swap. `STATIC`

**Fauna VAT swarm.** `Ocean_Crest.prefab` carries `SargassumMicroFaunaBoids` with
`boidMesh` = Unity's built-in **Plane**, both VAT textures `fileID: 0`, and `boidVatFrameCount: 1`.
`frameCount:1` is textbook silent degeneracy — the shader samples one frame forever and never errors, so
micro-fauna render as flat undeformed quads. Confirmed at scene level, not just prefab:
`Logs/boidassets_probe3.log:787 boidMesh = Plane`. `EDITOR`

Root cause is **content, not code**, and the chain is 4 links deep:
`Art/Fauna/Raw` -> `FaunaOfflineRigger1610` -> `Generated/Fauna/VAT1610` -> `Rigged1610` ->
`FaunaSwarmVatPrefabBinder`. `MESH_Fauna_Fish_2207_00.fbx` exists but is referenced by nothing except its
own `.meta`. The binder **correctly refuses** today. Do not hand-edit the prefab.

---

## 6. The forge / 3D model pipeline — what is real

7 generators exist under `Tools/Blender/generators/`: `coral_branching`, `fauna_fish`, `flora_capstem`,
`kelp`, `prop_handtool`, `rock`, plus probes. They produce **18 FBX** under
`Assets/_Project/Art/Generated/Forge/`, covering 9 distinct assets:

```
MESH_Fauna_Fish_2207_00          MESH_Flora_CapStem_1811_00
MESH_Flora_Coral_Branching_1712  MESH_Flora_Kelp
MESH_Geology_boulder_sedimentary MESH_Geology_cliffchunk_sedimentary
MESH_Geology_outcrop_sedimentary MESH_SmallProp_Tool_SeafloorDrill_1712/_2611
```
`MEASURED`

So the forge genuinely works as an **asset factory**. What is unproven is whether any of it is *in the
world* and whether it *looks* good — that requires the Visual Reference Parity Gate, which needs a human
or my own image reading, and has not been run on these. `9 assets` is also a small library for a survival
game.

### Geology texture budget — contested, do not trust either side yet
The bedding-run metric was failing (`p95 0.8036` vs budget `0.55`). I found the measuring probe itself was
broken: `build()` never accepted the `width_scale` kwarg that all three search modes pass, so every search
mode died on `TypeError` before measuring anything (fix `135566f61`).

I then over-claimed a pass from one seed and **retracted it myself** (`eb3d66c51`): across 5 seeds
`runP95` spans `0.4833..0.6829` — green on seed 1713, red elsewhere. Notably `runP95` does **not**
separate the accepted and rejected configurations at all, while coverage and cell-count metrics separate
cleanly, which suggests the metric being gated on is the least discriminating one available. `MEASURED`

Another session's root `BACKLOG.md` now claims a PASS at 2048 across seeds 0,1,2,7,13
(`p95_max=0.4590 eros_min=0.3417`). I have **not** verified that. `CLAIMED`

---

## 7. Silent degeneracy sweep — 4 findings REJECTED as correct

Agents reported "constant-returning method discards authored data" in several places. I checked each
myself and **rejected 4** — they were right about the shape and wrong about the consequence. Recording
this so nobody "fixes" them:

| Site | Why it is CORRECT |
|---|---|
| `LaserCutterDodJobs.cs:411` | `carve01` feeds `Progress01` = cut completion = **gameplay truth**. `AGENTS.md:235` forbids `GlobalQualityWeight` from touching it. `AuthoritativeQualityWeight = 1f` is established convention. |
| `DeployableSdfDrillRuntime.cs:1384` | `MaxCycles` clamps ore cycles (`DeployableSdfDrillContracts.cs:363`). Scaling it by tier would give low-end players **less ore**. Same file at `:1394` correctly *does* scale a visual carve weight — it draws the line right in both directions. |
| `HectonPlayerMovement.cs:12459` | `ApplyModePhysics` has no else branch, but the stale value never escapes: `SmoothDampingTransition` (`:10039`) recomputes and lerps every tick, and the only consumer runs at `:10191`, 152 lines later. |
| `WorldProceduralScatterDirector.cs:7685` | `UsesPatternAccentQuotas` is a subsystem **master switch**. Per-pattern behaviour is fully wired one level down via `ResolvePatternClusterAccentRoleMaxRatio(pattern, …)` → authored profile ratios; a pattern wanting no accents already refuses via ratio 0. |

**Lesson worth keeping:** a constant-returning method is a *hypothesis*, not a defect. Trace the consumer
before believing a "dead parameter" report.

Genuinely open leads I did *not* clear: `LeviathanTentacleVerletSolver` (fixed, §3.4),
`HectonAtmosphereManager.cs:2216` `ResolveAegirRingShadowMultiplier` returns `1f` while
`HectonCelestialEngine.cs:921-937` authors full ring geometry (`ringShadowStrength = 0.26`,
`ringPlaneNormal`, inner/outer radius). That one **is** visual-only so law 235 does not protect it — but
the ring data is private in another class and published only to shaders, so it needs a new cross-class
accessor plus a visual gate I cannot pass headlessly. Deferred, not dismissed.

---

## 8. Scenes

| Scene | Bytes | In build settings | Note |
|---|---|---|---|
| `00_BOOTSTRAP` | 163 729 | yes | entry point |
| `01_MAIN_MENU` | 966 238 | yes | |
| `01_ORBIT` | 24 194 | yes | nearly empty |
| `02_HECTON_WORLD` | 6 438 976 | yes | the world. repaired today (§3.1) |
| `010_TEST` | 5 810 980 | no | |
| `020_RENDER_SANDBOX` | **60 755 399** | no | 60 MB sandbox |
| `020_RENDER_SANDBOX_V2` | 5 023 428 | no | |

4 scenes are in build settings, so a player build has a route: bootstrap -> menu -> world. `MEASURED`
Note 4 of these scenes are **binary**, which defeats text-based GUID search — any "not referenced
anywhere" claim about a MonoBehaviour must be made by GUID, not class name.

---

## 9. Honest overall assessment

**Strengths.** The architecture is real and unusually disciplined: typed `SignalBus` lanes, a dispatcher
with named phases, zero-GC hot-path law, AUP double-precision world coordinates, ARM64-safe DTO rules,
binary checksummed saves. 2193 tests pass. The macro geology field you validated in May is intact and
improved. The forge produces real geometry.

**The actual problem.** It is not code quality — it is **integration**. Systems are written to a high
standard and then not connected. `GameBootstrapper`'s 43 components are the real game; everything outside
that list is a file. Flora genomics, the VAT swarm, and voxel volume are all in that outside category.
Three separate content pipelines terminate one link short of the world.

**The second problem.** Proof discipline has been inverted in places. `BUILD_PLAYTEST_ISSUES.md` itself
documents this beautifully: a recorded `dotnet build` PASS whose log file **does not exist anywhere in the
repository**, correctly demoted to `PENDING VERIFICATION`. Source-text assertions are the same disease in
test form. The repo has learned to record evidence *shapes* without the evidence.

**What I would do next, in order.**
1. **Decide the test gate deliberately** (§4). 2256 tests that mostly pass are the cheapest safety net this
   project will ever get, and they are currently off.
2. **Produce one player build.** There is no `PLAYER` evidence at all. Until an executable exists and boots,
   every readiness statement about this project is unfounded — mine included.
3. **Wire one dead pipeline end to end** rather than starting a fourth. The VAT swarm is closest: the tools
   exist and correctly refuse, so it needs content (one rigged fish), not code.
4. **Convert the 11 source-text assertions to behavioural ones.** They currently protect nothing and break
   on refactors.
5. Then, and only then, judge visual quality against the reference folder.

**What I will not claim.** Not release-ready, not optimized, not playable, not visually verified. No
profiler run, no GC measurement, no device test, no frame time, no player boot. Everything in §3 is
compile-gate, batchmode, or measured-test evidence, and I have labelled which is which.

# WorldContentSocket.ContentKind — Coverage, Consumers, And The Real Content Gap

Date: 2026-07-29

Status: PENDING VERIFICATION (static source + byte-level asset decode only; no Unity run, no Play Mode,
no profiler, no screenshot)

Owner domain: world content / socket authoring

Evidence class: STATIC_SOURCE + BINARY_ASSET_DECODE. Not runtime proof.

FIRST_20 moment: read-only architecture audit of the world-content authoring route. It removes a route
blocker by correcting the record on what content exists before anyone authors against a false premise.
No source, scene, prefab, or asset was modified.

Note on provenance: an earlier draft of this file existed on disk (untracked, 12:06 today). Every
load-bearing claim below was re-derived from live source and live scene bytes with independent tooling,
not inherited. That pass overturned two of the earlier draft's findings; both are marked
**CORRECTION TO THE EARLIER DRAFT** in place.

---

## 1. Headline — the investigated premise is FALSE

The premise:

> Seven `ContentKind` values have ZERO live instances — `ConstructionPoint`, `PowerPoint`,
> `ServiceTarget`, `NavigationMarker`, `HazardPoint`, `CombatPoint`, `Landmark` — all authored only
> inside a dev proving ground that ships suppressed, and THIS is the real content gap rather than the
> often-quoted "4 of 14 sockets".

Verdict, clause by clause:

| Clause | Verdict |
|---|---|
| "seven kinds have ZERO live instances" | **FALSE.** All seven have serialized instances in `02_HECTON_WORLD.unity`. Ten instances, decoded field-by-field out of the binary scene (section 2). |
| "authored only inside a dev proving ground that ships suppressed" | **TRUE**, and proven twice over (section 4). |
| "THIS is the real content gap rather than 4 of 14" | **Half true, and it undersells the defect.** "Seven kinds" and "4 of 14" are the same fact counted from opposite ends, not two competing claims. Neither is the real gap. |

The real gap is two levels deeper and is not a shortage of authored content:

1. **`--- WORLD ---` is switched off in the shipping scene** (`m_IsActive = 0`, byte-decoded in section
   4.2). Every socket is a descendant of it, so `OnEnable` never runs, `_ActiveSockets` stays empty, and
   **zero of the 14 sockets reach any consumer — not ten, and not four.**
2. **No `ContentKind` value reaches anything a player can see.** Every runtime consumer of `socket.Kind`
   terminates in `[SerializeField] private` Inspector diagnostics (section 5). The one system that
   actually instantiates never consults a socket.

`outcome = PREMISE_WRONG`.

---

## 2. Method — where this claim lived or died

### 2.1 The reachability tool, and the question it cannot answer

`python -B Tools/SceneGuidReachability.py --self-test`, run this session:

```
999 scene/prefab files: 995 text, 4 BINARY
self-test
  ok   the four known binary scenes are detected as binary (4 binary total)
  ok   text search misses the world scene, byte-aware search finds it
  ok   FaunaBrain absent from every scene and prefab, both encodings
SELF_TEST=PASS
```

That tool answers *"is this TYPE present"*. `ContentKind` is a serialized **enum field value**, not a
script GUID, so the tool cannot answer *"which value do the present instances carry"*. A separate decode
was required and was written for this audit.

### 2.2 Positive control on every binary file, before believing any zero

| File | Bytes | `%YAML` | `m_Script` as text | **`Untagged` control** | `WorldContentSocket` string | script GUID (nibble-swapped) |
|---|---|---|---|---|---|---|
| `02_HECTON_WORLD.unity` | 6,270,260 | no | 0 | **505** | 14 | 1 |
| `010_TEST.unity` | 5,810,980 | no | 0 | **421** | 14 | 1 |
| `020_RENDER_SANDBOX.unity` | 60,755,399 | no | 91 | **102** | 0 | 0 |
| `020_RENDER_SANDBOX_V2.unity` | 5,023,428 | no | 0 | **28** | 0 | 0 |

Every binary file returns a positive control, and a negative control string
(`socket.definitely.not.authored`) returns 0 in all four. So the zeros from the two sandbox scenes are
real zeros and not a broken search. The raw (non-swapped) GUID returns 0 hits in all four files, which is
the trap restated: a text `rg` for the GUID would have reported the world scene as socket-free.

### 2.3 How the enum value was actually read — the decode is bracketed, not fitted

`WorldContentSocket` declares its serialized fields in this order (`WorldContentSocket.cs:27-39`):

```
socketId | socketLabel | contentKind | contentProfile (PPtr) | preferredFidelity |
interactionRadius | weight | futurePrefabKey | contentIntent | ...
```

Unity serializes a `string` as `int32 length + bytes + align(4)` and a `PPtr` as
`int32 m_FileID + int64 m_PathID`. The decoder anchors on each `socketId` string, walks that layout
forward, and **accepts the walk only if `futurePrefabKey` and `contentIntent` land exactly on their
authored literals.**

That is the point of the design: those two strings sit *after* `contentKind`, so a correct landing
brackets `contentKind` between two high-entropy string anchors without ever consulting `contentKind`
itself. The answer is proven by the walk, not fitted to the expected value. Landing accidentally on
`"resource.scrap.titanium"` and `"Starter loose scrap pickup."` is not a coincidence that happens.

Result: **14/14 sockets in `02_HECTON_WORLD.unity` decoded with all six of
(`contentKind`, `interactionRadius`, `weight`, `preferredFidelity`, `futurePrefabKey`, `contentIntent`)
agreeing with the authoring source.** Exactly one anchor per `socketId` — no duplicates. The same decoder
reproduced 14/14 independently in `010_TEST.unity`, where the `contentProfile` PPtr `m_FileID` numbering
differs per scene, so the grouping is not a copied artifact.

### 2.4 What is UNKNOWN and was not guessed

- **Parent/child edges were not read from the binary bytes.** No `m_Father` PPtr was resolved.
  Parentage rests on the authoring-path contract (section 4.1), which is strong but is a different class
  of evidence, and it is labelled as such wherever it is used.
- **No Unity run.** No Play Mode, no profiler, no capture. Every runtime statement below is source
  reading plus on-disk decode.

---

## 3. Deliverable 1 — the full enum, with proven instance counts

The enum has **11 values**, not 14 (`WorldContentSocket.cs:11-24`). The "14" in "4 of 14 sockets" is the
number of authored socket *instances* — 14 `ConfigureContentSocket(...)` calls at
`WorldRuntimeBootstrapAuthoring.cs:1008-1021`, inside `ConfigureSceneContentSockets()` which spans
`:1006-1022`. The cited line range is exact.

`ProjectSettings/EditorBuildSettings.asset` ships exactly four scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`,
`01_ORBIT`, `02_HECTON_WORLD`. **`010_TEST.unity` is not in the build**, so its identical 14 sockets are
not shipped content and are excluded from the counts below.

| # | ContentKind | Instances PROVEN in `02_HECTON_WORLD.unity` | Socket ids | Hierarchy | Ships? |
|---|---|---|---|---|---|
| 0 | `Generic` | **0** | — | — | n/a |
| 1 | `ResourcePickup` | 1 | `socket.resources.scrap_a` | `Resource_FieldSources/Scrap_Field/Scrap_A` | yes |
| 2 | `ResourceNode` | 2 | `socket.resources.copper_a`, `socket.resources.silver_a` | `Resource_FieldSources/Mineral_Pocket/*` | yes |
| 3 | `FabricationStation` | 1 | `socket.fabrication.forward` | `Fabrication_Outpost/Forward_Fabricator` | yes |
| 4 | `ConstructionPoint` | **1** | `socket.construction.socket_base` | `Tool_Staging/Tool_TrialRange/Lane_ConstructionOps/` | **suppressed** |
| 5 | `PowerPoint` | **2** | `socket.power.generator`, `socket.power.load` | `Tool_Staging/Tool_TrialRange/Lane_PowerOps/` | **suppressed** |
| 6 | `ServiceTarget` | **1** | `socket.service.flooded_corridor` | `Tool_Staging/Tool_TrialRange/Lane_ServiceModules/` | **suppressed** |
| 7 | `NavigationMarker` | **2** | `socket.nav.anchor`, `socket.nav.frontier` | `Tool_Staging/Tool_TrialRange/Lane_BeaconRoute/` | **suppressed** |
| 8 | `HazardPoint` | **2** | `socket.hazard.dark_probe`, `socket.progression.ops_hazard` | `Tool_TrialRange/Lane_DarkRoute/`, `Lane_EndgameOps/` | **suppressed** |
| 9 | `CombatPoint` | **1** | `socket.combat.aggressive` | `Tool_Staging/Tool_TrialRange/Lane_CombatContacts/` | **suppressed** |
| 10 | `Landmark` | **1** | `socket.progression.frontier` | `Tool_Staging/Tool_TrialRange/Lane_EndgameOps/` | **suppressed** |

Total 14. **10 of 11 enum values carry at least one authored instance.** The only value with zero
instances is `Generic`, the field default, authored by no `ConfigureContentSocket` call.

Method for every row: byte decode per section 2.3. **No row is UNKNOWN — the encoding was solved, not
worked around.** No `WorldContentSocket` exists in any text scene or any prefab (GUID search over all 995
text files returns zero), so these 14 are the complete population.

### 3.1 Supporting data assets (text files, read directly)

- **10 `WorldContentProfile` assets** under `Assets/_Project/Data/World/ContentProfiles/` — exactly one
  per non-`Generic` kind, `contentKind` 1-10 all present.
- **10 `WorldPopulationRule` assets** under `Assets/_Project/Data/World/PopulationRules/` — exactly one
  per non-`Generic` kind, `contentKind` 1-10 all present.

The *data* layer has complete coverage for all ten kinds. The gap is placement and consumption, never
authoring volume.

### 3.2 CORRECTION TO THE EARLIER DRAFT — the 37 procedural rules are NOT empty

The earlier draft stated: *"All 37 `ProceduralRule_*.asset` files have an empty `preferredSocketKinds`"*,
and built three conclusions on it. **That is false, and it is false through exactly the failure class this
audit exists to prevent.**

Unity serialized these enum arrays in its **compact hex form**, not as a YAML list:

```
Assets/_Project/Data/World/ProceduralPlacementRules/ProceduralRule_rule_service_scar.asset:32
  preferredSocketKinds: 06000000
```

A search for `preferredSocketKinds:` followed by a `- ` list item returns zero hits and looks like proof
of emptiness. `06000000` is one little-endian `int32` = `6` = `ServiceTarget`.

Measured across all 37 assets: **17 empty, 20 populated.** Every populated array has exactly one element
(8 hex chars = one `int32`). Histogram:

| Value | Kind | Rules | Rule names |
|---|---|---|---|
| `0a000000` | `Landmark` (10) | **11** | `coral_plate`, `fauna_ruin_apex_zone`, `kelp_canopy`, `landmark_spire`, `plant_giant`, `rocks_arch`, `rocks_shelf`, `ruin_cluster_medium`, `ruin_medium`, `ruin_megastructure`, `ruin_module_single` |
| `08000000` | `HazardPoint` (8) | **4** | `fauna_abyss_apex_zone`, `fauna_large_threat_zone`, `fauna_predator`, `pocket_hazard` |
| `07000000` | `NavigationMarker` (7) | 1 | `cave_entries` |
| `06000000` | `ServiceTarget` (6) | 1 | `service_scar` |
| `05000000` | `PowerPoint` (5) | 1 | `route_power` |
| `03000000` | `FabricationStation` (3) | 1 | `pocket_safe` |
| `01000000` | `ResourcePickup` (1) | 1 | `pocket_resource` |

No rule gates on `ResourceNode` (2), `ConstructionPoint` (4), or `CombatPoint` (9).

Three consequences, all of which the earlier draft got backwards:

1. **`WorldProceduralPlacementRule.Matches` `:105-122` is a LIVE gate, not dead code.** 20 of 37 rules
   require the socket's `Kind` to equal their one preferred kind.
2. **`GetScatterContentKind()` `:196-214` does NOT always fall through to the domain mapping.** For those
   20 rules it returns `preferredSocketKinds[0]` from the asset and never reaches the `switch`.
3. **The scatter self-consistency filter `WorldProceduralScatterDirector.cs:2652-2663` IS still
   tautological — but for a different reason.** It compares `PreferredSocketKinds[i]` against
   `runtimeRule.ScatterKind`, and `ScatterKind` is set from `rule.GetScatterContentKind()` at `:2072`,
   which returns `preferredSocketKinds[0]`. With every populated array length 1, the check compares an
   element to itself and always passes. It is inert because the arrays are singletons, not because they
   are empty. (It is additionally skipped whenever `StrictEnvelopeMapping` is set.)

The sharpest number in this document falls out of that table: **18 of the 20 socket-gated procedural
placement rules require a `ContentKind` whose only instances are inside the suppressed proving ground**
(Landmark 11 + HazardPoint 4 + NavigationMarker 1 + ServiceTarget 1 + PowerPoint 1). Only `pocket_safe`
and `pocket_resource` can ever match a shipping socket.

---

## 4. Two suppression layers, and the one that kills everything

### 4.1 Layer 1 — `WorldShippingContentFilter` (kills 10 of 14)

`Assets/_Project/Scripts/World/WorldShippingContentFilter.cs`. A socket is suppressed if:

- its `GetZoneAnchor()` resolves to a zone with `ZoneKind.Trial` **or** a `zoneId` starting
  `zone.trial.` (`:51-59`, `:73-77`, `:175-180`); or
- an ancestor transform is named `Tool_Staging`, `Fabrication_Trial`, `Tool_TrialRange`, or starts with
  `__TEMP_` (`:15-20`, `:296-316`).

`WorldContentSocket.CopyActiveSocketsTo` (`:111-132`) applies `IsSuppressedSocket` on every read at
`:127`, and that method is the only way any runtime system obtains sockets.

Precision the earlier draft missed: the hierarchy-name half of the filter only fires once the per-scene
cache is primed, and `PrimeSuppressionCacheForScene` (`:201-254`) is reached only through
`DeactivateSuppressedSceneObjects` (`:107-173`), called from `GameBootstrapper.cs:8150` and
`WorldShippingSceneRuntimeGuard.cs:54`. Before that call, `IsSuppressedByHierarchy` falls back to the
ancestor zone-contract walk alone (`:88-102`). **All ten suppressed sockets fail the zone route
regardless**, so the outcome is the same either way:

- the eight under `Lane_ConstructionOps` / `Lane_PowerOps` / `Lane_ServiceModules` /
  `Lane_CombatContacts` / `Lane_EndgameOps` sit under zones with `zone.trial.*` ids
  (`WorldRuntimeBootstrapAuthoring.cs:998-1002`);
- the two under `Lane_BeaconRoute` and the one under `Lane_DarkRoute` have **no zone anchor of their
  own** — `ConfigureSceneZones()` authors none for those lanes — so `GetComponentInParent` resolves up to
  `Tool_TrialRange`, which is `zone.trial.range` with `ZoneKind.Trial` (`:997`). Suppressed.

The seven kinds in the premise are precisely the seven authored **exclusively** inside that subtree. That
is the accurate statement of the defect: not "zero instances", but **"authored only where shipping content
is filtered out"**. The premise's second clause is right even though its first is wrong.

### 4.2 Layer 2 — the world root is switched off (kills all 14)

Byte-decoded `GameObject.m_IsActive` from `02_HECTON_WORLD.unity`, walking
`m_Name → m_TagString → m_Icon(PPtr) → m_NavMeshLayer → m_StaticEditorFlags → m_IsActive`:

| GameObject | `m_TagString` (inline control) | `m_IsActive` |
|---|---|---|
| `--- WORLD ---` | `Untagged` | **0** |
| `DEPRECATED_STUFF` | `Untagged` | **0** |
| `Tool_Staging`, `Tool_TrialRange`, `Fabrication_Trial` | `Untagged` | 1 |
| `Resource_FieldSources`, `Fabrication_Outpost`, `Starter_ReefField` | `Untagged` | 1 |
| every `Lane_*` present (8 of 9) | `Untagged` | 1 |
| all 14 socket GameObjects | `Untagged` | 1 |
| `[MANAGERS]` | `Untagged` | 1 |
| `Lane_ChoiceHub` | — | **absent from the scene** (0 anchors) |

**The gate was verified in both directions, not just the passing one.** The decoder emits both `0` and
`1` across the scene, so a `0` is a real read and not a stuck field; it rejects any walk whose
`m_IsActive` byte is not exactly 0 or 1, whose `m_NavMeshLayer` is out of range, or whose `m_TagString`
is not printable. Two independent controls back it: the `Untagged` string count (505), and — stronger —
the `Player` object decoding `m_TagString = "Player"` rather than the constant `Untagged`, which proves
the walk lands on the real tag field instead of reproducing one expected value.

This corroborates existing in-repo documentation rather than discovering it.
`Assets/_Project/Editor/H8_SceneCleaner.cs` reparents non-whitelisted scene roots
(`:41 SetParent`, `:42 SetActive(false)`) and then `:47 EditorSceneManager.SaveScene(scene)`;
`H8_WorldRootGraveyardRepair.cs:13-23` records that `--- WORLD ---` matched none of the cleaner's keep
tokens while `[MANAGERS]` contains `MANAGER` and survived. **The value of the decode is that it proves the
repair has not been applied to the file on disk: `--- WORLD ---` still reads `m_IsActive = 0` today.**

**Parentage (inference from a contract, not a byte read — stated as such).** All 14 sockets are created by
one call site, `GetOrAddComponent<WorldContentSocket>(target)` at
`WorldRuntimeBootstrapAuthoring.cs:1684`, where `target = FindByPathIncludingInactive(objectPath)` and
`ConfigureContentSocket` returns early if the path does not resolve (`:1680-1682`). Every one of the 14
`objectPath` literals begins `--- WORLD ---/`, and `EnsureRoutePath` (`:1242-1256`) creates exactly those
15 Transform chains under `worldRoot`. The decoded sockets carry precisely the values those calls write.
Residual uncertainty: an object could in principle have been reparented afterwards; the only tool
documented to reparent in this scene is `H8_SceneCleaner`, which touched roots only.

**Consequence.** An object under an inactive ancestor has `activeInHierarchy == false`, so Unity runs no
`Awake`/`OnEnable` in the subtree. `WorldContentSocket` registers itself *only* in `OnEnable` (`:95-99`).
`_ActiveSockets` therefore stays empty, `CopyActiveSocketsTo` copies nothing, and
`WorldContentDirector.Sockets` is empty for **all 14 sockets including the four shipping ones**. Layer 1
never gets a chance to run. No runtime code re-activates the root: a scoped search for `WorldRootName`,
`"--- WORLD ---"` and `DEPRECATED_STUFF` across all non-Editor, non-Test `.cs` under `Assets` returns
**zero hits**.

**Order of operations for anyone fixing this: the graveyard repair is a prerequisite, not an
alternative. Repair it and the reachable count goes 0 → 4, not 0 → 14.**

And the graveyard is actively hostile to new authoring. `H8_WorldRootGraveyardRepair.cs:45-50` records
that `WorldRuntimeBootstrapAuthoring`'s root-reuse check is now inactive-inclusive, so it **finds the
buried root and adopts it** — every route path it writes, plus the biolum zones and `Starter_ReefField`,
is parented under the disabled root and inherits `activeInHierarchy = false`. New sockets authored today
would be born dead.

### 4.3 A third, smaller defect

`ConfigureZone(".../Lane_ChoiceHub", "zone.trial.choice", ...)` at `:1003` silently no-ops:
`EnsureWorldRouteSkeleton` (`:1242-1256`) never creates a `Lane_ChoiceHub` path, so the path lookup fails
and the call returns without authoring anything. Confirmed twice independently — the path is absent from
the `EnsureRoutePath` list, and the `m_IsActive` decode finds zero `Lane_ChoiceHub` anchors in the scene.
`ConfigureSceneZones()` authors ten zones; nine exist. A silently skipped authoring line is a trap.

---

## 5. Deliverable 2 — consumers: which kinds are read, and what happens

### 5.1 Every runtime read of `socket.Kind` comes through one filtered accessor

| Reader | File / line | Source of sockets |
|---|---|---|
| `WorldProceduralFillDirector.EvaluateFill` | `:179`, `:225` | `worldContentDirector.Sockets` (filtered) |
| `WorldPopulationDirector` | `:145`, `:173`, `:250` | `worldContentDirector.Sockets` (filtered) |
| `WorldContentDirector.UpdateDiagnostics` | `:228` | its own `_sockets`, filled by `CopyActiveSocketsTo` (`:156-159`) |
| `WorldProceduralProxyInstance` | `:241` | socket passed in by caller |
| `MapMagicWorldValidator` (**editor**) | `:1053`, `:1574`, `:1599` | `Resources.FindObjectsOfTypeAll` — *does* see the buried sockets |
| `WorldPopulationValidator` (**editor**) | `:218` | inactive-inclusive scene search — *does* see them |
| `WorldProceduralProxySceneBuilder` (**editor**) | `:104` | `FindObjectsByType(...Exclude)` — cannot see them |

`WorldContentDirector.Sockets` is read by exactly **two** runtime systems. (Other `.Sockets` hits in the
project are `views.Sockets` in the construction/VR native lanes, `HectonLayerMasks.Sockets`, and
`System.Net.Sockets` — unrelated.)

### 5.2 What a match actually does — the finding that outranks the premise

`WorldPopulationRule.Matches` (`:31-67`) requires `zone != null`, then `zoneKind` match unless the rule's
`zoneKind` is `Generic`, then tier range, then `socket.Kind == contentKind` unless the rule's
`contentKind` is `Generic`. `WorldProceduralPlacementRule.Matches` has the equivalent gate at `:105-122`.
`WorldProceduralFillDirector` switches on `socket.Kind` in three complete 10-arm tables (`:393-406`,
`:417-430`, `:435-448`) mapping every kind to a zone-plan family, a zone visual profile, and a family id.

So the wiring looks complete. Follow what a successful match writes:

- `WorldProceduralFillDirector.cs:192` → `candidateSocket.ApplyProceduralRecommendation(...)`; the `else`
  branch at `:209` calls `ClearProceduralRecommendation()`. Nothing else happens per socket.
- `WorldPopulationDirector.cs:188` → `candidateSocket.ApplyPopulationRecommendation(...)`; `else` at
  `:212` clears. Nothing else happens per socket.
- `ApplyPopulationRecommendation` (`WorldContentSocket.cs:207-258`) writes 19 `_debug*` strings and 4
  `_debug*` numbers. `ApplyProceduralRecommendation` (`:288-316`) writes 9 strings and 5 numbers.
- Those fields are exposed only through the `Resolved*` properties (`:175-205`).
- **The only file in the project that reads any `Resolved*` property is `WorldContentDirector.cs`**,
  which copies them into its own `_debugNearest*` fields (`:227-259`) — 33 `[SerializeField] private`
  fields (`:27-59`) that are written there and **read by nothing**.

The chain terminates in Inspector diagnostics. **No `ContentKind` value causes anything to spawn, unlock,
damage, light, sound, or persist.**

Three dead ends confirmed by direct search:

- `FuturePrefabKey` and `ContentIntent` appear only as their own property declarations
  (`WorldContentSocket.cs:92-93`). Zero readers.
- `WorldProceduralProxyInstance.socketKind` has exactly three occurrences: the declaration (`:39`) and
  two writes (`:241`, `:334`). **Write-only field, zero reads, no accessor.**
- `WorldContentProfile.contentKind` (`WorldContentProfile.cs:13`) is read by one place in the entire
  project — the editor validator `MapMagicWorldValidator.cs:1053`. No runtime reader.

### 5.3 The one path that does spawn does not read sockets

`WorldProceduralScatterDirector` genuinely instantiates (`:8437 Instantiate(prefab, ...)`). Its selection
gate is the static `MatchesScatter` (`:2588`), called from
`WorldProceduralScatterDirectorSamplingPipeline.cs:669`, which tests `runtimeRule.ScatterKind` — derived
from the rule asset at `:2072` — and **never consults a `WorldContentSocket`**. The kind is then stamped
onto the write-only `WorldProceduralProxyInstance.socketKind`.

Two mapping gaps worth naming, from `GetScatterContentKind()`'s domain `switch` (`:200-213`):
`ResourceNode` and `ConstructionPoint` are produced by **no** `ProceduralDomain`, so the scatter pipeline
can never emit them; `Landmark` is produced by two (`Landmark`, `RuinModule`).

### 5.4 Defect classification

| Class | Kinds | Detail |
|---|---|---|
| Instances exist, consumer exists, consumer only writes diagnostics | **all 10 non-`Generic`** | The dominant defect. Section 5.2. |
| Consumer exists, zero *reachable* instances | `PowerPoint`, `ServiceTarget`, `NavigationMarker`, `HazardPoint`, `Landmark` | 18 of 20 socket-gated `ProceduralRule_*` assets (section 3.2) plus their 5 `PopulationRule` assets require a kind that never survives the filter. |
| Consumer exists, instances suppressed, and no *procedural* consumer at all | `ConstructionPoint`, `CombatPoint` | Their `PopulationRule` exists; no `ProceduralRule_*` gates on either. |
| Instances exist but unreachable for a second, independent reason | **all 14 sockets** | `--- WORLD ---` is `m_IsActive = 0`. Section 4.2. |
| Enum value with no instance and no authoring call | `Generic` | Field default only. Harmless — and it is the wildcard in both `Matches` implementations, so it is load-bearing as a *rule* value. |

`RuntimePerformanceProfiler` (`:2311-2314`) is a real runtime read of `socket.Kind`, but it attributes
**renderers** to a socket by walking up parents (`:2290-2316`). Socket GameObjects are bare Transforms
created by `EnsureRoutePath` and nothing is ever spawned under them, so that histogram is structurally
always empty regardless of activation.

---

## 6. Deliverable 3 — ranked by first-twenty-minutes impact

### 6.1 Correction to the second-hand report about the route contract

An earlier agent reported `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` as "unnumbered
with two disagreeing lists". Read in full myself (167 lines): **"unnumbered" is true; "two disagreeing
lists" is false.**

The pipeline code block at `:31-37` and the `Required Route` table at `:66-90` state the same moments in
the same order. The table folds the pipeline's `save -> load -> return to same state` into one
`Save/load` row, and adds a `Proof` row that is an acceptance gate rather than a route moment — 10 table
rows, 9 route moments. That is a granularity difference, not a contradiction. Neither list is numbered,
so citing a moment by index is unsafe; cite it by name.

### 6.2 The honest precondition, stated before the ranking

**No socket authoring changes anything a player can see until (a) `--- WORLD ---` is active and (b) a
consumer exists that spawns or activates at a socket.** The ranking below is which authoring gap to close
*once those two hold*. Ranking socket placement above them would be authoring into a dead channel — and
worse, per section 4.2, into a channel that silently adopts new content into the graveyard.

1. **`NavigationMarker` — highest.** The `Swim` row requires the player can "return to a known point";
   `First exit` requires a readable semi-open shallow. It is the only kind mapped to
   `family.cave.entrance` (`WorldProceduralFillDirector.cs:443`). Its rule
   `PopulationRule_Navigation_Mid` is `zoneKind: 0` (= any, since `Matches` skips the check for
   `Generic`), so it is also among the cheapest to place correctly. One procedural rule
   (`cave_entries`) already gates on it. Zero shipping instances today.
2. **`HazardPoint`.** The `Hazard` row is mandatory: "One fair hazard creates a decision".
   `PopulationRule_Hazard_Generic` is the most permissive rule in the set — `zoneKind: 0, minTier: 0,
   maxTier: 4` — so it matches every existing shipping zone with no rule edit. Four procedural rules gate
   on it, the second-largest block. Best ratio of route value to authoring cost. Zero shipping instances.
3. **`Landmark`.** Serves `First exit` and `Swim` readability; the only kind routed to
   `zone.Profile.farSilhouetteProfile` (`:428`) and `family.landmark.spire` (`:446`) — the only
   distance-legibility channel the socket system has. **Eleven procedural rules gate on it, more than
   every other kind combined**, so it unblocks the most rule surface per socket placed. It ranks third
   only because its rule `PopulationRule_Progression_Endgame` needs `ZoneKind.Progression` and tier
   Late-Endgame, which no shipping zone provides. Zero shipping instances.
4. **`ServiceTarget`.** The `Tool` row accepts "scan, cut, repair, drill, or harvest"; this is the repair
   option and the only kind mapped to `family.service.scar` (`:442`). Not the only way to satisfy that
   row, so below the three above.
5. **`ConstructionPoint`.** Serves `Craft/repair/build`'s "base-support action". Ranks here because
   `FabricationStation` already has a live shipping socket satisfying that row's "one recipe" option, so
   this is a second path rather than the only path. Note it is reachable from **no** procedural rule and
   **no** `ProceduralDomain`.
6. **`PowerPoint`.** No `Required Route` row needs power in the first twenty minutes. Post-route.
7. **`CombatPoint` — lowest, and it would be authoring into a vacuum.** The `Hazard` row lists fauna as
   one of seven options, and a `CombatPoint` socket has nothing to spawn: `family.creature.spawn.predator`
   (`:445`) needs a creature that acts, and `Tools/SceneGuidReachability.py`'s self-test — reproduced in
   section 2.1 — records `FaunaBrain` as absent from every scene and prefab in both encodings. No
   procedural rule gates on `CombatPoint` either. Placing combat sockets before there is a brain produces
   a marker with no behaviour behind it.

`ResourcePickup`, `ResourceNode` and `FabricationStation` are excluded: they already have shipping-zone
instances, and `Resource` / `Craft` are the two route moments socket authoring already serves. Their
problem is section 4.2, not placement.

---

## 7. Deliverable 4 — the authoring that would close it (DO NOT EXECUTE FROM THIS DOCUMENT)

### 7.1 Prerequisite A — activate the world root, first

Run `Hecton8/Authoring/World Root Graveyard Repair - REPORT ONLY`
(`H8_WorldRootGraveyardRepair.cs:85-86`) before anything else; it changes nothing, which matters because
the scene is binary and there is no diff to inspect afterwards. Only then
`Hecton8/Authoring/World Root Graveyard Repair - APPLY AND SAVE`.

Read `H8_WorldRootGraveyardRepair.cs:63-72` first: the tool moves **one** object and refuses ambiguous
cases on purpose, because a blanket re-enable of `DEPRECATED_STUFF` would resurrect anything an author
deliberately switched off before the cleaner ever ran. Also read its `:29-43` retraction — the graveyard
and the boot timeout are two independent faults, and this repair will **not** fix the boot. Judge it on
world content becoming live, not on `gameReady` turning true.

Without this, nothing below has any runtime effect, and per section 4.2 anything authored is adopted into
the graveyard.

### 7.2 Prerequisite B — a consumer that does something

Until some system reads `socket.Kind` and spawns, unlocks, or activates, additional sockets only add
Inspector strings (section 5.2). This is the larger piece of work and it is not an authoring task. It is
also the only prerequisite that no existing button can satisfy.

### 7.3 The three zones any new shipping socket must land in

The only non-suppressed zones in `02_HECTON_WORLD.unity`:

| Zone object path | zoneId | ZoneKind | ZoneTier |
|---|---|---|---|
| `--- WORLD ---/Resource_FieldSources` | `zone.resources.field` | Resources (1) | Starter (0) |
| `--- WORLD ---/Starter_ReefField` | `zone.resources.starter_reef` | Resources (1) | Starter (0) |
| `--- WORLD ---/Fabrication_Outpost` | `zone.fabrication.outpost` | Fabrication (2) | Early (1) |

`ZoneKind { Generic=0, Resources=1, Fabrication=2, Trial=3, Construction=4, Power=5, Service=6,
Progression=7, Combat=8, Navigation=9 }`; `ZoneTier { Starter=0, Early=1, Mid=2, Late=3, Endgame=4 }`
(`WorldZoneAnchor.cs:24-45`).

### 7.4 Placement that works against the existing rule assets, with no rule edits

Because `Matches` gates on `zone.Kind` **and** tier range **and** `socket.Kind`, only the two `zoneKind:
0` rules can fire inside a Resources or Fabrication zone:

- **`HazardPoint`** — `PopulationRule_Hazard_Generic` is `zoneKind: 0, minTier: 0, maxTier: 4`. Placeable
  in **all three** shipping zones with no rule change. Add sockets under
  `--- WORLD ---/Resource_FieldSources/...` and `--- WORLD ---/Starter_ReefField/...`, mirroring the
  argument shape of `WorldRuntimeBootstrapAuthoring.cs:1018` and reusing
  `ContentProfile_HazardPoint.asset`.
- **`NavigationMarker`** — `PopulationRule_Navigation_Mid` is `zoneKind: 0` but `minTier: 1` (Early), so
  it matches `zone.fabrication.outpost` (Early) and **not** the two Resources zones (Starter). Either
  place route markers under `--- WORLD ---/Fabrication_Outpost/...`, or raise a Resources zone's tier to
  Early, or lower `minTier` on that one rule. Reuse `ContentProfile_NavigationMarker.asset`.

### 7.5 Placement that requires a new shipping zone

`Landmark`, `ServiceTarget`, `ConstructionPoint`, `PowerPoint`, `CombatPoint` need a zone whose `ZoneKind`
matches their rule (`Progression 7 / Service 6 / Construction 4 / Power 5 / Combat 8`) and whose tier is
inside the rule's range (`Landmark` needs Late-Endgame; the other four need Mid-Endgame). None of the
three shipping zones qualifies.

A new zone must clear **every** suppression test in `WorldShippingContentFilter`. The existing authoring
failed all three at once — this is the exact mistake not to repeat:

1. `zoneId` must **not** start with `zone.trial.` — use e.g. `zone.route.service`.
2. `ZoneKind` must **not** be `Trial`.
3. No ancestor may be named `Tool_Staging`, `Tool_TrialRange`, or `Fabrication_Trial`, and none may start
   with `__TEMP_`. Parent the new zone **directly under `--- WORLD ---`**, not under `Tool_Staging`.

Concretely, for the top-ranked kinds: a `zone.route.beacon` (`ZoneKind.Navigation`, tier Early) and a
`zone.route.service` (`ZoneKind.Service`, tier Mid), authored as direct children of `--- WORLD ---`, each
with a `WorldZoneProfile` following the `EnsureZoneProfile` pattern at
`WorldRuntimeBootstrapAuthoring.cs:1699-1728`, would let `NavigationMarker` and `ServiceTarget` sockets
survive the filter and match their existing rules with no rule-asset edits. A `zone.route.frontier`
(`ZoneKind.Progression`, tier Late or Endgame) is the highest-leverage single addition, because it
unblocks the 11 `Landmark`-gated procedural rules.

### 7.6 Also worth fixing while in there

- `zone.trial.choice` / `Lane_ChoiceHub` (section 4.3): either add the path at `:1242-1256` or delete the
  `ConfigureZone` call at `:1003`. A silently skipped authoring line is a trap.
- The 17 `ProceduralRule_*` assets with a genuinely empty `preferredSocketKinds` (section 3.2) skip the
  socket gate entirely. Decide per rule whether that is intended breadth or an unfinished field, and
  record the decision — do not assume emptiness means "unused", because 20 of the 37 are populated.
- Any future audit of a Unity enum-array field must read it as **compact hex**, not as a YAML list. That
  false negative is what put the wrong claim in section 3.2's predecessor.

### 7.7 The button that looks like the fix and is not

`[MenuItem("Hecton8/Authoring/Rebuild World Runtime Stack", priority = 177)]`
(`WorldRuntimeBootstrapAuthoring.cs:55`) is **not** a safe way to apply socket authoring. Verified by
reading `:56-255`:

- One press runs **twelve** other authoring subsystems: `ConstructionBootstrapAuthoring` (`:224`),
  `WorldProceduralSupportFinalAuthoring` (`:225`), `WorldProceduralOrganicMiscFinalAuthoring` (`:226`),
  `WorldProceduralGeologyProfileAuthoring` (`:227`), `WorldProceduralGeologyFinalAuthoring` (`:228`),
  `WorldProceduralFinalVariantAuthoring` (`:229`), `WorldProceduralFloraTextureAuthoring` (`:230`),
  `WorldProceduralFloraMaterialAuthoring` (`:231`), `WorldProceduralFloraBakedStarterGenerator` (`:232`),
  `WorldProceduralFloraFinalVariantAuthoring` (`:233`), `HectonRockRuntimeBootstrapAuthoring` (`:234`),
  `WorldProceduralPlaceholderAuthoring` (`:243`).
- It writes prefabs and **commits every asset edit**: `AssetDatabase.SaveAssets()` at `:250` then
  `AssetDatabase.Refresh()` at `:251`.
- It **never saves the scene** — only `EditorSceneManager.MarkSceneDirty(activeScene)` at `:252`. So the
  asset half is permanent and the scene half is not. An editor crash or a `Don't Save` leaves the two
  halves inconsistent in a binary scene with no diff to recover from.
- It also `GetOrAddComponent`s 30+ directors onto `[MANAGERS]` and runs
  `ConfigureWorldProceduralScatterDirector` **twice** (`:172`, `:235`).

Any recommendation to press it must carry all four of those facts.

---

## 8. What remains unverified

- **No Unity, no Play Mode, no profiler, no screenshot.** Every runtime claim is static source reading
  plus on-disk asset decode.
- **`activeInHierarchy` was not observed at runtime.** The `m_IsActive = 0` on `--- WORLD ---` is a byte
  read of the saved scene; the consequence for `OnEnable` is derived from documented Unity semantics, not
  measured.
- **Parent/child edges were not read from the binary scene.** Parentage rests on the authoring path
  contract (section 4.2). Closing that gap belongs in `Tools/SceneGuidReachability.py`.
- **The `preferredSocketKinds` census covers the 37 `ProceduralRule_*.asset` files found by name.** A
  rule asset under a different naming convention was not counted. The same compact-hex trap applies to
  any other enum-array field in this project that has been audited by text search.
- The authoritative way to confirm sections 4 and 5 in one pass is
  `Hecton8/Diagnostics/Scene Root Activation Audit` (`H8_SceneRootActivationAudit.cs`), which already
  watches `WorldContentSocket` by name and reports `authoredActiveButSuppressed` per deactivated subtree.
  It is read-only and never saves. It requires Unity, so it was not run.

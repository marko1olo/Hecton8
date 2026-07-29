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

---

## 9. Second pass, 2026-07-29 — the authoring plan redone for a world where the root repair happens first

Added 2026-07-29, after sections 1-8. Sections 1-8 are unchanged; this section supersedes **section 7 only**
as the executable plan, and corrects four specific claims in sections 2.2, 2.4, 5.3 and 6.2 in place below.

Why it was redone: sections 1-8 were written before this session ran the repair's REPORT ONLY pass. That run
turned the graveyard from a byte-decode inference into a Unity-observed fact, and it changes the shape of the
plan — "before the repair" and "after the repair" are two different worlds, and section 7 partly conflates
them. The plan below states which world each step belongs to.

Evidence class: STATIC_SOURCE + BINARY/TEXT_ASSET_DECODE + one Unity editor REPORT-ONLY log line from this
session. No Play Mode, no profiler, no capture, no screenshot. Unity was held by another session; this pass
did not launch it.

FIRST_20 moment: `Hazard` and `Swim` are the two `Required Route` rows this plan aims at, via the
socket-driven placement consumer in 9.4. Route blocker removed: the plan stops the next author from placing
content into a disabled subtree.

### 9.1 Proof preamble — the reachability tool's self-test is RED right now

`python -B Tools/SceneGuidReachability.py --self-test`, run this session, verbatim:

```
1000 scene/prefab files: 996 text, 4 BINARY
self-test
  ok   the four known binary scenes are detected as binary (4 binary total)
  ok   text search misses the world scene, byte-aware search finds it
  FAIL FaunaBrain was expected absent everywhere, found in 5
SELF_TEST=FAIL
```

`SELF_TEST=FAIL`. Do not quote section 2.1's `SELF_TEST=PASS` as current — it is not.

The failure is real and is not a socket problem. `FaunaBrain` was hard-coded into the self-test as a
known-absent negative control, and commit `0a3a73109` ("authoring: creatures in HECTON-8 now have brains")
made it present:

```
FaunaBrain  guid f97102d76d9d9d04f95ccebcd55b7079
  PRESENT in 5 live scene/prefab file(s):
    text   Assets/_Project/Data/AI/GeneratedProxies/Prefabs/DroneProxy.prefab
    text   Assets/_Project/Data/AI/GeneratedProxies/Prefabs/HeavyHunterProxy.prefab
    text   Assets/_Project/Data/AI/GeneratedProxies/Prefabs/HunterProxy.prefab
    text   Assets/_Project/Data/AI/GeneratedProxies/Prefabs/LeviathanProxy.prefab
    text   Assets/_Project/Data/AI/GeneratedProxies/Prefabs/TerritorialProxy.prefab
```

The two assertions that carry socket reachability — binary detection, and byte-aware search finding the world
scene where text search misses it — both still pass. So the numbers in sections 2-4 survive.

**Standing debt, not fixed here (out of this pass's file scope):** a self-test that is permanently red trains
the next reader to ignore it, which is exactly the habit the tool was written to break.
`Tools/SceneGuidReachability.py` needs its third assertion re-pointed at a type that is genuinely absent, or
inverted to assert `FaunaBrain` is present on those five prefabs and absent from all 31 scenes. The tool file
was not edited by this pass.

#### 9.1.1 Per-binary-scene positive control — section 2.2's control was for the wrong search mode

Section 2.2 offered `Untagged` string counts as the per-file positive control. That validates a **literal
string** search. It does not validate a **nibble-swapped GUID byte** search, which is the search every socket
count depends on. Re-controlled per file, both ways:

| Binary file | `Untagged` (string control) | first-party `.cs` GUIDs found, swapped | ALL asset GUIDs found, swapped (16 133 indexed) | `WorldContentSocket` swapped GUID | literal `WorldContentSocket` | negative control |
|---|---|---|---|---|---|---|
| `02_HECTON_WORLD.unity` | 505 | **78** | **571** | 1 | 14 | 0 |
| `010_TEST.unity` | 421 | **70** | **514** | 1 | 14 | 0 |
| `020_RENDER_SANDBOX.unity` | 102 | **0** | **0** | 0 | 0 | 0 |
| `020_RENDER_SANDBOX_V2.unity` | 28 | **3** | **4** | 0 | 0 | 0 |

Negative control string: `socket.definitely.not.authored`, 0 in all four.

**CORRECTION TO SECTION 2.2.** `020_RENDER_SANDBOX.unity` returns a GUID-search positive control of **zero
out of 16 133 GUIDs**, so its zero for `WorldContentSocket` was never controlled in the search mode that
mattered. The reason is that the file is not Unity-binary at all — it is a `binary2text`-style dump. Its
first 48 bytes:

```
External References
path(1): "" GUID: 09ae21fcfc5845d48b7ff185c43c4b68 Type: 3
```

100% printable in the first 4 KB, 91 literal `m_Script`, zero `%YAML`, zero `guid: ` (lowercase key), and 23
unique external GUIDs written as ASCII behind the **uppercase** key `GUID: `. Object references in the body
point at indices into that table, not at GUID bytes — so a swapped-GUID search structurally cannot hit it,
and `SceneGuidReachability.py` classifying it as `BINARY` alongside the three real ones is wrong in kind, not
just in degree. **"Four binary scenes" is really three Unity-binary scenes plus one text dump.**

The correct control for that file is its own External References table, and with it the zero holds: the table
is the complete set of external references for the file, `943c010c0447dc44a8a0c3f750f346ff`
(`WorldContentSocket.cs`) is not in it, in either case. **So the answer does not change — no socket in the
render sandbox — but section 2.2's justification for it did not hold, and a future audit that trusts
`Untagged` as a GUID-search control will get a silent false zero on that file.**

#### 9.1.2 CORRECTION TO SECTIONS 2.4 AND 8 — parentage is no longer UNKNOWN

Sections 2.4 and 8 record parent/child edges as unread, with parentage resting on the authoring-path
contract. There is a **text** copy of this scene on disk from before the cleaner ran:

`Docs/DEPRECATED/RejectedVisualPasses/20260608_scene_cleanup/02_HECTON_WORLD_before_rejected_visual_cleanup.unity`
— 4 053 465 B, starts `%YAML 1.1`, 987 GameObjects, 1 224 Transforms, **14 `WorldContentSocket`
MonoBehaviours**.

Resolving `m_GameObject → Transform → m_Father` chains in that file gives all 14 parent chains, read rather
than inferred:

```
kind= 9 socket.combat.aggressive        Combat_Aggressive < Lane_CombatContacts < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind= 4 socket.construction.socket_base Construct_SocketBase < Lane_ConstructionOps < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind= 3 socket.fabrication.forward      Forward_Fabricator < Fabrication_Outpost < --- WORLD ---
kind= 8 socket.hazard.dark_probe        DarkRoute_HazardProbe < Lane_DarkRoute < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind= 7 socket.nav.anchor               Route_Anchor < Lane_BeaconRoute < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind= 7 socket.nav.frontier             Route_Frontier < Lane_BeaconRoute < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind= 5 socket.power.generator          Power_CurrentTurbine < Lane_PowerOps < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind= 5 socket.power.load               Power_ServicePump < Lane_PowerOps < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind=10 socket.progression.frontier     Ops_Frontier < Lane_EndgameOps < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind= 8 socket.progression.ops_hazard   Ops_Hazard < Lane_EndgameOps < Tool_TrialRange < Tool_Staging < --- WORLD ---
kind= 2 socket.resources.copper_a       Node_Copper_A < Mineral_Pocket < Resource_FieldSources < --- WORLD ---
kind= 1 socket.resources.scrap_a        Scrap_A < Scrap_Field < Resource_FieldSources < --- WORLD ---
kind= 2 socket.resources.silver_a       Node_Silver_A < Mineral_Pocket < Resource_FieldSources < --- WORLD ---
kind= 6 socket.service.flooded_corridor Trial_Module_Corridor_Flooded < Lane_ServiceModules < Tool_TrialRange < Tool_Staging < --- WORLD ---
```

Every chain terminates at `--- WORLD ---`. The `contentKind` integers are 1×1, 2×2, 1×3, 1×4, 2×5, 1×6, 2×7,
2×8, 1×9, 1×10 — **identical to section 3's table**, derived from a different file in a different encoding by
a different decoder. That is independent corroboration of the binary decode, not a restatement of it.

In the same snapshot, `--- WORLD ---` is `m_Father: 0` (a scene root) with `m_IsActive: 1`, **7 direct
children, 79 descendants**, and there is **no `DEPRECATED_STUFF` root at all**. This session's Unity
REPORT-ONLY run on the live scene reports the same object with `directChildren=7 descendants=77`,
`activeSelf=False`, buried one level under `DEPRECATED_STUFF`. Seven direct children match exactly; the
descendant count differs by 2 over seven weeks of other work.

**Honest limits of this evidence.** The snapshot is 2026-06-08 and lives under `Docs/DEPRECATED/`. It proves
what the parentage was then, not what it is now. What makes the chain hold today is the third leg:
`Assets/_Project/Scenes/02_HECTON_WORLD.unity` has mtime **2026-07-27 16:25:31**, is clean in `git status`,
and its last commit is `32c3c8a1a` (2026-07-27 16:29). The scene bytes that section 4.2 decoded are the
scene bytes on disk right now — nothing has written it since, including this pass.

### 9.2 Deliverable 1 — sequencing, and the deadlock nobody has hit yet

Ordered. Steps 1-3 are prerequisites in the strict sense: skipping them makes every later step produce
nothing.

**Step 1. Run `Hecton8/Authoring/World Root Graveyard Repair - REPORT ONLY`.** Already done this session;
output is the two `[H8_WORLDROOTREPAIR]` lines quoted at the top of this section. Its value is that it proves
the repair will not refuse: `worldRootsAtSceneRoot=active:0/inactive:0` means no duplicate world root exists,
and `H8_WorldRootGraveyardRepair.cs:171-179` refuses outright if an active one does. Re-run it if any Rebuild
menu item has been pressed since.

**Step 2. Run `Hecton8/Authoring/World Root Graveyard Repair - APPLY AND SAVE`.** Read
`H8_WorldRootGraveyardRepair.cs:63-72` first: it moves exactly one object and refuses every ambiguous case,
because a blanket re-enable of `DEPRECATED_STUFF` would resurrect objects an author disabled deliberately
before the cleaner ever ran. Its `:29-43` retraction also applies — the graveyard and the boot timeout are
independent faults and this does not fix the boot. Judge it on world content becoming live, not on
`gameReady`.

Do not read "root repair applied" as "scene restored". The June snapshot had **79** scene roots and no
graveyard; the live scene reports **29** roots with a graveyard present. The repair lifts **one** object out.
Whatever else the cleaner buried stays buried, on purpose.

**Step 3. Understand that authoring before step 2 lands inside the graveyard.** Verified in source rather
than taken from the tool's comment: `EnsureWorldRouteSkeleton` resolves the root with
`FindInLoadedScenesIncludingInactive(WorldRootName)` at `WorldRuntimeBootstrapAuthoring.cs:1230`, and that
helper (`:1139-1199`) is a depth-first, inactive-inclusive walk. It therefore **finds the buried root and
adopts it**. All 15 `EnsureRoutePath` calls (`:1242-1256`), the four biolum zones (`:1029-1103`) and
`Starter_ReefField` (`:1105-1121`) are parented under it and inherit `activeInHierarchy == false`. New
sockets authored today are born dead, silently, in a binary scene with no diff.

**Step 4 — the deadlock, and the reason the order above is mechanical rather than merely tidy.** Not
previously recorded anywhere in this document:

- `Rebuild World Runtime Stack` ends at `AssetDatabase.SaveAssets()` (`:250`), `AssetDatabase.Refresh()`
  (`:251`), `EditorSceneManager.MarkSceneDirty(activeScene)` (`:252`). There is **no `SaveScene` call
  anywhere in `WorldRuntimeBootstrapAuthoring.cs`** — the only two hits for the token in that file are inside
  doc comments describing the cleaner. So it leaves the scene dirty and unsaved.
- `H8_WorldRootGraveyardRepair.Execute` opens with a loop over every loaded scene and **refuses if any is
  dirty** (`:119-129`), because `OpenScene(Single)` would silently discard the unsaved work.

Press Rebuild first and the repair will not run. Breaking that requires a manual save of the binary
production scene, which commits the scene-side output of twelve other authoring subsystems in one
unreviewable write. **Repair, verify, then author. Not the other way round.**

**Step 5. Only now is socket authoring worth doing.** After step 2, `CopyActiveSocketsTo`
(`WorldContentSocket.cs:111-132`) starts returning sockets — **4 of 14**, because
`WorldShippingContentFilter.IsSuppressedSocket` at `:127` still drops the 10 under
`Tool_Staging`/`Tool_TrialRange` and the `zone.trial.*` ids. 0 → 4, per section 4.2. Anyone expecting 14 has
misread the filter.

**Step 6. Write the consumer (9.4) before or alongside placement.** Sockets that reach a reader today reach
only `_debug*` strings. Placement without a consumer converts an invisible zero into an invisible four.

### 9.3 Deliverable 2 — per kind: which need instances, which need code, which need neither

Sections 3 and 5 establish that all seven "dead" kinds have instances (10 of them) and that the defect is
reachability plus missing consumers. The distinction that section 7 blurs, made sharp. Verdicts assume steps
1-2 are done.

All ten `WorldPopulationRule` gate values were re-read from the assets for this table:

| Kind | Authored instances | Reachable after repair | `PopulationRule` gate (`zoneKind`/`minTier`-`maxTier`) | Placeable in a shipping zone with **no** rule or zone edit? | **Verdict** |
|---|---|---|---|---|---|
| `ResourcePickup` (1) | 1 | **1** | 1 / 0-1 | yes — both Resources zones | **NEITHER.** Has an instance and a match. Needs the consumer, nothing else. |
| `ResourceNode` (2) | 2 | **2** | 1 / 0-2 | yes — both Resources zones | **NEITHER.** Same. |
| `FabricationStation` (3) | 1 | **1** | 2 / 1-2 | yes — `zone.fabrication.outpost` | **NEITHER.** Same. |
| `HazardPoint` (8) | 2 | 0 | **0 / 0-4** | **yes — all three shipping zones** | **INSTANCES.** Only kind whose rule is a zone-kind wildcard across every tier. Cheapest real gain. |
| `NavigationMarker` (7) | 2 | 0 | **0 / 1-4** | yes — `zone.fabrication.outpost` only (Early); both Resources zones are Starter and fail `minTier: 1` | **INSTANCES.** |
| `Landmark` (10) | 1 | 0 | 7 / 3-4 | no — needs `ZoneKind.Progression` at Late/Endgame | **INSTANCES + A NEW ZONE.** |
| `ServiceTarget` (6) | 1 | 0 | 6 / 2-4 | no — needs `ZoneKind.Service` at Mid+ | **INSTANCES + A NEW ZONE.** |
| `ConstructionPoint` (4) | 1 | 0 | 4 / 2-4 | no — needs `ZoneKind.Construction` at Mid+ | **INSTANCES + A NEW ZONE.** Also reachable from no `ProceduralRule_*` and no `ProceduralDomain`. |
| `PowerPoint` (5) | 2 | 0 | 5 / 2-4 | no — needs `ZoneKind.Power` at Mid+ | **INSTANCES + A NEW ZONE**, and no first-20 row needs it. Post-route. |
| `CombatPoint` (9) | 1 | 0 | 8 / 2-4 | no — needs `ZoneKind.Combat` at Mid+ | **CODE FIRST.** See 9.5 rank 8. |
| `Generic` (0) | 0 | 0 | — | — | **NEITHER, and leave it alone.** It is the wildcard value in both `Matches` implementations; authoring an instance of it is meaningless, deleting the enum value would break the rules. |

Read the table this way: **the three kinds that already have reachable instances need zero authoring.** They
need 9.4. Sending someone to place more `ResourceNode` sockets is sending them to do work that changes
nothing, and that is the specific error this section exists to prevent.

The three shipping zones every new socket must land in, and the suppression tests a new zone must clear, are
correct as written in sections 7.3 and 7.5 and are not restated here. Re-verified from
`WorldRuntimeBootstrapAuthoring.cs:994-996`.

### 9.4 Deliverable 3 — the consumer gap, verified, and the single consumer worth writing

#### 9.4.1 Section 5's claims, independently re-run

| Claim in section 5 | Command evidence this pass | Verdict |
|---|---|---|
| `WorldContentDirector.Sockets` has exactly two runtime readers | `rg -n '\.Sockets\b' --glob '*.cs' Assets` → 19 matching lines across 8 files. Only **two files** read `worldContentDirector.Sockets`: `WorldProceduralFillDirector.cs:179,225` and `WorldPopulationDirector.cs:145,173,250`. The other 14 lines are `views.Sockets` (construction/VR native lanes), `HectonLayerMasks.Sockets`, `System.Net.Sockets`. | **CONFIRMED** |
| Both readers only write `_debug*` | `WorldProceduralFillDirector.cs:192-204` → `ApplyProceduralRecommendation`, `:209` → `ClearProceduralRecommendation`; `WorldPopulationDirector.cs:188/:212` the same pair. `WorldContentSocket.cs:207-258` and `:288-316` assign nothing but `_debug*`. | **CONFIRMED** |
| `Resolved*` reaches 33 `[SerializeField] private _debugNearest*` fields read by nothing | Only two files mention any `Resolved*` socket property: the declaring `WorldContentSocket.cs` and `WorldContentDirector.cs`. In `WorldContentDirector.cs`, `rg -c _debugNearest` = **66** and `[SerializeField] private ..._debugNearest` = **33** — exactly one declaration and one write each, **zero reads**. No other file names them (`WorldZoneDirector.cs`'s `_debugNearestZone` is a separate field on a different class). | **CONFIRMED** |
| `FuturePrefabKey` and `ContentIntent` have zero readers | 8 hits total: `WorldContentSocket.cs:38,39` (backing fields), `:92,93` (the properties themselves), `WorldRuntimeBootstrapAuthoring.cs:1676,1677,1692,1693` (editor writes via `SerializedObject`). No read anywhere. | **CONFIRMED** |
| `WorldProceduralProxyInstance.socketKind` written twice, read never | Exactly 3 occurrences project-wide: declaration `:39`, writes `:241` and `:334`. No accessor, no read. | **CONFIRMED** |

Two further checks that were not asked for and change the picture:

**CORRECTION TO SECTION 5.3 — the one path that spawns does not `Instantiate` at runtime.** Section 5.3 says
`WorldProceduralScatterDirector` "genuinely instantiates (`:8437`)". The call is now at **`:8441`** (the file
was edited at 12:19 today; line citations into this 12 776-line file drift — cite by symbol), and it sits
inside `CreateScatterInstance` (`:8410`) behind this guard:

```csharp
if (instance == null)
{
    if (Application.isPlaying)
        return null;

    instance = Instantiate(prefab, runtimePosition, placement.Rotation, parent);
}
```

At play time the only path to a visible object is `pool.Spawn(...)` via `TryResolveCachedObjectPool`; if the
pool cannot serve, the method returns null and nothing appears. The raw `Instantiate` is an editor/bake
fallback. That is correct under `AGENTS.md` `Runtime Hot-Path Law` ("[FORBID] Runtime `Object.Instantiate`
for frequent world items") — but it means **any socket consumer must go through `IObjectPoolService` and
needs a warmup entry, or it will silently place nothing in a player build while looking correct in the
editor.** That is the single most likely way this work gets a false pass.

**New: `PlacementMode.SocketDriven` exists in name only, in both code and data.**
`WorldPrefabFamilyProfile.PlacementMode` is `{ Scatter, Cluster, Patch, Solitary, Landmark, SpawnAnchor,
SocketDriven }` (`:33-42`). `SocketDriven` has exactly four occurrences project-wide, all of them
`case ... SocketDriven: return "SocketDriven";` label conversions (`WorldContentSocket.cs:409-410`,
`WorldProceduralFillDirector.cs:651-652`). Both placement-mode switches in the scatter director —
`baseRadius` (`:8525-8530`) and `GetPlacementModeBonus` (`:8676-8681`) — have no `SocketDriven` arm and fall
to `_ =>`. And **zero of the 111 `.asset` files carrying a `placementMode` field are set to `6`** —
`rg -c '^  placementMode: 6$' --glob '*.asset' Assets` returns no files at all; the maximum authored value
is `5` (`SpawnAnchor`).

#### 9.4.2 The single consumer with the largest reach

**Implement `PlacementMode.SocketDriven` in `WorldProceduralScatterDirector`'s placement path, fed by the
`ProceduralSelection` that `WorldProceduralFillDirector.EvaluateFill` already computes per socket and
currently throws into `_debug*` strings.**

Why this one and not another:

1. **The placement instruction is already fully computed at runtime and discarded.**
   `WorldProceduralFillDirector.cs:189` builds a `ProceduralSelection` per socket and `:192-204` passes
   `Rule, Family, VariantId, Source, Reason, Intent, HeatmapChannel, MinCount, MaxCount, MinSpacingMeters,
   ClusterRadiusMeters, Score` straight into `ApplyProceduralRecommendation`. That is a complete placement
   order — rule, family, variant, counts, spacing, cluster radius, score — recomputed every slow tick and
   spent entirely on Inspector text. **No new data authoring is required to make it act.**
2. **No kind is left without a family.** `MapSocketKindToDefaultFamily` (`:433-451`) gives all ten kinds a
   fallback `family.*` id, and all ten exist as `ProceduralFamily_*.asset` files. The zone-plan and
   zone-profile resolvers (`:387-431`) sit in front of it as richer paths.
3. **The seam is already wired, in the right direction.** `WorldProceduralScatterDirector` holds
   `[SerializeField] private WorldProceduralFillDirector proceduralFillDirector` at `:308` and already uses
   it at runtime — `proceduralFillDirector.ForceRefresh()` (`:1327`) and `proceduralFillDirector.Rules`
   (`:1339-1340`), with `WorldRuntimeReferenceUtility.TryResolveWorldProceduralFillDirector` as the
   self-heal (`:11963`). No new global surface, no route card, no `GlobalRegistry` addition. The only new
   API is a public per-socket read of the selection the fill director already has; `ProceduralSelection` is
   currently a `private readonly struct` (`:658`), so it needs promoting or projecting into a public
   unmanaged DTO.
4. **Coverage beats every alternative.** Post-repair it reaches **4 of 4** reachable sockets (100%), and
   14 of 14 if the trial filter is ever lifted, because it is keyed on `Kind` rather than on any one domain.

Alternatives, and why they reach less:

- **A `FuturePrefabKey`-driven spawner.** Attractive because `FuturePrefabKey` is a dead field with an
  obvious purpose, but no resolver exists and **none of the four shipping keys matches any asset id
  exactly**. `socket.resources.copper_a` carries `resource.node.copper`; the nearest asset is
  `ResourceNodeTemplate_CopperVein.asset` with `stableId: resource.node.copper_vein` — a prefix relation,
  not an identity. `resource.scrap.titanium` and `station.fabricator.forward` appear nowhere outside
  `WorldRuntimeBootstrapAuthoring.cs` and dated reports. This is strictly more work (build a key table
  first) for the same or less coverage.
- **A socket bridge in `ResourceDistributionDirector`/`ScavengePopulator`.** Reaches 3 of 4 reachable
  sockets and serves the mandatory `Resource` row, so it is the strongest runner-up — but it leaves
  `FabricationStation` and every future kind out, and duplicates placement authority that the scatter
  director already owns (`AGENTS.md` `Global Systems Doctrine`: one fact, one owner).

Two design constraints to state before anyone writes it, because both are silent-failure shaped:

- `WorldProceduralFillDirector` is an `ISlowTickable` (`:11`, registered on `PriorityLayer.Environment`).
  Socket-driven placement must be **idempotent and one-shot per socket**, keyed on a stable socket id hash —
  not re-placed every slow tick. A naive implementation multiplies content every tick and looks like it works
  for the first few seconds.
- Placement must route through `IObjectPoolService` with a warmup entry, per 9.4.1. The editor will look
  right either way; only a player run distinguishes them.

The 20 socket-gated `ProceduralRule_*` assets are relevant here in one specific way:
`WorldProceduralPlacementRule.Matches` returns **false when `socket == null`** and the rule has a populated
`preferredSocketKinds` (`:104-122`, re-read this pass). The scatter pipeline's own gate is the separate
static `MatchesScatter` (`:2588`, called from `WorldProceduralScatterDirectorSamplingPipeline.cs:669`) and
passes no socket. So those 20 rules are today reachable **only** through the fill director — the debug-only
path. The consumer above is what makes them mean something. The census reproduced exactly this pass: 37 rule
assets, 20 populated, 17 empty; `0a000000` `Landmark` ×11, `08000000` `HazardPoint` ×4, then one each of
`07/06/05/03/01`.

### 9.5 Deliverable 4 — first-twenty-minutes ranking

**Which list, and why.** `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` read in full, 167
lines. Section 6.1's correction of the second-hand report is **confirmed independently**: unnumbered is true,
"two disagreeing lists" is false. The pipeline block at `:31-37` reads
`boot -> world load -> semi-open beautiful shallow exit -> swim -> find resource -> tool interaction ->
craft/repair/build -> hazard response -> save -> load -> return to same state` — 11 tokens; the
`Required Route` table at `:66-90` has 10 rows. The table folds the pipeline's final three tokens into one
`Save/load` row and adds `Proof`, which is an acceptance gate rather than a route moment: 9 route moments
either way, same order. Granularity, not contradiction.

**I ranked against the `Required Route` table**, for a reason the pipeline block cannot supply: the table is
the only one of the two carrying a `Minimum acceptance` column. "One fair hazard creates a decision" and
"return to a known point" are what make a socket kind provably relevant to a moment; the pipeline block is a
one-line summary with no acceptance text, so it can order work but cannot justify it. Neither list is
numbered — cite moments by name, never by index.

Ranked by route impact. **Rank 1 is code, not authoring, and that is the point:**

1. **The socket-driven placement consumer (9.4.2) — highest, and it is not authoring.** Until it exists,
   every item below moves zero rows in the `Required Route` table. It is also the only step that converts
   the three kinds with already-reachable instances (`ResourcePickup`, `ResourceNode`,
   `FabricationStation`) from invisible to visible, which touches `Resource`, `Tool` and
   `Craft/repair/build` at once without a single new socket.
2. **`HazardPoint` instances in the three shipping zones.** `Hazard` is a mandatory row and
   `PopulationRule_Hazard_Generic` is the only wildcard rule in the set — re-read from the asset:
   `zoneKind: 0, minTier: 0, maxTier: 4, contentKind: 8`. Placeable in all three shipping zones with no rule
   and no zone edit. Second-largest block of procedural rules (4). Best route value per unit of work.
3. **`NavigationMarker` instances under `--- WORLD ---/Fabrication_Outpost`.** `Swim` requires the player can
   "return to a known point". `PopulationRule_Navigation_Mid` is `zoneKind: 0` but `minTier: 1`, so the
   Fabrication zone (Early) matches and both Resources zones (Starter) do not. Only kind mapped to
   `family.cave.entrance`. One rule (`cave_entries`) gates on it.
4. **A `zone.route.frontier` (`ZoneKind.Progression`, tier Late or Endgame) plus `Landmark` sockets.**
   Serves `First exit` and `Swim` readability, and is the only kind routed to
   `zone.Profile.farSilhouetteProfile` and `family.landmark.spire` — the socket system's only
   distance-legibility channel. **Eleven procedural rules gate on `Landmark`, more than every other kind
   combined**, so it unblocks the most rule surface per socket placed. Ranks below 2 and 3 only because
   `PopulationRule_Progression_Endgame` is `zoneKind: 7, minTier: 3, maxTier: 4` and no shipping zone
   qualifies, so it costs a new zone as well as sockets.
5. **`ServiceTarget` plus a `zone.route.service` (`ZoneKind.Service`, tier Mid+).** The `Tool` row accepts
   "scan, cut, repair, drill, or harvest"; this is the repair option and the only kind mapped to
   `family.service.scar`. Below rank 4 because the existing resource nodes already offer harvest/drill for
   that row.
6. **`ConstructionPoint`.** `Craft/repair/build` needs one action that "changes player capability or route
   safety"; `FabricationStation` already has a reachable socket for the recipe option, so this is a second
   path. Reachable from no procedural rule and no `ProceduralDomain`.
7. **`PowerPoint`.** No `Required Route` row needs power in the first twenty minutes. Post-route.
8. **`CombatPoint` — last, and section 6.2's reason for it is now stale.** Section 6.2 rests on `FaunaBrain`
   being "absent from every scene and prefab in both encodings". **That is no longer true** — see 9.1:
   `FaunaBrain` is on five generated proxy prefabs as of commit `0a3a73109`. It stays last for different and
   still-sufficient reasons: those five prefabs are referenced from **zero scenes** — all five
   `FaunaBrain` hits are `.prefab`, none of the 25 `.unity` files under `Assets` carries one — no
   `ProceduralRule_*` gates on `CombatPoint`, `family.creature.spawn.predator` reaches the player only
   through the pool path in 9.4.1, and `Hazard` lists fauna as one of seven options. Placing combat sockets
   now still yields a marker with nothing behind it — but say so because the spawn path is unwired, not
   because brains do not exist.

`FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` `Agent Contract` fields for this plan, since it asks for them
explicitly: First 20 Minutes moment — `Hazard` and `Swim`. Route impact — rank 1 makes existing `Resource`
and `Craft/repair/build` sockets visible; ranks 2-3 add the two rows with no live socket. Proof required —
Play Mode run showing pooled instances at socket transforms, plus a GC-zero slow-tick capture, neither of
which exists. Parked work rejected — ranks 6-8 until ranks 1-3 are proven.

### 9.6 Deliverable 5 — the trap, restated

`[MenuItem("Hecton8/Authoring/Rebuild World Runtime Stack", priority = 177)]`
(`WorldRuntimeBootstrapAuthoring.cs:55`) **is not the fix**, and **no step in 9.2-9.5 may be executed by
pressing it.** Re-verified this pass by reading `:218-256`:

- One press runs **twelve** other authoring subsystems in addition to the world work:
  `ConstructionBootstrapAuthoring.RebuildStarterConstructionKit`,
  `WorldProceduralSupportFinalAuthoring.RebuildWorldSupportFinals`,
  `WorldProceduralOrganicMiscFinalAuthoring.RebuildOrganicMiscFinals`,
  `WorldProceduralGeologyProfileAuthoring.EnsureProfiles`,
  `WorldProceduralGeologyFinalAuthoring.RebuildGeologyFinals`,
  `WorldProceduralFinalVariantAuthoring.ApplyFirstWave`, `WorldProceduralFloraTextureAuthoring.Apply`,
  `WorldProceduralFloraMaterialAuthoring.Apply`, `WorldProceduralFloraBakedStarterGenerator.Generate`,
  `WorldProceduralFloraFinalVariantAuthoring.ApplyBakedFloraFinals`,
  `HectonRockRuntimeBootstrapAuthoring.RebuildRockRuntimeStack`,
  `WorldProceduralPlaceholderAuthoring.RebuildPlaceholderProxyVariants`.
- It writes prefabs and **commits every asset edit**: `AssetDatabase.SaveAssets()` (`:250`) then
  `AssetDatabase.Refresh()` (`:251`).
- It **never saves the scene** — only `EditorSceneManager.MarkSceneDirty(activeScene)` (`:252`). Confirmed
  by search: the token `SaveScene` appears in this file only inside doc comments about the cleaner. So the
  asset half is permanent and the scene half is not; an editor crash or a `Don't Save` leaves the two halves
  inconsistent in a binary scene with no diff to recover from.
- **And, per 9.2 step 4, pressing it locks out the repair.** The dirty-but-unsaved scene it leaves behind
  makes `H8_WorldRootGraveyardRepair` refuse (`:119-129`). That is the trap's sharpest edge and it is new to
  this section.

Any plan step, report, or task file that mentions this menu item must carry all four facts.

### 9.7 What section 9 did not verify

- **No Unity run by this pass.** Unity was held by another session. The only Unity evidence used is the
  `[H8_WORLDROOTREPAIR]` REPORT-ONLY output produced earlier in this session; everything else is static
  source plus on-disk decode. No Play Mode, no profiler, no GC measurement, no capture.
- **Post-repair reachability is predicted, not observed.** "0 → 4" follows from
  `WorldShippingContentFilter.IsSuppressedSocket` at `WorldContentSocket.cs:127` plus the zone ids at
  `WorldRuntimeBootstrapAuthoring.cs:994-1002`. It has not been observed with a live `Sockets` count.
  `Hecton8/Diagnostics/Scene Root Activation Audit` is the read-only way to check it and still was not run.
- **Parentage of the live scene is corroborated, not read.** 9.1.2 reads it from a June text snapshot and
  ties it to today via mtime, git cleanliness and the repair tool's own descendant count. The live binary
  scene's `m_Father` PPtrs were still not decoded.
- **Section 5's remaining claims were sampled, not exhausted.** Five claims were re-run (9.4.1). The
  `RuntimePerformanceProfiler` histogram claim and the three 10-arm switch tables in
  `WorldProceduralFillDirector` were read but not independently re-derived; they are inherited from sections
  5.2 and 5.4.
- **`Tools/SceneGuidReachability.py` is red and was not fixed**, and it mis-classifies
  `020_RENDER_SANDBOX.unity` as Unity-binary when it is a text dump (9.1.1). Both are live debts against
  that tool, not against this document.
- **No line-citation sweep.** `WorldProceduralScatterDirector.cs` is 12 776 lines and was edited by another
  session at 12:19 today; two of section 5's citations into it are 4 lines stale (`:8437`→`:8441`,
  `:2072`→`:2076`). Other citations into that file in sections 1-8 may drift the same way. Cite by symbol.

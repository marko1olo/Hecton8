# World Shipping Content Filter

Date: 2026-07-29

Status: STATIC_SOURCE REVIEWED + BINARY SCENE READ / UNITY RUNTIME PENDING

Owner domain: world / shipping content suppression

FIRST_20_MINUTES relevance: the filter removes 7 of the 10 authored `ConfigureZone` zone anchors from
the live route (see [Pattern 3](#pattern-3--zonetrial-zone-id-prefix)). Any first-20-minutes
content-density question about zone guidance has to account for this gate before blaming authoring.

## Why this document exists

`WorldShippingContentFilter` is a live runtime gate that silently deactivates authored GameObjects in
shipping builds. Until now its only written rationale sat in two archived, never-verified audit
documents, and one of its four suppression patterns was not described in any live document at all.

This document is descriptive, not authorising. It records what the code does today and how well each
behaviour is evidenced. It does not upgrade any inherited `PENDING VERIFICATION` claim into fact.

## Source anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check, plus binary scene byte reads described under
[Scene presence](#scene-presence-today-measured). No Unity play-mode, profiler, or player-build proof
is claimed anywhere in this document.

- `Assets/_Project/Scripts/World/WorldShippingContentFilter.cs` — the filter itself
- `Assets/_Project/Scripts/World/WorldShippingSceneRuntimeGuard.cs` — every-scene-load enforcement
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` — bootstrap-route enforcement
- `Assets/_Project/Scripts/WorldZoneAnchor.cs` — zone anchor read filtering
- `Assets/_Project/Scripts/WorldContentSocket.cs` — content socket read filtering
- `Assets/_Project/Scripts/World/WorldLODSceneBootstrap.cs` — LOD registration filtering
- `Assets/_Project/Scripts/Editor/WorldSceneCleanupValidator.cs` — editor-side naming scanner
- `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs` — recreates `Tool_Staging`
- `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs` — authors the trial zones
- `Assets/_Project/Scripts/Editor/Diagnostics/H8_AuthoringRootReachabilityGate.cs` — expects `Tool_Staging`

## The four suppression patterns

All four live in one file. Three are name/id string rules, one is an enum rule.

| # | Pattern | Match kind | Declared | Evaluated | Documented before this file? |
|---|---|---|---|---|---|
| 1 | `__TEMP_` | GameObject name **prefix** | `:10` | `:301-302` | Only in an archived unverified plan |
| 2 | `Tool_Staging`, `Fabrication_Trial`, `Tool_TrialRange` | GameObject name **exact** | `:11-13`, array `:15-20` | `:304-313` | Archived unverified audit |
| 3 | `zone.trial.` | `WorldZoneAnchor.ZoneId` **prefix** | `:9` | `:73-77` | Archived unverified audit |
| 4 | `WorldZoneAnchor.ZoneKind.Trial` | enum value | `WorldZoneAnchor.cs:29` | `:56`, `:178` | Archived unverified audit |

### Pattern 1 — `__TEMP_` name prefix

```csharp
// WorldShippingContentFilter.cs:296-316
private static bool IsSuppressedHierarchyName(string objectName)
{
    if (string.IsNullOrWhiteSpace(objectName))   // :298-299
        return false;

    if (objectName.StartsWith(TempHierarchyPrefix, System.StringComparison.Ordinal))  // :301-302
        return true;

    for (int i = 0; i < _SuppressedHierarchyNames.Length; i++)  // :304-313
    ...
```

Confirmed properties:

- The prefix is `"__TEMP_"` exactly, `StringComparison.Ordinal`, so it is **case-sensitive**.
  `__temp_Foo` does not match; `__TEMP_Foo` does.
- It is evaluated **before** the exact-name loop. Functionally the order is irrelevant, since every
  branch returns `true`. It matters for auditing only: this is the branch with no allowlist, no
  per-name entry, and no reviewer-visible list of what it caught.
- There is **no allowlist and no opt-out**. Nothing in the class exempts a name once it matches.
- Matching is by **name only**. No tag, no layer, no component, no scene, and no build-configuration
  condition narrows it. It applies to every runtime scene the guard sees, including test and sandbox
  scenes.

### Pattern 2 — three exact hierarchy names

`_SuppressedHierarchyNames` at `:15-20` holds exactly `Tool_Staging`, `Fabrication_Trial`,
`Tool_TrialRange`, compared with `string.Equals(..., StringComparison.Ordinal)` at `:306-309`. Exact
match, case-sensitive, no prefix or substring behaviour.

### Pattern 3 — `zone.trial.` zone-id prefix

```csharp
// WorldShippingContentFilter.cs:73-77
internal static bool IsSuppressedZoneId(string zoneId)
{
    return !string.IsNullOrWhiteSpace(zoneId) &&
           zoneId.StartsWith(TrialZonePrefix, System.StringComparison.Ordinal);
}
```

Measured blast radius. `WorldRuntimeBootstrapAuthoring.cs` contains 10 `ConfigureZone` call sites
(lines 994-1003; the eleventh `ConfigureZone(` match at `:1575` is the method definition). Seven of
the ten author a `zone.trial.*` id:

| Line | Zone id | `ZoneKind` passed | Suppressed by |
|---|---|---|---|
| 994 | `zone.resources.field` | `Resources` | — survives |
| 995 | `zone.resources.starter_reef` | *(not on the matched signature)* | — survives |
| 996 | `zone.fabrication.outpost` | `Fabrication` | — survives |
| 997 | `zone.trial.range` | `Trial` | patterns 2, 3 **and** 4 |
| 998 | `zone.trial.construction` | `Construction` | patterns 2 and 3 |
| 999 | `zone.trial.service` | `Service` | patterns 2 and 3 |
| 1000 | `zone.trial.power` | `Power` | patterns 2 and 3 |
| 1001 | `zone.trial.endgame` | `Progression` | patterns 2 and 3 |
| 1002 | `zone.trial.combat` | `Combat` | patterns 2 and 3 |
| 1003 | `zone.trial.choice` | `Navigation` | patterns 2 and 3 |

Two things follow that are easy to get wrong:

- **Only one of the seven trial zones actually uses `ZoneKind.Trial`.** The other six are authored as
  ordinary gameplay kinds (`Construction`, `Service`, `Power`, `Progression`, `Combat`, `Navigation`)
  and are caught purely by the `zone.trial.` **id string**. Pattern 4 alone would miss six of seven.
- All seven are authored under `--- WORLD ---/Tool_Staging/Tool_TrialRange/...`, so pattern 2 would
  also catch them via the ancestor. The three rules are redundant here, not additive.

### Pattern 4 — `ZoneKind.Trial`

`WorldZoneAnchor.ZoneKind` (`WorldZoneAnchor.cs:24-36`) is
`Generic, Resources, Fabrication, Trial, Construction, Power, Service, Progression, Combat, Navigation`.
`Trial` is checked at `:56` in `IsSuppressedZone` and `:178` in `IsSuppressedZoneContract`.

### Fail-closed on null

`IsSuppressedZone` returns `true` for a null anchor (`:53-54`) and `IsSuppressedSocket` returns `true`
for a null socket (`:62-63`). A destroyed or unassigned reference is treated as suppressed, not as
allowed. That is the safe direction for a shipping gate, but it means a null-reference bug upstream
presents as missing world content rather than as an exception.

## Evaluation order and traversal semantics

Two separate orderings matter. Do not conflate them.

**Within `IsSuppressedHierarchyName` (`:296-316`):** null/whitespace guard → `__TEMP_` prefix →
exact-name loop. All branches return `true`, so order changes nothing observable.

**Within `PrimeSuppressionCacheForScene` (`:201-254`)** — this is where names are read at all:

1. Guard on scene validity/loaded (`:203-204`), prune caches for unloaded scenes (`:206`).
2. Skip entirely if this scene handle is already primed (`:209-210`). **Priming happens once per
   scene handle.** An object renamed after priming is not re-evaluated.
3. Depth-first walk of every root and descendant (`:217-249`).
4. For each transform, `IsSuppressedHierarchyName(current.name)` at `:237`. On a match, the
   transform's `EntityId` is recorded (`:239`) and the walk **stops descending into that subtree**
   (`continue` at `:240`).

That `continue` is the important semantic: **children of a matched object are never individually
tested and are never individually recorded.** The whole subtree is suppressed implicitly, because
`DeactivateSuppressedSceneObjects` deactivates the matched ancestor and also stops descending
(`:151-160`). So a legitimately-named child of a `__TEMP_*` parent dies with the parent and never
appears in any count.

**Deactivation** (`DeactivateSuppressedSceneObjects`, `:107-173`): primes the cache (`:115`), returns
early if nothing is suppressed (`:116-122`), then walks roots and calls `currentObject.SetActive(false)`
at `:155`, incrementing the count only for objects that were `activeSelf` (`:153`).

### Order dependency in the read path — latent hazard

`IsSuppressedByHierarchy` (`:79-105`) **does not prime the cache**. It calls
`TryGetSuppressedHierarchyIds` (`:85`) and, if no primed entry exists, sets `hasSuppressionCache =
false` and falls through to walking parents for `WorldZoneAnchor` contracts only (`:94-99`).

Consequence: for any scene where `DeactivateSuppressedSceneObjects` has not run,
**patterns 1 and 2 silently do not apply to the read path** — only patterns 3 and 4 do.
`WorldLODSceneBootstrap.cs:118` and the anchor/socket readers therefore depend on the deactivation
pass having already run for that scene.

This was a deliberate trade. Per `Docs/Archive/Batch015/Tasks/Status_UNKNOWN.md:2427`, an earlier
`EnsureSuppressionCacheForScene()` call was removed from `IsSuppressedByHierarchy` to keep scene-root
scanning and `HashSet` allocation out of the hot read accessor. The GC win is real; the ordering
coupling is the cost, and it is not asserted anywhere. UNVERIFIED whether any live scene load order
actually hits the unprimed case — that needs a play-mode probe, not a code read.

## Who invokes the filter

| Caller | Line | Effect |
|---|---|---|
| `WorldShippingSceneRuntimeGuard` | `:54` | Deactivates, for **every** scene load |
| `GameBootstrapper.ApplyShippingSceneCleanup` | `:8150` | Deactivates on the bootstrap route |
| `WorldZoneAnchor.CopyActiveAnchorsTo` | `:133` | Drops suppressed anchors from reads |
| `WorldContentSocket.CopyActiveSocketsTo` | `:127` | Drops suppressed sockets from reads |
| `WorldLODSceneBootstrap` | `:118` | Skips suppressed `LODGroup`s at registration |

`WorldShippingSceneRuntimeGuard` installs via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`
(`:28-42`) and subscribes to `SceneManager.sceneLoaded` (`:37-38`). There is no bootstrap-route
requirement and no build-configuration guard on the suppression itself.

### What gets logged — and what does not

Both deactivation sites log a **count only**:

- `WorldShippingSceneRuntimeGuard.cs:59-69` logs the count and scene name, wrapped in
  `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- `GameBootstrapper.cs:8155-8156` logs the count via `LogSceneActivation`.

So, precisely:

- In a **release build** the guard's log is compiled out. Nothing reports the suppression.
- In **every** configuration, no log line ever names a suppressed object or prints its hierarchy path.

An author whose object vanished cannot learn from any log which rule fired or which object was hit.
That is the practical problem, and it is worse for pattern 1 than for the others, because patterns 2-4
match a short closed list an author can memorise while pattern 1 matches an open-ended prefix.

## Scene presence today (measured)

`Assets/_Project/Scenes/` holds 7 scenes. Four are serialised **binary**, so ordinary text search
cannot answer questions about them:

| Scene | Format |
|---|---|
| `00_BOOTSTRAP.unity` | text YAML |
| `01_MAIN_MENU.unity` | text YAML |
| `01_ORBIT.unity` | text YAML |
| `010_TEST.unity` | **binary** |
| `020_RENDER_SANDBOX.unity` | **binary** |
| `020_RENDER_SANDBOX_V2.unity` | **binary** |
| `02_HECTON_WORLD.unity` | **binary** |

Method used, so the result can be re-derived or attacked: binary-safe fixed-string search
(`rg -a -F --count-matches`) over each scene file. This is valid for GameObject **names** because
Unity stores `m_Name` as a length-prefixed UTF-8 string in the binary format.

Each binary scene was given its **own** positive control so that no negative below rests on
"the method works on some other file". Searching the tag string `Untagged`, which accompanies every
default-tagged GameObject record, returns 505 hits in `02_HECTON_WORLD`, 421 in `010_TEST`, 102 in
`020_RENDER_SANDBOX`, and 28 in `020_RENDER_SANDBOX_V2`. All four binary files are therefore
demonstrably readable by this method, which makes the zero results below earned rather than assumed.

| String | `02_HECTON_WORLD` | `010_TEST` | Other 5 scenes |
|---|---|---|---|
| `Tool_Staging` | 1 | 1 | 0 |
| `Fabrication_Trial` | 1 | 1 | 0 |
| `Tool_TrialRange` | 1 | 1 | 0 |
| `zone.trial.` | 6 | 6 | 0 |
| `__TEMP` | **0** | **0** | **0** |
| `DENSE_KELP` / `KELP_PREVIEW` | 0 | 0 | 0 |

The six `zone.trial.*` ids present in both scenes are `range`, `construction`, `power`, `service`,
`endgame`, `combat`. **`zone.trial.choice` is in no scene** — it exists only in
`WorldRuntimeBootstrapAuthoring.cs:1003` and in archived documents, i.e. the authoring button that
would create it has not been pressed against these scenes.

`010_TEST.unity` is a distinct file from `02_HECTON_WORLD.unity` (different size and SHA-256), not a
copy, but it carries the same trial content. Cleanup that targets only the production world scene
leaves `010_TEST` dirty.

### The suppressed roots are authored ACTIVE today

The three exact-name roots are real GameObject names, not incidental strings: each is immediately
followed by `m_TagString` = `Untagged` in the byte stream, which is Unity's `m_Name` → `m_TagString`
field order.

Decoding forward from the end of `Untagged` by 20 bytes (`m_Icon` PPtr 12 + `m_NavMeshLayer` 4 +
`m_StaticEditorFlags` 4) reaches `m_IsActive`. All three roots decode to **`m_IsActive = 1`
(active)** in **both** binary scenes.

Confidence in that decode, stated honestly:

- It lands on the predicted byte for three different name lengths (12, 17, 15) with three different
  alignment paddings, which is a meaningful consistency check.
- Across all 505 `Untagged` GameObjects in `02_HECTON_WORLD` the same offset yields **only** `0` (143
  objects) or `1` (362 objects) — the value distribution of a real `bool` field, not a constant.
- It is still a hand-decoded serialisation read. It is **not** a Unity editor or play-mode readback.
  Treat it as strong evidence, not as Unity-confirmed proof.

This matters because it means **the runtime filter is currently the only thing deactivating these
three roots.** It is load-bearing today, whatever its provenance.

## Provenance: which suppressions are evidenced, and how weakly

Both justification documents are archived and both are `PENDING VERIFICATION`. They sit under
`Docs/_Archive/2026-04-29_Two_Day_Stale_Active_Docs/`, a folder whose own name records that they were
already stale when archived.

### Patterns 2, 3, 4 — from `SCENE_TRUTH_CLEANUP_AUDIT.md`

`Docs/_Archive/2026-04-29_Two_Day_Stale_Active_Docs/2026-04-15_Player_Retention_Recovery/SCENE_TRUTH_CLEANUP_AUDIT.md`,
Status `PENDING VERIFICATION` (line 3), dated 2026-04-15.

What it actually asked for, quoted in substance:

- Its "Shipping-suppress now" section (lines 41-56) names `Tool_Staging`, `Tool_TrialRange`,
  `Fabrication_Trial`, and "all `zone.trial.*` zone anchors and sockets under those hierarchies".
  Patterns 2, 3 and 4 match this request. **This part of the code does trace to a written request.**
- Its stated reason (lines 51-55) is product trust, not correctness or performance: "the names are
  explicit dev/proving-ground names", "they damage product trust if exposed to players".
- It classifies `Draft Terrain` and `CurrentVolume_PlayerSpawn_Test` as **"Audit-only for now"**
  (lines 58-67) and explicitly warns that "blind deactivation could break geometry, spawn safety, or
  atmosphere volumes". The code correctly does **not** suppress these. That restraint is faithful to
  the source document.
- It calls the runtime filter a "containment layer" and states in terms: **"This is not the final
  cleanup"** (lines 77-78).

### The gap between what was asked and what shipped

The audit's "Next Required Work" (lines 126-132) is five steps. Step 3 is **"Remove or migrate
dev-only authored content out of the production scene asset."** Step 5 is to capture logs proving the
cleanup path executes.

The end state the document asked for is therefore **scene surgery that removes the content**. What
exists instead is a runtime flag flip that deactivates it on every boot. These are not equivalent:

- The objects are still serialised in the scene asset and are still deserialised and instantiated at
  load. Deactivation does not reclaim that.
- `WorldShippingSceneRuntimeGuard` runs at `AfterSceneLoad` and on the `sceneLoaded` callback — both
  of which fire **after** the scene's objects have been created and enabled. UNVERIFIED but following
  directly from Unity's documented initialisation order: `Awake` and `OnEnable` on suppressed objects
  run at least once before the filter disables them. Proving or refuting this needs a play-mode probe;
  it has not been run.
- Every boot pays the walk. `PrimeSuppressionCacheForScene` traverses every transform in the scene
  once per scene handle, which removal would make unnecessary.

Whether that difference is acceptable is a judgement call this document does not make. What it does
record is that the difference exists and that the archived document asked for the other thing.

### Pattern 1 — from a second archived plan, not from the cleanup audit

Correcting a natural assumption: `SCENE_TRUTH_CLEANUP_AUDIT.md` **never proposes a `__TEMP_` rule.**
It mentions `__TEMP_DENSE_KELP_PREVIEW` only as one observed object (lines 86, 103).

The prefix rule traces to a different archived document:
`Docs/_Archive/2026-04-29_Two_Day_Stale_Active_Docs/2026-04-15_Subnautica_Gap_Audit/HECTON8_SUBNAUTICA_GAP_AUDIT_AND_EXECUTION_PLAN.md`,
also `PENDING VERIFICATION`, also 2026-04-15. Its "Phase 1 — Stop Shipping The Workshop" says at
line 102: "extend shipping cleanup to suppress `__TEMP_*`", and its change log at line 150 records
the code change. Its stated expected result (line 154) is that
"`__TEMP_DENSE_KELP_PREVIEW` stops acting like live shipping content".

So pattern 1 was requested in writing — but generalised from **a single observed object** to an
open-ended prefix rule, and never verified. Its own verification section (lines 156-170) records that
Unity script validation returned 0 errors and that the play smoke was "inconclusive", ending "still
`PENDING VERIFICATION`".

### The two archived documents contradict each other

`SCENE_TRUTH_CLEANUP_AUDIT.md` lines 97-111 claim authored cleanup was applied — that `Tool_Staging`,
`Fabrication_Trial` and `__TEMP_DENSE_KELP_PREVIEW` were "explicitly set inactive in the live
`02_HECTON_WORLD` authoring session". Note that even this claim is deactivation, not removal.

The gap audit's readback (lines 163-166), **same date**, records the opposite: `Tool_TrialRange` read
back inactive, but "`Tool_Staging`, `Fabrication_Trial`, `__TEMP_DENSE_KELP_PREVIEW` still read active
in that direct world-scene run".

The binary decode in this document agrees with the gap audit and not with the cleanup audit: all three
roots are `m_IsActive = 1` today. Neither archived claim should be cited as settled.

## Is `__TEMP_` load-bearing or a tripwire?

**Nothing in the project produces a `__TEMP_*` object, at runtime or at bake time.** Searched
repo-wide, with binary-safe search across every `.unity`, `.prefab`, `.asset` and `.mat`. Every
occurrence of the token `__TEMP` in the repository is one of:

| Location | Kind |
|---|---|
| `WorldShippingContentFilter.cs:10` | the const declaration itself |
| `WorldSceneCleanupValidator.cs:20` | an **editor** naming scanner's report list |
| 5 files under `Docs/_Archive/**` | archived prose about `__TEMP_DENSE_KELP_PREVIEW` |

No pooling system, no bake step, no generator, no procedural spawner, no test fixture creates one.
Nearest miss, checked and excluded: `Assets/Crest/Crest/Scripts/Reflection/OceanPlanarReflection.cs:384`
builds `name = "__WaterReflection" + GetHashCode()`. That is `__W`, not `__TEMP_`, so it does not match.

And the one object the rule was written for is gone: `DENSE_KELP` and `KELP_PREVIEW` return **0
matches in all 7 scenes**, using the same binary-safe method that returns positive hits in those same
files.

Verdict, stated as a verdict and not as a fact about intent:

> Pattern 1 has **zero producers and zero current targets**. It is not load-bearing. It is a
> tripwire: the first author who names a placeholder `__TEMP_Foo` — the obvious thing to name a
> placeholder — gets it permanently deactivated in shipping with no log line naming it and no
> allowlist to escape through.

Patterns 2, 3 and 4, by contrast, **are** load-bearing today: their targets are present and authored
active in two scenes, and nothing else deactivates them.

This document does not remove pattern 1. Removing a rule from a shipping gate is a source change with
its own proof requirements, and `WorldShippingContentFilter.cs` was not editable at the time of
writing. The finding is recorded so the decision is made deliberately rather than inherited.

## Why the requested scene surgery is not a one-line change

The archived audit's step 3 — remove the content from the scene asset — conflicts with four editor
systems that require `Tool_Staging` to **exist**:

| File | Line | Expectation |
|---|---|---|
| `ConstructionBootstrapAuthoring.cs` | `339-348` | `GameObject.Find("Tool_Staging")`, else `new GameObject("Tool_Staging")` |
| `H8_AuthoringRootReachabilityGate.cs` | `127-130` | registers `Tool_Staging` as an expected authoring root |
| `ToolStackValidator.cs` | `392-450` | warns when `Tool_Staging` is missing or has no child pickups |
| `ToolWorldAuthoringValidator.cs` | `76`, `124` | warns when a tool has no staged instance |

`H8_AuthoringRootReachabilityGate.cs:112-118` likewise registers `Fabrication_Trial` and
`Fabrication_Trial/Trial_Fabricator`.

There is also a specific hazard in combining the audit's *own* remedy with the authoring tools.
`GameObject.Find` at `ConstructionBootstrapAuthoring.cs:339` does not return inactive objects. If an
author does what the cleanup audit says it did — set `Tool_Staging` inactive in the authoring scene —
the next press of that authoring button cannot see it and creates a **second** `Tool_Staging` at
`:345`. That is precisely the `ShadowedBelowRoot` / `AmbiguousDuplicate` failure class that
`H8_AuthoringRootReachabilityGate.cs:142-158` exists to enumerate.

UNVERIFIED: whether this actually happened. It is one plausible mechanism for why the roots read
active today despite the cleanup audit's claim, but the alternative — the authoring session was never
saved — is equally consistent with the evidence and has not been distinguished from it.

Residual trial-labelled data also survives outside the scenes, consistent with the audit's own "Known
retained risk" note (lines 114-118): `Assets/_Project/Data/World/FamilyProfiles/` holds 12
`FamilyProfile_trial_*.asset` files, and `ZoneProfile_Trial_Early.asset` has four derived companions
(`Read_`, `Motivation_`, `Sandbox_`, `ZonePlan_`).

## Content author rules

Read this before naming a GameObject in any scene.

**Never name a GameObject, or any ancestor of one you need, any of these:**

1. Anything starting with `__TEMP_` — case-sensitive, exact prefix. `__TEMP_Kelp`, `__TEMP_2`,
   `__TEMP_placeholder` are all permanently deactivated in shipping. This is the trap: it is an
   open-ended prefix with no allowlist, no log line naming the victim, and no current legitimate use.
2. `Tool_Staging`
3. `Fabrication_Trial`
4. `Tool_TrialRange`

**Never author a `WorldZoneAnchor` with:**

5. A `ZoneId` starting with `zone.trial.` — the id prefix suppresses regardless of `ZoneKind`. Six of
   the seven authored trial zones are caught this way while carrying ordinary gameplay kinds.
6. `Kind = ZoneKind.Trial`.

**Also be aware:**

- Suppression is inherited downward. A correctly-named child under a suppressed ancestor is
  deactivated with it and is never individually evaluated or counted (`:237-241`, `:151-160`).
- `WorldContentSocket`s under a suppressed zone are dropped from `CopyActiveSocketsTo`
  (`WorldContentSocket.cs:127`), and `LODGroup`s under suppressed hierarchies are never registered
  (`WorldLODSceneBootstrap.cs:118`).
- The name cache is primed **once per scene handle** (`:209-210`). Renaming an object at runtime after
  priming does not re-evaluate it in either direction.
- Suppression is by name string only. Nothing about a tag, layer, component, or build configuration
  will exempt an object whose name matches.
- The editor scanner `WorldSceneCleanupValidator.cs` (menu `Hecton8/Validate World Scene Cleanup`)
  flags a much wider set of names (`:18-56`: `Test_`, `Debug_`, `Trial_`, `Staging_`, `Smoke_`,
  `_Prototype`, and keywords `preview`, `wip`, …). Those are **report-only** and are not suppressed at
  runtime. Do not read that list as the runtime contract; the runtime contract is the six rules above.

## Verification status

| Claim | Evidence class |
|---|---|
| The four patterns, their source lines, and evaluation order | STATIC_SOURCE — read directly, high confidence |
| `__TEMP_` has no producer anywhere in the repo | Repo-wide binary-safe search — high confidence |
| `__TEMP_*` absent from all 7 scenes | Binary-safe search; every binary scene carries its own `Untagged` positive control (505/421/102/28) |
| `Tool_Staging` / `Fabrication_Trial` / `Tool_TrialRange` present in `02_HECTON_WORLD` and `010_TEST` | Binary-safe search — earned positive |
| Those three roots are `m_IsActive = 1` today | Hand-decoded binary layout, validated against a 143/362 zero/one distribution over 505 objects — **not** a Unity readback |
| `Awake`/`OnEnable` run on suppressed objects before deactivation | **UNVERIFIED** — follows from documented Unity order; no play-mode probe run |
| The unprimed-cache read path is reachable in practice | **UNVERIFIED** — no play-mode probe run |
| Player-trust rationale for suppressing trial content | **INHERITED, `PENDING VERIFICATION`** — asserted by two archived documents, never verified |
| Any claim that the scene was cleaned in authoring | **CONTRADICTED** — the two archived documents disagree, and the binary decode favours "still active" |
| Load-cost impact of deactivate-instead-of-remove | **UNVERIFIED** — no profiler capture; argued structurally only |

No Unity play-mode run, profiler capture, or player build was executed for this document. There is no
automated test anywhere covering `WorldShippingContentFilter` — searched
`Assets/_Project/Tests/` for `WorldShippingContentFilter`, `IsSuppressedHierarchyName`, and
`Tool_Staging`, with zero hits. Every behaviour above is a code read plus a scene byte read.

# 1813 Stale Blocker Errata Packet

ID: 1813
Role: STALE_BLOCKER_ERRATA_PACKET
Proof class: STATIC VERIFIED only

This packet corrects stale or overstated blocker claims identified by `Docs/Reports/Batch18/1805_AGENT_OUTPUT_TRIAGE_DASHBOARD.md`. It is a routing errata artifact, not Unity acceptance, not profiler proof, not visual proof, and not DataMonolith bake proof.

Source basis:
- `C:/hades/Hecton8/taskslocal/batch18_night_orchestration/1813_STALE_BLOCKER_ERRATA_PACKET.txt`
- `C:/hades/Hecton8/Docs/Reports/Batch18/1805_AGENT_OUTPUT_TRIAGE_DASHBOARD.md`
- `C:/hades/Hecton8/Docs/Reports/Batch18/1804_APPLIED_LORE_DATAMONOLITH_RECONCILE.md`
- `C:/hades/Hecton8/Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`
- `C:/hades/Hecton8/Assets/_Project/Scripts/Quest/MissionMarkerSystem.cs`
- Older stale source examples under `Docs/BibleMandateAudits/1700/`, `Docs/BIBLE_MANDATE_AUDIT_1700_COMBINED.md`, `Docs/Tasks/Status_1428.md`, `Docs/AgentLogs/LOG_1428.md`, `Docs/Tasks/Status_1778.md`, and `Docs/AgentLogs/LOG_1778.md`

## Errata

1. ProceduralWreckGenerator merged-mesh fallback

Stale claim: player runtime has a proven `BuildMergedMesh*` / proxy-mesh fallback blocker that must be rewritten now.

Correction: current source places `BuildMergedMeshForTier`, `BuildMergedMeshForTierAsync`, `BuildMergedMesh`, `BuildMergedMeshAsync`, and `ShouldBuildMergedMeshFallback()` behind editor-only or play-guarded paths. `ShouldBuildMergedMeshFallback()` returns `!Application.isPlaying`. Current source search did not find `BuildProxyMesh`. Older 1700-era references to runtime proxy fallback are stale unless a future agent reproduces them in current source.

What to do instead: do not assign a runtime mesh rewrite from the old claim. If wreck visuals, collision, or import behavior need acceptance, inspect current source and run the scoped Unity/import/player proof only when a Unity slot is assigned.

2. MissionMarkerSystem marker fallback

Stale claim: current runtime still creates marker mesh/material fallback assets through `CreateMarkerMesh` or equivalent runtime generation.

Correction: current source search did not find `CreateMarkerMesh`. `EnsureRuntimeResources()` validates serialized `markerMesh` and `markerMaterial`; invalid resources set runtime refs to null and visible marker count to zero. The current problem, if any, is authored-resource assignment and visibility proof, not stale fallback code.

What to do instead: verify prefab/scene assignment and marker rendering in a Unity slot. Do not delete or rewrite fallback methods that do not exist in current source.

3. P288 DataMonolith mismatch

Stale claim: `P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP` blob length mismatch is a current blocker.

Correction: `Docs/Tasks/Status_1778.md` and `Docs/AgentLogs/LOG_1778.md` contain the older P288 mismatch. `Docs/Reports/Batch18/1804_APPLIED_LORE_DATAMONOLITH_RECONCILE.md` and 1805 classify it as stale/historical unless a fresh packet or bake rerun reproduces it. Current active AppliedLore blockers from 1804/1805 are P151 generated-page drift and P456 source-locale residue.

What to do instead: do not chase P288 unless a fresh importer/bake artifact reproduces it. Route current AppliedLore work through P151/P456 source repair first.

4. Static microsecond estimates

Stale claim: old static audit estimates such as 17-C, 17-D, or 1700-era "microseconds saved" are profiler proof.

Correction: static source review can identify suspicious patterns, but it cannot prove frame-time savings. `quality.md` and `QA_Evidence_Text_Filter_Audit.txt` require profiler artifact paths and sample context before a timing claim becomes profiler proof.

What to do instead: keep these entries as static optimization leads. Upgrade only after scoped Unity Profiler evidence exists.

5. Old PlayMode screenshots

Stale claim: old 1428 PlayMode screenshots still prove current visual acceptance.

Correction: `Docs/Tasks/Status_1428.md` and `Docs/AgentLogs/LOG_1428.md` are old scoped route leads. 1805 and `Docs/Reports/Batch18/1810_RUNTIME_PROOF_HARNESS_PREP.md` state that old screenshots are stale for current acceptance unless tied to current scene state and current source/assets.

What to do instead: use old screenshot paths only as route leads. Current surface, shallows, Aegir, sky, moons, coastline, and water acceptance still needs fresh current-candidate capture evidence.

6. Static visual reports

Stale claim: static inventory reports, asset lists, or route matrices are enough to close visual quality.

Correction: 1802 and 1803 are static audit artifacts. They can prove file presence, ownership, naming, and obvious source gaps. They do not prove current in-game appearance, camera exposure, water readability, route composition, or Subnautica-level visual floor.

What to do instead: use static reports to target runtime checks. Final visual acceptance needs current player-view or route-capture artifacts.

7. Generated AppliedLore pages

Stale claim: generated public/wiki pages are source truth for AppliedLore correctness.

Correction: 1804 and 1805 classify generated pages and indexes as downstream artifacts. If source CSV, packet rows, locale manifests, or export routes drift, generated pages can preserve or hide the error.

What to do instead: repair and verify source packets/CSVs first. Regenerate pages only after source parity and locale residue are corrected by the owning task.

8. Localization and native review

Stale claim: static localization queues or generated non-English rows prove native review and final localization quality.

Correction: 1804 and 1805 state that no native-review proof was found for the relevant non-English rows. Static text presence is not linguistic acceptance.

What to do instead: mark non-English rows as draft/native-review-pending unless a named native-review artifact exists.

9. Runtime proof boundary

Stale claim: source snippets, text search, static screenshots, or controller summaries can be promoted to runtime proof.

Correction: per `quality.md`, static evidence remains `STATIC VERIFIED`. Unity import, PlayMode, player build, profiler, GC, frame debugger, visual capture, and DataMonolith bake proof require current artifact paths from the actual tool/run.

What to do instead: keep proof labels exact. Use `PENDING UNITY SLOT` when runtime evidence is required but absent.

10. Do-not-launch cautions

Do not launch Unity, player builds, DataMonolith bakes, page exporters, or profiler runs from this errata task. Future agents should also avoid launching DataMonolith bakes or generated-page exporters while P151/P456 source repair is still unresolved unless they own that route and can serialize the output. Do not run Unity/profiler/player proof in parallel with another active Unity slot.

## Future-Agent Copy Prompt

Before acting on claims about ProceduralWreckGenerator runtime merged-mesh fallback, MissionMarkerSystem marker fallback, P288 DataMonolith mismatch, old 1428 screenshots, static microsecond estimates, generated AppliedLore pages, or native localization review, read `Docs/Reports/Batch18/1813_STALE_BLOCKER_ERRATA_PACKET.md`. Treat it as STATIC VERIFIED errata. If your task needs runtime acceptance, keep the item `PENDING UNITY SLOT` until you produce current artifact paths.

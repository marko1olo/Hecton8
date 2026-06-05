# Rationale 1818 - Mission Marker Assignment Visibility Auditor

Updated: 2026-06-04 04:57 +04:00

## Non-Trivial Decisions

- Audit is static only. The task explicitly forbids Unity/runtime/profiler claims.
- Audit boundary is marker assignment and visibility proof. Older fallback-delete narratives are treated as stale unless current source/data proves them.
- `hud.md` and `navigation.md` were requested but are absent at project root. Used `ui.md`, sonar/navigation mandate, diegetic HUD mandate, quest-state mandate, and performance bible as nearest standing authority.
- Selected mandate set: UI diegetic physical interfaces, UI zero-GC streaming, quest state graph, acoustic/sonar visibility, signal lane segregation, zero-GC policy, and performance budgets.
- Did not edit source, assets, or quest data. The task is an audit-only lane and Unity is explicitly forbidden.
- Existing `M_HUD_ThreatChevron` and `MAT_HUD_ThreatChevronInstanced` are treated as candidate visual assets only. Static refs bind them to HUD/scanner surfaces, not mission marker presentation.
- Absence of serialized marker fields in `Quest_Arrival`, `Quest_CopperSample`, and `Quest_FirstBreath` is treated as missing proof. Default field values are not accepted as assignment proof.
- Fail-closed behavior is safe from placeholder art but not sufficient acceptance because active quests can become silently markerless.

## Locked Assumptions

- Mission markers are player-facing instruments. They must expose route truth and failure/confidence, not decorative icons.
- Marker assignment must be bound to named objective/quest/route owners and actual mesh/material/data references.
- Visibility can fail closed or fail visible, but it must not silently present false certainty or cheap placeholder art.

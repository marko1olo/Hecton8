# Rationale 2012

Evidence class: STATIC_DOC / STATIC_REPORT.

Decisions:

1. Used `Продолжить работу по логам` as the only valid current Unity owner string because the user supplied it and Batch20 task/index files contain mojibake.
2. Treated completed Batch20 evidence as static unless a report/status/log explicitly proved Unity, Play Mode, profiler, or capture. The completed workers found were `2002`, `2003`, `2004`, `2005`, and `2007`; all their evidence remains static-only.
3. Did not invent completion for `2001`, `2006`, `2008`, `2009`, `2010`, or `2011`. Existing reports for those domains are useful inputs, not proof that the sibling worker completed.
4. Serialized all Unity work through one owner. Parallel work is limited to no-Unity static lanes and manual/source generation lanes.
5. Split bake/import-style work into editor-only lanes because it must not steal Unity control from the current owner and must not mutate active Assets without a scoped owner.
6. Kept visual proof before final repair acceptance. Static ledgers can prioritize work but cannot prove surface, waterline, Aegir, moon, or shallow route quality.
7. Required low/middle/high/ultra gates as continuous `GlobalQualityWeight` consequences. No quality lane may change placement truth, gameplay truth, DTO layout, save identity, collider truth, or route ownership.
8. Required Black Box expectations only for future owners touching critical runtime systems. 2012 touched no runtime system.

Non-claims:

- No Unity state was observed by 2012.
- No runtime proof was created.
- No profiler/Frame Debugger/GC/memory proof exists from 2012.
- No generated bitmap candidates were proven by 2012.

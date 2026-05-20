# DOC_GLOBAL_DOCS_REFRESH Rationale

Active rationale file recreated on 2026-05-20 after concurrent workspace archival removed the live `Docs/AgentLogs` copy. Historical full snapshots remain under `Docs/Archive/Batch008`, `Docs/Archive/Batch009`, and `Docs/Archive/Batch010`.

## Decision 36: R36 Root / Architecture Authority Spine And Domain Map

Problem: R35 validation-pending wording survived in active root/architecture authority docs, and `Docs/Actual Domains of Project.txt` remained a 2026-05-17 R4-only domain authority with malformed domain lines. During R36, subagents caught the temporary false state where R36 was referenced before its report existed.

Solution: Create the R36 report, promote R36 through root/architecture surfaces, keep R35/R34 as prior boundaries, fix the domain map, add R4/R36 boundaries to the two repository-root docs in scope, regenerate the atlas, and record static validation.

Rejected Alternatives: Leaving R36 as an absent promised path was rejected as false documentation. Reverting R36 to R35 after R36 edits was rejected after the R36 report existed and validation was recorded. Creating placeholder vendor assets, Unity logs, screenshots, or source files was rejected as fake evidence. Runtime proof was not claimed because none was run.

Scalability potential: Low-tier readers get a single current authority chain and exact blockers. Middle-tier review gets corrected source anchors and domain-map routing. High/Ultra review can focus on real Unity import, profiler/GC, player build, AtlasCheck vendor cleanup, and stale generated project includes.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

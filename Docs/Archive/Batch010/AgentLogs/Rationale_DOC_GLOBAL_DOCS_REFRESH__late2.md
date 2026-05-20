# DOC_GLOBAL_DOCS_REFRESH Rationale

Rationale file recreated on 2026-05-20 after the active `Docs/AgentLogs` working set was archived/removed by concurrent workspace activity. Historical full rationale snapshots remain in `Docs/Archive/Batch008`, `Docs/Archive/Batch009`, and `Docs/Archive/Batch010`.

## Decision 36: R36 Root / Architecture Authority Spine And Domain Map

Problem: After R35, active root/architecture entrypoints still contained R35 validation-pending residue, and `Docs/Actual Domains of Project.txt` was still a 2026-05-17 R4-only authority file even though AGENTS treats it as the domain boundary. During R36, subagents correctly caught that R36 had been referenced before the report existed.

Solution: Create `Docs/Reports/2026-05-20_DOCUMENTATION_R36_ROOT_ARCHITECTURE_AUTHORITY_SPINE_LOCAL.md`, promote R36 through root/architecture authority surfaces, keep R35 as prior R4/counter-residue correction, keep R34 as prior source-counter/physical-line refresh, add R36 to the domain map, repair malformed domain-map lines, add R4 boundaries to root ledger/roadmap files, regenerate the atlas, and record static validation.

Rejected Alternatives: Reverting to R35 after R36 edits was rejected because the R36 report now exists and active docs changed. Leaving R36 as an absent promised path was rejected as false documentation. Creating placeholder vendor assets, screenshots, Unity logs, or source files was rejected as fake evidence. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run.

Scalability potential: Low-tier readers get a single current authority chain and do not chase absent R36/R35 validation states. Middle-tier review gets current domain-map formatting and exact static blockers. High/Ultra review can focus on real Unity import, profiler/GC, player build, AtlasCheck vendor cleanup, and stale generated project includes.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

# DOC_GLOBAL_DOCS_REFRESH Rationale

Date: 2026-05-20
Domain: Echelon 9.83 Chronicler / Project Documentation Currency

## R37 Decisions

Problem: root/architecture docs had R36 as latest while R37 artifact-path and counter edits existed.
Solution: promote R37 as latest local static artifact-path/proof-wording/source-counter boundary; keep R36 as prior authority-spine/domain-map correction.
Rejected Alternatives: leaving R36 as current would make fresh R37 counter edits undocumented; promoting R37 as runtime proof would violate evidence law.
Scalability potential: low/mid/high/ultra unaffected at runtime; documentation correctness prevents false platform/profiler promises.
Hardware Impact: 0 us frame-time impact on i3/MX350; documentation-only.

Problem: active docs cited missing `Docs/AgentLogs` artifacts after Batch010 archival.
Solution: point to `Docs/Archive/Batch010/AgentLogs/...` where artifacts exist and state active copies are absent.
Rejected Alternatives: recreate fake active artifacts or leave broken active paths.
Scalability potential: low/mid/high/ultra unaffected; avoids fake evidence entering build gates.
Hardware Impact: 0 us frame-time impact.

Problem: R34 source counters were stale under concurrent source churn.
Solution: recapture R37 static counters: `1960` project C# files, `1901` script C# files, `1338727` project lines, `133` asmdefs, `135` typed `SignalBus<T>` lanes.
Rejected Alternatives: using R34 as current or claiming counters as compile/runtime proof.
Scalability potential: low/mid/high/ultra unaffected directly; better source-scale risk framing.
Hardware Impact: 0 us frame-time impact.

Problem: large-owner lists missed current line-heavy owners.
Solution: update `HECTON8_GLOBAL_ARCHITECTURE_MAP.md` and `PROJECT_STATE_STATIC_XRAY.md` with current line/byte facts, including generated `H8LocHashes`, `GlobalSignals`, and `SpatialAudioManager`.
Rejected Alternatives: treating old file-size data as stable architecture truth.
Scalability potential: low-tier risk review now sees actual ownership concentration; high/ultra planning avoids false hot-path assumptions.
Hardware Impact: 0 us frame-time impact.

Problem: raw global-authority grep counts changed.
Solution: update them as orientation only, not gates: `GlobalRegistry.` `6068`, bus hits `596`, native-collection hits `20818`, `NativeQueue<...>` `115`, `Configure/EnsureInitialized` `271`, direct queues `73`, typed lanes `135`.
Rejected Alternatives: turning raw grep values into acceptance gates.
Scalability potential: low/mid/high/ultra unaffected directly; prevents global monolith drift from being hidden.
Hardware Impact: 0 us frame-time impact.

Problem: static validation shows atlas remains red.
Solution: record `ATLAS_CHECK_FAIL references=6638 missing=58` as blocker, with Dynamic Decals and RealtimeCSG vendor refs; do not call the atlas verified.
Rejected Alternatives: ignoring the red result because atlas generation succeeded.
Scalability potential: low/mid/high/ultra unaffected directly; preserves evidence quality.
Hardware Impact: 0 us frame-time impact.

Problem: runtime evidence is absent.
Solution: every touched doc keeps Unity/runtime/profiler/player-build proof pending unless a linked artifact exists.
Rejected Alternatives: inferred runtime cleanliness from static docs/source/tool success.
Scalability potential: protects low-tier and high-tier claims from unmeasured assumptions.
Hardware Impact: 0 us frame-time impact.

## R38 Decisions

Problem: R37 remained the latest advertised root/architecture boundary after a later source-counter drift pass existed on disk.
Solution: promote R38 as the latest local static root/architecture source-counter drift and boundary correction; keep R37 as prior artifact-path/proof-wording/source-counter correction and R36 as prior authority-spine/domain-map correction.
Rejected Alternatives: leaving R37 as latest would make current R38 counters undiscoverable; calling R38 runtime proof would violate evidence law.
Scalability potential: low/mid/high/ultra runtime unchanged; documentation now prevents false performance/platform confidence.
Hardware Impact: 0 us frame-time impact on i3/MX350; documentation-only.

Problem: source physical-line totals changed sharply after R37 and could not be reused as current scale data.
Solution: recapture R38 static counters and record them as volatile orientation: `1960` project C# files, `1901` script C# files, `1936` non-test C# files, `1167207` project physical lines, `1149917` script physical lines, `1161785` non-test physical lines, `133` asmdefs, and `131` non-test asmdefs.
Rejected Alternatives: treating R37 physical lines as stable or turning static counters into compile/runtime proof.
Scalability potential: low-tier risk review sees current source mass; high/ultra planning avoids basing review queues on stale owner counts.
Hardware Impact: 0 us frame-time impact.

Problem: global-authority orientation counts drifted and old bus/native collection numbers could be misread as current.
Solution: update root/architecture docs with R38 orientation counts only: `6069` `GlobalRegistry.` hits, `526` publish/subscribe hits, `21454` native-collection hits, `115` `NativeQueue<...>` refs, `73` `CreateQueue(...)` slots, `135` typed lanes in `GlobalSignals.cs`, `271` configure/ensure hits in `GlobalSignals.cs`, and `266` script-level typed lane matches.
Rejected Alternatives: using raw grep as a quality gate or claiming monolith risk improved/worsened without route-card review and runtime proof.
Scalability potential: low/mid/high/ultra unaffected directly; global-route review starts from current text scale instead of stale snapshots.
Hardware Impact: 0 us frame-time impact.

Problem: generated atlas outputs must not be hand-edited separately from their generator and tests.
Solution: update `Tools/BuildArchitectureAtlas.py` and `Tools/test_architecture_atlas.py`, then regenerate `Docs/DEPENDENCY_GRAPH.md` and `Docs/DEPENDENCY_GRAPH.json`.
Rejected Alternatives: editing generated docs only, which would be overwritten and would hide the persistent `AtlasCheck` red state.
Scalability potential: low/mid/high/ultra unaffected; static dependency review remains reproducible.
Hardware Impact: 0 us frame-time impact.

Problem: a mechanical replacement introduced malformed CRLF escape text into current entrypoint docs.
Solution: manually split the malformed R38/R37 lines in `Docs/README.md` and `Docs/Reports/README.md`, then rescan for the literal residue.
Rejected Alternatives: treating malformed index text as harmless, because the current evidence chain is an active navigation surface.
Scalability potential: low/mid/high/ultra unaffected; reduces human navigation errors in future passes.
Hardware Impact: 0 us frame-time impact.

Problem: `Tools/AtlasCheck.py` still fails after atlas regeneration.
Solution: keep AtlasCheck red in every touched current surface: `ATLAS_CHECK_FAIL references=6638 missing=58`, including `Assets/Dynamic Decals/Resources/Decal.obj` and RealtimeCSG vendor icon/readme image refs.
Rejected Alternatives: calling the atlas verified because generation and unit tests pass.
Scalability potential: prevents vendor/missing-asset debt from entering release evidence as green.
Hardware Impact: 0 us frame-time impact.

## R39 Decisions

Problem: root/architecture docs still treated R38/R37 material as latest where R39 authority-counter and proof-wording corrections were needed.
Solution: promote R39 as the latest local static root/architecture authority-counter/proof-wording boundary, while leaving R38 as the prior source-counter drift correction and R37 as the prior artifact-path/proof-wording/source-counter correction.
Rejected Alternatives: leaving R38 as latest after R39 edits, or promoting R39 as runtime/Unity proof.
Scalability potential: low/mid/high/ultra runtime unchanged; documentation no longer advertises stale proof status as current.
Hardware Impact: 0 us frame-time impact on i3/MX350; documentation-only.

Problem: active docs cited historical CLI/Python/static scan rows with proof verbs or missing artifact tuples.
Solution: demote those rows to `STATIC_SOURCE`, `STATIC_DOC`, `HISTORICAL_OFFLINE_STATIC_SIM`, or historical CLI report text unless they include artifact path, command, timestamp, environment, and output.
Rejected Alternatives: treating `0 Warning(s)` / `0 Error(s)`, Python harness text, or "clean scan" wording as current build/runtime proof.
Scalability potential: prevents low-tier and ultra-tier acceptance plans from relying on unmeasured compile/runtime claims.
Hardware Impact: 0 us frame-time impact.

Problem: several active root/architecture docs pointed at absent active files or former root mirrors.
Solution: route SHINOBU_02 and SHINOBU_160 evidence to Batch010 archive paths, mark active `Docs/AgentLogs/...` copies absent where applicable, and state root `PROJECT_ATLAS.md`, `BROKEN_PREFABS.md`, and `TERRAIN_AND_BIOME_REALITY_MAP.md` are absent after cleanup.
Rejected Alternatives: recreating fake active artifacts or keeping former root names as live authority.
Scalability potential: reduces false handoff work across all device tiers by keeping evidence retrieval deterministic.
Hardware Impact: 0 us frame-time impact.

Problem: `C:\Hecton8\Tools` verifier wording was stale for the current `C:\hades\Hecton8` workspace.
Solution: mark the NetProtocolGate row as historical/offline and require rerun under the active workspace before PASS language is current.
Rejected Alternatives: assuming an archived absolute path maps to the current checkout.
Scalability potential: prevents stale tool-cache state from contaminating future deterministic network proof.
Hardware Impact: 0 us frame-time impact.

Problem: broad `git diff --check` is polluted by unrelated dirty deprecated files with trailing whitespace.
Solution: record the full-scope blocker and rely on scoped root/architecture/R39 report/generated atlas diff check for this pass.
Rejected Alternatives: editing or reverting unrelated deprecated/user/agent files to make a broad diff check green.
Scalability potential: no runtime effect; preserves concurrent-agent ownership boundaries.
Hardware Impact: 0 us frame-time impact.

## R40 Decisions

Problem: active root/architecture entrypoints had R40 top boundaries, but interior read-order text still made R38 or R39 look current.
Solution: promote R40 through the interior root/architecture authority surfaces, including `Docs/Actual Domains of Project.txt`, and keep R39/R38 as explicitly prior layers.
Rejected Alternatives: leaving interior text stale because top R4 blocks were already correct, or calling R40 runtime proof.
Scalability potential: low/mid/high/ultra runtime unchanged; documentation consumers now start from the newest static evidence boundary.
Hardware Impact: 0 us frame-time impact on i3/MX350; documentation-only.

Problem: R40 source counters drifted during the pass and the early R40 snapshot was already stale.
Solution: rerun the source-counter scan and update active stable docs plus the R40 report to `1960/1901/1936` C# files, `1341123/1321033/1335006` lines, `324/321` broad interface hits, `271` direct interface declarations, `62` registry interfaces, `133/131` asmdefs, `6056` registry hits, `571` publish/subscribe hits, and `15465` native-collection hits.
Rejected Alternatives: keeping early R40 counts after validation disagreed, or treating volatile grep counts as quality gates.
Scalability potential: low-tier review sees current source mass; high/ultra planning avoids stale coupling and review-queue estimates.
Hardware Impact: 0 us frame-time impact.

Problem: archived SHINOBU_02 SignalCritical/Full audit artifacts were still described with latest/current wording in active root/architecture docs.
Solution: demote them to archived historical static-source artifacts until a fresh guarded audit/trend/H-Phi run produces current artifacts.
Rejected Alternatives: inferring current H-Phi, duplicate-signal, compile, Unity, or runtime status from archived current21/current36 artifacts.
Scalability potential: prevents all device-tier acceptance plans from relying on unmeasured runtime or platform claims.
Hardware Impact: 0 us frame-time impact.

Problem: global-authority route-card inventories could be read as route approval when they were only static orientation.
Solution: add explicit orientation-only wording and owner/phase/capacity/overflow/failure/proof framing where active route-card docs implied a global route.
Rejected Alternatives: treating static route lists as `GREEN` review disposition without the required route-card tuple.
Scalability potential: keeps low/mid/high/ultra route expansion bounded by owner-local proof and avoids uncontrolled global authority growth.
Hardware Impact: 0 us frame-time impact.

Problem: `py_compile` could not write pyc files in `Tools\__pycache__`, and an alternate workspace-local pycache prefix also failed on rename.
Solution: record py_compile as filesystem-blocked and run AST parse for the three atlas tools as syntax evidence only.
Rejected Alternatives: claiming bytecode verification passed, deleting unrelated pycache state, or escalating for a docs-only pass.
Scalability potential: no runtime effect; preserves evidence precision for future tool validation.
Hardware Impact: 0 us frame-time impact.

Problem: `Tools/AtlasCheck.py` remains red after atlas regeneration.
Solution: keep AtlasCheck red in R40 report and status: `ATLAS_CHECK_FAIL references=6638 missing=58`, including Dynamic Decals and RealtimeCSG vendor refs.
Rejected Alternatives: calling the atlas verified because generation and unit tests pass.
Scalability potential: prevents missing vendor payloads from being hidden behind green documentation wording.
Hardware Impact: 0 us frame-time impact.

## R41 Decisions

Problem: active docs were promoted to R41 text before the R41 report artifact existed.
Solution: create `Docs/Reports/2026-05-20_DOCUMENTATION_R41_ROOT_ARCHITECTURE_GLOBAL_AUTHORITY_INTERNAL_RESIDUE_LOCAL.md` and make R41 disk-backed before final validation.
Rejected Alternatives: reverting all R41 wording to R40 after R41 edits existed, or leaving a missing report path in current boundary text.
Scalability potential: low/mid/high/ultra runtime unchanged; documentation consumers can resolve the current boundary without fake evidence.
Hardware Impact: 0 us frame-time impact on i3/MX350; documentation-only.

Problem: R40 counters drifted again under concurrent source churn.
Solution: recapture R41 source counters and update active root/architecture entrypoints to `1962/1903/1938` C# files, `1344787/1324687/1338671` lines, `324/321` broad interface hits, `270` direct interface declarations, `62` registry interfaces, `133/131` asmdefs, `6060` registry hits, `340` publish/subscribe hits, and `15291` native-collection hits.
Rejected Alternatives: keeping R40 exact counters as current, or deleting exact counters entirely instead of marking them volatile static orientation.
Scalability potential: low-tier review sees current source mass; high/ultra planning avoids stale route-card and review-queue estimates.
Hardware Impact: 0 us frame-time impact.

Problem: generated atlas line counters overcounted newline-terminated files by one line.
Solution: change `Tools/BuildArchitectureAtlas.py` line counting to `raw.count(b"\n") + (0 if not raw or raw.endswith(b"\n") else 1)` and update `Tools/test_architecture_atlas.py`.
Rejected Alternatives: preserving inflated generated counts for test compatibility or hand-editing generated atlas output.
Scalability potential: no runtime effect; static dependency/scale review stops carrying artificial line inflation.
Hardware Impact: 0 us frame-time impact.

Problem: several global-authority route cards described cross-domain DataVault/SignalBus routes without explicit review disposition and failure/overflow/proof fields.
Solution: add `YELLOW / STATIC_SOURCE_ONLY` route-card disposition fields to SHINOBU_113, SHINOBU_138, SHINOBU_151, and SHINOBU_125.
Rejected Alternatives: letting route-card wording imply `GREEN` acceptance without compile/import/runtime/profiler artifacts.
Scalability potential: keeps low/mid/high/ultra expansion bounded by owner-local proof and avoids uncontrolled global authority growth.
Hardware Impact: 0 us frame-time impact.

Problem: `Tools/AtlasCheck.py` remains red after R41 report and atlas regeneration.
Solution: record current red state as `ATLAS_CHECK_FAIL references=6642 missing=58`, with Dynamic Decals and RealtimeCSG vendor refs; do not call the atlas verified.
Rejected Alternatives: calling the atlas verified because generation/tests pass, or fabricating missing vendor files.
Scalability potential: prevents missing vendor payloads from entering release evidence as green.
Hardware Impact: 0 us frame-time impact.

## R42 Decisions

Problem: active root/architecture docs still carried R41 or older currentness after the user narrowed the lane to root/architecture documentation.
Solution: promote R42 as the current static DOC_GLOBAL boundary across root anchors, active Docs indexes, Reports index, Architecture actuality ledger, generated atlas metadata, and active architecture documents.
Rejected Alternatives: leaving R41 current because top-level files already mentioned R42 in some places, or mutating historical archives as if they were current authority.
Scalability potential: no runtime effect; low/mid/high/ultra planning starts from one current static boundary instead of mixed R40/R41/R42 text.
Hardware Impact: 0 us frame-time impact.

Problem: newly active architecture files were added or surfaced during the pass without the current actuality boundary.
Solution: insert the R42 root/architecture actuality boundary and runtime-proof caveat into the missing active architecture/root files until the wide scan reached `ScopeFiles=125`, `Missing=0`.
Rejected Alternatives: accepting a 2026-05-17 R4-only marker as current enough for new SHINOBU route/asset docs.
Scalability potential: keeps future low-to-ultra device-tier claims subordinate to current source and fresh evidence instead of stale doc headers.
Hardware Impact: 0 us frame-time impact.

Problem: source counters drifted during the R42 pass.
Solution: recapture counters and update active surfaces to `2029/1970/2003` C# files, `1382236/1362107/1375742` lines, `302` direct interface declarations, `66` registry interfaces, `139/137` asmdefs, `6101` registry hits, `1200` publish/subscribe hits, and `16397` native-collection hits.
Rejected Alternatives: keeping the earlier R42 `2007/1948/1983` snapshot or deleting exact counters instead of marking them volatile static orientation.
Scalability potential: low-tier planning sees current source mass; high/ultra planning avoids stale review and route-capacity estimates.
Hardware Impact: 0 us frame-time impact.

Problem: `Tools/AtlasCheck.py` remains red after regeneration.
Solution: keep the atlas status red at `ATLAS_CHECK_FAIL references=6728 missing=58` and record the missing Dynamic Decals/RealtimeCSG vendor refs as the blocker.
Rejected Alternatives: calling the atlas verified because generation, unit tests, and py_compile pass, or fabricating vendor assets.
Scalability potential: prevents missing vendor payloads from entering release evidence as green on any device tier.
Hardware Impact: 0 us frame-time impact.

Problem: root playtest and roadmap docs used capture-time runtime language that could be read as current proof.
Solution: demote those rows to capture-time log/editor readback text and require artifact tuple before runtime/profiler/player claims.
Rejected Alternatives: treating subjective visual or timing notes as current Play Mode/profiler evidence.
Scalability potential: all device tiers require real artifact proof before performance or visual acceptance claims.
Hardware Impact: 0 us frame-time impact.

## R43 Decisions

Problem: root/architecture docs still carried R42 as latest after route-card, counter, and AtlasCheck red-state drift was found.
Solution: promote R43 as the current local static DOC_GLOBAL boundary across root entrypoints, active architecture docs, Reports index, generated atlas metadata, and `AGENTS.md`.
Rejected Alternatives: leaving R42 as current because it already had a clean boundary scan, or mutating archived historical reports as if they were active evidence.
Scalability potential: no runtime effect; low/mid/high/ultra planning starts from one current static boundary and does not inherit stale source-scale assumptions.
Hardware Impact: 0 us frame-time impact on i3/MX350; documentation-only.

Problem: source counters and first-party asmdef counts drifted again under concurrent source churn.
Solution: recapture R43 source counters and update active root/architecture surfaces to `2047/1986/2021` C# files, `1394096/1373895/1387908` lines, `375/370` broad interface hits, `272` direct interface declarations, `62` registry interfaces, `141/139` asmdefs, `6131` registry hits, `310` publish/subscribe hits, and `23109` native-collection hits.
Rejected Alternatives: keeping R42 `2029/1970/2003` and `139/137`, or removing exact counters instead of marking them volatile STATIC_SOURCE orientation.
Scalability potential: low-tier review sees current source mass; high/ultra route and owner review planning stops using stale queue sizes.
Hardware Impact: 0 us frame-time impact.

Problem: route-card documents used route language without a complete review tuple, and some still used `proof required before acceptance` wording.
Solution: scan all active architecture `*ROUTE_CARD*.md` files for route ID, owner, producer/consumer phase, cadence, capacity, overflow/failure, shutdown, `Proof required before GREEN`, and `Review disposition`; patch all missing fields until `RouteCardFiles=14`, `Missing=0`.
Rejected Alternatives: allowing `YELLOW` static route text to imply `GREEN`, or checking only the first eight route cards found by the earlier audit.
Scalability potential: keeps low/mid/high/ultra route expansion bounded by owner-local proof and prevents uncontrolled global-authority growth.
Hardware Impact: 0 us frame-time impact.

Problem: AtlasCheck red state changed after regeneration and one generated report path was transiently missing before the R43 report existed.
Solution: rerun after the report was created and record the stable red tuple: `ATLAS_CHECK_FAIL references=6736 missing=59`, covering Dynamic Decals, RealtimeCSG vendor icons/readme images, and missing `HabitatDamageBakePipeline.cs`.
Rejected Alternatives: calling the atlas verified because generator, unit tests, and py_compile pass; fabricating missing vendor/source files.
Scalability potential: prevents missing payload/source references from entering release evidence as green on any device tier.
Hardware Impact: 0 us frame-time impact.

Problem: runtime proof remains absent.
Solution: every touched doc/report keeps Unity import, clean Console, Play Mode, profiler, GCMonitor, player build, save/load, platform run, and visual-route evidence pending unless a fresh artifact tuple is linked.
Rejected Alternatives: inferring runtime or performance truth from static source scans, route-card completeness, or documentation checks.
Scalability potential: all device tiers require real proof before platform/performance acceptance claims.
Hardware Impact: 0 us frame-time impact.

## R44 Decisions

Problem: active root/architecture documents still carried R42/R43 interior residue after R43 was promoted.
Solution: promote R44 as the latest local static DOC_GLOBAL boundary and patch the actual interior statements: root authority chains, architecture-map current-state section, Reports index, generated atlas metadata, and active architecture boundary notes.
Rejected Alternatives: leaving R43 as current because the top boundary line existed, or editing historical dated reports as if they were current authority.
Scalability potential: no runtime effect; low/mid/high/ultra planning now starts from one current source/doc boundary instead of mixed R42/R43 text.
Hardware Impact: 0 us frame-time impact on i3/MX350; documentation-only.

Problem: active route-card scan accepted loose labels (`Overflow / failure`, `Overflow/failure mode`, and `Shutdown`) that do not match the review template.
Solution: require exact labels `Overflow/failure` and `Shutdown/disposal` across all active architecture route cards and keep `Proof required before GREEN` plus `Review disposition`.
Rejected Alternatives: treating loose labels as review-ready route metadata or calling static route cards `GREEN`.
Scalability potential: route growth stays owner-local and evidence-gated before low-tier/mobile or high-tier/ultra route expansion.
Hardware Impact: 0 us frame-time impact.

Problem: proof wording in binary/AUP/construction architecture docs implied static scans, fixture text, or attempted builds were current artifact-backed proof.
Solution: demote those rows to STATIC_SOURCE/PY_TOOL/CLI_ATTEMPTED unless the doc links artifact path, command/tool, timestamp, environment, and output.
Rejected Alternatives: claiming clean compile, runtime cleanliness, or fixture proof from unlinked prose.
Scalability potential: all device tiers require real proof before performance/platform acceptance.
Hardware Impact: 0 us frame-time impact.

Problem: source counters drifted during the R44 pass after concurrent source changes.
Solution: recapture and promote R44 counters: `2050/1989/2024` C# files, `1399032/1378730/1392642` physical lines, `345/342` broad interface hits, `279` interface declarations, `62` registry interfaces, `141/139` asmdefs, `6201` registry hits, `526` publish/subscribe hits, `17840` native hits, `116` `NativeQueue` refs, `73` create slots, `135` typed lanes, `271` configure/ensure hits, and `1345` script typed-lane matches.
Rejected Alternatives: keeping R43 source counters after the fresh scan disagreed, or deleting exact counters instead of marking them volatile static orientation.
Scalability potential: review estimates use current source mass and signal/native pressure before hardware-tier planning.
Hardware Impact: 0 us frame-time impact.

Problem: AtlasCheck red tuple changed after the R44 report and atlas regeneration.
Solution: update generator, generated atlas, tests, root/architecture docs, and R44 report to `ATLAS_CHECK_FAIL references=6739 missing=59`.
Rejected Alternatives: calling the atlas verified because generation/tests pass, or fabricating missing Dynamic Decals/RealtimeCSG/Habitat paths.
Scalability potential: missing payload/source references do not become green evidence on any device tier.
Hardware Impact: 0 us frame-time impact.

## R45 Decisions

Problem: active root/architecture documents still carried R43/R44 interior residue after R44 was promoted.
Solution: promote R45 as the latest local static DOC_GLOBAL boundary and patch the actual interior statements: root read order, glossary/FAQ/atlas currentness, architecture actuality ledger, architecture README, generated atlas metadata, and active architecture boundary notes.
Rejected Alternatives: leaving R44 as current because the top boundary line existed, or editing historical dated reports as if they were current authority.
Scalability potential: no runtime effect; low/mid/high/ultra planning starts from one current source/doc boundary instead of mixed R43/R44 text.
Hardware Impact: 0 us frame-time impact on i3/MX350; documentation-only.

Problem: proof wording in binary and architecture ledgers treated local scan text or `git diff --check` prose as stronger proof than the artifacts supported.
Solution: demote those rows to STATIC_DOC/STATIC_SOURCE/PY_TOOL/POWERSHELL_STATIC unless they include a command, artifact path, environment, timestamp, and output tuple.
Rejected Alternatives: claiming compile, runtime, profiler, or route proof from prose-only evidence.
Scalability potential: all device tiers require artifact-backed proof before platform/performance acceptance.
Hardware Impact: 0 us frame-time impact.

Problem: source counters drifted again during the R45 pass under concurrent source changes.
Solution: recapture and promote R45 counters: `2052/1991/2026` C# files, `1401183/1380785/1394758` physical lines, `345/342` broad interface hits, `280` interface declarations, `63` registry interfaces, `141/139` asmdefs, `6199` registry hits, `575` publish/subscribe hits, `18045` native hits, `116` `NativeQueue` refs, `73` create slots, `135` typed lanes, `271` configure/ensure hits, and `1345` script typed-lane matches.
Rejected Alternatives: keeping R44 source counters after the fresh scan disagreed, or deleting exact counters instead of marking them volatile static orientation.
Scalability potential: review estimates use current source mass and signal/native pressure before hardware-tier planning.
Hardware Impact: 0 us frame-time impact.

Problem: R45 boundary text existed in indexes but not in every active root/architecture file surfaced by the strict scan.
Solution: add direct R45 actuality boundary lines until `R45BoundaryScope=112`, `Missing=0`, then replace stale interior R42/R44 current-boundary paragraphs until `SpecificStaleCurrentHits=0`.
Rejected Alternatives: treating older R4/R42/R44 boundary lines as sufficient for files changed in the R45 lane, or accepting a later R45 paragraph while an earlier paragraph in the same file still said R44 was current.
Scalability potential: no runtime effect; future device-tier docs inherit the same current static boundary.
Hardware Impact: 0 us frame-time impact.

Problem: AtlasCheck red tuple changed after the R45 report and atlas regeneration.
Solution: update generator, generated atlas, tests, root/architecture docs, and R45 report to `ATLAS_CHECK_FAIL references=6741 missing=59`.
Rejected Alternatives: calling the atlas verified because generation/tests/py_compile pass, or fabricating missing Dynamic Decals/RealtimeCSG/Habitat paths.
Scalability potential: missing payload/source references do not become green evidence on any device tier.
Hardware Impact: 0 us frame-time impact.

# Rationale DOC_ROOT_ARCH_AUDIT
Date: 2026-05-28
Status: DONE

Problem: Root and architecture documents are the long-lived project brain; if stale, new agents will follow wrong routes and contaminate global authority.
Solution: Audit stable docs against current filesystem/source facts, then patch only stable root/architecture docs that agents need for onboarding and execution.
Rejected Alternatives: Reading archived batch reports as current authority; editing `AGENTS.md`; broad narrative rewrite without source checks.
Scalability potential: Low/Middle agents consume fewer wrong files; High/Ultra can still drill into detailed route cards and source artifacts.
Hardware Impact: Runtime/game impact 0 us. This is documentation and static source inspection only.

Problem: User asked for depth, but HECTON docs forbid fluff and false runtime claims.
Solution: Separate source-proven facts from pending runtime proof; mark Unity/player/profiler readiness as PENDING unless current artifacts exist.
Rejected Alternatives: Declaring project ready from docs, static grep, or archived logs.
Scalability potential: Stable evidence classes prevent overclaiming across all hardware tiers.
Hardware Impact: Runtime/game impact 0 us.

Problem: Root docs lacked one compact current topology map. Agents had to infer Unity version, active scene route, DataMonolith file state, asmdef scale, and source owners from scattered docs.
Solution: Added `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` as a static source/filesystem map and linked it from root and architecture indexes.
Rejected Alternatives: Bloated `Docs/README.md` with full topology; edited `AGENTS.md`; used dated batch reports as current doctrine.
Scalability potential: Low/Middle agents can enter through one source-backed map; High/Ultra agents can drill into route cards and source without stale route assumptions.
Hardware Impact: Runtime/game impact 0 us.

Problem: Architecture read order hid current route-critical docs behind generic contract lists.
Solution: Promoted topology, boot sequence, dispatch pipeline, First 20 Minutes route, and platform proof ladder into the first-read sequence.
Rejected Alternatives: Leaving route docs discoverable only by filename search; creating a separate task-board index.
Scalability potential: All agents converge on the Copper Wire V0 route before adding breadth; overkill work stays tied to route proof and quality scalar policy.
Hardware Impact: Runtime/game impact 0 us.

Problem: Platform ladder repeated a historical DataMonolith missing fact after the payload appeared on disk.
Solution: Marked the old HFI R21 map historical and recorded the 2026-05-28 static payload presence while keeping runtime readiness pending.
Rejected Alternatives: Calling DataMonolith ready from file presence; deleting the historical map.
Scalability potential: Prevents false platform blockade from stale file presence while still blocking readiness claims without import/boot/checksum/player proof.
Hardware Impact: Runtime/game impact 0 us.

Problem: Root `PROJECT_ATLAS.md` preserved `observedAssemblyCount = 83` while the current first-party asmdef count is `167`, making the root doc look stale to new agents.
Solution: Kept the legacy token for tool compatibility but explicitly marked it as a compatibility token and routed current counts to topology/generated dependency graph.
Rejected Alternatives: Replacing the token and risking validator/tool breakage; leaving it unexplained.
Scalability potential: Low/Middle agents avoid false assembly-count assumptions; High/Ultra agents can use regenerated graph artifacts for full source pressure.
Hardware Impact: Runtime/game impact 0 us.

Problem: Generated dependency graph snapshot was from 2026-05-26, while root/architecture docs now cite 2026-05-28 source topology.
Solution: Regenerated `Docs/Generated/DEPENDENCY_GRAPH.md/json/cache.json` with `BuildArchitectureAtlas.py` and verified `AtlasCheck` pass.
Rejected Alternatives: Treating stale generated artifacts as current; pasting generated graph into root docs.
Scalability potential: Static dependency pressure is current without bloating first-read docs.
Hardware Impact: Runtime/game impact 0 us.

Problem: `Docs/Generated/README.md` listed `PROJECT_ATLAS_HPHI.md`, but the file is absent.
Solution: Marked it optional/currently absent and recorded that `HectonPhiStaticAudit.py --no-fail` timed out after 300 seconds before producing proof.
Rejected Alternatives: Creating a fake placeholder; claiming H-Phi proof from a timed-out run.
Scalability potential: Agents do not chase or cite a missing artifact; H-Phi regeneration remains a known blocked verification lane.
Hardware Impact: Runtime/game impact 0 us.

Problem: The project had a domain roster and many route cards, but no compact map from the 85 domains to the architecture documents an assigned agent must read.
Solution: Added `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` with nine echelon rows, active architecture docs, source anchors, and default proof gaps.
Rejected Alternatives: 85-row copy of the domain roster; leaving agents to filename-grep `Docs/ARCHITECTURE`.
Scalability potential: Low/Middle agents read the minimum relevant contracts; High/Ultra agents still have source and route-card depth without root-doc bloat.
Hardware Impact: Runtime/game impact 0 us.

Problem: `BuildArchitectureAtlas.py` regenerated markdown without UTF-8 BOM, which made `VerifyDocStructure.py` fail immediately after a valid graph regeneration.
Solution: Patched the generator's markdown writes to `utf-8-sig` and reran graph generation.
Rejected Alternatives: Manually fixing generated markdown after every run; ignoring a repeatable doc-tool failure.
Scalability potential: Future doc agents can regenerate the graph without breaking the structure gate.
Hardware Impact: Runtime/game impact 0 us.

Problem: The new domain coverage matrix could become another false map if listed docs or source anchors did not exist.
Solution: Parsed every backticked architecture doc and `Assets/_Project/Scripts` anchor from the matrix and checked filesystem existence; all listed paths exist.
Rejected Alternatives: Treating a hand-written matrix as proof by assertion.
Scalability potential: Any agent can trust the matrix as a starting route without losing time on missing files.
Hardware Impact: Runtime/game impact 0 us.

Problem: Root dependency stub still listed `PROJECT_ATLAS_HPHI.md` as a normal generated path although that artifact is absent.
Solution: Changed the root dependency graph contract to mark H-Phi atlas as optional and absent unless `HectonPhiStaticAudit.py` completes.
Rejected Alternatives: Leaving a missing path in a normal output list; fabricating an empty H-Phi artifact.
Scalability potential: Prevents toolchain agents from citing absent H-Phi proof or chasing a false required output.
Hardware Impact: Runtime/game impact 0 us.

Problem: Domain roster and atlas still described black-box telemetry as 32-byte `.h8dump` output, while the active mandate and current source use fixed-size last-300-frame rings with `Dump_*.bin` as primary in many systems.
Solution: Updated stable domain text to the current rule: fixed-size last-300-frame black-box rings; primary bounded dumps under `Docs/AgentLogs/Dump_*.bin`; `.h8dump` only where current source retains it as a legacy mirror.
Rejected Alternatives: Erasing `.h8dump` completely despite retained source mirrors; leaving `.h8dump` as the generic doctrine.
Scalability potential: All tiers get bounded fault evidence without teaching new agents an obsolete dump route.
Hardware Impact: Runtime/game impact 0 us.

Problem: Current static scene facts and route docs include `01_ORBIT`, while `AGENTS.md` still contains older no-orbit handoff wording.
Solution: Marked the conflict explicitly in stable docs instead of editing `AGENTS.md` or pretending the static route is settled runtime doctrine.
Rejected Alternatives: Changing `AGENTS.md` without explicit authorization; deleting `01_ORBIT` from docs despite BuildSettings; claiming Play Mode route proof from file state.
Scalability potential: Low/Middle agents avoid following an unacknowledged authority split; High/Ultra route work has a clear integrator decision point.
Hardware Impact: Runtime/game impact 0 us.

Problem: Active architecture docs still pointed at absent source paths, absent AgentLogs proof files, and absent dated reports.
Solution: Corrected current source anchors where a replacement exists, and downgraded absent paths to historical/planned targets where no current file exists.
Rejected Alternatives: Creating fake placeholder proof files; removing route-card context wholesale; leaving absent files as proof anchors.
Scalability potential: Agents spend less time chasing dead paths and avoid promoting historical reports into current contracts.
Hardware Impact: Runtime/game impact 0 us.

Problem: H8BIN/vocal docs repeated old `static_data.h8bin` missing language after the payload appeared on disk.
Solution: Ran a scoped Python h8bin validator pass over current StreamingAssets and Data Monolith source roots, then updated docs to cite that payload/schema proof with strict non-claims.
Rejected Alternatives: Running full default validator through broad source roots under the 10s tool watchdog; claiming Unity boot or audio proof from Python validation.
Scalability potential: Binary payload source truth is less ambiguous for all tiers while runtime readiness remains blocked until player/boot/profiler proof.
Hardware Impact: Runtime/game impact 0 us. Python validator processed 1.0495 MB in 0.491846 s outside the game.

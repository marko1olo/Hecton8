# DOCS_ACTUALIZATION Rationale

Problem: Reports/logs/status files are large and noisy; stable docs needed an evidence boundary without turning process folders into the project brain.
Solution: Distill durable facts into `Docs/PROJECT_BASELINE.md` and patch stable entry documents with non-claim boundaries. Do not archive or delete live report artifacts while current evidence still cites them.
Rejected Alternatives: Bulk moving reports would break active evidence paths. Keeping a maintained process-folder index was rejected by user correction. Running compile is explicitly out of scope per user.
Scalability potential: Low/MX350 work benefits indirectly by keeping ownership/report evidence discoverable so future fixes target real hot-path and native-memory debts instead of stale claims. Middle/High/Ultra benefit only after real runtime proof; no runtime improvement claimed.
Hardware Impact: Documentation pass saves 0 us/frame. Expected engineering impact is lower search overhead and lower risk of agents citing stale report snapshots.

Problem: User rejected transient folder indexing as noise because `Docs/Reports`, `Docs/Tasks`, and `Docs/AgentLogs` should eventually archive/deprecate rather than become maintained surfaces.
Solution: Removed active transient indexes and replaced the current-note file with `Docs/PROJECT_BASELINE.md`, a baseline focused on authority, proof boundary, native-memory state, and stable system facts.
Rejected Alternatives: Keeping `Docs/Tasks/README.md`, `Docs/AgentLogs/README.md`, or `Docs/ACTIVE_WORKSPACE_INDEX.md` would preserve process-folder clutter. Bulk-moving live agent artifacts now would break active citations.
Scalability potential: Distilled facts reduce future search cost and reduce stale-proof propagation. This has no runtime scalability effect.
Hardware Impact: Runtime gain remains 0 us/frame. Documentation gain is operational only.

Problem: Active docs still exposed local token telemetry, old FAQ prose, glossary prose, a binary marketing archive, and long report/concision artifact chains as if they were base navigation.
Solution: Moved root noise and token audit report/json to `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/`, added a manifest, and replaced active references with `Docs/PROJECT_BASELINE.md` plus category-level report boundaries.
Rejected Alternatives: Keeping token telemetry, `TECHNICAL_FAQ.md`, `H8_GLOSSARY.md`, or `Marketing.rar` in active docs would keep stale/local/process material in the main read path. Enumerating every old scanner artifact in README/ledger was also rejected as bureaucracy.
Scalability potential: No runtime scalability effect. Engineering scalability improves only by reducing stale navigation and forcing durable facts into contracts.
Hardware Impact: Runtime gain 0 us/frame. Search/read overhead reduced for humans and agents; no frame-time claim.

Problem: `Docs/CURRENT_ENGINEERING_DISTILLATE.md` put current-work framing into `Docs` root, where the user wants base documentation only.
Solution: Replaced it with `Docs/PROJECT_BASELINE.md` and removed active references to current batch/agent framing.
Rejected Alternatives: Keeping a current-note file in root or renaming without rewriting content.
Scalability potential: No runtime effect. Documentation base is less volatile.
Hardware Impact: Runtime gain 0 us/frame.

Problem: `Docs/AI_Fauna`, `Docs/Flora_Pipeline`, and `Docs/Scatter_Runtime` were legacy planning bundles with stale dated report-chain read order, not base documentation.
Solution: Moved the three folders to `Docs/DEPRECATED/Legacy_Domain_Bundles_2026-05-26/` with a manifest. A code-adjacent flora README that pointed to the old flora bundle was relinked to `Docs/PROJECT_BASELINE.md` and `Docs/ARCHITECTURE/FLOODED_TERRESTRIAL_GEOGRAPHY.md`.
Rejected Alternatives: Rewriting every stale May report pointer in place would preserve dead planning folders as active surface. Adding redirect stubs would keep noise in the read path.
Scalability potential: No runtime effect. Documentation navigation loses obsolete branch points.
Hardware Impact: Runtime gain 0 us/frame.

Problem: `Docs/README.md` still embedded historical X_012/APEX pass logs, which made the root index read like a change diary instead of base navigation.
Solution: Removed the historical pass sections from `Docs/README.md`; history remains in the actuality ledger and archives.
Rejected Alternatives: Keeping detailed pass history in the root index or creating another summary file.
Scalability potential: No runtime effect. Root documentation is shorter and less volatile.
Hardware Impact: Runtime gain 0 us/frame.

Problem: Root/base docs still carried current proof slices and native-memory counters, making the base layer read like a live status note.
Solution: Removed volatile proof/native/build data from root/index/governance docs. Moved the concise native-memory and compile snapshots into `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
Rejected Alternatives: Keeping `BUILD_UNKNOWN_*`, native ledger counters, and guard-block notes in root docs. That repeats report state and violates the root-as-base boundary.
Scalability potential: No runtime effect. Durable doctrine and volatile proof now have separate owners.
Hardware Impact: Runtime gain 0 us/frame.

Problem: runtime master plan, global architecture map, quality gates, and systems contracts had become dated correction diaries.
Solution: Rewrote them as concise contracts: runtime execution order, global authority map, quality gates, and systems contracts. Removed old DOC_GLOBAL chains and stale build-status prose.
Rejected Alternatives: Leaving historical R-chain text in active base docs or creating another summary file that readers must reconcile.
Scalability potential: No runtime effect. Documentation read path now favors owner routes, native-memory rules, proof gates, and continuous quality scaling.
Hardware Impact: Runtime gain 0 us/frame.

Problem: active generated/reference docs still carried repeated DOC_GLOBAL R51/Rxx banner residue and one generated atlas treated `CURRENT_BATCH` as source authority.
Solution: Replaced repeated banners with short authority boundaries in atlas/handbook/procedural/tech-art docs; removed `CURRENT_BATCH` from dependency graph authority inputs.
Rejected Alternatives: Leaving stale history in every document header. Full regeneration was rejected because the user requested documentation actualization, not compile/tool churn.
Scalability potential: No runtime effect. Reduces probability of future work citing prompt/status residue as doctrine.
Hardware Impact: Runtime gain 0 us/frame.

Problem: root still contained oversized generated/reference bodies and domain doctrine that made `Docs/` root read like a current-state dump instead of base documentation.
Solution: Archived full snapshots to `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/`. Replaced `ARCHITECT_HANDBOOK.md` and `DEPENDENCY_GRAPH.md` with short tool-entry contracts. Compressed `PROJECT_ATLAS.md` to required validator tokens plus the 85-domain table. Moved tech-art PBR and procedural vertical-world doctrine into `Docs/ARCHITECTURE`.
Rejected Alternatives: Deleting tool-required paths would break local validators. Keeping full generated handbook/graph bodies in root would preserve the exact clutter the user rejected.
Scalability potential: Runtime gain remains 0. Documentation scalability improves because root now carries stable owner routes and tool contracts, while generated detail has a single deprecated provenance path.
Hardware Impact: Runtime gain 0 us/frame.

Problem: active Design, Modding, Marketing, Lore, and route-card docs still carried DOC_GLOBAL/Rxx boilerplate and stale correction labels.
Solution: Replaced 64 DOC_GLOBAL guard blocks with one short authority boundary, normalized route-card R48 headings to stable route-field contract language, and removed stale DOC_GLOBAL wording from active procedural/UI docs.
Rejected Alternatives: Leaving dated correction chains in important folders would keep old process residue as active doctrine. Moving whole Design/Marketing/Modding folders to deprecated was rejected because many files remain useful static contracts or operating docs.
Scalability potential: No runtime effect. Reduces stale-proof propagation across active docs and keeps important folders readable.
Hardware Impact: Runtime gain 0 us/frame.

Problem: The dependency-graph generator would recreate the removed DOC_GLOBAL/R51 boilerplate on the next run.
Solution: Updated `Tools/BuildArchitectureAtlas.py` to emit a short authority boundary and neutral AtlasCheck wording. Updated `Tools/test_architecture_atlas.py` to validate the root dependency graph as a stub instead of expecting old generated sections.
Rejected Alternatives: Cleaning only generated markdown would be unstable because a later tool run would reintroduce stale correction chains.
Scalability potential: No runtime effect. Documentation generation now preserves the root-as-base boundary.
Hardware Impact: Runtime gain 0 us/frame.

Problem: `Docs` root still held generated dependency-graph JSON/cache and CSV authoring profiles, which are data/artifacts rather than base documentation.
Solution: Moved graph JSON/cache to `Docs/Generated` and CSV profiles to `Docs/Data/Profiles`. Updated graph generators/validators, source/editor path strings that read the profiles, and the OOP doc scanner stale-marker literals.
Rejected Alternatives: Keeping data artifacts in root would violate the root-as-base boundary. Moving files without updating tools/source paths would create broken references. Leaving raw stale-marker strings in the scanner made active residue scans noisy.
Scalability potential: No runtime effect from the documentation move. Editor/data path ownership is clearer and root navigation is smaller.
Hardware Impact: Runtime gain 0 us/frame.

Problem: `Tools/HectonPhiStaticAudit.py` could still overwrite root `Docs/PROJECT_ATLAS.md` with a generated H-Phi report, and `Tools/AtlasCheck.py` could accept a placeholder generated graph as if it were a real artifact.
Solution: Redirected the H-Phi generated atlas to `Docs/Generated/PROJECT_ATLAS_HPHI.md`, left root `PROJECT_ATLAS.md` as the stable validator contract, and added a placeholder guard to `AtlasCheck`.
Rejected Alternatives: Relying on operators to remember `--no-atlas` was rejected. Keeping stale generated JSON/cache active under `Docs/Generated` was rejected because stale artifacts should not masquerade as current evidence.
Scalability potential: No runtime effect. Tooling now preserves the root-as-base boundary and avoids false architecture-validation greens.
Hardware Impact: Runtime gain 0 us/frame.

Problem: Profile CSVs had named columns but no explicit schema version/hash metadata, which violates the CSV bridge mandate and makes column drift hard to review.
Solution: Added parser-safe `#` schema metadata to ambient lighting, flora sway, water extinction, and water optics profile CSVs. Removed BOMs so the first byte is `#`; existing parsers skip metadata without code changes.
Rejected Alternatives: Adding schema columns would change parser contracts. Adding parser enforcement now would touch runtime/editor code beyond the documentation/data pass and needs compile/Unity validation.
Scalability potential: Low/MX350 benefits indirectly through safer tuning data review; middle/high/ultra can carry richer authoring profiles without column ambiguity. Runtime behavior is unchanged.
Hardware Impact: Runtime gain 0 us/frame.

Problem: `Tools/BuildArchitectureAtlas.py` still treated process artifacts as architecture inputs: it scanned active agent logs for SHERST/PHI sections, listed current batch/log files as source authority, and could emit broken references when VRAM audit evidence was absent.
Solution: Removed agent-log and batch-file scans from atlas data collection. Replaced PHI/SHERST sections with a code-only selected signal route snapshot. Source authority now points to stable docs/contracts plus generated outputs and tools. Missing VRAM audit is rendered as absent evidence without a fake file reference. Regenerated `Docs/Generated/DEPENDENCY_GRAPH.md/json/cache.json` and validated the markdown with AtlasCheck.
Rejected Alternatives: Keeping agent logs as architecture authority was rejected because process folders are volatile evidence, not base documentation. Leaving the placeholder graph was rejected after the generator was fixed because it would keep stale generated evidence active. Moving generated graph bodies back to root was rejected because root must remain stable base/tool contract.
Scalability potential: No runtime effect. Documentation/tool scalability improves because future atlas regeneration cannot pull mutable agent-process noise into the project contract. Low/Middle/High/Ultra runtime quality is unchanged; route evidence is static source only.
Hardware Impact: Runtime gain 0 us/frame. Tooling gain is false-reference removal and lower risk of stale-process citations.

Problem: Active authority docs still mixed stable contracts with process diaries: future seams tracked batch/status/log ownership, modding reservations exposed SHINOBU process slots, global authority docs carried old loop/build history, and the Data Monolith spec used `VERIFIED` wording from static file/header evidence.
Solution: Rewrote the future seam architecture doc as a stable dormant-seam contract, updated the modding reservation cross-reference and owner column to domain owners, stripped compile-loop diary text from global authority boundary/model docs, and downgraded Data Monolith status to static file/header parse with runtime proof pending.
Rejected Alternatives: Moving the future seam path was rejected because modding docs link to it. Keeping old loop/build facts in authority docs was rejected because logs/reports own process evidence. Claiming Data Monolith verification from blob presence was rejected because runtime load/profiler/player evidence is absent.
Scalability potential: No runtime effect. Architecture scalability improves because global-route and modding contracts now state durable owner/proof rules instead of mutable agent/process evidence. Low/Middle/High/Ultra behavior is unchanged until runtime owners implement and prove the reserved surfaces.
Hardware Impact: Runtime gain 0 us/frame. Expected gain is reduced false authority and lower risk of API/global-route expansion from stale documentation.

Problem: The active Outpost fail-safe mission contract still treated mutable batch-prompt drift as source authority. The prose and JSON pointed at current task files, and the Python/Editor validators enforced `ACTIVE_BATCH_*` status instead of a stable mission contract.
Solution: Promoted the Outpost handoff to `STATIC_MISSION_CONTRACT`, made the prose document and JSON handoff the contract source, removed live batch reads from both validators, and retargeted gas checks to the current `HectonSurvivalContract` owner plus `GasDynamicsSolver` references. Updated the Babel manifest hash for the changed JSON.
Rejected Alternatives: Keeping batch drift as a validator state was rejected because task prompts are process evidence, not design authority. Mutating runtime localization tables was rejected because the handoff explicitly requires a coordinated localization bake.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Design scalability improves because Ghost Power and gas/fallback rules remain stable mission facts independent of whichever batch prompt is active.
Hardware Impact: Runtime gain 0 us/frame. Editor/static validation avoids a false fail/pass caused by current task-file churn.

Problem: Active Design docs still cited current-batch prompt extraction as proof material in stable runbooks.
Solution: Removed the current-batch proof requirement from the hardware adaptive UI runbook and replaced the save header prompt-extraction bullet with a stable local anti-bloat evidence statement.
Rejected Alternatives: Editing archived reports was rejected; archives are historical evidence. Rewriting UX/AI/Taxonomy toolchains in this pass was rejected because those pipelines have cross-domain generated reports/tests and need their own validation pass.
Scalability potential: No runtime effect. Low/Middle/High/Ultra documentation consumers get a cleaner authority boundary: stable specs and source files own policy, generated reports may carry legacy provenance only as audit metadata.
Hardware Impact: Runtime gain 0 us/frame.

Problem: Active documentation still contained obvious process/output residue: `README_SHINOBU_269`, old marketing verification sprint batches, SHINOBU art-drop PNGs, stale economy runtime-binding claims, and prompt/lane owner labels in active docs.
Solution: Moved clear process/output artifacts to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/`, created a stable AI texturing template README, patched marketing workflows to treat old verification batches as archived reference only, corrected economy binding paths to `Data/Economy`, and normalized active owner labels to domain ownership.
Rejected Alternatives: Moving Modding audit matrices was rejected because `Docs/Modding/Validate_Mod_API_Static.ps1` and `Docs/Modding/Signal_Schema.json` still bind those files into a live static contract. Moving raw marketing ledgers or CRM inputs was rejected because active workflows still depend on them. Editing Reports/Tasks/AgentLogs was rejected unless needed for this agent's own status/rationale/log.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Documentation scalability improves because active folders now expose contracts/workflows instead of prompt IDs and dropped output packs; future cleanup has a manifest trail rather than broken active links.
Hardware Impact: Runtime gain 0 us/frame. Engineering gain is lower stale-path and stale-owner lookup cost; no frame-time claim.

Problem: `Docs/ARCHITECTURE` still contained unindexed SHINOBU-labeled process notes beside stronger route cards or active stable contracts. Marketing daily validation still hard-coded one agent rationale file and stale file-count expectation.
Solution: Moved 14 non-route/stub/duplicate architecture notes into `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Architecture/Unindexed_Process_Notes/`; kept formal route cards and indexed architecture contracts active. Patched the Deep Sea Noir route card to point at the archived implementation note. Changed marketing rationale audit to a domain-neutral optional rationale file and corrected the expected Marketing file count to 90 after the verification-batch archive.
Rejected Alternatives: Moving every SHINOBU route card was rejected because route cards are still the strongest available global-authority contracts until their facts are distilled into domain-owned stable docs. Moving Audio CSVs was rejected because `VO_SHINOBU_MOCK` is referenced by runtime code and changing it is not documentation-only. Moving raw marketing CRM/lead CSVs was rejected because active workflows still use them as gated data sources.
Scalability potential: Runtime behavior is unchanged across Low/Middle/High/Ultra. Documentation scalability improves because active architecture now loses a layer of stale process-note clutter while route-card proof boundaries remain intact.
Hardware Impact: Runtime gain 0 us/frame. Search/review cost is reduced; no performance claim.

Problem: `Docs/ARCHITECTURE/SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD.md` was still active even though its own status says `HISTORICAL` and `SUPERSEDED FOR LIVE SHOCKWAVE NAN ROUTE BY SHINOBU_248`.
Solution: Moved SHINOBU_156 to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Architecture/Superseded_Route_Cards/`, added manifest provenance, and patched `SHINOBU_248_SHOCKWAVE_NAN_ROUTE_CARD.md` to point to the archived historical route card.
Rejected Alternatives: Keeping SHINOBU_156 active would leave two route cards around the same buffer range and make current ownership ambiguous. Moving all SHINOBU route cards was rejected because most remaining cards are active/static-source contracts without named replacements.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Documentation scalability improves by keeping one live shockwave route owner and one archived historical context file.
Hardware Impact: Runtime gain 0 us/frame. Search/review cost decreases; no frame-time or GC claim.

Problem: `Docs/Marketing/CreatorOutreach/PRIORITY_250_PITCH_SHEET_FROM_RAW.md` was a 128 KB parked raw public-index pitch sheet in the active CreatorOutreach folder. Its own rules said `not outreach-ready` and `Keep this file parked while the active gate is asset proof`.
Solution: Moved the file to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/CreatorOutreach/`, updated the marketing README, source ledger, backlog, deprecation manifest, and daily validation file-count gate.
Rejected Alternatives: Moving `PRIORITY_50_MESSAGE_DRAFTS_FROM_RAW.md` was rejected because the generator, CRM rows, and active gate history still reference it as a hand-curated draft bank. Moving raw CRM/lead CSVs was rejected because active workflows and validation still consume them.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Marketing documentation scalability improves because active CreatorOutreach keeps current gated workflows rather than parked raw bulk sheets.
Hardware Impact: Runtime gain 0 us/frame. Search/review cost decreases; no frame-time, memory, outreach, or conversion claim.

Problem: `Docs/Marketing/CreatorOutreach/RAW_LEAD_EXPANSION_QUEUE.md` was a parked public seed list in active CreatorOutreach. Its own status said `public seed list / not outreach-ready`, and the active daily loop says not to expand raw leads unless a human explicitly asks for a source-backed lead sprint.
Solution: Moved the file to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/CreatorOutreach/`, updated README/source-ledger/backlog references to the archived path, changed the Marketing validation count gate to `88`, and recorded the move in the deprecation manifest and actuality ledger.
Rejected Alternatives: Keeping the file active would make raw public-index seeds look like an operating input. Creating an active redirect stub would preserve clutter. Moving live CRM/template CSVs or the Priority 50 draft bank was rejected because current validation, generator history, and workflows still consume those active surfaces.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Documentation scalability improves because active CreatorOutreach no longer exposes parked seed volume as a current route; future lead expansion must be a bounded, source-backed sprint after asset-gap proof.
Hardware Impact: Runtime gain 0 us/frame. Search/review cost decreases; no frame-time, memory, outreach, or conversion claim.

Problem: `Docs/Marketing/CreatorOutreach/ADJACENT_SURVIVAL_CREATOR_LEADS.md` and `Docs/Marketing/Regional/REGIONAL_CREATOR_LEADS.md` were raw public prospecting sheets in active Marketing. Both files explicitly said `raw public prospecting list / not outreach-ready`, while active operations say raw lead work needs a source-backed sprint and asset/route gap.
Solution: Moved both files to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/`, removed them from the active README directory map, changed source-ledger/backlog references to archived paths, and rewrote P1 regional/adjacent tasks so future verification starts from a fresh bounded sprint rather than copying old seed rows.
Rejected Alternatives: Moving active regional outreach/localization plans was rejected because those files define current gates and reviewed-copy requirements. Moving live CRM/template CSVs, curated creator database, or Priority 50 draft bank was rejected because current validation and workflows still consume those active surfaces.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Documentation scalability improves because active Marketing now keeps workflows/gates and removes raw lead-volume sheets that looked like immediate work queues.
Hardware Impact: Runtime gain 0 us/frame. Search/review cost decreases; no frame-time, memory, outreach, or conversion claim.

Problem: `Docs/Marketing/Data/RAW_LEAD_SCRAPE_SUMMARY_2026-05-18.md` was a dated human-readable scrape result sitting beside active CSV schemas/data. The scraper would also recreate a summary in active `Data` on a future forced run.
Solution: Moved the summary to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/Data/`, updated active references to archived evidence, and changed `scrape_letsplayindex_public_leads.ps1` so future summaries write to configurable `SummaryDir` outside active `Data`.
Rejected Alternatives: Moving raw CSVs was rejected because the raw lead dictionary, CRM enrichment history, and verification workflow still consume those data surfaces. Leaving the script unchanged was rejected because it would reintroduce the same active-doc clutter.
Scalability potential: Runtime Low/Middle/High/Ultra behavior is unchanged. Documentation/tool scalability improves because active `Data` carries schema/data contracts while narrative scrape proof moves to archive/report surfaces.
Hardware Impact: Runtime gain 0 us/frame. Search/review cost decreases; no frame-time, memory, outreach, or conversion claim.

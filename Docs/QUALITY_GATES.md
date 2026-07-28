# QUALITY_GATES.md

Date: 2026-05-26
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC

Purpose: acceptance gates. This file defines what proof is required; it is not proof that anything passed.

## Authority Boundary

- Read `Docs/PROJECT_BASELINE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/README.md`, and current source before using these gates.
- Root `quality.md` owns cross-domain proof language and screenshot/profiler review doctrine. This file owns executable hard gates and acceptance checklists.
- Current proof snapshots live in `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Dated reports and archives are historical evidence only.
- A filled checklist is not proof without a current artifact path.

## Universal Proof Rules

- Static source proof does not prove runtime behavior.
- Compile proof does not prove Unity import, Console, Play Mode, profiler, GC, player build, save/load, shader, visual, or platform readiness.
- Runtime claims require artifact paths with command or capture context, timestamp, target, exit/result, and blocker class.
- Do not report `VERIFIED`, `COMPLETE`, or `PRODUCTION READY` from stale logs.
- Proof state labels come from root `quality.md`. Evidence classes name the backing artifact type. Do not treat `STATIC_DOC`, `STATIC_SOURCE`, or `CLI_COMPILE` as a higher proof state than their artifact permits.

## Build Gate Protocol

| Gate | Rule |
|---|---|
| command | record exact build command, target, timestamp, exit code, warning/error count |
| shared servers | use non-shared compiler/server settings when required by task policy |
| queue discipline | one active compile owner per target; no parallel full builds |
| failure class | distinguish C# diagnostics from SDK/restore/environment failures |
| forbidden | claiming compile success from stale logs |

## Runtime Evidence Gates

| Gate | Required evidence | Blocks readiness claim |
|---|---|---|
| Unity import / Console | Unity log or Console export | Yes |
| Play Mode or player | run/capture log | Yes |
| Profiler | frame-time capture with target scene/load | Yes |
| GC | GC Alloc / GCMonitor / Memory Profiler artifact | Yes |
| Memory | Memory Profiler or platform memory capture | Yes |
| Rendering | Frame Debugger, RenderDoc, screenshot, GPU timing, or shader import proof | Yes |
| Save/load | write/read/migration/checksum failure artifacts | Yes |
| Platform | build artifact plus device/runtime proof for target platform | Yes |

## Native Memory and DataVault Gate

| Gate | Command / proof | Blocks merge |
|---|---|---|
| no new direct native allocation debt | `Tools\DataVaultSovereigntyAudit.py --fail-on-regression` or current successor | Yes |
| no new runtime native field debt | runtime/native ownership ledger or successor scanner | Yes |
| no duplicate central `BufferID` values | `Tools\BufferIDSovereigntyAudit.py --fail-on-duplicates` | Yes |
| no local numeric `BufferID` casts at final migration | `Tools\BufferIDSovereigntyAudit.py --fail-on-local-casts` | Yes when declared complete |
| native owner proof | owner, allocator, lifetime, disposal, phase, failure mode | Yes |
| DataVault write-lock/fence proof | same-phase `try/finally ReleaseWriteLock` or equivalent scoped-dispose proof; no lock held across frame/await/worker/UI lifetime; job fence named | Yes |

Rules:

- `H8Memory.cs` and `GlobalDataVault.cs` are the approved central native ownership surfaces.
- Private persistent native collection fields elsewhere are migration debt unless proven owner-local scratch.
- `MonoBehaviour` native fields need explicit disposal and lifecycle proof; otherwise they are debt.
- The zero-debt gate is expected to fail until remaining migrations complete. New regressions are not accepted.

## Job and Burst Gate

| Gate | Required state | Blocks merge |
|---|---|---|
| hot path GC | `0 B/frame` by profiler/GC artifact | Yes |
| same-frame schedule/readback | profiler proof or removal | Yes |
| hidden `.Complete()` | owner-dispatcher completion window or proof | Yes |
| Burst payload | unmanaged, no managed references, no captured lambdas | Yes |
| Unity object access from jobs | absent | Yes |
| deterministic math | no gameplay-authority `UnityEngine.Random` or wall-clock time | Yes |

## Global Authority Gate

| Gate | Rule | Blocks merge |
|---|---|---|
| registry | no new hot `GlobalRegistry.Get<T>` / `TryGet<T>` polling | Yes |
| first-party hot events | no new hot `HectonEventBus` traffic | Yes |
| SignalBus | new routes use typed `SignalBus<T>` with owner/capacity/overflow/telemetry | Yes |
| GlobalSignals | no new direct queue surface unless explicit bridge migration | Yes |
| DataVault | no new global native buffer without owner/lifetime route | Yes |
| route card | new or changed global route has a complete route card | Yes |
| review | route review disposition is `GREEN` | Yes |

## Performance Gate

Compact 2GB-VRAM / 8GB-RAM class hardware is the minimum supported proof lane.

Root `AGENTS.md` guardrails are the target standard: main thread `<= 12ms`, GC `0 B/frame`, SetPass `<= 600`, batches `<= 1800`, total memory `<= 4096MB`, compact VRAM hard ceiling `<= 1800MB`, texture memory `<= 900MB`, and render targets plus depth `<= 320MB`. Any temporary emergency threshold below must be named as a blocker ceiling, not a success target.

| Metric | Limit | Blocks merge | Evidence |
|---|---:|---|---|
| Total frame time | `<= 16.67ms p95` | Yes | 60s player/profiler capture |
| Main thread | `<= 12.0ms p95` | Yes | Unity Profiler |
| Single runtime system | `<= 0.1ms` unless cold/amortized | Yes | Profiler marker proof |
| Gameplay physics total | `<= 2.0ms p95` planning gate; `<= 5.0ms` spike ceiling | Yes above spike ceiling | Profiler/FixedStep capture |
| GC hot path | `0 B/frame` | Yes | GC Alloc / GCMonitor |
| VRAM guard | `<= 1.6GB` | Risk marker | Memory Profiler / platform counter |
| VRAM hard ceiling | `<= 1.8GB` | Yes | Memory Profiler / platform counter |
| Texture memory | `<= 900MB` | Yes | Memory Profiler |
| Render targets + depth | `<= 320MB` | Yes | Memory Profiler / RenderDoc |
| SetPass target | `<= 600` | Yes above emergency ceiling; otherwise route risk | Frame Debugger / Stats |
| SetPass emergency ceiling | `<= 800` hard blocker ceiling | Yes | Frame Debugger / Stats |
| Batches target | `<= 1800` | Yes above target without owner route and profiler proof | Frame Debugger / Stats |
| Total memory target | `<= 4096MB` | Yes above target without memory owner proof | Memory Profiler / platform counter |
| Native persistent memory | flat over 10 min idle | Yes | NativeMemorySentinel / Memory Profiler |

Required scene-load memory proof:

- after boot or scene load and before gameplay release, record Texture Memory and Total Reserved Memory;
- when rendering/streaming participated, also record render target/depth budget and loaded handle count where available;
- `used/total > 0.90` on an owned memory/residency budget triggers mip/residency downgrade or equivalent load-shed response and blocks a healthy-memory claim until fresh evidence shows recovery.

Load shed:

- VRAM above guard: request mip downgrade, drain release queue, reduce non-primary render targets.
- Frame time above `25ms` for 3 frames: lower quality weight target and reduce raymarch/post/boid/rigidbody budgets by owner order.
- Physics p95 above `2.0ms`: reduce solver scope or replace noncritical work with a premium presentation approximation.
- GC hot path above `0 B`: block until allocation source is removed.

## Asset Validation Gate

Run only after the production prefab, material/shader, and active scatter/profile route exist.

Geometry and LOD:

- Poly count within category budget.
- LOD Group has at least 3 levels.
- LOD thresholds target `0.6 / 0.15 / 0.04`.
- No missing mesh references.
- No noncritical dynamic Rigidbody on static props.

Shader and texture:

- Shader compiles without errors.
- GPU instancing enabled where applicable.
- Texture samples within budget.
- Runtime quality input is continuous (`GlobalQualityWeight`, material scalar, or source-required ABI).
- Albedo/mask/normal formats and mip settings match platform plan.

Scatter:

- Density within tile budget.
- Floor offset and clearance validated.
- No overlap with active base modules.
- Yaw randomization and placement constraints validated.
- No floating instances.

## First 20 Minutes Product Gate

| Gate | Rule | Blocks merge |
|---|---|---|
| route relevance | every product/runtime/content task names the route moment it improves | Yes |
| route blocker | off-route work names the blocker it removes | Yes |
| proof package | route claims require runtime, profiler, GC, memory, save/load, screenshot/clip proof | Yes |
| breadth control | net-new systems not needed by the route are parked | Yes |
| marketing send | no public send-ready state without real screenshot/clip/demo proof | Yes |

## Visual Reference Parity Gate

Run for player-visible water, terrain, sky, flora, UI, VFX, lighting, camera, materials, surface route, or hero-biome work before accepting a visual result.

| Gate | Command / proof | Blocks visual acceptance |
|---|---|---|
| mandatory reference owner matrix | `python -B Tools/ValidateVisualReferenceOwnerMatrix.py` | Yes |
| current rejection matrix | `python -B Tools/ValidateVisualReferenceCurrentRejectionMatrix.py` | Yes while it reports rejection-only state |
| visual validator self-tests | `python -B Tools/test_validate_visual_reference_owner_matrix.py`; `python -B Tools/test_validate_visual_reference_current_rejection_matrix.py` | Yes after changing visual reference validators |
| repeated shot-list comparison | current screenshot/clip set must repeat the failed route class and compare against mandatory references plus April/previously-in-development internal baseline where present | Yes |

Rules:

- Raw diagnostic MCP screenshots, static reports, and near-identical capture galleries can reject bad visuals only. They cannot accept visual quality.
- If current captures are below the mandatory reference floor or April/previously-in-development baseline on base geometry, material truth, waterline/contact, sky/Aegir/terrain readability, route cues, or compact-tier composition, declare `VISUAL_ROUTE_INVALID`.
- After `VISUAL_ROUTE_INVALID`, the next work must recover or replace the owner stack: scene object, Crest/ocean binding, terrain source, texture/material source, lighting, camera composition, or asset package. Tint, fog, bloom, exposure, grading, and screenshot staging do not count as recovery.
- Acceptance must preserve performance gates: compact-tier beauty is required, not optional.

## AppliedLore Content Gate

Use for in-world prose, codex/wiki/site articles, terminal notes, scanner text, diaries, audio transcript packets, and AppliedContent release sets.

| Gate | Command / proof | Blocks content completion |
|---|---|---|
| Grand Library source quality | `python -B Tools/ValidateGrandLibraryLoreQuality.py --article-glob <glob> --require-status-comment` | Yes for Grand Library article work |
| AI prose firewall | Manual static source review against `writing.md` Anti-AI Prose Ban, LLM Style Suppression Law, Creative Freedom Envelope, Risk Word And Rhythm Firewall, AI Phrase Family Quarantine, Living Prose Floor, Zero-Shot Writer Contract, Few-Shot Rewrite Pattern Bank, Paragraph Evidence Firewall, Manual Redline Protocol, Legacy Corpus Rewrite Law; `localization.md` Multilingual AI-Style Localization Firewall; `Docs/Lore/WriterScenarioAgentPrompt.md` `AI_STYLE_FIREWALL`, `CREATIVE_FREEDOM_ENVELOPE`, `RISK_WORD_AND_RHYTHM_FIREWALL`, `AI_PHRASE_FAMILY_QUARANTINE`, `LIVING_PROSE_FLOOR`, `ZERO_SHOT_CONTRACT`, and `FEW_SHOT_REWRITE LAW`; and `Docs/Lore/LoreCorpusManualRewriteAgentPrompt.md` for old corpus repair/manual rewrite waves; all English authority and generated locale rows in scope must pass. Detector/script output may triage only and cannot accept prose | Yes for in-world prose and AppliedContent source/page work |
| production packet source guard | `python -B Tools/AppliedLoreProductionSourceGuard.py --release-glob <RS*>` | Yes when `.production.md` packet sources are present; packet-JSON releases rely on import/page/coverage gates |
| production source guard self-test | `python -B Tools/AppliedLoreProductionSourceGuard.py --self-test` | Yes after changing the guard |
| Data Monolith import freshness | `python -B Tools/AppliedLoreImporter.py --check` | Yes when packet JSON/import tables are touched |
| runtime route-card export freshness | `python -B Tools/AppliedLoreRouteCardExporter.py --check` | Yes when route cards, packet JSON, or DataMonolith route sources are touched |
| localized page freshness | `python -B Tools/AppliedLorePageExporter.py --packet-glob <P*> --check` | Yes when site/wiki pages are expected |
| packet coverage | `python -B Tools/AppliedLorePacketCoverageAudit.py --packet-id <P*>` | Yes when a packet claims production coverage |
| production packet inventory | `python -B Tools/AppliedLorePacketCoverageAudit.py --inventory` and, for broad packet/import/export changes, `python -B Tools/AppliedLorePacketCoverageAudit.py --all --sample-limit 3` | Yes when a task changes canonical-ready status, packet manifests, importer selection, route-card export, or generated publication indexes |

Rules:

- A lore `CONTENT_ARTIFACT` is not complete if the only output is chat prose, a source brief, route card, outline, validator log, or packet plan.
- Unless the task explicitly says English-only, production content carries all 15 locale rows. Non-English agent-generated rows remain `draft_machine_or_llm` unless native review proof exists.
- AI-style filler is a hard content failure, not an editing preference. Reject abstract category-collapse prose, organic metaphor spam, fake terminal prophecy, scanner poetry, audio trailer lines, authoring notes in player-facing text, and legal/corporate mistranslations before export or localization.
- AI phrase families are rejected by function, not by exact spelling. Replacing "testament" with a synonym, or translating the same museum-label/essay-thesis move into another language, still fails.
- Anti-AI cleanup must preserve living source voice. A rewrite that becomes sterile, neutral documentation without source pressure also fails the writing gate.
- A writer prompt that lacks source/object/pressure/limit, forbidden facts, forbidden style, and surface-specific acceptance is a draft-risk prompt. Strong prose generated from a weak prompt still requires manual redline and may fail completion.
- Few-shot examples must include the bad line, the failure class, and the repaired artifact. A style example that only says "more human" or "more atmospheric" is not a valid content gate.
- If the English authority row cannot pass the firewall, do not translate it. Rewrite the English source from scene, evidence object, speaker, and knowledge boundary first.
- Every old or new locale row in scope must be read manually. Non-English rows copied in English, rows with local AI boilerplate, and rows that add local metaphors or moral interpretation fail even when the packet structure is valid.
- Existing lore files, generated pages, source packets, terminal fragments, scanner text, audio text, and locale rows are not grandfathered. If they are touched or included in the task scope, unchanged AI-style prose still blocks completion.
- Completion names concrete files under `Docs/Lore/Grand_Library`, `Docs/Lore/AppliedContent`, `Assets/_SourceData/DataMonolith/Narrative`, or the generated page/binding/route-card outputs. If a canon fact is missing, stop as `BLOCKER` with the exact missing source.
- Runtime route-card export may only contain baked packet IDs. Source CSVs may carry draft prerequisite refs, but `AppliedLoreRouteCardExporter.py` must prune non-baked refs from DataMonolith output and `AppliedLorePacketCoverageAudit.py --all` must pass against the runtime-pruned export.

## Tasklocal Lane Contract Gate

Run only for new or materially rewritten serious `taskslocal` batches before user distribution or controller dispatch. Do not run strict mode across all historical `taskslocal` folders by default.

| Gate | Command / proof | Blocks dispatch |
|---|---|---|
| lane contract strict check | `python -B Tools/Docs/TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict`; requires `LANE_CLASS`, lane-compatible `DELIVERABLE_CLASS`, `VALID_COMPLETION`, `INVALID_COMPLETION`, `KILL_SWITCH`, executable lane-specific `PROOF_ROUTE`, and `EVIDENCE_BUDGET` | Yes |
| legacy inspection | `python -B Tools/Docs/TestTaskLocalLaneContracts.py taskslocal/<batch_name> --allow-legacy` | No, unless the batch is being reissued |
| exported subagent task | strict check applies if a subagent becomes a standalone task file | Yes |
| internal bounded subagent | governed by root subagent rules; no separate batch roster required | No |

## Agent Rule Routing Gate

Run after root authority, route bible index, routing docs, doc governance, quality gates, local agent shims, or mandate registry-surface edits.

| Gate | Command / proof | Blocks reporting |
|---|---|---|
| agent rule routing lint | `python -B Tools/Docs/TestAgentRuleRouting.py`; checks root mirror byte sync, authority read-order hooks, current live-path references, unguarded upper-authority/route-bible ambiguity/readiness/runtime tokens, subagent/orchestrator boundaries, lane contracts, mandate gate surfacing, and content-production gates | Yes |
| agent rule routing lint self-test | `python -B Tools/Docs/TestAgentRuleRouting.py --self-test`; reject-case proof for the path-existence check — a moved citation must fail and each legitimate not-on-disk idiom (dump target, migrated-away path, artifact-missing note) must stay silent | Yes after changing the agent rule routing lint tool |

Live-path coverage was extended on 2026-07-28 from nine routing files to every route bible in `PROJECT_BIBLES.md` and every mandate in `.agents-skills`. Three route bibles were citing owner `.cs` files that had moved, and one root doc was citing a build log that no longer exists; both classes had been rotting unchecked. A citation whose target is deliberately absent needs wording the skip list recognises, not a weakened check.

## Mandate Registry Gate

Run after `.agents-skills/*.txt` or `.agents-skills/README.md` edits.

| Gate | Command / proof | Blocks dispatch |
|---|---|---|
| mandate registry lint | `python -B Tools/Docs/TestMandateRegistry.py`; checks inventory count, command-language discipline, weak wording, ambiguous escape clauses, false readiness labels, truncated mandate bodies, visual parity inheritance for player-visible mandates, dangerous active runtime API examples, and proof/evidence language | Yes |
| mandate registry lint self-test | `python -B Tools/Docs/TestMandateRegistry.py --self-test` | Yes after changing the mandate registry lint tool |
| strict mandate format cleanup | `python -B Tools/Docs/TestMandateRegistry.py --strict-format` | Yes only when a mandate-format cleanup is the task |

## Signoff Rule

An unchecked item means the task is not done. State what is missing and why.

Do not submit this file, or a copied checklist from it, as proof of work.

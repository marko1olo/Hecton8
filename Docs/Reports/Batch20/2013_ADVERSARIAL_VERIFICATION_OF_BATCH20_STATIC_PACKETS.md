# 2013 Adversarial Verification Of Batch20 Static Packets

Status: STATIC ADVERSARIAL VERIFICATION / NO UNITY / NO ASSETS EDITS
Worker: Batch20 verifier 2013
Date: 2026-06-04

## Evidence Boundary

This verification inspected the scoped Batch20 reports and root authority only. It did not run Unity, Unity MCP, Play Mode, builds, imports, profiler, Frame Debugger, Memory Profiler, image generation, or active scene edits.

All runtime, visual, profiler, GC, import, material binding, placement, save/load, and first-hour gameplay claims remain `PENDING VERIFICATION`.

## Mandates Used

- `QA_Evidence_Text_Filter_Audit.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Verdict

The Batch20 packets are mostly honest about static-only proof. They are not safe as a direct Unity repair handoff without correction. The orchestrator may send a baseline/proof-capture handoff only. A repair/import/relink handoff must wait for sky/Aegir 2006 completion and must not pretend 2008/2009 prompt/static packets created usable source files, material imports, or Unity bindings.

Blocking findings: 10. Warnings: 8.

## Blocking Findings

1. `2012_SCENE_REPAIR_INTEGRATION_BACKLOG.md`, `2012_OWNER_HANDOFF_SEQUENCE.csv`, and `2012_BLOCKER_REGISTER.csv` are stale for 2010/2011. They state completed 2010/2011 status/log evidence is missing, but `Status_2010.md`, `LOG_2010.md`, `Rationale_2010.md`, `Status_2011.md`, `LOG_2011.md`, and `Rationale_2011.md` now exist. Orchestrator must not quote stale pending labels for those lanes.

2. 2006 completion is actually missing. `Status_2006.md`, `LOG_2006.md`, and `Rationale_2006.md` are absent. The supplemental sky package exists, but it is static and explicitly says Unity/import/profiler/build proof was not produced. Sky/Aegir cannot be treated as completed.

3. ProductFace counts contradict if read carelessly. `2011_STATIC_VALIDATOR_RESULTS.md` says `ProductFaceStaticRouteAudit.py` has zero findings, while `2008_PRODUCTFACE_DEBT_MATRIX.csv` and `1851_GENERATED_ASSET_PRODUCTION_AUDIT.md` retain 42 ProductFace built-in primitive prefab errors, 55 blocked material rows, and 17 package/default Lit rows. The green route audit only proves route-contract drift, not visual debt closure.

4. Generated asset readiness is still rejected at scale. `1851_GENERATED_ASSET_PRODUCTION_AUDIT.md` reports 83 errors, 1281 warnings, 338 missing manifests, 338 missing named proof artifacts, 338 surface/shallow visual-proof-pending rows, 42 ProductFace primitive errors, 21 final prefab primitive errors, and 20 family final-ready primitive links. No Batch20 packet closes that.

5. Active-scene debt remains a Unity-slot blocker. `2002_SURFACE_SHALLOW_SCENE_REPAIR_LEDGER.md` reports 342 built-in primitive mesh refs, 137 product-face filtered primitive refs, and 45 active-scene null material slots. No later packet provides active scene validation or relink proof.

6. `2003_CANDIDATE_RULE_PATCHES.diff.txt` is too broad to apply as-is. It flips strict filter guards and adds submerged/proxy rejection helpers, but helper visibility, partial-class duplication, `FamilySupportsFinalVariant`, and non-strict legacy scoring behavior are unresolved. Direct application risks compile break or broad placement changes without proof.

7. The 2012 Unity queue misses first-hour gameplay as a hard blocker. `FIRST_HOUR_RESOURCE_TOOL_OXYGEN_REACHABILITY_20260604.md` shows CopperVein is Drill-gated, starts at 40 m, the starter player lacks Drill, canonical SeafloorDrill item/prefab paths are missing, CopperWire has a 1-vs-2 copper authority conflict, and copper placement/reachability is unproved. Beautiful route captures cannot close this.

8. 2009 is prompt/QA only, not generated source. `2009_CANDIDATE_INTAKE_MANIFEST_TEMPLATE.csv` contains a template row only. `2009_GEMINI_SURFACE_SHALLOWS_PROMPT_PACKS.md` generated no images and imported nothing. Any Unity handoff that expects actual candidate files must wait.

9. Material/channel contracts remain blocked. `2008_TEXTURE_CHANNEL_CONTRACT_GAPS.csv` has 11 blocked/partial ProductFace channel-contract gaps. `2011_STATIC_VALIDATOR_RESULTS.md` reports 65 materials with issues, 21 materials with unresolved texture refs, 50 unresolved refs, 14 surface blocker materials, and 31 surface unresolved refs.

10. The current Unity owner identity in 2012 is mojibake. `2012_UNITY_SLOT_QUEUE.md` blocks on `ÐŸÑ€Ð¾Ð´Ð¾Ð»Ð¶Ð¸Ñ‚ÑŒ Ñ€Ð°Ð±Ð¾Ñ‚Ñƒ Ð¿Ð¾ Ð»Ð¾Ð³Ð°Ð¼`, which is not an operationally reliable owner label. The orchestrator must replace it with a real active owner or explicit free-slot statement before dispatch.

## Warnings

1. 2007 says “First-20-minutes route blocker removed” but only means proof-language clarity, not runtime water repair. Its own residual risk correctly keeps Unity visual proof pending.

2. 2010 uses `TOASTER`, `DECK`, `PRO`, and `GOD_MODE` labels from data files. The report treats them as review labels, but the orchestrator must explicitly prevent them becoming binary branch authorities.

3. 2012 queues ProductFace relink before or near channel lock. The relink slot must be blocked until shader/channel contracts are resolved.

4. 2012 queues placement repair using BioForge/Geology manifests that do not yet exist as accepted bake outputs. Placement edits before final variant/material proof risk deleting ecology or exposing proxies.

5. 2007’s ocean source facts include static scans for no hidden blits/cameras, but no Frame Debugger or profiler proof. This must not become render acceptance.

6. Crest quarantine remains partial. `2011_STATIC_VALIDATOR_RESULTS.md` says the existing Crest quarantine report is `FAIL` because Easy Save default scan still lists Crest/WaveHarmonic assemblies.

7. Black-box requirements are described, not proven. 2010/2012 specify 300-frame rings and dump paths, but no dump artifact exists for Batch20 visual repair.

8. `Docs/Actual Domains of Project.txt` was missing during multiple packet runs. They inferred narrow domains correctly, but the orchestrator should not present that file as read evidence.

## Required Checks Answered

1. Runtime proof without Unity: no scoped packet directly claims runtime proof as complete. Risk language exists in 2007’s “blocker removed” phrase and 2011’s `PASS` summaries; both must be quoted with their static boundaries.

2. Contradictory counts/findings: the main contradiction is ProductFace route audit zero findings versus 2008/1851/2011 aggregate debt. Exact files: `2011_STATIC_VALIDATOR_RESULTS.md`, `2011_AGGREGATE_VISUAL_DEBT_MATRIX.csv`, `2008_PRODUCTFACE_DEBT_MATRIX.csv`, `2008_PRODUCTFACE_RELINK_HANDOFF.md`, `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`.

3. Fake dependencies or simultaneous Unity ownership: 2012 correctly serializes Unity, but blocks on a mojibake active owner and references stale missing 2010/2011 evidence. It also implies downstream lanes depend on absent source candidates/bake manifests.

4. Missing or weak Unity-slot blockers: primitives, null materials, dry-land scatter, waterline/Crest, Aegir/sky, ProductFace, material channel contracts, and first-hour copper/drill/O2 route all need to be explicit blockers. First-hour copper/drill/O2 is underrepresented in the 2012 slot queue.

5. Low quality becoming primitive/flat/muddy: 2010 itself rejects ugly compact mode. The risk is the underlying `TOASTER/DECK/PRO/GOD_MODE` labels and missing Unity captures, not the 2010 prose.

6. Dangerous candidate patches: 2003 candidate runtime code is dangerous if applied without source inspection, compile proof, and placement preview. It is a candidate only.

7. Unity handoff: see below.

## Orchestrator Handoff, 12 Points Max

1. This is a baseline/proof-capture handoff only, not a repair-ready acceptance handoff.
2. Use exactly one Unity owner. Do not start if the active owner is unknown or still running.
3. Save proof only under `Docs/Reports/Batch20/VisualProof/<session>/`, never under `Assets`.
4. Start with baseline Game View and Scene View matched captures: surface/coast/ocean/Aegir, shoreline close, 0-5 m shallows, 20-50 m medium depth, Aegir crop, 360 sky pan.
5. Capture Compact/Middle/High/Ultra using numeric `GlobalQualityWeight`; reject any muddy/flat/primitive compact result.
6. Validate active scene primitive refs and null material slots from 2002 before claiming ProductFace or surface repair.
7. Do not apply 2003 candidate patches until source owner resolves helper visibility, proxy rejection, strict/non-strict behavior, and telemetry.
8. Do not relink ProductFace materials until 2008 channel contracts are resolved and source files/import settings exist.
9. Do not import 2009 prompt outputs unless real candidate files, hashes, QA rows, and license/status rows exist.
10. Ocean proof must include Frame Debugger/RenderGraph, Profiler/GC/memory/VRAM, and Crest hidden-pass inspection; static source route is not enough.
11. Sky/Aegir proof must wait for a real 2006 completion or equivalent owner packet; current source package is static only.
12. First-hour gameplay proof must include copper/tool/O2/craft/save/death route gates; visual proof alone is rejected.

## Final Gate

Unity-owner handoff is not safe for repair/import/relink execution now. It must wait for 2006 sky/Aegir completion or equivalent owner packet, real 2009 candidate files if imports are expected, and 2008 channel-contract closure before ProductFace relink. A narrow baseline capture handoff is safe only if the owner treats every result as proof gathering, not acceptance.

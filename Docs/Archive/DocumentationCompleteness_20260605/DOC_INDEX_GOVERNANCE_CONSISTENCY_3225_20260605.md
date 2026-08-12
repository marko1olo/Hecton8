# Doc Index Governance Consistency Audit 3225

Date: 2026-06-05
Worker: Documentation Worker 3225
Scope: documentation governance/index consistency vs filesystem layout
Evidence class: STATIC_DOC / FILESYSTEM
Runtime evidence: none
Final status: PENDING VERIFICATION

## Evidence Boundary

This audit used static document reads and filesystem scans only. No Unity, dotnet, build, importer, test, Play Mode, or runtime command was run.

Full-read sources:

- `AGENTS.md`
- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `PROJECT_BIBLES.md`
- `Docs/PROJECT_BASELINE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`

Targeted path-reference scans also inspected files listed in `Docs/README.md` Active Contract Map for path tokens only. That scan did not treat conceptual labels as runtime proof.

Commands and static checks used:

- `Get-Content -Raw` on the full-read source list.
- `Get-ChildItem -LiteralPath . -File -Filter *.md`
- `Get-ChildItem -LiteralPath Docs -File -Filter *.md`
- `Get-ChildItem -LiteralPath Docs -Directory`
- `Select-String` for authority lines and missing-reference lines.
- PowerShell regex extraction of backticked paths from `Docs/README.md` Active Contract Map.
- PowerShell regex extraction of `PROJECT_BIBLES.md` route file references.
- PowerShell regex extraction of project-relative references from Active Contract Map files.
- `Get-ChildItem -Recurse -Filter` for targeted source/artifact location checks.
- `git status --short -- Docs/Reports/DocumentationCompleteness_20260605 Docs/README.md Docs/DOC_GOVERNANCE.md Docs/ROOT_DOCS_REFERENCE.md PROJECT_BIBLES.md Docs/PROJECT_BASELINE.md Docs/QUALITY_GATES.md Docs/SYSTEMS_CONTRACTS.md`

Worktree boundary:

- Existing unrelated modified stable docs were present: `Docs/QUALITY_GATES.md`, `Docs/README.md`, `Docs/SYSTEMS_CONTRACTS.md`, `PROJECT_BIBLES.md`.
- Existing untracked files were present under `Docs/Reports/DocumentationCompleteness_20260605/`.
- This audit did not edit those files.

## Active Contract Map Filesystem Check

`Docs/README.md` Active Contract Map scan result:

- Active map refs checked: 57.
- Existing files: 48.
- Existing directories: 9.
- Missing refs: 0.

No file or directory listed in the `Docs/README.md` Active Contract Map was missing in this static scan.

Boundary defect: `Docs/README.md` includes `Docs/PROCEDURAL_ASSET_PIPELINE.md` as an active domain contract, while `PROJECT_BIBLES.md` routes `PROCEDURAL_ASSET_PIPELINE.md` at repository root. Both files exist and differ by size/date:

- Root: `PROCEDURAL_ASSET_PIPELINE.md`, 14313 bytes, 2026-06-05 03:18:19.
- Docs: `Docs/PROCEDURAL_ASSET_PIPELINE.md`, 7805 bytes, 2026-05-26 22:31:35.

That is a duplicated authority identity. Static filesystem evidence cannot decide which text is binding.

## Root Placement Rule vs Current Root Reality

`Docs/DOC_GOVERNANCE.md` states root may contain only five active anchors: `AGENTS.md`, `TASTE.md`, `textes.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`. `Docs/ROOT_DOCS_REFERENCE.md` repeats the same root policy. `Docs/PROJECT_BASELINE.md` repeats the same root text-anchor boundary.

Current root `.md` scan:

- Root markdown files: 75.
- Governance-allowed active root anchors: 5.
- Root markdown files outside the governance allowlist: 70.

`PROJECT_BIBLES.md` states that files listed under `Routes` are standing root bibles. Its route list contains 63 root `.md` file refs. Missing route files: 0.

Contradiction:

- Governance docs say active docs belong in `Docs/` or `Docs/ARCHITECTURE/`, with only five root active anchors.
- `PROJECT_BIBLES.md` says 63 route files are standing root bibles.
- 61 `PROJECT_BIBLES.md` route files sit outside the five-file root allowlist.

This is not a missing-file problem. It is an authority-placement contradiction.

## Missing References and Ambiguous References

### Active docs citing missing concrete refs

Concrete project-relative refs missing in the static scan:

| Active doc | Missing ref | Current classification |
|---|---|---|
| `Docs/ARCHITECTURE/DISPATCH_PIPELINE.md` | `Docs/AgentLogs/Dump_SYSTEM_DISPATCHER.bin` | Planned fault artifact; absent. |
| `Docs/ARCHITECTURE/DISPATCH_PIPELINE.md` | `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md` | Historical output path; absent. |
| `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md` | `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log` | Historical proof path; absent. |
| `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` | `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` | Historical manifest; absent. |
| `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` | `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` | Historical actuality manifest; absent. |
| `Docs/SYSTEMS_CONTRACTS.md` | `Docs/AgentLogs/Dump_SHINOBU_100.bin` | Dump target; absent. |

`Docs/Generated/README.md` states `PROJECT_ATLAS_HPHI.md` is currently absent and warns not to cite it as current proof. That is internally labeled, not an accidental broken ref.

### Active docs using bare markdown refs that do not resolve next to the source doc or root

These are likely relative-link hygiene defects or shorthand labels:

- `Docs/_Archive/README.md`: `MANIFEST.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`: `QUALITY_GATES.md`, `SYSTEMS_CONTRACTS.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`: `QUALITY_GATES.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`: `QUALITY_GATES.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`: `PROJECT_ATLAS_HPHI.md`
- `Docs/DEPENDENCY_GRAPH.md`: `PROJECT_ATLAS_HPHI.md`
- `Docs/Generated/README.md`: `PROJECT_ATLAS_HPHI.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`: `PROJECT_RUNTIME_TOPOLOGY.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`: `DATA_MONOLITH_H8BIN_SPEC.md`, `DATA_MONOLITH_RUNTIME_INTEGRATION.md`, `SAVE_PAGING_PROTOCOL.md`

The likely intended targets for several of these are same-family docs under `Docs/ARCHITECTURE/` or `Docs/Generated/`. They should be patched as explicit paths, not inferred by readers.

### Missing docs listed as active

No `Docs/README.md` Active Contract Map entry was missing.

No `PROJECT_BIBLES.md` route file was missing.

### Root files with unclear authority class

Unclear or inconsistent root authority classes from selected-doc evidence:

- `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md`: exists at root, not allowed by root placement policy, not listed by `PROJECT_BIBLES.md`, and not referenced by selected governance/index docs.
- `HECTON8_ORCHESTRATOR.md`: root file with conditional `AGENTS.md` instruction for orchestrator work, but not allowed by root placement policy and not listed by `PROJECT_BIBLES.md`.
- `3DMODEL_EQUIPMENT_PROPS.md`, `3DMODEL_FAUNA.md`, `3DMODEL_FLORA_CORAL.md`, `3DMODEL_GEOLOGY_ROCKS.md`, `3DMODEL_HARD_SURFACE_MODULES.md`, `3DMODEL_TEXTURES_MATERIALS.md`: root files referenced by `AGENTS.md` for generated asset work, but not listed as `PROJECT_BIBLES.md` routes and not allowed by root placement policy.
- `PROJECT_BIBLES.md`: active routing index per `Docs/README.md` and `AGENTS.md`, but not in the five-file root allowlist.
- `VISION_LOCKS.md`: active product-vision lock per `Docs/README.md`, `AGENTS.md`, and `PROJECT_BIBLES.md`, but not in the five-file root allowlist.

Root route bibles listed by `PROJECT_BIBLES.md` are not unclear semantically, but they are root-placement violations under current governance docs.

## Recommended Patch Order

Do not patch stable docs from this worker. Recommended owner patch order:

1. `Docs/DOC_GOVERNANCE.md` and `Docs/ROOT_DOCS_REFERENCE.md`: decide root policy. Either expand the root allowlist for route bibles and locks, or require route bibles to move under `Docs/` with explicit compatibility aliases. Current mixed policy is contradictory.
2. `PROJECT_BIBLES.md`: list every AGENTS-referenced 3D sub-bible that is meant to be standing authority, or label them as subordinate files owned by `3dmodel.md`.
3. `Docs/README.md`: align Active Contract Map with the root-policy decision. Specifically resolve the `PROCEDURAL_ASSET_PIPELINE.md` root-vs-Docs duplicate identity.
4. `Docs/PROJECT_BASELINE.md`: update the root/docs boundary only after the policy owner decision above.
5. `Docs/ARCHITECTURE/*` active docs: patch bare same-family markdown refs to explicit `Docs/ARCHITECTURE/...` paths where intended.
6. `Docs/ARCHITECTURE/DISPATCH_PIPELINE.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`, `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`, and `Docs/SYSTEMS_CONTRACTS.md`: relabel absent historical artifacts as missing historical evidence, planned output, or target path. Do not present absent paths as proof.
7. `Docs/Generated/README.md`, `Docs/DEPENDENCY_GRAPH.md`, and `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`: keep `PROJECT_ATLAS_HPHI.md` absent-state language consistent until an actual artifact exists.

## GlobalQualityWeight Consequences for Governance Planning

`GlobalQualityWeight` does not change doc truth. It can scale proof planning density, scan cadence, and generated-report breadth only.

| Lane | Governance/proof planning consequence |
|---|---|
| Low | Keep a compact active index. Run only cheap filesystem/path scans before work assignment. Do not expand root ambiguity. |
| Middle | Add scheduled link/path scans for active docs and route bibles. Keep generated reports outside stable docs. |
| High | Add broader citation integrity scans across `Docs/ARCHITECTURE`, `Docs/Generated`, and root route bibles; promote only durable facts. |
| Ultra | Add full documentation graph generation, orphan/citation diffing, route-bible coverage matrix checks, and proof-artifact freshness scoring. Still no prose-only readiness claim. |

## Regression Model

CPU: Static PowerShell and `rg`/`Get-ChildItem` scans only. No runtime CPU path touched.

GC: No Unity runtime or game hot path touched. Audit allocations are shell/process-local and irrelevant to frame GC.

Memory: No project assets imported or loaded by Unity. Filesystem scans only.

Cadence: No dispatcher, build, test, Play Mode, importer, or editor cadence touched.

Correctness: Risk is report classification error only. Main correctness risks are false-positive missing refs from conceptual labels and false-negative refs outside backticks/markdown links. Stable docs remain unchanged.

## Final Status

PENDING VERIFICATION.

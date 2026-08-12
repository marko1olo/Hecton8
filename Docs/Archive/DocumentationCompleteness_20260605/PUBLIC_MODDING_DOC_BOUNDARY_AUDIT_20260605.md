# Public And Modding Documentation Boundary Audit - 2026-06-05

Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / FILESYSTEM
Owner: documentation completeness audit worker

## Scope

Mission: audit `Docs/Marketing` and `Docs/Modding` boundaries as planning/API/public-copy corpora, not shipping, publication, platform, SDK-loader, or runtime readiness proof.

Mandates followed:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Pentarchy_Audit.txt`

Authority/docs read or searched:

- `AGENTS.md` targeted lines for public copy, modding, proof, readiness, evidence, and logging rules.
- `PROJECT_BIBLES.md` route entries for `textes.md`, `modding.md`, `platform.md`, `release.md`, and `quality.md`.
- `textes.md`, `modding.md`, `quality.md`.
- `Docs/README.md`.
- `Docs/Marketing/README.md` plus targeted top-level/entry files by text search.
- `Docs/Modding/README.md` plus targeted index/audit/API/quarantine files by text search.
- `Docs/Reports/DocumentationCompleteness_20260605/DOC_COMPLETENESS_SYNTHESIS_AND_PATCH_QUEUE_20260605.md`.

No Unity, dotnet build, importer, test, Play Mode, profiler, browser/platform action, source edit, stable doc edit, marketing doc edit, or modding doc edit was run.

First-20 route impact: removes a public/modding claim-boundary blocker before any first public proof packet, SDK-facing claim, Steam/demo claim, platform claim, or release claim can be trusted.

## Audit Answers

1. Are `Docs/Marketing` and `Docs/Modding` clearly bounded?

Mostly yes at corpus-entry level.

- `Docs/README.md` classifies `Docs/Marketing` as public-copy/launch/outreach/support corpus and says public work must read `textes.md`; no public send/readiness claim without proof gates.
- `Docs/README.md` classifies `Docs/Modding` as API/product plan only unless current source and runtime artifacts prove loader behavior.
- `Docs/Marketing/README.md` carries a static-documentation boundary and repeated publication gates.
- `Docs/Modding/README.md` carries `ENVELOPE-ONLY ... RUNTIME_PENDING`, states runtime proof is `PENDING`, and forbids `VERIFIED` until the runtime playbook passes.

Weakness: the boundary is not uniformly local. Static scan found 46 of 75 marketing markdown files without an `Authority Boundary` section, and 4 of 18 modding markdown files without an `Authority Boundary` section.

2. Do marketing docs route public voice to `textes.md` and proof/release claims to `quality.md` / `release.md` / `platform.md`?

Functionally yes, explicitly weak.

- `textes.md` is the root public-copy authority and requires proof sources before performance, platform, demo, Steam, visual-quality, gameplay-scope, or release-readiness claims.
- `quality.md` defines public-writing proof requirements and proof-state labels.
- Marketing docs contain strong local gates for Steam page, demo, public CTA, press release, public post, localization, private access, paid spend, account registration, and support surfaces.

Gap: `rg "textes\.md|quality\.md|release\.md|platform\.md" Docs/Marketing --glob "*.md"` returned no matches. Marketing docs duplicate some proof rules but do not directly cite the root public-copy/proof/platform/release bibles, so route authority can be lost when individual files are quoted out of context.

3. Do modding docs preserve envelope-only/public API boundaries and avoid loader/runtime readiness claims without proof?

Yes at the main contract level.

- `modding.md` defines envelope-only public modding and forbids arbitrary runtime code, direct Unity object handles, direct native handles, loose runtime asset loading, and public runtime readiness claims without runtime artifacts.
- `Docs/Modding/README.md` says runtime proof is `PENDING` and points at `Runtime_Verification_Playbook.md` before `VERIFIED`.
- `Docs/Modding/Mod_API_Specification.md`, `Command_Audit_Matrix.md`, `API_Surface_Audit_Matrix.md`, `Event_Subscription_Audit_Matrix.md`, `Payload_Layout_Audit_Matrix.md`, `Loader_Save_Audit_Matrix.md`, `Resource_Content_Audit_Matrix.md`, and `Signal_Audit_Matrix.md` repeatedly label claims static/runtime-pending and deny direct first-party lanes.

Weakness: several modding planning/product files lack the same local authority-boundary block, and user-facing phrases like `first playable mod`, `local discovery install`, and `current file contract for random internet authors` need stronger front-loaded denial that they are authoring/review/discovery states, not runtime loader readiness.

## Top 10 Gaps

1. `Docs/Marketing/README.md`

Corpus entry is strong on gates but does not explicitly route public voice to `textes.md` or proof/platform/release claims to `quality.md`, `release.md`, and `platform.md`. Search found zero direct root-route citations in `Docs/Marketing`.

2. `Docs/Marketing/MARKETING_CONTROL_TOWER.md`

Primary marketing entry file lacks a local `Authority Boundary` section. It says `Runtime impact: none` and `no public push`, but as the control layer it needs the standard static-doc/no-readiness proof block and direct root-route citations.

3. `Docs/Marketing/Ads/PAID_MICROTESTS_AND_AD_CREATIVE_MATRIX.md`

No local `Authority Boundary` section. It includes `Wishlist on Steam` / `Play demo or wishlist` CTA rows behind gates; without the standard boundary header, excerpts can look like approved public ad copy.

4. `Docs/Marketing/Analytics/MEASUREMENT_AND_UTM_PLAN.md`

No local `Authority Boundary` section. It contains Steam visits, wishlist conversion, demo play, and directional KPI language. It has a measurement boundary, but not the standard no-public-page/no-release/no-demand-proof disclaimer at the top.

5. `Docs/Marketing/Press/PRESS_RELEASE_AND_EMAIL_TEMPLATES.md`

No local `Authority Boundary` section. It has good press release gates, but it contains launch/demo/Steam skeleton copy. Because this is directly reusable public copy, it needs explicit `textes.md` voice routing and `quality.md` / `release.md` / `platform.md` proof routing in the file itself.

6. `Docs/Marketing/Social/SOCIAL_ACCOUNT_SETUP_AND_PLATFORM_PLAYBOOK.md`

No local `Authority Boundary` section by static scan. Search hits show account states, public profile notes, public-post gates, and automation/publish command examples. This file needs a local no-publication-proof boundary so account existence or browser/session notes cannot be mistaken for publication permission.

7. `Docs/Marketing/Steam/DEMO_PLAYTEST_AND_TELEMETRY_PLAN.md`

No local `Authority Boundary` section by static scan. Demo/playtest telemetry is one of the highest-risk publication surfaces and should repeat static-only, no-demo-public-access, no-platform-readiness, and proof-artifact requirements locally.

8. `Docs/Marketing/Roadmap/PUBLIC_ROADMAP_LANGUAGE_AND_PROMISE_POLICY.md`

No local `Authority Boundary` section by static scan. Public roadmap policy should explicitly route to `textes.md` for voice and `quality.md` / `release.md` / `platform.md` for any schedule, platform, demo, or feature-scope promise.

9. `Docs/Modding/External_Starter_Kit_File_Contract.md`

No local `Authority Boundary` section. The document correctly says runtime stays envelope-only, but it also uses terms like `first playable package draft`, `install-local`, and `local loader discovery`. It needs a standard static-only / authoring-only / no-runtime-loader-proof boundary before the file roles.

10. `Docs/Modding/Mod_API_Sandbox_Quarantine.md`, `Docs/Modding/SDK_Authoring_Interface_Plan.md`, `Docs/Modding/SDK_Product_Blueprint.md`

These high-authority modding planning files lack a local `Authority Boundary` section. Their status lines are strong, but the standard boundary block should be present because these files discuss current editor surfaces, starter kits, local install/diagnosis, SDK product UX, and runtime quarantine.

## Static Counts

- `Docs/Marketing/**/*.md`: 75 files.
- Marketing markdown files missing `Authority Boundary`: 46.
- `Docs/Modding/**/*.md`: 18 files.
- Modding markdown files missing `Authority Boundary`: 4.
- Direct root-route citations in `Docs/Marketing` for `textes.md`, `quality.md`, `release.md`, `platform.md`: 0 by `rg`.

## Regression Model

- CPU: documentation-only scan and report write. No runtime CPU path touched.
- GC: no runtime code changed. No `0 B/frame` claim.
- Memory: no Unity asset import, player memory, or editor memory path touched.
- Cadence: no dispatcher, importer, test, scene, Play Mode, or build cadence touched.
- Correctness: improves boundary visibility only by reporting gaps. Stable docs were not patched.
- Hot path impact: none.
- Failure modes: static grep can miss semantic contradictions; dated platform statements may be stale; source/runtime claims were not verified; no external platform docs were checked in this pass.

## Continuous Quality Consequences

`GlobalQualityWeight` has no runtime effect in this audit.

- Low: keep corpus entry boundaries and no-readiness labels visible.
- Middle: add local boundary headers to high-risk marketing/modding files.
- High: add root-route citations and proof-artifact checklists to public-copy and SDK/public API files.
- Ultra: add automated documentation lint for missing authority boundaries, root-route citations, and forbidden readiness wording. Still no prose-only readiness claim.

## Checks Run

Static commands used:

```powershell
Get-ChildItem -Path .agents-skills -File | Select-Object -ExpandProperty Name
Get-Content -Path .agents-skills\QA_Evidence_Text_Filter_Audit.txt
Get-Content -Path .agents-skills\ARCH_Pentarchy_Audit.txt
Select-String -Path AGENTS.md -Pattern 'public copy|marketing|textes|modding|proof|evidence|quality|release|platform|readiness|publication|runtime readiness|FORBID|Docs/Marketing|Docs/Modding' -Context 2,3
Select-String -Path PROJECT_BIBLES.md -Pattern 'textes|modding|platform|release|quality' -Context 4,12
Get-Content -Path textes.md
Get-Content -Path modding.md
Get-Content -Path quality.md
Get-Content -Path Docs\README.md
Get-Content -Path Docs\Marketing\README.md
Get-Content -Path Docs\Modding\README.md
Get-Content -Path Docs\Reports\DocumentationCompleteness_20260605\DOC_COMPLETENESS_SYNTHESIS_AND_PATCH_QUEUE_20260605.md
rg -n "textes\.md|quality\.md|release\.md|platform\.md" Docs\Marketing --glob "*.md"
rg -n "Authority Boundary" Docs\Marketing --glob "*.md"
rg -n "Authority Boundary" Docs\Modding --glob "*.md"
```

Pending required check after writing this report:

```powershell
git diff --check -- Docs/Reports/DocumentationCompleteness_20260605/PUBLIC_MODDING_DOC_BOUNDARY_AUDIT_20260605.md
```

Final status: PENDING VERIFICATION.

# DENTE Rationale

Status: VERIFIED
Evidence policy: static source and CLI tests only unless artifact says otherwise.

## 2026-05-23 - Scope

Problem: User requested DENTE dental CRM site and Telegram bot improvements while root HECTON8 instructions target Unity runtime.
Solution: Keep edits inside `C:\hades\dental-crm`; use HECTON8 process files only for status/rationale evidence.
Rejected Alternatives: Editing Unity systems or inventing cross-domain dependencies would not improve DENTE and would violate domain boundary.
Scalability potential: Multi-clinic bot/document/schedule flows stay data-driven at API/UI level; no per-clinic hardcoded branch.
Hardware Impact: 0 us runtime on HECTON8; web/API impact verified by Node/Vite tests only.

## 2026-05-23 - Product Direction

Problem: Telegram callbacks for reviews/maps ended as URL-only dead ends; real document requests omitted refusal forms; schedule capacity used chair hours but load math counted all dates.
Solution: Add next-action inline keyboards, include refusal form in patient request UI, show visual card previews and warnings, and date-scope shift loads.
Rejected Alternatives: Command-only Telegram UX; fake document labels; reverting chair capacity to clinic-level hours.
Scalability potential: Low tier uses short inline keyboards and server-rendered preview state; middle/high/ultra can add richer images and per-clinic bot records without changing patient workflow truth.
Hardware Impact: Node-side filtering is small linear scan over demo appointment arrays; no Unity frame impact. Browser preview images use bounded CSS dimensions.

## 2026-05-23 - Verification

Problem: Per-chair capacity made `shiftIntelligence.chairLoads[].utilizationPercent` exceed the shared schema max of 200 and caused appointment creation to return 409.
Solution: Filter resource loads to the active clinic date and cap displayed utilization to 200 while preserving `bookedMinutes` and an overload flag with the factual raw percentage.
Rejected Alternatives: Raising DTO max without UI contract review; restoring clinic-level chair capacity; ignoring the failed smoke.
Scalability potential: Low tier gets a bounded meter; middle/high/ultra can add richer overload drilldowns from `bookedMinutes` and flags without DTO breakage.
Hardware Impact: 0 us Unity runtime. Node-side cost is one date-key filter per resource load over current in-memory appointments; no profiler claim.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 625.66 kB > 500 kB.
- CLI_TEST: `npm run smoke:schedule-configuration` passed.
- CLI_TEST: `npm run smoke:telegram-bot` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed.
- BROWSER_SMOKE: `npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed with `.telegram-settings`, viewport 390, overflow 0.
- BLOCKED_ARTIFACT: screenshot-mode mobile smoke hung inside headless Edge twice; no PNG artifact produced.

## 2026-05-23 - Tax, Receipt, Schedule Hardening

Problem: Tax application UI used eligible fiscal payments instead of the explicit selected payment ids; payment receipt payer fields could fall back from the patient card; INN comparison used raw strings; schedule gap/buffer suggestions compared adjacent appointments across clinic-local dates.
Solution: Use `selectedTaxPaymentIdsForCurrentDocument()` for tax application/certificate/registry payloads, prune invalid tax payment selections without auto-selecting all, remove patient-card fallback from receipt payer fields, require payer facts on selected payments in API guard, normalize INN to digits for document/payment comparisons, and compare schedule gap/buffer candidates only on the same clinic-local date.
Rejected Alternatives: Keeping auto-select all behind a UI effect; treating patient profile as payer truth; validating formatted INN by raw text; allowing midnight-crossing appointment pairs to create same-day operational hints.
Scalability potential: Low tier keeps small deterministic filters and explicit operator choices; middle/high/ultra can add richer document previews and multi-clinic queues without changing fiscal truth ownership.
Hardware Impact: 0 us Unity runtime. Web/API impact is bounded by selected/eligible payment counts and adjacent appointment count; no profiler claim.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23 after final TSX/API changes.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 625.49 kB > 500 kB.
- CLI_TEST: `npm run smoke:schedule-configuration` passed after adding a cross-date negative case.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed after adding negative guards for silent all-payment selection and patient-card receipt fallbacks.
- CLI_TEST: `npm run smoke:document-guards` passed after adding INN normalization and missing payer-fact cases.
- CLI_TEST: `npm run smoke:tax-document-explicit-payment-scope` passed.
- CLI_TEST: `npm run smoke:tax-registry-fiscal` passed.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:tax-payment-explicit-payer` passed.
- CLI_TEST: `npm run smoke:document-payloads` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed.
- CLI_TEST: `npm run smoke:telegram-bot` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- CLI_TEST: `npm run smoke:onboarding-configuration-source` passed.
- CLI_TEST: `npm run smoke:schedule-autosave-retry` passed.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.

## 2026-05-23 - Telegram Outbox Paging And Onboarding Readiness

Problem: Telegram outbox counts were computed after page slicing and the web UI filtered/sliced locally, so large clinics would see misleading queue numbers and hidden sendable reminders. First-run onboarding also ignored active appointment readiness and could finish while the first appointment was outside team or schedule constraints.
Solution: Add server-side outbox `status`/`templateKind` filters, `limit`/`cursor` paging, `totalCount`, `filteredCount`, and `nextCursor`; make due-send use the due-filtered server queue; make the Settings UI send filters to the API and load more by cursor; include active appointment `team`/`schedule` readiness blockers in onboarding completion.
Rejected Alternatives: Keeping browser-only filtering; increasing the local slice; duplicating schedule logic in onboarding instead of reading the dashboard readiness DTO.
Scalability potential: Low tier loads one compact page; middle/high/ultra clinics can page through bigger reminder batches with the same API contract and can later move queue construction to a database without changing the UI shape.
Hardware Impact: 0 us Unity runtime. Web/API impact is bounded by current in-memory outbox list filtering and one dashboard readiness lookup; no profiler claim.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 626.69 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed after adding outbox cursor/filter/count regressions.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed after requiring API query params, cursor UI, and count fields.
- CLI_TEST: `npm run smoke:onboarding-configuration-source` passed after requiring active appointment readiness blockers.
- CLI_TEST: `npm run smoke:schedule-configuration` passed.

## 2026-05-23 - Telegram Link/Chat Ledgers And Document Audit

Problem: Telegram link-code and chat-link endpoints still returned fixed small latest lists while outbox already had server paging. Busy clinics could not review old connection codes, active/revoked chat links, or subject-specific history from the Settings UI. Parallel document audit also showed that the catalog/smokes are broad, but release-grade official output still lacks real PDF and signed/XSD-validated tax XML artifacts.
Solution: Add shared list response schemas for link codes and chat links; add API query parsing for status, subject type, subject id, limit, and cursor; add count-before-page totals and operational counters; update Settings to keep ledger metadata and load more by cursor; update Telegram smoke/source smoke and docs. Record document residual risks instead of overstating release readiness.
Rejected Alternatives: Increasing latest-list caps; keeping UI-only filtering; exposing internal chat transport references; treating HTML previews and draft XML as final official document artifacts.
Scalability potential: Low tier loads one compact page with counters; middle/high/ultra clinics can page through large ledgers with the same API shape. The same contract can later move from sample arrays to DB-backed per-clinic bot runtime without UI rewrites.
Hardware Impact: 0 us Unity runtime. Web/API impact is bounded by current in-memory ledger filtering and React merge over visible page ids; no profiler claim.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 628.66 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed after adding link-code/chat-link paging regressions.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed after requiring ledger response state, parsers, and load-more UI.
- SUBAGENT_AUDIT: Telegram audit identified singleton runtime and fixed ledger caps; fixed ledger caps in this loop, singleton runtime remains open.
- SUBAGENT_AUDIT: Document audit verified existing document smoke suite passed; release-grade PDF/signature/XSD gates remain open.

## 2026-05-23 - Scoped Multi-Clinic Telegram Runtime

Problem: Clinic-owned Telegram runtime still depended on one active settings object. A multi-clinic deployment could not safely route `/status` or webhook traffic to a specific clinic bot, and a same-organization multi-bot setup could accidentally use the first matching env token/secret.
Solution: Add a server-only runtime resolver for `DENTE_TELEGRAM_CLINIC_BOTS_JSON` records keyed by `organizationId`, optional `clinicId`, and `botConfigId`; add scoped status/webhook routes; prefer the matched env config token/secret in the runtime context; reject ambiguous `organizationId`-only routes when more than one bot config matches.
Rejected Alternatives: Browser-entered bot tokens; continuing singleton demo runtime; first-match fallback for organizations with multiple bots; faking DB-backed runtime before a real per-organization settings owner exists.
Scalability potential: Low tier uses one clinic-owned bot config and simple routes. Middle tier can run multiple clinics on one deployment with scoped webhook URLs. High/ultra tier can replace the env JSON bridge with DB-backed runtime without changing the public route shape.
Hardware Impact: 0 us Unity runtime. API cost is a small request-time scan of server config records; no profiler claim.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23 after scoped runtime changes.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 628.66 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed with same-organization two-bot regression: scoped route used the requested bot token and ambiguous route returned 404.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed after requiring scoped route/source markers.
- CLI_TEST: `npm run smoke:telegram-validation` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- SUBAGENT_AUDIT: Telegram runtime audit identified scoped route resolver as the smallest practical next step before DB-backed runtime.

## 2026-05-23 - Document Issue Passport And Archive Download

Problem: Issued document HTML snapshots were already immutable, but the operator UI only exposed a generic preview link. There was no privileged issue-passport route showing archive/source facts, no explicit archived HTML attachment download, and the remaining PDF/FNS gaps could be blurred by UI labels.
Solution: Add `documentAuditFactsSchema`, `/api/documents/:id/audit-facts`, and `/api/documents/:id/html?download=1`; show a Documents-screen passport panel with source authority, source reference, source status, snapshot SHA-256, blockers/warnings, preview and archived HTML download actions; update smokes and docs to state that this is verified HTML, not PDF.
Rejected Alternatives: Re-rendering current patient/profile data for downloads; exposing `storagePath`; calling unsigned XML a final FNS package; adding a fake PDF button without renderer/signature proof.
Scalability potential: Low tier uses one compact passport panel and a verified HTML attachment. Middle tier can add DB-backed snapshot storage under the same public DTO. High/ultra tier can add PDF/signature/XSD routes without changing document issue truth ownership.
Hardware Impact: 0 us Unity runtime. API work is bounded by one document lookup, one patient lookup and existing snapshot integrity read; no profiler claim.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 631.85 kB > 500 kB.
- CLI_TEST: `npm run smoke:document-lifecycle` passed with audit-facts, no `storagePath`, attachment download, draft no-download, and voided warning checks.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed after requiring passport/download UI source markers.
- CLI_TEST: `npm run smoke:document-html-issue-guards` passed.
- CLI_TEST: `npm run smoke:documents-catalog` passed.
- CLI_TEST: `npm run smoke:document-guards` passed.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed.
- CLI_TEST: `npm run smoke:document-legal-confirmations` passed.
- CLI_TEST: `npm run smoke:document-issue-chains` passed.
- CLI_TEST: `npm run smoke:tax-registry-fiscal` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- BROWSER_SMOKE: `npm run smoke:mobile -- http://127.0.0.1:5173/#documents` passed with `.document-list`, viewport 390, overflow 0.
- BLOCKED_ARTIFACT: expanded mobile click smoke targeting `.document-audit-facts` hung inside headless Edge and was killed; no screenshot artifact.

## 2026-05-23 - Issued Document PDF Export

Problem: The system had immutable issued HTML snapshots and archive downloads, but still lacked a real PDF export. Calling HTML an official PDF would be false; generating PDF from mutable current data would break the issued snapshot guarantee.
Solution: Add `/api/documents/:id/pdf` that reads the verified issued/voided snapshot and prints it to PDF through server-side Chromium/Edge (`DENTE_PDF_BROWSER_BIN` or standard Edge/Chrome paths). Add `canExportPdf` and `pdfDownloadUrl` to audit facts. Add `Скачать PDF` actions in the document row and passport panel. Extend lifecycle smoke to assert a real `%PDF` attachment with non-empty bytes.
Rejected Alternatives: Adding a fake PDF label; relying on browser `window.print`; creating a custom minimal PDF without Cyrillic/font fidelity; exporting drafts as PDF.
Scalability potential: Low tier uses local Edge/Chromium print on demand. Middle tier can move PDF rendering to a worker queue. High/ultra tier can add signed PDFs and PDF/A without changing the document issue truth route.
Hardware Impact: 0 us Unity runtime. API impact is a per-export browser process, acceptable for manual document download but not for high-volume batch jobs without a worker pool.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 632.80 kB > 500 kB. First build attempt timed out without compiler output; rerun passed.
- CLI_TEST: `npm run smoke:document-lifecycle` passed and asserted `%PDF` attachment > 1 KB from issued snapshot.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed after requiring PDF route/action markers.
- CLI_TEST: `npm run smoke:document-html-issue-guards` passed.
- CLI_TEST: `npm run smoke:documents-catalog` passed.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed.
- CLI_TEST: `npm run smoke:document-guards` passed.
- CLI_TEST: `npm run smoke:document-legal-confirmations` passed.
- CLI_TEST: `npm run smoke:tax-registry-fiscal` passed.
- BROWSER_SMOKE: `npm run smoke:mobile -- http://127.0.0.1:5173/#documents` passed with `.document-list`, viewport 390, overflow 0.

## 2026-05-23 - Button-First Document Telegram Outbox

Problem: Automatic document-ready messages existed, but the real outbox markup was too thin: document readiness gave only one portal link, and tax-document status had no useful patient next actions. That failed the button-first bot direction and made the Settings preview understate what the patient should receive.
Solution: Extend `telegramReplyMarkupFor` for document-ready, tax-status, payment, post-visit and recall patient templates. Document-ready and tax-status messages now keep portal, documents/tax submenu, clinic contact and privacy buttons while text remains generic. Smoke tests assert visual card usage and that no diagnosis, tooth, treatment, fiscal receipt, amount, payer INN, PDF/file wording or document content leaks into previews.
Rejected Alternatives: Slash-command instructions; embedding document names or tax/fiscal details in Telegram; forcing the patient to use only a portal URL; changing the site preview separately from the API payload.
Scalability potential: Low tier sends a compact two-to-three-row keyboard. Middle/high/ultra clinics can add richer per-clinic images or portal deep links behind the same `replyMarkup` DTO without changing patient data policy.
Hardware Impact: 0 us Unity runtime. API work is a constant-size keyboard construction per outbox item; no profiler claim.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 632.80 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed after button/card/no-leak assertions.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed after source guard for richer patient template keyboards.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed.
- BROWSER_SMOKE: `npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed with `.telegram-settings`, viewport 390, overflow 0.

## 2026-05-23 - Telegram Portal Section Handoff

Problem: The `Открыть DENTE` inline button was safe but too generic. A patient receiving a document, tax, payment, post-visit or recall message landed on the same portal root instead of the relevant section.
Solution: Build section-specific portal URLs from the configured HTTPS base and append only non-identifying parameters: `dente_source=telegram` and `dente_section=documents|tax|billing|care|schedule`. Apply the same handoff to outbox previews, outbox delivery markup, and webhook document/tax/care menus.
Rejected Alternatives: Raw portal root for every action; embedding patient/document/appointment/payment ids in Telegram URLs; inventing authenticated per-document deep links before the portal identity layer exists.
Scalability potential: Low tier gets deterministic section routing with no new storage. Middle tier can route those sections in the web portal. High/ultra tier can add authenticated portal sessions and signed one-time handoff tokens behind the same button contract.
Hardware Impact: 0 us Unity runtime. API work is constant-size URL construction per keyboard row; no profiler claim.

Evidence:
- CLI_COMPILE: `npm run typecheck` passed on 2026-05-23.
- CLI_COMPILE: `npm run build` passed on 2026-05-23; residual Vite warning: main app chunk 632.80 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed after rebuilding dist; the first attempt failed because stale dist still used root portal URLs.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:telegram-validation` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed.
- CLI_TEST: `npm run smoke:document-lifecycle` passed.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed with viewport 390 and overflow 0.

## 2026-05-23 - Web-Side Telegram Section Handoff

Problem: Telegram buttons now emitted safe section intent, but the web shell still had to consume that intent. Without site-side routing, `dente_section=tax|billing|care|schedule` was only a query string and patients still landed in the wrong place or kept stale query state in browser history.
Solution: Add a first-load handoff parser in `apps/web/src/App.tsx` that accepts only `dente_source=telegram` and known `dente_section` values, maps them to Documents, Finance, Communications or Schedule, preselects the tax/document form where useful, shows a Russian handoff notice, and strips the query from the URL. Add a source smoke and mobile browser smokes that prove malicious patient/document/appointment/payment ids are ignored and removed.
Rejected Alternatives: Keeping API-only handoff; persisting Telegram section in UI preferences; accepting identity-bearing deep links before a real authenticated portal session exists; routing by arbitrary query string.
Scalability potential: Low tier gets deterministic section routing with one URL parse. Middle tier can add authenticated portal sessions behind the same safe section contract. High/ultra tier can add signed one-time handoff tokens without changing bot keyboard labels or patient workflow.
Hardware Impact: 0 us Unity runtime. Browser impact is a single first-load URL parse, one section state update, and one history replacement; no hot-loop or server cost.

Evidence:
- CLI_TEST: `npm run smoke:telegram-handoff-source` passed.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: main app chunk 635.26 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:telegram-bot` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed.
- BROWSER_SMOKE: tax handoff opened `#documents`, showed `.document-factory-tax-payments`, and stripped malicious `patientId`.
- BROWSER_SMOKE: billing handoff opened `#finance`, showed `.finance-panel .payment-capture`, and stripped malicious `documentId`.
- BROWSER_SMOKE: care handoff opened `#communications`, showed `.communications-panel .communication-task-list`, and stripped malicious `appointmentId`.
- BROWSER_SMOKE: schedule handoff opened `#schedule`, showed `.schedule-panel .schedule-filter-strip`, and stripped malicious `paymentId`.

## 2026-05-23 - Telegram Home Handoff And Russian Bridge Copy

Problem: API and bot routes already emitted `dente_section=home` for generic `/start`, `/help`, clinic and privacy portal buttons, but the web handoff parser accepted only documents/tax/billing/care/schedule. A mobile behavior smoke also showed that the handoff query could remain in `location.href` until UI preference hydration completed. Local OCR/OHIF readiness copy still had English role/setup/next-action strings.
Solution: Add `home` to the web handoff target map and route it to `#shift`; run query cleanup immediately on mount while preserving the target in a ref for post-hydration reapplication; translate OCR/OHIF bridge setup, roles, warnings and price-photo next actions to Russian; extend source smokes so this does not regress silently.
Rejected Alternatives: Treating `home` as invalid; relying on the default `shift` hash while leaving stale query strings; accepting English technical hints in Settings; changing Telegram URLs to remove `home` instead of making the site consume the contract it already shares with API.
Scalability potential: Low tier gets one deterministic start-screen handoff and no retained query state. Middle/high/ultra can later put an authenticated patient portal home behind the same `home` section without changing bot keyboards.
Hardware Impact: 0 us Unity runtime. Browser impact is one first-load URL parse and one `history.replaceState`; API impact is static response copy only.

Evidence:
- CLI_TEST: `npm run smoke:telegram-handoff-source` passed after requiring `home`, `#shift`, and ref-backed cleanup.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed after adding new Russian required snippets and forbidden English bridge strings.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: main app chunk 635.62 kB > 500 kB.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 checked strings and 0 mojibake hits.
- CLI_TEST: `npm run smoke:telegram-bot` passed for `@dentecrm_bot`.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.shift-hero SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/?dente_source=telegram&dente_section=home&patientId=SHOULD_NOT_SURVIVE#settings` passed; final href `http://127.0.0.1:5173/#shift`, viewport 390, overflow 0.

## 2026-05-23 - Document Payment Selection Persistence

Problem: Tax documents and payment receipts had explicit payment selection, but the chosen ids lived only in React state. Reloading the page lost the tax selection, while the payment receipt effect restored every eligible payment and could undo a doctor's explicit cleared/subset choice.
Solution: Add a dedicated local document-payment selection store keyed by patient/year/payer for tax documents and patient/visit for payment receipts. Hydrate through refs before saving, prune ids against current eligible payments, preserve first-time receipt default-all behavior, and persist empty selections when the operator clears them.
Rejected Alternatives: Saving payment ids in generic UI preferences; keeping effect-level receipt auto-select-all; changing API truth to infer all eligible payments; persisting patient-specific Telegram/query state.
Scalability potential: Low tier keeps a small bounded local store with 80 recent document contexts. Middle/high/ultra can move this to a per-operator server preference table without changing the document payload route or UI contract.
Hardware Impact: 0 us Unity runtime. Browser cost is localStorage parse/write on document-scope changes, bounded to 80 entries and 80 payment ids per entry; no profiler claim.

Evidence:
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed after requiring persistence keys/refs and forbidding the old receipt reset pattern.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: main app chunk 637.56 kB > 500 kB.
- CLI_TEST: `npm run smoke:tax-document-explicit-payment-scope` passed.
- CLI_TEST: `npm run smoke:document-payloads` passed.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.document-list SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#documents` passed with viewport 390, overflow 0.

Residual risk:
- This is local operator convenience, not cross-device server sync.
- First-time payment receipts still default to all active-visit paid payments; after any operator change, the stored subset or empty selection wins.

## 2026-05-23 - Russian STT/OCR/DICOM Operator Copy

Problem: Local bridge and speech gateway readiness still exposed English plan titles and technical mixed-language terms in doctor/admin paths: `fallback`, `chunks`, `transcript`, `cooldown`, `prompt pack`, `server env`, English Vosk warnings, and DICOM import actions.
Solution: Translate the human-facing STT/OCR/DICOM plan titles, warnings and next actions to Russian; replace technical slang with understandable terms: резервная цепочка, аудиофрагменты, локальный текст, пауза из-за лимитов, пакет стоматологических подсказок, серверные переменные.
Rejected Alternatives: Changing contract enum values such as `fallback_text` and `providerSelectionMode: "fallback"`; that would break API/schema compatibility without improving doctor-facing UX. Also rejected leaving this as manual review because these strings surface in settings/readiness flows.
Scalability potential: Low tier keeps concise Russian operator guidance and local/manual recovery. Middle/high/ultra can add richer local bridge providers and admin UI without changing the route schema or provider ids.
Hardware Impact: 0 us Unity runtime. API impact is static response copy only; no hot path, allocation, or frame-time claim.

Evidence:
- CLI_TEST: `npm run smoke:russian-fallback-source` passed with 50 required snippets.
- CLI_TEST: `npm run smoke:ui-preferences` passed; Russian remains the only selectable fallback language.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 checked strings and 0 mojibake hits.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: main app chunk 637.56 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed for `@dentecrm_bot`.
- CLI_TEST: `npm run smoke:schedule-configuration` passed; active check stayed `прием вне окна клиники 10:00-12:00 (Europe/Samara)`.

Residual risk:
- API schema field names and provider ids still use English where they are machine contracts.
- This pass does not add new public patient-portal identity; it removes visible language debt in readiness/actions.

## 2026-05-23 - Telegram QR Target Persistence And QR-First Bot Linking

Problem: The Settings QR generator could create patient/staff link codes, but the selected target mode/staff member was not persisted like other operator preferences. The generated QR card also lacked direct copy/download actions. Bot `/start` and clinic guidance were improved toward QR, but invalid-code handling still pushed patients toward a new manual code instead of QR-first linking.
Solution: Add `telegramLinkSubjectType` and `telegramLinkStaffId` to the shared UI preference schema, web defaults, hydration, autosave and file-backed persistence normalization. Add copy-code, copy-link, copy-share-text and QR SVG download actions to the generated QR card. Change invalid/non-private code replies and bot guidance to tell patients to ask the clinic for QR first, with manual code as fallback. Extend source and behavior smokes so this cannot silently regress.
Rejected Alternatives: Keeping QR target as local component state; storing raw one-time codes or deep links in preferences; making slash commands the primary workflow; adding patient-specific Telegram deep links before authenticated portal identity exists.
Scalability potential: Low tier clinics get one compact QR workflow on a phone/tablet browser. Middle tier can use the same preferences on a shared reception workstation. High/ultra tier can move preferences to per-operator server profiles and add signed portal handoff tokens without changing QR/link-code schema.
Hardware Impact: 0 us Unity runtime. Browser work is bounded to preference JSON writes and click-time clipboard/blob creation. API work is static reply text and existing link-code creation; no hot loop or frame-time claim.

Evidence:
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed after requiring QR target preferences, copy/download actions, and QR-first route text.
- CLI_TEST: `npm run smoke:ui-preferences` passed with 40 required UI preference keys.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: main app chunk 639.90 kB > 500 kB.
- CLI_TEST: `npm run smoke:settings-preferences` passed for server preference persistence.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed for file-backed persistence/reload.
- CLI_TEST: `npm run smoke:telegram-bot` first failed because invalid-code replies were not QR-first; after route text fix and API rebuild it passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed after final Telegram route text change.
- CLI_COMPILE: `npm run build -w @dental/api` passed after final Telegram route text change.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 checked strings and 0 mojibake hits.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed with viewport 390 and overflow 0.

Residual risk:
- Clipboard copy still depends on browser permission/runtime behavior; fallback textarea copy exists.
- QR SVG download is operator convenience, not signed portal identity.
- Public patient portal identity and per-operator cross-device preference ownership remain future product gates.

## 2026-05-24 - Telegram Review/Map Visual Cards And Four Care Callbacks

Problem: Review/map patient replies had inline buttons but still behaved like text-only utility answers while the rest of the patient menu used the configured clinic visual card. The care menu exposed filling and hygiene buttons, but the behavior smoke mainly exercised implant; a button without callback/task/repeat proof can regress silently.
Solution: Add `photoUrl: patientMenuCardPhoto(settings)` to successful `/review` and map callback replies. Extend Telegram smoke to require review/map `sendPhoto`, care menu filling/hygiene buttons, and full create/repeat behavior for `telegram_care_filling_request` and `telegram_care_hygiene_request`. Extend source smoke with filling/hygiene route and workflow-code markers. Update README and `docs/13-dente-telegram-bot-plan.md`.
Rejected Alternatives: Text-only review/map replies; source-only assertions for filling/hygiene; embedding patient or appointment ids in review/map/care buttons; adding a new image pipeline instead of reusing the configured clinic card.
Scalability potential: Low tier clinics get the same compact visual card and two-row button flow. Middle/high/ultra clinics can swap richer per-clinic card imagery through existing settings without route or schema changes. The care workflow stays data-owned by existing communication tasks and stable workflow codes.
Hardware Impact: 0 us Unity runtime. API impact is constant-size reply package construction and Telegram transport mock calls in smoke; no browser hot path or Unity frame-time impact.

Evidence:
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_TEST: `npm run smoke:telegram-bot` first failed on stale `apps/api/dist`, then passed after API rebuild; final post-full-build run passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 checked strings and 0 mojibake hits.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: main app chunk 639.90 kB > 500 kB.

Residual risk:
- The subagent explorer handles from this turn resumed as `pending_init` after the environment transition and were closed without output; this loop used local source/test evidence only.
- Review/map images depend on `welcomeImageUrl` being configured. Without it, the transport remains text message fallback by design.

## 2026-05-24 - Telegram Free-Text Document Requests

Problem: Inline document submenu callbacks created real administrator tasks, but clear patient text such as "нужна справка для налогового вычета", "нужна выписка из медкарты" or "пришлите анкету и согласие" only opened menus. That made the bot feel responsive while the clinic queue could miss real work unless the patient pressed the exact next button.
Solution: Route explicit tax, medical-record and patient-form text intents through `createDenteTelegramDocumentRequest`, preserving the same `telegram_tax_document_request`, `telegram_medical_document_request` and `telegram_patient_forms_request` workflow codes. Keep generic `документы/договор/акт` as the menu path. Generalize task body/audit wording from "pressed a button" to "sent/requested in Telegram".
Rejected Alternatives: Creating separate text-only workflow codes; storing arbitrary patient message text in the task; treating every broad `документы` mention as an administrator handoff. Those paths would either fragment the queue, store unnecessary free-form content, or spam staff when the patient only needs the menu.
Scalability potential: Low tier gets one deduplicated administrator task per topic from either text or button. Middle/high/ultra can add better NLP and per-clinic synonyms without changing workflow ownership, outbox templates, or communication-task actions.
Hardware Impact: 0 us Unity runtime. Node route cost is bounded string-fragment checks and an existing communication task/event mutation when a linked patient asks for a document.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 checked strings and 0 mojibake hits.
- CLI_TEST: `npm run smoke:telegram-bot` initially failed on a time-dependent outbox regex that treated appointment minute `:36` as leaked tooth 36; the guard was narrowed to `лечение 36` / `зуб 36` forms and passed.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: main app chunk 639.90 kB > 500 kB.
- CLI_TEST: final post-build `npm run smoke:telegram-bot` passed with `processedUpdateCount: 32`, `activeChatLinkCount: 1`, `privacyMode: no_phi_by_default`.

Residual risk:
- The free-text intent detector is deterministic fragments, not language-model NLU. It deliberately favors predictable Russian triggers over fuzzy matching that could create false administrator tasks.
- It does not solve the deeper document-signing issue found by audit; issued/signed artifact separation remains a separate document-lifecycle hardening task.

## 2026-05-24 - Document Issue Attestation, Medical Release Journal, Schedule Persistence Proof

Problem: The document lifecycle could issue archived HTML/PDF/XML from a reviewed draft without a durable system fact that the recipient identity was checked, the opened document was reviewed, and both recipient and clinic representative signed/accepted issue. Medical-record copy requests, extracts and release receipts rendered useful text, but there was no structured release journal entry for later registry/export/audit use. Settings persistence proof covered broad preferences, but did not prove doctor/assistant/chair schedule hours or patient preferred appointment windows survived a file-backed reload.
Solution: Add shared issue-attestation schemas and require `signatureAttestation` on `/api/documents/:id/issue`. Store the attestation on the issued document, render it into immutable HTML, expose it through audit-facts, and block PDF/XML export when missing. Add structured release journal entries for medical copy request, medical-record extract, and medical-document release receipt issue paths. Extend the browser issue modal with required signature fields/checks and local default persistence. Extend file-backed settings smoke to save/reload staff, chair and patient schedule configuration.
Rejected Alternatives: Treating printed signature lines as enough; making PDF/XML export infer that the document was signed because it has `issued` status; adding free-text release notes instead of a structured journal object; claiming schedule persistence from source grep or UI-only localStorage checks.
Scalability potential: Low tier clinics get a compact issue modal and one JSON journal entry per relevant document. Middle tier can export the same attestation/journal DTOs to accountant/legal review. High tier can sync them to PostgreSQL audit tables. Ultra tier can add QES/EDS integrations behind the existing signature mode enum without changing document archive ownership.
Hardware Impact: 0 us Unity runtime. API cost is bounded zod validation plus one small stored object per issue. Browser cost is modal state and localStorage persistence for reusable issue defaults. File-backed schedule proof runs only in smoke/test.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-lHUqv54z.js` 645.86 kB > 500 kB.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed.
- CLI_TEST: `npm run smoke:document-legal-confirmations` passed.
- CLI_TEST: `npm run smoke:document-lifecycle` initially rejected missing/incorrect issue body behavior, then passed after the smoke accepted `DocumentIssueValidationFailed` and verified rendered signature attestation.
- CLI_TEST: `npm run smoke:document-issue-chains` passed and verified copy request, extract and release receipt journal kinds.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:tax-document-explicit-payment-scope` passed.
- CLI_TEST: `npm run smoke:tax-certificate-duplicate-issue` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` initially exposed a wrong synthetic patient id and weekday-index assumption in the smoke; after using seeded ids and weekday lookup it passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 checked strings, 3 checked document HTML samples, and 0 mojibake hits.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:ui-preferences` passed.
- CLI_TEST: `npm run smoke:schedule-configuration` passed.

Residual risk:
- This is explicit clinic attestation, not a certified electronic signature provider integration.
- The release journal currently lives on the generated document record; PostgreSQL migration must add matching columns/tables before production DB mode is release-ready.

## 2026-05-24 - Document Issue Persistence, DB Snapshot, Telegram Safe Handoff

Problem: The document issue/release journal fields existed in shared/file-backed runtime and PostgreSQL migration/schema, but Drizzle meta lacked the `0017` snapshot, so future DB generation could miss the actual document columns. Issue signature defaults were also only browser-local while adjacent document settings already used server UI preferences. Telegram portal buttons added safe DENTE params but could retain harmless or unsafe query params already present in the configured portal base URL.
Solution: Generate `apps/api/drizzle/meta/0017_snapshot.json` from the current Drizzle schema, set it as the current `0017` snapshot, and update `smoke:db-runtime-contract` to assert snapshot `jsonb` columns. Add `documentIssueSignatureMode`, `documentIssueStaffFullName`, and `documentIssueStaffRole` to shared UI preferences, web hydration/autosave, API file-backed normalization, and persistence smokes; keep the legacy local signature store as a migration fallback. Clear portal URL query/hash before adding `dente_source=telegram` and `dente_section`, then harden Telegram smoke to require exactly those two params.
Rejected Alternatives: Release-journal table split before the current generated-document DTO requires it; source-only DB checks; storing recipient/patient identity in reusable preferences; trusting preconfigured portal query params from settings.
Scalability potential: Low tier clinics get stable defaults on a shared workstation and safe compact Telegram links. Middle tier can share issuer defaults through file-backed or server settings. High/ultra tier can move document attestation/journal records into richer DB-backed audit tables and add signed portal tokens without changing the current issue/handoff DTO.
Hardware Impact: 0 us Unity runtime. API cost is constant URL normalization and bounded preference JSON parsing. Browser cost is three small preference fields in existing debounce autosave.

Evidence:
- SUBAGENT_AUDIT: DB contract auditor identified missing `apps/api/drizzle/meta/0017_snapshot.json` and recommended snapshot smoke hardening.
- SUBAGENT_AUDIT: UX/bot auditor identified local-only issue signature defaults and inherited Telegram portal query params.
- CLI_TEST: `npm run smoke:ui-preferences` passed with 43 preference keys.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed.
- CLI_TEST: `npm run smoke:db-runtime-contract` passed and now inspects `0017_snapshot.json`.
- CLI_COMPILE: `npm run typecheck` passed for shared, API and web.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-BkIB6jfF.js` 646.74 kB > 500 kB.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed with file reload mode `child_process`.
- CLI_TEST: `npm run smoke:telegram-bot` passed with strict portal query check.
- CLI_TEST: `npm run smoke:document-lifecycle` passed.
- CLI_TEST: `npm run smoke:document-issue-chains` passed.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 strings, 3 document HTML samples, 3 document reasons and 0 mojibake hits.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:tax-document-explicit-payment-scope` passed.
- CLI_TEST: `npm run smoke:document-legal-confirmations` passed.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed with viewport 390 and overflow 0.
- BROWSER_SMOKE: first parallel Documents mobile run timed out because both browser smokes used CDP port 9323; rerun with `SMOKE_CDP_PORT=9324` passed for `#documents`, viewport 390 and overflow 0.

Residual risk:
- The `0017` snapshot is current-schema metadata, while earlier custom migrations `0005` through `0016` still lack individual snapshot files; the DB smoke now protects the release-critical `0017` columns, not full historical Drizzle snapshot hygiene.
- Issue signature mode is a clinic attestation mode, not certified EDS/QES provider integration.

## 2026-05-24 - Onboarding Fallback, Release Source Hashes, Telegram Scoped Config

Problem: Three release-facing seams were still loose. First-run draft dismissal could write a legacy local dismissal before full UI preferences were persisted, so a browser storage failure could hide onboarding without preserving draft-mode recovery. Medical release journal entries carried the right shape but did not force a source hash chain visible to the operator. Telegram multi-bot APIs already supported `organizationId + botConfigId`, but the Settings UI always loaded the unscoped status.
Solution: Move onboarding draft dismissal through a complete `UiPreferences` write before legacy fallback and persist `draftMode` in the fallback object. Add deterministic release-source hashing for medical copy requests and extracts, and make release receipts point to the issued copy-request snapshot hash when available. Add `telegramBotConfigId` to shared UI preferences, API normalization, React autosave and Settings UI; route `clinic_owned_bot` status polling to `/api/telegram/status/:organizationId/:botConfigId` when both ids are known. Extend smokes for onboarding, document issue chains, Telegram callbacks, preference persistence and source guards.
Rejected Alternatives: Keeping onboarding dismissal as a boolean-only local shortcut; allowing null source hashes in release audit data; trusting the unscoped Telegram status route for clinics with more than one bot config; adding token entry to the browser.
Scalability potential: Low tier clinics get reliable onboarding recovery, visible document-release provenance and a simple saved bot-config field. Middle/high tiers can add multiple clinic-owned bots through existing env JSON without changing UI preference ownership. Ultra tier can map release journal hashes and bot configs into dedicated DB/audit tables while preserving the current DTO route.
Hardware Impact: 0 us Unity runtime. API cost is bounded SHA-256 over small issue-time DTOs. Browser cost is one persisted string preference and localStorage/server preference writes already amortized by the UI preference debounce.

Evidence:
- SUBAGENT_AUDIT: onboarding audit found dismissal/fallback ordering risk.
- SUBAGENT_AUDIT: document audit found release-journal source hash visibility gap.
- SUBAGENT_AUDIT: Telegram audit found UI did not consume scoped status config routes.
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed on rerun with longer timeout.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-Dr_xTiKF.js` 648.10 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:ui-preferences` passed with 44 required preference keys.
- CLI_TEST: `npm run smoke:settings-preferences` passed with `telegramBotConfigId`.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:telegram-bot` passed with `processedUpdateCount: 36`.
- CLI_TEST: `npm run smoke:document-issue-chains` passed.
- CLI_TEST: `npm run smoke:document-lifecycle` passed.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed.
- CLI_TEST: `npm run smoke:onboarding-configuration-source` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 strings, 3 document HTML samples, 3 document reasons and 0 mojibake hits.
- CLI_TEST: `npm run smoke:schedule-configuration` passed.
- BROWSER_SMOKE: `#settings/telegram` passed at 390 px width with overflow 0 on `SMOKE_CDP_PORT=9330`.
- BROWSER_SMOKE: `#documents` passed at 390 px width with overflow 0 after correcting the selector to `.documents-panel` and clearing stale smoke processes.

Residual risk:
- `telegramBotConfigId` selects status for an existing server-side config; the browser still cannot and must not provide bot tokens.
- Release journal hashes live on generated document records; a later DB-normalized release registry can index them without changing the current audit DTO.

## 2026-05-24 - Telegram Visual Replies, Russian Speech UI, Patient Forms Lifecycle

Problem: Telegram still had several text-only reply paths after the visual-card work: free-text schedule, help/privacy/clinic/contact, link-code success/reject, and appointment callbacks. The webhook diagnostic response also did not expose the suggested photo/reply markup. Web speech/MPR controls still had visible English technical fragments such as `smart chunks`, `smart chunking`, English MPR plane names, `prompt terms`, `retry`, and `timeout`. Patient form templates were covered by renderer/payload smokes, but the core patient forms did not have a direct API lifecycle proof from create through issue/audit/immutable archive.
Solution: Add `photoUrl` to common Telegram reply paths and expose `suggestedReplyMarkup` / `suggestedPhotoUrl` in the webhook response schema. Replace doctor-facing speech/MPR labels with Russian copy and extend `smoke:ui-preferences` with forbidden old fragments. Add `smoke:patient-forms-lifecycle` for intake questionnaire, personal-data consent, minor/legal representative consent, and photo/video consent; it verifies missing payload blocks, visit-required minor consent block, signature-attestation requirement, issued audit facts, archived HTML download, immutable snapshot reload after patient mutation, and hidden storage paths.
Rejected Alternatives: Keeping reply images only on `/start` and document/care menus; accepting technical English until a full i18n project; relying only on catalog/payload renderer tests for patient forms; adding fake PDF/signature claims to forms without exercising the issue route.
Scalability potential: Low tier clinics get compact photo+inline-keyboard replies and deterministic patient-form lifecycle checks. Middle/high/ultra clinics can swap richer clinic card imagery and later add portal identity or DB-backed form registry without changing the current route contracts.
Hardware Impact: 0 us Unity runtime. API impact is constant-size Telegram payload construction and zod schema fields. Browser impact is text-only React rendering. New patient-form smoke runs in Node/Fastify only.

Evidence:
- SUBAGENT_AUDIT: Telegram audit identified text-only schedule/link/appointment callback gaps and missing webhook suggested visual fields.
- SUBAGENT_AUDIT: Document audit identified that patient forms were renderer-tested but lacked route lifecycle proof.
- SUBAGENT_AUDIT: Frontend audit identified doctor-facing English STT/MPR fragments.
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build -w @dental/shared` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-DOynAqcO.js` 648.37 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed after final build with `processedUpdateCount:37`.
- CLI_TEST: `npm run smoke:patient-forms-lifecycle` passed with four issued patient-form documents.
- CLI_TEST: `npm run smoke:document-payloads` passed.
- CLI_TEST: `npm run smoke:document-lifecycle` first timed out in parallel PDF export, then passed solo.
- CLI_TEST: `npm run smoke:ui-preferences` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 checked strings and 0 mojibake hits.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:schedule-configuration` passed.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- BROWSER_SMOKE: `#documents` passed at 390 px width with overflow 0.
- BROWSER_SMOKE: `#settings/telegram` first timed out during a parallel run, then passed solo at 390 px width with overflow 0.

Residual risk:
- Patient forms now have route lifecycle proof, but certified electronic signature/QES integration remains future work.
- Telegram photos depend on `welcomeImageUrl`/visual card settings. Without a configured image, reply behavior intentionally falls back to text plus inline keyboard.

## 2026-05-24 - Annual Tax Certificate Scope, Preference Backfill, Outbox Lookup

Problem: Three release-facing gaps remained. First, KND/legacy tax certificate duplicate issue was receipt-overlap based, so staff could issue a second same-year certificate for the same patient/taxpayer by selecting a later fiscal receipt. Second, server-loaded `uiPreferences` could be an old partial state blob missing newer required fields, making `/api/settings/preferences` fail during schema parse. Third, Telegram direct outbox send searched only the first 300 generated rows, so a paginated item could be visible but unsendable by id.
Solution: Add annual taxpayer scope matching to duplicate tax certificate detection: organization, patient, tax year, certificate kind and taxpayer identity must be unique until the prior certificate is voided/corrected; receipt/payment overlap remains fallback for older records. Move UI preference defaults into `uiPreferencesSchema` and normalize loaded API state through that schema. Extract full Telegram outbox item generation for id lookup, while paged listing remains capped. Add/extend smokes for annual same-taxpayer duplicate issue, legacy partial preference state reload, and direct send lookup beyond the first page.
Rejected Alternatives: Raising the outbox page cap; relying on browser default preferences; treating same-year new fiscal receipts as separate certificates; deleting older issued tax certificate records; weakening schema validation to accept arbitrary partial objects.
Scalability potential: Low tier clinics avoid wrong duplicate tax paperwork and keep settings after upgrades. Middle/high tiers can grow more UI preference fields without breaking old state files. Ultra tier can move tax certificate uniqueness and outbox lookup to indexed DB tables while preserving the current route contract.
Hardware Impact: 0 us Unity runtime. API overhead is bounded document/payment scope comparison during certificate issue/XML export, one zod normalization during state load, and one generated outbox scan for direct send by id.

Evidence:
- SUBAGENT_AUDIT: bot/schedule audit found outbox direct lookup capped to first 300 rows.
- SUBAGENT_AUDIT: document audit found structured annul/correction workflow is still underspecified; this loop tightened duplicate blocking until that workflow exists.
- SUBAGENT_AUDIT: frontend/onboarding audit found older `uiPreferences` migration gaps.
- CLI_COMPILE: `npm run typecheck` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-CjMyQWLg.js` 648.37 kB > 500 kB.
- CLI_TEST: `npm run smoke:tax-certificate-duplicate-issue` passed with same-taxpayer annual new-payment block 409 and different-taxpayer separate issue true.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:tax-document-explicit-payment-scope` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed with legacy partial UI preference reload.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- CLI_TEST: `npm run smoke:telegram-outbox-lookup` passed with first page limit 300 and total count 323.
- CLI_TEST: `npm run smoke:telegram-bot` passed with `botUsername: dentecrm_bot` and 37 processed updates.
- CLI_TEST: `npm run smoke:telegram-outbox-persistence` passed.
- CLI_TEST: `npm run smoke:onboarding-configuration-source` passed.
- CLI_TEST: `npm run smoke:schedule-configuration` passed.
- CLI_TEST: `npm run smoke:ui-preferences` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with 1843 checked strings and 0 mojibake hits.

Residual risk:
- `/api/documents/:id/void` is still a generic void operation without structured reason/staff/correction reference; the stricter duplicate guard intentionally forces annul/correction to become explicit before a second annual certificate can be issued.
- Telegram due reminder dispatch still depends on manual/admin `/api/telegram/outbox/send-due`; a real server-side bounded worker remains open work.
- Some visible English/mixed frontend labels remain in imaging/source panels per subagent audit; current loop did not touch that UI copy.

## 2026-05-24 - Structured Void, Telegram Due Worker, Russian Imaging Labels

Problem: Three previously recorded release gaps remained open. `/api/documents/:id/void` could void with no durable reason/staff/correction facts, which made annual tax-certificate replacement unsafe. Telegram due reminders still required a manual/admin route, so reminder delivery was not a deployment behavior. Web imaging/source panels still leaked English/raw enum labels in doctor/admin UI.
Solution: Add shared `voidDocumentSchema` and `documentVoidAttestation` fields, persist them in file-backed state and PostgreSQL columns, validate void requests server-side, expose void facts in audit passport, and add a Russian UI confirmation modal before voiding. Update the tax duplicate smoke so same-taxpayer annual duplicates stay blocked until a structured tax-correction void, then prove replacement issue from fresh explicit payment scope. Extract the Telegram send-due batch executor, add an env-gated bounded recursive worker with stop hook, in-flight guard and failed-due retry path. Replace visible imaging/source labels with Russian fallbacks and harden source smokes. Update README, document-generation docs and Telegram bot plan with only implemented behavior.
Rejected Alternatives: Status-only void; deleting or rewriting old certificates; allowing second same-year tax certificates from later receipts; browser-only void confirmation; external cron as the only due-reminder path; unbounded `setInterval`; permanent failed-due replay; broad fragile grep rules or a full i18n rewrite in this loop.
Scalability potential: Low tier clinics get explicit correction facts, manual/admin due-send plus an off-by-default bounded worker, and Russian operator labels. Middle tier can enable worker batches per deployment and page operational queues. High/ultra tier can move void attestations, due retries and dead-letter handling to indexed PostgreSQL/queue workers without changing the current DTO contracts.
Hardware Impact: 0 us Unity runtime. API impact is bounded zod validation, small JSON persistence and limited outbox batch execution only when enabled. Browser impact is modal state and text labels. No frame-time claim.

Evidence:
- SUBAGENT_AUDIT: document audit identified generic void as the blocker for safe correction/replacement.
- SUBAGENT_AUDIT: Telegram audit identified manual-only due reminders and failed due receipt retry risk.
- SUBAGENT_AUDIT: frontend audit identified English/raw imaging enum leakage.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-D-WguNuM.js` 656.64 kB > 500 kB.
- CLI_TEST: `npm run smoke:document-lifecycle` passed with void-without-body rejection and structured void/audit checks.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed with void UI source guards.
- CLI_TEST: `npm run smoke:db-runtime-contract` passed after `0018_document_void_attestation` migration/snapshot and BOM-safe JSON parsing.
- CLI_TEST: `npm run smoke:telegram-due-worker-source` passed.
- CLI_TEST: `npm run smoke:tax-certificate-duplicate-issue` passed with `replacementIssuedAfterStructuredVoid: true`.
- CLI_TEST: `npm run smoke:ui-preferences` passed after false-positive guard fixes and Russian imaging label checks.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:telegram-bot` passed with `botUsername: dentecrm_bot` and `processedUpdateCount: 37`.
- CLI_TEST: `npm run smoke:telegram-outbox-persistence` passed.
- CLI_TEST: `npm run smoke:telegram-outbox-lookup` passed.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- CLI_TEST: `npm run smoke:onboarding-configuration-source` passed.
- CLI_TEST: `npm run smoke:schedule-configuration` passed.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:tax-document-explicit-payment-scope` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `checkedStrings:1843`, `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:patient-forms-lifecycle` passed.
- DOC_CHECK: `README.md`, `docs/12-document-generation-forms.md`, and `docs/13-dente-telegram-bot-plan.md` updated with structured void and due-worker behavior.

Residual risk:
- Void attestation is a durable clinic/operator attestation, not certified EDS/QES.
- The due worker is in-process and off by default. Distributed queue, backoff, rate limits and dead-letter handling remain production gates.
- Vite still reports the web app chunk above 500 kB.

## 2026-05-24 - Medical Document Date Guards and PDF Export Timeout

Problem: Medical document chain dates were normalized through a permissive parser path. A non-empty invalid date such as `2026-02-31` could collapse to `null` and pass the issue chain like a blank optional field. During verification, real PDF export also exposed a hardcoded 30-second Chromium/Edge timeout that can fail on a slow clinic server.
Solution: Replace the comparable chain date parser with a strict `YYYY-MM-DD` prefix parser that round-trips UTC year/month/day. Add issue-time guards for medical copy requests, medical-record extracts and medical-document release receipts, including reversed periods and release access before delivery. Keep draft creation editable but block legal issue until dates are corrected. Add `DENTE_PDF_EXPORT_TIMEOUT_MS` with a bounded 10000-180000 ms range and 60000 ms default for real PDF generation.
Rejected Alternatives: Free-form `Date.parse`; silently treating invalid dates as blank; making all draft creation fail before staff can correct forms; keeping the hardcoded 30-second PDF timeout after a measured smoke failure; replacing the PDF check with a fake artifact.
Scalability potential: Low tier clinics get fewer legally invalid issued copies/extracts/releases and can raise PDF wait on slower servers. Middle/high tiers can keep stricter issue guards while adding richer date pickers. Ultra tier can move date validation into per-form official field mapping and queued PDF workers without changing the current API contract.
Hardware Impact: 0 us Unity runtime. API impact is constant-size string/date checks during issue/source matching. PDF impact is no added steady-state cost; only a longer bounded wait for explicit export calls on slow hardware.

Evidence:
- CLI_COMPILE: `npm run build -w @dental/api` passed after API changes.
- CLI_TEST: `npm run smoke:document-issue-chains` passed with `copyRequestDateGuard`, `releaseReceiptDateGuard` and `extractDateGuard`.
- CLI_TEST: `npm run smoke:document-lifecycle` initially failed with `PDF-экспорт не завершился за 30 секунд`; after configurable timeout it passed with real `%PDF` export.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `checkedStrings:1843`, `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:document-payloads` passed.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-D-WguNuM.js` 656.64 kB > 500 kB.
- DOC_CHECK: `README.md` and `docs/12-document-generation-forms.md` updated with strict medical-chain dates and `DENTE_PDF_EXPORT_TIMEOUT_MS`.

Residual risk:
- This is clinic/operator issue validation, not certified EDS/QES validation.
- PDF export still depends on a configured local Chromium/Edge binary; queue/backoff/worker isolation remains production infrastructure work.
- Vite chunk size warning remains open.

## 2026-05-25 - Outpatient Medical Card 025/u Plan

Problem: DENTE had a generic medical-record extract but no dedicated source-verified `025/u` outpatient medical card. Labeling the extract as official Order 274n output would be false because 025/u has separate card sections for organization header, patient registration/insurance, diagnosis sheets, specialist visit records, dynamic observation, consultations, commissions, hospitalization/surgery/xray dose, lab/functional results and epicrisis.
Solution: Add a dedicated document kind and structured payload for `025/u`; render each implemented section explicitly; block issue unless the payload is built from signed source visits, has one or more clinical tooth rows in a specialist visit record, and confirms Order 274n mapping plus third-party-data review.
Rejected Alternatives: Reusing `medical_record_extract`; emitting a blank scanned PDF shell; auto-filling unknown fields with plausible text; making the card visit-required instead of source-visit-driven; claiming legal electronic exchange without UKEP/MIS/GIS contour.
Scalability potential: Low tier uses one compact payload form for the active signed visit. Middle tier can add multi-visit import into the same arrays. High/ultra tier can add signed PDF/PDF-A, EGISZ/MIS routes and batch card generation without changing the payload owner.
Hardware Impact: 0 us Unity runtime. API work is bounded date/source validation and HTML rendering on document actions only; browser work is one conditionally mounted editor.

## 2026-05-24 - Web Route Render Gating

Problem: The web shell had a 656 kB main chunk warning and, more importantly for actual feel, `App.tsx` rendered most major work surfaces at once as hidden DOM. That meant a doctor opening one view still paid React/DOM construction cost for unrelated panels such as Imaging, Documents, Finance, Communications and Settings.
Solution: Keep the route model but change top-level work surfaces to conditional mounting by `currentView`: Shift, patient cockpit, Imaging, Schedule, Patients, Visit, Documents, Finance, Communications, compliance and Settings. Add `smoke:web-render-gating-source` to require these gates and forbid the previous `hidden={currentView...}` top-level pattern. Update README and UX principles with the implemented behavior.
Rejected Alternatives: Terser minification; it was tested, increased the main chunk from about 656.64 kB to about 658.65 kB, and did not address hidden DOM render cost. Raising Vite `chunkSizeWarningLimit`; it would hide the warning without improving the app. A broad component split in this loop; useful later, but riskier than removing active first-render waste now.
Scalability potential: Low tier clinic PCs and phones avoid building the DICOM/workbench/document/settings DOM until needed. Middle tier keeps the same navigation behavior with less background render work. High/ultra tier should still split `App.tsx` into lazy route modules so the 656 kB main JS warning becomes a real code-split win, not a suppressed warning.
Hardware Impact: 0 us Unity runtime. Browser impact is fewer mounted React elements outside the active route. No microsecond claim without profiler; the proof is source gating plus mobile Edge smoke. The JS payload warning remains factual residual debt.

Evidence:
- CLI_COMPILE: `npm run build -w @dental/web` passed after JSX fixes; Vite still reports `assets/index-DTu950lQ.js` 656.59 kB > 500 kB.
- CLI_TEST: `npm run smoke:web-render-gating-source` passed with `gatedTopLevelSections: 12`.
- CLI_COMPILE: `npm run build` passed for shared, api and web; same Vite chunk warning remains.
- BROWSER_SMOKE: `#documents` at 390 px passed with `.documents-panel`, overflow 0.
- BROWSER_SMOKE: `#settings/telegram` at 390 px passed with `.settings-zone`, overflow 0.
- BROWSER_SMOKE: `#imaging` at 390 px passed with `.imaging-panel`, overflow 0.
- BROWSER_SMOKE: `#shift` at 390 px passed with `.shift-hero`, overflow 0.
- DOC_CHECK: `README.md` and `docs/03-ux-principles.md` updated with route render gating.

Residual risk:
- Main `App.tsx` is still a very large module; conditional mounting improves render/DOM work but does not remove the Vite chunk warning.
- Settings sub-tabs still use intra-settings hidden sections; that is lower priority than top-level route gating but should become conditional tab mounting during a route-module split.

## 2026-05-24 - Visit Workflow Forms Route Lifecycle

Problem: Visit/workflow documents had renderer, guard and catalog coverage, but several real forms did not have a direct API lifecycle proof from create through issue, audit and immutable archive. A future edit could keep the template rendering while breaking actual issue/download behavior for anesthesia, lab, X-ray/CBCT, refusal, warranty, attendance or refund forms.
Solution: Added `scripts/smoke-visit-workflow-forms-lifecycle.mjs` and `smoke:visit-workflow-forms-lifecycle`. The smoke creates eight structured documents through Fastify routes, verifies missing payload 409 blocks, rejects issue without signature attestation, issues with durable attestation, checks audit facts, downloads archived HTML, hides storage paths and proves issued HTML does not re-render after patient mutation.
Rejected Alternatives: Catalog-only proof; UI source grep; exporting PDF for every form in this smoke, because document lifecycle already proves real PDF export and this slice targets route coverage breadth without turning every run into a Chromium batch.
Scalability potential: Low tier clinics get deterministic proof that common visit documents do not regress. Middle tier can add more route cases without changing API contracts. High/ultra tier can later move these issue/archive proofs to DB-backed fixtures and queued PDF exports while preserving the same document truth route.
Hardware Impact: 0 us Unity runtime. API overhead is test-only Fastify route execution over fixture data; product runtime code was not changed. No browser or frame-time claim.

Evidence:
- CLI_TEST: `npm run smoke:visit-workflow-forms-lifecycle` passed before rebuild with eight issued documents.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:30`.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-DTu950lQ.js` 656.59 kB > 500 kB.
- CLI_TEST: `npm run smoke:patient-forms-lifecycle` passed.
- CLI_TEST: `npm run smoke:document-guards` passed.
- CLI_TEST: `npm run smoke:visit-workflow-forms-lifecycle` passed again after rebuild.
- CLI_TEST: `npm run smoke:document-lifecycle` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `checkedStrings:1843`, `mojibakeHits:0`.
- DOC_CHECK: `README.md` and `docs/12-document-generation-forms.md` updated with the visit/workflow lifecycle proof.

Residual risk:
- This is route lifecycle proof, not certified EDS/QES.
- The smoke checks archived HTML and audit facts; it does not batch-export PDF for every workflow form.
- Vite chunk size warning remains open.

## 2026-05-24 - Strict Appointment Datetime Boundary

Problem: Appointment create/update schemas used `Date.parse` as validation. JavaScript accepts impossible or rollover times such as `2027-02-29T10:00:00+04:00` and `2026-05-12T24:00:00+04:00`, then normalizes them to another calendar day before schedule/resource checks. That can move a real patient visit silently.
Solution: Added a strict appointment datetime parser in `packages/shared/src/index.ts`: ISO date, `T`, time, seconds optional, milliseconds optional, explicit `Z` or `+HH:MM` offset, calendar day round-trip, hour 0-23, minute/second 0-59. Rewired create/update appointment schemas to use it. Extended `smoke:schedule-configuration` with route-level invalid calendar and rollover cases.
Rejected Alternatives: Keeping `Date.parse`; UI-only validation; accepting timezone-less browser/local strings in API payloads; adding a hidden correction step that mutates dates silently.
Scalability potential: Low tier clinics avoid corrupt visits without extra UI work. Middle tier can add locale-specific display while keeping the API strict. High/ultra tier can add calendar-provider imports and timezone mapping, but imported values must still normalize before this API boundary.
Hardware Impact: 0 us Unity runtime. API overhead is one fixed regex, numeric range checks, one UTC calendar round-trip and one `Date.parse` after structural validation per supplied appointment datetime.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run build -w @dental/shared` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_TEST: `npm run smoke:schedule-configuration` passed before full build with invalid date and `24:00` route checks.
- CLI_TEST: `npm run smoke:schedule-admin-guard` passed.
- CLI_TEST: `npm run smoke:schedule-autosave-retry` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `checkedStrings:1843`, `mojibakeHits:0`.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-BnM5NB7Z.js` 656.59 kB > 500 kB.
- CLI_TEST: `npm run smoke:schedule-configuration` passed again after full build and docs update.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:30`.
- DOC_CHECK: `README.md` and `docs/03-ux-principles.md` updated with strict appointment datetime boundary.

Residual risk:
- Existing stored appointments are assumed valid; this change blocks future API mutations, it does not migrate historical data.
- API accepts explicit-offset/Z ISO only. External importers must normalize local clinic times before calling schedule mutation routes.
- Vite chunk size warning remains open.

## 2026-05-24 - Document Fact Mojibake Storage Boundary

Problem: The app repaired mojibake mostly near rendering/output, but legal document mutation routes could persist broken legacy CP1252-style Russian text in document payloads, issue signature attestation, void attestation and release journal facts. That would leak into public issue DTOs, audit passports and immutable archived HTML even if the renderer hid some cases.
Solution: Apply `repairMojibakeDeep` on document create payloads, issue signature attestation and void attestation before persistence/snapshot work. Extend `smoke:document-issue-chains` with mojibake recipient/authority/staff/note inputs and assert readable Russian in the issue response, audit facts and archived HTML, with no mojibake markers.
Rejected Alternatives: Render-only repair; browser-only sanitization; broad repair in unrelated API schemas; accepting bad input and relying on `repairMojibakeText` at display time.
Scalability potential: Low tier clinics get clean legal text without extra UI cost. Middle tier can keep adding form kinds behind the same mutation boundary. High/ultra tier can add official document validation and signed export pipelines while preserving the repaired storage contract.
Hardware Impact: 0 us Unity runtime. API cost is bounded recursive string repair only during document mutation routes, not per-frame or schedule polling. No microsecond savings claimed.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_TEST: `npm run smoke:document-issue-chains` passed with mojibake release/signature repair proof.
- CLI_TEST: `npm run smoke:document-lifecycle` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed.
- CLI_TEST: `npm run smoke:documents-catalog` passed.
- CLI_TEST: `npm run smoke:patient-forms-lifecycle` passed.
- CLI_TEST: `npm run smoke:visit-workflow-forms-lifecycle` passed.
- CLI_TEST: `npm run smoke:document-payloads` passed.
- CLI_TEST: `npm run smoke:document-guards` passed.
- CLI_TEST: `npm run smoke:telegram-bot` passed.
- CLI_TEST: `npm run smoke:schedule-configuration` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-BnM5NB7Z.js` 656.59 kB > 500 kB.
- CLI_TEST_AFTER_BUILD: `npm run smoke:document-issue-chains` passed.
- CLI_TEST_AFTER_BUILD: `npm run smoke:telegram-bot` passed.
- CLI_TEST_AFTER_BUILD: `npm run smoke:api-text-encoding` passed.
- DOC_CHECK: `README.md` and `docs/12-document-generation-forms.md` updated with repaired storage-boundary behavior.

Residual risk:
- Historical already-stored documents may need a one-time audit/migration if they were saved before this boundary.
- This still is durable clinic/operator attestation, not certified EDS/QES.
- Vite chunk size warning remains open.

## 2026-05-24 - Telegram Validation Text Boundary

Problem: Telegram malformed-payload handling is an operator/patient-facing edge. The existing validation smoke proved status/error shape and a readable Russian substring, but it did not fail if the response also contained legacy mojibake markers. Generic callback acknowledgement text is another direct Telegram transport string and must not silently regress to broken Russian.
Solution: Strengthened `scripts/smoke-telegram-validation.mjs`: every malformed webhook/control route 400 body is scanned for mojibake markers, admin/webhook secrets remain blocked, and the source fallback for `answerCallbackQuery` must be the readable Russian `DENTE: безопасный ответ отправлен.`. README and `docs/13-dente-telegram-bot-plan.md` now document this proof.
Rejected Alternatives: Broadly rewriting Telegram text generation; source-only grep without route injection; trusting `smoke:telegram-bot` alone, because it exercises many happy flows but can miss generic transport fallback text.
Scalability potential: Low tier clinics get deterministic Russian error text without extra runtime work. Middle tier can add more Telegram control routes behind the same response scan. High/ultra tier can later add per-locale dictionaries, but malformed-route errors must still pass this no-mojibake/no-secret boundary.
Hardware Impact: 0 us Unity runtime. Product runtime code unchanged in this loop; the added cost is smoke-only Fastify response scanning and one source read.

Evidence:
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_TEST: `npm run smoke:telegram-validation` passed.
- CLI_TEST: `npm run smoke:telegram-bot` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-BnM5NB7Z.js` 656.59 kB > 500 kB.

Residual risk:
- This proves malformed Telegram route responses and the generic callback fallback source; it is not a full localization system.
- Telegram Bot API live delivery was not called; transport remains covered by synthetic `smoke:telegram-bot`.
- Vite chunk size warning remains open.

## 2026-05-24 - Document Payload Conditional Mounting

Problem: The Documents screen routed the selected document kind visually, but 27 structured payload editors stayed mounted as hidden `document-payload-card` DOM. That wastes browser work on inactive legal/tax/patient/payment/workflow forms and leaves more inactive controls alive during doctor document creation.
Solution: Replaced the hidden payload-card pattern in `apps/web/src/App.tsx` with `selectedDocumentKind === ... ? (...) : null` conditional mounts for all 27 payload editors. Extended `scripts/smoke-document-payload-ui-source.mjs` so it fails if any payload card returns to `hidden={selectedDocumentKind !== ...}` or if a payload card is not conditionally mounted. README, UX principles, and document-generation docs now record the implemented contract.
Rejected Alternatives: Keeping hidden cards and trusting CSS; moving all document forms to a route-module split in this loop; relying only on screenshot smoke. The chunk split is still needed, but the narrower mount fix removes inactive DOM without touching API or renderer contracts.
Scalability potential: Low tier clinics get less inactive DOM on the Documents screen. Middle tier can add more document kinds behind the same selected-kind mount rule. High/ultra tier should use this as the transition point toward lazy route/module splits without changing document payload schemas.
Hardware Impact: 0 us Unity runtime. Browser improvement is reduced DOM/render work for inactive document editors; no profiler microsecond savings were measured, so no exact frame-time claim is made.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run build -w @dental/web` passed; Vite still reports `assets/index-CvTpyIQo.js` 656.54 kB > 500 kB.
- CLI_COMPILE: `npm run build` passed for shared, api and web; same Vite chunk warning remains.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed with 27 structured kinds and zero hidden payload cards.
- CLI_TEST: `npm run smoke:web-render-gating-source` passed with `gatedTopLevelSections: 12`.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:30`.
- BROWSER_SMOKE: default `npm run smoke:mobile` passed at 390 px with overflow 0.
- BROWSER_SMOKE: `#documents` mobile smoke passed at 390 px with `.documents-panel` visible and overflow 0.
- DOC_CHECK: `README.md`, `docs/03-ux-principles.md`, and `docs/12-document-generation-forms.md` updated with the conditional payload mount rule.

Residual risk:
- The main `App.tsx` bundle remains oversized; conditional mounting reduces runtime DOM work but does not remove the Vite chunk warning.
- Settings still has lower-priority hidden sub-tab sections from prior review.
- No live clinic data or Telegram network call was used in this loop.

## 2026-05-24 - Settings Tab Conditional Mounting

Problem: Settings still kept heavy sub-tab sections mounted behind hidden state. Clinic profile, access policies, Telegram controls, protocol/rule editors, price/source/DICOM/admin import tools, AI recognition and audit panels could all exist in DOM while only one Settings tab was visible. During the change, an over-broad mechanical rewrite also removed required TSX conditional closings, which temporarily broke web typecheck.
Solution: Restored the missing route conditional closings first, then changed Settings sub-tabs to explicit conditional mounting by `settingsTab`. Sources/imports keep the one intentional shared imaging-import route, and DICOM tool-state is gated by both `settingsTab === "sources"` and the existing bundle presence. `smoke:web-render-gating-source` now requires 15 Settings tab gates and forbids tab-level hidden DOM regressions. README and UX principles record the implemented rule.
Rejected Alternatives: Keeping hidden Settings panels; suppressing the Vite chunk warning; accepting the first broad regex rewrite; performing a full App route-module split in this loop. The module split is still valid debt, but this smaller change removes inactive Settings DOM without touching API contracts.
Scalability potential: Low tier clinic PCs and phones avoid building inactive admin panels while opening Settings. Middle tier keeps direct hash links such as `#settings/sources`. High/ultra tier can later lazy-load Settings tab modules without changing the tab URL contract.
Hardware Impact: 0 us Unity runtime. Browser DOM/render work is reduced for inactive Settings tabs, but no exact browser microsecond timing was measured.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed after JSX repair and tab gating.
- CLI_COMPILE: `npm run build -w @dental/web` passed; Vite still reports `assets/index-DuBrS_86.js` 656.51 kB > 500 kB.
- CLI_COMPILE: `npm run build` passed for shared, api and web; same Vite warning remains.
- CLI_TEST: `npm run smoke:web-render-gating-source` passed with `gatedTopLevelSections:12` and `gatedSettingsSections:15`.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed.
- CLI_TEST: `npm run smoke:ui-preferences` passed with `requiredPreferenceCount:44`.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed with child-process reload proof.
- CLI_TEST: `npm run smoke:settings-preferences` passed with saved server UI preferences.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- BROWSER_SMOKE: `#settings/telegram` at 390 px passed with `.telegram-settings`, overflow 0.
- BROWSER_SMOKE: `#settings/imports` at 390 px passed with `.smart-import-studio`, overflow 0.
- BROWSER_SMOKE: `#settings/sources` at 390 px passed with `.dicom-capability-panel`, overflow 0.
- DOC_CHECK: `README.md` and `docs/03-ux-principles.md` updated with Settings tab conditional mounting.

Residual risk:
- Main `App.tsx` remains a large bundle; this loop does not code-split route or Settings modules.
- The smoke proves source shape and three browser Settings routes, not live clinic data or live Telegram network delivery.
- Vite chunk size warning remains open.

## 2026-05-24 - Scoped Telegram Outbox Runtime

Problem: Scoped status and webhook routes already resolved clinic-owned bots by `organizationId` + `botConfigId`, but the outbound outbox path still used the singleton active Telegram settings. In a multi-clinic deployment, the operator could inspect the correct clinic bot status while outbox list/send/send-due still prepared messages with the wrong portal, visual card, readiness and token route.
Solution: Added an outbox runtime scope derived from the same Telegram runtime context. `GET /api/telegram/outbox`, `POST /api/telegram/outbox/:itemId/send`, and `POST /api/telegram/outbox/send-due` now parse `organizationId`/`botConfigId`, pass scoped settings into outbox generation and delivery preparation, and use the scoped bot token for actual send. Settings UI appends the saved clinic bot config scope automatically in `clinic_owned_bot` mode. The control source smoke now requires this propagation; the bot smoke now proves scoped list, due dry-run and real send use the selected bot token, portal host and welcome image.
Rejected Alternatives: Keeping singleton outbox until DB-backed bot configs exist; adding browser token paste; duplicating a separate outbox-only resolver; increasing manual operator steps by requiring per-send config entry.
Scalability potential: Low tier clinics with one bot keep the old simple path. Middle tier can run several clinic-owned bots from `DENTE_TELEGRAM_CLINIC_BOTS_JSON` without shared-token misroutes. High/ultra tier can replace env JSON with encrypted DB-backed `telegram_bot_configs` while preserving the same scoped route/query contract.
Hardware Impact: 0 us Unity runtime. API cost is a bounded request-time runtime config lookup plus existing in-memory outbox filtering. Browser cost is small URLSearchParams construction. No frame-time or microsecond savings are claimed.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed before route-layer smoke.
- CLI_COMPILE: `npm run build` passed for shared, api and web; Vite warning remains: web `assets/index-D8WkVl88.js` 656.82 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed after API build with scoped outbox token/portal/visual-card assertions.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed with scoped outbox query propagation guards.
- CLI_TEST: `npm run smoke:telegram-validation` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:30`.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed at 390 px with overflow 0.
- DOC_CHECK: `README.md` and `docs/13-dente-telegram-bot-plan.md` updated with scoped outbox runtime behavior.

Residual risk:
- `DENTE_TELEGRAM_CLINIC_BOTS_JSON` is still a prototype bridge; production needs encrypted DB-backed bot config storage and tenant auth.
- The due worker default path still uses the default runtime when no scoped query/context is supplied by an operator or future scheduler owner.
- Telegram live network delivery was not called; synthetic transport asserts the selected token appears in the API request URL.

## 2026-05-24 - Immutable KND XML Source And Export Snapshots

Problem: KND 1151156 HTML/PDF issue archives were immutable, but `/api/documents/:id/tax-xml` still had a mutable edge: it could read current patient administrative facts, current clinic profile and current environment tax-office code after the certificate was already issued. That can make a later XML download disagree with the issued certificate trail.
Solution: Added shared `TaxXmlSourceSnapshot` and `TaxXmlSnapshot` schemas, PostgreSQL JSONB columns, sample-state persistence and route logic. Tax certificate issue now freezes patient, clinic profile and selected payment facts for XML. The first successful XML export persists exact XML bytes, SHA-256, source-snapshot hash, tax-office code and created time; later downloads return the archived XML before touching live records or env. Audit facts expose hashes/timestamp, while public document DTOs omit raw snapshots.
Rejected Alternatives: Re-render XML from live records; store only selected payment ids; expose source snapshots to the web client; claim signed FNS output without ТКС/KEP/XSD validation.
Scalability potential: Low tier keeps one bounded JSON snapshot and one XML string per issued certificate. Middle tier can move snapshots to DB/blob storage with the same DTO hashes. High/ultra tier can add queue-based signing/XSD validation without changing issue truth ownership.
Hardware Impact: 0 us Unity runtime. API impact is bounded JSON clone/hash on issue and one SHA-256 over XML on first export; no frame-time claim. Browser impact is only three short audit fields.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run build -w @dental/shared` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_COMPILE: `npm run build -w @dental/web` passed with Vite warning.
- CLI_COMPILE: `npm run build` passed; residual Vite warning: web `assets/index-DEyo19Ro.js` 657.96 kB > 500 kB.
- CLI_TEST: `npm run smoke:db-runtime-contract` passed with migration `0019_document_tax_xml_snapshot`.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed with `nonSelfPatientClinicXmlFrozen:true`, `taxXmlSnapshotFrozen:true`, and `issuedXmlFrozen:true`.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed.
- CLI_TEST: `npm run smoke:document-lifecycle` passed.
- CLI_TEST: `npm run smoke:document-issue-chains` passed.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:30`.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:telegram-bot` passed for `@dentecrm_bot` synthetic flow.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.documents-panel SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#documents` passed at 390 px with overflow 0.
- DOC_CHECK: `README.md` and `docs/12-document-generation-forms.md` updated with implemented XML snapshot behavior.

Residual risk:
- XML remains an unsigned draft export, not a signed ТКС package and not XSD-validated in this loop.
- Existing certificates issued before this change do not have `taxXmlSourceSnapshot`; route returns a repair-oriented 409 instead of guessing from mutable data.
- Vite chunk size warning remains open.

## 2026-05-24 - First-Run Source Configuration

Problem: The first-run `sources` onboarding step described import safety but mostly linked to Settings. A new doctor or clinic could finish setup without actually choosing persistent defaults for price lists, patient migration, document ingestion, imaging, DICOMweb, or OHIF.
Solution: Added an inline source-configuration panel to `apps/web/src/App.tsx` bound to the existing UI preference state and save pipeline. The step now selects price-list source, import source, smart import mode, document ingestion target, imaging source, DICOMweb root, and OHIF root. Stale preview/commit state is cleared when the relevant source choice changes. CSS adds compact responsive cards/chips for the onboarding-only surface. `smoke:onboarding-configuration-source` now guards the source step, setters, autosave wording, and Russian labels.
Rejected Alternatives: Keeping route-only buttons into Settings; adding a second source preference store; saving these choices only on final onboarding completion; storing import data, file paths, DICOM pixels, or patient payloads in UI preferences.
Scalability potential: Low tier clinics get one setup screen with compact chip controls and no extra heavy route. Middle tier keeps the same persisted choices across workstations through `/api/settings/preferences`. High/ultra tier can add DB-backed connector catalogs and clinic-owned adapters while preserving the same preference keys and onboarding contract.
Hardware Impact: 0 us Unity runtime. Browser cost exists only when the onboarding source step is visible. The change adds no clinical payload persistence and no API hot path.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run build -w @dental/web` passed; residual Vite warning: web `assets/index-CU-8Mjes.js` 661.88 kB > 500 kB.
- CLI_COMPILE: `npm run build` passed for shared, api and web; same Vite warning remains.
- CLI_TEST: `npm run smoke:onboarding-configuration-source` passed with `onboarding-sources-persisted-configuration`.
- CLI_TEST: `npm run smoke:ui-preferences` passed with `requiredPreferenceCount:44`.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed with child-process reload proof.
- CLI_TEST: `npm run smoke:settings-preferences` passed with saved server UI preferences.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.onboarding-source-config SMOKE_CLICK_SELECTOR=.onboarding-step-list button:nth-child(6) SMOKE_DISMISS_ONBOARDING=0 npm run smoke:mobile -- http://127.0.0.1:5173/#shift` passed at 390 px with overflow 0.
- DOC_CHECK: `README.md` and `docs/03-ux-principles.md` updated with inline persisted source setup.

Residual risk:
- Full Settings source/import tools still hold the detailed preview/commit workflow; onboarding only sets defaults.
- DICOMweb/OHIF endpoints are saved as operator preferences, not validated live in this step.
- Vite chunk size warning remains open.

## 2026-05-24 - Scoped Telegram Link And Chat Bindings

Problem: Scoped clinic-owned status/webhook/outbox existed, but link-code and chat-link truth was still effectively global inside an organization. A patient code issued for bot A could be attempted through bot B, and outbox readiness could accidentally rely on a chat binding created by another bot config.
Solution: Added `botConfigId` to link-code and chat-link schemas, normalized legacy in-memory records to the current runtime, scoped create/list/consume/revoke paths by `organizationId + clinicId + botConfigId`, propagated webhook scope into document/care/contact/schedule callbacks, and made outbox active-chat lookup plus staff digest generation read only links for the selected bot config. The site now sends the saved scoped runtime for link-code ledgers, chat-link ledgers, revocation and QR/link-code creation.
Rejected Alternatives: Leaving linking global while only outbox sends are scoped; storing browser-side bot tokens; letting `clinicId` overwrite tenant resolution; first-match bot config lookup for multi-bot clinics.
Scalability potential: Low tier keeps the shared `@dentecrm_bot` path. Middle tier can run one clinic-owned bot per clinic. High/ultra tier can run several bot configs per organization and later swap env JSON for encrypted DB-backed `telegram_bot_configs` without changing the route contract.
Hardware Impact: 0 us Unity runtime. API impact is bounded array filtering in prototype ledgers and one extra string comparison by `botConfigId`; browser impact is URLSearchParams/payload fields only.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run build -w @dental/shared` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_COMPILE: `npm run build` passed for shared, api and web; residual Vite warning: web `assets/index-BDxbPAyE.js` 662.01 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed with primary/secondary scoped bot link-code isolation and scoped outbox readiness proof.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed with scoped link/chat/outbox UI source guards.
- CLI_TEST: `npm run smoke:telegram-validation` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- CLI_TEST: `npm run smoke:ui-preferences` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed at 390 px with overflow 0.
- DOC_CHECK: `README.md` and `docs/13-dente-telegram-bot-plan.md` updated with implemented scoped link/chat behavior.

Residual risk:
- Env JSON remains prototype bot-config storage; production still needs encrypted DB storage, tenant auth and bot-secret rotation.
- Existing persisted prototype records without `botConfigId` are normalized to the current runtime on load, not migrated with historical per-bot evidence.
- Vite chunk size warning remains open.

## 2026-05-24 - Telegram Tenant Callback Ownership

Problem: Multi-bot link/chat/outbox scope was fixed, but code issue and callback handoff still had singleton leftovers. `createDenteTelegramLinkCode` validated subjects and clinic against the default sample clinic, blocking a real second tenant. Document, care and contact callbacks then risked creating communication tasks/events/audit in the default organization after a scoped chat link was found.
Solution: Link-code issue now validates patient/staff subjects inside the resolved runtime organization and route-level clinic mismatch is rejected before issue. Telegram tax/document/care/contact handoff helpers now resolve patient, duplicate task lookup, new task, communication event and audit ownership from `chatLink.organizationId`. Smoke creates a second-organization patient, links through a second-clinic bot config, presses `dente:tax`, `dente:care-implant`, and `dente:contact`, and asserts tasks/events/audit stay in that organization while Telegram sends use the tenant bot token.
Rejected Alternatives: Keep second-clinic coverage limited to status/webhook; accept link/outbox scope without handoff task scope; use global `denteTelegramBotSettings.organizationId` as fallback after a scoped chat link is known; disable the second-clinic test after it exposed the old link-code validator.
Scalability potential: Low tier keeps shared bot or one clinic-owned bot with cheap array filters. Middle tier can run several bot configs per organization without cross-bot handoff leaks. High/ultra tier can move the same route contract to encrypted DB-backed bot configs and tenant-authenticated ledgers without changing callback workflow ownership.
Hardware Impact: 0 us Unity runtime. Node API cost is one organization string comparison in subject validation and handoff duplicate lookup; browser impact is none.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_COMPILE: `npm run build` passed for shared, api and web; residual Vite warning: web `assets/index-BDxbPAyE.js` 662.01 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed with second-clinic link-code creation plus scoped tax/care/contact callback ownership proof.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed with source guards for runtime subject validation and callback organization scope.
- CLI_TEST: `npm run smoke:telegram-validation` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- DOC_CHECK: `README.md` and `docs/13-dente-telegram-bot-plan.md` updated with scoped subject/clinic validation and linked-chat handoff ownership.

Residual risk:
- `DENTE_TELEGRAM_CLINIC_BOTS_JSON` remains prototype storage; production still needs encrypted DB-backed bot configs, tenant auth and webhook-secret rotation.
- Appointment callback HMAC still uses the global organization secret input; runtime chat-link scope blocks unauthorized use, but cross-bot replay should get a dedicated signature scope in a later loop.
- Vite chunk size warning remains open.

## 2026-05-25 - Telegram Appointment Callback Scope

Problem: Appointment inline-button signatures were compact and time-limited, but the HMAC input did not include `clinicId` or `botConfigId`. After multi-bot chat-link support, the same patient/chat could be legitimately linked to two clinic-owned bot configs; a primary-bot appointment button needed an explicit rejection path when replayed through the secondary webhook.
Solution: Appointment callback signing and verification now normalize `organizationId`, `clinicId` and `botConfigId` from the runtime settings, then HMAC `organizationId:clinicId:botConfigId:appointmentId:action:expiry`. Webhook callback handling passes the resolved `clinicId`; outbox and linked schedule markup sign buttons with the same runtime scope. The bot smoke creates primary and secondary bot links for the same patient/chat, sends a primary appointment confirmation button, replays it through the secondary webhook, and asserts rejection with no appointment/task/event/audit mutation.
Rejected Alternatives: Rely only on active chat-link lookup; embed bot config ids directly in callback data; make appointment callbacks global and check transport token later; accept source-only proof. Chat-link lookup is not a signature boundary, explicit bot ids in callback data waste Telegram's 64-byte limit and expose topology, and token-route checks do not prove callback replay behavior.
Scalability potential: Low tier uses the shared bot path with the same signing contract. Middle tier can run one clinic-owned bot per clinic. High/ultra tier can run multiple bot configs per organization and move bot configs to encrypted DB storage without changing the callback verifier.
Hardware Impact: 0 us Unity runtime. API impact is one slightly longer HMAC input string per appointment button generation/verification; browser impact is none. Low-end clinic hardware sees no extra web rendering cost.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_COMPILE: `npm run build` passed for shared, api and web; residual Vite warning: web `assets/index-BDxbPAyE.js` 662.01 kB > 500 kB.
- CLI_TEST: `npm run smoke:telegram-bot` passed with cross-bot appointment callback replay rejection and unchanged appointment/task/event/audit counts.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed with scoped appointment callback source guards.
- CLI_TEST: `npm run smoke:telegram-validation` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:settings-persistence-file` passed.
- CLI_TEST: `npm run smoke:settings-preferences` passed.
- DOC_CHECK: `README.md` and `docs/13-dente-telegram-bot-plan.md` updated with scoped appointment callback behavior.

Residual risk:
- Existing pre-deploy appointment buttons signed with the older input will reject after deploy; patients can request `/schedule` or wait for the next reminder to get fresh scoped buttons.
- `DENTE_TELEGRAM_CLINIC_BOTS_JSON` remains prototype storage; production still needs encrypted DB-backed bot configs, tenant auth and webhook-secret rotation.
- Official outpatient medical-card form `025/у` mapping still needs source-verified field implementation before DENTE can claim exact 274n outpatient card generation.
- Vite chunk size warning remains open.

## 2026-05-25 - Outpatient Medical Card 025/u

Problem: DENTE could generate medical-record extracts/copy/release documents, but it could not generate a dedicated outpatient medical card 025/у from Health Ministry Order N 274n. Calling the generic extract an official outpatient card would be false, and issuing a card from unsigned visit notes would create a bad legal trail.
Solution: Added `outpatient_medical_card_025u` as a first-class shared document kind, DB enum migration, structured payload schema, server render template, missing-payload guard, signed-source/date/period issue blockers, clinical-tooth-row blocker, doctor-facing payload editor, Telegram/Communications workflow entry and documentation. The renderer uses clinic legal facts, patient administrative facts, final diagnoses, signed specialist visit records, clinical tooth rows, observations/events/X-ray dose sections, final epicrisis and explicit operator confirmations. Official anchor is `https://publication.pravo.gov.ru/document/0001202505300033`; DENTE still does not claim ЕГИСЗ/MIS electronic exchange or УКЭП signing for 025/у.
Rejected Alternatives: Reusing `medical_record_extract`; free-text 025/у payload; allowing draft issue without signed source visits; storing Telegram-issued medical facts; claiming official electronic medical-card storage from HTML/PDF output.
Scalability potential: Low tier uses one compact visible payload editor and server-rendered HTML/PDF. Middle tier can add more 274n forms with the same typed payload/source/blocker pattern. High/ultra tier can add signed electronic exchange, XSD/ЕГИСЗ integration and DB-backed source snapshots without changing document kind ownership.
Hardware Impact: 0 us Unity runtime. API cost is bounded validation/render over selected signed visits and payload rows. Browser cost is limited to the selected Documents payload editor because inactive editors are not mounted. Mobile Documents smoke passed at 390 px with no horizontal overflow.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run build` passed for shared, api and web; residual Vite warning: web `assets/index-CwFm35PI.js` 676.25 kB > 500 kB.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:31` including `outpatient_medical_card_025u`.
- CLI_TEST: `npm run smoke:document-payloads` passed and checked `outpatient_medical_card_025u`.
- CLI_TEST: `npm run smoke:document-guards` passed with missing-payload 025/у guard.
- CLI_TEST: `npm run smoke:document-issue-chains` passed with `outpatient025uSignedSourceGuard`, `outpatient025uDateGuard` and `outpatient025uSourcePeriodGuard`.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed with conditional 025/у payload editor source guards.
- CLI_TEST: `npm run smoke:document-legal-confirmations` passed with `confirmedLiteralFields:65`.
- CLI_TEST: `npm run smoke:telegram-bot` passed for `@dentecrm_bot` synthetic flow.
- CLI_TEST: `npm run smoke:telegram-control-ui-source` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- CLI_TEST: `npm run smoke:db-runtime-contract` passed.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.documents-panel SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#documents` passed at 390 px with overflow 0.
- DOC_CHECK: `README.md`, `docs/12-document-generation-forms.md`, and `docs/13-dente-telegram-bot-plan.md` updated with implemented 025/у behavior and limitations.

Residual risk:
- 025/у electronic exchange remains out of scope until DENTE has a real MIS/ЕГИСЗ/УКЭП contour.
- Additional Order N 274n forms beyond 025/у still need separate typed payloads and blockers.
- Web bundle chunk warning increased to 676.25 kB and remains open.

## 2026-05-25 - Outpatient 025/u Local Draft Recovery

Problem: The 025/u payload editor is long enough that a browser reload or accidental navigation could destroy real operator work before issue. At the same time, storing medical free text in global UI preferences would turn preference storage into a hidden clinical data cache. The KND XML documentation also overclaimed XSD readiness where code only shapes fields and does not validate against a real external schema.
Solution: Added a scoped local draft store for `outpatient_medical_card_025u` keyed by document kind, patient and visit. The store hydrates only 025/u editor fields, caps entries to 60, and resets signed-source/274n/third-party confirmations on hydrate. Global UI preference smoke now rejects 025/u clinical fields. Documentation now states KND XML is shaped to published fields and still needs external XSD/EDO validation before official submission.
Rejected Alternatives: Saving clinical draft text in global UI preferences; auto-persisting legal confirmations; claiming XSD compliance without a validator; forcing operators to retype long 025/u payloads after refresh.
Scalability potential: Low tier uses cheap browser-local recovery for one doctor workstation. Middle tier can move the same scoped draft contract to encrypted server drafts per tenant/user. High/ultra tier can add MIS/EGISZ/EDO source snapshots, XSD validation and signed exchange without changing document-kind ownership.
Hardware Impact: 0 us Unity runtime. Browser impact is one bounded localStorage JSON read/write for active 025/u only; inactive document editors remain unmounted. Low-end clinic PCs avoid re-rendering all forms and avoid retyping losses.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/web` passed.
- CLI_COMPILE: `npm run build -w @dental/web` passed; residual Vite warning: `assets/index-DlM45n0F.js` 683.60 kB > 500 kB.
- CLI_COMPILE: `npm run build` passed for shared, api and web; same residual Vite warning.
- CLI_TEST: `npm run smoke:document-payload-ui-source` passed with draft-source guards.
- CLI_TEST: `npm run smoke:ui-preferences` passed with `forbiddenClinicalKeyCount:16`.
- CLI_TEST: `npm run smoke:document-payloads` passed.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:31`.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- BROWSER_SMOKE: `SMOKE_SELECTOR=.documents-panel SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#documents` passed at 390 px with overflow 0.
- BROWSER_SMOKE: CDP reload check selected 025/u, typed `TEST-025-DRAFT`, verified `dental-crm:document-payload-drafts:v1`, reloaded and confirmed restored value with overflow 0.
- DOC_CHECK: `README.md`, `docs/12-document-generation-forms.md`, and `docs/03-ux-principles.md` updated.

Residual risk:
- Local drafts are browser-local recovery, not encrypted tenant-synced clinical storage.
- KND XML still lacks real XSD validator/source checksum automation.
- Official source URL checks are static; fetch/checksum evidence is still missing.
- Web chunk warning remains open at 683.60 kB.

## 2026-05-25 - FNS KND 1151156 Source Pinning

Problem: KND 1151156 XML generation had an internal structural preflight, but official FNS source attachments were only loose URLs in metadata/docs. If the FNS page changed, DENTE had no local proof of which appendices and XSD the implementation was checked against.
Solution: Rechecked the official FNS Order EA-7-11/824@ page. Added `docs/legal-sources/fns-knd-1151156.json` with appendices 1-4 and `UT_SVOPLMEDUSL_1_278_00_05_01_02.xsd`, including URL, byte size and SHA-256. Added the XSD URL to `tax_deduction_certificate.sourceUrls`, added `smoke:official-document-sources`, and documented the source-pinning boundary.
Rejected Alternatives: Treating the order page URL alone as enough; fetching the network in every default smoke; claiming the pinned XSD equals official validation; adding a generic XML validator without proving the FNS schema.
Scalability potential: Low tier gets deterministic offline source proof. Middle tier can add periodic admin source-refresh checks. High/ultra tier can add real XSD validation, KEP signing, EDO/TKS submission and receipt lifecycle under the same source manifest boundary.
Hardware Impact: 0 us Unity runtime. API/web runtime cost is one extra metadata URL only. The new smoke runs cold in Node and does not touch the app hot path.

Evidence:
- OFFICIAL_SOURCE: FNS order page `https://www.nalog.gov.ru/rn77/about_fts/docs/14112883/` showed publication date 2023-12-05, document date 2023-11-08, KND 1151156, page update 2026-05-25, appendices 1-4 and XSD.
- SOURCE_HASH: `pril1_14112883.pdf` SHA-256 `520bee5e688f6dc1da4c8edf109e07409a90fd9791af999a9d551fc7824500d2`.
- SOURCE_HASH: `pril2_14112883.docx` SHA-256 `32543ac7f100184de3d27d6632b48f77b5c9cd0b2d91db6ed81bd1dfb9aa0938`.
- SOURCE_HASH: `pril3_14112883.doc` SHA-256 `c850f344d213711dfe40e12a8d3e41c3f9dbf1bab9f07eb080d01ab76d4ae6b9`.
- SOURCE_HASH: `pril4_14112883.docx` SHA-256 `18ab0c72674998feda85427aa44ee7aaeb81e3f66ff089f816c07b12304db0c7`.
- SOURCE_HASH: `UT_SVOPLMEDUSL_1_278_00_05_01_02.xsd` SHA-256 `c6f4b26841436853add552324a690c8cee0d9f66072d750cb502098839a1ec83`.
- CLI_COMPILE: `npm run typecheck -w @dental/shared` passed.
- CLI_COMPILE: `npm run build -w @dental/shared` passed.
- CLI_COMPILE: `npm run build` passed for shared, api and web; residual Vite warning: web `assets/index-D68nz3qQ.js` 683.60 kB > 500 kB.
- CLI_TEST: `npm run smoke:official-document-sources` passed with `attachmentCount:5` and pinned XSD SHA-256.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:31`.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:document-payloads` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.

Residual risk:
- Source pinning is not official XSD validation.
- No KEP signature, EDO/TKS submission, operator protocol or FNS receipt lifecycle yet.
- Default smoke does not live-fetch FNS; the manifest must be refreshed deliberately when legal/source updates are reviewed.
- Web chunk warning remains open at 683.60 kB.

## 2026-05-25 - KND XML Structural Preflight

Problem: KND 1151156 XML export was frozen and documented as a draft, but the API returned bytes for archival without its own final structural preflight. A malformed template regression could be snapshotted before an external operator/XSD contour ever sees it.
Solution: Added `validateKnd1151156XmlDraft` inside `apps/api/src/documents/taxXml.ts`. It performs bounded string checks for XML declaration, `Файл`/`Документ`/`СведРасхУсл` tag balance, `Документ/@КНД="1184043"`, `ВерсФорм="5.01"`, `КодНО`, `ОтчГод`, `НомерСвед` length, `НомКорр="0"`, `ПрПациент`, payer/patient node rules, service-code sums, technical placeholder tokens and mojibake markers before the first XML snapshot can be stored.
Rejected Alternatives: Claiming official XSD validation without loading the FNS XSD; adding a generic XML dependency that still would not prove the FNS schema; delaying all checks to an external EDO operator and allowing DENTE to archive obviously broken XML.
Scalability potential: Low tier gets cheap deterministic preflight on manual export. Middle tier can wire the same failure surface into an operator queue. High/ultra tier can add real FNS XSD validation, source checksum pinning, KEP signing and EDO submission without changing the issued-document snapshot route.
Hardware Impact: 0 us Unity runtime. API cost is one bounded pass over a single generated XML string during manual tax XML export; no hot path, no browser cost, no added dependency.

Evidence:
- CLI_COMPILE: `npm run typecheck -w @dental/api` passed.
- CLI_COMPILE: `npm run build -w @dental/api` passed.
- CLI_COMPILE: `npm run build` passed for shared, api and web; residual Vite warning: web `assets/index-DlM45n0F.js` 683.60 kB > 500 kB.
- CLI_TEST: `npm run smoke:tax-knd-xml` passed after full build with preflight source guards and self/non-self XML structural assertions.
- CLI_TEST: `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- CLI_TEST: `npm run smoke:documents-catalog` passed with `renderedCount:31`.
- CLI_TEST: `npm run smoke:document-payloads` passed.
- CLI_TEST: `npm run smoke:russian-fallback-source` passed.
- DOC_CHECK: `README.md` and `docs/12-document-generation-forms.md` now state internal preflight plus remaining external XSD/ЭДО/КЭП requirement.
- SOURCE_CHECK: Official FNS source page for Order N ЕА-7-11/824@ still exposes KND 1151156 and attached XSD as of the checked page update; DENTE did not implement that XSD validator in this loop.

Residual risk:
- This is DENTE structural preflight, not official XSD validation.
- No FNS XSD download/checksum pinning yet.
- No KEP signature, ТКС/ЭДО submission, operator protocol or receipt lifecycle yet.
- Web chunk warning remains open at 683.60 kB.

## 2026-05-25 - FNS KND 1151156 Source Pinning - Bottom Index

Problem: Rationale for source pinning is recorded above after the prior official-source residual-risk block. This bottom index exists so the latest DENTE loop is visible from tail reads.
Solution: Loop 41 pinned FNS Order EA-7-11/824@ appendices 1-4 and XSD in `docs/legal-sources/fns-knd-1151156.json`, added the XSD metadata URL, and added `smoke:official-document-sources`.
Rejected Alternatives: Duplicating the full rationale block here; treating the source manifest as real XSD validation.
Scalability potential: Same as Loop 41 source pinning above.
Hardware Impact: 0 us Unity runtime; cold source/docs/smoke work only.

# DENTE Agent Log

## 2026-05-23

What was wrong:
- Telegram review/map replies ended after URL buttons instead of keeping patient next actions.
- Patient forms request omitted `medical_intervention_refusal` from quick UI/document task actions.
- Telegram control UI showed weak source states: raw link-code statuses, no visual-card thumbnail preview, and hidden outbox warnings/block reasons.
- Schedule defaults could overwrite server-loaded doctor/assistant/chair selections during first-open seeding.
- Chair load switched to chair working hours, but load math counted appointments outside the active shift date and could exceed the `ResourceLoad.utilizationPercent <= 200` schema.
- Clinic-owned Telegram bot config was documented as future-only despite existing server-env path being feasible.

What was done:
- Added inline next-action keyboards after Telegram review/map callbacks.
- Added refusal form support to patient forms quick workflow labels.
- Added Russian fallback labels for Telegram statuses, feature-disabled messages, blocked reasons, and link-code states.
- Added visual-card image previews and outbox warning/block reason rendering.
- Added first-open appointment draft guard so saved schedule defaults seed until the user edits the draft.
- Added server-only clinic-owned Telegram bot env/JSON smoke coverage.
- Added active-date resource load filtering and capped displayed utilization to schema range while preserving factual booked minutes.
- Updated Telegram plan docs and README.

Cinematic Cheats used:
- Bounded visual-card preview: browser renders compact image thumbnail instead of building a separate rich preview surface.
- Bounded utilization meter: UI keeps 0-200 contract and exposes true overload through minutes/flags.

Exact Microseconds saved:
- 0 us claimed. No profiler artifact. This was product correctness and UX hardening, not measured performance work.

Evidence:
- `npm run typecheck`
- `npm run build`
- `npm run smoke:schedule-configuration`
- `npm run smoke:telegram-bot`
- `npm run smoke:settings-persistence-file`
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:document-payload-ui-source`
- `npm run smoke:settings-preferences`
- `npm run smoke:russian-fallback-source`
- `npm run smoke:api-text-encoding`
- `npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` with `.telegram-settings`, 390px viewport, overflow 0.

Residual risk:
- Screenshot capture mode in `smoke-mobile-overflow.mjs` hung in headless Edge; overflow smoke without screenshot passed.
- Vite still warns that `assets/index-*.js` is above 500 kB after minification.

## 2026-05-23 - Fiscal Documents And Schedule Follow-up

What was wrong:
- Tax application payload could use all eligible fiscal payments instead of the checkboxes explicitly selected by the operator.
- Tax selection `useEffect` silently selected every eligible payment again after year/payer/form changes.
- Payment receipt UI could derive payer birth date, INN, or identity document from the patient card instead of the selected payment ledger.
- API receipt guard accepted selected payments without enough stored payer facts for tax-support receipts.
- Tax payer INN comparison used raw trimmed strings, so formatted operator input could fail against unformatted ledger data.
- Schedule gap/buffer hints compared adjacent appointments across clinic-local dates.

What was done:
- Unified tax application/certificate/registry selected-payment payload source through `selectedTaxPaymentIdsForCurrentDocument()`.
- Changed tax payment selection refresh to prune invalid ids only; explicit `Все` remains as a visible button action.
- Removed patient-card fallbacks from payment receipt payer fields.
- Added API guard checks for missing stored payer full name and tax-support payer facts on selected payments.
- Normalized INN comparisons to digits in document guards and tax payment snapshots.
- Added same-clinic-date guard before schedule gap/buffer suggestion creation.
- Added smoke regressions for explicit tax payment scope, INN formatting, missing receipt payer facts, and cross-date schedule suggestions.
- Updated architecture/UX docs with explicit fiscal selection, payer-fact ownership, and same-day schedule hint rules.

Cinematic Cheats used:
- Deterministic source-of-truth narrowing: operator-selected ids and payment ledger facts replace UI guesswork.
- Same-day schedule hinting: cheap date key comparison prevents false cross-midnight “gap/buffer” noise.

Exact Microseconds saved:
- 0 us claimed. No profiler artifact. Runtime impact is bounded list filtering over visible payments and adjacent schedule rows.

Evidence:
- `npm run typecheck`
- `npm run build`
- `npm run smoke:schedule-configuration`
- `npm run smoke:document-payload-ui-source`
- `npm run smoke:document-guards`
- `npm run smoke:tax-document-explicit-payment-scope`
- `npm run smoke:tax-registry-fiscal`
- `npm run smoke:tax-knd-xml`
- `npm run smoke:tax-payment-explicit-payer`
- `npm run smoke:document-payloads`
- `npm run smoke:api-text-encoding`
- `npm run smoke:telegram-bot`
- `npm run smoke:settings-persistence-file`
- `npm run smoke:settings-preferences`
- `npm run smoke:onboarding-configuration-source`
- `npm run smoke:schedule-autosave-retry`
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:russian-fallback-source`

Residual risk:
- Big multi-clinic Telegram runtime still needs per-organization settings/runtime resolver instead of one active runtime context.
- Vite still warns that main app chunk is above 500 kB after minification.

## 2026-05-23 - Telegram Outbox Paging And First-Run Readiness

What was wrong:
- `GET /api/telegram/outbox` returned one local slice and computed ready/due/blocked counts from that slice, not from the real queue.
- Settings filtered Telegram outbox items in the browser and hid everything after a short UI slice, which would feel wrong for real reminder batches.
- Bulk due-send selected from the same unfiltered page instead of asking the queue for due items.
- First-run setup checked that a doctor/chair existed but did not block on the active first appointment's team/schedule readiness.
- The Telegram smoke had a time-dependent false positive because a generated appointment minute could contain `36`, the same string used as a tooth-number leak sentinel.

What was done:
- Added outbox query options for `status`, `templateKind`, `limit`, and `cursor`.
- Added `totalCount`, `filteredCount`, `limit`, `cursor`, and `nextCursor` to the shared outbox response schema.
- Moved outbox status/template filtering and count-before-page calculation into the API.
- Updated due-send to consume the server due queue.
- Updated Settings Telegram tab to request server-filtered pages and provide `Показать еще` through `nextCursor`.
- Added onboarding completion blockers from active appointment `team` and `schedule` readiness checks.
- Made the Telegram smoke appointment time deterministic enough not to collide with the tooth-number sentinel.
- Updated README, UX principles, and Telegram bot plan.

Cinematic Cheats used:
- Cursor paging is a cheap queue viewport instead of building a heavy real-time queue UI.
- Onboarding reuses the schedule readiness DTO instead of duplicating schedule math in React.

Exact Microseconds saved:
- 0 us claimed. No profiler artifact. This is correctness/scalability work over bounded Node arrays and React state.

Evidence:
- `npm run typecheck`
- `npm run build`
- `npm run smoke:telegram-bot`
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:onboarding-configuration-source`
- `npm run smoke:schedule-configuration`

Residual risk:
- Link-code/chat-link lists still use fixed small latest lists; they need the same status/subject/cursor contract next.
- Multi-clinic Telegram runtime still uses one active settings object; full per-organization settings/runtime resolver remains open.
- Vite still warns that main app chunk is above 500 kB after minification.

## 2026-05-23 - Telegram Link/Chat Ledger Paging And Document Audit

What was wrong:
- `GET /api/telegram/link-codes` and `GET /api/telegram/chat-links` returned fixed small latest lists while outbox already had server-owned paging.
- Settings could not page through real clinic connection history or see reliable filtered totals for pending/used/expired/revoked codes and active/revoked chat links.
- The bot/site work still had a documented release-risk gap: document catalog and tax smokes pass, but final official output still needs real PDF export and signed/XSD-validated tax XML before calling the document stack release-complete.

What was done:
- Added shared link-code and chat-link list response schemas with `totalCount`, `filteredCount`, `limit`, `cursor`, `nextCursor`, and operational counters.
- Added API query parsing for `status`, `subjectType`, `subjectId`, `limit`, and `cursor`.
- Added server-side filtered ledger builders for link codes and chat links with public-only DTO output.
- Updated Settings Telegram tab to store ledger metadata, show shown/filtered counts, and load more link codes/chat links by cursor.
- Added Telegram smoke regressions for link-code/chat-link paging, filters, counters, and cursor fields.
- Updated Telegram control UI source smoke to guard the new API/UI contract.
- Updated Telegram plan docs and README with the paged ledger contract.
- Ran subagent audits for Telegram multi-clinic/runtime risks and document/forms readiness; recorded remaining release gaps instead of hiding them.

Cinematic Cheats used:
- Cursor ledger viewport: small deterministic pages and counters instead of heavy live tables.
- Public DTO narrowing: Settings gets operational facts without leaking chat transport storage references.

Exact Microseconds saved:
- 0 us claimed. No profiler artifact. This is product correctness and scalable UX over bounded Node arrays and React state.

Evidence:
- `npm run typecheck`
- `npm run build`
- `npm run smoke:telegram-bot`
- `npm run smoke:telegram-control-ui-source`

Residual risk:
- Multi-clinic Telegram runtime still resolves one active settings object; DB-backed per-organization bot runtime is the next hard blocker.
- Outbox cursor is still offset-based over regenerated queue; stable id/time cursor should replace it before high-volume production.
- Document release gap remains: PDF export for issued snapshots, signed/XSD-validated FNS XML, and privileged issued-facts/audit endpoint.
- Vite still warns that main app chunk is above 500 kB after minification.

## 2026-05-23 - Scoped Multi-Clinic Telegram Runtime

What was wrong:
- The Telegram runtime still behaved like a singleton in the important path: one deployment could not safely expose a clinic-specific status/webhook route for a second clinic-owned bot.
- `DENTE_TELEGRAM_CLINIC_BOTS_JSON` could describe per-clinic token material, but webhook/status resolution did not accept a stable `botConfigId` route.
- If one organization had multiple bot configs, first-match fallback could route a webhook through the wrong token/secret.

What was done:
- Added server-only runtime config parsing for `organizationId`, `clinicId`, `botConfigId`, `botUsername`, `botToken`, `webhookSecret`, `webhookBaseUrl`, `patientPortalBaseUrl`, `welcomeImageUrl`, `clinicReviewUrl`, and `clinicMapsUrl`.
- Added scoped status routes: `/api/telegram/status/:organizationId` and `/api/telegram/status/:organizationId/:botConfigId`.
- Added scoped webhook route: `/api/telegram/webhook/:organizationId/:botConfigId`.
- Made runtime context prefer the exact matched env config token/secret instead of re-resolving by organization only.
- Made same-organization multi-bot configs fail closed on `organizationId`-only status/webhook routes.
- Added Telegram smoke coverage for two bots in the same organization: requested `botConfigId` must use its own token, must not use the first config token, and ambiguous routes must return 404.
- Updated README and Telegram plan docs.

Cinematic Cheats used:
- Server-only JSON runtime bridge: enough to operate multiple clinic-owned bots now without building a premature database runtime layer.
- Fail-closed ambiguity guard: cheap deterministic branch instead of runtime guessing.

Exact Microseconds saved:
- 0 us claimed. No profiler artifact. This is API correctness and multi-tenant routing work over small env-config arrays.

Evidence:
- `npm run typecheck`
- `npm run build`
- `npm run smoke:telegram-bot`
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:telegram-validation`
- `npm run smoke:settings-persistence-file`

Residual risk:
- Full production still needs DB-backed per-organization bot runtime, not env JSON.
- Link codes, chat links, outbox, and webhook event ledgers still need full per-clinic DB ownership beyond the current sample-data bridge.
- Document release gap remains: real PDF export for issued snapshots and signed/XSD-validated FNS XML; no fake PDF was added.
- Vite still warns that main app chunk is above 500 kB after minification.

## 2026-05-23 - Document Issue Passport And Archived HTML Download

What was wrong:
- The API already froze issued HTML snapshots, but the product surface did not expose a document passport with source authority, blockers, warnings, archive status, and exact SHA-256.
- Operators had only `Открыть`; there was no explicit archive download action for the verified issued snapshot.
- The document release gap needed a hard boundary: current output is verified HTML plus draft XML data, not a real PDF or signed FNS package.

What was done:
- Added shared `DocumentAuditFacts` schema and `/api/documents/:id/audit-facts`.
- Added `/api/documents/:id/html?download=1` attachment behavior for issued or later-voided issued documents.
- Added Documents UI state and actions for `Паспорт`, `Скачать HTML`, source authority/reference, source status, snapshot SHA-256, blockers and warnings.
- Kept KND XML labeled as draft and documented that signature, FNS transport and XSD validation remain separate release work.
- Extended lifecycle smoke to assert audit facts, matching snapshot hash, no `storagePath`, draft no-download behavior, voided warning, and HTML attachment body equality.
- Updated document UI source smoke, document-generation docs, and README.

Cinematic Cheats used:
- Issue passport panel: one compact facts surface instead of a heavy document management screen.
- Verified HTML archive: uses the already frozen snapshot as the release artifact until a real PDF/signature contour exists.

Exact Microseconds saved:
- 0 us claimed. No profiler artifact. This is correctness/UX work on API routes, React state, and bounded Node smoke tests.

Evidence:
- `npm run typecheck`
- `npm run build`
- `npm run smoke:document-lifecycle`
- `npm run smoke:document-payload-ui-source`
- `npm run smoke:document-html-issue-guards`
- `npm run smoke:documents-catalog`
- `npm run smoke:document-guards`
- `npm run smoke:tax-knd-xml`
- `npm run smoke:api-text-encoding`
- `npm run smoke:document-legal-confirmations`
- `npm run smoke:document-issue-chains`
- `npm run smoke:tax-registry-fiscal`
- `npm run smoke:russian-fallback-source`
- `npm run smoke:settings-persistence-file`
- `npm run smoke:mobile -- http://127.0.0.1:5173/#documents` with `.document-list`, viewport 390, overflow 0.

Residual risk:
- No real PDF export/signature pipeline yet.
- FNS XML still lacks XSD validation, qualified signature, and official transport.
- Expanded mobile click smoke for the passport panel hung in headless Edge; lifecycle/source smokes cover the behavior, but no screenshot artifact was produced.
- Vite still warns that main app chunk is above 500 kB after minification.

## 2026-05-23 - Issued Document PDF Export

What was wrong:
- Document issue was now auditable, but the clinic still could not download a real PDF from the issued archive.
- A fake PDF button would be worse than no PDF because it would blur the legal boundary between preview HTML and an exported file.
- Any PDF generated from current mutable patient/profile state would violate the immutable issued snapshot contract.

What was done:
- Added `/api/documents/:id/pdf`.
- The route accepts only issued or later-voided issued documents, reads the verified immutable HTML snapshot, and prints it through server-side Chromium/Edge.
- Added browser discovery through `DENTE_PDF_BROWSER_BIN`, `BROWSER_BIN`, and standard Edge/Chrome paths.
- Added explicit service failure when no browser exists instead of emitting a fake PDF.
- Added `canExportPdf` and `pdfDownloadUrl` to shared `DocumentAuditFacts`.
- Added `Скачать PDF` to document rows and the passport panel.
- Extended lifecycle smoke to assert a real `%PDF` attachment, filename, content type, and non-empty payload.
- Updated README and document-generation docs.

Cinematic Cheats used:
- Browser print pipeline: reuses the exact issued HTML visual snapshot instead of building a second renderer that can drift.
- On-demand export: cheap implementation now, worker-pool migration later if clinics batch-export many files.

Exact Microseconds saved:
- 0 us claimed. No profiler artifact. Per-export browser spawn is intentionally not a hot path.

Evidence:
- `npm run typecheck`
- `npm run build` (first attempt timed out without compiler output; rerun passed)
- `npm run smoke:document-lifecycle`
- `npm run smoke:document-payload-ui-source`
- `npm run smoke:document-html-issue-guards`
- `npm run smoke:documents-catalog`
- `npm run smoke:tax-knd-xml`
- `npm run smoke:api-text-encoding`
- `npm run smoke:document-guards`
- `npm run smoke:document-legal-confirmations`
- `npm run smoke:tax-registry-fiscal`
- `npm run smoke:mobile -- http://127.0.0.1:5173/#documents` with `.document-list`, viewport 390, overflow 0.

Residual risk:
- PDF export depends on Chromium/Edge being installed or `DENTE_PDF_BROWSER_BIN` being configured on the server.
- No PDF/A profile, qualified signature, stamped clinic seal workflow, or batch worker queue yet.
- FNS XML still needs XSD validation, qualified signature, and official transport before it can be called a final electronic package.
- Vite still warns that main app chunk is above 500 kB after minification.

## 2026-05-23 - Button-First Document Telegram Outbox

What was wrong:
- Automatic document-ready outbox existed, but the patient got a thin one-link keyboard.
- Tax-document status outbox had safe text and a visual card, but no useful inline next actions.
- The site Settings preview could show a message that was technically safe but operationally weak.

What was done:
- Expanded `telegramReplyMarkupFor` in `apps/api/src/sampleData.ts`.
- Document-ready outbox now includes `Открыть DENTE`, `Документы`, `Связаться`, and `Конфиденциальность`.
- Tax-document-status outbox now includes `Открыть DENTE`, `Налоговая`, `Документы`, `Связаться`, and `Конфиденциальность`.
- Payment, post-visit and recall templates also keep relevant contact/privacy/care/schedule actions instead of becoming one-link dead ends.
- Added Telegram smoke assertions for richer inline buttons, visual-card use, sendPhoto fallback preservation, and no preview leakage of diagnosis, tooth, treatment, fiscal receipt, amount, payer INN, PDF/file wording or document content.
- Added source-smoke guards and updated README plus `docs/13-dente-telegram-bot-plan.md`.

Cinematic Cheats used:
- Generic safe text plus strong buttons: better patient UX without carrying PHI in Telegram.
- Single `replyMarkup` source: the API outbox and Settings preview stay identical.

Exact Microseconds saved:
- 0 us claimed. Constant-size keyboard assembly only; no profiler artifact.

Evidence:
- `npm run typecheck`
- `npm run build`
- `npm run smoke:telegram-bot`
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:russian-fallback-source`
- `npm run smoke:api-text-encoding`
- `npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` with `.telegram-settings`, viewport 390, overflow 0.

Residual risk:
- Telegram still does not send PDFs, tax files or medical document contents by design.
- Portal deep links are still generic `patientPortalBaseUrl`; per-document authenticated deep links need a real portal identity layer.
- Vite still warns that main app chunk is above 500 kB after minification.

## 2026-05-23 - Telegram Portal Section Handoff

What was wrong:
- `Открыть DENTE` was safe but blunt: document, tax, payment, care and recall messages all opened the same portal root.
- Patients had useful inline buttons, but the primary portal action did not carry the requested section intent.
- A per-patient or per-document deep link would require real portal authentication; doing it in Telegram now would leak identifiers.

What was done:
- Added section-specific portal URL builders in `apps/api/src/sampleData.ts`.
- Outbox previews and delivery markup now append only `dente_source=telegram` and `dente_section=documents|tax|billing|care|schedule`.
- Tax-status preview text now points to the tax section when portal URL is configured.
- Updated Telegram webhook `portalButton` in `apps/api/src/routes/telegram.ts` so document, tax and care menus use the same safe section handoff.
- Extended `scripts/smoke-telegram-bot.mjs` to verify portal section, Telegram source marker, HTTPS, and absence of sensitive query keys for previews, outbox items and webhook menus.
- Updated README and `docs/13-dente-telegram-bot-plan.md`.

Cinematic Cheats used:
- Section intent instead of identity-bearing deep link: useful navigation now, no premature portal-auth design.
- One URL contract for site preview, outbox delivery and webhook menus.

Exact Microseconds saved:
- 0 us claimed. URL construction is constant-size Node/API work and not a Unity/runtime hot path.

Evidence:
- `npm run typecheck`
- `npm run build`
- `npm run smoke:telegram-bot` after rebuild; first attempt failed on stale dist and was not accepted.
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:settings-persistence-file`
- `npm run smoke:telegram-validation`
- `npm run smoke:russian-fallback-source`
- `npm run smoke:api-text-encoding`
- `npm run smoke:document-lifecycle`
- `npm run smoke:tax-knd-xml`
- `npm run smoke:document-payload-ui-source`
- `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram`, viewport 390, overflow 0.

Residual risk:
- These are section handoffs, not authenticated document-specific portal sessions.
- Actual portal routing must read `dente_section` and open the matching section when the patient portal shell is implemented.
- Vite still warns that main app chunk is above 500 kB after minification.

## 2026-05-23 - Web-Side Telegram Section Handoff

What was wrong:
- Bot/API buttons had safe section handoff URLs, but the web shell did not yet consume them.
- A patient could press `Открыть DENTE` from a tax/payment/care/schedule message and still depend on generic navigation.
- Leaving query strings in the URL would make stale Telegram state easy to reopen from browser history.

What was done:
- Added Telegram handoff parsing in `apps/web/src/App.tsx`.
- Accepted only `dente_source=telegram` and known `dente_section` values.
- Routed `documents` and `tax` to Documents, `billing` to Finance, `care` to Communications, and `schedule` to Schedule.
- Preselected the patient-intake or tax certificate form where the section implies a document workflow.
- Added a Russian notice: `Открыто из Telegram`.
- Stripped query strings with `history.replaceState`, leaving only `#documents`, `#finance`, `#communications`, or `#schedule`.
- Added `scripts/smoke-telegram-handoff-source.mjs` and `npm run smoke:telegram-handoff-source`.
- Updated README and `docs/13-dente-telegram-bot-plan.md`.

Cinematic Cheats used:
- Section intent instead of identity-bearing deep links: useful patient navigation now, authenticated portal identity later.
- One first-load parser: no hot polling, no preference pollution, no repeated URL mutation.

Exact Microseconds saved:
- 0 us claimed. No Unity/runtime hot path. Browser work is one URL parse and one route state update on entry.

Evidence:
- `npm run smoke:telegram-handoff-source`
- `npm run typecheck`
- `npm run build`
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:telegram-bot`
- `npm run smoke:russian-fallback-source`
- `npm run smoke:settings-persistence-file`
- `npm run smoke:api-text-encoding`
- `SMOKE_SELECTOR=.documents-panel .document-factory-tax-payments SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/?dente_source=telegram&dente_section=tax&patientId=SHOULD_NOT_SURVIVE`
- `SMOKE_SELECTOR=.finance-panel .payment-capture SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/?dente_source=telegram&dente_section=billing&documentId=SHOULD_NOT_SURVIVE`
- `SMOKE_SELECTOR=.communications-panel .communication-task-list SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/?dente_source=telegram&dente_section=care&appointmentId=SHOULD_NOT_SURVIVE`
- `SMOKE_SELECTOR=.schedule-panel .schedule-filter-strip SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/?dente_source=telegram&dente_section=schedule&paymentId=SHOULD_NOT_SURVIVE`

Residual risk:
- This is still a safe section handoff, not authenticated document-specific portal access.
- The web app currently opens internal CRM sections; a dedicated patient portal identity layer is still required before exposing patient-specific documents from Telegram.
- Vite still warns that the main app chunk is above 500 kB after minification.

## 2026-05-23 - Telegram Home Handoff And Russian Bridge Copy

What was wrong:
- Bot/API shared contract allowed `dente_section=home`, but the web parser did not.
- Generic bot buttons could land on the right-looking start screen while keeping stale Telegram query parameters in browser history.
- Mobile smoke proved the URL was not cleaned until preference hydration; `patientId=SHOULD_NOT_SURVIVE` stayed in `location.href` on the first attempt.
- Local OCR/OHIF readiness still exposed English setup and next-action text in an operator-facing settings path.

What was done:
- Added `home` to `DenteTelegramPortalSection` in `apps/web/src/App.tsx`.
- Mapped `home` to `#shift` with Russian DENTE start-screen notice text.
- Split handoff behavior into immediate URL cleanup on mount plus post-hydration section/form reapplication from `initialTelegramHandoffTargetRef`.
- Extended `scripts/smoke-telegram-handoff-source.mjs` to require the shared `home` section and ref-backed cleanup marker.
- Translated OCR/OHIF bridge roles, workloads, privacy boundaries, setup hints, health warnings and price-photo next actions in `apps/api/src/routes/system.ts`.
- Extended `scripts/smoke-russian-fallback-source.mjs` to forbid the old English bridge strings.
- Updated README and `docs/13-dente-telegram-bot-plan.md` to document `home|documents|tax|billing|care|schedule`.

Cinematic Cheats used:
- Section-level `home` handoff instead of premature authenticated patient-session routing.
- Immediate `history.replaceState` instead of waiting on settings synchronization.

Exact Microseconds saved:
- 0 us claimed. This is entry-time browser routing and static API copy, not a runtime hot path.

Evidence:
- `npm run smoke:telegram-handoff-source`
- `npm run smoke:russian-fallback-source`
- `npm run typecheck`
- `npm run build`; residual Vite warning: main app chunk 635.62 kB > 500 kB.
- `npm run smoke:api-text-encoding`
- `npm run smoke:telegram-bot`
- `SMOKE_SELECTOR=.shift-hero SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/?dente_source=telegram&dente_section=home&patientId=SHOULD_NOT_SURVIVE#settings`, final href `http://127.0.0.1:5173/#shift`, viewport 390, overflow 0.

Residual risk:
- `home` still opens the internal DENTE shift screen. A public patient portal home needs a real authenticated identity layer before patient-specific content can be shown from Telegram.
- Vite still warns that the main app chunk is above 500 kB after minification.

## 2026-05-23 - Document Payment Selection Persistence

What was wrong:
- Tax document and payment receipt selections were real operator choices, but they lived only in React memory.
- Tax document payment selection disappeared after reload.
- Payment receipt selection reset to every eligible active-visit payment after reload, which could undo an explicit cleared/subset choice.

What was done:
- Added `documentPaymentSelectionStorageKey` and bounded local store helpers in `apps/web/src/App.tsx`.
- Tax document selection now persists by patient id, fiscal year and payer key.
- Payment receipt selection now persists by patient id and active visit id.
- Added hydration refs so the app loads stored choices before saving them back.
- Kept first-time payment receipt behavior convenient: default all eligible paid active-visit payments until the operator changes the selection.
- Updated `scripts/smoke-document-payload-ui-source.mjs` to require the new store/keys/refs and reject the old receipt reset effect.

Cinematic Cheats used:
- Local bounded operator cache instead of premature cross-device preference backend.
- Patient/year/payer and patient/visit keys instead of broad global UI preferences.

Exact Microseconds saved:
- 0 us claimed. Browser localStorage work happens on document-scope changes, not a hot path. Unity runtime is untouched.

Evidence:
- `npm run smoke:document-payload-ui-source`
- `npm run typecheck`
- `npm run build`; residual Vite warning: main app chunk 637.56 kB > 500 kB.
- `npm run smoke:tax-document-explicit-payment-scope`
- `npm run smoke:document-payloads`
- `SMOKE_SELECTOR=.document-list SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#documents`, viewport 390, overflow 0.

Residual risk:
- Selection persistence is local to the browser/operator machine. Server-side cross-device sync remains a future product step.
- First-time payment receipts still default to all eligible paid active-visit payments; saved explicit choices win after any operator change.

## 2026-05-23 - Russian STT/OCR/DICOM Operator Copy

What was wrong:
- Local bridge use plans still had English titles/actions in visible admin flows: `Visit dictation`, `Capture without blocking`, `PDF / document OCR`, `Imaging import`, DICOM import warnings and Vosk bridge warnings.
- Speech gateway warnings mixed Russian with `fallback`, `chunks`, `transcript`, `cooldown`, `prompt pack`, `server env`, and `MVP` wording.
- Existing Russian fallback smoke did not forbid the newer English/technical strings.

What was done:
- Translated STT/OCR/price-photo/DICOM use-plan titles, steps, warnings and next actions in `apps/api/src/routes/system.ts`.
- Translated speech gateway provider warnings and local bridge recovery messages in `apps/api/src/speech/gateway.ts`.
- Kept machine contracts unchanged: enum values, provider ids, payload fields and route names were not renamed.
- Extended `scripts/smoke-russian-fallback-source.mjs` to require the Russian replacements and forbid the old English/technical phrases.

Cinematic Cheats used:
- Static Russian operator copy instead of a heavier i18n refactor inside API readiness payloads.
- Contract-preserving visible text replacement instead of breaking enum/schema values.

Exact Microseconds saved:
- 0 us claimed. This is API response copy and source guard work, not a runtime hot path. Unity runtime untouched.

Evidence:
- `npm run smoke:russian-fallback-source`
- `npm run smoke:ui-preferences`
- `npm run smoke:api-text-encoding`
- `npm run typecheck`
- `npm run build`; residual Vite warning: main app chunk 637.56 kB > 500 kB.
- `npm run smoke:telegram-bot`
- `npm run smoke:schedule-configuration`

Residual risk:
- Internal API/schema identifiers remain English by design.
- This does not implement new identity-backed patient portal access; it fixes visible doctor/admin language debt in readiness and action payloads.

## 2026-05-23 - Loop 15 Telegram QR Persistence And QR-First Linking

What was wrong:
- The Telegram QR generator remembered neither patient/staff mode nor selected staff member across reload/server hydration.
- Generated QR output forced too much manual selection/copy work.
- Invalid-code Telegram replies still told the patient to request a new manual code instead of returning to QR-first linking.

What was done:
- Added `telegramLinkSubjectType` and `telegramLinkStaffId` to shared UI preferences, web defaults, hydration, autosave payloads, API sample-state normalization and persistence smokes.
- Added QR card actions in Settings: copy code, copy deep link, copy patient/staff share text, and download QR SVG.
- Cleared stale generated QR output when the operator changes patient/staff target.
- Changed `/start`, `/clinic`, non-private link rejection and invalid-code rejection copy to QR-first guidance.
- Updated README and `docs/13-dente-telegram-bot-plan.md`.
- Extended source/behavior smokes to guard QR preferences, QR actions, and QR-first rejected-code replies.

Cinematic Cheats used:
- Reused existing UI preference blob and link-code API instead of inventing a new QR workflow owner.
- QR SVG download is generated from the existing server QR payload; no new image pipeline.

Exact Microseconds saved:
- 0 us claimed. Unity runtime untouched. Browser clipboard/blob work runs only on button clicks; API reply text is constant-size.

Evidence:
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:ui-preferences`
- `npm run smoke:russian-fallback-source`
- `npm run typecheck`
- `npm run build`; residual Vite warning: main app chunk 639.90 kB > 500 kB.
- `npm run smoke:settings-preferences`
- `npm run smoke:settings-persistence-file`
- `npm run smoke:telegram-bot` failed once on invalid-code QR-first text, then passed after fix and API rebuild.
- `npm run typecheck -w @dental/api`
- `npm run build -w @dental/api`
- `npm run smoke:api-text-encoding`
- `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram`, viewport 390, overflow 0.

Residual risk:
- Vite main chunk remains above 500 kB.
- Authenticated public patient portal identity is still not implemented; Telegram buttons stay section-safe and non-identifying.

## 2026-05-24 - Loop 16 Telegram Review/Map Visual Cards And Care Callback Proof

What was wrong:
- `/review` and `dente:map` replies had useful inline buttons but did not send the configured clinic image, unlike start/documents/care flows.
- Filling and hygiene appeared in the care menu, but behavior coverage was weaker than implant/extraction. The risk was a visible button that fails to create/reuse the actual doctor task.
- Source guard did not require filling/hygiene workflow code coverage.

What was done:
- `apps/api/src/routes/telegram.ts`: successful review and map replies now include `photoUrl: patientMenuCardPhoto(settings)`, so configured clinics send `sendPhoto` visual cards with the existing safe inline keyboard.
- `scripts/smoke-telegram-bot.mjs`: added review/map `sendPhoto` assertions, care menu assertions for `dente:care-filling` and `dente:care-hygiene`, and full callback create/repeat checks for filling/hygiene doctor tasks, workflow codes, audit actions, visual card delivery, care-section portal handoff and admin fallback.
- `scripts/smoke-telegram-control-ui-source.mjs`: added filling/hygiene care request and workflow-code markers.
- `README.md` and `docs/13-dente-telegram-bot-plan.md`: documented review/map visual cards and four-topic care callback smoke coverage.

Cinematic Cheats used:
- Reused the existing clinic visual-card setting and Telegram `sendPhoto` transport instead of creating a separate media system.
- Kept Telegram payloads section-safe and non-identifying; no patient/document/payment ids added to button URLs or callback data.

Exact Microseconds saved:
- 0 us claimed. Unity runtime untouched. API work is constant-size reply construction and test-only sample ledger mutation.

Evidence:
- `npm run smoke:telegram-control-ui-source`
- `npm run typecheck -w @dental/api`
- `npm run build -w @dental/api`
- `npm run smoke:telegram-bot` passed after API dist rebuild and again after full build.
- `npm run smoke:russian-fallback-source`
- `npm run smoke:api-text-encoding`
- `npm run typecheck`
- `npm run build`; residual Vite warning: main app chunk 639.90 kB > 500 kB.

Residual risk:
- `welcomeImageUrl` must be configured for review/map replies to send a photo; otherwise they remain text fallback.
- Explorer subagents were interrupted by the environment transition and returned no audit output.

## 2026-05-24 - Loop 17 - Telegram free-text document handoff

What was wrong:
- The bot had proper inline buttons for `Налоговая`, `Медкарта` and `Формы пациента`, but a linked patient typing the same intent in plain Russian could get only an informational menu.
- Document request task/audit wording assumed the patient pressed a button, which became false once the bot accepts button and text entry points.
- Telegram outbox PHI smoke had a brittle guard: any appointment time ending in `:36` could be treated as leaked tooth `36`.

What was done:
- `apps/api/src/routes/telegram.ts`: explicit Russian free-text tax phrases now create/reuse the tax document administrator task; medical-record phrases create/reuse the medical document task; form/consent/PДн phrases create/reuse the patient forms task. Generic document words still open the menu.
- `apps/api/src/sampleData.ts`: Telegram document request task bodies and audit reasons now describe a Telegram request instead of button-only input.
- `scripts/smoke-telegram-bot.mjs`: added linked-patient behavior checks for tax free-text repeat, medical free-text task creation and forms free-text task creation, including workflow codes, communication events, audit actions, visual-card delivery and portal handoff.
- `scripts/smoke-telegram-bot.mjs`: narrowed the PHI guard to real clinical leakage patterns such as `лечение 36` / `зуб 36`.
- `README.md` and `docs/13-dente-telegram-bot-plan.md`: documented that clear free-text document phrases create the same admin tasks as inline buttons.

Cinematic Cheats used:
- Reused the existing deterministic fragment router and workflow-code task creator instead of adding a new NLP or command parser.
- Kept Telegram replies as non-PHI visual cards plus section-safe portal buttons; no patient/document/payment identifiers were added to callback data or URLs.

Exact Microseconds saved:
- 0 us claimed. Unity/runtime frame path untouched. API path adds only bounded string checks before existing communication task/event writes.

Evidence:
- `npm run typecheck -w @dental/api`
- `npm run build -w @dental/api`
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:russian-fallback-source`
- `npm run smoke:api-text-encoding`
- `npm run smoke:telegram-bot` passed after narrowing the false-positive time regex.
- `npm run typecheck`
- `npm run build`; residual Vite warning: main app chunk 639.90 kB > 500 kB.
- post-build `npm run smoke:telegram-bot`

Residual risk:
- Free-text detection is intentionally deterministic and Russian-first; broader multilingual/NLU routing remains future work.
- Document signing/attestation hardening identified by audit was not implemented in this loop.

## 2026-05-24 - Loop 18 - Document issue attestation and release journal

What was wrong:
- DENTE could mark a reviewed document as issued and then serve archived HTML/PDF/XML without a structured proof that recipient identity was checked, the opened document was reviewed, and recipient/clinic signatures were acknowledged.
- Medical copy request, medical-record extract and medical-document release receipt outputs lacked a machine-readable release journal entry.
- Settings persistence proof did not cover staff/chair working hours or patient preferred appointment windows after a fresh API module load.

What was done:
- `packages/shared/src/index.ts`: added issue signature mode, issue signature attestation, issue request payload, and document release journal schemas/types.
- `apps/api/src/routes/documents.ts`: `/api/documents/:id/issue` now requires signature attestation; tax XML and PDF exports require attestation; medical release-related documents create structured journal entries; audit-facts exposes signature and release journal data.
- `apps/api/src/sampleData.ts`: issued document storage now preserves signature attestation and release journal entries with issuer and snapshot hash facts.
- `apps/api/src/documents/renderDocument.ts`: issued HTML renders signature attestation and release journal blocks into the immutable archive.
- `apps/web/src/App.tsx` and `apps/web/src/styles/main.css`: issue modal now collects signature mode/time/recipient/staff/checks, disables issue until complete, persists reusable signature defaults, and shows signature/release facts in audit passport.
- `scripts/lib/documentIssueAttestation.mjs` plus document/tax smokes: positive issue paths now send real attestation payloads and negative paths prove issue without attestation is rejected.
- `scripts/smoke-settings-persistence-file.mjs`: file-backed reload now proves doctor, assistant, chair and patient preferred schedule settings survive.
- `docs/12-document-generation-forms.md` and `README.md`: documented the issue attestation, release journal, PDF/XML gate and schedule persistence proof.

Cinematic Cheats used:
- Used explicit DTO facts and a compact modal gate instead of a fake signature engine or PDF text inference.
- Reused the issued snapshot route as the source of the release journal hash, avoiding a second archive pipeline.
- Extended deterministic smokes instead of adding a broad E2E harness that would be slower and less precise for this defect.

Exact Microseconds saved:
- 0 us claimed. Unity/runtime frame path untouched. API path adds bounded schema validation and one small JSON object per issue; browser path adds modal state/localStorage only.

Evidence:
- `npm run typecheck -w @dental/shared`
- `npm run typecheck -w @dental/api`
- `npm run typecheck -w @dental/web`
- `npm run build`; residual Vite warning: web `assets/index-lHUqv54z.js` 645.86 kB > 500 kB.
- `npm run smoke:document-payload-ui-source`
- `npm run smoke:document-legal-confirmations`
- `npm run smoke:document-lifecycle`
- `npm run smoke:document-issue-chains`
- `npm run smoke:tax-knd-xml`
- `npm run smoke:tax-document-explicit-payment-scope`
- `npm run smoke:tax-certificate-duplicate-issue`
- `npm run smoke:settings-persistence-file`
- `npm run smoke:russian-fallback-source`
- `npm run smoke:api-text-encoding`
- `npm run smoke:telegram-control-ui-source`
- `npm run smoke:ui-preferences`
- `npm run smoke:schedule-configuration`

Residual risk:
- Signature attestation is explicit clinic confirmation, not certified EDS/QES integration.
- PostgreSQL mode still needs matching storage/migration for signature attestation and release journal fields before production DB release.
## 2026-05-24 - Loop 19 - Document Issue Persistence And Telegram Safe Handoff

What was wrong:
- PostgreSQL schema and migration had `signature_attestation` / `release_journal_entry`, but Drizzle meta lacked the matching `0017_snapshot.json`.
- Browser issue signature defaults were local-only, unlike adjacent document preferences.
- Telegram portal buttons could inherit existing query params from `patientPortalBaseUrl` before adding DENTE handoff params.

What was done:
- Generated `apps/api/drizzle/meta/0017_snapshot.json` from current schema and hardened `smoke:db-runtime-contract` to assert both document issue columns are nullable `jsonb`.
- Added server/file-backed UI preferences for `documentIssueSignatureMode`, `documentIssueStaffFullName`, and `documentIssueStaffRole`; kept localStorage only as migration fallback.
- Cleared portal URL query/hash before adding `dente_source=telegram` and `dente_section`; Telegram smoke now requires exactly those two query params.
- Updated README and DENTE docs to match actual persistence and Telegram handoff behavior.

Cinematic Cheats used:
- No physical/visual simulation. Product cheat is a compact, deterministic URL normalizer and bounded preference DTO instead of a heavier identity/session subsystem before the portal auth layer exists.

Exact Microseconds saved:
- 0 us Unity runtime. Work is Node/API, React preference state and Drizzle metadata. No frame-time claim.

Verification:
- Passed: `npm run smoke:ui-preferences`, `npm run smoke:document-payload-ui-source`, `npm run smoke:db-runtime-contract`, `npm run typecheck`, `npm run build`, `npm run smoke:settings-preferences`, `npm run smoke:settings-persistence-file`, `npm run smoke:telegram-bot`, `npm run smoke:document-lifecycle`, `npm run smoke:document-issue-chains`, `npm run smoke:telegram-control-ui-source`, `npm run smoke:russian-fallback-source`, `npm run smoke:api-text-encoding`, `npm run smoke:tax-knd-xml`, `npm run smoke:tax-document-explicit-payment-scope`, `npm run smoke:document-legal-confirmations`.
- Passed mobile: `#settings/telegram` and `#documents` at 390 px width with overflow 0. First parallel Documents run timed out due CDP port collision, then passed on `SMOKE_CDP_PORT=9324`.

## 2026-05-24 - Loop 20 - Onboarding, Release Hashes, Telegram Scoped Config

What was wrong:
- Onboarding draft dismissal could set the legacy dismissal key before the full UI preference state was persisted.
- Medical release audit data needed a non-null source hash chain visible in audit-facts and the browser passport.
- Telegram Settings could configure clinic-owned bots, while the UI status loader still hit only `/api/telegram/status`.
- Telegram medical-document and patient-form inline callbacks were present but needed direct behavior coverage.

What was done:
- `apps/web/src/App.tsx`: draft-mode onboarding dismissal now persists complete UI preferences before the legacy fallback, stores fallback `draftMode`, shows release-journal `sourceSnapshotSha256`, adds `telegramBotConfigId`, and routes clinic-owned bot status to `/api/telegram/status/:organizationId/:botConfigId`.
- `packages/shared/src/index.ts` and `apps/api/src/sampleData.ts`: added/normalized `telegramBotConfigId` in server-backed UI preferences.
- `apps/api/src/routes/documents.ts`: added deterministic release source hashing; release receipts reuse the issued copy-request snapshot hash when available.
- `scripts/smoke-document-issue-chains.mjs`: asserts release journal SHA-256 values and receipt-to-copy-request hash linkage.
- `scripts/smoke-telegram-bot.mjs`: directly smokes `dente:medical-docs` and `dente:patient-forms` callbacks, including task creation/reuse, events, audit actions, callback acknowledgement and visual card delivery.
- `scripts/smoke-onboarding-configuration-source.mjs`, `scripts/smoke-ui-preferences.mjs`, `scripts/smoke-settings-preferences.mjs`, `scripts/smoke-document-payload-ui-source.mjs`, and `scripts/smoke-telegram-control-ui-source.mjs`: added regression guards for the new persistence/hash/status behavior.
- `README.md`, `docs/12-document-generation-forms.md`, and `docs/13-dente-telegram-bot-plan.md`: updated with scoped bot config status and release source-hash facts.

Cinematic Cheats used:
- Reused existing UI preference sync instead of adding a separate Telegram config store.
- Used deterministic SHA-256 over bounded DTOs instead of a heavier release registry before DB normalization.
- Button behavior was proven through webhook smokes instead of a broad browser E2E loop.

Exact Microseconds saved:
- 0 us Unity runtime. This is Node/API, React preference state and documentation. No frame-time claim.

Verification:
- Passed: `npm run typecheck -w @dental/shared`, `npm run typecheck -w @dental/api`, `npm run typecheck -w @dental/web`, `npm run typecheck`, `npm run build -w @dental/api`, `npm run build`.
- Passed: `npm run smoke:telegram-control-ui-source`, `npm run smoke:ui-preferences`, `npm run smoke:settings-preferences`, `npm run smoke:settings-persistence-file`, `npm run smoke:telegram-bot`, `npm run smoke:document-issue-chains`, `npm run smoke:document-lifecycle`, `npm run smoke:document-payload-ui-source`, `npm run smoke:onboarding-configuration-source`, `npm run smoke:russian-fallback-source`, `npm run smoke:api-text-encoding`, `npm run smoke:schedule-configuration`.
- Passed mobile: `#settings/telegram` and `#documents` at 390 px width with overflow 0. The first Documents run used the wrong stale selector/process set; stale smoke processes were stopped, then `.documents-panel` passed on `SMOKE_CDP_PORT=9332`.

## 2026-05-24 - Loop 21 - Telegram Visual Replies, Russian UI, Patient Forms Lifecycle

What was wrong:
- Telegram still had text-only paths after the prior visual-card work: free-text schedule, contact/help/privacy/clinic replies, link-code accept/reject paths, and appointment callback replies.
- The webhook diagnostic payload did not expose suggested reply markup/photo URL, so site/operator tooling could not inspect the exact button/photo answer shape.
- Doctor-facing speech/MPR UI still leaked English labels: `smart chunks`, `smart chunking`, English plane names, `prompt terms`, `retry`, `timeout`, and related STT control fragments.
- Core patient forms were renderer/payload-tested, but not proven through the real API lifecycle from create to issue/audit/archive.

What was done:
- `apps/api/src/routes/telegram.ts`: added configured visual-card photo delivery to the common patient reply paths and added suggested photo/reply markup fields to webhook responses.
- `packages/shared/src/index.ts`: extended the Telegram webhook response schema with `suggestedReplyMarkup` and `suggestedPhotoUrl`.
- `scripts/smoke-telegram-bot.mjs`: now asserts the visual-card/photo behavior and webhook suggested markup/photo fields, including free-text schedule and appointment callback paths.
- `apps/web/src/App.tsx`: replaced visible English speech/MPR labels with Russian equivalents and Russian second-unit wording.
- `scripts/smoke-ui-preferences.mjs`: forbids the old English doctor-facing fragments.
- `scripts/smoke-patient-forms-lifecycle.mjs` and `package.json`: added a route-level smoke for intake questionnaire, personal-data consent, minor/legal representative consent, and photo/video consent.
- `README.md` and `docs/12-document-generation-forms.md`: documented the new patient-forms lifecycle proof.

Cinematic Cheats used:
- Reused one configured clinic visual card instead of adding per-command image generation.
- Used deterministic source/lifecycle smokes instead of a broad slow UI suite for form correctness.
- Kept patient forms on immutable HTML snapshot route instead of introducing another archive path.

Exact Microseconds saved:
- 0 us Unity runtime. API payload assembly is constant-size; browser changes are text-only; patient-form proof is smoke-only.

Verification:
- Passed: `npm run typecheck -w @dental/shared`, `npm run typecheck -w @dental/api`, `npm run typecheck -w @dental/web`, `npm run typecheck`, `npm run build -w @dental/shared`, `npm run build -w @dental/api`, `npm run build`.
- Passed: `npm run smoke:telegram-bot`, `npm run smoke:patient-forms-lifecycle`, `npm run smoke:document-payloads`, `npm run smoke:document-lifecycle`, `npm run smoke:ui-preferences`, `npm run smoke:russian-fallback-source`, `npm run smoke:api-text-encoding`, `npm run smoke:settings-persistence-file`, `npm run smoke:schedule-configuration`, `npm run smoke:telegram-control-ui-source`.
- Passed mobile: `#documents` and `#settings/telegram` at 390 px width with overflow 0. The first parallel `#settings/telegram` mobile run timed out; solo rerun passed.

Residual risk:
- Telegram photo delivery still depends on configured clinic visual URL; missing image falls back to text plus buttons.
- Patient form issue attestation is clinic confirmation, not certified EDS/QES.
## 2026-05-24 - Loop 22 - Annual Tax Certificate, Preferences, Telegram Outbox

What was wrong:
- Tax certificates were duplicate-safe only by selected receipt/payment overlap. Same patient, same taxpayer, same year, later receipt could produce a second KND/legacy certificate instead of forcing the annual cumulative certificate workflow.
- API-loaded `uiPreferences` had partial legacy backfill. Old state files missing newer required fields could break settings hydration instead of preserving operator choices with defaults.
- Telegram outbox direct-send lookup used the first 300 generated rows. A row available through pagination could return false item-not-found by id.

What was done:
- Added annual taxpayer-scope matching in `apps/api/src/routes/documents.ts` before receipt/payment fallback duplicate checks.
- Updated `scripts/smoke-tax-certificate-duplicate-issue.mjs` to prove same-taxpayer annual new-payment block and separate different-taxpayer issue.
- Added schema-level UI preference defaults in `packages/shared/src/index.ts`; API state load now normalizes loaded preferences through the shared schema.
- Extended `scripts/smoke-settings-persistence-file.mjs` with a legacy partial preference state reload.
- Extracted full Telegram outbox generation in `apps/api/src/sampleData.ts`; direct send now resolves by id from the full generated set, not the paged response.
- Added `scripts/smoke-telegram-outbox-lookup.mjs` and package script `smoke:telegram-outbox-lookup`.
- Updated README, UX principles, Telegram plan and document-generation docs.

Cinematic Cheats used:
- None. API/data correctness slice only.

Exact Microseconds saved:
- 0 us Unity runtime.
- Avoided operator/admin time loss and legal rework by preventing duplicate annual certificates at issue time.
- API runtime impact is bounded: document/payment scope scan on issue/XML export, zod preference normalization on load, and one full generated outbox scan for direct send lookup.

Verification:
- `npm run typecheck` passed.
- `npm run build` passed; residual Vite warning: web `assets/index-CjMyQWLg.js` 648.37 kB > 500 kB.
- `npm run smoke:tax-certificate-duplicate-issue` passed.
- `npm run smoke:tax-knd-xml` passed.
- `npm run smoke:tax-document-explicit-payment-scope` passed.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:settings-preferences` passed.
- `npm run smoke:telegram-outbox-lookup` passed.
- `npm run smoke:telegram-bot` passed.
- `npm run smoke:telegram-outbox-persistence` passed.
- `npm run smoke:onboarding-configuration-source` passed.
- `npm run smoke:schedule-configuration` passed.
- `npm run smoke:ui-preferences` passed.
- `npm run smoke:russian-fallback-source` passed.
- `npm run smoke:api-text-encoding` passed.

Residual:
- Structured void/correction metadata for documents is still open.
- Autonomous Telegram due-reminder worker is still open.
- Frontend imaging/source panel English labels are still open.

## 2026-05-24 - Loop 23 - Structured Void, Due Worker, Russian Labels

What was wrong:
- Document voiding was still too cheap: a status flip could erase the operational reason trail needed for tax/legal correction.
- Same-year tax certificate duplicate protection blocked bad replacements, but there was no proven safe replacement path after annul/correction.
- Telegram due reminders depended on manual/admin execution only.
- Imaging/source UI still exposed English/raw enum labels in doctor/admin surfaces.

What was done:
- Added shared structured void input and attestation models; persisted `voidAttestation`, `voidedAt` and `voidedByUserId` in file-backed state and PostgreSQL migration/snapshot `0018_document_void_attestation`.
- Changed `/api/documents/:id/void` to reject missing/invalid void bodies, validate correction-document ownership/scope, preserve archive facts and expose void attestation through audit facts.
- Added a Documents UI void confirmation flow with reason, staff, notification/archive/status checks and optional correction reference.
- Updated the tax duplicate smoke so old annual certificates can be replaced only after structured tax-correction void; the old document stays archived/voided.
- Extracted Telegram due batch execution and added an env-gated in-process due worker with interval, batch limit, dry-run, run-on-start, in-flight guard, Fastify shutdown stop and failed-due retry behavior.
- Replaced the audited English/raw imaging labels with Russian fallbacks and tightened source guards without doing a broad i18n rewrite.
- Updated README, document-generation docs and Telegram bot plan with the implemented behavior and env keys.

Cinematic Cheats used:
- Used one structured attestation DTO instead of a larger legal workflow engine.
- Used a bounded off-by-default worker instead of pretending the prototype has a distributed queue.
- Used targeted Russian label mapping and source guards instead of a slow full localization project.

Exact Microseconds saved:
- 0 us Unity runtime.
- API/browser cost is bounded: zod validation, one JSON attestation record, one optional due batch timer when enabled, and text-only UI label rendering. No profiler claim.

Verification:
- `npm run build` passed; residual Vite warning: web `assets/index-D-WguNuM.js` 656.64 kB > 500 kB.
- `npm run smoke:document-lifecycle` passed.
- `npm run smoke:document-payload-ui-source` passed.
- `npm run smoke:db-runtime-contract` passed.
- `npm run smoke:telegram-due-worker-source` passed.
- `npm run smoke:tax-certificate-duplicate-issue` passed with replacement after structured void.
- `npm run smoke:ui-preferences` passed.
- `npm run smoke:russian-fallback-source` passed.
- `npm run smoke:telegram-bot` passed.
- `npm run smoke:telegram-outbox-persistence` passed.
- `npm run smoke:telegram-outbox-lookup` passed.
- `npm run smoke:telegram-control-ui-source` passed.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:settings-preferences` passed.
- `npm run smoke:onboarding-configuration-source` passed.
- `npm run smoke:schedule-configuration` passed.
- `npm run smoke:tax-knd-xml` passed.
- `npm run smoke:tax-document-explicit-payment-scope` passed.
- `npm run smoke:api-text-encoding` passed.
- `npm run smoke:patient-forms-lifecycle` passed.

Residual:
- Void attestation is clinic/operator evidence, not certified EDS/QES.
- Telegram due worker is in-process and disabled by default; distributed queue/backoff/dead-letter remains production work.
- Vite chunk size warning remains open.

## 2026-05-24 - Loop 24 - Medical Date Guards, PDF Timeout

What was wrong:
- Medical copy request, extract and release receipt issue chains could treat malformed non-empty dates as absent dates.
- `smoke:document-issue-chains` proved reversed periods and source scope, but did not prove invalid calendar dates.
- Real PDF generation had a hardcoded 30-second browser timeout; lifecycle smoke exposed this by returning 503 on `%PDF` export.

What was done:
- Added strict `YYYY-MM-DD`-prefixed date parsing with calendar round-trip validation in `apps/api/src/routes/documents.ts`.
- Added issue-chain blockers for invalid copy-request dates, extract dates, release receipt dates, reversed periods and access expiry before delivery.
- Extended `scripts/smoke-document-issue-chains.mjs` with invalid-date issue attempts for copy request, release receipt and medical extract.
- Added bounded `DENTE_PDF_EXPORT_TIMEOUT_MS` support for server-side Chromium/Edge PDF export; default is 60000 ms, clamped to 10000-180000 ms.
- Updated README and document-generation docs with the implemented legal-date and PDF timeout behavior.

Cinematic Cheats used:
- Kept drafts editable and moved the hard stop to legal issue, avoiding a heavy form-state workflow while preserving the real legal gate.
- Used one bounded env-controlled browser wait instead of introducing a fake PDF renderer or distributed queue in this slice.

Exact Microseconds saved:
- 0 us Unity runtime.
- API overhead is constant per issue attempt: a few string trims, regex match and UTC date round-trip checks.
- PDF overhead changes only explicit export calls; no dashboard or frame-loop cost.

Verification:
- `npm run build -w @dental/api` passed.
- `npm run smoke:document-issue-chains` passed with `copyRequestDateGuard`, `releaseReceiptDateGuard`, `extractDateGuard`.
- `npm run smoke:document-lifecycle` initially failed on hardcoded 30-second PDF timeout, then passed after configurable timeout.
- `npm run smoke:api-text-encoding` passed.
- `npm run smoke:document-payloads` passed.
- `npm run smoke:document-payload-ui-source` passed.
- `npm run build` passed; residual Vite warning: web `assets/index-D-WguNuM.js` 656.64 kB > 500 kB.

Residual:
- PDF export still requires a configured Chromium/Edge binary and is not yet a queued worker.
- Certified EDS/QES is still outside current attestation scope.
- Vite chunk size warning remains open.

## 2026-05-24 - Loop 25 - Web Route Render Gating

What was wrong:
- The site rendered most major DENTE workspaces as hidden DOM at once.
- This meant a doctor opening Documents, Shift, Imaging or Settings still paid render cost for unrelated panels.
- The main web chunk warning remained visible; testing Terser proved that minification was not the fix.

What was done:
- Changed top-level route sections in `apps/web/src/App.tsx` to mount conditionally by `currentView`.
- Covered Shift, patient cockpit, Imaging, Schedule, Patients, Visit, Documents, Finance, Communications, compliance and Settings.
- Added `scripts/smoke-web-render-gating-source.mjs`.
- Added `smoke:web-render-gating-source` to `package.json`.
- Updated README and `docs/03-ux-principles.md` with the implemented route render rule.

Cinematic Cheats used:
- Removed invisible work from the first render path instead of spending effort on a fake bundle-size win.
- Kept the existing hash navigation and user-visible layout unchanged.

Exact Microseconds saved:
- 0 us Unity runtime.
- Browser savings are DOM/React construction avoided for inactive sections; exact microseconds were not claimed without profiler capture.
- The production JS chunk is still 656.59 kB, so the remaining real fix is route-level lazy module split.

Verification:
- `npm run build -w @dental/web` passed.
- `npm run smoke:web-render-gating-source` passed with `gatedTopLevelSections: 12`.
- `npm run build` passed for shared, api and web.
- Browser smoke `#documents` at 390 px passed, `.documents-panel`, overflow 0.
- Browser smoke `#settings/telegram` at 390 px passed, `.settings-zone`, overflow 0.
- Browser smoke `#imaging` at 390 px passed, `.imaging-panel`, overflow 0.
- Browser smoke `#shift` at 390 px passed, `.shift-hero`, overflow 0.

Residual:
- Vite still reports `assets/index-DTu950lQ.js` 656.59 kB > 500 kB.
- Settings sub-tabs still use hidden tab sections internally; top-level route waste is fixed first, tab-level lazy mounting remains next.

## 2026-05-24 - Loop 26 - Visit Workflow Forms Lifecycle

What was wrong:
- Renderer/catalog/guard checks proved that visit/workflow form templates exist, but did not prove the real API route lifecycle for several high-use documents.
- Anesthesia logs, medication orders, lab orders, X-ray/CBCT referrals, attendance certificates, warranty memos, intervention refusals and refund/correction requests needed create -> issue -> audit -> archive proof, not just isolated rendering.

What was done:
- Added `scripts/smoke-visit-workflow-forms-lifecycle.mjs`.
- Added `smoke:visit-workflow-forms-lifecycle` to `package.json`.
- The smoke covers eight document kinds: `anesthesia_consent_log`, `prescription_medication_order`, `lab_work_order`, `xray_cbct_referral`, `visit_attendance_certificate`, `warranty_service_memo`, `medical_intervention_refusal`, `payment_refund_correction_request`.
- For each kind it verifies missing structured payload rejection, draft creation, issue rejection without signature attestation, successful issue with attestation, audit facts, HTML archive download filename, hidden storage paths and immutable HTML after patient data mutation.
- Updated README and `docs/12-document-generation-forms.md` with the exact proof command and covered forms.

Cinematic Cheats used:
- Used one route-level lifecycle smoke over fixture data instead of building a separate form test harness.
- Avoided per-form PDF export in this smoke; the existing document lifecycle smoke already proves real PDF export from archived HTML.

Exact Microseconds saved:
- 0 us Unity runtime.
- Product runtime code was not changed; this is a regression proof artifact.
- Test cost is bounded Fastify route execution and snapshot file writes for eight forms.

Verification:
- `npm run smoke:visit-workflow-forms-lifecycle` passed before rebuild.
- `npm run smoke:documents-catalog` passed with `renderedCount:30`.
- `npm run build` passed; residual Vite warning: web `assets/index-DTu950lQ.js` 656.59 kB > 500 kB.
- `npm run smoke:patient-forms-lifecycle` passed.
- `npm run smoke:document-guards` passed.
- `npm run smoke:visit-workflow-forms-lifecycle` passed again after rebuild.
- `npm run smoke:document-lifecycle` passed.
- `npm run smoke:api-text-encoding` passed with `checkedStrings:1843`, `mojibakeHits:0`.

Residual:
- This proves route lifecycle and archived HTML, not certified EDS/QES.
- Full per-form PDF batch export is intentionally not in this smoke.
- Vite chunk size warning remains open.

## 2026-05-24 - Loop 27 - Strict Schedule Datetimes

What was wrong:
- Schedule create/update validation trusted `Date.parse`.
- Impossible or rollover times could be normalized by JavaScript before schedule rules ran, making a malformed visit look like a different real appointment.

What was done:
- Added strict appointment datetime parsing in `packages/shared/src/index.ts`.
- Create/update appointment schemas now require real ISO datetimes with explicit `Z` or `+HH:MM` timezone.
- Extended `scripts/smoke-schedule-configuration.mjs` with API checks for `2027-02-29T10:00:00+04:00` and `2026-05-12T24:00:00+04:00`.
- Updated README and `docs/03-ux-principles.md`.

Cinematic Cheats used:
- None. This is schedule data integrity.

Exact Microseconds saved:
- 0 us Unity runtime.
- API adds fixed-size validation per supplied appointment datetime; it prevents silent schedule corruption instead of saving frame time.

Verification:
- `npm run typecheck -w @dental/shared` passed.
- `npm run build -w @dental/shared` passed.
- `npm run build -w @dental/api` passed.
- `npm run smoke:schedule-configuration` passed before and after full build.
- `npm run smoke:schedule-admin-guard` passed.
- `npm run smoke:schedule-autosave-retry` passed.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- `npm run smoke:documents-catalog` passed with `renderedCount:30`.
- `npm run build` passed; residual Vite warning: web `assets/index-BnM5NB7Z.js` 656.59 kB > 500 kB.

Residual:
- Historical appointment data is not migrated.
- External importers must normalize local clinic times to explicit-offset ISO before calling schedule mutation routes.
- Vite chunk size warning remains open.
## 2026-05-24 - Loop 28 - Document Fact Text Boundary

What was wrong -> Document mutation routes could store legacy mojibake Russian in legal payload/attestation facts. Render repair was not enough: issue response, audit passport, release journal and archived HTML can read stored facts directly.

What was done -> `apps/api/src/routes/documents.ts` now repairs create payloads, issue signature attestation and void attestation before persistence/snapshot. `scripts/smoke-document-issue-chains.mjs` now injects broken release/signature facts and proves readable Russian in public issue response, audit facts and archived immutable HTML. README and document-generation docs now describe the implemented storage-boundary repair.

Cinematic Cheats used -> None. This is deterministic text normalization at mutation boundary, not a visual/physics path.

Exact Microseconds saved -> 0 us claimed. Unity/frame runtime unaffected. API cost is bounded recursive string repair only on document mutation routes.

Evidence -> `npm run typecheck -w @dental/api`, `npm run typecheck -w @dental/shared`, `npm run build -w @dental/api`, `npm run smoke:document-issue-chains`, `npm run smoke:document-lifecycle`, `npm run smoke:api-text-encoding`, `npm run smoke:documents-catalog`, `npm run smoke:patient-forms-lifecycle`, `npm run smoke:visit-workflow-forms-lifecycle`, `npm run smoke:document-payloads`, `npm run smoke:document-guards`, `npm run smoke:telegram-bot`, `npm run smoke:schedule-configuration`, `npm run smoke:russian-fallback-source`, `npm run build`, then post-build `smoke:document-issue-chains`, `smoke:telegram-bot`, `smoke:api-text-encoding` passed.

Residual -> Historical documents saved before this boundary still need audit/migration if contaminated. No certified EDS/QES implemented. Vite still reports the web main chunk above 500 kB.

## 2026-05-24 - Loop 29 - Telegram Validation Text Boundary

What was wrong -> Telegram malformed-payload smoke accepted any response containing a readable Russian substring. That left room for mixed readable/mojibake 400 bodies and direct callback fallback text regressions, both visible to clinic operators or patients.

What was done -> `scripts/smoke-telegram-validation.mjs` now scans every controlled Telegram 400 response for mojibake markers, keeps admin/webhook secret leakage checks, and reads `apps/api/src/routes/telegram.ts` to require the generic callback acknowledgement source to remain readable Russian. README and `docs/13-dente-telegram-bot-plan.md` now document the validation proof.

Cinematic Cheats used -> None. This is deterministic API/output-boundary proof, not simulation or visual rendering.

Exact Microseconds saved -> 0 us claimed. Runtime code was not changed; the added cost is smoke-only route injection and source scanning.

Evidence -> `npm run build -w @dental/api`, `npm run typecheck -w @dental/api`, `npm run smoke:telegram-validation`, `npm run smoke:telegram-bot`, `npm run smoke:api-text-encoding`, `npm run smoke:russian-fallback-source`, and `npm run build` passed. Full build still reports the known web `assets/index-BnM5NB7Z.js` chunk above 500 kB.

Residual -> This is not dictionary-level i18n and not a live Telegram delivery test. The broad bot flow remains synthetic. Vite chunk size warning remains open.

## 2026-05-24 - Loop 30 - Document Payload Conditional Mounting

What was wrong -> Documents used selected-kind UI state, but all 27 structured payload cards still stayed mounted behind `hidden={selectedDocumentKind !== ...}`. That kept inactive legal/tax/patient/payment/workflow editors alive while a doctor edits one document packet.

What was done -> `apps/web/src/App.tsx` now conditionally mounts each `document-payload-card` only for its selected `DocumentKind`. `scripts/smoke-document-payload-ui-source.mjs` now fails on hidden payload-card regressions and requires every payload card to be selected-kind mounted. README, UX principles, and document-generation docs now state the implemented rule.

Cinematic Cheats used -> None. This is UI lifecycle gating, not physics or visual simulation. The practical cheat is not building inactive form DOM until the user selects that document kind.

Exact Microseconds saved -> 0 measured us. No profiler timing was taken; Unity/frame runtime unaffected. Browser DOM/render work is reduced for inactive document payload editors.

Evidence -> `npm run typecheck -w @dental/web`, `npm run build -w @dental/web`, `npm run build`, `npm run smoke:document-payload-ui-source`, `npm run smoke:web-render-gating-source`, `npm run smoke:documents-catalog`, default `npm run smoke:mobile`, and `SMOKE_SELECTOR=.documents-panel npm run smoke:mobile -- http://127.0.0.1:5173/#documents` passed. Full build still reports the known web `assets/index-CvTpyIQo.js` chunk above 500 kB.

Residual -> Main `App.tsx` is still too large and needs route/module splitting. Settings sub-tabs still have lower-priority hidden panels. No live clinic data or Telegram network call was used in this loop.

## 2026-05-24 - Loop 31 - Settings Tab Conditional Mounting

What was wrong -> Settings had the same hidden-DOM problem as the top-level routes and document payload editor: inactive clinic/access/Telegram/protocol/source/AI/import/audit panels were still mounted. First repair attempt used an over-broad regex and temporarily removed required JSX conditional closings.

What was done -> Restored compile by repairing the lost Imaging and Communications conditional closings. Then replaced Settings sub-tab hidden rendering with explicit `settingsTab` conditional mounts for 15 sections, including the existing shared sources/imports imaging-import route and the DICOM tool-state bundle condition. Extended `scripts/smoke-web-render-gating-source.mjs` to require those 15 tab gates and forbid `hidden={settingsTab !== ...}` regressions. Updated README and UX principles.

Cinematic Cheats used -> None. This is UI lifecycle gating. The practical cheat is not constructing inactive admin DOM until the operator opens that tab.

Exact Microseconds saved -> 0 measured us. Unity/frame runtime unaffected. Browser work is reduced for inactive Settings panels, but no profiler timing was taken.

Evidence -> `npm run typecheck -w @dental/web`, `npm run build -w @dental/web`, `npm run build`, `npm run smoke:web-render-gating-source`, `npm run smoke:document-payload-ui-source`, `npm run smoke:ui-preferences`, `npm run smoke:settings-persistence-file`, `npm run smoke:settings-preferences`, `npm run smoke:russian-fallback-source`, and mobile Edge smokes for `#settings/telegram`, `#settings/imports`, `#settings/sources` passed. Full build still reports web `assets/index-DuBrS_86.js` at 656.51 kB, above the 500 kB warning threshold.

Residual -> Main `App.tsx` still needs route/module code splitting. This loop did not call live Telegram or live clinic data. Vite chunk size warning remains open.
## 2026-05-24 - Scoped Telegram Outbox Runtime

What was wrong:
- Telegram status/webhook routes could resolve a selected clinic-owned bot by `organizationId` + `botConfigId`, but outbox list, single send and manual send-due still defaulted to singleton active settings.
- That mismatch could show the operator a healthy clinic-owned bot while preparing outbound messages with the shared DENTE token, old portal URL or old visual card.

What was done:
- `apps/api/src/routes/telegram.ts`: added query parsing and runtime-scope resolution for outbox list/send/send-due; single send and due batch now pass scoped settings/token context into delivery preparation.
- `apps/api/src/sampleData.ts`: outbox generation now accepts scoped Telegram settings, scopes chat-link/payment/review/post-visit queries by organization, and renders portal/review/maps/welcome image from the selected bot config.
- `apps/web/src/App.tsx`: Settings appends saved `organizationId` + `telegramBotConfigId` to outbox list/send/send-due in `clinic_owned_bot` mode.
- `scripts/smoke-telegram-control-ui-source.mjs`: source guard now requires scoped outbox query propagation.
- `scripts/smoke-telegram-bot.mjs`: behavior smoke now proves scoped outbox list, due dry-run and real send use the selected clinic bot token, portal host and visual card.
- `README.md` and `docs/13-dente-telegram-bot-plan.md`: documented implemented scoped outbox runtime behavior.

Cinematic Cheats used:
- No simulation/visual cheat involved. This is control-plane routing. The practical cheat is keeping runtime selection as a small server-side scope object instead of introducing a premature database layer.

Exact Microseconds saved:
- 0 us Unity/runtime frame impact.
- No browser microsecond claim. UI adds only bounded `URLSearchParams` construction for scoped Telegram actions.
- No Node microsecond claim. API adds a request-time env config lookup and existing bounded in-memory queue filters.

Verification:
- `npm run typecheck -w @dental/api` passed.
- `npm run typecheck -w @dental/web` passed.
- `npm run build -w @dental/api` passed before route-layer smoke.
- `npm run build` passed; residual Vite warning remains at `assets/index-D8WkVl88.js` 656.82 kB.
- `npm run smoke:telegram-bot` passed after API build.
- `npm run smoke:telegram-control-ui-source` passed.
- `npm run smoke:telegram-validation` passed.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:settings-preferences` passed.
- `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- `npm run smoke:documents-catalog` passed with `renderedCount:30`.
- `npm run smoke:russian-fallback-source` passed.
- `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed at 390 px with overflow 0.

Residual:
- Env JSON clinic bot configs are a prototype bridge. Production still needs encrypted DB-backed bot config storage and tenant auth.
- Live Telegram network was not called; transport proof is synthetic and checks the generated Bot API request.

## 2026-05-24 - Loop 33 - Immutable KND XML Source And Export Snapshots

What was wrong:
- KND 1151156 XML export could still read live patient, clinic profile and env tax-office data after document issue.
- That made issued HTML/PDF immutable, but left XML as a mutable artifact.
- Two newly added API error strings initially entered source in mojibake and were caught by `smoke:api-text-encoding`.

What was done:
- `packages/shared/src/index.ts`: added `TaxXmlSourceSnapshot` and `TaxXmlSnapshot` schemas, hidden from public generated-document DTOs and exposed only as audit hashes.
- `apps/api/src/routes/documents.ts`: issue now freezes patient/clinic/payment facts for KND XML; `/tax-xml` reuses frozen source facts and returns stored XML snapshots on later downloads.
- `apps/api/src/sampleData.ts`: persists `taxXmlSourceSnapshot` at issue and `taxXmlSnapshot` after first successful XML export with SHA-256 and source hash.
- `apps/api/src/db/schema.ts`, `apps/api/drizzle/0019_document_tax_xml_snapshot.sql`, and drizzle journal: added JSONB columns for XML source/export snapshots.
- `apps/web/src/App.tsx`: document passport shows XML source hash, archived XML hash and export timestamp.
- `scripts/smoke-tax-knd-xml.mjs`: proves non-self patient/clinic facts are frozen, first XML export is archived, and later patient/clinic/payment/tax-office mutations do not change XML bytes.
- `scripts/smoke-db-runtime-contract.mjs`: guards the new migration/schema contract.
- `README.md` and `docs/12-document-generation-forms.md`: document implemented immutable KND XML source/export snapshots without claiming signed FNS package readiness.

Cinematic Cheats used:
- None. This is legal/tax artifact immutability, not visual simulation.
- Practical data cheat: store a bounded issue-time source snapshot plus first XML bytes instead of rebuilding mutable XML.

Exact Microseconds saved:
- 0 us Unity/frame impact.
- No measured Node/browser microsecond savings. API adds bounded JSON clone/hash on issue and one XML SHA-256 on first export; later XML downloads avoid regeneration and return stored bytes.

Verification:
- `npm run typecheck -w @dental/shared` passed.
- `npm run typecheck -w @dental/api` passed.
- `npm run typecheck -w @dental/web` passed.
- `npm run build -w @dental/shared` passed.
- `npm run build -w @dental/api` passed.
- `npm run build -w @dental/web` passed with known Vite chunk warning.
- `npm run build` passed; known Vite warning remains at `assets/index-DEyo19Ro.js` 657.96 kB.
- `npm run smoke:db-runtime-contract` passed.
- `npm run smoke:tax-knd-xml` passed with frozen source/XML flags.
- `npm run smoke:document-payload-ui-source` passed.
- `npm run smoke:document-lifecycle` passed.
- `npm run smoke:document-issue-chains` passed.
- `npm run smoke:documents-catalog` passed.
- `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- `npm run smoke:russian-fallback-source` passed.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:telegram-bot` passed for the synthetic `@dentecrm_bot` flow.
- `SMOKE_SELECTOR=.documents-panel SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#documents` passed at 390 px with overflow 0.

Residual:
- XML is still an unsigned draft export; ТКС signing and XSD validation remain separate release work.
- Older issued tax certificates without `taxXmlSourceSnapshot` intentionally return 409 instead of reconstructing from mutable data.
- Main web bundle remains above Vite's 500 kB warning threshold.

## 2026-05-24 - Loop 34 - First-Run Source Configuration

What was wrong:
- The first-run source step explained safe import but mostly routed the user to Settings.
- A new clinic could leave onboarding without actually setting persistent defaults for price lists, patient migration, mixed import, document ingestion, imaging, DICOMweb, or OHIF.

What was done:
- `apps/web/src/App.tsx`: added inline source setup to the onboarding `sources` step, bound to existing persisted UI preference state.
- Source defaults now cover price-list source, patient import source, smart import mode, document ingestion target, imaging source, DICOMweb root, and OHIF root.
- Source changes clear stale preview/commit state where relevant, so old import previews do not masquerade as the newly selected source.
- `apps/web/src/styles/main.css`: added compact responsive source sections, chip buttons, and URL fields; mobile collapses the grid to one column.
- `scripts/smoke-onboarding-configuration-source.mjs`: added guards for inline source configuration, setters, Russian labels, autosave wording, DICOMweb, and OHIF.
- `README.md` and `docs/03-ux-principles.md`: recorded the implemented first-run source configuration contract.

Cinematic Cheats used:
- None. This is onboarding/configuration UX, not physical or visual simulation.
- Practical UX shortcut: reuse the existing preference save pipeline instead of adding a second setup store.

Exact Microseconds saved:
- 0 us Unity/frame impact.
- No measured browser microsecond savings. The change adds visible onboarding controls only on the active source step and avoids extra Settings navigation during first setup.

Verification:
- `npm run typecheck -w @dental/web` passed.
- `npm run build -w @dental/web` passed with known Vite chunk warning.
- `npm run build` passed for shared, api and web; known Vite warning remains at `assets/index-CU-8Mjes.js` 661.88 kB.
- `npm run smoke:onboarding-configuration-source` passed with `onboarding-sources-persisted-configuration`.
- `npm run smoke:ui-preferences` passed with `requiredPreferenceCount:44`.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:settings-preferences` passed.
- `npm run smoke:russian-fallback-source` passed.
- `SMOKE_SELECTOR=.onboarding-source-config SMOKE_CLICK_SELECTOR=.onboarding-step-list button:nth-child(6) SMOKE_DISMISS_ONBOARDING=0 npm run smoke:mobile -- http://127.0.0.1:5173/#shift` passed at 390 px with overflow 0.

Residual:
- Onboarding sets defaults; detailed preview/commit still belongs to Settings/Sources and Settings/Imports.
- DICOMweb/OHIF URLs are saved but not live-probed inside onboarding.
- Main web bundle remains above Vite's 500 kB warning threshold.

## 2026-05-24 - Loop 35 - Scoped Telegram Link And Chat Bindings

What was wrong:
- Telegram status/webhook/outbox were scoped by `organizationId + botConfigId`, but link-code and chat-link ledgers were not strict enough.
- A code generated for one clinic-owned bot could be attempted through another bot config, and scoped outbox readiness could rely on a chat link created by the wrong bot.
- The Settings site scoped outbox actions but did not consistently scope link-code/chat-link lists, revocation, and create-code payloads.

What was done:
- `packages/shared/src/index.ts`: added `botConfigId` to link-code/chat-link schemas and create-code input.
- `apps/api/src/sampleData.ts`: scoped link-code create/list/consume, chat-link list/revoke, active-chat lookup, staff digest generation, linked schedule/contact/document/care request callbacks, and outbox item construction by bot config.
- `apps/api/src/routes/telegram.ts`: resolved create/list/revoke/webhook flows through the selected runtime, preserved foreign-clinic rejection, and passed `organizationId`, `clinicId`, and `botConfigId` into link consumption and patient action handlers.
- `apps/web/src/App.tsx`: appended saved runtime scope to link-code/chat-link ledgers and revoke calls; create-code sends selected `organizationId` and `botConfigId` in clinic-owned mode.
- `scripts/smoke-telegram-bot.mjs`: added primary/secondary bot config scenario proving a primary code is rejected by the secondary webhook, creates no secondary chat link, remains pending for the primary, then links and enables only the primary scoped outbox.
- `scripts/smoke-telegram-control-ui-source.mjs`: added source guards for scoped link/chat/outbox propagation and scoped schema/route markers.
- `README.md` and `docs/13-dente-telegram-bot-plan.md`: documented only the implemented scoped link/chat behavior and proof.

Cinematic Cheats used:
- None. This is tenant/runtime routing, not simulation.
- Practical implementation shortcut: prototype state uses bounded in-memory filtering and env JSON runtime configs; production can replace this with encrypted DB-backed bot configs without changing the public route shape.

Exact Microseconds saved:
- 0 us Unity/frame impact.
- No browser timing claim. Runtime cost added is one extra `botConfigId` string comparison in prototype ledger filters and URLSearchParams construction in Settings.

Verification:
- `npm run typecheck -w @dental/shared` passed.
- `npm run typecheck -w @dental/api` passed.
- `npm run typecheck -w @dental/web` passed.
- `npm run build -w @dental/shared` passed.
- `npm run build -w @dental/api` passed.
- `npm run build` passed for shared, api and web; known Vite warning remains at `assets/index-BDxbPAyE.js` 662.01 kB.
- `npm run smoke:telegram-bot` passed with scoped multi-bot link isolation and scoped outbox readiness.
- `npm run smoke:telegram-control-ui-source` passed.
- `npm run smoke:telegram-validation` passed.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:settings-preferences` passed.
- `npm run smoke:ui-preferences` passed.
- `npm run smoke:russian-fallback-source` passed.
- `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- `SMOKE_SELECTOR=.telegram-settings SMOKE_DISMISS_ONBOARDING=1 npm run smoke:mobile -- http://127.0.0.1:5173/#settings/telegram` passed at 390 px with overflow 0.

Residual:
- `DENTE_TELEGRAM_CLINIC_BOTS_JSON` remains prototype storage; production still needs encrypted DB-backed bot configs and tenant authorization.
- Existing prototype records without `botConfigId` are normalized to current runtime on load, not historically attributed.
- Main web bundle remains above Vite's 500 kB warning threshold.
## 2026-05-24 - Loop 36 - Telegram Tenant Callback Ownership

What was wrong:
- Second-clinic Telegram link-code issue still validated patient/staff subjects and clinic against the singleton demo clinic.
- After a scoped chat binding was found, tax/document, care-topic and contact callbacks could still create communication tasks/events/audit under the default organization path.

What was done:
- `createDenteTelegramLinkCode` now validates subjects inside the resolved runtime organization.
- `/api/telegram/link-codes` now rejects clinic mismatch against the resolved runtime clinic before issuing a code.
- Telegram document/care/contact handoff helpers now use `chatLink.organizationId` for patient lookup, duplicate task lookup, new tasks, communication events and audit records.
- `recordAuditEvent` accepts an optional organization override while preserving the old default for unrelated callers.
- `smoke:telegram-bot` now creates a second-organization patient, links through that clinic-owned bot config, then presses `dente:tax`, `dente:care-implant`, and `dente:contact`; it asserts tasks/events/audit stay in the second organization and Telegram sends use the second clinic token.
- README and `docs/13-dente-telegram-bot-plan.md` now document scoped subject/clinic validation and linked-chat handoff ownership.

Cinematic Cheats used:
- None. This is tenant ownership and workflow routing, not a visual/physics system.

Exact Microseconds saved:
- 0 us Unity runtime.
- API overhead is one organization/clinic string comparison and scoped in-memory lookup per link/callback in prototype state.

Evidence:
- `npm run typecheck -w @dental/api` passed.
- `npm run build -w @dental/api` passed.
- `npm run build` passed; residual Vite warning: web `assets/index-BDxbPAyE.js` 662.01 kB > 500 kB.
- `npm run smoke:telegram-bot` passed.
- `npm run smoke:telegram-control-ui-source` passed.
- `npm run smoke:telegram-validation` passed.
- `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- `npm run smoke:russian-fallback-source` passed.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:settings-preferences` passed.

Residual risk:
- Env JSON bot config storage is still prototype-only.
- Appointment callback signature input still needs bot/runtime scope hardening in a separate loop.
- Web bundle chunk warning remains open.

## 2026-05-25 - Loop 37 - Telegram Appointment Callback Scope

What was wrong:
- Appointment confirm/reschedule/call callback signatures were scoped by global organization data, appointment id, action and expiry, but not by `clinicId` or `botConfigId`.
- With multi-bot linking, the same patient/chat can be valid in two bot configs. A callback generated by the primary bot needed cryptographic rejection when replayed through the secondary webhook.

What was done:
- Added normalized appointment callback scope: `organizationId`, `clinicId`, `botConfigId`.
- Changed appointment callback HMAC input to `organizationId:clinicId:botConfigId:appointmentId:action:expiry`.
- Passed runtime `clinicId` from Telegram webhook routing into callback verification.
- Passed runtime bot scope into outbox and linked schedule inline-button generation.
- Extended `smoke:telegram-bot` with a real cross-bot replay case: primary and secondary bot links for the same patient/chat, primary appointment button replayed through secondary webhook, rejection asserted, appointment/task/event/audit counts unchanged.
- Extended `smoke:telegram-control-ui-source` to guard scoped callback normalization, HMAC input and route parsing markers.
- Updated README and `docs/13-dente-telegram-bot-plan.md` with the scoped callback contract and replay proof.

Cinematic Cheats used:
- None. This is Telegram routing/signature ownership, not rendering/physics.

Exact Microseconds saved:
- 0 us Unity runtime.
- No browser-path savings claimed.
- API adds one longer HMAC input string per appointment button generation/verification. This is constant-size work and not a frame path.

Evidence:
- `npm run typecheck -w @dental/api` passed.
- `npm run build -w @dental/api` passed.
- `npm run build` passed for shared, api and web; residual Vite warning: web `assets/index-BDxbPAyE.js` 662.01 kB > 500 kB.
- `npm run smoke:telegram-bot` passed after the cross-bot appointment replay regression was added.
- `npm run smoke:telegram-validation` passed.
- `npm run smoke:api-text-encoding` passed with `mojibakeHits:0`.
- `npm run smoke:russian-fallback-source` passed.
- `npm run smoke:telegram-control-ui-source` passed.
- `npm run smoke:settings-persistence-file` passed.
- `npm run smoke:settings-preferences` passed.

Residual risk:
- Appointment buttons generated before this change will fail verification after deploy; patients need a fresh `/schedule` response or next reminder.
- Env JSON bot config storage is still prototype-only; production needs encrypted DB-backed configs, tenant auth and webhook-secret rotation.
- Official outpatient medical-card `025/у` exact generation remains open pending source-verified field mapping.
- Web bundle chunk warning remains open.

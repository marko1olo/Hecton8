# DENTE Status

Status: VERIFIED
Domain: dental-crm site + Telegram bot
Task count: open-ended product hardening

## Loop 1

- [x] Scope extracted from user prompt.
  - DOD practice: bounded to DENTE site/API/bot; HECTON8 Unity runtime domains left untouched.
  - Rejected: broad Unity/gameplay edits without relation to DENTE.
  - Estimate: 0 us runtime.
- [x] Relevant mandates read.
  - DOD practice: UI localization, UI data cadence, persistence, evidence reporting, signal segregation.
  - Rejected: physics/render mandates because this is CRM web/API work.
  - Estimate: 0 us runtime.
- [x] Product gaps identified.
  - DOD practice: static source search plus focused subagent review; evidence class remains STATIC_SOURCE until tests pass.
  - Rejected: claiming full release readiness from source inspection.
  - Estimate: 0 us runtime.

## Loop 2

- [x] Fix active-shift resource load date scoping.
  - DOD practice: chair/doctor/assistant loads now count active clinic date only; utilization is capped to schema range while booked minutes remain factual.
  - Rejected: reverting chair capacity to clinic schedule, because that hides per-chair availability.
  - Estimate: 0 us Unity runtime; Node array filter cost is small and not profiled.
- [x] Verify schedule smoke after chair working-hours capacity change.
  - DOD practice: `npm run smoke:schedule-configuration`.
  - Rejected: accepting previous 409 without fixing contract/data mismatch.
  - Estimate: 0 us claimed; no profiler artifact.
- [x] Verify Telegram bot and UI source smokes after inline keyboard/image work.
  - DOD practice: bot, settings persistence, source guards, Russian fallback, API text encoding, and mobile overflow smoke.
  - Rejected: screenshot proof because Edge screenshot capture hung twice without artifact.
  - Estimate: 0 us claimed; browser check is layout evidence only.
- [x] Append final log.
  - DOD practice: report appended to `Docs/AgentLogs/LOG_DENTE.md`.
  - Rejected: chat-only report.
  - Estimate: 0 us runtime.

## Loop 3

- [x] Fix tax document payment scope.
  - DOD practice: tax application, certificate, and registry now use the same explicit selected fiscal payment ids; year/payer/form changes only prune invalid ids and do not silently select all.
  - Rejected: auto-selecting every eligible receipt because it can generate an application for more payments than the doctor selected.
  - Estimate: 0 us Unity runtime; React set filtering is bounded by visible payment count.
- [x] Fix payment receipt payer facts.
  - DOD practice: payment receipt payer fields come from selected payment ledger or explicit receipt fields; patient-card fallbacks were removed, and API guards reject selected payments missing payer facts.
  - Rejected: using patient profile as a payer fallback for real fiscal/tax-support receipts.
  - Estimate: 0 us Unity runtime; one selected-payment loop in API guard.
- [x] Fix tax INN comparison.
  - DOD practice: document/payment INN comparisons normalize to digits while existing schema validation still owns legal length.
  - Rejected: raw `trim()` comparison because formatted INN breaks real operator input.
  - Estimate: 0 us Unity runtime; string normalization per selected payment.
- [x] Fix schedule cross-date suggestions.
  - DOD practice: schedule gap/buffer hints compare adjacent appointments only inside the same clinic-local date.
  - Rejected: continuous timeline comparison across midnight because it produces false operational hints.
  - Estimate: 0 us Unity runtime; one clinic-date key comparison per adjacent pair.
- [x] Verify document, tax, schedule, Telegram, onboarding, settings persistence, Russian fallback.
  - DOD practice: typecheck, production build, and targeted smoke suite passed.
  - Rejected: source-only report after behavior-affecting changes.
  - Estimate: 0 us Unity runtime; Node/Vite only.

## Loop 4

- [x] Fix Telegram outbox filtering and paging.
  - DOD practice: API now owns status/template filters, count-before-page totals, cursor paging, and due-send selection; Settings UI sends filters to API and uses `Показать еще`.
  - Rejected: browser-only filtering and `slice(0, 12)` as queue truth, because it breaks large reminder batches.
  - Estimate: 0 us Unity runtime; Node list filtering only, no profiler claim.
- [x] Fix first-run readiness for active appointment schedule/team blockers.
  - DOD practice: onboarding completion now reads the same active appointment readiness checks used by the schedule.
  - Rejected: allowing setup completion from only generic clinic/staff/chair presence.
  - Estimate: 0 us Unity runtime; one readiness lookup over dashboard DTO.
- [x] Verify Telegram, onboarding and schedule regressions.
  - DOD practice: typecheck, production build, Telegram bot smoke, Telegram UI source smoke, onboarding source smoke, and schedule smoke passed.
  - Rejected: relying on source inspection after API response contract changes.
  - Estimate: 0 us Unity runtime; Vite/Node only.

## Loop 5

- [x] Fix Telegram link-code and chat-link ledger paging.
  - DOD practice: link-code and chat-link APIs now own status/subject filters, count-before-page totals, cursor paging, pending/used/expired/revoked/active counters, and public-only DTOs.
  - Rejected: fixed latest-50 lists and browser-only truncation, because busy clinics would lose operational history.
  - Estimate: 0 us Unity runtime; Node list filtering only, no profiler claim.
- [x] Fix Settings Telegram ledger UX for larger clinics.
  - DOD practice: Settings now stores ledger response metadata, shows "shown from filtered total" counts, and loads more link codes/chat links by cursor.
  - Rejected: increasing `slice()` limits, because it only delays the same hidden-data failure.
  - Estimate: 0 us Unity runtime; React state merge over visible page ids.
- [x] Audit document/forms readiness with subagent evidence.
  - DOD practice: document catalog and tax smoke suite passed; remaining release gaps recorded as PDF export, signed/XSD-validated tax XML, and privileged issued-facts API.
  - Rejected: claiming all official documents are release-complete without PDF/signature/XSD artifacts.
  - Estimate: 0 us Unity runtime; documentation and CLI smoke evidence only.
- [x] Verify Telegram ledgers and control UI source.
  - DOD practice: typecheck, production build, Telegram bot smoke, and Telegram control UI source smoke passed after the ledger contract change.
  - Rejected: accepting stale `dist` smoke failure before rebuilding.
  - Estimate: 0 us Unity runtime; Vite/Node only.

## Loop 6

- [x] Fix scoped multi-clinic Telegram runtime routing.
  - DOD practice: status/webhook routes can now resolve server-only clinic bot runtime by `organizationId` and `botConfigId`, including bot username, token, webhook secret, portal/review/maps URLs, clinic id, and bot config id.
  - Rejected: singleton active settings as the only runtime route, because it prevents one deployment from serving multiple clinic-owned bots.
  - Estimate: 0 us Unity runtime; request-time env JSON scan only, no profiler claim.
- [x] Block ambiguous same-organization bot selection.
  - DOD practice: if one organization has multiple bot configs, `:organizationId`-only status/webhook routes do not silently pick the first token; callers must provide `:botConfigId`.
  - Rejected: first-match fallback because it can send replies through the wrong clinic bot.
  - Estimate: 0 us Unity runtime; small list filter over server runtime config.
- [x] Verify scoped bot routing and saved settings persistence.
  - DOD practice: typecheck, production build, Telegram bot smoke, Telegram UI source smoke, Telegram validation smoke, and settings persistence smoke passed.
  - Rejected: source-only report after token/secret routing changes.
  - Estimate: 0 us Unity runtime; Node/Vite only.

## Loop 7

- [x] Add document issue passport API and archive download route.
  - DOD practice: `/api/documents/:id/audit-facts` returns source authority/status, snapshot sha256, archive/download/XML availability, blockers and warnings without exposing local snapshot paths; `/html?download=1` sets an HTML attachment for issued or later-voided archived snapshots.
  - Rejected: labeling current HTML as PDF or claiming signed FNS output before a real PDF/signature/XSD contour exists.
  - Estimate: 0 us Unity runtime; one document/patient lookup and snapshot hash verification on request.
- [x] Add operator UI for document passport and archived HTML.
  - DOD practice: Documents screen has `Паспорт`, `Скачать HTML`, archive/source facts, blockers/warnings, and mobile-responsive layout.
  - Rejected: hiding verification behind the generic `Открыть` link or forcing operators to infer issue state from status chips.
  - Estimate: 0 us Unity runtime; React state for one selected passport panel.
- [x] Verify document lifecycle and adjacent legal/tax regressions.
  - DOD practice: typecheck, production build, document lifecycle/source/catalog/guard/KND/chain/legal/API encoding smokes passed; mobile overflow passed on Documents list.
  - Rejected: source-only report after API/UI behavior changes.
  - Estimate: 0 us Unity runtime; Vite/Node/browser smoke only.

## Loop 8

- [x] Add real issued-document PDF export.
  - DOD practice: `/api/documents/:id/pdf` prints the immutable issued HTML snapshot through server-side Chromium/Edge and returns a `%PDF` attachment; draft documents are blocked and missing browser returns a service error instead of fake output.
  - Rejected: client-only print instructions, fake PDF button, or a PDF generated from mutable current patient/profile data.
  - Estimate: 0 us Unity runtime; one browser print process per PDF request.
- [x] Expose PDF export in the operator workflow.
  - DOD practice: audit facts now include PDF availability/download URL; Documents UI and passport panel show `Скачать PDF` only for issued/voided archived documents.
  - Rejected: making PDF available for drafts, because legal export must come from the frozen issued snapshot.
  - Estimate: 0 us Unity runtime; React button/action only.
- [x] Verify PDF export and adjacent regressions.
  - DOD practice: lifecycle smoke asserts real `%PDF` attachment >1 KB from issued snapshot; typecheck, build, UI source, document, tax, encoding, legal and mobile checks passed.
  - Rejected: accepting source-only proof for PDF generation.
  - Estimate: 0 us Unity runtime; Node/Vite/browser only.

## Loop 9

- [x] Expand Telegram document-ready and tax-status inline keyboards.
  - DOD practice: outbox reply markup now adds portal, documents/tax, clinic-contact and privacy actions for document-ready and tax-status messages while preserving generic non-PHI text and visual-card delivery.
  - Rejected: one-link document notification and command-only tax status, because they leave patients in a dead end and hide the real button UX from the Settings preview.
  - Estimate: 0 us Unity runtime; one small reply-markup branch per outbox item.
- [x] Verify outbox visual/card behavior and no PHI leaks.
  - DOD practice: Telegram smoke checks document-ready/tax-status buttons, configured visual card, photo fallback, no fiscal receipt, no payer INN, no amount, no PDF/file text, no clinical/task detail in title/preview.
  - Rejected: source-only proof for bot delivery markup.
  - Estimate: 0 us Unity runtime; Node Telegram transport mock only.
- [x] Verify site preview and mobile Settings page.
  - DOD practice: source smoke verifies exact outbox markup is still returned for the Settings UI; mobile overflow smoke passed on `#settings/telegram` with `.telegram-settings` visible and no horizontal overflow.
  - Rejected: changing bot markup without checking the operator-facing site.
  - Estimate: 0 us Unity runtime; browser layout smoke only.

## Loop 10

- [x] Fix Telegram portal handoff dead end.
  - DOD practice: `Открыть DENTE` now routes by safe section intent only: `dente_source=telegram` plus `dente_section=documents|tax|billing|care|schedule`; no patient/document/appointment/payment ids are embedded.
  - Rejected: raw `patientPortalBaseUrl` for every button, because it sends patients to a generic landing page and hides the real next action.
  - Estimate: 0 us Unity runtime; constant URL construction in Node/API only.
- [x] Apply section handoff to webhook menus and outbox previews.
  - DOD practice: documents/tax/care callbacks and document/payment/post-visit/recall outbox templates share the same button-first section route while keeping Telegram text non-PHI.
  - Rejected: per-document or per-patient deep links before a real authenticated portal identity layer exists.
  - Estimate: 0 us Unity runtime; one URL object per generated keyboard row.
- [x] Verify source, dist, bot behavior and mobile settings page.
  - DOD practice: typecheck, production build, Telegram bot smoke, Telegram UI source smoke, validation, persistence, document/tax adjacent smokes, Russian fallback/API encoding, and mobile settings overflow checks passed after rebuild.
  - Rejected: accepting the first bot smoke failure, because it used stale `apps/api/dist` before rebuild.
  - Estimate: 0 us Unity runtime; Node/Vite/browser only.

## Loop 11

- [x] Add web-side Telegram section handoff.
  - DOD practice: the web shell reads only `dente_source=telegram` plus `dente_section`, opens the matching DENTE section, preselects document/tax form intent when applicable, shows a Russian notice, and strips the query from browser history.
  - Rejected: leaving the API/bot section URL as a dead letter; storing Telegram handoff in local preferences; accepting patient/document/appointment/payment ids from Telegram URLs.
  - Estimate: 0 us Unity runtime; one browser URL parse and bounded React state update on first load only.
- [x] Verify source and browser behavior.
  - DOD practice: `smoke:telegram-handoff-source`, typecheck, production build, and mobile browser smokes for tax, billing, care and schedule passed; malicious `patientId`, `documentId`, `appointmentId`, and `paymentId` query keys were removed from the final URL.
  - Rejected: source-only proof after changing first-load routing.
  - Estimate: 0 us Unity runtime; Vite/Node/browser only.
- [x] Verify adjacent Telegram, persistence and Russian fallback.
  - DOD practice: Telegram bot smoke, Telegram control UI source smoke, settings persistence smoke, Russian fallback source smoke, and API text encoding smoke passed after the web handoff change.
  - Rejected: assuming bot buttons still worked because the previous API-only handoff smoke passed.
  - Estimate: 0 us Unity runtime; Node smoke only.
- [x] Update docs and logs.
  - DOD practice: README, Telegram bot plan, Status, Rationale and LOG now record the web-side handoff and its proof artifacts.
  - Rejected: chat-only report.
  - Estimate: 0 us runtime.

## Loop 12

- [x] Close Telegram `home` handoff mismatch.
  - DOD practice: web handoff now accepts the same `home|documents|tax|billing|care|schedule` section set emitted by API/bot, maps `home` to `#shift`, and keeps the Russian notice.
  - Rejected: leaving `home` as a bot-only query value, because `/start`, `/help`, `/clinic` and privacy buttons can open DENTE with a stale unconsumed section.
  - Estimate: 0 us Unity runtime; one first-load browser URL parse only.
- [x] Fix URL cleanup timing.
  - DOD practice: Telegram query cleanup now runs immediately on mount and re-applies the selected section/form after preferences hydrate, so slow settings sync cannot leave `patientId` or stale `dente_section` in the address bar.
  - Rejected: waiting for server preference hydration before cleanup; the mobile smoke proved that left the query in `location.href`.
  - Estimate: 0 us Unity runtime; one `history.replaceState` on entry.
- [x] Replace English local-bridge fallback copy.
  - DOD practice: OCR/OHIF/system bridge roles, hints, warnings and next actions now use Russian fallback text; the Russian fallback smoke now forbids the old English strings.
  - Rejected: keeping technical setup hints in English, because those surface in settings/readiness flows.
  - Estimate: 0 us runtime; API response text only.
- [x] Verify source, compile, build, bot and mobile behavior.
  - DOD practice: handoff source smoke, Russian fallback source smoke, typecheck, production build, API text encoding, Telegram bot smoke, and mobile home handoff smoke passed; mobile proof ended at `http://127.0.0.1:5173/#shift` with no query and no horizontal overflow.
  - Rejected: accepting the first mobile smoke where `.shift-hero` was visible but `location.href` still contained `patientId`.
  - Estimate: 0 us Unity runtime; Vite/Node/browser only.

## Loop 13

- [x] Persist operator payment selection for real documents.
  - DOD practice: tax document payment selections now hydrate/save by patient/year/payer; payment receipt selections hydrate/save by patient/visit and no longer reset to every eligible payment after reload.
  - Rejected: putting payment ids into generic UI preferences or continuing effect-level auto-select-all, because doctors expect document configuration to stay as explicitly chosen until changed.
  - Estimate: 0 us Unity runtime; browser localStorage read/write only on document selection scope changes.
- [x] Add regression guard for selection persistence.
  - DOD practice: document UI source smoke now requires the dedicated document-payment selection store, hydration refs, and persistence keys, and forbids the old receipt reset pattern.
  - Rejected: relying on source review without a guard for this fragile effect behavior.
  - Estimate: 0 us runtime; source smoke only.
- [x] Verify document, tax, build and mobile behavior.
  - DOD practice: document payload UI source smoke, tax explicit payment scope smoke, document payload smoke, typecheck, production build, and mobile Documents overflow smoke passed.
  - Rejected: claiming release readiness without compile/build/browser evidence.
  - Estimate: 0 us Unity runtime; Vite/Node/browser only.

## Loop 14

- [x] Replace remaining English/technical STT/OCR/DICOM operator copy.
  - DOD practice: local bridge use plans and speech gateway warnings now use Russian doctor/admin wording for dictation, OCR, price-photo OCR, imaging import, provider reserve chains, local text recovery, and local bridge readiness.
  - Rejected: leaving `fallback`, `chunks`, `transcript`, `cooldown`, `prompt pack`, `server env`, and English plan titles in visible readiness/actions.
  - Estimate: 0 us Unity runtime; static API response text only.
- [x] Add source guard for Russian fallback/action copy.
  - DOD practice: `smoke-russian-fallback-source` now requires the new Russian snippets and forbids the old English/technical phrases.
  - Rejected: manual source review without a regression guard.
  - Estimate: 0 us runtime; source smoke only.
- [x] Verify localization, bot and schedule regressions.
  - DOD practice: Russian fallback source, UI language preferences, API text encoding, typecheck, production build, Telegram bot smoke, and schedule configuration smoke passed.
  - Rejected: treating text-only changes as safe without build and adjacent workflow smokes.
  - Estimate: 0 us Unity runtime; Node/Vite only.

## Loop 15

- [x] Persist Telegram QR target selection.
  - DOD practice: QR patient/staff mode and selected staff id now live in shared UI preferences, browser autosave, API settings preferences, and file-backed persistence with stale-save protection.
  - Rejected: leaving QR target selection as transient React state, because reception/admin staff repeatedly generate the same patient/staff connection flow.
  - Estimate: 0 us Unity runtime; browser preference write and API JSON persistence only.
- [x] Add operator QR quick actions.
  - DOD practice: generated QR card now has copy-code, copy-deep-link, copy-share-text, QR SVG download, and visible action state; changing the QR target clears stale generated output.
  - Rejected: relying on raw code selection/manual copy as the primary operator workflow.
  - Estimate: 0 us Unity runtime; click-time DOM clipboard/blob work only.
- [x] Make Telegram linking QR-first in real bot replies.
  - DOD practice: `/start`, `/clinic`, non-private link rejection, invalid code rejection, and rejected-code inline keyboard now point patients back to clinic QR first while manual code remains fallback.
  - Rejected: command/manual-code-first onboarding, because the requested bot UX is button/QR led.
  - Estimate: 0 us Unity runtime; static Telegram reply text and constant-size keyboard rows only.
- [x] Add regression evidence and docs.
  - DOD practice: source smokes now require QR preference fields, QR copy/download actions, QR-first route text, and settings endpoint persistence; README and Telegram plan document the behavior.
  - Rejected: documentation-only claim without executable guards.
  - Estimate: 0 us runtime.
- [x] Verify QR/settings/bot/site behavior.
  - DOD practice: source smokes, full typecheck, production build, API build after final route text, settings persistence smokes, Telegram bot smoke, API encoding smoke, and mobile Telegram settings overflow smoke passed.
  - Rejected: accepting the first Telegram bot smoke failure; it exposed rejected-code text that still did not send patients back to QR-first linking.
  - Estimate: 0 us Unity runtime; Node/Vite/browser only.

## Loop 16

- [x] Add visual clinic card to review/map bot replies.
  - DOD practice: `/review` and `dente:map` now reuse the configured `welcomeImageUrl` via `sendPhoto`, matching start/documents/care card behavior while keeping only clinic-level HTTPS review/maps URLs.
  - Rejected: leaving review/map as text-only replies, because the requested bot UX is inline buttons plus visuals rather than command-style text.
  - Estimate: 0 us Unity runtime; one constant-size Telegram photo payload in Node per reply when configured.
- [x] Close after-care callback coverage for filling and hygiene.
  - DOD practice: Telegram smoke now drives `dente:care-filling` and `dente:care-hygiene`, verifies doctor-owned task creation, workflow codes, repeat reuse, visual card delivery, care portal handoff and administrator fallback.
  - Rejected: source-only guard for the two extra buttons, because a visible button without behavior proof is a release trap.
  - Estimate: 0 us Unity runtime; Node smoke over in-memory sample ledgers only.
- [x] Update DENTE docs for the changed bot behavior.
  - DOD practice: README and Telegram plan now state review/map visual-card delivery and four-topic care callback smoke coverage.
  - Rejected: chat-only report and broad roadmap inflation.
  - Estimate: 0 us runtime.
- [x] Verify bot, source guards, localization, encoding, compile and production build.
  - DOD practice: `smoke:telegram-bot`, `smoke:telegram-control-ui-source`, `smoke:russian-fallback-source`, `smoke:api-text-encoding`, full `typecheck`, and full `build` passed.
  - Rejected: accepting the first Telegram bot smoke failure before rebuilding API dist; the smoke imports `apps/api/dist`.
  - Estimate: 0 us Unity runtime; Node/Vite only.

## Loop 17

- [x] Convert clear Telegram document free text into real DENTE work.
  - DOD practice: `freeTextReplyFor` now routes explicit tax, medical-record and patient-form phrases to `createDenteTelegramDocumentRequest`, so linked patients create or reuse the same administrator tasks as inline buttons while broad `документы` still opens the menu.
  - Rejected: leaving typed phrases as informational menu replies, because patients naturally write `нужна справка для налоговой` or `нужна выписка из медкарты` and expect the clinic queue to receive work.
  - Estimate: 0 us Unity runtime; constant-size Node route branch and existing in-memory/file-backed communication task mutation only.
- [x] Remove button-only wording from document request facts.
  - DOD practice: task bodies and audit reasons now say the patient requested/sent a Telegram document request, not specifically that a button was pressed.
  - Rejected: duplicating separate task creators for text and buttons, because one workflow code per document topic is the stable owner route.
  - Estimate: 0 us runtime; string-only API data.
- [x] Add behavior proof for tax/medical/forms text requests.
  - DOD practice: `smoke:telegram-bot` now drives linked-patient free-text tax request reuse, medical-document request creation and patient-form request creation; it verifies workflow codes, communication events, audit actions, visual-card `sendPhoto`, portal section handoff and no raw chat id leakage.
  - Rejected: source-only checks for strings, because button-first bot behavior must be proven through the webhook transport path.
  - Estimate: 0 us runtime; smoke-only sample ledger checks.
- [x] Fix the Telegram outbox PHI guard false positive.
  - DOD practice: the outbox guard no longer treats any clock minute `:36` as a leaked tooth number; it now searches clinical forms such as `лечение 36` or `зуб 36`.
  - Rejected: weakening the guard entirely; it still fails on real tooth/treatment leakage.
  - Estimate: 0 us runtime; test regex only.
- [x] Update DENTE bot docs and verify.
  - DOD practice: README and Telegram plan now document that clear free-text document phrases create the same admin tasks as buttons.
  - Rejected: chat-only report.
  - Estimate: 0 us runtime.
- [x] Verify API, bot, localization, encoding and production build.
  - DOD practice: `typecheck -w @dental/api`, `build -w @dental/api`, `smoke:telegram-control-ui-source`, `smoke:russian-fallback-source`, `smoke:api-text-encoding`, `smoke:telegram-bot`, full `typecheck`, full `build`, and post-build `smoke:telegram-bot` passed.
  - Rejected: accepting the first Telegram bot smoke failure; it exposed a time-dependent test guard, not a product route failure.
  - Estimate: 0 us Unity runtime; Node/Vite only.

## Loop 18

- [x] Add signed issue attestation to the document lifecycle.
  - DOD practice: shared schema now requires issue signature mode/time/recipient/staff plus identity, opened-document, recipient-signature, and clinic-signature confirmations; `/api/documents/:id/issue` rejects missing attestation before status mutation.
  - Rejected: treating printed signature lines as proof of legal receipt, because a blank paper line does not create a durable system fact.
  - Estimate: 0 us Unity runtime; constant-size API validation and one stored DTO per issued document.
- [x] Add medical release journal entries.
  - DOD practice: medical-record copy request, medical-record extract, and medical-document release receipt issue paths now stamp request/extract/release journal facts with recipient, authority, delivery/material scope, source snapshot hash, and staff issuer.
  - Rejected: burying release proof only in rendered HTML; audit-facts must expose a structured route for later registry/export work.
  - Estimate: 0 us Unity runtime; one bounded JSON object per relevant issued document.
- [x] Make issue UX hard to misclick and reusable.
  - DOD practice: the browser issue modal now collects the attestation fields, disables final issue until all four legal confirmations are checked, saves reusable signature/staff defaults locally, and shows signature/release facts in audit passport.
  - Rejected: a row-level issue click or command-like shortcut; doctors/admins need an explicit review gate.
  - Estimate: 0 us Unity runtime; localStorage write and modal form state only.
- [x] Prove schedule/settings persistence beyond source grep.
  - DOD practice: file-backed settings persistence smoke now saves and reloads doctor, assistant, chair working hours plus patient preferred appointment window through a fresh module load.
  - Rejected: claiming configuration persistence from UI preference source checks alone.
  - Estimate: 0 us Unity runtime; smoke-only state file reload.
- [x] Verify document, tax, settings, localization and build regressions.
  - DOD practice: shared/API/web typechecks, full production build, document lifecycle/chains/tax smokes, settings persistence smoke, Russian fallback, API encoding, Telegram source, UI preferences and schedule configuration passed.
  - Rejected: stopping after source tests; issue/PDF/XML guards and settings reload needed behavior-level proof.
  - Estimate: 0 us Unity runtime; Node/Vite/browser-smoke only.

## Loop 19

- [x] Close PostgreSQL metadata gap for issued-document attestation.
  - DOD practice: added the `0017` Drizzle snapshot generated from the current schema and hardened the DB runtime smoke to inspect snapshot column types for `signature_attestation` and `release_journal_entry`.
  - Rejected: relying on schema/migration string checks only; adding a release-journal table before the current document DTO needs it.
  - Estimate: 0 us Unity runtime; migration metadata only.
- [x] Persist document issue signature defaults through UI preferences.
  - DOD practice: signature mode, issuer full name and issuer role now hydrate/save through shared `/api/settings/preferences`, file-backed API persistence and browser autosave; legacy localStorage remains migration fallback only.
  - Rejected: storing recipient identity in preferences; leaving issuer defaults browser-local after adjacent document preferences were server-backed.
  - Estimate: 0 us Unity runtime; bounded preference JSON update only.
- [x] Strip unsafe inherited query parameters from Telegram portal buttons.
  - DOD practice: API webhook buttons and outbox preview URLs clear configured portal query/hash before adding only `dente_source=telegram` and `dente_section`.
  - Rejected: trusting preconfigured base URL query params; embedding patient/document/payment identifiers in Telegram links.
  - Estimate: 0 us Unity runtime; constant URL normalization only.
- [x] Verify compile, behavior, source guards and mobile screens.
  - DOD practice: full typecheck/build, DB/UI/document/source smokes, settings persistence, Telegram bot, document lifecycle/chains, tax XML/payment-scope, API text encoding, Russian fallback and mobile overflow checks passed.
  - Rejected: accepting the first parallel mobile failure; it was caused by CDP port collision, then Documents passed on a separate port.
  - Estimate: 0 us Unity runtime; Node/Vite/Edge smoke only.

## Loop 20

- [x] Fix first-run onboarding draft dismissal persistence.
  - DOD practice: draft-mode dismissal now writes the complete UI preference state before the legacy local fallback key, and the fallback key stores `draftMode` so reload cannot hide the wizard without the draft banner state.
  - Rejected: writing `saveOnboardingDismissed(true)` first, because a local preference failure could make the first-run guide disappear with no durable draft-mode recovery.
  - Estimate: 0 us Unity runtime; browser local preference write only.
- [x] Harden medical release journal source hashes.
  - DOD practice: medical copy requests/extracts stamp a deterministic `sourceSnapshotSha256`; release receipts link to the issued copy-request snapshot hash when the source request exists; the audit passport shows the hash and smokes assert the chain.
  - Rejected: leaving `sourceSnapshotSha256` null or hashing the receipt itself when a prior source request exists.
  - Estimate: 0 us Unity runtime; bounded SHA-256 over a small document-source DTO at issue time.
- [x] Prove Telegram document submenu callbacks behavior.
  - DOD practice: `smoke:telegram-bot` now directly drives `dente:medical-docs` and `dente:patient-forms`, verifies inline callback acknowledgement, visual card delivery, task creation/reuse, workflow codes, events and audit actions.
  - Rejected: relying on source grep or free-text smokes for buttons that patients will actually press.
  - Estimate: 0 us Unity runtime; Node webhook smoke over in-memory ledgers.
- [x] Add persisted clinic bot config id for scoped status checks.
  - DOD practice: `telegramBotConfigId` is now in shared UI preferences, browser autosave, API file-backed normalization and settings persistence smoke; Settings uses `/api/telegram/status/:organizationId/:botConfigId` in `clinic_owned_bot` mode when both ids are known.
  - Rejected: always polling unscoped `/api/telegram/status`, because multi-bot clinics need to see the exact runtime config they selected.
  - Estimate: 0 us Unity runtime; one bounded string preference and one URL branch in the React control plane.
- [x] Update docs and verify regressions.
  - DOD practice: README, Telegram plan and document-generation docs now describe scoped bot config status and release-journal source hashes; full typecheck/build, source/behavior smokes and mobile overflow checks passed.
  - Rejected: chat-only reporting or documentation without executable guards.
  - Estimate: 0 us Unity runtime; docs plus Node/Vite/Edge verification only.

## Loop 21

- [x] Make Telegram replies more visual and button-first.
  - DOD practice: free-text schedule/help/privacy/contact/link/appointment replies now reuse the configured clinic visual card where available, and webhook responses expose suggested photo/reply markup for site diagnostics.
  - Rejected: command-style text-only replies for common patient flows.
  - Estimate: 0 us Unity runtime; constant-size Telegram payload assembly only.
- [x] Close Russian fallback leaks in doctor-facing speech/MPR UI.
  - DOD practice: replaced visible `smart chunks`, `smart chunking`, English plane names and STT status fragments with Russian labels; `smoke:ui-preferences` now forbids the old strings.
  - Rejected: leaving technical English labels in the doctor workflow until a future i18n pass.
  - Estimate: 0 us Unity runtime; React text rendering only.
- [x] Add route-level lifecycle proof for patient forms.
  - DOD practice: new `smoke:patient-forms-lifecycle` creates, blocks incomplete, issues with signature attestation, audits, downloads and immutable-reloads intake, personal-data, minor-representative and photo/video forms.
  - Rejected: relying only on renderer/catalog tests that do not exercise the API lifecycle.
  - Estimate: 0 us Unity runtime; smoke-only Fastify route checks.
- [x] Update DENTE docs and verify.
  - DOD practice: README and document-generation docs now mention the patient-forms lifecycle smoke; full typecheck/build and targeted bot/document/settings/schedule/mobile smokes passed.
  - Rejected: chat-only report or documentation without executable proof.
  - Estimate: 0 us Unity runtime; Node/Vite/Edge verification only.

## Loop 22

- [x] Block duplicate annual tax certificate issue by patient/year/form/taxpayer.
  - DOD practice: KND/legacy certificate duplicate detection now compares annual taxpayer scope first, with fiscal receipt/payment overlap kept as old-record fallback; the smoke proves same taxpayer + later same-year receipt is blocked, while another taxpayer can still receive a separate certificate.
  - Rejected: receipt-only duplicate detection, because KND 1151156 is an annual taxpayer certificate and a later receipt must belong to the same cumulative yearly certificate.
  - Estimate: 0 us Unity runtime; bounded in-memory outbox/document scan in API issue/export paths only.
- [x] Make UI preference persistence backward-compatible.
  - DOD practice: shared `uiPreferencesSchema` now owns safe defaults for role, specialty, payment method, tax year, source/import modes, imaging URLs, Telegram filters and onboarding state; API state load normalizes old partial preference blobs, and the file persistence smoke reloads a deliberately damaged legacy preference state.
  - Rejected: browser-only fallback defaults, because `/api/settings/preferences` must not fail when the saved server state predates a preference field.
  - Estimate: 0 us Unity runtime; one zod normalization on API boot/state load.
- [x] Fix Telegram outbox direct-send lookup beyond first page.
  - DOD practice: direct send resolves an outbox item from the full generated outbox, not `buildDenteTelegramOutbox(300)`, and a new smoke proves a row outside the first 300 returns its real blocking state instead of false 404.
  - Rejected: increasing the page limit, because pagination and direct-send identity are separate responsibilities.
  - Estimate: 0 us Unity runtime; API-only generated list reuse.
- [x] Update DENTE docs and verify.
  - DOD practice: README, UX principles, Telegram plan and document-generation docs now describe annual taxpayer duplicate rules, preference migration defaults and uncapped outbox lookup; full typecheck/build and targeted tax/settings/Telegram/onboarding/schedule/Russian smokes passed.
  - Rejected: relying on subagent audit notes without executable regression coverage.
  - Estimate: 0 us Unity runtime; Node/Vite verification only.

## Loop 23

- [x] Replace generic document void with structured attestation.
  - DOD practice: shared schema, API validation, UI confirmation, audit facts and PostgreSQL migration now require/persist void reason, staff, archive/status confirmations, notification flag and optional correction document reference.
  - Rejected: status-only void, delete-and-reissue, and browser-only confirmation, because tax/legal replacements need durable operator facts.
  - Estimate: 0 us Unity runtime; bounded zod validation and one small JSON record per void.
- [x] Unlock tax-certificate replacement only after correction void.
  - DOD practice: annual taxpayer duplicate issue remains blocked until the old certificate is voided through structured tax-correction attestation; smoke proves replacement issue after void while preserving the old archived record.
  - Rejected: allowing another same-year receipt to create a second certificate without an annul/correction trail.
  - Estimate: 0 us Unity runtime; bounded document/payment checks at issue/export time.
- [x] Add bounded Telegram due reminder worker.
  - DOD practice: server starts an env-gated recursive due worker with batch/interval/dry-run/run-on-start controls, in-flight guard, Fastify shutdown, shared send-due executor and retry path for failed due receipts.
  - Rejected: browser polling, unbounded setInterval, and permanent failed-due replay.
  - Estimate: 0 us Unity runtime; Node timer only when env-enabled, default disabled.
- [x] Close visible English/raw imaging labels and document the new behavior.
  - DOD practice: web labels now use Russian fallbacks for imaging/source technical UI; README, document generation and Telegram bot docs record only implemented structured void and due-worker behavior.
  - Rejected: broad i18n rewrite or roadmap-only docs.
  - Estimate: 0 us Unity runtime; React text rendering and docs only.
- [x] Verify compile and targeted regressions.
  - DOD practice: full build plus document lifecycle/source, DB, Telegram worker/source, tax replacement, Russian fallback, UI preferences, settings, schedule, tax XML, API encoding, patient forms and bot smokes passed.
  - Rejected: source-only report after API schema/migration/UI changes.
  - Estimate: 0 us Unity runtime; Node/Vite verification only.

## Loop 24

- [x] Reject invalid medical-chain dates at issue time.
  - DOD practice: copy requests, medical extracts and release receipts now require real `YYYY-MM-DD`-prefixed request/period/issue/delivery/access dates before legal issue; invalid non-empty dates and reversed windows return 409.
  - Rejected: free-form `Date.parse`, treating invalid dates as blank, and blocking editable draft creation before the operator can correct a form.
  - Estimate: 0 us Unity runtime; constant-size API parsing during document issue/source matching only.
- [x] Prove date guards in the issue-chain smoke.
  - DOD practice: `smoke:document-issue-chains` now creates invalid-date drafts for copy request, release receipt and extract, then proves issue is blocked with date-facing Russian errors.
  - Rejected: only testing reversed periods or source-visit scope; the real failure was unparseable calendar dates.
  - Estimate: 0 us Unity runtime; Fastify smoke-only route checks.
- [x] Make real PDF export less brittle on slow clinic servers.
  - DOD practice: Chromium/Edge PDF rendering now uses bounded `DENTE_PDF_EXPORT_TIMEOUT_MS`, default 60000 ms, clamped to 10000-180000 ms; lifecycle smoke then passed with a real `%PDF`.
  - Rejected: keeping the hardcoded 30-second timeout after observed failure, or bypassing PDF proof with a placeholder.
  - Estimate: 0 us Unity runtime; one env parse and longer bounded wait only when exporting PDF.
- [x] Update docs and verify regressions.
  - DOD practice: README and document-generation docs describe strict medical-chain dates and configurable PDF wait; full build and targeted document/API smokes passed.
  - Rejected: chat-only reporting or docs without executable proof.
  - Estimate: 0 us Unity runtime; Node/Vite/Chromium verification only.

## Loop 25

- [x] Stop rendering heavy hidden work surfaces.
  - DOD practice: Shift, patient cockpit, Imaging, Schedule, Patients, Visit, Documents, Finance, Communications, compliance, and Settings now mount conditionally by `currentView` instead of building hidden top-level DOM.
  - Rejected: Terser-only minification, because it made the chunk slightly larger and did not reduce first React render work.
  - Estimate: 0 us Unity runtime; browser DOM/render work reduced on non-active views, JS bundle warning remains.
- [x] Add source guard for top-level render gating.
  - DOD practice: `smoke:web-render-gating-source` requires route-gated section snippets and forbids returning those top-level panels to `hidden={currentView...}` rendering.
  - Rejected: relying on visual smoke alone, because hidden DOM can pass screenshots while still hurting first paint.
  - Estimate: 0 us Unity runtime; source-only Node smoke.
- [x] Update UX/product docs.
  - DOD practice: README and UX principles now state that top-level workspaces mount by route and name the smoke proof.
  - Rejected: undocumented performance behavior that future edits can accidentally undo.
  - Estimate: 0 us Unity runtime; docs only.
- [x] Verify compile and mobile browser behavior.
  - DOD practice: full workspace build passed, the new source smoke passed, and mobile browser smokes for Documents, Settings Telegram, Imaging and Shift found no 390px overflow with required sections visible.
  - Rejected: stopping after TypeScript compile without loading the real app shell.
  - Estimate: 0 us Unity runtime; Vite/Edge verification only.

## Loop 26

- [x] Add route lifecycle proof for visit/workflow forms.
  - DOD practice: `smoke:visit-workflow-forms-lifecycle` now creates, blocks incomplete, issues with signature attestation, audits, downloads and immutable-reloads anesthesia log, medication order, lab order, X-ray/CBCT referral, attendance certificate, warranty memo, intervention refusal and refund/correction request.
  - Rejected: relying only on renderer/catalog/guard tests, because those do not prove real API issue/archive flow.
  - Estimate: 0 us Unity runtime; smoke-only Fastify route checks.
- [x] Update document generation docs.
  - DOD practice: README and document-generation docs name the new visit/workflow lifecycle smoke and exact covered document kinds.
  - Rejected: broad roadmap text without executable proof.
  - Estimate: 0 us runtime; docs only.
- [x] Verify document regressions.
  - DOD practice: full workspace build, new visit/workflow smoke, patient-form lifecycle smoke, document lifecycle smoke, document guards, document catalog, and API text encoding smoke passed.
  - Rejected: accepting pre-build smoke only; the new route proof was rerun after dist rebuild.
  - Estimate: 0 us Unity runtime; Node/Vite verification only.

## Loop 27

- [x] Stop appointment date/time normalization in schedule mutations.
  - DOD practice: shared appointment create/update schemas now require real ISO datetimes with explicit timezone and strict calendar/time parts before any scheduling merge or resource checks.
  - Rejected: `Date.parse` as the validator, because it silently normalizes values like `2027-02-29` and `24:00` to another day.
  - Estimate: 0 us Unity runtime; constant-size regex/calendar checks per appointment mutation.
- [x] Add API proof for invalid schedule datetime input.
  - DOD practice: `smoke:schedule-configuration` now posts a non-existent calendar date and patches a `24:00` rollover, expecting route-level 400 responses with field paths before schedule state can mutate.
  - Rejected: shared-only unit proof, because the real risk is an API route accepting malformed clinic visits.
  - Estimate: 0 us Unity runtime; smoke-only Fastify route checks.
- [x] Update product/UX docs and verify regressions.
  - DOD practice: README and UX principles document the strict server boundary; shared typecheck/build, API build, full workspace build, schedule/admin/autosave/persistence/API-encoding/document catalog smokes passed.
  - Rejected: undocumented validation behavior and chat-only reporting.
  - Estimate: 0 us Unity runtime; Node/Vite verification only.

## Loop 28

- [x] Repair legacy mojibake before legal document facts are stored.
  - DOD practice: document create payloads, issue signature attestation, and void attestation pass through API repair before draft/issue/void persistence and archive snapshot creation.
  - Rejected: render-only repair, because audit passports and public issue responses can still expose stored broken recipient/staff facts.
  - Estimate: 0 us Unity runtime; bounded recursive string repair only on document mutation routes.
- [x] Prove repaired facts in issue response, audit passport and archived HTML.
  - DOD practice: `smoke:document-issue-chains` injects mojibake release/signature facts and asserts readable Russian in issue response, audit-facts and immutable archived HTML, with no mojibake markers.
  - Rejected: checking only rendered document HTML or dashboard encoding.
  - Estimate: 0 us Unity runtime; smoke-only Fastify route checks.
- [x] Update implemented documentation and verify adjacent regressions.
  - DOD practice: README and document-generation docs now state the storage-boundary repair; API/shared typecheck, API/full build, document lifecycle/catalog/payload/guard/forms smokes, Telegram bot, schedule and Russian fallback passed.
  - Rejected: source-only report after API mutation boundary changes.
  - Estimate: 0 us Unity runtime; Node/Vite verification only.

## Loop 29

- [x] Harden Telegram malformed-payload Russian error proof.
  - DOD practice: `smoke:telegram-validation` now fails on mojibake markers in every controlled 400 response and still verifies no admin/webhook secret leakage.
  - Rejected: checking only that a readable Russian substring exists, because a mixed readable/broken response could still pass and reach a doctor or operator.
  - Estimate: 0 us Unity runtime; smoke-only Fastify response scan.
- [x] Guard Telegram callback fallback text at source.
  - DOD practice: the validation smoke reads `apps/api/src/routes/telegram.ts` and requires the generic `answerCallbackQuery` fallback to remain readable Russian.
  - Rejected: relying on only the large bot smoke, because it can miss a generic fallback text path when appointment callbacks are handled.
  - Estimate: 0 us Unity runtime; source-only string check.
- [x] Update implemented Telegram docs and verify regressions.
  - DOD practice: README and Telegram bot plan now name the validation proof; API typecheck/build, Telegram validation, bot, API encoding, Russian fallback and full workspace build passed.
  - Rejected: log-only change or documentation without executable proof.
  - Estimate: 0 us Unity runtime; Node/Vite verification only.

## Loop 30

- [x] Stop rendering inactive structured document payload editors.
  - DOD practice: all 27 `document-payload-card` editors in `apps/web/src/App.tsx` now mount only when `selectedDocumentKind` matches the card kind.
  - Rejected: leaving `hidden={selectedDocumentKind !== ...}` cards in the DOM, because hidden legal/tax/patient editors still create browser work and increase the chance of stale inactive controls.
  - Estimate: 0 us Unity runtime; browser DOM/render work reduced inside Documents, exact microseconds not profiled.
- [x] Add source guard for the document payload mount rule.
  - DOD practice: `smoke:document-payload-ui-source` now fails if structured payload cards return to hidden DOM or if any payload card is not conditionally mounted.
  - Rejected: relying on the mobile screenshot smoke only, because hidden inactive payload forms can pass screenshots while still hurting the Documents route.
  - Estimate: 0 us Unity runtime; source-only Node smoke.
- [x] Update factual product/docs text.
  - DOD practice: README, UX principles, and document-generation docs now state that inactive structured document editors are not kept as hidden DOM and name the smoke proof.
  - Rejected: chat-only reporting, because future edits need an on-disk contract.
  - Estimate: 0 us Unity runtime; docs only.
- [x] Verify web compile, build, source/API smokes, and mobile Documents route.
  - DOD practice: web typecheck, web build, full workspace build, document payload UI source smoke, top-level render gating smoke, document catalog smoke, default mobile smoke, and `#documents` mobile smoke passed.
  - Rejected: stopping after TypeScript compile without loading the actual Documents route on a 390 px viewport.
  - Estimate: 0 us Unity runtime; Vite/Edge verification only. Residual Vite chunk warning remains.

## Loop 31

- [x] Repair Settings tab gating after an over-broad mechanical rewrite.
  - DOD practice: restored lost TSX conditional closings for Imaging and Communications before accepting the tab-gating work; `npm run typecheck -w @dental/web` passed after repair.
  - Rejected: leaving the app with broken JSX or claiming a source-only improvement after a failed compile.
  - Estimate: 0 us Unity runtime; browser DOM work only, no profiler microsecond claim.
- [x] Stop rendering inactive Settings sub-tab tools.
  - DOD practice: clinic, access, Telegram, protocols, rules, prices, sources, DICOM tool-state, AI, imports, shared imaging imports, audit, and legacy import sections now mount only for the active Settings tab or explicit sources/imports shared route.
  - Rejected: keeping `hidden={settingsTab !== ...}` sections, because inactive admin tools still build heavy DOM on weak clinic PCs.
  - Estimate: 0 us Unity runtime; inactive Settings DOM/render work reduced, exact browser timing not measured.
- [x] Add a regression guard for Settings tab mount behavior.
  - DOD practice: `smoke:web-render-gating-source` now requires 15 Settings tab gates and fails if tab sections return to hidden DOM.
  - Rejected: relying on manual review after the previous overmatch proved this area is fragile.
  - Estimate: 0 us runtime; source-only Node smoke.
- [x] Verify compile, build, source guards, and mobile Settings routes.
  - DOD practice: web typecheck, web build, full workspace build, render-gating source smoke, document payload source smoke, UI preferences smoke, settings file-persistence smoke, settings API-preferences smoke, Russian fallback smoke, and mobile `#settings/telegram`, `#settings/imports`, `#settings/sources` passed.
  - Rejected: accepting only typecheck, because Settings tabs need real mobile route proof after mount-rule changes.
  - Estimate: 0 us Unity runtime; Vite/Edge verification only. Residual Vite chunk warning remains at 656.51 kB.

## Loop 32

- [x] Scope Telegram outbox runtime for clinic-owned bots.
  - DOD practice: `/api/telegram/outbox`, `/api/telegram/outbox/:itemId/send`, and `/api/telegram/outbox/send-due` now resolve the same `organizationId` + `botConfigId` runtime as scoped status/webhook routes; generated outbox items use scoped portal/review/maps/welcome image settings and scoped transport readiness.
  - Rejected: keeping outbox/send/send-due on singleton active Telegram settings after status/webhook became multi-clinic; that can route messages through the wrong bot.
  - Estimate: 0 us Unity runtime; one request-time env runtime lookup and in-memory queue filter in Node.
- [x] Propagate saved clinic bot scope from Settings UI.
  - DOD practice: Settings appends saved `organizationId` and `telegramBotConfigId` to outbox list, single send and send-due actions only in `clinic_owned_bot` mode.
  - Rejected: requiring doctors/admins to reselect or paste config ids per send action.
  - Estimate: 0 us Unity runtime; URLSearchParams construction only.
- [x] Add scoped outbox regression proof.
  - DOD practice: `smoke:telegram-bot` now verifies scoped outbox uses the selected bot token, portal host and visual card for list, due dry-run and real send; `smoke:telegram-control-ui-source` now guards scoped query propagation.
  - Rejected: source-only confidence after changing transport routing.
  - Estimate: 0 us Unity runtime; synthetic Fastify/Telegram transport only.
- [x] Verify build, source, bot, persistence, docs and mobile behavior.
  - DOD practice: API/web typecheck, full workspace build, Telegram bot/source/validation smokes, settings persistence/preference smokes, API text encoding, document catalog, Russian fallback and mobile `#settings/telegram` overflow smoke passed.
  - Rejected: accepting the first mobile smoke failure that used the default DICOM selector instead of the Telegram Settings selector.
  - Estimate: 0 us Unity runtime; Vite/Node/Edge verification only. Residual Vite chunk warning remains at 656.82 kB.

## Loop 33

- [x] Freeze KND 1151156 XML source facts at issue time.
  - DOD practice: issued tax certificates now carry a server-side `taxXmlSourceSnapshot` with patient, clinic profile and selected payment rows; XML export reads frozen facts instead of mutable live records.
  - Rejected: rebuilding XML from current patient/clinic/payment state after issue, because it can change an already-issued tax-support artifact.
  - Estimate: 0 us Unity runtime; bounded JSON snapshot per issued tax certificate.
- [x] Persist first successful FNS XML export as immutable bytes.
  - DOD practice: `/api/documents/:id/tax-xml` stores XML bytes, SHA-256, source-snapshot hash, tax-office code and creation time, then returns the stored snapshot on every later download.
  - Rejected: re-running XML generation on every download and reading a later `DENTE_FNS_TAX_OFFICE_CODE`.
  - Estimate: 0 us Unity runtime; one SHA-256/hash record per successful XML export.
- [x] Expose XML snapshot evidence in operator audit UI.
  - DOD practice: audit facts and Documents passport show source-facts hash, archived XML hash and timestamp; public generated-document DTOs still omit raw source/XML snapshots.
  - Rejected: exposing full patient/payment source snapshots to the browser.
  - Estimate: 0 us Unity runtime; small audit DTO fields only.
- [x] Update docs and verify regressions.
  - DOD practice: README and document-generation docs now describe immutable XML source/export snapshots; shared/api/web typecheck, full build, DB contract, KND XML, document lifecycle/chains/catalog/source, Telegram, settings, Russian encoding and mobile Documents smokes passed.
  - Rejected: source-only report after schema, route, migration and UI contract changes.
  - Estimate: 0 us Unity runtime; Node/Vite/Edge verification only. Residual Vite chunk warning remains at 657.96 kB.

## Loop 34

- [x] Replace first-run source links with persisted inline configuration.
  - DOD practice: the onboarding `sources` step now edits price-list source, patient import source, smart import mode, document ingestion route, imaging source, DICOMweb URL, and OHIF URL directly through existing UI preference setters.
  - Rejected: keeping the first-run source step as four navigation buttons into Settings, because that does not actually configure a new clinic during setup.
  - Estimate: 0 us Unity runtime; browser work is limited to the visible onboarding step.
- [x] Keep source configuration mobile-safe.
  - DOD practice: added compact responsive source sections and URL fields; 390 px browser smoke opened the import step and saw `.onboarding-source-config` with horizontal overflow 0.
  - Rejected: adding another wide Settings-style grid to onboarding.
  - Estimate: 0 us Unity runtime; CSS-only responsive layout.
- [x] Add regression proof for source onboarding.
  - DOD practice: `smoke:onboarding-configuration-source` now fails if source onboarding loses inline persisted controls, autosave wording, Russian labels, DICOMweb, or OHIF setters.
  - Rejected: relying on manual visual review after this area previously contained only route links.
  - Estimate: 0 us Unity runtime; source-only Node smoke.
- [x] Update product docs and verify compile/persistence contracts.
  - DOD practice: README and UX principles now record the inline persisted source setup; web typecheck, web build, full build, UI preferences, settings file/API persistence, Russian fallback, onboarding source smoke, and mobile onboarding smoke passed.
  - Rejected: documentation-only claim without source and browser proof.
  - Estimate: 0 us Unity runtime; Node/Vite/Edge verification only. Residual Vite chunk warning remains at 661.88 kB.

## Loop 35

- [x] Scope Telegram link codes and chat links to selected bot config.
  - DOD practice: link-code creation/listing/consumption, chat-link listing/revocation, webhook request callbacks, and outbox active-chat lookup now carry `organizationId`, `clinicId`, and `botConfigId` instead of sharing one clinic-wide Telegram binding.
  - Rejected: letting a code issued for one clinic-owned bot bind through another bot, because multi-bot clinics would route reminders and support replies through the wrong identity.
  - Estimate: 0 us Unity runtime; API cost is bounded in-memory ledger filtering by bot config.
- [x] Propagate scoped Telegram ledger settings from the site.
  - DOD practice: Settings appends saved clinic bot runtime scope to link-code/chat-link lists and revocation, and link-code creation sends selected `botConfigId` while retaining Russian QR/share workflows.
  - Rejected: only scoping outbox while keeping QR/linking global, because that creates a ready outbox for the wrong bot.
  - Estimate: 0 us Unity runtime; browser cost is URLSearchParams construction and one create payload field.
- [x] Add multi-bot regression proof.
  - DOD practice: `smoke:telegram-bot` now creates a primary scoped code, rejects consuming it through a secondary bot config, proves no secondary chat link appears, then links through the primary bot and verifies scoped outbox readiness.
  - Rejected: relying on the old scoped outbox smoke, because it reused a shared-bot chat link and did not prove code isolation.
  - Estimate: 0 us Unity runtime; synthetic Fastify/Telegram transport only.
- [x] Verify compile, build, docs, source guards, bot behavior and mobile Settings.
  - DOD practice: shared/api/web typecheck, full build, Telegram bot/source/validation, settings persistence/preferences, UI preferences, Russian fallback, API encoding, and mobile `#settings/telegram` passed.
  - Rejected: accepting stale `apps/api/dist` or source-only proof after route/schema changes.
  - Estimate: 0 us Unity runtime; Node/Vite/Edge verification only. Residual Vite chunk warning remains at 662.01 kB.

## Loop 36

- [x] Remove remaining default-organization leakage from Telegram patient workflows.
  - DOD practice: link-code subject validation now checks the resolved runtime organization, clinic mismatch is rejected before issue, and tax/document/care/contact callbacks create tasks, communication events and audit records in the organization from the active chat binding.
  - Rejected: testing only same-organization primary/secondary bot configs, because a second clinic could still fail code issue or write handoff work into the default clinic.
  - Estimate: 0 us Unity runtime; API work is bounded array lookup by organization and bot config in prototype state.
- [x] Add second-clinic callback regression proof.
  - DOD practice: `smoke:telegram-bot` now creates a patient in a second organization, links through that clinic's bot config, then verifies `dente:tax`, `dente:care-implant`, and `dente:contact` tasks/events/audit stay in the second organization and use the second clinic bot token.
  - Rejected: source-only guard after the first smoke failure exposed link-code creation still validating against the singleton clinic.
  - Estimate: 0 us Unity runtime; synthetic Fastify/Telegram transport only.
- [x] Update docs and verify compile/smoke coverage.
  - DOD practice: README and Telegram plan now document scoped subject/clinic validation plus linked-chat organization ownership for document/care/contact handoffs; API typecheck, API build, full build, Telegram bot/source/validation, API encoding, Russian fallback, and settings persistence/preference smokes passed.
  - Rejected: claiming multi-tenant readiness while docs only mentioned link/outbox scope and not callback handoff ownership.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 662.01 kB.

## Loop 37

- [x] Scope signed appointment callback data to the selected Telegram bot runtime.
  - DOD practice: appointment callback HMAC now uses `organizationId + clinicId + botConfigId + appointmentId + action + expiry`; webhook verification receives `clinicId` from the resolved runtime, and schedule/outbox markup signs buttons with the same scope.
  - Rejected: relying only on active chat-link scope, because a linked same-patient/same-chat record on another bot config could replay a compact callback signed only by global organization.
  - Estimate: 0 us Unity runtime; Node HMAC input adds two short scope fields per generated/verified appointment button.
- [x] Add cross-bot replay regression proof.
  - DOD practice: `smoke:telegram-bot` now links the same patient/chat through primary and secondary clinic-owned bots, sends a primary appointment confirmation button, replays it through the secondary webhook, and asserts rejection plus unchanged appointment/task/event/audit state.
  - Rejected: source-only proof and token-route checks without a real callback replay attempt.
  - Estimate: 0 us Unity runtime; synthetic Fastify/Telegram transport only.
- [x] Guard source and docs for scoped callback behavior.
  - DOD practice: `smoke:telegram-control-ui-source` now requires scoped callback normalization/HMAC/parse route markers; README and Telegram plan document scoped callback signatures and the cross-bot replay smoke.
  - Rejected: leaving the fix invisible to future agents and operators.
  - Estimate: 0 us runtime; source/doc checks only.
- [x] Verify compile and adjacent Telegram/persistence regressions.
  - DOD practice: API typecheck/build, full workspace build, Telegram bot/source/validation smokes, Russian fallback/API encoding and settings persistence/preference smokes passed.
  - Rejected: stopping after the first failed replay smoke; the failure was ordering-related and was fixed before verification.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 662.01 kB.

## Loop 38

- [x] Implement official outpatient medical card 025/u.
  - DOD practice: dedicated structured payload, renderer, source URL, DB enum migration, missing-payload guard, signed-source issue blocker, clinical-row blocker, and 274n source note.
  - Rejected: labeling the generic extract as official 025/u output.
  - Estimate: 0 us Unity runtime; web/API document generation only.
- [x] Expose 025/u in the doctor-facing document UI and Telegram document workflow.
  - DOD practice: Russian fallback labels, conditional payload editor mount, signed-source/274n/third-party confirmations, Communications quick action, and medical-document request workflow inclusion.
  - Rejected: command-only or English fallback entry points.
  - Estimate: 0 us Unity runtime; bounded React form work.
- [x] Verify shared/API/web contracts and document smoke coverage.
  - DOD practice: shared/API/web typecheck, full build, catalog, payload, guard, issue-chain, UI-source, legal-confirmation, Telegram, encoding, DB-contract, Russian fallback and mobile Documents smokes passed.
  - Rejected: source-only report for official-form output.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 676.25 kB.

## Loop 39

- [x] Add scoped local draft recovery for 025/u.
  - DOD practice: patient + visit + document-kind draft key stores only the long 025/u editor fields and restores them after reload while the selected document kind remains active.
  - Rejected: putting clinical 025/u text into global UI preferences, because preferences must not become a medical data cache.
  - Estimate: 0 us Unity runtime; bounded browser localStorage JSON under 60 draft entries.
- [x] Keep legal confirmations non-persistent.
  - DOD practice: local draft hydrate resets signed-source, 274n and third-party confirmations, so recovered text cannot silently re-attest legal/source facts.
  - Rejected: auto-saving checkboxes together with medical text.
  - Estimate: 0 us Unity runtime; three boolean resets on hydrate.
- [x] Correct official-document claims.
  - DOD practice: KND XML documentation now says the output is shaped to published fields and still needs external XSD/EDO validation, instead of claiming complete published-XSD validation.
  - Rejected: leaving an overclaim after source audit found no real XSD validator in the code path.
  - Estimate: 0 us Unity runtime; docs-only correction.
- [x] Verify compile, build, source, persistence and mobile behavior.
  - DOD practice: web typecheck, full workspace build, document catalog/payload/source smokes, UI-preference clinical-key guard, encoding/Russian fallback smokes, 390 px Documents mobile smoke and a reload browser smoke for 025/u draft recovery passed.
  - Rejected: source-only proof for a data-preservation UX change.
  - Estimate: 0 us Unity runtime; Node/Vite/Edge verification only. Residual Vite chunk warning remains at 683.60 kB.

## Loop 40

- [x] Add internal structural preflight for KND XML before archiving.
  - DOD practice: `buildKnd1151156Xml` now checks XML declaration, root/document/expense tag balance, KND 1184043, version 5.01, tax office/year, `НомерСвед`, correction number, patient flag, payer/patient nodes, service-code sums, technical placeholders and mojibake before returning bytes for snapshot storage.
  - Rejected: claiming official XSD/ЭДО validation or adding an ad hoc dependency without the real FNS XSD validator contour.
  - Estimate: 0 us Unity runtime; bounded string checks over one generated XML document on manual tax export.
- [x] Expand KND XML regression proof.
  - DOD practice: `smoke:tax-knd-xml` now guards preflight source markers and asserts real self/non-self XML flags, patient node presence/absence, correction number, no `undefined`/`NaN`/`null`/object placeholders, frozen facts and immutable first export.
  - Rejected: source-only validator proof without exercising the route after API build.
  - Estimate: 0 us Unity runtime; Fastify smoke only.
- [x] Update factual documentation without overstating legality.
  - DOD practice: README and document-generation docs now say DENTE runs an internal structural preflight but still requires external XSD/ЭДО/КЭП validation for official submission.
  - Rejected: calling the draft a signed ФНС package or complete operator validation.
  - Estimate: 0 us runtime; docs only.
- [x] Verify compile, build and adjacent document/text smokes.
  - DOD practice: API typecheck/build, full workspace build, KND XML smoke after rebuild, API encoding, document catalog, document payload and Russian fallback smokes passed.
  - Rejected: stopping after the first pre-build smoke because dist can go stale.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 683.60 kB.

## Loop 41

- [x] Pin official FNS KND 1151156 source attachments.
  - DOD practice: official FNS order page was rechecked; appendices 1-4 and `UT_SVOPLMEDUSL_1_278_00_05_01_02.xsd` were downloaded once and recorded in `docs/legal-sources/fns-knd-1151156.json` with URL, byte size and SHA-256.
  - Rejected: sourceUrls-only documentation, because it cannot detect attachment drift; also rejected claiming this is official XSD validation.
  - Estimate: 0 us Unity runtime; cold documentation/source manifest only.
- [x] Expose the FNS XSD source in document metadata.
  - DOD practice: `tax_deduction_certificate.sourceUrls` now includes the official XSD 5.01 URL alongside the order and PDF form, so the issue passport can show the schema source to operators.
  - Rejected: hiding XSD only in prose docs, because the app passport is the operator-facing source route.
  - Estimate: 0 us Unity runtime; one extra metadata URL in shared dist.
- [x] Add source-regression smoke.
  - DOD practice: `smoke:official-document-sources` checks the manifest, appendices 1-4, XSD URL/filename/bytes/SHA-256, package script wiring, shared metadata and docs.
  - Rejected: live network smoke in the default suite, because CI/offline clinic builds should not fail on public-site latency.
  - Estimate: 0 us runtime; source/dist smoke only.
- [x] Verify build and adjacent document/tax regressions.
  - DOD practice: shared typecheck, shared build, full workspace build, official-source smoke, documents catalog, tax XML, API encoding, document payload and Russian fallback smokes passed.
  - Rejected: accepting stale shared dist after metadata change.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 683.60 kB.

## Loop 42

- [x] Make KND XML official-validation status explicit.
  - DOD practice: `DocumentAuditFacts` now carries `taxXmlOfficialValidationStatus` and `taxXmlOfficialValidationNote`; API returns `external_validation_required` when KND XML source facts or archived draft XML exist.
  - Rejected: relying on prose warnings only, because operator dashboards and UI can hide prose while still showing an action as ready.
  - Estimate: 0 us Unity runtime; one small JSON field pair on cold document passport request.
- [x] Fix doctor-facing KND XML wording.
  - DOD practice: Documents passport now says `черновик XML КНД`, `черновик XML заархивирован`, and `нужна XSD/КЭП/ЭДО проверка`; the note states XSD validation, KEP signing and EDO/TKS submission are outside DENTE.
  - Rejected: leaving `XML КНД доступен` wording that can be mistaken for an official package.
  - Estimate: 0 us Unity runtime; React string/render branch only when passport is open.
- [x] Add regression proof against overclaiming official XML readiness.
  - DOD practice: `smoke:tax-knd-xml` now source-checks the Russian draft wording and runtime-checks audit facts before and after first XML export.
  - Rejected: source-only guards without hitting `/api/documents/:id/audit-facts`.
  - Estimate: 0 us Unity runtime; Fastify smoke only.
- [x] Verify compile, build and adjacent document regressions.
  - DOD practice: shared/API/web typecheck, full workspace build, tax XML, payload UI source, API encoding, document catalog, official sources, document payloads, Russian fallback, document issue-chain and 390 px Documents mobile smokes passed.
  - Rejected: stopping after typecheck, because API dist and source smokes can diverge.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 683.76 kB.

## Loop 43

- [x] Add a button-first main-menu escape route to Telegram subflows.
  - DOD practice: `Главное меню` now maps to allowlisted `dente:start` and is present in document, care, contact, linked/rejected, clinic, privacy and appointment-callback menus.
  - Rejected: adding a new slash command or embedding patient/appointment state in callback data.
  - Estimate: 0 us Unity runtime; one extra Telegram inline row in cold message payloads only.
- [x] Prove the Telegram UX with runtime callbacks, not source claims.
  - DOD practice: `smoke:telegram-bot` now asserts `Главное меню` for review/map, document, tax, care, tenant-scoped callback and signed appointment callback paths.
  - Rejected: checking only that the source string exists.
  - Estimate: 0 us Unity runtime; Fastify webhook smoke only.
- [x] Repair the due-worker source guard to match scoped runtime routing.
  - DOD practice: the guard now expects `executeDenteTelegramOutboxDueBatch(input, runtimeResult.runtime)`, proving manual send-due reuses the worker batch service under the resolved bot runtime scope.
  - Rejected: weakening the smoke to a generic substring that would miss singleton-bot regressions.
  - Estimate: 0 us Unity runtime; source smoke only.
- [x] Update Telegram documentation and verify the loop.
  - DOD practice: Telegram plan documents `dente:start` and `Главное меню`; API typecheck, full build, Telegram runtime/source smokes, encoding and Russian fallback smokes passed.
  - Rejected: leaving docs with an incomplete callback allowlist.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 683.76 kB.

## Loop 44

- [x] Harden doctor-facing language selection to Russian fallback.
  - DOD practice: one typed `uiLanguageOptions` owner, one `normalizeUiLanguageInput` fallback route, no raw DOM `as UiLanguage` casts in language selectors.
  - Rejected: trusting select DOM values or adding hidden English choices before a real localization dictionary exists.
  - Estimate: 0 us Unity runtime; two cold React select handlers only.
- [x] Make language autosave visible in first-run and settings.
  - DOD practice: onboarding and clinic profile settings both show the same Russian hint that the language choice is autosaved until changed.
  - Rejected: relying on implicit `uiPreferences` behavior with no doctor-facing explanation.
  - Estimate: 0 us Unity runtime; one short static text node in two cold settings panels.
- [x] Add regression proof for language fallback.
  - DOD practice: `smoke:ui-preferences` now rejects raw DOM language casts, English language options and missing autosave explanation.
  - Rejected: source-only Russian label check without guarding unsafe future edits.
  - Estimate: 0 us runtime; source smoke only.
- [x] Verify compile, persistence and adjacent onboarding/schedule behavior.
  - DOD practice: initial web typecheck caught nullable fallback; fixed by `defaultUiLanguageOption`, then web typecheck, full build, UI preferences, settings persistence, onboarding, schedule and Russian fallback smokes passed.
  - Rejected: stopping after source smokes or ignoring the compiler warning.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 684.20 kB.

## Loop 45

- [x] Persist per-scenario Telegram visual-card configuration.
  - DOD practice: one shared schema owner, file-state normalization, PostgreSQL jsonb column and migration `0021_telegram_visual_cards.sql`; old saved settings normalize to null scenario URLs instead of failing.
  - Rejected: one global image for every patient message, because documents, tax, billing, care and review flows need different clinic-facing cards.
  - Estimate: 0 us Unity runtime; cold settings JSON and DB metadata only.
- [x] Route Telegram previews, outbox and replies to the matching visual card.
  - DOD practice: template kind resolves to `mainMenu`, `appointment`, `documents`, `tax`, `billing`, `care`, `review` or `staff`; delivery falls back from scenario card to welcome image to text; outbox keyboards keep `Главное меню`.
  - Rejected: putting media decisions in browser preview code or in Telegram callback payloads.
  - Estimate: 0 us Unity runtime; one HTTPS string lookup on cold Telegram message construction.
- [x] Expose scenario cards in first-run onboarding, Settings and env docs.
  - DOD practice: the doctor/admin can set the same cards during clinic setup or later in the Telegram Settings tab; `DENTE_TELEGRAM_CLINIC_BOTS_JSON` documents `visualCardUrls`.
  - Rejected: hidden env-only configuration, because clinics need visible setup without redeploying for every card change.
  - Estimate: 0 us Unity runtime; cold React settings/onboarding form fields only.
- [x] Verify compile, build, mobile layout and source guards.
  - DOD practice: shared/API/web typechecks, full build, Telegram runtime/source smokes, DB runtime contract, onboarding/configuration, preferences, Russian fallback, API encoding, mobile Telegram settings screen and adjacent Telegram worker/handoff checks passed.
  - Rejected: trusting source grep without runtime Telegram preview assertions.
  - Estimate: 0 us Unity runtime; Node/Vite verification only. Residual Vite chunk warning remains at 686.97 kB.

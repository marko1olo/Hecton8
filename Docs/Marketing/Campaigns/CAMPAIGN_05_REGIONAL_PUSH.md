# Campaign 05 - Regional Push

Status: future / after English and RU assets are stable
Public stance: single-player-first scope / proof-first campaign copy
Runtime impact: none

## Objective

Use regional creators and press without machine-translated spam. Regional push matters because a few thousand dollars cannot buy global reach, but localized creator trust can outperform broad ads.

## Priority Regions

| Priority | Region | Reason | Required asset |
|---:|---|---|---|
| 1 | RU/CIS | User-native market knowledge, strong horror/survival video culture | Russian one-pager, screenshots, no payment/platform confusion |
| 2 | German | Survival/sim/long-form audience | Base/machinery proof, German short pitch |
| 3 | Polish | Survival/horror/indie overlap | Demo or clear screenshots |
| 4 | Portuguese/Brazil | Strong creator reach, horror streams | PT-BR pitch and captions |
| 5 | Spanish | Broad survival/horror audience | Short clips and simple pitch |
| 6 | French | Indie press + variety creators | Polished visual pack |
| 7 | Japanese/Korean | High localization expectations | Localized Steam/trailer only after proof |

## Regional Outreach Rule

Do not translate the whole pitch mechanically. Use short, reviewed localized copy:

- one-sentence game promise;
- what exists now;
- what does not exist;
- one asset;
- one ask.
## Regional Send Gate V0

Status: active / blocks regional send and regional lead expansion.

Regional outreach is not a shortcut around the English proof gates. A region can move only when all items below are true:

- `localization_public_permission_gate = ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` has passed for the exact language/surface.
- English first screenshot or clip packet has passed current asset QA.
- Localized short pitch has native/fluent review and no mojibake.
- Gameplay, pressure, route-risk, threat, salvage, base-failure, or first-public agency proof is backed by AB-009/KPI field source: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.
- Public Steam, wishlist, demo, signup, presskit, or trailer CTA has passed Official CTA Link Activation Gate V0. If not, use no-link feedback ask or private access route.
- Private key/demo/playtest/preview route has access-log fields: `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and `agency_decision_field_source` where proof claims are used.
- Batch size is a ceiling, not an instruction; do not verify new regional leads unless the current CRM has a source-backed asset/route gap.

## RU/CIS Pitch

Subject:

HECTON-8 - mrachnoe podvodnoe vyzhivanie v NASA-punk stile

Body:

HECTON-8 - odinochnoe podvodnoe vyzhivanie pro davlenie, tekhniku, poisk resursov i chernuyu vodu. Eto ne obeshchanie kooperativa i ne popytka prodavat igru cherez sravnenie s konkurentami.

Kryuchok dlya zritelei: baza kak mashina, glubina kak ugroza, Seed Ship kak anomalya, kotoraya portit pribory, marshruty i oshchushchenie bezopasnosti.

Materialy: [odobrennyi Steam/skrinshoty/klip/demo tolko posle public CTA activation dlya public-linkov ili recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` s `verified_contact_route`, `access_route_class`, `reply_consent_provenance` dlya private-route]

Esli format podoidet, mozhno obsudit demo ili press-kit, kogda build, dostup, `verified_contact_route`, `access_route_class` i `reply_consent_provenance` budut gotovy.

## German Pitch

Subject:

HECTON-8 - industrielles Unterwasser-Survival mit Druck und Maschinen

Body:

HECTON-8 ist ein single-player-first Unterwasser-Survival-Spiel ueber Druck, Maschinen, Bergung und schwarze Tiefsee. Der Fokus bleibt single-player-first und proof-first; kein Wettbewerber-Angriff.

Der passende Winkel: eine Basis als Maschine, Tiefe als Risiko, und Survival-Systeme, die der Spieler lesen kann.

## Spanish Pitch

Subject:

HECTON-8 - supervivencia submarina industrial en aguas oscuras

Body:

HECTON-8 es un juego single-player-first de supervivencia submarina sobre presion, maquinaria, rescate y exploracion en aguas oscuras. El enfoque se mantiene single-player-first y basado en pruebas; sin ataque a competidores.

## PT-BR Pitch

Subject:

HECTON-8 - sobrevivencia submarina industrial em aguas escuras

Body:

HECTON-8 e um jogo single-player-first de sobrevivencia submarina sobre pressao, maquinas, salvamento e exploracao em aguas escuras. O foco continua single-player-first e baseado em prova; sem ataque a concorrentes.

## 2026-05-19 Regional First-Wave Package V0

Status: blocked / localization review required / do not send.

Use regional outreach only after the English first screenshot pack passes and the region has a reviewed short pitch. Bad localization is worse than silence.

### First Wave Order

| Wave | Region | Max sends | Required proof | Primary targets | Stop rule |
|---|---|---:|---|---|---|
| R1 | RU/CIS | 5 | RU one-pager, `PLAN-SHOT-001`, `PLAN-SHOT-003`, AB-009/KPI field source for pressure/route-risk claims, Official CTA Link Activation Gate V0 if any public link is mentioned, or exact private access-log fields plus `reply_consent_provenance`. | Mid-size survival/horror creators before huge channels. | Stop if replies focus on translation, payment/access confusion, missing send/access fields, or competitor-attack framing. |
| R2 | German | 5 | German short pitch, base/machinery proof, AB-009/KPI field source for player-decision claims, CTA activation or no-link fallback. | Long-form survival/sim creators. | Stop if base/machinery proof is too weak, copy sounds machine-translated, or send/access fields plus `reply_consent_provenance` are missing. |
| R3 | PT-BR | 3 | PT-BR short pitch, one strong clip, AB-009/KPI field source for agency claims, CTA activation or no-link fallback. | Horror/survival creators with current activity. | Stop if no Portuguese reviewer, no clip exists, or send/access fields plus `reply_consent_provenance` are missing. |
| R4 | Spanish | 3 | ES short pitch, one strong clip, AB-009/KPI field source for agency claims, CTA activation or no-link fallback. | Horror/survival or critique channels. | Stop if broad variety target would need hype framing or proof custody is missing. |
| R5 | Polish/French | 2 each | Reviewed short pitch, demo or strong screenshot pack, AB-009/KPI field source where proof claims are used, CTA activation or exact private access-log fields. | Indie/horror/systems fit only. | Stop if localization is unreviewed or send/access fields plus `reply_consent_provenance` are missing. |

### One-Pager Fields

```text
Title:
One-line pitch:
What exists now:
What does not exist:
Asset links:
Steam/demo status:
CTA activation packet or no-link route:
AB-009/KPI agency field source:
send_route_class for sends, or exact private access-log fields if private:
reply_consent_provenance:
Contact:
Disclosure/private access policy:
```

### Regional Copy Kill Rules

Kill localized copy if:

- it contains mojibake;
- it uses a machine-translated idiom that sounds unnatural;
- it adds unsupported multiplayer scope, release date, performance, or platform promises;
- it explains HECTON-8 only through a competitor comparison;
- it mentions Steam/demo before Official CTA Link Activation Gate V0 for public links or recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED` plus exact access-log fields for private routes;
- it uses gameplay, pressure, route-risk, threat, salvage, base-failure, or first-public agency proof without `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`;
- it lacks `send_route_class` for sends or exact private access-log fields for access asks, plus `reply_consent_provenance` custody;
- a native/fluent reviewer has not approved it for public use.
- `localization_public_permission_gate` is not `ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` for the exact surface.

## Regional Batch Size

Start tiny. These are ceilings after Regional Send Gate V0, not verification quotas:

- 10 RU/CIS;
- 5 German;
- 5 Polish;
- 5 PT-BR;
- 5 Spanish/French combined;
- JP/KR hold until localized asset quality is real.

## Metrics

- reply rate by region;
- wishlist traffic by country after Official CTA Link Activation Gate V0 only;
- agency-decision read by region: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`;
- send/access field and `reply_consent_provenance` failures by region;
- comment language;
- repeated confusion;
- localization complaints;
- creator fit.

## Kill Criteria

Pause a region if:

- pitch sounds machine-translated;
- creators ask for features we are not building;
- no localized asset exists;
- Steam page cannot support the language expectation;
- regional platform/payment issues make the CTA useless;
- AB-009/KPI field source is missing for gameplay, pressure, route-risk, threat, salvage, base-failure, or first-public agency proof;
- `send_route_class`, exact private access-log fields, or `reply_consent_provenance` custody is missing.

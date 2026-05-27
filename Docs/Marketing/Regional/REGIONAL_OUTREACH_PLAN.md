# Regional Outreach Plan

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Status: planning / no outreach without verified leads

## Rule

Do not machine-translate mass spam. For high-value leads, use a short localized pitch and keep the game promise simple.

R19 localization boundary: all localized pitch drafts below are `LOCALIZATION_REVIEW_PENDING / DO_NOT_SEND` until a native speaker or professional reviewer approves them. Do not send raw machine-translated or mojibake text to creators. Public regional use additionally requires `localization_public_permission_gate = ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` for the exact language/surface.

## Regional Send Gate V0

Regional outreach cannot bypass English proof gates. Before any regional email, DM, press note, key/demo access ask, or public community post:

- `localization_public_permission_gate = ALLOW_LOCALIZED_PUBLIC_USE_VERIFIED` must pass for the exact language/surface;
- the English asset packet must pass current asset QA;
- the localized pitch must be native/fluent reviewed and encoding-clean;
- gameplay, pressure, route-risk, threat, salvage, base-failure, or first-public agency proof must name an AB-009/KPI source field: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`;
- public Steam, wishlist, demo, signup, presskit, or trailer links must pass the exact destination gate (`steam_page_publish_permission_gate`, `demo_public_access_permission_gate`, `owned_audience_permission_gate`, `press_release_permission_gate`, or matching asset/publication gate) plus destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`;
- if the public CTA is not activated, use a no-link feedback ask or private access route with `verified_contact_route`, `access_route_class`, `reply_status_after_send`, `reply_consent_provenance`, and `agency_decision_field_source` where proof claims are used;
- new regional lead verification happens only when the current CRM has a source-backed asset/route gap.

## Priority Regions

| Region | Why It Matters | First Asset Needed | Risk |
|---|---|---|---|
| English global | Largest creator/press surface | screenshots + Steam page | high competition |
| Russian/CIS | Strong survival/horror video culture; user-native market knowledge | Russian one-pager + screenshots | platform/payment/geopolitical complications |
| German | Long-form LP, simulation, survival audiences | systems/base/machinery proof | expects clear production quality |
| Polish | Survival/horror and indie coverage fit | demo/screenshot pack | smaller pool, needs verification |
| French | Strong YouTube/Twitch variety and indie press | polished visual hook | top creators hard to reach |
| Spanish | Horror/survival reaction potential | short clips | broad variety channels can be poor fit |
| Portuguese/Brazil | Survival/horror/YouTube reach | localized short pitch | localization quality |
| Japanese/Korean | Potential later if visual hook is extremely strong | localized trailer/Steam page | high localization expectation |

## Russian Pitch Draft

Subject:

HECTON-8 - mrachnoe podvodnoe vyzhivanie v NASA-punk stile

Short pitch:

HECTON-8 - odinochnoe podvodnoe vyzhivanie pro davlenie, tekhniku, poisk resursov i chernuyu vodu. Eto ne obeshchanie kooperativa i ne popytka prodavat igru cherez sravnenie s konkurentami. Glavnyi kryuchok: baza kak mashina, glubina kak ugroza, Seed Ship kak anomalya, kotoraya portit pribory, marshruty i oshchushchenie bezopasnosti.

Ask:

Esli format podoidet, mozhno obsudit demo ili press-kit, kogda build, dostup, `verified_contact_route`, `access_route_class` i `reply_consent_provenance` budut gotovy. Materialy otpravlyat tolko posle exact destination gate plus `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED` dlya public-linkov ili recipient/batch `private_access_permission_gate = ALLOW_PRIVATE_ACCESS_VERIFIED`, official inbox custody, disclosure, plus exact access-log fields dlya private-route.

## German Pitch Draft

Subject:

HECTON-8 - Deep-sea noir survival with pressure and machinery

Short pitch:

HECTON-8 ist ein single-player-first Unterwasser-Survival-Spiel mit NASA-punk Maschinen, schwarzem Tiefseewasser, Bergung, Drucksystemen und industrieller Isolation. Der Fokus bleibt single-player-first und proof-first; kein Wettbewerber-Angriff. Der Fokus liegt auf lesbaren Survival-Systemen und schwerer Technik.

## French Pitch Draft

Subject:

HECTON-8 - survie sous-marine industrielle / deep-sea noir

Short pitch:

HECTON-8 est un jeu de survie sous-marine single-player-first autour de la pression, des machines, de la recuperation, de la corrosion et de l'isolation en eaux profondes. Le positionnement reste single-player-first et fonde sur les preuves; pas d'attaque concurrentielle.

## Spanish Pitch Draft

Subject:

HECTON-8 - supervivencia submarina industrial y terror de profundidad

Short pitch:

HECTON-8 es un juego de supervivencia submarina single-player-first centrado en presion, maquinaria, rescate, oscuridad y sistemas que pueden fallar. El enfoque se mantiene single-player-first y basado en pruebas; sin ataque a competidores.

## Portuguese/Brazil Pitch Draft

Subject:

HECTON-8 - sobrevivencia submarina industrial em aguas escuras

Short pitch:

HECTON-8 e um jogo single-player-first de sobrevivencia submarina sobre pressao, maquinas, salvamento, corrosao e isolamento no fundo do mar. O foco continua single-player-first e baseado em prova; sem ataque a concorrentes.

## Regional Lead Workflows

For each region:

1. record the source-backed asset/route gap that English CRM cannot cover;
2. verify only the rows needed for that gap; 30 raw leads is a ceiling, not a default quota;
3. write native or reviewed pitch;
4. prepare one localized one-page PDF/Markdown;
5. do not translate gameplay, pressure, route-risk, threat, salvage, or base-failure claims unless AB-009/KPI field source exists;
6. route public CTA through the exact destination permission gate plus destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`, or use a no-link/private-access fallback;
7. track `send_route_class` for sends, exact private access-log fields if private, and `reply_consent_provenance` separately from English outreach.

## Regional One-Pager Fields

- Title.
- One-sentence pitch.
- What exists now.
- Proof boundaries / unsupported scope.
- Screenshots.
- Steam link only after `steam_page_publish_permission_gate = ALLOW_STEAM_PAGE_PUBLISH_VERIFIED` and destination-specific `public_cta_permission_gate = ALLOW_PUBLIC_CTA_VERIFIED`.
- Demo status.
- Destination permission gate plus `public_cta_permission_gate`, or no-link route.
- AB-009/KPI agency field source: `what_decision_next`, `agency_decision_read`, or `cold_read_agency_decision`.
- `send_route_class` for sends, or exact private access-log fields if private.
- `reply_consent_provenance`.
- Contact.
- Disclosure/private access policy.

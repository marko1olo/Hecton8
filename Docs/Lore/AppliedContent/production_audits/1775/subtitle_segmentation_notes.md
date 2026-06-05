# Subtitle Segmentation Notes - 1775

Evidence class: STATIC_DOC.

Scope: changed `en_US` authority audio/transcript lines only. Non-English rows need text sync and native review before VO or runtime-ready claims.

## Changed Source Lines And Beats

| Packet | Source text | Subtitle beats | Notes |
|---|---|---|---|
| P436_BLACK_KEEL_APPROACH_TRANSCRIPT_SEED | Black Keel tender to claimant. Lag four seconds. Descent billed. Return not guaranteed. Send health, mass, proof. | 1. Black Keel tender to claimant. 2. Lag four seconds. 3. Descent billed. 4. Return not guaranteed. 5. Send health, mass, proof. | Carrier automation. Five short beats; source label required on first beat. |
| P437_DEEP_REACH_SANITIZED_PACKET_TRANSCRIPT_SEED | Recovery Compliance packet. Safety priority acknowledged. Coordinates first. Quarantine cost pending. Rescue cannot be priced blind. | 1. Recovery Compliance packet. 2. Safety priority acknowledged. 3. Coordinates first. 4. Quarantine cost pending. 5. Rescue cannot be priced blind. | Corporate omission. Beats 4-5 are legal/cost pressure, not comfort. |
| P438_WORKER_DOSSIER_AUDIO_TRANSCRIPT_SEED | Mara Venn, pump chief. If the pump sings clean, log it. If it coughs twice, stop arguing and take the upper stairs. | 1. Mara Venn, pump chief. 2. If the pump sings clean, log it. 3. If it coughs twice, stop arguing and take the upper stairs. | Human worker fragment. Keep job title in beat 1. |
| P439_ATLAS_REPAIR_TRACE_TRANSCRIPT_SEED | ATLAS-6 maintenance trace. Leak closed. Cable growth accepted. Warm-body route serviceable. Human-use category unresolved. | 1. ATLAS-6 maintenance trace. 2. Leak closed. 3. Cable growth accepted. 4. Warm-body route serviceable. 5. Human-use category unresolved. | Atlas damaged telemetry. Preserve category conflict clue. |
| P440_ENDING_RECORD_TRANSCRIPT_SEED | After-action record. Receiver accepted payload hash. Claim closed for accounting. Suppression flag unreadable. Truth custody unresolved. | 1. After-action record. 2. Receiver accepted payload hash. 3. Claim closed for accounting. 4. Suppression flag unreadable. 5. Truth custody unresolved. | Ending/dossier. Spoiler-gated use only. |
| P286_CAPSULE_BLACKBOX_AUDIO_01 | Black box event. Survival burn complete. Ascent sleeve spent. Recovery ring no-lock. Frames twelve through nineteen missing. | 1. Black box event. 2. Survival burn complete. 3. Ascent sleeve spent. 4. Recovery ring no-lock. 5. Frames twelve through nineteen missing. | Black-box fragment. Core clue: ascent path was spent. |
| P290_QUARANTINE_RELAY_FRAGMENT | Relay hold. Extraction accepted. Release denied: sample custody, air review, claimant dispute. You are dry, not free. | 1. Relay hold. 2. Extraction accepted. 3. Release denied: sample custody, air review, claimant dispute. 4. You are dry, not free. | Custody relay. Beat 3 may need two subtitle cards in narrow UI. |
| P246_BLACK_KEEL_APPROACH_AUDIO_PACKET | Black Keel tender to claimant. Descent window billed. Return conditional. Four-point-eight tonne-window lien open. | 1. Black Keel tender to claimant. 2. Descent window billed. 3. Return conditional. 4. Four-point-eight tonne-window lien open. | First-hour approach. Preserve numeric debt phrase. |
| P249_SANITIZED_ACCIDENT_PACKET_BODY | Deep Reach public packet. Correct nouns. Missing decisions. Evacuation hold field not provided. | 1. Deep Reach public packet. 2. Correct nouns. 3. Missing decisions. 4. Evacuation hold field not provided. | First lie. No full liability chain revealed. |
| P250_FIRST_ATLAS_REPAIR_TRACE_SCENE | Maintenance trace. Object stabilized. Name tag sealed under growth. Owner category unresolved. | 1. Maintenance trace. 2. Object stabilized. 3. Name tag sealed under growth. 4. Owner category unresolved. | First Atlas trace. Useful repair plus identity violation. |

## Subtitle Risk Notes

| Packet | Duration risk | Subtitle risk | Action |
|---|---|---|---|
| P436 | Medium | Five short beats. | Runtime caption timing required. |
| P437 | Medium | Legal phrase expansion in DE/RU/PL likely. | Locale short-form review required. |
| P438 | Low | Human name/title must remain stable. | Voice casting required. |
| P439 | Low | Atlas terms are clipped but abstract. | Keep source label. |
| P440 | Low | Spoiler-gated ending text. | Do not place before ending route. |
| P286 | Low | Numbers must render clearly. | Caption UI digit test required. |
| P290 | Medium | Beat 3 is long. | Split on narrow subtitle UI. |
| P246 | Low | Tonne-window phrase must survive locale. | Native review required. |
| P249 | Low | Short legal omission line. | Good for early subtitle. |
| P250 | Low | Category language may expand. | Locale review required. |

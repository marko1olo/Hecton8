# Controller Public/Wiki Surface Map P500-P520

Evidence class: STATIC_CONTROLLER_SYNTHESIS.
Runtime proof: absent.
Publication proof: absent.
Native localization proof: absent.
Generated-page proof: absent.

Mandates followed: QA_Evidence_Text_Filter_Audit; UI_Localization_Babel_RTL_FontSwap_ZeroAlloc; DATA_Runtime_Struct_Layout_ARM64; TOOL_Designer_Facades_CSV_Binary_Bridge.

## Scope

Maps P500-P520 AppliedContent packets to future public website and wiki surface roles. This is not publication, generated-page export, source admission, or runtime string-pool extraction.

## Surface Groups

| Packet range | Public website role | Wiki role | In-game evidence role |
|---|---|---|---|
| P500-P502 | Public evidence governance: receiver ambiguity, cleanup bids, claimant-safe conflict. | Evidence policy pages for receiver labels, market pressure, and claimant-safe summaries. | PDA/archive notes that prevent early conclusion text. |
| P503-P505 | Counter-index, payload alias, quarantine/legal hold conflict. | Procedure notes for contested labels, alias registers, and double-hold classification. | Scanner/terminal prompts that keep public labels and physical route marks separate. |
| P506-P508 | Proof order, spoiler-safe relation edge, terminal receipt rewrite. | Evidence graph and receipt procedure pages. | Scanner/PDA/terminal prompts that keep object proof before clean labels. |
| P509-P511 | Custody divergence, scanner downgrade reason, PDA family review. | Confidence and custody pages explaining partial evidence. | PDA family review prompts and scanner downgrade labels. |
| P512-P514 | Dispute reason code, resolution hold, next-proof checklist. | Unresolved-evidence procedure pages. | Action prompts that name the next proof target without declaring a conclusion. |
| P515-P517 | Contradiction card, claimant-safe redaction audit, route-alias conflict hint. | Contradiction, redaction, and alias procedure pages that keep disputed facts visible. | PDA/scanner/archive prompts that preserve both sides of a conflict and point to proof order. |
| P518-P520 | Source voice label, evidence confidence ladder, proof escalation warning. | Provenance, field-confidence, and proof-gate pages that explain how to read evidence safely. | PDA/scanner/terminal labels that name speaker lane, confidence state, held proof class, and safe comparison lane. |

## Public Site Guardrails

- Public copy may explain the evidence-reading method.
- Public copy must not reveal final receiver, final legal result, protected claimant, exact route branch, Atlas consequence, ending branch, rescue conclusion, source admission, runtime placement, or h8bin state.
- Public pages should use short paragraphs and avoid in-game checklist density unless the page is a procedure sidebar.
- The public site can link related packet families, but relation edges must stay spoiler-safe.
- P518-P520 public copy must separate source voice, field confidence, and escalation warning. Do not compress them into one generic "uncertain evidence" label.

## Wiki Guardrails

- Wiki pages may include procedure definitions and relation labels.
- Wiki pages must mark confidence state and missing proof class when relevant.
- Wiki pages must not convert `disputed`, `held`, `downgraded`, `claimant-safe`, `contradiction-open`, or `proof-escalated` into verdict language.
- Wiki pages must keep source text separate from runtime implementation status.
- P518 source voice pages must label speaker lane before making any relation edge.
- P519 confidence pages must attach confidence to fields, not whole stories.
- P520 escalation pages must name the held proof class and one safe comparison lane without revealing the held answer.

## In-Game Guardrails

- Scanner strings stay short: confidence, reason, next proof.
- PDA strings can show family grouping, checklist state, source voice, and held proof class.
- Terminal strings stay procedural and object-specific.
- Captions should name the evidence object and current uncertainty, not conclusion.
- Runtime must consume baked string-pool rows after source/bake gates; it must not parse Markdown or JSON source candidates.

## Review Queue

1. Public spoiler review for P500-P520.
2. Wiki relation-label review for P507, P511, P512, P513, P514, P515, P518, P519, and P520.
3. Native localization review for all non-English rows.
4. Source admission planning under explicit source/bake owner.
5. Generated-page export only after source admission and page-template owner proof.

## Boundary

This map is a controller routing artifact. It does not prove source CSV rows, route cards, generated pages, public site pages, wiki pages, native-reviewed text, runtime string pools, Unity placement, DataMonolith payload, h8bin bake, or player-build behavior.

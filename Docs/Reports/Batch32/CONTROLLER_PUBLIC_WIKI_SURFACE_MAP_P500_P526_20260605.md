# Controller Public/Wiki Surface Map P500-P526

Evidence class: STATIC_CONTROLLER_SYNTHESIS.
Runtime proof: absent.
Publication proof: absent.
Native localization proof: absent.
Generated-page proof: absent.

Mandates followed: QA_Evidence_Text_Filter_Audit; UI_Localization_Babel_RTL_FontSwap_ZeroAlloc; DATA_Runtime_Struct_Layout_ARM64; TOOL_Designer_Facades_CSV_Binary_Bridge.

## Scope

Maps P500-P526 AppliedContent packets to future public website, wiki, PDA, scanner, terminal, caption, and string-pool surface roles. This is not publication, generated-page export, source admission, or runtime string-pool extraction.

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
| P521-P523 | Spoiler-safe crosslink label, related-article unlock hint, relation-edge suppression reason. | Navigation and relation-edge pages that keep links useful without exposing hidden relations. | PDA/scanner/terminal navigation prompts that explain safe links, locked links, and suppressed graph edges. |
| P524-P526 | Review queue stamp, page-template hold notice, evidence link audit trail. | Page-state and link-history pages that explain held review, held sections, and changed relation links. | PDA/scanner/terminal state prompts that show visible cause and safe next action without exposing hidden relations. |

## Public Site Guardrails

- Public copy may explain the evidence-reading and evidence-navigation method.
- Public copy must not reveal final receiver, final legal result, protected claimant, exact route branch, Atlas consequence, ending branch, rescue conclusion, source admission, runtime placement, or h8bin state.
- Public pages should use short paragraphs and avoid in-game checklist density unless the page is a procedure sidebar.
- The public site can link related packet families, but relation edges must stay spoiler-safe.
- P524-P526 public copy must separate review queue state, template hold state, and link audit state. Do not make held pages look published, final, broken, or source-admitted.

## Wiki Guardrails

- Wiki pages may include procedure definitions and relation labels.
- Wiki pages must mark confidence state and missing proof class when relevant.
- Wiki pages must not convert `disputed`, `held`, `downgraded`, `claimant-safe`, `contradiction-open`, `proof-escalated`, `crosslink-held`, `edge-suppressed`, `review-queued`, `section-held`, or `link-audited` into verdict language.
- Wiki pages must keep source text separate from runtime implementation status.
- P524 review queue pages must name pending proof class and safe next action.
- P525 template hold pages must name section type and safe requirement without revealing the held answer.
- P526 link audit pages must show previous state, new state, visible cause, and safe next action without revealing hidden relation.

## In-Game Guardrails

- Scanner strings stay short: confidence, reason, next proof.
- PDA strings can show family grouping, checklist state, source voice, held proof class, link state, review state, and suppression reason.
- Terminal strings stay procedural and object-specific.
- Captions should name the evidence object and current uncertainty, not conclusion.
- Runtime must consume baked string-pool rows after source/bake gates; it must not parse Markdown or JSON source candidates.

## Review Queue

1. Public spoiler review for P500-P526.
2. Wiki relation-label review for P507, P511, P512, P513, P514, P515, P518, P519, P520, P521, P522, P523, P524, P525, and P526.
3. Native localization review for all non-English rows. P521-P526 especially need native replacement because current non-English rows are ASCII-safe machine drafts.
4. Source admission planning under explicit source/bake owner.
5. Generated-page export only after source admission and page-template owner proof.

## Boundary

This map is a controller routing artifact. It does not prove source CSV rows, route cards, generated pages, public site pages, wiki pages, native-reviewed text, runtime string pools, Unity placement, DataMonolith payload, h8bin bake, or player-build behavior.

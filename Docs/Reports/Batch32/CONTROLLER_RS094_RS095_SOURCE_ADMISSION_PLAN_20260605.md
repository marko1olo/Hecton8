# Controller RS094/RS095 Source Admission Plan

Evidence class: STATIC_CONTROLLER_PLAN.

Scope: P465-P477 lore-system bridge packets and their future admission into AppliedLore source/import/export flow.

No runtime, Unity, h8bin, source CSV, route-card CSV, generated hash/page, native localization, or publication readiness is claimed here.

## Current State

RS093 is the only new bridge set with source wiring:

- P461-P464 have canonical packet JSON and release-set manifest source wiring.
- Latest source-only audit reported `AppliedLore source audit OK` with 464 packets and 6960 localized rows.
- Runtime, native localization, DataMonolith/static_data.h8bin, Unity placement, route-card readiness, player-build proof, and publication readiness remain unclaimed.

RS094 is a source candidate:

- P467-P474 have `RS094_PUBLIC_AUTHORITY_BRIDGE_EXPANSION.packets.json`.
- Static JSON shape passes: 8 packets, 15 locales per packet, required localized surface keys present, U+FFFD=0.
- Manifest flags `canonical_importer_ready=false` and `runtime_ready=false`.
- Protected-path clean check is blocked by shared dirty worktree, not by RS094 JSON shape.

Loose STATIC_DOC packets not admitted to source:

- P465_DEEP_REACH_MANAGED_VARIANCE_BRIDGE
- P466_WORKER_TAG_EVIDENCE_BRIDGE
- P475_CENTAURI_CHARTER_LEGITIMACY_BRIDGE
- P476_AEGIR_CONTINUITY_HOLDINGS_SHELL_CHAIN_BRIDGE, controller-repaired static packet
- P477_RECOVERY_COMPLIANCE_RETURN_ACTION_QUEUE_BRIDGE, controller-repaired static packet
- P478_ATLAS_CONTINUITY_OFFICE_WORKER_SAFETY_WAIVER_BRIDGE, controller-repaired static packet
- P479_KEELMARK_LOSS_DESK_CONVERSION_BRIDGE, controller-repaired static packet
- P480_CONTRACT_CONTINUITY_DESK_RECOVERY_LANGUAGE_BRIDGE, validated static packet
- P481_PACKET_NOTARY_INTERFACE_WITNESS_HASH_BRIDGE, validated static packet
- P482_QUARANTINE_REVIEW_GATE_DELAY_BRIDGE, validated static packet
- P483_ASSET_SILENCE_BOARD_SUPPRESSION_BRIDGE, validated static packet

## Admission Boundary

Runtime must consume only baked/static-data rows after validated source flow. Runtime must not parse Markdown production packets, report files, release-set prose, task files, or live JSON candidates.

Next source owner must not edit `static_data.h8bin` or generated hashes/pages unless the process gate is clean and the import/export toolchain owner is explicitly assigned.

## Proposed Release Split

Keep RS094 as public authority expansion:

- P467 Atlas-6 public repair network
- P468 Xenon-Omega public material
- P469 Aegir relay window
- P470 Keelmark tonne-window
- P471 Luyten packet custody relay
- P472 Tau Ceti public ledger pressure
- P473 Barnard Yards salvage origin
- P474 Sol Core remote claim authority

Create a later RS095 candidate for corporate pressure and shell-chain evidence:

- P465 Deep Reach managed variance
- P466 Worker tag evidence
- P475 Centauri charter legitimacy
- P476 Aegir Continuity Holdings shell chain
- P477 Recovery Compliance return-action queue
- P478 Atlas Continuity Office worker-safety waiver
- P479 Keelmark Loss Desk conversion
- P480 Contract Continuity Desk recovery language
- P481 Packet Notary Interface witness hash
- P482 Quarantine Review Gate delay
- P483 Asset Silence Board suppression

Reason: P467-P474 already form a broad public authority bridge. P465/P466/P475-P483 are stronger as the next chain: corporate variance language, human evidence tags, old legitimacy charter, dirty Aegir shell ownership, present-tense return pressure, Atlas/process continuity waiver language, loss-ledger conversion, contract wording, notary custody, quarantine delay, and suppression surfaces.

## Source Owner Checklist

1. P476/P477/P478/P479 controller validation is complete: 15 locale sections, 1 source_authority, 14 draft_machine_or_llm, U+FFFD=0, mojibake marker hits=0, readiness overclaims=0.
2. P480/P481/P482/P483 controller validation is complete: 15 locale sections, 1 source_authority, 14 draft_machine_or_llm, U+FFFD=0, mojibake marker hits=0, readiness overclaims=0.
3. Build RS095 packet JSON candidate from P465, P466, P475, P476, P477, P478, P479, P480, P481, P482, and P483 only after the current 3233 seven-packet candidate is integrated or superseded by a new explicit source-prep owner.
3. Add manifest with `authoring_packet_sources`, `canonical_importer_ready=false`, `runtime_ready=false`.
4. Do not set `packet_sources` or `canonical_importer_sources` until importer ownership is active and the shape matches the accepted importer schema.
5. Run source-only JSON shape validation.
6. Only with a clean process gate, assign a separate importer/source owner to wire source CSV/export.
7. Only after source audit passes, assign route-card/binding/graph owners.
8. Only after source/import/export proof, assign h8bin/DataMonolith validation.

## Failure Modes

- False runtime claim: report says runtime-ready because a JSON or Markdown packet exists. Reject.
- Native localization claim: non-English rows are draft coverage without native review. Reject.
- Route-card overreach: route cards for packets absent from source/export are candidate-only. Reject.
- Dirty worktree confusion: protected-path diffs from other agents are not proof of failure, but they block clean-path admission proof.
- CSV mojibake inheritance: existing older source CSV rows visibly contain mojibake; do not hide that by admitting new packets as localized quality proof.

## Low / Middle / High / Ultra Consequences

- Low: compact runtime can expose fewer optional surfaces, but Article ID, LocID, unlock identity, canon fact, and evidence state stay identical.
- Middle: scanner/terminal/wiki surfaces can all exist after bake, with audio transcript as text-only if audio system proof is absent.
- High: add richer cross-links, relation records, black-box fragments, and route-specific variants after source proof.
- Ultra: add dense website/wiki branching, voiced variants, evidence graph overlays, and localization QA passes, still without changing gameplay truth ownership or save identity.

# 2026-05-20 Documentation R46 Root/Architecture Interior Authority, Route Fields, and Proof Language

Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL planned validation.

Scope: active root and `Docs/ARCHITECTURE` documentation. Historical archives remain historical snapshots.

## What Was Wrong

- Some active root glossary/FAQ text still opened with an R43 "current boundary" paragraph before the later R45 paragraph.
- Root release/workflow docs still allowed singleton and `DontDestroyOnLoad` wording to read as acceptable architecture for new work.
- Global-authority boundary/migration docs foregrounded older R43/R42 counter tuples instead of the R45 source-scale baseline.
- Route-card tables had owner/phase/capacity/proof fields but no explicit `Instrument` row for the route mechanism.
- Some route-card black-box dump paths were written like existing proof artifacts instead of planned/generated-on-fault targets.
- Static source scans, RenderGraph wording, AudioSource wording, and microsecond text still let docs read stronger than the available evidence.

## What Changed

- Promoted R46 as the current local static root/architecture documentation boundary for interior authority wording, route-field completion, and proof-language cleanup.
- Reclassified singleton/DDOL entries in `MASTER_RELEASE_WORK_PLAN.md` as historical capture/legacy notes, not new architecture approval.
- Updated global-authority counter orientation to the R45/R46 baseline: `GlobalRegistryHits=6199`, `PubSubHits=575`, `NativeHits=18045`, `NativeQueueRefs=116`, `ConfigureEnsure=271`, `CreateQueueSlots=73`, `EnsureLanes=135`, and `ScriptTypedLanes=1345`.
- Added `Instrument` fields to active route-card tables and clarified telemetry/black-box/fault-dump fields for SHINOBU_138 and SHINOBU_200.
- Demoted dump paths to planned/generated-on-fault targets unless a timestamped runtime trigger and output artifact are linked.
- Tightened static-evidence wording in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `GLOBAL_SIGNAL_CORRIDOR.md`, `SYSTEM_INTERCONNECT_MATRIX.md`, `URP_SCREENSHOT_PIPELINE.md`, `ADAPTIVE_STEM_AUDIO_MIXER.md`, `MACRO_ECOSYSTEM_MATHEMATICIAN.md`, `SAVE_V8_BINARY_SPEC.md`, `SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`, and `SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`.

## Validation

Pending final static gate run after atlas regeneration and boundary promotion.

## Runtime Boundary

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, analytics endpoint, network send, or visual-route proof was run in this pass.

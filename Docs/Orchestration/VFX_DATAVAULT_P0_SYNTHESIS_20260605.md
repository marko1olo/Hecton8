# VFX DataVault P0 Synthesis - 2026-06-05

Status: `STATIC_SYNTHESIS / SOURCE ROUTE IMPROVED / PENDING AUDIT AND UNITY PROOF`
Evidence class: `STATIC_DOC + STATIC_SOURCE_CONTEXT + STATIC_TOOL_OUTPUT`

No Unity, dotnet build, import, Play Mode, profiler, source code edit, scene edit, prefab edit, material edit, or raw YAML edit was performed by this synthesis.

## Verdict

VFX DataVault status is not a single blanket migration task. The current evidence splits into three different routes:

- Biolum black-box mirror: owner-local telemetry exception is accepted as a source decision, pending proof.
- MarineSnow runtime scratch: current disk state appears rewritten through DataVault; proof is still missing.
- Editor/offline scratch: must be documented or isolated, but must not be treated as gameplay runtime DataVault debt.
- PlasmaBeam fault dump payload: still needs bounded fault/export review.

## Biolum Route

Source decision:

- `Docs/AssetAudit/BIOLUM_BLACKBOX_ROUTE_DECISION_20260605.md`
- `BiolumPulseSyncRuntime.cs:311-315` source decision comment.

Current classification:

- `ACCEPT_OWNER_LOCAL_PENDING_PROOF`
- owner: `BIOLUM_PULSE_SYNC`
- capacity: 300 frames
- allocator: `Allocator.Persistent`
- lifetime: session
- purpose: crash/explicit-dump black-box telemetry mirror, not gameplay authority
- rejected alternative: blind DataVault migration

Anchor-map current rows:

- `BIOLUM_319`
- `BIOLUM_336`
- `BIOLUM_384`
- `BIOLUM_3993`

Required proof still missing:

- compile;
- Unity import;
- GC/profiler;
- deterministic runtime dump artifact;
- scanner re-run after any source touch.

## MarineSnow Route

Current disk state appears better than the older anchor-map wording:

- runtime `_mockWakeScratch` / `_propwashEventScratch` ownership debt is no longer present as persistent runtime scratch in the reviewed source;
- mock wake writes through DataVault lock and uploads from that buffer around `HectonMarineSnowRenderer.cs:2569`;
- mock propwash and procedural wake use DataVault write buffers around `HectonMarineSnowRenderer.cs:2780` and `3020`.

Required status:

- preserve this DataVault rewrite;
- do not reintroduce `_mockWakeScratch` or `_propwashEventScratch`;
- rerun `Tools/DataVaultSovereigntyAudit.py` and targeted tests before claiming repair.

Editor/offline debt:

- `MARINE_712`
- `MARINE_2005`

`Docs/AssetAudit/DATAVAULT_AUDIT_EXECUTION_SURFACE_RECHECK_20260605.md` proves line `2005` is inside the editor CSV reader region. Do not perform runtime source repair against `MARINE_2005`; route it as editor/offline owner debt.

## Fault Dump Route

MarineSnow and PlasmaBeam share a bounded fault-path caveat:

- MarineSnow `RecordTelemetry` can call `DumpBlackBoxOnce` from `RunMarineSnowVisualTick` around `HectonMarineSnowRenderer.cs:1171` and `5516`.
- MarineSnow dump payload uses `NativeFaultDumpWriter.CreateTransientPayload` around `HectonMarineSnowRenderer.cs:5604`.
- PlasmaBeam fault payload allocation is around `ShinobuPlasmaBeamRuntime.cs:1487`, not the old 1483 anchor.
- `NativeFaultDumpWriter.CreateTransientPayload` defaults to `Allocator.Temp`.
- `CoreLowLevelUtilities.cs` file-write helpers allocate managed arrays around lines `197`, `207`, and `233`.

Classification: bounded fault-path risk, not steady-state native ownership debt. If tightening zero-GC fault handling, fix `NativeFaultDumpWriter.TryWriteAll` first or move MarineSnow/Plasma dumps to a preowned background dump worker like Biolum.

## Owner 05 Repair Order

1. Do not bulk-migrate all NativeArray declarations.
2. Preserve Biolum owner-local black-box decision unless fresh source/proof contradicts it.
3. Preserve current MarineSnow DataVault rewrite; do not reintroduce runtime scratch fields.
4. Route MarineSnow editor/offline scratch separately: `MARINE_712`, `MARINE_2005`.
5. Review MarineSnow/PlasmaBeam fault payloads as bounded fault/export routes; address shared `NativeFaultDumpWriter` managed allocation risk only with an owner-correct background/preowned route.
6. Rerun DataVault audit and unit tests.
7. Only after source edits: compile, Unity Console, Play Mode, GC/profiler, and forced dump artifacts.

## Low / Middle / High / Ultra

- Low: preserve route readability and no hot allocation; no binary quality switches.
- Middle: same gameplay truth with conservative VFX staging.
- High: richer VFX only after DataVault/telemetry proof.
- Ultra: extra density/debug evidence only through continuous `GlobalQualityWeight`, without changing authority, DTO layout, save identity, or public contracts.

Final status: `PENDING VERIFICATION`.

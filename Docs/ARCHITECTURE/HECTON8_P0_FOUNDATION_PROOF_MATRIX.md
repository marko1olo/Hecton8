# HECTON-8 P0 Foundation Proof Matrix

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

## Current Matrix

| Foundation area | Current state | Missing proof |
|---|---|---|
| root documentation policy | root scan shows only allowed markdown files | none for filesystem scan; rerun before release |
| save container constants | source reports version `0x000B`, header `56`, legacy header `44` | save/load runtime artifact |
| Data Monolith | target path defined; `static_data.h8bin` present in X_012 filesystem scan; Unity/player boot proof pending | bake/import/boot validation |
| global authority boundaries | contracts updated for registry, signals, event bus, vault | runtime lane/profiler proof |
| memory sovereignty | DataVault route documented; buffer ranges summarized | leak/dump proof and owner disposal proof |
| continuous scalability | `GlobalQualityWeight` route documented | frame-time and shader capture |
| AUP precision | double-subtract sequence documented | static compliance scan and runtime rebase replay |
| netcode | Merkle protocol documented as static only | transport, loopback, fuzz, replay, profiler, GC proof |
| UI | zero-GC route documented | GCMonitor/Memory Profiler artifact |

## 2026-05-21 Static Source Risk Register

These counts are regex triage over `Assets/_Project/Scripts/**/*.cs`; they are not compiler, AST, Burst Inspector, profiler, or runtime proof.

| Risk | Count | Required owner action |
|---|---:|---|
| private persistent native collection fields | 878 raw / 842 focused underscore-field hits | prove owner-local private scratch or migrate shared/persistent truth to `GlobalDataVault` |
| `[BurstCompile]` missing explicit flags | 24 sidecar / 23 exact bare local hits | add domain-appropriate Burst flags or document deterministic exception |
| interface-array declarations | 6 | prove cold/editor path or replace hot route with contract/generic/static dispatch |
| direct `[StructLayout(... Pack = 1)]` attributes | 0 | keep zero; scanner-string hits are false positives unless source attribute appears |
| files with both `GlobalRegistry` and frame-loop method names | 60 | method-scope review; hot polling remains rejected |
| live Unity random gameplay hits | 0 | keep gameplay RNG on deterministic `Unity.Mathematics.Random` |
| direct physics query / `Rigidbody.AddForce` outside allowed owners | 0 sidecar-confirmed live hits | keep large terrain/mass queries on SDF/Burst/GPU routes |
| UI/visor hot `.ToString()` or interpolated strings | 0 sidecar-confirmed live hits | keep hot UI text on char-buffer/TMP route |

Static scan also identified active documentation defects in modding/geology/kinematics wording. Those were patched to continuous `GlobalQualityWeight` and AUP-local math on 2026-05-21.

## Evidence Labels

Allowed statuses:

- `PENDING VERIFICATION`
- `STATIC_SOURCE OBSERVED`
- `STATIC_TOOL PASS`
- `RUNTIME ARTIFACT LINKED`

Do not use `COMPLETE`, `VERIFIED`, or `PRODUCTION READY` without artifact links.

## Non-Claims

This matrix is not a build log, Unity import log, Play Mode proof, profiler proof, GC proof, or player-build proof.

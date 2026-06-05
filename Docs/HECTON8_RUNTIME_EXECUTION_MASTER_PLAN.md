# HECTON-8 Runtime Execution Master Plan

Date: 2026-05-28
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC

Purpose: stable execution order for moving HECTON-8 toward a measurable runtime slice. This is not a build log, DOTS advocacy note, or dated report chain.

## Authority Boundary

- Read `Docs/PROJECT_BASELINE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, and `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` before using this plan.
- Product-route work must also read `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` and `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`.
- Current proof snapshots live in the actuality ledger, not here.
- Source under `Assets/_Project` wins over this plan when implementation has drifted.
- No Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, save/load, shader, platform, or visual proof is implied by this document.

## Operating Rules

- One owner per runtime truth.
- `GlobalRegistry` is cold identity/DI only.
- Hot first-party broadcasts use `SignalBus<T>`.
- `GlobalSignals` direct queues are legacy bridge lanes.
- `HectonEventBus` is managed mod/API isolation.
- Cross-domain persistent/job-visible native state routes through `GlobalDataVault` or an explicit owner-local contract.
- Read accessors stay pure: no allocation, scene search, publish, sync, job completion, or global mutation.
- `GlobalQualityWeight` is continuous. No binary low/ultra quality branch is accepted as the scalability model.
- Any system above `0.1ms` per frame is suspicious until profiler proof and load-shed behavior exist.
- Visual fake first for water, light, flow, pressure, deformation, ambience, cables, particles, and distant motion.

## Execution Order

1. Re-establish proof boundary.
   - Fresh compile only proves the named source slice.
   - Runtime readiness needs Unity import, Console, Play Mode or player, profiler, GC/memory, shader/render, save/load, platform, and visual artifacts.

2. Clean native ownership.
   - Remove persistent native collections from `MonoBehaviour` and manager fields unless an owner contract proves lifetime and disposal.
   - Prefer `GlobalDataVault` handles, owner-local scratch, stack-only views, and job-transient buffers.
   - Use the latest native ownership ledger from the actuality ledger as the current debt map.

3. Protect global authority.
   - No new hot registry polling.
   - No unmanaged state hidden behind read accessors.
   - No new queue/signal route without owner, phase, capacity, overflow policy, telemetry, shutdown, and proof.

4. Prove Data Monolith boot.
   - `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` must pass import, bake, boot, checksum, and player validation before any static-data readiness claim.

5. Prove save and paging.
   - Use source contracts in `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`, `Docs/ARCHITECTURE/DATA_MONOLITH_RUNTIME_INTEGRATION.md`, and `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`.
   - Required proof: write/read, migration, checksum failure, locked-file behavior, and player path.

6. Stabilize world runtime.
   - Keep one world/scatter owner path.
   - Finish hybrid owner cleanup before expanding DOTS.
   - DOTS is accepted only after total-frame profiler gain, semantic parity, and no second-owner debt.

7. Prove first playable route.
   - Runtime/content work should remove a blocker for the first 20 minutes.
   - No broad system expansion without route value or owner-debt reduction.
   - Current static new-game spine: `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.
   - Current V0 proof target: Copper Wire route from boot/world/swim/copper/quest/craft to save/load restored state.

8. Harden presentation with budgeted fakes.
   - Use saved CPU/GPU budget to buy better visuals.
   - Quality weight scales cadence, fidelity, density, buffers, and optional telemetry.
   - It does not change gameplay truth ownership, DTO layout, save identity, or authority route.

9. Close verification.
   - Compile.
   - Unity import and Console.
   - Play Mode or player capture.
   - Profiler and GC/memory capture.
   - Save/load proof.
   - Shader/render proof.
   - Platform proof.
   - Screenshot/clip proof for player-facing claims.

## Rejection Conditions

- New second owner for existing gameplay truth.
- Private persistent native collections in `MonoBehaviour` without owner/disposal proof.
- Same-frame job schedule/readback without profiler proof.
- Current report path used as architecture doctrine.
- Binary quality switch where a continuous scalar is required.
- "Works in editor" used as production proof.
- DOTS migration justified by style instead of measured total-frame value.

## Immediate Engineering Bias

- Burn down native ownership and global authority debt before feature expansion.
- Prefer source-backed contracts over report prose.
- Keep docs short enough to be read before coding.
- Move stale report chains and obsolete planning bundles to archive/deprecated storage.

STATUS: PENDING VERIFICATION

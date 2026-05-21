# Platform Portability Proof Ladder

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence class: STATIC_DOC. This is platform policy, not proof that any
platform build, import, launch, profiler capture, GC capture, memory capture,
Frame Debugger pass, or device run has succeeded.

## Purpose

This file is the stable policy for platform readiness claims. Dated reports may
record snapshots; this file owns the order and proof standard.

## Senior Verdict

Windows flat PC plus the Copper Wire V0 route is the first proof target. Other
platform work is allowed only when it reduces risk for that route or removes a
known blocker in the proof ladder.

Do not try to make every platform ready at once. A platform is not "ready"
because code mentions it, packages exist in `manifest.json`, settings serialize
platform fields, or an editor-only validator exists.

## Proof Ladder

1. Windows Editor import and current Console proof.
2. Windows standalone flat player build and launch.
3. Copper Wire V0 route: boot, world, swim, copper collect, quest, craft, save,
   load, return to the same state.
4. Low-end PC/MX350 capture: frame time, GC, memory, VRAM, hitches, readability.
5. Content payload gate: DataMonolith blob, Addressables settings/groups,
   payload hashes, and load proof.
6. Linux/Steam Deck: native or Proton path, 1280x800 UI, controller glyphs,
   storage, frame pacing, native plugin behavior.
7. macOS: Metal shader compile, Apple Silicon/Intel decision, native plugin
   parity, signing/notarization path.
8. XR package resolve/import and XR Plug-in Management provider settings.
9. PCVR smoke: headset boot, input, UI, comfort, foveation on/off comparison.
10. Android ARM64 non-XR IL2CPP smoke.
11. Quest 3 standalone smoke, then Quest 2 thermal/stress pass.
12. PICO provider/package smoke after Quest path is stable.
13. Consoles only after platform-holder SDK/devkit/certification constraints
    are real.

## Per-Platform Claim Rules

| Target | Minimum claim before "ready" |
|---|---|
| Windows PC | player build, launch, Copper Wire route, profiler, GC, memory/VRAM |
| Low-end PC | same route on target-class hardware or honest proxy with VRAM/frame proof |
| High-end PC | same route stable first; visual overkill is additive only |
| Steam Deck/Linux | Linux or Proton run, input glyphs, storage, shader/native plugin proof |
| macOS | Metal import/compile, player launch on Mac hardware, native plugin parity |
| PCVR | OpenXR provider configured plus headset runtime smoke |
| Quest 2/3 | Android ARM64 IL2CPP build, install, launch, comfort, thermal, foveation proof |
| PICO | PICO SDK/provider configured and device smoke; Quest proof is not PICO proof |
| Consoles | vendor SDK/devkit/TRC path; serialized Unity fields are not readiness |

## Runtime Rules

- `GlobalQualityWeight` is continuous. Do not create binary low/high platform
  branches for gameplay truth.
- Platform differences may change presentation cost, cadence, load-shed,
  telemetry density, and visual overkill.
- Platform differences must not create different gameplay authority, save
  truth, DataVault ownership, or signal meaning without a route card.
- Native plugins need a target matrix or a managed/Unity fallback before the
  platform can be called viable.
- Shader portability needs target import/compile or device capture; D3D success
  does not prove Vulkan, Metal, Android, or console success.

## Blockers

Block platform readiness claims when any are missing:

- artifact path, command, timestamp, and target
- Unity import/Console proof
- player build and launch proof
- profiler/GC/memory proof for player-facing targets
- input/UI/storage proof for handheld or XR targets
- native plugin parity
- shader/import proof for the target graphics API
- content payload proof for route assets

## Current Status

Current status remains `PENDING VERIFICATION` until fresh artifacts exist. The
known current direction is correct: prove Windows/Copper Wire first, then climb
the ladder. Skipping to XR, Steam Deck, macOS, PICO, or consoles before the
route is proven is platform theater.

## Static No-Claim Gate

Run the static proof map before any platform-readiness discussion:

```powershell
python Tools/PlatformPortabilityProofAudit.py
```

Use stricter flags only when the corresponding claim is being made:

```powershell
python Tools/PlatformPortabilityProofAudit.py --fail-on-missing-xr-provider
python Tools/PlatformPortabilityProofAudit.py --fail-on-missing-addressables
python Tools/PlatformPortabilityProofAudit.py --fail-on-missing-data-monolith
python Tools/PlatformPortabilityProofAudit.py --fail-on-missing-build-artifact
```

Current HFI R21 static map:

- required XR packages are present in `Packages/manifest.json` and
  `Packages/packages-lock.json`;
- Android ARM64-only, IL2CPP, and target SDK `35` are serialized;
- Android/Quest scaffold flag is true;
- XR provider serialized proof is false;
- Addressables data files: `0`;
- Data Monolith payload is missing;
- build artifacts/logs: `0`;
- PICO package candidates: `0`.

Interpretation: Quest scaffold exists. Quest readiness does not.

# Platform Portability Proof Ladder

Date: 2026-05-28

Status: PENDING VERIFICATION

Owner domain: platform portability proof policy

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

4. Low-end compact PC capture: frame time, GC, memory, VRAM, hitches, readability.

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

Historical HFI R21 static map:

- required XR packages are present in `Packages/manifest.json` and

  `Packages/packages-lock.json`;

- Android ARM64-only, IL2CPP, and target SDK `35` are serialized;

- Android/Quest scaffold flag is true;

- XR provider serialized proof is false;

- Addressables data files: `0`;

- Data Monolith payload was missing in that historical map;

- build artifacts/logs: `0`;

- PICO package candidates: `0`.

Current 2026-05-28 static filesystem check:

- Data Monolith payload exists at `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`;
- payload size was `1,804,864` bytes in that check; the 2026-08-05 measurement is `7,457,664` bytes, mtime 2026-06-07;
- Agent 1504 static-source proof adds an Android NDK `AAssetManager` bridge with an FD-backed/uncompressed APK entry guard and h8bin validator pass;
- this changes static code/payload facts only, not Android device readiness.

Interpretation: Quest scaffold and static Android PAL bridge proof exist. Quest readiness does not. Data Monolith file presence plus static source proof still does not prove content-payload readiness without Unity import, boot, checksum, player, and device route proof.

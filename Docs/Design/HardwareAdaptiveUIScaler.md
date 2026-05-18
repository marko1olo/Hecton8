# Hardware Adaptive UI Scaler

Date: 2026-05-17
Status: STATIC UI SCALE PROFILE AUTHORED / PY ARTIFACTS ABSENT IN R12 CHECK / PENDING UNITY PROFILER
Owner: UX_ENGINEER  
Prompt ID: HARDWARE_ADAPTIVE_UI_BAKER  

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Evidence: STATIC_DOC / STATIC_SOURCE / PY_READABILITY_PENDING_RERUN / UNITY_PROFILER_PENDING

## Mandates
- `UI_Data_Streaming_ZeroGC_Optimization.txt`: TMP text updates must stay zero-GC; runtime text remains `SetCharArray`.
- `UI_Diegetic_Physical_Interfaces.txt`: interactive HUD is world-space, FOV-projected, not ScreenSpaceOverlay.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`: MX350 path rejects bloom and heavy UI post.
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`: chroma, blur, scanline, and wetness are visual fakes unless gameplay truth requires more.
- `QA_Evidence_Text_Filter_Audit.txt`: source and Python results are not Unity profiler proof.

## TMP-SDF Weight Matrix
Runtime owner: `Assets/_Project/Scripts/UI/WorldSpaceTMPSharpnessController.cs`.

The controller now writes TMP `_WeightNormal`, `_WeightBold`, `_FaceDilate`, and `_OutlineSoftness` on its existing throttled sharpness cadence. It does not create a new tick path and does not allocate in `LateFrameTick`.

| Bucket | Resolution | `_WeightNormal` | `_WeightBold` | `_FaceDilate` Offset | `_OutlineSoftness` Offset | Intent |
|---|---:|---:|---:|---:|---:|---|
| TOASTER_800P | 1280x800 | 0.24 | 0.82 | +0.065 | -0.055 | Heavy SDF ink, minimal blur survival. |
| LOW_900P | 1600x900 | 0.18 | 0.74 | +0.045 | -0.040 | Clear low-tier laptop output. |
| STANDARD_1080P | 1920x1080 | 0.12 | 0.66 | +0.025 | -0.020 | Default crisp HUD. |
| HIGH_1440P | 2560x1440 | 0.06 | 0.58 | +0.000 | +0.000 | Authored sharpness. |
| GOD_MODE_4K | 3840x2160 | 0.00 | 0.50 | -0.018 | +0.018 | Thinner glyphs, richer post treatment. |

Rejected: swapping font assets at runtime for each tier. It forces atlas churn and can trigger layout rebuilds. The matrix uses material scalar writes only.

## HMD FOV Layout Rules
Runtime authority is `projectionCamera.fieldOfView`; device names are bake presets, not hard dependencies.

| Device Preset | Bake Vertical FOV | Edge Cluster Shift | Insets | Critical Warning Y | Reticle Scale |
|---|---:|---:|---:|---:|---:|
| Quest 2 | 89 deg | -24 px inward | 72x48 px | -18 px | 1.06 |
| Quest 3 | 96 deg | +16 px outward | 48x36 px | -10 px | 1.00 |

Rules:
- FOV changes below 2 degrees are ignored.
- A new FOV bucket must remain stable for 2.0 seconds before layout is re-applied.
- Movement happens in `VISUAL_SYNC`; gameplay systems never query UI layout state.
- Edge buttons move horizontally first. Critical warnings move vertically toward the optical center before any font downscale.

## Contrast Profiles
TOASTER uses solid high-contrast backgrounds and disables blur/chromatic effects. GOD_MODE uses blur and chromatic aberration only as a gated post pass on the HUD RT. Per-element shaders remain capped at two texture samples.

| Profile | Background | Primary | Warning | Critical | Blur | Chroma | Per-Element Samples |
|---|---|---|---|---|---|---|---:|
| TOASTER | `#020706E6` | `#B8FFF4` | `#FFB02E` | `#FF3B1F` | off | off | 2 |
| MIDDLE | `#03110FCC` | `#98F7E8` | `#FFC45A` | `#FF4A35` | off | off | 2 |
| HIGH | `#03110F99` | `#82FFE8` | `#FFD66A` | `#FF5E47` | on | off | 2 |
| GOD_MODE | `#03110F73` | `#D8FFF8` | `#FFE08A` | `#FF6D5A` | on | on | 2 |

## Icon Pixel-Snap
Tool: `Tools/IconBaker.py`.

The baker trims transparent borders, centers the source into a square canvas, then emits `32`, `128`, and `512` pixel variants. The 32 px variant receives nearest-neighbor alpha snapping after high-quality resize so small warning icons do not dissolve under bilinear filtering.

Rejected: runtime icon scaling. It wastes bandwidth and makes low-tier icon edges unstable.

## Readability Test
Tool: `Tools/UX/ui_readability_test.py`.

The test renders `O2 LOW`, applies a Gaussian blur plus downsample/upsample pass, then checks:
- contrast delta between glyph and background,
- template correlation against the clean glyph mask,
- ink survival after degradation.

Acceptance thresholds live in `HardwareAdaptiveUIScaler.json`.

## Texture-Sample Audit
Self-audit found and reduced four over-budget UI shaders:
- `Assets/_Project/Shaders/UI/Hecton_DiegeticVisorCurvedHUD.shader`: chromatic aberration changed from 3 HUD RT samples to a one-sample channel-weight fake, plus one dirt sample.
- `Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader`: four texture inputs collapsed to `_ToolScreenTex` plus math overlays.
- `Assets/_Project/Art/Shaders/Hecton_HUD_AcousticRadarOverlay.shader`: neighbor sampling collapsed to one angular radar lookup plus math widening.
- `Assets/_Project/Art/Shaders/Hecton_DiegeticPanelUnlit.shader`: dual UI texture blend collapsed to `_BaseMap`, leaving room for the depth occlusion sample.

Current target: every owned UI element shader sampled by the audit globs has at most two fragment texture samples, including scene depth. Unity Frame Debugger proof remains pending.

## Industrial Brutalism Rationale
The UI is not soft consumer glass. It is stamped hardware: hard edges, high contrast, short labels, thick emergency glyphs, visible scanlines, and low-color industrial signal logic. Low-end hardware gets the clearest possible black-backed instruments. High-end hardware spends saved samples on controlled visor post: blur, dirt, chroma, and pressure damage, without letting individual widgets become texture-sample pigs.

Scalability:
- Low: solid panels, heavy SDF, no blur, no chroma, two samples max.
- Middle: lighter background alpha, standard SDF, math-only scanline.
- High: blur allowed after profiler proof, no extra widget samples.
- Ultra: visual overkill through HUD RT/post process, not per-button shader bloat.

## Verification Boundary
No Unity Editor, Play Mode, Frame Debugger, GCMonitor, or profiler capture was available in this pass. Runtime readiness remains `PENDING VERIFICATION` until Unity import and profiler evidence exists.

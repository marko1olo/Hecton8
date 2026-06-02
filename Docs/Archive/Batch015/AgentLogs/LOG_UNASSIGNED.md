# LOG_UNASSIGNED

Date: 2026-06-01
Status: PENDING VERIFICATION

What was wrong:
- Source was not a finished media file. It was an HTML animation embedded in a txt/markdown block.
- Source depended on browser capture libraries and included mojibake text.

What was done:
- Mandates and taste authority were read.
- Offline output path selected: MarketingAssets/02_Video/Splash_TeniGames_20260601.

Cinematic Cheats used:
- Deterministic raster fakes: scanlines, vignette, silt specks, glitch slices, center mirror seam.
- No physical simulation or Unity runtime path.

Exact microseconds saved:
- Runtime Unity frame cost: not measured; no runtime code added.
- Expected saved frame cost versus in-engine procedural DOM/UI recreation: 100% of that path because it is converted to video. Unity playback proof still PENDING VERIFICATION.

---

Date: 2026-06-01
Status: MEDIA GENERATED / UNITY PLAYBACK PENDING VERIFICATION

What was wrong:
- Source was an HTML demo, not usable MP4/GIF media.
- Source text contained mojibake for Japanese glyphs.
- Browser/CDN export route was nondeterministic.
- First mirror render pass hurt brand readability by reversing the center logo.

What was done:
- Built offline deterministic renderer: MarketingAssets/02_Video/Splash_TeniGames_20260601/render_teni_splash.py.
- Generated normal MP4: tenigames_splash_normal_1080p.mp4, 1920x1080, H.264, yuv420p, 30fps, 5.000s, 8.81 MB.
- Generated mirror MP4: tenigames_splash_mirror_1080p.mp4, 1920x1080, H.264, yuv420p, 30fps, 5.000s, 10.12 MB.
- Generated normal GIF: tenigames_splash_normal_960w.gif, 960x540, 50 frames, 5.000s, 17.07 MB.
- Generated mirror GIF: tenigames_splash_mirror_960w.gif, 960x540, 50 frames, 5.000s, 17.42 MB.
- Generated comparison sheet: tenigames_splash_comparison_contact_sheet.png.

Cinematic Cheats used:
- Static video playback asset instead of runtime procedural UI/VFX.
- Dither/grid/silt/scanline/vignette fakes for deep-sea noir.
- Center mirror applied to background/shards, not to final brand text.
- GIF downsampled to 960w/10fps; MP4 kept as game-splash candidate.

Exact microseconds saved:
- Measured runtime save: 0 us because no Unity runtime path was profiled.
- Build-time/offline render cost is irrelevant to frame budget.
- Expected frame cost avoided versus in-engine procedural reconstruction: unmeasured; Unity VideoPlayer playback proof still required.

Verification:
- py_compile passed for render_teni_splash.py.
- ffprobe passed for both MP4 files: 1920x1080, h264, yuv420p, 30/1, duration=5.000000.
- PIL ImageSequence passed for both GIF files: 960x540, 50 frames, duration_ms=5000.
- Visual comparison sheet inspected; first mirror pass rejected and fixed.

Regression model:
- CPU/GC: no runtime code added; Unity GC impact not tested.
- Memory: MP4 sizes are moderate; GIFs are preview-weight and not recommended as Unity runtime splash.
- Cadence: MP4 30fps, GIF 10fps.
- Correctness: brand text readable in final contact sheet.
- Failure mode: Unity import/playback may require VideoClip settings and platform transcode proof.

# Rationale_UNASSIGNED

Date: 2026-06-01
Domain: Presentation & UX / Offline Splash Media

Problem: Source file is an HTML demo wrapped in markdown, with CDN dependencies and mojibake text.
Solution: Recreate the animation with a deterministic offline raster renderer and export fixed media files.
Rejected Alternatives: Browser-side gifshot/html2canvas capture was rejected because CDN dependency, UI controls, timing variance, and lower export control are unfit for game splash delivery.
Scalability potential: Low uses 960w GIF and H.264 MP4 playback; Middle uses 1080p MP4; High/Ultra can use the same design with higher bitrate, denser particles, and richer post without changing splash readability.
Hardware Impact: No runtime code path. Startup playback cost depends on Unity video import/playback path later; current artifact avoids per-frame procedural UI or shader work in-game. Estimated runtime save versus live HTML/DOM capture: not a Unity path; offline generation removes all runtime DOM/JS cost.

Problem: Original text contains mojibake for Japanese glyphs.
Solution: Decode the intended Katakana/Kanji strings while preserving TENI GAMES and footer copy.
Rejected Alternatives: Keeping mojibake was rejected because it visibly breaks a splash screen. Replacing the brand with HECTON-8 was rejected because the source and user request point to the supplied animation.
Scalability potential: Text remains large, centered, and readable from low-resolution splash playback through high-resolution capture.
Hardware Impact: Correct glyphs prevent needing fallback/font substitution during final video playback. Estimated runtime gain: 0 us; quality correctness only.

Problem: User requested a normal version and a center-mirror version that assembles and disassembles.
Solution: Produce two separate variants: base logo assembly and center-mirror seam assembly/disassembly.
Rejected Alternatives: A single combined video was rejected because it prevents direct comparison and reuse.
Scalability potential: Low/Middle can use normal MP4; High/Ultra can use mirror variant when a stronger studio-ident sting is desired.
Hardware Impact: Runtime impact is one video decode stream either way; no simultaneous layered playback required. Estimated gain versus two-layer in-engine effect: pending Unity video pipeline proof.

Problem: Local ffmpeg is old and lacks palettegen.
Solution: Keep ffmpeg for H.264 MP4; generate GIFs through PIL adaptive palette at 960w/10fps/50 frames.
Rejected Alternatives: Installing or relying on a newer ffmpeg was rejected because the existing local toolchain was enough after fallback; browser GIF capture was still rejected.
Scalability potential: 10fps GIF is preview/export only. Runtime splash should use MP4. Low/Middle avoid huge GIF decode. High/Ultra can use the MP4 without changing truth.
Hardware Impact: GIF size dropped from about 26 MB to about 17 MB per variant after 10fps correction. Runtime gain in Unity: not measured because GIF is not a proposed runtime path.

Problem: First mirror pass reversed the centered logo during the hold frame.
Solution: Apply mirror assembly to background/shards before drawing text, then overlay a narrow center seam over readable logo.
Rejected Alternatives: Keeping fully mirrored text was rejected because brand readability is required for splash use.
Scalability potential: Low keeps readable static text with seam. Middle/High/Ultra get stronger motion/shard perception without changing brand read.
Hardware Impact: Same MP4 playback class. No runtime layering. Estimated runtime gain versus in-engine layered mirror effect: pending Unity video pipeline proof.

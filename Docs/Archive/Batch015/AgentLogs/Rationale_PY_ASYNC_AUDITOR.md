# PY_ASYNC_AUDITOR Rationale

Problem: Audit target is a Python bot repository outside the Unity project; HECTON mandates still require evidence-based resource lifecycle review.
Solution: Apply async lifecycle, bounded resource, execution ownership, and telemetry mandates as review criteria only. No source edits in `stomchat`.
Rejected Alternatives: No dotnet/Unity compile; irrelevant and blocked by scope. No full repo prompt/content scan; user explicitly restricted focus.
Scalability potential: Low devices need bounded media concurrency and deterministic subprocess cleanup; high devices may raise concurrency only through explicit limits, not unbounded `asyncio.gather` or orphan processes.
Hardware Impact: Expected gain cannot be measured without runtime profiling; audit targets likely tail-latency and memory/process retention, not steady-state microsecond claims.

Status: PENDING VERIFICATION.

Problem: `database.py` declares async functions but uses synchronous sqlite3 connections and `with _connect() as db`, which commits/rolls back but does not close the connection.
Solution: Recommend `contextlib.closing(_connect())` or explicit `try/finally: db.close()` around every connection, preserving SQL and call sites.
Rejected Alternatives: Full aiosqlite migration is larger than needed for immediate file-handle leak closure.
Scalability potential: Low devices avoid file descriptor exhaustion and SQLite lock amplification; high devices can later move to one owned async DB worker if throughput requires it.
Hardware Impact: Likely prevents open handle growth; microseconds saved not measured.

Problem: Media analysis has no global concurrency gate in `main.handle_new_message`, while every media event can download files, run OpenCV/Pillow work in the default executor, allocate base64 payloads, and open HTTP clients.
Solution: Recommend one module-level `asyncio.Semaphore` around the media download/extract/describe/update block and a bounded media executor for CPU work.
Rejected Alternatives: Changing bot routing or disabling media analysis.
Scalability potential: Weak devices keep one or two media jobs; high devices can raise the semaphore deliberately.
Hardware Impact: Avoids memory spikes and default executor saturation; measured proof absent.

Problem: OpenCV `VideoCapture` release is not protected by `finally` in both frame extractors.
Solution: Recommend initialize `vid_cap = None`, release in `finally` if opened.
Rejected Alternatives: Replacing with ffmpeg subprocess in this audit; subprocess lifecycle would need a kill policy.
Scalability potential: All tiers avoid native decoder handle retention on read exceptions.
Hardware Impact: Prevents native handle leak; measured proof absent.

Problem: `visionproc.main` downloads videos twice and can leave partial temp files if `iter_download` fails before `file_path` is assigned.
Solution: Remove duplicate fetch block or guard second fetch behind missing `final_img_path`; assign `file_path = temp_video_path` before opening the file.
Rejected Alternatives: Full offline processing redesign.
Scalability potential: Halves video network/disk work in the local processor.
Hardware Impact: Saves one duplicate video download and frame extract per video; exact microseconds depend on media size.

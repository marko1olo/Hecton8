# PY_ASYNC_AUDITOR Report - 2026-05-31

Scope: read-only static audit of `C:\Users\danat\Desktop\stomchat` Python runtime files named by user.

What was wrong:
- `database.py` uses synchronous sqlite3 in async functions and leaks connection handles because `sqlite3.Connection.__enter__/__exit__` does not close the connection.
- `main.handle_new_message` has no media-analysis concurrency gate. Multiple Telegram media events can concurrently download files, run OpenCV/Pillow work on the default executor, allocate base64 payloads, and open HTTP clients.
- `main.extract_first_frame` and `visionproc.extract_frame` call `VideoCapture.release()` only on the successful path after `read()`. Exceptions before release can retain native decoder resources.
- `visionproc.main` downloads videos in two duplicated blocks and can leave partial temp files if `iter_download` fails before `file_path` is assigned.
- `summarizer._generate_text_singleflight` serializes summary generation but runs blocking Gemini retry/sleep logic without a local timeout, so one long retry train can starve all summaries behind the lock.
- `asyncio.wait_for(loop.run_in_executor(...))` in media/Telegraph paths times out the awaiter, not the underlying thread. Hung blocking work can continue in the executor after caller believes it timed out.

What was done:
- Static line-referenced inspection only. No source edits in `stomchat`.
- No bot execution, no network calls, no pycache creation, no compile/test command that writes target artifacts.

Cinematic cheats used:
- Not applicable. This is Python bot runtime audit, not simulation/rendering.

Exact microseconds saved:
- Not measured. Static audit only. Likely savings: one duplicate video download/frame-extract removed per video in `visionproc`; connection close fix prevents file descriptor growth rather than steady-state microsecond savings.

Minimal fixes:
- Wrap SQLite connections with `contextlib.closing(_connect())` or explicit `db.close()`.
- Add a module-level media semaphore and bounded executor for media CPU work.
- Release OpenCV `VideoCapture` in `finally`.
- Assign temp video path before write and remove duplicate `visionproc` download block.
- Add explicit timeout/backoff budget around Gemini generation and clear summary status on cancellation.
- Treat executor timeouts as non-cancelling; use cooperative library timeouts or killable subprocesses for operations that can hang.

Status: AUDIT COMPLETE / PENDING RUNTIME VERIFICATION.

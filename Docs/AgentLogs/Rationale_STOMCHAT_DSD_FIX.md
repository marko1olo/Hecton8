# Rationale_STOMCHAT_DSD_FIX

Problem: `dsd.txt` showed 3484 Telethon connection warnings and the last useful `[Чат:]` line was `2026-05-13 08:11:06` (`MSG_160032`), while process PID 63044 was still alive.
Solution: added `health_watchdog_task()` to `main.py`. It performs a 5-minute control poll of the source chat, compares remote latest message ID to `database.get_last_msg_id()`, runs existing `sync_history()` on gaps, and disconnects after 3 watchdog failures so `start.bat` can restart the process.
Rejected Alternatives: relying only on `client.run_until_disconnected()` because the observed failure mode was "process alive, no useful updates"; hiding Telethon warnings because that would leave missed messages invisible.
Scalability potential: Low - 1 `get_messages(limit=1)` call per 300 seconds and DB max-id read. Middle/High/Ultra - can be extended to per-target lag telemetry if needed.
Hardware Impact: negligible CPU/network overhead; no measured microsecond claim.

Problem: The actual live folder was `C:\Users\danat\Desktop\stomchat`, not only the copied context folder.
Solution: applied the same patch set to the live folder and verified the running process was launched through `C:\Users\danat\Desktop\stomchat\start.bat`.
Rejected Alternatives: leaving only the copied context patched; that would not affect the real `.env`, session, DB, or running process.
Scalability potential: Low/Middle/High/Ultra identical: live runtime folder fixed; context copy kept consistent.
Hardware Impact: no runtime cost.

Problem: `bot_client.start(...)` ran at module import, before `start_bot()` logging, DB init, or watchdog creation. Network stall there recreates "alive process, no useful log."
Solution: changed `bot_client` to construct at module load but start inside `start_bot()` via `await asyncio.wait_for(..., timeout=90)`. On failure it logs and exits so `start.bat` restarts.
Rejected Alternatives: leaving synchronous `.start()` at import.
Scalability potential: Low - explicit startup failure instead of silent hang. Middle/High/Ultra - restart loop can recover without manual intervention.
Hardware Impact: no steady-state cost.

Problem: Scheduler state was memory-only and Daily/Weekly operations had no per-target timeout. Restart after `REPORT_HOUR` can resend Daily, and a stuck LLM/Telegraph call can hold scheduler indefinitely.
Solution: added `bot_state.json` persisted dates plus `SUMMARY_TASK_TIMEOUT_SECONDS = 900` around Daily/Weekly target calls.
Rejected Alternatives: filtering only by `is_summarized`; existing daily logic intentionally backfills older messages to hit `min_count`, so this would change product behavior more broadly.
Scalability potential: Low - no duplicate daily on restart after completion. Middle/High/Ultra - bounded external API stalls.
Hardware Impact: one tiny JSON read at scheduler start and atomic JSON write after report completion.

Problem: `dsd.txt` displayed mojibake in batch echo lines, while explicit UTF-8 byte reads proved source files contain valid Cyrillic.
Solution: changed `start.bat` to `chcp 65001`, `PYTHONUTF8=1`, `PYTHONIOENCODING=utf-8`, and `python -X utf8 -u main.py`.
Rejected Alternatives: mass rewriting Python strings; the source files were already valid UTF-8.
Scalability potential: Low/Middle/High/Ultra identical: readable logs and immediate unbuffered output.
Hardware Impact: no measurable CPU cost; avoids diagnostic time loss.

Problem: `dsd.txt` was dominated by Telethon reconnect warnings after the useful recovery signal was added.
Solution: changed Telethon logger level from WARNING to ERROR for future restarts; health status is now covered by `health_watchdog_task()`.
Rejected Alternatives: leaving warning spam in the primary diagnostic file; it hid the real last useful bot event.
Scalability potential: Low/Middle/High/Ultra identical: smaller logs, same bot behavior.
Hardware Impact: reduced disk/log churn; no measured microsecond claim.

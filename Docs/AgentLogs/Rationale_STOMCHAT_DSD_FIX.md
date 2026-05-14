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

Problem: User reported the content still did not arrive after the first restart. Runtime evidence showed PID 30052 alive, `bot.log` stuck after `Отправка Daily`, no `bot_state.json`, and no successful send marker.
Solution: instrumented `summarizer.py` with stage checkpoints and hard timeouts for reply lookup, Gemini, Telegraph, Telegram send, and pinning. Replaced per-message reply DB lookups with `database.get_texts_by_ids()` to avoid N SQLite connections during Daily assembly.
Rejected Alternatives: marking the day as sent on scheduler timeout; this hides lost content. Blindly increasing the 900-second outer timeout; the missing log showed the stall was before or inside the delivery chain and needed stage visibility.
Scalability potential: Low - one batch SQL query instead of dozens of SQLite connections. Middle/High/Ultra - deterministic stage logs identify the external bottleneck without manual guessing.
Hardware Impact: lower SQLite overhead during Daily assembly; no measured microsecond claim.

Problem: `scheduler_task()` marked messages summarized and advanced dates after target-loop exceptions, even if no target received content.
Solution: added `sent_any_daily` and `sent_any_weekly`; DB summarize flags and `bot_state.json` advance only after at least one successful target send.
Rejected Alternatives: treating "attempted send" as completion; that is the exact failure mode where content disappears.
Scalability potential: Low/Middle/High/Ultra identical: delivery confirmation now gates state mutation.
Hardware Impact: no meaningful runtime cost.

Problem: The live process still had old code loaded and was stuck on the failed Daily run.
Solution: stopped PID 30052. `start.bat` restarted PID 7944 with the patched code. PID 7944 synced 5 messages, generated Daily for 178 messages, created Telegraph, sent to `-1001820467444`, sent cached teaser to `-1003735006121` topic `26`, and persisted `last_daily_date=2026-05-14`.
Rejected Alternatives: waiting for PID 30052; it had already exceeded the expected delivery window and could not load the patch without restart.
Scalability potential: Low - restart recovers current run. Middle/High/Ultra - watchdog/timeouts reduce future manual intervention.
Hardware Impact: restart only; steady-state impact limited to stage logs and bounded waits.

Problem: After successful delivery, user explicitly requested no more bot restarts because they can provoke repeated sends.
Solution: stopped all restart actions and continued by observation only. Verified one live PID 7944, state date `2026-05-14`, and no duplicate Daily send after the next scheduler window.
Rejected Alternatives: applying another live restart to validate cold-start behavior; it was unnecessary and contradicted the user's constraint.
Scalability potential: Low/Middle/High/Ultra identical: use file state and passive monitoring for verification.
Hardware Impact: no runtime change.

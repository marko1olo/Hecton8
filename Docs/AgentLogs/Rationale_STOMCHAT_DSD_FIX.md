# Rationale_STOMCHAT_DSD_FIX

Problem: `dsd.txt` shows Telethon disconnect storms and mojibake; user clarified the relevant codebase is `stomchat`, not the package documentation generator.
Solution: inspect `stomchat` runtime, config, startup batch, and logging setup before any source edit.
Rejected Alternatives: direct log cleanup or editing unrelated `скрипт.py`; neither repairs the bot runtime.
Scalability potential: not a Unity frame-time system. For low-end hardware, avoid busy reconnect loops and unbounded logging. For high-end hardware, reliability still dominates throughput.
Hardware Impact: Python bot CPU impact depends on retry cadence and message handlers; no profiler data yet.

Problem: `dsd.txt` had 3484 Telethon connection warnings and the last useful `[Чат:]` line was `2026-05-13 08:11:06` (`MSG_160032`), while process PID 63044 was still alive.
Solution: added `health_watchdog_task()` to `main.py`. It performs a 5-minute control poll of the source chat, compares remote latest message ID to `database.get_last_msg_id()`, runs existing `sync_history()` on gaps, and disconnects after 3 watchdog failures so `start.bat` can restart the process.
Rejected Alternatives: relying only on `client.run_until_disconnected()` because the observed failure mode was "process alive, no useful updates"; hiding Telethon warnings because that would leave missed messages invisible; rewriting Telethon handling because existing `sync_history()` already provides the low-risk catch-up path.
Scalability potential: Low - 1 `get_messages(limit=1)` call per 300 seconds and DB max-id read. Middle - catches stalled update delivery without full restart. High/Ultra - can be extended to per-target lag telemetry if needed, but not implemented without runtime proof.
Hardware Impact: negligible CPU/network overhead on low-end hardware; no measured microsecond claim.

Problem: `dsd.txt` displayed mojibake in batch echo lines, while explicit UTF-8 byte reads proved `main.py`, `config.py`, `database.py`, `summarizer.py`, `vision.py`, and `start.bat` contain valid Cyrillic.
Solution: changed `start.bat` to `chcp 65001`, `PYTHONUTF8=1`, `PYTHONIOENCODING=utf-8`, and `python -X utf8 -u main.py`.
Rejected Alternatives: mass rewriting Python strings; the source files were already valid UTF-8. Cleaning `dsd.txt` would not fix future runs.
Scalability potential: Low/Middle/High/Ultra identical: readable logs and immediate unbuffered output.
Hardware Impact: no measurable CPU cost; avoids diagnostic time loss, not frame/runtime optimization.

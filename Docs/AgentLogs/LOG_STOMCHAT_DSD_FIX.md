# LOG_STOMCHAT_DSD_FIX

What was wrong:
`dsd.txt` showed a silent-stall pattern. The bot process continued running, but after `2026-05-13 08:11:06` it no longer logged useful source-chat messages. `start.bat` only restarts after process exit, so it could not recover a living but stale Telethon client. The log also showed 3484 Telethon connection warnings and mojibake in batch echo lines.

What was done:
Patched `stomchat/main.py` with longer Telethon retry settings and a `health_watchdog_task()` that polls the latest source-chat message every 300 seconds, compares it to the local DB max ID, calls existing `sync_history()` when messages were missed, and disconnects after 3 watchdog failures so the batch loop restarts the bot.

Patched `stomchat/start.bat` to use UTF-8 code page, `PYTHONUTF8=1`, `PYTHONIOENCODING=utf-8`, and unbuffered `python -X utf8 -u main.py`.

Cinematic Cheats used:
N/A. This is a Python Telegram bot reliability fix, not a simulation/rendering system.

Exact Microseconds saved:
No profiler measurement. Claim withheld. Static runtime cost added: one Telegram `get_messages(limit=1)` probe per 300 seconds plus one SQLite `MAX(msg_id)` read. This trades negligible periodic I/O for recovery from silent update stalls.

Verification:
`python -m py_compile main.py config.py database.py summarizer.py vision.py search_engine.py gemini_client.py` returned exit code 0.
Explicit UTF-8 source scan showed `main.py` and `start.bat` have `mojibake_markers=0`.
Runtime Telegram verification remains PENDING because this copied package does not contain `.env` or session files.


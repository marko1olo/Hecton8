# MANUAL_AUDIT Log

## 2026-05-12 Initial
What was wrong: No project documentation was supplied for the Python bot/site task, and no XML batch prompt/agent id was provided.
What was done: Created audit status and rationale files, selected relevant evidence/memory/persistence mandates, and began readonly discovery.
Cinematic Cheats used: None yet; bot reliability comes before feature theatrics.
Exact Microseconds saved: 0 measured; estimates are marked in status until diagnostics are complete.

## 2026-05-12 Reply Fix And Documentation Pass
What was wrong: Old replies in the bot were structurally broken because `cleanup_old_posts_from_db()` deleted `PostCopies` after 48 hours while keeping logical `Posts`. Site history remained, but native Telegram replies lost the per-user `message_id` map. Shadow reject also passed a global post number as if it were a Telegram `message_id`, creating a stealth leak.
What was done: Patched `common/config.py`, `common/database.py`, and `main.py`: added bounded copy retention config, capped startup copy hydration, replaced 48-hour copy purge with age+post-window retention, and routed shadow replies through normal `reply_to_post` resolution. Created `Docs/BOT_ARCHITECTURE.md`, `Docs/AUDIT_2026-05-12.md`, `Docs/OPERATIONS_RUNBOOK.md`, and `Docs/MODES_AND_ROADMAP.md`.
Cinematic Cheats used: Reliability first. Creative systems were inventoried but not expanded in the hot path. Proposed future visual cheats: deterministic mode cards, queued image generation, and site-first chess boards rendered from proven chess rules.
Exact Microseconds saved: Old 48h cleanup would have deleted about 113385 currently retained copy rows at audit time; new policy deleted 0 current rows under existing DB state. RAM savings are from capping startup copy hydration to 3300 posts instead of hydrating the full retained copy window; expected savings are hundreds of MB on active boards, exact value requires post-restart measurement.

## 2026-05-12 Live Restart Verification
What was wrong: The source patch was not active in the live bot process until restart; old process `21132` was still running with about 1009 MB private memory before restart.
What was done: Stopped bot Python PIDs `21132` and `15108` while leaving `start_bot.bat` watchdog alive. Watchdog spawned new bot Python PIDs `39360` and `12080`. Post-restart memory sample for active child `12080`: about 462 MB working set and 672 MB private memory. Readonly SQLite `quick_check` returned `ok`; DB had `Posts=150003`, `PostCopies=1081946`, distinct copy posts `1644`, copy span `373298..375047`.
Cinematic Cheats used: None in runtime. This was a reliability activation step, not feature work.
Exact Microseconds saved: Private memory dropped by roughly 337 MB in the sampled active child process after restart (`1009 MB -> 672 MB`). This is a snapshot, not a proven long-term leak fix; long-term slope still needs rotating logs/telemetry.

## 2026-05-12 Memory Telemetry And Cache Reduction
What was wrong: The bot had no durable runtime memory log. First telemetry proved startup bloat: `messages_storage` loaded about 25k heavy post records and `message_to_post` held about 1.12M hot copy entries. That is not just a leak; it is an oversized hot cache.
What was done: Added `logs/bot_runtime.log` rotating telemetry, `runtime_telemetry_task`, richer `/debug_memory`, richer `/queues`, and emergency memory snapshots. Added `BOT_POST_CACHE_LIMIT=3300`, changed `load_state_from_db()` to load heavy post content by RAM limit, preserved thread lifecycle via lightweight thread post-number lists, and reduced `BOT_COPY_CACHE_POST_LIMIT` default to `1000`. Restarted through watchdog; final live child PID `39032`.
Cinematic Cheats used: Kept the expensive historical truth in SQLite and made Python RAM a small recent-window illusion. Old replies still resolve through DB, while only the hot layer stays in memory.
Exact Microseconds saved: No per-request latency benchmark yet. Live memory evidence: `private_mb` dropped from about `724` to `552`, `messages_storage` from `25015` to `3303`, and `message_to_post` from `1118939` to `630988`. Queue total was `0`; SQLite `quick_check=ok`.

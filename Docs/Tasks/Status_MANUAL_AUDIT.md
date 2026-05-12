# MANUAL_AUDIT Status

Scope: `C:\Users\danat\Desktop\dvachbot` and `C:\Users\danat\Desktop\stomchat`.
Prompt source: manual user request, no `<AGENT_PROMPT>` XML block provided.
Domain: Python Telegram bot and site audit, documentation, reply-chain bug, memory and queue risk review.

Relevant mandates selected before code/document edits:
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` mapped to Python hot-path allocation discipline and leak triage.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` mapped to CPU queue/backpressure constraints.
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt` mapped to SQLite persistence, backups, WAL, and data integrity.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt` mapped to process logs, memory black-box, and failure evidence.
- `QA_Evidence_Text_Filter_Audit.txt` mapped to documentation from code/log evidence, no invented status.
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt` mapped narrowly to decoupled service ownership and avoiding cross-system hard dependencies.

## Loop 1: Discovery
- [x] Identify running bot/site processes | DOD: process table evidence | Rejected: guessing from batch names | Estimate: 3000 us
- [x] Read project authority/rules and domain note | DOD: direct file reads | Rejected: applying Unity-only details blindly to Python | Estimate: 4000 us
- [x] Inventory Python entry points and DB schema | DOD: `rg`/SQLite readonly evidence found bot `main.py`, site `site_tgach.main`, shared `common/database.py`, `Posts`, `PostCopies`, `Users`, `BroadcastQueue` | Rejected: importing live app modules first | Estimate: 18000 us
- [x] Inspect logs for reply, queue, memory, DB errors | DOD: process table + available logs; bot console file stale, site/stomchat logs sampled; live memory measured via process table | Rejected: anecdotal diagnosis | Estimate: 22000 us
- [x] Trace reply handling implementation | DOD: `handle_message()` -> `get_post_info_by_copy()` -> `process_new_post()` -> `send_message_to_users()` -> `PostCopies`; cleanup proved as failure source | Rejected: patching blind | Estimate: 26000 us

## Loop 2: Core Fix/Docs Prep
- [x] Trace outbound message mapping storage | DOD: `add_post_copies()` persists `(post_num, recipient_id, message_id)`, startup hydrates `post_to_messages/message_to_post`, cleanup deleted old truth | Rejected: memory-only mapping | Estimate: 24000 us
- [x] Review admin/user stats and shadow mute paths | DOD: command inventory found `/admin`, `/stats`, `/queues`, `/debug_memory`, `/whois`, mute/shadow/media restrictions; shadow reply leak patched | Rejected: UI claims without code proof | Estimate: 31000 us
- [x] Review mode modules and creative surface | DOD: inspected mode dispatcher and mode modules: anime, zaputin, ukrainian, polish, warhammer, imperial, gopnik, schizo, mode visuals | Rejected: adding modes before stability | Estimate: 37000 us
- [x] Draft architecture documentation | DOD: created `Docs/BOT_ARCHITECTURE.md`, `Docs/AUDIT_2026-05-12.md`, `Docs/OPERATIONS_RUNBOOK.md`, `Docs/MODES_AND_ROADMAP.md` | Rejected: chat-only report | Estimate: 11000000 us
- [x] Compile/import-check Python after edits | DOD: `python -m py_compile main.py common\database.py common\config.py` passed | Rejected: runtime restart as first test | Estimate: 4300000 us

## Loop 3: Verification
- [x] Run readonly DB diagnostics | DOD: `Posts=149860`, `PostCopies=1170072`, distinct posts with copies `1743`, DB `408.4 MB`, WAL `17.5 MB`, old 48h policy would delete `113385` current copy rows | Rejected: mutating production DB | Estimate: 6400000 us
- [x] Run static leak/queue diagnostics | DOD: identified unbounded bot board queues, fanout bottleneck, RAM maps, site in-memory cache surfaces, and logging gap | Rejected: relying on GC mythology | Estimate: 42000 us
- [x] Apply minimal code fix if isolated | DOD: bounded `PostCopies` retention, bounded startup hydration, shadow reply path corrected, compile green | Rejected: broad rewrite | Estimate: 9000000 us
- [x] Write final project docs | DOD: four markdown documents created under `C:\Users\danat\Desktop\dvachbot\Docs` | Rejected: undocumented tribal knowledge | Estimate: 26000000 us
- [x] Restart bot to activate fix | DOD: stopped old `python main.py` PIDs `15108/21132`; watchdog `start_bot.bat` spawned new `39360/12080`; post-restart `py_compile` already green and DB quick_check ok | Rejected: assuming patched code affects live process | Estimate: 30000000 us
- [x] Append final agent log | DOD: `LOG_MANUAL_AUDIT.md` appended with fix/docs/restart evidence | Rejected: chat-only status | Estimate: 5000 us

## Loop 4: Restart Verification
- [x] Confirm new bot process stayed alive after restart | DOD: active child PID `12080` sampled after restart, watchdog parent `44540` still alive | Rejected: trusting immediate spawn only | Estimate: 20000000 us
- [x] Measure post-restart memory | DOD: active child around `462 MB` working set / `672 MB` private memory vs old `~1009 MB` private sample | Rejected: claiming long-term leak fixed from one snapshot | Estimate: 3000 us
- [x] Re-check SQLite after restart | DOD: readonly `PRAGMA quick_check=ok`, `Posts=150003`, `PostCopies=1081946`, copy span `373298..375047` | Rejected: mutating production DB | Estimate: 11000000 us

## Loop 5: Self-Review
- [x] Re-read changed code chunks | DOD: verified capped copy hydration, retention query, and shadow reject reply path in source after restart | Rejected: assuming patch intent equals actual file content | Estimate: 18000 us
- [x] Re-read generated docs for stale status | DOD: corrected audit note from "not live until restart" to restarted state; added image-option reference links | Rejected: stale docs after runtime change | Estimate: 12000 us
- [x] Final compile remains green | DOD: last `python -m py_compile main.py common\database.py common\config.py` passed | Rejected: restart without syntax check | Estimate: 6900000 us

## Loop 6: Memory Telemetry
- [x] Add durable runtime telemetry | DOD: `logs/bot_runtime.log` rotating logger, `runtime_telemetry_task`, runtime snapshot helper, background task exception logging | Rejected: relying on console-only prints and `gc.collect()` mythology | Estimate: 12000000 us
- [x] Improve admin memory/queue commands | DOD: `/debug_memory` now includes process/queue/map/DB snapshot and starts `tracemalloc` on demand; `/queues` shows total/top RAM queues and RSS/private memory | Rejected: top-10 tracemalloc only | Estimate: 5000000 us
- [x] Restart bot with telemetry code | DOD: stopped dvachbot PIDs `39360/12080`, kept `start_bot.bat` PID `44540`, watchdog spawned `33904/69808`; `stomchat` PID `15656` was not touched | Rejected: killing every `python main.py` blindly | Estimate: 12000000 us
- [x] Update docs for telemetry and memory status | DOD: updated `BOT_ARCHITECTURE.md`, `AUDIT_2026-05-12.md`, `OPERATIONS_RUNBOOK.md` with runtime log, commands, and honest leak status | Rejected: claiming multi-day leak fixed from a short sample | Estimate: 8000000 us

## Loop 7: RAM Cache Reduction
- [x] Separate SQLite retention from RAM post cache | DOD: added `BOT_POST_CACHE_LIMIT=3300`; `load_state_from_db()` now loads heavy `messages_storage` by RAM cache limit, not `DB_POST_LIMIT=25000` | Rejected: waiting 30 minutes for `auto_memory_cleaner()` to purge startup bloat | Estimate: 9000000 us
- [x] Preserve thread post counts without heavy content cache | DOD: active thread `posts` lists are loaded as lightweight post numbers from SQLite after content cache load | Rejected: shrinking `messages_storage` and silently breaking thread lifecycle counts | Estimate: 6000000 us
- [x] Reduce hot Telegram copy cache | DOD: `BOT_COPY_CACHE_POST_LIMIT` default changed from `3300` to `1000`; DB `PostCopies` remains the fallback for older replies | Rejected: keeping 1.1M `(recipient,message_id)` dict entries hot by default | Estimate: 3000000 us
- [x] Verify live memory after restart | DOD: watchdog spawned `32776/39032`; telemetry line shows `private_mb=551.86`, `messages_storage=3303`, `post_to_messages=965`, `message_to_post=630988`, queues total `0`, DB `quick_check=ok` | Rejected: compile-only verification | Estimate: 100000000 us

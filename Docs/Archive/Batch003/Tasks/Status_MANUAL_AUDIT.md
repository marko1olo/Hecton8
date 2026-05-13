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

## Loop 8: Priority Delivery
- [x] Measure weekly-active population from readonly DB | DOD: last 7 days visible authors: total `113` distinct users, board entries later refreshed as `138`, `/b/=98`, `/sex/=19` | Rejected: guessing the priority list size | Estimate: 4000000 us
- [x] Add DB helper and refresh task | DOD: `get_weekly_active_users()`, `weekly_active_refresh_task()`, env knobs `BOT_PRIORITY_DELIVERY`, `BOT_WEEKLY_ACTIVE_DAYS`, `BOT_WEEKLY_ACTIVE_REFRESH_SEC` | Rejected: new persistent table before proving query cost | Estimate: 7000000 us
- [x] Reorder fanout without dropping passive users | DOD: `send_message_to_users()` now orders recipients `weekly-active -> passive`, logs `prio X/Y`, and marks new non-shadow bot authors hot immediately | Rejected: separate destructive queue rewrite on live bot | Estimate: 6000000 us
- [x] Restart and verify live priority telemetry | DOD: watchdog chain `28036/11260`; `weekly_active_refresh total=138`; runtime snapshot has `delivery_priority.enabled=true`, `private_mb=578.8`, `message_to_post=623416`, DB `quick_check=ok` | Rejected: compile-only verification | Estimate: 120000000 us

## Loop 9: Delivery Ops Metrics
- [x] Add bounded delivery metric buffer | DOD: `delivery_metrics` stores last 100 completed verbose fanouts per board | Rejected: unbounded delivery history in RAM | Estimate: 2500000 us
- [x] Log completed fanout results | DOD: `delivery_result` runtime log entries include post, type, recipients, priority/passive split, success/errors/retries, seconds | Rejected: stdout-only timing | Estimate: 3000000 us
- [x] Expose last delivery in `/queues` | DOD: admin queue command now shows last completed delivery and avg/max from process-local buffer | Rejected: adding a new command before improving existing ops screen | Estimate: 2500000 us
- [x] Restart and verify metric-capable process | DOD: watchdog chain `54940/53852`; runtime snapshot has `delivery_priority.enabled=true`, `delivery={}` until a post completes after restart, `private_mb=559.06`, DB `quick_check=ok` | Rejected: pretending an empty fresh buffer is a failure | Estimate: 100000000 us

## Loop 10: Reverse Map Leak Guard
- [x] Identify orphan reverse-map leak path | DOD: old reply fallback can cache `(chat_id,message_id)->post_num` in `message_to_post` without a matching `post_to_messages` owner | Rejected: blaming Python GC while references remain reachable | Estimate: 6000000 us
- [x] Patch cleaner to prune orphan reverse entries | DOD: `auto_memory_cleaner()` removes `message_to_post` entries whose post is outside current `messages_storage` and `post_to_messages` hot windows | Rejected: deleting durable `PostCopies` or shrinking DB history | Estimate: 4000000 us
- [x] Remove priority startup blind spot | DOD: `weekly_active_refresh_task()` no longer sleeps 20 seconds before first DB refresh | Rejected: accepting first-after-restart fanouts with `priority_recipients=0` | Estimate: 1200000 us
- [x] Restart and verify live runtime | DOD: watchdog chain `66668/59476`; first new delivery `#375197` had `priority_recipients=92`, runtime snapshot `private_mb=579.18`, `message_to_post=622762`, DB `quick_check=ok` | Rejected: source-only leak claim | Estimate: 140000000 us

## Loop 11: Shadow Stealth Audit
- [x] Audit shadow reject call sites | DOD: `rg process_shadow_reject` and source reads confirmed every audited call now passes `stream`; multi-reply path converted from positional/default calls to explicit keyword calls | Rejected: assuming default `stream='ru'` is harmless on multilingual boards | Estimate: 8000000 us
- [x] Add per-user fake post counters | DOD: `shadow_fake_post_counters[(board_id,user_id)]` prevents duplicate fake post numbers in shadow bursts and is visible in runtime snapshots | Rejected: full per-recipient post-number virtualization as too large for live hot patch | Estimate: 5000000 us
- [x] Bound new shadow cache | DOD: `shadow_fake_post_counters` is included in emergency global cache cleanup and `maps.shadow_fake_post_counters` telemetry | Rejected: unbounded stealth helper state | Estimate: 1500000 us
- [x] Restart and verify shadow telemetry | DOD: watchdog chain `59116/18352`; runtime snapshot shows `shadow_fake_post_counters=7`, queues `0`, priority enabled, compile green | Rejected: source-only verification | Estimate: 180000000 us

## Loop 12: Queue Lag Evidence
- [x] Add completed fanout age metric | DOD: `delivery_result` includes `post_age_sec` from post creation to completed fanout; `/queues` last-delivery text includes age when present | Rejected: queue-size-only admin stats | Estimate: 3500000 us
- [x] Verify live post-age logging | DOD: runtime log contains examples like `/b/ #375250 seconds=9.073 post_age_sec=22.602` and snapshot has `avg_age_sec/max_age_sec` | Rejected: waiting for anecdotal lag report | Estimate: 60000000 us
- [x] Update project docs | DOD: updated `AUDIT_2026-05-12.md`, `BOT_ARCHITECTURE.md`, `OPERATIONS_RUNBOOK.md`, and `MODES_AND_ROADMAP.md` for shadow and queue-age facts | Rejected: chat-only report | Estimate: 9000000 us

## Loop 13: Memory Limit Guard
- [x] Inspect live process chain | DOD: `start_bot.bat` launches venv Python parent, which spawns the real child interpreter; parent private memory is under 1 MB, so this is not the multi-GB leak | Rejected: treating two `main.py` processes as duplicate bots without checking memory/parentage | Estimate: 4000000 us
- [x] Patch emergency memory threshold source | DOD: `memory_restarter()` now checks `max(RSS, private/USS)` instead of RSS only | Rejected: relying on working set while user reports private/process memory blowups | Estimate: 2500000 us
- [x] Restart and verify live guard | DOD: watchdog chain `68564/66240`; child private memory sample `560.50 MB`, later runtime snapshot `private_mb=589.73`, `message_to_post=623452`, `queues.total=0`, DB `quick_check=ok`, compile green | Rejected: waiting for 3.2 GB incident to discover guard mismatch | Estimate: 60000000 us

## Loop 14: Image Generation Option Check
- [x] Verify current provider docs | DOD: checked Pollinations API docs, Cloudflare Workers AI pricing, Hugging Face Inference Providers pricing, and Google Gemini image generation docs | Rejected: relying on stale "free unlimited" assumptions | Estimate: 120000000 us
- [x] Update mode roadmap | DOD: `MODES_AND_ROADMAP.md` now treats image generation as queued/cooldown-budgeted work and lists provider caveats | Rejected: putting image generation in Telegram hot path | Estimate: 2500000 us

## Loop 15: Old-Reply Quote Coverage
- [x] Unify fallback quote extraction | DOD: added `build_quick_quote_info()` and content scanner for text/caption/media/files/direct file IDs/image bytes/URLs/polls | Rejected: keeping fragmented per-handler quote logic | Estimate: 4500000 us
- [x] Cover more Telegram content types | DOD: old-reply quick quotes now cover media groups, photo/video/GIF/document/audio/voice/sticker/video_note and multi-reply flows | Rejected: text-only fallback for old replies | Estimate: 3500000 us
- [x] Restart and verify live bot | DOD: watchdog chain `17116/44180` was active after quote patch; process sample `private_mb=566.01`, DB `quick_check=ok`, compile green | Rejected: source-only reply claim | Estimate: 60000000 us

## Loop 16: Live Queue Age Telemetry
- [x] Add queue enqueue timestamps | DOD: all `message_queues[...] .put()` producer paths now go through `enqueue_board_message()` and stamp `enqueued_at` | Rejected: only completed fanout age, which misses live backlog | Estimate: 4000000 us
- [x] Track current in-flight fanout | DOD: `current_deliveries[board_id]` records post, start time, queue wait, recipient count, and thread id while worker sends | Rejected: queue size without current worker state | Estimate: 3500000 us
- [x] Expose live queue age | DOD: `runtime_snapshot` includes `queues.age_by_board`, `queues.oldest`, and `queues.in_flight`; `/queues` shows `Live age/current` | Rejected: adding a separate admin command before improving incident screen | Estimate: 3000000 us
- [x] Restart and verify live metrics | DOD: watchdog chain `67092/56092`, child `private_mb=609.49`; `delivery_result` examples include `queue_wait_sec=0.005/10.395/18.585`, `queue_total_sec=10.244/18.515/26.168`; runtime snapshot showed `queues.in_flight.b=#375314 run=9.6s age=9.6s`; DB `quick_check=ok` | Rejected: waiting for a 30-minute lag incident to discover missing fields | Estimate: 90000000 us

## Loop 17: Reply Coverage Admin Stats
- [x] Add DB coverage helper | DOD: `get_reply_coverage_stats()` reports total copy rows, distinct covered posts, min/max covered post, latest post gap, and per-board spans | Rejected: manual SQL during every incident | Estimate: 4500000 us
- [x] Cache coverage in background task | DOD: `reply_coverage_refresh_task()` updates process cache and logs `reply_coverage`; measured raw query about `0.486s`, so it is not run synchronously in every runtime snapshot | Rejected: expensive COUNT(DISTINCT) in the hot telemetry path | Estimate: 5000000 us
- [x] Expose coverage to admins | DOD: `runtime_snapshot.reply_coverage` and `/queues Reply copies` show all-board and current-board copy coverage | Rejected: adding another hidden admin command | Estimate: 2000000 us
- [x] Restart and verify live coverage | DOD: watchdog chain `37248/17352`; `reply_coverage total_copies=1231051 copy_posts=1889 span=373298..375320 gap=0`, `/b/=1660`; runtime snapshot includes `reply_coverage`, DB `quick_check=ok`, compile green | Rejected: source-only statistics claim | Estimate: 80000000 us

## Loop 18: Site Memory Visibility
- [x] Measure live site memory | DOD: pre-restart `uvicorn` PID `1168` sampled at `private_mb=1132.68`, much higher than bot child | Rejected: assuming only bot has memory pressure | Estimate: 2500000 us
- [x] Add admin site runtime stats | DOD: `/api/admin/stats` and `/api/admin/system_health` now include process RSS/private/VMS, thread/open-file counts, broadcast queue size, active websocket keys, captcha/session/cache/tracker sizes | Rejected: chat-only site memory notes | Estimate: 4000000 us
- [x] Restart and verify site | DOD: `start_site.bat` watchdog spawned `uvicorn` PID `68716`; root returned HTTP `200`, port `8000` listening, post-restart `private_mb=604.7`, compile green | Rejected: leaving patched admin API inactive | Estimate: 60000000 us

## Loop 19: Hot Copy Cache Reduction
- [x] Reduce default hot Telegram copy cache | DOD: `BOT_COPY_CACHE_POST_LIMIT` default changed `1000 -> 700`; durable `PostCopies` retention remains unchanged and DB fallback is indexed | Rejected: shrinking SQLite history or breaking old replies | Estimate: 1200000 us
- [x] Avoid single-message list objects for new copies | DOD: author copy and fanout copy storage now keep an `int` for one Telegram message and a `list` only for multi-message/album copies | Rejected: keeping hundreds of thousands of one-element Python lists | Estimate: 2500000 us
- [x] Restart and verify bot memory | DOD: watchdog chain `16232/51612`; runtime snapshot `private_mb=523.71`, `post_to_messages=666`, `message_to_post=417771`, `messages_storage=3300`, queues `0`, DB `quick_check=ok` | Rejected: claiming savings from config without live process evidence | Estimate: 90000000 us

## Loop 20: Site Cache Cleanup Guard
- [x] Confirm FastAPI cache retention behavior | DOD: inspected `InMemoryBackend`: expired entries are deleted only when the same key is read; rare expired keys can remain in class-level `_store` | Rejected: blaming Python GC for reachable cache entries | Estimate: 3000000 us
- [x] Add site periodic cleanup | DOD: `site_cache_cleanup_task()` removes expired FastAPI cache keys, caps cache key count, prunes old `THREAD_VERSIONS`, stale `URL_STATUS_CACHE`, and old `POST_RATE_LIMITER` timestamps | Rejected: clearing all site cache blindly or adding always-on heavy telemetry | Estimate: 6000000 us
- [x] Expose FastAPI cache stats | DOD: site runtime snapshot includes `fastapi_cache.keys`, `expired_keys`, and approximate `data_mb` | Rejected: cache cleanup with no observability | Estimate: 2500000 us
- [x] Restart and verify site | DOD: `start_site.bat` watchdog spawned `uvicorn` PID `55360`; root returned HTTP `200`; post-restart site `private_mb=643.41`, compile green | Rejected: leaving cache cleanup inactive | Estimate: 70000000 us

## Loop 21: Cleaner Limit Consistency
- [x] Compare config limits against periodic cleaner | DOD: found `auto_memory_cleaner()` still used hardcoded `REAL_RAM_LIMIT=2000` while startup uses `BOT_POST_CACHE_LIMIT=3300` | Rejected: accepting drift between restart state and 30-minute cleanup state | Estimate: 1200000 us
- [x] Patch cleaner to use unified hot post limit | DOD: changed `REAL_RAM_LIMIT = MAX_MESSAGES_IN_MEMORY`, so RAM post cleanup follows `BOT_POST_CACHE_LIMIT` | Rejected: adding another env knob or shrinking DB history | Estimate: 800000 us
- [x] Update project documentation | DOD: updated audit/runbook/architecture/roadmap for `BOT_COPY_CACHE_POST_LIMIT=700`, int single-copy storage sample, and unified cleaner limit | Rejected: stale docs saying 1000-copy hot cache | Estimate: 2500000 us

## Loop 22: Site Security Map Cleanup
- [x] Audit site request/security maps | DOD: found `REQUEST_FLOOD_TRACKER`, expired `IP_BAN_LIST`, expired `IP_TROLL_CONFIG`, `BOT_VIOLATIONS`, and `KNOWN_IPS` had partial/event-driven cleanup only | Rejected: waiting for 10k-map emergency clears | Estimate: 3500000 us
- [x] Add periodic stale security-map cleanup | DOD: `cleanup_site_runtime_maps_once()` now trims stale flood keys, expired bans/troll configs, and cap-clears security maps by `SITE_SECURITY_MAP_MAX_KEYS` | Rejected: clearing all maps every interval and losing useful abuse state | Estimate: 5000000 us
- [x] Expose site security-map counts | DOD: site runtime snapshot now includes request flood tracker, known IPs, bot violations, bans, and troll config counts | Rejected: hidden cleanup with no cardinality telemetry | Estimate: 1500000 us
- [x] Restart and verify site | DOD: watchdog spawned site PID `21252`; root returned HTTP `200`; post-start site private memory sampled about `650.88 MB`; compile green | Rejected: leaving site patch inactive | Estimate: 45000000 us

## Loop 23: Bot Small Map And Recipient Hygiene
- [x] Audit global bot cooldown/rate maps | DOD: found poll/thread/reaction/image/author-notify maps were either hidden from telemetry or only emergency-cleared | Rejected: treating small maps as harmless forever | Estimate: 3000000 us
- [x] Add TTL cleanup for small maps | DOD: `auto_memory_cleaner()` now prunes stale hourly image counters, thread action cooldowns, reaction ratelimits, poll cooldowns, and author reaction notify trackers | Rejected: `gc.collect()` or only `>10000` emergency clears | Estimate: 3500000 us
- [x] Expose small-map cardinalities | DOD: runtime snapshots now include user thread actions, reaction ratelimit, poll cooldowns, image counters, author notify tracker, and network retry state | Rejected: memory leak hunt without named counters | Estimate: 1500000 us
- [x] Fix worker recipient telemetry/filtering | DOD: message worker now filters `uid > 0` before `current_deliveries` and send; fixed misleading in-flight sample `3986` vs delivered `626` | Rejected: relying on later filtering inside `send_message_to_users()` | Estimate: 1200000 us
- [x] Restart and verify bot | DOD: watchdog spawned `70736/51640`; runtime snapshot `private_mb=551.15`, `messages_storage=3300`, `post_to_messages=666`, `message_to_post=409328`, small-map counters `0`, queues `0`, compile green | Rejected: source-only telemetry patch | Estimate: 90000000 us

## Loop 24: Orphan ProcessPool Memory
- [x] Audit system Python processes | DOD: found orphaned `multiprocessing.spawn` PIDs `37776/38024` from dead parent `1168`, each about `515 MB` private, not current bot/site/stomchat | Rejected: blaming bot heap for OS-level orphan workers | Estimate: 3000000 us
- [x] Remove orphan worker processes | DOD: stopped only PIDs `37776/38024`; remaining Python process list contains current bot `70736/51640`, site `38420`, stomchat `15656`, Unity MCP `72292/68200` | Rejected: killing all `python main.py` or unrelated MCP/stomchat processes | Estimate: 5000000 us
- [x] Remove site ProcessPool source | DOD: changed `site_tgach/image_processing.py` grimdark/thumbnail workers from `ProcessPoolExecutor` to bounded `ThreadPoolExecutor`, added `shutdown_image_executors()` and lifespan shutdown hook | Rejected: keeping 500MB child interpreters for occasional PIL work | Estimate: 6000000 us
- [x] Restart and verify site | DOD: watchdog spawned site PID `38420`; root returned HTTP `200`; no `spawn_main` worker children found after restart; site private memory about `577.62 MB` after warmup; compile green | Rejected: source-only process-pool fix | Estimate: 60000000 us

## Loop 25: Board Map Leak Edges
- [x] Add nested board-map telemetry | DOD: `runtime_snapshot.board_maps` now exposes cooldown/spam/reaction/image/thread-lock/anime daily tracker cardinalities; verified live snapshot PID `32160` contains `board_maps` | Rejected: hidden nested maps under one vague `board_totals` number | Estimate: 2500000 us
- [x] Prune small stale bot maps | DOD: `auto_memory_cleaner()` now prunes `anime_daily_tracker`, stale `image_spam_tracker` timestamps, old `unknown_command_tracker` entries, and orphan thread locks | Rejected: more frequent `gc.collect()` or waiting for emergency `>10000` clears | Estimate: 3500000 us
- [x] Fix callback image spam stale-window bug | DOD: quick-menu hentai/loli callback now removes expired `image_spam_tracker['b']` timestamps before checking the `/b/` limit | Rejected: letting expired timestamps block image buttons or accumulate until another command path cleans them | Estimate: 800000 us
- [x] Restart and verify bot | DOD: watchdog chain `65632/32160`; runtime snapshot `private_mb=553.23`, queues `0`, `board_maps.image_spam_items=0`, `/b/ #375378` delivered to `626` in `9.759s`, DB `quick_check=ok` | Rejected: compile-only cleanup claim | Estimate: 90000000 us
- [x] Patch unused Dubsite process-pool landmine | DOD: old `Dubsite_tgach/image_processing.py` no longer imports `ProcessPoolExecutor`; compile green and `rg ProcessPoolExecutor` returns no active matches under site/Dubsite/stomchat | Rejected: leaving duplicate code that can recreate orphan workers if started later | Estimate: 2000000 us

## Loop 26: Stomchat Memory Baseline
- [x] Audit stomchat process/logs | DOD: live stomchat PID `15656` was started 2026-05-03, about `451.88 MB` private; `bot.log` was `18.84 MB` plain FileHandler; DBs readonly `quick_check=ok` | Rejected: assuming dvachbot is the only memory source | Estimate: 4000000 us
- [x] Add stomchat rotating log and memory heartbeat | DOD: `main.py` now replaces the old file handler with `RotatingFileHandler(5MB, backupCount=5)` and logs `runtime_memory` via optional `psutil` every 900s | Rejected: unbounded log growth and no process-memory time series | Estimate: 2500000 us
- [x] Restart and verify stomchat | DOD: watchdog `10676` spawned PID `26736`; post-restart sample `private_mb=413.64`; `bot.log.1` holds old `18.84 MB`, new `bot.log` has `runtime_memory pid=26736 rss_mb=211.73 private_mb=412.88`; DBs quick_check ok | Rejected: source-only telemetry claim | Estimate: 50000000 us

## Loop 27: User Mode Backlog
- [x] Inspect current mode wiring | DOD: confirmed modes are explicit flags/lists in `main.py`, JSON settings in `Boards.settings`, reset on startup in `common/database.py`, and public help text in `help_text.py` | Rejected: broad registry refactor during live bot reliability work | Estimate: 2500000 us
- [x] Implement four cheap modes | DOD: added `new_modes.py` and commands `/matrix`, `/america`, `/holiday`, `/oldweb`; transforms are text-only and use precompiled regex patterns | Rejected: live image generation or external API calls inside message delivery | Estimate: 7000000 us
- [x] Wire modes through bot state | DOD: added new flags to `MODE_FLAGS`, `board_data`, DB load defaults, headers, transform dispatch, end phrases, auto-disable, `/stop`, and help text | Rejected: SQLite schema migration for temporary mode state | Estimate: 4500000 us
- [x] Document content decision | DOD: `MODES_AND_ROADMAP.md`, `BOT_ARCHITECTURE.md`, and audit doc now document implemented modes and why direct "Jewish mode" is held for manual editorial design | Rejected: protected-class caricature generator | Estimate: 2500000 us
- [x] Compile new mode code | DOD: `python -m py_compile main.py common\database.py help_text.py new_modes.py` exits 0 | Rejected: restarting live bot after uncompiled mode patch | Estimate: 66700000 us
- [x] Restart and verify bot | DOD: watchdog chain `43848/540`, runtime snapshot PID `540` reports `private_mb=551.47`, queues `0`, `messages_storage=3300`, `post_to_messages=666`, `message_to_post=409455`; DB `quick_check=ok`, `Posts=150337`, `PostCopies=1255801` | Rejected: source-only mode deployment | Estimate: 65000000 us
- [x] Microbench text transforms | DOD: 2000-call local sample on medium repeated text: `matrix=331.13us`, `america=419.69us`, `holiday=264.03us`, `oldweb=228.25us` per transform | Rejected: unmeasured claim that regex modes are cheap | Estimate: 4000000 us

## Loop 28: Hot Copy Cache Tightening
- [x] Re-evaluate `message_to_post` RAM pressure | DOD: post-mode restart still had `message_to_post=409455` and `post_to_messages=666` at `private_mb=551.47`; confirmed DB fallback uses indexed `PostCopies` primary key `(recipient_id, message_id)` | Rejected: treating the hot reverse map as an unavoidable leak | Estimate: 3000000 us
- [x] Lower hot copy window | DOD: `BOT_COPY_CACHE_POST_LIMIT` default changed `700 -> 400`; durable `PostCopies` retention unchanged | Rejected: deleting DB copy rows or removing native reply durability | Estimate: 700000 us
- [x] Compile and restart bot | DOD: `python -m py_compile common\config.py main.py common\database.py` exits 0; watchdog chain `39996/20448` active | Rejected: config-only claim without process restart | Estimate: 90000000 us
- [x] Verify memory reduction | DOD: runtime snapshot PID `20448` reports `private_mb=485.86`, queues `0`, `messages_storage=3300`, `post_to_messages=368`, `message_to_post=212316`, `reply_coverage.gap_from_latest=0`; DB `quick_check=ok`, `Posts=150338`, `PostCopies=1256428` | Rejected: guessing Python dict savings | Estimate: 120000000 us

## Loop 29: Mode Creativity Expansion
- [x] Expand new mode dictionaries | DOD: `new_modes.py` now has larger profile dictionaries: matrix `38`, america `37`, holiday `33`, oldweb `35` replacements; each expanded mode has `8` prefixes, `8` suffixes, `10` injections, and `replace_chance=0.62` | Rejected: adding image/API generation to the hot message path | Estimate: 4500000 us
- [x] Add safe Jewish/Talmudic mode | DOD: added `/jewish` plus `/talmud`, `/odessa`, `/shabbat`, `/rabbi`, `/evrei`, `/evrey`; wired `jewish_mode` through imports, `MODE_FLAGS`, `board_data`, DB load defaults, headers, transform dispatch, activation, end phrases, and help text | Rejected: protected-class stereotype generator | Estimate: 3500000 us
- [x] Update mode documentation | DOD: updated `MODES_AND_ROADMAP.md`, `AUDIT_2026-05-12.md`, and `BOT_ARCHITECTURE.md` so they no longer falsely say Jewish mode is unimplemented; documented guardrails | Rejected: stale docs after live behavior change | Estimate: 2200000 us
- [x] Compile and microbench | DOD: `python -m py_compile main.py common\database.py help_text.py new_modes.py` exits `0`; 2000-call sample: matrix `137.53us`, america `80.87us`, holiday `75.34us`, oldweb `80.93us`, jewish `153.27us` | Rejected: shipping creative content without runtime cost check | Estimate: 4000000 us
- [x] Restart and verify bot | DOD: watchdog chain `8444/14024/22048`; runtime snapshot PID `22048` reports `private_mb=504.78`, queues `0`, `messages_storage=3301`, `post_to_messages=369`, `message_to_post=212085`, `reply_coverage.gap_from_latest=0`; DB `quick_check=ok`, `Posts=150366`, `PostCopies=1273984`; live `/b/ #375412` delivered to `626` in `7.292s` | Rejected: source-only mode deployment | Estimate: 120000000 us

## Loop 30: Priority Split Fanout
- [x] Identify remaining queue-lag edge | DOD: delivery log showed `/b/` wait spikes up to `145.994s` and a live `#375422` fanout taking `101.082s`; existing priority only reordered recipients inside one uninterrupted fanout | Rejected: claiming weekly-active ordering alone solves passive-tail blocking | Estimate: 2500000 us
- [x] Add split fanout controls | DOD: added `BOT_PRIORITY_SPLIT_FANOUT`, `BOT_PRIORITY_SPLIT_MIN_PASSIVE`, and `BOT_PRIORITY_PASSIVE_SLICE_SIZE` config knobs | Rejected: hardcoding a risky queue policy with no rollback switch | Estimate: 800000 us
- [x] Split active and passive delivery phases | DOD: `message_worker()` now sends weekly-active recipients as `delivery_phase=priority`, requeues passive tails, slices large passive tails, and logs `delivery_passive_deferred` | Rejected: dropping passive users or rewriting the queue into a durable DB job table during the live patch | Estimate: 5500000 us
- [x] Expose phase telemetry | DOD: `delivery_result` includes `phase`; runtime and `/queues` expose split settings and in-flight phase/recipient split | Rejected: invisible scheduling changes | Estimate: 1800000 us
- [x] Compile and restart bot | DOD: `python -m py_compile main.py common\config.py common\database.py help_text.py new_modes.py` exits `0`; watchdog chain after restart `8444/64968/72112` | Rejected: source-only queue patch | Estimate: 50000000 us
- [x] Verify runtime and DB | DOD: runtime PID `72112` reports `private_mb=484.66`, `delivery_priority.split_fanout=true`, `passive_slice_size=120`, queues `0`, `messages_storage=3300`, `post_to_messages=369`, `message_to_post=210213`; DB `quick_check=ok`, `Posts=150376`, `PostCopies=1278382` | Rejected: waiting for another 30-minute incident before verifying process health | Estimate: 60000000 us
- [x] Update docs | DOD: updated `BOT_ARCHITECTURE.md`, `OPERATIONS_RUNBOOK.md`, and `AUDIT_2026-05-12.md` with split-fanout behavior, knobs, verification, and remaining durable-queue gap | Rejected: chat-only operational memory | Estimate: 3500000 us

## Loop 31: All-Mode Punch-Up Layer
- [x] Audit mode transform wiring | DOD: re-read `main.py` `_apply_mode_transformations()`, existing mode imports, `MODE_FLAGS`, and content branches before patching | Rejected: blindly editing every mode module and risking visual/image paths | Estimate: 2200000 us
- [x] Add shared punch-up profiles | DOD: created `mode_punchup.py` with bounded text profiles for `anime`, `zaputin`, `slavaukraine`, `suka_blyat`, `polish`, `warhammer`, `imperial`, `gopnik`, `schizo`, `matrix`, `america`, `holiday`, `oldweb`, and `jewish` modes | Rejected: network/media generation in the hot message path | Estimate: 4800000 us
- [x] Integrate without breaking escaping/media | DOD: anime punch-up runs before HTML escaping; other text modes run after their primary transform; image-byte paths return unchanged | Rejected: applying regex to binary/image payloads or already-escaped HTML in the wrong branch | Estimate: 2500000 us
- [x] Compile and microbench | DOD: `python -m py_compile main.py mode_punchup.py new_modes.py gopnik_mode.py imperial_mode.py polish_mode.py shizo_mode.py ukrainian_mode.py warhammer_mode.py zaputin_mode.py common\config.py common\database.py help_text.py` exits `0`; 10000-call benchmark: `52.51..163.55us` per punch-up call depending on mode | Rejected: shipping creative density without cost evidence | Estimate: 8500000 us
- [x] Restart and verify live bot | DOD: watchdog chain after restart `8444/13556/15356`; runtime snapshot PID `15356` reports `private_mb=481.16`, queues `0`, `messages_storage=3300`, `post_to_messages=369`, `message_to_post=210213`, `reply_coverage.gap_from_latest=0`; no compile errors | Rejected: source-only mode deployment | Estimate: 70000000 us
- [x] Update project docs | DOD: updated `Docs/MODES_AND_ROADMAP.md` and `Docs/AUDIT_2026-05-12.md` with all-mode punch-up design, cost, and guardrails | Rejected: chat-only creative implementation notes | Estimate: 3000000 us

## Loop 32: Punch-Up Density Expansion
- [x] Quantify first punch-up density | DOD: introspected profiles and found only `8` replacement triggers, `4` prefixes, `4` suffixes, `5` injections per mode | Rejected: assuming the first creative pass was dense enough | Estimate: 1200000 us
- [x] Add common semantic vocabulary expansion | DOD: added `_COMMON_SOURCE_TERMS`, `_MODE_VOCAB`, and `_MODE_PHRASE_EXPANSIONS`; every mode now has `55` replacement triggers, `6` prefixes, `6` suffixes, and `7` injections | Rejected: copy-pasting 14 huge independent dictionaries by hand | Estimate: 6200000 us
- [x] Re-measure cost after expansion | DOD: short 27-token benchmark range `275.37..1216.16us`; longer 81-token benchmark range `1231.53..4642.70us` | Rejected: continuing to inflate dictionaries without timing guardrails | Estimate: 95000000 us
- [x] Compile and restart expanded layer | DOD: compile command exits `0`; restarted only dvachbot child through watchdog to chain `8444/58248/11916` | Rejected: leaving new profile density inactive in live bot | Estimate: 50000000 us
- [x] Verify runtime and DB after restart | DOD: runtime snapshot PID `11916` reports `private_mb=482.34`, queues `0`, `messages_storage=3300`, `post_to_messages=369`, `message_to_post=210213`, `reply_coverage.gap_from_latest=0`; SQLite `quick_check=ok`, `Posts=150376`, `PostCopies=1278382` | Rejected: process table only | Estimate: 70000000 us
- [x] Update docs with final density/cost | DOD: updated `MODES_AND_ROADMAP.md` and `AUDIT_2026-05-12.md` to replace the obsolete `50-165us` punch-up note | Rejected: stale docs that understate runtime cost | Estimate: 2200000 us

## Loop 33: Punch-Up Rollback Knob
- [x] Add env-controlled safety switch | DOD: `BOT_MODE_PUNCHUP_ENABLED` in `common/config.py`, default `1`; `main.py` gates only the shared punch-up layer, not base mode transforms | Rejected: requiring code rollback if CPU pressure appears | Estimate: 1500000 us
- [x] Expose state in telemetry/admin memory view | DOD: `runtime_snapshot.mode_punchup.enabled` and `/debug_memory` formatted snapshot include the switch state | Rejected: hidden env behavior with no runtime proof | Estimate: 1500000 us
- [x] Compile and restart rollback-capable bot | DOD: compile command exits `0`; watchdog chain `8444/19564/20720`; runtime snapshot PID `20720` reports `mode_punchup.enabled=true`, `private_mb=483.62`, queues `0`, `message_to_post=210213` | Rejected: source-only switch | Estimate: 90000000 us
- [x] Re-check DB readonly | DOD: SQLite `quick_check=ok`, `Posts=150376`, `PostCopies=1278382`, `max_post=375422` | Rejected: assuming a restart never affects WAL/database health | Estimate: 5000000 us
- [x] Update operator docs | DOD: `OPERATIONS_RUNBOOK.md`, `MODES_AND_ROADMAP.md`, and audit doc now describe `BOT_MODE_PUNCHUP_ENABLED=0` for CPU/lag incidents | Rejected: undocumented emergency knob | Estimate: 2000000 us

## Loop 34: `/b/` Recipient Truth And Start-Crash Fix
- [x] Prove `/b/` users were not deleted | DOD: readonly SQLite `quick_check=ok`, `/b/ active Telegram=625`, `/b/ banned Telegram=1`, `/b/ active site guests=3359`, `Posts=150427`, `PostCopies=1311219` | Rejected: reading `91/91` priority phase as full board count | Estimate: 18000000 us
- [x] Diagnose `/b/` stall from runtime logs | DOD: `logs/bot_runtime.log` showed `/b/` queue waits above 100s during 2026-05-13 12:59-13:02 while smaller boards still delivered | Rejected: calling it data loss or generic deadlock without queue-age evidence | Estimate: 22000000 us
- [x] Add truthful delivery/recipient telemetry | DOD: `delivery_result` now logs `phase_recipients`, `original_recipients`, `deferred_recipients`; runtime and `/queues` expose real Telegram recipient counts | Rejected: hiding phase counts behind old ambiguous console text | Estimate: 4500000 us
- [x] Add `/b/` media/backpressure guards | DOD: added `BOT_PRIORITY_PASSIVE_MEDIA_SLICE_SIZE=40`, `BOT_PASSIVE_MAX_PREEMPTIONS=3`, `BOT_DELIVERY_SLOW_PHASE_SEC=10`, `BOT_B_MAX_STACKED_ANIME_IMAGES=4`, `BOT_ANIME_MEDIA_CONCURRENCY=1` | Rejected: growing fanout worker count and risking ordering bugs | Estimate: 6500000 us
- [x] Fix restart crash from import-order bug | DOD: import failed because `anime_media_gate` referenced `ANIME_MEDIA_CONCURRENCY` before assignment; moved gate initialization after constant; `python -c "import main"` passes | Rejected: blaming Telegram/SQLite while import failed before `main()` lock update | Estimate: 2500000 us
- [x] Capture future early startup crashes | DOD: `start_bot.bat` now tees `python -u main.py` stdout/stderr to `logs/bot_stdout.log`, covering import errors before runtime logger exists | Rejected: accepting console-only evidence | Estimate: 1200000 us
- [x] Compile/restart/verify live bot | DOD: compile exits `0`; watchdog chain `71864 -> 49008 -> 19792`; `bot.lock=19792`; healthcheck `200`; runtime snapshot PID `19792` reports `/b/ recipients=625`, `passive_media_slice_size=40`, `passive_max_preemptions=3`, `anime_media.concurrency=1`, queues `0` | Rejected: source-only incident fix | Estimate: 90000000 us
- [x] Update docs and append final evidence | DOD: updated `BOT_ARCHITECTURE.md`, `OPERATIONS_RUNBOOK.md`, `AUDIT_2026-05-12.md`, and agent rationale/log with user-count and restart-loop facts | Rejected: chat-only explanation after an incident | Estimate: 4000000 us

## Loop 35: Punch-Up Load Shed And Clean Startup Logs
- [x] Add adaptive punch-up guardrails | DOD: `BOT_MODE_PUNCHUP_QUEUE_SHED_SEC`, `BOT_MODE_PUNCHUP_SLOW_LOG_US`, runtime stats, `/queues` summary, and `/punchup on/off/reset/status` admin command are wired | Rejected: blindly expanding regex flavor during queue pressure | Estimate: 4500000 us
- [x] Fix stdout capture regression | DOD: replaced PowerShell `Tee-Object` pipeline with `python -X utf8 -u main.py >> logs\bot_stdout_utf8.log 2>&1`; preserved old mixed file as `bot_stdout_legacy_mixed_20260513.log` | Rejected: keeping a logging path that can crash on Unicode before runtime logger starts | Estimate: 2500000 us
- [x] Restart and verify live bot | DOD: watchdog chain `60964 -> 36596 -> 31484`; `bot.lock=31484`; healthcheck `200`; runtime PID `31484`, private `500.73 MB`, queues `0`, `/b/ active Telegram recipients `625`, mode punch-up `runtime_enabled=true`, shed `8.0s` | Rejected: source-only startup/logging fix | Estimate: 90000000 us
- [x] Verify database and docs | DOD: SQLite `quick_check=ok`, `Posts=150448`, `PostCopies=1324453`, `/b/ active Telegram=625`, banned `1`, site guests `3359`; updated runbook, mode roadmap, audit, rationale, and log | Rejected: trusting console after a log-encoding incident | Estimate: 8000000 us

## Loop 36: Hidden Watchdog And Media Stall Recovery
- [x] Capture live stall evidence | DOD: old chain `60964 -> 36596 -> 31484`, `bot.lock=31484`, port `8080` owned by `31484`, healthcheck timed out, runtime stopped at `2026-05-13 16:50:53`, stdout stopped in external anime/media URL fetch, process had many `CloseWait` sockets | Rejected: calling it a normal crash or deleting user data | Estimate: 18000000 us
- [x] Restore visible control | DOD: stopped only the bot chain, removed stale `bot.lock`, started visible watchdog as `68260 -> 30504 -> 38448`, then after backlog drained restarted to final visible chain `69912 -> 2584 -> 32956`; healthcheck returned `HTTP 200` | Rejected: killing every Python process or leaving a hidden uninterruptible watchdog | Estimate: 45000000 us
- [x] Bound external media commands | DOD: added URL-fetch/download timeout and parallelism knobs; runtime PID `38448` exposed `anime_media.url_timeout_sec=12`, `url_total_sec=35`, `url_parallel=3`, `download_timeout_sec=35`, `download_total_sec=45`, `download_parallel=2`; final PID `32956` is running same compiled code | Rejected: letting external image APIs/proxies hold the bot loop | Estimate: 6000000 us
- [x] Add operator stop script and truthful startup counts | DOD: added `stop_bot.bat`; startup logs now print `tg_active`, `site_active`, `active_total`, `tg_banned`, `banned_total`; verified `/b/ DB truth `625` TG active, `3361` site guests, `1` TG banned | Rejected: continuing to print `active=3986` with no explanation | Estimate: 2500000 us
- [x] Compile and live verify | DOD: `python -m py_compile main.py common\config.py japanese_translator.py mode_punchup.py new_modes.py common\database.py help_text.py` exits `0`; `python -c "import main"` exits `0`; live healthcheck `HTTP 200`; SQLite `quick_check=ok`, `Posts=150501`, `PostCopies=1350840` | Rejected: restarting again while `/b/` RAM passive backlog was still draining | Estimate: 90000000 us

## Loop 37: Threaded Healthcheck And Per-Recipient Delivery Watchdog
- [x] Prove old healthcheck still lied under stall | DOD: visible chain `69912 -> 2584 -> 32956` stopped writing logs at `2026-05-13 19:08:14`; healthcheck timed out while runtime showed `/b/` `queues.total=4` and in-flight passive slice | Rejected: treating a listening port as process health | Estimate: 6000000 us
- [x] Move healthcheck off the bot event loop | DOD: `start_healthcheck()` now uses `ThreadingHTTPServer`; live chain `24100 -> 15068 -> 32608` returned `HTTP 200` JSON under `/b/` load with `loop_lag_sec=0.8` and `queues_total=4` | Rejected: leaving aiohttp healthcheck on the same stalled asyncio loop | Estimate: 4500000 us
- [x] Bound one-recipient send stalls | DOD: added `BOT_DELIVERY_PER_RECIPIENT_TIMEOUT_SEC=75`, `BOT_DELIVERY_MAX_RECIPIENT_RETRIES=25`, `delivery_result.timeouts`, `delivery_recipient_timeout`, and `delivery_recipient_retry_exhausted`; runtime PID `66628` exposed the knobs | Rejected: letting one hung Telegram/aiohttp request hold a whole board chunk forever | Estimate: 5000000 us
- [x] Add media URL text fallback | DOD: Telegram `wrong type of the web page content` / bad HTTP media URL errors now set a per-phase text fallback and log `delivery_media_url_text_fallback`; old evidence showed post `#375677` photo phases returning `120/120` errors before this patch | Rejected: accepting all-recipient phase failure for one malformed media URL | Estimate: 3500000 us
- [x] Compile, restart, and verify final live bot | DOD: compile command exits `0`; final visible chain `40020 -> 18380 -> 54740`; `bot.lock=54740`; healthcheck `HTTP 200` JSON; runtime PID `54740` reports `private_mb=494.8`, `queues.total=0`, `/b/ Telegram active `625`, `delivery_per_recipient_timeout_sec=75.0`, `delivery_max_recipient_retries=25`, reply gap `0`; SQLite `quick_check=ok`, `Posts=150638`, `PostCopies=1444259`, `max_post=375684` | Rejected: deploying without runtime and DB proof | Estimate: 120000000 us

## Loop 38: External Supervisor After In-Process Health Was Insufficient
- [x] Re-check the supposedly healthy threaded-health process | DOD: old chain `40020 -> 18380 -> 54740`, `bot.lock=54740`, and port `8080` still existed, but healthcheck failed from outside and the process was not a reliable bot | Rejected: calling a live PID healthy | Estimate: 8000000 us
- [x] Add an out-of-process supervisor | DOD: created `bot_watchdog.py`; it launches `main.py`, probes health externally, logs to `logs/bot_supervisor.log`, captures child stdout/stderr in `logs/bot_stdout_utf8.log`, and restarts the child after repeated failed probes plus stale logs or `status=stale` | Rejected: another in-process watchdog inside the same stuck runtime | Estimate: 6500000 us
- [x] Wire visible start/controlled stop | DOD: `start_bot.bat` now runs `python -X utf8 -u bot_watchdog.py`; `stop_bot.bat` now climbs through `bot_watchdog.py` and `start_bot.bat` from `bot.lock` | Rejected: hidden window dependency and killing random Python processes | Estimate: 2500000 us
- [x] Compile and replace old process safely | DOD: `python -m py_compile bot_watchdog.py main.py common\config.py japanese_translator.py mode_punchup.py new_modes.py common\database.py help_text.py` exits `0`; stopped only old tree `18380,29332,40020,54740`; removed `bot.lock`; port `8080` became free | Rejected: restart while pretending old PID was serviceable | Estimate: 45000000 us
- [x] Verify live supervised bot | DOD: visible window `58728`; supervisor chain `43256 -> 23944`; main chain `69868 -> 46488`; `bot.lock=46488`; 3 health samples `HTTP 200` with `status=ok`, `loop_lag_sec=0.487..0.743`, queues `0`; process private memory about `514.08 MB` | Rejected: source-only supervisor patch | Estimate: 90000000 us
- [x] Prove `/b/` users still exist | DOD: SQLite readonly `quick_check=ok`, `Posts=150649`, `PostCopies=1449384`, `max_post=375695`, `/b/ tg_active=625`, `/b/ site_active=3361`, `/b/ tg_banned=1` | Rejected: interpreting `89/624 phase` as user loss | Estimate: 6000000 us
- [x] Update project and audit docs | DOD: updated `BOT_ARCHITECTURE.md`, `OPERATIONS_RUNBOOK.md`, `AUDIT_2026-05-12.md`, rationale, and log with the superseded threaded-health result and current external supervisor evidence | Rejected: leaving stale "final" verification in docs | Estimate: 4500000 us

## Loop 39: Heartbeat Supervisor And Image Command Guardrails
- [x] Capture HTTP-health edge under live `/b/` load | DOD: after restart, `/b/` priority/passive phases and image command deliveries continued, but HTTP health produced timeout/refused failures until the supervisor restarted at `queues.total=0` | Rejected: pretending the port listener alone proved service health | Estimate: 18000000 us
- [x] Add event-loop heartbeat file | DOD: `main.py` writes `logs/bot_heartbeat.json` every ~2s with `pid`, `queues_total`, `queues_top`, `post_counter`, and `is_shutting_down` | Rejected: relying only on stale 5-minute runtime snapshots for kill safety | Estimate: 2500000 us
- [x] Teach supervisor to use heartbeat | DOD: `bot_watchdog.py` reads fresh heartbeat before runtime/stdout tail parsing; added `BOT_WATCHDOG_HEARTBEAT_STALE_SEC=15` | Rejected: killing while last known queue value was stale and nonzero | Estimate: 2500000 us
- [x] Harden HTTP health socket behavior | DOD: health server now uses daemon handler threads, request queue `64`, per-request socket timeout `2s`, and `Connection: close` | Rejected: unbounded health handler threads/closed sockets during probe storms | Estimate: 1500000 us
- [x] Sanitize image command source path | DOD: booru API queries and post selection use shared safety blocked tags; legacy `/loli` now fetches safe cute/chibi sources only; full media URLs no longer print in fetch/download logs | Rejected: continuing to fetch/log unsafe tag-heavy URLs from external image boards | Estimate: 5500000 us
- [x] Compile, restart, and verify bot/site/DB/images | DOD: compile exits `0`; final chain `36128 -> 35556 -> 32376 -> 71836 -> 3456`; health 3/3 `HTTP 200`; heartbeat `queues_total=0`; site `8000=200`; SQLite `quick_check=ok`, `Posts=150715`, `PostCopies=1491101`; image probes pass | Rejected: source-only incident patch | Estimate: 140000000 us
- [x] Update docs and logs | DOD: updated `OPERATIONS_RUNBOOK.md`, `BOT_ARCHITECTURE.md`, `AUDIT_2026-05-12.md`, status, rationale, and log | Rejected: chat-only operational memory | Estimate: 4500000 us

## Loop 40: Visible Watchdog Output And Heartbeat-First Probing
- [x] Accept operator log regression | DOD: visible `start_bot.bat` window showed supervisor health lines while delivery truth was only in `logs/bot_stdout_utf8.log` and `logs/bot_runtime.log` | Rejected: telling the operator to tail hidden files for normal delivery status | Estimate: 1200000 us
- [x] Tee child stdout back to the visible console | DOD: `bot_watchdog.py` now launches `main.py` with `stdout=PIPE`; a daemon pump thread writes child lines to both the console and `logs/bot_stdout_utf8.log` | Rejected: PowerShell `Tee-Object`, because it already caused Unicode startup failures | Estimate: 2200000 us
- [x] Make heartbeat primary for supervisor health | DOD: watchdog checks fresh `logs/bot_heartbeat.json` before probing HTTP; after restart no new `HTTP health failed` spam appeared past `23:56:53` during the warmup verification window | Rejected: hammering the health socket while event-loop heartbeat is fresh | Estimate: 1800000 us
- [x] Harden health socket cleanup | DOD: `main.py` health handler now closes requests in `finish()` and catches `OSError`; final port `8080` had `Listen=1`, `TimeWait=1`, `CloseWait=0` | Rejected: leaving `CloseWait` buildup as acceptable noise | Estimate: 1000000 us
- [x] Compile and restart only after queue drain | DOD: waited for heartbeat `queues_total=0`, then controlled restart through `stop_bot.bat` and `start_bot.bat`; final chain `9380 -> 69104 -> 29660 -> 59488 -> 7972` | Rejected: restarting while `/b/` RAM queue was `7-8` and losing passive tails | Estimate: 120000000 us
- [x] Add next-stall stack evidence | DOD: `main.py` now starts an event-loop stall watchdog thread; after `BOT_EVENT_LOOP_DUMP_STALE_SEC=30`, it writes all Python thread stacks to `logs/bot_deadlock_watchdog.log` | Rejected: another unexplained watchdog kill with no stack trace | Estimate: 2500000 us
- [x] Verify bot/site/DB after restart | DOD: heartbeat `pid=7972`, `queues_total=0`, `post_counter=375809`; bot health `HTTP 200`; site `8000=200`; DB `Posts=150763`, `PostCopies=1518162`; `/b/ tg_active=625`, site active `3362`, banned `1`; deadlock dump file not created because no post-restart 30s stall occurred | Rejected: relying only on process table | Estimate: 95000000 us
- [x] Update docs and logs | DOD: updated `OPERATIONS_RUNBOOK.md`, `BOT_ARCHITECTURE.md`, `AUDIT_2026-05-12.md`, rationale, and final log with visible-console behavior and latest evidence | Rejected: leaving the operator-visible regression undocumented | Estimate: 4000000 us

## Loop 41: Mode Signature Punchlines
- [x] Add bounded signature punchline layer | DOD: `mode_punchup.py` now appends cheap per-mode final punchlines via `signatures`, `signature_chance=0.16`, and `max_text_for_signature=1200` | Rejected: more regex vocabulary expansion after measured hot-path cost | Estimate: 1800000 us
- [x] Add mature per-mode signature banks | DOD: every supported punch-up mode has `6` signatures; `jewish_mode` remains Talmudic/Odessa debate, not protected-class caricature | Rejected: childish filler and ethnicity-as-punchline content | Estimate: 2500000 us
- [x] Compile and benchmark | DOD: `python -m py_compile main.py mode_punchup.py new_modes.py common\config.py common\database.py help_text.py` exits `0`; source-trigger 1200-call sample short p95 about `0.09-0.34ms`, 4x repeated p95 about `0.34-0.84ms` | Rejected: shipping flavor without timing | Estimate: 9000000 us
- [x] Restart only after queue drain | DOD: heartbeat `pid=7972` had queues `1..3`; waited until `queues_total=0`, then stopped tree `7972,9380,13564,29660,59488,69104` and started visible chain `58540 -> 20412 -> 20888 -> 48028 -> 8176` | Rejected: killing a nonempty RAM passive tail | Estimate: 60000000 us
- [x] Verify live bot/site/DB | DOD: heartbeat `pid=8176`, queues `0`; bot health `HTTP 200`; site `8000=200`; runtime `private_mb~=509.02`, `mode_punchup.enabled=true`; SQLite `quick_check=ok`, `Posts=150806`, `PostCopies=1544883`, `/b/ tg_active=624`, site active `3362`, banned `1`; no deadlock dump | Rejected: source-only creative patch | Estimate: 90000000 us
- [x] Update docs/logs | DOD: updated mode roadmap, runbook, audit, rationale, and agent log with signature layer, cost, restart, and DB evidence | Rejected: chat-only report | Estimate: 3500000 us

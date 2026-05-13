# MANUAL_AUDIT Rationale

## Decision 1: Treat Hecton Unity mandates as secondary constraints
Problem: The active request targets Python Telegram bot/site code, while provided project rules are Unity/C# specific.
Solution: Use the rule spine only for evidence discipline, memory ownership, persistence safety, telemetry, and fail-fast behavior. Do not force Unity `GlobalRegistry`, NativeArray, or frame rendering rules into Python.
Rejected Alternatives: Blindly applying Unity architecture would create fake dependencies and irrelevant code churn. Ignoring all rules would violate the user's requested workflow.
Scalability potential: Low-end device path focuses on bounded memory, SQLite indexes, backpressure, and no large in-RAM fanout maps; high-end path can add richer bot modes, media generation, and analytics once reliability is flat.
Hardware Impact: On i3/MX350-class hardware, moving from unbounded Python memory structures to bounded DB-backed caches can save hundreds of MB over multi-day uptime and reduce swap/HDD thrash.

## Decision 2: Start with readonly evidence
Problem: Live bot and site are running for real users, with active SQLite WAL files.
Solution: Inspect processes, source, logs, and readonly SQLite queries first; only patch after the failure path is proven.
Rejected Alternatives: Restarting services or vacuuming/checkpointing DB immediately is unsafe without knowing active transactions and current queue state.
Scalability potential: Low tier remains operational during audit; high tier can later use structured telemetry and priority queues.
Hardware Impact: Readonly diagnostics avoid blocking the bot's write path and reduce risk of long SQLite locks on slow storage.

## Decision 3: Preserve Telegram copy truth in SQLite, not RAM
Problem: Telegram replies require the per-recipient `PostCopies` row. `cleanup_old_posts_from_db()` deleted those rows after 48 hours, while `Posts` survived. Site history stayed visible, but bot replies lost the exact `reply_to_message_id`.
Solution: Replace the 48-hour copy purge with bounded rolling retention: keep copy rows by recent post distance and by age, and cap startup hydration with `BOT_COPY_CACHE_POST_LIMIT`. This keeps old reply targets queryable from SQLite without loading the entire fanout map into RAM.
Rejected Alternatives: Keeping every `PostCopies` row forever would let SQLite grow without policy. Loading all retained copy rows into `message_to_post` on startup would reproduce the memory blow-up. Rebuilding deleted historical copies is not possible from `Posts`; Telegram message IDs are per user and already discarded.
Scalability potential: Low: 3300 hot posts in RAM, old replies served from indexed SQLite lookup. Middle: 12000 retained post-copy window. High: larger retention via env without code change. Ultra: separate copy-store shard or C extension only after measuring SQLite as bottleneck.
Hardware Impact: On i3/MX350, capping hydration prevents millions of Python tuple/dict entries at boot. Expected RAM saved versus naive 12000-post hydration is hundreds of MB to >1 GB depending on active recipients; per-reply DB lookup remains indexed by `idx_postcopies_post_num`.

## Decision 4: Shadow reject must not pass post numbers as Telegram message IDs
Problem: `process_shadow_reject()` passed `{user_id: reply_to_post}` as `reply_info`, but `send_message_to_users()` interprets `reply_info` values as Telegram `message_id`, not global post numbers. A shadow-muted user replying to old content could get a broken/non-reply echo, which is a detection leak.
Solution: Store `reply_to_post` in `content` and let the normal DB/RAM resolver find the correct per-user Telegram copy. Do not forge `reply_info`.
Rejected Alternatives: Leaving broken reply behavior exposes shadow state. Doing a special one-off resolver inside `process_shadow_reject()` duplicates the existing resolver and increases divergence.
Scalability potential: Low through Ultra use the same single reply resolution path; quality scales with `PostCopies` retention, not a separate shadow branch.
Hardware Impact: One indexed lookup on old replies only. Hot path unchanged for recent RAM-cached messages.

## Decision 5: Document before broad queue rewrite
Problem: Queue lag, memory growth, mode duplication, and admin stats are real, but a large fanout rewrite on a live 630-user bot would risk dropping messages without enough instrumentation.
Solution: Apply only the isolated reply/copy retention fix now, document the architecture and operational hazards, and mark queue priority/fanout persistence as the next reliability project.
Rejected Alternatives: Replacing the FIFO broadcaster immediately would require changing delivery semantics, retry state, and crash recovery without current queue telemetry. Ignoring the queue issue would leave the half-hour delay failure mode undocumented.
Scalability potential: Low: current FIFO remains, but operators know the bottleneck. Middle: weekly-active priority fanout. High: persisted fanout progress. Ultra: separate delivery service and shardable copy store.
Hardware Impact: On i3/MX350, prioritizing active weekly users can make the visible community feel near-real-time even while passive fanout drains slowly under CPU saturation.

## Decision 6: Keep creative expansion behind reliability gates
Problem: The bot has strong mode content, but adding chess/image generation/AI without delivery observability would spend CPU and memory exactly where the bot is currently weak.
Solution: Put mode registry, image-generation queue, and chess as roadmap items after logging, queue lag telemetry, and priority delivery.
Rejected Alternatives: Direct hot-path image generation would block user interaction and create external API backpressure. Handwritten chess rules would be unnecessary bug surface.
Scalability potential: Low: text-only deterministic modes. Middle: visual mode cards. High: queued generation. Ultra: isolated media worker.
Hardware Impact: On weak hardware, this avoids image/API bursts in the bot loop; on high-end hardware, isolated workers can use saved cycles for visual overkill without poisoning message delivery.

## Decision 7: Instrument memory before claiming the leak is fixed
Problem: The bot reportedly grows to 3 GB after days, but a restart snapshot only proves current memory, not leak absence. Existing `memory_restarter` kills the process at the limit and `log_memory_summary()` prints heavy diagnostics to stdout, leaving weak postmortem evidence.
Solution: Add a cheap rotating runtime telemetry log and admin snapshots: process RSS/private/VMS, queue sizes, hot map sizes, media groups, pending edit tasks, DB/WAL sizes, asyncio task count, and GC counters. Keep `tracemalloc` off until `/debug_memory` to avoid always-on allocation tracking overhead.
Rejected Alternatives: Enabling permanent `tracemalloc` would improve allocation detail but can add runtime memory/CPU cost to a live bot. Calling `gc.get_objects()` every few minutes would create observer overhead and produce noisy logs. Rewriting the fanout queue now would be higher risk without baseline telemetry.
Scalability potential: Low: 5-minute telemetry on weak hardware. Middle: threshold warnings for queue/memory growth. High: admin dashboard graphs. Ultra: external collector with per-post fanout latency and shardable worker metrics.
Hardware Impact: On i3/MX350-class hardware, telemetry is O(number of boards + tracked maps) and avoids object-heap walks. It should cost negligible CPU while providing enough signal to identify whether growth comes from queues, copy maps, media groups, pending tasks, WAL, or site/bot caches.

## Decision 8: Cut startup RAM caches, not database history
Problem: Telemetry proved that after restart the bot loaded `messages_storage` around 25k posts and `message_to_post` around 1.1M entries. This was not a leak over time; it was startup bloat caused by confusing SQLite retention with Python hot cache size.
Solution: Add `BOT_POST_CACHE_LIMIT=3300` for heavy content cache while keeping `DB_POST_LIMIT=25000` for SQLite policy. Keep thread lifecycle safe by loading thread post number lists separately. Reduce `BOT_COPY_CACHE_POST_LIMIT` to `1000`; older reply/copy lookups use indexed `PostCopies`.
Rejected Alternatives: Reducing `DB_POST_LIMIT` would destroy site/bot history and make old replies worse. Dropping thread `posts` lists would break archive/milestone behavior. Keeping 3300 copy-cache posts created over one million hot dict entries for little benefit because DB fallback already exists.
Scalability potential: Low: 3300 content posts + 1000 copy-cache posts. Middle: tune env knobs per hardware. High: active-board adaptive cache. Ultra: separate copy-store service or C extension only if SQLite lookup becomes measured bottleneck.
Hardware Impact: On the live sample, private memory dropped from about 724 MB to about 552 MB, `messages_storage` from ~25k to ~3300, and `message_to_post` from ~1.12M to ~631k. On weak hardware this reduces paging risk and leaves more headroom for burst queues.

## Decision 9: Prioritize visible humans before passive fanout
Problem: `/b/` fanout used an unordered recipient set and one sequential board worker. Under CPU saturation, active writers and passive lurkers waited in the same random order, causing the people who notice lag first to suffer the same delay as inactive readers.
Solution: Add a SQLite-backed weekly-active set and reorder recipients inside the existing `send_message_to_users()` path as `weekly-active -> passive`. New non-shadow bot authors are marked hot immediately; DB refresh runs every 900 seconds by default. This changes delivery order only, not message eligibility.
Rejected Alternatives: A new durable fanout table is the correct end-state, but it touches crash recovery, retries, and cleanup. Doing that immediately on the live bot risks losing or duplicating deliveries. Dropping passive recipients would improve perceived speed dishonestly and break the product contract.
Scalability potential: Low: existing one-worker fanout but active users receive first. Middle: persisted fanout progress. High: per-board active/passive latency stats. Ultra: separate delivery service with shardable queues and multiple sender pools.
Hardware Impact: On i3/MX350-class hardware, this does not reduce total Telegram API work, but it moves perceived latency to the least sensitive users. Live verification found `138` priority author-board entries, `/b/=98`; runtime snapshot after restart showed `private_mb=578.8` and priority telemetry active.

## Decision 10: Keep delivery metrics bounded and process-local
Problem: Admin stats could show queue size, but not whether fanout itself was slow, how many recipients were priority, or how many retries/errors happened. Without per-delivery evidence, queue complaints degrade into console archaeology.
Solution: Add a bounded `delivery_metrics` deque per board and write `delivery_result` JSON for completed verbose fanouts. `/queues` now shows the last completed delivery and recent avg/max seconds from the in-memory buffer.
Rejected Alternatives: Persisting every delivery result in SQLite would be better for historical analytics, but it adds write load during the exact hot path being diagnosed. An unbounded Python list would become a new memory leak. A separate admin command would hide the data from the existing incident workflow.
Scalability potential: Low: last 100 fanouts per board in RAM. Middle: hourly aggregate table. High: percentiles and oldest queued age. Ultra: external metrics collector with board/fanout dashboards.
Hardware Impact: Bounded O(boards * 100) records is negligible on i3/MX350-class hardware. It spends a few tiny dicts to buy operational visibility and avoids large DB writes during broadcast.

## Decision 11: Prune orphan reply reverse-cache entries
Problem: `message_to_post` is both a hot copy reverse map and a fallback cache for old DB-resolved replies. Some fallback paths add `(chat_id, message_id) -> post_num` without adding that post to `post_to_messages`. The existing cleaner removed reverse entries only through `post_to_messages`, so old one-off reply lookups could remain reachable indefinitely.
Solution: During `auto_memory_cleaner()`, compute the current hot post window from `messages_storage` and `post_to_messages`, then remove `message_to_post` entries pointing outside that window. Durable resolution remains in SQLite `PostCopies`, so old replies can be resolved again without keeping the Python reverse cache forever.
Rejected Alternatives: Calling `gc.collect()` more often cannot free reachable dict entries. Deleting `PostCopies` would break old replies. Keeping all old reverse lookup hits in RAM recreates the leak under heavy old-reply usage.
Scalability potential: Low: 30-minute cleaner pass over the reverse map. Middle: cap fallback reverse-cache by LRU. High: split hot copy map from fallback lookup cache. Ultra: C-backed or external copy index only if measured as bottleneck.
Hardware Impact: On weak hardware, the pass costs an O(message_to_post) scan every cleaner interval, but prevents unbounded old-reply cache growth. Live post-restart snapshot showed `message_to_post=622762` with DB fallback still intact.

## Decision 12: Shadow mute should not leak through language or duplicate fake numbers
Problem: Some shadow reject paths relied on the default `stream='ru'`, so multilingual boards could receive fake headers in the wrong language. The fake post number was also `state['post_counter'] + random(1..3)` every time, making repeated shadowed posts from one user vulnerable to duplicate numbers.
Solution: Convert audited shadow reject calls to pass `stream` explicitly and add `shadow_fake_post_counters[(board_id,user_id)]` for monotonic fake numbering in bursts. Add the cache to runtime telemetry and emergency cleanup.
Rejected Alternatives: Full per-recipient post-number virtualization would be cleaner stealth, but it would require rewriting all outbound headers for one user and is too large for a live patch. Leaving duplicates is a direct detection leak.
Scalability potential: Low: small in-memory per-shadow-user counter. Middle: expire counters by last activity. High: per-recipient number translation layer. Ultra: independent shadow timeline with translated public numbers for the muted user only.
Hardware Impact: Negligible on i3/MX350; runtime snapshot after restart showed only `shadow_fake_post_counters=7`.

## Decision 13: Measure completed queue lag as post age
Problem: Queue size alone does not explain the user's 10-30 minute lag complaint. A queue can be empty after a delay spike, and fanout duration alone misses time spent waiting behind earlier posts.
Solution: Add `post_age_sec` to `delivery_result`, measured from logical post creation timestamp to completed fanout. Include average/max age in runtime delivery summaries and show last age in `/queues` when present.
Rejected Alternatives: Adding `enqueued_at` to every queue producer now would touch many call sites. Persisting every delivery metric to SQLite would add write load to the hot path. Process-local bounded metrics plus durable runtime JSON gives immediate evidence with low risk.
Scalability potential: Low: JSON line telemetry. Middle: per-board oldest queued item age. High: persisted delivery percentiles. Ultra: external metrics collector and durable fanout progress table.
Hardware Impact: One timestamp subtraction per completed fanout; effectively zero overhead, and it reveals whether cheap hardware is suffering CPU/API delay or just normal fanout time.

## Decision 14: Memory restarter must use committed/private memory on Windows
Problem: The emergency memory monitor used RSS only. The user's observed failure mode is process memory climbing toward multi-GB, and on Windows private committed memory can be the decisive pressure metric while working set/RSS is lower.
Solution: Compute RSS and private/USS from psutil, then compare `max(RSS, private/USS)` against the existing `MEMORY_LIMIT_GB`.
Rejected Alternatives: Lowering the limit without changing the metric could still miss private-memory blowups. Enabling permanent heap tracing would add overhead and does not stop the process before OS pressure.
Scalability potential: Low: safer process restart guard. Middle: make the limit env-configurable. High: restart only after dumping full telemetry and queue state. Ultra: graceful handoff to a separate delivery worker before self-termination.
Hardware Impact: Negligible per-minute psutil call. On weak hardware, it reduces the chance that swap/commit pressure kills the bot before the watchdog can restart it cleanly.

## Decision 15: Image generation must be a queued sidecar, not a chat hot-path feature
Problem: The user asked about free image generation. Current providers have changing free tiers, keys, or paid limits. Running image generation inline would compete directly with the fanout queue and worsen the delay problem.
Solution: Document providers as candidates only behind `/imagine` job queue, per-user cooldown, per-board daily budget, and Telegram file-id reuse after upload.
Rejected Alternatives: Calling a free endpoint directly from message handling would turn external latency/rate limits into chat latency. Building a local generator on the same weak machine would compete with bot delivery CPU/RAM.
Scalability potential: Low: Pollinations/Hugging Face experiment with strict quotas. Middle: Cloudflare Workers AI sidecar. High: provider abstraction with fallback and queue depth controls. Ultra: separate media worker host with cached generated assets and moderation.
Hardware Impact: On i3/MX350-class hardware, queued sidecar prevents image work from blocking text delivery. On high-end hardware, the queue can be expanded without changing bot delivery semantics.

## Decision 16: Old replies need one quote extractor, not handler-specific fragments
Problem: Fallback quotes for old replies were implemented in several handlers with different coverage. Text/media paths had partial quote logic, audio looked only at `files`, voice/video_note had no quick quote, and multi-reply paths could miss attachment summaries. This made old replies inconsistent exactly where native Telegram replies are weakest.
Solution: Add `build_quick_quote_info(reply_to_post)` and `_quote_info_from_content()` as one source for old-reply quote snippets. The helper reads stored logical `Posts.content` and summarizes text/caption plus common attachment shapes: media groups, `files`, direct `file_id`, image bytes/URLs, stickers, voice, video notes, audio, documents, GIFs, and polls.
Rejected Alternatives: Duplicating more quote logic per handler would keep behavior drifting. Forcing native Telegram replies without `PostCopies` is impossible because message IDs are per recipient. Loading old posts into RAM just for quotes would increase memory pressure.
Scalability potential: Low: compact text quote and attachment counters. Middle: richer localized quote labels. High: quote cards on the site. Ultra: per-recipient timeline virtualization, but only after delivery persistence exists.
Hardware Impact: One SQLite `get_post_by_num()` only for replies beyond `QUICK_QUOTE_POST_DISTANCE=330`; recent replies stay cheap. On weak hardware this avoids widening hot RAM caches while making old conversations readable.

## Decision 17: Queue incidents need live age, not only completed fanout time
Problem: `post_age_sec` on completed fanouts proved end-to-end age after the fact, but it did not expose a currently growing RAM queue or the current in-flight post. During CPU saturation, the operator needs to know whether `/b/` is actively sending, stuck behind one large media post, or accumulating live backlog.
Solution: Route all board queue producers through `enqueue_board_message()` and stamp `enqueued_at`. Track `current_deliveries[board_id]` while the worker is inside `send_message_to_users()`. Expose `queues.age_by_board`, `queues.oldest`, and `queues.in_flight` in runtime snapshots and add `Live age/current` to `/queues`. Completed fanouts now also log `queue_wait_sec` and `queue_total_sec`.
Rejected Alternatives: Rewriting the whole fanout queue into a durable priority table is still the correct end-state, but too broad for this live pass. Queue size alone is insufficient; it can be zero after the damage is done. Persisting every queue metric to SQLite would add hot-path writes.
Scalability potential: Low: process-local live queue age. Middle: persisted hourly aggregates. High: durable fanout progress with resume. Ultra: separate delivery service with active/passive shards and external metrics.
Hardware Impact: One timestamp per queued item and one small in-flight dict per board. On i3/MX350-class hardware, overhead is negligible; operational value is high because it identifies backlog before users spend 30 minutes waiting.

## Decision 18: Reply health must be visible without hand-written SQL
Problem: The old-reply fix depends on `PostCopies`, but admins previously had no quick way to see how many posts still have native-reply copy coverage, what post range is covered, or whether the latest posts are being recorded.
Solution: Add `get_reply_coverage_stats()` and a background `reply_coverage_refresh_task()`. It caches total copy rows, distinct covered posts, min/max covered post, latest-post gap, and per-board spans. Runtime snapshots and `/queues` now show the cached values.
Rejected Alternatives: Running `COUNT(DISTINCT)` inside every `runtime_snapshot` would add measurable DB work every 5 minutes. A separate command would hide the most relevant reply-health fact from the existing incident screen. Manual SQL is slow and error-prone during production incidents.
Scalability potential: Low: 15-minute cached coverage. Middle: per-board last-24h coverage. High: alert when latest gap grows. Ultra: copy-store dashboard with delete/edit/reply coverage by board and age.
Hardware Impact: Raw coverage query measured about 0.486 seconds against the live DB. Running it every 900 seconds is acceptable on weak hardware; keeping it cached avoids adding query cost to hot admin/runtime paths.

## Decision 19: Site memory needs admin visibility too
Problem: The live site process was heavier than the bot at the sampled moment (`private_mb` above 1.1 GB), but site admin stats only exposed content counts, queue counts, and websocket count. That makes site memory/cache growth invisible during the same incidents blamed on the bot.
Solution: Add `get_site_process_snapshot()` and `get_site_runtime_snapshot()` to `site_tgach.main`, then expose them in `/api/admin/stats` and `/api/admin/system_health`. Track process RSS/private/VMS, threads/open files, broadcast queue size, active connection keys, captcha sessions, spam tracker size, post-rate limiter, system log count, spam-word boards, board/thread version maps, and URL status cache.
Rejected Alternatives: Adding a separate site telemetry daemon would be larger than needed. Clearing site caches blindly would destroy performance and hide the evidence. Treating process restart memory drop as proof of a leak would be dishonest.
Scalability potential: Low: admin API visibility. Middle: periodic site runtime log. High: alert on process private memory and cache cardinality. Ultra: external metrics collector across bot/site/media workers.
Hardware Impact: The snapshot is computed only on admin requests, not on every web hit. On weak hardware it adds negligible overhead and gives enough evidence to distinguish cache growth from process allocator reserve.

## Decision 20: Hot copy cache should be smaller and object-cheaper
Problem: Even after cutting startup bloat, `message_to_post` still held hundreds of thousands of entries because every hot post fans out to hundreds of recipients. A one-element list per normal text/photo copy adds avoidable Python object overhead.
Solution: Reduce `BOT_COPY_CACHE_POST_LIMIT` default from `1000` to `700` and store a plain `int` for one Telegram copy, using a list only for albums or multi-message sends. SQLite `PostCopies` remains the durable old-reply resolver.
Rejected Alternatives: Reducing `PostCopies` retention would break old replies again. Keeping `1000` hot posts is convenient but burns RAM on a machine already fighting other workloads. Rewriting the copy store in C++ now is premature without proving SQLite lookup is the bottleneck.
Scalability potential: Low: 700 hot copy posts. Middle: tune via env per hardware. High: adaptive board-specific hot windows. Ultra: C-backed/external copy index after measured lookup pressure.
Hardware Impact: Live restart sample after this change showed about `523.71 MB` private memory, `post_to_messages=666`, and `message_to_post=417771`, down from the previous ~630k reverse entries. On weak hardware this reduces allocator pressure and leaves more headroom for burst queues.

## Decision 21: Periodic cleaner must follow the same RAM limit as startup
Problem: Startup hydration used `BOT_POST_CACHE_LIMIT=3300`, but `auto_memory_cleaner()` still had a hardcoded `REAL_RAM_LIMIT=2000`. That made runtime state drift after the first 30-minute cleaner pass and made documentation/telemetry harder to interpret.
Solution: Replace the hardcoded cleaner limit with `MAX_MESSAGES_IN_MEMORY`, which is sourced from `BOT_POST_CACHE_LIMIT`.
Rejected Alternatives: Adding a second cleaner-specific env knob would increase operator confusion. Keeping the stale 2000 value is not a deliberate memory policy; it is hidden drift. Lowering `BOT_POST_CACHE_LIMIT` globally would reduce context cache for no proven need.
Scalability potential: Low through Ultra share one explicit hot-content-cache knob. Hardware classes can tune the knob without editing code.
Hardware Impact: Negligible CPU change. The value is now predictable: weak machines can set `BOT_POST_CACHE_LIMIT` lower, high-end machines can raise it, and the cleaner will obey the same policy after uptime.

## Decision 22: Site abuse maps need periodic stale cleanup
Problem: Site request/security state had several maps that only cleaned themselves on repeat access or emergency size thresholds: `REQUEST_FLOOD_TRACKER`, expired `IP_BAN_LIST`, expired `IP_TROLL_CONFIG`, `BOT_VIOLATIONS`, and `KNOWN_IPS`. Under scanners, many one-off IP/path keys can remain reachable longer than useful.
Solution: Extend `cleanup_site_runtime_maps_once()` to trim stale flood tracker entries, remove expired bans/troll configs, cap-clear large abuse maps, and expose cardinalities in site runtime snapshots.
Rejected Alternatives: Clearing security maps blindly every interval would make abuse detection forget too much. Leaving only emergency `>10000` clears accepts avoidable memory drift. Moving this state into SQLite is unnecessary for short-lived anti-flood windows.
Scalability potential: Low: local in-process cleanup. Middle: env-tuned TTL/caps. High: persisted coarse abuse counters only for severe offenders. Ultra: external reverse proxy/WAF handles hot IP tracking before Python.
Hardware Impact: Cleanup runs every site cache interval by default and scans small maps. On weak hardware this is cheaper than letting thousands of dead lists/dicts accumulate under bot traffic.

## Decision 23: Small bot maps should be visible and TTL-pruned
Problem: Several global bot maps are small during normal operation but unbounded by design: thread viewer cooldowns, reaction ratelimits, poll cooldowns, hourly image counters, and author reaction notification throttles. Some were hidden from telemetry, so a slow leak would only appear as private-memory drift.
Solution: Add named runtime counters and TTL cleanup inside `auto_memory_cleaner()`. The cleanup removes stale entries after their operational usefulness has expired.
Rejected Alternatives: Emergency `len(cache)>10000` clears are too late and too blunt. More frequent `gc.collect()` cannot free reachable dict entries. Persisting cooldown maps to DB is unnecessary churn for short-lived state.
Scalability potential: Low: in-process TTL pruning. Middle: env-tuned TTL values. High: per-board rate-map metrics. Ultra: external rate limiter only if the bot becomes multi-process.
Hardware Impact: The cleaner scans small dicts every 30 minutes. On weak hardware this is negligible and prevents silent accumulation over multi-day uptime.

## Decision 24: Worker telemetry must count deliverable Telegram users only
Problem: `message_worker()` counted negative site guest IDs in `current_deliveries.recipients`, producing a live sample of `3986` recipients while the completed delivery correctly sent to `626`. Delivery itself was safe because `send_message_to_users()` later filtered `uid > 0`, but the live queue telemetry was misleading and did extra set work.
Solution: Filter `uid > 0` in the worker before setting `current_deliveries` and before calling `send_message_to_users()`.
Rejected Alternatives: Leaving the mismatch would make future lag incidents look worse or point at the wrong source. Removing site guest IDs from shared board state globally is riskier because the site may depend on them for user-state routing.
Scalability potential: Low through Ultra: bot fanout metrics count actual Telegram recipients; site guest state remains site-local operational state.
Hardware Impact: Minor CPU/set-size reduction on posts where `user_state` contains many site guests. Main win is diagnostic correctness.

## Decision 25: Site image processing should not use per-site ProcessPool workers on this host
Problem: Two orphaned Python `multiprocessing.spawn` workers from dead site parent PID `1168` survived since 2026-05-11 and held about 515 MB private memory each. The source was consistent with `site_tgach/image_processing.py` using lazy `ProcessPoolExecutor(max_workers=2)` for grimdark/thumbnail work.
Solution: Kill the two orphan workers after verifying they were not current bot/site/stomchat/MCP processes. Replace image-processing process pools with bounded `ThreadPoolExecutor` instances and add `shutdown_image_executors()` in the site lifespan shutdown path.
Rejected Alternatives: Keeping process pools preserves some CPU parallelism but duplicates a full Python/PIL interpreter and creates large orphan risk on force restarts. Killing all Python children would be reckless. Moving image processing inline would block the event loop.
Scalability potential: Low: thread executors on the same host. Middle: separate media worker process with explicit watchdog and health checks. High: external media service. Ultra: dedicated host/GPU sidecar for image generation and heavy transforms.
Hardware Impact: Immediate OS-level memory recovery was about 983 MiB private memory from PIDs `37776/38024`. Future image transforms avoid spawning 2 large child interpreters; CPU parallelism may be lower, but event-loop safety remains because work stays in executor threads.

## Decision 26: Nested bot maps need their own counters and TTL cleanup
Problem: Several small nested maps inside `board_data` were still second-class citizens: cooldowns, spam deques, reaction queues, thread locks, image spam timestamps, and anime daily limits. They were not the current 500 MB memory source, but without telemetry they can produce multi-day private-memory drift that looks like "Python leak".
Solution: Add `runtime_snapshot.board_maps` with per-map cardinalities and item counts. Extend `auto_memory_cleaner()` to prune expired `anime_daily_tracker`, expired `image_spam_tracker`, old `unknown_command_tracker`, and thread locks whose thread no longer exists. Fix the quick-menu image callback to clean stale `/b/` image timestamps before enforcing the limit.
Rejected Alternatives: Calling `gc.collect()` more often cannot free reachable globals. Clearing whole board maps would break moderation state, user settings, and thread navigation. Moving these short-lived maps to SQLite would add write churn for disposable cooldown state.
Scalability potential: Low: visible bounded in-process maps. Middle: env-tuned TTLs. High: alert when a specific board map grows. Ultra: external rate limiter and metrics collector only if the bot becomes multi-process.
Hardware Impact: Cleaner scans a few small dicts every 30 minutes and the snapshot does simple length sums. On weak hardware this cost is negligible; the gain is that future drift has named counters instead of blind heap growth.

## Decision 27: Stomchat needs cheap runtime evidence before deeper surgery
Problem: `stomchat` was a long-lived Python process from 2026-05-03 with about 452 MB private memory, OpenCV/Groq/Telethon loaded, and an 18.8 MB unbounded `bot.log`. There was no memory time series, so any claim about leak or stability would be guesswork.
Solution: Replace the plain file logger with a rotating 5 MB file handler and add an optional-psutil `runtime_memory` heartbeat every 900 seconds. Restart through the existing watchdog to activate telemetry and reset the baseline.
Rejected Alternatives: Deep Telethon/OpenCV refactor without time-series evidence would be cargo-cult work. Killing/restarting all Python processes would risk unrelated services. Permanent tracemalloc would add overhead for a small summary bot.
Scalability potential: Low: rotating local logs and private/RSS heartbeat. Middle: add DB/WAL and queue counts. High: share the same external telemetry format as dvachbot/site. Ultra: split media analysis into a sidecar if OpenCV/Groq memory growth is proven.
Hardware Impact: Immediate restart baseline moved from about 451.88 MB private to about 413.64 MB private. The main gain is not that this proves a leak fixed; it creates enough process evidence to catch future slope without leaving log files to grow forever.

## Decision 28: User-suggested modes must be cheap and bounded
Problem: The user supplied Matrix, holiday, American, old-internet, and Jewish-mode ideas while the bot is still being hunted for memory drift and queue delays. Adding heavy media generation or broad architecture churn here would create a new failure source in the same hot path.
Solution: Implement four text-only modes in `new_modes.py`: `/matrix`, `/america`, `/holiday`, `/oldweb`. Reuse the existing one-active-mode lifecycle, `Boards.settings` JSON, normal system-post fanout, and auto-disable. Precompile regex replacement patterns once at import. Keep direct "Jewish mode" out of code and document it as requiring manual non-hateful editorial framing.
Rejected Alternatives: A full data-driven mode registry is the right long-term architecture but too broad for a live reliability pass. Image/card generation inside mode transforms would add CPU, memory, and external latency. Implementing a religion/ethnicity caricature mode would be cheap technically but high-risk socially and operationally.
Scalability potential: Low: text-only transforms on the current host. Middle: registry-driven modes with per-mode telemetry. High: optional short visual cards generated by a bounded sidecar. Ultra: separate media worker with cached assets and board-specific mode analytics.
Hardware Impact: Per message, the new modes do a single precompiled regex pass plus a few random choices. No new process, no network, no persistent cache. On weak hardware this is materially safer than image generation; on high-end hardware the same interface can later buy richer visuals through a sidecar.

## Decision 29: Hot copy cache can be 400 because SQLite now owns old-reply truth
Problem: After mode deployment, the bot still held about 409k `message_to_post` entries and 666 `post_to_messages` posts in RAM. This is intentional hot reply/copy cache, but it is still a large Python dict surface on a weak host. Before the old-reply fix, reducing it too far would have broken replies. After indexed `PostCopies` fallback and longer retention, RAM no longer has to be the source of truth.
Solution: Lower `BOT_COPY_CACHE_POST_LIMIT` default from `700` to `400`. Keep durable `PostCopies` retention unchanged. Recent replies remain hot; older replies use the SQLite primary key `(recipient_id, message_id)` and the existing quick-quote fallback.
Rejected Alternatives: Deleting more `PostCopies` would recreate the old-reply bug. Removing `message_to_post` completely would save more RAM but would push every reply lookup through SQLite and needs a separate measured rollout. Rewriting the copy index in C++ now is premature because the Python hot-window knob gave a clear win.
Scalability potential: Low: 400-post RAM hot window. Middle: board-specific hot windows, `/b/` larger than cold boards. High: LRU reverse map separate from edit/delete copy map. Ultra: external/C-backed copy index only after measured SQLite pressure.
Hardware Impact: Runtime snapshot after restart changed from `private_mb=551.47`, `post_to_messages=666`, `message_to_post=409455` to `private_mb=485.86`, `post_to_messages=368`, `message_to_post=212316`. Approximate private memory reduction: `65.61 MB` without reducing DB reply retention.

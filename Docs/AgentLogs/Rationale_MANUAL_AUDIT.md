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

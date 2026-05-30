# dvachbot / site_tgach rationale

Problem: `site_tgach.main` starts `backup_loop(app.state.file_uploader_bot)`, while `site_tgach.backup` sleeps 10h then uploads zipped DB parts every 12h to Telegram admins.
Solution: Disable only the scheduler call in `site_tgach.main`; keep backup implementation importable for manual recovery.
Rejected Alternatives: Editing `backup_loop` or `create_db_backup` was broader than requested and would remove manual backup ability.
Scalability potential: low-tier network stops multi-part DB uploads; mid/high/ultra retain manual backup path without scheduled bandwidth spikes.
Hardware Impact: i3/MX350 avoids periodic SQLite copy, zip compression, disk churn, and Telegram upload traffic from this daemon.

Problem: User needed traffic attribution from this PC.
Solution: Ran 1804s non-admin monitor with 5s adapter byte deltas and TCP owner snapshots. Physical Ethernet total: 214.88 MB. WireGuard `tgach-pc` tunnel total: 195.78 MB inside that physical traffic. Top persistent owner by connection-seconds: `dvachbot` PID 18076 -> Telegram `149.154.166.110:443` at 724.33 connection-minutes. Second: `codex.exe` PID 68656 -> Cloudflare/OpenAI endpoints at 311.42 connection-minutes.
Rejected Alternatives: Packet capture / ETW byte attribution rejected because shell is not elevated; claiming per-process bytes from TCP counts would be false.
Scalability potential: Low/mid/high/ultra not applicable to Windows traffic audit; actionable route is to reduce persistent Telegram workers and avoid scheduled DB uploads.
Hardware Impact: Removing scheduled backup eliminates large 1GB DB zip/upload spikes. Persistent bot traffic remains dominated by Telegram sockets, not site backup scheduler.

ARCHIVE_11 status

[x] Task 1 - Read authority docs and relevant mandates | DOD: batch hygiene rule plus evidence text filter; rejected blind move without structure check; estimate 120000 us.
[x] Task 2 - Inspect Batch010 archive layout | DOD: source/destination shape verified before move; rejected ad hoc flat archive; estimate 90000 us.
[x] Task 3 - Move active Tasks and AgentLogs to Batch011 except CURRENT_BATCH.md | DOD: literal path move with root guard; rejected delete/copy-cleanup workflow; estimate 850000 us.
[x] Task 4 - Generate sanitized md/txt summaries | DOD: extractive compressed text only, chunked under size cap with overlap; rejected binary/log/json ingestion; estimate 2100000 us.
[x] Task 5 - Verify post-move state and append final archive report | DOD: manifest plus source folder scan; rejected chat-only report; estimate 450000 us.

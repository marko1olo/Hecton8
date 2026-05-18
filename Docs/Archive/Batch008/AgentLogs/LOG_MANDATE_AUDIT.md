# LOG_MANDATE_AUDIT

Date: 2026-05-17
Agent: MANDATE_AUDIT
Domain: Docs/Mandates
Evidence class: STATIC_DOC / STATIC_SOURCE

What was wrong:
- The new polish prompt mostly matched existing mandates, but ARM64 runtime struct layout existed only as scattered notes and prompt text.
- `DATA_Inventory_Resources_Items_SOA_Layout.txt` contained a conflicting runtime layout example using `Pack=4` and an ambiguous 20B size.
- Designer-facing Editor/CSV-to-binary bridges were not centralized in the mandate registry.
- Steam Deck/MicroSD I/O pressure was present by implication in MMF/async docs, not as an explicit main-thread I/O ban.
- `Docs/takoi prompt dlya gemini.txt` was mojibake and non-authoritative.

What was done:
- Added `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`.
- Added `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`.
- Updated `.agents-skills/README.md` inventory, read rules, current doctrine, conflict resolution, and additions section.
- Patched `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt` `ItemData` layout to 24B explicit ARM64-safe runtime record.
- Patched `.agents-skills/STRM_Async_Standard.txt` with explicit Steam Deck/MicroSD and no-main-thread-random-I/O rules.
- Replaced `Docs/takoi prompt dlya gemini.txt` with a clean UTF-8 prompt template subordinate to authority.
- Created `Docs/Tasks/Status_MANDATE_AUDIT.md` and `Docs/AgentLogs/Rationale_MANDATE_AUDIT.md`.

Cinematic Cheats used:
- Documentation-level only. The prompt now preserves fake-first guidance: LUTs, shader fakes, squared-distance math, low-cadence proxies, and visual-overkill consumers only outside gameplay truth.

Exact Microseconds saved:
- Measured runtime savings: 0 us. No runtime code was changed and no profiler was run.
- Static future-prevention estimate: PENDING VERIFICATION. Mandates are expected to prevent misaligned ARM64 reads, parser hot paths, and main-thread MicroSD stalls, but no runtime claim is made.

Verification:
- Static docs and registry scans only.
- Unity compile, Unity import, Play Mode, profiler, GCMonitor, player build, memory, VRAM, and visual proof were not run because this was a docs/mandate update.
- `Get-ChildItem .agents-skills -Filter *.txt | Measure-Object` returned `80`.
- `README.md` now reports `Current inventory: 80` and `Mandate files | 80`.
- `rg` scan found no remaining old positive `Pack=4` runtime-layout recommendation in the touched mandate set; remaining hits are explicit `[FORBID]` text.
- `rg` scan confirmed prompt file has no common mojibake marker codepoints `U+00D0` or `U+00E2`.
- `rg` scan confirmed `TOOL_Designer_Facades_CSV_Binary_Bridge.txt` and Steam Deck/MicroSD I/O rules are discoverable through the mandate registry.

---

Date: 2026-05-18
Agent: MANDATE_AUDIT
Domain: Docs/Mandates
Evidence class: STATIC_DOC / STATIC_SOURCE

What was wrong:
- The raw chat prompt was submitted again. It is not authority and still contains a weaker compute-thread simplification than the current mandate registry.

What was done:
- Re-read `Docs/Tasks/Status_MANDATE_AUDIT.md` and `Docs/AgentLogs/Rationale_MANDATE_AUDIT.md`.
- Re-read `Docs/takoi prompt dlya gemini.txt`, `.agents-skills/README.md`, `DATA_Runtime_Struct_Layout_ARM64.txt`, `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`, `GPU_Compute_Warp_Sizing_Mobile.txt`, and `STRM_Async_Standard.txt`.
- Re-scanned mandate count and conflict terms.
- No mandate files required new edits.

Cinematic Cheats used:
- Documentation-level only. Existing fake-first policy remains active: deterministic visual/audio/haptic/UI fakes before physical simulation.

Exact Microseconds saved:
- Measured runtime savings: 0 us. No runtime code was changed and no profiler was run.

Verification:
- `.agents-skills` txt count remains `80`.
- Registry still points to `DATA_Runtime_Struct_Layout_ARM64.txt` and `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`.
- Runtime `Pack=1/Pack=4` hits in mandate registry are explicit `[FORBID]` text, not positive guidance.
- Prompt template uses the stricter `GPU_Compute_Warp_Sizing_Mobile.txt` policy: query group sizes, default 64-thread portable kernels, no 1024-thread mobile/Metal assumption.
- Designer bridge and MicroSD/File I/O requirements remain discoverable.
- Unity compile, Unity import, Play Mode, profiler, GCMonitor, player build, memory, VRAM, and visual proof were not run because this was a docs/mandate audit only.

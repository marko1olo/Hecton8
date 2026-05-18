# Rationale_MANDATE_AUDIT

Date: 2026-05-17
Domain: Docs/Mandates
Evidence class: STATIC_DOC / STATIC_SOURCE

## Decision 1 - ARM64 Runtime DTO Law

Problem: The prompt added strict ARM64 struct alignment language, but the mandate registry only had scattered layout notes and a generic layout-proof rule. One old inventory mandate still suggested `Pack=4` for a runtime item record.

Solution: Added `DATA_Runtime_Struct_Layout_ARM64.txt` and patched `DATA_Inventory_Resources_Items_SOA_Layout.txt` to use 24B explicit padding with the 64-bit UID first.

Rejected Alternatives: Leaving layout rules embedded only in prompt text was rejected because prompt dumps are not authority. Updating every domain mandate was rejected as noisy and unsafe during concurrent agent work.

Scalability potential: Low/MX350/Steam Deck/Quest avoid misaligned runtime reads. Middle/High/Ultra can add richer payloads only with explicit offset proof and padding.

Hardware Impact: Runtime impact of this docs-only patch is 0 us. Expected future gain is fewer ARM64 alignment stalls and fewer false data-sovereignty claims once agents follow the mandate.

## Decision 2 - Designer Bridge Mandate

Problem: The prompt required Editor facades and CSV-to-binary control, but the registry had no central bridge law. Existing docs mention designer tuning in domain files, not as a global enforcement rule.

Solution: Added `TOOL_Designer_Facades_CSV_Binary_Bridge.txt` and linked it from the registry.

Rejected Alternatives: Forcing designers to edit `.h8bin` or requiring runtime JSON/CSV parsing was rejected. Runtime parsers violate zero-GC and I/O mandates.

Scalability potential: Low hardware keeps binary runtime tables. High/Ultra can use richer editor previews without increasing gameplay hot-path cost.

Hardware Impact: Runtime impact of this docs-only patch is 0 us. Future impact is avoiding parser/I/O spikes in gameplay while preserving designer iteration.

## Decision 3 - Prompt File Cleanup

Problem: `Docs/takoi prompt dlya gemini.txt` was mojibake and explicitly marked non-authority. The user-provided polish prompt also contained a GPU thread-group statement weaker than current `GPU_Compute_Warp_Sizing_Mobile.txt`.

Solution: Replaced the damaged dump with a clean UTF-8 prompt template. It is explicitly subordinate to AGENTS.md and `.agents-skills`; compute guidance now follows the stricter 64-thread portable default and queried group-size law.

Rejected Alternatives: Copying the chat prompt verbatim was rejected because it would preserve contradictions and encoding damage. Promoting the prompt itself to authority was rejected by the authority spine.

Scalability potential: Agents get consistent instructions from toaster/MX350 through Ultra without overriding stricter GPU mandates.

Hardware Impact: Runtime impact of this docs-only patch is 0 us. Future impact is fewer oversized compute dispatch assumptions on mobile/Metal-class GPUs.

## Decision 4 - I/O Pressure Wording

Problem: The prompt's Steam Deck MicroSD warning was already mostly covered by async/MMF mandates, but exact main-thread random-access I/O wording was not explicit enough.

Solution: Updated `STRM_Async_Standard.txt` with Steam Deck/MicroSD as baseline, persistent workers, staged blocks, and explicit bans on main-thread random-access file APIs after bootstrap.

Rejected Alternatives: Adding a separate streaming mandate for one paragraph was rejected as registry bloat. Blocking all FileStream usage was rejected because cold editor/build tools and owned workers still need file APIs.

Scalability potential: Low/Steam Deck avoids MicroSD stalls; High/Ultra can use bigger staging buffers without changing gameplay code paths.

Hardware Impact: Runtime impact of this docs-only patch is 0 us. Future impact is eliminating main-thread storage stalls if enforced.

## 2026-05-18 Decision - No New Mandate Patch

Problem: The same raw polish prompt was submitted again. The raw text still contains an outdated GPU simplification that says 256/512 thread groups are mobile-safe, while current registry authority is stricter.

Solution: Kept the existing prompt template and mandates unchanged. `Docs/takoi prompt dlya gemini.txt` already points compute work to `GPU_Compute_Warp_Sizing_Mobile.txt`, which requires `GetKernelThreadGroupSizes`, 64-thread portable default, and capture before larger groups.

Rejected Alternatives: Replacing the cleaned prompt with the raw chat text was rejected because it would reintroduce encoding risk and weaker GPU policy. Editing mandates again was rejected because static scans found no new policy drift.

Scalability potential: Low/MX350/mobile keep conservative kernel groups and staged work. Middle/High/Ultra can scale only after profiler/RenderDoc capture, protecting gameplay determinism and avoiding mobile/Metal regressions.

Hardware Impact: Runtime impact of this re-audit is 0 us. No runtime code changed. Future impact remains policy-level prevention of oversized compute dispatches, misaligned DTOs, hot-path parsers, and main-thread storage stalls.

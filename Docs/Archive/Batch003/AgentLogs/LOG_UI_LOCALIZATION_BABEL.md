# LOG_UI_LOCALIZATION_BABEL

## 2026-05-12 - Zero-GC Localization Babel

What was wrong:
- `LocalizationManager.Instance` existed as a singleton facade.
- Runtime localization registry was char/string-table backed and could not expose raw UTF-8 byte spans.
- TMP font swap refresh pulled managed char-table buffers, not Babel byte spans.
- `{0}` variable tokens were not accepted by the zero-GC numeric formatter.
- Font swaps did not emit a layout rescale signal.
- World-space localized signs did not explicitly repair text bounds on AUP shifts.

What was done:
- Removed `LocalizationManager.Instance`; exposed `IBabelLocalization` through `GlobalRegistry`.
- Added `H8StaticDataArena` raw LocData UTF-8 block/slice APIs.
- Added Babel native UTF-8 blob plus `NativeParallelHashMap<uint,int2>` hash-to-slice LUT.
- Added `TryGetLocalizedSpan(uint,out ReadOnlySpan<byte>)` and TMP char-buffer decode bridge.
- Added visible-hash Burst prefetch and RTL reverse-buffer Burst job contract.
- Added `{0}` parsing and integer injection through `ZeroGCFormatter.FastIntToChars`.
- Added glyph clamp with native `...`, English fallback, and `[MISSING_HASH]`.
- Added `UIRescaleRequestSignal` and signal-driven `DiegeticHudManualLayout` rebuild.
- Added AUP listener path for `LocalizedWorldSign`.
- Added 300-frame Babel telemetry ring with corruption dump path.

Cinematic Cheats used:
- Byte-span localization instead of managed localized strings.
- RTL reverse fake instead of full bidi shaping.
- Signal-driven bounds rebuild instead of per-frame layout polling.
- English fallback hash chain instead of exception-heavy missing-key path.

Exact microseconds saved:
- Hash prefetch: estimated 8-20 us per 100 visible hashes.
- TMP Babel refresh: estimated 10-30 us per staged label batch.
- Native byte LUT versus managed string refresh: estimated 40-120 us per 100 visible labels plus avoided GC.
- StaticDataArena raw span bridge: estimated 15-35 us per static-data lookup batch.
- Layout signal versus polling: estimated 20-60 us per language switch.

Verification:
- `LocalizationManager.Instance` scan: clean.
- Variable injection scan: no `string.Split`; no `new string` in Babel variable path.
- Three compile attempts with `dotnet build Hecton8.Core.csproj --no-restore`: blocked by external BootstrapContracts, Cartography, GlobalSignals, World, and Physics contract errors. No Babel-edited file appears in the reported error set.

---

## 2026-05-13 - Patient Re-Audit / User No-Build Override

What was wrong:
- Static `LocData` from `H8StaticDataArena` could be skipped if managed localization tables were null or empty.
- UTF-8 labels were decoded to full char count before 1024-glyph clamping, allowing bad content to permanently grow the thread-local decode buffer.
- The visible-hash Burst prefetch job completed in the collection path and did not feed the staged label queue.
- `LocalizedWorldSign` still assigned `TMP_Text.text` and used managed uppercase/string fallback during language refresh.

What was done:
- `LocRegistry` now counts and loads monolith `LocData` independently from managed tables.
- UTF-8 truncation now scans to a valid byte boundary before decode, then appends `...` in the same buffer.
- `LocRegistry` now tracks the prefetch read fence so UTF-8 native maps/blobs are not disposed while a prefetch job can still read them.
- `FontStreamingManager` tracks the prefetch `JobHandle`, applies completed `int2` slices into `LabelSwapScheduler`, and prevents stale prefetch slices after reset.
- `LabelSwapScheduler` now stores optional prefetched slices per pending entry and decodes via `TryGetVisualBufferFromUtf8Slice`.
- `LocalizedWorldSign` now caches its key hash, owns cold fallback/display char buffers, writes TMP via `SetCharArray`, and keeps AUP shift refresh behavior.

Cinematic Cheats used:
- UTF-8 byte-span lookup instead of managed strings.
- Native ellipsis clamp instead of layout-driven overflow rebuild.
- Prefetched slice reuse instead of repeated hash-map lookup during staged label drain.
- Sign fallback buffer instead of `TMP_Text.text` assignment.

Exact microseconds saved:
- Estimated 8-20 us per 100 queued localized labels from consuming prefetched offset slices.
- Estimated 10-30 us per sign language refresh by avoiding managed text assignment and uppercase string path.
- Worst-case memory win: bounded decode prevents oversized thread-local buffer expansion from long localized content.

Verification:
- User explicitly forbade `dotnet build`; no build command was launched.
- `git diff --check` passed with line-ending warnings only.
- Targeted `rg` scan found no `LocalizationManager.Instance`, `JsonUtility.FromJson`, `FindObjectOfType`, `targetText.text`, `new string`, `string.Split`, `Encoding.UTF8.GetString`, or `string.Format` in the audited Babel files.
- Status remains PENDING VERIFICATION until Unity import/console/profiler evidence exists.

---

## 2026-05-13 - Native Sentinel / Prefetch Staleness Audit

What was wrong:
- `_utf8Offsets` allocated persistent native hash-map memory but only `_utf8Bytes` was registered with `NativeMemorySentinel`.
- Rapid language changes could abandon a visible-hash prefetch job while leaving its apply flag live, creating a stale-slice risk against a rebuilt swap queue.
- Abandoned prefetch jobs still gated `ProcessSwapBatch`, creating an avoidable label-drain frame delay when their result was no longer needed.

What was done:
- Added `NativeMemorySentinel.RegisterNativeParallelHashMap` and matching unregister for `LocRegistry._utf8Offsets`.
- Added explicit `AbandonVisibleHashPrefetchResults()` in `FontStreamingManager`.
- Changed swap draining to wait only when the in-flight prefetch result still belongs to the active queue.
- Re-extracted the `UI_LOCALIZATION_BABEL` prompt and reran targeted hot-path scans without launching `dotnet build`.

Cinematic Cheats used:
- Explicit ownership bit for prefetch results instead of a heavy cancellation framework.
- Native sentinel pointerless hash-map accounting instead of bespoke memory counters.

Exact microseconds saved:
- Native sentinel registration: 0 runtime us; audit/leak detection correctness.
- Abandoned-prefetch drain bypass: 0-16 ms worst-case UX stall avoided after rapid language toggles; steady-state cost 0 us.
- Hot-path lookup remains 8-20 us saved per 100 queued localized labels when prefetch slices are active.

Verification:
- `git diff --check` completed with line-ending warnings only.
- Targeted `rg` scan found no `LocalizationManager.Instance`, `targetText.text`, `.text =`, `SetText(`, `new string`, `Encoding.UTF8.GetString`, `string.Split`, `string.Format`, or `JsonUtility.FromJson` in the active Babel/TMP handoff files.
- `dotnet build` was not launched per current user override.

Final status: PENDING VERIFICATION.

---

## 2026-05-13 - Static LocData Authored Hash Alias Audit

What was wrong:
- Static `LocData` bytes were resident, but the Babel LUT only registered value-content hashes.
- Data Monolith records store authored hashes (`HashId`, `SpeciesHash`, `BiomeHash`, `ModuleHash`, `ErrorHash`) plus UTF-8 offsets, so authored hash lookups could miss despite valid byte slices.
- A naive indexed alias API would rescan records during reload and scale poorly on a large monolith.

What was done:
- Added `H8StaticLocalizationReference` and `H8StaticLocalizationCursor`.
- Added `H8StaticDataArena.TryGetNextStaticLocalizationReference` for zero-allocation O(n) cursor enumeration over primary static display/message text aliases.
- Updated `LocRegistry.TryLoadStaticArenaUtf8` to register static authored hash aliases into `_utf8Offsets` after copying the static byte block.
- Kept item descriptions out of the alias bridge because the record has no stored description key hash.

Cinematic Cheats used:
- Numeric hash-to-byte-slice aliasing instead of string key reconstruction.
- Caller-owned cursor instead of managed enumeration or temporary lists.
- Primary display-name alias only, avoiding invented field hashes.

Exact microseconds saved:
- Cold reload: avoids O(n^2) indexed rescans for static alias import; steady-frame cost 0 us.
- Runtime static labels: estimated 15-45 us saved per 100 monolith-backed label resolutions by preventing authored hash misses and fallback work.
- GC: 0 B/frame; no managed string or collection allocation added.

Verification:
- `git diff --check` passed on touched files with line-ending warnings only.
- Targeted `rg` scan found no hot-path `new string`, `Encoding.UTF8.GetString`, `string.Split`, `string.Format`, `LocalizationManager.Instance`, `.text =`, `SetText(`, `FindObjectOfType`, or `JsonUtility.FromJson`.
- `dotnet build` was not launched per current user override.

Final status: PENDING VERIFICATION.

---

## 2026-05-13 - Unicode Hash / RTL Consistency Audit

What was wrong:
- Static `LocData` slices were hashed as raw UTF-8 bytes, which only matches the project FNV contract for ASCII.
- Legacy char-table visual RTL calls returned logical order while the Babel UTF-8 path reversed in place.
- `LocalizedWorldSign` trusted upstream display length even if a corrupt buffer length exceeded the backing array.

What was done:
- Added `LocHash.ComputeUtf8AsUtf16`, a zero-allocation UTF-8 scalar reader that hashes identical UTF-16 units to `LocHash.Compute(ReadOnlySpan<char>)`.
- Switched static arena UTF-8 slice registration to the UTF-16-equivalent hash.
- Updated `RTLProcessor.ToVisualOrder` and `TryGetVisualBuffer` to reverse through the existing thread-local buffer.
- Clamped world-sign display length before `TMP_Text.SetCharArray`.

Cinematic Cheats used:
- UTF-8 scalar hash parity instead of allocating a decoded string.
- Thread-local reverse-buffer RTL fake instead of full bidi shaping.
- Defensive buffer-length clamp instead of layout-time failure recovery.

Exact microseconds saved:
- Non-ASCII static LocData: correctness fix, no steady-frame cost.
- RTL legacy path: estimated 5-15 us per short Arabic label versus later corrective shaping/string repair.
- World-sign clamp: 0 steady-frame us; prevents corrupt metadata from reading past char storage.

Verification:
- Re-read Babel and zero-GC mandates plus the extracted `UI_LOCALIZATION_BABEL` prompt.
- `git diff --check` passed on touched Babel files with line-ending warnings only.
- Targeted `rg` scan found no hot-path `new string`, `Encoding.UTF8.GetString`, `string.Split`, `string.Format`, `LocalizationManager.Instance`, `.text =`, `SetText(`, `FindObjectOfType`, or `JsonUtility.FromJson`.
- `dotnet build` was not launched.

Final status: PENDING VERIFICATION.

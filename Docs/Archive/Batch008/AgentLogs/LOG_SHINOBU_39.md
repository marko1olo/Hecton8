# LOG_SHINOBU_39

## 2026-05-18 - SHINOBU_LOCALIZATION_BABEL Zero-GC Subtitle Pass

What was wrong:
- Babel text could still fall back to managed string expansion paths before reaching TextMeshPro.
- UTF-8 decoding depended on managed Encoding buffers in the visual path.
- Subtitle typewriter behavior risked repeated visible text rebuilds instead of using TMP visibility control.
- CharBufferPool had too few slots for localization swap bursts and no active lease telemetry.
- Missing-hash, slow-decode, and corrupt-slice faults lacked a fixed 300-frame Babel blackbox dump.
- Designers lacked an editor facade for reading and saving Babel .h8bin dictionaries and applying CSV overrides.

What was done:
- Added field-only LocalizationEntryDTO, SubtitleCommandDTO, SubtitleStateDTO, BabelFormatArgs, and mock signal DTOs.
- Added span-based UTF-8 to UTF-16 conversion in LocRegistry with malformed byte handling, surrogate pairs, optional rich-tag strip, ^0..^3 integer injection, RTL reverse, truncation, and [ERR_LOC_HASH] fallback.
- Added emergency mock locale path for empty dictionaries: hash(ERROR) maps to MOCK_DATA.
- Expanded CharBufferPool to 500 fixed slots, kept 256-char physical capacity for existing VR text, exposed 128-char Babel target, and tracked active leases with NativeBitArray.
- Routed hash subtitle display through SubtitleCommandDTO fixed ring, CharBufferPool leases, TMP_Text.SetCharArray, and TMP_Text.maxVisibleCharacters.
- Updated LabelSwapScheduler to consume prefetched Babel slices or direct hashes through the span decoder and SetCharArray.
- Added BabelBinarySearchJob and MockTranslationRequestJob as Burst-compatible isolated kernels.
- Added LocalizationManager.SetLanguageAsync as a frame-boundary deferral wrapper around the existing language swap path.
- Added Babel blackbox telemetry ring and binary dumps at Docs/AgentLogs/Dump_SHINOBU_39.bin and Docs/AgentLogs/Dump_BABEL_SYSTEM.bin.
- Added editor-only Babel Localization Manager for .h8bin read/validate/preview/save, CSV override polling, and SceneView GUI.Label preview.

Cinematic cheats used:
- Typewriter reveal is a Dear Lie: full text is uploaded once, then maxVisibleCharacters advances; no per-glyph string rebuild.
- Low/Mx350 text LOD strips rich tags during decode and disables rich-text parsing for subtitles instead of paying TMP formatting cost.
- Missing localization is rendered as a deterministic fixed UTF-8 token, not an exception or allocation path.

Exact microseconds saved:
- Hash subtitle managed string removal: estimated 35-120 us per subtitle on i3/MX350 class CPU, not profiler-measured because Hecton8.Core currently fails on pre-existing dispatcher/input/world contracts.
- Span UTF-8 decode vs Encoding buffer path: estimated 10-80 us per subtitle, 0 managed bytes on the Babel hash path.
- Typewriter maxVisibleCharacters vs per-frame rebuild: estimated 15-60 us per reveal frame.
- Label swap span decode and prefetched slice path: estimated 8-30 us per 18-label drain.
- Integer ^0..^3 injection vs string.Format/interpolation: estimated 20-150 us per dynamic subtitle.
- CharBufferPool burst avoidance: estimated 20-100 us avoided during subtitle/localization bursts, dependent on prior GC pressure.

Compile status:
- dotnet build Hecton8.Core.csproj was attempted three times earlier in the loop and remains blocked before SHINOBU-owned code by pre-existing missing Hecton8.Input.Determinism, dispatcher DTOs, input DTOs, and world streaming DTOs.
- Per compile-wall protocol, no fourth rebuild was run after the polish mandate. Static scans were used for SHINOBU-owned hot-path files.

## 2026-05-18 - Ultra Polish Loop 6

What was wrong:
- The first CharBufferPool implementation was allocation-free for TMP but did not literally own the requested NativeArray<char>[64000] Babel arena.
- SubtitleManager had a zero-GC local SubtitleCommandDTO ring but did not consume the existing GlobalSignals SubtitleSignal NativeQueue lane.
- Locale reload still relied on legacy LocalizationEvents only; it lacked a typed unmanaged SignalBus notification after Babel registry rebuild.

What was done:
- Added NativeArray<char>[64000] Babel arena in CharBufferPool, split into 500 slots of 128 chars.
- Added BabelLease that exposes a native-backed Span<char> for UTF-8 decode and copies at most 128 chars into a preallocated char[] TMP bridge for TMP_Text.SetCharArray.
- Kept the old 256-char lease path for existing VR/HUD users to avoid broad regression.
- Added SubtitleManager drain of SignalBus<SubtitleSignal>.GetFrameSnapshot(), capped at 8 signals per frame, mapped into SubtitleCommandDTO.
- Added LocalizationLanguageChangedSignal and publish it through SignalBus after LocRegistry reload.
- Wrapped BabelLocalizationManagerWindow in #if UNITY_EDITOR explicitly.

Cinematic cheats used:
- Same Dear Lie retained: decode/upload once, reveal with maxVisibleCharacters.
- Low-tier rich text remains stripped in the decoder; no material keyword churn was introduced.

Exact microseconds saved:
- No new profiler proof. The native arena correction preserves 0 B GC but adds a fixed native-to-TMP bridge copy estimated below 3 us for <=128 chars.
- SignalBus subtitle drain is capped at 8 unmanaged structs and estimated below 1 us/frame when the manager is active.

Compile status:
- Full rebuild still intentionally not repeated. The known compile wall is outside SHINOBU_39 ownership.
- Static scans after Loop 6 found no runtime Encoding.GetChars/GetCharCount, TMP .text assignment, SetText, new string, string.Format, ToString, or Pack=1 in SHINOBU-owned runtime files.

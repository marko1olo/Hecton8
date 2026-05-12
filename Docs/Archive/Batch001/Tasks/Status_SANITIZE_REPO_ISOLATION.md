# Status: SANITIZE_REPO_ISOLATION

Source: latest user prompt, 25 tasks. `CURRENT_BATCH.md` was not present.
Status rule: all runtime claims remain `PENDING VERIFICATION` until Unity Editor/player logs confirm them.

## Loop 1: Tasks 1-5
- [x] Task 1 MMF Scalability Profile | DOD: `options.h8cfg` v2 header contains `byte ScalabilityTier` with 0=Low/MX350 and 1=High/RTX | Alternative rejected: JSON-only tier lookup because it forces runtime parsing before platform selection | Estimate: 0us/frame, saves one settings parse on boot/update path.
- [x] Task 2 On-The-Fly Quality Swap | DOD: `IPlatformIntegration.SetScalabilityTier(byte)` persists, applies, and broadcasts `ScalabilityChangedEvent` through dispatcher-flushed `NativeQueue` | Alternative rejected: direct render/VFX service calls because they couple Core to foreign domains | Estimate: 0us idle, <5us on user-triggered tier change with listeners.
- [x] Task 3 Global Settings Persistence | DOD: one cached 64KB `_payloadBuffer` is reused for MMF IO | Alternative rejected: per-save `new byte[]` payload staging | Estimate: avoids 64KB managed allocation per save/load.
- [x] Task 4 Core ASMDEF Isolation | DOD: `Hecton8.Core.asmdef` contains no `Crest`, `MapMagic`, `WaveHarmonic.Crest`, `Den.Tools`, or `Steamworks` references | Alternative rejected: direct Core GUID/reference to SDK asmdefs | Estimate: editor compile isolation only.
- [x] Task 5 ACL001 Compliance Validator | DOD: `HectonComplianceBuildGate : IPreprocessBuildWithReport` calls `ValidateAllContracts(... throwOnFailure: true ...)` and ACL001 scans runtime third-party tokens outside plugin/editor boundaries | Alternative rejected: manual review-only SDK border | Estimate: editor/build only.

## Loop 2: Tasks 6-10
- [x] Task 6 Shim Removal | DOD: `HectonUnderwaterVisuals` resolves `IOceanVisualBridge`; Crest camera ownership is assigned through plugin bridge methods, not Core SDK calls | Alternative rejected: fake Core shim with direct Crest types | Estimate: 0us/frame compared with prior bridge call shape.
- [x] Task 7 Modding Read-Only Seam | DOD: `ModPlayerSpawnedEvent` and `ModBiomeChangedEvent` are `readonly struct`; handlers consume payloads by `in` or copied `ReadOnlySpan<byte>` | Alternative rejected: exposing mutable native buffers to mods | Estimate: prevents buffer write hazards; no hot-path allocation added.
- [x] Task 8 PAL Gamepad Isolation | DOD: `rg "Gamepad.current"` outside Plugins/Editor returned no hits | Alternative rejected: hardware checks in Core gameplay scripts | Estimate: architecture boundary only.
- [x] Task 9 Repository Hygiene | DOD: Cyrillic content scan and path scan across `Assets` and `Docs` returned clean | Alternative rejected: manual folder sampling | Estimate: build portability only.
- [x] Task 10 UTF-8 BOM Removal | DOD: first-party `.cs` BOM scan returned `FIRST_PARTY_CS_BOM_OK` | Alternative rejected: trusting editor encoding defaults | Estimate: build portability only.

## Loop 3: Tasks 11-15
- [x] Task 11 FrostTick Steam Polling | DOD: only `SteamManager.FrostTick()` contains `SteamAPI.RunCallbacks()` | Alternative rejected: 60Hz `Update()` callback drain | Estimate: avoids 60Hz callback overhead; expected random-frame save 1000-2000us when Steam callbacks spike.
- [x] Task 12 Build Versioning | DOD: `BuildInfoPreprocess` writes git hash32; HUD uses `WriteVersionWatermark(char[])` + `SetCharArray`, no `.text =` | Alternative rejected: runtime `TextAsset` parse or formatted string assignment | Estimate: avoids HUD string allocation on version draw.
- [x] Task 13 Build Log Obfuscator | DOD: `BuildLogPathScrubber` uses cached linear scan/char buffer and `[H8_BUILD_MACHINE]` token | Alternative rejected: regex-heavy post-build path scrub | Estimate: editor-only; no runtime cost.
- [x] Task 14 Third-Party Stripping Guard | DOD: build preprocess strips Demo/Demos/Sample/Samples/Documentation/Documentations/Docs under `_ThirdParty` | Alternative rejected: manual package cleanup | Estimate: build-size hygiene only.
- [ ] Task 15 Assembly Reload Optimization | Status: BLOCKED BY EXISTING ASMDEF SCOPE | DOD attempted: asmdef graph inspected | Blocking fact: root runtime/UI scripts still share `Hecton8.Core.asmdef`; moving UI into its own runtime asmdef is a larger ownership migration | Alternative rejected: pretending static inspection proves UI edits cannot recompile Core/Physics | Estimate: editor workflow only.

## Loop 4: Tasks 16-20
- [x] Task 16 PreInitAssetIdMap NativeArray | DOD: `AssetGuidIdRecord` is 16 bytes and `PreInitAssetIdMap` allocates one persistent `NativeArray<AssetGuidIdRecord>` then fills via generated `CopyTo` | Alternative rejected: managed `AssetGuidIdRecord[]` staging table | Estimate: avoids one managed array equal to generated record table size.
- [x] Task 17 Palette Interpolated Strings | DOD: `HectonOceanPalette.cs` scan found no non-ASCII/mojibake, no interpolated strings, and no debug log tokens | Alternative rejected: runtime formatted palette errors | Estimate: 0us/frame.
- [x] Task 18 Logging `string.Format` Replacement | DOD: written build/logging utilities do not use `string.Format`; build scrubber uses linear char copy | Alternative rejected: runtime/editor format churn | Estimate: editor-only, prevents avoidable post-build allocations.
- [x] Task 19 Cached MMF Payload Buffer | DOD: `UserOptionsPersistence` owns one `byte[64K]` `_payloadBuffer` for MMF payload IO | Alternative rejected: new payload buffer per save/load | Estimate: avoids 64KB managed allocation per options write/read.
- [x] Task 20 GeneratedAssetGuidIdTable CopyTo | DOD: generated table exposes `RecordCount` and `CopyTo(NativeArray<AssetGuidIdRecord>)` | Alternative rejected: managed array-only export | Estimate: avoids generated managed table allocation path.

## Loop 5: Tasks 21-25
- [x] Task 21 Steam Callback Debug.Log Strip | DOD: `SteamManager` callback/init path contains no `Debug.Log` tokens | Alternative rejected: callback string logs | Estimate: avoids callback-path string allocation.
- [x] Task 22 Steam Achievement Allocation | DOD: `SteamManager` currently contains no achievement DTO/fetch allocation path; callbacks are raw FrostTick only | Alternative rejected: allocating achievement objects while polling | Estimate: 0us/frame for achievements in this class.
- [x] Task 23 Native Options Loading | DOD: `UserOptionsPersistence` uses MMF/FileStream path and no `File.ReadAllText` | Alternative rejected: full text file load for `options.h8cfg` | Estimate: avoids full-file managed string read path except legacy stream fallback.
- [x] Task 24 PAL Boxing Avoidance | DOD: no `Gamepad.current` leak; platform tier event is a 2-byte readonly struct passed by `in` through typed listeners | Alternative rejected: generic boxed input/platform payloads | Estimate: avoids event payload boxing on scalability changes.
- [x] Task 25 CI Validator Execution | DOD: validator is wired to build preprocess and `Hecton8.Editor.csproj` compiled after rerun | Alternative rejected: menu-only validation | Estimate: editor/build only.

## Verification Notes
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal`: succeeded, 1 third-party obsolete warning, 0 errors.
- `dotnet build Hecton8.Plugins.csproj`: blocked because Unity did not generate `Hecton8.Plugins.csproj`; `Hecton8.Plugins.asmdef` was statically verified instead.
- Static scans clean: Cyrillic content/path, first-party `.cs` BOM, forbidden Crest/MapMagic/Steamworks runtime tokens outside Plugins/Editor, and `Gamepad.current` outside Plugins/Editor.
- Boundary debt found outside the requested Crest/MapMagic/Steamworks pass: Core still has direct `GPUInstancer`/`VLB` runtime references in rock/scatter/flashlight/underwater visual files. I did not migrate those because they require separate adapter ownership and prefab field migration.

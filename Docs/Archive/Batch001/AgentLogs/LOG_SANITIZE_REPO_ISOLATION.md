# LOG: SANITIZE_REPO_ISOLATION

Source: latest user prompt. `CURRENT_BATCH.md` was not present.

## 2026-05-11 - Scalability Persistence + Border Verification

What was wrong:
- `options.h8cfg` had no first-class scalability tier for Low/MX350 vs High/RTX profile selection.
- Runtime tier change had no typed platform integration API or Core event lane for Render/VFX listeners.
- The Core/Input assembly direction required a shared contract; putting the interface only in Core broke the Input assembly.
- The requested Crest/MapMagic/Steamworks border needed evidence, not claims.

What was done:
- Added `ScalabilityTierProfiles`, `IPlatformIntegration`, and `PlatformIntegrationBridge` to `Hecton8.Bootstrap.Contracts`.
- Implemented `UserOptionsPersistence.SetScalabilityTier(byte)` with normalization, MMF persistence, Core apply bridge, and typed broadcast.
- Expanded `options.h8cfg` to a 16-byte MMF header: magic, version, payload length, `byte ScalabilityTier`, three reserved bytes.
- Reused the existing fixed 64KB `_payloadBuffer` for MMF payload IO; no per-read/write payload byte array was introduced.
- Added `ScalabilityChangedEvent` as a 2-byte readonly struct and a bounded `NativeQueue` event lane drained by `SystemDispatcher`.
- Wired `GlobalRegistry` to resolve/apply the persisted scalability override and raise the event without direct VFX/render references.
- Updated `Status_SANITIZE_REPO_ISOLATION.md` and `Rationale_SANITIZE_REPO_ISOLATION.md` with five loops, DOD, alternatives, and estimates.

Cinematic cheats used:
- Settings tier is a one-byte deterministic selector instead of runtime hardware re-evaluation on every quality change. Estimated save: 0us/frame idle, avoids a settings parse on change.
- Scalability notification is a 2-byte readonly event in a bounded NativeQueue instead of C# event captures or service graph traversal. Estimated save: allocation-free change path, <5us with small listener count.
- Steam callbacks remain on FrostTick, not render Tick. Estimated saved spike: 1000-2000us on frames that previously drained Steam callbacks at 60Hz.
- Build watermark uses integer hash + char buffer + `SetCharArray`, not `.text =` string assignment. Estimated save: avoids one HUD string allocation per draw.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`: succeeded, 0 warnings, 0 errors.
- `dotnet build Hecton8.Editor.csproj --no-restore -v:minimal`: succeeded, 1 third-party obsolete warning, 0 errors.
- Cyrillic content scan across `Assets` and `Docs`: clear.
- Cyrillic path scan across `Assets` and `Docs`: clear.
- First-party `.cs` BOM scan under `Assets/_Project/Scripts`: clear.
- Forbidden Crest/MapMagic/Steamworks runtime token scan outside Plugins/Editor: clear.
- `Gamepad.current` scan outside Plugins/Editor: clear.
- `SteamAPI.RunCallbacks()` scan: only in `Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs` inside `FrostTick()`.

Blocked / pending:
- `Hecton8.Plugins.csproj` is not generated in this workspace, so Plugins could not be built as a standalone csproj. `Hecton8.Plugins.asmdef` was statically verified: it references `Hecton8.Core`, MapMagic, Crest, and WaveHarmonic SDKs.
- Broader third-party Core leakage still exists for `GPUInstancer` and `VLB` in Core runtime files. I did not remove those because doing it correctly needs separate adapters and serialized prefab migration.
- Assembly reload optimization remains blocked by root asmdef scope: UI runtime files still live inside `Hecton8.Core.asmdef`.

Final diff:
- Tracked diff for this pass: `InputBindingServiceContracts.cs`, `GlobalRegistry.cs`, `SystemDispatcher.cs`, `UserOptionsPersistence.cs` = 251 insertions, 13 deletions.
- New files: `Assets/_Project/Scripts/Core/IPlatformIntegration.cs`, `.meta`, `Docs/Tasks/Status_SANITIZE_REPO_ISOLATION.md`, `Docs/AgentLogs/Rationale_SANITIZE_REPO_ISOLATION.md`, this log.

STATUS: PENDING VERIFICATION

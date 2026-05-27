# Sample Mod Spec - Infinite O2

Date: 2026-05-19
Status: ENVELOPE-ONLY SAMPLE_SPEC / NO_RUNTIME_AUTHORITY

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Owner domain: Modding API static contract

## 2026-05-19 Envelope-Only Override

The managed sample below is retained only as a historical source-audit pattern. In current envelope-only UGC mode, this package shape is rejected because it declares `EntryAssembly` and `EntryType`.

The current SDK-safe Infinite O2 sample is:

- author a setting in the Workbench;
- persist the setting in package/mod-owned metadata when a safe save path is approved;
- emit no gameplay mutation until an engine-owned `SurvivalOverride` envelope opcode exists;
- if the future opcode exists, emit a bounded TTL `FutureCommandEnvelope`;
- accept rejection and show the mod as inactive when the engine refuses the packet.

There is still no public oxygen mutation authority.

## Source-Backed Boundary

Current public API status: true Infinite O2 cannot mutate gameplay yet. There is no approved `SurvivalOverride` command opcode or `PlayerSurvival` mod command target in the current source.

This sample is therefore a safe pattern:

- store a mod-owned toggle;
- subscribe to read-only projected events;
- listen for command rejections;
- do not mutate player physiology, DataVault, save truth, `GameObject`, or `Transform`;
- wait for a future engine-owned survival command kernel before affecting oxygen truth.

## Manifest

```json
{
  "Id": "com.example.infinite_o2",
  "Name": "Infinite O2 Sample",
  "Version": "0.1.0",
  "Author": "HECTON-8 Sample",
  "Dependencies": [],
  "EntryAssembly": "com.example.infinite_o2.dll",
  "EntryType": "ExampleMods.InfiniteO2Mod",
  "RequiredAPIVersion": 2,
  "ModPriority": 0
}
```

## Managed Entry

```csharp
using Hecton8.Modding;

namespace ExampleMods
{
    public sealed class InfiniteO2Mod : IHectonVersionedMod
    {
        private const string ModId = "com.example.infinite_o2";
        private const string SettingName = "infinite_o2";
        private const string SaveKey = "com.example.infinite_o2.enabled";

        private bool _enabled;
        private HectonEventSubscription _projectionSub;
        private HectonEventSubscription _rejectSub;

        public int RequiredAPIVersion => 2;

        public void OnLoad()
        {
            _enabled = HectonAPI.SaveState.GetModString(SaveKey, "0") == "1";
            HectonAPI.UI.RegisterSetting(ModId, SettingName, _enabled, OnToggle);
            _projectionSub = HectonAPI.Events.SubscribeProjected(OnProjectedEvent, ModId);
            _rejectSub = HectonAPI.Events.Subscribe<ModInteractionRejectedPayload>(OnRejected, ModId);
        }

        public void OnInitialize()
        {
        }

        public void OnUnload()
        {
            _projectionSub?.Dispose();
            _rejectSub?.Dispose();
            _projectionSub = null;
            _rejectSub = null;
        }

        private void OnToggle(bool enabled)
        {
            _enabled = enabled;
            HectonAPI.SaveState.SetModString(SaveKey, enabled ? "1" : "0");
        }

        private void OnProjectedEvent(ModEventDto dto)
        {
            if (!_enabled)
                return;

            // Current API has no SurvivalOverride opcode. A real oxygen effect must wait
            // for an engine-owned command kernel that clamps, expires, logs, and rejects.
        }

        private void OnRejected(in ModInteractionRejectedPayload payload)
        {
            if (payload.ModHash == 0u)
                return;

            HectonAPI.UI.ShowWarning("Infinite O2 request rejected by engine authority.");
        }
    }
}
```

## Forbidden In This Sample

- No reflection.
- No `GlobalRegistry` access.
- No `GameObject`, `Transform`, `MonoBehaviour`, prefab, material, texture, or audio clip reference.
- No `SignalBus<T>`, `NativeQueue`, `NativeArray`, or DataVault handle.
- No direct player oxygen, physiology, inventory, or save truth write.
- No JSON event payloads.

## Future Kernel Required

Before this sample can affect gameplay, a separate owning system must implement and audit:

- `ModCommandOpcode.SurvivalOverride`
- target system `PlayerSurvival`
- max TTL 3 seconds
- oxygen floor clamp in engine code
- save exclusion for transient oxygen override
- accepted/rejected telemetry
- revocation on mod unload or quarantine
- Unity runtime proof through `Runtime_Verification_Playbook.md`

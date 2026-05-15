# Hardware Profile Guards

Cold-path validation tools for hardware profile data. These scripts do not run
Unity, do not compile C#, and do not prove Play Mode, profiler, or GCMonitor
behavior.

## Primary Command

```powershell
python -B Tools/Hardware/ValidateAllHardwareProfiles.py --check-report
```

This validates:
- `Data/Hardware/Profiles.json` against `HardwareProfileCatalog.cs` and related runtime call sites.
- `Data/System/Hardware_Profiles.json` against H8 profile invariants.
- `Docs/AgentLogs/Hardware_Profile_Audit_H8_HARDWARE_TIER_MATRIX_BKR.json`.
- `Docs/AgentLogs/Hardware_Profile_All_Guards_H8_HARDWARE_TIER_MATRIX_BKR.json`.

## Report Refresh

Run this only after intentionally changing hardware profile data and updating
rationale/status logs in the same patch:

```powershell
python -B Tools/Hardware/ValidateSystemHardwareProfiles.py --write-report
python -B Tools/Hardware/ValidateAllHardwareProfiles.py --write-report
python -B Tools/Hardware/ValidateAllHardwareProfiles.py --check-report
```

## Test Commands

```powershell
python -B Tools/Hardware/test_validate_system_hardware_profiles.py -v
python -B Tools/Hardware/test_validate_all_hardware_profiles.py -v
python -B -m py_compile Tools/Hardware/ValidateAllHardwareProfiles.py Tools/Hardware/ValidateSystemHardwareProfiles.py Tools/Hardware/test_validate_all_hardware_profiles.py Tools/Hardware/test_validate_system_hardware_profiles.py
```

## Acceptance Notes

- H8 system profile count must remain four: `PC_High`, `SteamDeck_Mid`, `Quest2_Low`, `Quest3_LowPlus`.
- Quest2 committed budget plus reserve must remain `4096 MB` or lower.
- Runtime hot-path impact from these tools is `0 microseconds/frame`.
- Runtime GC impact from these tools is `0 B/frame`.
- Unity import, Play Mode, profiler, and GCMonitor remain separate verification gates.

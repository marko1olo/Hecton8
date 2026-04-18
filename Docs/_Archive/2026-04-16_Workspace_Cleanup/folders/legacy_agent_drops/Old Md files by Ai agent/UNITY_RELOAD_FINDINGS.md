**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

# Unity Reload Findings

- Generated: 2026-03-30 22:45:57
- Source: C:\Users\danat\AppData\Local\Unity\Editor\Editor.log
- Tail lines analyzed: 8000

## Summary

- Domain reload samples: 7
- Domain reload max: 153476 ms
- Domain reload avg: 130760 ms
- Asset refresh samples: 42
- Asset refresh max: 338.952 s
- Asset refresh avg: 24.901 s

## Top Expensive Reload Steps

- `FinalizeReload`: max 146739 ms, avg 125671 ms, seen 7x
- `SetupLoadedEditorAssemblies`: max 66877 ms, avg 52362 ms, seen 7x
- `AwakeInstancesAfterBackupRestoration`: max 60221 ms, avg 46427 ms, seen 7x
- `ProcessInitializeOnLoadAttributes`: max 34922 ms, avg 30071 ms, seen 7x
- `BeforeProcessingInitializeOnLoad`: max 26385 ms, avg 17261 ms, seen 7x
- `BeginReloadAssembly`: max 5548 ms, avg 4082 ms, seen 7x
- `ProcessInitializeOnLoadMethodAttributes`: max 4668 ms, avg 4095 ms, seen 7x
- `CreateAndSetChildDomain`: max 1384 ms, avg 1242 ms, seen 4x
- `InitializePlatformSupportModulesInManaged`: max 1049 ms, avg 1049 ms, seen 1x
- `LoadAllAssembliesAndSetupDomain`: max 1020 ms, avg 1014 ms, seen 2x

## Reading

- If `CompileScripts` is small but `SetupLoadedEditorAssemblies` and `ProcessInitializeOnLoadAttributes` are large, the bottleneck is editor reload work, not plain script compilation.
- If `AwakeInstancesAfterBackupRestoration` is large, edit-mode objects and editor-time scene state are taking too long to wake back up after reload.
- If asset refresh spikes into triple digits, import/refresh behavior and package editors may also be contributing.

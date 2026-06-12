# Audio Synthesis Apex Line Audit 1308

Date: 2026-05-25
Domain requested: `Assets/Project/Scripts/Audio/Synthesis`
Actual audited path: `Assets/_Project/Scripts/Audio/Synthesis`
Build policy: no full build in this pass. Apex Loop 8 used only no-build Roslyn after CPU gate cleared to 29.72% and no `dotnet`/`csc` process was active. Apex Loop 9 did not run dotnet/Roslyn because CPU gate reported 96.57%.

## Runtime Forbidden Token Scan

Scope: six non-editor runtime files under `Assets/_Project/Scripts/Audio/Synthesis`.

Result after Apex Loop 9: zero hits for direct `new`, `string.Format`, `.ToString(`, `System.Linq`, `foreach`, interpolation, string concat, `.Complete(`, scene search, `GetComponent<`, `GetComponents<`, `StartCoroutine`, `Debug.Log`, `H8Debug.Log`, `throw new`, `NativeList<`, `NativeHashMap<`, `NativeQueue<`, `UnsafeList<`, and `AddComponent<`.

Residual cold managed surfaces:

| File | Line | Surface | Disposition |
| --- | ---: | --- | --- |
| `VocalBankPlaybackRuntime.cs` | 976 | `catch (Exception)` | Cold bank file load fail-closed return. No managed log. |
| `VocalBankPlaybackRuntime.cs` | 1090 | `catch (Exception)` | Cold dialogue CSV parse fail-closed. No managed log. |
| `VocalBankPlaybackRuntime.cs` | 1332 | `catch (Exception)` | Cold binary dump fail-closed. No managed log. |
| `DynamicMusicGranularSynthesizer.cs` | 542 | `catch (Exception)` | Cold preset CSV parse fail-closed return. No managed log. |
| `DynamicMusicGranularSynthesizer.cs` | 1585 | `catch (Exception)` | Cold telemetry dump fail-closed. No managed log. |

Authored bootstrap route after Apex Loop 9:

| File | Lines | Proof |
| --- | ---: | --- |
| `VocalBankPlaybackRuntime.cs` | 197-204 | Runtime now accepts only an existing `VocalBankPlaybackRuntime` on the player `AudioListener` host; absent component returns fail-closed. No `AddComponent`. |
| `DynamicMusicGranularSynthesizer.cs` | 363-367 | Runtime now accepts only an existing `DynamicMusicGranularSynthesizer` on the player `AudioListener` host; absent component returns fail-closed. No `AddComponent`. |
| `Assets/_Project/Prefabs/Player.prefab` | 139-140, 374-405 | Player camera/listener prefab now owns the vocal and dynamic synth components plus the existing `AudioSource`, preserving synthesis without runtime component allocation. |

## Vault Lock / Phase-Local View Proof

| File | Lines | Proof |
| --- | ---: | --- |
| `VocalBankPlaybackRuntime.cs` | 336-427 | `OnAudioFilterRead` zeros output on invalid state, acquires views with `TryAcquireAudioCallbackViews`, derives raw pointers only inside callback scope, releases locks in `finally`. |
| `VocalBankPlaybackRuntime.cs` | 429-457 | Audio callback view acquisition uses same `IDataVault` instance and validates capacities before DSP. |
| `DynamicMusicGranularSynthesizer.cs` | 687-735 | `OnAudioFilterRead` locks only the ready output buffer for the copy window and releases in `finally`. |
| `DynamicMusicGranularSynthesizer.cs` | 952-980 | `TryResolveSynthPublishViews` resolves publish views through the locked vault, not current `_dataVault`. |
| `DynamicMusicGranularSynthesizer.cs` | 1301-1404 | Scheduled job acquires DataVault write locks, passes only transient pointers into jobs, and transfers lock ownership to `_synthJobLockedVault` only after schedule succeeds. |
| `DynamicMusicGranularSynthesizer.cs` | 1408-1431 | Completed job finalization publishes, then releases outstanding locks in `finally`. |
| `DynamicMusicGranularSynthesizer.cs` | 1458-1481 | Publish rejects unless required lock bits are present and writes telemetry/shared state only through locked-vault views. |

## Editor Validator Closure

| File | Lines | Proof |
| --- | ---: | --- |
| `AudioSynthesisMemorySovereigntyValidator.cs` | 60-96 | Validator now scans all non-editor synthesis runtime files, not just vocal runtime. |
| `AudioSynthesisMemorySovereigntyValidator.cs` | 113-150 | Validator line-scans runtime forbidden tokens and skips comment-only lines. |
| `AudioSynthesisMemorySovereigntyValidator.cs` | 800-835 | `GenerateMockSynthesisLoadJob` writes deterministic telemetry/waveform/counter samples. |

Editor validator expected shell counts after patch:

| Metric | Count |
| --- | ---: |
| Runtime files | 6 |
| Runtime forbidden token hits | 0 |
| Cold `AddComponent<` calls | 0 |
| Cold `catch (Exception)` branches | 5 |
| Broad mutable view symbols | 0 |

## ARM64 / DTO Proof

Full map: `Docs/Reports/AUDIO_SYNTHESIS_DTO_OFFSET_MAP_1308.md`.

Source validator anchors:

| File | Lines | Proof |
| --- | ---: | --- |
| `AudioSynthesisMemorySovereigntyValidator.cs` | 85-345 | Exact `UnsafeUtility.SizeOf` and `UnsafeUtility.OffsetOf` assertions for vocal and dynamic music DTOs. |
| `VocalStateLayoutValidator.cs` | 74 | Size assertion helper uses `UnsafeUtility.SizeOf<T>()`. |

Layout text scans:

| Scan | Result |
| --- | --- |
| `LayoutKind.Sequential` under synthesis | 0 hits |
| Public padding fields under synthesis | 0 hits |
| Private padding `nameof`/dot access from editor | 0 hits |

## AUP Formula

| File | Line | Formula |
| --- | ---: | --- |
| `HullStressGranularDspKernel.cs` | 340 | `AupPrecisionMath.LocalDeltaFloat3Clamped(voice.EpicenterAUP, block.ListenerAUP, ...)` |
| `AupPrecisionContracts.cs` | 63-65 | `LocalDeltaDouble(targetAup, observerAup) => targetAup - observerAup` in double precision. |
| `AupPrecisionContracts.cs` | 81-83 | Downcast/clamp occurs only after the local double delta exists. |

No direct absolute AUP-to-float cast was found in synthesis runtime scan.

## Assembly / Dependency Boundary

Runtime asmdef: `Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef`.

Allowed references: `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, `Unity.Burst`, `Unity.Collections`, `Unity.Jobs`, `Unity.Mathematics`.

Editor asmdef: `Assets/_Project/Scripts/Audio/Synthesis/Editor/Hecton8.Audio.Synthesis.Editor.asmdef`.

Editor-only added references: `Unity.Burst`, `Unity.Jobs`; `allowUnsafeCode=true` for validator pointer probes.

No `HectonEventBus`, `GlobalSignals`, `TryGetLatestCreated`, scene search, `Camera.main`, or hot `GetComponent<` surface was found in runtime synthesis.

## Current Blockers

| Blocker | Fact |
| --- | --- |
| Runtime GC proof | Unity validator not executed. No `GC.GetAllocatedBytesForCurrentThread` artifact exists yet. |
| Roslyn hash | Apex Loop 8 hash is `6bfbc5b6a59b0a7a9107c097b50ebde636dabcbf04035100030766333bb78174`, but Apex Loop 9 changed runtime C# after that report. Rerun is pending CPU/process gate. |
| Compile | Last full build failed outside synthesis in `AcousticPortalPropagation.cs` and `TetherInstance.cs`; no synthesis compile error was emitted in that pass. |

## Apex Loop 9 Bootstrap Excision

Static commands:

| Scan | Result |
| --- | --- |
| `rg "AddComponent<" Assets/_Project/Scripts/Audio/Synthesis --glob runtime` | 0 hits |
| `rg --pcre2 "\bnew\b" Assets/_Project/Scripts/Audio/Synthesis --glob runtime` | 0 hits |
| runtime managed formatting/LINQ/scene-search/debug/native-list scan | 0 hits |
| `git diff --check` for changed synthesis/runtime prefab files | pass; CRLF warnings only |

Dotnet/Roslyn rerun: not executed. Gate: CPU 96.57%, no `dotnet`/`csc` process active.

## Apex Loop 10 Prefab Sanity And Runtime Reaudit

Prefab YAML proof:

| File | Lines | Proof |
| --- | ---: | --- |
| `Assets/_Project/Prefabs/Player.prefab` | 126 | Root GameObject fileID is `2193605564943894971`. |
| `Assets/_Project/Prefabs/Player.prefab` | 139-140 | Root component list references `7348103083027130801` and `7348103083027130802`. |
| `Assets/_Project/Prefabs/Player.prefab` | 225-284 | `AudioListener` and `AudioSource` both bind to GameObject `2193605564943894971`. |
| `Assets/_Project/Prefabs/Player.prefab` | 374-389 | `VocalBankPlaybackRuntime` component binds to GameObject `2193605564943894971`, script GUID `acfd32c0ce821314fbd470b2ff615f5d`. |
| `Assets/_Project/Prefabs/Player.prefab` | 390-405 | `DynamicMusicGranularSynthesizer` component binds to GameObject `2193605564943894971`, script GUID `265100ac4b374295a6b78bb7a4753f2e`. |
| `VocalBankPlaybackRuntime.cs.meta` | 2 | GUID matches prefab vocal component. |
| `DynamicMusicGranularSynthesizer.cs.meta` | 2 | GUID matches prefab dynamic component. |

AGENTS root check:

| Command | Result | Disposition |
| --- | ---: | --- |
| `Get-Content Player.prefab | Select-String m_RootGameObject -Quiet` | `False` | `Player.prefab` is prefab YAML, not scene YAML; targeted FileID/GUID proof above is the valid ownership check. |

Static runtime commands:

| Scan | Result |
| --- | ---: |
| Runtime file list built via `rg --files` with explicit `/Editor/` exclusion | 6 files |
| Direct `new` | 0 hits |
| `string.Format`, `.ToString(`, LINQ, `foreach`, interpolation, string concat | 0 hits |
| `.Complete(`, scene search, `GetComponent<`, `AddComponent<`, `StartCoroutine` | 0 hits |
| `Debug.Log`, `H8Debug.Log`, `throw new` | 0 hits |
| `NativeList<`, `NativeHashMap<`, `NativeQueue<`, `UnsafeList<` | 0 hits |

Dotnet/Roslyn rerun: not executed. Gates immediately before rerun attempts: CPU 66%, then 61%, no `dotnet`/`csc` process active.

## Apex Loop 11 Audio Driver Allocation Excision

Runtime managed object factory proof:

| File | Lines | Proof |
| --- | ---: | --- |
| `DynamicMusicGranularSynthesizer.cs` | 265 | `_driverClip` is now a serialized authored `AudioClip` reference. |
| `DynamicMusicGranularSynthesizer.cs` | 1090-1093 | `ConfigureAudioHostCold` only assigns the authored clip and returns fail-closed when no clip exists. |
| `Assets/_Project/Prefabs/Player.prefab` | 406 | `_driverClip` points to GUID `0d1a03d1d70c9dd448ad1fbab16de520`. |
| `Assets/_Project/Audio/Underwater Ambient.wav.meta` | 2 | GUID `0d1a03d1d70c9dd448ad1fbab16de520` exists as an authored asset. |
| `AudioSynthesisMemorySovereigntyValidator.cs` | 142-144 | Validator now fails runtime source purity on `AudioClip.Create`, `Resources.Load`, or `Instantiate(`. |

Static runtime commands:

| Scan | Result |
| --- | ---: |
| `AudioClip.Create` | 0 hits |
| `Destroy(_driverClip)` | 0 hits |
| `Resources.Load` / `Instantiate` | 0 hits |
| Direct `new`, `AddComponent<`, `GetComponent<`, scene search | 0 hits |
| Formatting/LINQ/foreach/string concat/interpolation/.Complete/log/throw | 0 hits |

Dotnet/Roslyn rerun: not executed. Gates after this source edit: CPU 96%, then 99%, then 100%; no `dotnet`/`csc` process active.

## Apex Loop 12 Validator Metadata And Static Reaudit

Unity asset metadata proof:

| File | Lines | Proof |
| --- | ---: | --- |
| `Assets/_Project/Scripts/Audio/Synthesis/Editor/AudioSynthesisMemorySovereigntyValidator.cs.meta` | 1-2 | Unity `.meta` exists for the new editor validator; GUID `e5bd4741781444dc8da11767858c388e`. |

Static runtime commands:

| Scan | Result |
| --- | ---: |
| Runtime file list built via `rg --files` with explicit `/Editor/` exclusion | 6 files |
| Forbidden runtime tokens excluding deliberate numeric math | 0 hits |
| String concatenation literal scan | 0 hits |
| `AudioClip.Create`, `Destroy(_driverClip)`, `Resources.Load`, `Instantiate` | 0 hits |
| Direct `new`, `AddComponent<`, `GetComponent<`, scene search | 0 hits |
| Formatting/LINQ/foreach/interpolation/.Complete/log/throw | 0 hits |
| Current Roslyn JSON summary path | `summary.parseFailures=0`, `summary.forbiddenPersistentCandidates=0`, `summary.forbiddenMonoBehaviourCandidates=0` |
| Boxing/managed-collection suspicion scan | 4 expected hits: `object` parameters in GlobalRegistry replacement callbacks (`VocalBankPlaybackRuntime.cs:319`, `DynamicMusicGranularSynthesizer.cs:667-668`) and static `string.Equals` in cold CSV reload state (`DynamicMusicGranularSynthesizer.cs:1781`). No `params`, casts to `object`, `IEnumerable`, `IEnumerator`, delegate allocation, `Activator`, `Array.Resize`, or `GC` hits. |

Dotnet/Roslyn rerun: not executed. Gates after this source edit: CPU 100%, then 87%; no `dotnet`/`csc` process active.

## Apex Loop 8 Roslyn Rerun

Command: `dotnet run --no-build --project Tools/VaultNativeAliasRoslynAudit/VaultNativeAliasRoslynAudit.csproj -- --repo . --root Assets/_Project/Scripts/Audio/Synthesis --output Docs/Reports/VAULT_EXORCISM_REPORT_1308.json --agent-id 1308`

Gate: CPU 29.72%, no `dotnet`/`csc` active.

Result:

| Metric | Count |
| --- | ---: |
| Scanned files | 10 |
| Parse failures | 0 |
| Total native field declarations | 65 |
| Forbidden persistent candidates | 0 |
| Forbidden MonoBehaviour candidates | 0 |
| Job transient fields | 47 |
| Stack-only ref struct view fields | 18 |

Hash: `6bfbc5b6a59b0a7a9107c097b50ebde636dabcbf04035100030766333bb78174`.

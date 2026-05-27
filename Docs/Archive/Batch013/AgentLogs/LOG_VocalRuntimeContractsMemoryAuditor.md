Vocal Runtime / Contracts Memory Audit

What was wrong: Static scan found audio callback write-lock scope across decode, cold file I/O/catch/log sites, path string construction in Tick-triggered dump path, and unchecked integer/ulong arithmetic edges in bank parsing/decode paths.
What was done: Read AGENTS, relevant mandates, domain doc, and line-numbered source for VocalBankPlaybackRuntime.cs and VocalBankContracts.cs. No source files edited.
Cinematic Cheats used: None. Audit only.
Exact Microseconds saved: 0 measured. Static risk reduction only; profiler/GCMonitor proof absent.

Concrete findings:
- VocalBankPlaybackRuntime.cs:337-427: OnAudioFilterRead takes DataVault write locks for state/codec/telemetry/counters/waveform/bank and holds them across decode. This is fail-closed for release safety, but it violates audio-thread DataVault lock minimality and can stall relocation/ownership windows.
- VocalBankPlaybackRuntime.cs:350-356 and 423-426: Interlocked in-flight tracking is paired with finally. Fail-closed path zeros output on failed acquire.
- VocalBankPlaybackRuntime.cs:419-420 and 300-302: audio callback requests blackbox dump when DSP exceeds 1000 us; Tick later performs DumpBlackboxCold. Dump path is not audio-thread direct, but it is Tick-triggered file I/O.
- VocalBankPlaybackRuntime.cs:917, 1068, 1304-1307: string path construction and Directory/File calls exist in cold/file dump paths. They are not in OnAudioFilterRead, but DumpBlackboxCold is called from Tick after _dumpRequested.
- VocalBankPlaybackRuntime.cs:977-980, 1092-1095, 1335-1338: catch(Exception) plus H8Debug.LogWarning. Cold/error path only. Still managed exception/log surface.
- VocalBankPlaybackRuntime.cs:984-989: BeginBankMutationCold spin-waits until audio callback in-flight count reaches zero. Forbidden on audio thread; current call sites are bank mutation/teardown, but this is a main-thread stall risk.
- VocalBankPlaybackRuntime.cs:21-30: VocalVaultViews is a ref struct holding transient NativeArray aliases only. No persistent NativeArray field found in runtime class; persistent fields are VaultGenerationHandle only at 78-87.
- VocalBankPlaybackRuntime.cs:574-595 and 603-630: DataVault locks release through lockMask; acquire failure releases partial locks. Runtime callback/control/build/init/csv callers use finally or immediate release on failed acquire.
- VocalBankContracts.cs:31-163: DTOs use explicit layout sizes 16/32/64, no bool/string/reference fields, sizes are multiples of 8. No Pack=1/4 found. ARM64 size rule statically satisfied.
- VocalBankContracts.cs:85-92: VocalStateDTO padding fields are named Pad0..Pad7, not _pad0 style from mandate. Alignment is intact; naming contract gap only.
- VocalBankContracts.cs:233-234, 243, 273-276, 313-315, 555, 608, 612, 627, 671, 691, 707-708: arithmetic is mostly range-guarded, but several additions/multiplications are unchecked. Highest risk: codecRef.PayloadOffset + codecRef.PayloadByteLength can wrap before comparison at 555; candidate.ByteOffset + ByteLength can wrap at 273; header.HeaderSize + indexBytes can wrap at 234.
- VocalBankContracts.cs:620-625 and 768-770 plus VocalBankPlaybackRuntime.cs:1273-1274, 1341-1344: non-finite handling exists for final sample and quality/fallback paths. Filter internal state is not explicitly scrubbed if LowState/BandState become non-finite before final output.
- No LINQ, foreach, ToString, boxing/IEnumerable iteration, naked Debug.Log, local new NativeArray/List/Queue, or managed new in the assigned files by static pattern scan.

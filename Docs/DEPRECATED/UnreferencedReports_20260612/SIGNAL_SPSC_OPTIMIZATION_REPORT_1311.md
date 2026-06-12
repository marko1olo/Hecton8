# SIGNAL SPSC OPTIMIZATION REPORT 1311

Status: GREEN_STATIC_ONLY

## Status Reasons
- No red token found by static scan. Runtime/profiler proof still absent.

## Byte Offset Maps
- SignalRingCursorState: size=128 multiple8=True file=Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs
  - offset 0: long Head (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:14)
  - offset 8: ulong _headPad0 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:15)
  - offset 16: ulong _headPad1 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:16)
  - offset 24: ulong _headPad2 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:17)
  - offset 32: ulong _headPad3 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:18)
  - offset 40: ulong _headPad4 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:19)
  - offset 48: ulong _headPad5 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:20)
  - offset 56: ulong _headPad6 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:21)
  - offset 64: long Tail (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:22)
  - offset 72: ulong _tailPad0 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:23)
  - offset 80: ulong _tailPad1 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:24)
  - offset 88: ulong _tailPad2 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:25)
  - offset 96: ulong _tailPad3 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:26)
  - offset 104: ulong _tailPad4 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:27)
  - offset 112: ulong _tailPad5 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:28)
  - offset 120: ulong _tailPad6 (Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:29)
- SignalLaneDispatch: size=32 multiple8=True file=Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs
  - offset 0: delegate*<void> Dispose (Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:239)
  - offset 8: delegate*<int, void> Flush (Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:240)
  - offset 16: delegate*<ref SignalLaneTelemetry, void> CopyTelemetry (Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:241)
  - offset 24: uint _pad0 (Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:242)
  - offset 28: ushort _pad1 (Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:243)
  - offset 30: byte FlushDuringSimulationPause (Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:244)
  - offset 31: byte _pad2 (Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:245)
- SignalLaneTelemetry: size=32 multiple8=True file=Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs
  - offset 0: ulong Reserved2 (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:230)
  - offset 8: uint LaneHash (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:231)
  - offset 12: int QueuedBeforeFlush (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:232)
  - offset 16: int SnapshotCount (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:233)
  - offset 20: int DroppedCount (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:234)
  - offset 24: int CoalescedCount (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:235)
  - offset 28: ushort Reserved1 (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:237)
  - offset 30: byte Flags (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:239)
  - offset 31: byte Reserved0 (Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:241)
- SignalTelemetryFrame: size=64 multiple8=True file=Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs
  - offset 0: ulong Reserved0 (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:740)
  - offset 8: ulong Reserved1 (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:741)
  - offset 16: ulong Reserved2 (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:742)
  - offset 24: uint Frame (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:743)
  - offset 28: uint TotalPushedSignals (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:744)
  - offset 32: uint PeakSignalsPerFrame (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:745)
  - offset 36: uint CoalescedSignals (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:746)
  - offset 40: uint DroppedSignals (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:747)
  - offset 44: uint CorruptedSignals (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:748)
  - offset 48: uint ActiveLaneCount (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:749)
  - offset 52: uint Flags (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:750)
  - offset 56: uint GlobalQualityMilli (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:751)
  - offset 60: uint SystemStressMilli (Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:752)
- SignalStormFuzzerPayload1311: size=24 multiple8=True file=Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs
  - offset 0: ulong Hash (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:17)
  - offset 8: uint Producer (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:18)
  - offset 12: uint Sequence (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:19)
  - offset 16: uint GlobalSequence (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:20)
  - offset 20: uint Flags (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:21)
- SignalStormFuzzerResult1311: size=64 multiple8=True file=Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs
  - offset 0: ulong ResultHash (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:27)
  - offset 8: long ElapsedTicks (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:28)
  - offset 16: int ProducerCount (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:29)
  - offset 20: int WritesPerProducer (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:30)
  - offset 24: int ExpectedWrites (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:31)
  - offset 28: int AcceptedWrites (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:32)
  - offset 32: int DrainedWrites (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:33)
  - offset 36: int UniqueWrites (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:34)
  - offset 40: int DroppedWrites (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:35)
  - offset 44: int DuplicateWrites (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:36)
  - offset 48: int CorruptedWrites (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:37)
  - offset 52: int MissingWrites (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:38)
  - offset 56: uint Status (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:39)
  - offset 60: uint Reserved0 (Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:40)

## NativeQueue Red Zone

## Project Writer Callsite Count
- native_queue_writer_field: 16
- signalbus_ring_writer_request: 112
- signalbus_try_enqueue_bounded: 58
- signalbus_nativequeue_writer_hits: 0

## Phase Route
- dispatcher_pre_sim_heartbeat: 1
  - Assets/_Project/Scripts/Core/SystemDispatcher.cs:5046 SignalCorridorRuntime.PreSimulationHeartbeat();
- dispatcher_post_sim_flush: 1
  - Assets/_Project/Scripts/Core/SystemDispatcher.cs:5453 SignalCorridorRuntime.FlushPostSimulation();
- dispatcher_pre_sim_flush: 0
- dispatcher_post_sim_clear: 0
- registry_post_sim_flush: 1
  - Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs:349 SignalBusRegistry.FlushPostSimulation();
- registry_pre_sim_flush: 0
- snapshot_clear_delegate: 0

## Fail-Closed Proof
- registration_gate_field: 3
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:46 private static int _registrationGate;
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:298 while (Interlocked.CompareExchange(ref _registrationGate, 1, 0) != 0)
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:304 Volatile.Write(ref _registrationGate, 0);
- registration_gate_enter: 3
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:85 EnterRegistrationGate();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:164 EnterRegistrationGate();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:295 private static void EnterRegistrationGate()
- registration_gate_exit: 3
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:121 ExitRegistrationGate();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:186 ExitRegistrationGate();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:302 private static void ExitRegistrationGate()
- registration_gate_compare_exchange: 1
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:298 while (Interlocked.CompareExchange(ref _registrationGate, 1, 0) != 0)
- registration_gate_release: 1
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:304 Volatile.Write(ref _registrationGate, 0);
- registration_returns_bool: 1
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:76 internal static bool Register(
- registered_latch_from_result: 1
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:1331 _registered = SignalBusRegistry.Register(
- registration_overflow_log_once: 1
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:103 int firstOverflow = Interlocked.Exchange(ref _registrationOverflow, 1);
- spsc_partial_allocation_cleanup: 1
  - Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:57 if (!_buffer.IsCreated || !_cursor.IsCreated)
- mpsc_partial_allocation_cleanup: 1
  - Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:197 if (!_buffer.IsCreated || !_publishedTickets.IsCreated || !_cursor.IsCreated)
- failed_ring_check: 2
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:548 if (!_ring.IsCreated)
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:610 if (!_ring.IsCreated)
- ring_dispose_on_failure: 4
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:550 _ring.Dispose();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:557 _ring.Dispose();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:569 _ring.Dispose();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:1230 _ring.Dispose();
- frame_snapshot_release_on_failure: 3
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:571 ReleaseFrameSnapshotBuffer();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:1241 ReleaseFrameSnapshotBuffer();
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:1432 private static void ReleaseFrameSnapshotBuffer()
- async_dump_request: 1
  - Assets/_Project/Scripts/Core/Signals/SignalWardenRuntime.cs:867 public static bool RequestDumpToDiskAsync()
- ring_clear_drop_to_tail: 2
  - Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:99 Interlocked.Exchange(ref cursor->Head, tail);
  - Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:259 Interlocked.Exchange(ref cursor->Head, tail);
- ring_clear_tail_reset: 0
- ring_clear_ticket_loop: 0
- fuzzer_allocation_fail_closed: 1
  - Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs:85 if (!ring.IsCreated || !seen.IsCreated)
- dispatch_storage_guard: 3
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:198 if (!_laneDispatch.IsCreated)
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:222 if (!_laneDispatch.IsCreated)
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:265 if (!_laneDispatch.IsCreated)
- dispatch_length_clamp: 2
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:201 int copyCount = Math.Min(Math.Min(Volatile.Read(ref _laneCount), _laneDispatch.Length), destination.Length);
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:268 int dispatchCount = Math.Min(Volatile.Read(ref _laneCount), _laneDispatch.Length);
- writer_sanitize_before_budget: 1
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:676 int writerGuardCode = SignalPayloadFiniteGuards.Sanitize(ref signal);
- writer_corrupt_drop: 1
  - Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:677 if (writerGuardCode != 0)

## New Expression Classification
- Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:222 managedHeap=no hotPath=writer_open class=value_type_parallel_writer text=return new ParallelWriter(_buffer, _publishedTickets, _cursor, _mask, _capacity);
- Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:111 managedHeap=no hotPath=registration class=value_type_dispatch_record text=_laneDispatch[laneCount] = new SignalLaneDispatch(
- Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:544 managedHeap=no hotPath=lane_bootstrap class=native_container_struct_construction text=_ring = new global::Hecton8.Core.MpscSignalRingBuffer<T>(
- Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:723 managedHeap=no hotPath=read_accessor class=ref_struct_view text=return new ReadOnlySpan<T>(pointer, snapshotCount);
- Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:3215 managedHeap=no hotPath=sanitize_default class=value_type_float3 text=signal.Direction = new float3(0f, 0f, -1f);
- Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs:4631 managedHeap=no hotPath=sanitize_default class=value_type_float3 text=signal.SurfaceNormal = new float3(0f, 1f, 0f);

## Fuzzer
- Assets/_Project/Scripts/Core/Signals/SignalStormConcurrencyFuzzer1311.cs exists=True

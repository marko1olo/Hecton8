using System;
using System.Runtime.CompilerServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-GC TMP lane for diegetic visor labels. Runtime writes must enter through spans.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text), typeof(HectonTextNode))]
    public sealed class DiegeticHudTextNode : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int DefaultCapacity = 256;
        private const int MaxDiegeticHudSignalsPerLateFrame = 8;
        private const string DiegeticSignalFallbackPrefix = "DIEGETIC HUD 0x";
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private static readonly uint DiegeticHudSignalMissWarningHash = unchecked((uint)LocHash.Compute("DiegeticHudTextNode.SignalMessageMiss"));
        private static readonly uint DiegeticHudSignalWriteMissWarningHash = unchecked((uint)LocHash.Compute("DiegeticHudTextNode.SignalWriteMiss"));
        private static readonly uint DiegeticHudDuplicateOwnerWarningHash = unchecked((uint)LocHash.Compute("DiegeticHudTextNode.DuplicateSignalOwner"));
        private static readonly uint DiegeticHudContextHash = unchecked((uint)LocHash.Compute("DiegeticHudTextNode"));

        [SerializeField] private TMP_Text target;
        [SerializeField, Min(8)] private int capacity = DefaultCapacity;
        [SerializeField] private bool registerWithTextRegistry = true;
        [SerializeField] private bool consumeDiegeticHudSignals = true;

        // COLD ALLOC: char[capacity] - persistent TMP SetCharArray backing store - owner: DiegeticHudTextNode
        private char[] _buffer;
        // COLD ALLOC: char[256] - hash-only diegetic HUD localization decode buffer - owner: DiegeticHudTextNode
        private readonly char[] _signalDecodeBuffer = new char[DefaultCapacity];
        private uint _lastHash;
        private int _lastLength = -1;
        private int _lastOxygenPercent = int.MinValue;
        private uint _lastDiegeticSignalMessageHash;
        private uint _lastDiegeticSignalContextHash;
        private int _consumedDiegeticSignalCount;
        private int _diegeticSignalMessageMissCount;
        private int _diegeticSignalWriteMissCount;
        private int _duplicateSignalOwnerCount;
        private int _lastSignalMessageMissTelemetryFrame = -1;
        private int _lastSignalWriteMissTelemetryFrame = -1;
        private int _lastDuplicateOwnerTelemetryFrame = -1;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private static DiegeticHudTextNode s_signalOwner;

        public TMP_Text Target => target;
        public int Capacity => _buffer != null ? _buffer.Length : 0;
        public int ConsumedDiegeticSignalCount => _consumedDiegeticSignalCount;
        public int DiegeticSignalMessageMissCount => _diegeticSignalMessageMissCount;
        public int DiegeticSignalWriteMissCount => _diegeticSignalWriteMissCount;
        public int DuplicateSignalOwnerCount => _duplicateSignalOwnerCount;
        public uint LastDiegeticSignalMessageHash => _lastDiegeticSignalMessageHash;
        public uint LastDiegeticSignalContextHash => _lastDiegeticSignalContextHash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_signalOwner = null;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            if (registerWithTextRegistry && target != null && target.TryGetComponent(out HectonTextNode _))
                TMP_TextRegistry.EnsureRegistered(target);

            TryRegisterHotSwapListener();
            TryClaimSignalOwner();
        }

        private void OnDisable()
        {
            ReleaseSignalOwner();
            TryUnregisterHotSwapListener();
            ClearDiegeticSignalRuntimeState();
            ClearDiegeticHudSignalDiagnostics();
        }

        private void OnDestroy()
        {
            ReleaseSignalOwner();
            TryUnregisterHotSwapListener();
            ClearDiegeticSignalRuntimeState();
            ClearDiegeticHudSignalDiagnostics();
        }

        public bool SetSpan(ReadOnlySpan<char> value)
        {
            if (!EnsureInitialized() || value.Length > _buffer.Length)
                return false;

            uint hash = Hash(value);
            if (_lastLength == value.Length && _lastHash == hash)
                return true;

            value.CopyTo(_buffer.AsSpan(0, value.Length));
            target.SetCharArray(_buffer, 0, value.Length);
            _lastLength = value.Length;
            _lastHash = hash;
            return true;
        }

        public bool SetFormattedInt(ReadOnlySpan<char> prefix, int value, ReadOnlySpan<char> suffix)
        {
            if (!EnsureInitialized())
                return false;

            Span<char> destination = _buffer.AsSpan();
            int cursor = 0;
            if (!ZeroGCFormatter.AppendToSpan(prefix, destination, ref cursor) ||
                !ZeroGCFormatter.FastIntToChars(value, destination, ref cursor) ||
                !ZeroGCFormatter.AppendToSpan(suffix, destination, ref cursor))
            {
                return false;
            }

            return Commit(cursor);
        }

        public bool SetFormattedFloat(ReadOnlySpan<char> prefix, float value, int decimals, ReadOnlySpan<char> suffix)
        {
            if (!EnsureInitialized())
                return false;

            Span<char> destination = _buffer.AsSpan();
            int cursor = 0;
            if (!ZeroGCFormatter.AppendToSpan(prefix, destination, ref cursor) ||
                !ZeroGCFormatter.FastFloatToChars(value, decimals, destination, ref cursor) ||
                !ZeroGCFormatter.AppendToSpan(suffix, destination, ref cursor))
            {
                return false;
            }

            return Commit(cursor);
        }

        public bool SetOxygenPercent(int oxygenPercent)
        {
            oxygenPercent = math.clamp(oxygenPercent, 0, 100);
            if (oxygenPercent == _lastOxygenPercent)
                return true;

            if (!EnsureInitialized())
                return false;

            Span<char> destination = _buffer.AsSpan();
            int cursor = 0;
            if (!ZeroGCFormatter.AppendToSpan("O2 ".AsSpan(), destination, ref cursor) ||
                !ZeroGCFormatter.FastIntToChars(oxygenPercent, destination, ref cursor) ||
                !ZeroGCFormatter.AppendChar('%', destination, ref cursor))
            {
                return false;
            }

            if (!Commit(cursor))
                return false;

            _lastOxygenPercent = oxygenPercent;
            return true;
        }

        public void LateFrameTick()
        {
            if (!ReferenceEquals(s_signalOwner, this))
                return;

            DrainDiegeticHudSignalLane();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            bool ownedSignals = ReferenceEquals(s_signalOwner, this);
            UnregisterLateFrame();
            if (ownedSignals && currentService != null && isActiveAndEnabled)
                TryRegisterLateFrame();
        }

        private bool Commit(int length)
        {
            if (!EnsureInitialized() || length < 0 || length > _buffer.Length)
                return false;

            ReadOnlySpan<char> value = _buffer.AsSpan(0, length);
            uint hash = Hash(value);
            if (_lastLength == length && _lastHash == hash)
                return true;

            target.SetCharArray(_buffer, 0, length);
            _lastLength = length;
            _lastHash = hash;
            return true;
        }

        private bool EnsureInitialized()
        {
            if (target == null && !TryGetComponent(out target))
                return false;

            int resolvedCapacity = math.max(8, capacity);
            if (_buffer == null || _buffer.Length != resolvedCapacity)
                _buffer = new char[resolvedCapacity]; // COLD ALLOC: char[resolvedCapacity] - rebuilt authoring-sized text buffer - owner: DiegeticHudTextNode

            return target != null;
        }

        private void TryClaimSignalOwner()
        {
            if (!consumeDiegeticHudSignals || !Application.isPlaying)
                return;

            if (s_signalOwner != null && !ReferenceEquals(s_signalOwner, this))
            {
                ReportDuplicateSignalOwner();
                return;
            }

            s_signalOwner = this;
            TryRegisterLateFrame();
        }

        private void ReleaseSignalOwner()
        {
            if (ReferenceEquals(s_signalOwner, this))
                s_signalOwner = null;

            UnregisterLateFrame();
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
            _registeredLateFrame = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void DrainDiegeticHudSignalLane()
        {
            int budget = MaxDiegeticHudSignalsPerLateFrame;
            while (budget-- > 0 && SignalBus<DiegeticHudSignal>.TryConsumeFrame(out DiegeticHudSignal signal))
                ApplyDiegeticHudSignal(in signal);
        }

        private void ApplyDiegeticHudSignal(in DiegeticHudSignal signal)
        {
            if (!TryWriteDiegeticSignalMessage(in signal, out int length))
                return;

            if (!SetSpan(_signalDecodeBuffer.AsSpan(0, length)))
            {
                ReportDiegeticSignalWriteMiss(in signal);
                return;
            }

            _lastDiegeticSignalMessageHash = signal.MessageHash;
            _lastDiegeticSignalContextHash = signal.ContextHash;
            _consumedDiegeticSignalCount++;
        }

        private bool TryWriteDiegeticSignalMessage(in DiegeticHudSignal signal, out int length)
        {
            length = 0;
            if (signal.MessageHash == 0u)
            {
                ReportDiegeticSignalMessageMiss(in signal);
                return false;
            }

            bool found = LocRegistry.TryWriteVisualSpanFromUtf8(
                signal.MessageHash,
                _signalDecodeBuffer.AsSpan(),
                out length,
                stripRichText: true);
            if (found && length > 0)
                return true;

            ReportDiegeticSignalMessageMiss(in signal);
            return TryWriteDiegeticSignalFallback(signal.MessageHash, _signalDecodeBuffer.AsSpan(), out length);
        }

        private static bool TryWriteDiegeticSignalFallback(uint messageHash, Span<char> target, out int length)
        {
            length = 0;
            if (target.Length < DiegeticSignalFallbackPrefix.Length + 8)
                return false;

            DiegeticSignalFallbackPrefix.AsSpan().CopyTo(target);
            length = DiegeticSignalFallbackPrefix.Length;
            for (int shift = 28; shift >= 0; shift -= 4)
                target[length++] = ToUpperHexNibble((messageHash >> shift) & 0xFu);

            return true;
        }

        private void ReportDiegeticSignalMessageMiss(in DiegeticHudSignal signal)
        {
            _diegeticSignalMessageMissCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastSignalMessageMissTelemetryFrame == frame)
                return;

            _lastSignalMessageMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                DiegeticHudSignalMissWarningHash,
                DiegeticHudContextHash ^ signal.MessageHash ^ signal.ContextHash,
                math.max(1, _diegeticSignalMessageMissCount));
        }

        private void ReportDiegeticSignalWriteMiss(in DiegeticHudSignal signal)
        {
            _diegeticSignalWriteMissCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastSignalWriteMissTelemetryFrame == frame)
                return;

            _lastSignalWriteMissTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                DiegeticHudSignalWriteMissWarningHash,
                DiegeticHudContextHash ^ signal.MessageHash ^ signal.ContextHash,
                math.max(1, _diegeticSignalWriteMissCount));
        }

        private void ReportDuplicateSignalOwner()
        {
            _duplicateSignalOwnerCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastDuplicateOwnerTelemetryFrame == frame)
                return;

            _lastDuplicateOwnerTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                DiegeticHudDuplicateOwnerWarningHash,
                DiegeticHudContextHash,
                math.max(1, _duplicateSignalOwnerCount));
        }

        private void ClearDiegeticSignalRuntimeState()
        {
            _lastDiegeticSignalMessageHash = 0u;
            _lastDiegeticSignalContextHash = 0u;
            _consumedDiegeticSignalCount = 0;
        }

        private void ClearDiegeticHudSignalDiagnostics()
        {
            _diegeticSignalMessageMissCount = 0;
            _diegeticSignalWriteMissCount = 0;
            _duplicateSignalOwnerCount = 0;
            _lastSignalMessageMissTelemetryFrame = -1;
            _lastSignalWriteMissTelemetryFrame = -1;
            _lastDuplicateOwnerTelemetryFrame = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash(ReadOnlySpan<char> value)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= FnvPrime;
            }

            return hash;
        }

        private static char ToUpperHexNibble(uint value)
        {
            value &= 0xFu;
            return value < 10u
                ? (char)('0' + value)
                : (char)('A' + (value - 10u));
        }
    }
}

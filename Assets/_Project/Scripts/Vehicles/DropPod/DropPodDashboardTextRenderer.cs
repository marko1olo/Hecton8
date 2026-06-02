namespace Hecton8.Vehicles.DropPod
{
    using System;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using TMPro;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Vehicles/Drop Pod/Dashboard Text Renderer")]
    public sealed class DropPodDashboardTextRenderer : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const string StatusPrefix = "STAT ";
        private const string OxygenPrefix = "O2 ";
        private const string VelocityPrefix = "VEL ";
        private const string IntegrityPrefix = "HULL ";
        private const string IdleLabel = "IDLE";
        private const string MovingLabel = "MOVING";
        private const string OpenLabel = "OPEN";
        private const string TransitLabel = "TRANSIT";
        private const string SealedLabel = "SEALED";
        private const string SeatLabel = "SEATED";
        private const string ArmedLabel = "ARMED";
        private const string IgnitionLabel = "IGNITION";
        private const string HatchOpenLabel = "HATCH OPEN";
        private const string FailLabel = "FAULT";
        private const int TextCapacity = 40;
        private const uint InvalidMetricValue = uint.MaxValue;
        private const float MaxDisplayedVelocityMetersPerSecond = 99999f;
        private const float MaxNeedleSweepDegrees = 220f;
        private const float MaxNeedleJitterDegrees = 12f;

        [Header("Text")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text oxygenText;
        [SerializeField] private TMP_Text velocityText;
        [SerializeField] private TMP_Text integrityText;

        [Header("Needles")]
        [SerializeField] private Transform oxygenNeedle;
        [SerializeField] private Transform velocityNeedle;
        [SerializeField] private Transform integrityNeedle;
        [SerializeField, Range(1f, 220f)] private float needleSweepDegrees = 126f;
        [SerializeField, Range(0f, 12f)] private float maxJitterDegrees = 2.2f;

        [Header("Simulation Display")]
        [SerializeField, Range(0f, 100f)] private float oxygenPercent = 98f;
        [SerializeField, Range(0f, 100f)] private float integrityPercent = 100f;
        [SerializeField, Range(0f, 12000f)] private float velocityMetersPerSecond;
        [SerializeField, Range(0.02f, 1f)] private float lowTierRefreshSeconds = 0.24f;
        [SerializeField, Range(0.02f, 1f)] private float highTierRefreshSeconds = 0.05f;

        // COLD ALLOC: char[40] - persistent TMP SetCharArray status buffer - owner: DropPodDashboardTextRenderer
        private readonly char[] _statusBuffer = new char[TextCapacity];
        // COLD ALLOC: char[40] - persistent TMP SetCharArray oxygen buffer - owner: DropPodDashboardTextRenderer
        private readonly char[] _oxygenBuffer = new char[TextCapacity];
        // COLD ALLOC: char[40] - persistent TMP SetCharArray velocity buffer - owner: DropPodDashboardTextRenderer
        private readonly char[] _velocityBuffer = new char[TextCapacity];
        // COLD ALLOC: char[40] - persistent TMP SetCharArray integrity buffer - owner: DropPodDashboardTextRenderer
        private readonly char[] _integrityBuffer = new char[TextCapacity];

        private Quaternion _oxygenNeedleBase = Quaternion.identity;
        private Quaternion _velocityNeedleBase = Quaternion.identity;
        private Quaternion _integrityNeedleBase = Quaternion.identity;
        private DropPodStatusId _statusId = DropPodStatusId.Idle;
        private DropPodStatusId _lastRenderedStatusId = (DropPodStatusId)uint.MaxValue;
        private uint _lastOxygenValue = InvalidMetricValue;
        private uint _lastVelocityValue = InvalidMetricValue;
        private uint _lastIntegrityValue = InvalidMetricValue;
        private float _refreshTimer;
        private double _lastLateTickTimeSeconds;
        private uint _lastStatusFrame;
        private ushort _lastStatusSequence;
        private bool _textDirty = true;
        private bool _registeredLate;
        private bool _registeredHotSwap;

        private void Awake()
        {
            CacheNeedleBases();
            DropPodSignalLaneBootstrap.EnsureConfigured();
        }

        private void OnEnable()
        {
            DropPodSignalLaneBootstrap.EnsureConfigured();
            ResetStatusCursor();
            TryRegisterHotSwapListener();
            bool lateRouteReady = TryRegisterLate();
            _lastLateTickTimeSeconds = SystemDispatcher.CurrentUnscaledTimeSeconds;
            _textDirty = true;
            DrainStatusSignals();
            if (Application.isPlaying && !lateRouteReady)
                MarkFailClosedPresentationFallback();
            RenderNow();
        }

        private void OnDisable()
        {
            UnregisterLate();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            UnregisterLate();
            TryUnregisterHotSwapListener();
        }

        public void LateFrameTick()
        {
            DrainStatusSignals();
            float quality = DropPodSplineMath.SanitizeUnit01(SignalBusRegistry.GlobalQualityWeight01);
            float interval = ResolveRefreshInterval(quality);
            _refreshTimer -= ResolveLateDeltaSeconds(0.05f);
            if (_refreshTimer > 0f && !_textDirty)
                return;

            _refreshTimer = math.max(0.02f, interval);
            RenderNow();
        }

        private void ResetStatusCursor()
        {
            _lastStatusFrame = 0u;
            _lastStatusSequence = 0;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            UnregisterLate();
            if (!isActiveAndEnabled)
                return;

            if (currentService == null || !TryRegisterLate())
                MarkFailClosedPresentationFallback();
        }

        private void DrainStatusSignals()
        {
            ReadOnlySpan<DropPodStatusSignal> signals = SignalBus<DropPodStatusSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                DropPodStatusSignal signal = signals[i];
                if (!DropPodSignalLaneBootstrap.IsNewerSignal(signal.Frame, signal.Sequence, _lastStatusFrame, _lastStatusSequence))
                    continue;

                _lastStatusFrame = signal.Frame;
                _lastStatusSequence = signal.Sequence;
                DropPodStatusId status = (DropPodStatusId)signal.StatusId;
                if (_statusId != status)
                {
                    _statusId = status;
                    _textDirty = true;
                }
            }
        }

        private void RenderNow()
        {
            uint oxygenValue = ResolvePercentMetric(oxygenPercent);
            uint velocityValue = ResolveVelocityMetric(velocityMetersPerSecond);
            uint integrityValue = ResolvePercentMetric(integrityPercent);

            if (_textDirty || _lastRenderedStatusId != _statusId)
            {
                WriteStatus();
                _lastRenderedStatusId = _statusId;
            }

            if (_textDirty || _lastOxygenValue != oxygenValue)
            {
                WriteMetric(oxygenText, _oxygenBuffer, OxygenPrefix.AsSpan(), oxygenValue, "%".AsSpan());
                _lastOxygenValue = oxygenValue;
            }

            if (_textDirty || _lastVelocityValue != velocityValue)
            {
                WriteMetric(velocityText, _velocityBuffer, VelocityPrefix.AsSpan(), velocityValue, "M/S".AsSpan());
                _lastVelocityValue = velocityValue;
            }

            if (_textDirty || _lastIntegrityValue != integrityValue)
            {
                WriteMetric(integrityText, _integrityBuffer, IntegrityPrefix.AsSpan(), integrityValue, "%".AsSpan());
                _lastIntegrityValue = integrityValue;
            }

            _textDirty = false;
            ApplyNeedles();
        }

        private static uint ResolvePercentMetric(float value)
        {
            return (uint)math.round(ResolvePercent01(value) * 100f);
        }

        private static uint ResolveVelocityMetric(float value)
        {
            float safe = math.isfinite(value) ? math.clamp(value, 0f, MaxDisplayedVelocityMetersPerSecond) : 0f;
            return (uint)math.round(safe);
        }

        private static float ResolvePercent01(float value)
        {
            return DropPodSplineMath.SanitizeUnit01(math.isfinite(value) ? value * 0.01f : 0f);
        }

        private static float ResolveVelocity01(float value)
        {
            float safe = math.isfinite(value) ? math.max(0f, value) : 0f;
            return DropPodSplineMath.SanitizeUnit01(safe / 12000f);
        }

        private void WriteStatus()
        {
            if (statusText == null)
                return;

            int cursor = Append(StatusPrefix.AsSpan(), _statusBuffer, 0);
            cursor = Append(ResolveStatusLabel(), _statusBuffer, cursor);
            statusText.SetCharArray(_statusBuffer, 0, cursor);
        }

        private static void WriteMetric(TMP_Text target, char[] buffer, ReadOnlySpan<char> prefix, uint value, ReadOnlySpan<char> suffix)
        {
            if (target == null || buffer == null)
                return;

            int cursor = Append(prefix, buffer, 0);
            if (cursor < buffer.Length && value.TryFormat(buffer.AsSpan(cursor), out int written))
                cursor += written;
            if (cursor < buffer.Length)
                buffer[cursor++] = ' ';
            cursor = Append(suffix, buffer, cursor);
            target.SetCharArray(buffer, 0, math.clamp(cursor, 0, buffer.Length));
        }

        private void ApplyNeedles()
        {
            float quality = DropPodSplineMath.SanitizeUnit01(SignalBusRegistry.GlobalQualityWeight01);
            float jitterScale = math.lerp(0.25f, 1f, quality);
            float phase = (SystemDispatcher.CurrentFrameId & 1023u) * 0.071f;
            float jitter = DropPodSplineMath.ApproxSinBhaskara(phase) * ResolveNeedleJitterDegrees(maxJitterDegrees) * jitterScale;
            ApplyNeedle(oxygenNeedle, _oxygenNeedleBase, ResolvePercent01(oxygenPercent), jitter);
            ApplyNeedle(velocityNeedle, _velocityNeedleBase, ResolveVelocity01(velocityMetersPerSecond), -jitter * 0.65f);
            ApplyNeedle(integrityNeedle, _integrityNeedleBase, ResolvePercent01(integrityPercent), jitter * 0.45f);
        }

        private float ResolveLateDeltaSeconds(float maxDeltaSeconds)
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            float rawDeltaSeconds = (float)(now - _lastLateTickTimeSeconds);
            float dt = math.clamp(math.isfinite(rawDeltaSeconds) ? rawDeltaSeconds : 0f, 0f, math.max(0f, maxDeltaSeconds));
            _lastLateTickTimeSeconds = now;
            return dt;
        }

        private void ApplyNeedle(Transform needle, Quaternion baseRotation, float value01, float jitterDegrees)
        {
            if (needle == null)
                return;

            float sweepDegrees = ResolveNeedleSweepDegrees(needleSweepDegrees);
            float safeJitter = DropPodSplineMath.SanitizeRange(jitterDegrees, -MaxNeedleJitterDegrees, MaxNeedleJitterDegrees, 0f);
            float safeValue01 = DropPodSplineMath.SanitizeUnit01(value01);
            float degrees = math.lerp(-sweepDegrees * 0.5f, sweepDegrees * 0.5f, safeValue01) + safeJitter;
            needle.localRotation = baseRotation * Quaternion.Euler(0f, 0f, degrees);
        }

        private float ResolveRefreshInterval(float quality)
        {
            float low = ResolveRefreshSeconds(lowTierRefreshSeconds);
            float high = ResolveRefreshSeconds(highTierRefreshSeconds);
            return math.max(0.02f, math.lerp(low, high, DropPodSplineMath.SanitizeUnit01(quality)));
        }

        private static float ResolveRefreshSeconds(float seconds)
        {
            return math.isfinite(seconds) ? math.clamp(seconds, 0.02f, 1f) : 0.24f;
        }

        private static float ResolveNeedleSweepDegrees(float value)
        {
            return DropPodSplineMath.SanitizeRange(value, 1f, MaxNeedleSweepDegrees, 126f);
        }

        private static float ResolveNeedleJitterDegrees(float value)
        {
            return DropPodSplineMath.SanitizeRange(value, 0f, MaxNeedleJitterDegrees, 0f);
        }

        private ReadOnlySpan<char> ResolveStatusLabel()
        {
            switch (_statusId)
            {
                case DropPodStatusId.AirlockMoving:
                    return MovingLabel.AsSpan();
                case DropPodStatusId.AirlockSealed:
                    return SealedLabel.AsSpan();
                case DropPodStatusId.AirlockOpen:
                    return OpenLabel.AsSpan();
                case DropPodStatusId.SeatTransitActive:
                    return TransitLabel.AsSpan();
                case DropPodStatusId.SeatTransitArmed:
                    return ArmedLabel.AsSpan();
                case DropPodStatusId.Seated:
                    return SeatLabel.AsSpan();
                case DropPodStatusId.EngineIgnitionArmed:
                    return IgnitionLabel.AsSpan();
                case DropPodStatusId.SeatBlockedAirlockOpen:
                    return HatchOpenLabel.AsSpan();
                case DropPodStatusId.FailClosed:
                    return FailLabel.AsSpan();
                default:
                    return IdleLabel.AsSpan();
            }
        }

        private static int Append(ReadOnlySpan<char> source, char[] buffer, int cursor)
        {
            if (buffer == null)
                return 0;
            if (cursor >= buffer.Length)
                return buffer.Length;
            if (cursor < 0)
                cursor = 0;

            int count = math.min(source.Length, buffer.Length - cursor);
            for (int i = 0; i < count; i++)
                buffer[cursor + i] = source[i];
            return cursor + math.max(0, count);
        }

        private void CacheNeedleBases()
        {
            if (oxygenNeedle != null)
                _oxygenNeedleBase = oxygenNeedle.localRotation;
            if (velocityNeedle != null)
                _velocityNeedleBase = velocityNeedle.localRotation;
            if (integrityNeedle != null)
                _integrityNeedleBase = integrityNeedle.localRotation;
        }

        private bool TryRegisterLate()
        {
            if (_registeredLate || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return _registeredLate;

            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
            return _registeredLate;
        }

        private void MarkFailClosedPresentationFallback()
        {
            if (_statusId == DropPodStatusId.FailClosed)
                return;

            _statusId = DropPodStatusId.FailClosed;
            _textDirty = true;
        }

        private void UnregisterLate()
        {
            if (!_registeredLate)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLate = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }
    }
}

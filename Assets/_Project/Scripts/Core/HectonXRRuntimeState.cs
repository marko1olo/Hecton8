using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;

namespace Hecton8.Core
{
    /// <summary>
    /// XR runtime state bridge for shader globals, cadence selection, and VR-only pressure gates.
    /// </summary>
    internal static class HectonXRRuntimeState
    {
        private const int RefreshSampleIntervalFrames = 30;
        private const float DefaultXRRefreshRateHz = 72f;
        private const float MinimumXRRefreshRateHz = 60f;
        private const float MaximumXRRefreshRateHz = 144f;
        private const float CadenceSnapToleranceFraction = 0.35f;

        private static readonly int _HectonXRFoveatedParamsId = Shader.PropertyToID("_HectonXRFoveatedParams");
        private static readonly int _HectonXRFoveatedCenterRadiusId = Shader.PropertyToID("_HectonXRFoveatedCenterRadius");
        private static readonly int _HectonXRNearClipDitherParamsId = Shader.PropertyToID("_HectonXRNearClipDitherParams");
        private static readonly int _HectonXROriginShiftStateId = Shader.PropertyToID("_HectonXROriginShiftState");
        private static readonly int _HectonXRCadenceStateId = Shader.PropertyToID("_HectonXRCadenceState");
        private static readonly int _HectonXRPoseSyncStateId = Shader.PropertyToID("_HectonXRPoseSyncState");

        // COLD ALLOC: List<XRDisplaySubsystem>[4] - XR refresh-rate query scratch reused without per-frame allocation - owner: HectonXRRuntimeState
        private static readonly List<XRDisplaySubsystem> _displaySubsystems = new List<XRDisplaySubsystem>(4);

        private static bool _isXRActive;
        private static float _refreshRateHz = DefaultXRRefreshRateHz;
        private static int _nextRefreshSampleFrame;
        private static Vector4 _lastFoveatedParams = Vector4.positiveInfinity;
        private static Vector4 _lastFoveatedCenterRadius = Vector4.positiveInfinity;
        private static Vector4 _lastNearClipDitherParams = Vector4.positiveInfinity;
        private static Vector4 _lastCadenceState = Vector4.positiveInfinity;
        private static Vector4 _lastPoseSyncState = Vector4.positiveInfinity;
        private static bool _originShiftPoseLocked;
        private static int _originShiftPoseLockFrame = -1;
        private static int _lastForcedPoseRefreshFrame = -1;
        private static Vector3 _lockedCenterEyePosition;
        private static Vector3 _lockedLeftEyePosition;
        private static Vector3 _lockedRightEyePosition;
        private static Quaternion _lockedCenterEyeRotation = Quaternion.identity;
        private static Quaternion _lockedLeftEyeRotation = Quaternion.identity;
        private static Quaternion _lockedRightEyeRotation = Quaternion.identity;

        internal static bool IsXRActive => _isXRActive;

        internal static float RefreshRateHz => _refreshRateHz;

        internal static float FrameIntervalSeconds => _refreshRateHz > 0f ? 1f / _refreshRateHz : 1f / DefaultXRRefreshRateHz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _displaySubsystems.Clear();
            _isXRActive = false;
            _refreshRateHz = DefaultXRRefreshRateHz;
            _nextRefreshSampleFrame = 0;
            _lastFoveatedParams = Vector4.positiveInfinity;
            _lastFoveatedCenterRadius = Vector4.positiveInfinity;
            _lastNearClipDitherParams = Vector4.positiveInfinity;
            _lastCadenceState = Vector4.positiveInfinity;
            _lastPoseSyncState = Vector4.positiveInfinity;
            _originShiftPoseLocked = false;
            _originShiftPoseLockFrame = -1;
            _lastForcedPoseRefreshFrame = -1;
            _lockedCenterEyePosition = Vector3.zero;
            _lockedLeftEyePosition = Vector3.zero;
            _lockedRightEyePosition = Vector3.zero;
            _lockedCenterEyeRotation = Quaternion.identity;
            _lockedLeftEyeRotation = Quaternion.identity;
            _lockedRightEyeRotation = Quaternion.identity;
            ResetShaderGlobals();
        }

        internal static void RefreshFrameState(int frame)
        {
            bool active = XRSettings.enabled && XRSettings.isDeviceActive;
            if (active && frame >= _nextRefreshSampleFrame)
            {
                _nextRefreshSampleFrame = frame + RefreshSampleIntervalFrames;
                _refreshRateHz = ResolveDisplayRefreshRateHz();
            }
            else if (!active)
            {
                _refreshRateHz = DefaultXRRefreshRateHz;
            }

            _isXRActive = active;
            PublishStaticShaderState();
        }

        internal static float ResolveDispatcherDeltaTime(float measuredDeltaTime)
        {
            if (!_isXRActive || measuredDeltaTime <= 0f)
                return measuredDeltaTime;

            float targetDeltaTime = FrameIntervalSeconds;
            float tolerance = targetDeltaTime * CadenceSnapToleranceFraction;
            return math.abs(measuredDeltaTime - targetDeltaTime) <= tolerance
                ? targetDeltaTime
                : measuredDeltaTime;
        }

        internal static void PublishOriginShiftState(uint shiftSequence, float fixedInterpolationAlpha)
        {
            float active = _isXRActive ? 1f : 0f;
            Shader.SetGlobalVector(
                _HectonXROriginShiftStateId,
                new Vector4(active, shiftSequence, Time.frameCount, math.saturate(fixedInterpolationAlpha)));
        }

        internal static void BeginOriginShiftPoseLock()
        {
            if (!_isXRActive)
                return;

            _originShiftPoseLocked = true;
            _originShiftPoseLockFrame = Time.frameCount;
            SampleCurrentEyePoses();
            PublishPoseSyncState();
        }

        internal static void EndOriginShiftPoseLock(uint shiftSequence, float fixedInterpolationAlpha)
        {
            if (!_isXRActive)
            {
                _originShiftPoseLocked = false;
                PublishPoseSyncState();
                return;
            }

            SampleCurrentEyePoses();
            _lastForcedPoseRefreshFrame = Time.frameCount;
            _originShiftPoseLocked = false;
            PublishOriginShiftState(shiftSequence, fixedInterpolationAlpha);
            PublishPoseSyncState();
        }

        internal static bool TryGetOriginShiftLockedEyePose(
            XRNode eye,
            out Vector3 position,
            out Quaternion rotation)
        {
            if (!_originShiftPoseLocked)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }

            switch (eye)
            {
                case XRNode.LeftEye:
                    position = _lockedLeftEyePosition;
                    rotation = _lockedLeftEyeRotation;
                    return true;
                case XRNode.RightEye:
                    position = _lockedRightEyePosition;
                    rotation = _lockedRightEyeRotation;
                    return true;
                default:
                    position = _lockedCenterEyePosition;
                    rotation = _lockedCenterEyeRotation;
                    return true;
            }
        }

        internal static void ResetShaderGlobals()
        {
            Shader.SetGlobalVector(_HectonXRFoveatedParamsId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXRFoveatedCenterRadiusId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXRNearClipDitherParamsId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXROriginShiftStateId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXRCadenceStateId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXRPoseSyncStateId, Vector4.zero);
        }

        private static float ResolveDisplayRefreshRateHz()
        {
            _displaySubsystems.Clear();
            SubsystemManager.GetSubsystems(_displaySubsystems);
            for (int i = 0; i < _displaySubsystems.Count; i++)
            {
                XRDisplaySubsystem display = _displaySubsystems[i];
                if (display == null || !display.running)
                    continue;

                if (display.TryGetDisplayRefreshRate(out float refreshRate) &&
                    refreshRate >= MinimumXRRefreshRateHz &&
                    refreshRate <= MaximumXRRefreshRateHz)
                {
                    return refreshRate;
                }
            }

            return DefaultXRRefreshRateHz;
        }

        private static void PublishStaticShaderState()
        {
            Vector4 foveatedParams = _isXRActive
                ? new Vector4(1f, 0.65f, 0.5f, _refreshRateHz)
                : Vector4.zero;
            Vector4 foveatedCenterRadius = _isXRActive
                ? new Vector4(0.5f, 0.5f, 0.31f, 0.52f)
                : Vector4.zero;
            Vector4 nearClipDitherParams = _isXRActive
                ? new Vector4(1f, 0.1f, 0.025f, 1f)
                : Vector4.zero;
            Vector4 cadenceState = _isXRActive
                ? new Vector4(1f, _refreshRateHz, FrameIntervalSeconds, Time.frameCount)
                : Vector4.zero;

            PublishIfChanged(_HectonXRFoveatedParamsId, foveatedParams, ref _lastFoveatedParams);
            PublishIfChanged(_HectonXRFoveatedCenterRadiusId, foveatedCenterRadius, ref _lastFoveatedCenterRadius);
            PublishIfChanged(_HectonXRNearClipDitherParamsId, nearClipDitherParams, ref _lastNearClipDitherParams);
            PublishIfChanged(_HectonXRCadenceStateId, cadenceState, ref _lastCadenceState);
            PublishPoseSyncState();
        }

        private static void SampleCurrentEyePoses()
        {
            _lockedCenterEyePosition = GetLocalPosition(XRNode.CenterEye);
            _lockedLeftEyePosition = GetLocalPosition(XRNode.LeftEye);
            _lockedRightEyePosition = GetLocalPosition(XRNode.RightEye);
            _lockedCenterEyeRotation = GetLocalRotation(XRNode.CenterEye);
            _lockedLeftEyeRotation = GetLocalRotation(XRNode.LeftEye);
            _lockedRightEyeRotation = GetLocalRotation(XRNode.RightEye);
        }

        private static Vector3 GetLocalPosition(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position)
                ? position
                : Vector3.zero;
        }

        private static Quaternion GetLocalRotation(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid && device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation)
                ? rotation
                : Quaternion.identity;
        }

        private static void PublishPoseSyncState()
        {
            Vector4 poseSyncState = _isXRActive
                ? new Vector4(_originShiftPoseLocked ? 1f : 0f, _originShiftPoseLockFrame, _lastForcedPoseRefreshFrame, Time.frameCount)
                : Vector4.zero;
            PublishIfChanged(_HectonXRPoseSyncStateId, poseSyncState, ref _lastPoseSyncState);
        }

        private static void PublishIfChanged(int propertyId, Vector4 value, ref Vector4 previous)
        {
            if (Approximately(previous, value))
                return;

            Shader.SetGlobalVector(propertyId, value);
            previous = value;
        }

        private static bool Approximately(Vector4 a, Vector4 b)
        {
            return math.abs(a.x - b.x) <= 0.0001f &&
                   math.abs(a.y - b.y) <= 0.0001f &&
                   math.abs(a.z - b.z) <= 0.0001f &&
                   math.abs(a.w - b.w) <= 0.0001f;
        }
    }
}

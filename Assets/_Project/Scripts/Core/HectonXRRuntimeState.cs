using System.Collections.Generic;
using Hecton8.World;
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
        private static bool _hardwareFoveationActive;
        private static float _hardwareFoveationLevel01;
        private static int _hardwareEyeTextureWidth;
        private static int _hardwareEyeTextureHeight;
        private static int _nextRefreshSampleFrame;
        private static Vector4 _lastFoveatedParams = Vector4.positiveInfinity;
        private static Vector4 _lastFoveatedCenterRadius = Vector4.positiveInfinity;
        private static Vector4 _lastNearClipDitherParams = Vector4.positiveInfinity;
        private static Vector4 _lastOriginShiftState = Vector4.positiveInfinity;
        private static Vector4 _lastCadenceState = Vector4.positiveInfinity;
        private static Vector4 _lastPoseSyncState = Vector4.positiveInfinity;
        private static bool _publishedInactiveShaderState;
        private static bool _originShiftPoseLocked;
        private static int _originShiftPoseLockFrame = -1;
        private static int _lastForcedPoseRefreshFrame = -1;
        private static Vector3 _lockedCenterEyePosition;
        private static Vector3 _lockedLeftEyePosition;
        private static Vector3 _lockedRightEyePosition;
        private static Quaternion _lockedCenterEyeRotation = Quaternion.identity;
        private static Quaternion _lockedLeftEyeRotation = Quaternion.identity;
        private static Quaternion _lockedRightEyeRotation = Quaternion.identity;
        private static Vector3 _cachedHeadRuntimePosition;
        private static AbsoluteUniversePosition _cachedHeadAup;
        private static int _cachedHeadAupFrame = -1;
        private static bool _hasCachedHeadAup;

        internal delegate void XRActiveChangedHandler(bool isActive);

        internal static event XRActiveChangedHandler XRActiveChanged;

        internal static bool IsXRActive => _isXRActive;

        internal static float RefreshRateHz => _refreshRateHz;

        internal static float FrameIntervalSeconds => _refreshRateHz > 0f ? 1f / _refreshRateHz : 1f / DefaultXRRefreshRateHz;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _displaySubsystems.Clear();
            _isXRActive = false;
            _refreshRateHz = DefaultXRRefreshRateHz;
            _hardwareFoveationActive = false;
            _hardwareFoveationLevel01 = 0f;
            _hardwareEyeTextureWidth = 0;
            _hardwareEyeTextureHeight = 0;
            _nextRefreshSampleFrame = 0;
            _lastFoveatedParams = Vector4.positiveInfinity;
            _lastFoveatedCenterRadius = Vector4.positiveInfinity;
            _lastNearClipDitherParams = Vector4.positiveInfinity;
            _lastOriginShiftState = Vector4.positiveInfinity;
            _lastCadenceState = Vector4.positiveInfinity;
            _lastPoseSyncState = Vector4.positiveInfinity;
            _publishedInactiveShaderState = false;
            _originShiftPoseLocked = false;
            _originShiftPoseLockFrame = -1;
            _lastForcedPoseRefreshFrame = -1;
            _lockedCenterEyePosition = Vector3.zero;
            _lockedLeftEyePosition = Vector3.zero;
            _lockedRightEyePosition = Vector3.zero;
            _lockedCenterEyeRotation = Quaternion.identity;
            _lockedLeftEyeRotation = Quaternion.identity;
            _lockedRightEyeRotation = Quaternion.identity;
            _cachedHeadRuntimePosition = Vector3.zero;
            _cachedHeadAup = default;
            _cachedHeadAupFrame = -1;
            _hasCachedHeadAup = false;
            XRActiveChanged = null;
            ResetShaderGlobals();
        }

        internal static void RefreshFrameState(int frame)
        {
            bool active = XRSettings.enabled && XRSettings.isDeviceActive;
            bool wasActive = _isXRActive;
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
            if (wasActive != _isXRActive)
                XRActiveChanged?.Invoke(_isXRActive);

            if (!_isXRActive)
                _hasCachedHeadAup = false;
            else if (!_hasCachedHeadAup)
                SlowTickHeadAupCache();

            PublishStaticShaderState();
        }

        internal static float ResolveDispatcherDeltaTime(float measuredDeltaTime)
        {
            if (!math.isfinite(measuredDeltaTime))
                return _isXRActive ? FrameIntervalSeconds : 0f;

            if (!_isXRActive || measuredDeltaTime <= 0f)
                return measuredDeltaTime;

            float targetDeltaTime = FrameIntervalSeconds;
            float tolerance = targetDeltaTime * CadenceSnapToleranceFraction;
            return math.abs(measuredDeltaTime - targetDeltaTime) <= tolerance
                ? targetDeltaTime
                : measuredDeltaTime;
        }

        internal static void SlowTickHeadAupCache()
        {
            if (!_isXRActive)
            {
                _hasCachedHeadAup = false;
                return;
            }

            if (TryResolveHeadRuntimePosition(out Vector3 runtimePosition, out AbsoluteUniversePosition headAup))
                CacheHeadAup(runtimePosition, in headAup);
        }

        internal static bool TryResolveCachedHeadAup(Vector3 runtimePosition, out AbsoluteUniversePosition headAup)
        {
            if (_isXRActive && _hasCachedHeadAup && _cachedHeadAupFrame >= 0 && IsFinite(runtimePosition))
            {
                headAup = OffsetAupLocal(in _cachedHeadAup, runtimePosition - _cachedHeadRuntimePosition);
                return true;
            }

            headAup = default;
            return false;
        }

        internal static bool TryGetCachedHeadAup(out AbsoluteUniversePosition headAup)
        {
            if (_isXRActive && _hasCachedHeadAup && _cachedHeadAupFrame >= 0)
            {
                headAup = _cachedHeadAup;
                return true;
            }

            headAup = default;
            return false;
        }

        internal static void PublishOriginShiftState(uint shiftSequence, float fixedInterpolationAlpha)
        {
            if (!_isXRActive)
            {
                PublishIfChanged(_HectonXROriginShiftStateId, Vector4.zero, ref _lastOriginShiftState);
                return;
            }

            Vector4 originShiftState = new Vector4(
                1f,
                shiftSequence,
                _lastForcedPoseRefreshFrame,
                math.saturate(fixedInterpolationAlpha));
            PublishIfChanged(_HectonXROriginShiftStateId, originShiftState, ref _lastOriginShiftState);
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
            SlowTickHeadAupCache();
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
            InvalidateShaderStateCache();
            Shader.SetGlobalVector(_HectonXRFoveatedParamsId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXRFoveatedCenterRadiusId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXRNearClipDitherParamsId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXROriginShiftStateId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXRCadenceStateId, Vector4.zero);
            Shader.SetGlobalVector(_HectonXRPoseSyncStateId, Vector4.zero);
            MarkInactiveShaderStatePublished();
        }

        internal static void ReportHardwareFoveationState(bool active, float level01, int eyeTextureWidth, int eyeTextureHeight)
        {
            float sanitizedLevel = math.saturate(math.isfinite(level01) ? level01 : 0f);
            int sanitizedWidth = math.max(0, eyeTextureWidth);
            int sanitizedHeight = math.max(0, eyeTextureHeight);
            if (_hardwareFoveationActive == active &&
                math.abs(_hardwareFoveationLevel01 - sanitizedLevel) <= 0.0001f &&
                _hardwareEyeTextureWidth == sanitizedWidth &&
                _hardwareEyeTextureHeight == sanitizedHeight)
            {
                return;
            }

            _hardwareFoveationActive = active;
            _hardwareFoveationLevel01 = sanitizedLevel;
            _hardwareEyeTextureWidth = sanitizedWidth;
            _hardwareEyeTextureHeight = sanitizedHeight;
            _lastFoveatedParams = Vector4.positiveInfinity;
            _lastFoveatedCenterRadius = Vector4.positiveInfinity;
        }

        private static void InvalidateShaderStateCache()
        {
            _lastFoveatedParams = Vector4.positiveInfinity;
            _lastFoveatedCenterRadius = Vector4.positiveInfinity;
            _lastNearClipDitherParams = Vector4.positiveInfinity;
            _lastOriginShiftState = Vector4.positiveInfinity;
            _lastCadenceState = Vector4.positiveInfinity;
            _lastPoseSyncState = Vector4.positiveInfinity;
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
            if (!_isXRActive && _publishedInactiveShaderState)
                return;

            float foveatedResolveWeight = _hardwareFoveationActive
                ? math.clamp(0.65f + _hardwareFoveationLevel01 * 0.25f, 0.65f, 0.90f)
                : 0.65f;
            float foveatedInnerRadius = _hardwareFoveationActive
                ? math.lerp(0.62f, 0.54f, _hardwareFoveationLevel01)
                : 0.62f;
            float foveatedOuterRadius = _hardwareFoveationActive
                ? math.lerp(1.04f, 0.96f, _hardwareFoveationLevel01)
                : 1.04f;
            Vector4 foveatedParams = _isXRActive
                ? new Vector4(1f, foveatedResolveWeight, _hardwareFoveationLevel01, _refreshRateHz)
                : Vector4.zero;
            Vector4 foveatedCenterRadius = _isXRActive
                ? new Vector4(0f, 0f, foveatedInnerRadius, foveatedOuterRadius)
                : Vector4.zero;
            Vector4 nearClipDitherParams = _isXRActive
                ? new Vector4(1f, 0.1f, 0.025f, 1f)
                : Vector4.zero;
            Vector4 cadenceState = _isXRActive
                ? new Vector4(1f, _refreshRateHz, FrameIntervalSeconds, RefreshSampleIntervalFrames)
                : Vector4.zero;

            PublishIfChanged(_HectonXRFoveatedParamsId, foveatedParams, ref _lastFoveatedParams);
            PublishIfChanged(_HectonXRFoveatedCenterRadiusId, foveatedCenterRadius, ref _lastFoveatedCenterRadius);
            PublishIfChanged(_HectonXRNearClipDitherParamsId, nearClipDitherParams, ref _lastNearClipDitherParams);
            PublishIfChanged(_HectonXRCadenceStateId, cadenceState, ref _lastCadenceState);
            if (!_isXRActive)
                PublishIfChanged(_HectonXROriginShiftStateId, Vector4.zero, ref _lastOriginShiftState);
            PublishPoseSyncState();
            _publishedInactiveShaderState = !_isXRActive;
            if (_isXRActive)
                return;

            MarkInactiveShaderStatePublished();
        }

        private static void MarkInactiveShaderStatePublished()
        {
            _lastFoveatedParams = Vector4.zero;
            _lastFoveatedCenterRadius = Vector4.zero;
            _lastNearClipDitherParams = Vector4.zero;
            _lastOriginShiftState = Vector4.zero;
            _lastCadenceState = Vector4.zero;
            _lastPoseSyncState = Vector4.zero;
            _publishedInactiveShaderState = true;
        }

        private static void SampleCurrentEyePoses()
        {
            SampleEyePose(XRNode.CenterEye, out _lockedCenterEyePosition, out _lockedCenterEyeRotation);
            SampleEyePose(XRNode.LeftEye, out _lockedLeftEyePosition, out _lockedLeftEyeRotation);
            SampleEyePose(XRNode.RightEye, out _lockedRightEyePosition, out _lockedRightEyeRotation);
        }

        private static void SampleEyePose(XRNode node, out Vector3 position, out Quaternion rotation)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return;
            }

            if (!device.TryGetFeatureValue(CommonUsages.devicePosition, out position))
                position = Vector3.zero;
            else if (!IsFinite(position))
                position = Vector3.zero;

            if (!device.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
                rotation = Quaternion.identity;
            else if (!IsFinite(rotation))
                rotation = Quaternion.identity;
        }

        private static void PublishPoseSyncState()
        {
            Vector4 poseSyncState = _isXRActive
                ? new Vector4(
                    _originShiftPoseLocked ? 1f : 0f,
                    _originShiftPoseLockFrame,
                    _lastForcedPoseRefreshFrame,
                    _originShiftPoseLocked ? _originShiftPoseLockFrame : _lastForcedPoseRefreshFrame)
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

        private static bool TryResolveHeadRuntimePosition(out Vector3 runtimePosition, out AbsoluteUniversePosition headAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                TryResolveHeadRuntimePosition(in runtimeContext, out runtimePosition, out headAup))
            {
                return true;
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                if (playerContext.PlayerCamera != null)
                {
                    runtimePosition = playerContext.PlayerCamera.transform.position;
                    if (!IsFinite(runtimePosition))
                    {
                        headAup = default;
                        return false;
                    }

                    var movement = playerContext.PlayerMovement;
                    if (movement != null)
                    {
                        AbsoluteUniversePosition bodyAup = movement.CurrentAup;
                        Vector3 bodyRuntimePosition = (Vector3)bodyAup.ToRuntimeFloat3();
                        if (IsFinite(bodyRuntimePosition))
                        {
                            headAup = OffsetAupLocal(in bodyAup, runtimePosition - bodyRuntimePosition);
                            return true;
                        }
                    }

                    headAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
                    return true;
                }

                if (playerContext.PlayerMovement != null)
                {
                    headAup = playerContext.PlayerMovement.CurrentAup;
                    runtimePosition = (Vector3)headAup.ToRuntimeFloat3();
                    return IsFinite(runtimePosition);
                }

                if (playerContext.PlayerTransform != null)
                {
                    runtimePosition = playerContext.PlayerTransform.position;
                    if (!IsFinite(runtimePosition))
                    {
                        headAup = default;
                        return false;
                    }

                    headAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
                    return true;
                }
            }

            runtimePosition = Vector3.zero;
            headAup = default;
            return false;
        }

        private static bool TryResolveHeadRuntimePosition(
            in PlayerRuntimeContext runtimeContext,
            out Vector3 runtimePosition,
            out AbsoluteUniversePosition headAup)
        {
            runtimePosition = Vector3.zero;
            headAup = default;
            if (runtimeContext == null || !runtimeContext.IsBound)
                return false;

            PlayerLookState lookState = runtimeContext.LookState;
            if ((lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.all(math.isfinite(lookState.EyePosition)))
            {
                return false;
            }

            runtimePosition = new Vector3(lookState.EyePosition.x, lookState.EyePosition.y, lookState.EyePosition.z);
            var movement = runtimeContext.PlayerMovement;
            if (movement != null)
            {
                float3 bodyRuntimePosition = runtimeContext.MovementState.WorldPosition;
                if (math.all(math.isfinite(bodyRuntimePosition)))
                {
                    Vector3 bodyPosition = new Vector3(bodyRuntimePosition.x, bodyRuntimePosition.y, bodyRuntimePosition.z);
                    AbsoluteUniversePosition bodyAup = movement.CurrentAup;
                    headAup = OffsetAupLocal(in bodyAup, runtimePosition - bodyPosition);
                    return true;
                }
            }

            headAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return true;
        }

        private static void CacheHeadAup(Vector3 runtimePosition, in AbsoluteUniversePosition headAup)
        {
            if (!IsFinite(runtimePosition))
                return;

            _cachedHeadRuntimePosition = runtimePosition;
            _cachedHeadAup = headAup;
            _cachedHeadAupFrame = Time.frameCount;
            _hasCachedHeadAup = true;
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
            if (local >= 0f && local < cellSize)
                return;

            long gridDelta = (long)math.floor(local / cellSize);
            grid += gridDelta;
            local -= gridDelta * cellSize;
            if (local < 0f)
            {
                local += cellSize;
                grid--;
                return;
            }

            if (local >= cellSize)
            {
                local -= cellSize;
                grid++;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                   !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }
    }
}

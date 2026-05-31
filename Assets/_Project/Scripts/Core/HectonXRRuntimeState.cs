using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct XRRuntimeAup48
    {
        [FieldOffset(0)] public long GridX;
        [FieldOffset(8)] public long GridY;
        [FieldOffset(16)] public long GridZ;
        [FieldOffset(24)] public float LocalX;
        [FieldOffset(28)] public float LocalY;
        [FieldOffset(32)] public float LocalZ;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public ulong Reserved1;

        internal static bool TryFromRuntimePosition(Vector3 runtimePosition, out XRRuntimeAup48 aup)
        {
            if (!IsFinite(runtimePosition))
            {
                aup = default;
                return false;
            }

            Hecton8.World.AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
            {
                aup = default;
                return false;
            }

            double3 absolutePosition = Hecton8.World.AbsoluteUniversePosition.OffsetAbsoluteMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return TryFromAbsoluteDouble3(absolutePosition, out aup);
        }

        internal static bool TryFromFields(
            long gridX,
            long gridY,
            long gridZ,
            float localX,
            float localY,
            float localZ,
            out XRRuntimeAup48 aup)
        {
            float3 local = new float3(localX, localY, localZ);
            if (!math.all(math.isfinite(local)))
            {
                aup = default;
                return false;
            }

            aup = new XRRuntimeAup48
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = localX,
                LocalY = localY,
                LocalZ = localZ,
                Reserved0 = 0u,
                Reserved1 = 0ul
            };
            return true;
        }

        internal static bool TryOffsetLocal(in XRRuntimeAup48 anchorAup, Vector3 runtimeOffset, out XRRuntimeAup48 result)
        {
            if (!IsFinite(runtimeOffset))
            {
                result = default;
                return false;
            }

            result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return math.all(math.isfinite(new float3(result.LocalX, result.LocalY, result.LocalZ)));
        }

        internal static bool TryToRelativeFloat3(in XRRuntimeAup48 position, in XRRuntimeAup48 origin, out float3 relative)
        {
            double3 delta = new double3(
                (((double)position.GridX - origin.GridX) * HectonPhysicsContract.AupSectorSizeMetersDouble) + ((double)position.LocalX - origin.LocalX),
                (((double)position.GridY - origin.GridY) * HectonPhysicsContract.AupSectorSizeMetersDouble) + ((double)position.LocalY - origin.LocalY),
                (((double)position.GridZ - origin.GridZ) * HectonPhysicsContract.AupSectorSizeMetersDouble) + ((double)position.LocalZ - origin.LocalZ));
            if (!math.all(math.isfinite(delta)) ||
                math.any(math.abs(delta) > HectonPhysicsContract.AupMaxFloatSafeMeters))
            {
                relative = float3.zero;
                return false;
            }

            relative = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            return math.all(math.isfinite(relative));
        }

        internal bool TryToRuntimeFloat3(out float3 runtimePosition)
        {
            double3 absolute = ToAbsoluteDouble3(in this);
            Hecton8.World.AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
            {
                runtimePosition = float3.zero;
                return false;
            }

            double3 runtime = absolute - originAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(runtime)) ||
                math.any(math.abs(runtime) > HectonPhysicsContract.AupMaxFloatSafeMeters))
            {
                runtimePosition = float3.zero;
                return false;
            }

            runtimePosition = AupPrecisionMath.DowncastLocalDelta(runtime, float3.zero);
            return math.all(math.isfinite(runtimePosition));
        }

        private static bool TryFromAbsoluteDouble3(double3 absolutePosition, out XRRuntimeAup48 aup)
        {
            if (!math.all(math.isfinite(absolutePosition)))
            {
                aup = default;
                return false;
            }

            double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            long gridX = (long)math.floor(absolutePosition.x / cellSize);
            long gridY = (long)math.floor(absolutePosition.y / cellSize);
            long gridZ = (long)math.floor(absolutePosition.z / cellSize);
            return TryFromFields(
                gridX,
                gridY,
                gridZ,
                (float)(absolutePosition.x - (gridX * cellSize)),
                (float)(absolutePosition.y - (gridY * cellSize)),
                (float)(absolutePosition.z - (gridZ * cellSize)),
                out aup);
        }

        private static double3 ToAbsoluteDouble3(in XRRuntimeAup48 position)
        {
            return new double3(
                (position.GridX * HectonPhysicsContract.AupSectorSizeMetersDouble) + position.LocalX,
                (position.GridY * HectonPhysicsContract.AupSectorSizeMetersDouble) + position.LocalY,
                (position.GridZ * HectonPhysicsContract.AupSectorSizeMetersDouble) + position.LocalZ);
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = HectonPhysicsContract.AupSectorSizeMetersFloat;
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
    }

    /// <summary>
    /// XR runtime state bridge for shader globals, cadence selection, and VR-only pressure gates.
    /// </summary>
    public static class HectonXRRuntimeState
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
        private static float _pendingRefreshRateRequestHz;
        private static bool _hasPendingRefreshRateRequest;
        private static Vector4 _lastFoveatedParams = Vector4.positiveInfinity;
        private static Vector4 _lastFoveatedCenterRadius = Vector4.positiveInfinity;
        private static Vector4 _lastNearClipDitherParams = Vector4.positiveInfinity;
        private static Vector4 _lastOriginShiftState = Vector4.positiveInfinity;
        private static Vector4 _lastCadenceState = Vector4.positiveInfinity;
        private static Vector4 _lastPoseSyncState = Vector4.positiveInfinity;
        private static Vector4 _pendingFoveatedParams;
        private static Vector4 _pendingFoveatedCenterRadius;
        private static Vector4 _pendingNearClipDitherParams;
        private static Vector4 _pendingOriginShiftState;
        private static Vector4 _pendingCadenceState;
        private static Vector4 _pendingPoseSyncState;
        private static bool _pendingFoveatedParamsDirty;
        private static bool _pendingFoveatedCenterRadiusDirty;
        private static bool _pendingNearClipDitherParamsDirty;
        private static bool _pendingOriginShiftStateDirty;
        private static bool _pendingCadenceStateDirty;
        private static bool _pendingPoseSyncStateDirty;
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
        private static XRRuntimeAup48 _cachedHeadAup;
        private static IPlayerRuntimeContext _coldPlayerContextFallback;
        private static int _cachedHeadAupFrame = -1;
        private static bool _hasCachedHeadAup;

        internal delegate void XRActiveChangedHandler(bool isActive);

        internal static event XRActiveChangedHandler XRActiveChanged;

        public static bool IsXRActive => _isXRActive;

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
            _pendingRefreshRateRequestHz = 0f;
            _hasPendingRefreshRateRequest = false;
            _lastFoveatedParams = Vector4.positiveInfinity;
            _lastFoveatedCenterRadius = Vector4.positiveInfinity;
            _lastNearClipDitherParams = Vector4.positiveInfinity;
            _lastOriginShiftState = Vector4.positiveInfinity;
            _lastCadenceState = Vector4.positiveInfinity;
            _lastPoseSyncState = Vector4.positiveInfinity;
            _pendingFoveatedParamsDirty = false;
            _pendingFoveatedCenterRadiusDirty = false;
            _pendingNearClipDitherParamsDirty = false;
            _pendingOriginShiftStateDirty = false;
            _pendingCadenceStateDirty = false;
            _pendingPoseSyncStateDirty = false;
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
            if (!_isXRActive)
                _hasCachedHeadAup = false;

            PublishStaticShaderState();
        }

        internal static void RefreshPlatformStateCold(int frame)
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
                _nextRefreshSampleFrame = frame;
                _pendingRefreshRateRequestHz = 0f;
                _hasPendingRefreshRateRequest = false;
            }

            _isXRActive = active;
            if (wasActive != _isXRActive)
                XRActiveChanged?.Invoke(_isXRActive);

            if (!_isXRActive)
                _hasCachedHeadAup = false;

            if (_isXRActive && _hasPendingRefreshRateRequest)
                TryApplyDisplayRefreshRateRequestCold(_pendingRefreshRateRequestHz, frame);
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

            if (TryResolveHeadRuntimePosition(out Vector3 runtimePosition, out XRRuntimeAup48 headAup))
                CacheHeadAup(runtimePosition, in headAup);
        }

        internal static void BindPlayerContextFallbackCold(IPlayerRuntimeContext playerContext)
        {
            _coldPlayerContextFallback = playerContext;
        }

        internal static bool TryResolveCachedHeadAup48(Vector3 runtimePosition, out XRRuntimeAup48 headAup)
        {
            if (_isXRActive &&
                _hasCachedHeadAup &&
                _cachedHeadAupFrame >= 0 &&
                IsFinite(runtimePosition) &&
                XRRuntimeAup48.TryOffsetLocal(in _cachedHeadAup, runtimePosition - _cachedHeadRuntimePosition, out headAup))
                return true;

            headAup = default;
            return false;
        }

        internal static bool TryResolveCachedHeadAupFields(
            Vector3 runtimePosition,
            out long gridX,
            out long gridY,
            out long gridZ,
            out float localX,
            out float localY,
            out float localZ)
        {
            if (TryResolveCachedHeadAup48(runtimePosition, out XRRuntimeAup48 headAup))
            {
                CopyAupFields(in headAup, out gridX, out gridY, out gridZ, out localX, out localY, out localZ);
                return true;
            }

            gridX = 0L;
            gridY = 0L;
            gridZ = 0L;
            localX = 0f;
            localY = 0f;
            localZ = 0f;
            return false;
        }

        internal static bool TryRequestDisplayRefreshRateHz(float targetRefreshRateHz)
        {
            if (!_isXRActive ||
                !math.isfinite(targetRefreshRateHz) ||
                targetRefreshRateHz < MinimumXRRefreshRateHz ||
                targetRefreshRateHz > MaximumXRRefreshRateHz)
            {
                return false;
            }

            _pendingRefreshRateRequestHz = targetRefreshRateHz;
            _hasPendingRefreshRateRequest = true;
            return true;
        }

        private static bool TryApplyDisplayRefreshRateRequestCold(float targetRefreshRateHz, int frame)
        {
            if (!_isXRActive ||
                !math.isfinite(targetRefreshRateHz) ||
                targetRefreshRateHz < MinimumXRRefreshRateHz ||
                targetRefreshRateHz > MaximumXRRefreshRateHz)
            {
                _pendingRefreshRateRequestHz = 0f;
                _hasPendingRefreshRateRequest = false;
                return false;
            }

            _displaySubsystems.Clear();
            SubsystemManager.GetSubsystems(_displaySubsystems);
            bool requested = false;
            for (int i = 0; i < _displaySubsystems.Count; i++)
            {
                XRDisplaySubsystem display = _displaySubsystems[i];
                if (display == null || !display.running)
                    continue;

                requested = true;
            }

            if (requested)
            {
                int frameRate = (int)math.round(targetRefreshRateHz);
                if (Application.targetFrameRate <= 0 || Application.targetFrameRate > frameRate)
                    Application.targetFrameRate = frameRate;

                _refreshRateHz = targetRefreshRateHz;
                _nextRefreshSampleFrame = frame + RefreshSampleIntervalFrames;
                _pendingRefreshRateRequestHz = 0f;
                _hasPendingRefreshRateRequest = false;
                InvalidateShaderStateCache();
            }

            return requested;
        }

        internal static void PublishOriginShiftState(uint shiftSequence, float fixedInterpolationAlpha)
        {
            if (!_isXRActive)
            {
                QueueIfChanged(_HectonXROriginShiftStateId, Vector4.zero, ref _lastOriginShiftState);
                return;
            }

            Vector4 originShiftState = new Vector4(
                1f,
                shiftSequence,
                _lastForcedPoseRefreshFrame,
                math.saturate(fixedInterpolationAlpha));
            QueueIfChanged(_HectonXROriginShiftStateId, originShiftState, ref _lastOriginShiftState);
        }

        internal static void BeginOriginShiftPoseLock()
        {
            if (!_isXRActive)
                return;

            _originShiftPoseLocked = true;
            _originShiftPoseLockFrame = SystemDispatcher.CurrentFrameIndex;
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
            _lastForcedPoseRefreshFrame = SystemDispatcher.CurrentFrameIndex;
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
            ClearPendingShaderState();
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
            ClearPendingShaderState();
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

            QueueIfChanged(_HectonXRFoveatedParamsId, foveatedParams, ref _lastFoveatedParams);
            QueueIfChanged(_HectonXRFoveatedCenterRadiusId, foveatedCenterRadius, ref _lastFoveatedCenterRadius);
            QueueIfChanged(_HectonXRNearClipDitherParamsId, nearClipDitherParams, ref _lastNearClipDitherParams);
            QueueIfChanged(_HectonXRCadenceStateId, cadenceState, ref _lastCadenceState);
            if (!_isXRActive)
                QueueIfChanged(_HectonXROriginShiftStateId, Vector4.zero, ref _lastOriginShiftState);
            PublishPoseSyncState();
            _publishedInactiveShaderState = !_isXRActive;
            if (_isXRActive)
                return;

            MarkInactiveShaderStatePublished();
        }

        private static void MarkInactiveShaderStatePublished()
        {
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
            QueueIfChanged(_HectonXRPoseSyncStateId, poseSyncState, ref _lastPoseSyncState);
        }

        internal static void FlushVisualSyncShaderState()
        {
            FlushQueuedIfDirty(_HectonXRFoveatedParamsId, ref _pendingFoveatedParams, ref _pendingFoveatedParamsDirty, ref _lastFoveatedParams);
            FlushQueuedIfDirty(_HectonXRFoveatedCenterRadiusId, ref _pendingFoveatedCenterRadius, ref _pendingFoveatedCenterRadiusDirty, ref _lastFoveatedCenterRadius);
            FlushQueuedIfDirty(_HectonXRNearClipDitherParamsId, ref _pendingNearClipDitherParams, ref _pendingNearClipDitherParamsDirty, ref _lastNearClipDitherParams);
            FlushQueuedIfDirty(_HectonXROriginShiftStateId, ref _pendingOriginShiftState, ref _pendingOriginShiftStateDirty, ref _lastOriginShiftState);
            FlushQueuedIfDirty(_HectonXRCadenceStateId, ref _pendingCadenceState, ref _pendingCadenceStateDirty, ref _lastCadenceState);
            FlushQueuedIfDirty(_HectonXRPoseSyncStateId, ref _pendingPoseSyncState, ref _pendingPoseSyncStateDirty, ref _lastPoseSyncState);

            if (!_isXRActive && !_pendingFoveatedParamsDirty && !_pendingFoveatedCenterRadiusDirty && !_pendingNearClipDitherParamsDirty &&
                !_pendingOriginShiftStateDirty && !_pendingCadenceStateDirty && !_pendingPoseSyncStateDirty)
            {
                _publishedInactiveShaderState = true;
            }
        }

        private static void QueueIfChanged(int propertyId, Vector4 value, ref Vector4 previous)
        {
            if (propertyId == _HectonXRFoveatedParamsId)
            {
                if (!_pendingFoveatedParamsDirty && Approximately(previous, value))
                    return;
                _pendingFoveatedParams = value;
                _pendingFoveatedParamsDirty = true;
            }
            else if (propertyId == _HectonXRFoveatedCenterRadiusId)
            {
                if (!_pendingFoveatedCenterRadiusDirty && Approximately(previous, value))
                    return;
                _pendingFoveatedCenterRadius = value;
                _pendingFoveatedCenterRadiusDirty = true;
            }
            else if (propertyId == _HectonXRNearClipDitherParamsId)
            {
                if (!_pendingNearClipDitherParamsDirty && Approximately(previous, value))
                    return;
                _pendingNearClipDitherParams = value;
                _pendingNearClipDitherParamsDirty = true;
            }
            else if (propertyId == _HectonXROriginShiftStateId)
            {
                if (!_pendingOriginShiftStateDirty && Approximately(previous, value))
                    return;
                _pendingOriginShiftState = value;
                _pendingOriginShiftStateDirty = true;
            }
            else if (propertyId == _HectonXRCadenceStateId)
            {
                if (!_pendingCadenceStateDirty && Approximately(previous, value))
                    return;
                _pendingCadenceState = value;
                _pendingCadenceStateDirty = true;
            }
            else if (propertyId == _HectonXRPoseSyncStateId)
            {
                if (!_pendingPoseSyncStateDirty && Approximately(previous, value))
                    return;
                _pendingPoseSyncState = value;
                _pendingPoseSyncStateDirty = true;
            }
        }

        private static void FlushQueuedIfDirty(int propertyId, ref Vector4 pending, ref bool dirty, ref Vector4 previous)
        {
            if (!dirty)
                return;

            dirty = false;
            if (Approximately(previous, pending))
                return;

            Shader.SetGlobalVector(propertyId, pending);
            previous = pending;
        }

        private static void ClearPendingShaderState()
        {
            _pendingFoveatedParamsDirty = false;
            _pendingFoveatedCenterRadiusDirty = false;
            _pendingNearClipDitherParamsDirty = false;
            _pendingOriginShiftStateDirty = false;
            _pendingCadenceStateDirty = false;
            _pendingPoseSyncStateDirty = false;
        }

        private static bool Approximately(Vector4 a, Vector4 b)
        {
            return math.abs(a.x - b.x) <= 0.0001f &&
                   math.abs(a.y - b.y) <= 0.0001f &&
                   math.abs(a.z - b.z) <= 0.0001f &&
                   math.abs(a.w - b.w) <= 0.0001f;
        }

        private static bool TryResolveHeadRuntimePosition(out Vector3 runtimePosition, out XRRuntimeAup48 headAup)
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                TryResolveHeadRuntimePosition(in runtimeContext, out runtimePosition, out headAup))
            {
                return true;
            }

            IPlayerRuntimeContext playerContext = _coldPlayerContextFallback;
            if (playerContext != null)
            {
                if (playerContext.PlayerCamera != null)
                {
                    runtimePosition = playerContext.PlayerCamera.transform.position;
                    return XRRuntimeAup48.TryFromRuntimePosition(runtimePosition, out headAup);
                }

                if (playerContext.TryGetPlayerPoseSnapshot(out var poseSnapshot) &&
                    math.all(math.isfinite(poseSnapshot.RuntimePosition)))
                {
                    float3 poseRuntime = poseSnapshot.RuntimePosition;
                    runtimePosition = new Vector3(poseRuntime.x, poseRuntime.y, poseRuntime.z);
                    return XRRuntimeAup48.TryFromRuntimePosition(runtimePosition, out headAup);
                }

                if (playerContext.PlayerTransform != null)
                {
                    runtimePosition = playerContext.PlayerTransform.position;
                    return XRRuntimeAup48.TryFromRuntimePosition(runtimePosition, out headAup);
                }
            }

            runtimePosition = Vector3.zero;
            headAup = default;
            return false;
        }

        private static bool TryResolveHeadRuntimePosition(
            in PlayerRuntimeContext runtimeContext,
            out Vector3 runtimePosition,
            out XRRuntimeAup48 headAup)
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
            return XRRuntimeAup48.TryFromRuntimePosition(runtimePosition, out headAup);
        }

        private static void CacheHeadAup(Vector3 runtimePosition, in XRRuntimeAup48 headAup)
        {
            if (!IsFinite(runtimePosition))
                return;

            _cachedHeadRuntimePosition = runtimePosition;
            _cachedHeadAup = headAup;
            _cachedHeadAupFrame = SystemDispatcher.CurrentFrameIndex;
            _hasCachedHeadAup = true;
        }

        private static void CopyAupFields(
            in XRRuntimeAup48 aup,
            out long gridX,
            out long gridY,
            out long gridZ,
            out float localX,
            out float localY,
            out float localZ)
        {
            gridX = aup.GridX;
            gridY = aup.GridY;
            gridZ = aup.GridZ;
            localX = aup.LocalX;
            localY = aup.LocalY;
            localZ = aup.LocalZ;
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

using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Menu Camera Controller")]
    public sealed class MenuCameraController : MonoBehaviour
    {
        internal enum MenuCameraRoute : byte
        {
            Main = 0,
            Saves = 1,
            Settings = 2,
            Loading = 3,
            Handoff = 4
        }

        private const float MinimumDurationSeconds = 0.05f;
        private const float DefaultPanelRouteSeconds = 0.62f;
        private const float DefaultHandoffRouteSeconds = 0.85f;

        private Camera _camera;
        private Transform _cameraTransform;
        private Vector3 _basePosition;
        private Quaternion _baseRotation = Quaternion.identity;
        private Vector3 _startPosition;
        private Vector3 _controlA;
        private Vector3 _controlB;
        private Vector3 _targetPosition;
        private Quaternion _startRotation = Quaternion.identity;
        private Quaternion _targetRotation = Quaternion.identity;
        private float _elapsed;
        private float _duration = DefaultPanelRouteSeconds;
        private bool _configured;
        private bool _active;
        private MenuCameraRoute _currentRoute = MenuCameraRoute.Main;

        internal bool IsActive => _active;

        internal void Configure(Camera camera)
        {
            if (camera == null && !TryGetComponent(out camera))
                camera = null;

            _camera = camera;
            _cameraTransform = camera != null ? camera.transform : transform;
            if (_cameraTransform == null)
                return;

            if (!_configured)
            {
                Vector3 safePosition = ResolveSafePosition(_cameraTransform.position, Vector3.zero);
                Quaternion safeRotation = ResolveSafeRotation(_cameraTransform.rotation, Quaternion.identity);
                _cameraTransform.SetPositionAndRotation(safePosition, safeRotation);
                _basePosition = safePosition;
                _baseRotation = safeRotation;
                _configured = true;
            }
        }

        internal void BeginRoute(MenuCameraRoute route, float durationSeconds)
        {
            if (!_configured)
                Configure(_camera);
            if (_cameraTransform == null)
                return;

            ResolveRoutePose(route, out Vector3 targetPosition, out Quaternion targetRotation);
            _startPosition = ResolveSafePosition(_cameraTransform.position, _basePosition);
            _startRotation = ResolveSafeRotation(_cameraTransform.rotation, _baseRotation);
            _targetPosition = ResolveSafePosition(targetPosition, _basePosition);
            _targetRotation = ResolveSafeRotation(targetRotation, _baseRotation);
            _duration = math.max(MinimumDurationSeconds, math.isfinite(durationSeconds) ? durationSeconds : DefaultPanelRouteSeconds);
            ResolveControls(_startPosition, _targetPosition, out _controlA, out _controlB);
            _elapsed = 0f;
            _currentRoute = route;
            _active = true;
        }

        internal void BeginHandoff()
        {
            BeginRoute(MenuCameraRoute.Handoff, DefaultHandoffRouteSeconds);
        }

        internal void Advance(float unscaledDeltaTime)
        {
            if (!_active || _cameraTransform == null)
                return;

            if (!math.isfinite(_elapsed))
                _elapsed = 0f;

            if (!math.isfinite(_duration) || _duration < MinimumDurationSeconds)
                _duration = MinimumDurationSeconds;

            float safeDeltaTime = math.isfinite(unscaledDeltaTime) ? math.max(0f, unscaledDeltaTime) : 0f;
            _elapsed = math.min(_duration, _elapsed + safeDeltaTime);
            float t = _duration > 0f ? math.saturate(_elapsed / _duration) : 1f;
            float eased = SmoothStep01(t);
            Vector3 position = ResolveBezier(_startPosition, _controlA, _controlB, _targetPosition, eased);
            Quaternion rotation = ResolveSlerp(_startRotation, _targetRotation, eased);
            if (t >= 1f)
            {
                _cameraTransform.SetPositionAndRotation(_targetPosition, _targetRotation);
                _active = false;
                return;
            }

            position = ResolveSafePosition(position, _targetPosition);
            rotation = ResolveSafeRotation(rotation, _targetRotation);
            _cameraTransform.SetPositionAndRotation(position, rotation);
        }

        private void ResolveRoutePose(MenuCameraRoute route, out Vector3 position, out Quaternion rotation)
        {
            float q = ResolveQualityWeight01();
            float parallax = math.lerp(0.38f, 1.18f, SmoothStep01(q));
            Vector3 right = _baseRotation * Vector3.right;
            Vector3 up = _baseRotation * Vector3.up;
            Vector3 forward = _baseRotation * Vector3.forward;

            switch (route)
            {
                case MenuCameraRoute.Saves:
                    position = _basePosition + (right * -0.34f * parallax) + (up * -0.015f) + (forward * 0.08f * parallax);
                    rotation = _baseRotation * Quaternion.Euler(0.6f, -4.2f * parallax, -0.9f * parallax);
                    return;
                case MenuCameraRoute.Settings:
                    position = _basePosition + (right * 0.32f * parallax) + (up * 0.035f) + (forward * 0.07f * parallax);
                    rotation = _baseRotation * Quaternion.Euler(-0.4f, 4.0f * parallax, 0.85f * parallax);
                    return;
                case MenuCameraRoute.Loading:
                    position = _basePosition + (up * -0.16f) + (forward * 0.24f * parallax);
                    rotation = _baseRotation * Quaternion.Euler(3.0f * parallax, 0f, 0f);
                    return;
                case MenuCameraRoute.Handoff:
                    position = _basePosition + (up * -0.42f) + (forward * 0.62f * parallax);
                    rotation = _baseRotation * Quaternion.Euler(7.0f * parallax, 0f, 0f);
                    return;
                default:
                    position = _basePosition;
                    rotation = _baseRotation;
                    return;
            }
        }

        private void ResolveControls(Vector3 start, Vector3 end, out Vector3 controlA, out Vector3 controlB)
        {
            Vector3 delta = end - start;
            Vector3 forward = _baseRotation * Vector3.forward;
            Vector3 up = _baseRotation * Vector3.up;
            float distance = math.max(0.001f, delta.magnitude);
            float bend = distance * 0.32f;
            controlA = start + (forward * bend) + (up * 0.025f);
            controlB = end - (forward * bend) + (up * -0.015f);
        }

        private static Vector3 ResolveBezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float x = math.saturate(t);
            float omt = 1f - x;
            float omt2 = omt * omt;
            float t2 = x * x;
            return (a * (omt2 * omt)) +
                   (b * (3f * omt2 * x)) +
                   (c * (3f * omt * t2)) +
                   (d * (t2 * x));
        }

        private static Quaternion ResolveSlerp(Quaternion from, Quaternion to, float t)
        {
            float x = math.saturate(t);
            float4 a = new float4(from.x, from.y, from.z, from.w);
            float4 b = new float4(to.x, to.y, to.z, to.w);
            float dot = math.dot(a, b);
            if (!math.isfinite(dot))
                return ResolveSafeRotation(from, Quaternion.identity);

            if (dot < 0f)
            {
                b = -b;
                dot = -dot;
            }

            dot = math.clamp(dot, -1f, 1f);
            if (dot > 0.9995f)
            {
                float4 nlerpValue = math.lerp(a, b, x);
                nlerpValue *= math.rsqrt(math.max(0.000001f, math.dot(nlerpValue, nlerpValue)));
                return new Quaternion(nlerpValue.x, nlerpValue.y, nlerpValue.z, nlerpValue.w);
            }

            float theta0 = math.acos(dot);
            float theta = theta0 * x;
            float sinTheta0 = math.max(0.000001f, math.sin(theta0));
            float sinTheta = math.sin(theta);
            float s0 = math.cos(theta) - (dot * sinTheta / sinTheta0);
            float s1 = sinTheta / sinTheta0;
            float4 value = (a * s0) + (b * s1);
            value *= math.rsqrt(math.max(0.000001f, math.dot(value, value)));
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private static Vector3 ResolveSafePosition(Vector3 value, Vector3 fallback)
        {
            float3 v = new float3(value.x, value.y, value.z);
            if (math.all(math.isfinite(v)))
                return value;

            float3 f = new float3(fallback.x, fallback.y, fallback.z);
            return math.all(math.isfinite(f)) ? fallback : Vector3.zero;
        }

        private static Quaternion ResolveSafeRotation(Quaternion value, Quaternion fallback)
        {
            float4 v = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.dot(v, v);
            if (math.all(math.isfinite(v)) && math.isfinite(lengthSq) && lengthSq > 0.000001f)
            {
                v *= math.rsqrt(lengthSq);
                return new Quaternion(v.x, v.y, v.z, v.w);
            }

            float4 f = new float4(fallback.x, fallback.y, fallback.z, fallback.w);
            float fallbackLengthSq = math.dot(f, f);
            if (math.all(math.isfinite(f)) && math.isfinite(fallbackLengthSq) && fallbackLengthSq > 0.000001f)
            {
                f *= math.rsqrt(fallbackLengthSq);
                return new Quaternion(f.x, f.y, f.z, f.w);
            }

            return Quaternion.identity;
        }

        private static float SmoothStep01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 0f);
            return x * x * (3f - (2f * x));
        }

        private static float ResolveQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }
    }
}

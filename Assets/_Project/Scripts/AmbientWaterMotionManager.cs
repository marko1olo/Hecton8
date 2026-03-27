// ============================================================================
// HECTON-8 — AmbientWaterMotionManager.cs
// Centralized visual bob/sway updater. One tick for many decorative props.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4900)]
    [AddComponentMenu("Hecton/Physics/Ambient Water Motion Manager")]
    public sealed class AmbientWaterMotionManager : MonoBehaviour, ITickable
    {
        private static AmbientWaterMotionManager _instance;

        [Header("Observer / LOD")]
        [SerializeField] private Transform lodObserver;
        [SerializeField] private float nearDistance = 20f;
        [SerializeField] private float mediumDistance = 45f;
        [SerializeField] private float farDistance = 90f;
        [SerializeField] private float cullDistance = 150f;
        [SerializeField, Range(1, 8)] private int mediumDivisor = 2;
        [SerializeField, Range(1, 16)] private int farDivisor = 4;
        [SerializeField, Range(1, 32)] private int cullDivisor = 8;

        [Header("Global")]
        [SerializeField] private float globalAmplitude = 1f;
        [SerializeField] private float globalFrequency = 1f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugActiveObjects;
        [SerializeField] private int _debugNearCount;
        [SerializeField] private int _debugMediumCount;
        [SerializeField] private int _debugFarCount;
        [SerializeField] private int _debugCulledCount;

        private readonly List<AmbientWaterMotion> _objects = new List<AmbientWaterMotion>(128);
        private float _time;
        private int _frameCounter;

        public static AmbientWaterMotionManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            TryResolveObserver();
        }

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((ITickable)this);
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((ITickable)this);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void Register(AmbientWaterMotion motion)
        {
            if (motion == null)
                return;

            if (!_objects.Contains(motion))
                _objects.Add(motion);

            _debugActiveObjects = _objects.Count;
        }

        public void Unregister(AmbientWaterMotion motion)
        {
            _objects.Remove(motion);
            _debugActiveObjects = _objects.Count;
        }

        public void Tick(float deltaTime)
        {
            if (_objects.Count == 0)
                return;

            _frameCounter++;
            _time += deltaTime;
            if (_time > 100000f)
                _time -= 100000f;

            TryResolveObserver();

            _debugNearCount = 0;
            _debugMediumCount = 0;
            _debugFarCount = 0;
            _debugCulledCount = 0;

            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                AmbientWaterMotion motion = _objects[i];
                if (motion == null || motion.CachedTransform == null)
                {
                    int last = _objects.Count - 1;
                    _objects[i] = _objects[last];
                    _objects.RemoveAt(last);
                    continue;
                }

                if (!ShouldUpdate(motion, i))
                    continue;

                ApplyMotion(motion);
            }

            _debugActiveObjects = _objects.Count;
        }

        private bool ShouldUpdate(AmbientWaterMotion motion, int index)
        {
            if (!motion.AllowDistanceLod || lodObserver == null)
            {
                _debugNearCount++;
                return true;
            }

            Vector3 pos = motion.CachedTransform.position;
            Vector3 observerPos = lodObserver.position;
            float bias = Mathf.Max(0.1f, motion.LodBias);
            float dx = pos.x - observerPos.x;
            float dy = pos.y - observerPos.y;
            float dz = pos.z - observerPos.z;
            float distanceSq = dx * dx + dy * dy + dz * dz;

            float nearSq = nearDistance * nearDistance * bias * bias;
            float mediumSq = mediumDistance * mediumDistance * bias * bias;
            float farSq = farDistance * farDistance * bias * bias;
            float cullSq = cullDistance * cullDistance * bias * bias;

            if (distanceSq <= nearSq)
            {
                _debugNearCount++;
                return true;
            }

            if (distanceSq <= mediumSq)
            {
                _debugMediumCount++;
                return ((_frameCounter + index) % Mathf.Max(1, mediumDivisor)) == 0;
            }

            if (distanceSq <= farSq)
            {
                _debugFarCount++;
                return ((_frameCounter + index) % Mathf.Max(1, farDivisor)) == 0;
            }

            _debugCulledCount++;
            return distanceSq <= cullSq && ((_frameCounter + index) % Mathf.Max(1, cullDivisor)) == 0;
        }

        private void ApplyMotion(AmbientWaterMotion motion)
        {
            Transform tr = motion.CachedTransform;
            Vector3 worldPos = tr.position;
            Vector3 volumeCurrent = CurrentVolume.SampleAt(worldPos);
            float3 phantomCurrent = CurrentManager.SampleHorizontal(
                new float3(worldPos.x, worldPos.y, worldPos.z),
                _time,
                0.018f,
                0.12f,
                motion.CurrentCoupling);

            Vector3 current = volumeCurrent + new Vector3(phantomCurrent.x, phantomCurrent.y, phantomCurrent.z);
            float currentMagnitude = current.magnitude;
            Vector3 currentDir = currentMagnitude > 0.0001f ? current / currentMagnitude : Vector3.forward;

            float t = (_time + motion.Phase) * Mathf.Max(0f, motion.BaseFrequency * globalFrequency);
            float bobY = Mathf.Sin(t * 1.13f) * motion.VerticalAmplitude;
            float bobX = Mathf.Sin(t * 0.91f) * motion.PositionalAmplitude.x;
            float bobZ = Mathf.Cos(t * 1.07f) * motion.PositionalAmplitude.z;

            Vector3 offset = new Vector3(
                bobX + currentDir.x * currentMagnitude * 0.03f * motion.CurrentCoupling,
                bobY,
                bobZ + currentDir.z * currentMagnitude * 0.03f * motion.CurrentCoupling) * globalAmplitude;

            float pitch = Mathf.Sin(t * 0.87f) * motion.AngularAmplitude.x + currentDir.z * currentMagnitude * 2f;
            float yaw = Mathf.Sin(t * 0.43f) * motion.AngularAmplitude.y;
            float roll = Mathf.Cos(t * 0.79f) * motion.AngularAmplitude.z - currentDir.x * currentMagnitude * 3f;

            tr.localPosition = motion.RestLocalPosition + offset;
            tr.localRotation = motion.RestLocalRotation * Quaternion.Euler(pitch, yaw, roll);
        }

        private void TryResolveObserver()
        {
            if (lodObserver != null)
                return;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                lodObserver = mainCam.transform;
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                lodObserver = player.transform;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (nearDistance < 1f) nearDistance = 1f;
            if (mediumDistance < nearDistance) mediumDistance = nearDistance;
            if (farDistance < mediumDistance) farDistance = mediumDistance;
            if (cullDistance < farDistance) cullDistance = farDistance;
            if (globalAmplitude < 0f) globalAmplitude = 0f;
            if (globalFrequency < 0f) globalFrequency = 0f;
        }
#endif
    }
}

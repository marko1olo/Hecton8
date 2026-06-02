using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class HectonWorldShellVisualDriver1428 : MonoBehaviour
    {
        [SerializeField] private Transform[] causticRibs;
        [SerializeField] private Transform[] hazeBands;
        [SerializeField] private Transform[] suspendedParticles;
        [SerializeField] private Light[] pulseLights;
        [SerializeField] private float motionWeight = 1f;

        private Vector3[] _causticBasePositions;
        private Vector3[] _hazeBasePositions;
        private Vector3[] _hazeBaseScales;
        private Vector3[] _particleBasePositions;
        private Quaternion[] _particleBaseRotations;
        private float[] _lightBaseIntensities;

        private void Awake()
        {
            BuildColdCachesIfNeeded();
            CaptureBases();
        }

        private void LateUpdate()
        {
            float weight = Mathf.Clamp01(motionWeight);
            if (weight <= 0f)
                return;

            float time = Time.time;
            AnimateCaustics(time, weight);
            AnimateHaze(time, weight);
            AnimateParticles(time, weight);
            AnimateLights(time, weight);
        }

        private void BuildColdCachesIfNeeded()
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude);

            if (causticRibs == null || causticRibs.Length == 0)
                causticRibs = CollectTransformsByPrefix(transforms, "WATER_CAUSTIC_RIB_");

            if (hazeBands == null || hazeBands.Length == 0)
                hazeBands = CollectTransformsByPrefix(transforms, "ABYSS_HAZE_BAND_");

            if (suspendedParticles == null || suspendedParticles.Length == 0)
                suspendedParticles = CollectTransformsByPrefix(transforms, "SUSPENDED_PARTICULATE_");

            if (pulseLights == null || pulseLights.Length == 0)
                pulseLights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        }

        private static Transform[] CollectTransformsByPrefix(Transform[] transforms, string prefix)
        {
            var collected = new List<Transform>(16); // COLD ALLOC: editor/runtime shell startup cache, owner: HectonWorldShellVisualDriver1428.
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name.StartsWith(prefix, System.StringComparison.Ordinal))
                    collected.Add(candidate);
            }

            return collected.ToArray();
        }

        private void CaptureBases()
        {
            _causticBasePositions = CapturePositions(causticRibs);
            _hazeBasePositions = CapturePositions(hazeBands);
            _hazeBaseScales = CaptureScales(hazeBands);
            _particleBasePositions = CapturePositions(suspendedParticles);
            _particleBaseRotations = CaptureRotations(suspendedParticles);
            _lightBaseIntensities = CaptureIntensities(pulseLights);
        }

        private static Vector3[] CapturePositions(Transform[] transforms)
        {
            int length = transforms == null ? 0 : transforms.Length;
            var positions = new Vector3[length]; // COLD ALLOC: transform base cache, owner: HectonWorldShellVisualDriver1428.
            for (int i = 0; i < length; i++)
                positions[i] = transforms[i] != null ? transforms[i].localPosition : Vector3.zero;
            return positions;
        }

        private static Vector3[] CaptureScales(Transform[] transforms)
        {
            int length = transforms == null ? 0 : transforms.Length;
            var scales = new Vector3[length]; // COLD ALLOC: transform scale cache, owner: HectonWorldShellVisualDriver1428.
            for (int i = 0; i < length; i++)
                scales[i] = transforms[i] != null ? transforms[i].localScale : Vector3.one;
            return scales;
        }

        private static Quaternion[] CaptureRotations(Transform[] transforms)
        {
            int length = transforms == null ? 0 : transforms.Length;
            var rotations = new Quaternion[length]; // COLD ALLOC: transform rotation cache, owner: HectonWorldShellVisualDriver1428.
            for (int i = 0; i < length; i++)
                rotations[i] = transforms[i] != null ? transforms[i].localRotation : Quaternion.identity;
            return rotations;
        }

        private static float[] CaptureIntensities(Light[] lights)
        {
            int length = lights == null ? 0 : lights.Length;
            var intensities = new float[length]; // COLD ALLOC: light intensity cache, owner: HectonWorldShellVisualDriver1428.
            for (int i = 0; i < length; i++)
                intensities[i] = lights[i] != null ? lights[i].intensity : 0f;
            return intensities;
        }

        private void AnimateCaustics(float time, float weight)
        {
            if (causticRibs == null || _causticBasePositions == null)
                return;

            int count = Mathf.Min(causticRibs.Length, _causticBasePositions.Length);
            for (int i = 0; i < count; i++)
            {
                Transform rib = causticRibs[i];
                if (rib == null)
                    continue;

                float phase = time * 0.32f + i * 0.73f;
                Vector3 offset = new Vector3(Mathf.Sin(phase) * 0.42f, Mathf.Sin(phase * 0.71f) * 0.05f, 0f) * weight;
                rib.localPosition = _causticBasePositions[i] + offset;
            }
        }

        private void AnimateHaze(float time, float weight)
        {
            if (hazeBands == null || _hazeBasePositions == null || _hazeBaseScales == null)
                return;

            int count = Mathf.Min(hazeBands.Length, Mathf.Min(_hazeBasePositions.Length, _hazeBaseScales.Length));
            for (int i = 0; i < count; i++)
            {
                Transform haze = hazeBands[i];
                if (haze == null)
                    continue;

                float phase = time * 0.18f + i * 1.31f;
                haze.localPosition = _hazeBasePositions[i] + new Vector3(Mathf.Sin(phase) * 0.18f, Mathf.Sin(phase * 0.57f) * 0.08f, 0f) * weight;
                haze.localScale = _hazeBaseScales[i] + new Vector3(Mathf.Sin(phase * 0.43f) * 0.35f * weight, 0f, 0f);
            }
        }

        private void AnimateParticles(float time, float weight)
        {
            if (suspendedParticles == null || _particleBasePositions == null || _particleBaseRotations == null)
                return;

            int count = Mathf.Min(suspendedParticles.Length, Mathf.Min(_particleBasePositions.Length, _particleBaseRotations.Length));
            for (int i = 0; i < count; i++)
            {
                Transform particle = suspendedParticles[i];
                if (particle == null)
                    continue;

                float phase = time * 0.27f + i * 0.91f;
                particle.localPosition = _particleBasePositions[i] + new Vector3(
                    Mathf.Sin(phase * 0.7f) * 0.08f,
                    Mathf.Sin(phase) * 0.16f,
                    Mathf.Cos(phase * 0.5f) * 0.08f) * weight;
                particle.localRotation = _particleBaseRotations[i] * Quaternion.Euler(0f, Mathf.Sin(phase) * 3.5f * weight, 0f);
            }
        }

        private void AnimateLights(float time, float weight)
        {
            if (pulseLights == null || _lightBaseIntensities == null)
                return;

            int count = Mathf.Min(pulseLights.Length, _lightBaseIntensities.Length);
            for (int i = 0; i < count; i++)
            {
                Light lightSource = pulseLights[i];
                if (lightSource == null)
                    continue;

                float phase = time * 0.68f + i * 1.17f;
                float pulse = 0.86f + Mathf.Sin(phase) * 0.14f * weight;
                lightSource.intensity = _lightBaseIntensities[i] * pulse;
            }
        }
    }
}

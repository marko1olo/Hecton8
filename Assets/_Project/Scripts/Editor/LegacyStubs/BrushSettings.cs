using UnityEngine;

namespace UnityEditor.Polybrush
{
    /// <summary>
    /// Legacy compatibility stub for abandoned Polybrush brush settings assets.
    /// </summary>
    public sealed class BrushSettings : ScriptableObject
    {
        public float brushRadiusMin = 0.001f;
        public float brushRadiusMax = 5f;
        public float _radius = 1f;
        public float _falloff = 0.5f;
        public float _strength = 1f;
        public AnimationCurve _curve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public bool allowNonNormalizedFalloff;
    }
}

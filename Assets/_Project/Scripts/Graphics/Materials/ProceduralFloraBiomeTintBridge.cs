using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using UnityEngine;

namespace Hecton8.Graphics.Materials
{
    /// <summary>
    /// Drains biome transition signals into global flora shader tint state.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-87)]
    public sealed class ProceduralFloraBiomeTintBridge : MonoBehaviour, IUpdatable
    {
        private static readonly int FloraBiomeTintId = Shader.PropertyToID("_HectonFloraBiomeTint");
        private static readonly int FloraBiomeTintParamsId = Shader.PropertyToID("_HectonFloraBiomeTintParams");

        [Header("Biome Tint")]
        [Tooltip("Fallback flora tint published before a biome signal is consumed.")]
        [SerializeField]
        private Vector4 _defaultTint = new Vector4(0.72f, 0.96f, 0.84f, 1f);

        [Tooltip("Global tint blend strength consumed by procedural flora shaders.")]
        [SerializeField, Range(0f, 1f)]
        private float _tintStrength = 0.32f;

        private uint _lastBiomeHash = uint.MaxValue;
        private Vector4 _lastTint;
        private Vector4 _lastParams;
        private bool _registered;

        private void OnEnable()
        {
            _lastBiomeHash = uint.MaxValue;
            PublishTint(_defaultTint, 0u);
            TryRegisterTick();
        }

        private void Start()
        {
            TryRegisterTick();
        }

        private void OnDisable()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }

            PublishTint(_defaultTint, 0u);
            _lastBiomeHash = uint.MaxValue;
        }

        private void TryRegisterTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        /// <summary>
        /// Consumes biome change signals and republishes flora shader globals only when the biome hash changes.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta time; unused because biome tint is signal driven.</param>
        public void Tick(float deltaTime)
        {
            ReadOnlySpan<BiomeChangedSignal> signals = SignalBus<BiomeChangedSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                uint currentHash = signals[i].CurrentBiomeHash;
                if (currentHash == _lastBiomeHash)
                    continue;

                _lastBiomeHash = currentHash;
                PublishTint(ResolveBiomeTint(currentHash), currentHash);
            }
        }

        private void PublishTint(Vector4 tint, uint biomeHash)
        {
            tint = SanitizeTint(tint);
            Vector4 parameters = new Vector4(Mathf.Clamp01(SanitizeFloat(_tintStrength, 0.32f)), biomeHash, 0f, 0f);
            if (_lastTint == tint && _lastParams == parameters)
                return;

            Shader.SetGlobalVector(FloraBiomeTintId, tint);
            Shader.SetGlobalVector(FloraBiomeTintParamsId, parameters);
            _lastTint = tint;
            _lastParams = parameters;
        }

        private static Vector4 ResolveBiomeTint(uint biomeHash)
        {
            uint hash = biomeHash == 0u ? 0x6C8E9CF5u : biomeHash;
            hash = Mix(hash);
            float r = Mathf.Lerp(0.36f, 0.74f, Byte01(hash));
            float g = Mathf.Lerp(0.62f, 1.00f, Byte01(hash >> 8));
            float b = Mathf.Lerp(0.64f, 1.00f, Byte01(hash >> 16));
            b = Mathf.Max(b, g * 0.78f);
            return new Vector4(r, g, b, 1f);
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        private static float Byte01(uint value)
        {
            return (value & 0xFFu) * (1f / 255f);
        }

        private static Vector4 SanitizeTint(Vector4 tint)
        {
            tint.x = SanitizeFloat(tint.x, 0.72f);
            tint.y = SanitizeFloat(tint.y, 0.96f);
            tint.z = SanitizeFloat(tint.z, 0.84f);
            tint.w = 1f;
            return tint;
        }

        private static float SanitizeFloat(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}

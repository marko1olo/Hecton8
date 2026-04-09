// ============================================================================
// HECTON-8 — HectonHazardManager.cs  v1.0
// Глобальный реестр локальных источников опасности (Zero-GC).
// ============================================================================

using System;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Управляет всеми локальными источниками опасности (радиация, тепло).
    /// Используется SurvivalSystem (игрок) и HectonBaseAI (существа) для 
    /// получения данных об опасности в конкретной точке мира.
    /// </summary>
    public sealed class HectonHazardManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════════════
        //  INTERNAL STRUCTS
        // ══════════════════════════════════════════════════════════════════

        private struct RegisteredSource
        {
            public HazardFloat3 Position;
            public float  Intensity;
            public float  Radius;
            public float  InvRadiusSqr; // 1/(Radius^2) для быстрого расчета затухания
            public HazardType Type;
            public bool   IsActive;
            public int    InstanceID;
        }

        // ══════════════════════════════════════════════════════════════════
        //  STATIC ACCESS
        // ══════════════════════════════════════════════════════════════════

        public static HectonHazardManager Instance { get; private set; }

        private const int MAX_SOURCES = 128;
        private const float MinHazardRadius = 0.01f;
        private const float OverflowLogIntervalSeconds = 5f;
        private static readonly RegisteredSource[] _sources = new RegisteredSource[MAX_SOURCES];
        private static int _sourceCount;
        private static float _nextOverflowLogTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
            _sourceCount = 0;
            _nextOverflowLogTime = 0f;
            System.Array.Clear(_sources, 0, _sources.Length);
        }

        // ══════════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════════════════
        //  PUBLIC API (REGISTRY)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Регистрация источника. Вызывается в OnEnable объекта.
        /// </summary>
        public static void Register(int id, Vector3 pos, float intensity, float radius, HazardType type)
        {
            float safeRadius = radius > MinHazardRadius ? radius : MinHazardRadius;

            // Ищем свободный слот или уже существующий ID (на случай двойного вызова)
            int firstFree = -1;
            for (int i = 0; i < MAX_SOURCES; i++)
            {
                if (_sources[i].IsActive)
                {
                    if (_sources[i].InstanceID == id)
                    {
                        UpdateSource(i, pos, intensity, safeRadius);
                        return;
                    }
                }
                else if (firstFree == -1)
                {
                    firstFree = i;
                }
            }

            if (firstFree != -1)
            {
                _sources[firstFree].InstanceID = id;
                _sources[firstFree].Position = pos;
                _sources[firstFree].Intensity = intensity;
                _sources[firstFree].Radius = safeRadius;
                _sources[firstFree].InvRadiusSqr = 1f / (safeRadius * safeRadius);
                _sources[firstFree].Type = type;
                _sources[firstFree].IsActive = true;
                _sourceCount++;
            }
            else
            {
                LogRegistryOverflow();
            }
        }

        /// <summary>
        /// Удаление источника. Вызывается в OnDisable.
        /// </summary>
        public static void Unregister(int id)
        {
            for (int i = 0; i < MAX_SOURCES; i++)
            {
                if (_sources[i].IsActive && _sources[i].InstanceID == id)
                {
                    _sources[i].IsActive = false;
                    _sources[i].InstanceID = 0;
                    _sourceCount--;
                    return;
                }
            }
        }

        /// <summary>
        /// Обновление параметров существующего источника (например, при движении).
        /// </summary>
        public static void UpdateSource(int index, Vector3 pos, float intensity, float radius)
        {
            float safeRadius = radius > MinHazardRadius ? radius : MinHazardRadius;
            _sources[index].Position = pos;
            _sources[index].Intensity = intensity;
            _sources[index].Radius = safeRadius;
            _sources[index].InvRadiusSqr = 1f / (safeRadius * safeRadius);
        }

        // ══════════════════════════════════════════════════════════════════
        //  PUBLIC API (QUERY)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает суммарную интенсивность опасности в точке point для типа type.
        /// Zero-GC: использует фиксированный массив источников.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetHazardIntensity(Vector3 point, HazardType type)
        {
            if (_sourceCount <= 0) return 0f;

            float totalIntensity = 0f;
            float px = point.x;
            float py = point.y;
            float pz = point.z;

            // Прямой итератор по массиву (самый быстрый способ в C# для Unity)
            for (int i = 0; i < MAX_SOURCES; i++)
            {
                if (!_sources[i].IsActive || _sources[i].Type != type) continue;

                float dx = _sources[i].Position.x - px;
                float dy = _sources[i].Position.y - py;
                float dz = _sources[i].Position.z - pz;
                float distSqr = dx * dx + dy * dy + dz * dz;

                float r = _sources[i].Radius;
                if (distSqr < r * r)
                {
                    // Linear falloff: Intensity * (1 - normalizedDist)
                    // Для оптимизации используем квадратичное затухание (быстрее, и физически корректнее для radiation/heat)
                    float t = 1f - (distSqr * _sources[i].InvRadiusSqr);
                    totalIntensity += _sources[i].Intensity * (t * t); // smoothstep-like falloff
                }
            }

            return totalIntensity;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogRegistryOverflow()
        {
            if (Time.unscaledTime < _nextOverflowLogTime)
                return;

            _nextOverflowLogTime = Time.unscaledTime + OverflowLogIntervalSeconds;
            UnityEngine.Debug.LogWarning("[HectonHazardManager] Реестр источников переполнен (MAX_SOURCES=128)!");
        }
    }

    // Вспомогательная структура для математики в стиле Unity.Mathematics
    internal struct HazardFloat3
    {
        public float x;
        public float y;
        public float z;

        public static implicit operator HazardFloat3(Vector3 v) => new HazardFloat3 { x = v.x, y = v.y, z = v.z };
        public static implicit operator Vector3(HazardFloat3 v) => new Vector3(v.x, v.y, v.z);
    }
}

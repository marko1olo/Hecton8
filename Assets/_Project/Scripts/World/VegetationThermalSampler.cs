using System;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        public float GetWaterTemperature(Vector3 position)
        {
            if (!_abyssalThermalGridInitialized ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalThermalGridHandle,
                    BufferID.VegetationAbyssalThermalGrid,
                    _abyssalThermalGridCellCount,
                    out _) ||
                _abyssalThermalGridResolutionXZ <= 0 ||
                _abyssalThermalGridResolutionY <= 0)
            {
                return thermalSurfaceTemperatureCelsius;
            }

            return SampleThermalGridAtPosition(position);
        }

        /// <summary>
        /// Returns a runtime-only cold-stress multiplier derived from abyssal thermal pockets.
        /// 1.0 means neutral water; values above 1 amplify suit heating drain and cold damage.
        /// </summary>
        public float GetDeepColdStressMultiplier(Vector3 position)
        {
            float localTemperature = GetWaterTemperature(position);
            if (localTemperature >= deepColdPocketTemperatureThresholdCelsius)
                return 1f;

            float depth01 = Mathf.InverseLerp(deepColdPocketTemperatureThresholdCelsius, thermalAbyssTemperatureCelsius, localTemperature);
            return LerpClamped(1f, deepColdPocketStressMultiplierMax, depth01);
        }

        /// <summary>
        /// Resolves the authored prefab backing a published mega-wreck section payload.
        /// </summary>

        private void InitializeThermalGridMetadata()
        {
            int horizontalResolution = Mathf.RoundToInt((thermalGridRadius * 2f) / Mathf.Max(1f, thermalGridHorizontalCellSize)) + 1;
            if ((horizontalResolution & 1) == 0)
                horizontalResolution++;

            int verticalResolution = Mathf.RoundToInt(thermalGridDepthMeters / Mathf.Max(1f, thermalGridVerticalCellSize)) + 1;
            _abyssalThermalGridResolutionXZ = Mathf.Max(3, horizontalResolution);
            _abyssalThermalGridResolutionY = Mathf.Max(2, verticalResolution);
            _abyssalThermalGridCellCount = _abyssalThermalGridResolutionXZ * _abyssalThermalGridResolutionXZ * _abyssalThermalGridResolutionY;
        }

        private void ShiftThermalGridRing(Vector3 offset)
        {
            if (_abyssalThermalGridResolutionXZ <= 0 || _abyssalThermalGridResolutionY <= 0)
                return;

            int shiftX = Mathf.RoundToInt(offset.x / Mathf.Max(1f, thermalGridHorizontalCellSize));
            int shiftY = Mathf.RoundToInt(-offset.y / Mathf.Max(1f, thermalGridVerticalCellSize));
            int shiftZ = Mathf.RoundToInt(offset.z / Mathf.Max(1f, thermalGridHorizontalCellSize));
            _abyssalThermalGridRingOffsetX = PositiveModulo(_abyssalThermalGridRingOffsetX + shiftX, _abyssalThermalGridResolutionXZ);
            _abyssalThermalGridRingOffsetY = PositiveModulo(_abyssalThermalGridRingOffsetY + shiftY, _abyssalThermalGridResolutionY);
            _abyssalThermalGridRingOffsetZ = PositiveModulo(_abyssalThermalGridRingOffsetZ + shiftZ, _abyssalThermalGridResolutionXZ);
        }

        private float SampleThermalGridAtPosition(Vector3 position)
        {
            if (!TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalThermalGridHandle,
                    BufferID.VegetationAbyssalThermalGrid,
                    _abyssalThermalGridCellCount,
                    out NativeArray<float> thermalGrid) ||
                _abyssalThermalGridResolutionXZ <= 0 ||
                _abyssalThermalGridResolutionY <= 0 ||
                thermalGridHorizontalCellSize <= 0f ||
                thermalGridVerticalCellSize <= 0f)
            {
                return thermalSurfaceTemperatureCelsius;
            }

            float halfExtent = (_abyssalThermalGridResolutionXZ - 1) * 0.5f * thermalGridHorizontalCellSize;
            float minX = _abyssalThermalGridCenter.x - halfExtent;
            float minZ = _abyssalThermalGridCenter.z - halfExtent;
            float maxY = waterLevel;
            float minY = waterLevel - thermalGridDepthMeters;
            if (position.x < minX || position.z < minZ || position.x > minX + (halfExtent * 2f) || position.z > minZ + (halfExtent * 2f))
                return thermalSurfaceTemperatureCelsius;

            float clampedY = Mathf.Clamp(position.y, minY, maxY);
            float normalizedX = Mathf.Clamp((position.x - minX) / thermalGridHorizontalCellSize, 0f, _abyssalThermalGridResolutionXZ - 1);
            float normalizedZ = Mathf.Clamp((position.z - minZ) / thermalGridHorizontalCellSize, 0f, _abyssalThermalGridResolutionXZ - 1);
            float normalizedY = Mathf.Clamp((maxY - clampedY) / thermalGridVerticalCellSize, 0f, _abyssalThermalGridResolutionY - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(normalizedX), 0, _abyssalThermalGridResolutionXZ - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(normalizedZ), 0, _abyssalThermalGridResolutionXZ - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(normalizedY), 0, _abyssalThermalGridResolutionY - 1);
            int x1 = Mathf.Min(x0 + 1, _abyssalThermalGridResolutionXZ - 1);
            int z1 = Mathf.Min(z0 + 1, _abyssalThermalGridResolutionXZ - 1);
            int y1 = Mathf.Min(y0 + 1, _abyssalThermalGridResolutionY - 1);
            float fracX = normalizedX - x0;
            float fracZ = normalizedZ - z0;
            float fracY = normalizedY - y0;

            float sample000 = thermalGrid[GetThermalGridPhysicalIndex(x0, y0, z0)];
            float sample100 = thermalGrid[GetThermalGridPhysicalIndex(x1, y0, z0)];
            float sample010 = thermalGrid[GetThermalGridPhysicalIndex(x0, y0, z1)];
            float sample110 = thermalGrid[GetThermalGridPhysicalIndex(x1, y0, z1)];
            float sample001 = thermalGrid[GetThermalGridPhysicalIndex(x0, y1, z0)];
            float sample101 = thermalGrid[GetThermalGridPhysicalIndex(x1, y1, z0)];
            float sample011 = thermalGrid[GetThermalGridPhysicalIndex(x0, y1, z1)];
            float sample111 = thermalGrid[GetThermalGridPhysicalIndex(x1, y1, z1)];
            float sampleX00 = LerpClamped(sample000, sample100, fracX);
            float sampleX10 = LerpClamped(sample010, sample110, fracX);
            float sampleX01 = LerpClamped(sample001, sample101, fracX);
            float sampleX11 = LerpClamped(sample011, sample111, fracX);
            float sampleZ0 = LerpClamped(sampleX00, sampleX10, fracZ);
            float sampleZ1 = LerpClamped(sampleX01, sampleX11, fracZ);
            return LerpClamped(sampleZ0, sampleZ1, fracY);
        }

        private int GetThermalGridPhysicalIndex(int x, int y, int z)
        {
            int wrappedX = PositiveModulo(x + _abyssalThermalGridRingOffsetX, _abyssalThermalGridResolutionXZ);
            int wrappedY = PositiveModulo(y + _abyssalThermalGridRingOffsetY, _abyssalThermalGridResolutionY);
            int wrappedZ = PositiveModulo(z + _abyssalThermalGridRingOffsetZ, _abyssalThermalGridResolutionXZ);
            return (wrappedY * _abyssalThermalGridResolutionXZ * _abyssalThermalGridResolutionXZ) +
                   (wrappedZ * _abyssalThermalGridResolutionXZ) +
                   wrappedX;
        }
    }
}

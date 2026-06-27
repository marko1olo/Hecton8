using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ModuleThermalDissipationRate.
    /// Extracted from SubmarineAtmosphereSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ModuleThermalDissipationRate
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        public static float Calculate(
            float currentRoomTemp,
            float moduleWattage,
            float coolantFlowRate,
            float roomVolumeM3,
            float deltaTime,
            float airDensity,
            float airSpecificHeat,
            float minRoomVolume)
        {
            if (float.IsNaN(currentRoomTemp) || float.IsInfinity(currentRoomTemp)) currentRoomTemp = 0f;
            if (float.IsNaN(moduleWattage) || float.IsInfinity(moduleWattage)) moduleWattage = 0f;
            if (float.IsNaN(coolantFlowRate) || float.IsInfinity(coolantFlowRate)) coolantFlowRate = 0f;
            if (float.IsNaN(roomVolumeM3) || float.IsInfinity(roomVolumeM3)) roomVolumeM3 = 0f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) deltaTime = 0f;
            if (float.IsNaN(airDensity) || float.IsInfinity(airDensity)) airDensity = 0f;
            if (float.IsNaN(airSpecificHeat) || float.IsInfinity(airSpecificHeat)) airSpecificHeat = 0f;
            if (float.IsNaN(minRoomVolume) || float.IsInfinity(minRoomVolume)) minRoomVolume = 0f;

            if (deltaTime <= 0f)
                return 0f;

            if (roomVolumeM3 <= minRoomVolume)
                roomVolumeM3 = minRoomVolume;

            float safeCoolantFlowRate = Math.Max(0f, coolantFlowRate);
            float safeModuleWattage = Math.Max(0f, moduleWattage);

            float roomAirMass = roomVolumeM3 * airDensity;
            float roomThermalCapacity = roomAirMass * airSpecificHeat;

            if (roomThermalCapacity <= 0f)
                return 0f;

            float netWattage = Math.Max(0f, safeModuleWattage - safeCoolantFlowRate);
            float energyJoules = netWattage * deltaTime;

            float temperatureDelta = energyJoules / roomThermalCapacity;

            return temperatureDelta;
        }
    }
}

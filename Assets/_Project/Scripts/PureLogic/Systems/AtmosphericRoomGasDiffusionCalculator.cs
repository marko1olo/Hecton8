using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for AtmosphericRoomGasDiffusionCalculator.
    /// Extracted from SubmarineAtmosphereSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class AtmosphericRoomGasDiffusionCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="roomAO2">Parameter representing the roomAO2 (float).</param>
        /// <param name="roomBO2">Parameter representing the roomBO2 (float).</param>
        /// <param name="roomACO2">Parameter representing the roomACO2 (float).</param>
        /// <param name="roomBCO2">Parameter representing the roomBCO2 (float).</param>
        /// <param name="doorAreaM2">Parameter representing the doorAreaM2 (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <param name="doorConductance">Parameter representing the door conductance rate (float).</param>
        /// <param name="maxTransferRatio">Parameter representing the maximum transfer ratio to prevent over-correction (float).</param>
        /// <returns>Returns X=O2 transfer amount, Y=CO2 transfer amount of type Vector2.</returns>
        public static Vector2 Compute(
            float roomAO2,
            float roomBO2,
            float roomACO2,
            float roomBCO2,
            float doorAreaM2,
            float deltaTime,
            float doorConductance,
            float maxTransferRatio)
        {
            float safeRoomAO2 = float.IsNaN(roomAO2) || float.IsInfinity(roomAO2) ? 0f : Math.Max(0f, roomAO2);
            float safeRoomBO2 = float.IsNaN(roomBO2) || float.IsInfinity(roomBO2) ? 0f : Math.Max(0f, roomBO2);
            float safeRoomACO2 = float.IsNaN(roomACO2) || float.IsInfinity(roomACO2) ? 0f : Math.Max(0f, roomACO2);
            float safeRoomBCO2 = float.IsNaN(roomBCO2) || float.IsInfinity(roomBCO2) ? 0f : Math.Max(0f, roomBCO2);
            float safeDoorArea = float.IsNaN(doorAreaM2) || float.IsInfinity(doorAreaM2) ? 0f : Math.Max(0f, doorAreaM2);
            float safeDeltaTime = float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ? 0f : Math.Max(0f, deltaTime);
            float safeConductance = float.IsNaN(doorConductance) || float.IsInfinity(doorConductance) ? 0f : Math.Max(0f, doorConductance);
            float safeRatio = float.IsNaN(maxTransferRatio) || float.IsInfinity(maxTransferRatio) ? 0f : Math.Max(0f, maxTransferRatio);

            if (safeDoorArea <= 0f || safeDeltaTime <= 0f || safeConductance <= 0f)
            {
                return Vector2.Zero;
            }

            // Fick's law simplified: Exchange = Area * Time * Rate * (ConcentrationA - ConcentrationB)
            // Note: Positive value means transfer from A to B. We just return the transfer amount for A to B.
            float transferO2 = (safeRoomAO2 - safeRoomBO2) * safeDoorArea * safeDeltaTime * safeConductance;
            float transferCO2 = (safeRoomACO2 - safeRoomBCO2) * safeDoorArea * safeDeltaTime * safeConductance;

            // Prevent extreme values / infinite loops (e.g. over-transfer)
            float diffO2 = safeRoomAO2 - safeRoomBO2;
            float maxTransferO2 = Math.Abs(diffO2) * safeRatio;
            float clampedTransferO2 = Math.Sign(transferO2) * Math.Min(Math.Abs(transferO2), maxTransferO2);

            float diffCO2 = safeRoomACO2 - safeRoomBCO2;
            float maxTransferCO2 = Math.Abs(diffCO2) * safeRatio;
            float clampedTransferCO2 = Math.Sign(transferCO2) * Math.Min(Math.Abs(transferCO2), maxTransferCO2);

            return new Vector2(clampedTransferO2, clampedTransferCO2);
        }
    }
}

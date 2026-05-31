using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public sealed unsafe partial class ShinobuPhysiologyRuntime
    {
        /// <summary>
        /// Reads a decompression authority row for editor diagnostics. Refuses same-frame job readback.
        /// </summary>
        public bool TryGetDecompressionState(int entityIndex, out DecompressionStateDTO state)
        {
            state = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _decompressionHandle, BufferID.ShinobuDecompressionStates, entityCapacity, out NativeArray<DecompressionStateDTO> states) ||
                (uint)entityIndex >= (uint)states.Length)
                return false;

            state = states[entityIndex];
            return true;
        }

        /// <summary>
        /// Reads a Buhlmann coefficient row for editor diagnostics.
        /// </summary>
        public bool TryGetHaldaneCoefficient(int tissueIndex, out HaldaneTissueCoefficientDTO coefficient)
        {
            coefficient = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _coefficientHandle, BufferID.ShinobuHaldaneCoefficients, ShinobuPhysiologyConstants.TissueCompartmentCount, out NativeArray<HaldaneTissueCoefficientDTO> coefficients) ||
                (uint)tissueIndex >= (uint)coefficients.Length)
                return false;

            coefficient = coefficients[tissueIndex];
            return true;
        }

        /// <summary>
        /// Reads the latest completed decompression black-box row for editor diagnostics.
        /// </summary>
        public bool TryGetLatestDecompressionTelemetry(out DecompressionTelemetryEntry entry)
        {
            entry = default;
            if (_jobScheduled)
                return false;

            if (!TryReadPhysiologyVaultArray(in _decompressionTelemetryHandle, ShinobuPhysiologyConstants.DecompressionTelemetryRingBuffer, ShinobuPhysiologyConstants.TelemetryFrameCount, out NativeArray<DecompressionTelemetryEntry> telemetry) ||
                telemetry.Length <= 0)
                return false;

            int index = _decompressionTelemetryCursor - 1;
            if (index < 0)
                index += telemetry.Length;
            entry = telemetry[index % telemetry.Length];
            return entry.Frame != 0u;
        }

        /// <summary>
        /// Editor-only play-mode gas override. Writes Vault rows directly; no runtime UI allocation path.
        /// </summary>
        public bool SetEditorBreathingGasNitrogenFraction(float nitrogenFraction)
        {
            if (_jobScheduled)
                return false;

            float n2 = math.clamp(ShinobuPhysiologyJobMath.SanitizeFinite(nitrogenFraction, ShinobuPhysiologyConstants.NitrogenFraction), 0f, 0.95f);
            float co2 = ShinobuPhysiologyConstants.CarbonDioxideFraction;
            float o2 = math.max(0.05f, 1f - n2 - co2);
            BreathingGasFractionsDTO gas = ShinobuPhysiologyJobMath.SanitizeBreathingGas(new BreathingGasFractionsDTO
            {
                OxygenFraction = o2,
                NitrogenFraction = n2,
                CarbonDioxideFraction = co2,
                GasHash = 0x45444753u,
                Flags = ShinobuPhysiologyFlags.CsvOverride
            });

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsPhysiologyVaultHandle(in _breathingGasHandle, ShinobuPhysiologyConstants.BreathingGasFractionsBuffer) ||
                !vault.TryAcquireWriteLock(in _breathingGasHandle, OwnerSystem, out NativeArray<BreathingGasFractionsDTO> rows))
            {
                return false;
            }

            try
            {
                if (!rows.IsCreated)
                    return false;

                int count = math.min(entityCapacity, rows.Length);
                for (int i = 0; i < count; i++)
                    rows[i] = gas;

                _breathingGasOverride = gas;
                _breathingGasOverrideActive = true;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _breathingGasHandle, OwnerSystem);
            }
        }
    }
}

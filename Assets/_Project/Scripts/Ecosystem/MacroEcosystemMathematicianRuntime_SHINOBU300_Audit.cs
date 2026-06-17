using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using EcosystemSectorDTO = Hecton8.Core.Contracts.EcosystemSectorDTO;

namespace Hecton8.Ecosystem
{
    public unsafe sealed partial class MacroEcosystemMathematicianRuntime
    {
        public static bool RunShinobu300SelfAudit(out string failure)
        {
            failure = string.Empty;

            try
            {
                MacroEcosystemLayoutManifest.VerifyColdBoot();
            }
            catch (CriticalBootException)
            {
                failure = "Layout manifest failed.";
                return false;
            }
            catch (ArgumentException)
            {
                failure = "Layout manifest failed.";
                return false;
            }
            catch (InvalidOperationException)
            {
                failure = "Layout manifest failed.";
                return false;
            }

            if (!CheckSize<EcosystemSectorDTO>(64, ref failure) ||
                !CheckOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.SectorHash), 0, ref failure) ||
                !CheckOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.FloraBiomass), 8, ref failure) ||
                !CheckOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.PreyBiomass), 12, ref failure) ||
                !CheckOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.PredatorBiomass), 16, ref failure) ||
                !CheckOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.CarryingCapacity), 20, ref failure) ||
                !CheckOffset<EcosystemSectorDTO>(nameof(EcosystemSectorDTO.DominantSpeciesMask), 24, ref failure) ||
                !CheckSize<BiomeEcosystemSpecDTO>(64, ref failure) ||
                !CheckOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.BiomeHash), 0, ref failure) ||
                !CheckOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.CarryingCapacityPrey), 4, ref failure) ||
                !CheckOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.ToxinPenalty), 20, ref failure) ||
                !CheckOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.BaseBirthRate), 24, ref failure) ||
                !CheckOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.PredatorConversionRate), 32, ref failure) ||
                !CheckOffset<BiomeEcosystemSpecDTO>(nameof(BiomeEcosystemSpecDTO.PredatorStarvationRate), 36, ref failure) ||
                !CheckSize<MacroEcosystemTelemetryEntry>(64, ref failure) ||
                !CheckOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TotalFloraBiomass), 8, ref failure) ||
                !CheckOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.DiffusionTransfers), 20, ref failure) ||
                !CheckOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.Flags), 44, ref failure) ||
                !CheckOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TimingMode), 56, ref failure) ||
                !CheckOffset<MacroEcosystemTelemetryEntry>(nameof(MacroEcosystemTelemetryEntry.TimingSourceHash), 60, ref failure))
            {
                return false;
            }

            int lowSubsteps = MacroEcosystemMath.ResolveIntegrationSubsteps(0f);
            int middleSubsteps = MacroEcosystemMath.ResolveIntegrationSubsteps(0.5f);
            int highSubsteps = MacroEcosystemMath.ResolveIntegrationSubsteps(1f);
            if (lowSubsteps < 1 ||
                highSubsteps > 6 ||
                middleSubsteps < lowSubsteps ||
                highSubsteps < middleSubsteps)
            {
                failure = "GlobalQualityWeight integration substeps are not continuous monotonic 1..6.";
                return false;
            }

            float nanClamp = MacroEcosystemMath.SanitizeBiomass(float.NaN, 10f);
            float overClamp = MacroEcosystemMath.SanitizeBiomass(20f, 10f);
            if (nanClamp != 0f || math.abs(overClamp - 10f) > 0.0001f)
            {
                failure = "Biomass finite/carrying-capacity clamps failed.";
                return false;
            }

            uint dominantMask = MacroEcosystemMath.PackDominantSpeciesMask(100f, 50f, 10f, 100f);
            if ((dominantMask & MacroEcosystemMath.DominantFlora) == 0u ||
                MacroEcosystemMath.DecodePreyDensity01(dominantMask) <= 0f ||
                MacroEcosystemMath.DecodePredatorDensity01(dominantMask) <= 0f)
            {
                failure = "DominantSpeciesMask packed density proof failed.";
                return false;
            }

            failure = "OK";
            return true;
        }

        private static bool CheckSize<T>(int expected, ref string failure)
            where T : unmanaged
        {
            int observed = UnsafeUtility.SizeOf<T>();
            if (observed == expected)
                return true;

            failure = "DTO size mismatch.";
            return false;
        }

        private static bool CheckOffset<T>(string fieldName, int expected, ref string failure)
            where T : unmanaged
        {
            int observed = (int)Marshal.OffsetOf<T>(fieldName);
            if (observed == expected)
                return true;

            failure = "DTO offset mismatch.";
            return false;
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Hecton8/Ecosystem/Run Macro Ecosystem Self Audit")]
        private static void RunShinobu300SelfAuditMenu()
        {
            if (RunShinobu300SelfAudit(out string failure))
                Hecton8.Core.H8Debug.Log("[SHINOBU_300] Macro ecosystem self audit passed.");
            else
                Hecton8.Core.H8Debug.LogError("[SHINOBU_300] Macro ecosystem self audit failed.");
        }
#endif
    }
}

using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Editor gizmo hook for inspecting SHINOBU_116 sector biomass without spawning fauna objects.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Ecosystem/Macro Ecosystem Heatmap Gizmo")]
    public sealed class MacroEcosystemHeatmapGizmo : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField, Range(1, 10000), Tooltip("Maximum sector cubes drawn by OnDrawGizmos.")]
        private int maxDrawnSectors = 512;

        [SerializeField, Range(0.01f, 1f), Tooltip("Scale multiplier applied to the 1 km sector wire cubes.")]
        private float cubeScale = 0.96f;

        private void OnDrawGizmos()
        {
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;

            if (vault == null ||
                !vault.TryGetBuffer<EcosystemSectorDTO>(BufferID.ShinobuMacroEcosystemSectorFront, out NativeArray<EcosystemSectorDTO> sectors) ||
                !vault.TryGetBuffer<EcosystemSectorCoordDTO>(BufferID.ShinobuMacroEcosystemSectorCoords, out NativeArray<EcosystemSectorCoordDTO> coords) ||
                !vault.TryGetBuffer<MacroEcosystemTuningDTO>(BufferID.ShinobuMacroEcosystemTuning, out NativeArray<MacroEcosystemTuningDTO> tuning) ||
                !sectors.IsCreated ||
                !coords.IsCreated ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                return;
            }

            MacroEcosystemTuningDTO tune = MacroEcosystemTuningDTO.Sanitize(tuning[0]);
            int count = math.min(math.min(sectors.Length, coords.Length), math.max(1, maxDrawnSectors));
            float invPrey = math.rcp(math.max(1f, tune.CarryingCapacityPrey));
            float invPredator = math.rcp(math.max(1f, tune.CarryingCapacityPredator));
            Vector3 size = Vector3.one * (1000f * math.clamp(cubeScale, 0.01f, 1f));
            for (int i = 0; i < count; i++)
            {
                EcosystemSectorDTO sector = sectors[i];
                EcosystemSectorCoordDTO coord = coords[i];
                float prey01 = math.saturate(sector.PreyBiomass * invPrey);
                float predator01 = math.saturate(sector.PredatorBiomass * invPredator);
                float toxin01 = math.saturate(sector.ToxinLevel);
                Gizmos.color = new Color(predator01, toxin01, prey01, 0.8f);
                Gizmos.DrawWireCube(
                    new Vector3((float)coord.SectorX * 1000f, (float)coord.SectorY * 1000f, (float)coord.SectorZ * 1000f),
                    size);
            }
        }
    }
}

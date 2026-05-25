using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using EcosystemSectorDTO = Hecton8.Core.Contracts.EcosystemSectorDTO;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Editor gizmo hook for inspecting SHINOBU_300 sector biomass without spawning fauna objects.
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
#if UNITY_EDITOR
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!TryReadBuffer(vault, BufferID.ShinobuMacroEcosystemSectorFront, out NativeArray<EcosystemSectorDTO> sectors) ||
                !TryReadBuffer(vault, BufferID.ShinobuMacroEcosystemSectorCoords, out NativeArray<EcosystemSectorCoordDTO> coords) ||
                !TryReadBuffer(vault, BufferID.ShinobuMacroEcosystemTuning, out NativeArray<MacroEcosystemTuningDTO> tuning) ||
                !sectors.IsCreated ||
                !coords.IsCreated ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                return;
            }

            MacroEcosystemTuningDTO tune = MacroEcosystemTuningDTO.Sanitize(tuning[0]);
            int count = math.min(math.min(sectors.Length, coords.Length), math.max(1, maxDrawnSectors));
            Vector3 size = Vector3.one * (1000f * math.clamp(cubeScale, 0.01f, 1f));
            for (int i = 0; i < count; i++)
            {
                EcosystemSectorDTO sector = sectors[i];
                EcosystemSectorCoordDTO coord = coords[i];
                float capacity = math.max(1f, math.select(tune.CarryingCapacityPrey + tune.CarryingCapacityPredator, sector.CarryingCapacity, math.isfinite(sector.CarryingCapacity) & sector.CarryingCapacity > 0f));
                float flora01 = math.saturate(sector.FloraBiomass * math.rcp(capacity));
                float prey01 = math.saturate(sector.PreyBiomass * math.rcp(capacity));
                float predator01 = math.saturate(sector.PredatorBiomass * math.rcp(capacity));
                Gizmos.color = new Color(predator01, flora01, prey01, 0.8f);
                Gizmos.DrawWireCube(
                    new Vector3((float)coord.SectorX * 1000f, (float)coord.SectorY * 1000f, (float)coord.SectorZ * 1000f),
                    size);
            }
#endif
        }

        private static bool TryReadBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return
                vault != null &&
                vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                vault.TryReadHandle(in handle, out buffer) &&
                buffer.IsCreated;
        }
    }
}

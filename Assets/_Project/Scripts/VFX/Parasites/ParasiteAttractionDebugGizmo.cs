#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.VFX.Parasites
{
    [ExecuteAlways]
    public sealed class ParasiteAttractionDebugGizmo : MonoBehaviour
    {
        public static bool DrawTargets;

        private void OnDrawGizmos()
        {
            if (!DrawTargets)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuParasiteTargets, out VaultGenerationHandle<ParasiteTargetDTO> targetsHandle) ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuParasiteTargetCount, out VaultGenerationHandle<int> countHandle) ||
                !vault.TryReadHandle(in targetsHandle, out NativeArray<ParasiteTargetDTO> targets) ||
                !vault.TryReadHandle(in countHandle, out NativeArray<int> countBuffer) ||
                !targets.IsCreated ||
                !countBuffer.IsCreated ||
                countBuffer.Length <= 0)
            {
                return;
            }

            Vector3 origin = Vector3.zero;
            IPlayerRuntimeContext player = GlobalRegistry.Player;
            if (player != null &&
                player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                math.all(math.isfinite(snapshot.RuntimePosition)))
            {
                origin = new Vector3(snapshot.RuntimePosition.x, snapshot.RuntimePosition.y, snapshot.RuntimePosition.z);
            }

            int count = math.clamp(countBuffer[0], 0, math.min(targets.Length, ParasiteSwarmContracts.MaxTargetCount));
            for (int i = 0; i < count; i++)
            {
                ParasiteTargetDTO target = targets[i];
                if (!math.all(math.isfinite(target.LocalPosition)) || !math.isfinite(target.AttractionRadius))
                    continue;

                float heat01 = math.saturate(target.ThermalSignature / 120f);
                Gizmos.color = new Color(heat01, 1f - heat01 * 0.35f, 0.16f, 0.85f);
                Gizmos.DrawWireSphere(origin + (Vector3)target.LocalPosition, math.max(0.05f, target.AttractionRadius));
            }
        }
    }
}
#endif

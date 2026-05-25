#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Contracts.Physiology;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physiology
{
    public sealed unsafe partial class ShinobuMetabolismRuntime
    {
        private void OnDrawGizmos()
        {
            if (!drawDebugGizmos || debugGizmoRows <= 0 || !Application.isPlaying)
                return;

            if (_jobScheduled)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            TryReadMetabolismVaultBuffer(
                vault,
                in _stateHandle,
                ShinobuMetabolismConstants.MetabolismStatesBuffer,
                1,
                out NativeArray<MetabolicStateDTO> states);
            TryReadMetabolismVaultBuffer(
                vault,
                in _entityAupHandle,
                ShinobuMetabolismConstants.MetabolismEntityAupsBuffer,
                1,
                out NativeArray<double3> aups);
            if (!states.IsCreated || !aups.IsCreated)
                return;

            int count = math.min(math.min(states.Length, aups.Length), debugGizmoRows);
            for (int i = 0; i < count; i++)
            {
                MetabolicStateDTO state = states[i];
                double3 aup = aups[i];
                if (!math.all(math.isfinite(aup)))
                    continue;

                float temperature01 = math.saturate((state.CoreTemperature - 30f) * math.rcp(12f));
                float barHeight = math.lerp(0.35f, 1.4f, temperature01);
                Vector3 runtimePosition = HectonFloatingOrigin.ToRuntimePosition(aup);
                Vector3 barCenter = runtimePosition + Vector3.up * (1.15f + barHeight * 0.5f);
                Gizmos.color = Color.Lerp(Color.blue, Color.red, temperature01);
                Gizmos.DrawCube(barCenter, new Vector3(0.08f, barHeight, 0.08f));
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using Hecton8.Core;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Thermodynamics
{
    [InitializeOnLoad]
    public static class ReactorThermalDebugGizmo
    {
        static ReactorThermalDebugGizmo()
        {
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView view)
        {
            AbyssalThermodynamicsSolver runtime = AbyssalThermodynamicsSolver.ActiveRuntimeInstance;
            if (runtime == null ||
                !runtime.TryGetNuclearReactorDebugReadback(
                    out Unity.Collections.NativeArray<BaseReactorStateDTO>.ReadOnly reactors,
                    out Unity.Collections.NativeArray<ReactorKinematicStateDTO>.ReadOnly kinematics,
                    out Unity.Collections.NativeArray<ReactorThermalVisualDTO>.ReadOnly visuals,
                    out int count,
                    out ThermalGridTuningDTO gridTuning,
                    out NuclearReactorThermalTuningDTO reactorTuning))
            {
                return;
            }

            int limit = math.min(count, math.min(reactors.Length, kinematics.Length));
            float cell = math.max(0.001f, gridTuning.CellSizeMeters);
            int3 resolution = AbyssalThermalMath.SafeResolution(gridTuning.GridResolution);
            for (int i = 0; i < limit; i++)
            {
                BaseReactorStateDTO reactor = reactors[i];
                if ((reactor.ReactorFlags & BaseReactorStateDTO.FlagActive) == 0u)
                    continue;

                double3 aup = kinematics[i].Aup;
                if (!math.all(math.isfinite(aup)))
                    continue;

                Vector3 runtimePosition = HectonFloatingOrigin.ToRuntimePosition(aup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
                bool inside = ReactorThermalMath.TryMapAupToCell(aup, gridTuning.GridOriginAup, cell, resolution, out int3 voxel);
                float safe = math.max(100f, reactorTuning.SafeCoreTempCelsius);
                float critical = math.max(safe + 1f, reactorTuning.MeltdownCoreTempCelsius);
                float t = math.saturate((reactor.CoreTemperatureCelsius - safe) / math.max(1f, critical - safe));
                Handles.color = inside
                    ? Color.Lerp(Color.cyan, Color.red, t)
                    : new Color(1f, 0f, 1f, 0.8f);
                float size = cell * math.lerp(0.18f, 0.42f, t);
                Handles.SphereHandleCap(0, runtimePosition, Quaternion.identity, size, EventType.Repaint);
                ReactorThermalVisualDTO visual = i < visuals.Length ? visuals[i] : default;
                Handles.Label(
                    runtimePosition + Vector3.up * math.max(0.5f, size),
                    $"R{i} {reactor.CoreTemperatureCelsius:0}C | {visual.GeneratedMegawatts:0.0}MW | rod {reactor.ControlRodInsertion01:0.00}");
                if (inside)
                {
                    Vector3 voxelCenter = HectonFloatingOrigin.ToRuntimePosition(
                        gridTuning.GridOriginAup + new double3((voxel.x + 0.5f) * cell, (voxel.y + 0.5f) * cell, (voxel.z + 0.5f) * cell),
                        HectonFloatingOrigin.CurrentTotalOffsetDouble);
                    Handles.DrawWireCube(voxelCenter, Vector3.one * cell);
                    Handles.DrawLine(runtimePosition, voxelCenter);
                    Handles.color = Color.red;
                    Handles.DotHandleCap(0, voxelCenter, Quaternion.identity, cell * 0.12f, EventType.Repaint);
                }
            }
        }
    }
}
#endif

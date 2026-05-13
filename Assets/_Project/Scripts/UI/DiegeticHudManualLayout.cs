using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    public enum DiegeticHudLayoutAxis : byte
    {
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>
    /// Manual transform layout for diegetic HUD elements. Replaces managed layout-component paths.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiegeticHudManualLayout : MonoBehaviour
    {
        // COLD ALLOC: DiegeticHudManualLayout[128] - registered diegetic HUD layouts for signal-driven rescale - owner: DiegeticHudManualLayout
        private static readonly DiegeticHudManualLayout[] s_registeredLayouts = new DiegeticHudManualLayout[128];
        private static int s_registeredCount;

        [SerializeField] private Transform[] targets;
        [SerializeField] private DiegeticHudLayoutAxis axis;
        [SerializeField] private bool collectDirectChildrenOnEnable = true;
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool releaseNativeStateOnDisable;
        [SerializeField] private float startOffset = -0.22f;
        [SerializeField] private float itemExtent = 0.045f;
        [SerializeField] private float spacing = 0.012f;
        [SerializeField] private float crossOffset;
        [SerializeField] private float depthOffset = 0.002f;

        private NativeArray<DiegeticHudLayoutInput> _inputs;
        private NativeArray<float3> _outputs;
        private bool _inputsRegistered;
        private bool _outputsRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_registeredCount; i++)
                s_registeredLayouts[i] = null;

            s_registeredCount = 0;
        }

        private void OnEnable()
        {
            RegisterLayout(this);

            if (collectDirectChildrenOnEnable)
                CollectDirectChildren();

            if (applyOnEnable)
                RebuildLayout();
        }

        private void OnDisable()
        {
            UnregisterLayout(this);

            if (releaseNativeStateOnDisable)
                DisposeNativeState();
        }

        private void OnDestroy()
        {
            DisposeNativeState();
        }

        public void SetTargets(Transform[] newTargets)
        {
            targets = newTargets;
        }

        public bool RebuildLayout()
        {
            int count = targets != null ? targets.Length : 0;
            if (count <= 0)
                return false;

            EnsureNativeCapacity(count);
            for (int i = 0; i < count; i++)
            {
                _inputs[i] = new DiegeticHudLayoutInput
                {
                    Offset = 0f,
                    CrossOffset = crossOffset,
                    DepthOffset = depthOffset
                };
            }

            DiegeticHudLayoutJob job = new DiegeticHudLayoutJob
            {
                Inputs = _inputs,
                Outputs = _outputs,
                Settings = new DiegeticHudLayoutSettings
                {
                    Axis = (byte)axis,
                    StartOffset = startOffset,
                    ItemExtent = math.max(0f, itemExtent),
                    Spacing = math.max(0f, spacing)
                }
            };

            JobHandle handle = job.Schedule(count, 8);
            handle.Complete();

            for (int i = 0; i < count; i++)
            {
                Transform target = targets[i];
                if (target == null)
                    continue;

                float3 local = _outputs[i];
                target.localPosition = new Vector3(local.x, local.y, local.z);
            }

            return true;
        }

        public static void FlushGlobalRescaleRequests()
        {
            bool rebuild = false;
            while (GlobalSignals.TryDequeueUIRescaleRequest(out UIRescaleRequestSignal _))
                rebuild = true;

            if (!rebuild)
                return;

            for (int i = 0; i < s_registeredCount; i++)
            {
                DiegeticHudManualLayout layout = s_registeredLayouts[i];
                if (layout != null && layout.isActiveAndEnabled)
                    layout.RebuildLayout();
            }
        }

        private static void RegisterLayout(DiegeticHudManualLayout layout)
        {
            if (layout == null)
                return;

            for (int i = 0; i < s_registeredCount; i++)
            {
                if (ReferenceEquals(s_registeredLayouts[i], layout))
                    return;
            }

            if (s_registeredCount >= s_registeredLayouts.Length)
                return;

            s_registeredLayouts[s_registeredCount++] = layout;
        }

        private static void UnregisterLayout(DiegeticHudManualLayout layout)
        {
            if (layout == null)
                return;

            for (int i = 0; i < s_registeredCount; i++)
            {
                if (!ReferenceEquals(s_registeredLayouts[i], layout))
                    continue;

                int last = s_registeredCount - 1;
                s_registeredLayouts[i] = s_registeredLayouts[last];
                s_registeredLayouts[last] = null;
                s_registeredCount = last;
                return;
            }
        }

        private void CollectDirectChildren()
        {
            int childCount = transform.childCount;
            if (childCount <= 0)
                return;

            if (targets == null || targets.Length != childCount)
                targets = new Transform[childCount]; // COLD ALLOC: Transform[childCount] - manual diegetic HUD layout targets - owner: DiegeticHudManualLayout

            for (int i = 0; i < childCount; i++)
                targets[i] = transform.GetChild(i);
        }

        private void EnsureNativeCapacity(int count)
        {
            if (_inputs.IsCreated && _inputs.Length == count && _outputs.IsCreated && _outputs.Length == count)
                return;

            DisposeNativeState();
            _inputs = new NativeArray<DiegeticHudLayoutInput>(
                count,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<DiegeticHudLayoutInput>[count] - manual HUD layout input lane - owner: DiegeticHudManualLayout
            _outputs = new NativeArray<float3>(
                count,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float3>[count] - manual HUD layout output lane - owner: DiegeticHudManualLayout
            NativeMemorySentinel.RegisterNativeArray(_inputs, nameof(DiegeticHudManualLayout), nameof(_inputs), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_outputs, nameof(DiegeticHudManualLayout), nameof(_outputs), NativeAllocationLifetime.Scene);
            _inputsRegistered = true;
            _outputsRegistered = true;
        }

        private void DisposeNativeState()
        {
            if (_inputs.IsCreated)
            {
                if (_inputsRegistered)
                {
                    NativeMemorySentinel.UnregisterNativeArray(_inputs);
                    _inputsRegistered = false;
                }

                _inputs.Dispose();
                _inputs = default;
            }

            if (_outputs.IsCreated)
            {
                if (_outputsRegistered)
                {
                    NativeMemorySentinel.UnregisterNativeArray(_outputs);
                    _outputsRegistered = false;
                }

                _outputs.Dispose();
                _outputs = default;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DiegeticHudLayoutInput
    {
        public float Offset;
        public float CrossOffset;
        public float DepthOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DiegeticHudLayoutSettings
    {
        public byte Axis;
        public float StartOffset;
        public float ItemExtent;
        public float Spacing;
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct DiegeticHudLayoutJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<DiegeticHudLayoutInput> Inputs;
        [WriteOnly] public NativeArray<float3> Outputs;
        public DiegeticHudLayoutSettings Settings;

        public void Execute(int index)
        {
            DiegeticHudLayoutInput input = Inputs[index];
            float lane = Settings.StartOffset + (index * (Settings.ItemExtent + Settings.Spacing)) + input.Offset;
            int axisMask = Settings.Axis & 1;
            float vertical = axisMask;
            float horizontal = 1f - vertical;
            Outputs[index] = new float3(
                (lane * horizontal) + (input.CrossOffset * vertical),
                (input.CrossOffset * horizontal) + (lane * vertical),
                input.DepthOffset);
        }
    }
}

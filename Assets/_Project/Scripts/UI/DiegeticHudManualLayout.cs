using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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
        private static uint s_lastRescaleFrame;
        private static uint s_lastRescaleSourceHash;
        private static uint s_lastRescaleFontScaleBits;
        private static ushort s_lastRescaleReason;

        [SerializeField] private Transform[] targets;
        [SerializeField] private DiegeticHudLayoutAxis axis;
        [SerializeField] private bool collectDirectChildrenOnEnable = true;
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private float startOffset = -0.22f;
        [SerializeField] private float itemExtent = 0.045f;
        [SerializeField] private float spacing = 0.012f;
        [SerializeField] private float crossOffset;
        [SerializeField] private float depthOffset = 0.002f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_registeredCount; i++)
                s_registeredLayouts[i] = null;

            s_registeredCount = 0;
            s_lastRescaleFrame = 0u;
            s_lastRescaleSourceHash = 0u;
            s_lastRescaleFontScaleBits = 0u;
            s_lastRescaleReason = 0;
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
        }

        private void OnDestroy()
        {
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

            DiegeticHudLayoutSettings settings = new DiegeticHudLayoutSettings
            {
                Axis = (byte)axis,
                StartOffset = startOffset,
                ItemExtent = math.max(0f, itemExtent),
                Spacing = math.max(0f, spacing)
            };

            for (int i = 0; i < count; i++)
            {
                Transform target = targets[i];
                if (target == null)
                    continue;

                DiegeticHudLayoutInput input = new DiegeticHudLayoutInput
                {
                    Offset = 0f,
                    CrossOffset = crossOffset,
                    DepthOffset = depthOffset
                };
                float3 local = ComputeLayoutPosition(i, in input, in settings);
                target.localPosition = new Vector3(local.x, local.y, local.z);
            }

            return true;
        }

        public static void FlushGlobalRescaleRequests()
        {
            ReadOnlySpan<UIRescaleRequestSignal> signals = SignalBus<UIRescaleRequestSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            bool rebuild = false;
            for (int i = 0; i < signals.Length; i++)
                rebuild |= TryRecordRescaleSignal(in signals[i]);

            if (!rebuild)
                return;

            RebuildRegisteredLayouts();
        }

        public static void ApplyGlobalRescaleRequest(in UIRescaleRequestSignal signal)
        {
            if (!TryRecordRescaleSignal(in signal))
                return;

            RebuildRegisteredLayouts();
        }

        private static bool TryRecordRescaleSignal(in UIRescaleRequestSignal signal)
        {
            uint fontScaleBits = math.asuint(signal.FontScale);
            if (signal.Frame == s_lastRescaleFrame &&
                signal.SourceHash == s_lastRescaleSourceHash &&
                fontScaleBits == s_lastRescaleFontScaleBits &&
                signal.Reason == s_lastRescaleReason)
            {
                return false;
            }

            s_lastRescaleFrame = signal.Frame;
            s_lastRescaleSourceHash = signal.SourceHash;
            s_lastRescaleFontScaleBits = fontScaleBits;
            s_lastRescaleReason = signal.Reason;
            return true;
        }

        private static void RebuildRegisteredLayouts()
        {
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

        internal static float3 ComputeLayoutPosition(
            int index,
            in DiegeticHudLayoutInput input,
            in DiegeticHudLayoutSettings settings)
        {
            float lane = settings.StartOffset + (index * (settings.ItemExtent + settings.Spacing)) + input.Offset;
            int axisMask = settings.Axis & 1;
            float vertical = axisMask;
            float horizontal = 1f - vertical;
            return new float3(
                (lane * horizontal) + (input.CrossOffset * vertical),
                (input.CrossOffset * horizontal) + (lane * vertical),
                input.DepthOffset);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct DiegeticHudLayoutInput
    {
        [FieldOffset(0)]
        public float Offset;
        [FieldOffset(4)]
        public float CrossOffset;
        [FieldOffset(8)]
        public float DepthOffset;
        [FieldOffset(12)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct DiegeticHudLayoutSettings
    {
        [FieldOffset(0)]
        public byte Axis;
        [FieldOffset(1)]
        private byte _pad0;
        [FieldOffset(2)]
        private ushort _pad1;
        [FieldOffset(4)]
        public float StartOffset;
        [FieldOffset(8)]
        public float ItemExtent;
        [FieldOffset(12)]
        public float Spacing;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DiegeticHudLayoutJob : IJobParallelFor
    {
        [NoAlias] [ReadOnly] public NativeArray<DiegeticHudLayoutInput> Inputs;
        [NoAlias] [WriteOnly] public NativeArray<float3> Outputs;
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

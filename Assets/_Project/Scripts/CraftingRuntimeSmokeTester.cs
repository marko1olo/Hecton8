using Hecton8.Crafting;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Debug/Crafting Runtime Smoke Tester")]
    public sealed class CraftingRuntimeSmokeTester : MonoBehaviour
    {
        private const string NativeMemoryOwner = nameof(CraftingRuntimeSmokeTester);
        private const int SmokeQueueExpectedCapacity = 1;

        [SerializeField] private bool runOnStart;
        [SerializeField, Min(0.001f)] private float taskDurationSeconds = 1f;
        [SerializeField, Range(0.05f, 1f)] private float thermalThrottleMultiplier = 1f;

#pragma warning disable CS0414
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private float _debugPausedProgress;
        [SerializeField] private float _debugFirstPoweredProgress;
        [SerializeField] private float _debugCompletedProgress;
#pragma warning restore CS0414

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        [ContextMenu("Run Crafting Queue Smoke Pass")]
        public void RunSmokePass()
        {
            _debugLastPass = RunAsyncQueueSmoke(
                taskDurationSeconds,
                thermalThrottleMultiplier,
                out _debugPausedProgress,
                out _debugFirstPoweredProgress,
                out _debugCompletedProgress);
        }

        public static bool RunAsyncQueueSmoke(
            float durationSeconds,
            float thermalThrottleMultiplier,
            out float pausedProgress,
            out float firstPoweredProgress,
            out float completedProgress)
        {
            pausedProgress = 0f;
            firstPoweredProgress = 0f;
            completedProgress = 0f;

            float safeDuration = Mathf.Max(0.001f, durationSeconds);
            NativeQueue<Fabricator.CraftingTask> queue = new NativeQueue<Fabricator.CraftingTask>(Allocator.Temp);
            NativeMemorySentinel.RegisterNativeQueue(
                queue,
                SmokeQueueExpectedCapacity,
                NativeMemoryOwner,
                nameof(queue),
                NativeAllocationLifetime.Temp);
            try
            {
                queue.Enqueue(new Fabricator.CraftingTask
                {
                    ResultHashId = 1,
                    ResultQuantity = 1,
                    Progress = 0f,
                    DurationSeconds = safeDuration,
                    PowerMultiplier = 1f
                });

                if (!queue.TryDequeue(out Fabricator.CraftingTask task))
                    return false;

                pausedProgress = task.Progress;
                queue.Enqueue(task);
                if (pausedProgress != 0f)
                    return false;

                if (!queue.TryDequeue(out task))
                    return false;

                bool completed = Fabricator.AdvanceCraftingTask(
                    ref task,
                    safeDuration * 0.5f,
                    thermalThrottleMultiplier,
                    out _,
                    out firstPoweredProgress);
                if (completed || !(firstPoweredProgress > 0f) || !(firstPoweredProgress < 1f))
                    return false;

                queue.Enqueue(task);
                int guard = 0;
                while (!queue.IsEmpty() && guard++ < 8)
                {
                    if (!queue.TryDequeue(out task))
                        return false;

                    completed = Fabricator.AdvanceCraftingTask(
                        ref task,
                        safeDuration,
                        thermalThrottleMultiplier,
                        out _,
                        out completedProgress);

                    if (completed)
                        break;

                    queue.Enqueue(task);
                }

                return completed && completedProgress >= 1f && queue.IsEmpty();
            }
            finally
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(queue));
                queue.Dispose();
            }
        }
    }
}

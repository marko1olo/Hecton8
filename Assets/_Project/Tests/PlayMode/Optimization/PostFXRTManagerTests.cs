using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Optimization;

namespace Hecton8.Tests.Optimization
{
    public class PostFXRTManagerTests
    {
        private PostFXRTManager _manager;
        private GameObject _go;
        private MockRenderTextureLifecycleService _mockLifecycle;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("PostFXRTManager_Test");
            _manager = _go.AddComponent<PostFXRTManager>();

            _mockLifecycle = new MockRenderTextureLifecycleService();
            _manager.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime, null, _mockLifecycle);
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [UnityTest]
        public IEnumerator CheckBudget_WhenUnderBudget_DoesNotLog()
        {
            _mockLifecycle.Allocations.Clear();
            _manager.SlowTick();

            yield return null;

            Assert.IsFalse(_manager.IsOverBudget);
        }

        [UnityTest]
        public IEnumerator CheckBudget_WhenOverBudget_LogsWarning()
        {
            // Allocate 256MB to exceed 128MB budget (ARGB32 is 32 bpp -> 4 bytes per pixel. 8192*8192*4 = 268,435,456 bytes = 256MB)
            _mockLifecycle.Allocations.Add(new RenderTextureAllocationRecord
            {
                Width = 8192,
                Height = 8192,
                Format = RenderTextureFormat.ARGB32,
                OwnerCategory = RenderTextureOwnerCategory.PostFX,
                IsDisposed = false
            });

            // Reset _nextLogTime via reflection
            FieldInfo nextLogTimeField = typeof(PostFXRTManager).GetField("_nextLogTime", BindingFlags.NonPublic | BindingFlags.Static);
            if (nextLogTimeField != null)
            {
                nextLogTimeField.SetValue(null, 0f);
            }

            LogAssert.Expect(LogType.Warning, "[PostFXRTManager] BUDGET EXCEEDED: 256.00 MB / 128.00 MB");

            _manager.SlowTick();

            yield return null;
        }

        [UnityTest]
        public IEnumerator CheckBudget_WhenOverBudget_ThrottlesLogs()
        {
            _mockLifecycle.Allocations.Add(new RenderTextureAllocationRecord
            {
                Width = 8192,
                Height = 8192,
                Format = RenderTextureFormat.ARGB32,
                OwnerCategory = RenderTextureOwnerCategory.PostFX,
                IsDisposed = false
            });

            FieldInfo nextLogTimeField = typeof(PostFXRTManager).GetField("_nextLogTime", BindingFlags.NonPublic | BindingFlags.Static);
            if (nextLogTimeField != null)
            {
                nextLogTimeField.SetValue(null, 0f);
            }

            // Expect exactly one log
            LogAssert.Expect(LogType.Warning, "[PostFXRTManager] BUDGET EXCEEDED: 256.00 MB / 128.00 MB");

            // 1st call - should log
            _manager.SlowTick();

            // 2nd call - should NOT log because it's throttled
            _manager.SlowTick();

            yield return null;
        }
    }

    public class MockRenderTextureLifecycleService : IRenderTextureLifecycleService
    {
        public List<RenderTextureAllocationRecord> Allocations = new List<RenderTextureAllocationRecord>();

        public int TrackedRenderTextureCount => Allocations.Count;
        public long TrackedRenderTextureMemoryBytes => 0;

        public void RegisterAllocation(RenderTexture rt, Component owner, string allocationStackTrace = null) { }
        public void RegisterDisposal(RenderTexture rt) { }
        public void GenerateAuditReport(StringBuilder reportBuilder) { }
        public void GetLeakedRenderTextures(List<RenderTextureAllocationRecord> results) { }

        public void GetAllocationsByCategory(string category, List<RenderTextureAllocationRecord> results) { }

        public void GetAllocationsByCategory(RenderTextureOwnerCategory category, List<RenderTextureAllocationRecord> results)
        {
            if (category == RenderTextureOwnerCategory.PostFX)
            {
                results.AddRange(Allocations);
            }
        }
    }
}

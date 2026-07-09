using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using Hecton8.Optimization;
using Hecton8.Core;

namespace Hecton8.Optimization.Tests
{
    public class MockVramPressure : IVramPressureReadModel
    {
        public bool HasSample { get; set; }
        public float PressureFactor { get; set; }

        // Stubs for remaining interface members
        public float VramPressureFactor { get; set; }
        public float RamPressureFactor { get; set; }
        public float BrgLodDistanceScalar { get; set; }
    }

    public class MockVramMonitor : IVramBudgetReadModel
    {
        public bool IsRenderTextureMemoryOverBudget { get; set; }
        public bool IsTotalVRAMOverBudget { get; set; }
        public byte PressureStateCode { get; set; }

        // Stubs for remaining interface members
        public long TextureMemoryBytes { get; }
        public long RenderTextureMemoryBytes { get; }
        public long TotalVRAMBytes { get; }
        public float RenderTextureBudgetUtilization { get; }
        public bool IsTextureMemoryOverBudget { get; }
        public void GetVRAMBreakdown(out long textureMemoryBytes, out long renderTextureMemoryBytes, out long totalVRAMBytes)
        {
            textureMemoryBytes = 0;
            renderTextureMemoryBytes = 0;
            totalVRAMBytes = 0;
        }
    }

    [TestFixture]
    public class RenderTexturePoolTests
    {
        private GameObject _poolGo;
        private RenderTexturePool _pool;

        private static ulong GetKey(int width, int height, RenderTextureFormat format, int depth)
        {
            uint safeWidth = width > 0 ? (uint)Mathf.Min(width, 0xFFFFF) : 0u;
            uint safeHeight = height > 0 ? (uint)Mathf.Min(height, 0xFFFFF) : 0u;
            uint safeFormat = (uint)((int)format & 0xFFFF);
            uint safeDepth = (uint)Mathf.Clamp(depth, 0, 0xFF);
            return ((ulong)safeWidth << 44) | ((ulong)safeHeight << 24) | ((ulong)safeFormat << 8) | safeDepth;
        }

        [SetUp]
        public void Setup()
        {
            _poolGo = new GameObject("RenderTexturePool");
            _pool = _poolGo.AddComponent<RenderTexturePool>();
            SetPrivateField(_pool, "_lastScreenWidth", Mathf.Max(1, Screen.width));
            SetPrivateField(_pool, "_lastScreenHeight", Mathf.Max(1, Screen.height));
        }

        [TearDown]
        public void Teardown()
        {
            if (_poolGo != null)
            {
                Object.DestroyImmediate(_poolGo);
            }
        }

        [Test]
        public void SlowTick_ChangesScreenSize_DefragsScreenQueues()
        {
            // Set screen size diff from fields
            SetPrivateField(_pool, "_lastScreenWidth", Screen.width + 100);
            SetPrivateField(_pool, "_lastScreenHeight", Screen.height + 100);

            // Rent a texture to simulate pool usage
            var queue = new Queue<RenderTexture>();
            queue.Enqueue(new RenderTexture(100, 100, 0));
            var poolDict = new Dictionary<ulong, Queue<RenderTexture>>();
            poolDict[GetKey(100, 100, RenderTextureFormat.R8, 0)] = queue;
            SetPrivateField(_pool, "_poolR8", poolDict);

            _pool.SlowTick();

            // After SlowTick, with Screen.width != stored width, it clears the pool.
            Assert.That(_pool.TotalPooledCount, Is.EqualTo(0));
        }

        [Test]
        public void SlowTick_HighVramPressure_TrimsPools()
        {
            // Setup a mock vram pressure that is high
            var pressureMock = new MockVramPressure { HasSample = true, PressureFactor = 0.9f };
            SetPrivateField(_pool, "_vramPressure", pressureMock);

            // Rent a texture to simulate pool usage
            var queue = new Queue<RenderTexture>();
            queue.Enqueue(new RenderTexture(100, 100, 0));
            var poolDict = new Dictionary<ulong, Queue<RenderTexture>>();
            poolDict[GetKey(100, 100, RenderTextureFormat.R8, 0)] = queue;
            SetPrivateField(_pool, "_poolR8", poolDict);

            _pool.SlowTick();

            Assert.That(_pool.TotalPooledCount, Is.EqualTo(0));

            bool trimActive = GetPrivateField<bool>(_pool, "_vramPressureTrimActive");
            Assert.That(trimActive, Is.True);
        }

        [Test]
        public void SlowTick_OverBudgetMonitor_TrimsPools()
        {
            // No pressure sample, but monitor says over budget
            var monitorMock = new MockVramMonitor { IsTotalVRAMOverBudget = true };
            SetPrivateField(_pool, "_vramMonitor", monitorMock);

            // Rent a texture to simulate pool usage
            var queue = new Queue<RenderTexture>();
            queue.Enqueue(new RenderTexture(100, 100, 0));
            var poolDict = new Dictionary<ulong, Queue<RenderTexture>>();
            poolDict[GetKey(100, 100, RenderTextureFormat.R8, 0)] = queue;
            SetPrivateField(_pool, "_poolR8", poolDict);

            _pool.SlowTick();

            Assert.That(_pool.TotalPooledCount, Is.EqualTo(0));
        }

        [Test]
        public void SlowTick_NoPressure_DoesNotTrim()
        {
            // Rent a texture to simulate pool usage
            var queue = new Queue<RenderTexture>();
            queue.Enqueue(new RenderTexture(100, 100, 0));
            var poolDict = new Dictionary<ulong, Queue<RenderTexture>>();
            poolDict[GetKey(100, 100, RenderTextureFormat.R8, 0)] = queue;
            SetPrivateField(_pool, "_poolR8", poolDict);

            _pool.SlowTick();

            Assert.That(_pool.TotalPooledCount, Is.EqualTo(1));
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (T)field.GetValue(target);
            }
            return default;
        }
    }
}

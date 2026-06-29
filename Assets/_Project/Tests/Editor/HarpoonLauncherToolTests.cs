#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Hecton8.Core.Contracts.Physics;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class HarpoonLauncherToolTests
    {
        private class TestHarpoonLauncherTool : HarpoonLauncherTool
        {
            public bool ThrowOnWrite { get; set; }
            protected override void WriteTracerPositionData(NativeArray<GpuCableSplinePointDTO> points, Vector3 start, Vector3 end)
            {
                if (ThrowOnWrite)
                    throw new System.InvalidOperationException("Test exception");
                base.WriteTracerPositionData(points, start, end);
            }

            public void InvokeUploadTracerGpuData(Vector3 start, Vector3 end)
            {
                base.UploadTracerGpuData(start, end);
            }
        }

        private GameObject _gameObject;
        private HarpoonLauncherTool _tool;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject("HarpoonLauncherTool_Test");
            _tool = _gameObject.AddComponent<HarpoonLauncherTool>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void LateFrameTick_WhenTracerInactive_DoesNotThrow()
        {
            // By default, private fields are initialized to default values (false, 0, etc.)
            // so _tracerActive is false.
            Assert.DoesNotThrow(() => _tool.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_WhenTracerActiveButNotReady_DoesNotThrow()
        {
            SetPrivateField(_tool, "_tracerActive", true);
            SetPrivateField(_tool, "_tracerTimer", 1.0f);

            // The buffers and material are null, so HasTracerReady should be false.
            Assert.DoesNotThrow(() => _tool.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_WhenTracerActiveAndMaterialNull_ReturnsEarlyWithoutThrowing()
        {
            SetPrivateField(_tool, "_tracerActive", true);
            SetPrivateField(_tool, "_tracerTimer", 1.0f);

            // Set fields to make HasTracerReady() return true, but tracerMaterial is null
            SetPrivateField(_tool, "_tracerPositionBuffer", new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, UnsafeUtility.SizeOf<GpuCableSplinePointDTO>()));
            SetPrivateField(_tool, "_tracerTensionBuffer", new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(float)));
            SetPrivateField(_tool, "_tracerDrawParamsBuffer", new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, UnsafeUtility.SizeOf<GpuCableDrawParamsDTO>()));
            SetPrivateField(_tool, "_tracerPropertyBlock", new MaterialPropertyBlock());

            // Material is naturally null, HasTracerReady() returns true, should return at Material check
            try
            {
                Assert.DoesNotThrow(() => _tool.LateFrameTick());
            }
            finally
            {
                CleanupBuffers();
            }
        }

        private void CleanupBuffers()
        {
            var posBuffer = GetPrivateField<GraphicsBuffer>(_tool, "_tracerPositionBuffer");
            var tenBuffer = GetPrivateField<GraphicsBuffer>(_tool, "_tracerTensionBuffer");
            var drawBuffer = GetPrivateField<GraphicsBuffer>(_tool, "_tracerDrawParamsBuffer");

            if (posBuffer != null) posBuffer.Release();
            if (tenBuffer != null) tenBuffer.Release();
            if (drawBuffer != null) drawBuffer.Release();

            SetPrivateField(_tool, "_tracerPositionBuffer", null);
            SetPrivateField(_tool, "_tracerTensionBuffer", null);
            SetPrivateField(_tool, "_tracerDrawParamsBuffer", null);
        }

        [Test]
        public void UploadTracerGpuData_WhenPositionWriteThrows_UnlocksBuffer()
        {
            var go = new GameObject("HarpoonLauncherTool_Test_ErrorPath");
            var testTool = go.AddComponent<TestHarpoonLauncherTool>();
            testTool.ThrowOnWrite = true;

            var posBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, UnsafeUtility.SizeOf<GpuCableSplinePointDTO>());
            var tenBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(float));
            var drawBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, UnsafeUtility.SizeOf<GpuCableDrawParamsDTO>());

            try
            {
                SetPrivateField(testTool, "_tracerPositionBuffer", posBuffer);
                SetPrivateField(testTool, "_tracerTensionBuffer", tenBuffer);
                SetPrivateField(testTool, "_tracerDrawParamsBuffer", drawBuffer);

                Assert.Throws<System.InvalidOperationException>(() => testTool.InvokeUploadTracerGpuData(Vector3.zero, Vector3.one));

                // Verify the buffer was successfully unlocked in the finally block
                // If it wasn't unlocked, attempting to lock it again would throw an InvalidOperationException "The buffer is already locked"
                Assert.DoesNotThrow(() =>
                {
                    posBuffer.LockBufferForWrite<GpuCableSplinePointDTO>(0, 2);
                    posBuffer.UnlockBufferAfterWrite<GpuCableSplinePointDTO>(2);
                });
            }
            finally
            {
                if (posBuffer != null) posBuffer.Release();
                if (tenBuffer != null) tenBuffer.Release();
                if (drawBuffer != null) drawBuffer.Release();
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = target.GetType().BaseType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType()}");
            field.SetValue(target, value);
        }

        private T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = target.GetType().BaseType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType()}");
            return (T)field.GetValue(target);
        }
    }
}
#endif

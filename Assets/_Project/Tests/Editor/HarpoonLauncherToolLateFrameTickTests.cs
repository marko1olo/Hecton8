#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Hecton8.Gameplay;
using Hecton8.Core.Contracts.Physics;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class HarpoonLauncherToolLateFrameTickTests
    {
        private GameObject _go;
        private HarpoonLauncherTool _tool;
        private FieldInfo _tracerActiveField;
        private FieldInfo _tracerTimerField;
        private FieldInfo _tracerMaterialField;
        private FieldInfo _tracerPositionBufferField;
        private FieldInfo _tracerTensionBufferField;
        private FieldInfo _tracerDrawParamsBufferField;
        private FieldInfo _tracerPropertyBlockField;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestTool");
            _tool = _go.AddComponent<HarpoonLauncherTool>();

            var type = typeof(HarpoonLauncherTool);
            _tracerActiveField = type.GetField("_tracerActive", BindingFlags.NonPublic | BindingFlags.Instance);
            _tracerTimerField = type.GetField("_tracerTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            _tracerMaterialField = type.GetField("tracerMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
            _tracerPositionBufferField = type.GetField("_tracerPositionBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            _tracerTensionBufferField = type.GetField("_tracerTensionBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            _tracerDrawParamsBufferField = type.GetField("_tracerDrawParamsBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            _tracerPropertyBlockField = type.GetField("_tracerPropertyBlock", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(_tracerActiveField, "Field _tracerActive not found");
            Assert.IsNotNull(_tracerTimerField, "Field _tracerTimer not found");
            Assert.IsNotNull(_tracerMaterialField, "Field tracerMaterial not found");
            Assert.IsNotNull(_tracerPositionBufferField, "Field _tracerPositionBuffer not found");
            Assert.IsNotNull(_tracerTensionBufferField, "Field _tracerTensionBuffer not found");
            Assert.IsNotNull(_tracerDrawParamsBufferField, "Field _tracerDrawParamsBuffer not found");
            Assert.IsNotNull(_tracerPropertyBlockField, "Field _tracerPropertyBlock not found");
        }

        [TearDown]
        public void Teardown()
        {
            if (_tool != null)
            {
                var positionBuffer = (GraphicsBuffer)_tracerPositionBufferField.GetValue(_tool);
                positionBuffer?.Release();
                var tensionBuffer = (GraphicsBuffer)_tracerTensionBufferField.GetValue(_tool);
                tensionBuffer?.Release();
                var drawParamsBuffer = (GraphicsBuffer)_tracerDrawParamsBufferField.GetValue(_tool);
                drawParamsBuffer?.Release();
            }

            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        private void SetupValidReadyState()
        {
            _tracerMaterialField.SetValue(_tool, new Material(Shader.Find("Hidden/InternalErrorShader")));

            var posBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, 2, UnsafeUtility.SizeOf<GpuCableSplinePointDTO>());
            var tensionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<float>());
            var drawParamsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, 1, UnsafeUtility.SizeOf<GpuCableDrawParamsDTO>());

            _tracerPositionBufferField.SetValue(_tool, posBuffer);
            _tracerTensionBufferField.SetValue(_tool, tensionBuffer);
            _tracerDrawParamsBufferField.SetValue(_tool, drawParamsBuffer);

            _tracerPropertyBlockField.SetValue(_tool, new MaterialPropertyBlock());
        }

        [Test]
        public void LateFrameTick_TracerNotActive_DoesNothingAndDoesNotThrow()
        {
            _tracerActiveField.SetValue(_tool, false);
            _tracerTimerField.SetValue(_tool, 1f);
            SetupValidReadyState();

            Assert.DoesNotThrow(() => _tool.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_TracerTimerZeroOrLess_DoesNothingAndDoesNotThrow()
        {
            _tracerActiveField.SetValue(_tool, true);
            _tracerTimerField.SetValue(_tool, 0f);
            SetupValidReadyState();

            Assert.DoesNotThrow(() => _tool.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_TracerNotReady_DoesNothingAndDoesNotThrow()
        {
            _tracerActiveField.SetValue(_tool, true);
            _tracerTimerField.SetValue(_tool, 1f);

            // Do not call SetupValidReadyState, so HasTracerReady is false
            Assert.DoesNotThrow(() => _tool.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_TracerActiveAndReady_CallsRenderTracerSuccessfully()
        {
            _tracerActiveField.SetValue(_tool, true);
            _tracerTimerField.SetValue(_tool, 1f);
            SetupValidReadyState();

            // When active and ready, LateFrameTick calls UploadTracerGpuData which uses UnsafeUtility and LockBufferForWrite
            Assert.DoesNotThrow(() => _tool.LateFrameTick());
        }
    }
}
#endif

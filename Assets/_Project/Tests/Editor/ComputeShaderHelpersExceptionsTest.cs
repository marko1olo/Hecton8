#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using Crest;
using System;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Editor
{
    public class ComputeShaderHelpersExceptionsTest
    {
        private FieldInfo _isSupportedField;
        private FieldInfo _getKernelThreadGroupSizesField;
        private FieldInfo _hasKernelField;
        private FieldInfo _findKernelField;

        private object _originalIsSupported;
        private object _originalGetKernelThreadGroupSizes;
        private object _originalHasKernel;
        private object _originalFindKernel;

        [SetUp]
        public void SetUp()
        {
            var type = typeof(ComputeShaderHelpers);

            _isSupportedField = type.GetField("s_isSupported", BindingFlags.Static | BindingFlags.NonPublic);
            _getKernelThreadGroupSizesField = type.GetField("s_getKernelThreadGroupSizes", BindingFlags.Static | BindingFlags.NonPublic);
            _hasKernelField = type.GetField("s_hasKernel", BindingFlags.Static | BindingFlags.NonPublic);
            _findKernelField = type.GetField("s_findKernel", BindingFlags.Static | BindingFlags.NonPublic);

            _originalIsSupported = _isSupportedField.GetValue(null);
            _originalGetKernelThreadGroupSizes = _getKernelThreadGroupSizesField.GetValue(null);
            _originalHasKernel = _hasKernelField.GetValue(null);
            _originalFindKernel = _findKernelField.GetValue(null);
        }

        [TearDown]
        public void TearDown()
        {
            _isSupportedField.SetValue(null, _originalIsSupported);
            _getKernelThreadGroupSizesField.SetValue(null, _originalGetKernelThreadGroupSizes);
            _hasKernelField.SetValue(null, _originalHasKernel);
            _findKernelField.SetValue(null, _originalFindKernel);
        }

        private void InjectThrowingMocksForGetAndFind(Exception ex)
        {
            // Set IsSupported / HasKernel to return true
            Delegate mockIsSupported = Delegate.CreateDelegate(_isSupportedField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockIsSupported_True", BindingFlags.NonPublic | BindingFlags.Static));
            _isSupportedField.SetValue(null, mockIsSupported);

            Delegate mockHasKernel = Delegate.CreateDelegate(_hasKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockHasKernel_True", BindingFlags.NonPublic | BindingFlags.Static));
            _hasKernelField.SetValue(null, mockHasKernel);

            // Set GetKernel / FindKernel to throw
            if (ex is ObjectDisposedException)
            {
                Delegate mockGetKernel = Delegate.CreateDelegate(_getKernelThreadGroupSizesField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockGetKernel_ObjectDisposedException", BindingFlags.NonPublic | BindingFlags.Static));
                _getKernelThreadGroupSizesField.SetValue(null, mockGetKernel);

                Delegate mockFindKernel = Delegate.CreateDelegate(_findKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockFindKernel_ObjectDisposedException", BindingFlags.NonPublic | BindingFlags.Static));
                _findKernelField.SetValue(null, mockFindKernel);
            }
            else if (ex is InvalidOperationException)
            {
                Delegate mockGetKernel = Delegate.CreateDelegate(_getKernelThreadGroupSizesField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockGetKernel_InvalidOperationException", BindingFlags.NonPublic | BindingFlags.Static));
                _getKernelThreadGroupSizesField.SetValue(null, mockGetKernel);

                Delegate mockFindKernel = Delegate.CreateDelegate(_findKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockFindKernel_InvalidOperationException", BindingFlags.NonPublic | BindingFlags.Static));
                _findKernelField.SetValue(null, mockFindKernel);
            }
            else if (ex is ArgumentException)
            {
                Delegate mockGetKernel = Delegate.CreateDelegate(_getKernelThreadGroupSizesField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockGetKernel_ArgumentException", BindingFlags.NonPublic | BindingFlags.Static));
                _getKernelThreadGroupSizesField.SetValue(null, mockGetKernel);

                Delegate mockFindKernel = Delegate.CreateDelegate(_findKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockFindKernel_ArgumentException", BindingFlags.NonPublic | BindingFlags.Static));
                _findKernelField.SetValue(null, mockFindKernel);
            }
            else if (ex is MissingReferenceException)
            {
                Delegate mockGetKernel = Delegate.CreateDelegate(_getKernelThreadGroupSizesField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockGetKernel_MissingReferenceException", BindingFlags.NonPublic | BindingFlags.Static));
                _getKernelThreadGroupSizesField.SetValue(null, mockGetKernel);

                Delegate mockFindKernel = Delegate.CreateDelegate(_findKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockFindKernel_MissingReferenceException", BindingFlags.NonPublic | BindingFlags.Static));
                _findKernelField.SetValue(null, mockFindKernel);
            }
            else if (ex is UnityException)
            {
                Delegate mockGetKernel = Delegate.CreateDelegate(_getKernelThreadGroupSizesField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockGetKernel_UnityException", BindingFlags.NonPublic | BindingFlags.Static));
                _getKernelThreadGroupSizesField.SetValue(null, mockGetKernel);

                Delegate mockFindKernel = Delegate.CreateDelegate(_findKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockFindKernel_UnityException", BindingFlags.NonPublic | BindingFlags.Static));
                _findKernelField.SetValue(null, mockFindKernel);
            }
        }

        #region Mocks
        private static bool MockIsSupported_True(ComputeShader shader, int kernel) => true;
        private static bool MockHasKernel_True(ComputeShader shader, string kernelName) => true;

        private static bool MockIsSupported_ObjectDisposedException(ComputeShader shader, int kernel) { throw new ObjectDisposedException("ComputeShader"); }
        private static bool MockHasKernel_ObjectDisposedException(ComputeShader shader, string kernelName) { throw new ObjectDisposedException("ComputeShader"); }
        private static void MockGetKernel_ObjectDisposedException(ComputeShader shader, int kernel, out uint x, out uint y, out uint z) { throw new ObjectDisposedException("ComputeShader"); }
        private static int MockFindKernel_ObjectDisposedException(ComputeShader shader, string kernelName) { throw new ObjectDisposedException("ComputeShader"); }

        private static bool MockIsSupported_InvalidOperationException(ComputeShader shader, int kernel) { throw new InvalidOperationException("ComputeShader"); }
        private static bool MockHasKernel_InvalidOperationException(ComputeShader shader, string kernelName) { throw new InvalidOperationException("ComputeShader"); }
        private static void MockGetKernel_InvalidOperationException(ComputeShader shader, int kernel, out uint x, out uint y, out uint z) { throw new InvalidOperationException("ComputeShader"); }
        private static int MockFindKernel_InvalidOperationException(ComputeShader shader, string kernelName) { throw new InvalidOperationException("ComputeShader"); }

        private static bool MockIsSupported_ArgumentException(ComputeShader shader, int kernel) { throw new ArgumentException("ComputeShader"); }
        private static bool MockHasKernel_ArgumentException(ComputeShader shader, string kernelName) { throw new ArgumentException("ComputeShader"); }
        private static void MockGetKernel_ArgumentException(ComputeShader shader, int kernel, out uint x, out uint y, out uint z) { throw new ArgumentException("ComputeShader"); }
        private static int MockFindKernel_ArgumentException(ComputeShader shader, string kernelName) { throw new ArgumentException("ComputeShader"); }

        private static bool MockIsSupported_MissingReferenceException(ComputeShader shader, int kernel) { throw new MissingReferenceException("ComputeShader"); }
        private static bool MockHasKernel_MissingReferenceException(ComputeShader shader, string kernelName) { throw new MissingReferenceException("ComputeShader"); }
        private static void MockGetKernel_MissingReferenceException(ComputeShader shader, int kernel, out uint x, out uint y, out uint z) { throw new MissingReferenceException("ComputeShader"); }
        private static int MockFindKernel_MissingReferenceException(ComputeShader shader, string kernelName) { throw new MissingReferenceException("ComputeShader"); }

        private static bool MockIsSupported_UnityException(ComputeShader shader, int kernel) { throw new UnityException("ComputeShader"); }
        private static bool MockHasKernel_UnityException(ComputeShader shader, string kernelName) { throw new UnityException("ComputeShader"); }
        private static void MockGetKernel_UnityException(ComputeShader shader, int kernel, out uint x, out uint y, out uint z) { throw new UnityException("ComputeShader"); }
        private static int MockFindKernel_UnityException(ComputeShader shader, string kernelName) { throw new UnityException("ComputeShader"); }
        #endregion

        [Test]
        public void TryGetPortableKernelThreadGroupSizes_CatchesExceptions()
        {
            var shader = (ComputeShader)FormatterServices.GetUninitializedObject(typeof(ComputeShader));

            Exception[] exceptions = new Exception[]
            {
                new ObjectDisposedException("ComputeShader"),
                new InvalidOperationException("ComputeShader"),
                new ArgumentException("ComputeShader"),
                new MissingReferenceException("ComputeShader"),
                new UnityException("ComputeShader")
            };

            foreach (var ex in exceptions)
            {
                InjectThrowingMocksForGetAndFind(ex);

                // Expect exception log in unity? We just want the method to return false.
                bool result = ComputeShaderHelpers.TryGetPortableKernelThreadGroupSizes(shader, 0, out int x, out int y, out int z);

                Assert.IsFalse(result, $"Expected false when GetKernelThreadGroupSizes throws {ex.GetType().Name}");
            }

            foreach (var ex in exceptions)
            {
                // IsSupported throws
                Delegate mockIsSupported = null;
                if (ex is ObjectDisposedException) mockIsSupported = Delegate.CreateDelegate(_isSupportedField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockIsSupported_ObjectDisposedException", BindingFlags.NonPublic | BindingFlags.Static));
                if (ex is InvalidOperationException) mockIsSupported = Delegate.CreateDelegate(_isSupportedField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockIsSupported_InvalidOperationException", BindingFlags.NonPublic | BindingFlags.Static));
                if (ex is ArgumentException) mockIsSupported = Delegate.CreateDelegate(_isSupportedField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockIsSupported_ArgumentException", BindingFlags.NonPublic | BindingFlags.Static));
                if (ex is MissingReferenceException) mockIsSupported = Delegate.CreateDelegate(_isSupportedField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockIsSupported_MissingReferenceException", BindingFlags.NonPublic | BindingFlags.Static));
                if (ex is UnityException) mockIsSupported = Delegate.CreateDelegate(_isSupportedField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockIsSupported_UnityException", BindingFlags.NonPublic | BindingFlags.Static));

                _isSupportedField.SetValue(null, mockIsSupported);

                bool result = ComputeShaderHelpers.TryGetPortableKernelThreadGroupSizes(shader, 0, out int x, out int y, out int z);
                Assert.IsFalse(result, $"Expected false when IsSupported throws {ex.GetType().Name}");
            }
        }

        [Test]
        public void TryFindKernel_CatchesExceptions()
        {
            var shader = (ComputeShader)FormatterServices.GetUninitializedObject(typeof(ComputeShader));

            Exception[] exceptions = new Exception[]
            {
                new ObjectDisposedException("ComputeShader"),
                new InvalidOperationException("ComputeShader"),
                new ArgumentException("ComputeShader"),
                new MissingReferenceException("ComputeShader"),
                new UnityException("ComputeShader")
            };

            foreach (var ex in exceptions)
            {
                InjectThrowingMocksForGetAndFind(ex);

                bool result = ComputeShaderHelpers.TryFindKernel(shader, "KernelName", out int kernel);

                Assert.IsFalse(result, $"Expected false when FindKernel throws {ex.GetType().Name}");
                Assert.AreEqual(-1, kernel, $"Expected kernel to be -1 when FindKernel throws {ex.GetType().Name}");
            }

            foreach (var ex in exceptions)
            {
                Delegate mockHasKernel = null;
                if (ex is ObjectDisposedException) mockHasKernel = Delegate.CreateDelegate(_hasKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockHasKernel_ObjectDisposedException", BindingFlags.NonPublic | BindingFlags.Static));
                if (ex is InvalidOperationException) mockHasKernel = Delegate.CreateDelegate(_hasKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockHasKernel_InvalidOperationException", BindingFlags.NonPublic | BindingFlags.Static));
                if (ex is ArgumentException) mockHasKernel = Delegate.CreateDelegate(_hasKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockHasKernel_ArgumentException", BindingFlags.NonPublic | BindingFlags.Static));
                if (ex is MissingReferenceException) mockHasKernel = Delegate.CreateDelegate(_hasKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockHasKernel_MissingReferenceException", BindingFlags.NonPublic | BindingFlags.Static));
                if (ex is UnityException) mockHasKernel = Delegate.CreateDelegate(_hasKernelField.FieldType, typeof(ComputeShaderHelpersExceptionsTest).GetMethod("MockHasKernel_UnityException", BindingFlags.NonPublic | BindingFlags.Static));

                _hasKernelField.SetValue(null, mockHasKernel);

                bool result = ComputeShaderHelpers.TryFindKernel(shader, "KernelName", out int kernel);
                Assert.IsFalse(result, $"Expected false when HasKernel throws {ex.GetType().Name}");
                Assert.AreEqual(-1, kernel, $"Expected kernel to be -1 when HasKernel throws {ex.GetType().Name}");
            }
        }
    }
}
#endif

using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Hecton8.AI.Ambient;
using Hecton8.Core.Contracts;

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS

namespace Hecton8.Tests.Editor
{
    public sealed class AmbientBiotaDirectorExceptionEditTests
    {
        [Test]
        public void AmbientBiotaDirector_TryWriteBlackBoxSnapshotCold_HandlesExceptionsProperly()
        {
            Type directorType = typeof(AmbientBiotaDirector);
            FieldInfo countField = directorType.GetField("s_blackBoxDumpCount", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo inFlightField = directorType.GetField("s_blackBoxDumpInFlight", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo snapshotField = directorType.GetField("s_blackBoxDumpSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo method = directorType.GetMethod("TryWriteBlackBoxSnapshotCold", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(countField);
            Assert.IsNotNull(inFlightField);
            Assert.IsNotNull(snapshotField);
            Assert.IsNotNull(method);

            var oldSnapshot = snapshotField.GetValue(null);

            try
            {
                // Bypass guard
                countField.SetValue(null, 300);

                // Force an exception during execution of the try block by nullifying the snapshot array
                snapshotField.SetValue(null, null);

                // The method should throw due to null reference exception on snapshot element access
                var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, null));
                Assert.IsInstanceOf<NullReferenceException>(exception.InnerException);

                // The memory tracking bridge would throw if a transient payload leaked.
                // We also verify that we can still allocate and free without issue.
                NativeArray<byte> testArray = NativeFaultDumpWriter.CreateTransientPayload(10, "TestOwner", "TestLabel");
                NativeFaultDumpWriter.DisposeTransientPayload(ref testArray, "TestOwner", "TestLabel");
                Assert.IsFalse(testArray.IsCreated);
            }
            finally
            {
                // Restore original state
                snapshotField.SetValue(null, oldSnapshot);
                countField.SetValue(null, 0);
                inFlightField.SetValue(null, 0);
            }
        }
    }
}
#endif

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using Hecton8.AI.Ambient;
using Hecton8.Core.Contracts;

namespace Hecton8.Tests.Editor.AI.Ambient
{
    [TestFixture]
    public class AmbientBiotaDirectorEditTests
    {
        [Test]
        public void TryWriteBlackBoxSnapshotCold_ReturnsFalseOnInvalidOperationExceptionFromWriter()
        {
            Type directorType = typeof(AmbientBiotaDirector);

            FieldInfo countField = directorType.GetField("s_blackBoxDumpCount", BindingFlags.NonPublic | BindingFlags.Static);
            int originalCount = (int)countField.GetValue(null);
            countField.SetValue(null, 1);

            MethodInfo writeMethod = directorType.GetMethod("TryWriteBlackBoxSnapshotCold", BindingFlags.NonPublic | BindingFlags.Static);

            Type bridgeType = typeof(NativeMemoryTrackingBridge);
            FieldInfo registerBytesField = bridgeType.GetField("s_registerBytes", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo registerBytesInstanceField = bridgeType.GetField("s_registerBytesInstance", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo unregisterOwnerLabelField = bridgeType.GetField("s_unregisterOwnerLabel", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo unregisterIdField = bridgeType.GetField("s_unregisterId", BindingFlags.NonPublic | BindingFlags.Static);

            object originalRegisterBytes = registerBytesField.GetValue(null);
            object originalRegisterBytesInstance = registerBytesInstanceField.GetValue(null);
            object originalUnregisterOwnerLabel = unregisterOwnerLabelField.GetValue(null);
            object originalUnregisterId = unregisterIdField.GetValue(null);

            NativeMemoryTrackingBridge.Install(null, null, null, null);

            try
            {
                bool result = (bool)writeMethod.Invoke(null, null);
                Assert.IsFalse(result);
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is InvalidOperationException)
                {
                    Assert.Fail("TryWriteBlackBoxSnapshotCold did not catch InvalidOperationException.");
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                countField.SetValue(null, originalCount);

                NativeMemoryTrackingBridge.Install(
                    (NativeMemoryTrackingBridge.RegisterBytesDelegate)originalRegisterBytes,
                    (NativeMemoryTrackingBridge.RegisterBytesDelegate)originalRegisterBytesInstance,
                    (NativeMemoryTrackingBridge.UnregisterOwnerLabelDelegate)originalUnregisterOwnerLabel,
                    (NativeMemoryTrackingBridge.UnregisterIdDelegate)originalUnregisterId);
            }
        }
    }
}
#endif

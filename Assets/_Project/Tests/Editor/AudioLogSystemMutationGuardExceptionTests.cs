using System;
using System.Reflection;
using Hecton8.Core.Memory;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class AudioLogSystemMutationGuardExceptionTests
    {
        [Test]
        public void EnqueuePlayback_ThrowsException_ReleasesMutationGuard()
        {
            GameObject go = new GameObject("AudioLogSystemTest");
            AudioLogSystem system = go.AddComponent<AudioLogSystem>();
            GlobalDataVault vault = GlobalDataVault.Create();

            try
            {
                // Inject the vault
                typeof(AudioLogSystem).GetField("_dataVault", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(system, vault);

                // Initialize handles
                VaultGenerationHandle<uint> handle = vault.EnsureGenerationHandle<uint>(BufferID.AudioLogPlaybackQueue, 16, SystemID.Audio, VaultHandleOptions.None);
                typeof(AudioLogSystem).GetField("_queuedLogHashesHandle", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(system, handle);

                // Set write index to force exception
                typeof(AudioLogSystem).GetField("_playbackQueueWriteIndex", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(system, 999);

                ulong mask = 1UL << (unchecked((int)(uint)BufferID.AudioLogPlaybackQueue) & 31);
                Assert.AreEqual(0, vault.ActiveMutationGuardMask & mask, "Mutation guard should not be active initially.");

                // Invoke EnqueuePlayback
                MethodInfo enqueuePlayback = typeof(AudioLogSystem).GetMethod("EnqueuePlayback", BindingFlags.NonPublic | BindingFlags.Instance);

                try
                {
                    enqueuePlayback.Invoke(system, new object[] { 1u });
                    Assert.Fail("Expected TargetInvocationException due to IndexOutOfRangeException in NativeArray access.");
                }
                catch (TargetInvocationException e)
                {
                    Assert.IsInstanceOf<IndexOutOfRangeException>(e.InnerException, "Expected inner exception to be IndexOutOfRangeException.");
                }

                Assert.AreEqual(0, vault.ActiveMutationGuardMask & mask, "Mutation guard was not released in the finally block after an exception!");
            }
            finally
            {
                vault.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}

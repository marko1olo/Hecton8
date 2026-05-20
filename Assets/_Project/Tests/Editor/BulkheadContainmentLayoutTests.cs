using Hecton8.Construction;
using Hecton8.Core.Contracts;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed class BulkheadContainmentLayoutTests
    {
        [Test]
        public void BulkheadStateDTO_IsExactPromptLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadStateDTO>(), Is.EqualTo(32));
            Assert.That(BulkheadStateLayoutGuard.ValidateLayout(), Is.True);
        }

        [Test]
        public void BulkheadIntentDTO_IsCacheLineAligned()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadContainmentIntentDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.SizeOf<BulkheadContainmentIntentControlDTO>(), Is.EqualTo(64));
        }
    }
}

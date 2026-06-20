using NUnit.Framework;
using System;
using System.Reflection;
using Hecton8.Core;
using System.Collections;

namespace Hecton8.Core.Editor.Tests
{
    [TestFixture]
    public class GameTickManagerTickListEditTests
    {
        private class DummyTickable : ITickable
        {
            public void Tick(float dt) {}
        }

        [Test]
        public void TickList_Clear_ResetsCountAndClearsAllBuffers()
        {
            // The TickList<T> is private, we must use reflection to test it.
            Type tickManagerType = typeof(GameTickManager);
            Type tickListGenericType = tickManagerType.GetNestedType("TickList`1", BindingFlags.NonPublic);
            Assert.IsNotNull(tickListGenericType, "Could not find private TickList<T> nested type in GameTickManager");

            Type tickListType = tickListGenericType.MakeGenericType(typeof(DummyTickable));

            object tickList = Activator.CreateInstance(tickListType, new object[] { 10 });

            MethodInfo addMethod = tickListType.GetMethod("Add");
            MethodInfo beginIterMethod = tickListType.GetMethod("BeginIteration");
            MethodInfo removeMethod = tickListType.GetMethod("Remove");
            MethodInfo clearMethod = tickListType.GetMethod("Clear");

            FieldInfo itemsField = tickListType.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo toAddField = tickListType.GetField("_toAdd", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo toRemoveField = tickListType.GetField("_toRemove", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo isIteratingField = tickListType.GetField("_isIterating", BindingFlags.NonPublic | BindingFlags.Instance);

            DummyTickable t1 = new DummyTickable();
            DummyTickable t2 = new DummyTickable();

            // Add t1 to main list
            addMethod.Invoke(tickList, new object[] { t1 });

            // Start iteration
            beginIterMethod.Invoke(tickList, null);

            // Add t2 (goes to _toAdd buffer)
            addMethod.Invoke(tickList, new object[] { t2 });

            // Remove t1 (goes to _toRemove buffer)
            removeMethod.Invoke(tickList, new object[] { t1 });

            // Verify state BEFORE Clear
            IList itemsList = (IList)itemsField.GetValue(tickList);
            IList toAddList = (IList)toAddField.GetValue(tickList);
            IList toRemoveList = (IList)toRemoveField.GetValue(tickList);
            bool isIterating = (bool)isIteratingField.GetValue(tickList);

            Assert.AreEqual(1, itemsList.Count, "Precondition: _items should have 1 element");
            Assert.AreEqual(1, toAddList.Count, "Precondition: _toAdd should have 1 element");
            Assert.AreEqual(1, toRemoveList.Count, "Precondition: _toRemove should have 1 element");
            Assert.IsTrue(isIterating, "Precondition: _isIterating should be true");

            // Execute Clear
            clearMethod.Invoke(tickList, null);

            // Verify state AFTER Clear
            itemsList = (IList)itemsField.GetValue(tickList);
            toAddList = (IList)toAddField.GetValue(tickList);
            toRemoveList = (IList)toRemoveField.GetValue(tickList);
            isIterating = (bool)isIteratingField.GetValue(tickList);

            Assert.AreEqual(0, itemsList.Count, "_items should be empty after Clear()");
            Assert.AreEqual(0, toAddList.Count, "_toAdd should be empty after Clear()");
            Assert.AreEqual(0, toRemoveList.Count, "_toRemove should be empty after Clear()");
            Assert.IsFalse(isIterating, "_isIterating should be false after Clear()");
        }
    }
}

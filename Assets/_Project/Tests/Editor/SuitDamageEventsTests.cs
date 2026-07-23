using System;
using System.Reflection;
using NUnit.Framework;

namespace Hecton8.Tests.Interaction
{
    public class SuitDamageEventsTests
    {
        private class DummyListener : Hecton8.Interaction.ISuitDamageEventListener
        {
            public int DamageCount = 0;
            public void OnSuitDamage(in Hecton8.Interaction.SuitDamageEvent damageEvent)
            {
                DamageCount++;
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Reset SuitDamageEvents static state to avoid test pollution
            var suitDamageEventsType = typeof(Hecton8.Interaction.SuitDamageEvents);
            var countField = suitDamageEventsType.GetField("_listenerCount", BindingFlags.NonPublic | BindingFlags.Static);
            var listenersField = suitDamageEventsType.GetField("_listeners", BindingFlags.NonPublic | BindingFlags.Static);

            countField.SetValue(null, 0);

            var array = (Array)listenersField.GetValue(null);
            var slotType = suitDamageEventsType.GetNestedType("ListenerSlot", BindingFlags.NonPublic);
            var clearMethod = slotType.GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < array.Length; i++)
            {
                var slot = array.GetValue(i);
                // Box the struct to mutate it via reflection
                object boxedSlot = slot;
                clearMethod.Invoke(boxedSlot, null);
                array.SetValue(boxedSlot, i);
            }
        }

        [Test]
        public void ListenerSlot_Clear_NullifiesListener()
        {
            // Specifically test the Clear method on line 73
            var slotType = typeof(Hecton8.Interaction.SuitDamageEvents).GetNestedType("ListenerSlot", BindingFlags.NonPublic);
            var clearMethod = slotType.GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);
            var listenerField = slotType.GetField("Listener", BindingFlags.Public | BindingFlags.Instance);

            object boxedSlot = Activator.CreateInstance(slotType);
            var dummy = new DummyListener();

            // Set listener
            listenerField.SetValue(boxedSlot, dummy);
            Assert.IsNotNull(listenerField.GetValue(boxedSlot));

            // Invoke Clear
            clearMethod.Invoke(boxedSlot, null);

            // Verify cleared
            Assert.IsNull(listenerField.GetValue(boxedSlot));
        }

        [Test]
        public void RegisterAndPublish_NotifiesListener()
        {
            var listener = new DummyListener();
            Hecton8.Interaction.SuitDamageEvents.Register(listener);

            // Reflection to create event since missing some struct dependencies in unity test framework
            var dmgEvent = (Hecton8.Interaction.SuitDamageEvent)Activator.CreateInstance(typeof(Hecton8.Interaction.SuitDamageEvent));

            Hecton8.Interaction.SuitDamageEvents.Publish(in dmgEvent);

            Assert.AreEqual(1, listener.DamageCount);
        }

        [Test]
        public void Unregister_RemovesListenerAndCallsClear()
        {
            var listener = new DummyListener();
            Hecton8.Interaction.SuitDamageEvents.Register(listener);

            // Verify it was added
            var countField = typeof(Hecton8.Interaction.SuitDamageEvents).GetField("_listenerCount", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.AreEqual(1, (int)countField.GetValue(null));

            Hecton8.Interaction.SuitDamageEvents.Unregister(listener);

            // Verify count decreased
            Assert.AreEqual(0, (int)countField.GetValue(null));

            // Verify no notification is sent
            var dmgEvent = (Hecton8.Interaction.SuitDamageEvent)Activator.CreateInstance(typeof(Hecton8.Interaction.SuitDamageEvent));
            Hecton8.Interaction.SuitDamageEvents.Publish(in dmgEvent);
            Assert.AreEqual(0, listener.DamageCount);
        }
    }
}

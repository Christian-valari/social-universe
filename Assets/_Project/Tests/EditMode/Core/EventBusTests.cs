using NUnit.Framework;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class EventBusTests
    {
        private struct TestEvent { public int Value; }

        [SetUp]
        public void SetUp() => EventBus.Clear();

        [Test]
        public void Subscribe_and_Publish_invokes_handler()
        {
            int received = 0;
            EventBus.Subscribe<TestEvent>(e => received = e.Value);
            EventBus.Publish(new TestEvent { Value = 42 });
            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_stops_invocation()
        {
            int count = 0;
            void Handler(TestEvent _) => count++;
            EventBus.Subscribe<TestEvent>(Handler);
            EventBus.Unsubscribe<TestEvent>(Handler);
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Publish_with_no_subscribers_does_not_throw()
        {
            Assert.DoesNotThrow(() => EventBus.Publish(new TestEvent()));
        }

        [Test]
        public void Multiple_subscribers_all_receive_event()
        {
            int a = 0, b = 0;
            EventBus.Subscribe<TestEvent>(_ => a++);
            EventBus.Subscribe<TestEvent>(_ => b++);
            EventBus.Publish(new TestEvent());
            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
        }
    }
}

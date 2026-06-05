using NUnit.Framework;
using SocialUniverse.Core;

namespace SocialUniverse.Tests
{
    public class GameStateMachineTests
    {
        private class FakeState : IGameState
        {
            public int EnterCount;
            public int ExitCount;
            public int TickCount;

            public void Enter() => EnterCount++;
            public void Tick()  => TickCount++;
            public void Exit()  => ExitCount++;
        }

        [Test]
        public void TransitionTo_calls_enter_on_first_state()
        {
            var fsm   = new GameStateMachine();
            var state = new FakeState();
            fsm.TransitionTo(state);
            Assert.AreEqual(1, state.EnterCount);
            Assert.AreEqual(0, state.ExitCount);
        }

        [Test]
        public void TransitionTo_exits_previous_and_enters_next()
        {
            var fsm    = new GameStateMachine();
            var stateA = new FakeState();
            var stateB = new FakeState();
            fsm.TransitionTo(stateA);
            fsm.TransitionTo(stateB);
            Assert.AreEqual(1, stateA.ExitCount);
            Assert.AreEqual(1, stateB.EnterCount);
        }

        [Test]
        public void Tick_delegates_to_current_state()
        {
            var fsm   = new GameStateMachine();
            var state = new FakeState();
            fsm.TransitionTo(state);
            fsm.Tick();
            fsm.Tick();
            Assert.AreEqual(2, state.TickCount);
        }

        [Test]
        public void Tick_with_no_state_does_not_throw()
        {
            var fsm = new GameStateMachine();
            Assert.DoesNotThrow(() => fsm.Tick());
        }

        [Test]
        public void Current_reflects_active_state()
        {
            var fsm   = new GameStateMachine();
            var state = new FakeState();
            fsm.TransitionTo(state);
            Assert.AreSame(state, fsm.Current);
        }
    }
}

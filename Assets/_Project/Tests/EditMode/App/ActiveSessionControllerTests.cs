using NUnit.Framework;
using SocialUniverse.App;
using SocialUniverse.Core;
using SocialUniverse.UI;
using UnityEngine;

namespace SocialUniverse.Tests
{
    // One active device per account: when the server reports this device's session was replaced, a
    // device in the game leaves chat and is shown a dialog whose only way out is Quit — it is not
    // signed out, so reopening the app signs in again (newest launch wins). While in the game the
    // session is re-checked so an idle replaced device finds out too.
    public class ActiveSessionControllerTests
    {
        private class GameState : IGameState
        {
            public void Enter() { }
            public void Tick()  { }
            public void Exit()  { }
        }

        private GameStateMachine        _fsm;
        private FakeSocialBackendClient _backend;
        private FakeChatService         _chat;
        private ActiveSessionController _controller;

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear();
            _fsm     = new GameStateMachine();
            _backend = new FakeSocialBackendClient();
            _chat    = new FakeChatService();
            _chat.ConnectAsync("Player", null);

            _controller = new ActiveSessionController(_backend, _fsm, _chat);
            _controller.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Dispose();
            EventBus.Clear();
            foreach (var dialog in Dialogs())
                Object.DestroyImmediate(dialog.gameObject);
        }

        private static QuitDialogView[] Dialogs() =>
            Object.FindObjectsByType<QuitDialogView>(FindObjectsSortMode.None);

        [Test]
        public void A_replaced_session_in_the_game_shows_the_quit_dialog_without_signing_out()
        {
            var game = new GameState();
            _fsm.TransitionTo(game);

            EventBus.Publish(new SessionReplacedEvent());

            Assert.AreEqual(1, Dialogs().Length, "The player must be shown the quit dialog");
            Assert.AreSame(game, _fsm.Current, "No logout: the device stays signed in until the player quits");
            Assert.IsFalse(_chat.IsConnected, "The replaced device must leave chat");
        }

        [Test]
        public void Further_rejections_keep_the_one_dialog()
        {
            _fsm.TransitionTo(new GameState());

            EventBus.Publish(new SessionReplacedEvent());
            EventBus.Publish(new SessionReplacedEvent());

            Assert.AreEqual(1, Dialogs().Length);
        }

        [Test]
        public void A_replaced_session_on_the_sign_in_screen_is_left_to_the_auth_flow()
        {
            var authState = new AuthState(null, _fsm, null, null, null, null);
            _fsm.TransitionTo(authState);

            EventBus.Publish(new SessionReplacedEvent());

            CollectionAssert.IsEmpty(Dialogs());
            Assert.IsTrue(_chat.IsConnected);
        }

        [Test]
        public void The_session_is_checked_in_the_game_then_not_again_within_the_interval()
        {
            _backend.SessionId = "session-1";
            _fsm.TransitionTo(new GameState());

            _controller.Tick();
            _controller.Tick();

            CollectionAssert.AreEqual(new[] { "ClaimSession" }, _backend.AllCalledFunctions);
        }

        [Test]
        public void No_check_runs_without_a_session()
        {
            _fsm.TransitionTo(new GameState());

            _controller.Tick();

            CollectionAssert.IsEmpty(_backend.AllCalledFunctions);
        }

        [Test]
        public void No_check_runs_outside_the_game()
        {
            _backend.SessionId = "session-1";
            _fsm.TransitionTo(new AuthState(null, _fsm, null, null, null, null));

            _controller.Tick();

            CollectionAssert.IsEmpty(_backend.AllCalledFunctions);
        }
    }
}

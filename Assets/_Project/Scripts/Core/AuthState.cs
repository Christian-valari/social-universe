using VContainer;

namespace SocialUniverse.Core
{
    public class AuthState : IGameState
    {
        private readonly GameStateMachine _fsm;
        private readonly IObjectResolver  _resolver;

        public AuthState(GameStateMachine fsm, IObjectResolver resolver)
        {
            _fsm      = fsm;
            _resolver = resolver;
        }

        public void Enter() => SULog.Info("Auth: awaiting login");
        public void Tick()  { }
        public void Exit()  { }

        public void OnAuthComplete() => _fsm.TransitionTo(_resolver.Resolve<HubState>());
    }
}

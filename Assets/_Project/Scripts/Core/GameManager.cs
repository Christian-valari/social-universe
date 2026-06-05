namespace SocialUniverse.Core
{
    public class GameManager
    {
        public GameStateMachine FSM { get; }

        private readonly BootState _bootState;

        public GameManager(GameStateMachine fsm, BootState bootState)
        {
            FSM        = fsm;
            _bootState = bootState;
        }

        public void Initialize()
        {
            SULog.Info("GameManager: starting boot sequence");
            FSM.TransitionTo(_bootState);
        }

        public void Tick() => FSM.Tick();
    }
}

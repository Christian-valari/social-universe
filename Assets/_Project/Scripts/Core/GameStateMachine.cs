namespace SocialUniverse.Core
{
    public class GameStateMachine
    {
        public IGameState Current { get; private set; }

        public void TransitionTo(IGameState next)
        {
            Current?.Exit();
            Current = next;
            Current.Enter();
            SULog.Info($"FSM → {next.GetType().Name}");
        }

        public void Tick() => Current?.Tick();
    }
}

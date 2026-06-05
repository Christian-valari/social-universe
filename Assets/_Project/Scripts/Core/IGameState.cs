namespace SocialUniverse.Core
{
    public interface IGameState
    {
        void Enter();
        void Tick();
        void Exit();
    }
}

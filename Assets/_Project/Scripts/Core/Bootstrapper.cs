using VContainer.Unity;

namespace SocialUniverse.Core
{
    public class Bootstrapper : IStartable, ITickable
    {
        private readonly GameManager _gameManager;

        public Bootstrapper(GameManager gameManager)
        {
            _gameManager = gameManager;
        }

        void IStartable.Start() => _gameManager.Initialize();
        void ITickable.Tick()   => _gameManager.Tick();
    }
}

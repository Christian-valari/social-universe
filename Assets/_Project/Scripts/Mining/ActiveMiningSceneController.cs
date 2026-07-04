using System.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;
using SocialUniverse.World;
using SocialUniverse.Core;

namespace SocialUniverse.Mining
{
    // Owns loading/unloading the ActiveMining minigame scene on top of the (still-running)
    // Planet scene, and disabling the Planet camera while it's up so only one camera renders at
    // a time. Reacts to MiningController.OnActiveSessionChanged rather than being called
    // directly by MiningModePromptView, so starting/stopping an active-mining session is the
    // single source of truth for whether the minigame scene should be loaded.
    public class ActiveMiningSceneController : IStartable
    {
        private readonly MiningController       _mining;
        private readonly SceneLoader            _sceneLoader;
        private readonly PlanetCameraController _planetCamera;

        private bool _sceneLoaded;

        public ActiveMiningSceneController(MiningController mining, SceneLoader sceneLoader, PlanetCameraController planetCamera)
        {
            _mining       = mining;
            _sceneLoader  = sceneLoader;
            _planetCamera = planetCamera;
        }

        public void Start() => _mining.OnActiveSessionChanged += OnActiveSessionChanged;

        private void OnActiveSessionChanged(ActiveMiningSession session)
        {
            if (session != null && !_sceneLoaded)
                _ = EnterAsync();
            else if (session == null && _sceneLoaded)
                _ = ExitAsync();
        }

        private async Task EnterAsync()
        {
            _sceneLoaded = true;
            SetPlanetCameraEnabled(false);
            await _sceneLoader.LoadAsync(Constants.SceneNames.ActiveMining);
        }

        private async Task ExitAsync()
        {
            await _sceneLoader.UnloadAsync(Constants.SceneNames.ActiveMining);
            SetPlanetCameraEnabled(true);
            _sceneLoaded = false;
        }

        private void SetPlanetCameraEnabled(bool isEnabled)
        {
            var camera = _planetCamera.GetComponent<Camera>();
            if (camera != null) camera.enabled = isEnabled;
        }
    }
}

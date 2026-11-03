using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using SocialUniverse.App;
using SocialUniverse.Config;
using SocialUniverse.Core;
using SocialUniverse.Mining;

namespace SocialUniverse.Tests
{
    // The real Planet scene under a dev-mode parent scope. Dev mode's backend answers every call with
    // nothing, so mining only works there through the local services.
    public class PlanetDevModeFlowTests
    {
        private const string PlanetScenePath = "Assets/Scenes/Planet.unity";

        private GameObject       _rootScopeObject;
        private PlanetSceneScope _scope;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            PlanetTestRootScope.DevMode = true;
            _rootScopeObject = new GameObject("PlanetTestRootScope");
            UnityEngine.Object.DontDestroyOnLoad(_rootScopeObject);
            _rootScopeObject.AddComponent<PlanetTestRootScope>();

            bool sceneReady = false;
            Action<SceneReadyEvent> onReady = _ => sceneReady = true;
            EventBus.Subscribe(onReady);

            yield return SceneManager.LoadSceneAsync(PlanetScenePath, LoadSceneMode.Single);

            _scope = UnityEngine.Object.FindFirstObjectByType<PlanetSceneScope>();
            Assert.IsNotNull(_scope?.Container, "PlanetSceneScope container not built");

            float timeout = Time.realtimeSinceStartup + 10f;
            while (!sceneReady && Time.realtimeSinceStartup < timeout)
                yield return null;
            EventBus.Unsubscribe(onReady);
            Assert.IsTrue(sceneReady, "Planet scene never published SceneReadyEvent");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlanetTestRootScope.DevMode = false;
            if (_rootScopeObject != null) UnityEngine.Object.Destroy(_rootScopeObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dev_mode_uses_the_local_mining_services()
        {
            Assert.IsInstanceOf<LocalMockMineralService>(Resolve<IMineralService>());
            Assert.IsInstanceOf<LocalMockDroneService>(Resolve<IDroneService>());
            Assert.IsInstanceOf<LocalMockIdleMiningService>(Resolve<IIdleMiningService>());
            Assert.IsInstanceOf<LocalMockAsteroidRespawnService>(Resolve<IAsteroidRespawnService>());
            yield return null;
        }

        [UnityTest]
        public IEnumerator A_dev_mode_claim_grants_minerals()
        {
            var inventory = Resolve<MineralInventory>();
            var planet    = Resolve<PlanetDefinition>();
            var mineral   = Resolve<DatabaseRegistry>().AllMinerals.First(m => m != null);
            int before    = inventory.Get(mineral.MineralId);

            var claim = Resolve<IMineralService>().GrantMiningAsync(planet.PlanetId, mineral.MineralId, 10, slotId: "Iron#0");
            yield return new WaitUntil(() => claim.IsCompleted);

            Assert.AreEqual(10, claim.Result.Granted);
            Assert.AreEqual(before + 10, inventory.Get(mineral.MineralId));
        }

        private T Resolve<T>() where T : class => _scope.Container.Resolve(typeof(T)) as T;
    }
}

using NUnit.Framework;
using SocialUniverse.Core;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class SaveKeysTests
    {
        private const string PlayerA = "player_a";
        private const string PlayerB = "player_b";

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SaveKeys.LastPlanetIdKey(PlayerA));
            PlayerPrefs.DeleteKey(SaveKeys.LastPlanetIdKey(PlayerB));
        }

        [Test]
        public void LastPlanetIdKey_differs_per_player()
        {
            Assert.AreNotEqual(SaveKeys.LastPlanetIdKey(PlayerA), SaveKeys.LastPlanetIdKey(PlayerB));
        }

        [Test]
        public void LastPlanetIdKey_does_not_leak_between_players_sharing_a_device()
        {
            // Reproduces the reported bug: Player A travels to Mars, then Player B
            // signs in on the same device. Before this fix both accounts read/wrote
            // the same bare "last_planet_id" PlayerPrefs key, so B would see A's planet.
            PlayerPrefs.SetString(SaveKeys.LastPlanetIdKey(PlayerA), "Mars");

            string resumedForB = PlayerPrefs.GetString(SaveKeys.LastPlanetIdKey(PlayerB), "Earth");

            Assert.AreEqual("Earth", resumedForB);
        }
    }
}

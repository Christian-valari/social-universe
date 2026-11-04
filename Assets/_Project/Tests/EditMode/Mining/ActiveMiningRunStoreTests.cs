using System.Reflection;
using NUnit.Framework;
using SocialUniverse.Core;
using SocialUniverse.Mining;
using SocialUniverse.Net;
using UnityEngine;

namespace SocialUniverse.Tests
{
    public class ActiveMiningRunStoreTests
    {
        private LocalMockAuthService _auth;
        private ActiveMiningRunStore _store;

        private void SignInAs(string playerId) =>
            typeof(LocalMockAuthService).GetField("_playerId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_auth, playerId);

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);
            _auth  = new LocalMockAuthService();
            _store = new ActiveMiningRunStore(_auth);
            SignInAs("p1");
        }

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(SaveKeys.ActiveMiningRun);

        [Test]
        public void Save_then_Load_round_trips_and_stamps_the_player()
        {
            _store.Save("earth", "Iron#1", "iron", 80, 0.925f);

            var record = _store.Load();

            Assert.AreEqual("p1", record.PlayerId);
            Assert.AreEqual("earth", record.PlanetId);
            Assert.AreEqual("Iron#1", record.SlotId);
            Assert.AreEqual("iron", record.MineralId);
            Assert.AreEqual(80, record.FullQuantity);
            Assert.AreEqual(0.925f, record.BankedFraction);
        }

        // PlayerPrefs is shared by every account on the device.
        [Test]
        public void Another_players_marker_is_dropped()
        {
            _store.Save("earth", "Iron#1", "iron", 80, 0.5f);

            SignInAs("p2");
            Assert.IsNull(_store.Load());

            SignInAs("p1");
            Assert.IsNull(_store.Load(), "It was deleted, not just hidden");
        }

        [Test]
        public void A_malformed_marker_is_dropped()
        {
            PlayerPrefs.SetString(SaveKeys.ActiveMiningRun, "not json {");

            Assert.IsNull(_store.Load());
            Assert.IsFalse(PlayerPrefs.HasKey(SaveKeys.ActiveMiningRun));
        }

        [Test]
        public void Clearing_the_markers_slot_clears_it()
        {
            _store.Save("earth", "Iron#1", "iron", 80, 0.5f);

            _store.Clear("earth", "Iron#1");

            Assert.IsNull(_store.Load());
        }

        [Test]
        public void Clearing_another_slot_leaves_the_marker()
        {
            _store.Save("earth", "Iron#2", "iron", 80, 0.5f);

            _store.Clear("earth", "Iron#1");

            Assert.AreEqual("Iron#2", _store.Load()?.SlotId);
        }

        // Slot ids repeat from planet to planet, so a late answer for one must not clear another's marker.
        [Test]
        public void Clearing_the_same_slot_on_another_planet_leaves_the_marker()
        {
            _store.Save("earth", "Iron#1", "iron", 80, 0.5f);

            _store.Clear("mars", "Iron#1");

            Assert.AreEqual("Iron#1", _store.Load()?.SlotId);
        }
    }
}

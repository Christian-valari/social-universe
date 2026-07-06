using NUnit.Framework;
using SocialUniverse.Progression;

namespace SocialUniverse.Tests
{
    public class PlayerStateAvatarTests
    {
        [Test]
        public void SetAvatarId_sets_field_and_fires_event()
        {
            var playerState = new PlayerState();
            string eventAvatarId = null;
            playerState.OnAvatarChanged += id => eventAvatarId = id;

            playerState.SetAvatarId("avatar_wizard");

            Assert.AreEqual("avatar_wizard", playerState.AvatarId);
            Assert.AreEqual("avatar_wizard", eventAvatarId);
        }

        [Test]
        public void AvatarId_defaults_to_null()
        {
            var playerState = new PlayerState();

            Assert.IsNull(playerState.AvatarId);
        }
    }
}

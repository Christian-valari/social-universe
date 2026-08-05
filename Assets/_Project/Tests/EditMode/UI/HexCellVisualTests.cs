using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class HexCellVisualTests
    {
        [Test] public void Locked_when_not_unlocked() =>
            Assert.AreEqual(HexCellVisual.State.Locked, HexCellVisual.Resolve(false, null));
        [Test] public void Empty_when_unlocked_no_item() =>
            Assert.AreEqual(HexCellVisual.State.Empty, HexCellVisual.Resolve(true, null));
        [Test] public void Occupied_when_unlocked_with_item() =>
            Assert.AreEqual(HexCellVisual.State.Occupied, HexCellVisual.Resolve(true, "hut"));
        [Test] public void Locked_ignores_item_if_locked() =>
            Assert.AreEqual(HexCellVisual.State.Locked, HexCellVisual.Resolve(false, "hut"));
    }
}

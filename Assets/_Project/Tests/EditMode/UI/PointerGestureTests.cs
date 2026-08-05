using NUnit.Framework;
using UnityEngine;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    public class PointerGestureTests
    {
        [Test] public void Small_move_is_tap() =>
            Assert.IsTrue(PointerGesture.IsTap(new Vector2(100, 100), new Vector2(104, 103), 10f));
        [Test] public void Large_move_is_not_tap() =>
            Assert.IsFalse(PointerGesture.IsTap(new Vector2(100, 100), new Vector2(140, 100), 10f));
    }
}

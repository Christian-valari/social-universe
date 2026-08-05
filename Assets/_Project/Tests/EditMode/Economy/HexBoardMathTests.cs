using System.Linq;
using NUnit.Framework;
using UnityEngine;
using SocialUniverse.Economy;

namespace SocialUniverse.Tests
{
    public class HexBoardMathTests
    {
        [Test] public void HexCount_radius2_is_19() => Assert.AreEqual(19, HexBoardMath.HexCount(2));

        [Test] public void Cells_radius2_has_19_center_first()
        {
            var cells = HexBoardMath.Cells(2);
            Assert.AreEqual(19, cells.Count);
            Assert.AreEqual(new Vector2Int(0, 0), cells[0]);
            Assert.AreEqual(19, cells.Distinct().Count()); // no duplicates
        }

        [Test] public void Free5_is_contiguous_center_cluster()
        {
            var nb = HexBoardMath.Neighbors(2);
            // indices 1..4 are each adjacent to center (0)
            for (int i = 1; i <= 4; i++)
                Assert.Contains(0, nb[i], $"cell {i} should neighbor center");
        }

        [Test] public void Neighbors_are_symmetric()
        {
            var nb = HexBoardMath.Neighbors(2);
            for (int i = 0; i < nb.Length; i++)
                foreach (var j in nb[i])
                    Assert.Contains(i, nb[j], $"{j} lists {i}? symmetry");
        }

        [Test] public void IsAdjacentToUnlocked_true_next_to_free()
        {
            var unlocked = HexBoardMath.EnsureUnlocked(null, 2, 5); // 0..4 true
            // some ring-1 cell not in free set (index 5) must touch an unlocked one
            Assert.IsTrue(HexBoardMath.IsAdjacentToUnlocked(5, unlocked, 2));
        }

        [Test] public void EnsureUnlocked_defaults_free_true_rest_false()
        {
            var u = HexBoardMath.EnsureUnlocked(null, 2, 5);
            Assert.AreEqual(19, u.Length);
            Assert.IsTrue(u.Take(5).All(b => b));
            Assert.IsTrue(u.Skip(5).All(b => !b));
        }

        [Test] public void Price_escalates_linearly()
        {
            Assert.AreEqual(200, HexBoardMath.HexatilePrice(5, 5, 200, 100));   // first buy
            Assert.AreEqual(300, HexBoardMath.HexatilePrice(6, 5, 200, 100));
            Assert.AreEqual(1500, HexBoardMath.HexatilePrice(18, 5, 200, 100)); // 14th buy
        }
    }
}

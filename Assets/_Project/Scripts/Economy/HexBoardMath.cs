using System.Collections.Generic;
using UnityEngine;

namespace SocialUniverse.Economy
{
    // Pure geometry + economy for the LandBuilding hex board. MIRRORED by
    // ServerCode/PurchaseHexatile.js (canonical spiral order, neighbors, price) —
    // keep the two in sync. Canonical order: index 0 = center, then ring 1..radius
    // walked as a spiral, so the first (freeCount) indices form a contiguous cluster.
    public static class HexBoardMath
    {
        // Axial neighbor directions (pointy-top). Direction 4 is the ring start corner.
        static readonly Vector2Int[] Dirs =
        {
            new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1)
        };

        public static int HexCount(int radius) => 3 * radius * radius + 3 * radius + 1;

        public static IReadOnlyList<Vector2Int> Cells(int radius)
        {
            var cells = new List<Vector2Int> { Vector2Int.zero };
            for (int k = 1; k <= radius; k++)
            {
                var hex = Dirs[4] * k;                 // start corner of ring k
                for (int side = 0; side < 6; side++)
                    for (int step = 0; step < k; step++)
                    {
                        cells.Add(hex);
                        hex += Dirs[side];
                    }
            }
            return cells;
        }

        public static int[][] Neighbors(int radius)
        {
            var cells = Cells(radius);
            var index = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < cells.Count; i++) index[cells[i]] = i;

            var result = new int[cells.Count][];
            for (int i = 0; i < cells.Count; i++)
            {
                var list = new List<int>();
                foreach (var d in Dirs)
                    if (index.TryGetValue(cells[i] + d, out var n)) list.Add(n);
                result[i] = list.ToArray();
            }
            return result;
        }

        public static bool IsAdjacentToUnlocked(int index, bool[] unlocked, int radius)
        {
            var nb = Neighbors(radius);
            if (index < 0 || index >= nb.Length) return false;
            foreach (var n in nb[index])
                if (n < unlocked.Length && unlocked[n]) return true;
            return false;
        }

        public static bool[] EnsureUnlocked(bool[] src, int radius, int freeCount)
        {
            int n = HexCount(radius);
            var result = new bool[n];
            if (src != null)
                for (int i = 0; i < n && i < src.Length; i++) result[i] = src[i];
            else
                for (int i = 0; i < freeCount && i < n; i++) result[i] = true;
            return result;
        }

        public static Vector3[] LocalPositions(int radius, float size)
        {
            var cells = Cells(radius);
            var pos = new Vector3[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                float q = cells[i].x, r = cells[i].y;
                float x = size * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
                float z = size * (1.5f * r);
                pos[i] = new Vector3(x, 0f, z);
            }
            return pos;
        }

        public static int HexatilePrice(int unlockedCount, int freeCount, int basePrice, int step) =>
            basePrice + step * (unlockedCount - freeCount);
    }
}

using SocialUniverse.Core;

namespace SocialUniverse.World
{
    // P1 — identifies the 12 pentagon tiles inherent to any hexasphere geometry.
    public class LandmarkService
    {
        private const int LandmarkCount = 12;

        public void MarkLandmarks(HexasphereManager hexasphere)
        {
            // TODO: Query hexasphere.PentagonTileIds once the asset is installed.
            // Pentagon tiles are guaranteed by icosahedral subdivision geometry.
            int marked = 0;
            foreach (var tile in hexasphere.Tiles.Values)
            {
                if (marked >= LandmarkCount) break;
                tile.IsLandmark = true;
                tile.State      = TileState.Landmark;
                marked++;
            }

            SULog.Info($"LandmarkService: marked {marked} landmark tiles (stub)", SULog.Channel.World);
        }
    }
}

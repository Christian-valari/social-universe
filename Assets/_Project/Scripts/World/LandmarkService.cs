using SocialUniverse.Core;

namespace SocialUniverse.World
{
    public class LandmarkService
    {
        private const int LandmarkCount = 12;

        public void MarkLandmarks(HexasphereManager hexasphere)
        {
            int marked = 0;
            foreach (var tileId in hexasphere.PentagonTileIds)
            {
                if (marked >= LandmarkCount) break;
                var tile = hexasphere.GetTile(tileId);
                if (tile == null) continue;
                tile.IsLandmark = true;
                tile.State = TileState.Landmark;
                marked++;
            }

            SULog.Info($"LandmarkService: marked {marked} landmark tiles.", SULog.Channel.World);
        }
    }
}

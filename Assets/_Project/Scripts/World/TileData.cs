namespace SocialUniverse.World
{
    public enum TileState { Available, OwnedByPlayer, OwnedByOther, Landmark }

    public class TileData
    {
        public string    TileId     { get; }
        public string    OwnerId    { get; set; }
        public TileState State      { get; set; } = TileState.Available;
        public float     YieldRate  { get; set; }
        public bool      IsLandmark { get; set; }
        public int       BuildLevel { get; set; }

        public TileData(string tileId) => TileId = tileId;
    }
}

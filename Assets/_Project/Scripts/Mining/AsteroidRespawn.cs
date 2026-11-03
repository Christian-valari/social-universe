namespace SocialUniverse.Mining
{
    // One asteroid slot the player emptied in the last few hours, as GetAsteroidRespawns reports it.
    // Shape MUST MATCH ServerCode/GetAsteroidRespawns.js. SecondsLeft is measured on the server clock,
    // so every device brings the asteroid back at the same moment.
    public class AsteroidRespawn
    {
        public string SlotId;
        public string MineralId;
        public float  SecondsLeft;
    }
}

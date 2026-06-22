using System;

namespace SocialUniverse.Progression
{
    public class PlayerState
    {
        public string PlayerId    { get; set; } = "local_player";
        public string DisplayName { get; private set; } = "Player";
        public int    Level       { get; private set; } = 1;
        public int    XP          { get; private set; }
        public float  Fuel        { get; private set; } = 100f;
        public float  MaxFuel     { get; private set; } = 100f;

        public event Action<string> OnDisplayNameChanged;
        public event Action<int>    OnLevelChanged;
        public event Action<float>  OnFuelChanged;
        public event Action<float>  OnMaxFuelChanged;

        public void SetDisplayName(string name)
        {
            DisplayName = name;
            OnDisplayNameChanged?.Invoke(name);
        }

        public void AddXP(int amount) => XP += amount;

        public void SetLevel(int level)
        {
            Level = level;
            OnLevelChanged?.Invoke(Level);
        }

        public void SetFuel(float value)
        {
            Fuel = Math.Clamp(value, 0f, MaxFuel);
            OnFuelChanged?.Invoke(Fuel);
        }

        public void SetMaxFuel(float value)
        {
            MaxFuel = value;
            OnMaxFuelChanged?.Invoke(MaxFuel);
            SetFuel(Fuel); // re-clamp against the new max
        }
    }
}

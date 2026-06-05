using System;

namespace SocialUniverse.Economy
{
    public class Wallet
    {
        public int Coins    { get; private set; }
        public int Stardust { get; private set; }

        public event Action<int> OnCoinsChanged;
        public event Action<int> OnStardustChanged;

        public bool CanAfford(int coins) => Coins >= coins;

        public void SetCoins(int value)
        {
            Coins = value;
            OnCoinsChanged?.Invoke(Coins);
        }

        public void SetStardust(int value)
        {
            Stardust = value;
            OnStardustChanged?.Invoke(Stardust);
        }
    }
}

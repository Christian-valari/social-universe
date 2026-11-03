using System.Threading.Tasks;

namespace SocialUniverse.Economy
{
    public interface IEconomyService
    {
        Task<Wallet> GetWalletAsync();
        Task<bool>   SpendCoinsAsync(int amount);

        // No grant methods: the client never mints currency (Architecture Rule 1). Coins and
        // Stardust are only granted by server functions that validate their own reason
        // (ValidateMining → SellMinerals, ClaimYield, SellLand, …).
    }
}

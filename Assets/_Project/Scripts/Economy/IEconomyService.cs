using System.Threading.Tasks;

namespace SocialUniverse.Economy
{
    public interface IEconomyService
    {
        Task<Wallet> GetWalletAsync();
        Task<bool>   SpendCoinsAsync(int amount);
        Task         GrantCoinsAsync(int amount);
        Task         GrantStardustAsync(int amount);
    }
}

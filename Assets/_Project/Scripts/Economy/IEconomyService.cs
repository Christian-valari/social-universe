using System.Threading.Tasks;

namespace SocialUniverse.Economy
{
    public interface IEconomyService
    {
        Task<Wallet> GetWalletAsync();
        Task<bool>   SpendCoinsAsync(int amount);
        Task         GrantCoinsAsync(int amount);

        // Idle-claim and active-mining-success payouts go through here instead of
        // GrantCoinsAsync, so the server can validate the amount against the session's
        // duration/rate rather than trusting a bare client-supplied amount.
        Task<int> GrantMiningRewardAsync(int claimedCoins, float sessionDurationSec, float coinsPerSec);

        Task         GrantStardustAsync(int amount);
    }
}

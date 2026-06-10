# Unity Economy — Currency Configuration

Reference for the currencies to configure in the Unity Cloud Dashboard
(**Economy → Currencies**) for this project. Values must mirror
`Assets/_Project/ScriptableObjects/EconomyConfig.asset`
(`Assets/_Project/Scripts/Config/EconomyConfig.cs`) so client-side
`LocalMockEconomy` and the real `EconomyService` start new players in the
same state.

Unity Economy grants each currency's **Initial Balance** to a player's wallet
automatically the first time `getPlayerCurrencies` (or any balance read) is
called for that player — no Cloud Code grant is needed for starting funds.

| ID | Name | Type | Initial Balance | Max Balance | Notes |
|---|---|---|---|---|---|
| `COINS` | Coins | Currency | **500** | 999,999,999 | Soft currency. Matches `EconomyConfig.StartingCoins`. Earned via mining, spent on land/upgrades. |
| `STARDUST` | Stardust | Currency | **10** | 999,999,999 | Premium currency. Matches `EconomyConfig.StartingStardust`. Granted via `GrantStardust`, spent on premium purchases. |

## Dashboard setup steps

1. Open the project in [Unity Cloud Dashboard](https://cloud.unity.com) → **Economy** → **Currencies**.
2. Create **Coins**:
   - ID: `COINS`
   - Name: `Coins`
   - Type: `Currency`
   - Initial balance: `500`
   - Max balance: `999999999` (or leave default cap if higher)
3. Create **Stardust**:
   - ID: `STARDUST`
   - Name: `Stardust`
   - Type: `Currency`
   - Initial balance: `10`
   - Max balance: `999999999`
4. Publish the changes to each environment used (`development`, `production`).

## Keeping in sync

If `EconomyConfig.StartingCoins` / `StartingStardust` ever change, update the
**Initial Balance** values above in the dashboard for every environment —
these are configured server-side and are not pushed automatically from the
client ScriptableObject.

No `InventoryItems` or `VirtualPurchases` are defined yet; only the two
currencies above are referenced by `ServerCode/` (see `CLOUD_CODE_FUNCTIONS.md`).

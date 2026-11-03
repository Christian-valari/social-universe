using NUnit.Framework;
using SocialUniverse.UI;

namespace SocialUniverse.Tests
{
    // The mineral sale popup's wording and button state (TransactionModal shows these as-is).
    public class MineralSalePromptTests
    {
        private static readonly MineralSale TwelveIron = MineralSale.ForMineral("iron", "Iron", null, 12, 30);
        private static readonly MineralSale Everything = MineralSale.ForAll(23, 1250);

        [Test]
        public void Selling_one_mineral_names_the_quantity_and_unit_value()
        {
            var s = MineralSalePrompt.Confirm(TwelveIron);

            Assert.AreEqual("Sell 12 Iron?", s.Title);
            Assert.AreEqual("30 coins each", s.Body);
            Assert.AreEqual("You'll get 360 coins", s.Amount);
            Assert.AreEqual("Sell", s.ConfirmLabel);
            Assert.AreEqual("", s.Status);
            Assert.IsTrue(s.ConfirmEnabled);
        }

        [Test]
        public void Selling_everything_counts_the_units()
        {
            var s = MineralSalePrompt.Confirm(Everything);

            Assert.AreEqual("Sell all minerals?", s.Title);
            Assert.AreEqual("23 minerals", s.Body);
            Assert.AreEqual("You'll get 1250 coins", s.Amount);
            Assert.AreEqual("Sell all", s.ConfirmLabel);
        }

        [Test]
        public void A_single_unit_reads_singular()
        {
            Assert.AreEqual("1 mineral", MineralSalePrompt.Confirm(MineralSale.ForAll(1, 30)).Body);
        }

        [Test]
        public void Nothing_to_sell_disables_confirm()
        {
            Assert.IsFalse(MineralSalePrompt.Confirm(MineralSale.ForAll(0, 0)).ConfirmEnabled);
        }

        [Test]
        public void Success_for_one_mineral_shows_what_sold_and_the_coins()
        {
            var s = MineralSalePrompt.Success(TwelveIron);

            Assert.AreEqual("Sold 12 Iron", s.Title);
            Assert.AreEqual("+360 coins", s.Body);
        }

        [Test]
        public void Success_for_everything_counts_the_units()
        {
            var s = MineralSalePrompt.Success(Everything);

            Assert.AreEqual("Sold 23 minerals", s.Title);
            Assert.AreEqual("+1250 coins", s.Body);
        }
    }
}

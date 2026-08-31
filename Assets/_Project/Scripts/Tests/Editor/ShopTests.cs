using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    /**
     * ShopTests
     *
     * 카드 상점 진열 추첨 (DESIGN §13.2) — ShopOffer
     * 보유 카드 인벤토리는 StageTests 참고
     */
    public class ShopTests
    {
        [Test]
        public void 상점은_카드_4장을_진열한다()
        {
            Assert.AreEqual(ShopOffer.OfferCount, new ShopOffer(1).Roll(null).Count);
        }

        [Test]
        public void 진열한_카드는_서로_다르다()
        {
            var cards = new ShopOffer(1).Roll(null);

            Assert.AreEqual(cards.Count, cards.Distinct().Count());
        }

        [Test]
        public void 같은_시드는_같은_진열을_만든다()
        {
            CollectionAssert.AreEqual(new ShopOffer(7).Roll(null), new ShopOffer(7).Roll(null));
        }

        [Test]
        public void 다른_시드는_보통_다른_진열을_만든다()
        {
            Assert.IsFalse(new ShopOffer(1).Roll(null).SequenceEqual(new ShopOffer(2).Roll(null)));
        }

        [Test]
        public void 이미_든_카드는_진열에_안_나온다()
        {
            var held = new[]
            {
                new Card(Rank.Ace, Suit.Spade),
                new Card(Rank.King, Suit.Heart),
                new Card(Rank.Two, Suit.Club),
            };

            for (int seed = 0; seed < 40; seed++)
            {
                var cards = new ShopOffer(seed).Roll(held);

                foreach (var h in held)
                {
                    CollectionAssert.DoesNotContain(cards, h);
                }
            }
        }
    }
}

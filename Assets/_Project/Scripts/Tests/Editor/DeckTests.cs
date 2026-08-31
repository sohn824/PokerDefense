using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    public class DeckTests
    {
        [Test]
        public void 덱은_중복_없는_52장이다()
        {
            var deck = new Deck(seed: 1);
            Assert.AreEqual(52, deck.Remaining);

            var drawn = new List<Card>();
            while (deck.Remaining > 0)
            {
                drawn.Add(deck.Draw());
            }

            Assert.AreEqual(52, drawn.Distinct().Count());
        }

        [Test]
        public void 같은_시드는_같은_순서를_만든다()
        {
            Assert.AreEqual(DrawAll(new Deck(seed: 42)), DrawAll(new Deck(seed: 42)));
        }

        [Test]
        public void 다른_시드는_다른_순서를_만든다()
        {
            Assert.AreNotEqual(DrawAll(new Deck(seed: 1)), DrawAll(new Deck(seed: 2)));
        }

        [Test]
        public void 드로우하면_남은_장수가_줄어든다()
        {
            var deck = new Deck(seed: 7);
            deck.Draw();
            deck.Draw();
            Assert.AreEqual(50, deck.Remaining);
        }

        [Test]
        public void 빈_덱에서_드로우하면_예외를_던진다()
        {
            var deck = new Deck(seed: 7);
            for (int i = 0; i < 52; i++)
            {
                deck.Draw();
            }

            Assert.Throws<InvalidOperationException>(() => deck.Draw());
        }

        [Test]
        public void 제외한_카드는_덱에_없다()
        {
            var excluded = new[]
            {
                new Card(Rank.Ace, Suit.Spade),
                new Card(Rank.King, Suit.Heart),
            };
            var deck = new Deck(seed: 1, excluded);

            Assert.AreEqual(50, deck.Remaining);

            var drawn = DrawAll(deck);
            CollectionAssert.DoesNotContain(drawn, excluded[0]);
            CollectionAssert.DoesNotContain(drawn, excluded[1]);
        }

        [Test]
        public void 빈_제외_목록이면_52장_그대로다()
        {
            var deck = new Deck(seed: 1, new Card[0]);
            Assert.AreEqual(52, deck.Remaining);
        }

        static List<Card> DrawAll(Deck deck)
        {
            var cards = new List<Card>(52);
            while (deck.Remaining > 0)
            {
                cards.Add(deck.Draw());
            }

            return cards;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerDefense.Poker;

namespace PokerDefense.Tests
{
    /**
     * DeckTests
     *
     * 덱이 중복 없는 52장으로 시드에 따라 섞이고, 뽑을 때마다 줄어드는지 검사하는 클래스
     */
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
        public void BuildCards는_제외한_카드를_뺀다()
        {
            var excluded = new[]
            {
                new Card(Rank.Ace, Suit.Spade),
                new Card(Rank.King, Suit.Heart),
            };
            var cards = Deck.BuildCards(excluded);

            Assert.AreEqual(50, cards.Count);
            CollectionAssert.DoesNotContain(cards, excluded[0]);
            CollectionAssert.DoesNotContain(cards, excluded[1]);
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

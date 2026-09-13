using System.Linq;
using NUnit.Framework;
using PokerDefense.Game;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Tests
{
    public class HandLoopTests
    {
        [Test]
        public void 선택_교체가_없거나_사용한_뒤에는_잠긴_자리의_목표가_사라진다()
        {
            var round = new RoundContext(12);
            round.Draw();
            round.Exchange(new[] { 0, 1, 2, 3, 4 });
            Assert.IsEmpty(round.FindGoals(false));
            Assert.IsNotEmpty(round.FindGoals());
            round.TryRevealCandidates(0);
            round.ChooseCandidate(0);
            Assert.IsEmpty(round.FindGoals());
        }

        [Test]
        public void 막힌_자리나_없는_카드로_목표를_제안하지_않는다()
        {
            var hand = new[] { new Card(Rank.Two,Suit.Heart), new Card(Rank.Five,Suit.Heart),
                new Card(Rank.Seven,Suit.Heart), new Card(Rank.Ten,Suit.Heart),new Card(Rank.King,Suit.Club) };
            var heart = new Card(Rank.Queen,Suit.Heart);
            Assert.IsEmpty(HandGoals.Find(hand, new[]{heart}, new bool[5]));
            Assert.IsEmpty(HandGoals.Find(hand, new Card[0], new[]{false,false,false,false,true}));
            var goals = HandGoals.Find(hand,new[]{heart},new[]{false,false,false,false,true});
            Assert.AreEqual(HandCategory.Flush,goals.Single().Category);
            CollectionAssert.AreEqual(new[]{heart},goals.Single().Cards);
            Assert.AreEqual(Suit.Club,hand[4].Suit);
        }

        [Test]
        public void 가득_찬_보드도_확인한_유닛만_교체하고_실패시_유지한다()
        {
            var a = ScriptableObject.CreateInstance<UnitDefinition>();
            var b = ScriptableObject.CreateInstance<UnitDefinition>();
            try
            {
                var board = new GridBoard();
                for(int i=0;i<GridBoard.SlotCount;i++) board.TryPlace(i,new UnitInstance(a));
                UnitInstance old=board[4];
                var next=new UnitInstance(b);
                Assert.IsFalse(board.TryReplace(4, new UnitInstance(a),next));
                Assert.AreSame(old,board[4]);
                Assert.IsFalse(board.TryReplace(4,old,new UnitInstance(a)), "합치기를 퇴장으로 처리하면 안 됨");
                Assert.IsFalse(board.TryReplace(-1,old,next));
                Assert.IsTrue(board.TryReplace(4,old,next));
                Assert.AreSame(next,board[4]);
                Assert.AreEqual(GridBoard.SlotCount,board.OccupiedCount);
                Assert.IsFalse(board.TryReplace(4,old,new UnitInstance(a)));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }
    }
}

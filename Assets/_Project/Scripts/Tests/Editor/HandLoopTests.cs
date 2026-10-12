using NUnit.Framework;
using PokerDefense.Game;
using UnityEngine;

namespace PokerDefense.Tests
{
    public class HandLoopTests
    {
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

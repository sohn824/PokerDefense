using System;
using NUnit.Framework;
using PokerDefense.Game;
using UnityEngine;

namespace PokerDefense.Tests
{
    public class GridBoardTests
    {
        UnitDefinition archer;
        UnitDefinition knight;

        /// <summary>
        /// UnitDefinition은 ScriptableObject라 new로 못 만든다. 에셋을 읽지 않고 메모리에만 띄운다.
        /// </summary>
        static UnitDefinition MakeDefinition(string id, float attackPower)
        {
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            var so = new UnityEditor.SerializedObject(definition);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("attackPower").floatValue = attackPower;

            var multipliers = so.FindProperty("starMultipliers");
            multipliers.arraySize = 3;
            multipliers.GetArrayElementAtIndex(0).floatValue = 1f;
            multipliers.GetArrayElementAtIndex(1).floatValue = 2f;
            multipliers.GetArrayElementAtIndex(2).floatValue = 4f;

            so.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        [SetUp]
        public void SetUp()
        {
            archer = MakeDefinition("archer", 10f);
            knight = MakeDefinition("knight", 20f);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(archer);
            UnityEngine.Object.DestroyImmediate(knight);
        }

        UnitInstance Archer() => new UnitInstance(archer);

        UnitInstance Knight() => new UnitInstance(knight);

        [Test]
        public void 보드는_5열_3행_15슬롯이다()
        {
            Assert.AreEqual(5, GridBoard.Columns);
            Assert.AreEqual(3, GridBoard.Rows);
            Assert.AreEqual(15, GridBoard.SlotCount);
        }

        [Test]
        public void 처음에는_모든_슬롯이_비어_있다()
        {
            var board = new GridBoard();

            Assert.AreEqual(0, board.OccupiedCount);
            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                Assert.IsNull(board[i], $"{i}번 슬롯이 비어 있지 않다");
            }
        }

        [Test]
        public void 빈_칸에_놓으면_배치된다()
        {
            var board = new GridBoard();
            var unit = Archer();

            Assert.AreEqual(PlacementResult.Placed, board.TryPlace(7, unit));
            Assert.AreSame(unit, board[7]);
            Assert.AreEqual(1, board.OccupiedCount);
        }

        [Test]
        public void 같은_유닛_같은_성급이면_머지되어_별이_오른다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());

            Assert.AreEqual(PlacementResult.Merged, board.TryPlace(0, Archer()));
            Assert.AreEqual(2, board[0].Star);
        }

        [Test]
        public void 머지하면_슬롯_수가_늘지_않는다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            board.TryPlace(0, Archer());

            Assert.AreEqual(1, board.OccupiedCount, "머지는 자리를 추가로 먹지 않아야 한다");
        }

        [Test]
        public void 머지하면_공격력이_성급_배수만큼_오른다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            float star1 = board[0].AttackPower;

            board.TryPlace(0, Archer());
            float star2 = board[0].AttackPower;

            Assert.AreEqual(10f, star1);
            Assert.AreEqual(20f, star2);
        }

        /// <summary>0번과 1번을 각각 ★2로 만든다.</summary>
        static void TwoStarPair(GridBoard board, System.Func<UnitInstance> make)
        {
            board.TryPlace(0, make());
            board.TryPlace(0, make());
            board.TryPlace(1, make());
            board.TryPlace(1, make());
        }

        [Test]
        public void 별2_둘을_슬롯간_머지하면_별3이_된다()
        {
            // 소환 유닛을 놓을 때만 머지되면 ★2 두 기를 합칠 방법이 없어 ★3에 도달하지 못한다.
            var board = new GridBoard();
            TwoStarPair(board, Archer);

            Assert.AreEqual(PlacementResult.Merged, board.TryMergeSlots(1, 0));
            Assert.AreEqual(3, board[0].Star);
            Assert.AreEqual(40f, board[0].AttackPower);
        }

        [Test]
        public void 슬롯간_머지하면_원래_자리가_비워진다()
        {
            var board = new GridBoard();
            TwoStarPair(board, Archer);

            board.TryMergeSlots(1, 0);

            Assert.IsNull(board[1], "합쳐진 쪽 슬롯이 비워지지 않았다");
            Assert.AreEqual(1, board.OccupiedCount);
        }

        [Test]
        public void 슬롯간_머지도_성급과_종류가_같아야_한다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            board.TryPlace(1, Knight());
            board.TryPlace(2, Archer());
            board.TryPlace(2, Archer());   // ★2

            Assert.AreEqual(PlacementResult.Rejected, board.TryMergeSlots(1, 0), "종류가 다르다");
            Assert.AreEqual(PlacementResult.Rejected, board.TryMergeSlots(2, 0), "성급이 다르다");
            Assert.AreEqual(3, board.OccupiedCount, "거부된 머지가 보드를 바꿨다");
            Assert.AreEqual(1, board[0].Star);
        }

        [Test]
        public void 같은_슬롯끼리는_머지되지_않는다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());

            Assert.AreEqual(PlacementResult.Rejected, board.TryMergeSlots(0, 0));
            Assert.AreEqual(1, board[0].Star);
        }

        [Test]
        public void 빈_칸은_슬롯간_머지의_대상이_아니다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());

            Assert.AreEqual(PlacementResult.Rejected, board.TryMergeSlots(0, 5), "빈 칸으로는 못 합친다");
            Assert.AreEqual(PlacementResult.Rejected, board.TryMergeSlots(5, 0), "빈 칸을 재료로 못 쓴다");
            Assert.AreSame(archer, board[0].Definition);
        }

        [Test]
        public void 별3끼리는_슬롯간_머지도_안_된다()
        {
            var board = new GridBoard();
            TwoStarPair(board, Archer);
            board.TryMergeSlots(1, 0);     // 0번 ★3

            TwoStarPair2(board);
            Assert.AreEqual(3, board[0].Star);
            Assert.AreEqual(3, board[2].Star);
            Assert.AreEqual(PlacementResult.Rejected, board.TryMergeSlots(2, 0));
            Assert.AreEqual(3, board[0].Star);
        }

        /// <summary>2번 슬롯을 ★3으로 만든다 (3·4번을 재료로 쓴다).</summary>
        void TwoStarPair2(GridBoard board)
        {
            board.TryPlace(2, Archer());
            board.TryPlace(2, Archer());   // ★2
            board.TryPlace(3, Archer());
            board.TryPlace(3, Archer());   // ★2
            board.TryMergeSlots(3, 2);     // ★3
        }

        [Test]
        public void HasMergePartner는_합칠_상대가_있을_때만_참이다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            board.TryPlace(1, Archer());
            board.TryPlace(2, Knight());

            Assert.IsTrue(board.HasMergePartner(0));
            Assert.IsTrue(board.HasMergePartner(1));
            Assert.IsFalse(board.HasMergePartner(2), "짝 없는 유닛");
            Assert.IsFalse(board.HasMergePartner(9), "빈 칸");
        }

        [Test]
        public void CanMergeSlots는_TryMergeSlots의_결과와_일치한다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            board.TryPlace(1, Archer());
            board.TryPlace(2, Knight());

            Assert.IsTrue(board.CanMergeSlots(1, 0));
            Assert.IsFalse(board.CanMergeSlots(2, 0));
            Assert.IsFalse(board.CanMergeSlots(0, 0));
            Assert.IsFalse(board.CanMergeSlots(0, 9));
        }

        [Test]
        public void 성급이_다르면_머지되지_않는다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            board.TryPlace(0, Archer());   // ★2

            Assert.AreEqual(PlacementResult.Rejected, board.TryPlace(0, Archer()), "★2에 ★1을 얹을 수 없다");
            Assert.AreEqual(2, board[0].Star);
        }

        [Test]
        public void 다른_종류는_머지되지_않는다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());

            Assert.AreEqual(PlacementResult.Rejected, board.TryPlace(0, Knight()));
            Assert.AreSame(archer, board[0].Definition);
            Assert.AreEqual(1, board[0].Star);
        }

        [Test]
        public void 별3에는_소환_유닛을_얹을_수_없다()
        {
            var board = new GridBoard();
            TwoStarPair(board, Archer);
            board.TryMergeSlots(1, 0);   // 0번 ★3

            Assert.AreEqual(3, board[0].Star);
            Assert.AreEqual(PlacementResult.Rejected, board.TryPlace(0, Archer()));
            Assert.AreEqual(3, board[0].Star);
        }

        [Test]
        public void 거부된_배치는_보드를_바꾸지_않는다()
        {
            var board = new GridBoard();
            var placed = Archer();
            board.TryPlace(3, placed);

            board.TryPlace(3, Knight());

            Assert.AreSame(placed, board[3]);
            Assert.AreEqual(1, board.OccupiedCount);
        }

        [Test]
        public void CanPlaceAt는_TryPlace의_결과와_일치한다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            board.TryPlace(1, Knight());

            Assert.IsTrue(board.CanPlaceAt(2, Archer()), "빈 칸");
            Assert.IsTrue(board.CanPlaceAt(0, Archer()), "같은 유닛 같은 성급");
            Assert.IsFalse(board.CanPlaceAt(1, Archer()), "다른 유닛");
        }

        [Test]
        public void 보드가_가득_차면_새_유닛을_받지_못한다()
        {
            var board = new GridBoard();
            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                board.TryPlace(i, Knight());
            }

            Assert.IsFalse(board.CanAccept(Archer()), "빈 칸도 머지 대상도 없다");
        }

        [Test]
        public void 가득_차도_머지할_수_있으면_받는다()
        {
            var board = new GridBoard();
            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                board.TryPlace(i, Knight());
            }

            Assert.IsTrue(board.CanAccept(Knight()), "가득 차 있어도 같은 유닛이면 머지로 받을 수 있다");
        }

        [TestCase(-1)]
        [TestCase(15)]
        public void 범위를_벗어난_슬롯은_예외를_던진다(int index)
        {
            var board = new GridBoard();

            Assert.Throws<ArgumentOutOfRangeException>(() => board.TryPlace(index, Archer()));
        }

        [Test]
        public void 널_유닛은_예외를_던진다()
        {
            var board = new GridBoard();

            Assert.Throws<ArgumentNullException>(() => board.TryPlace(0, null));
        }

        [Test]
        public void Clear하면_전부_비워진다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            board.TryPlace(5, Knight());

            board.Clear();

            Assert.AreEqual(0, board.OccupiedCount);
        }

        [Test]
        public void 머지는_원본_인스턴스를_바꾸지_않는다()
        {
            // UnitInstance는 불변이다. 승급은 새 인스턴스를 만든다.
            var board = new GridBoard();
            var original = Archer();
            board.TryPlace(0, original);

            board.TryPlace(0, Archer());

            Assert.AreEqual(1, original.Star, "원본 인스턴스의 성급이 바뀌었다");
            Assert.AreEqual(2, board[0].Star);
        }
    }
}

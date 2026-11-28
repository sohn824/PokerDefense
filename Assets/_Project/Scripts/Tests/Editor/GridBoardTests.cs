using System;
using NUnit.Framework;
using PokerDefense.Game;
using UnityEngine;

namespace PokerDefense.Tests
{
    /**
     * GridBoardTests
     *
     * 보드의 슬롯 좌표와 배치·머지·이동·자리 교환 규칙을 검사하는 클래스
     */
    public class GridBoardTests
    {
        UnitDefinition archer;
        UnitDefinition knight;

        // UnitDefinition은 ScriptableObject라 new로 못 만듦 -> 에셋 없이 메모리에만 만들고 SerializedObject로 private 필드를 채움
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

        // 0번과 1번을 각각 ★2로 만듦
        static void TwoStarPair(GridBoard board, Func<UnitInstance> make)
        {
            board.TryPlace(0, make());
            board.TryPlace(0, make());
            board.TryPlace(1, make());
            board.TryPlace(1, make());
        }

        // ---------- 슬롯 좌표 ----------

        [Test]
        public void 슬롯은_좌상단부터_우하단으로_놓이고_중앙이_원점이다()
        {
            // 5 × 3이라 정가운데(0부터 세어 1행 2열 = 7번)가 보드 중심
            Assert.AreEqual(Vector2.zero, GridBoard.SlotToLocalPosition(7));

            Vector2 topLeft = GridBoard.SlotToLocalPosition(0);
            Vector2 bottomRight = GridBoard.SlotToLocalPosition(14);

            Assert.Less(topLeft.x, 0f, "0번이 왼쪽이 아니다");
            Assert.Greater(topLeft.y, 0f, "0번이 위쪽이 아니다");
            Assert.Greater(bottomRight.x, 0f, "14번이 오른쪽이 아니다");
            Assert.Less(bottomRight.y, 0f, "14번이 아래쪽이 아니다");
        }

        [Test]
        public void 범위를_벗어난_슬롯은_예외를_던진다()
        {
            var board = new GridBoard();

            foreach (int index in new[] { -1, GridBoard.SlotCount })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => GridBoard.SlotToLocalPosition(index), $"좌표 {index}");
                Assert.Throws<ArgumentOutOfRangeException>(() => board.TryPlace(index, Archer()), $"배치 {index}");
            }
        }

        // ---------- 배치와 머지 ----------

        [Test]
        public void 빈_칸에_놓고_비울_수_있다()
        {
            var board = new GridBoard();
            var unit = Archer();

            Assert.AreEqual(PlacementResult.Placed, board.TryPlace(7, unit));
            Assert.AreSame(unit, board[7]);
            Assert.AreEqual(1, board.OccupiedCount);

            board.ClearAt(7);
            Assert.IsNull(board[7]);
            Assert.AreEqual(0, board.OccupiedCount);

            Assert.DoesNotThrow(() => board.ClearAt(7), "빈 칸을 비워도 예외가 나면 안 됨");
        }

        [Test]
        public void 같은_유닛_같은_성급을_놓으면_머지되어_공격력이_오른다()
        {
            // UnitInstance는 불변이라 승급은 새 객체를 만듦
            var board = new GridBoard();
            var original = Archer();
            board.TryPlace(0, original);
            Assert.AreEqual(10f, board[0].AttackPower);

            Assert.AreEqual(PlacementResult.Merged, board.TryPlace(0, Archer()));
            Assert.AreEqual(2, board[0].Star);
            Assert.AreEqual(20f, board[0].AttackPower, "성급 배수만큼 오른다");
            Assert.AreEqual(1, original.Star, "원본 인스턴스의 성급이 바뀌었다");
        }

        [Test]
        public void 머지할_수_없는_배치는_거부되고_보드를_바꾸지_않는다()
        {
            var board = new GridBoard();
            var placed = Archer();
            board.TryPlace(3, placed);

            Assert.AreEqual(PlacementResult.Rejected, board.TryPlace(3, Knight()), "다른 종류");
            Assert.AreSame(placed, board[3]);
            Assert.AreEqual(1, board.OccupiedCount);

            board.TryPlace(4, Archer());
            board.TryPlace(4, Archer());   // ★2
            Assert.AreEqual(PlacementResult.Rejected, board.TryPlace(4, Archer()), "★2에 ★1을 얹을 수 없다");
            Assert.AreEqual(2, board[4].Star);

            TwoStarPair(board, Archer);
            board.TryMergeSlots(1, 0);   // 0번 ★3
            Assert.AreEqual(PlacementResult.Rejected, board.TryPlace(0, Archer()), "★3에는 얹을 수 없다");
            Assert.AreEqual(3, board[0].Star);
        }

        [Test]
        public void 별2_둘을_슬롯간_머지하면_별3이_되고_원래_자리가_비워진다()
        {
            // 소환 유닛을 놓을 때만 머지되면 ★2 두 기를 합칠 방법이 없어 ★3에 도달하지 못함
            var board = new GridBoard();
            TwoStarPair(board, Archer);

            Assert.AreEqual(PlacementResult.Rejected, board.TryMergeSlots(0, 0), "같은 슬롯끼리는 머지하지 않는다");

            Assert.AreEqual(PlacementResult.Merged, board.TryMergeSlots(1, 0));
            Assert.AreEqual(3, board[0].Star);
            Assert.AreEqual(40f, board[0].AttackPower);
            Assert.IsNull(board[1], "합쳐진 쪽 슬롯이 비워지지 않았다");
            Assert.AreEqual(1, board.OccupiedCount);
        }

        [Test]
        public void 가득_찬_보드는_머지할_수_있는_유닛만_받는다()
        {
            var board = new GridBoard();
            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                board.TryPlace(i, Knight());
            }

            Assert.IsFalse(board.CanAccept(Archer()), "빈 칸도 머지 대상도 없다");
            Assert.IsTrue(board.CanAccept(Knight()), "가득 차 있어도 같은 유닛이면 머지로 받을 수 있다");
        }

        [Test]
        public void 미리_보는_판정은_실제_배치_머지_결과와_일치한다()
        {
            var board = new GridBoard();
            board.TryPlace(0, Archer());
            board.TryPlace(1, Archer());
            board.TryPlace(2, Knight());

            Assert.IsTrue(board.CanPlaceAt(5, Archer()), "빈 칸");
            Assert.IsTrue(board.CanPlaceAt(0, Archer()), "같은 유닛 같은 성급");
            Assert.IsFalse(board.CanPlaceAt(2, Archer()), "다른 유닛");

            Assert.IsTrue(board.CanMergeSlots(1, 0));
            Assert.IsFalse(board.CanMergeSlots(2, 0));
            Assert.IsFalse(board.CanMergeSlots(0, 0));
            Assert.IsFalse(board.CanMergeSlots(0, 9));

            Assert.IsTrue(board.HasMergePartner(0));
            Assert.IsFalse(board.HasMergePartner(2), "짝 없는 유닛");
            Assert.IsFalse(board.HasMergePartner(9), "빈 칸");
        }

        // ---------- 이동과 자리 교환 ----------

        [Test]
        public void 유닛은_빈_칸으로만_옮길_수_있다()
        {
            // 옮기기와 머지를 섞지 않음 (머지는 TryMergeSlots가 맡음)
            var board = new GridBoard();
            var unit = Archer();
            board.TryPlace(0, unit);
            board.TryPlace(1, Archer());

            Assert.IsFalse(board.TryMoveSlot(0, 1), "유닛이 있는 칸");
            Assert.AreEqual(1, board[1].Star);

            Assert.IsTrue(board.TryMoveSlot(0, 8));
            Assert.IsNull(board[0]);
            Assert.AreSame(unit, board[8]);
            Assert.AreEqual(2, board.OccupiedCount);
        }

        [Test]
        public void 자리_교환은_서로_다른_칸의_두_유닛끼리만_된다()
        {
            var board = new GridBoard();
            var archerUnit = Archer();
            var knightUnit = Knight();
            board.TryPlace(0, archerUnit);
            board.TryPlace(1, knightUnit);

            Assert.IsFalse(board.TrySwapSlots(0, 0), "같은 슬롯");
            Assert.IsFalse(board.TrySwapSlots(0, 2), "상대 칸이 비어 있다");
            Assert.IsFalse(board.TrySwapSlots(2, 0), "출발 칸이 비어 있다");
            Assert.AreSame(archerUnit, board[0]);

            Assert.IsTrue(board.TrySwapSlots(0, 1));
            Assert.AreSame(knightUnit, board[0]);
            Assert.AreSame(archerUnit, board[1]);
        }
    }
}

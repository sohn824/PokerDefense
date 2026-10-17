using System;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * HandUnitTable
     *
     * 족보마다 소환할 유닛을 정한 표 (에셋은 Data/HandUnitTable.asset 1개)
     *
     * 쓰는 곳
     * PlacementController - 족보가 확정되면 소환할 유닛을 찾음
     * RoundScreen·AssistScreen - 손패를 맞추는 동안 소환될 유닛을 미리 보여줌
     * HandCheatWindow(에디터) - 치트 창에 족보별로 소환될 유닛을 보여줌
     *
     * 족보와 유닛을 코드가 아니라 표로 연결해, 코드 수정 없이 밸런스를 바꿀 수 있음
     * 예) 백스트레이트는 정통 포커에서는 가장 약한 스트레이트지만 스트레이트와 다른 전용 유닛을 줌
     */
    [CreateAssetMenu(menuName = "PokerDefense/Hand Unit Table", fileName = "HandUnitTable")]
    public sealed class HandUnitTable : ScriptableObject
    {
        // 족보 1개와 그 족보로 소환할 유닛
        // category는 에셋에 이름이 아니라 정수 값으로 저장됨 (예: category: 4 = Straight)
        // HandCategory 중간에 새 족보를 끼워 넣으면 뒤쪽 값이 한 칸씩 밀려 엉뚱한 유닛과 연결됨
        // 그러므로 새 족보는 HandCategory 맨 뒤에 추가해야 함
        [Serializable]
        public struct Entry
        {
            public HandCategory category;
            public UnitDefinition unit;
        }

        // 족보별 연결 (족보마다 1줄)
        [SerializeField] Entry[] entries;

        // category에 연결된 유닛 반환 (표를 위에서부터 훑어 처음 맞는 줄을 씀)
        // unit이 비어 있는 줄은 건너뛰고, 맞는 줄이 하나도 없으면 예외
        public UnitDefinition GetDefinition(HandCategory category)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].category == category && entries[i].unit != null)
                {
                    return entries[i].unit;
                }
            }

            throw new InvalidOperationException($"{name}에 {category} 연결이 없음");
        }
    }
}

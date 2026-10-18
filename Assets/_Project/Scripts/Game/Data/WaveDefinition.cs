using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * WaveDefinition
     *
     * 웨이브 하나의 스폰 구성·제한시간·보상을 담는 ScriptableObject (Data/Waves에 50개)
     * 엔트리마다 "어떤 적을, 언제부터, 몇 마리를, 몇 초 간격으로" 내보낼지 적음
     */
    [CreateAssetMenu(menuName = "PokerDefense/Wave Definition", fileName = "Wave_")]
    public sealed class WaveDefinition : ScriptableObject
    {
        // enemy를 startDelay초부터 interval초 간격으로 count마리 내보내는 엔트리
        // enemy가 비어 있으면 이 엔트리로는 적이 나오지 않음
        [Serializable]
        public struct SpawnEntry
        {
            // 내보낼 적 종류
            public EnemyDefinition enemy;

            // 내보낼 마리 수
            public int count;

            [Tooltip("이 엔트리에서 한 마리씩 나오는 간격(초)\n0이면 모두 같은 시각에 나옴")]
            public float interval;

            [Tooltip("웨이브 시작 후 이 엔트리의 첫 마리가 나오는 시각(초)")]
            public float startDelay;
        }

        // 화면에 보이는 웨이브 번호
        [SerializeField] int waveNumber = 1;

        // 제한시간(초)
        [SerializeField] float timeLimit = 30f;

        [Tooltip("이 웨이브를 클리어하면 받는 승급권(Joker) 수")]
        [SerializeField] int jokerReward;

        // 이 웨이브의 SpawnEntry 목록
        // 배열 순서는 상관없음, CombatContext가 적이 나오는 시각순으로 다시 정렬
        [SerializeField] SpawnEntry[] entries;

        public int WaveNumber => waveNumber;
        public float TimeLimit => timeLimit;
        public int JokerReward => jokerReward;
        public IReadOnlyList<SpawnEntry> Entries => entries;
    }
}

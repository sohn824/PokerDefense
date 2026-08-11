using System;
using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * WaveDefinition
     *
     * 웨이브 하나의 스폰 구성과 제한시간
     * 엔트리마다 "언제부터, 몇 마리를, 몇 초 간격으로" 내보낼지 적는다
     */
    [CreateAssetMenu(menuName = "PokerDefense/Wave Definition", fileName = "Wave_")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [Serializable]
        public struct SpawnEntry
        {
            public EnemyDefinition enemy;
            public int count;

            [Tooltip("Entry에서 한 마리씩 나오는 간격(초)")]
            public float interval;

            [Tooltip("웨이브 시작 후 이 Entry가 시작되는 시각(초)")]
            public float startDelay;
        }

        [SerializeField] int waveNumber = 1;

        [SerializeField] float timeLimit = 30f;

        [Tooltip("이 웨이브를 클리어하면 받는 Joker 수 (보스 웨이브 전용)")]
        [SerializeField] int jokerReward;

        [Tooltip("이 웨이브를 클리어하면 딜러 특전을 고른다 (보스 웨이브 전용)")]
        [SerializeField] bool perkReward;

        [SerializeField] SpawnEntry[] entries;

        public int WaveNumber => waveNumber;
        public float TimeLimit => timeLimit;
        public int JokerReward => jokerReward;
        public bool PerkReward => perkReward;
        public IReadOnlyList<SpawnEntry> Entries => entries;

        public int TotalEnemyCount
        {
            get
            {
                int total = 0;

                for (int i = 0; i < entries.Length; i++)
                {
                    total += entries[i].count;
                }

                return total;
            }
        }
    }
}

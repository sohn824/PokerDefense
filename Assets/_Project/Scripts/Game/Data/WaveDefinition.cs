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

            [Tooltip("이 엔트리 안에서 한 마리씩 나오는 간격(초)")]
            public float interval;

            [Tooltip("웨이브 시작 후 이 엔트리가 시작되는 시각(초)")]
            public float startDelay;
        }

        [SerializeField] int waveNumber = 1;

        [Tooltip("이 시간이 지나면 남은 적 수만큼 라이프가 깎인다")]
        [SerializeField] float timeLimit = 30f;

        [SerializeField] SpawnEntry[] entries;

        public int WaveNumber => waveNumber;
        public float TimeLimit => timeLimit;
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

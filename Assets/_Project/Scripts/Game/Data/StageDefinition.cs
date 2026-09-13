using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * StageDefinition
     *
     * 웨이브 진행 순서와 시작 라이프
     */
    [CreateAssetMenu(menuName = "PokerDefense/Stage Definition", fileName = "Stage_")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] int startingLife = 15;

        [Tooltip("한 웨이브가 깎을 수 있는 최대 라이프. 상한이 없으면 Swarm 웨이브 한 번에 게임이 끝난다")]
        [SerializeField] int maxLifeDamagePerWave = 5;

        [SerializeField] WaveDefinition[] waves;

        public int StartingLife => startingLife;
        public int MaxLifeDamagePerWave => maxLifeDamagePerWave;
        public IReadOnlyList<WaveDefinition> Waves => waves;
    }
}

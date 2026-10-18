using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * StageDefinition
     *
     * 한 Run의 진행 설정을 담는 ScriptableObject (Data/Stage_1.asset)
     * 담는 값 - 시작 라이프, 웨이브 순서, 웨이브당 라이프 피해 상한
     */
    [CreateAssetMenu(menuName = "PokerDefense/Stage Definition", fileName = "Stage_")]
    public sealed class StageDefinition : ScriptableObject
    {
        // Run을 시작할 때의 라이프
        [SerializeField] int startingLife = 15;

        [Tooltip("한 웨이브가 깎을 수 있는 최대 라이프")]
        [SerializeField] int maxLifeDamagePerWave = 5;

        // 진행할 웨이브 목록
        // 배열 순서가 곧 진행 순서이고, StageContext가 WaveIndex 번째 웨이브를 꺼내 씀
        [SerializeField] WaveDefinition[] waves;

        public int StartingLife => startingLife;
        public int MaxLifeDamagePerWave => maxLifeDamagePerWave;
        public IReadOnlyList<WaveDefinition> Waves => waves;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PokerDefense.Game
{
    /**
     * StageDefinition
     *
     * 한 Run의 진행 설정을 담는 ScriptableObject (Data/Stage_1.asset)
     * 담는 값 - 시작 라이프, 웨이브 순서, 라이프 위험 기준
     */
    [CreateAssetMenu(menuName = "PokerDefense/Stage Definition", fileName = "Stage_")]
    public sealed class StageDefinition : ScriptableObject
    {
        // Run을 시작할 때의 라이프
        [SerializeField] int startingLife = 15;

        [Tooltip("라이프가 이 값 이하면 위험으로 보고 화면 가장자리를 붉게 물들임")]
        [FormerlySerializedAs("maxLifeDamagePerWave")]
        [SerializeField] int dangerLife = 3;

        // 진행할 웨이브 목록
        // 배열 순서가 곧 진행 순서이고, StageContext가 WaveIndex 번째 웨이브를 꺼내 씀
        [SerializeField] WaveDefinition[] waves;

        public int StartingLife => startingLife;
        public int DangerLife => dangerLife;
        public IReadOnlyList<WaveDefinition> Waves => waves;
    }
}

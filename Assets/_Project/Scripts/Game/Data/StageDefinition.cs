using System.Collections.Generic;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * StageDefinition
     *
     * 웨이브 진행 순서와 시작 라이프
     * 시작 골드는 §7 열린 이슈 1·3번이 정해지면 그때 추가한다
     */
    [CreateAssetMenu(menuName = "PokerDefense/Stage Definition", fileName = "Stage_")]
    public sealed class StageDefinition : ScriptableObject
    {
        [SerializeField] int startingLife = 20;
        [SerializeField] WaveDefinition[] waves;

        public int StartingLife => startingLife;
        public IReadOnlyList<WaveDefinition> Waves => waves;
    }
}

using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * StageController
     *
     * StageContext와 RunStats를 씬에서 들고 있음
     * 승급권 사용과 전투 결과 반영은 여기를 거치고, 상태가 바뀌면 Changed 이벤트를 발생시켜 구독자에게 알림
     */
    public sealed class StageController : MonoBehaviour
    {
        [SerializeField] StageDefinition definition;

        // 라이프·승급권이 바뀌면 알림 (BoardScreen이 구독)
        public event Action Changed;

        StageContext stage;

        /**
         * 하나의 게임 Run의 StageContext (외부 컴포넌트에서 처음 접근할 때 생성)
         *
         * 다른 컴포넌트가 자기 Awake에서 Stage를 읽을 수 있는데,
         * 컴포넌트 간 Awake 호출 순서는 보장되지 않음
         * 그래서 StageController.Awake에서 미리 만들지 않고 처음 접근하는 쪽에서 만들게 함
         */
        public StageContext Stage => stage ?? (stage = new StageContext(definition));

        // Run 결과 기록 (최종 결과 화면·결과 전송이 읽음)
        public RunStats Stats { get; } = new RunStats();

        // 승급권(Joker) 하나 사용 (남은 게 없으면 false)
        public bool TryUseJoker()
        {
            if (Stage.TryUseJoker() == false)
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        // 이어하기 - 라이프·승급권·웨이브와 결과 기록을 되돌림 (호출부는 RunSaver)
        // 값이 범위 밖이면 아무것도 바꾸지 않고 예외
        public void Restore(int life, int jokers, int waveIndex, int summons, HandCategory? bestHand, UnitInstance bestUnit)
        {
            if (summons < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(summons), $"소환 수는 0 이상이어야 함: {summons}");
            }

            Stage.Restore(life, jokers, waveIndex);
            Stats.Restore(summons, bestHand, bestUnit);
            Changed?.Invoke();
        }

        public void ApplyCombatResult(CombatOutcome outcome, int unresolvedEnemies, int unresolvedBosses)
        {
            Stage.ApplyResult(outcome, unresolvedEnemies, unresolvedBosses);
            Changed?.Invoke();
        }
    }
}

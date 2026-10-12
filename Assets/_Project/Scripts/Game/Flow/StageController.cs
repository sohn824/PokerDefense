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

        public void ApplyCombatResult(CombatOutcome outcome, int unresolvedEnemies, int unresolvedBosses)
        {
            Stage.ApplyResult(outcome, unresolvedEnemies, unresolvedBosses);
            Changed?.Invoke();
        }
    }
}

using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * RoundContext를 씬에서 구동하는 클래스
     * UI는 이 컴포넌트의 이벤트를 구독하고 입력만 되돌려줌
     * Game은 UI를 모른다
     */
    public sealed class RoundController : MonoBehaviour
    {
        readonly System.Random seedSource = new System.Random();

        public enum AssistMode { Disabled, ChooseThree, RandomOne }

        [SerializeField] AssistMode assistMode = AssistMode.ChooseThree;
        RoundContext round;
        HandOddsWorker odds;
        int oddsVersion;

        public bool IsAssistOddsActive { get; private set; }
        public HandOddsState OddsState => odds?.State ?? HandOddsState.Idle;
        public IReadOnlyList<HandOddsEntry> Odds => odds?.Result;
        public event Action OddsChanged;

        public int LastSeed { get; private set; }

        public AssistMode Assistance => assistMode;
        public bool IsChoosingCandidate => round != null && round.IsChoosingCandidate;
        public bool AssistUsed => round != null && round.AssistUsed;
        public IReadOnlyList<Card> Candidates => round.Candidates;
        public IReadOnlyList<Card> Hand => round.Hand;
        public bool CanAssist(int index) => assistMode != AssistMode.Disabled && round != null && round.CanAssist(index);
        public event Action AssistanceChanged;

        public bool RevealCandidates(int index)
        {
            if (GameSession.IsPaused || assistMode == AssistMode.Disabled || round == null)
            {
                return false;
            }
            if (round.TryRevealCandidates(index, assistMode == AssistMode.RandomOne ? 1 : 3) == false)
            {
                return false;
            }
            InvalidateOdds();
            AssistanceChanged?.Invoke();
            return true;
        }

        public HandResult PreviewCandidate(int index) => round.PreviewCandidate(index);

        public IReadOnlyList<HandGoal> FindGoals() => round.FindGoals(assistMode != AssistMode.Disabled);

        // UI는 이제 CaptureOdds + HandOddsWorker(비동기)를 쓴다. 이 동기 메서드는 테스트의 기준값 계산에만 남겨둔다.
        public IReadOnlyList<HandOddsEntry> FindExchangeOdds(IReadOnlyList<int> indices) => round?.FindExchangeOdds(indices);

        public void ChooseCandidate(int index)
        {
            if (GameSession.IsPaused)
            {
                return;
            }
            round.ChooseCandidate(index);
            InvalidateOdds();
            HandChanged?.Invoke(round.Hand);
            AssistanceChanged?.Invoke();
        }

        public event Action<IReadOnlyList<Card>> HandChanged;
        public event Action<RoundPhase> PhaseChanged;
        public event Action<HandResult> Evaluated;

        public bool IsLocked(int index) => round.IsLocked(index);

        public int ExchangeableCount => round.ExchangeableCount;

        public int UsedExchanges => round == null ? 0 : round.UsedExchanges;

        // 라운드가 아직 안 열렸으면 Draw Phase로 본다
        public RoundPhase Phase => round == null ? RoundPhase.Draw : round.Phase;

        // 지금 확정하면 어떤 족보가 되는지 표시용
        public HandResult PreviewHand() => HandEvaluator.Evaluate(round.Hand);

        void OnEnable()
        {
            if (odds == null)
            {
                odds = new HandOddsWorker();
            }
        }

        // 재사용을 위해 Worker는 살려두고 진행 중인 계산만 취소한다
        void OnDisable()
        {
            odds?.Cancel();
            IsAssistOddsActive = false;
        }

        void OnDestroy() => odds?.Dispose();

        // 매 프레임 완료 여부만 확인 - 계산 자체는 Worker 스레드에서 돈다
        void Update()
        {
            if (odds != null && odds.Poll())
            {
                if (odds.Error != null)
                {
                    Debug.LogException(odds.Error, this);
                }

                OddsChanged?.Invoke();
            }
        }

        public void SetAssistOddsActive(bool active)
        {
            if (IsAssistOddsActive == active)
            {
                return;
            }

            IsAssistOddsActive = active;
            odds?.Cancel();
            OddsChanged?.Invoke();
        }

        // 일반 교체·선택 교체는 Worker 하나를 공유한다. 지금 활성 모드와 다른 출처의 요청은 무시
        public void RequestOdds(HandOddsSource source, IReadOnlyList<int> indices)
        {
            if (odds == null || (source == HandOddsSource.Assist) != IsAssistOddsActive)
            {
                return;
            }

            if (source == HandOddsSource.Assist && assistMode == AssistMode.Disabled)
            {
                odds.Cancel();
                return;
            }

            odds.Submit(round?.CaptureOdds(oddsVersion, source, indices));
        }

        // Worker가 지금 그 출처의 요청을 들고 있을 때만 취소 - 다른 출처의 진행 중인 계산은 건드리지 않는다
        public void CancelOdds(HandOddsSource source)
        {
            if (odds?.Source == source)
            {
                odds.Cancel();
            }
        }

        void InvalidateOdds()
        {
            oddsVersion++;
            odds?.Cancel();
        }

        // 새 라운드가 시작될 때 호출 (호출부는 GameFlowController)
        // 덱을 새로 셔플하고 5장 뽑는다
        public void StartRound()
        {
            InvalidateOdds();
            IsAssistOddsActive = false;
            LastSeed = seedSource.Next();
            round = new RoundContext(LastSeed);
            round.Draw();

            PhaseChanged?.Invoke(round.Phase);
            HandChanged?.Invoke(round.Hand);
        }

        // 인벤토리에서 빼는 것은 호출부(StageController) 책임

        // 고른 자리의 카드 교체
        public void ExchangeCards(IReadOnlyList<int> indices)
        {
            round.Exchange(indices);
            InvalidateOdds();

            HandChanged?.Invoke(round.Hand);
        }

        // 교체를 끝내고 족보 확정
        public void ConfirmHand()
        {
            if (GameSession.IsPaused || IsChoosingCandidate)
            {
                return;
            }
            round.FinishExchange();
            InvalidateOdds();

            HandResult result = round.Evaluate();
            PhaseChanged?.Invoke(round.Phase);
            Evaluated?.Invoke(result);
        }

#if UNITY_EDITOR
        public bool CanForceHand => round != null && round.Phase == RoundPhase.Exchange;

        // 개발 전용
        // 원하는 족보의 5장으로 손패 강제 교체
        public void DevForceHand(IReadOnlyList<Card> cards)
        {
            round.ForceHand(cards);
            InvalidateOdds();

            HandChanged?.Invoke(round.Hand);
        }
#endif //UNITY_EDITOR
    }
}

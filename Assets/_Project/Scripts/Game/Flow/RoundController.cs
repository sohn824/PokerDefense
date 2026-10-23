using System;
using System.Collections.Generic;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * RoundController
     *
     * RoundContext를 씬에서 구동하는 MonoBehaviour
     * 라운드마다 새 시드로 RoundContext를 만들고, 손패·단계·확률이 바뀌면 이벤트로 알림
     * 화면(RoundScreen·AssistScreen·HandOddsScreen)은 이 클래스의 이벤트를 받아 갱신되고,
     * 버튼 입력은 이 클래스의 메서드를 불러 전달함
     * 반대로 이 클래스는 UI 쪽을 참조하지 않음 (UI를 바꿔도 이 클래스는 고칠 필요 없음)
     */
    public sealed class RoundController : MonoBehaviour
    {
        // 라운드마다 덱 시드를 뽑는 난수
        readonly System.Random seedSource = new System.Random();

        // 이번 라운드의 손패·교체 상태 (첫 StartRound 전에는 null)
        RoundContext round;
        // 일반 교체·선택 교체가 같이 쓰는 확률 계산 Worker
        HandOddsWorker oddsWorker;
        // 손패 버전 (손패나 덱이 바뀔 때마다 InvalidateOdds에서 1 증가, 확률 요청이 어느 손패 기준인지 구분용)
        int handVersion;

        // 손패가 바뀌었을 때 발생하는 이벤트 (새 라운드·일반 교체·후보 선택·강제 손패)
        public event Action<IReadOnlyList<Card>> HandChanged;
        // 새 라운드(Exchange)·손패 확정(Place) 때 발생하는 이벤트
        public event Action<RoundPhase> PhaseChanged;
        // 족보 확정 때 발생하는 이벤트 (GameFlowController·PlacementController·RunJournal·RoundScreen이 구독)
        public event Action<HandResult> Evaluated;
        // 선택 교체 후보를 공개했거나 골랐을 때 발생하는 이벤트
        public event Action AssistanceChanged;
        // 확률 결과(또는 실패)가 나왔거나, 선택 교체 창이 열리고 닫혀 받을 확률 쪽이 바뀌었을 때 발생하는 이벤트
        public event Action OddsChanged;

        // 선택 교체 창이 열려 있는지 (true면 선택 교체 확률만, false면 일반 교체 확률만 계산)
        public bool IsAssistOddsActive { get; private set; }
        // 지금 확률 계산 상태 (Worker가 아직 없으면 Idle)
        public HandOddsState OddsState => oddsWorker?.State ?? HandOddsState.Idle;
        // 확률 계산 결과 (OddsState가 Ready일 때만 값이 있음)
        public IReadOnlyList<HandOddsEntry> Odds => oddsWorker?.Result;

        // 이번 라운드 덱 셔플 시드 (RunJournal 기록용)
        public int LastSeed { get; private set; }

        // 후보를 공개하고 아직 고르지 않은 상태인지
        public bool IsChoosingCandidate => round != null && round.IsChoosingCandidate;
        // 이번 손패의 선택 교체 기회를 썼는지 (후보를 공개하는 순간 쓴 것으로 취급)
        public bool AssistUsed => round != null && round.AssistUsed;
        public IReadOnlyList<Card> Candidates => round.Candidates;
        public IReadOnlyList<Card> Hand => round.Hand;

        // 이 자리를 선택 교체할 수 있는지 (조건은 RoundContext.CanAssist)
        public bool CanAssist(int index)
            => round != null && round.CanAssist(index);

        // 선택 교체 후보 3장 공개 (공개하면 true)
        // 일시정지 중·선택 교체할 수 없는 자리면 아무것도 하지 않고 false
        public bool RevealCandidates(int index)
        {
            if (GameSession.IsPaused || round == null)
            {
                return false;
            }
            if (round.TryRevealCandidates(index) == false)
            {
                return false;
            }
            InvalidateOdds();
            AssistanceChanged?.Invoke();
            return true;
        }

        // index번 후보로 바꿨을 때의 족보 (손패는 그대로, AssistScreen 미리보기용)
        public HandResult PreviewCandidate(int index) => round.PreviewCandidate(index);

        // 고른 자리들을 일반 교체했을 때 족보별 경우의 수를 지금 스레드에서 바로 계산함 (라운드 시작 전 등 계산할 수 없으면 null)
        // 화면이 멈출 수 있어 게임에서는 RequestOdds(Worker 스레드)를 쓰고, 이 메서드는 HandOddsThreadingTests의 기준값으로만 씀
        public IReadOnlyList<HandOddsEntry> FindExchangeOdds(IReadOnlyList<int> indices)
            => round?.FindExchangeOdds(indices);

        // 공개한 후보 중 index번으로 교체 (일시정지 중이면 무시)
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

        // index 자리가 일반 교체로 이미 바뀌어 잠겼는지
        public bool IsLocked(int index) => round.IsLocked(index);

        // 아직 일반 교체할 수 있는(잠기지 않은) 자리 수
        public int ExchangeableCount => round.ExchangeableCount;

        // 이번 라운드에 교체한 총 장수 (일반 교체 + 선택 교체, RunJournal 기록용)
        public int UsedExchanges => round == null ? 0 : round.UsedExchanges;

        // 라운드가 아직 안 열렸으면 Draw Phase로 봄
        public RoundPhase Phase => round == null ? RoundPhase.Draw : round.Phase;

        // 지금 확정하면 어떤 족보가 되는지 표시용
        public HandResult PreviewHand() => HandEvaluator.Evaluate(round.Hand);

        void OnEnable()
        {
            if (oddsWorker == null)
            {
                oddsWorker = new HandOddsWorker();
            }
        }

        // 비활성화되면 진행 중인 계산과 결과를 버리고 선택 교체 창 상태를 초기화함 (Worker는 다시 켜질 때 재사용하도록 남겨 둠)
        void OnDisable()
        {
            oddsWorker?.Cancel();
            IsAssistOddsActive = false;
        }

        void OnDestroy() => oddsWorker?.Dispose();

        // 매 프레임 Worker 스레드 계산이 끝났는지 확인하고, 결과나 실패가 나오면 OddsChanged 발생 (실패면 예외를 콘솔에 남김)
        void Update()
        {
            if (oddsWorker != null && oddsWorker.Poll())
            {
                if (oddsWorker.Error != null)
                {
                    Debug.LogException(oddsWorker.Error, this);
                }

                OddsChanged?.Invoke();
            }
        }

        // 선택 교체 창이 열리고 닫힐 때 AssistScreen이 호출
        // 받을 확률 쪽(IsAssistOddsActive)이 실제로 바뀔 때만 이전 쪽 계산과 결과를 버리고 OddsChanged로 다시 요청하게 함
        public void SetAssistOddsActive(bool active)
        {
            if (IsAssistOddsActive == active)
            {
                return;
            }

            IsAssistOddsActive = active;
            oddsWorker?.Cancel();
            OddsChanged?.Invoke();
        }

        // 일반 교체·선택 교체는 Worker 하나를 공유하므로 지금 활성 쪽(IsAssistOddsActive)과 다른 source의 요청은 무시
        // CaptureOdds가 null(지금 계산할 수 없는 조건)이면 Submit이 계산을 취소함
        public void RequestOdds(HandOddsSource source, IReadOnlyList<int> indices)
        {
            if (oddsWorker == null || (source == HandOddsSource.Assist) != IsAssistOddsActive)
            {
                return;
            }

            oddsWorker.Submit(round?.CaptureOdds(handVersion, source, indices));
        }

        // Worker가 지금 source의 요청을 들고 있을 때만 취소 - 다른 source의 진행 중인 계산은 건드리지 않음
        public void CancelOdds(HandOddsSource source)
        {
            if (oddsWorker?.Source == source)
            {
                oddsWorker.Cancel();
            }
        }

        // 손패나 덱이 바뀌었을 때 호출 - 손패 버전을 올리고 진행 중인 계산과 이전 결과를 버림
        void InvalidateOdds()
        {
            handVersion++;
            oddsWorker?.Cancel();
        }

        // 새 라운드가 시작될 때 호출 (호출부는 GameFlowController)
        // 새 시드로 덱을 셔플하고 5장 뽑아 바로 Exchange 단계로 넘김
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

        // 고른 자리 일반 교체 (교체한 자리는 잠김, 일시정지 중이면 무시)
        public void ExchangeCards(IReadOnlyList<int> indices)
        {
            if (GameSession.IsPaused)
            {
                return;
            }
            round.Exchange(indices);
            InvalidateOdds();

            HandChanged?.Invoke(round.Hand);
        }

        // 교체를 끝내고 족보 확정 (FinishExchange·Evaluate를 한 번에 실행 -> Place 단계)
        // 일시정지 중이거나 선택 교체 후보를 고르는 중이면 무시
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
        // 지금 강제 손패를 쓸 수 있는지 (Exchange 단계에서만, HandCheatWindow 버튼 표시용)
        public bool CanForceHand => round != null && round.Phase == RoundPhase.Exchange;

        // 개발 전용 - 손패를 넘겨받은 5장으로 강제로 바꿈 (HandCheatWindow가 족보별 예시 손패를 넘김)
        public void DevForceHand(IReadOnlyList<Card> cards)
        {
            round.ForceHand(cards);
            InvalidateOdds();

            HandChanged?.Invoke(round.Hand);
        }
#endif // UNITY_EDITOR
    }
}

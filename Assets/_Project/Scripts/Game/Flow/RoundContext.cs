using System;
using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * RoundContext
     *
     * 한 라운드의 손패를 소유하고, 5장을 뽑고 교체해 족보를 확정하기까지의 순서를 관리하는 객체
     * 라운드마다 RoundController가 새로 만듦
     *
     * 일반 교체 - 고른 자리를 덱의 다음 카드로 바꾸고 그 자리를 잠금
     * 여러 번 나눠 바꿀 수 있지만 한 자리는 라운드당 한 번만 바꿀 수 있음
     *   
     * 선택 교체(Assist) - 일반 교체로 바꾼 카드 한 장을 한 번 더 바꿀 기회 (라운드당 1회)
     * 1. 일반 교체했던 자리 하나를 고름
     * 2. 덱에서 후보 3장을 뽑아 보여 줌
     * 3. 후보 중 한 장을 골라 그 자리에 넣음
     * 2번에서 후보를 보여 주는 순간 기회를 쓴 것이므로, 3번을 마치기 전에는 일반 교체·손패 확정을 할 수 없음
     * 고르지 않은 후보는 버려지며 이후 교체에서 다시 나오지 않음
     *
     * CaptureOdds() - 교체 확률 계산에 넘길 값을 복사해 HandOddsRequest로 만듦
     * 손패 - 지금 손패 5장
     * 남은 덱 - 아직 뽑히지 않은 카드 (교체하면 이 중에서 나옴)
     * 바꿀 자리 - 일반 교체는 1~4자리, 선택 교체는 1자리
     * 세 값 모두 복사본이라 계산 중에 손패나 덱이 바뀌어도 계산에 영향 없음
     *
     * 예) 손패 A·B·C·D·E
     * 일반 교체로 A·D를 바꿈 -> A'·B·C·D'·E (A'·D' 자리는 잠김)
     * 선택 교체로 D' 자리를 고름 -> 후보 X·Y·Z 공개 -> Y 선택 -> A'·B·C·Y·E
     * 손패 확정 -> 족보 판정
     *
     * Actions - 이번 라운드에서 성공한 행동을 순서대로 기록함 (실패한 행동은 남기지 않음)
     * Replay() - 같은 시드로 새로 만든 뒤 기록을 재현해 손패·덱·후보가 같은 라운드를 만듦
     * 덱은 시드로 한 번 섞은 뒤 순서대로 뽑기만 하므로 시드와 행동만 같으면 결과도 같음
     * 이어하기에서 덱 순서를 따로 저장하지 않고 이 방식으로 복원함
     */
    public sealed class RoundContext
    {
        public const int HandSize = 5;
        public const int CandidateCount = 3; // 선택 교체로 공개하는 후보 수

        readonly Deck deck; // 이번 라운드의 셔플된 52장 덱 (손패·일반 교체·선택 교체 후보 모두 여기서 뽑음)
        readonly Card[] hand = new Card[HandSize]; // 5장 손패
        readonly bool[] locked = new bool[HandSize]; // 일반 교체해서 잠긴 자리인지 여부

        // 교체한 총 장수 (일반 교체 장수 + 선택 교체 1장) - 선택 교체는 잠금을 늘리지 않으므로 locked와 따로 셈
        int exchangedCount;
        // 일반 교체한 자리인지 (선택 교체는 이 자리만 가능)
        readonly bool[] normallyExchanged = new bool[HandSize];
        // 선택 교체로 공개한 후보 (공개 전·선택 후에는 빈 배열)
        Card[] candidates = Array.Empty<Card>();
        // 선택 교체로 바꿀 자리 (후보를 고르는 중이 아니면 -1)
        int assistTarget = -1;

        // 이번 라운드에서 성공한 행동 기록
        readonly List<RoundAction> actions = new List<RoundAction>();

        // 이번 손패의 선택 교체 기회를 썼는지 (후보를 공개하는 순간 쓴 것으로 취급)
        public bool AssistUsed { get; private set; }

        // 덱을 섞은 시드
        public int Seed { get; }

        public IReadOnlyList<RoundAction> Actions => actions;

        // 시드와 행동 기록만으로 다시 만들 수 있는지
        // ForceHand(치트)는 덱에서 뽑지 않고 손패를 바꾸므로 쓰면 false가 됨
        public bool IsReplayable { get; private set; } = true;

        // seed로 새 RoundContext를 만들고 actions를 순서대로 재현함
        // 기록이 이 시드의 라운드와 맞지 않으면(공개할 수 없는 자리 등) 예외
        public static RoundContext Replay(int seed, IReadOnlyList<RoundAction> actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            RoundContext round = new RoundContext(seed);

            foreach (RoundAction action in actions)
            {
                switch (action.Type)
                {
                    case RoundActionType.Draw: round.Draw(); break;
                    case RoundActionType.Exchange: round.Exchange(action.Indices); break;
                    case RoundActionType.ChooseCandidate: round.ChooseCandidate(action.Index); break;
                    case RoundActionType.FinishExchange: round.FinishExchange(); break;
                    case RoundActionType.Evaluate: round.Evaluate(); break;
                    case RoundActionType.RevealCandidates:
                        if (round.TryRevealCandidates(action.Index) == false)
                        {
                            throw new InvalidOperationException($"기록과 맞지 않아 후보를 공개할 수 없음: {action.Index}");
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(actions), $"알 수 없는 행동: {action.Type}");
                }
            }

            return round;
        }

        // 고른 자리들을 한꺼번에 일반 교체했을 때 족보별 경우의 수를 계산해 바로 돌려줌 (테스트용)
        // 호출한 스레드에서 끝날 때까지 계산하므로 게임에서는 쓰지 않음 (4자리면 178,365가지라 화면이 멈춤)
        // 실제 게임에서는 CaptureOdds -> HandOddsWorker로 Worker 스레드에서 계산함
        // HandOddsThreadingTests에서 Worker 스레드 계산 결과가 맞는지 비교하는 기준값으로만 사용
        // Exchange 단계가 아니거나 잠긴 자리가 섞여 있으면 null
        public List<HandOddsEntry> FindExchangeOdds(IReadOnlyList<int> indices)
        {
            if (Phase != RoundPhase.Exchange || indices == null)
            {
                return null;
            }

            for (int i = 0; i < indices.Count; i++)
            {
                if (locked[indices[i]])
                {
                    return null;
                }
            }

            return HandOdds.Find(hand, BuildUnseen(), indices);
        }

        // 교체 확률 계산 요청(HandOddsRequest)을 만듦 - 손패·남은 덱·고른 자리를 복사해 담음
        // version - 손패 버전 (손패나 덱이 바뀔 때마다 RoundController가 1씩 올림)
        // source - 요청한 쪽 (Exchange = 일반 교체, Assist = 선택 교체)
        //
        // 아래 경우에는 계산할 수 없으므로 null (HandOddsWorker가 null을 받으면 진행 중인 계산을 취소함)
        // Exchange 단계가 아니거나 선택 교체 후보를 고르는 중
        // 자리가 0개거나 HandOdds.MaxSlots(4)보다 많음, 범위 밖 자리, 같은 자리 중복
        // 일반 교체인데 잠긴 자리가 섞임
        // 선택 교체인데 자리가 1개가 아니거나 선택 교체할 수 없는 자리
        //
        // 위 조건 검사와 손패·남은 덱·고른 자리 복사를 모두 메인 스레드에서 끝냄
        // (Worker 스레드가 원본 hand·덱을 읽는 도중 메인 스레드가 카드를 교체하면 Worker가 읽는 값이 오염되므로)
        public HandOddsRequest CaptureOdds(int version, HandOddsSource source, IReadOnlyList<int> indices)
        {
            if (Phase != RoundPhase.Exchange || IsChoosingCandidate || indices == null
                || indices.Count == 0 || indices.Count > HandOdds.MaxSlots) return null;
            if (source == HandOddsSource.Assist && (indices.Count != 1 || CanAssist(indices[0]) == false))
            {
                return null;
            }

            // 이미 확인한 자리 비트마스크 (n번 비트 = n번 자리) - 같은 자리가 두 번 들어왔는지 확인용
            int seen = 0;
            foreach (int index in indices)
            {
                if (index < 0 || index >= HandSize || (seen & (1 << index)) != 0)
                {
                    return null;
                }

                if (source == HandOddsSource.Exchange && locked[index])
                {
                    return null;
                }

                seen |= 1 << index;
            }

            return new HandOddsRequest(version, source, hand, BuildUnseen(), indices);
        }

        // 교체하면 나올 수 있는, 아직 덱에서 뽑히지 않은 카드 (Unseen = 플레이어가 아직 못 본 카드)
        // 손패 제외 조건은 평소에는 첫 조건에서 이미 걸러지고, ForceHand(치트) 뒤에만 의미 있음
        List<Card> BuildUnseen()
        {
            return Deck.BuildCards().FindAll(card =>
                deck.ContainsRemaining(card)        // 이미 뽑은 카드 제외
                && Array.IndexOf(hand, card) < 0);  // ForceHand로 손패에 들어온 카드 제외 (Array.IndexOf는 없으면 -1 반환)
        }

        // 후보를 공개하고 아직 고르지 않은 상태인지
        public bool IsChoosingCandidate => assistTarget >= 0;
        public IReadOnlyList<Card> Candidates => candidates;

        // 이 자리를 선택 교체할 수 있는지
        // index 범위 확인은 normallyExchanged[index]보다 앞에 둬야 함 (범위 밖 접근 방지)
        public bool CanAssist(int index)
        {
            return Phase == RoundPhase.Exchange    // Exchange 단계
                && AssistUsed == false             // 이번 라운드 기회를 아직 안 씀
                && index >= 0 && index < HandSize  // 손패 범위 안 자리
                && normallyExchanged[index]        // 일반 교체한 자리
                && deck.Remaining > 0;             // 후보를 뽑을 카드가 덱에 남음
        }

        // index 자리에 넣을 후보 3장을 덱에서 뽑아 공개하고, 이번 라운드의 선택 교체 기회를 씀
        // 선택 교체할 수 없는 자리면 아무것도 바꾸지 않고 false
        // 공개한 뒤에는 되돌릴 수 없고, ChooseCandidate로 한 장을 고르기 전까지 일반 교체·손패 확정이 막힘
        public bool TryRevealCandidates(int index)
        {
            if (CanAssist(index) == false)
            {
                return false;
            }
            candidates = deck.DrawUpTo(CandidateCount);
            assistTarget = index;
            AssistUsed = true;
            actions.Add(RoundAction.RevealCandidates(index));
            return true;
        }

        // index번 후보로 바꿨을 때의 족보 (손패는 그대로)
        public HandResult PreviewCandidate(int index)
        {
            ValidateCandidate(index);
            Card[] preview = (Card[])hand.Clone();
            preview[assistTarget] = candidates[index];
            return HandEvaluator.Evaluate(preview);
        }

        // index번 후보로 교체하고 선택 교체를 끝냄 (그 자리는 일반 교체 때 이미 잠겨 있음)
        public void ChooseCandidate(int index)
        {
            ValidateCandidate(index);
            hand[assistTarget] = candidates[index];
            exchangedCount++;
            assistTarget = -1;
            candidates = Array.Empty<Card>();
            actions.Add(RoundAction.ChooseCandidate(index));
        }

        void ValidateCandidate(int index)
        {
            if (Phase != RoundPhase.Exchange || IsChoosingCandidate == false)
            {
                throw new InvalidOperationException("공개된 선택 교체 후보가 없습니다");
            }
            if (index < 0 || index >= candidates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public RoundContext(int seed)
        {
            Seed = seed;
            deck = new Deck(seed);
        }

        public RoundPhase Phase { get; private set; } = RoundPhase.Draw;

        public IReadOnlyList<Card> Hand => hand;

        // 손패의 족보 - Evaluate로 판정하기 전에는 의미 없는 값
        public HandResult Result { get; private set; }

        public bool IsLocked(int index) => locked[index];

        public int UsedExchanges => exchangedCount;

        // 아직 일반 교체할 수 있는(잠기지 않은) 자리 수
        public int ExchangeableCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < HandSize; i++)
                {
                    if (locked[i] == false)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        // Draw Phase -> 덱에서 카드를 드로우하고 Exchange Phase로 넘김
        public void Draw()
        {
            Require(RoundPhase.Draw);

            for (int i = 0; i < HandSize; i++)
            {
                hand[i] = deck.Draw();
            }

            Phase = RoundPhase.Exchange;
            actions.Add(RoundAction.Of(RoundActionType.Draw));
        }

        // 고른 자리를 덱의 다음 카드로 바꾸고 그 자리를 잠금
        // 범위 밖·잠긴 자리·중복 자리가 하나라도 있으면 예외를 던지고 한 장도 바꾸지 않음
        public void Exchange(IReadOnlyList<int> indices)
        {
            Require(RoundPhase.Exchange);

            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            Validate(indices);
            if (indices.Count > deck.Remaining)
            {
                throw new InvalidOperationException("교체할 카드가 덱에 부족합니다");
            }

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                hand[index] = deck.Draw();
                locked[index] = true;
                normallyExchanged[index] = true;
            }

            exchangedCount += indices.Count;
            actions.Add(RoundAction.Exchange(indices));
        }

        // Exchange Phase를 끝내고 Evaluate Phase로 넘김
        public void FinishExchange()
        {
            Require(RoundPhase.Exchange);

            Phase = RoundPhase.Evaluate;
            actions.Add(RoundAction.Of(RoundActionType.FinishExchange));
        }

        // Evaluate Phase - 족보를 판정하고 Place Phase로 넘김
        public HandResult Evaluate()
        {
            Require(RoundPhase.Evaluate);

            Result = HandEvaluator.Evaluate(hand);
            Phase = RoundPhase.Place;
            actions.Add(RoundAction.Of(RoundActionType.Evaluate));
            return Result;
        }

#if UNITY_EDITOR
        // 개발 전용 메소드 (HandCheatWindow)
        // 손패를 원하는 5장으로 갈아끼움
        // 덱에서 빼지 않으므로 이후 교체·후보에서 같은 카드가 또 나올 수 있음
        public void ForceHand(IReadOnlyList<Card> cards)
        {
            Require(RoundPhase.Exchange);

            if (cards == null)
            {
                throw new ArgumentNullException(nameof(cards));
            }

            if (cards.Count != HandSize)
            {
                throw new ArgumentException($"손패는 {HandSize}장이어야 합니다. 받은 수: {cards.Count}", nameof(cards));
            }

            for (int i = 0; i < HandSize; i++)
            {
                hand[i] = cards[i];
            }

            IsReplayable = false;
        }
#endif // UNITY_EDITOR

        // 교체할 자리 목록 검증 - 범위 밖·잠긴 자리·같은 자리 중복이면 예외
        void Validate(IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];

                if (index < 0 || index >= HandSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(indices), $"손패 범위를 벗어난 인덱스 입니다: {index}");
                }

                if (locked[index])
                {
                    throw new InvalidOperationException($"이미 교체한 자리는 이번 라운드에 다시 바꿀 수 없습니다: {index}");
                }

                for (int j = i + 1; j < indices.Count; j++)
                {
                    if (indices[j] == index)
                    {
                        throw new ArgumentException($"같은 인덱스를 두 번 교체할 수 없습니다: {index}", nameof(indices));
                    }
                }
            }
        }

        // 단계별 동작(Draw·Exchange·FinishExchange·Evaluate·ForceHand)을 지금 해도 되는지 맨 앞에서 확인
        // 선택 교체 후보를 고르는 중이거나 지금 단계가 인자로 들어온 expected가 아니면 예외
        void Require(RoundPhase expected)
        {
            if (IsChoosingCandidate)
            {
                throw new InvalidOperationException("선택 교체 후보를 먼저 선택하세요");
            }

            if (Phase != expected)
            {
                throw new InvalidOperationException($"{expected} 페이즈에서만 가능한 동작입니다. 현재: {Phase}");
            }
        }
    }
}

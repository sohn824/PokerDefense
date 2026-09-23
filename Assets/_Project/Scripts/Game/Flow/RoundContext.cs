using System;
using System.Collections.Generic;
using PokerDefense.Poker;

namespace PokerDefense.Game
{
    /**
     * RoundContext
     *
     * 라운드 하나의 흐름
     * 카드 드로우 -> 카드 교체 (카드 하나당 한번씩 가능, 교체한 자리는 잠김) -> 족보 판정 순서로 진행
     *
     * PlaceHeldCard로 손패 자리에 놓을 수 있음
     */
    public sealed class RoundContext
    {
        public const int HandSize = 5;

        readonly Deck deck; // 전체 덱
        readonly Card[] hand = new Card[HandSize]; // 5장 손패
        readonly bool[] locked = new bool[HandSize]; // 교체 or 상점 카드 배치로 잠긴 자리인지 여부

        // 실제로 교체한 장수. 자리 잠금(locked)과 분리
        int exchangedCount;
        readonly bool[] normallyExchanged = new bool[HandSize];
        Card[] candidates = Array.Empty<Card>();
        int assistTarget = -1;

        public bool AssistUsed { get; private set; }

        public List<HandGoal> FindGoals(bool includeAssist = true)
        {
            var changeable = new bool[HandSize];
            for (int i = 0; i < HandSize; i++)
                changeable[i] = Phase == RoundPhase.Exchange && (locked[i] == false || (includeAssist && CanAssist(i)));
            return HandGoals.Find(hand, BuildUnseen(), changeable);
        }

        // 실제로 일반 교체할 자리들을 한꺼번에 채웠을 때 나올 수 있는 족보의 경우의 수
        // UI는 이제 CaptureOdds + HandOddsWorker(비동기)를 쓴다. 이 동기 메서드는 테스트의 기준값 계산에만 남겨둔다.
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

        // 호출 시점의 검증과 복사는 메인에서 끝낸다. Worker는 RoundContext를 읽지 않는다.
        public HandOddsRequest CaptureOdds(int version, HandOddsSource source, IReadOnlyList<int> indices)
        {
            if (Phase != RoundPhase.Exchange || IsChoosingCandidate || indices == null
                || indices.Count == 0 || indices.Count > HandOdds.MaxSlots) return null;
            if (source == HandOddsSource.Assist && (indices.Count != 1 || CanAssist(indices[0]) == false))
            {
                return null;
            }

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

        List<Card> BuildUnseen() => Deck.BuildCards().FindAll(card => deck.ContainsRemaining(card) && Array.IndexOf(hand, card) < 0);

        public bool IsChoosingCandidate => assistTarget >= 0;
        public IReadOnlyList<Card> Candidates => candidates;
        public int AssistTarget => assistTarget;

        public bool CanAssist(int index) => Phase == RoundPhase.Exchange && AssistUsed == false
            && index >= 0 && index < HandSize && normallyExchanged[index] && deck.Remaining > 0;

        // 소비와 후보 추첨을 한 동작으로 처리한다. 공개 뒤에는 같은 후보에서 반드시 한 장을 골라야 한다.
        public bool TryRevealCandidates(int index, int count = 3)
        {
            if (CanAssist(index) == false || (count != 1 && count != 3))
            {
                return false;
            }
            candidates = deck.DrawUpTo(count);
            assistTarget = index;
            AssistUsed = true;
            return true;
        }

        public HandResult PreviewCandidate(int index)
        {
            ValidateCandidate(index);
            Card[] preview = (Card[])hand.Clone();
            preview[assistTarget] = candidates[index];
            return HandEvaluator.Evaluate(preview);
        }

        public void ChooseCandidate(int index)
        {
            ValidateCandidate(index);
            hand[assistTarget] = candidates[index];
            exchangedCount++;
            assistTarget = -1;
            candidates = Array.Empty<Card>();
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
            deck = new Deck(seed);
        }

        public RoundPhase Phase { get; private set; } = RoundPhase.Draw;

        public IReadOnlyList<Card> Hand => hand;

        // 손패 족보 - Evaluate로 판정하기 전에는 의미 없는 값
        public HandResult Result { get; private set; }

        public bool IsLocked(int index) => locked[index];

        public int UsedExchanges => exchangedCount;

        // 아직 바꿀 수 있는 손패 자리 수
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
        }

        // 고른 자리의 카드를 교체하고 그 자리는 잠금
        // 이미 잠긴 자리를 골랐으면 예외를 던지고 실패 처리
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
        }

        // Exchange Phase를 끝내고 Evaluate Phase로 넘김
        public void FinishExchange()
        {
            Require(RoundPhase.Exchange);

            Phase = RoundPhase.Evaluate;
        }

        // Evaluate Phase - 족보를 판정하고 Place Phase로 넘김
        public HandResult Evaluate()
        {
            Require(RoundPhase.Evaluate);

            Result = HandEvaluator.Evaluate(hand);
            Phase = RoundPhase.Place;
            return Result;
        }

#if UNITY_EDITOR
        // 개발 전용 메소드
        // 손패를 원하는 5장으로 갈아끼운다
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
        }
#endif // UNITY_EDITOR

        // 손패 검증 - 비정상 시도일 경우 예외를 던지고 실패 처리
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

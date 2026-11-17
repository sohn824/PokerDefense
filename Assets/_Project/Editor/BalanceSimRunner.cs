#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PokerDefense.Game;
using PokerDefense.Poker;
using UnityEditor;
using UnityEngine;

namespace PokerDefense.EditorTools
{
    /**
     * BalanceSimRunner
     *
     * 스테이지를 자동으로 끝까지 플레이해 웨이브별 라이프·성급 변화를 CSV로 남기는 클래스
     * Game 씬을 연 상태에서 StartRun으로 시작함
     * 예 - StartRun("greedy", "run1", "Artifacts/Balance/run1.csv", 10f)
     * 전략 - no_exchange : 교체하지 않음, 나머지 문자열은 모두 greedy
     * greedy - 페어·같은 무늬를 남기고 교체한 뒤 족보가 가장 높아지는 후보를 고름
     *
     * 배속은 GameSession.SetSpeed로만 올림
     * combat.Tick을 직접 부르면 CombatController와 순서가 어긋나 결과가 빠질 수 있음
     */
    [InitializeOnLoad]
    public static class BalanceSimRunner
    {
        const string ActiveKey = "PokerDefense.BalanceSim.Active";
        const string StrategyKey = "PokerDefense.BalanceSim.Strategy";
        const string RunIdKey = "PokerDefense.BalanceSim.RunId";
        const string TimeScaleKey = "PokerDefense.BalanceSim.TimeScale";
        const string LogPathKey = "PokerDefense.BalanceSim.LogPath";

        static GameFlowController flow;
        static RoundController round;
        static PlacementController placement;
        static CombatController combat;
        static StageController stageCtrl;

        static bool setupDone;
        static string strategy;
        static string runId;
        static string logPath;
        static IReadOnlyList<Card> cachedHand;
        static float startedRealtime;

        static BalanceSimRunner()
        {
            EditorApplication.update += Tick;
        }

        public static void StartRun(string strategyId, string runIdentifier, string outputCsvPath, float timeScale = 10f)
        {
            // 폴더가 없으면 헤더 쓰기가 매 프레임 실패하므로 시작 전에 만들어 둠
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsvPath)));

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(StrategyKey, strategyId);
            SessionState.SetString(RunIdKey, runIdentifier);
            SessionState.SetString(LogPathKey, outputCsvPath);
            SessionState.SetFloat(TimeScaleKey, timeScale);
            setupDone = false;
            flow = null;

            if (EditorApplication.isPlaying == false)
            {
                EditorApplication.isPlaying = true;
            }
        }

        public static void StopActiveRun()
        {
            SessionState.SetBool(ActiveKey, false);
            setupDone = false;
            flow = null;

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }

        static void Tick()
        {
            if (SessionState.GetBool(ActiveKey, false) == false)
            {
                return;
            }

            if (EditorApplication.isPlaying == false || EditorApplication.isPaused)
            {
                return;
            }

            // 씬이 아직 준비되지 않음, 다음 프레임에 재시도
            if (setupDone == false && TrySetup() == false)
            {
                return;
            }

            Drive();
        }

        static bool TrySetup()
        {
            flow = Object.FindAnyObjectByType<GameFlowController>();
            round = Object.FindAnyObjectByType<RoundController>();
            placement = Object.FindAnyObjectByType<PlacementController>();
            combat = Object.FindAnyObjectByType<CombatController>();
            stageCtrl = Object.FindAnyObjectByType<StageController>();

            if (flow == null || round == null || placement == null || combat == null || stageCtrl == null)
            {
                return false;
            }

            strategy = SessionState.GetString(StrategyKey, "no_exchange");
            runId = SessionState.GetString(RunIdKey, "run");
            logPath = SessionState.GetString(LogPathKey, "");

            Application.targetFrameRate = 250;
            QualitySettings.vSyncCount = 0;
            // Time.timeScale을 직접 바꾸면 안내창을 닫을 때 GameSession이 기존 배속으로 되돌림
            GameSession.SetSpeed(SessionState.GetFloat(TimeScaleKey, 10f));
            flow.MarkSimulated();

            cachedHand = null;
            round.HandChanged += h => cachedHand = h;
            combat.CombatFinished += (outcome, unresolved) => LogWaveRow(outcome);

            EnsureHeader();
            startedRealtime = Time.realtimeSinceStartup;
            setupDone = true;
            return true;
        }

        // 지금 상태에서 한 프레임에 플레이어가 할 일 하나를 함
        static void Drive()
        {
            if (flow.IsFinished)
            {
                FinalizeRun();
                return;
            }

            // 안내는 저장하지 않고 닫음
            var guide = UnityEngine.Object.FindAnyObjectByType<PokerDefense.UI.OnboardingGuide>();
            if (guide != null)
                guide.Close();

            if (flow.IsWaitingForBreak)
            { 
                flow.DismissBreak();
                return;
            }

            if (placement.Pending != null)
            {
                DoPlacement();
                return;
            }

            if (round.Phase == RoundPhase.Exchange)
            {
                DoHandDecision();
                return;
            }

            if (stageCtrl.Stage.Jokers > 0)
            {
                int jokerTarget = BestJokerTarget();

                if (jokerTarget >= 0)
                {
                    placement.TryUseJoker(jokerTarget);
                    return;
                }
            }

            // 전투는 CombatController가 알아서 진행하므로 시작만 함
            if (combat.CanStart)
            {
                combat.StartCombat();
            }
        }

        // 소환된 유닛을 보드에 놓고, 놓을 자리가 없으면 버림
        // 합치기 -> 첫 빈 칸 -> 소환 포기 순으로 처리
        // 교체는 사용하지 않음
        static void DoPlacement()
        {
            GridBoard board = placement.Board;
            UnitInstance pending = placement.Pending;
            int mergeIndex = -1;
            int emptyIndex = -1;

            // 첫 빈 칸은 기억만 하고 합칠 칸을 찾으면 바로 멈춤
            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                if (board[i] == null)
                {
                    if (emptyIndex < 0)
                        emptyIndex = i;


                    continue;
                }

                if (board.CanPlaceAt(i, pending))
                {
                    mergeIndex = i;
                    break;
                }
            }

            if (mergeIndex >= 0)
            {
                placement.TryPlace(mergeIndex);
            }
            else if (emptyIndex >= 0)
            {
                placement.TryPlace(emptyIndex);
            }
            else
            {
                placement.TryDiscardPending();
            }
        }

        // 승급권을 쓸 수 있는 칸 중 DPS가 가장 높은 유닛을 고름
        static int BestJokerTarget()
        {
            GridBoard board = placement.Board;
            int best = -1;
            float bestDps = -1f;

            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                if (placement.CanUseJokerOn(i) == false)
                {
                    continue;
                }

                float dps = board[i].AttackPower * board[i].AttacksPerSecond;

                if (dps > bestDps)
                {
                    bestDps = dps;
                    best = i;
                }
            }

            return best;
        }

        // 받은 손패를 전략에 따라 교체하고 확정해 이번 손패를 결정함
        // 교체 -> 선택 교체 -> 손패 확정 순으로 처리
        // (전략이 no_exchange일 경우 교체 없이 바로 확정함)
        static void DoHandDecision()
        {
            List<int> exchange = strategy == "no_exchange" || cachedHand == null
                ? new List<int>()
                : DecideGreedyExchange(cachedHand);

            if (exchange.Count > 0)
            {
                round.ExchangeCards(exchange);
            }

            if (strategy != "no_exchange" && exchange.Count > 0 && round.RevealCandidates(exchange[0]))
            {
                int best = 0;
                for (int i = 1; i < round.Candidates.Count; i++)
                {
                    if (HandRarity.RankOf(round.PreviewCandidate(i).Category) > HandRarity.RankOf(round.PreviewCandidate(best).Category)) best = i;
                }
                round.ChooseCandidate(best);
            }

            round.ConfirmHand();
        }

        // 다섯 장이 모두 족보를 이루므로 한 장이라도 바꾸면 깨지는 족보
        static readonly HashSet<HandCategory> CompleteHands = new HashSet<HandCategory>
        {
            HandCategory.Straight, HandCategory.BackStraight, HandCategory.Mountain, HandCategory.Flush, HandCategory.FullHouse,
            HandCategory.StraightFlush, HandCategory.BackStraightFlush, HandCategory.RoyalStraightFlush,
        };

        // 확률을 따지지 않고 지금 손패에서 이미 맞은 쪽만 남기고 나머지를 교체할 자리로 고름
        // 같은 숫자가 2장 이상이면 그 묶음을 남기고 나머지를 교체함
        // 페어가 없으면 같은 무늬 3장 이상을, 그것도 없으면 숫자가 높은 2장을 남김
        // 다섯 장이 모두 쓰이는 족보(스트레이트 계열·플러시·풀하우스)가 이미 완성돼 있으면 바꾸지 않음
        static List<int> DecideGreedyExchange(IReadOnlyList<Card> hand)
        {
            if (CompleteHands.Contains(HandEvaluator.CategoryOf(hand)))
            {
                return new List<int>();
            }

            // 같은 숫자끼리 자리 번호를 모음
            Dictionary<Rank, List<int>> byRank = new Dictionary<Rank, List<int>>();

            for (int i = 0; i < hand.Count; i++)
            {
                if (byRank.TryGetValue(hand[i].Rank, out List<int> list) == false)
                {
                    list = new List<int>();
                    byRank[hand[i].Rank] = list;
                }

                list.Add(i);
            }

            // 장수가 많은 묶음부터, 장수가 같으면 숫자가 높은 묶음부터 둠
            List<List<int>> groups = byRank.Values
                .OrderByDescending(g => g.Count)
                .ThenByDescending(g => (int)hand[g[0]].Rank)
                .ToList();

            HashSet<int> keep = new HashSet<int>();

            // 페어 이상이 있으면 가장 큰 묶음을 남김
            if (groups[0].Count >= 2)
            {
                keep.UnionWith(groups[0]);

                // 투페어·풀하우스면 두 번째 묶음도 남김
                if (groups.Count > 1 && groups[1].Count >= 2)
                {
                    keep.UnionWith(groups[1]);
                }
            }
            else
            {
                // 페어가 없으면 같은 무늬끼리 자리 번호를 모음
                Dictionary<Suit, List<int>> bySuit = new Dictionary<Suit, List<int>>();

                for (int i = 0; i < hand.Count; i++)
                {
                    if (bySuit.TryGetValue(hand[i].Suit, out List<int> list) == false)
                    {
                        list = new List<int>();
                        bySuit[hand[i].Suit] = list;
                    }

                    list.Add(i);
                }

                List<int> bestSuit = bySuit.Values.OrderByDescending(g => g.Count).First();

                // 가장 많은 무늬가 3장 이상이면 플러시를 노리고 그 무늬를 남김
                if (bestSuit.Count >= 3)
                {
                    keep.UnionWith(bestSuit);
                }
                else
                {
                    // 노릴 것이 없으면 숫자가 높은 2장만 남김
                    keep.UnionWith(Enumerable.Range(0, hand.Count).OrderByDescending(i => (int)hand[i].Rank).Take(2));
                }
            }

            // 남기지 않은 자리를 교체 목록으로 돌려줌
            return Enumerable.Range(0, hand.Count).Where(i => keep.Contains(i) == false).ToList();
        }

        // 웨이브 전투가 끝날 때마다 CSV에 로그 한 줄을 추가함
        static void LogWaveRow(CombatOutcome outcome)
        {
            StageContext stage = stageCtrl.Stage;
            GridBoard board = placement.Board;
            int star1 = 0, star2 = 0, star3 = 0, occupied = 0;

            // 보드의 유닛 수를 성급별로 셈
            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                UnitInstance u = board[i];
                if (u == null) continue;
                occupied++;
                if (u.Star == 1) star1++;
                else if (u.Star == 2) star2++;
                else star3++;
            }

            // 값 순서가 EnsureHeader의 열 이름 순서와 같아야 하므로 열을 바꾸면 FinalizeRun까지 세 곳을 함께 고침
            string row = string.Join(",", new[]
            {
                runId, strategy, stage.WaveIndex.ToString(), outcome.ToString(),
                stage.Life.ToString(), stage.Jokers.ToString(),
                star1.ToString(), star2.ToString(), star3.ToString(), occupied.ToString(),
                (Time.realtimeSinceStartup - startedRealtime).ToString("F1", CultureInfo.InvariantCulture),
            });

            File.AppendAllText(logPath, row + "\n");
        }

        // 파일이 없을 때만 헤더를 쓰고, 있으면 기존 결과 아래에 이어 씀
        static void EnsureHeader()
        {
            if (File.Exists(logPath))
            {
                return;
            }

            File.WriteAllText(logPath,
                "run_id,strategy,wave,outcome,life,jokers,star1,star2,star3,occupied,wall_sec\n");
        }

        static void FinalizeRun()
        {
            StageContext stage = stageCtrl.Stage;
            string reason = stage.IsGameOver ? "GameOver" : "Cleared";

            // 값 순서가 EnsureHeader의 열 이름 순서와 같아야 하므로 열을 바꾸면 LogWaveRow까지 세 곳을 함께 고침
            // 보드 집계 네 칸은 비워 둠
            string summary = string.Join(",", new[]
            {
                runId, strategy, "FINAL", reason,
                stage.Life.ToString(), stage.Jokers.ToString(),
                "", "", "", "",
                (Time.realtimeSinceStartup - startedRealtime).ToString("F1", CultureInfo.InvariantCulture),
            });

            File.AppendAllText(logPath, summary + "\n");

            string best = stageCtrl.Stats.BestUnit != null ? stageCtrl.Stats.BestUnit.ToString() : "none";
            File.AppendAllText(logPath + ".summary.txt",
                $"{runId},{strategy},{reason},wave={stage.WaveIndex}/{stage.TotalWaves},life={stage.Life},summons={stageCtrl.Stats.Summons},bestHand={stageCtrl.Stats.BestHand},bestUnit={best}\n");

            GameSession.SetSpeed(1f);
            SessionState.SetBool(ActiveKey, false);
            setupDone = false;
            flow = null;

            EditorApplication.isPlaying = false;
        }
    }
}
#endif

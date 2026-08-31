#if UNITY_EDITOR
using System.Collections.Generic;
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
     * 개발/밸런스 검증 전용. 정상적인 포커 드로우(치트 없음)로 스테이지를 끝까지 자동 플레이해
     * 라이프·성급 추이를 CSV로 남긴다. GUI 없이 StartRun 호출(주로 MCP execute_code)로만 제어한다.
     * Time.timeScale로 CombatController.Update를 가속하는 기존 검증 방식(HISTORY 참고)을 그대로 따른다 —
     * combat.Tick을 직접 호출하지 않는다(CombatController와의 순서 어긋남으로 결과 유실 버그가 났던 적이 있음).
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

        // strategy: "no_exchange"(교체 안 함) | "greedy"(같은 숫자/무늬 우선 유지) | "greedy_summon"(greedy + 지원 소환 적극 사용)
        public static void StartRun(string strategyId, string runIdentifier, string outputCsvPath, float timeScale = 10f)
        {
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

            if (setupDone == false && TrySetup() == false)
            {
                return; // 씬이 아직 준비되지 않음, 다음 프레임에 재시도
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
            Time.timeScale = SessionState.GetFloat(TimeScaleKey, 10f);

            cachedHand = null;
            round.HandChanged += h => cachedHand = h;
            combat.CombatFinished += (outcome, unresolved) => LogWaveRow(outcome);

            EnsureHeader();
            startedRealtime = Time.realtimeSinceStartup;
            setupDone = true;
            return true;
        }

        static void Drive()
        {
            if (flow.IsFinished)
            {
                FinalizeRun();
                return;
            }

            // 카드 상점은 지금은 그냥 닫는다 (구매 전략은 Phase 6 재밸런싱에서 붙인다 — DESIGN §13.7)
            if (flow.ShopCards != null)
            {
                flow.CloseShop();
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

            if (strategy == "greedy_summon" && placement.CanSupportSummon)
            {
                placement.TrySupportSummon();
                return;
            }

            if (combat.CanStart)
            {
                combat.StartCombat();
            }
        }

        // 빈 칸보다 머지를 우선한다 (실제 플레이어가 보통 그렇게 하듯)
        static void DoPlacement()
        {
            GridBoard board = placement.Board;
            UnitInstance pending = placement.Pending;
            int mergeIndex = -1;
            int emptyIndex = -1;

            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                if (board[i] == null)
                {
                    if (emptyIndex < 0) emptyIndex = i;
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
                placement.TrySellPending();
            }
        }

        // 조커를 쓸 수 있는 슬롯 중 지금 DPS가 가장 높은 유닛을 고른다 (에이스를 더 키우는 흔한 플레이)
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

        static void DoHandDecision()
        {
            List<int> exchange = strategy == "no_exchange" || cachedHand == null
                ? new List<int>()
                : DecideGreedyExchange(cachedHand);

            if (exchange.Count > 0)
            {
                round.ExchangeCards(exchange);
            }

            round.ConfirmHand();
        }

        // 같은 숫자가 2장 이상 모이면 그 그룹(들)을 남기고 나머지를 교체.
        // 페어가 없으면 무늬 3장 이상(플러시 노림)을, 그것도 없으면 숫자 높은 2장만 남긴다.
        static List<int> DecideGreedyExchange(IReadOnlyList<Card> hand)
        {
            var byRank = new Dictionary<Rank, List<int>>();

            for (int i = 0; i < hand.Count; i++)
            {
                if (byRank.TryGetValue(hand[i].Rank, out List<int> list) == false)
                {
                    list = new List<int>();
                    byRank[hand[i].Rank] = list;
                }

                list.Add(i);
            }

            var groups = byRank.Values
                .OrderByDescending(g => g.Count)
                .ThenByDescending(g => (int)hand[g[0]].Rank)
                .ToList();

            var keep = new HashSet<int>();

            if (groups[0].Count >= 2)
            {
                keep.UnionWith(groups[0]);

                if (groups.Count > 1 && groups[1].Count >= 2)
                {
                    keep.UnionWith(groups[1]);
                }
            }
            else
            {
                var bySuit = new Dictionary<Suit, List<int>>();

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

                if (bestSuit.Count >= 3)
                {
                    keep.UnionWith(bestSuit);
                }
                else
                {
                    keep.UnionWith(Enumerable.Range(0, hand.Count).OrderByDescending(i => (int)hand[i].Rank).Take(2));
                }
            }

            return Enumerable.Range(0, hand.Count).Where(i => keep.Contains(i) == false).ToList();
        }

        static void LogWaveRow(CombatOutcome outcome)
        {
            StageContext stage = stageCtrl.Stage;
            GridBoard board = placement.Board;
            int star1 = 0, star2 = 0, star3 = 0, occupied = 0;

            for (int i = 0; i < GridBoard.SlotCount; i++)
            {
                UnitInstance u = board[i];
                if (u == null) continue;
                occupied++;
                if (u.Star == 1) star1++;
                else if (u.Star == 2) star2++;
                else star3++;
            }

            string row = string.Join(",", new[]
            {
                runId, strategy, stage.WaveIndex.ToString(), outcome.ToString(),
                stage.Life.ToString(), stage.Chip.ToString(), stage.Jokers.ToString(),
                star1.ToString(), star2.ToString(), star3.ToString(), occupied.ToString(),
                (Time.realtimeSinceStartup - startedRealtime).ToString("F1"),
            });

            File.AppendAllText(logPath, row + "\n");
        }

        static void EnsureHeader()
        {
            if (File.Exists(logPath))
            {
                return;
            }

            File.WriteAllText(logPath,
                "run_id,strategy,wave,outcome,life,chip,jokers,star1,star2,star3,occupied,wall_sec\n");
        }

        static void FinalizeRun()
        {
            StageContext stage = stageCtrl.Stage;
            string reason = stage.IsGameOver ? "GameOver" : "Cleared";

            string summary = string.Join(",", new[]
            {
                runId, strategy, "FINAL", reason,
                stage.Life.ToString(), stage.Chip.ToString(), stage.Jokers.ToString(),
                "", "", "", "",
                (Time.realtimeSinceStartup - startedRealtime).ToString("F1"),
            });

            File.AppendAllText(logPath, summary + "\n");

            string best = stageCtrl.Stats.BestUnit != null ? stageCtrl.Stats.BestUnit.ToString() : "none";
            File.AppendAllText(logPath + ".summary.txt",
                $"{runId},{strategy},{reason},wave={stage.WaveIndex}/{stage.TotalWaves},life={stage.Life},summons={stageCtrl.Stats.Summons},bestHand={stageCtrl.Stats.BestHand},bestUnit={best},chipSpent={stageCtrl.Stats.ChipSpent}\n");

            Time.timeScale = 1f;
            SessionState.SetBool(ActiveKey, false);
            setupDone = false;
            flow = null;

            EditorApplication.isPlaying = false;
        }
    }
}
#endif

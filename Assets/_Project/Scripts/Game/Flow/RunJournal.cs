using System;
using System.Collections.Generic;
using System.IO;
using PokerDefense.Poker;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * RunJournal
     *
     * 사람 플레이 비교용 로컬 기록. 게임 규칙이나 난수는 변경하지 않는다.
     * 자동 비교와 사람 플레이 결과를 혼동하지 않도록 원시 이벤트와 단계별 시간을 남긴다.
     */
    public sealed class RunJournal : MonoBehaviour
    {
        [Serializable]
        sealed class Entry
        {
            public string action;
            public int wave;
            public float seconds;
            public int seed;
            public string hand;
            public int exchanges;
            public bool assisted;
            public int life;
        }

        [Serializable]
        sealed class Journal
        {
            public string startedUtc;
            public string mode;
            public string ruleset = "hand-loop-v1";
            public float trackLength;
            public float exchangeSeconds;
            public float placementSeconds;
            public float combatSeconds;
            public float breakSeconds;
            public List<Entry> entries = new List<Entry>();
        }

        [SerializeField] RoundController round;
        [SerializeField] StageController stage;
        [SerializeField] CombatController combat;
        [SerializeField] GameFlowController flow;
        [SerializeField] PlacementController placement;

        Journal journal;
        float elapsed;
        string fileName;

        void Awake()
        {
            journal = new Journal { startedUtc = DateTime.UtcNow.ToString("O"), mode = round.Assistance.ToString(), trackLength = GridBoard.TrackLength };
            fileName = "run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json";
            round.Evaluated += OnEvaluated;
            round.AssistanceChanged += OnAssistance;
            flow.FlowChanged += OnFlow;
            combat.CombatStarted += OnCombat;
            combat.CombatFinished += OnFinished;
            placement.Placed += OnPlaced;
        }

        void Update()
        {
            if (flow.IsFinished || GameSession.IsPaused)
            {
                return;
            }
            float delta = Time.deltaTime;
            elapsed += delta;
            if (flow.IsWaitingForBreak)
            {
                journal.breakSeconds += delta;
            }
            else if (combat.IsFighting)
            {
                journal.combatSeconds += delta;
            }
            else if (round.Phase == RoundPhase.Exchange)
            {
                journal.exchangeSeconds += delta;
            }
            else journal.placementSeconds += delta;
        }

        void OnEvaluated(HandResult result) => Record("hand:" + result.Category);
        void OnAssistance() => Record(round.IsChoosingCandidate ? "assist:reveal" : "assist:choose");
        void OnCombat(CombatContext context) => Record("combat:start");
        void OnFinished(CombatOutcome outcome, int unresolved) => Record("combat:" + outcome);
        void OnPlaced(int index, PlacementResult result) => Record("board:" + result);
        void OnFlow()
        {
            Record(flow.IsFinished ? "run:finished" : "round:start");
            if (flow.IsFinished)
            {
                Save();
            }
        }

        void Record(string action)
        {
            string hand = "";
            if (round.Phase != RoundPhase.Draw)
                foreach (Card card in round.Hand)
                {
                    hand += card + " ";
                }
            journal.entries.Add(new Entry { action = action, wave = flow.RoundNumber, seconds = elapsed,
                seed = round.LastSeed, hand = hand.Trim(), exchanges = round.UsedExchanges, assisted = round.AssistUsed,
                life = stage.Stage.Life });
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
            }
        }

        void OnDestroy()
        {
            round.Evaluated -= OnEvaluated;
            round.AssistanceChanged -= OnAssistance;
            flow.FlowChanged -= OnFlow;
            combat.CombatStarted -= OnCombat;
            combat.CombatFinished -= OnFinished;
            placement.Placed -= OnPlaced;
            Save();
        }

        void Save()
        {
            if (journal == null || journal.entries.Count == 0)
            {
                return;
            }
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "RunJournals");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, fileName), JsonUtility.ToJson(journal, true));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("플레이 비교 기록을 저장하지 못했습니다: " + exception.Message);
            }
        }
    }
}

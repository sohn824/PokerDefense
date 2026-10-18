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
     * 사람이 플레이한 한 Run의 행동과 단계별 소요 시간을 JSON으로 남기는 분석용 로그
     * 게임 이벤트를 받아 기록만 하고 게임 진행에는 관여하지 않음
     * 분석용 기록이라 이 파일로 Run을 이어서 할 수는 없음
     *
     * 저장 위치 - persistentDataPath/RunJournals (Run마다 파일 하나)
     * 저장 시점 - Run 종료, 앱이 백그라운드로 갈 때, 씬을 떠날 때(재시작·메뉴로 나가기·앱 종료)
     * 저장할 때마다 같은 파일을 덮어씀
     */
    public sealed class RunJournal : MonoBehaviour
    {
        // 이벤트 1건과 그 순간의 상태
        [Serializable]
        sealed class Entry
        {
            // 무슨 일이 있었는지 ("종류:세부" 형식)
            // hand:족보, assist:reveal, assist:choose
            // combat:start, combat:전투 결과(Cleared·TimedOut)
            // board:배치 결과(PlacementResult), board:보드 조작(BoardAction)
            // round:start, run:finished
            public string action;

            // 라운드 번호 (GameFlowController.RoundNumber)
            public int wave;

            // Run 시작부터 지난 시간(초), 일시정지 시간은 빠짐
            public float seconds;

            // 이번 라운드 덱 셔플 시드
            public int seed;

            // 그 순간의 손패 (Draw 단계면 빈 문자열)
            public string hand;

            // 이번 라운드에 교체한 장수
            public int exchanges;

            // 이번 라운드에 선택 교체를 썼는지
            public bool assisted;

            public int life;
        }

        // 파일 하나에 저장되는 한 Run의 기록
        [Serializable]
        sealed class Journal
        {
            public string startedUtc;

            // 규칙 식별자 (다른 규칙의 로그와 섞지 않기 위함)
            public string ruleset = "hand-loop-v1";

            // 트랙 길이 (트랙이 바뀐 버전의 로그를 구분하기 위함)
            public float trackLength;

            // 단계별 누적 시간(초), 일시정지 시간은 빠짐
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

        // 승급권 사용 직후 Merged 인자로 오는 Placed를 건너뛸지 (board:JokerUsed와 중복 기록 방지)
        bool skipJokerMerge;

        void Awake()
        {
            journal = new Journal { startedUtc = DateTime.UtcNow.ToString("O"), trackLength = GridBoard.TrackLength };
            fileName = "run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json";
            round.Evaluated += OnEvaluated;
            round.AssistanceChanged += OnAssistance;
            flow.FlowChanged += OnFlow;
            combat.CombatStarted += OnCombat;
            combat.CombatFinished += OnFinished;
            placement.Placed += OnPlaced;
            placement.BoardActed += OnBoardActed;
        }

        // 일시정지 중이나 Run 종료 뒤가 아니면 프레임 시간을 지금 단계에 더함
        // 우선순위 - 결과 화면 대기 > 전투 > 교체 > 그 밖(배치)
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
        // 후보 공개 직후에는 IsChoosingCandidate가 true라 reveal, 후보를 고른 뒤에는 false라 choose
        void OnAssistance() => Record(round.IsChoosingCandidate ? "assist:reveal" : "assist:choose");
        void OnCombat(CombatContext context) => Record("combat:start");
        void OnFinished(CombatOutcome outcome, int unresolved) => Record("combat:" + outcome);
        void OnPlaced(int index, PlacementResult result)
        {
            if (skipJokerMerge)
            {
                skipJokerMerge = false;
                return;
            }
            Record("board:" + result);
        }
        void OnBoardActed(BoardAction action)
        {
            skipJokerMerge = action == BoardAction.JokerUsed;
            Record("board:" + action);
        }
        void OnFlow()
        {
            Record(flow.IsFinished ? "run:finished" : "round:start");
            if (flow.IsFinished)
            {
                Save();
            }
        }

        // action과 그 순간의 상태를 Entry 한 건으로 남김
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
            placement.BoardActed -= OnBoardActed;
            Save();
        }

        // 기록을 JSON 파일로 씀 (기록이 없으면 건너뜀, 저장 실패는 경고만 남김)
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
                Debug.LogWarning("플레이 기록을 저장하지 못함: " + exception.Message);
            }
        }
    }
}

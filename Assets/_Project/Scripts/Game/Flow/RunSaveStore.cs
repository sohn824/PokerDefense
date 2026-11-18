using System;
using System.IO;
using UnityEngine;

namespace PokerDefense.Game
{
    /**
     * RunSaveLoadResult
     *
     * RunSaveStore.TryLoad의 결과
     * None - 저장이 없음
     * Loaded - 읽음
     * Invalid - 저장은 있지만 깨졌거나 형식 버전·규칙이 달라 쓸 수 없음 (파일은 .bad로 옮김)
     */
    public enum RunSaveLoadResult
    {
        None,
        Loaded,
        Invalid,
    }

    /**
     * RunSaveStore
     *
     * 이어하기 저장 파일 하나(current.json)를 읽고 쓰고 지우는 클래스
     * Save()는 .tmp에 다 쓴 뒤 바꿔 넣으므로, 쓰는 도중 꺼져도 직전 저장이 남고 쓰다 만 파일을 읽는 일은 없음
     * 쓸 수 없는 저장은 .bad로 옮겨 다음 실행에 다시 읽지 않게 함 (원인을 볼 수 있게 지우지는 않음)
     */
    public sealed class RunSaveStore
    {
        const string FileName = "current.json";
        const string TempFileName = "current.tmp";
        const string BadFileName = "current.bad";

        readonly string directory;

        public RunSaveStore(string directory)
        {
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        string FilePath => Path.Combine(directory, FileName);

        public bool Exists => File.Exists(FilePath);

        public void Save(RunSaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Directory.CreateDirectory(directory);
            string temp = Path.Combine(directory, TempFileName);
            File.WriteAllText(temp, JsonUtility.ToJson(data));

            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, null);
            }
            else
            {
                File.Move(temp, FilePath);
            }
        }

        public RunSaveLoadResult TryLoad(out RunSaveData data)
        {
            data = null;

            if (Exists == false)
            {
                return RunSaveLoadResult.None;
            }

            RunSaveData read = null;

            try
            {
                read = JsonUtility.FromJson<RunSaveData>(File.ReadAllText(FilePath));
            }
            catch (ArgumentException)
            {
                // JSON 형식이 아니면 JsonUtility가 ArgumentException을 던짐
            }

            if (IsUsable(read) == false)
            {
                MoveAside();
                return RunSaveLoadResult.Invalid;
            }

            data = read;
            return RunSaveLoadResult.Loaded;
        }

        public void Delete()
        {
            if (Exists)
            {
                File.Delete(FilePath);
            }
        }

        // 형식 버전·규칙이 지금과 같고 보드 배열 길이가 맞는지
        // 값 범위(라이프·성급 등)와 행동 기록은 복원하면서 Restore·Replay가 확인함
        static bool IsUsable(RunSaveData data)
        {
            return data != null
                && data.version == RunSaveData.FormatVersion
                && data.ruleset == RunResult.CurrentRuleset
                && data.boardUnitIds != null && data.boardUnitIds.Length == GridBoard.SlotCount
                && data.boardStars != null && data.boardStars.Length == GridBoard.SlotCount
                && data.roundActions != null;
        }

        void MoveAside()
        {
            string bad = Path.Combine(directory, BadFileName);

            if (File.Exists(bad))
            {
                File.Delete(bad);
            }

            File.Move(FilePath, bad);
        }
    }
}

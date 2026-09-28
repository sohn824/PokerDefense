using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PokerDefense.Game
{
    /**
     * RunOutbox
     *
     * 보낼 결과를 파일로 보관하는 대기열 - ACK를 받아야 지우므로 꺼져도 다음 실행 때 다시 보냄
     *
     * 파일 하나 = 기록 하나 (이름은 runId, 내용은 제출 본문 바이트)
     * .tmp에 다 쓴 뒤 이름을 바꿔 넣어 쓰다 만 파일이 섞이지 않음
     * 깨진 파일은 .bad, 거부된 기록은 .rejected로 옮겨 다시 보내지 않음
     */
    public sealed class RunOutbox
    {
        const string PendingExtension = ".run";        // 보낼 기록
        const string TempExtension = ".tmp";           // 쓰는 중 (무시)
        const string BadExtension = ".bad";            // 읽을 수 없는 파일
        const string RejectedExtension = ".rejected";  // 서버가 거부한 기록

        readonly string directory;

        public RunOutbox(string directory)
        {
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        // Save - 대기열에 추가 (같은 runId면 그대로)
        public void Save(RunResult result)
        {
            Directory.CreateDirectory(directory);
            string path = PathOf(result.RunId, PendingExtension);
            if (File.Exists(path))
            {
                return;
            }

            // 임시 파일에 쓴 뒤 이름만 바꿈 - .run은 항상 완성본
            string temp = PathOf(result.RunId, TempExtension);
            File.WriteAllBytes(temp, RunProtocol.EncodeSubmit(result));
            File.Move(temp, path);
        }

        // LoadPending - 보낼 기록을 오래된 순으로 읽음
        public List<RunResult> LoadPending()
        {
            List<RunResult> results = new List<RunResult>();
            if (Directory.Exists(directory) == false)
            {
                return results;
            }

            IEnumerable<FileInfo> files = new DirectoryInfo(directory).GetFiles("*" + PendingExtension)
                .OrderBy(file => file.LastWriteTimeUtc);
            foreach (FileInfo file in files)
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(file.FullName);
                }
                catch (IOException)
                {
                    // 막 옮겨지거나 지워진 파일 - 다음에 다시 봄
                    continue;
                }

                // 깨졌거나 이름과 runId가 다르면 치움
                if (RunProtocol.TryDecodeSubmit(bytes, out RunResult result) && FileNameMatches(file, result.RunId))
                {
                    results.Add(result);
                }
                else
                {
                    MoveAside(file.FullName, Path.ChangeExtension(file.FullName, BadExtension));
                }
            }

            return results;
        }

        // Remove - 서버에 저장된 기록 삭제
        public void Remove(Guid runId)
        {
            string path = PathOf(runId, PendingExtension);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        // MarkRejected - 거부된 기록을 대기열에서 빼고 남겨 둠
        public void MarkRejected(Guid runId)
        {
            MoveAside(PathOf(runId, PendingExtension), PathOf(runId, RejectedExtension));
        }

        // 파일 이름은 runId - 같은 기록이 두 파일로 나뉘지 않음
        string PathOf(Guid runId, string extension)
        {
            return Path.Combine(directory, runId.ToString("N") + extension);
        }

        static bool FileNameMatches(FileInfo file, Guid runId)
        {
            return Path.GetFileNameWithoutExtension(file.Name) == runId.ToString("N");
        }

        // MoveAside - 확장자를 바꿔 대기열에서 뺌 (같은 이름이면 덮어씀)
        static void MoveAside(string from, string to)
        {
            if (File.Exists(from) == false)
            {
                return;
            }

            if (File.Exists(to))
            {
                File.Delete(to);
            }

            File.Move(from, to);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PokerDefense.Game
{
    /**
     * RunOutbox - 서버에 보낼 결과를 파일로 보관하는 대기열
     *
     * ACK를 받기 전에는 지우지 않으므로, 게임이 꺼져도 기록이 남아 다음 판 종료나 다음 실행 때 다시 보낼 수 있음
     *
     * 파일 하나 = 기록 하나 (이름은 runId, 내용은 제출 본문 바이트)
     * 메서드마다 부르는 스레드가 다름 (모두 RunReporter가 부름)
     *   Save() - 메인 스레드 (게임이 끝났을 때)
     *   LoadPending()·Remove()·MarkRejected() - 워커 스레드 (전송 루프 안에서)
     *   (Save()가 .tmp에 다 쓴 뒤 이름을 .run으로 바꾸므로, LoadPending()이 쓰다 만 파일을 읽는 일은 없음)
     * 깨진 파일은 .bad, 거부된 기록은 .rejected로 확장자를 바꿔 대기열에서 뺌 (다시 보내지 않음)
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

        // Save - 대기열에 추가 (같은 runId 파일이 이미 있으면 아무것도 안 함)
        public void Save(RunResult result)
        {
            Directory.CreateDirectory(directory);
            string path = PathOf(result.RunId, PendingExtension);
            if (File.Exists(path))
            {
                return;
            }

            // 임시 파일에 쓴 뒤 이름만 바꿈 - 확장자 .run은 항상 완성본
            string temp = PathOf(result.RunId, TempExtension);
            File.WriteAllBytes(temp, RunProtocol.EncodeSubmit(result));
            File.Move(temp, path);
        }

        // LoadPending - 보낼 기록을 오래된 순으로 읽음 (읽을 수 없는 파일은 이때 확장자를 .bad로 바꿈)
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
                    // 막 옮겨지거나 지워진 파일은 다음에 다시 봄
                    continue;
                }

                // 깨졌거나 이름과 runId가 다르면 확장자를 .bad로 바꿔 대기열에서 뺌
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

        // MarkRejected - 확장자를 .rejected로 바꿔 대기열에서 뺌 (파일은 남김)
        public void MarkRejected(Guid runId)
        {
            MoveAside(PathOf(runId, PendingExtension), PathOf(runId, RejectedExtension));
        }

        // PathOf - runId로 대기열 파일 경로를 만듦
        //   (이름이 runId 하나로 정해지므로 Remove·MarkRejected가 runId만으로 파일을 찾을 수 있고,
        //    같은 기록을 두 번 Save해도 파일은 하나뿐임)
        //
        // 주의: Guid.ToString("N")은 앞 8바이트를 4·2·2바이트 묶음별로 뒤집어 써서, 서버 로그의 runId(ToHex)와 글자가 다름
        //   예: 바이트 00 01 02 03 04 05 06 07 … -> 파일 이름 "0302010005040706…", 서버 로그 "0001020304050607…"
        string PathOf(Guid runId, string extension)
        {
            return Path.Combine(directory, runId.ToString("N") + extension);
        }

        // 파일 이름과 내용의 runId가 같은지 확인
        // (다르면 Remove·MarkRejected가 runId로 파일을 찾지 못해, 같은 기록을 끝없이 다시 보내게 됨)
        static bool FileNameMatches(FileInfo file, Guid runId)
        {
            return Path.GetFileNameWithoutExtension(file.Name) == runId.ToString("N");
        }

        // MoveAside - from 파일을 to 경로로 옮김
        // (from이 없으면 아무것도 안 함, to가 이미 있으면 지우고 덮어씀)
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

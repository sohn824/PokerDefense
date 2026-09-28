using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PokerDefense.Game;

namespace PokerDefense.Tests
{
    public class RunSubmitterTests
    {
        /**
         * 테스트 안에서 띄우는 가짜 서버. 연결 하나를 받아 프레임 하나를 읽고, respond가 정한 대로 응답함
         * 실제 C++ 서버 없이 정상 응답·끊김·지연·조각난 응답을 마음대로 만들기 위해 씀
         */
        sealed class FakeServer : IDisposable
        {
            readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            readonly Task serving;

            // respond: 받은 본문 → 보낼 바이트. null이면 아무것도 보내지 않고 끊음
            public FakeServer(Func<byte[], byte[]> respond, int delayMs = 0, bool dribble = false)
            {
                listener.Start();
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                serving = Task.Run(() => Serve(respond, delayMs, dribble));
            }

            public int Port { get; }
            public byte[] Received { get; private set; }

            void Serve(Func<byte[], byte[]> respond, int delayMs, bool dribble)
            {
                try
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    {
                        var reader = new RunFrameReader();
                        var buffer = new byte[256];
                        byte[] payload;
                        while (reader.Next(out payload) == RunFrameReader.Result.NeedMore)
                        {
                            int count = stream.Read(buffer, 0, buffer.Length);
                            if (count == 0)
                            {
                                return;
                            }

                            reader.Append(buffer, count);
                        }

                        Received = payload;
                        Thread.Sleep(delayMs);
                        var response = respond(payload);
                        if (response == null)
                        {
                            return;
                        }

                        if (dribble)
                        {
                            // 한 바이트씩 보내 클라이언트가 조각을 이어 붙이는지 확인
                            foreach (var b in response)
                            {
                                stream.Write(new[] { b }, 0, 1);
                                stream.Flush();
                                Thread.Sleep(2);
                            }
                        }
                        else
                        {
                            stream.Write(response, 0, response.Length);
                        }
                    }
                }
                catch (Exception error) when (error is SocketException || error is System.IO.IOException
                    || error is ObjectDisposedException || error is InvalidOperationException)
                {
                    // 클라이언트가 시간 초과로 먼저 끊은 경우 등
                }
            }

            public void Dispose()
            {
                listener.Stop();
                serving.Wait(3000);
            }
        }

        static byte[] AckFor(byte[] payload, RunAckStatus status, int rank, int total, Guid? overrideId = null)
        {
            RunProtocol.TryDecodeSubmit(payload, out var result);
            var ack = new RunAck(overrideId ?? result.RunId, status, 0, rank, total);
            return RunProtocol.Frame(RunProtocol.EncodeAck(ack));
        }

        // 메인 스레드를 막고 기다려도 되도록 스레드 풀에서 돌림
        static SubmitOutcome Submit(int port, int timeoutMs = 3000)
        {
            var submitter = new RunSubmitter("127.0.0.1", port, TimeSpan.FromMilliseconds(timeoutMs));
            var task = Task.Run(() => submitter.SubmitAsync(RunProtocolTests.Sample(Guid.NewGuid()), CancellationToken.None));
            Assert.IsTrue(task.Wait(10000), "전송 작업이 끝나지 않음");
            return task.Result;
        }

        [Test]
        public void 서버가_받으면_순위를_돌려준다()
        {
            using (var server = new FakeServer(payload => AckFor(payload, RunAckStatus.Accepted, 3, 12)))
            {
                var outcome = Submit(server.Port);
                Assert.AreEqual(SubmitStatus.Accepted, outcome.Status);
                Assert.IsTrue(outcome.IsStored);
                Assert.AreEqual(3, outcome.Rank);
                Assert.AreEqual(12, outcome.Total);
                Assert.IsTrue(RunProtocol.TryDecodeSubmit(server.Received, out _));
            }
        }

        [Test]
        public void 중복_응답도_저장된_것으로_본다()
        {
            using (var server = new FakeServer(payload => AckFor(payload, RunAckStatus.Duplicate, 1, 5)))
            {
                var outcome = Submit(server.Port);
                Assert.AreEqual(SubmitStatus.Duplicate, outcome.Status);
                Assert.IsTrue(outcome.IsStored);
            }
        }

        [Test]
        public void 응답을_조각내_보내도_읽는다()
        {
            using (var server = new FakeServer(payload => AckFor(payload, RunAckStatus.Accepted, 2, 2), dribble: true))
            {
                Assert.AreEqual(SubmitStatus.Accepted, Submit(server.Port).Status);
            }
        }

        [Test]
        public void 다른_기록의_ACK는_실패로_본다()
        {
            using (var server = new FakeServer(payload => AckFor(payload, RunAckStatus.Accepted, 1, 1, Guid.NewGuid())))
            {
                var outcome = Submit(server.Port);
                Assert.AreEqual(SubmitStatus.Failed, outcome.Status);
                Assert.IsFalse(outcome.IsStored);
            }
        }

        [Test]
        public void 응답_전에_끊기면_실패로_본다()
        {
            using (var server = new FakeServer(payload => null))
            {
                Assert.AreEqual(SubmitStatus.Failed, Submit(server.Port).Status);
            }
        }

        [Test]
        public void 응답이_늦으면_시간_초과로_실패한다()
        {
            using (var server = new FakeServer(payload => AckFor(payload, RunAckStatus.Accepted, 1, 1), delayMs: 2000))
            {
                var watch = Stopwatch.StartNew();
                var outcome = Submit(server.Port, timeoutMs: 300);
                Assert.AreEqual(SubmitStatus.Failed, outcome.Status);
                Assert.Less(watch.ElapsedMilliseconds, 1500, "시간 제한이 지나면 응답을 기다리지 않아야 함");
            }
        }

        [Test]
        public void 서버가_없으면_실패한다()
        {
            // 빈 포트를 하나 받아 바로 닫아서, 아무도 듣지 않는 포트를 만듦
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            Assert.AreEqual(SubmitStatus.Failed, Submit(port).Status);
        }
    }
}

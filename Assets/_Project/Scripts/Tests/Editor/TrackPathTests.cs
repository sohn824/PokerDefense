using NUnit.Framework;
using PokerDefense.Game;
using UnityEngine;

namespace PokerDefense.Tests
{
    /**
     * TrackPathTests
     *
     * 적 이동 경로에서 같은 시간에 같은 거리를 가고, 한 바퀴의 끝과 시작이 끊기지 않는지 검사하는 클래스
     */
    public class TrackPathTests
    {
        // 2000등분한 progress 간격마다 이동 거리가 같고 진행 방향(MoveDirection) 길이가 1인지 확인
        // progress 0과 1이 같은 점이고, 0 바로 앞뒤의 위치·방향이 끊기지 않는지도 확인
        [Test]
        public void 경로는_일정한_속도로_끊김_없이_한_바퀴를_돈다()
        {
            TrackPath path = TrackPath.Arena;
            const int samples = 2000;
            float expected = path.Length / samples;
            for (int i = 0; i < samples; i++)
            {
                float distance = Vector2.Distance(path.Position(i / (float)samples), path.Position((i + 1f) / samples));
                Assert.AreEqual(expected, distance, .00003f);
                Assert.AreEqual(1f, path.MoveDirection(i / (float)samples).magnitude, .00001f);
            }

            Assert.AreEqual(path.Position(0), path.Position(1));
            Assert.Less(Vector2.Distance(path.Position(-.00001f), path.Position(.00001f)), .001f);
            Assert.Greater(Vector2.Dot(path.MoveDirection(-.00001f), path.MoveDirection(.00001f)), .999f);
        }
    }
}

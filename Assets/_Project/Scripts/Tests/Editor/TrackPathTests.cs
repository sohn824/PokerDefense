using NUnit.Framework;
using PokerDefense.Game;
using UnityEngine;

namespace PokerDefense.Tests
{
    public class TrackPathTests
    {
        [Test]
        public void 같은_시간의_이동_거리는_직선과_코너에서_일정하다()
        {
            TrackPath path = TrackPath.Arena;
            const int samples = 2000;
            float expected = path.Length / samples;
            for (int i = 0; i < samples; i++)
            {
                float distance = Vector2.Distance(path.Position(i / (float)samples), path.Position((i + 1f) / samples));
                Assert.AreEqual(expected, distance, .00003f);
                Assert.AreEqual(1f, path.Tangent(i / (float)samples).magnitude, .00001f);
            }
        }

        [Test]
        public void 루프_이음부에서_위치와_접선이_이어진다()
        {
            TrackPath path = TrackPath.Arena;
            Assert.AreEqual(path.Position(0), path.Position(1));
            Assert.Less(Vector2.Distance(path.Position(-.00001f), path.Position(.00001f)), .001f);
            Assert.Greater(Vector2.Dot(path.Tangent(-.00001f), path.Tangent(.00001f)), .999f);
        }
    }
}

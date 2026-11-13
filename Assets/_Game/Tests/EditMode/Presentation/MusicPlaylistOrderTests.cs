using System.Collections.Generic;
using DontTrustYourLuck.Presentation.Audio;
using NUnit.Framework;

namespace DontTrustYourLuck.Tests.Presentation
{
    public sealed class MusicPlaylistOrderTests
    {
        [Test]
        public void EmptyPlaylist_ReturnsMinusOne()
        {
            var order = new MusicPlaylistOrder(0, 1);
            Assert.AreEqual(-1, order.Next());
        }

        [Test]
        public void SingleTrack_AlwaysReturnsIt()
        {
            var order = new MusicPlaylistOrder(1, 7);
            for (int i = 0; i < 5; i++) Assert.AreEqual(0, order.Next());
        }

        [Test]
        public void EachRound_PlaysEveryTrackOnce()
        {
            var order = new MusicPlaylistOrder(4, 42);
            for (int round = 0; round < 10; round++)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < 4; i++) Assert.IsTrue(seen.Add(order.Next()), $"repeat inside round {round}");
                Assert.AreEqual(4, seen.Count);
            }
        }

        [Test]
        public void NeverRepeatsTheSameTrackBackToBack([Values(2, 3, 4)] int count)
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var order = new MusicPlaylistOrder(count, seed);
                int last = order.Next();
                for (int i = 0; i < count * 8; i++)
                {
                    int next = order.Next();
                    Assert.AreNotEqual(last, next, $"seed {seed}, step {i}");
                    last = next;
                }
            }
        }

        [Test]
        public void SameSeed_GivesSameOrder()
        {
            var a = new MusicPlaylistOrder(4, 99);
            var b = new MusicPlaylistOrder(4, 99);
            for (int i = 0; i < 12; i++) Assert.AreEqual(a.Next(), b.Next());
        }
    }
}

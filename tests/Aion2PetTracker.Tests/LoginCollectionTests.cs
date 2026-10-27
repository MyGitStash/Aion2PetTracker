using System.Net;
using Aion2PetTracker.Core.Capture;
using Aion2PetTracker.Core.Protocol;

namespace Aion2PetTracker.Tests;

public class LoginCollectionTests
{
    [Theory]
    [InlineData(13328, true)]
    [InlineData(13329, false)]
    public void GameLoginRetainsEarlyCollectionAcrossLargeBurstUntilHeartbeatConfirmation(int port, bool retained)
    {
        var tracker = new StreamTracker { LockThreshold = 3 };
        bool collection = false;
        tracker.OnFrame = (GameFlow f, in FrameMeta m, ReadOnlySpan<byte> body) => { if (m.Opcode == 0x0090) collection = true; };
        var key = new FlowKey(IPAddress.Parse("127.0.0.2"), (ushort)port, IPAddress.Loopback, 50000);
        uint seq = 1000;
        tracker.OnSegment(10, key, seq - 1, true, false, []);
        void Feed(byte[] bytes, long t)
        {
            tracker.OnSegment(t, key, seq, false, false, bytes);
            seq += (uint)bytes.Length;
        }
        Feed([12, 0, 0x90, 0, 0, 0, 0, 0, 0], 11); // complete empty collection at the beginning
        for (int i = 0; i < 16; i++)
            Feed(new byte[65536], 12 + i);
        for (int i = 0; i < 3; i++)
            Feed([14, 0, 0x36, 0, 0, 0, 0, 0, 0, 0, 0], 50 + i);
        Assert.Single(tracker.GameFlows);
        Assert.Equal(retained, collection);
        Assert.Equal(retained, tracker.GameFlows[0].Aligned);
    }
}
